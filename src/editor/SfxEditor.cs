namespace mono8.editor;

/// <summary>
/// PICO-8 style SFX editor: per-SFX speed &amp; loop points, an index selector,
/// waveform (instrument) pens, a 32-column pitch region and a 32-column volume region.
/// </summary>
internal class SfxEditor : IEditor
{
    private readonly IMono8API _api;
    private readonly EventNotifier eventNotifier;

    // ── Layout ────────────────────────────────────────────────────────────────
    private const int NoteCount = SfxSheet.NotesPerSfx;   // 32
    private const int RegionX = 2;                         // ~2px side margin
    private static readonly int RegionW = Constants.Screen.ResolutionX - RegionX * 2; // fills width (252)
    private const int BarGap = 1;                         // gap between adjacent bars

    private const int HeaderY = 9;
    private const int WaveY = 18;

    private const int PitchTop = 28;
    private const int PitchHeight = 60;
    private const int PitchBottom = PitchTop + PitchHeight - 1;

    private const int VolLabelY = 89;
    private const int VolLabelH = 8;

    private const int VolTop = 98;
    private const int VolHeight = 32;
    private const int VolBottom = VolTop + VolHeight - 1;

    private static readonly int BottomBarY = Constants.Screen.ResolutionY - Constants.GameDataSizes.TileSize;

    private const int DefaultPlaceVolume = 5;
    private const int WaveformIconStart = 32;   // waveform icons occupy icon indices 32..39

    private static readonly int[] PaletteColors =
    {
        Constants.Colors.Red, Constants.Colors.Orange, Constants.Colors.Yellow, Constants.Colors.Green,
        Constants.Colors.Blue, Constants.Colors.Indigo, Constants.Colors.Pink, Constants.Colors.Peach,
    };

    // ── Alternate (tracker) view ───────────────────────────────────────────────
    private const int OctaveCount = 5;           // selectable base octaves 1..5
    private const int EffectCount = 8;           // effects 0..7
    private const int EffectIconStart = 47;      // effect icons occupy icon indices 47..54
    private const int GridRows = 8;
    private const int GridCols = 4;              // 8 rows x 4 cols = 32 notes
    private const int CellH = 12;
    private const int GridTop = 37;
    private const int PaletteRowY = WaveY;       // horizontal VOL/FX row, right below the header line
    private const int OctLabelX = 196;           // octave selector sits after the waveform buttons
    private const int OctBoxX = 210;
    private const int VolLabelX = 2;
    private const int VolCellsX = 18;
    private const int FxLabelX = 93;
    private const int FxButtonsX = 105;
    private const int NoteColStart = RegionX;    // centered: VOL/FX no longer occupy the left column
    private const int NoteColGap = 6;
    private static readonly int NoteColW =
        (Constants.Screen.ResolutionX - NoteColStart - 2 - (GridCols - 1) * NoteColGap) / GridCols;

    // A note cell is split into clickable parts. Selecting a part decides what a key press edits:
    // the note part is played from the piano keys; the rest take a digit from the number row.
    private const int PartNote = 0;   // note name (2 chars) -> pitch via piano keys
    private const int PartOct = 1;   // octave (1 digit)
    private const int PartVol = 2;   // volume (1 digit)
    private const int PartWave = 3;   // waveform (1 digit)
    private const int PartFx = 4;   // effect (1 digit)
    private const int PartCount = 5;

    // X offset / width of each part inside a cell (relative to the cell's left edge).
    private static readonly int[] PartX = { 3, 11, 19, 25, 31 };
    private static readonly int[] PartW = { 8, 5, 6, 6, 6 };

    private static readonly string[] NoteNames =
    {
        "C-", "C#", "D-", "D#", "E-", "F-", "F#", "G-", "G#", "A-", "A#", "B-",
    };

