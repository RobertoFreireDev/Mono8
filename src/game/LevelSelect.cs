namespace mono8.game;

/// <summary>
/// The level select: the screen the game opens on, and what the pause menu goes back to.
///
/// One number per level in a grid centred on the screen. Level N is the room authored as the object
/// named "N" under ROOMS, so what the developer has authored is what can be picked — a number with
/// no room behind it is not drawn at all, which is how a grid laid out for twenty shows five.
///
/// Picked with the mouse: the number under the pointer is green, every other one white.
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
    private const string FieldPad = "PAD";
    private const string FieldTitle = "TITLE";

    // MENU/GRID is not authored yet, so everything it holds has a default here: the 5x4 of twenty
    // levels the game asks for, in cells wide enough for two digits and the hover box around them.
    private const int DefaultCols = 5;
    private const int DefaultRows = 4;
    private const int DefaultCellW = 32;
    private const int DefaultCellH = 20;
    private const int DefaultPad = 3;

    // A group holds at most 64 objects, so no grid can offer more levels than that.
    private const int MaxLevels = 64;

    // Debug owns entry 0.
    private const int MenuIndex = 1;
    private const string MenuLabel = "LEVELS";

    // Measured once per Init and indexed by level, so a frame of the menu allocates nothing.
    private static readonly string[] Names = new string[MaxLevels];
    private static readonly bool[] Authored = new bool[MaxLevels];
    private static readonly int[] TextX = new int[MaxLevels];
    private static readonly int[] TextY = new int[MaxLevels];

    private static int Count;
    private static int Pad;

    private static string Title;
    private static int TitleX;
    private static int TitleY;

    private static int Hover;

    /// <summary>The menu is up: nothing in the room runs while it is.</summary>
    public static bool Active { get; private set; }

    /// <summary>The ROOMS object last picked — null until the player picks one.</summary>
    public static string Picked { get; private set; }

    public static void Init()
    {
        Layout();
        Show();
    }

    /// <summary>Puts the menu up. Also the pause-menu entry's callback.</summary>
    public static void Show()
    {
        Active = true;
        Picked = null;
        Hover = -1;

        // Nothing to go back to while the menu is what is on screen.
        YourGame.API.menuitem(MenuIndex);
    }

    /// <summary>Takes the menu down, once <see cref="Picked"/> has been read.</summary>
    public static void Close()
    {
        Active = false;
        YourGame.API.menuitem(MenuIndex, MenuLabel, Show);
    }

    public static void Update()
    {
        var api = YourGame.API;

        Hover = -1;

        var (mx, my) = api.mousexy();
        for (int i = 0; i < Count; i++)
        {
            if (Authored[i] && Over(i, mx, my))
            {
                Hover = i;
                break;
            }
        }

        // On the press, not the release: the menu is gone before the button comes back up, and the
        // release would land in the room it opened.
        if (Hover >= 0 && api.mouselp())
        {
            Picked = Names[Hover];
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
            if (!Authored[i])
            {
                continue;
            }

            Font.PrintOutlined(Names[i], TextX[i], TextY[i],
                i == Hover ? Constants.Colors.Green : Constants.Colors.White);
        }
    }

    // The grid is measured once, on Init: which level a number stands for, where it prints and the
    // box the pointer has to be inside are all fixed for as long as the menu is up.
    private static void Layout()
    {
        var api = YourGame.API;

        int cols = DefaultCols;
        int rows = DefaultRows;
        int cellW = DefaultCellW;
        int cellH = DefaultCellH;
        Pad = DefaultPad;
        Title = string.Empty;

        // Re-read every Init: Ctrl+S in the JSON editor rebuilds the data without a restart.
        var grid = api.gjson(JsonGroup, JsonObject);
        if (grid != null)
        {
            cols = grid.GetInt(FieldCols, 0, DefaultCols);
            rows = grid.GetInt(FieldRows, 0, DefaultRows);
            Pad = grid.GetInt(FieldPad, 0, DefaultPad);
            Title = grid.GetStr(FieldTitle);

            // (0, 0) would stack every number on one spot, so an unauthored cell keeps the default.
            if (grid.Has(FieldCell))
            {
                (cellW, cellH) = grid.GetXY(FieldCell);
            }
        }

        cols = (int)api.mid(1, cols, MaxLevels);
        rows = (int)api.mid(1, rows, MaxLevels);
        cellW = (int)api.mid(1, cellW, Constants.Screen.ResolutionX);
        cellH = (int)api.mid(1, cellH, Constants.Screen.ResolutionY);
        Pad = (int)api.mid(0, Pad, Constants.Screen.ResolutionY);
        Count = (int)api.mid(1, cols * rows, MaxLevels);

        int originX = (Constants.Screen.ResolutionX - cols * cellW) / 2;
        int originY = (Constants.Screen.ResolutionY - rows * cellH) / 2;

        for (int i = 0; i < Count; i++)
        {
            // Level 1 is the room named "1". Built here rather than per frame — a level number is
            // the one caption in the game that can never change.
            Names[i] = (i + 1).ToString();
            Authored[i] = Room.Exists(Names[i]);

            int col = i % cols;
            int row = i / cols;
            TextX[i] = originX + col * cellW + (cellW - Font.Width(Names[i])) / 2;
            TextY[i] = originY + row * cellH + (cellH - Font.Height) / 2;
        }

        // Centred in whatever room is left above the grid, so it follows the cell size rather than
        // needing a place of its own.
        TitleX = (Constants.Screen.ResolutionX - Font.Width(Title)) / 2;
        TitleY = (int)api.mid(1, (originY - Font.Height) / 2, originY);
    }

    // The number itself, not the cell around it — with Pad on every side, so a one-digit level is no
    // harder to land on than a two-digit one.
    private static bool Over(int level, int x, int y)
    {
        int x0 = TextX[level] - Pad;
        int y0 = TextY[level] - Pad;

        return x >= x0 && x < x0 + Font.Width(Names[level]) + Pad * 2
            && y >= y0 && y < y0 + Font.Height + Pad * 2;
    }
}
