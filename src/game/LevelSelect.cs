namespace mono8.game;

/// <summary>
/// The level select: the screen the game opens on, and what the pause menu goes back to.
///
/// One number per level in a grid centred on the screen. Level N is the room whose NUMBER is N —
/// <see cref="Levels"/> is what finds it, whatever the developer called the object — so what has
/// been authored is what can be picked, and a number with no room behind it is not drawn at all.
/// That is how a grid laid out for twenty shows five.
///
/// Behind the grid is the level the cursor is on: its backdrop, its cells and its flag, at the
/// camera the room itself would use — picking a hole is picking a picture of it. Only the three
/// fields that takes are read out of ROOMS, and only for the one level showing: the menu draws a
/// room, it does not run one. Moving the cursor slides that picture off the way the cursor went and
/// brings the new one in behind it, the two crossing over <see cref="SlideSeconds"/>.
///
/// Driven by the d-pad: a hole already sunk is yellow, one still to play white, and the number the
/// cursor is on takes the warmer half of its own pair — orange over yellow, green over white — and
/// sits a pixel lower than the rest. Each number is read against a dark green disc, since what is
/// behind it now is a whole level rather than a flat screen. The cursor clamps at the edges of the
/// grid rather than wrapping, and steps over the numbers no room stands behind — they are not drawn,
/// so a cursor sitting on one would be a cursor that vanished.
///
/// Screen pixels for the grid; the preview behind it is the room's own map-sheet pixels, put on
/// screen by the camera exactly as <see cref="Room"/> puts it there.
///
/// Named LevelSelect rather than Menu: every type here is in scope engine-wide, and the engine
/// already has a Menu — the pause one.
/// </summary>
internal static class LevelSelect
{
    private const string JsonGroup = "MENU";
    private const string JsonObject = "GRID";
    private const string FieldCols = "COLS";
    private const string FieldRows = "ROWS";
    private const string FieldCell = "CELL";
    private const string FieldTitle = "TITLE";

    // The preview reads the room group directly rather than through Room: a Room is a level being
    // played — a ball, a player, a stroke count — and none of that is wanted behind a menu.
    private const string RoomGroup = "ROOMS";
    private const string FieldCellPos = "CELLPOS";
    private const string FieldBackPos = "BACKPOS";
    private const string FieldFlagPos = "FLAGPOS";

    // The clip the previewed flag waves on — the same one the room's flag runs.
    private const string FlagAnim = "FLAG";

    // MENU/GRID is not authored yet, so everything it holds has a default here: the 5x4 of twenty
    // levels the game asks for, in cells wide enough for two digits and the space around them.
    private const int DefaultCols = 5;
    private const int DefaultRows = 4;
    private const int DefaultCellW = 32;
    private const int DefaultCellH = 20;

    // The disc every number is read against. Radius rather than diameter, so it is what circfill
    // takes; 8 clears two digits and still leaves a gap in a 32x20 cell.
    private const int DiscRadius = 8;

    // How long a change of level takes to cross over. A whole second is slow for a menu on purpose:
    // the point of the preview is to be looked at, and a cut would give nothing to look at.
    private const float SlideSeconds = 0.5f;

    // The grid offers one cell per level number there can be, and no more.
    private const int MaxLevels = Levels.MaxNumber;

    // Debug owns entry 0, YourGame 2, Save 3.
    private const int MenuIndex = 1;
    private const string MenuLabel = "LEVELS";

    // Measured once per Init and indexed by cell, so a frame of the menu allocates nothing. Cell i is
    // level i + 1: Captions is the number it prints, Rooms the ROOMS object behind it — null for a
    // number no room claims, which is what makes the cell empty.
    private static readonly string[] Captions = new string[MaxLevels];
    private static readonly string[] Rooms = new string[MaxLevels];
    private static readonly int[] TextX = new int[MaxLevels];
    private static readonly int[] TextY = new int[MaxLevels];

    // Centre of the disc behind each number, measured off the caption's own ink rather than off the
    // cell, so a two-digit number sits in the middle of its circle exactly as a one-digit one does.
    private static readonly int[] DiscX = new int[MaxLevels];
    private static readonly int[] DiscY = new int[MaxLevels];