    // Tracker piano: bottom key row plays the base octave, top row one octave up.
    private static readonly (Keys key, int semitone)[] PianoKeys =
    {
        (Keys.Z, 0), (Keys.S, 1), (Keys.X, 2), (Keys.D, 3), (Keys.C, 4), (Keys.V, 5),
        (Keys.G, 6), (Keys.B, 7), (Keys.H, 8), (Keys.N, 9), (Keys.J, 10), (Keys.M, 11),
        (Keys.OemComma, 12), (Keys.L, 13), (Keys.OemPeriod, 14),
        (Keys.Q, 12), (Keys.D2, 13), (Keys.W, 14), (Keys.D3, 15), (Keys.E, 16), (Keys.R, 17),
        (Keys.D5, 18), (Keys.T, 19), (Keys.D6, 20), (Keys.Y, 21), (Keys.D7, 22), (Keys.U, 23),
        (Keys.I, 24),
    };

    // ── Header controls ───────────────────────────────────────────────────────
    private readonly Rectangle idxPrevBox = new(2, HeaderY, 7, 7);
    private readonly Rectangle idxNextBox = new(33, HeaderY, 7, 7);
    private readonly Rectangle spdBox = new(57, HeaderY, 15, 7);
    private readonly Rectangle loopStartBox = new(85, HeaderY, 12, 7);
    private readonly Rectangle loopEndBox = new(103, HeaderY, 12, 7);
    private const int WaveButtonsX = 122;

    private readonly Rectangle pitchRegion = new(RegionX, PitchTop, RegionW, PitchHeight);
    private readonly Rectangle volRegion = new(RegionX, VolTop, RegionW, VolHeight);

    private readonly Button[] waveButtons;
    private int selectedWaveform = 0;

    // Alternate-view controls / state.
    private readonly Rectangle[] octBoxes;     // base octave 1..4
    private readonly Rectangle[] volCells;     // index = volume level 0..7 (top = 7)
    private readonly Button[] effectButtons;   // effect 0..7

    private int selectedOctave = 2;
    private int selectedVolume = DefaultPlaceVolume;
    private int selectedEffect = SfxEffect.None;
    private int selectedCell = 0;
    private int selectedPart = PartNote;

    private int sfxIndex = 0;

    // Set by the menu bar toggle (top-left, only shown while the sfx editor is active).
    // false = primary pitch/volume view (below); true = the alternate view.
    public bool AltView { get; set; }

    public SfxEditor(IMono8API api)
    {
        _api = api;
        eventNotifier = new EventNotifier(api, 2f, 1, Constants.Screen.ResolutionY - Constants.GameDataSizes.TileSize + 1);

        waveButtons = new Button[SfxSheet.WaveformCount];
        for (int i = 0; i < waveButtons.Length; i++)
        {
            waveButtons[i] = new Button(WaveButtonsX + i * (Constants.GameDataSizes.TileSize + 1), HeaderY,
                Constants.GameDataSizes.TileSize, WaveformIconStart + i);
        }

        octBoxes = new Rectangle[OctaveCount];
        for (int i = 0; i < OctaveCount; i++)
            octBoxes[i] = new Rectangle(OctBoxX + i * 9, HeaderY, 7, 7);

        volCells = new Rectangle[SfxSheet.MaxVolume + 1];
        for (int v = 0; v <= SfxSheet.MaxVolume; v++)
            volCells[v] = new Rectangle(VolCellsX + v * 9, PaletteRowY, 8, 7);

        effectButtons = new Button[EffectCount];
        for (int i = 0; i < EffectCount; i++)
            effectButtons[i] = new Button(FxButtonsX + i * 9, PaletteRowY,
                Constants.GameDataSizes.TileSize, EffectIconStart + i);
    }

    public void Init()
    {
    }

    private SfxSheet Sheet => Mono8API.SfxSheet;

    private void Sync() => mono8.GameAPI.SyncSfx(sfxIndex);

    private void ChangeIndex(int delta)
    {
        sfxIndex = (sfxIndex + delta + SfxSheet.Count) % SfxSheet.Count;
    }

    private static int NoteUnderMouse(int mouseX) =>
        Math.Clamp((mouseX - RegionX) * NoteCount / RegionW, 0, NoteCount - 1);

