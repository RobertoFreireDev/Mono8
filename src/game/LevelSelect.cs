namespace mono8.game;

/// <summary>
/// The level select: the screen the game opens on, and what the pause menu goes back to.
///
/// One number per level in a grid centred on the screen. Level N is the room whose NUMBER is N —
/// <see cref="Levels"/> is what finds it, whatever the developer called the object — so what has
/// been authored is what can be picked, and a number with no room behind it is not drawn at all.
/// That is how a grid laid out for twenty shows five.
///
/// Driven by the d-pad: a hole already sunk is yellow, one still to play white, and the number the
/// cursor is on takes the warmer half of its own pair — orange over yellow, green over white — and
/// sits a pixel lower than the rest. The cursor clamps at the edges of the grid rather than wrapping,
/// and steps over the numbers no room stands behind — they are not drawn, so a cursor sitting on one
/// would be a cursor that vanished.
///
/// Screen pixels throughout — the menu has no room, and no camera to be offset by.
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

    // MENU/GRID is not authored yet, so everything it holds has a default here: the 5x4 of twenty
    // levels the game asks for, in cells wide enough for two digits and the space around them.
    private const int DefaultCols = 5;
    private const int DefaultRows = 4;
    private const int DefaultCellW = 32;
    private const int DefaultCellH = 20;

    // Pixels the number under the cursor is dropped. One is enough to read as picked out of a row —
    // it breaks the line the others sit on.
    private const int CursorDrop = 1;

    // The grid offers one cell per level number there can be, and no more.
    private const int MaxLevels = Levels.MaxNumber;

    // Debug owns entry 0.
    private const int MenuIndex = 1;
    private const string MenuLabel = "LEVELS";

    // Measured once per Init and indexed by cell, so a frame of the menu allocates nothing. Cell i is
    // level i + 1: Captions is the number it prints, Rooms the ROOMS object behind it — null for a
    // number no room claims, which is what makes the cell empty.
    private static readonly string[] Captions = new string[MaxLevels];
    private static readonly string[] Rooms = new string[MaxLevels];
    private static readonly int[] TextX = new int[MaxLevels];
    private static readonly int[] TextY = new int[MaxLevels];

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
        for (int i = 0; i < Count; i++)
        {
            Done[i] = Save.Played(i + 1);
        }

        // The pause menu is down to Continue and Exit while the menu is what is on screen: nothing to
        // go back to, nothing to debug, and Restart only re-runs Init, which lands right back here.
        YourGame.API.menuitem(MenuIndex);
        YourGame.API.menurestart(false);
        Debug.Hide();
    }

    /// <summary>Takes the menu down, once <see cref="Picked"/> has been read.</summary>
    public static void Close()
    {
        Active = false;
        YourGame.API.menuitem(MenuIndex, MenuLabel, Show);
        YourGame.API.menurestart(true);
        Debug.Show();
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
        }
    }

    public static void Update()
    {
        var api = YourGame.API;

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
        // The room paints its own backdrop; the menu has none, so it is the one screen that clears.
        YourGame.API.cls(Constants.Colors.DarkGreen);

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

            // Two pairs, not two signals fighting over one number: a hole already sunk is yellow and
            // orange under the cursor, one still to play white and green. The cursor keeps its own
            // colour either way, so it is still the warmer of the pair it is standing in.
            int color = Done[i]
                ? (on ? Constants.Colors.Orange : Constants.Colors.Yellow)
                : (on ? Constants.Colors.Green : Constants.Colors.White);

            // Dropped as well as coloured: with no pointer on screen, the row breaking out of line is
            // what finds the cursor at a glance, and colour alone is a difference you have to look for.
            Font.PrintOutlined(Captions[i], TextX[i], on ? TextY[i] + CursorDrop : TextY[i], color);
        }
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
        }

        // Centred in whatever room is left above the grid, so it follows the cell size rather than
        // needing a place of its own.
        TitleX = (Constants.Screen.ResolutionX - Font.Width(Title)) / 2;
        TitleY = (int)api.mid(1, (originY - Font.Height) / 2, originY);
    }
}