    // Which levels have been sunk, taken off Save whenever the menu comes up rather than per frame:
    // the only thing that can finish a hole is a room, and a room can only hand back through Show.
    private static readonly bool[] Done = new bool[MaxLevels];

    private static int Cols;
    private static int Rows;
    private static int Count;

    private static string Title;
    private static int TitleX;
    private static int TitleY;

    // Where the d-pad has walked to, as an index into the grid. -1 when no level is authored at all,
    // which is the one case there is nothing to put a cursor on.
    private static int Cursor;

    // The level showing behind the grid, and the one still sliding off behind it. Loaded on a move,
    // never per frame — a frame of the menu asks json nothing.
    private static Preview Shown;
    private static Preview Leaving;

    // The move that started the slide, as a step on the grid: (1, 0) for a press right. It is what
    // gives the picture its direction, so the screen moves the way the cursor did.
    private static int SlideX;
    private static int SlideY;

    // Seconds into the slide. At SlideSeconds or past it the menu is settled and Leaving is gone.
    private static float SlideTime;

    // One clip for both previews: they are the same flag, so there is nothing to keep apart.
    private static readonly Anim Wave = new Anim();

    /// <summary>
    /// A room reduced to the picture of it: which screenful of the sheet it cuts out, where its
    /// backdrop sits and where its flag stands. Nothing else under ROOMS is read — spawns, strokes
    /// and the level number are all things a room being played needs, and the menu is not playing
    /// one. A struct, so holding two of them costs no allocation.
    /// </summary>
    private struct Preview
    {
        public bool Loaded;
        public int CellX;
        public int CellY;
        public int BackX;
        public int BackY;
        public bool HasFlag;
        public int FlagX;
        public int FlagY;

        // The room's top-left corner in map-sheet pixels, which is what the camera subtracts — the
        // same reading Room.OriginX gives, since it is the same CELLPOS.
        public int OriginX => CellX * Terrain.TileSize;
        public int OriginY => CellY * Terrain.TileSize;
    }

    /// <summary>The menu is up: nothing in the room runs while it is.</summary>
    public static bool Active { get; private set; }

    /// <summary>The ROOMS object last picked — null until the player picks one.</summary>
    public static string Picked { get; private set; }

    public static void Init()
    {
        Layout();
        Show();
    }

    /// <summary>
    /// Puts the menu up. Also the pause-menu entry's callback. The cursor is left where it was, so
    /// coming back from a level lands on the level you came back from.
    /// </summary>
    public static void Show()
    {
        Active = true;
        Picked = null;

        // The hole that was just sunk is one of these, so the results are re-read on the way back
        // rather than measured once with the grid.
        Refresh();

        // Re-read on the way back rather than measured with the grid, for the same reason the
        // results are: a Ctrl+S in the JSON editor while a level was up rebuilds the data, and the
        // room the cursor is sitting on is one of the objects it rebuilt.
        Wave.Load(FlagAnim);
        Settle();

        // The room entries come down while the menu is what is on screen: no level to restart,
        // nothing to go back to and nothing to overlay. Deleting the save is not one of them — that
        // one is up on both screens, and this is the screen it shows in.
        YourGame.API.menuitem(MenuIndex);
        YourGame.HideRestart();
        Debug.Hide();
    }

    /// <summary>Takes the menu down, once <see cref="Picked"/> has been read.</summary>
    public static void Close()
    {
        Active = false;
        YourGame.API.menuitem(MenuIndex, MenuLabel, Show);
        YourGame.ShowRestart();
        Debug.Show();
    }

    /// <summary>
    /// Re-reads which levels have been sunk. <see cref="Show"/> takes them on the way in, since the
    /// hole just finished is one of them; this is for the other thing that can change them under a
    /// grid already laid out — the save being deleted from the pause menu.
    /// </summary>
    public static void Refresh()
    {
        for (int i = 0; i < Count; i++)
        {
            Done[i] = Save.Played(i + 1);
        }
    }