    // Column edges spread evenly across the full region width (bars end up 7-8px wide).
    private static int ColLeft(int n) => RegionX + n * RegionW / NoteCount;
    private static int ColRight(int n) => RegionX + (n + 1) * RegionW / NoteCount - 1;

    private int PitchFromY(int y)
    {
        float frac = (PitchBottom - y) / (float)(PitchHeight - 1);
        return Math.Clamp((int)Math.Round(frac * SfxSheet.MaxPitch), 0, SfxSheet.MaxPitch);
    }

    private int VolumeFromY(int y)
    {
        float frac = (VolBottom - y) / (float)(VolHeight - 1);
        return Math.Clamp((int)Math.Round(frac * SfxSheet.MaxVolume), 0, SfxSheet.MaxVolume);
    }

    public void Update(float elapsedSeconds)
    {
        eventNotifier.Update(elapsedSeconds);

        if (KeybrdInput.IsSaveShortcutPressed())
        {
            mono8.GameAPI.Save();
            eventNotifier.AddEvent("SAVED");
        }

        if (KeybrdInput.JustPressed(Keys.Space))
        {
            if (mono8.GameAPI.CurrentSfxNote(sfxIndex) >= 0)
            {
                _api.sfx(-1);
            }
            else
            {
                Sync();
                _api.sfx(sfxIndex);
            }
        }

        if (AltView)
        {
            UpdateAltView();
            return;
        }

        if (KeybrdInput.JustPressed(Keys.Left)) ChangeIndex(-1);
        if (KeybrdInput.JustPressed(Keys.Right)) ChangeIndex(+1);

        UpdatePrimaryView();
    }

    private void UpdatePrimaryView()
    {
        var mouse = _api.mousexy();

        if (pitchRegion.Contains(mouse.x, mouse.y))
        {
            int n = NoteUnderMouse(mouse.x);
            if (_api.mousel())
            {
                Sheet.SetPitch(sfxIndex, n, PitchFromY(mouse.y));
                Sheet.SetWaveform(sfxIndex, n, selectedWaveform);
                if (Sheet.GetVolume(sfxIndex, n) == 0)
                    Sheet.SetVolume(sfxIndex, n, DefaultPlaceVolume);
                Sync();
            }
            else if (_api.mouser())
            {
                Sheet.ClearNote(sfxIndex, n);
                Sync();
            }
        }
        else if (volRegion.Contains(mouse.x, mouse.y))
        {
            int n = NoteUnderMouse(mouse.x);
            if (_api.mousel())
            {
                int v = VolumeFromY(mouse.y);
                if (v > 0 && Sheet.GetVolume(sfxIndex, n) == 0)
                    Sheet.SetWaveform(sfxIndex, n, selectedWaveform);
                Sheet.SetVolume(sfxIndex, n, v);
                Sync();
            }
            else if (_api.mouser())
            {
                Sheet.SetVolume(sfxIndex, n, 0);
                Sync();
            }
        }
        else
        {
            UpdateHeader(mouse);

            for (int i = 0; i < waveButtons.Length; i++)
            {
                if (waveButtons[i].IsClicked(_api, mouse))
                {
                    selectedWaveform = i;
                    break;
                }
            }
        }
    }

