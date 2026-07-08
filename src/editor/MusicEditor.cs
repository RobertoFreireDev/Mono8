namespace mono8.editor;

/// <summary>
/// PICO-8 style Music editor: a pattern bank where each pattern plays up to four SFX at once,
/// a scrollable pattern-index strip, loop/stop controls, per-channel on/off toggles with SFX
/// selectors, and a tracker-style note editor for the SFX referenced by each active channel.
/// </summary>
internal class MusicEditor : IEditor
{
    private readonly IMono8API _api;
    private readonly EventNotifier eventNotifier;
    private static readonly int BottomBarY = Constants.Screen.ResolutionY - Constants.GameDataSizes.TileSize;

    // ── Pattern-index strip ─────────────────────────────────────────────────────
    private const int HeaderY = 9;
    private const int VisiblePatterns = 5;
    private const int PatBoxW = 13;
    private const int PatBoxH = 7;
    private const int PatBoxGap = 1;
    private const int PatBoxStartX = 11;   // right after the "<" arrow
    private readonly Rectangle prevArrow = new(2, HeaderY, 7, 7);
    private readonly Rectangle nextArrow = new(PatBoxStartX + VisiblePatterns * (PatBoxW + PatBoxGap), HeaderY, 7, 7);

    // ── Loop controls (top-right) ───────────────────────────────────────────────
    private const int LoopStartIcon = 55;
    private const int LoopEndIcon = 56;
    private const int StopIcon = 57;
    private const int PencilIcon = 25;
    private readonly Button loopStartBtn;
    private readonly Button loopEndBtn;
    private readonly Button stopBtn;

    // ── Channel columns ─────────────────────────────────────────────────────────
    private const int ChannelCount = MusicSheet.ChannelCount;   // 4
    private const int ChannelHeaderY = 19;
    private const int ColStart = 4;
    private const int ColW = 52;
    private const int ColGap = 8;
    private const int ToggleW = 7;
    private const int SfxNumX = 9;    // relative to column left
    private const int SfxNumW = 11;
    private const int PencilX = 22;   // relative to column left

    // ── Note grid ───────────────────────────────────────────────────────────────
    private const int NoteCount = SfxSheet.NotesPerSfx;   // 32
    private const int VisibleRows = 12;
    private const int NoteTop = 28;
    private const int NoteCellH = 9;   // 12 rows * 9 = 108 -> ends exactly at the bottom bar

    // Note-cell parts (identical semantics to the SFX editor's alternate view).
    private const int PartNote = 0;   // note name (piano keys)
    private const int PartOct = 1;   // octave (digit)
    private const int PartWave = 2;   // waveform (digit)
    private const int PartVol = 3;   // volume (digit)
    private const int PartFx = 4;   // effect (digit)
    private const int PartCount = 5;
    private static readonly int[] PartX = { 3, 11, 19, 25, 31 };
    private static readonly int[] PartW = { 8, 5, 6, 6, 6 };

    private const int DefaultOctave = 2;
    private const int DefaultVolume = 5;

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

    // ── State ───────────────────────────────────────────────────────────────────
    private int patternIndex = 0;
    private int viewStart = 0;
    private int selectedChannel = 0;
    private int selectedCell = 0;
    private int selectedPart = PartNote;
    private readonly int[] scroll = new int[ChannelCount];   // each column scrolls independently
    private int playStartPattern = -1;

    public MusicEditor(IMono8API api)
    {
        _api = api;
        eventNotifier = new EventNotifier(api, 2f, 1, Constants.Screen.ResolutionY - Constants.GameDataSizes.TileSize + 1);

        int size = Constants.GameDataSizes.TileSize;
        int loopY = HeaderY - 1;
        int startX = Constants.Screen.ResolutionX - 2 - (3 * size + 2);
        loopStartBtn = new Button(startX, loopY, size, LoopStartIcon);
        loopEndBtn = new Button(startX + size + 1, loopY, size, LoopEndIcon);
        stopBtn = new Button(startX + 2 * (size + 1), loopY, size, StopIcon);
    }

    public void Init()
    {
    }

    private MusicSheet Music => Mono8API.MusicSheet;
    private SfxSheet Sfx => Mono8API.SfxSheet;

    private int ChannelSfx(int c) => Music.GetChannelSfx(patternIndex, c);

    private void SyncMusic() => mono8.GameAPI.SyncMusic(patternIndex);
    private void SyncAllMusic()
    {
        for (int p = 0; p < MusicSheet.Count; p++) mono8.GameAPI.SyncMusic(p);
    }
    private void SyncSfx(int sfx) => mono8.GameAPI.SyncSfx(sfx);
    private int Playing => mono8.GameAPI.CurrentMusicPattern();

