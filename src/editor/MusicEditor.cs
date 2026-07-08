namespace mono8.editor;

/// <summary>
/// PICO-8 style Music editor: a Pattern selector strip, per-pattern loop/stop controls,
/// four toggleable channels (each pointing at an SFX index) and, below every active channel,
/// a 32-note piano-roll of that SFX which edits the shared <see cref="SfxSheet"/> in place.
/// </summary>
internal class MusicEditor : IEditor
{
    private readonly IMono8API _api;
    private readonly EventNotifier eventNotifier;

    // ── Layout ────────────────────────────────────────────────────────────────
    private const int ChannelCount = MusicSheet.ChannelCount;   // 4
    private const int NoteCount = SfxSheet.NotesPerSfx;         // 32
    private const int DefaultPlaceVolume = 5;

    private const int HeaderY = 9;

    // Pattern strip (top-left) with pink scroll arrows on either side.
    private const int VisiblePatterns = 16;
    private const int StripX = 13;
    private const int StripStep = 12;
    private readonly Rectangle prevBox = new(2, HeaderY, 7, 7);
    private readonly Rectangle nextBox = new(205, HeaderY, 7, 7);

    // Loop controls (top-right): loop-start / loop-end / stop icons.
    private const int LoopStartIcon = 55;
    private const int LoopEndIcon = 56;
    private const int StopIcon = 57;
    private readonly Button loopStartButton = new(228, HeaderY, Constants.GameDataSizes.TileSize, LoopStartIcon);
    private readonly Button loopEndButton = new(237, HeaderY, Constants.GameDataSizes.TileSize, LoopEndIcon);
    private readonly Button stopButton = new(246, HeaderY, Constants.GameDataSizes.TileSize, StopIcon);

    // Channel columns.
    private const int ColStride = 63;
    private const int ColInnerW = 58;
    private const int ChannelHeaderY = 20;
    private const int ToggleW = 7;
    private const int SfxNumX = 10;   // sfx number offset from the toggle box
    private const int SfxNumW = 16;

    // Piano-roll note area.
    private const int NoteTop = 30;
    private const int NoteRowH = 3;
    private static readonly int NoteBottom = NoteTop + NoteCount * NoteRowH;

    private static readonly int BottomBarY = Constants.Screen.ResolutionY - Constants.GameDataSizes.TileSize;

    private static readonly int[] PaletteColors =
    {
        Constants.Colors.Red, Constants.Colors.Orange, Constants.Colors.Yellow, Constants.Colors.Green,
        Constants.Colors.Blue, Constants.Colors.Indigo, Constants.Colors.Pink, Constants.Colors.Peach,
    };

    private int patternIndex = 0;
    private int windowStart = 0;

    public MusicEditor(IMono8API api)
    {
        _api = api;
        eventNotifier = new EventNotifier(api, 2f, 1, Constants.Screen.ResolutionY - Constants.GameDataSizes.TileSize + 1);
    }

    public void Init()
    {
    }

    private MusicSheet Music => Mono8API.MusicSheet;
    private SfxSheet Sfx => Mono8API.SfxSheet;

    private void SyncPattern() => mono8.GameAPI.SyncMusic(patternIndex);
    private void SyncSfx(int sfx) => mono8.GameAPI.SyncSfx(sfx);

    private static int ColX(int c) => 2 + c * ColStride;

    // ── Update ──────────────────────────────────────────────────────────────────

    public void Update(float elapsedSeconds)
    {
        eventNotifier.Update(elapsedSeconds);

        if (KeybrdInput.IsSaveShortcutPressed())
        {
            mono8.GameAPI.Save();
            eventNotifier.AddEvent("SAVED");
        }

        // Play the current pattern (or stop if music is already playing).
        if (KeybrdInput.JustPressed(Keys.Space))
        {
            if (mono8.GameAPI.CurrentMusicPattern() >= 0) _api.music(-1);
            else _api.music(patternIndex);
        }

        if (KeybrdInput.JustPressed(Keys.Left)) SelectPattern(patternIndex - 1);
        if (KeybrdInput.JustPressed(Keys.Right)) SelectPattern(patternIndex + 1);

        var mouse = _api.mousexy();
        UpdateHeader(mouse);
        UpdateChannels(mouse);
        UpdateNoteColumns(mouse);

        ScrollWindowToSelection();
    }

