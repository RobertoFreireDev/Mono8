namespace mono8.editor;

internal class MapEditor : IEditor
{
    private enum Tool
    {
        Pixel,
        RectFill,
        Select,
        Hand,
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

    // --- RectFill / Select drag ---
    private bool dragging;
    private int dragStartCellX;
    private int dragStartCellY;

    // --- Selection (Select tool): a committed area, kept in map-cell space so it stays
    // anchored to the map as the camera pans and zooms. ---
    private bool hasSelection;
    private Rectangle selection;

    // Marching-ants border animation.
    private const float AntsFrameSeconds = 0.12f;
    private static readonly int[] AntsPalette =
        { Constants.Colors.White, Constants.Colors.LightGray, Constants.Colors.DarkGray };
    private float antsElapsed;
    private int antsPhase;

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
            (new Button(2 * size, labelRowY - 1, size, 24), Tool.Select),
            (new Button(3 * size, labelRowY - 1, size, 26), Tool.Hand),
        };
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

    public void Exit()
    {
        ClearSelection();
    }

    private void ClearSelection()
    {
        hasSelection = false;
        dragging = false;
    }

    public void Update(float elapsedSeconds)
    {
        eventNotifier.Update(elapsedSeconds);

        antsElapsed += elapsedSeconds;
        while (antsElapsed >= AntsFrameSeconds)
        {
            antsElapsed -= AntsFrameSeconds;
            antsPhase = (antsPhase + 1) % AntsPalette.Length;
        }

        if (KeybrdInput.IsSaveShortcutPressed())
        {
            Mono8Game.GameAPI.Save();
            eventNotifier.AddEvent("SAVED");
        }

        UpdateEditShortcuts();

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
            foreach (var (button, tool) in toolButtons)
            {
                if (button.IsClicked(_api, mouse))
                {
                    if (tool != selectedTool) ClearSelection();
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

    private void UpdateEditShortcuts()
    {
        var map = Mono8API.MapSheet;

        if (KeybrdInput.IsUndoShortcutPressed() && map.CanUndo)
        {
            map.Undo();
            eventNotifier.AddEvent("UNDO");
        }
        else if (KeybrdInput.IsRedoShortcutPressed() && map.CanRedo)
        {
            map.Redo();
            eventNotifier.AddEvent("REDO");
        }

        if (!hasSelection) return;

        if (KeybrdInput.JustPressed(Keys.Delete))
        {
            map.ClearRegion(selection.X, selection.Y, selection.Width, selection.Height);
            eventNotifier.AddEvent("DELETE");
        }

        if (KeybrdInput.IsCopyShortcutPressed())
        {
            map.CopyRegion(selection.X, selection.Y, selection.Width, selection.Height);
            eventNotifier.AddEvent("COPY");
        }

        if (KeybrdInput.IsPasteShortcutPressed() && map.HasClipboard)
        {
            map.PasteRegion(selection.X, selection.Y);
            eventNotifier.AddEvent("PASTE");
        }
    }

    private void UpdateMapPainting((int x, int y) mouse, Rectangle mapArea)
    {
        var (cellX, cellY) = CellUnderMouse(mouse, mapArea);

        // Right-click anywhere on the map cancels the current selection / drag.
        if (_api.mouserp() && (hasSelection || dragging))
        {
            ClearSelection();
            eventNotifier.AddEvent("CANCEL");
            return;
        }

        if (selectedTool == Tool.Pixel)
        {
            if (!MapSheet.IsValidTile(navigator.SelectedSprite)) return;

            // One snapshot per stroke: taken on press, then paint under it while held.
            if (_api.mouselp()) Mono8API.MapSheet.SaveSnapshot();
            if (_api.mousel()) _api.mset(cellX, cellY, navigator.SelectedSprite);
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
                int minX = Math.Min(dragStartCellX, cellX);
                int minY = Math.Min(dragStartCellY, cellY);
                int w = Math.Abs(cellX - dragStartCellX) + 1;
                int h = Math.Abs(cellY - dragStartCellY) + 1;
                Mono8API.MapSheet.FillRegion(minX, minY, w, h, navigator.SelectedSprite);
                dragging = false;
            }
        }
        else if (selectedTool == Tool.Select)
        {
            if (_api.mouselp())
            {
                dragStartCellX = cellX;
                dragStartCellY = cellY;
                dragging = true;
            }
            else if (dragging && _api.mouselr())
            {
                CommitSelection(dragStartCellX, dragStartCellY, cellX, cellY);
                dragging = false;
            }
        }
    }

    private void CommitSelection(int x0, int y0, int x1, int y1)
    {
        int minX = Math.Clamp(Math.Min(x0, x1), 0, Constants.GameDataSizes.MapSheetX - 1);
        int minY = Math.Clamp(Math.Min(y0, y1), 0, Constants.GameDataSizes.MapSheetY - 1);
        int maxX = Math.Clamp(Math.Max(x0, x1), 0, Constants.GameDataSizes.MapSheetX - 1);
        int maxY = Math.Clamp(Math.Max(y0, y1), 0, Constants.GameDataSizes.MapSheetY - 1);

        selection = new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
        hasSelection = true;
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

        _api.map(camX, camY, mapArea.X, mapArea.Y, MapCols, MapRows, Zooms[zoomIdx]);

        // A committed selection is drawn wherever the camera is; it stays anchored to cells.
        if (hasSelection)
        {
            DrawMarchingAntsCells(mapArea, selection.X, selection.Y, selection.Width, selection.Height);
        }

        var mouse = _api.mousexy();
        if (dragging && mapArea.Contains(mouse.x, mouse.y) && !IsOverButtonRow(mouse))
        {
            var (cellX, cellY) = CellUnderMouse(mouse, mapArea);
            int minX = Math.Min(dragStartCellX, cellX);
            int minY = Math.Min(dragStartCellY, cellY);
            int w = Math.Abs(cellX - dragStartCellX) + 1;
            int h = Math.Abs(cellY - dragStartCellY) + 1;

            if (selectedTool == Tool.RectFill)
            {
                int px = mapArea.X + (minX - camX) * size;
                int py = mapArea.Y + (minY - camY) * size;
                _api.rectfill(px, py, px + w * size - 1, py + h * size - 1, Constants.Colors.White);
            }
            else if (selectedTool == Tool.Select)
            {
                DrawMarchingAntsCells(mapArea, minX, minY, w, h);
            }
        }
    }

    // Converts a cell-space rectangle to screen space at the current camera/zoom and outlines
    // it with an animated white / light-grey / dark-grey border, clipped to the map viewport.
    private void DrawMarchingAntsCells(Rectangle mapArea, int cellX, int cellY, int cellW, int cellH)
    {
        int size = CellPx;
        int x0 = mapArea.X + (cellX - camX) * size;
        int y0 = mapArea.Y + (cellY - camY) * size;
        int x1 = x0 + cellW * size - 1;
        int y1 = y0 + cellH * size - 1;

        void Ant(int x, int y, int t)
        {
            if (x < mapArea.X || x >= mapArea.X + mapArea.Width ||
                y < mapArea.Y || y >= mapArea.Y + mapArea.Height) return;
            _api.pixel(x, y, AntsPalette[(t + antsPhase) % AntsPalette.Length]);
        }

        int step = 0;
        for (int x = x0; x <= x1; x++) Ant(x, y0, step++);       // top, L->R
        for (int y = y0 + 1; y <= y1; y++) Ant(x1, y, step++);   // right, T->B
        for (int x = x1 - 1; x >= x0; x--) Ant(x, y1, step++);   // bottom, R->L
        for (int y = y1 - 1; y > y0; y--) Ant(x0, y, step++);    // left, B->T
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

        navigator.DrawPageButtons();
        navigator.DrawNumberLabel();
    }
}
