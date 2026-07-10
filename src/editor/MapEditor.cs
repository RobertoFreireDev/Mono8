namespace mono8.editor;

internal class MapEditor : IEditor
{
    private enum Tool
    {
        Pixel,
        RectFill,
        RectDelete,
        Hand,
    }

    // The map sheet doubles as its own second layer: the half of the sheet the camera is not
    // looking at is drawn at the same camera offset, either under or over the current map.
    private enum BackgroundMode
    {
        None,
        BottomBehind,
        BottomInFront,
    }

    private readonly IMono8API _api;
    private readonly EventNotifier eventNotifier;

    // --- Map viewport ---
    private const int MapTop = Constants.GameDataSizes.TileSize;
    // Cell sizes on screen: 4px, 8px (native), 16px.
    private static readonly float[] Zooms = { 0.5f, 1f, 2f };
    private const int DefaultZoomIdx = 1;
    private int zoomIdx = DefaultZoomIdx;
    private int camX;
    private int camY;

    // Set by the menu bar toggle (top-left, only shown while the map editor is active).
    public bool FullMapView { get; set; }

    // --- Sprite navigator (bottom panel, shared with SpriteEditor) ---
    private readonly SpriteNavigator navigator;
    private int labelRowY => navigator.LabelRowY;

    // --- Tools (drawn on the sprite-number / page-button row) ---
    private readonly (Button Button, Tool Tool)[] toolButtons;
    private Tool selectedTool = Tool.Pixel;

    // --- Background layer toggle (sits just right of the last tool button) ---
    private readonly Rectangle backgroundButton;
    private BackgroundMode backgroundMode = BackgroundMode.None;

    // --- RectFill drag ---
    private bool dragging;
    private int dragStartCellX;
    private int dragStartCellY;

    // --- Hand (pan) drag ---
    private bool panning;
    private int panStartMouseX;
    private int panStartMouseY;
    private int panStartCamX;
    private int panStartCamY;

    // --- Hover position (shown on the tool/sprite-number/page-button row) ---
    private bool hoveringMap;
    private int hoverCellX;
    private int hoverCellY;

    public MapEditor(IMono8API api)
    {
        _api = api;
        eventNotifier = new EventNotifier(api, 2f, 1, Constants.Screen.ResolutionY - Constants.GameDataSizes.TileSize + 1);

        navigator = new SpriteNavigator(api);

        int size = Constants.GameDataSizes.TileSize;
        toolButtons = new[]
        {
            (new Button(0 * size, labelRowY - 1, size, 25), Tool.Pixel),
            (new Button(1 * size, labelRowY - 1, size, 23), Tool.RectFill),
            (new Button(2 * size, labelRowY - 1, size, 24), Tool.RectDelete),
            (new Button(3 * size, labelRowY - 1, size, 26), Tool.Hand),
        };

        backgroundButton = new Rectangle(4 * size, labelRowY - 1, size, size);
    }

    private const int HalfMapX = Constants.GameDataSizes.MapSheetX / 2;
    private const int HalfMapY = Constants.GameDataSizes.MapSheetY / 2;

    private int BackgroundIcon => backgroundMode switch
    {
        BackgroundMode.BottomBehind or BackgroundMode.BottomInFront => 10,
        _ => 14,
    };

    // Dark grey marks the modes where the other half sits behind the map being edited.
    private bool BackgroundIsBehind =>
        backgroundMode is BackgroundMode.BottomBehind;

    private void CycleBackgroundMode(int delta)
    {
        int count = Enum.GetValues<BackgroundMode>().Length;
        backgroundMode = (BackgroundMode)(((int)backgroundMode + delta + count) % count);
    }

    private bool CameraInTopHalf => camY < HalfMapY;

    // Camera row of the same window taken from the sheet half the camera is not looking at.
    private int OtherHalfCamY => CameraInTopHalf ? camY + HalfMapY : camY - HalfMapY;

    // Jump the camera to the matching window in the other half of the sheet. Only meaningful
    // while a background layer is shown, since that layer is what the camera swaps with.
    private void ToggleMapHalf()
    {
        if (backgroundMode == BackgroundMode.None) return;

        camY = OtherHalfCamY;
        ClampCamera();

        eventNotifier.AddEvent(CameraInTopHalf ? "TOP HALF" : "BOTTOM HALF");
    }

    public void Init()
    {
    }

    private string ZoomLabel => Zooms[zoomIdx] switch
    {
        0.5f => "x1/2",
        var z => "x" + (int)z,
    };

    // On-screen size of one map cell at the current zoom. Every zoom level divides
    // TileSize evenly, so this stays an exact integer.
    private int CellPx => (int)(Constants.GameDataSizes.TileSize * Zooms[zoomIdx]);

    private int MapCols => Constants.Screen.ResolutionX / CellPx;

