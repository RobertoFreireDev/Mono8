namespace mono8.core.sfx;

internal sealed class ChannelState : IDisposable
{
    private const int SampleRate = 44100;

    // ── DSFI ─────────────────────────────────────────────────────────────────
    private readonly DynamicSoundEffectInstance _dsfi;
    private readonly byte[] _byteBuffer;
    private readonly Action<ChannelState> _onBufferNeeded;
    private readonly Dictionary<int, SfxData> _sfxBank;

    // ── Playback state ───────────────────────────────────────────────────────
    private SfxData? _sfx;
    private int _sfxIndex = -1;
    private int _noteIndex;
    private int _noteOffset;
    private int _noteLength;

    // Within-note sample counter (JS: offset within the note's sample block)
    private int _sampleInNote;
    private int _samplesPerNote;

    // Total samples synthesised since the last Play() — keeps counting through SFX
    // loops, so the music engine can time pattern length even when the SFX never ends.
    private long _samplesPlayed;

    private bool _isPlaying;

    // ── Per-note previous-note state (needed for slide) ───────────────────────
    private int _prevNote;
    private float _prevFreq;
    private int _prevVolume;   // 0-7
    private int _prevWaveform;
    private int _prevEffect;

    // ── Oscillator phase (NOT reset between legato notes — matches JS) ────────
    private double _phi;

    // ── Brown noise state ─────────────────────────────────────────────────────
    private double _prevNoise;
    private readonly Random _rng = new();

    public int CurrentSfxIndex => _sfxIndex;
    public int CurrentNoteIndex => _noteIndex;
    public bool IsPlaying => _isPlaying;
    public long SamplesPlayed => _samplesPlayed;
    public float Progress => _sfx == null ? 1f :
        (_noteIndex - _noteOffset) / (float)Math.Max(1, _noteLength);

    public ChannelState(int index, int bufferSamples, Action<ChannelState> onBufferNeeded,
                        Dictionary<int, SfxData> sfxBank)
    {
        _onBufferNeeded = onBufferNeeded;
        _sfxBank = sfxBank;

        _byteBuffer = new byte[bufferSamples * 2];

        _dsfi = new DynamicSoundEffectInstance(SampleRate, AudioChannels.Mono);
        _dsfi.BufferNeeded += (_, _) => _onBufferNeeded(this);
    }

    // ── Public control ───────────────────────────────────────────────────────

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
        _phi = 0;
        _prevNoise = 0;
        _isPlaying = true;

        // Initialise "prev note" state — JS uses note 24 (C2) as the default
        _prevNote = 24;
        _prevFreq = GetFreq(24);
        _prevVolume = -1;
        _prevWaveform = -1;
        _prevEffect = -1;

        _samplesPerNote = ComputeSamplesPerNote(data);

