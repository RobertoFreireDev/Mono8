namespace mono8.core.sfx;

/// <summary>
/// A voice that reads its samples out of the baked <see cref="SfxBank"/> instead of computing them.
/// <para>
/// It keeps the same note clock <see cref="SfxVoice"/> does and derives the read position from it
/// rather than holding a pointer of its own. That is what makes the loop wrap, the stop at the end
/// of a requested note range, <see cref="SamplesPlayed"/> and <see cref="Progress"/> come out
/// identical to live synthesis — the sequencer above cannot tell the two apart.
/// </para>
/// </summary>
internal sealed class BankChannel : IAudioChannel
{
    private readonly DynamicSoundEffectInstance _dsfi;
    private readonly byte[] _byteBuffer;
    private readonly Action<IAudioChannel> _onBufferNeeded;
    private readonly SfxBank _bank;

    private SfxData? _sfx;
    private int _sfxIndex = -1;
    private int _noteIndex;
    private int _noteOffset;
    private int _noteLength;
    private int _sampleInNote;
    private int _samplesPerNote;
    private long _samplesPlayed;
    private bool _isPlaying;

    // Resolved once per Play, not per sample.
    private bool _hasBank;
    private int _start;
    private int _notesRendered;
    // The note's length in the bank, which is not its length here when the device plays at a rate
    // the bake was not made at. The two together are the resampling ratio, note by note.
    private int _bakedSamplesPerNote;

    public int CurrentSfxIndex => _sfxIndex;
    public int CurrentNoteIndex => _noteIndex;
    public bool IsPlaying => _isPlaying;
    public long SamplesPlayed => _samplesPlayed;
    public float Progress => _sfx == null ? 1f :
        (_noteIndex - _noteOffset) / (float)Math.Max(1, _noteLength);

    public BankChannel(int index, int bufferSamples, Action<IAudioChannel> onBufferNeeded, SfxBank bank)
    {
        _onBufferNeeded = onBufferNeeded;
        _bank = bank;

        _byteBuffer = new byte[bufferSamples * 2];

        try
        {
            _dsfi = new DynamicSoundEffectInstance(AudioFormat.OutputSampleRate, AudioChannels.Mono);
            _dsfi.BufferNeeded += (_, _) => _onBufferNeeded(this);
        }
        catch (Exception)
        {
            // A device that will not hand out a voice at all — a browser with audio switched off, or
            // one whose rate we failed to read. Silence is a state this channel is already able to be
            // in; refusing to boot the console over it is not. This runs from a static initialiser,
            // where a throw takes the whole game down before a frame is drawn.
            _dsfi = null;
        }
    }

    public void Play(int sfxIndex, SfxData data, int offset, int length)
    {
        Stop();

        // No voice on this device: the channel stays free rather than pretending to play something
        // nothing will ever pull buffers for.
        if (_dsfi == null) return;

        _sfxIndex = sfxIndex;
        _sfx = data;
        _noteOffset = offset;
        _noteLength = length;
        _noteIndex = offset;
        _sampleInNote = 0;
        _samplesPlayed = 0;
        _isPlaying = true;

        // The clock runs in the device's samples, which is what the sequencer measures a pattern
        // with. Never zero: a speed-0 note is one sample here and advances on the next tick either
        // way, and the read below divides by it.
        _samplesPerNote = Math.Max(1, AudioFormat.OutputSamplesPerNote(data));

        // An SFX with nothing baked for it still occupies the channel for its full length rather
        // than declining to start: the music sequencer times patterns off SamplesPlayed, and
        // channel stealing ranks on Progress, so a channel that bailed out here would change both.
        // The stride is checked in the bank's own rate — the bake knew nothing of this device.
        _hasBank = _bank.TryGet(sfxIndex, out _start, out _notesRendered, out _bakedSamplesPerNote)
                   && _bakedSamplesPerNote == AudioFormat.SamplesPerNote(data);

        FillBuffer();
        try { _dsfi.Play(); } catch (Exception) { }
    }

    public void Stop()
    {
        if (!_isPlaying) return;
        _isPlaying = false;
        _sfx = null;
        ReleaseChannel();
    }