    private void UpdateAltView()
    {
        var mouse = _api.mousexy();

        // Shared header + waveform selector (same controls/logic as the primary view).
        UpdateHeader(mouse);

        for (int i = 0; i < waveButtons.Length; i++)
            if (waveButtons[i].IsClicked(_api, mouse)) selectedWaveform = i;

        // Octave / volume / effect palettes (values applied to newly typed notes).
        for (int i = 0; i < octBoxes.Length; i++)
            if (octBoxes[i].Contains(mouse.x, mouse.y) && _api.mouselp()) selectedOctave = i + 1;

        for (int v = 0; v < volCells.Length; v++)
            if (volCells[v].Contains(mouse.x, mouse.y) && _api.mousel()) selectedVolume = v;

        for (int i = 0; i < effectButtons.Length; i++)
            if (effectButtons[i].IsClicked(_api, mouse)) selectedEffect = i;

        // Note grid: left-click selects the cursor cell (and the part clicked on),
        // right-click clears a note.
        for (int cell = 0; cell < NoteCount; cell++)
        {
            if (!CellRect(cell).Contains(mouse.x, mouse.y)) continue;
            if (_api.mouselp()) { selectedCell = cell; selectedPart = PartUnderMouse(cell, mouse.x); }
            else if (_api.mouserp()) { Sheet.ClearNote(sfxIndex, cell); Sync(); }
        }

        // Cursor navigation (down a column, then across to the next column).
        if (KeybrdInput.JustPressed(Keys.Up)) selectedCell = Math.Max(0, selectedCell - 1);
        if (KeybrdInput.JustPressed(Keys.Down)) selectedCell = Math.Min(NoteCount - 1, selectedCell + 1);
        if (KeybrdInput.JustPressed(Keys.Left))
        {
            if (selectedPart > 0) selectedPart--;
            else if (selectedCell - GridRows >= 0) { selectedCell -= GridRows; selectedPart = PartCount - 1; }
        }
        if (KeybrdInput.JustPressed(Keys.Right))
        {
            if (selectedPart < PartCount - 1) selectedPart++;
            else if (selectedCell + GridRows < NoteCount) { selectedCell += GridRows; selectedPart = 0; }
        }

        if (KeybrdInput.JustPressed(Keys.Delete) || KeybrdInput.JustPressed(Keys.Back))
        {
            Sheet.ClearNote(sfxIndex, selectedCell);
            Sync();
            selectedCell = Math.Min(selectedCell + 1, NoteCount - 1);
        }

        // Keyboard editing depends on which part of the cell is selected:
        // the note part plays from the piano keys, every other part takes a number-row digit.
        if (selectedPart == PartNote)
        {
            foreach (var (key, semitone) in PianoKeys)
                if (KeybrdInput.JustPressed(key)) { EnterNote(semitone); break; }
        }
        else
        {
            int digit = JustPressedDigit();
            if (digit >= 0) SetPartValue(selectedPart, digit);
        }
    }

    // Number-row or numpad digit just pressed, or -1 if none.
    private static int JustPressedDigit()
    {
        for (int d = 0; d <= 9; d++)
            if (KeybrdInput.JustPressed(Keys.D0 + d) || KeybrdInput.JustPressed(Keys.NumPad0 + d))
                return d;
        return -1;
    }

    // Apply a typed digit to the selected part of the current cell. Out-of-range digits are ignored.
    private void SetPartValue(int part, int digit)
    {
        switch (part)
        {
            case PartOct:
                if (digit < 1 || digit > OctaveCount) return;
                int pitch = Sheet.GetPitch(sfxIndex, selectedCell);
                Sheet.SetPitch(sfxIndex, selectedCell, Math.Clamp(digit * 12 + pitch % 12, 0, SfxSheet.MaxPitch));
                break;
            case PartVol:
                if (digit > SfxSheet.MaxVolume) return;
                if (digit > 0 && Sheet.GetVolume(sfxIndex, selectedCell) == 0)
                    Sheet.SetWaveform(sfxIndex, selectedCell, selectedWaveform);
                Sheet.SetVolume(sfxIndex, selectedCell, digit);
                break;
            case PartWave:
                if (digit >= SfxSheet.WaveformCount) return;
                Sheet.SetWaveform(sfxIndex, selectedCell, digit);
                break;
            case PartFx:
                if (digit > SfxSheet.MaxEffect) return;
                Sheet.SetEffect(sfxIndex, selectedCell, digit);
                break;
            default:
                return;
        }
        Sync();
        _api.sfx(sfxIndex, -1, selectedCell, 1);   // preview just this note
    }

    private void EnterNote(int semitone)
    {
        int pitch = Math.Clamp(selectedOctave * 12 + semitone, 0, SfxSheet.MaxPitch);
        Sheet.SetPitch(sfxIndex, selectedCell, pitch);
        Sheet.SetWaveform(sfxIndex, selectedCell, selectedWaveform);
        Sheet.SetVolume(sfxIndex, selectedCell, selectedVolume);
        Sheet.SetEffect(sfxIndex, selectedCell, selectedEffect);
        Sync();
        _api.sfx(sfxIndex, -1, selectedCell, 1);   // preview just this note
        selectedCell = Math.Min(selectedCell + 1, NoteCount - 1);
    }

