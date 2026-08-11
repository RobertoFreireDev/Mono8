namespace mono8.core.sfx;

/// <summary>
/// One of the console's four voices, as <see cref="AudioSequencer"/> sees it. The sequencer never
/// asks where the samples come from — <see cref="ChannelState"/> synthesises them, while
/// <see cref="BankChannel"/> reads them out of a baked wav — so channel allocation, music pattern
/// timing and the editor playhead are the same code either way.
/// </summary>
internal interface IAudioChannel : IDisposable
{
    void Play(int sfxIndex, SfxData data, int offset, int length);

    void Stop();

    bool IsPlaying { get; }

    int CurrentSfxIndex { get; }

    int CurrentNoteIndex { get; }

    /// <summary>Samples emitted since the last <see cref="Play"/>; keeps counting through SFX loops.</summary>
    long SamplesPlayed { get; }

    /// <summary>Fraction of the requested note range consumed — how the sequencer picks which channel to steal.</summary>
    float Progress { get; }

    void FillBuffer();
}
