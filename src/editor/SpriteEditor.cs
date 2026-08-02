namespace mono8.editor;

internal class SpriteEditor : IEditor
{
    private enum Tool
    {
        Pixel,
        Rect,
        RectFill,
        Oval,
        OvalFill,
        PaintBucket,
        Select,
    }

    private readonly IEditorAPI _api;
    private Rectangle sprcnvsarea;
    private Rectangle palettearea;
    private int SprSclIdx = 0;
    private int[] Zooms = { 1, 2, 4, 8 };
    private int ColorSelected = Constants.Colors.White;
    private readonly EventNotifier eventNotifier;
    private readonly SpriteNavigator navigator;

    // Screen pixels per sheet pixel on the canvas: the whole 8x8-tile canvas keeps a fixed
    // size, so zooming in on more tiles shrinks each one.
    private int CanvasScale => Constants.GameDataSizes.TileSize / Zooms[SprSclIdx];

    private enum ReferenceOrder { Behind, Front }
    private enum ReferenceVisualization { Original, Red, Green, Blue }
    private const int ReferenceVisualizationCount = 4;

    private static readonly float[] ReferenceOpacities = { 0.2f, 0.4f, 0.6f, 0.8f, 1.0f };
    private const int SpriteCount = Constants.GameDataSizes.MaxSpriteIndex + 1;

    private bool editingReferenceNumber;
    private int referenceNumberInput = -1;

    // Order, tint and opacity are per-sprite like the reference number itself: each tile keeps the
    // onion setup it was drawn with, so switching tiles never rewrites another tile's settings.
    private readonly ReferenceOrder[] referenceOrders = new ReferenceOrder[SpriteCount];
    private readonly int[] referenceVisualizationIdxs = new int[SpriteCount];
    private readonly int[] referenceOpacityIdxs = new int[SpriteCount];

    private ReferenceOrder referenceOrder => referenceOrders[sprNmbr];
    private ReferenceVisualization referenceVisualization => (ReferenceVisualization)referenceVisualizationIdxs[sprNmbr];
    private float referenceOpacity => ReferenceOpacities[referenceOpacityIdxs[sprNmbr]];

    private Rectangle refNumberBtn;
    private Rectangle refOrderBtn;
    private Rectangle refVisualizationBtn;
    private Rectangle refOpacityBtn;

    // Ordered so that a left-click steps forward through the list and a right-click steps back.
    private enum LoopMode
    {
        Pause,
        Forward,
        Reverse,
        PingPong,
    }
    private const int LoopModeCount = 4;

    private const int AnimFrameCount = 8;
    private int[] AnimFrames = { -1, -1, -1, -1, -1, -1, -1, -1 };
    private int AnimSclIdx = 0;
    private int[] AnimSpeeds = { 1, 2, 4, 6, 8, 10 };
    private int AnimSpeedIdx = 0;
    private int animLoopModeIdx = (int)LoopMode.Pause;
    private LoopMode animLoopMode => (LoopMode)animLoopModeIdx;
    private int animCurrentFrame = 0;
    private int animPingPongDir = 1;
    private float animElapsed = 0f;

    private readonly Rectangle[] animFrameSlots;
    private Rectangle animZoomBtn;
    private Rectangle animSpeedBtn;
    private Rectangle animLoopModeBtn;
    private Rectangle animPreviewArea;

    private readonly (Button Button, Tool Tool)[] toolButtons;
    private Tool selectedTool = Tool.Pixel;

    // The tool row runs the width of the palette, eight buttons across. The guide sits between the
    // paint tools and the selection tool, so both keep a fixed slot rather than one following the
    // other's count.
    private const int AutotileGuideSlot = 6;
    private const int SelectSlot = 7;
    private const int SelectIcon = 24;

    // --- Autotile guide ---
    // Overlays the terrain a 4x4 autotile block is expected to hold on the canvas. Its button sits
    // on the tool row but is not one of the tools: it toggles on its own, so the guide can be shown
    // while any paint tool is selected.
    private readonly Button autotileGuideButton;
    private bool showAutotileGuide;

    // --- Dither row ---
    // A second row under the tools, live only while the pencil is selected. Slot 0 is the plain pencil
    // (a solid mask); slots 1-7 each hold the sprite whose non-black pixels stencil it, -1 until one is
    // given. The slots are exclusive, so the row always has exactly one of them active.
    private const int DitherSlotCount = 8;
    private const int DitherSolidSlot = 0;
    private readonly int[] ditherSprites = new int[DitherSlotCount];
    private readonly Rectangle[] ditherButtons = new Rectangle[DitherSlotCount];
    private int selectedDitherSlot = DitherSolidSlot;

    private int ActiveDitherSprite =>
        selectedDitherSlot == DitherSolidSlot ? -1 : ditherSprites[selectedDitherSlot];

    // --- Selection (Select tool): a committed area of the canvas, kept in sheet-pixel space. It
    // only describes the canvas it was drawn on, so changing sprite or zoom drops it. ---
    private bool hasSelection;
    private Rectangle selection;
    private int selectionSprite;
    private int selectionZoomIdx;
    private readonly MarchingAnts ants = new();

    private const int FlagCount = 8;
    private const int FlagIconIndex = 43;
    private readonly Rectangle[] flagButtons;
    private bool dragging;
    private int dragStartX;
    private int dragStartY;
    private readonly ShapePreviewGrid shapePreview = new();

    private const int PaletteColumns = 8;
    private const int PaletteRows = Constants.GameDataSizes.ColorPalette / PaletteColumns;

    // What the cursor is over, shown on the right edge of the bottom bar: the sprite under the
    // navigator, or the sheet pixel under the canvas. Null when it is over neither.
    private string hoverLabel;

    private int sprNmbr => navigator.SelectedSprite;