    // ── Geometry helpers ────────────────────────────────────────────────────────
    private static int ColX(int c) => ColStart + c * (ColW + ColGap);
    private Rectangle ToggleRect(int c) => new(ColX(c), ChannelHeaderY, ToggleW, ToggleW);
    private Rectangle SfxNumRect(int c) => new(ColX(c) + SfxNumX, ChannelHeaderY, SfxNumW, 7);
    private Rectangle PencilRect(int c) => new(ColX(c) + PencilX, ChannelHeaderY, Constants.GameDataSizes.TileSize, Constants.GameDataSizes.TileSize);
    private Rectangle ColumnRect(int c) => new(ColX(c), NoteTop, ColW, VisibleRows * NoteCellH);
    private Rectangle CellRect(int c, int visRow) => new(ColX(c), NoteTop + visRow * NoteCellH, ColW, NoteCellH);
    private Rectangle PatBox(int slot) => new(PatBoxStartX + slot * (PatBoxW + PatBoxGap), HeaderY, PatBoxW, PatBoxH);

    private int PartUnderMouse(int c, int mouseX)
    {
        int relX = mouseX - ColX(c);
        for (int p = PartCount - 1; p >= 0; p--)
            if (relX >= PartX[p]) return p;
        return PartNote;
    }

    // ── Update ──────────────────────────────────────────────────────────────────
    public void Update(float elapsedSeconds)
    {
        eventNotifier.Update(elapsedSeconds);

        if (KeybrdInput.IsSaveShortcutPressed())
        {
            mono8.GameAPI.Save();
            eventNotifier.AddEvent("SAVED");
        }

        if (KeybrdInput.JustPressed(Keys.Space)) TogglePlayback();

        var mouse = _api.mousexy();
        UpdatePatternStrip(mouse);
        UpdateLoopControls(mouse);
        UpdateChannels(mouse);
        UpdateNoteGrid(mouse);
        UpdateKeyboard();
        UpdatePlaybackScroll();
        UpdatePlaybackPattern();
    }

    // While playing, follow the engine's current pattern so the selected box and the
    // visible strip window advance along with playback instead of staying pinned to
    // wherever the user last clicked.
    private void UpdatePlaybackPattern()
    {
        if (Playing < 0 || Playing == patternIndex) return;
        patternIndex = Playing;
        if (patternIndex < viewStart) viewStart = patternIndex;
        else if (patternIndex > viewStart + VisiblePatterns - 1) viewStart = patternIndex - (VisiblePatterns - 1);
        viewStart = Math.Clamp(viewStart, 0, MusicSheet.Count - VisiblePatterns);
    }

    // While playing, each column independently follows its own SFX playhead: it stays
    // anchored at the top until the sounding note reaches the middle row, then scrolls
    // down to keep that note pinned to mid height. When the SFX loops back the note
    // returns near the top and the column re-centers as it passes the middle again.
    private void UpdatePlaybackScroll()
    {
        if (Playing < 0) return;

        for (int c = 0; c < ChannelCount; c++)
        {
            if (!Music.IsChannelOn(patternIndex, c)) continue;

            int playNote = mono8.GameAPI.CurrentSfxNote(ChannelSfx(c));
            if (playNote < 0) continue;

            scroll[c] = Math.Clamp(playNote - VisibleRows / 2, 0, NoteCount - VisibleRows);
        }
    }

    private void TogglePlayback()
    {
        if (Playing >= 0)
        {
            _api.music(-1);
            playStartPattern = -1;
        }
        else
        {
            SyncAllMusic();
            playStartPattern = patternIndex;
            Array.Clear(scroll, 0, scroll.Length);   // start each column at the first note of its SFX
            _api.music(patternIndex);
        }
    }

    private void ChangePattern(int delta)
    {
        patternIndex = Math.Clamp(patternIndex + delta, 0, MusicSheet.Count - 1);
        if (patternIndex < viewStart) viewStart = patternIndex;
        else if (patternIndex > viewStart + VisiblePatterns - 1) viewStart = patternIndex - (VisiblePatterns - 1);
        viewStart = Math.Clamp(viewStart, 0, MusicSheet.Count - VisiblePatterns);
    }