    // In split view, this rounds up (+1) so the map fills the gap down to the button row
    // instead of leaving a partial-tile strip; Update() excludes the button rects from
    // hoveringMap so that overlapping row still lets clicks reach the tool/page buttons.
    private int MapRows => FullMapView
        ? (EditorUI.BottomBarY - MapTop) / CellPx
        : (labelRowY - MapTop + CellPx - 1) / CellPx;

    private Rectangle MapArea => new Rectangle(0, MapTop, MapCols * CellPx, MapRows * CellPx);

    private void ClampCamera()
    {
        camX = Math.Clamp(camX, 0, Math.Max(0, Constants.GameDataSizes.MapSheetX - MapCols));
        camY = Math.Clamp(camY, 0, Math.Max(0, Constants.GameDataSizes.MapSheetY - MapRows));
    }

    // Zoom about the cursor: the cell under the mouse stays under the mouse.
    private void UpdateZoom((int x, int y) mouse, Rectangle mapArea)
    {
        int delta = _api.mouseup() ? 1 : _api.mousedown() ? -1 : 0;
        if (delta == 0) return;

        int next = Math.Clamp(zoomIdx + delta, 0, Zooms.Length - 1);
        if (next == zoomIdx) return;

        var (anchorX, anchorY) = CellUnderMouse(mouse, mapArea);
        zoomIdx = next;

        camX = anchorX - (mouse.x - mapArea.X) / CellPx;
        camY = anchorY - (mouse.y - mapArea.Y) / CellPx;
        ClampCamera();
    }

    public void Update(float elapsedSeconds)
    {
        eventNotifier.Update(elapsedSeconds);

        if (KeybrdInput.IsSaveShortcutPressed())
        {
            Mono8Game.GameAPI.Save();
            eventNotifier.AddEvent("SAVED");
        }

        if (KeybrdInput.JustPressed(Keys.Q)) ToggleMapHalf();

        // Pan the map viewport. Holding Control moves 8 tiles at a time instead of one.
        int panStep = KeybrdInput.IsCtrlPressed() ? 8 : 1;
        if (KeybrdInput.Pressed(Keys.Left)) camX -= panStep;
        if (KeybrdInput.Pressed(Keys.Right)) camX += panStep;
        if (KeybrdInput.Pressed(Keys.Up)) camY -= panStep;
        if (KeybrdInput.Pressed(Keys.Down)) camY += panStep;
        ClampCamera();

        var mouse = _api.mousexy();
        var mapArea = MapArea;

        if (!dragging && !panning && mapArea.Contains(mouse.x, mouse.y) && !IsOverButtonRow(mouse))
        {
            UpdateZoom(mouse, mapArea);
            mapArea = MapArea;   // a zoom step resizes the viewport
        }

        if (dragging && !mapArea.Contains(mouse.x, mouse.y))
        {
            dragging = false;
        }

        if (selectedTool == Tool.Hand)
        {
            if (!panning && mapArea.Contains(mouse.x, mouse.y) && !IsOverButtonRow(mouse) && _api.mouselp())
            {
                panning = true;
                panStartMouseX = mouse.x;
                panStartMouseY = mouse.y;
                panStartCamX = camX;
                panStartCamY = camY;
            }
            else if (panning && _api.mousel() && mapArea.Contains(mouse.x, mouse.y))
            {
                camX = panStartCamX + (panStartMouseX - mouse.x) / CellPx;
                camY = panStartCamY + (panStartMouseY - mouse.y) / CellPx;
                ClampCamera();
            }
            else
            {
                panning = false;
            }
        }
        else
        {
            panning = false;
        }

        hoveringMap = mapArea.Contains(mouse.x, mouse.y) && !IsOverButtonRow(mouse);

        if (hoveringMap)
        {
            (hoverCellX, hoverCellY) = CellUnderMouse(mouse, mapArea);
            UpdateMapPainting(mouse, mapArea);
        }
        else if (!FullMapView && navigator.ViewerArea.Contains(mouse.x, mouse.y))
        {
            if (_api.mousel()) navigator.SelectAt(mouse);
        }
        else if (!FullMapView)
        {
            if (backgroundButton.Contains(mouse.x, mouse.y))
            {
                if (_api.mouselp()) CycleBackgroundMode(1);
                else if (_api.mouserp()) CycleBackgroundMode(-1);
                return;
            }

            foreach (var (button, tool) in toolButtons)
            {
                if (button.IsClicked(_api, mouse))
                {
                    selectedTool = tool;
                    break;
                }
            }

            navigator.TryPickPage(mouse);
        }
    }

    // The extra (rounded-up) map row can overlap the dark grey tool/page-button row;
    // that whole row must keep taking priority over map hover/painting.
    private bool IsOverButtonRow((int x, int y) mouse) =>
        !FullMapView && mouse.y >= labelRowY - 1;

