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

    private readonly IMono8API _api;
    private Rectangle sprvwrarea;
    private Rectangle sprcnvsarea;
    private Rectangle palettearea;
    private int sprNmbr;
    private int SprX;
    private int SprY;
    private int SprSclIdx = 0;
    private int[] Zooms = { 1, 2, 4, 8 };
    private int[] CnvScale = { 8, 4, 2, 1 };
    private int ColorSelected = Constants.Colors.White;
    private readonly EventNotifier eventNotifier;

    private readonly (Button Button, Tool Tool)[] toolButtons;
    private Tool selectedTool = Tool.Pixel;

    private const int FlagCount = 8;
    private const int FlagIconIndex = 43;
    private readonly Rectangle[] flagButtons;
    private bool dragging;
    private int dragStartX;
    private int dragStartY;
    private readonly ShapePreviewGrid shapePreview = new();

    private const int VisibleRows = 6;
    private const int SpritePages = Constants.GameDataSizes.SpriteSheetRows / VisibleRows;
    private const int PageIconSelected = 45;
    private const int PageIconNotSelected = 46;
    private readonly Rectangle[] pageButtons;
    private readonly int labelRowY;
    private readonly Rectangle sprNmbrLabelArea;
    private int spritePage = 0;

    public SpriteEditor(IMono8API api)
    {
        _api = api;
        eventNotifier = new EventNotifier(api, 2f, 1, Constants.Screen.ResolutionY - Constants.GameDataSizes.TileSize + 1);
        sprvwrarea = new Rectangle(0,
            Constants.Screen.ResolutionY - 1 - (VisibleRows + 1) * Constants.GameDataSizes.TileSize,
            Constants.GameDataSizes.SpriteSheetX,
            VisibleRows * Constants.GameDataSizes.TileSize);
        sprcnvsarea = new Rectangle(100, 15, 8*8, 8 * 8);
        palettearea = new Rectangle(170, 15, 8 * Constants.GameDataSizes.TileSize, 2 * Constants.GameDataSizes.TileSize);
        sprNmbr = 0;
        SprX = 0;
        SprY = Constants.Screen.ResolutionY - 1 - (VisibleRows + 1) * Constants.GameDataSizes.TileSize;

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

        int flagButtonY = toolButtonY + size + 2;
        flagButtons = new Rectangle[FlagCount];
        for (int i = 0; i < FlagCount; i++)
        {
            flagButtons[i] = new Rectangle(palettearea.X + i * size, flagButtonY, size, size);
        }

        labelRowY = sprvwrarea.Y - size;

        int pageButtonsStartX = Constants.Screen.ResolutionX - SpritePages * size;
        pageButtons = new Rectangle[SpritePages];
        for (int i = 0; i < pageButtons.Length; i++)
        {
            pageButtons[i] = new Rectangle(pageButtonsStartX + i * size, labelRowY - 1, size, size);
        }

        const int labelGap = 4;
        sprNmbrLabelArea = new Rectangle(pageButtonsStartX - labelGap - size * 2, labelRowY - 1, size * 2, size - 1);
    }

    public void Init()
    {
    }

    private (int x, int y, int w, int h) CurrentCanvasRegion()
    {
        int size = Zooms[SprSclIdx] * Constants.GameDataSizes.TileSize;
        int x = (sprNmbr % Constants.GameDataSizes.SpriteSheetColumns) * Constants.GameDataSizes.TileSize;
        int y = (sprNmbr / Constants.GameDataSizes.SpriteSheetColumns) * Constants.GameDataSizes.TileSize;
        return (x, y, size, size);
    }

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

    public void Update(float elapsedSeconds)
    {
        eventNotifier.Update(elapsedSeconds);

        if (KeybrdInput.IsSaveShortcutPressed())
        {
            mono8.GameAPI.Save();
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

        if (KeybrdInput.JustPressed(Keys.Delete))
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

        if (!KeybrdInput.Pressed(Keys.LeftControl) && !KeybrdInput.Pressed(Keys.RightControl)
            && KeybrdInput.JustPressed(Keys.V))
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

        var mouse = _api.mousexy();

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

        if (sprvwrarea.Contains(mouse.x, mouse.y))
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
                ColorSelected = x + y * 8;
            }
        }
        else
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

            for (int i = 0; i < flagButtons.Length; i++)
            {
                if (flagButtons[i].Contains(mouse.x, mouse.y) && _api.mouselp())
                {
                    bool current = Mono8API.SpriteSheet.GetFlag(sprNmbr, i);
                    Mono8API.SpriteSheet.SetFlag(sprNmbr, i, !current);
                    break;
                }
            }
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
             SprX + Constants.GameDataSizes.TileSize * Zooms[SprSclIdx],
             SprY + Constants.GameDataSizes.TileSize * Zooms[SprSclIdx],
             Constants.Colors.White);
            _api.rect(SprX -2, SprY - 2,
             SprX + 1 + Constants.GameDataSizes.TileSize * Zooms[SprSclIdx],
             SprY + 1 + Constants.GameDataSizes.TileSize * Zooms[SprSclIdx],
             Constants.Colors.Black);
        }
        _api.rectfill(0, Constants.GameDataSizes.TileSize + 1,
            Constants.Screen.ResolutionX, 85, Constants.Colors.DarkGray);
        _api.rectfill(sprcnvsarea.X -1, sprcnvsarea.Y - 1,
            sprcnvsarea.X + sprcnvsarea.Width,
            sprcnvsarea.Y + sprcnvsarea.Height, Constants.Colors.Black);

        var (regionX, regionY, regionW, regionH) = CurrentCanvasRegion();
        int scale = CnvScale[SprSclIdx];
        int validW = Math.Min(regionW, Constants.GameDataSizes.SpriteSheetX - regionX);
        int validH = Math.Min(regionH, Constants.GameDataSizes.SpriteSheetY - regionY);

        if (validW < regionW)
        {
            DrawEmptyWorkspacePattern(sprcnvsarea.X + validW * scale, sprcnvsarea.Y,
                (regionW - validW) * scale, regionH * scale);
        }

        if (validH < regionH)
        {
            DrawEmptyWorkspacePattern(sprcnvsarea.X, sprcnvsarea.Y + validH * scale,
                validW * scale, (regionH - validH) * scale);
        }

        _api.spr(sprNmbr, sprcnvsarea.X, sprcnvsarea.Y,
             validW / Constants.GameDataSizes.TileSize,
             validH / Constants.GameDataSizes.TileSize,
             scale);

        if (shapePreview.HasPixels)
        {
            shapePreview.Draw(_api, regionX, regionY, sprcnvsarea.X, sprcnvsarea.Y, scale);
        }

        _api.rectfill(0,Constants.Screen.ResolutionY - Constants.GameDataSizes.TileSize, Constants.Screen.ResolutionX, Constants.Screen.ResolutionY -1,Constants.Colors.Orange);

        _api.rectfill(palettearea.X - 1, palettearea.Y - 1,
            palettearea.X + palettearea.Width,
            palettearea.Y + palettearea.Height, Constants.Colors.Black);
        for (int color = 0; color < Constants.GameDataSizes.ColorPalette; color++)
        {
            int col = color % 8;
            int row = color / 8;
            int x = palettearea.X + col * Constants.GameDataSizes.TileSize;
            int y = palettearea.Y + row * Constants.GameDataSizes.TileSize;
            _api.rectfill(x, y,
                x + Constants.GameDataSizes.TileSize - 1,
                y + Constants.GameDataSizes.TileSize - 1,
                color);
        }

        for (int color = 0; color < Constants.GameDataSizes.ColorPalette; color++)
        {
            int col = color % 8;
            int row = color / 8;
            int x = palettearea.X + col * Constants.GameDataSizes.TileSize;
            int y = palettearea.Y + row * Constants.GameDataSizes.TileSize;
            if (color == ColorSelected)
            {
                _api.rect(x, y,
                    x + Constants.GameDataSizes.TileSize - 1,
                    y + Constants.GameDataSizes.TileSize - 1,
                    Constants.Colors.Black);
                _api.rect(x - 1, y - 1,
                    x + Constants.GameDataSizes.TileSize,
                    y + Constants.GameDataSizes.TileSize,
                    Constants.Colors.White);
            }
        }

        foreach (var (button, tool) in toolButtons)
        {
            button.Draw(_api, tool == selectedTool);
        }

        for (int i = 0; i < flagButtons.Length; i++)
        {
            var bounds = flagButtons[i];
            if (Mono8API.SpriteSheet.GetFlag(sprNmbr, i))
            {
                _api.pal(1, 8 + i);
                _api.icon(FlagIconIndex, bounds.X, bounds.Y);
                _api.pal();
            }
            else
            {
                _api.icon(FlagIconIndex, bounds.X, bounds.Y);
            }
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

        eventNotifier.Draw();
    }
}