    private void UpdatePatternStrip((int x, int y) mouse)
    {
        if (prevArrow.Contains(mouse.x, mouse.y) && _api.mouselp()) { ChangePattern(-1); return; }
        if (nextArrow.Contains(mouse.x, mouse.y) && _api.mouselp()) { ChangePattern(+1); return; }

        for (int slot = 0; slot < VisiblePatterns; slot++)
        {
            int p = viewStart + slot;
            if (p >= MusicSheet.Count) break;
            if (PatBox(slot).Contains(mouse.x, mouse.y) && _api.mouselp())
            {
                patternIndex = p;
                return;
            }
        }
    }

    private void UpdateLoopControls((int x, int y) mouse)
    {
        if (loopStartBtn.IsClicked(_api, mouse)) { Music.ToggleLoopStart(patternIndex); SyncMusic(); }
        else if (loopEndBtn.IsClicked(_api, mouse)) { Music.ToggleLoopEnd(patternIndex); SyncMusic(); }
        else if (stopBtn.IsClicked(_api, mouse)) { Music.ToggleStop(patternIndex); SyncMusic(); }
    }

    private void UpdateChannels((int x, int y) mouse)
    {
        for (int c = 0; c < ChannelCount; c++)
        {
            if (ToggleRect(c).Contains(mouse.x, mouse.y) && _api.mouselp())
            {
                Music.ToggleChannel(patternIndex, c);
                selectedChannel = c;
                SyncMusic();
                return;
            }

            if (!Music.IsChannelOn(patternIndex, c)) continue;

            if (SfxNumRect(c).Contains(mouse.x, mouse.y))
            {
                if (_api.mouselp()) { selectedChannel = c; Music.ChangeChannelSfx(patternIndex, c, +1); SyncMusic(); return; }
                if (_api.mouserp()) { selectedChannel = c; Music.ChangeChannelSfx(patternIndex, c, -1); SyncMusic(); return; }
            }

            if (PencilRect(c).Contains(mouse.x, mouse.y) && _api.mouselp())
            {
                OpenSfxEditor(ChannelSfx(c));
                return;
            }
        }
    }

    private void UpdateNoteGrid((int x, int y) mouse)
    {
        for (int c = 0; c < ChannelCount; c++)
        {
            if (!Music.IsChannelOn(patternIndex, c)) continue;

            // Mouse wheel over a column scrolls that column's 32-note list.
            if (ColumnRect(c).Contains(mouse.x, mouse.y))
            {
                if (_api.mouseup()) scroll[c] = Math.Max(0, scroll[c] - 1);
                if (_api.mousedown()) scroll[c] = Math.Min(NoteCount - VisibleRows, scroll[c] + 1);
            }

            int sfx = ChannelSfx(c);
            for (int r = 0; r < VisibleRows; r++)
            {
                int note = scroll[c] + r;
                if (note >= NoteCount) break;
                if (!CellRect(c, r).Contains(mouse.x, mouse.y)) continue;

                if (_api.mouselp())
                {
                    selectedChannel = c;
                    selectedCell = note;
                    selectedPart = PartUnderMouse(c, mouse.x);
                }
                else if (_api.mouserp())
                {
                    Sfx.ClearNote(sfx, note);
                    SyncSfx(sfx);
                }
            }
        }
    }

    private void UpdateKeyboard()
    {
        if (!Music.IsChannelOn(patternIndex, selectedChannel)) return;
        int sfx = ChannelSfx(selectedChannel);

        if (KeybrdInput.JustPressed(Keys.Up)) { selectedCell = Math.Max(0, selectedCell - 1); EnsureCellVisible(); }
        if (KeybrdInput.JustPressed(Keys.Down)) { selectedCell = Math.Min(NoteCount - 1, selectedCell + 1); EnsureCellVisible(); }
        if (KeybrdInput.JustPressed(Keys.Left)) selectedPart = Math.Max(0, selectedPart - 1);
        if (KeybrdInput.JustPressed(Keys.Right)) selectedPart = Math.Min(PartCount - 1, selectedPart + 1);

        if (KeybrdInput.JustPressed(Keys.Delete) || KeybrdInput.JustPressed(Keys.Back))
        {
            Sfx.ClearNote(sfx, selectedCell);
            SyncSfx(sfx);
            AdvanceCell();
            return;
        }

        if (selectedPart == PartNote)
        {
            foreach (var (key, semitone) in PianoKeys)
                if (KeybrdInput.JustPressed(key)) { EnterNote(sfx, semitone); break; }
        }
        else
        {
            int digit = JustPressedDigit();
            if (digit >= 0) SetPartValue(sfx, selectedPart, digit);
        }
    }

