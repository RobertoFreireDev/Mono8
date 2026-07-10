namespace mono8.core.sfx;

/// <summary>Immutable snapshot of one music pattern. Built by <see cref="MusicSheet.ToMusicData"/>.</summary>
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
}