    // ── Alt-view grid geometry ─────────────────────────────────────────────────
    private int CellCol(int cell) => cell / GridRows;
    private int CellRow(int cell) => cell % GridRows;
    private int NoteColX(int col) => NoteColStart + col * (NoteColW + NoteColGap);
    private int NoteRowY(int row) => GridTop + row * CellH;
    private Rectangle CellRect(int cell) =>
        new(NoteColX(CellCol(cell)), NoteRowY(CellRow(cell)), NoteColW, CellH);

    private Rectangle PartRect(int cell, int part)
    {
        var r = CellRect(cell);
        return new Rectangle(r.X + PartX[part], r.Y, PartW[part], CellH - 1);
    }

    // Map a click's x within a cell to the nearest part (thresholds fill the gaps between parts).
    private int PartUnderMouse(int cell, int mouseX)
    {
        int relX = mouseX - CellRect(cell).X;
        for (int p = PartCount - 1; p >= 0; p--)
            if (relX >= PartX[p]) return p;
        return PartNote;
    }

    private void UpdateHeader((int x, int y) mouse)
    {
        if (idxPrevBox.Contains(mouse.x, mouse.y) && _api.mouselp()) ChangeIndex(-1);
        else if (idxNextBox.Contains(mouse.x, mouse.y) && _api.mouselp()) ChangeIndex(+1);
        else if (spdBox.Contains(mouse.x, mouse.y))
        {
            if (_api.mouselp() || _api.mouseup()) { Sheet.SetSpeed(sfxIndex, Sheet.GetSpeed(sfxIndex) + 1); Sync(); }
            else if (_api.mouserp() || _api.mousedown()) { Sheet.SetSpeed(sfxIndex, Sheet.GetSpeed(sfxIndex) - 1); Sync(); }
        }
        else if (loopStartBox.Contains(mouse.x, mouse.y))
        {
            if (_api.mouselp() || _api.mouseup()) { Sheet.SetLoopStart(sfxIndex, Sheet.GetLoopStart(sfxIndex) + 1); Sync(); }
            else if (_api.mouserp() || _api.mousedown()) { Sheet.SetLoopStart(sfxIndex, Sheet.GetLoopStart(sfxIndex) - 1); Sync(); }
        }
        else if (loopEndBox.Contains(mouse.x, mouse.y))
        {
            if (_api.mouselp() || _api.mouseup()) { Sheet.SetLoopEnd(sfxIndex, Sheet.GetLoopEnd(sfxIndex) + 1); Sync(); }
            else if (_api.mouserp() || _api.mousedown()) { Sheet.SetLoopEnd(sfxIndex, Sheet.GetLoopEnd(sfxIndex) - 1); Sync(); }
        }
    }

    public void Draw()
    {
        _api.rectfill(0, Constants.GameDataSizes.TileSize,
            Constants.Screen.ResolutionX, Constants.Screen.ResolutionY - 1, Constants.Colors.Black);

        if (AltView)
        {
            DrawAltView();
        }
        else
        {
            DrawPrimaryView();
        }

        _api.rectfill(0, BottomBarY, Constants.Screen.ResolutionX, Constants.Screen.ResolutionY - 1, Constants.Colors.Orange);

        eventNotifier.Draw();
    }

    private void DrawPrimaryView()
    {
        DrawHeader();
        DrawWaveButtons();
        DrawPitchRegion();
        DrawVolumeRegion();
    }

    private void DrawWaveButtons()
    {
        for (int i = 0; i < waveButtons.Length; i++)
        {
            if (i == selectedWaveform)
            {
                var b = waveButtons[i].Bounds;
                _api.rectfill(b.X, b.Y, b.X + b.Width - 1, b.Y + b.Height - 2, PaletteColor(i));
            }
            waveButtons[i].Draw(_api, i == selectedWaveform);
        }
    }