    private void SelectPattern(int index)
    {
        patternIndex = Math.Clamp(index, 0, MusicSheet.Count - 1);
    }

    private void ScrollWindowToSelection()
    {
        if (patternIndex < windowStart) windowStart = patternIndex;
        else if (patternIndex >= windowStart + VisiblePatterns) windowStart = patternIndex - VisiblePatterns + 1;
        windowStart = Math.Clamp(windowStart, 0, MusicSheet.Count - VisiblePatterns);
    }

    private void UpdateHeader((int x, int y) mouse)
    {
        if (prevBox.Contains(mouse.x, mouse.y) && _api.mouselp()) SelectPattern(patternIndex - 1);
        else if (nextBox.Contains(mouse.x, mouse.y) && _api.mouselp()) SelectPattern(patternIndex + 1);

        // Click a visible pattern number to jump to it.
        for (int i = 0; i < VisiblePatterns; i++)
        {
            var box = new Rectangle(StripX + i * StripStep, HeaderY, StripStep, 7);
            if (box.Contains(mouse.x, mouse.y) && _api.mouselp())
            {
                SelectPattern(windowStart + i);
                break;
            }
        }

        // Loop / stop flags for the current pattern.
        if (loopStartButton.IsClicked(_api, mouse)) { Music.ToggleLoopStart(patternIndex); SyncPattern(); }
        if (loopEndButton.IsClicked(_api, mouse)) { Music.ToggleLoopEnd(patternIndex); SyncPattern(); }
        if (stopButton.IsClicked(_api, mouse)) { Music.ToggleStop(patternIndex); SyncPattern(); }
    }

    private void UpdateChannels((int x, int y) mouse)
    {
        for (int c = 0; c < ChannelCount; c++)
        {
            int cx = ColX(c);
            var toggle = new Rectangle(cx, ChannelHeaderY, ToggleW, ToggleW);
            if (toggle.Contains(mouse.x, mouse.y) && _api.mouselp())
            {
                Music.ToggleChannel(patternIndex, c);
                SyncPattern();
                continue;
            }

            if (!Music.IsChannelOn(patternIndex, c)) continue;

            // Left-click raises the SFX number, right-click lowers it.
            var num = new Rectangle(cx + SfxNumX, ChannelHeaderY, SfxNumW, ToggleW);
            if (num.Contains(mouse.x, mouse.y))
            {
                if (_api.mouselp()) { Music.ChangeChannelSfx(patternIndex, c, +1); SyncPattern(); }
                else if (_api.mouserp()) { Music.ChangeChannelSfx(patternIndex, c, -1); SyncPattern(); }
            }
        }
    }

    private void UpdateNoteColumns((int x, int y) mouse)
    {
        for (int c = 0; c < ChannelCount; c++)
        {
            if (!Music.IsChannelOn(patternIndex, c)) continue;

            int cx = ColX(c);
            var region = new Rectangle(cx, NoteTop, ColInnerW, NoteCount * NoteRowH);
            if (!region.Contains(mouse.x, mouse.y)) continue;

            int sfx = Music.GetChannelSfx(patternIndex, c);
            int n = Math.Clamp((mouse.y - NoteTop) / NoteRowH, 0, NoteCount - 1);

            if (_api.mousel())
            {
                int pitch = Math.Clamp((mouse.x - cx) * SfxSheet.MaxPitch / (ColInnerW - 1), 0, SfxSheet.MaxPitch);
                Sfx.SetPitch(sfx, n, pitch);
                if (Sfx.GetVolume(sfx, n) == 0) Sfx.SetVolume(sfx, n, DefaultPlaceVolume);
                SyncSfx(sfx);
            }
            else if (_api.mouser())
            {
                Sfx.ClearNote(sfx, n);
                SyncSfx(sfx);
            }
        }
    }

