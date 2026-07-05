namespace mono8.core.sfx;

public readonly struct SfxNote
{
    public int Pitch { get; }   // 0–Max
    public int Instrument { get; }   // 0–15
    public int Volume { get; }   // 0–7
    public int Effect { get; }   // 0–7

    public SfxNote(int pitch, int instrument, int volume, int effect)
    {
        Pitch = pitch;
        Instrument = instrument;
        Volume = volume;
        Effect = effect;
    }
}