    private static int JustPressedDigit()
    {
        for (int d = 0; d <= 9; d++)
            if (KeybrdInput.JustPressed(Keys.D0 + d) || KeybrdInput.JustPressed(Keys.NumPad0 + d))
                return d;
        return -1;
    }

    private void EnterNote(int sfx, int semitone)
    {
        int pitch = Math.Clamp(DefaultOctave * 12 + semitone, 0, SfxSheet.MaxPitch);
        Sfx.SetPitch(sfx, selectedCell, pitch);
        if (Sfx.GetVolume(sfx, selectedCell) == 0)
            Sfx.SetVolume(sfx, selectedCell, DefaultVolume);
        SyncSfx(sfx);
        _api.sfx(sfx, -1, selectedCell, 1);
        AdvanceCell();
    }

    private void SetPartValue(int sfx, int part, int digit)
    {
        switch (part)
        {
            case PartOct:
                if (digit < 1 || digit > SfxSheet.MaxPitch / 12) return;
                int pitch = Sfx.GetPitch(sfx, selectedCell);
                Sfx.SetPitch(sfx, selectedCell, Math.Clamp(digit * 12 + pitch % 12, 0, SfxSheet.MaxPitch));
                break;
            case PartVol:
                if (digit > SfxSheet.MaxVolume) return;
                Sfx.SetVolume(sfx, selectedCell, digit);
                break;
            case PartWave:
                if (digit >= SfxSheet.WaveformCount) return;
                Sfx.SetWaveform(sfx, selectedCell, digit);
                break;
            case PartFx:
                if (digit > SfxSheet.MaxEffect) return;
                Sfx.SetEffect(sfx, selectedCell, digit);
                break;
            default:
                return;
        }
        SyncSfx(sfx);
        _api.sfx(sfx, -1, selectedCell, 1);
        AdvanceCell();
    }

    private void AdvanceCell()
    {
        selectedCell = Math.Min(selectedCell + 1, NoteCount - 1);
        EnsureCellVisible();
    }

    // Keep the selected note pinned to mid height of its own column once it scrolls
    // past the middle row; above the middle the column stays anchored at the top.
    private void EnsureCellVisible()
    {
        scroll[selectedChannel] = Math.Clamp(selectedCell - VisibleRows / 2, 0, NoteCount - VisibleRows);
    }

