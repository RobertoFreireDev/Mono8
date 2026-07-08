using System.Text;

namespace mono8.core.sfx;

/// <summary>
/// Editable, mutable bank of all SFX (the source of truth the SFX editor writes to).
/// Serialises to/from the same 168-char PICO-8 hex rows that <see cref="SfxData"/> parses,
/// and produces immutable <see cref="SfxData"/> snapshots for the audio engine.
/// </summary>
internal sealed class SfxSheet
{
    public const int Count = Constants.GameDataSizes.Sfx;   // 64
    public const int NotesPerSfx = 32;
    public const int MaxPitch = 63;
    public const int MaxVolume = 7;
    public const int MaxEffect = 7;
    public const int WaveformCount = 8;   // built-in waveforms 0-7 (8-15 = custom instruments)
    public const int DefaultSpeed = 16;

    private const int HexLength = 168;
    private const int NoteStride = 5;
    private const int HeaderChars = 8;

    private readonly int[] _flags = new int[Count];
    private readonly int[] _speed = new int[Count];
    private readonly int[] _loopStart = new int[Count];
    private readonly int[] _loopEnd = new int[Count];
    private readonly int[,] _pitch = new int[Count, NotesPerSfx];
    private readonly int[,] _waveform = new int[Count, NotesPerSfx];
    private readonly int[,] _volume = new int[Count, NotesPerSfx];
    private readonly int[,] _effect = new int[Count, NotesPerSfx];

    public SfxSheet()
    {
        for (int i = 0; i < Count; i++)
            _speed[i] = DefaultSpeed;
    }

    // ── Per-SFX header ────────────────────────────────────────────────────────
    public int GetSpeed(int sfx) => _speed[sfx];
    public void SetSpeed(int sfx, int value) => _speed[sfx] = Math.Clamp(value, 1, 255);

    public int GetLoopStart(int sfx) => _loopStart[sfx];
    public void SetLoopStart(int sfx, int value) => _loopStart[sfx] = Math.Clamp(value, 0, NotesPerSfx);

    public int GetLoopEnd(int sfx) => _loopEnd[sfx];
    public void SetLoopEnd(int sfx, int value) => _loopEnd[sfx] = Math.Clamp(value, 0, NotesPerSfx);

    // ── Per-note ──────────────────────────────────────────────────────────────
    public int GetPitch(int sfx, int n) => _pitch[sfx, n];
    public int GetWaveform(int sfx, int n) => _waveform[sfx, n];
    public int GetVolume(int sfx, int n) => _volume[sfx, n];
    public int GetEffect(int sfx, int n) => _effect[sfx, n];

    public void SetPitch(int sfx, int n, int v) => _pitch[sfx, n] = Math.Clamp(v, 0, MaxPitch);
    public void SetWaveform(int sfx, int n, int v) => _waveform[sfx, n] = Math.Clamp(v, 0, 15);
    public void SetVolume(int sfx, int n, int v) => _volume[sfx, n] = Math.Clamp(v, 0, MaxVolume);
    public void SetEffect(int sfx, int n, int v) => _effect[sfx, n] = Math.Clamp(v, 0, MaxEffect);

    /// <summary>Silence a note (volume 0), matching PICO-8 right-click erase.</summary>
    public void ClearNote(int sfx, int n)
    {
        _pitch[sfx, n] = 0;
        _volume[sfx, n] = 0;
        _effect[sfx, n] = SfxEffect.None;
    }

    // ── Load / Save ───────────────────────────────────────────────────────────

    public void LoadSfxs(string[] lines)
    {
        for (int i = 0; i < Count; i++)
        {
            string hex = lines != null && i < lines.Length ? lines[i]?.Trim() : null;
            if (string.IsNullOrEmpty(hex) || hex.Length != HexLength)
            {
                _speed[i] = DefaultSpeed;
                continue;
            }

            _flags[i] = HexByte(hex, 0);
            _speed[i] = Math.Max(1, HexByte(hex, 1));
            _loopStart[i] = Math.Clamp(HexByte(hex, 2), 0, NotesPerSfx);
            _loopEnd[i] = Math.Clamp(HexByte(hex, 3), 0, NotesPerSfx);

            for (int n = 0; n < NotesPerSfx; n++)
            {
                int pos = HeaderChars + n * NoteStride;
                _pitch[i, n] = Math.Clamp(HexPair(hex, pos), 0, MaxPitch);
                _waveform[i, n] = HexNibble(hex, pos + 2);
                _volume[i, n] = Math.Clamp(HexNibble(hex, pos + 3), 0, MaxVolume);
                _effect[i, n] = Math.Clamp(HexNibble(hex, pos + 4), 0, MaxEffect);
            }
        }
    }

    public string[] ToSfxLines()
    {
        var lines = new string[Count];
        for (int i = 0; i < Count; i++)
        {
            var sb = new StringBuilder(HexLength);
            sb.Append(_flags[i].ToString("x2"));
            sb.Append(_speed[i].ToString("x2"));
            sb.Append(_loopStart[i].ToString("x2"));
            sb.Append(_loopEnd[i].ToString("x2"));
            for (int n = 0; n < NotesPerSfx; n++)
            {
                sb.Append(_pitch[i, n].ToString("x2"));
                sb.Append((_waveform[i, n] & 0xF).ToString("x1"));
                sb.Append((_volume[i, n] & 0xF).ToString("x1"));
                sb.Append((_effect[i, n] & 0xF).ToString("x1"));
            }
            lines[i] = sb.ToString();
        }
        return lines;
    }

    /// <summary>Immutable snapshot of one SFX for the audio engine.</summary>
    public SfxData ToSfxData(int sfx)
    {
        var notes = new SfxNote[NotesPerSfx];
        for (int n = 0; n < NotesPerSfx; n++)
            notes[n] = new SfxNote(_pitch[sfx, n], _waveform[sfx, n], _volume[sfx, n], _effect[sfx, n]);
        return new SfxData(_speed[sfx], _loopStart[sfx], _loopEnd[sfx], notes);
    }

    private static int HexByte(string s, int byteIdx) => Convert.ToInt32(s.Substring(byteIdx * 2, 2), 16);
    private static int HexPair(string s, int charIdx) => Convert.ToInt32(s.Substring(charIdx, 2), 16);
    private static int HexNibble(string s, int charIdx) => Convert.ToInt32(s.Substring(charIdx, 1), 16);
}
