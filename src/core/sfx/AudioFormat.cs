namespace mono8.core.sfx;

/// <summary>
/// The one definition of the console's audio shape. The sequencer, the live synthesiser, the baker
/// and the bank reader all derive note lengths from <see cref="SamplesPerNote"/> — a second copy of
/// this arithmetic anywhere would desynchronise the note-to-sample mapping the bank index is built
/// on, silently and only for some speeds.
/// </summary>
internal static class AudioFormat
{
    public const int SampleRate = 44100;
    public const int TicksPerSec = 120;
    public const int NumChannels = 4;

    // Buffer submitted per DynamicSoundEffectInstance callback.
    public const int BufferTicks = 16;
    public const int BufferSamples = SampleRate / TicksPerSec * BufferTicks;

    /// <summary>How many samples one note of <paramref name="sfx"/> lasts: its speed in 120ths of a second.</summary>
    public static int SamplesPerNote(SfxData sfx)
        => (int)Math.Round(sfx.Speed / (double)TicksPerSec * SampleRate);
}