    /// <summary>
    /// The room after <paramref name="name"/>: the next number up the developer has authored a room
    /// for, so a gap in the numbering is stepped over rather than ending the run at it — the same
    /// reading the cursor gives a gap. Null when there is no level above it, which is what sends the
    /// game back here once the last hole is sunk.
    /// </summary>
    public static string Next(string name)
    {
        int from = IndexOf(name);

        if (from < 0)
        {
            return null;
        }

        for (int i = from + 1; i < Count; i++)
        {
            if (Rooms[i] != null)
            {
                return Rooms[i];
            }
        }

        return null;
    }

    /// <summary>
    /// Puts the cursor on a level. The game walks on from the level that was picked, and the cursor
    /// is left where it was — so without this, coming back here after four holes would land on the
    /// one they started from.
    /// </summary>
    public static void Focus(string name)
    {
        int i = IndexOf(name);

        if (i >= 0 && Rooms[i] != null)
        {
            Cursor = i;

            // Put there rather than walked to, so there is no direction to slide in and nothing to
            // slide from: the menu is not on screen when this is called.
            Settle();
        }
    }

    public static void Update(float elapsedSeconds)
    {
        var api = YourGame.API;

        if (SlideTime < SlideSeconds)
        {
            SlideTime += elapsedSeconds;
        }

        Wave.Update(elapsedSeconds);

        // One cell per press, no repeat: a menu of twenty is small enough that held-key repeat would
        // overshoot more often than it would help.
        if (api.btnp(Btn.Left))
        {
            Move(-1, 0);
        }

        if (api.btnp(Btn.Right))
        {
            Move(1, 0);
        }

        if (api.btnp(Btn.Up))
        {
            Move(0, -1);
        }

        if (api.btnp(Btn.Down))
        {
            Move(0, 1);
        }

        // btnp, not btn: the room this opens reads Jump with btnp too, so the press that picked a
        // level cannot go on to be the jump its first frame sees.
        if (Cursor >= 0 && (api.btnp(Btn.Jump) || api.btnp(Btn.Swing)))
        {
            Picked = Rooms[Cursor];
        }
    }

    public static void Draw()
    {
        var api = YourGame.API;

        // The preview covers the screen only while it is settled: mid-slide it is two half-faded
        // pictures with a gap between them, and a fade over an uncleared frame smears rather than
        // crossing over. So the menu still clears — but to nothing, since what shows through the gap
        // is the edge of a level and not a colour the menu owns.
        api.cls();

        float t = SlideTime / SlideSeconds;
        if (t > 1f)
        {
            t = 1f;
        }

        // The level being left goes the way the cursor went — a press right carries it off to the
        // left — and the one arriving comes in from the far side, so what moves on screen is the
        // move that was made on the grid. They cross rather than cut: each fades as it travels.
        if (t < 1f)
        {
            DrawPreview(
                Leaving,
                (int)(-SlideX * Constants.Screen.ResolutionX * t),
                (int)(-SlideY * Constants.Screen.ResolutionY * t),
                1f - t);
        }

        DrawPreview(
            Shown,
            (int)(SlideX * Constants.Screen.ResolutionX * (1f - t)),
            (int)(SlideY * Constants.Screen.ResolutionY * (1f - t)),
            t);

        // The same dark the room would be under, over both halves of the slide at once rather than
        // per preview: it is one night, and dimming each picture separately would show the seam
        // where they cross. (0, 0) because the previews have put the camera back — the menu is drawn
        // in screen pixels, so the screenful that falls dark starts at the screen's own corner.
        //
        // Under the grid, though: the numbers are the menu's and not the level's, and a cursor you
        // have to find through a wash of black is a cursor lost at midnight.
        Night.Draw(0, 0);

        if (Title.Length > 0)
        {
            Font.PrintOutlined(Title, TitleX, TitleY, Constants.Colors.White);
        }

        for (int i = 0; i < Count; i++)
        {
            // An unauthored level is the empty cell it leaves: there is nothing to pick, so there is
            // nothing to show.
            if (Rooms[i] == null)
            {
                continue;
            }

            bool on = i == Cursor;

            // The disc the number is read against. There is a whole level behind the grid now, so a
            // number on its own would take whatever tile happened to fall under it — and the level
            // moving under a still grid is what says the grid is not part of it. It does not take
            // the cursor's drop: the number shifting inside its own disc is the cue.
            api.circfill(DiscX[i], DiscY[i], DiscRadius + 1, Constants.Colors.Black);
            api.circfill(DiscX[i], DiscY[i], DiscRadius, on ? Constants.Colors.White : Constants.Colors.DarkGreen);

            // Two pairs, not two signals fighting over one number: a hole already sunk is yellow and
            // orange under the cursor, one still to play white and green. The cursor keeps its own
            // colour either way, so it is still the warmer of the pair it is standing in.
            int color = Done[i]
                ? (on ? Constants.Colors.Orange : Constants.Colors.Yellow)
                : (on ? Constants.Colors.Green : Constants.Colors.White);

            // Dropped as well as coloured: with no pointer on screen, the row breaking out of line is
            // what finds the cursor at a glance, and colour alone is a difference you have to look for.
            Font.PrintOutlined(Captions[i], TextX[i], TextY[i], color);
        }
    }

