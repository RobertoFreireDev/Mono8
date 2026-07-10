namespace mono8.editor;

/// <summary>
/// The bottom sprite-picker panel: a paged view of the sprite sheet, the page buttons and the
/// selected sprite's number. Shared by the Sprite and Map editors, which differ only in how
/// large a selection outline they draw and in what they layer around the panel.
/// </summary>
internal sealed class SpriteNavigator
{
    public const int VisibleRows = 6;
    public const int PageCount = Constants.GameDataSizes.SpriteSheetRows / VisibleRows;

    private const int PageIconSelected = 45;
    private const int PageIconNotSelected = 46;
    private const int LabelGap = 4;

    private readonly IMono8API _api;
    private readonly Rectangle[] _pageButtons;
    private readonly Rectangle _numberLabel;

    /// <summary>The sprite-sheet view itself; callers hit-test against this before delegating a click.</summary>
    public Rectangle ViewerArea { get; }

    /// <summary>Y of the row holding the page buttons and the sprite-number label.</summary>
    public int LabelRowY { get; }

    public int SelectedSprite { get; private set; }
    public int Page { get; private set; }

    /// <summary>
    /// Tiles spanned by the selection outline. The Sprite editor grows this with its canvas
    /// zoom to show the whole multi-tile region being edited; the Map editor leaves it at 1.
    /// </summary>
    public int SelectionScale { get; set; } = 1;

    private int _selX;
    private int _selY;

    public SpriteNavigator(IMono8API api)
    {
        _api = api;

        int size = Constants.GameDataSizes.TileSize;
        ViewerArea = new Rectangle(0,
            Constants.Screen.ResolutionY - 1 - (VisibleRows + 1) * size,
            Constants.GameDataSizes.SpriteSheetX,
            VisibleRows * size);

        _selX = ViewerArea.X;
        _selY = ViewerArea.Y;
        LabelRowY = ViewerArea.Y - size;

        int pageButtonsStartX = Constants.Screen.ResolutionX - PageCount * size;
        _pageButtons = new Rectangle[PageCount];
        for (int i = 0; i < PageCount; i++)
            _pageButtons[i] = new Rectangle(pageButtonsStartX + i * size, LabelRowY - 1, size, size);

        _numberLabel = new Rectangle(pageButtonsStartX - LabelGap - size * 2, LabelRowY - 1, size * 2, size - 1);
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    /// <summary>The sprite id under the cursor, accounting for the current page.</summary>
    public int SpriteUnderMouse((int x, int y) mouse)
    {
        int size = Constants.GameDataSizes.TileSize;
        int x = (mouse.x - ViewerArea.X) / size;
        int y = (mouse.y - ViewerArea.Y) / size;
        return x + (y + Page * VisibleRows) * Constants.GameDataSizes.SpriteSheetColumns;
    }

    /// <summary>Moves the selection to the sprite under the cursor.</summary>
    public void SelectAt((int x, int y) mouse)
    {
        int size = Constants.GameDataSizes.TileSize;
        int x = (mouse.x - ViewerArea.X) / size;
        int y = (mouse.y - ViewerArea.Y) / size;
        _selX = x * size + ViewerArea.X;
        _selY = y * size + ViewerArea.Y;
        SelectedSprite = SpriteUnderMouse(mouse);
    }

    /// <summary>Switches page if a page button was clicked. Returns true when the click was consumed.</summary>
    public bool TryPickPage((int x, int y) mouse)
    {
        for (int i = 0; i < _pageButtons.Length; i++)
        {
            if (_pageButtons[i].Contains(mouse.x, mouse.y) && _api.mouselp())
            {
                Page = i;
                return true;
            }
        }
        return false;
    }

    // ── Drawing ───────────────────────────────────────────────────────────────

    /// <summary>Draws the sheet page and, when it falls on this page, the selection outline.</summary>
    public void DrawSheet()
    {
        int size = Constants.GameDataSizes.TileSize;

        _api.sprr(Page * VisibleRows * Constants.GameDataSizes.SpriteSheetColumns,
            ViewerArea.X, ViewerArea.Y,
            Constants.GameDataSizes.SpriteSheetColumns, VisibleRows);

        int selectedRow = SelectedSprite / Constants.GameDataSizes.SpriteSheetColumns;
        bool onPage = selectedRow >= Page * VisibleRows && selectedRow < (Page + 1) * VisibleRows;
        if (!onPage || _selX < 0 || _selY < 0) return;

        int span = size * SelectionScale;
        _api.rect(_selX - 1, _selY - 1, _selX + span, _selY + span, Constants.Colors.White);
        _api.rect(_selX - 2, _selY - 2, _selX + 1 + span, _selY + 1 + span, Constants.Colors.Black);
    }

    public void DrawPageButtons()
    {
        for (int i = 0; i < _pageButtons.Length; i++)
        {
            if (i != Page) _api.pal(Constants.Colors.White, Constants.Colors.LightGray);

            var bounds = _pageButtons[i];
            _api.icon(i == Page ? PageIconSelected : PageIconNotSelected, bounds.X, bounds.Y);
            _api.print(i.ToString(), bounds.X + 2, bounds.Y + 2, Constants.Colors.Indigo);
            _api.pal();
        }
    }

    public void DrawNumberLabel() =>
        EditorUI.Box(_api, _numberLabel, SelectedSprite.ToString("D3"),
            Constants.Colors.LightGray, Constants.Colors.Indigo);
}
