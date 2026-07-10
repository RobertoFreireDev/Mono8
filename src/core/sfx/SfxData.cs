namespace mono8.core.sfx;

/// <summary>Immutable snapshot of one SFX, as handed to the audio engine. Built by <see cref="SfxSheet.ToSfxData"/>.</summary>
public sealed class SfxData
{
    public int Speed { get; }   // ticks per note (BASE_SPEED = 120)
    public int LoopStart { get; }
    public int LoopEnd { get; }   // exclusive upper bound; 0 in hex → 32
    public bool HasLoop { get; }  // true only when the raw bounds define a real loop
    public IReadOnlyList<SfxNote> Notes { get; }

    public SfxData(int speed, int loopStart, int loopEnd, IReadOnlyList<SfxNote> notes)
    {
        Speed = Math.Max(1, speed);
        LoopStart = loopStart;
        // A loop exists only when the raw end is non-zero and sits after the start.
        // (LoopEnd below is forced to 32 when raw is 0, so it can't answer this on its own.)
        HasLoop = loopEnd != 0 && loopStart < loopEnd;
        // JS: loopEnd = parseHex(sfxRow, 6, 2) || 32  — treat 0 as 32
        LoopEnd = loopEnd == 0 ? 32 : loopEnd;
        Notes = notes;
    }
}