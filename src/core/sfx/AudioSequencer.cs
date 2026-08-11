namespace mono8.core.sfx;

/// <summary>
/// Everything the console does with sound that is not making a sample: channel allocation and
/// stealing, the sfx() call's stop/offset/length semantics, and the music pattern sequencer.
/// <para>
/// A music pattern is only ever four SFX started together — pattern length comes from
/// <see cref="SfxData"/>'s speed and loop bounds, and the advance is timed off
/// <see cref="IAudioChannel.SamplesPlayed"/>. None of it reads a sample, which is why the live
/// synthesiser (<see cref="SfxEngine"/>) and the baked bank (<see cref="AudioPlayer"/>) can share
/// all of it and behave identically.
/// </para>
/// </summary>
internal abstract class AudioSequencer : IDisposable
{
    private const int NumChannels = AudioFormat.NumChannels;

    // ── State ────────────────────────────────────────────────────────────────
    // Set by the derived constructor, after base(): the base does nothing with it, so there is no
    // virtual call during construction.
    protected IAudioChannel[] Channels;

    // Protected because the live channels read it too, to resolve custom instruments.
    protected readonly Dictionary<int, SfxData> SfxBank = new();
    private readonly Dictionary<int, MusicData> _musicBank = new();
    private readonly object _lock = new();

    // ── Music playback state ─────────────────────────────────────────────────
    private int _currentMusicPattern = -1;
    private int _musicLoopStart = -1;
    private int _musicChannelMask = 0;
    private readonly bool[] _musicChannelActive = new bool[NumChannels];

    // Length of the current pattern in samples (its longest channel's single pass).
    // Patterns advance on elapsed time, so a pattern of looping SFX still moves on.
    private long _patternLengthSamples;

    // ── SFX bank ─────────────────────────────────────────────────────────────

    /// <summary>Register a parsed SFX so the engine can play it by index.</summary>
    public void SetSfx(int index, SfxData data)
    {
        if (index >= 0 && index < Constants.GameDataSizes.Sfx)
        {
            lock (_lock) SfxBank[index] = data;
        }
    }

    /// <summary>
    /// Note index a channel is currently playing for <paramref name="sfxIndex"/>, or -1 if it
    /// isn't playing. Sample-accurate, so an editor playhead stays correct at any FPS or SFX speed.
    /// </summary>
    public int CurrentNote(int sfxIndex)
    {
        lock (_lock)
        {
            foreach (var ch in Channels)
                if (ch.IsPlaying && ch.CurrentSfxIndex == sfxIndex)
                    return ch.CurrentNoteIndex;
        }
        return -1;
    }

    /// <summary>Replace a single music pattern in the bank (used by the Music editor after an edit).</summary>
    public void SetMusic(int index, MusicData data)
    {
        if (index >= 0 && index < Constants.GameDataSizes.Music)
            lock (_lock) _musicBank[index] = data;
    }

    /// <summary>Pattern the engine is currently playing (-1 if none); drives the Music editor's playing indicator.</summary>
    public int CurrentMusicPattern
    {
        get { lock (_lock) return _currentMusicPattern; }
    }

    /// <summary>Point the sequencer at the pre-rendered bank, if it plays one. A no-op for live synthesis.</summary>
    public virtual void LoadBank(string folderPath) { }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Play SFX <paramref name="sfxIndex"/> exactly like PICO-8's sfx() call.
    /// </summary>
    /// <param name="sfxIndex">
    ///   0–Max  = play that SFX.<br/>
    ///   -1    = stop all channels.<br/>
    ///   -2    = stop <paramref name="channel"/> only (extension).
    /// </param>
    /// <param name="channel">
    ///   -1  = auto-assign (default).<br/>
    ///   0–3 = explicit channel.
    /// </param>
    /// <param name="offset">Note index to start from (0–31).</param>
    /// <param name="length">Number of notes to play, or -1 for full SFX.</param>
    public void Sfx(int sfxIndex, int channel = -1, int offset = 0, int length = -1)
    {
        lock (_lock)
        {
            // sfx(-1) → stop everything
            if (sfxIndex == -1)
            {
                foreach (var ch in Channels) ch.Stop();
                return;
            }

            // sfx(-2, channel) → stop a specific channel
            if (sfxIndex == -2)
            {
                int target = channel < 0 ? 0 : Math.Clamp(channel, 0, NumChannels - 1);
                Channels[target].Stop();
                return;
            }

            // sfx(n, -1) → stop any channel currently playing sfx n
            if (channel == -1 && sfxIndex >= 0)
            {
                foreach (var ch in Channels)
                {
                    if (ch.CurrentSfxIndex == sfxIndex)
                    {
                        ch.Stop();
                    }
                }

                channel = FindFreeChannel();
            }
            else if (channel < 0)
            {
                channel = FindFreeChannel();
            }
            else
            {
                channel = Math.Clamp(channel, 0, NumChannels - 1);
            }

            if (!SfxBank.TryGetValue(sfxIndex, out var sfxData))
                return;

            int clampedOffset = Math.Clamp(offset, 0, sfxData.Notes.Count - 1);
            int clampedLength = length < 0
                ? sfxData.Notes.Count - clampedOffset
                : Math.Min(length, sfxData.Notes.Count - clampedOffset);

            if (channel >= 0 && channel < NumChannels)
            {
                Channels[channel].Play(sfxIndex, sfxData, clampedOffset, clampedLength);
            }
        }
    }

