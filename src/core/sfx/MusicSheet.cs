using System.Text;

namespace mono8.core.sfx;

/// <summary>
/// Editable, mutable bank of all Music Patterns (the source of truth the Music editor writes to).
/// Serialises to/from the same PICO-8 "ff cccccccc" rows that <see cref="MusicData"/> parses,
/// and produces immutable <see cref="MusicData"/> snapshots for the audio engine.
/// </summary>
internal sealed class MusicSheet
{
    public const int Count = Constants.GameDataSizes.Music;   // 64
    public const int ChannelCount = 4;

    // Pattern flag bits (mirrors MusicData).
    private const int FlagLoopStart = 0x01;
    private const int FlagLoopEnd = 0x02;
    private const int FlagStop = 0x04;

    // Channel byte layout: bit 6 (0x40) = muted/off, bits 0-5 = sfx index (0-63).
    private const int ChannelMuteBit = 0x40;
    private const int ChannelSfxMask = 0x3F;

    private readonly int[] _flags = new int[Count];
    private readonly bool[,] _enabled = new bool[Count, ChannelCount];
    private readonly int[,] _sfx = new int[Count, ChannelCount];

    // ── Pattern flags ──────────────────────────────────────────────────────────
    public bool IsLoopStart(int p) => (_flags[p] & FlagLoopStart) != 0;
    public bool IsLoopEnd(int p) => (_flags[p] & FlagLoopEnd) != 0;
    public bool IsStop(int p) => (_flags[p] & FlagStop) != 0;

    public void ToggleLoopStart(int p) => _flags[p] ^= FlagLoopStart;
    public void ToggleLoopEnd(int p) => _flags[p] ^= FlagLoopEnd;
    public void ToggleStop(int p) => _flags[p] ^= FlagStop;

    // ── Channels ───────────────────────────────────────────────────────────────
    public bool IsChannelOn(int p, int c) => _enabled[p, c];
    public void ToggleChannel(int p, int c) => _enabled[p, c] = !_enabled[p, c];

    public int GetChannelSfx(int p, int c) => _sfx[p, c];
    public void SetChannelSfx(int p, int c, int v) =>
        _sfx[p, c] = ((v % SfxSheet.Count) + SfxSheet.Count) % SfxSheet.Count;   // wrap 0..63
    public void ChangeChannelSfx(int p, int c, int delta) => SetChannelSfx(p, c, _sfx[p, c] + delta);

    // ── Load / Save ───────────────────────────────────────────────────────────

    public void LoadMusic(string[] lines)
    {
        for (int p = 0; p < Count; p++)
        {
            string line = lines != null && p < lines.Length ? lines[p]?.Trim() : null;
            if (string.IsNullOrEmpty(line) || line.Length < 11)
            {
                _flags[p] = 0;
                for (int c = 0; c < ChannelCount; c++) { _enabled[p, c] = false; _sfx[p, c] = 0; }
                continue;
            }

            var data = MusicData.FromLine(line);
            _flags[p] = data.Flags;
            for (int c = 0; c < ChannelCount; c++)
            {
                int raw = data.Channels[c];
                _enabled[p, c] = (raw & ChannelMuteBit) == 0;
                _sfx[p, c] = raw & ChannelSfxMask;
            }
        }
    }

    public string[] ToMusicLines()
    {
        var lines = new string[Count];
        for (int p = 0; p < Count; p++)
        {
            var sb = new StringBuilder(11);
            sb.Append(_flags[p].ToString("x2"));
            sb.Append(' ');
            for (int c = 0; c < ChannelCount; c++)
                sb.Append(ChannelByte(p, c).ToString("x2"));
            lines[p] = sb.ToString();
        }
        return lines;
    }

    /// <summary>Immutable snapshot of one pattern for the audio engine.</summary>
    public MusicData ToMusicData(int p)
    {
        var channels = new int[ChannelCount];
        for (int c = 0; c < ChannelCount; c++)
            channels[c] = ChannelByte(p, c);
        return new MusicData(_flags[p], channels);
    }

    private int ChannelByte(int p, int c) =>
        _enabled[p, c] ? (_sfx[p, c] & ChannelSfxMask) : ((_sfx[p, c] & ChannelSfxMask) | ChannelMuteBit);
}