        FillBuffer();
        _dsfi.Play();
    }

    public void Stop()
    {
        if (!_isPlaying) return;
        _isPlaying = false;
        _sfxIndex = -1;
        _sfx = null;
        if (!_dsfi.IsDisposed)
        {
            _dsfi.Stop();
        }
    }

    // ── Buffer synthesis ─────────────────────────────────────────────────────

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

            float sample = SynthesiseSample();

            short pcm = (short)Math.Clamp((int)(sample * 32767f), short.MinValue, short.MaxValue);
            _byteBuffer[s * 2] = (byte)(pcm & 0xFF);
            _byteBuffer[s * 2 + 1] = (byte)((pcm >> 8) & 0xFF);

            AdvanceSampleClock();
        }

        _dsfi.SubmitBuffer(_byteBuffer);
    }

    // ── Clock advancement (sample-granularity, matching JS loop structure) ────

    private void AdvanceSampleClock()
    {
        if (_sfx == null) return;

        _samplesPlayed++;
        _sampleInNote++;
        if (_sampleInNote < _samplesPerNote) return;

        // Note boundary — commit current note as "prev" before moving on
        var cur = _sfx.Notes[_noteIndex];
        _prevNote = cur.Pitch;
        _prevFreq = GetFreq(cur.Pitch);
        _prevWaveform = cur.Instrument;
        _prevVolume = cur.Volume;
        _prevEffect = cur.Effect;

        _sampleInNote = 0;
        _noteIndex = GetNextNoteIndex(_noteIndex);

        // A looping SFX never runs off the end — it wraps in GetNextNoteIndex and
        // plays until it's explicitly stopped (sfx(-1) / sfx(n,-1) / channel reuse).
        if (!_sfx.HasLoop && _noteIndex >= _noteOffset + _noteLength)
        {
            Stop();
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

    // ── Sample synthesis ─────────────────────────────────────────────────────

    private float SynthesiseSample()
    {
        if (_sfx == null) return 0f;

        var note = _sfx.Notes[_noteIndex];

        // noteFactor: 0.0 = start of note, 1.0 = end of note  (matches JS)
        float noteFactor = _samplesPerNote > 0
            ? (float)_sampleInNote / _samplesPerNote
            : 0f;

        // ── Envelope (attack / release) ────────────────────────────────────
        // JS: attack = 0.02, release = 0.05 unless conditions suppress them
        int nextIdx = GetNextNoteIndex(_noteIndex);
        var nextNote = _sfx.Notes[Math.Min(nextIdx, _sfx.Notes.Count - 1)];

        float attack = 0.02f;
        if (note.Effect == SfxEffect.FadeIn ||
            (note.Instrument == _prevWaveform &&
             (note.Pitch == _prevNote || note.Effect == SfxEffect.Slide) &&
             _prevVolume > 0 &&
             _prevEffect != SfxEffect.FadeOut))
        {
            attack = 0f;
        }

        float release = 0.05f;
        if (note.Effect == SfxEffect.FadeOut ||
            (note.Instrument == nextNote.Instrument &&
             (note.Pitch == nextNote.Pitch || nextNote.Effect == SfxEffect.Slide) &&
             nextNote.Volume > 0 &&
             nextNote.Effect != SfxEffect.FadeIn))
        {
            release = 0f;
        }

        float envelope = 1f;
        if (noteFactor < attack && attack > 0f)
            envelope = noteFactor / attack;
        else if (noteFactor > (1f - release) && release > 0f)
            envelope = (1f - noteFactor) / release;

        // ── Frequency and volume ───────────────────────────────────────────
        float freq = GetFreq(note.Pitch);
        float volume = note.Volume / 8f;   // JS: / 8.0

        if (note.Effect == SfxEffect.Slide)
        {
            freq = (1f - noteFactor) * _prevFreq + noteFactor * freq;
            if (_prevVolume > 0)
                volume = (1f - noteFactor) * (_prevVolume / 8f) + noteFactor * volume;
        }
        if (note.Effect == SfxEffect.Vibrato)
            freq *= 1f + 0.02f * (float)Math.Sin(7.5 * noteFactor);
        if (note.Effect == SfxEffect.Drop)
            freq *= 1f - noteFactor;
        if (note.Effect == SfxEffect.FadeIn)
            volume *= noteFactor;
        if (note.Effect == SfxEffect.FadeOut)
            volume *= 1f - noteFactor;

        // ── Arpeggio ───────────────────────────────────────────────────────
        if (note.Effect >= SfxEffect.ArpFast)
        {
            int speed = _sfx.Speed;
            // JS: m = (speed <= 8 ? 32 : 16) / (ArpFast ? 4 : 8)
            int m = (speed <= 8 ? 32 : 16) / (note.Effect == SfxEffect.ArpFast ? 4 : 8);
            int n = (int)(m * noteFactor);
            int arpNoteIdx = (_noteIndex & ~3) | (n & 3);
            arpNoteIdx = Math.Clamp(arpNoteIdx, 0, _sfx.Notes.Count - 1);
            freq = GetFreq(_sfx.Notes[arpNoteIdx].Pitch);
        }

        // ── Oscillator phase advance ───────────────────────────────────────
        _phi += freq / SampleRate;

        float waveOut;
        int instr = note.Instrument;

        if (instr < 8)
        {
            double t = _phi % 1.0;
            waveOut = instr switch
            {
                0 => WaveTriangle(t),
                1 => WaveTiltedSaw(t),
                2 => WaveSaw(t),
                3 => WaveSquare(t),
                4 => WavePulse(t),
                5 => WaveOrgan(t),
                6 => WaveNoise(),
                7 => WavePhaser(t, _phi),
                _ => 0f
            };
        }
        else
        {
            // Custom instrument: use sfx (instr - 8) as a wavetable
            waveOut = SampleCustomInstrument(instr - 8, note.Pitch);
        }

        // JS mixes 4 channels into a single buffer — 0.5 headroom is equivalent
        return waveOut * volume * envelope * 0.5f;
    }

    // ── Waveforms — ported directly from the JS reference ────────────────────

    // Triangle: |2t - 1| - 1.0  → range [-1, 0] (JS implementation)
    private static float WaveTriangle(double t)
        => (float)(Math.Abs(2.0 * t - 1.0) - 1.0);

    // Tilted saw: ramp up over [0, 0.9], sharp fall over [0.9, 1.0], ×0.5
    private static float WaveTiltedSaw(double t)
    {
        const double a = 0.9;
        double v = t < a
            ? 2.0 * t / a - 1.0
            : 2.0 * (1.0 - t) / (1.0 - a) - 1.0;
        return (float)(v * 0.5);
    }

    // Sawtooth: 0→1 ramp shifted to centre, ×0.6  (JS: 0.6*(t<0.5 ? t : t-1))
    private static float WaveSaw(double t)
        => (float)(0.6 * (t < 0.5 ? t : t - 1.0));

    // Square 50 % duty
    private static float WaveSquare(double t)
        => t < 0.5 ? 0.5f : -0.5f;

    // Pulse ~30 % duty
    private static float WavePulse(double t)
        => t < 0.3 ? 0.5f : -0.5f;

    // Organ: tri-uneven (JS formula verbatim)
    private static float WaveOrgan(double t)
        => (float)((t < 0.5
            ? 3.0 - Math.Abs(24.0 * t - 6.0)
            : 1.0 - Math.Abs(16.0 * t - 12.0)) / 9.0);

    // Brown noise (JS: white → IIR low-pass, gain ×10)
    private float WaveNoise()
    {
        double white = _rng.NextDouble() * 2.0 - 1.0;
        double brown = (_prevNoise + 0.02 * white) / 1.02;
        _prevNoise = brown;
        return (float)(brown * 10.0);
    }

    // Phaser: subfrequency modulation via accumulated phase  (JS formula verbatim)
    // JS: k = |2*((phi/128) % 1) - 1|; u = (t + 0.5*k) % 1; |4u - 2| - |8t - 4|) / 6
    private static float WavePhaser(double t, double phi)
    {
        double k = Math.Abs(2.0 * ((phi / 128.0) % 1.0) - 1.0);
        double u = (t + 0.5 * k) % 1.0;
        double ret = Math.Abs(4.0 * u - 2.0) - Math.Abs(8.0 * t - 4.0);
        return (float)(ret / 6.0);
    }

    // ── Custom instruments (sfx-as-wavetable) ─────────────────────────────────

    // Lazily built cache of custom instrument sample arrays.
    // Key: sfxIndex; value: pre-rendered float[] at SampleRate length.
    // For simplicity we render at pitch 24 (C2) and pitch-shift via _phi.
    // A full implementation would match JS's (sfxIndex, pitchOffset) keying.
    private readonly Dictionary<int, float[]> _customCache = new();

    private float SampleCustomInstrument(int sfxInstrIndex, int pitch)
    {
        if (!_sfxBank.TryGetValue(sfxInstrIndex, out var instrSfx))
            return 0f;

        if (!_customCache.TryGetValue(sfxInstrIndex, out var buf))
        {
            buf = BuildCustomInstrumentBuffer(instrSfx);
            _customCache[sfxInstrIndex] = buf;
        }

        if (buf.Length == 0) return 0f;

        // phi-based index into the buffer (wraps)
        int k = (int)((_phi % 1.0) * buf.Length + buf.Length) % buf.Length;
        return buf[k];
    }

    private float[] BuildCustomInstrumentBuffer(SfxData sfx)
    {
        // Render the SFX to a float[] at SampleRate with looping.
        // This is a simplified version of JS buildSound (no pitch offset, no FX chain).
        int loopEnd = sfx.LoopEnd;  // already defaulted to 32 in constructor
        int totalSamples = (int)((sfx.Speed / 120.0) * loopEnd * SampleRate);
        if (totalSamples <= 0) return Array.Empty<float>();

        var buf = new float[totalSamples];
        double phi = 0;
        double prevN = 0;
        var rng = new Random(0);

        int offset = 0;
        for (int i = 0; i < loopEnd; i++)
        {
            var note = sfx.Notes[Math.Min(i, sfx.Notes.Count - 1)];
            int noteSamples = (int)(sfx.Speed / 120.0 * SampleRate);
            float freq = GetFreq(note.Pitch);
            float vol = note.Volume / 8f;

            for (int j = 0; j < noteSamples && offset < totalSamples; j++, offset++)
            {
                phi += freq / SampleRate;
                double t = phi % 1.0;
                float raw = note.Instrument switch
                {
                    0 => WaveTriangle(t),
                    1 => WaveTiltedSaw(t),
                    2 => WaveSaw(t),
                    3 => WaveSquare(t),
                    4 => WavePulse(t),
                    5 => WaveOrgan(t),
                    6 => (float)(((prevN = (prevN + 0.02 * (rng.NextDouble() * 2 - 1)) / 1.02)) * 10.0),
                    7 => WavePhaser(t, phi),
                    _ => 0f
                };
                buf[offset] += raw * vol * 0.5f;
            }
        }
        return buf;
    }

    // ── Pitch → Hz ────────────────────────────────────────────────────────────

    // JS: getFreq = pitch => 65 * 2^(pitch/12)
    // Note: JS uses 65 (not 65.406) — match it exactly for tuning accuracy
    private static float GetFreq(int pitch)
        => (float)(65.0 * Math.Pow(2.0, pitch / 12.0));

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static int ComputeSamplesPerNote(SfxData sfx)
        => (int)Math.Round(sfx.Speed / 120.0 * SampleRate);

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (!_dsfi.IsDisposed)
        {
            _dsfi.Stop();
            _dsfi.Dispose();
        }        
    }
}