    public void Music(int musicId, int fadeLength = 0, int channelMask = 0)
    {
        lock (_lock)
        {
            if (musicId < 0)
            {
                StopMusic();
                return;
            }

            _musicChannelMask = channelMask;
            // The loop-back target is the nearest loop-start at or before the pattern we're
            // starting on, not just whichever loop-start pattern happens to have been played —
            // otherwise starting playback mid-sequence (e.g. at the loop-end pattern itself)
            // never learns where to loop back to and just keeps counting forward.
            _musicLoopStart = FindLoopStart(musicId);
            StartMusicPattern(musicId);
        }
    }

    public void UpdateMusic()
    {
        lock (_lock)
        {
            if (_currentMusicPattern < 0) return;

            // Advance once the pattern's playing time has elapsed. Channels play
            // in lockstep, so the furthest-along active channel measures elapsed time —
            // this keeps counting through SFX loops, unlike a "did every channel stop?"
            // check which never fires when a channel's SFX loops forever.
            long elapsed = 0;
            for (int ch = 0; ch < NumChannels; ch++)
                if (_musicChannelActive[ch])
                    elapsed = Math.Max(elapsed, Channels[ch].SamplesPlayed);

            if (elapsed < _patternLengthSamples) return;

            if (!_musicBank.TryGetValue(_currentMusicPattern, out var current))
            {
                StopMusic();
                return;
            }

            if (current.IsStop)
            {
                StopMusic();
                return;
            }

            int next = current.IsLoopEnd && _musicLoopStart >= 0
                ? _musicLoopStart
                : _currentMusicPattern + 1;

            StartMusicPattern(next);
        }
    }

    private int FindLoopStart(int patternId)
    {
        for (int p = patternId; p >= 0; p--)
        {
            if (_musicBank.TryGetValue(p, out var pattern) && pattern.IsLoopStart)
                return p;
        }
        return -1;
    }

    private void StartMusicPattern(int patternId)
    {
        if (!_musicBank.TryGetValue(patternId, out var pattern))
        {
            StopMusic();
            return;
        }

        _currentMusicPattern = patternId;

        if (pattern.IsLoopStart)
            _musicLoopStart = patternId;

        _patternLengthSamples = 0;
        for (int ch = 0; ch < NumChannels; ch++)
        {
            int raw = pattern.Channels[ch];
            int sfxIndex = raw & 0x3F;
            bool muted = (raw & 0x40) != 0;

            if (muted || !SfxBank.TryGetValue(sfxIndex, out var sfxData))
            {
                // Not used by this pattern — silence any sound carried over from the last one.
                if (_musicChannelActive[ch]) Channels[ch].Stop();
                _musicChannelActive[ch] = false;
                continue;
            }

            _musicChannelActive[ch] = true;
            Channels[ch].Play(sfxIndex, sfxData, 0, sfxData.Notes.Count);

            // Pattern length is its longest channel's single pass. A looping SFX passes
            // once at its loop point; a non-looping one passes over all its notes.
            // In the device's samples, which is what SamplesPlayed above counts in.
            int samplesPerNote = AudioFormat.OutputSamplesPerNote(sfxData);
            int passNotes = sfxData.HasLoop ? sfxData.LoopEnd : sfxData.Notes.Count;
            _patternLengthSamples = Math.Max(_patternLengthSamples, (long)passNotes * samplesPerNote);
        }

        // A pattern with no channel enabled has nothing to play and no way to measure
        // elapsed time (_patternLengthSamples stays 0), so it would otherwise advance
        // every tick instead of stopping.
        bool anyActive = false;
        for (int ch = 0; ch < NumChannels; ch++)
            anyActive |= _musicChannelActive[ch];

        if (!anyActive)
            StopMusic();
    }

    private void StopMusic()
    {
        for (int ch = 0; ch < NumChannels; ch++)
        {
            if (_musicChannelActive[ch])
            {
                Channels[ch].Stop();
                _musicChannelActive[ch] = false;
            }
        }
        _currentMusicPattern = -1;
        _musicLoopStart = -1;
    }

    // ── Buffer callback ──────────────────────────────────────────────────────

    protected void OnBufferNeeded(IAudioChannel ch)
    {
        lock (_lock)
        {
            ch.FillBuffer();
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private int FindFreeChannel()
    {
        for (int i = 0; i < NumChannels; i++)
            if (!Channels[i].IsPlaying) return i;

        int best = 0;
        float bestProgress = -1f;
        for (int i = 0; i < NumChannels; i++)
        {
            float p = Channels[i].Progress;
            if (p > bestProgress) { bestProgress = p; best = i; }
        }
        return best;
    }

    // ── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        foreach (var ch in Channels) ch.Dispose();
    }
}