    // One level's picture, offset by the slide and faded by it. Everything in it is the room's own
    // map-sheet pixels, so the camera is what puts it on screen exactly as Room.Draw does — with the
    // offset taken off the camera rather than added to each draw, which is what keeps the flag
    // standing on the cells it stands on.
    private static void DrawPreview(Preview preview, int offsetX, int offsetY, float opacity)
    {
        if (!preview.Loaded || opacity <= 0f)
        {
            return;
        }

        var api = YourGame.API;
        int originX = preview.OriginX;
        int originY = preview.OriginY;

        api.camera(originX - offsetX, originY - offsetY);

        // Backdrop first, its own patch of the sheet — the room draws it in screen pixels, and here
        // it travels with the room rather than staying put, since the whole picture is what moves.
        api.map(preview.BackX, preview.BackY, originX, originY, Room.CellW, Room.CellH, 1f, opacity);
        api.map(preview.CellX, preview.CellY, originX, originY, Room.CellW, Room.CellH, 1f, opacity);

        if (preview.HasFlag)
        {
            api.spr(Wave.Sprite, preview.FlagX, preview.FlagY, 1, 1, 1f, false, false, opacity);
        }

        api.camera();
    }

    // Just enough of a room to draw it: CELLPOS for the cells and the camera, BACKPOS for the
    // backdrop, FLAGPOS for the flag. An unknown or half-authored room reads as the empty picture it
    // is rather than failing — the same reading Room.Load gives it.
    private static Preview Load(string name)
    {
        Preview preview = default;

        var data = string.IsNullOrEmpty(name) ? null : YourGame.API.gjson(RoomGroup, name);
        if (data == null)
        {
            return preview;
        }

        preview.Loaded = true;
        preview.BackX = Room.DefaultBackX;
        preview.BackY = Room.DefaultBackY;

        // (0, 0) is a legitimate origin — the top-left room — so an unauthored CELLPOS costs nothing
        // and needs no Has of its own. A flag at (0, 0) is a flag in another room's corner, so that
        // one does.
        if (data.Has(FieldCellPos))
        {
            (preview.CellX, preview.CellY) = data.GetXY(FieldCellPos);
        }

        if (data.Has(FieldBackPos))
        {
            (preview.BackX, preview.BackY) = data.GetXY(FieldBackPos);
        }

        if (data.Has(FieldFlagPos))
        {
            (preview.FlagX, preview.FlagY) = data.GetXY(FieldFlagPos);
            preview.HasFlag = true;
        }

        return preview;
    }

    // The level the cursor is on, up with no slide at all — the menu coming back, or a cursor put
    // somewhere rather than walked there.
    private static void Settle()
    {
        Shown = Cursor >= 0 ? Load(Rooms[Cursor]) : default;
        Leaving = default;
        SlideX = 0;
        SlideY = 0;
        SlideTime = SlideSeconds;
    }

    // The cursor has walked a cell: what was showing starts leaving in that direction and the level
    // walked onto comes in behind it. A press part way through a slide drops whatever was still on
    // its way out and starts again from what is showing, so the picture never lags the cursor.
    private static void StartSlide(int dx, int dy)
    {
        Leaving = Shown;
        Shown = Load(Rooms[Cursor]);
        SlideX = dx;
        SlideY = dy;
        SlideTime = 0f;
    }

