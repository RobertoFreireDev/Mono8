namespace mono8.editor;

/// <summary>
/// The tracker note cell, shared by the SFX editor's alternate view and the Music editor.
/// A cell shows one note as five parts — name, octave, waveform, volume, effect. Selecting a
/// part decides what a key press edits: the note part is played from the piano keys, every
/// other part takes a digit from the number row.
/// </summary>
internal static class TrackerNote
{
    public const int PartNote = 0;
    public const int PartOct = 1;
    public const int PartWave = 2;
    public const int PartVol = 3;
    public const int PartFx = 4;
    public const int PartCount = 5;

    /// <summary>X offset / width of each part inside a cell, relative to the cell's left edge.</summary>
    private static readonly int[] PartX = { 3, 11, 19, 25, 31 };
    private static readonly int[] PartW = { 8, 5, 6, 6, 6 };

    /// <summary>Selectable base octaves are 1..MaxOctave.</summary>
    public static int MaxOctave => SfxSheet.MaxPitch / 12;

    private static readonly string[] NoteNames =
    {
        "C-", "C#", "D-", "D#", "E-", "F-", "F#", "G-", "G#", "A-", "A#", "B-",
    };

    /// <summary>Tracker piano: the bottom key row plays the base octave, the top row one octave up.</summary>
    public static readonly (Keys key, int semitone)[] PianoKeys =
    {
        (Keys.Z, 0), (Keys.S, 1), (Keys.X, 2), (Keys.D, 3), (Keys.C, 4), (Keys.V, 5),
        (Keys.G, 6), (Keys.B, 7), (Keys.H, 8), (Keys.N, 9), (Keys.J, 10), (Keys.M, 11),
        (Keys.OemComma, 12), (Keys.L, 13), (Keys.OemPeriod, 14),
        (Keys.Q, 12), (Keys.D2, 13), (Keys.W, 14), (Keys.D3, 15), (Keys.E, 16), (Keys.R, 17),
        (Keys.D5, 18), (Keys.T, 19), (Keys.D6, 20), (Keys.Y, 21), (Keys.D7, 22), (Keys.U, 23),
        (Keys.I, 24),
    };

    private static readonly int[] PaletteColors =
    {
        Constants.Colors.Red, Constants.Colors.Orange, Constants.Colors.Yellow, Constants.Colors.Green,
        Constants.Colors.Blue, Constants.Colors.Indigo, Constants.Colors.Pink, Constants.Colors.Peach,
    };

    public static int PaletteColor(int index) => PaletteColors[index & 7];

    // ── Geometry ──────────────────────────────────────────────────────────────

    public static Rectangle PartRect(Rectangle cell, int part) =>
        new(cell.X + PartX[part], cell.Y, PartW[part], cell.Height - 1);

    /// <summary>The span from the note part to the effect part, used to draw the playhead highlight.</summary>
    public static Rectangle PlayheadRect(Rectangle cell) =>
        new(cell.X + PartX[PartNote], cell.Y,
            PartX[PartFx] + PartW[PartFx] - PartX[PartNote], cell.Height - 1);

    /// <summary>Maps a click's x within a cell to the nearest part; thresholds fill the gaps between parts.</summary>
    public static int PartUnderMouse(Rectangle cell, int mouseX)
    {
        int relX = mouseX - cell.X;
        for (int p = PartCount - 1; p >= 0; p--)
            if (relX >= PartX[p]) return p;
        return PartNote;
    }

    // ── Editing ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Applies a typed digit to <paramref name="part"/> of note <paramref name="note"/>.
    /// Returns false — changing nothing — when the digit is out of range for that part.
    /// <paramref name="waveformOnActivate"/> is the waveform to stamp on a note whose volume
    /// rises from zero, or -1 to leave the waveform alone.
    /// </summary>
    public static bool TrySetPartValue(SfxSheet sheet, int sfx, int note, int part, int digit, int waveformOnActivate)
    {
        switch (part)
        {
            case PartOct:
                if (digit < 1 || digit > MaxOctave) return false;
                int pitch = sheet.GetPitch(sfx, note);
                sheet.SetPitch(sfx, note, Math.Clamp(digit * 12 + pitch % 12, 0, SfxSheet.MaxPitch));
                return true;
            case PartVol:
                if (digit > SfxSheet.MaxVolume) return false;
                if (digit > 0 && waveformOnActivate >= 0 && sheet.GetVolume(sfx, note) == 0)
                    sheet.SetWaveform(sfx, note, waveformOnActivate);
                sheet.SetVolume(sfx, note, digit);
                return true;
            case PartWave:
                if (digit >= SfxSheet.WaveformCount) return false;
                sheet.SetWaveform(sfx, note, digit);
                return true;
            case PartFx:
                if (digit > SfxSheet.MaxEffect) return false;
                sheet.SetEffect(sfx, note, digit);
                return true;
            default:
                return false;
        }
    }

    // ── Drawing ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Draws the five part labels of one note. An inactive note (volume 0) is drawn dimmed
    /// as placeholder dashes. <paramref name="textOffsetY"/> nudges the text within the cell.
    /// </summary>
    public static void DrawParts(IMono8API api, Rectangle cell, SfxSheet sheet, int sfx, int note, int textOffsetY)
    {
        int vol = sheet.GetVolume(sfx, note);
        bool active = vol > 0;

        int pitch = sheet.GetPitch(sfx, note);
        int oct = pitch / 12;
        int wf = sheet.GetWaveform(sfx, note);
        int fx = sheet.GetEffect(sfx, note);
        int dim = Constants.Colors.DarkGray;

        DrawPart(api, cell, PartNote, textOffsetY, active ? NoteNames[pitch % 12] : "--",
            active ? Constants.Colors.Blue : dim);
        DrawPart(api, cell, PartOct, textOffsetY, active ? oct.ToString() : "-", active ? PaletteColor(oct) : dim);
        DrawPart(api, cell, PartWave, textOffsetY, active ? wf.ToString() : "-", active ? PaletteColor(wf) : dim);
        DrawPart(api, cell, PartVol, textOffsetY, active ? vol.ToString() : "-", active ? PaletteColor(vol) : dim);
        DrawPart(api, cell, PartFx, textOffsetY, active ? fx.ToString() : "-", active ? PaletteColor(fx) : dim);
    }

    private static void DrawPart(IMono8API api, Rectangle cell, int part, int textOffsetY, string text, int color) =>
        api.print(text, cell.X + PartX[part], cell.Y + textOffsetY, color);
}
