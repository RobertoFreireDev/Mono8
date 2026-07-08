namespace mono8.editor;

internal class MapEditor : IEditor
{
    private enum Tool
    {
        Pixel,
        RectFill,
    }

    private readonly IMono8API _api;
    private readonly EventNotifier eventNotifier;

    // --- Map viewport ---
    private const int MapTop = Constants.GameDataSizes.TileSize;
    private static readonly int BottomBarY = Constants.Screen.ResolutionY - Constants.GameDataSizes.TileSize;
    private static readonly int MapCols = Constants.Screen.ResolutionX / Constants.GameDataSizes.TileSize; // 32
    private int camX;
    private int camY;

    // Set by the menu bar toggle (top-left, only shown while the map editor is active).
    public bool FullMapView { get; set; }

    // --- Sprite navigator (bottom panel, like SpriteEditor) ---
    private const int VisibleRows = 6;
    private const int SpritePages = Constants.GameDataSizes.SpriteSheetRows / VisibleRows;
    private const int PageIconSelected = 45;
    private const int PageIconNotSelected = 46;
    private Rectangle sprvwrarea;
    private int sprNmbr;
    private int SprX;
    private int SprY;
    private readonly int labelRowY;
    private readonly Rectangle[] pageButtons;
    private readonly Rectangle sprNmbrLabelArea;
    private int spritePage;

    // --- Tools (drawn on the sprite-number / page-button row) ---
    private readonly (Button Button, Tool Tool)[] toolButtons;
    private Tool selectedTool = Tool.Pixel;

    // --- RectFill drag ---
    private bool dragging;
    private int dragStartCellX;
    private int dragStartCellY;

    // --- Hover position (shown on the tool/sprite-number/page-button row) ---
    private bool hoveringMap;
    private int hoverCellX;
    private int hoverCellY;

    public MapEditor(IMono8API api)
    {
        _api = api;
        eventNotifier = new EventNotifier(api, 2f, 1, Constants.Screen.ResolutionY - Constants.GameDataSizes.TileSize + 1);

        sprvwrarea = new Rectangle(0,
            Constants.Screen.ResolutionY - 1 - (VisibleRows + 1) * Constants.GameDataSizes.TileSize,
            Constants.GameDataSizes.SpriteSheetX,
            VisibleRows * Constants.GameDataSizes.TileSize);
        sprNmbr = 0;
        SprX = 0;
        SprY = sprvwrarea.Y;

        int size = Constants.GameDataSizes.TileSize;
        labelRowY = sprvwrarea.Y - size;

        int pageButtonsStartX = Constants.Screen.ResolutionX - SpritePages * size;
        pageButtons = new Rectangle[SpritePages];
        for (int i = 0; i < pageButtons.Length; i++)
        {
            pageButtons[i] = new Rectangle(pageButtonsStartX + i * size, labelRowY - 1, size, size);
        }

        const int labelGap = 4;
        sprNmbrLabelArea = new Rectangle(pageButtonsStartX - labelGap - size * 2, labelRowY - 1, size * 2, size - 1);

        toolButtons = new[]
        {
            (new Button(0 * size, labelRowY - 1, size, 25), Tool.Pixel),
            (new Button(1 * size, labelRowY - 1, size, 23), Tool.RectFill),
        };
    }

    public void Init()
    {
    }

    private int MapRows => FullMapView
        ? (BottomBarY - MapTop) / Constants.GameDataSizes.TileSize
        : (labelRowY - MapTop) / Constants.GameDataSizes.TileSize;

    private Rectangle MapArea => new Rectangle(0, MapTop,
        MapCols * Constants.GameDataSizes.TileSize,
        MapRows * Constants.GameDataSizes.TileSize);

    private void ClampCamera()
    {
        camX = Math.Clamp(camX, 0, Math.Max(0, Constants.GameDataSizes.MapSheetX - MapCols));
        camY = Math.Clamp(camY, 0, Math.Max(0, Constants.GameDataSizes.MapSheetY - MapRows));
    }

