namespace mono8.editor;

internal class MapEditor : IEditor, IAutotileGrid, IEditorConfig
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
    // One game screen spans this many map cells; the reference grid marks screen boundaries.
    // The map sheet is an exact multiple of a screen (512x576 cells = 16x32 screens).
    private const int ScreenCols = Constants.Screen.ResolutionX / Constants.GameDataSizes.TileSize; // 32
    private const int ScreenRows = Constants.Screen.ResolutionY / Constants.GameDataSizes.TileSize; // 18
    // Cell sizes on screen: 4px, 8px (native), 16px.
    private static readonly float[] Zooms = { 0.5f, 1f, 2f };
    private const int DefaultZoomIdx = 1;
    private int zoomIdx = DefaultZoomIdx;
    private int camX;
    private int camY;

    // Arrow-key pan. A held arrow steps once every four frames rather than every one - at 60 FPS a
    // per-frame step runs away faster than the eye can follow the tiles going past.
    private const float PanRepeatInterval = 4f / 60f;
    private float panRepeatTimer;

    // Set by the menu bar toggle (top-left, only shown while the map editor is active).
    public bool FullMapView { get; set; }

    // --- Map layers ---
    // The map sheet is split into four equal quarters, each treated as an independent layer:
    // 1 top-left, 2 top-right, 3 bottom-left, 4 bottom-right. The viewport shows one quarter's
    // worth of area (so the camera moves in quarter-local cell space) and every layer is overlaid
    // there at its own quarter offset. Draw order front->back is 1,2,3,4, so they are painted
    // back-to-front and layer 1 lands on top.
    private const int LayerCount = 4;
    private const int QuarterCols = Constants.GameDataSizes.MapSheetX / 2; // 256
    private const int QuarterRows = Constants.GameDataSizes.MapSheetY / 2; // 288
    // Icons for the view/hide buttons: an open eye to show, a closed one to hide.
    private const int LayerViewIcon = 58;
    private const int LayerHideIcon = 59;
    // Opacity of layers drawn in front of the enabled layer, so it stays visible through them.
    private const float FrontLayerOpacity = 0.10f;
    // The eight layer buttons sit on the tool row, just past the four tool buttons.
    private const int LayerButtonsStartX = 5 * Constants.GameDataSizes.TileSize;

    // The layer edits currently target; only one is enabled at a time and it is always drawn.
    private int enabledLayer;
    private readonly bool[] layerVisible = { true, true, true, true };

    private static int LayerOffX(int layer) => (layer % 2) * QuarterCols;
    private static int LayerOffY(int layer) => (layer / 2) * QuarterRows;
    private int EnabledOffX => LayerOffX(enabledLayer);
    private int EnabledOffY => LayerOffY(enabledLayer);
    private static bool InQuarter(int cellX, int cellY) =>
        cellX >= 0 && cellX < QuarterCols && cellY >= 0 && cellY < QuarterRows;

    private Rectangle LayerButtonRect(int layer)
    {
        int size = Constants.GameDataSizes.TileSize;
        return new Rectangle(LayerButtonsStartX + layer * size * 2, labelRowY - 1, size, size);
    }

    private Rectangle ViewHideButtonRect(int layer)
    {
        int size = Constants.GameDataSizes.TileSize;
        return new Rectangle(LayerButtonsStartX + layer * size * 2 + size, labelRowY - 1, size, size);
    }

    // --- Autotile (47-piece) ---
    // Marks the selected sprite's 8x6 block as an autotile. The button sits on the tool row, one
    // tile past the layer buttons, and previews the block over the sheet while hovered.
    private const int AutotileButtonX =
        LayerButtonsStartX + LayerCount * 2 * Constants.GameDataSizes.TileSize + Constants.GameDataSizes.TileSize;

    private readonly Button autotileButton;
    private bool hoveringAutotile;

    // --- Sprite navigator (bottom panel, shared with SpriteEditor) ---
    private readonly SpriteNavigator navigator;
    private int labelRowY => navigator.LabelRowY;

    // --- Tools (drawn on the sprite-number / page-button row) ---
    private readonly (Button Button, Tool Tool)[] toolButtons;
    private Tool selectedTool = Tool.Pixel;
    private const int ToolCount = (int)Tool.Hand + 1;

    // --- RectFill / Select drag ---
    private bool dragging;
    private int dragStartCellX;
    private int dragStartCellY;

    // --- Selection (Select tool): a committed area, kept in map-cell space so it stays
    // anchored to the map as the camera pans and zooms. ---
    private bool hasSelection;
    private Rectangle selection;

    private readonly MarchingAnts ants = new();

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

        autotileButton = new Button(AutotileButtonX, labelRowY - 1, size, AutotileOverlay.Icon);

        ApplyConfig(Mono8API.ConfigSheet);
    }

    /// <summary>
    /// Brings the saved tool, layer setup and viewport back. The camera is clamped after the zoom
    /// is in, since how far it can travel is what the zoom decides.
    /// </summary>
    private void ApplyConfig(ConfigSheet config)
    {
        selectedTool = (Tool)EditorUI.ClampIndex(config.MapToolIdx, ToolCount);
        enabledLayer = EditorUI.ClampIndex(config.MapEnabledLayer, LayerCount);

        for (int i = 0; i < Math.Min(layerVisible.Length, config.MapLayerVisible.Length); i++)
        {
            layerVisible[i] = config.MapLayerVisible[i];
        }

        zoomIdx = EditorUI.ClampIndex(config.MapZoomIdx, Zooms.Length);
        camX = config.MapCamX;
        camY = config.MapCamY;
        ClampCamera();
    }

    void IEditorConfig.CaptureConfig(ConfigSheet config)
    {
        config.MapToolIdx = (int)selectedTool;
        config.MapEnabledLayer = enabledLayer;

        for (int i = 0; i < Math.Min(layerVisible.Length, config.MapLayerVisible.Length); i++)
        {
            config.MapLayerVisible[i] = layerVisible[i];
        }

        config.MapZoomIdx = zoomIdx;
        config.MapCamX = camX;
        config.MapCamY = camY;
    }

    // The 8x6 block the selected sprite belongs to. The block grid divides the sheet exactly, so
    // every sprite has one.
    private bool SelectedBlock(out int blockX, out int blockY) =>
        AutotileSheet.TryGetBlock(navigator.SelectedSprite, out blockX, out blockY);

    private bool SelectedBlockIsAutotile =>
        SelectedBlock(out int blockX, out int blockY) && Mono8API.AutotileSheet.IsEnabled(blockX, blockY);

    // The autotile piece the pencil stamps: the selected sprite, once its block is marked as an
    // autotile. False for any other sprite, which the pencil instead sets on the map as it is.
    private bool SelectedPiece(out int blockX, out int blockY, out int cell)
    {
        cell = 0;
        return SelectedBlock(out blockX, out blockY)
            && Mono8API.AutotileSheet.IsEnabled(blockX, blockY)
            && AutotileSheet.TryGetCell(navigator.SelectedSprite, blockX, blockY, out cell);
    }

    // The grid an autotile stroke paints on: the enabled layer's quarter of the map sheet, in the
    // quarter-local cells the editor works in, offset into the layer on every read and write.
    bool IAutotileGrid.Contains(int cellX, int cellY) => InQuarter(cellX, cellY);

    int IAutotileGrid.GetTile(int cellX, int cellY) => _api.mget(cellX + EnabledOffX, cellY + EnabledOffY);

    void IAutotileGrid.SetTile(int cellX, int cellY, int spriteId) =>
        _api.mset(cellX + EnabledOffX, cellY + EnabledOffY, spriteId);

    public void Init()
    {
    }

    private string ZoomLabel => Zooms[zoomIdx] switch
    {
        0.5f => "x1/2",
        var z => "x" + (int)z,
    };

    // Screen-grid cell (in the 16x32 grid of game screens) that the viewport's top-left corner
    // currently sits in, 0-based: "00x00" at the map's top-left. Each axis advances only after a
    // full screen has been scrolled past. Reported in map-sheet space, so the enabled layer's
    // quarter offset is included: the label says where on the sheet the view is, not where in the
    // quarter. Both quarter sizes are exact multiples of a screen, so this stays screen-aligned.
    private string GridLabel
    {
        get
        {
            int leftCellX = Math.Clamp(camX, 0, QuarterCols - 1) + EnabledOffX;
            int topCellY = Math.Clamp(camY, 0, QuarterRows - 1) + EnabledOffY;
            return $"{leftCellX / ScreenCols:D2}x{topCellY / ScreenRows:D2}";
        }
    }

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
        camX = Math.Clamp(camX, 0, Math.Max(0, QuarterCols - MapCols));
        camY = Math.Clamp(camY, 0, Math.Max(0, QuarterRows - MapRows));
    }

    // Pan the map viewport with the arrow keys. Holding Control moves 4 tiles at a time instead of
    // one. The first press moves at once; holding then repeats on the interval, so the pace is the
    // same whichever arrows are down.
    private void UpdateArrowPan(float elapsedSeconds)
    {
        int dx = (KeybrdInput.Pressed(Keys.Right) ? 1 : 0) - (KeybrdInput.Pressed(Keys.Left) ? 1 : 0);
        int dy = (KeybrdInput.Pressed(Keys.Down) ? 1 : 0) - (KeybrdInput.Pressed(Keys.Up) ? 1 : 0);

        if (dx == 0 && dy == 0)
        {
            panRepeatTimer = 0f;
            return;
        }

        panRepeatTimer -= elapsedSeconds;
        if (panRepeatTimer > 0f) return;
        panRepeatTimer += PanRepeatInterval;

        int panStep = KeybrdInput.IsCtrlPressed() ? 4 : 1;
        camX += dx * panStep;
        camY += dy * panStep;
        ClampCamera();
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

        ants.Update(elapsedSeconds);

        if (KeybrdInput.IsSaveShortcutPressed())
        {
            Mono8Game.GameAPI.Save();
            eventNotifier.AddEvent("SAVED");
        }

        UpdateEditShortcuts();

        UpdateArrowPan(elapsedSeconds);

        var mouse = _api.mousexy();
        var mapArea = MapArea;

        // Read-only pass over every control, kept out of the click chain below because the map and
        // the navigator take their own branches there and never reach the button row. The shared
        // menu bar sits above everything here, so it gets asked first.
        string controlLabel = Mono8API.MenuBar.HoverLabel ?? HoverLabelAt(mouse);
        if (controlLabel != null) eventNotifier.SetHover(controlLabel);

        EditorUI.HoverPointer(IsOverControl(mouse));

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
        hoveringAutotile = !FullMapView && autotileButton.Bounds.Contains(mouse.x, mouse.y);

        if (hoveringMap)
        {
            (hoverCellX, hoverCellY) = CellUnderMouse(mouse, mapArea);

            string mapLabel = MapHoverLabel(mouse, mapArea);
            if (mapLabel != null) eventNotifier.SetHover(mapLabel);

            UpdateMapPainting(mouse, mapArea);
        }
        else if (!FullMapView && navigator.ViewerArea.Contains(mouse.x, mouse.y))
        {
            if (_api.mousel()) navigator.SelectAt(mouse);
        }
        else if (!FullMapView)
        {
            HandleLayerButtons(mouse);
            HandleAutotileButton(mouse);

            foreach (var (button, tool) in toolButtons)
            {
                if (button.IsClicked(_api, mouse))
                {
                    if (tool != selectedTool) ClearSelection();
                    selectedTool = tool;
                    eventNotifier.AddEvent(ToolLabel(tool));
                    break;
                }
            }

            navigator.TryPickPage(mouse);
        }
    }

    // Right-click sets the enabled layer or toggles a layer's visibility. Only one layer is
    // enabled at a time, and the enabled layer can never be hidden (it stays visible so edits
    // are always shown).
    private void HandleLayerButtons((int x, int y) mouse)
    {
        if (!_api.mouselp()) return;

        for (int layer = 0; layer < LayerCount; layer++)
        {
            if (LayerButtonRect(layer).Contains(mouse.x, mouse.y))
            {
                if (layer != enabledLayer)
                {
                    enabledLayer = layer;
                    ClearSelection();
                }

                ShowAction(LayerLabel(layer));
                return;
            }

            if (ViewHideButtonRect(layer).Contains(mouse.x, mouse.y))
            {
                if (layer != enabledLayer) layerVisible[layer] = !layerVisible[layer];

                ShowAction(ViewHideLabel(layer));
                return;
            }
        }
    }

    // Marks the selected sprite's 8x6 block as a 47-piece autotile, or unmarks it.
    private void HandleAutotileButton((int x, int y) mouse)
    {
        if (!autotileButton.IsClicked(_api, mouse)) return;
        if (!SelectedBlock(out int blockX, out int blockY)) return;

        Mono8API.AutotileSheet.Toggle(blockX, blockY);
        eventNotifier.AddEvent(Mono8API.AutotileSheet.IsEnabled(blockX, blockY) ? "AUTOTILE ON" : "AUTOTILE OFF");
    }

    private static string ToolLabel(Tool tool) => tool switch
    {
        Tool.RectFill => "SQUARE",
        Tool.Select => "SELECTION",
        Tool.Hand => "HAND",
        _ => "PENCIL",
    };

    // The layer numbers are 1-based on the buttons, so the labels follow them rather than the
    // 0-based quarter index the code works in. Both lead with the action the button performs, so
    // they read as the opposite of the state it is currently in. The enabled layer's pair has no
    // action to name - it can be neither unselected nor hidden - and so goes unlabelled.
    private string LayerLabel(int layer) =>
        layer == enabledLayer ? null : $"SELECT LAYER {layer + 1}";

    private string ViewHideLabel(int layer) =>
        layer == enabledLayer ? null : $"{(layerVisible[layer] ? "HIDE" : "VIEW")} LAYER {layer + 1}";

    // A control's label, put up in place of whatever event is showing. The click that lands on one
    // of these buttons changes the very value its label carries, so the label is replaced with the
    // new one instead of keeping the action just carried out on screen. A null one clears the bar,
    // for the click that leaves the button with nothing to offer.
    private void ShowAction(string label)
    {
        eventNotifier.ClearEvent();
        eventNotifier.SetHover(label);
    }

    /// <summary>
    /// The name of the control under the cursor, carrying its current value where it holds one.
    /// Null over anything else, and over everything while the map fills the screen and the button
    /// row is gone. The areas are disjoint, so the first hit wins.
    /// </summary>
    private string HoverLabelAt((int x, int y) mouse)
    {
        if (FullMapView) return null;

        foreach (var (button, tool) in toolButtons)
        {
            if (button.Bounds.Contains(mouse.x, mouse.y)) return ToolLabel(tool);
        }

        for (int layer = 0; layer < LayerCount; layer++)
        {
            if (LayerButtonRect(layer).Contains(mouse.x, mouse.y)) return LayerLabel(layer);
            if (ViewHideButtonRect(layer).Contains(mouse.x, mouse.y)) return ViewHideLabel(layer);
        }

        if (autotileButton.Bounds.Contains(mouse.x, mouse.y)) return "AUTOTILE";

        return null;
    }

    /// <summary>
    /// Whether the cursor rests on a control. Separate from <see cref="HoverLabelAt"/>, which goes
    /// null on the enabled layer's own pair: those two buttons carry no action to name but are still
    /// buttons, and the pointer marks the row as chrome rather than as one more thing to paint on.
    /// </summary>
    private bool IsOverControl((int x, int y) mouse)
    {
        if (FullMapView) return false;

        foreach (var (button, _) in toolButtons)
        {
            if (button.Bounds.Contains(mouse.x, mouse.y)) return true;
        }

        for (int layer = 0; layer < LayerCount; layer++)
        {
            if (LayerButtonRect(layer).Contains(mouse.x, mouse.y)) return true;
            if (ViewHideButtonRect(layer).Contains(mouse.x, mouse.y)) return true;
        }

        return autotileButton.Bounds.Contains(mouse.x, mouse.y) || navigator.IsOverPageButton(mouse);
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

    // The map-sheet pixel under the cursor: the viewport's top-left pixel plus the offset into the
    // view, which zoom decides - one screen pixel covers TileSize / CellPx map pixels, so the
    // reading steps by two at x1/2 and repeats every second screen pixel at x2. Reported in
    // map-sheet space, so the enabled layer's quarter offset is included, as with GridLabel.
    private (int pixelX, int pixelY) PixelUnderMouse((int x, int y) mouse, Rectangle mapArea)
    {
        int size = Constants.GameDataSizes.TileSize;
        int pixelX = (camX + EnabledOffX) * size + (mouse.x - mapArea.X) * size / CellPx;
        int pixelY = (camY + EnabledOffY) * size + (mouse.y - mapArea.Y) * size / CellPx;
        return (pixelX, pixelY);
    }

    // What the cursor rests on over the map, in place of a control's name: the map-sheet pixel it
    // sits on and the sprite held by the cell it falls in, both taken on the enabled layer. Null
    // past that layer's quarter, which has no pixel or tile to report.
    private string MapHoverLabel((int x, int y) mouse, Rectangle mapArea)
    {
        var (cellX, cellY) = CellUnderMouse(mouse, mapArea);
        if (!InQuarter(cellX, cellY)) return null;

        var (pixelX, pixelY) = PixelUnderMouse(mouse, mapArea);
        int sprite = _api.mget(cellX + EnabledOffX, cellY + EnabledOffY);
        return $"X:{pixelX:D4} Y:{pixelY:D4} SPR:{sprite:D3}";
    }

    private void UpdateEditShortcuts()
    {
        var map = Mono8API.MapSheet;

        if (KeybrdInput.IsUndoShortcutPressed())
        {
            if (map.CanUndo)
            {
                map.Undo();
                eventNotifier.AddEvent("UNDO");
            }
            else
            {
                eventNotifier.AddEvent(EditorUI.NothingToUndoLabel);
            }
        }
        else if (KeybrdInput.IsRedoShortcutPressed())
        {
            if (map.CanRedo)
            {
                map.Redo();
                eventNotifier.AddEvent("REDO");
            }
            else
            {
                eventNotifier.AddEvent(EditorUI.NothingToRedoLabel);
            }
        }

        if (!hasSelection) return;

        // The selection is kept in quarter-local cells; every edit is offset into the enabled
        // layer's quarter and bounded to it so it never spills into a neighbouring layer.
        int offX = EnabledOffX;
        int offY = EnabledOffY;

        if (KeybrdInput.JustPressed(Keys.Delete))
        {
            map.ClearRegion(selection.X + offX, selection.Y + offY, selection.Width, selection.Height);
            eventNotifier.AddEvent("DELETE");
        }

        if (KeybrdInput.IsCopyShortcutPressed())
        {
            map.CopyRegion(selection.X + offX, selection.Y + offY, selection.Width, selection.Height);
            eventNotifier.AddEvent("COPY");
        }

        if (KeybrdInput.IsCutShortcutPressed())
        {
            map.CopyRegion(selection.X + offX, selection.Y + offY, selection.Width, selection.Height);
            map.ClearRegion(selection.X + offX, selection.Y + offY, selection.Width, selection.Height);
            eventNotifier.AddEvent("CUT");
        }

        if (KeybrdInput.IsPasteShortcutPressed() && map.HasClipboard)
        {
            map.PasteRegion(selection.X + offX, selection.Y + offY, offX, offY, QuarterCols, QuarterRows);
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
            if (!_api.mousel()) return;

            if (SelectedPiece(out int blockX, out int blockY, out int cell))
            {
                AutotileStamp.Paint(this, blockX, blockY, cellX, cellY, cell);
            }
            else if (InQuarter(cellX, cellY))
            {
                _api.mset(cellX + EnabledOffX, cellY + EnabledOffY, navigator.SelectedSprite);
            }
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
                // Clamp the region to the quarter before offsetting so the fill stays in-layer.
                int minX = Math.Max(0, Math.Min(dragStartCellX, cellX));
                int minY = Math.Max(0, Math.Min(dragStartCellY, cellY));
                int maxX = Math.Min(QuarterCols - 1, Math.Max(dragStartCellX, cellX));
                int maxY = Math.Min(QuarterRows - 1, Math.Max(dragStartCellY, cellY));
                if (maxX >= minX && maxY >= minY)
                {
                    Mono8API.MapSheet.FillRegion(minX + EnabledOffX, minY + EnabledOffY,
                        maxX - minX + 1, maxY - minY + 1, navigator.SelectedSprite);
                }
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
        // Selections stay in quarter-local cells; they are offset into the enabled layer at edit time.
        int minX = Math.Clamp(Math.Min(x0, x1), 0, QuarterCols - 1);
        int minY = Math.Clamp(Math.Min(y0, y1), 0, QuarterRows - 1);
        int maxX = Math.Clamp(Math.Max(x0, x1), 0, QuarterCols - 1);
        int maxY = Math.Clamp(Math.Max(y0, y1), 0, QuarterRows - 1);

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
        int zoomX = hoverTextX - labelGap - zoomText.Length * charAdvance;
        _api.print(zoomText, zoomX, bottomBarY + 1, Constants.Colors.Indigo);

        // Screen-grid position, sitting just to the left of the zoom label.
        string gridText = GridLabel;
        int gridX = zoomX - labelGap - gridText.Length * charAdvance;
        _api.print(gridText, gridX, bottomBarY + 1, Constants.Colors.Indigo);

        // The sprite the tools paint with, and its number just left of it.
        int tileX = gridX - labelGap - Constants.GameDataSizes.TileSize;
        DrawSelectedTile(tileX, bottomBarY);

        string spriteText = navigator.SelectedSprite.ToString("D3");
        _api.print(spriteText,
            tileX - labelGap - spriteText.Length * charAdvance,
            bottomBarY + 1,
            Constants.Colors.Indigo);

        if (hoveringMap)
        {
            // Map-sheet coordinates, i.e. the hovered cell offset into the enabled layer's quarter.
            _api.print($"X:{hoverCellX + EnabledOffX:D3} Y:{hoverCellY + EnabledOffY:D3}",
                hoverTextX, bottomBarY + 1, Constants.Colors.Indigo);
        }

        if (!FullMapView)
        {
            DrawSpriteNavigator();
        }

        eventNotifier.Draw();
    }

    // The sprite the tools paint with, one tile tall so it fills the bar's height. Backed with
    // black so its transparent pixels don't pick up the orange bar behind it.
    private void DrawSelectedTile(int x, int y)
    {
        int size = Constants.GameDataSizes.TileSize;
        _api.rectfill(x, y, x + size - 1, y + size - 1, Constants.Colors.Black);

        if (MapSheet.IsValidTile(navigator.SelectedSprite))
        {
            _api.spr(navigator.SelectedSprite, x, y);
        }
    }

    private void DrawMap()
    {
        var mapArea = MapArea;
        int size = CellPx;

        _api.rectfill(mapArea.X, mapArea.Y,
            mapArea.X + mapArea.Width - 1, mapArea.Y + mapArea.Height - 1,
            Constants.Colors.Black);

        DrawScreenGrid(mapArea);

        // Overlay the layers back-to-front (4->1) so layer 1 sits on top. The enabled layer is
        // always drawn; other layers only when visible. Layers drawn in front of the enabled one
        // (a lower index) are faded so the enabled layer shows through them.
        for (int layer = LayerCount - 1; layer >= 0; layer--)
        {
            if (layer != enabledLayer && !layerVisible[layer]) continue;

            float opacity = layer < enabledLayer ? FrontLayerOpacity : 1f;
            _api.map(camX + LayerOffX(layer), camY + LayerOffY(layer),
                mapArea.X, mapArea.Y, MapCols, MapRows, Zooms[zoomIdx], opacity);
        }

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

    // Draws a 1px dark-blue reference line on every game-screen boundary that falls inside the
    // viewport. Grid lines sit in map-cell space, so they stay anchored to the map as it pans and
    // zooms; drawn after the black background but before the tiles so tiles paint over them.
    private void DrawScreenGrid(Rectangle mapArea)
    {
        int size = CellPx;
        int right = mapArea.X + mapArea.Width - 1;
        int bottom = mapArea.Y + mapArea.Height - 1;

        // Vertical lines at every screen column boundary (cellX a multiple of ScreenCols).
        int firstCol = ((camX + ScreenCols - 1) / ScreenCols) * ScreenCols;
        for (int cellX = firstCol; cellX <= camX + MapCols; cellX += ScreenCols)
        {
            int px = mapArea.X + (cellX - camX) * size;
            if (px > right) break;
            _api.line(px, mapArea.Y, px, bottom, Constants.Colors.DarkBlue);
        }

        // Horizontal lines at every screen row boundary (cellY a multiple of ScreenRows).
        int firstRow = ((camY + ScreenRows - 1) / ScreenRows) * ScreenRows;
        for (int cellY = firstRow; cellY <= camY + MapRows; cellY += ScreenRows)
        {
            int py = mapArea.Y + (cellY - camY) * size;
            if (py > bottom) break;
            _api.line(mapArea.X, py, right, py, Constants.Colors.DarkBlue);
        }
    }

    // Converts a cell-space rectangle to screen space at the current camera/zoom and outlines
    // it with the animated selection border, clipped to the map viewport.
    private void DrawMarchingAntsCells(Rectangle mapArea, int cellX, int cellY, int cellW, int cellH)
    {
        int size = CellPx;
        int x0 = mapArea.X + (cellX - camX) * size;
        int y0 = mapArea.Y + (cellY - camY) * size;

        ants.Draw(_api, x0, y0, x0 + cellW * size - 1, y0 + cellH * size - 1, mapArea);
    }

    private void DrawSpriteNavigator()
    {
        // MapRows rounds up, so the bottom map row can spill past the button row and show
        // through the sprite sheet's transparent pixels. Blank the whole panel first.
        _api.rectfill(0, labelRowY - 1,
            Constants.Screen.ResolutionX, EditorUI.BottomBarY - 1,
            Constants.Colors.Black);

        navigator.DrawSheet();

        if (hoveringAutotile) DrawAutotilePreview();

        // Dark grey backs only the tool / sprite-number / page-button row.
        _api.rectfill(0, labelRowY - 1,
            Constants.Screen.ResolutionX, labelRowY - 2 + Constants.GameDataSizes.TileSize,
            Constants.Colors.DarkGray);

        foreach (var (button, tool) in toolButtons)
        {
            button.Draw(_api, tool == selectedTool);
        }

        DrawLayerButtons();

        autotileButton.Draw(_api, SelectedBlockIsAutotile);

        // The selected sprite's number lives on the bottom bar here, next to its tile preview,
        // so the navigator's own label is left out of this row.
        navigator.DrawPageButtons();
    }

    // Overlays the selected sprite's 8x6 block on the sheet while the autotile button is hovered:
    // which block the button will affect, and the neighbourhood each of its cells is drawn for.
    private void DrawAutotilePreview()
    {
        if (!SelectedBlock(out int blockX, out int blockY)) return;

        // A block is exactly as tall as a page, and there are as many block rows as pages, so a
        // block row is a page: either the whole block is on this one or none of it is.
        if (blockY != navigator.Page) return;

        int fill = AutotileOverlay.Fill(blockX, blockY);
        int size = Constants.GameDataSizes.TileSize;
        var viewer = navigator.ViewerArea;

        for (int cellY = 0; cellY < AutotileSheet.BlockH; cellY++)
        {
            int y = viewer.Y + cellY * size;

            for (int cellX = 0; cellX < AutotileSheet.BlockW; cellX++)
            {
                int x = viewer.X + (blockX * AutotileSheet.BlockW + cellX) * size;
                AutotileOverlay.DrawCell(_api, x, y, size, cellY * AutotileSheet.BlockW + cellX, fill);
            }
        }
    }

    // The eight layer controls, in pairs: a numbered "layer" swatch (white when enabled, dark grey
    // when not) followed by its view/hide toggle (eye open when drawn, closed when hidden). The
    // enabled layer's toggle always shows the open eye since it can't be hidden.
    private void DrawLayerButtons()
    {
        for (int layer = 0; layer < LayerCount; layer++)
        {
            bool enabled = layer == enabledLayer;

            var lb = LayerButtonRect(layer);
            _api.rectfill(lb.X, lb.Y, lb.X + lb.Width - 1, lb.Y + lb.Height - 1,
                enabled ? Constants.Colors.White : Constants.Colors.DarkGray);
            _api.print((layer + 1).ToString(), lb.X + 2, lb.Y + 1,
                enabled ? Constants.Colors.Indigo : Constants.Colors.White);

            var vb = ViewHideButtonRect(layer);
            bool visible = enabled || layerVisible[layer];
            _api.icon(visible ? LayerViewIcon : LayerHideIcon, vb.X, vb.Y);
        }
    }
}