    private void OpenSfxEditor(int sfx)
    {
        var entries = Mono8API.Editors.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].Editor is SfxEditor sfxEditor)
            {
                sfxEditor.OpenSfx(sfx);
                Mono8API.Editors.SwitchTo(i);
                return;
            }
        }
    }

    // ── Draw ────────────────────────────────────────────────────────────────────
    public void Draw()
    {
        // Dark grey backdrop over everything between the menu bar and the bottom bar.
        _api.rectfill(0, Constants.GameDataSizes.TileSize, Constants.Screen.ResolutionX, BottomBarY - 1, Constants.Colors.DarkGray);

        DrawPatternStrip();
        DrawLoopControls();
        DrawChannels();

        _api.rectfill(0, BottomBarY, Constants.Screen.ResolutionX, Constants.Screen.ResolutionY - 1, Constants.Colors.Orange);
        eventNotifier.Draw();
    }

    private void DrawPatternStrip()
    {
        DrawBox(prevArrow, "<", Constants.Colors.LightGray, Constants.Colors.Indigo);
        DrawBox(nextArrow, ">", Constants.Colors.LightGray, Constants.Colors.Indigo);

        int playing = Playing;
        for (int slot = 0; slot < VisiblePatterns; slot++)
        {
            int p = viewStart + slot;
            if (p >= MusicSheet.Count) break;

            var box = PatBox(slot);
            bool selected = p == patternIndex;
            _api.rectfill(box.X, box.Y, box.X + box.Width - 1, box.Y + box.Height - 1,
                selected ? Constants.Colors.White : Constants.Colors.LightGray);

            int fg = p == playStartPattern ? Constants.Colors.Green : Constants.Colors.Black;
            _api.print(p.ToString("D2"), box.X + 2, box.Y + 1, fg);

            // The currently playing pattern lights up white just below its number.
            if (p == playing)
                _api.rectfill(box.X, box.Y + box.Height + 1, box.X + box.Width - 1, box.Y + box.Height + 1, Constants.Colors.Orange);
        }
    }

    private void DrawLoopControls()
    {
        loopStartBtn.Draw(_api, Music.IsLoopStart(patternIndex));
        loopEndBtn.Draw(_api, Music.IsLoopEnd(patternIndex));
        stopBtn.Draw(_api, Music.IsStop(patternIndex));
    }

    private void DrawChannels()
    {
        for (int c = 0; c < ChannelCount; c++)
        {
            bool on = Music.IsChannelOn(patternIndex, c);
            var toggle = ToggleRect(c);
            _api.rectfill(toggle.X, toggle.Y, toggle.X + toggle.Width - 1, toggle.Y + toggle.Height - 1,
                on ? Constants.Colors.LightGray : Constants.Colors.DarkGray);
            _api.rect(toggle.X, toggle.Y, toggle.X + toggle.Width - 1, toggle.Y + toggle.Height - 1, Constants.Colors.Black);

            if (!on) continue;

            int sfx = ChannelSfx(c);
            bool sel = c == selectedChannel;

            var num = SfxNumRect(c);
            _api.rectfill(num.X, num.Y, num.X + num.Width - 1, num.Y + num.Height - 1,
                sel ? Constants.Colors.White : Constants.Colors.Indigo);
            _api.print(sfx.ToString("D2"), num.X + 1, num.Y + 1, sel ? Constants.Colors.Black : Constants.Colors.White);

            var pencil = PencilRect(c);
            _api.icon(PencilIcon, pencil.X, pencil.Y);

            DrawNoteColumn(c, sfx);
        }
    }

    private void DrawNoteColumn(int c, int sfx)
    {
        var col = ColumnRect(c);
        _api.rectfill(col.X, col.Y, col.X + col.Width - 1, col.Y + col.Height - 1, Constants.Colors.Black);

        for (int r = 0; r < VisibleRows; r++)
        {
            int note = scroll[c] + r;
            if (note >= NoteCount) break;
            DrawNoteCell(c, r, note, sfx);
        }
    }

    private void DrawNoteCell(int c, int visRow, int note, int sfx)
    {
        var r = CellRect(c, visRow);
        bool sel = c == selectedChannel && note == selectedCell;
        if (sel)
            _api.rectfill(r.X, r.Y, r.X + r.Width - 1, r.Y + r.Height - 1, Constants.Colors.DarkBlue);

        if (sel)
        {
            int px = r.X + PartX[selectedPart];
            _api.rectfill(px, r.Y, px + PartW[selectedPart] - 1, r.Y + r.Height - 2, Constants.Colors.White);
        }

        // Playhead: light up the note this channel's SFX is currently sounding.
        if (note == mono8.GameAPI.CurrentSfxNote(sfx))
        {
            int cx = r.X + PartX[PartNote];
            int cw = PartX[PartFx] + PartW[PartFx] - PartX[PartNote];
            _api.rectfill(cx, r.Y, cx + cw - 1, r.Y + r.Height - 2, Constants.Colors.Yellow);
        }

        int vol = Sfx.GetVolume(sfx, note);
        bool active = vol > 0;
        int pitch = Sfx.GetPitch(sfx, note);
        int oct = pitch / 12;
        int wf = Sfx.GetWaveform(sfx, note);
        int fx = Sfx.GetEffect(sfx, note);
        int dim = Constants.Colors.DarkGray;

        DrawPart(r, PartNote, active ? NoteNames[pitch % 12] : "--", active ? Constants.Colors.Blue : dim);
        DrawPart(r, PartOct, active ? oct.ToString() : "-", active ? PaletteColor(oct) : dim);
        DrawPart(r, PartWave, active ? wf.ToString() : "-", active ? PaletteColor(wf) : dim);
        DrawPart(r, PartVol, active ? vol.ToString() : "-", active ? PaletteColor(vol) : dim);
        DrawPart(r, PartFx, active ? fx.ToString() : "-", active ? PaletteColor(fx) : dim);

        // Faint note index on the right so scrolling stays legible.
        _api.print(note.ToString("D2"), r.X + 40, r.Y + 2, Constants.Colors.LightGray);
    }

    private void DrawPart(Rectangle cell, int part, string text, int color)
    {
        _api.print(text, cell.X + PartX[part], cell.Y + 2, color);
    }

    private void DrawBox(Rectangle b, string text, int bg, int fg)
    {
        _api.rectfill(b.X, b.Y, b.X + b.Width - 1, b.Y + b.Height - 1, bg);
        _api.print(text, b.X + 1, b.Y + 1, fg);
    }

    private static readonly int[] PaletteColors =
    {
        Constants.Colors.Red, Constants.Colors.Orange, Constants.Colors.Yellow, Constants.Colors.Green,
        Constants.Colors.Blue, Constants.Colors.Indigo, Constants.Colors.Pink, Constants.Colors.Peach,
    };

    private static int PaletteColor(int index) => PaletteColors[index & 7];
}