    private (int cellX, int cellY) CellUnderMouse((int x, int y) mouse, Rectangle mapArea)
    {
        int cellX = camX + (mouse.x - mapArea.X) / CellPx;
        int cellY = camY + (mouse.y - mapArea.Y) / CellPx;
        return (cellX, cellY);
    }

    private void UpdateMapPainting((int x, int y) mouse, Rectangle mapArea)
    {
        var (cellX, cellY) = CellUnderMouse(mouse, mapArea);

        if (selectedTool == Tool.Pixel)
        {
            if (_api.mousel()) _api.mset(cellX, cellY, navigator.SelectedSprite);
        }
        else if (selectedTool == Tool.RectFill || selectedTool == Tool.RectDelete)
        {
            if (_api.mouselp())
            {
                dragStartCellX = cellX;
                dragStartCellY = cellY;
                dragging = true;
            }
            else if (dragging && _api.mouselr())
            {
                int value = selectedTool == Tool.RectDelete ? 0 : navigator.SelectedSprite;
                ApplyRectFill(dragStartCellX, dragStartCellY, cellX, cellY, value);
                dragging = false;
            }
        }
    }

    private void ApplyRectFill(int x0, int y0, int x1, int y1, int value)
    {
        int minX = Math.Min(x0, x1);
        int minY = Math.Min(y0, y1);
        int maxX = Math.Max(x0, x1);
        int maxY = Math.Max(y0, y1);

        for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
                _api.mset(x, y, value);
    }

    public void Draw()
    {
        DrawMap();

        int bottomBarY = EditorUI.BottomBarY;
        _api.rectfill(0, bottomBarY, Constants.Screen.ResolutionX, Constants.Screen.ResolutionY - 1, Constants.Colors.Orange);

        const int charAdvance = 4;
        const int rightMargin = 2;
        const int labelGap = 4;
        const int hoverTextChars = 11;   // "X:000 Y:000" — fixed so the zoom label never shifts

        int hoverTextX = Constants.Screen.ResolutionX - rightMargin - hoverTextChars * charAdvance;

        string zoomText = ZoomLabel;
        _api.print(zoomText,
            hoverTextX - labelGap - zoomText.Length * charAdvance,
            bottomBarY + 1,
            Constants.Colors.Indigo);

        if (hoveringMap)
        {
            _api.print($"X:{hoverCellX:D3} Y:{hoverCellY:D3}", hoverTextX, bottomBarY + 1, Constants.Colors.Indigo);
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
        int size = CellPx;

        _api.rectfill(mapArea.X, mapArea.Y,
            mapArea.X + mapArea.Width - 1, mapArea.Y + mapArea.Height - 1,
            Constants.Colors.Black);

        DrawMapLayers(mapArea);

        var mouse = _api.mousexy();
        if (mapArea.Contains(mouse.x, mouse.y) && !IsOverButtonRow(mouse))
        {
            if (dragging && (selectedTool == Tool.RectFill || selectedTool == Tool.RectDelete))
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
                    selectedTool == Tool.RectDelete ? Constants.Colors.Red : Constants.Colors.White);
            }
        }
    }

    // The companion layer is the same camera window taken from the other half of the map sheet,
    // drawn under or over the map being edited depending on the mode.
    private void DrawMapLayers(Rectangle mapArea)
    {
        void Layer(int cellX, int cellY) =>
            _api.map(cellX, cellY, mapArea.X, mapArea.Y, MapCols, MapRows, Zooms[zoomIdx]);

        switch (backgroundMode)
        {
            case BackgroundMode.BottomBehind:
                Layer(camX, OtherHalfCamY);
                Layer(camX, camY);
                break;
            case BackgroundMode.BottomInFront:
                Layer(camX, camY);
                Layer(camX, OtherHalfCamY);
                break;
            default:
                Layer(camX, camY);
                break;
        }
    }

    private void DrawBackgroundButton()
    {
        if (BackgroundIsBehind) _api.pal(Constants.Colors.White, Constants.Colors.LightGray);
        _api.icon(BackgroundIcon, backgroundButton.X, backgroundButton.Y);
        _api.pal();
    }

    private void DrawSpriteNavigator()
    {
        // MapRows rounds up, so the bottom map row can spill past the button row and show
        // through the sprite sheet's transparent pixels. Blank the whole panel first.
        _api.rectfill(0, labelRowY - 1,
            Constants.Screen.ResolutionX, EditorUI.BottomBarY - 1,
            Constants.Colors.Black);

        navigator.DrawSheet();

        // Dark grey backs only the tool / sprite-number / page-button row.
        _api.rectfill(0, labelRowY - 1,
            Constants.Screen.ResolutionX, labelRowY - 2 + Constants.GameDataSizes.TileSize,
            Constants.Colors.DarkGray);

        foreach (var (button, tool) in toolButtons)
        {
            button.Draw(_api, tool == selectedTool);
        }

        DrawBackgroundButton();

        navigator.DrawPageButtons();
        navigator.DrawNumberLabel();
    }
}
