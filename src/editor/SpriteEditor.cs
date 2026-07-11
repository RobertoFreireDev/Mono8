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

    private bool editingReferenceNumber;
    private int referenceNumberInput = -1;
    private ReferenceOrder referenceOrder = ReferenceOrder.Behind;
    private int referenceVisualizationIdx = (int)ReferenceVisualization.Original;
    private ReferenceVisualization referenceVisualization => (ReferenceVisualization)referenceVisualizationIdx;
    private static readonly float[] ReferenceOpacities = { 0.2f, 0.4f, 0.6f, 0.8f, 1.0f };
    private int referenceOpacityIdx = ReferenceOpacities.Length - 1;

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

    // --- Autotile guide ---
    // Overlays the terrain a 4x4 autotile block is expected to hold on the canvas. Its button sits
    // on the tool row but is not one of the tools: it toggles on its own, so the guide can be shown
    // while any paint tool is selected.
    private readonly Button autotileGuideButton;
    private bool showAutotileGuide;

    private const int FlagCount = 8;
    private const int FlagIconIndex = 43;
    private readonly Rectangle[] flagButtons;
    private bool dragging;
    private int dragStartX;
    private int dragStartY;
    private readonly ShapePreviewGrid shapePreview = new();

    private const int PaletteColumns = 8;
    private const int PaletteRows = Constants.GameDataSizes.ColorPalette / PaletteColumns;

    private int sprNmbr => navigator.SelectedSprite;

    public SpriteEditor(IEditorAPI api)
    {
        _api = api;
        eventNotifier = new EventNotifier(api, 2f, 1, Constants.Screen.ResolutionY - Constants.GameDataSizes.TileSize + 1);
        navigator = new SpriteNavigator(api);

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
        };

        autotileGuideButton = new Button(palettearea.X + toolButtons.Length * size, toolButtonY, size, AutotileOverlay.Icon);

        int flagButtonY = toolButtonY + size + 2;
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

        if (KeybrdInput.IsSaveShortcutPressed())
        {
            Mono8Game.GameAPI.Save();
            eventNotifier.AddEvent("SAVED");
        }

        if (KeybrdInput.IsUndoShortcutPressed() && Mono8API.SpriteSheet.CanUndo)
        {
            Mono8API.SpriteSheet.Undo();
            eventNotifier.AddEvent("UNDO");
        }

        if (KeybrdInput.IsRedoShortcutPressed() && Mono8API.SpriteSheet.CanRedo)
        {
            Mono8API.SpriteSheet.Redo();
            eventNotifier.AddEvent("REDO");
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
            var (regionX, regionY, regionW, regionH) = CurrentCanvasRegion();
            Mono8API.SpriteSheet.ClearGrid(regionX, regionY, regionW, regionH);
        }

        if (KeybrdInput.IsCopyShortcutPressed())
        {
            var (regionX, regionY, regionW, regionH) = CurrentCanvasRegion();
            Mono8API.SpriteSheet.CopyRegion(regionX, regionY, regionW, regionH);
            eventNotifier.AddEvent("COPY");
        }

        if (KeybrdInput.IsPasteShortcutPressed())
        {
            var (regionX, regionY, _, _) = CurrentCanvasRegion();
            Mono8API.SpriteSheet.PasteRegion(regionX, regionY);
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

        if (navigator.ViewerArea.Contains(mouse.x, mouse.y))
        {
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
            int x = ((mouse.x - sprcnvsarea.X)) * Zooms[SprSclIdx] / Constants.GameDataSizes.TileSize + (sprNmbr % Constants.GameDataSizes.SpriteSheetColumns) * Constants.GameDataSizes.TileSize;
            int y = ((mouse.y - sprcnvsarea.Y)) * Zooms[SprSclIdx] / Constants.GameDataSizes.TileSize + (sprNmbr / Constants.GameDataSizes.SpriteSheetColumns) * Constants.GameDataSizes.TileSize;

            if (selectedTool == Tool.Pixel)
            {
                if (_api.mousel()) _api.SetPixel(x, y, ColorSelected);
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
                int x = (mouse.x - palettearea.X) / Constants.GameDataSizes.TileSize;
                int y = (mouse.y - palettearea.Y) / Constants.GameDataSizes.TileSize;
                ColorSelected = x + y * PaletteColumns;
            }
        }
        else
        {
            UpdateSideButtons(mouse);
        }
    }

    private void UpdateSideButtons((int x, int y) mouse)
    {
        // Not a tool: it only flips the guide, leaving the selected tool alone.
        if (autotileGuideButton.IsClicked(_api, mouse))
        {
            showAutotileGuide = !showAutotileGuide;
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
        if (EditorUI.CycleOnClick(_api, refVisualizationBtn, mouse, ref referenceVisualizationIdx, ReferenceVisualizationCount)) return;
        if (EditorUI.CycleOnClick(_api, refOpacityBtn, mouse, ref referenceOpacityIdx, ReferenceOpacities.Length)) return;

        if (refNumberBtn.Contains(mouse.x, mouse.y) && _api.mouselp())
        {
            editingReferenceNumber = true;
            referenceNumberInput = Mono8API.SpriteSheet.GetReferenceSprite(sprNmbr);
        }
        else if (refOrderBtn.Contains(mouse.x, mouse.y) && (_api.mouselp() || _api.mouserp()))
        {
            referenceOrder = referenceOrder == ReferenceOrder.Behind ? ReferenceOrder.Front : ReferenceOrder.Behind;
        }
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

        if (sprNmbr == 0)
        {
            DrawEmptySpriteCross(sprcnvsarea.X, sprcnvsarea.Y, Constants.GameDataSizes.TileSize * scale);
        }

        if (navigator.Page == 0)
        {
            DrawEmptySpriteCross(navigator.ViewerArea.X, navigator.ViewerArea.Y, Constants.GameDataSizes.TileSize);
        }

        _api.rectfill(0, EditorUI.BottomBarY, Constants.Screen.ResolutionX, Constants.Screen.ResolutionY - 1, Constants.Colors.Orange);

        DrawPalette();

        foreach (var (button, tool) in toolButtons)
        {
            button.Draw(_api, tool == selectedTool);
        }

        autotileGuideButton.Draw(_api, showAutotileGuide);

        DrawReferenceButtons();
        DrawFlagButtons();

        navigator.DrawPageButtons();
        navigator.DrawNumberLabel();

        DrawAnimationPanel();

        eventNotifier.Draw();
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
            ReferenceOpacities[referenceOpacityIdx]);

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

        EditorUI.TextButton(_api, refOpacityBtn, ((int)(ReferenceOpacities[referenceOpacityIdx] * 100)).ToString());
    }
}