    private void DrawAltView()
    {
        DrawHeader();
        DrawWaveButtons();

        // Octave selector, after the waveform buttons on the header line.
        _api.print("OCT", OctLabelX, HeaderY + 1, Constants.Colors.LightGray);
        for (int i = 0; i < octBoxes.Length; i++)
        {
            bool sel = selectedOctave == i + 1;
            DrawBox(octBoxes[i], (i + 1).ToString(),
                sel ? PaletteColor(i) : Constants.Colors.Indigo,
                sel ? Constants.Colors.Black : Constants.Colors.White);
        }

        // Background bar behind the VOL/FX row.
        _api.rectfill(0, PaletteRowY - 1, Constants.Screen.ResolutionX - 1, PaletteRowY + 7, Constants.Colors.DarkGray);

        // Horizontal volume fader (0 at the left, 7 at the right).
        _api.print("VOL", VolLabelX, PaletteRowY + 1, Constants.Colors.White);
        for (int v = 0; v < volCells.Length; v++)
        {
            var c = volCells[v];
            bool sel = v == selectedVolume;
            int bg = sel ? PaletteColor(v) : Constants.Colors.Indigo;
            int fg = sel ? Constants.Colors.Black : Constants.Colors.White;
            _api.rectfill(c.X, c.Y, c.X + c.Width - 1, c.Y + c.Height - 1, bg);
            _api.print(v.ToString(), c.X + 2, c.Y + 1, fg);
        }

        _api.print("FX", FxLabelX, PaletteRowY + 1, Constants.Colors.White);
        for (int i = 0; i < effectButtons.Length; i++)
        {
            if (i == selectedEffect)
            {
                var b = effectButtons[i].Bounds;
                _api.rectfill(b.X, b.Y, b.X + b.Width - 1, b.Y + b.Height - 2, PaletteColor(i));
            }
            effectButtons[i].Draw(_api, i == selectedEffect);
        }

        for (int cell = 0; cell < NoteCount; cell++)
            DrawNoteCell(cell);
    }

    private void DrawNoteCell(int cell)
    {
        var r = CellRect(cell);
        bool cellSel = cell == selectedCell;
        if (cellSel)
            _api.rectfill(r.X, r.Y, r.X + r.Width - 1, r.Y + r.Height - 1, Constants.Colors.DarkBlue);

        // Highlight the selected part behind its text.
        if (cellSel)
        {
            var pr = PartRect(cell, selectedPart);
            _api.rectfill(pr.X, pr.Y, pr.X + pr.Width - 1, pr.Y + pr.Height - 1, Constants.Colors.White);
        }

        int vol = Sheet.GetVolume(sfxIndex, cell);
        bool active = vol > 0;

        int pitch = Sheet.GetPitch(sfxIndex, cell);
        int oct = pitch / 12;
        int wf = Sheet.GetWaveform(sfxIndex, cell);
        int fx = Sheet.GetEffect(sfxIndex, cell);
        int dim = Constants.Colors.DarkGray;

        DrawPart(cell, PartNote, active ? NoteNames[pitch % 12] : "--", active ? Constants.Colors.Blue : dim);
        DrawPart(cell, PartOct, active ? oct.ToString() : "-", active ? PaletteColor(oct) : dim);
        DrawPart(cell, PartVol, active ? vol.ToString() : "-", active ? PaletteColor(vol) : dim);
        DrawPart(cell, PartWave, active ? wf.ToString() : "-", active ? PaletteColor(wf) : dim);
        DrawPart(cell, PartFx, active ? fx.ToString() : "-", active ? PaletteColor(fx) : dim);
    }

    private void DrawPart(int cell, int part, string text, int color)
    {
        var pr = PartRect(cell, part);
        _api.print(text, pr.X, pr.Y + 3, color);
    }