    public void Update(float elapsedSeconds)
    {
        eventNotifier.Update(elapsedSeconds);

        if (KeybrdInput.IsSaveShortcutPressed())
        {
            mono8.GameAPI.Save();
            eventNotifier.AddEvent("SAVED");
        }

        // Pan the map viewport.
        if (KeybrdInput.Pressed(Keys.Left)) camX -= 1;
        if (KeybrdInput.Pressed(Keys.Right)) camX += 1;
        if (KeybrdInput.Pressed(Keys.Up)) camY -= 1;
        if (KeybrdInput.Pressed(Keys.Down)) camY += 1;
        ClampCamera();

        var mouse = _api.mousexy();
        var mapArea = MapArea;

        if (dragging && !mapArea.Contains(mouse.x, mouse.y))
        {
            dragging = false;
        }

        hoveringMap = mapArea.Contains(mouse.x, mouse.y);

        if (hoveringMap)
        {
            (hoverCellX, hoverCellY) = CellUnderMouse(mouse, mapArea);
            UpdateMapPainting(mouse, mapArea);
        }
        else if (!FullMapView && sprvwrarea.Contains(mouse.x, mouse.y))
        {
            if (_api.mousel())
            {
                int x = (mouse.x - sprvwrarea.X) / Constants.GameDataSizes.TileSize;
                int y = (mouse.y - sprvwrarea.Y) / Constants.GameDataSizes.TileSize;
                SprX = x * Constants.GameDataSizes.TileSize + sprvwrarea.X;
                SprY = y * Constants.GameDataSizes.TileSize + sprvwrarea.Y;
                sprNmbr = x + (y + spritePage * VisibleRows) * Constants.GameDataSizes.SpriteSheetColumns;
            }
        }
        else if (!FullMapView)
        {
            foreach (var (button, tool) in toolButtons)
            {
                if (button.IsClicked(_api, mouse))
                {
                    selectedTool = tool;
                    break;
                }
            }

            for (int i = 0; i < pageButtons.Length; i++)
            {
                if (pageButtons[i].Contains(mouse.x, mouse.y) && _api.mouselp())
                {
                    spritePage = i;
                    break;
                }
            }
        }
    }

    private (int cellX, int cellY) CellUnderMouse((int x, int y) mouse, Rectangle mapArea)
    {
        int cellX = camX + (mouse.x - mapArea.X) / Constants.GameDataSizes.TileSize;
        int cellY = camY + (mouse.y - mapArea.Y) / Constants.GameDataSizes.TileSize;
        return (cellX, cellY);
    }

    private void UpdateMapPainting((int x, int y) mouse, Rectangle mapArea)
    {
        var (cellX, cellY) = CellUnderMouse(mouse, mapArea);

        if (selectedTool == Tool.Pixel)
        {
            if (_api.mousel()) _api.mset(cellX, cellY, sprNmbr);
        }
        else if (selectedTool == Tool.RectFill)
        {
            if (_api.mouselp())
            {
                dragStartCellX = cellX;
                dragStartCellY = cellY;
                dragging = true;
            }
            else if (dragging && _api.mouselr())
            {
                ApplyRectFill(dragStartCellX, dragStartCellY, cellX, cellY);
                dragging = false;
            }
        }
    }

    private void ApplyRectFill(int x0, int y0, int x1, int y1)
    {
        int minX = Math.Min(x0, x1);
        int minY = Math.Min(y0, y1);
        int maxX = Math.Max(x0, x1);
        int maxY = Math.Max(y0, y1);

        for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
                _api.mset(x, y, sprNmbr);
    }

    public void Draw()
    {
        DrawMap();

        _api.rectfill(0, BottomBarY, Constants.Screen.ResolutionX, Constants.Screen.ResolutionY - 1, Constants.Colors.Orange);

        if (hoveringMap)
        {
            const int charAdvance = 4;
            const int rightMargin = 2;
            string hoverText = $"X:{hoverCellX:D3} Y:{hoverCellY:D3}";
            _api.print(hoverText,
                Constants.Screen.ResolutionX - rightMargin - hoverText.Length * charAdvance,
                BottomBarY + 1,
                Constants.Colors.Indigo);
        }

        if (!FullMapView)
        {
            DrawSpriteNavigator();
        }

        eventNotifier.Draw();
    }