    // Which cell of the grid a room is: its NUMBER, one-based, and the grid is zero-based. A room
    // with no number, or one past the end of a grid too small to offer it, is not a cell here and
    // reads as -1.
    private static int IndexOf(string name)
    {
        int i = Levels.Number(name) - 1;

        return i >= 0 && i < Count ? i : -1;
    }

    // One step in the direction pressed, and on past every number no room stands behind. Clamped, not
    // wrapped: run out of grid — or out of authored levels — before finding one and the cursor has
    // not moved at all.
    private static void Move(int dx, int dy)
    {
        if (Cursor < 0)
        {
            return;
        }

        int col = Cursor % Cols;
        int row = Cursor / Cols;

        while (true)
        {
            col += dx;
            row += dy;

            if (col < 0 || col >= Cols || row < 0 || row >= Rows)
            {
                return;
            }

            // The last row is short when cols * rows overran the 64 a group holds.
            int i = row * Cols + col;
            if (i >= Count)
            {
                return;
            }

            if (Rooms[i] != null)
            {
                Cursor = i;

                // The step pressed, not the cells crossed: skipping over three unauthored numbers to
                // land on the fourth is still one move right, and the picture behind should read as
                // one move right too.
                StartSlide(dx, dy);
                return;
            }
        }
    }

    // The grid is measured once, on Init: which level a number stands for, where it prints and where
    // the cursor can walk are all fixed for as long as the menu is up.
    private static void Layout()
    {
        var api = YourGame.API;

        Cols = DefaultCols;
        Rows = DefaultRows;
        int cellW = DefaultCellW;
        int cellH = DefaultCellH;
        Title = string.Empty;

        // Re-read every Init: Ctrl+S in the JSON editor rebuilds the data without a restart.
        var grid = api.gjson(JsonGroup, JsonObject);
        if (grid != null)
        {
            Cols = grid.GetInt(FieldCols, 0, DefaultCols);
            Rows = grid.GetInt(FieldRows, 0, DefaultRows);
            Title = grid.GetStr(FieldTitle);

            // (0, 0) would stack every number on one spot, so an unauthored cell keeps the default.
            if (grid.Has(FieldCell))
            {
                (cellW, cellH) = grid.GetXY(FieldCell);
            }
        }

        Cols = (int)api.mid(1, Cols, MaxLevels);
        Rows = (int)api.mid(1, Rows, MaxLevels);
        cellW = (int)api.mid(1, cellW, Constants.Screen.ResolutionX);
        cellH = (int)api.mid(1, cellH, Constants.Screen.ResolutionY);
        Count = (int)api.mid(1, Cols * Rows, MaxLevels);

        int originX = (Constants.Screen.ResolutionX - Cols * cellW) / 2;
        int originY = (Constants.Screen.ResolutionY - Rows * cellH) / 2;

        Cursor = -1;

        for (int i = 0; i < Count; i++)
        {
            // Cell i is level i + 1, whichever room claimed that number. The caption is built here
            // rather than per frame — a level number is the one caption in the game that can never
            // change.
            Captions[i] = (i + 1).ToString();
            Rooms[i] = Levels.Name(i + 1);

            // The cursor opens on the lowest level there is a room for — level 1 in a finished game,
            // and whatever is authored so far in this one.
            if (Cursor < 0 && Rooms[i] != null)
            {
                Cursor = i;
            }

            int col = i % Cols;
            int row = i / Cols;
            TextX[i] = originX + col * cellW + (cellW - Font.Width(Captions[i])) / 2;
            TextY[i] = originY + row * cellH + (cellH - Font.Height) / 2;

            // Centred on the caption's ink rather than on its cell, so the disc is centred on the
            // number whatever the cell size is and however many digits the number has.
            DiscX[i] = TextX[i] + Font.Width(Captions[i]) / 2;
            DiscY[i] = TextY[i] + Font.Middle;
        }

        // Centred in whatever room is left above the grid, so it follows the cell size rather than
        // needing a place of its own.
        TitleX = (Constants.Screen.ResolutionX - Font.Width(Title)) / 2;
        TitleY = (int)api.mid(1, (originY - Font.Height) / 2, originY);
    }
}