    // Every call into the device below is allowed to fail. The browser's voice is only half built
    // while its audio worklet is still loading — stopping one in that window throws out of KNI — and
    // a console frozen on an error screen because a footstep was cut short is a far worse bug than a
    // lost footstep. The channel's own state is ours and stays correct either way.
    private void ReleaseChannel()
    {
        _sfxIndex = -1;
        if (_dsfi != null && !_dsfi.IsDisposed)
        {
            try { _dsfi.Stop(); } catch (Exception) { }
        }
    }

    public void FillBuffer()
    {
        if (_dsfi == null) return;

        if (!_isPlaying || _sfx == null)
        {
            Array.Clear(_byteBuffer, 0, _byteBuffer.Length);
            try { _dsfi.SubmitBuffer(_byteBuffer); } catch (Exception) { }
            return;
        }

        int bufSamples = _byteBuffer.Length / 2;

        for (int s = 0; s < bufSamples; s++)
        {
            if (!_isPlaying || _sfx == null)
            {
                for (int r = s; r < bufSamples; r++)
                {
                    _byteBuffer[r * 2] = 0;
                    _byteBuffer[r * 2 + 1] = 0;
                }
                break;
            }

            // Where in the note we are, in the bank's samples rather than the device's. Scaling by
            // the two strides rather than by a rate ratio is what keeps every note starting exactly
            // on its own first baked sample, however the rates divide.
            long scaled = (long)_sampleInNote * _bakedSamplesPerNote;
            long sample = _start + (long)_noteIndex * _bakedSamplesPerNote + scaled / _samplesPerNote;
            int frac = (int)(scaled % _samplesPerNote);

            // Past the baked length is silence — which is exactly what the synthesiser produced
            // there, since that is why the bake stopped where it did.
            if (_hasBank && _noteIndex >= 0 && _noteIndex < _notesRendered && _bank.Contains(sample))
            {
                if (frac == 0)
                {
                    _bank.CopySampleBytes(sample, _byteBuffer, s * 2);
                }
                else
                {
                    // Between two baked samples: interpolate rather than repeat the nearer one,
                    // which at 44100 into 48000 would beat against the waveform audibly.
                    int a = _bank.GetSample(sample);
                    int b = _bank.Contains(sample + 1) ? _bank.GetSample(sample + 1) : a;
                    int value = a + (b - a) * frac / _samplesPerNote;
                    _byteBuffer[s * 2] = (byte)value;
                    _byteBuffer[s * 2 + 1] = (byte)(value >> 8);
                }
            }
            else
            {
                _byteBuffer[s * 2] = 0;
                _byteBuffer[s * 2 + 1] = 0;
            }

            AdvanceSampleClock();

            if (!_isPlaying) ReleaseChannel();
        }

        try { _dsfi.SubmitBuffer(_byteBuffer); } catch (Exception) { }
    }

    // ── Clock advancement — the same walk ChannelState's synthesiser makes ─────

    private void AdvanceSampleClock()
    {
        if (_sfx == null) return;

        _samplesPlayed++;
        _sampleInNote++;
        if (_sampleInNote < _samplesPerNote) return;

        _sampleInNote = 0;
        _noteIndex = GetNextNoteIndex(_noteIndex);

        // A looping SFX never runs off the end — it wraps in GetNextNoteIndex and
        // plays until it's explicitly stopped (sfx(-1) / sfx(n,-1) / channel reuse).
        if (!_sfx.HasLoop && _noteIndex >= _noteOffset + _noteLength)
        {
            _isPlaying = false;
            _sfx = null;
        }
    }

    private int GetNextNoteIndex(int i)
    {
        if (_sfx == null) return i + 1;
        int next = i + 1;
        // Jump back to LoopStart once we pass the (exclusive) LoopEnd bound.
        if (_sfx.HasLoop && next >= _sfx.LoopEnd)
            next = _sfx.LoopStart;
        return next;
    }

    public void Dispose()
    {
        if (_dsfi != null && !_dsfi.IsDisposed)
        {
            try { _dsfi.Stop(); } catch (Exception) { }
            _dsfi.Dispose();
        }
    }
}
