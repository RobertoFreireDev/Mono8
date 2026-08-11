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

        _dsfi = new DynamicSoundEffectInstance(AudioFormat.SampleRate, AudioChannels.Mono);
        _dsfi.BufferNeeded += (_, _) => _onBufferNeeded(this);
    }

    public void Play(int sfxIndex, SfxData data, int offset, int length)
    {
        Stop();

        _sfxIndex = sfxIndex;
        _sfx = data;
        _noteOffset = offset;
        _noteLength = length;
        _noteIndex = offset;
        _sampleInNote = 0;
        _samplesPlayed = 0;
        _isPlaying = true;

        _samplesPerNote = AudioFormat.SamplesPerNote(data);

        // An SFX with nothing baked for it still occupies the channel for its full length rather
        // than declining to start: the music sequencer times patterns off SamplesPlayed, and
        // channel stealing ranks on Progress, so a channel that bailed out here would change both.
        _hasBank = _bank.TryGet(sfxIndex, out _start, out _notesRendered, out int bakedSamplesPerNote)
                   && bakedSamplesPerNote == _samplesPerNote;

        FillBuffer();
        _dsfi.Play();
    }

    public void Stop()
    {
        if (!_isPlaying) return;
        _isPlaying = false;
        _sfx = null;
        ReleaseChannel();
    }

    private void ReleaseChannel()
    {
        _sfxIndex = -1;
        if (!_dsfi.IsDisposed)
        {
            _dsfi.Stop();
        }
    }

    public void FillBuffer()
    {
        if (!_isPlaying || _sfx == null)
        {
            Array.Clear(_byteBuffer, 0, _byteBuffer.Length);
            _dsfi.SubmitBuffer(_byteBuffer);
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

            // Past the baked length is silence — which is exactly what the synthesiser produced
            // there, since that is why the bake stopped where it did.
            long sample = _start + (long)_noteIndex * _samplesPerNote + _sampleInNote;
            if (_hasBank && _noteIndex >= 0 && _noteIndex < _notesRendered && _bank.Contains(sample))
            {
                _bank.CopySampleBytes(sample, _byteBuffer, s * 2);
            }
            else
            {
                _byteBuffer[s * 2] = 0;
                _byteBuffer[s * 2 + 1] = 0;
            }

            AdvanceSampleClock();

            if (!_isPlaying) ReleaseChannel();
        }

        _dsfi.SubmitBuffer(_byteBuffer);
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
        if (!_dsfi.IsDisposed)
        {
            _dsfi.Stop();
            _dsfi.Dispose();
        }
    }
}