    // ── Draw ─────────────────────────────────────────────────────────────────────

    public void Draw()
    {
        _api.rectfill(0, Constants.GameDataSizes.TileSize,
            Constants.Screen.ResolutionX, Constants.Screen.ResolutionY - 1, Constants.Colors.Black);

        DrawHeader();
        DrawChannels();

        _api.rectfill(0, BottomBarY, Constants.Screen.ResolutionX, Constants.Screen.ResolutionY - 1, Constants.Colors.Orange);
        eventNotifier.Draw();
    }

    private void DrawHeader()
    {
        _api.rectfill(0, HeaderY - 1, Constants.Screen.ResolutionX - 1, HeaderY + 7, Constants.Colors.DarkGray);

        // Pink scroll arrows.
        DrawArrow(prevBox, "<");
        DrawArrow(nextBox, ">");

        int playing = mono8.GameAPI.CurrentMusicPattern();
        for (int i = 0; i < VisiblePatterns; i++)
        {
            int p = windowStart + i;
            int x = StripX + i * StripStep;
            bool selected = p == patternIndex;

            if (p == playing)
                _api.rectfill(x - 1, HeaderY, x + 8, HeaderY + 6, Constants.Colors.Red);

            int color = selected ? Constants.Colors.White : Constants.Colors.LightGray;
            _api.print(p.ToString("D2"), x, HeaderY + 1, color);
        }

        // Loop / stop controls: highlighted when set on the current pattern.
        DrawLoopButton(loopStartButton, Music.IsLoopStart(patternIndex));
        DrawLoopButton(loopEndButton, Music.IsLoopEnd(patternIndex));
        DrawLoopButton(stopButton, Music.IsStop(patternIndex));
    }

    private void DrawArrow(Rectangle b, string text)
    {
        _api.print(text, b.X, b.Y + 1, Constants.Colors.Pink);
    }

    private void DrawLoopButton(Button button, bool active)
    {
        if (active)
        {
            var b = button.Bounds;
            _api.rectfill(b.X, b.Y, b.X + b.Width - 1, b.Y + b.Height - 2, Constants.Colors.Green);
        }
        button.Draw(_api, active);
    }

    private void DrawChannels()
    {
        for (int c = 0; c < ChannelCount; c++)
        {
            int cx = ColX(c);
            bool on = Music.IsChannelOn(patternIndex, c);

            // Toggle box (lit green when the channel is on).
            int boxBg = on ? Constants.Colors.Green : Constants.Colors.DarkGray;
            _api.rectfill(cx, ChannelHeaderY, cx + ToggleW - 1, ChannelHeaderY + ToggleW - 1, boxBg);

            if (!on) continue;

            int sfx = Music.GetChannelSfx(patternIndex, c);
            _api.print(sfx.ToString("D2"), cx + SfxNumX, ChannelHeaderY + 1, Constants.Colors.White);

            DrawNoteColumn(c, cx, sfx);
        }
    }

    private void DrawNoteColumn(int channel, int cx, int sfx)
    {
        _api.rectfill(cx, NoteTop, cx + ColInnerW - 1, NoteBottom - 1, Constants.Colors.Black);

        int playing = mono8.GameAPI.CurrentSfxNote(sfx);
        for (int n = 0; n < NoteCount; n++)
        {
            int y = NoteTop + n * NoteRowH;

            if (n == playing)
                _api.rectfill(cx, y, cx + ColInnerW - 1, y + NoteRowH - 2, Constants.Colors.DarkBlue);

            int vol = Sfx.GetVolume(sfx, n);
            if (vol <= 0) continue;

            int pitch = Sfx.GetPitch(sfx, n);
            int barW = (int)Math.Round(pitch / (float)SfxSheet.MaxPitch * (ColInnerW - 1)) + 1;
            int color = n == playing ? Constants.Colors.White : PaletteColor(Sfx.GetWaveform(sfx, n));
            _api.rectfill(cx, y, cx + barW - 1, y + NoteRowH - 2, color);
        }
    }

    private static int PaletteColor(int index) => PaletteColors[index & 7];
}
