namespace mono8.core.sfx;

public sealed class MusicData
{
    public int Flags { get; }
    public int[] Channels { get; }  // raw bytes: bit 6 = muted, bits 0-5 = sfx index

    public bool IsLoopStart => (Flags & 0x01) != 0;
    public bool IsLoopEnd   => (Flags & 0x02) != 0;
    public bool IsStop      => (Flags & 0x04) != 0;

    public MusicData(int flags, int[] channels)
    {
        Flags = flags;
        Channels = channels;
    }

    public static MusicData FromLine(string line)
    {
        line = line?.Trim() ?? string.Empty;
        if (line.Length < 11)
            return new MusicData(0, new[] { 0x41, 0x42, 0x43, 0x44 });

        int flags = Convert.ToInt32(line.Substring(0, 2), 16);
        int c0 = Convert.ToInt32(line.Substring(3, 2), 16);
        int c1 = Convert.ToInt32(line.Substring(5, 2), 16);
        int c2 = Convert.ToInt32(line.Substring(7, 2), 16);
        int c3 = Convert.ToInt32(line.Substring(9, 2), 16);

        return new MusicData(flags, new[] { c0, c1, c2, c3 });
    }
}
