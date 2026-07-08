namespace mono8.core.sfx;

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

    /// <summary>Build from a raw 168-char PICO-8 hex string.</summary>
    public static SfxData FromHex(string hex)
    {
        hex = hex?.Trim() ?? string.Empty;

        if (hex.Length != 168)
        {
            hex = new string('0', 168);
        }

        // JS layout:  chars 0-1 = flags, 2-3 = speed, 4-5 = loopStart, 6-7 = loopEnd
        // Each note:  5 hex chars starting at offset 8  (pitch×2, waveform×1, volume×1, effect×1)
        int speed = HexByte(hex, 1);   // bytes 1 → chars 2-3
        int loopStart = HexByte(hex, 2);   // bytes 2 → chars 4-5
        int loopEnd = HexByte(hex, 3);   // bytes 3 → chars 6-7

        var notes = new SfxNote[32];
        for (int i = 0; i < 32; i++)
        {
            int pos = 8 + i * 5;
            int pitch = HexPair(hex, pos);        // 2 chars = note 0-Max
            int waveform = HexNibble(hex, pos + 2);
            int volume = HexNibble(hex, pos + 3);
            int effect = HexNibble(hex, pos + 4);
            notes[i] = new SfxNote(pitch, waveform, volume, effect);
        }
        return new SfxData(speed, loopStart, loopEnd, notes);
    }

    private static int HexByte(string s, int byteIdx) => Convert.ToInt32(s.Substring(byteIdx * 2, 2), 16);
    private static int HexPair(string s, int charIdx) => Convert.ToInt32(s.Substring(charIdx, 2), 16);
    private static int HexNibble(string s, int charIdx) => Convert.ToInt32(s.Substring(charIdx, 1), 16);
}