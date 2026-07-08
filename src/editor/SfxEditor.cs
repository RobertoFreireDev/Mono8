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

        if (KeybrdInput.JustPressed(Keys.Left)) ChangeIndex(-1);
        if (KeybrdInput.JustPressed(Keys.Right)) ChangeIndex(+1);
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

    // The alternate view — to be built next.
    private void UpdateAltView()
    {
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
    }

    // The alternate view — to be built next.
    private void DrawAltView()
    {
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
