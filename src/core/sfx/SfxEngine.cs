namespace mono8.core.sfx;

public sealed class SfxEngine : IDisposable
{
    // ── Audio constants ──────────────────────────────────────────────────────
    private const int SampleRate = 44100;
    private const int TicksPerSec = 120;
    private const int SamplesPerTick = SampleRate / TicksPerSec;
    private const int NumChannels = 4;

    // Buffer submitted per DynamicSFX callback
    private const int BufferTicks = 16;
    private const int BufferSamples = SamplesPerTick * BufferTicks;

    // ── State ────────────────────────────────────────────────────────────────
    private readonly ChannelState[] _channels;
    private readonly Dictionary<int, SfxData> _sfxBank = new();
    private readonly Dictionary<int, MusicData> _musicBank = new();
    private readonly object _lock = new();

    // ── Music playback state ─────────────────────────────────────────────────
    private int _currentMusicPattern = -1;
    private int _musicLoopStart = -1;
    private int _musicChannelMask = 0;
    private readonly bool[] _musicChannelActive = new bool[NumChannels];

    // ── Constructor ──────────────────────────────────────────────────────────
    public SfxEngine()
    {
        _channels = new ChannelState[NumChannels];
        for (int i = 0; i < NumChannels; i++)
            _channels[i] = new ChannelState(i, BufferSamples, OnBufferNeeded, _sfxBank);
    }

    // ── SFX bank ─────────────────────────────────────────────────────────────

    /// <summary>Register a parsed SFX so the engine can play it by index.</summary>
    private void LoadSfx(int index, SfxData data)
    {
        if (index >= 0 && index < Constants.GameDataSizes.Sfx)
        {
            lock (_lock) _sfxBank[index] = data;
        }
    }

    /// <summary>Replace a single SFX in the bank (used by the SFX editor after an edit).</summary>
    public void SetSfx(int index, SfxData data) => LoadSfx(index, data);

    /// <summary>
    /// Note index a channel is currently synthesising for <paramref name="sfxIndex"/>, or -1 if it
    /// isn't playing. Sample-accurate, so an editor playhead stays correct at any FPS or SFX speed.
    /// </summary>
    public int CurrentNote(int sfxIndex)
    {
        lock (_lock)
        {
            foreach (var ch in _channels)
                if (ch.IsPlaying && ch.CurrentSfxIndex == sfxIndex)
                    return ch.CurrentNoteIndex;
        }
        return -1;
    }

    /// <summary>Parse and register a raw hex SFX string at <paramref name="index"/>.</summary>
    private void LoadSfx(int index, string hexSfx)
        => LoadSfx(index, SfxData.FromHex(hexSfx));


    public void LoadSfxs(string[] sfxData)
    {
        for (int i = 0; i < sfxData.Length; i++)
            LoadSfx(i, sfxData[i]);
    }

    public void LoadMusicPatterns(string[] lines)
    {
        for (int i = 0; i < lines.Length && i < Constants.GameDataSizes.Music; i++)
        {
            var line = lines[i]?.Trim();
            if (!string.IsNullOrEmpty(line))
                _musicBank[i] = MusicData.FromLine(line);
        }
    }

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
                foreach (var ch in _channels) ch.Stop();
                return;
            }

            // sfx(-2, channel) → stop a specific channel
            if (sfxIndex == -2)
            {
                int target = channel < 0 ? 0 : Math.Clamp(channel, 0, NumChannels - 1);
                _channels[target].Stop();
                return;
            }

            // sfx(n, -1) → stop any channel currently playing sfx n
            if (channel == -1 && sfxIndex >= 0)
            {
                foreach (var ch in _channels)
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

            if (!_sfxBank.TryGetValue(sfxIndex, out var sfxData))
                return;

            int clampedOffset = Math.Clamp(offset, 0, sfxData.Notes.Count - 1);
            int clampedLength = length < 0
                ? sfxData.Notes.Count - clampedOffset
                : Math.Min(length, sfxData.Notes.Count - clampedOffset);

            if (channel >= 0 && channel < NumChannels)
            {
                _channels[channel].Play(sfxIndex, sfxData, clampedOffset, clampedLength);
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
            _musicLoopStart = -1;
            StartMusicPattern(musicId);
        }
    }

    public void UpdateMusic()
    {
        lock (_lock)
        {
            if (_currentMusicPattern < 0) return;

            bool anyPlaying = false;
            for (int ch = 0; ch < NumChannels; ch++)
            {
                if (_musicChannelActive[ch] && _channels[ch].IsPlaying)
                {
                    anyPlaying = true;
                    break;
                }
            }

            if (anyPlaying) return;

            if (!_musicBank.TryGetValue(_currentMusicPattern, out var current))
            {
                _currentMusicPattern = -1;
                return;
            }

            if (current.IsStop)
            {
                _currentMusicPattern = -1;
                return;
            }

            int next = current.IsLoopEnd && _musicLoopStart >= 0
                ? _musicLoopStart
                : _currentMusicPattern + 1;

            StartMusicPattern(next);
        }
    }

    private void StartMusicPattern(int patternId)
    {
        if (!_musicBank.TryGetValue(patternId, out var pattern))
        {
            _currentMusicPattern = -1;
            return;
        }

        _currentMusicPattern = patternId;

        if (pattern.IsLoopStart)
            _musicLoopStart = patternId;

        for (int ch = 0; ch < NumChannels; ch++)
        {
            _musicChannelActive[ch] = false;

            int raw = pattern.Channels[ch];
            if ((raw & 0x40) != 0) continue;  // muted

            int sfxIndex = raw & 0x3F;
            if (!_sfxBank.TryGetValue(sfxIndex, out var sfxData)) continue;

            _musicChannelActive[ch] = true;
            _channels[ch].Play(sfxIndex, sfxData, 0, sfxData.Notes.Count);
        }
    }

    private void StopMusic()
    {
        for (int ch = 0; ch < NumChannels; ch++)
        {
            if (_musicChannelActive[ch])
            {
                _channels[ch].Stop();
                _musicChannelActive[ch] = false;
            }
        }
        _currentMusicPattern = -1;
        _musicLoopStart = -1;
    }

    // ── Buffer callback ──────────────────────────────────────────────────────

    private void OnBufferNeeded(ChannelState ch)
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
            if (!_channels[i].IsPlaying) return i;

        int best = 0;
        float bestProgress = -1f;
        for (int i = 0; i < NumChannels; i++)
        {
            float p = _channels[i].Progress;
            if (p > bestProgress) { bestProgress = p; best = i; }
        }
        return best;
    }

    // ── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        foreach (var ch in _channels) ch.Dispose();
    }
}