namespace mono8.core.sfx;

/// <summary>
/// The one definition of the console's audio shape. The sequencer, the live synthesiser, the baker
/// and the bank reader all derive note lengths from <see cref="SamplesPerNote"/> — a second copy of
/// this arithmetic anywhere would desynchronise the note-to-sample mapping the bank index is built
/// on, silently and only for some speeds.
/// </summary>
internal static class AudioFormat
{
    /// <summary>What the synthesiser renders at and the baker bakes at. <c>data.wav</c> carries it, so it is fixed.</summary>
    public const int SampleRate = 44100;
    public const int TicksPerSec = 120;
    public const int NumChannels = 4;

    // Buffer submitted per DynamicSoundEffectInstance callback.
    public const int BufferTicks = 16;
    public const int BufferSamples = SampleRate / TicksPerSec * BufferTicks;

    /// <summary>
    /// What the device actually plays at. It is <see cref="SampleRate"/> everywhere but the browser:
    /// WebAudio runs at whatever rate the machine's output is set to — 48000 on most of them — and
    /// refuses a voice at any other, so a web build reads the 44100 bank into that rate instead.
    /// <para>
    /// Set once by the host before any audio is built, and only there; every playing channel counts
    /// its note clock in these samples, so moving it later would desynchronise the music sequencer.
    /// </para>
    /// </summary>
    public static int OutputSampleRate { get; private set; } = SampleRate;

    public static int OutputBufferSamples => OutputSampleRate / TicksPerSec * BufferTicks;

    /// <summary>Called by the Blazor host, before <c>Mono8Game</c> builds anything.</summary>
    internal static void SetOutputSampleRate(int rate)
    {
        // A rate we could not read comes back as 0 or as nonsense, and a voice built around it would
        // be worse than one built around the default.
        if (rate >= 8000 && rate <= 384000) OutputSampleRate = rate;
    }

    /// <summary>How many samples one note of <paramref name="sfx"/> lasts: its speed in 120ths of a second.</summary>
    public static int SamplesPerNote(SfxData sfx)
        => (int)Math.Round(sfx.Speed / (double)TicksPerSec * SampleRate);

    /// <summary>
    /// The same note in the device's samples — the unit a playing channel's <c>SamplesPlayed</c> is
    /// in, and so the one the music sequencer has to measure a pattern with. Identical to
    /// <see cref="SamplesPerNote"/> unless the device asked for a rate of its own.
    /// </summary>
    public static int OutputSamplesPerNote(SfxData sfx)
        => (int)Math.Round(sfx.Speed / (double)TicksPerSec * OutputSampleRate);
}
