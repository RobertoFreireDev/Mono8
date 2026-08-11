namespace mono8.core.sfx;

/// <summary>
/// The baked SFX bank as a published build reads it: <c>data.wav</c> holding every audible SFX end
/// to end, and <c>data.snx</c> saying where each one starts.
/// <para>
/// Every failure here — no wav, no index, a wav that is not the expected format, an index that does
/// not match the wav, a stride that disagrees with <c>data.sfx</c> — leaves the SFX (or the whole
/// bank) simply absent, and <see cref="BankChannel"/> plays silence for it. A game must never die
/// because its audio did not bake.
/// </para>
/// </summary>
internal sealed class SfxBank
{
    private const string Magic = "mono8snx";
    private const int FormatVersion = 1;
    private const int HeaderFields = 4;
    private const int EntryFields = 3;

    private static readonly char[] FieldSeparator = { ' ' };

    private byte[] _file = Array.Empty<byte>();
    private int _dataOffset;
    private int _sampleCount;

    private readonly int[] _start = new int[Constants.GameDataSizes.Sfx];
    private readonly int[] _notes = new int[Constants.GameDataSizes.Sfx];
    private readonly int[] _samplesPerNote = new int[Constants.GameDataSizes.Sfx];

    public bool IsLoaded { get; private set; }

    /// <summary>
    /// Where SFX <paramref name="sfxIndex"/> sits in the bank, or false when it was never baked —
    /// which is the normal answer for a silent one.
    /// </summary>
    public bool TryGet(int sfxIndex, out int startSample, out int notesRendered, out int samplesPerNote)
    {
        startSample = 0;
        notesRendered = 0;
        samplesPerNote = 0;

        if (!IsLoaded || sfxIndex < 0 || sfxIndex >= _notes.Length) return false;
        if (_notes[sfxIndex] <= 0) return false;

        startSample = _start[sfxIndex];
        notesRendered = _notes[sfxIndex];
        samplesPerNote = _samplesPerNote[sfxIndex];
        return true;
    }

    public bool Contains(long sampleIndex) => sampleIndex >= 0 && sampleIndex < _sampleCount;

    /// <summary>Copies one 16-bit LE sample into a playback buffer, already in the byte order it wants.</summary>
    public void CopySampleBytes(long sampleIndex, byte[] dest, int destByteOffset)
    {
        int src = _dataOffset + (int)(sampleIndex * 2);
        dest[destByteOffset] = _file[src];
        dest[destByteOffset + 1] = _file[src + 1];
    }

    /// <param name="sfxData">
    /// The freshly parsed <c>data.sfx</c>. Each entry's note length is re-derived from it and
    /// checked against the baked stride: a bank that is out of step with the sheet it came from is
    /// worse than no bank, because it would be read at the wrong offset and play as noise.
    /// </param>
    public void Load(string folderPath, IReadOnlyDictionary<int, SfxData> sfxData)
    {
        Reset();

        var file = FileIO.ReadBytes(Constants.File.Name, Constants.File.Extensions.SfxBank, folderPath);
        if (file == null || file.Length == 0) return;
        if (!WavFile.TryRead(file, out int dataOffset, out int sampleCount)) return;

        var lines = FileIO.SplitData(
            FileIO.Read(Constants.File.Name, Constants.File.Extensions.SfxBankIndex, folderPath));
        if (lines.Length < 1) return;

        var header = lines[0].Trim().Split(FieldSeparator, StringSplitOptions.RemoveEmptyEntries);
        if (header.Length != HeaderFields) return;
        if (header[0] != Magic) return;
        if (!int.TryParse(header[1], out int version) || version != FormatVersion) return;
        if (!int.TryParse(header[2], out int rate) || rate != AudioFormat.SampleRate) return;
        if (!int.TryParse(header[3], out int total) || total != sampleCount) return;

        _file = file;
        _dataOffset = dataOffset;
        _sampleCount = sampleCount;

        for (int i = 0; i < _notes.Length; i++)
        {
            int line = i + 1;
            if (line >= lines.Length) break;

            var fields = lines[line].Trim().Split(FieldSeparator, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length != EntryFields) continue;   // blank line — a silent SFX

            if (!int.TryParse(fields[0], out int start) ||
                !int.TryParse(fields[1], out int notes) ||
                !int.TryParse(fields[2], out int spn)) continue;

            if (start < 0 || notes <= 0 || spn <= 0) continue;
            if ((long)start + (long)notes * spn > sampleCount) continue;

            // The stride the sheet implies now must be the stride the bake used, or every note
            // boundary after the first would land in the wrong place.
            if (!sfxData.TryGetValue(i, out var data) || AudioFormat.SamplesPerNote(data) != spn) continue;

            _start[i] = start;
            _notes[i] = notes;
            _samplesPerNote[i] = spn;
        }

        IsLoaded = true;
    }

    private void Reset()
    {
        IsLoaded = false;
        _file = Array.Empty<byte>();
        _dataOffset = 0;
        _sampleCount = 0;
        Array.Clear(_start);
        Array.Clear(_notes);
        Array.Clear(_samplesPerNote);
    }
}