    public SpriteEditor(IEditorAPI api)
    {
        _api = api;
        eventNotifier = new EventNotifier(api, 2f, 1, Constants.Screen.ResolutionY - Constants.GameDataSizes.TileSize + 1);
        navigator = new SpriteNavigator(api);

        // Behind and Original are the zero values already; only opacity defaults to fully opaque.
        Array.Fill(referenceOpacityIdxs, ReferenceOpacities.Length - 1);

        sprcnvsarea = new Rectangle(100, 15, 8 * 8, 8 * 8);
        const int rightMargin = 2;
        int paletteWidth = PaletteColumns * Constants.GameDataSizes.TileSize;
        int paletteHeight = PaletteRows * Constants.GameDataSizes.TileSize;
        palettearea = new Rectangle(Constants.Screen.ResolutionX - paletteWidth - rightMargin, 15, paletteWidth, paletteHeight);

        int refBtnX = sprcnvsarea.X + sprcnvsarea.Width + 2;
        int refBtnW = 20;
        int refBtnH = Constants.GameDataSizes.TileSize;
        refNumberBtn = new Rectangle(refBtnX, sprcnvsarea.Y, refBtnW, refBtnH);
        refOrderBtn = new Rectangle(refBtnX, sprcnvsarea.Y + (refBtnH + 1), refBtnW, refBtnH);
        refVisualizationBtn = new Rectangle(refBtnX, sprcnvsarea.Y + 2 * (refBtnH + 1), refBtnW, refBtnH);
        refOpacityBtn = new Rectangle(refBtnX, sprcnvsarea.Y + 3 * (refBtnH + 1), refBtnW, refBtnH);

        int toolButtonY = palettearea.Y + palettearea.Height + 2;
        int size = Constants.GameDataSizes.TileSize;
        toolButtons = new[]
        {
            (new Button(palettearea.X + 0 * size, toolButtonY, size, 25), Tool.Pixel),
            (new Button(palettearea.X + 1 * size, toolButtonY, size, 14), Tool.Rect),
            (new Button(palettearea.X + 2 * size, toolButtonY, size, 23), Tool.RectFill),
            (new Button(palettearea.X + 3 * size, toolButtonY, size, 27), Tool.Oval),
            (new Button(palettearea.X + 4 * size, toolButtonY, size, 28), Tool.OvalFill),
            (new Button(palettearea.X + 5 * size, toolButtonY, size, 29), Tool.PaintBucket),
            (new Button(palettearea.X + SelectSlot * size, toolButtonY, size, SelectIcon), Tool.Select),
        };

        autotileGuideButton = new Button(palettearea.X + AutotileGuideSlot * size, toolButtonY, size, AutotileOverlay.Icon);

        Array.Fill(ditherSprites, -1);

        int ditherButtonY = toolButtonY + size + 1;
        for (int i = 0; i < DitherSlotCount; i++)
        {
            ditherButtons[i] = new Rectangle(palettearea.X + i * size, ditherButtonY, size, size);
        }

        int flagButtonY = toolButtonY + size + 10;
        flagButtons = new Rectangle[FlagCount];
        for (int i = 0; i < FlagCount; i++)
        {
            flagButtons[i] = new Rectangle(palettearea.X + i * size, flagButtonY, size, size);
        }

        animFrameSlots = new Rectangle[AnimFrameCount];
        for (int i = 0; i < AnimFrameCount; i++)
        {
            animFrameSlots[i] = new Rectangle(2, 15 + i * size, size, size);
        }
        animZoomBtn = new Rectangle(14, 15, 11, size);
        animSpeedBtn = new Rectangle(14, 25, 11, size);
        animLoopModeBtn = new Rectangle(14, 35, 11, size);
        animPreviewArea = new Rectangle(32, 15, 8 * 8, 8 * 8);
    }

