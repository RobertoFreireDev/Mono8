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

    private static readonly int[] WaveColors =
    {
        Constants.Colors.Blue, Constants.Colors.Green, Constants.Colors.Yellow, Constants.Colors.Orange,
        Constants.Colors.Red, Constants.Colors.Pink, Constants.Colors.Indigo, Constants.Colors.White,
    };

    // ── Alternate (tracker) view ───────────────────────────────────────────────
    private const int OctaveCount = 4;           // selectable base octaves 1..4
    private const int EffectCount = 8;           // effects 0..7
    private const int EffectIconStart = 38;      // effect icons occupy icon indices 38..45
    private const int GridRows = 8;
    private const int GridCols = 4;              // 8 rows x 4 cols = 32 notes
    private const int CellH = 12;
    private const int GridTop = 37;
    private const int PaletteLabelY = 28;
    private const int VolFaderX = 2;
    private const int VolFaderW = 13;
    private const int FxColX = 18;
    private const int NoteColStart = 34;
    private const int NoteColGap = 6;
    private static readonly int NoteColW =
        (Constants.Screen.ResolutionX - NoteColStart - 2 - (GridCols - 1) * NoteColGap) / GridCols;

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
    private readonly Rectangle playBox = new(206, HeaderY, 20, 7);
    private readonly Rectangle stopBox = new(228, HeaderY, 20, 7);

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
            waveButtons[i] = new Button(2 + i * (Constants.GameDataSizes.TileSize + 1), WaveY,
                Constants.GameDataSizes.TileSize, WaveformIconStart + i);
        }

        octBoxes = new Rectangle[OctaveCount];
        for (int i = 0; i < OctaveCount; i++)
            octBoxes[i] = new Rectangle(94 + i * 9, WaveY, 7, 7);

        volCells = new Rectangle[SfxSheet.MaxVolume + 1];
        for (int v = 0; v <= SfxSheet.MaxVolume; v++)
            volCells[v] = new Rectangle(VolFaderX, GridTop + (SfxSheet.MaxVolume - v) * CellH, VolFaderW, CellH - 1);

        effectButtons = new Button[EffectCount];
        for (int i = 0; i < EffectCount; i++)
            effectButtons[i] = new Button(FxColX, GridTop + i * CellH + 2,
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
            Sync();
            _api.sfx(sfxIndex);
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

        // Note grid: left-click selects the cursor cell, right-click clears a note.
        for (int cell = 0; cell < NoteCount; cell++)
        {
            if (!CellRect(cell).Contains(mouse.x, mouse.y)) continue;
            if (_api.mouselp()) selectedCell = cell;
            else if (_api.mouserp()) { Sheet.ClearNote(sfxIndex, cell); Sync(); }
        }

        // Cursor navigation (down a column, then across to the next column).
        if (KeybrdInput.JustPressed(Keys.Up)) selectedCell = Math.Max(0, selectedCell - 1);
        if (KeybrdInput.JustPressed(Keys.Down)) selectedCell = Math.Min(NoteCount - 1, selectedCell + 1);
        if (KeybrdInput.JustPressed(Keys.Left)) selectedCell = Math.Max(0, selectedCell - GridRows);
        if (KeybrdInput.JustPressed(Keys.Right)) selectedCell = Math.Min(NoteCount - 1, selectedCell + GridRows);

        if (KeybrdInput.JustPressed(Keys.Delete) || KeybrdInput.JustPressed(Keys.Back))
        {
            Sheet.ClearNote(sfxIndex, selectedCell);
            Sync();
            selectedCell = Math.Min(selectedCell + 1, NoteCount - 1);
        }

        // Piano keys enter a note at the current octave/waveform/volume/effect.
        foreach (var (key, semitone) in PianoKeys)
        {
            if (KeybrdInput.JustPressed(key)) { EnterNote(semitone); break; }
        }
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

    private static string NoteLabel(int pitch) => NoteNames[pitch % 12] + (pitch / 12);

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
        else if (playBox.Contains(mouse.x, mouse.y) && _api.mouselp())
        {
            Sync();
            _api.sfx(sfxIndex);
        }
        else if (stopBox.Contains(mouse.x, mouse.y) && _api.mouselp())
        {
            _api.sfx(-1);
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

        foreach (var button in waveButtons)
        {
            // highlight the selected waveform (Button dims non-selected via pal white->indigo)
            button.Draw(_api, Array.IndexOf(waveButtons, button) == selectedWaveform);
        }

        DrawPitchRegion();
        DrawVolumeRegion();
        DrawPlayhead();
    }

    // White outline around the note the audio engine is currently on. Reading the engine's
    // note index (instead of a frame counter) keeps it in sync at any FPS or SPD. Drawn last,
    // full region height, so it shows over both bars and silent (bar-less) notes.
    private void DrawPlayhead()
    {
        int playing = mono8.GameAPI.CurrentSfxNote(sfxIndex);
        if (playing < 0 || playing >= NoteCount) return;

        int x0 = ColLeft(playing);
        int x1 = ColRight(playing);
        _api.rect(x0, PitchTop, x1, PitchBottom, Constants.Colors.White);
        _api.rect(x0, VolTop, x1, VolBottom, Constants.Colors.White);
    }

    private void DrawAltView()
    {
        DrawHeader();

        for (int i = 0; i < waveButtons.Length; i++)
            waveButtons[i].Draw(_api, i == selectedWaveform);

        // Octave selector, on the same line as the waveforms.
        _api.print("OCT", 78, WaveY + 1, Constants.Colors.LightGray);
        for (int i = 0; i < octBoxes.Length; i++)
        {
            bool sel = selectedOctave == i + 1;
            DrawBox(octBoxes[i], (i + 1).ToString(),
                sel ? Constants.Colors.Green : Constants.Colors.Indigo,
                sel ? Constants.Colors.Black : Constants.Colors.White);
        }

        // Vertical volume fader (7 at the top, 0 at the bottom).
        _api.print("VOL", VolFaderX, PaletteLabelY, Constants.Colors.White);
        for (int v = 0; v < volCells.Length; v++)
        {
            var c = volCells[v];
            int bg = v == selectedVolume ? Constants.Colors.Green
                   : v < selectedVolume ? Constants.Colors.DarkGray
                   : Constants.Colors.Indigo;
            int fg = v == selectedVolume ? Constants.Colors.Black : Constants.Colors.White;
            _api.rectfill(c.X, c.Y, c.X + c.Width - 1, c.Y + c.Height - 1, bg);
            _api.print(v.ToString(), c.X + 5, c.Y + 3, fg);
        }

        // Effect selector (icons 38..45).
        _api.print("FX", FxColX, PaletteLabelY, Constants.Colors.White);
        for (int i = 0; i < effectButtons.Length; i++)
            effectButtons[i].Draw(_api, i == selectedEffect);

        for (int cell = 0; cell < NoteCount; cell++)
            DrawNoteCell(cell);
    }

    private void DrawNoteCell(int cell)
    {
        var r = CellRect(cell);
        if (cell == selectedCell)
            _api.rectfill(r.X, r.Y, r.X + r.Width - 1, r.Y + r.Height - 1, Constants.Colors.DarkBlue);

        int tx = r.X + 3;
        int ty = r.Y + 3;

        int vol = Sheet.GetVolume(sfxIndex, cell);
        if (vol <= 0)
        {
            _api.print("---", tx, ty, Constants.Colors.DarkGray);
            return;
        }

        int pitch = Sheet.GetPitch(sfxIndex, cell);
        int wf = Sheet.GetWaveform(sfxIndex, cell);
        _api.print(NoteLabel(pitch), tx, ty, Constants.Colors.White);   // e.g. "G#1"
        _api.print(wf.ToString(), tx + 12, ty, WaveColor(wf));          // waveform digit
        _api.print(vol.ToString(), tx + 16, ty, Constants.Colors.LightGray);
    }

    private void DrawHeader()
    {
        DrawBox(idxPrevBox, "<", Constants.Colors.LightGray, Constants.Colors.Indigo);
        _api.print($"SFX{sfxIndex:D2}", 11, HeaderY + 1, Constants.Colors.White);
        DrawBox(idxNextBox, ">", Constants.Colors.LightGray, Constants.Colors.Indigo);

        _api.print("SPD", 44, HeaderY + 1, Constants.Colors.LightGray);
        DrawBox(spdBox, Sheet.GetSpeed(sfxIndex).ToString("D3"), Constants.Colors.LightGray, Constants.Colors.Indigo);

        _api.print("LP", 76, HeaderY + 1, Constants.Colors.LightGray);
        DrawBox(loopStartBox, Sheet.GetLoopStart(sfxIndex).ToString("D2"), Constants.Colors.LightGray, Constants.Colors.Indigo);
        _api.print("-", 98, HeaderY + 1, Constants.Colors.LightGray);
        DrawBox(loopEndBox, Sheet.GetLoopEnd(sfxIndex).ToString("D2"), Constants.Colors.LightGray, Constants.Colors.Indigo);

        DrawBox(playBox, "PLAY", Constants.Colors.Green, Constants.Colors.Black);
        DrawBox(stopBox, "STOP", Constants.Colors.Red, Constants.Colors.Black);
    }

    private void DrawBox(Rectangle b, string text, int bg, int fg)
    {
        _api.rectfill(b.X, b.Y, b.X + b.Width - 1, b.Y + b.Height - 1, bg);
        _api.print(text, b.X + 1, b.Y + 1, fg);
    }

    private void DrawPitchRegion()
    {
        _api.rectfill(RegionX, PitchTop, RegionX + RegionW - 1, PitchBottom, Constants.Colors.Black);

        var mouse = _api.mousexy();
        if (pitchRegion.Contains(mouse.x, mouse.y))
        {
            int n = NoteUnderMouse(mouse.x);
            _api.rectfill(ColLeft(n), PitchTop, ColRight(n), PitchBottom, Constants.Colors.DarkBlue);
        }

        DrawLoopMarkers();

        for (int n = 0; n < NoteCount; n++)
        {
            int vol = Sheet.GetVolume(sfxIndex, n);
            if (vol <= 0) continue;

            int pitch = Sheet.GetPitch(sfxIndex, n);
            int barH = (int)Math.Round(pitch / (float)SfxSheet.MaxPitch * (PitchHeight - 1)) + 1;
            _api.rectfill(ColLeft(n), PitchBottom - barH + 1, ColRight(n) - BarGap, PitchBottom, WaveColor(Sheet.GetWaveform(sfxIndex, n)));
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

        for (int n = 0; n < NoteCount; n++)
        {
            int vol = Sheet.GetVolume(sfxIndex, n);
            if (vol <= 0) continue;

            int barH = (int)Math.Round(vol / (float)SfxSheet.MaxVolume * (VolHeight - 1)) + 1;
            _api.rectfill(ColLeft(n), VolBottom - barH + 1, ColRight(n) - BarGap, VolBottom, WaveColor(Sheet.GetWaveform(sfxIndex, n)));
        }
    }

    private static int WaveColor(int waveform) => WaveColors[waveform & 7];
}