    private void DrawHeader()
    {
        _api.rectfill(0, HeaderY - 1, Constants.Screen.ResolutionX - 1, HeaderY + 7, Constants.Colors.DarkGray);

        DrawBox(idxPrevBox, "<", Constants.Colors.LightGray, Constants.Colors.Indigo);
        _api.print($"SFX{sfxIndex:D2}", 11, HeaderY + 1, Constants.Colors.White);
        DrawBox(idxNextBox, ">", Constants.Colors.LightGray, Constants.Colors.Indigo);

        _api.print("SPD", 44, HeaderY + 1, Constants.Colors.LightGray);
        DrawBox(spdBox, Sheet.GetSpeed(sfxIndex).ToString("D3"), Constants.Colors.LightGray, Constants.Colors.Indigo);

        _api.print("LP", 76, HeaderY + 1, Constants.Colors.LightGray);
        DrawBox(loopStartBox, Sheet.GetLoopStart(sfxIndex).ToString("D2"), Constants.Colors.LightGray, Constants.Colors.Indigo);
        _api.print("-", 98, HeaderY + 1, Constants.Colors.LightGray);
        DrawBox(loopEndBox, Sheet.GetLoopEnd(sfxIndex).ToString("D2"), Constants.Colors.LightGray, Constants.Colors.Indigo);
    }

    private void DrawBox(Rectangle b, string text, int bg, int fg)
    {
        _api.rectfill(b.X, b.Y, b.X + b.Width - 1, b.Y + b.Height - 1, bg);
        _api.print(text, b.X + 1, b.Y + 1, fg);
    }

    private void DrawPitchRegion()
    {
        _api.rectfill(RegionX, PitchTop, RegionX + RegionW - 1, PitchBottom, Constants.Colors.Black);

        DrawLoopMarkers();

        int playing = mono8.GameAPI.CurrentSfxNote(sfxIndex);
        for (int n = 0; n < NoteCount; n++)
        {
            int vol = Sheet.GetVolume(sfxIndex, n);
            if (vol <= 0) continue;

            int pitch = Sheet.GetPitch(sfxIndex, n);
            int barH = (int)Math.Round(pitch / (float)SfxSheet.MaxPitch * (PitchHeight - 1)) + 1;
            int color = n == playing ? Constants.Colors.White : PaletteColor(Sheet.GetWaveform(sfxIndex, n));
            _api.rectfill(ColLeft(n), PitchBottom - barH + 1, ColRight(n) - BarGap, PitchBottom, color);
        }
    }

    private void DrawLoopMarkers()
    {
        int ls = Sheet.GetLoopStart(sfxIndex);
        int le = Sheet.GetLoopEnd(sfxIndex);
        if (le <= ls) return;

        int lsx = ColLeft(Math.Min(ls, NoteCount));
        int lex = ColLeft(Math.Min(le, NoteCount)) - 1;
        _api.line(lsx, PitchTop, lsx, PitchBottom, Constants.Colors.Green);
        _api.line(lex, PitchTop, lex, PitchBottom, Constants.Colors.Red);
    }

    private void DrawVolumeRegion()
    {
        _api.rectfill(0, VolLabelY, Constants.Screen.ResolutionX, VolLabelY + VolLabelH - 1, Constants.Colors.DarkGray);
        _api.print("VOLUME", 2, VolLabelY + 1, Constants.Colors.White);

        _api.rectfill(RegionX, VolTop, RegionX + RegionW - 1, VolBottom, Constants.Colors.Black);

        int playing = mono8.GameAPI.CurrentSfxNote(sfxIndex);
        for (int n = 0; n < NoteCount; n++)
        {
            int vol = Sheet.GetVolume(sfxIndex, n);
            if (vol <= 0) continue;

            int barH = (int)Math.Round(vol / (float)SfxSheet.MaxVolume * (VolHeight - 1)) + 1;
            int color = n == playing ? Constants.Colors.White : PaletteColor(Sheet.GetWaveform(sfxIndex, n));
            _api.rectfill(ColLeft(n), VolBottom - barH + 1, ColRight(n) - BarGap, VolBottom, color);
        }
    }

    private static int PaletteColor(int index) => PaletteColors[index & 7];
}