    public void Init()
    {
        ClearSelection();
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

    /// <summary>The sheet-space square covered by <paramref name="spriteIndex"/> at a zoom of <paramref name="zoom"/> tiles.</summary>
    private static (int x, int y, int w, int h) CanvasRegion(int spriteIndex, int zoom)
    {
        int size = zoom * Constants.GameDataSizes.TileSize;
        int x = (spriteIndex % Constants.GameDataSizes.SpriteSheetColumns) * Constants.GameDataSizes.TileSize;
        int y = (spriteIndex / Constants.GameDataSizes.SpriteSheetColumns) * Constants.GameDataSizes.TileSize;
        return (x, y, size, size);
    }

    private (int x, int y, int w, int h) CurrentCanvasRegion() => CanvasRegion(sprNmbr, Zooms[SprSclIdx]);

    /// <summary>
    /// What the clipboard and delete act on: the selection when there is one, the whole canvas
    /// otherwise. The shape-preserving transforms stay on the canvas either way.
    /// </summary>
    private (int x, int y, int w, int h) ActiveRegion() =>
        hasSelection
            ? (selection.X, selection.Y, selection.Width, selection.Height)
            : CurrentCanvasRegion();

    /// <summary>The sheet pixel the cursor sits on, valid only while it is over the canvas.</summary>
    private (int x, int y) SheetPixelUnderMouse((int x, int y) mouse)
    {
        int x = (mouse.x - sprcnvsarea.X) * Zooms[SprSclIdx] / Constants.GameDataSizes.TileSize
            + (sprNmbr % Constants.GameDataSizes.SpriteSheetColumns) * Constants.GameDataSizes.TileSize;
        int y = (mouse.y - sprcnvsarea.Y) * Zooms[SprSclIdx] / Constants.GameDataSizes.TileSize
            + (sprNmbr / Constants.GameDataSizes.SpriteSheetColumns) * Constants.GameDataSizes.TileSize;
        return (x, y);
    }

    /// <summary>How much of a region actually falls inside the sheet; the rest is empty workspace.</summary>
    private static (int w, int h) VisibleSize(int regionX, int regionY, int regionW, int regionH) =>
        (Math.Min(regionW, Constants.GameDataSizes.SpriteSheetX - regionX),
         Math.Min(regionH, Constants.GameDataSizes.SpriteSheetY - regionY));

    private void DrawEmptyWorkspacePattern(int x, int y, int w, int h)
    {
        const int stripe = 4;
        for (int py = 0; py < h; py++)
        {
            for (int px = 0; px < w; px++)
            {
                int color = ((px + py) / stripe) % 2 == 0 ? Constants.Colors.DarkBlue : Constants.Colors.Black;
                _api.pixel(x + px, y + py, color);
            }
        }
    }

    /// <summary>Hatches the parts of a canvas area that lie past the edge of the sheet.</summary>
    private void DrawWorkspaceOverflow(Rectangle area, int regionW, int regionH, int validW, int validH, int scale)
    {
        if (validW < regionW)
        {
            DrawEmptyWorkspacePattern(area.X + validW * scale, area.Y,
                (regionW - validW) * scale, regionH * scale);
        }

        if (validH < regionH)
        {
            DrawEmptyWorkspacePattern(area.X, area.Y + validH * scale,
                validW * scale, (regionH - validH) * scale);
        }
    }

    private void DrawEmptySpriteCross(int x, int y, int size)
    {
        const int steps = 5;
        int blockW = size / steps;
        int blockH = size / steps;
        int offsetX = x + (size - steps * blockW) / 2;
        int offsetY = y + (size - steps * blockH) / 2;
        for (int i = 0; i < steps; i++)
        {
            int bx = offsetX + i * blockW;
            // top-left to bottom-right diagonal
            int by1 = offsetY + i * blockH;
            _api.rectfill(bx, by1, bx + blockW - 1, by1 + blockH - 1, Constants.Colors.White);
            // top-right to bottom-left diagonal
            int by2 = offsetY + (steps - 1 - i) * blockH;
            _api.rectfill(bx, by2, bx + blockW - 1, by2 + blockH - 1, Constants.Colors.White);
        }
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

        if (KeybrdInput.IsUndoShortcutPressed())
        {
            if (Mono8API.SpriteSheet.CanUndo)
            {
                Mono8API.SpriteSheet.Undo();
                eventNotifier.AddEvent("UNDO");
            }
            else
            {
                eventNotifier.AddEvent(EditorUI.NothingToUndoLabel);
            }
        }

        if (KeybrdInput.IsRedoShortcutPressed())
        {
            if (Mono8API.SpriteSheet.CanRedo)
            {
                Mono8API.SpriteSheet.Redo();
                eventNotifier.AddEvent("REDO");
            }
            else
            {
                eventNotifier.AddEvent(EditorUI.NothingToRedoLabel);
            }
        }

        int moveX = 0, moveY = 0;
        if (KeybrdInput.JustPressed(Keys.Left)) moveX -= 1;
        if (KeybrdInput.JustPressed(Keys.Right)) moveX += 1;
        if (KeybrdInput.JustPressed(Keys.Up)) moveY -= 1;
        if (KeybrdInput.JustPressed(Keys.Down)) moveY += 1;
        if (moveX != 0 || moveY != 0)
        {
            var (regionX, regionY, regionW, regionH) = CurrentCanvasRegion();
            Mono8API.SpriteSheet.MoveGrid(regionX, regionY, regionW, regionH, moveX, moveY);
        }

        if (KeybrdInput.JustPressed(Keys.Delete) && !editingReferenceNumber)
        {
            var (regionX, regionY, regionW, regionH) = ActiveRegion();
            Mono8API.SpriteSheet.ClearGrid(regionX, regionY, regionW, regionH);
        }

        if (KeybrdInput.IsCopyShortcutPressed())
        {
            var (regionX, regionY, regionW, regionH) = ActiveRegion();
            Mono8API.SpriteSheet.CopyRegion(regionX, regionY, regionW, regionH);
            eventNotifier.AddEvent("COPY");
        }

        if (KeybrdInput.IsCutShortcutPressed())
        {
            var (regionX, regionY, regionW, regionH) = ActiveRegion();
            Mono8API.SpriteSheet.CopyRegion(regionX, regionY, regionW, regionH);
            Mono8API.SpriteSheet.ClearGrid(regionX, regionY, regionW, regionH);
            eventNotifier.AddEvent("CUT");
        }

        if (KeybrdInput.IsPasteShortcutPressed())
        {
            // Anchored to the region and bounded by it, so a larger clipboard is trimmed instead of
            // bleeding into the sprites around it.
            var (regionX, regionY, regionW, regionH) = ActiveRegion();
            Mono8API.SpriteSheet.PasteRegion(regionX, regionY, regionX, regionY, regionW, regionH);
            eventNotifier.AddEvent("PASTE");
        }

        if (KeybrdInput.JustPressed(Keys.F))
        {
            var (regionX, regionY, regionW, regionH) = CurrentCanvasRegion();
            Mono8API.SpriteSheet.FlipRegionHorizontal(regionX, regionY, regionW, regionH);
            eventNotifier.AddEvent("FLIP H");
        }

        if (!KeybrdInput.IsCtrlPressed() && KeybrdInput.JustPressed(Keys.V))
        {
            var (regionX, regionY, regionW, regionH) = CurrentCanvasRegion();
            Mono8API.SpriteSheet.FlipRegionVertical(regionX, regionY, regionW, regionH);
            eventNotifier.AddEvent("FLIP V");
        }

        if (KeybrdInput.JustPressed(Keys.R))
        {
            var (regionX, regionY, regionW, regionH) = CurrentCanvasRegion();
            Mono8API.SpriteSheet.RotateRegion90Clockwise(regionX, regionY, regionW, regionH);
            eventNotifier.AddEvent("ROTATE");
        }

        if (KeybrdInput.NoModifiersPressed() && !editingReferenceNumber)
        {
            Keys[] digitKeys = { Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8 };
            for (int i = 0; i < digitKeys.Length; i++)
            {
                if (KeybrdInput.JustPressed(digitKeys[i]))
                {
                    AnimFrames[i] = AnimFrames[i] == -1 ? sprNmbr : -1;
                    eventNotifier.AddEvent($"ANIM {i + 1} {(AnimFrames[i] == -1 ? "OFF" : "ON")}");
                }
            }

            if (KeybrdInput.JustPressed(Keys.D9))
            {
                for (int i = 0; i < AnimFrames.Length; i++) AnimFrames[i] = -1;
            }
        }

        if (editingReferenceNumber && !KeybrdInput.IsCtrlPressed())
        {
            int digit = KeybrdInput.JustPressedDigit();
            if (digit >= 0)
            {
                int candidate = (referenceNumberInput < 1 ? 0 : referenceNumberInput) * 10 + digit;
                if (candidate >= 1 && candidate <= Constants.GameDataSizes.MaxSpriteIndex)
                {
                    referenceNumberInput = candidate;
                    Mono8API.SpriteSheet.SetReferenceSprite(sprNmbr, referenceNumberInput);
                }
            }

            if (KeybrdInput.JustPressed(Keys.Delete))
            {
                referenceNumberInput = -1;
                Mono8API.SpriteSheet.SetReferenceSprite(sprNmbr, -1);
            }
        }

        UpdateAnimationPlayback(elapsedSeconds);

        var mouse = _api.mousexy();

        if (editingReferenceNumber && (_api.mouselp() || _api.mouserp()) && !refNumberBtn.Contains(mouse.x, mouse.y))
        {
            editingReferenceNumber = false;
        }

        if (_api.mousedown())
        {
            SprSclIdx += 1;
        }

        if (_api.mouseup())
        {
            SprSclIdx -= 1;
        }

        SprSclIdx = Math.Clamp(SprSclIdx, 0, Zooms.Length - 1);

        if (dragging && !sprcnvsarea.Contains(mouse.x, mouse.y))
        {
            dragging = false;
            shapePreview.Clear();
        }

        // Read-only pass over every control, kept out of the click chain below because the palette
        // takes its own branch there and never reaches UpdateSideButtons. The shared menu bar sits
        // above everything here, so it gets asked first.
        string controlLabel = Mono8API.MenuBar.HoverLabel ?? HoverLabelAt(mouse);
        if (controlLabel != null) eventNotifier.SetHover(controlLabel);

        hoverLabel = null;

        if (navigator.ViewerArea.Contains(mouse.x, mouse.y))
        {
            hoverLabel = $"SPR:{navigator.SpriteUnderMouse(mouse):D3}";

            if (_api.mousel())
            {
                navigator.SelectAt(mouse);
            }
            else if (_api.mouserp())
            {
                int picked = navigator.SpriteUnderMouse(mouse);

                // Sprite 0 is the empty sprite, and a sprite cannot reference itself.
                int reference = (picked == 0 || picked == sprNmbr) ? -1 : picked;
                Mono8API.SpriteSheet.SetReferenceSprite(sprNmbr, reference);
            }
        }
        else if (sprcnvsarea.Contains(mouse.x, mouse.y))
        {
            var (x, y) = SheetPixelUnderMouse(mouse);

            // Zooming out brings in tiles past the sheet's edge; that empty workspace has no
            // sheet pixel to report.
            if (x < Constants.GameDataSizes.SpriteSheetX && y < Constants.GameDataSizes.SpriteSheetY)
            {
                hoverLabel = $"X:{x:D3} Y:{y:D3}";
            }

            if (selectedTool == Tool.Select)
            {
                if (_api.mouserp() && (hasSelection || dragging))
                {
                    ClearSelection();
                    eventNotifier.AddEvent("CANCEL");
                }
                else if (_api.mouselp())
                {
                    dragStartX = x;
                    dragStartY = y;
                    dragging = true;
                }
                else if (dragging && _api.mouselr())
                {
                    CommitSelection(dragStartX, dragStartY, x, y);
                    dragging = false;
                }
            }
            else if (selectedTool == Tool.Pixel)
            {
                if (_api.mousel()) _api.SetPixelDithered(x, y, ColorSelected, ActiveDitherSprite);
            }
            else if (selectedTool == Tool.PaintBucket)
            {
                if (_api.mouselp())
                {
                    var (regionX, regionY, regionW, regionH) = CurrentCanvasRegion();
                    _api.SetPaintBucket(x, y, regionX, regionY, regionW, regionH, ColorSelected);
                }
            }
            else if (_api.mouselp())
            {
                dragStartX = x;
                dragStartY = y;
                dragging = true;
            }
            else if (dragging)
            {
                shapePreview.Clear();
                UpdateShapePreview(dragStartX, dragStartY, x, y);

                if (_api.mouselr())
                {
                    ApplyShapeTool(dragStartX, dragStartY, x, y);
                    shapePreview.Clear();
                    dragging = false;
                }
            }
        }
        else if (palettearea.Contains(mouse.x, mouse.y))
        {
            if (_api.mousel())
            {
                ColorSelected = ColorIndexAt(mouse);
            }
        }
        else
        {
            UpdateSideButtons(mouse);
        }

        // A selection describes one canvas, so anything that swaps the canvas out from under it -
        // picking another sprite, zooming - drops it. Checked last so the frame that made the
        // change already draws without it.
        if (hasSelection && (sprNmbr != selectionSprite || SprSclIdx != selectionZoomIdx))
        {
            ClearSelection();
        }
    }

    // Selections are clamped to the part of the canvas that holds real sheet pixels, so one can
    // never cover the hatched workspace a zoomed-out canvas shows past the edge of the sheet.
    private void CommitSelection(int x0, int y0, int x1, int y1)
    {
        var (regionX, regionY, regionW, regionH) = CurrentCanvasRegion();
        var (validW, validH) = VisibleSize(regionX, regionY, regionW, regionH);

        int minX = Math.Clamp(Math.Min(x0, x1), regionX, regionX + validW - 1);
        int minY = Math.Clamp(Math.Min(y0, y1), regionY, regionY + validH - 1);
        int maxX = Math.Clamp(Math.Max(x0, x1), regionX, regionX + validW - 1);
        int maxY = Math.Clamp(Math.Max(y0, y1), regionY, regionY + validH - 1);

        selection = new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
        hasSelection = true;
        selectionSprite = sprNmbr;
        selectionZoomIdx = SprSclIdx;
    }

    private void UpdateSideButtons((int x, int y) mouse)
    {
        // Not a tool: it only flips the guide, leaving the selected tool alone.
        if (autotileGuideButton.IsClicked(_api, mouse))
        {
            showAutotileGuide = !showAutotileGuide;
            eventNotifier.AddEvent(showAutotileGuide ? "GUIDE ON" : "GUIDE OFF");
            return;
        }

        // Inert under any other tool: the row still draws, but only the pencil paints through a mask.
        if (selectedTool == Tool.Pixel && UpdateDitherButtons(mouse)) return;

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

        for (int i = 0; i < flagButtons.Length; i++)
        {
            if (flagButtons[i].Contains(mouse.x, mouse.y) && _api.mouselp())
            {
                bool current = Mono8API.SpriteSheet.GetFlag(sprNmbr, i);
                Mono8API.SpriteSheet.SetFlag(sprNmbr, i, !current);
                break;
            }
        }

        for (int i = 0; i < animFrameSlots.Length; i++)
        {
            if (!animFrameSlots[i].Contains(mouse.x, mouse.y)) continue;

            if (_api.mouselp())
            {
                AnimFrames[i] = sprNmbr;
            }
            else if (_api.mouserp())
            {
                AnimFrames[i] = -1;
            }
            break;
        }

        if (EditorUI.CycleOnClick(_api, animZoomBtn, mouse, ref AnimSclIdx, Zooms.Length)) return;
        if (EditorUI.CycleOnClick(_api, animSpeedBtn, mouse, ref AnimSpeedIdx, AnimSpeeds.Length)) return;
        if (EditorUI.CycleOnClick(_api, animLoopModeBtn, mouse, ref animLoopModeIdx, LoopModeCount)) return;
        if (EditorUI.CycleOnClick(_api, refVisualizationBtn, mouse, ref referenceVisualizationIdxs[sprNmbr], ReferenceVisualizationCount)) return;
        if (EditorUI.CycleOnClick(_api, refOpacityBtn, mouse, ref referenceOpacityIdxs[sprNmbr], ReferenceOpacities.Length)) return;

        if (refNumberBtn.Contains(mouse.x, mouse.y) && _api.mouselp())
        {
            editingReferenceNumber = true;
            referenceNumberInput = Mono8API.SpriteSheet.GetReferenceSprite(sprNmbr);
        }
        else if (refOrderBtn.Contains(mouse.x, mouse.y) && (_api.mouselp() || _api.mouserp()))
        {
            referenceOrders[sprNmbr] = referenceOrder == ReferenceOrder.Behind ? ReferenceOrder.Front : ReferenceOrder.Behind;
        }
    }

    private bool DitherSlotIsEmpty(int slot) => slot != DitherSolidSlot && ditherSprites[slot] < 0;

    // A slot is in one of three states and each takes a different click. Empty: left-click gives it the
    // selected sprite and turns it on. Set but off: left-click turns it on, right-click empties it
    // again. On: it is the mask the pencil is painting through, so neither button touches it - which
    // also keeps the active slot from ever being emptied out from under the pencil.
    private bool UpdateDitherButtons((int x, int y) mouse)
    {
        bool leftClick = _api.mouselp();
        bool rightClick = _api.mouserp();
        if (!leftClick && !rightClick) return false;

        for (int i = 0; i < ditherButtons.Length; i++)
        {
            if (!ditherButtons[i].Contains(mouse.x, mouse.y)) continue;

            // Every dead end below says why, so a click that changes nothing never looks like one the
            // editor missed.
            if (i == selectedDitherSlot)
            {
                eventNotifier.AddEvent(rightClick ? "CANT REMOVE ENABLED" : "ALREADY ON");
                return true;
            }

            if (rightClick)
            {
                if (i == DitherSolidSlot)
                {
                    eventNotifier.AddEvent("PENCIL IS FIXED");
                    return true;
                }

                if (DitherSlotIsEmpty(i))
                {
                    eventNotifier.AddEvent("ALREADY EMPTY");
                    return true;
                }

                ditherSprites[i] = -1;
                eventNotifier.AddEvent("EMPTY");
                return true;
            }

            if (DitherSlotIsEmpty(i))
            {
                // Sprite 0 is the empty sentinel and the sheet holds its tile permanently blank, so it
                // can never carry a pattern.
                if (sprNmbr == 0)
                {
                    eventNotifier.AddEvent("SPR 000 IS EMPTY");
                    return true;
                }

                ditherSprites[i] = sprNmbr;
            }

            selectedDitherSlot = i;
            eventNotifier.AddEvent(i == DitherSolidSlot ? "PENCIL" : "DITHERING");
            return true;
        }

        return false;
    }

    private static string ToolLabel(Tool tool) => tool switch
    {
        Tool.Rect => "RECT",
        Tool.RectFill => "RECT FILL",
        Tool.Oval => "OVAL",
        Tool.OvalFill => "OVAL FILL",
        Tool.PaintBucket => "PAINT BUCKET",
        Tool.Select => "SELECTION",
        _ => "PIXEL",
    };

    // Spelled out for the bottom bar; the buttons themselves keep their two- and three-letter
    // labels because that is all their boxes fit.
    private static string LoopModeLabel(LoopMode mode) => mode switch
    {
        LoopMode.Forward => "FORWARD",
        LoopMode.Reverse => "REVERSE",
        LoopMode.PingPong => "PING PONG",
        _ => "PAUSE",
    };

    private static string ReferenceVisualizationLabel(ReferenceVisualization visualization) => visualization switch
    {
        ReferenceVisualization.Red => "RED",
        ReferenceVisualization.Green => "GREEN",
        ReferenceVisualization.Blue => "BLUE",
        _ => "ORIGINAL",
    };

    private int ColorIndexAt((int x, int y) mouse)
    {
        int x = (mouse.x - palettearea.X) / Constants.GameDataSizes.TileSize;
        int y = (mouse.y - palettearea.Y) / Constants.GameDataSizes.TileSize;
        return x + y * PaletteColumns;
    }

    /// <summary>
    /// The name of the control under the cursor, carrying its current value where it holds one.
    /// Null over anything else. The areas are disjoint, so the first hit wins.
    /// </summary>
    private string HoverLabelAt((int x, int y) mouse)
    {
        for (int i = 0; i < animFrameSlots.Length; i++)
        {
            if (animFrameSlots[i].Contains(mouse.x, mouse.y)) return $"ANIM {i}";
        }

        if (animZoomBtn.Contains(mouse.x, mouse.y)) return $"SCALE x{Zooms[AnimSclIdx]}";
        if (animSpeedBtn.Contains(mouse.x, mouse.y)) return $"SPEED {AnimSpeeds[AnimSpeedIdx]:D2}";
        if (animLoopModeBtn.Contains(mouse.x, mouse.y)) return $"MODE {LoopModeLabel(animLoopMode)}";

        if (refNumberBtn.Contains(mouse.x, mouse.y))
        {
            int reference = editingReferenceNumber ? referenceNumberInput : Mono8API.SpriteSheet.GetReferenceSprite(sprNmbr);
            return "ONION " + (reference < 0 ? "--" : reference.ToString("D3"));
        }
        if (refOrderBtn.Contains(mouse.x, mouse.y)) return referenceOrder == ReferenceOrder.Behind ? "ONION BEHIND" : "ONION FRONT";
        if (refVisualizationBtn.Contains(mouse.x, mouse.y)) return $"ONION {ReferenceVisualizationLabel(referenceVisualization)}";
        if (refOpacityBtn.Contains(mouse.x, mouse.y)) return $"ONION {(int)(referenceOpacity * 100)}";

        if (palettearea.Contains(mouse.x, mouse.y)) return $"COLOR {ColorIndexAt(mouse)}";

        for (int i = 0; i < ditherButtons.Length; i++)
        {
            if (ditherButtons[i].Contains(mouse.x, mouse.y))
                return i == DitherSolidSlot ? "PENCIL" : "DITHERING";
        }

        foreach (var (button, tool) in toolButtons)
        {
            if (button.Bounds.Contains(mouse.x, mouse.y)) return ToolLabel(tool);
        }

        if (autotileGuideButton.Bounds.Contains(mouse.x, mouse.y)) return "AUTOTILE";

        // The index the fget/fset flag overloads take.
        for (int i = 0; i < flagButtons.Length; i++)
        {
            if (flagButtons[i].Contains(mouse.x, mouse.y)) return $"FLAG {i}";
        }

        return null;
    }

    private (int first, int last) GetAnimFilledRange()
    {
        int first = -1, last = -1;
        for (int i = 0; i < AnimFrames.Length; i++)
        {
            if (AnimFrames[i] != -1)
            {
                if (first == -1) first = i;
                last = i;
            }
        }
        return (first, last);
    }

    private void UpdateAnimationPlayback(float elapsedSeconds)
    {
        var (first, last) = GetAnimFilledRange();

        if (first == -1)
        {
            animCurrentFrame = 0;
            animElapsed = 0f;
            animPingPongDir = 1;
            return;
        }

        if (animCurrentFrame < first || animCurrentFrame > last || AnimFrames[animCurrentFrame] == -1)
        {
            animCurrentFrame = first;
        }

        if (animLoopMode == LoopMode.Pause) return;

        float interval = 1f / AnimSpeeds[AnimSpeedIdx];
        animElapsed += elapsedSeconds;

        while (animElapsed >= interval)
        {
            animElapsed -= interval;
            StepAnimationFrame(first, last);
        }
    }

    private void StepAnimationFrame(int first, int last)
    {
        switch (animLoopMode)
        {
            case LoopMode.Forward:
                do
                {
                    animCurrentFrame = animCurrentFrame + 1 > last ? first : animCurrentFrame + 1;
                } while (AnimFrames[animCurrentFrame] == -1);
                break;
            case LoopMode.Reverse:
                do
                {
                    animCurrentFrame = animCurrentFrame - 1 < first ? last : animCurrentFrame - 1;
                } while (AnimFrames[animCurrentFrame] == -1);
                break;
            case LoopMode.PingPong:
                if (first == last) break;
                do
                {
                    int next = animCurrentFrame + animPingPongDir;
                    if (next > last)
                    {
                        animPingPongDir = -1;
                        next = animCurrentFrame - 1;
                    }
                    else if (next < first)
                    {
                        animPingPongDir = 1;
                        next = animCurrentFrame + 1;
                    }
                    animCurrentFrame = next;
                } while (AnimFrames[animCurrentFrame] == -1);
                break;
        }
    }

    private void ApplyShapeTool(int x0, int y0, int x1, int y1)
    {
        int x = Math.Min(x0, x1);
        int y = Math.Min(y0, y1);
        int w = Math.Abs(x1 - x0) + 1;
        int h = Math.Abs(y1 - y0) + 1;

        switch (selectedTool)
        {
            case Tool.Rect:
                _api.SetRect(x, y, w, h, ColorSelected);
                break;
            case Tool.RectFill:
                _api.SetRectFill(x, y, w, h, ColorSelected);
                break;
            case Tool.Oval:
                _api.SetOval(x0, y0, x1, y1, ColorSelected);
                break;
            case Tool.OvalFill:
                _api.SetOvalFill(x0, y0, x1, y1, ColorSelected);
                break;
        }
    }

    private void UpdateShapePreview(int x0, int y0, int x1, int y1)
    {
        int x = Math.Min(x0, x1);
        int y = Math.Min(y0, y1);
        int w = Math.Abs(x1 - x0) + 1;
        int h = Math.Abs(y1 - y0) + 1;

        switch (selectedTool)
        {
            case Tool.Rect:
                shapePreview.SetRect(x, y, w, h, ColorSelected);
                break;
            case Tool.RectFill:
                shapePreview.SetRectFill(x, y, w, h, ColorSelected);
                break;
            case Tool.Oval:
                shapePreview.SetOval(x0, y0, x1, y1, ColorSelected);
                break;
            case Tool.OvalFill:
                shapePreview.SetOvalFill(x0, y0, x1, y1, ColorSelected);
                break;
        }
    }

    public void Draw()
    {
        navigator.SelectionScale = Zooms[SprSclIdx];
        navigator.DrawSheet();

        _api.rectfill(0, Constants.GameDataSizes.TileSize,
            Constants.Screen.ResolutionX, 85, Constants.Colors.DarkGray);
        _api.rectfill(sprcnvsarea.X - 1, sprcnvsarea.Y - 1,
            sprcnvsarea.X + sprcnvsarea.Width,
            sprcnvsarea.Y + sprcnvsarea.Height, Constants.Colors.Black);

        var (regionX, regionY, regionW, regionH) = CurrentCanvasRegion();
        int scale = CanvasScale;
        var (validW, validH) = VisibleSize(regionX, regionY, regionW, regionH);

        DrawWorkspaceOverflow(sprcnvsarea, regionW, regionH, validW, validH, scale);

        if (referenceOrder == ReferenceOrder.Behind)
        {
            DrawReferenceSprite(scale, validW, validH);
        }

        _api.sprr(sprNmbr, sprcnvsarea.X, sprcnvsarea.Y,
             validW / Constants.GameDataSizes.TileSize,
             validH / Constants.GameDataSizes.TileSize,
             scale);

        if (referenceOrder == ReferenceOrder.Front)
        {
            DrawReferenceSprite(scale, validW, validH);
        }

        if (showAutotileGuide)
        {
            DrawAutotileGuide(scale, validW, validH);
        }

        if (shapePreview.HasPixels)
        {
            shapePreview.Draw(_api, regionX, regionY, sprcnvsarea.X, sprcnvsarea.Y, scale);
        }

        DrawSelection(regionX, regionY, scale);

        if (sprNmbr == 0)
        {
            DrawEmptySpriteCross(sprcnvsarea.X, sprcnvsarea.Y, Constants.GameDataSizes.TileSize * scale);
        }

        if (navigator.Page == 0)
        {
            DrawEmptySpriteCross(navigator.ViewerArea.X, navigator.ViewerArea.Y, Constants.GameDataSizes.TileSize);
        }

        _api.rectfill(0, EditorUI.BottomBarY, Constants.Screen.ResolutionX, Constants.Screen.ResolutionY - 1, Constants.Colors.Orange);

        DrawHoverLabel();

        DrawPalette();

        foreach (var (button, tool) in toolButtons)
        {
            button.Draw(_api, tool == selectedTool);
        }

        autotileGuideButton.Draw(_api, showAutotileGuide);

        DrawDitherButtons();

        DrawReferenceButtons();
        DrawFlagButtons();

        navigator.DrawPageButtons();
        navigator.DrawNumberLabel();

        DrawAnimationPanel();

        eventNotifier.Draw();
    }

    // The committed selection, plus the rectangle being dragged out. The drag is drawn unclamped so
    // the border tracks the cursor; CommitSelection is what pulls it back onto the sheet.
    private void DrawSelection(int regionX, int regionY, int scale)
    {
        if (hasSelection)
        {
            DrawSelectionAnts(regionX, regionY, scale, selection.X, selection.Y, selection.Width, selection.Height);
        }

        if (!dragging || selectedTool != Tool.Select) return;

        var mouse = _api.mousexy();
        if (!sprcnvsarea.Contains(mouse.x, mouse.y)) return;

        var (x, y) = SheetPixelUnderMouse(mouse);
        DrawSelectionAnts(regionX, regionY, scale,
            Math.Min(dragStartX, x), Math.Min(dragStartY, y),
            Math.Abs(x - dragStartX) + 1, Math.Abs(y - dragStartY) + 1);
    }

    private void DrawSelectionAnts(int regionX, int regionY, int scale, int x, int y, int w, int h)
    {
        int x0 = sprcnvsarea.X + (x - regionX) * scale;
        int y0 = sprcnvsarea.Y + (y - regionY) * scale;

        ants.Draw(_api, x0, y0, x0 + w * scale - 1, y0 + h * scale - 1, sprcnvsarea);
    }

    // Right-aligned on the bottom bar, so the event label on the left never collides with it.
    private void DrawHoverLabel()
    {
        if (hoverLabel == null) return;

        const int charAdvance = 4;
        const int rightMargin = 2;

        _api.print(hoverLabel,
            Constants.Screen.ResolutionX - rightMargin - hoverLabel.Length * charAdvance,
            EditorUI.BottomBarY + 1,
            Constants.Colors.Indigo);
    }

    private void DrawPalette()
    {
        _api.rectfill(palettearea.X - 1, palettearea.Y - 1,
            palettearea.X + palettearea.Width,
            palettearea.Y + palettearea.Height, Constants.Colors.Black);

        int size = Constants.GameDataSizes.TileSize;
        for (int color = 0; color < Constants.GameDataSizes.ColorPalette; color++)
        {
            int x = palettearea.X + (color % PaletteColumns) * size;
            int y = palettearea.Y + (color / PaletteColumns) * size;
            _api.rectfill(x, y, x + size - 1, y + size - 1, color);
        }

        // The selection outline is drawn in a second pass so a neighbouring swatch can't paint over it.
        int selX = palettearea.X + (ColorSelected % PaletteColumns) * size;
        int selY = palettearea.Y + (ColorSelected / PaletteColumns) * size;
        _api.rect(selX, selY, selX + size - 1, selY + size - 1, Constants.Colors.Black);
        _api.rect(selX - 1, selY - 1, selX + size, selY + size, Constants.Colors.White);
    }

    // Each slot previews its own mask in two colors: white where the pattern paints, dark grey where it
    // does not - and dark grey throughout for every slot that is not the active one. Under any tool but
    // the pencil nothing is active, so the whole row reads as off. Icons can't show this, so the button
    // widget isn't reused here.
    private void DrawDitherButtons()
    {
        bool rowActive = selectedTool == Tool.Pixel;

        for (int i = 0; i < ditherButtons.Length; i++)
        {
            var bounds = ditherButtons[i];
            int on = rowActive && i == selectedDitherSlot ? Constants.Colors.White : Constants.Colors.DarkGray;

            _api.rectfill(bounds.X, bounds.Y,
                bounds.X + bounds.Width - 1, bounds.Y + bounds.Height - 1, Constants.Colors.Black);

            if (i == DitherSolidSlot)
            {
                _api.rectfill(bounds.X, bounds.Y,
                    bounds.X + bounds.Width - 1, bounds.Y + bounds.Height - 1, on);
                continue;
            }

            // A slot that was never given a sprite stays a black square.
            int sprite = ditherSprites[i];
            if (sprite < 0) continue;

            for (int py = 0; py < bounds.Height; py++)
            {
                for (int px = 0; px < bounds.Width; px++)
                {
                    if (Mono8API.SpriteSheet.IsDitherMaskSet(sprite, px, py))
                        _api.pixel(bounds.X + px, bounds.Y + py, on);
                }
            }
        }
    }

    private void DrawFlagButtons()
    {
        for (int i = 0; i < flagButtons.Length; i++)
        {
            var bounds = flagButtons[i];
            bool set = Mono8API.SpriteSheet.GetFlag(sprNmbr, i);

            if (set) _api.pal(1, 8 + i);
            _api.icon(FlagIconIndex, bounds.X, bounds.Y);
            if (set) _api.pal();
        }
    }

    // The terrain each of a block's sixteen pieces is expected to cover, laid over the canvas from
    // its top-left tile. The canvas starts on the selected sprite, so that tile holds the sprite's
    // own cell and the rest of the block runs right and down from it: the guide is cut off both
    // where the block ends and where the current zoom stops bringing tiles onto the canvas.
    // Sprites in the sheet's leftover rows belong to no block and get no guide.
    private void DrawAutotileGuide(int scale, int validW, int validH)
    {
        if (!AutotileSheet.TryGetBlock(sprNmbr, out int blockX, out int blockY)) return;
        if (!AutotileSheet.TryGetCell(sprNmbr, blockX, blockY, out int firstCell)) return;

        int fill = AutotileOverlay.Fill(blockX, blockY);
        int tileSize = Constants.GameDataSizes.TileSize;
        int tilePx = tileSize * scale;

        int firstCellX = firstCell % AutotileSheet.BlockSize;
        int firstCellY = firstCell / AutotileSheet.BlockSize;

        int cols = Math.Min(AutotileSheet.BlockSize - firstCellX, validW / tileSize);
        int rows = Math.Min(AutotileSheet.BlockSize - firstCellY, validH / tileSize);

        for (int tileY = 0; tileY < rows; tileY++)
        {
            for (int tileX = 0; tileX < cols; tileX++)
            {
                int cell = (firstCellY + tileY) * AutotileSheet.BlockSize + firstCellX + tileX;

                AutotileOverlay.DrawCell(_api,
                    sprcnvsarea.X + tileX * tilePx, sprcnvsarea.Y + tileY * tilePx,
                    tilePx, cell, fill);
            }
        }
    }

    private void DrawAnimationPanel()
    {
        var firstSlot = animFrameSlots[0];
        var lastSlot = animFrameSlots[animFrameSlots.Length - 1];
        _api.rectfill(firstSlot.X, firstSlot.Y,
            lastSlot.X + lastSlot.Width - 1,
            lastSlot.Y + lastSlot.Height - 1,
            Constants.Colors.Black);

        for (int i = 0; i < animFrameSlots.Length; i++)
        {
            var bounds = animFrameSlots[i];
            if (AnimFrames[i] == -1) continue;

            // Active marker: a 1x6 line left of the slot, inset by 1px so it keeps a
            // one pixel gap from the slot and from the markers above and below it.
            int markerX = bounds.X + bounds.Width + 1;
            _api.rectfill(markerX, bounds.Y + 1,
                markerX, bounds.Y + bounds.Height - 2,
                Constants.Colors.Green);

            var (regionX, regionY, regionW, regionH) = CanvasRegion(AnimFrames[i], Zooms[AnimSclIdx]);
            var (validW, validH) = VisibleSize(regionX, regionY, regionW, regionH);

            _api.sprr(AnimFrames[i], bounds.X, bounds.Y,
                validW / Constants.GameDataSizes.TileSize,
                validH / Constants.GameDataSizes.TileSize,
                1f / Zooms[AnimSclIdx]);

            if (animLoopMode != LoopMode.Pause && i == animCurrentFrame)
            {
                _api.rect(bounds.X, bounds.Y, bounds.X + bounds.Width - 1, bounds.Y + bounds.Height - 1, Constants.Colors.White);
            }
        }

        EditorUI.TextButton(_api, animZoomBtn, "x" + Zooms[AnimSclIdx]);
        EditorUI.TextButton(_api, animSpeedBtn, AnimSpeeds[AnimSpeedIdx].ToString("D2"));
        EditorUI.TextButton(_api, animLoopModeBtn, animLoopMode switch
        {
            LoopMode.Forward => "FW",
            LoopMode.Reverse => "RV",
            LoopMode.PingPong => "PP",
            _ => "PA",
        });

        _api.rectfill(animPreviewArea.X - 1, animPreviewArea.Y - 1,
            animPreviewArea.X + animPreviewArea.Width,
            animPreviewArea.Y + animPreviewArea.Height, Constants.Colors.Black);

        if (AnimFrames[animCurrentFrame] != -1)
        {
            var (regionX, regionY, regionW, regionH) = CanvasRegion(AnimFrames[animCurrentFrame], Zooms[AnimSclIdx]);
            int scale = Constants.GameDataSizes.TileSize / Zooms[AnimSclIdx];
            var (validW, validH) = VisibleSize(regionX, regionY, regionW, regionH);

            DrawWorkspaceOverflow(animPreviewArea, regionW, regionH, validW, validH, scale);

            _api.sprr(AnimFrames[animCurrentFrame], animPreviewArea.X, animPreviewArea.Y,
                validW / Constants.GameDataSizes.TileSize,
                validH / Constants.GameDataSizes.TileSize,
                scale);
        }
    }

    private void DrawReferenceSprite(int scale, int validW, int validH)
    {
        int refSprite = Mono8API.SpriteSheet.GetReferenceSprite(sprNmbr);
        if (refSprite < 0) return;

        if (referenceVisualization != ReferenceVisualization.Original)
        {
            int targetColor = referenceVisualization switch
            {
                ReferenceVisualization.Red => Constants.Colors.Red,
                ReferenceVisualization.Green => Constants.Colors.Green,
                ReferenceVisualization.Blue => Constants.Colors.Blue,
                _ => Constants.Colors.White,
            };
            for (int ci = 1; ci < Constants.GameDataSizes.ColorPalette; ci++)
                _api.pal(ci, targetColor);
        }

        _api.spr(refSprite, sprcnvsarea.X, sprcnvsarea.Y,
            validW / Constants.GameDataSizes.TileSize,
            validH / Constants.GameDataSizes.TileSize, scale, false, false,
            referenceOpacity);

        if (referenceVisualization != ReferenceVisualization.Original)
        {
            _api.pal();
        }
    }

    private void DrawReferenceButtons()
    {
        int displayValue = editingReferenceNumber ? referenceNumberInput : Mono8API.SpriteSheet.GetReferenceSprite(sprNmbr);
        string numberText = displayValue < 0 ? "--" : displayValue.ToString("D3");
        int numberBg = editingReferenceNumber ? Constants.Colors.White : Constants.Colors.LightGray;
        EditorUI.Box(_api, refNumberBtn, numberText, numberBg, Constants.Colors.Indigo);

        EditorUI.TextButton(_api, refOrderBtn, referenceOrder == ReferenceOrder.Behind ? "BEH" : "FRO");

        EditorUI.TextButton(_api, refVisualizationBtn, referenceVisualization switch
        {
            ReferenceVisualization.Red => "RED",
            ReferenceVisualization.Green => "GRN",
            ReferenceVisualization.Blue => "BLU",
            _ => "ORG",
        });

        EditorUI.TextButton(_api, refOpacityBtn, ((int)(referenceOpacity * 100)).ToString());
    }
}