    private void DrawMap()
    {
        var mapArea = MapArea;
        int size = Constants.GameDataSizes.TileSize;

        _api.rectfill(mapArea.X, mapArea.Y,
            mapArea.X + mapArea.Width - 1, mapArea.Y + mapArea.Height - 1,
            Constants.Colors.Black);

        _api.map(camX, camY, mapArea.X, mapArea.Y, MapCols, MapRows);

        var mouse = _api.mousexy();
        if (mapArea.Contains(mouse.x, mouse.y))
        {
            if (dragging && selectedTool == Tool.RectFill)
            {
                var (cellX, cellY) = CellUnderMouse(mouse, mapArea);
                int minX = Math.Min(dragStartCellX, cellX);
                int minY = Math.Min(dragStartCellY, cellY);
                int maxX = Math.Max(dragStartCellX, cellX);
                int maxY = Math.Max(dragStartCellY, cellY);
                int px = mapArea.X + (minX - camX) * size;
                int py = mapArea.Y + (minY - camY) * size;
                _api.rectfill(px, py,
                    px + (maxX - minX + 1) * size - 1,
                    py + (maxY - minY + 1) * size - 1,
                    Constants.Colors.White);
            }
            else
            {
                int hx = mapArea.X + ((mouse.x - mapArea.X) / size) * size;
                int hy = mapArea.Y + ((mouse.y - mapArea.Y) / size) * size;
                _api.rectfill(hx, hy, hx + size - 1, hy + size - 1, Constants.Colors.White);
            }
        }
    }

    private void DrawSpriteNavigator()
    {
        _api.spr(spritePage * VisibleRows * Constants.GameDataSizes.SpriteSheetColumns,
            sprvwrarea.X,
            sprvwrarea.Y,
            Constants.GameDataSizes.SpriteSheetColumns,
            VisibleRows);

        int selectedRow = sprNmbr / Constants.GameDataSizes.SpriteSheetColumns;
        bool selectedOnPage = selectedRow >= spritePage * VisibleRows && selectedRow < (spritePage + 1) * VisibleRows;
        if (selectedOnPage && SprX > -1 && SprY > -1)
        {
            _api.rect(SprX - 1, SprY - 1,
                SprX + Constants.GameDataSizes.TileSize,
                SprY + Constants.GameDataSizes.TileSize,
                Constants.Colors.White);
            _api.rect(SprX - 2, SprY - 2,
                SprX + 1 + Constants.GameDataSizes.TileSize,
                SprY + 1 + Constants.GameDataSizes.TileSize,
                Constants.Colors.Black);
        }

        // Dark grey backs only the tool / sprite-number / page-button row.
        _api.rectfill(0, labelRowY - 1,
            Constants.Screen.ResolutionX, labelRowY - 2 + Constants.GameDataSizes.TileSize,
            Constants.Colors.DarkGray);

        foreach (var (button, tool) in toolButtons)
        {
            button.Draw(_api, tool == selectedTool);
        }


        for (int i = 0; i < pageButtons.Length; i++)
        {
            if (i != spritePage)
            {
                _api.pal(Constants.Colors.White, Constants.Colors.LightGray);
            }

            var bounds = pageButtons[i];
            _api.icon(i == spritePage ? PageIconSelected : PageIconNotSelected, bounds.X, bounds.Y);
            _api.print(i.ToString(), bounds.X + 2, bounds.Y + 2, Constants.Colors.Indigo);
            _api.pal();
        }

        _api.rectfill(sprNmbrLabelArea.X, sprNmbrLabelArea.Y,
            sprNmbrLabelArea.X + sprNmbrLabelArea.Width - 1,
            sprNmbrLabelArea.Y + sprNmbrLabelArea.Height - 1,
            Constants.Colors.LightGray);
        _api.print(sprNmbr.ToString("D3"), sprNmbrLabelArea.X + 1, sprNmbrLabelArea.Y + 1, Constants.Colors.Indigo);
    }
}
