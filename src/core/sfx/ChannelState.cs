namespace mono8.core.sfx;

/// <summary>
/// A voice that synthesises its samples as it plays them — what the editors audition with, and what
/// a dev build uses throughout. The synthesis itself lives in <see cref="SfxVoice"/>; this is the
/// <c>DynamicSoundEffectInstance</c> around it.
/// </summary>
internal sealed class ChannelState : IAudioChannel
{
    // ── DSFI ─────────────────────────────────────────────────────────────────
    private readonly DynamicSoundEffectInstance _dsfi;
    private readonly byte[] _byteBuffer;
    private readonly Action<IAudioChannel> _onBufferNeeded;

    private readonly SfxVoice _voice;
    private int _sfxIndex = -1;

    public int CurrentSfxIndex => _sfxIndex;
    public int CurrentNoteIndex => _voice.NoteIndex;
    public bool IsPlaying => _voice.IsPlaying;
    public long SamplesPlayed => _voice.SamplesPlayed;
    public float Progress => _voice.Progress;

    public ChannelState(int index, int bufferSamples, Action<IAudioChannel> onBufferNeeded,
                        Dictionary<int, SfxData> sfxBank)
    {
        _onBufferNeeded = onBufferNeeded;
        _voice = new SfxVoice(sfxBank);

        _byteBuffer = new byte[bufferSamples * 2];

        _dsfi = new DynamicSoundEffectInstance(AudioFormat.SampleRate, AudioChannels.Mono);
        _dsfi.BufferNeeded += (_, _) => _onBufferNeeded(this);
    }

    // ── Public control ───────────────────────────────────────────────────────

    public void Play(int sfxIndex, SfxData data, int offset, int length)
    {
        Stop();

        _sfxIndex = sfxIndex;
        _voice.Start(data, offset, length);

        FillBuffer();
        _dsfi.Play();
    }

    public void Stop()
    {
        if (!_voice.IsPlaying) return;
        _voice.Stop();
        ReleaseChannel();
    }

    // The voice also stops itself when a non-looping SFX runs off its end, part-way through a
    // buffer. That has to reach the instance the same way an explicit Stop() does — forget the sfx
    // index, and cut whatever is still queued — or the tail plays on past the last note.
    private void ReleaseChannel()
    {
        _sfxIndex = -1;
        if (!_dsfi.IsDisposed)
        {
            _dsfi.Stop();
        }
    }

    // ── Buffer synthesis ─────────────────────────────────────────────────────

    public void FillBuffer()
    {
        if (!_voice.IsPlaying)
        {
            Array.Clear(_byteBuffer, 0, _byteBuffer.Length);
            _dsfi.SubmitBuffer(_byteBuffer);
            return;
        }

        int bufSamples = _byteBuffer.Length / 2;

        for (int s = 0; s < bufSamples; s++)
        {
            // The SFX can end mid-buffer — the rest of it is silence, and the submit still happens
            // even though Stop() already stopped the instance.
            if (!_voice.IsPlaying)
            {
                for (int r = s; r < bufSamples; r++)
                {
                    _byteBuffer[r * 2] = 0;
                    _byteBuffer[r * 2 + 1] = 0;
                }
                break;
            }

            short pcm = SfxVoice.Quantise(_voice.Next());
            _byteBuffer[s * 2] = (byte)(pcm & 0xFF);
            _byteBuffer[s * 2 + 1] = (byte)((pcm >> 8) & 0xFF);

            // The sample above was the last one — released here, at the same point in the loop the
            // synthesiser used to release it from inside its own clock.
            if (!_voice.IsPlaying) ReleaseChannel();
        }

        _dsfi.SubmitBuffer(_byteBuffer);
    }

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
