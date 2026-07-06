namespace mono8.editor;

internal class SpriteEditor : IEditor
{
    private readonly IMono8API _api;
    private Rectangle sprvwrarea;
    private Rectangle sprcnvsarea;
    private Rectangle palettearea;
    private int sprNmbr;
    private int SprX;
    private int SprY;
    private int SprSclIdx = 0;
    private int[] Zooms = { 1, 2, 4 };
    private int[] CnvScale = { 8, 4, 2 };
    private int ColorSelected = Constants.Colors.White;
    private readonly EventNotifier eventNotifier;

    public SpriteEditor(IMono8API api)
    {
        _api = api;
        eventNotifier = new EventNotifier(api, 2f, 1, Constants.Screen.ResolutionY - Constants.GameDataSizes.TileSize + 1);
        sprvwrarea = new Rectangle(0,
            Constants.Screen.ResolutionY - 1 - Constants.GameDataSizes.SpriteSheetY - Constants.GameDataSizes.TileSize,
            Constants.GameDataSizes.SpriteSheetX,
            Constants.GameDataSizes.SpriteSheetY);
        sprcnvsarea = new Rectangle(80, 15, 8*8, 8 * 8);
        palettearea = new Rectangle(170, 15, 8 * Constants.GameDataSizes.TileSize, 2 * Constants.GameDataSizes.TileSize);
        sprNmbr = 0;
        SprX = 0;
        SprY = Constants.Screen.ResolutionY - 1 - Constants.GameDataSizes.SpriteSheetY - Constants.GameDataSizes.TileSize;
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

        if (sprvwrarea.Contains(mouse.x, mouse.y))
        {
            if (_api.mousel())
            {
                int x = (mouse.x - sprvwrarea.X) / Constants.GameDataSizes.TileSize;
                int y = (mouse.y - sprvwrarea.Y) / Constants.GameDataSizes.TileSize;
                SprX = x * Constants.GameDataSizes.TileSize + sprvwrarea.X;
                SprY = y * Constants.GameDataSizes.TileSize + sprvwrarea.Y;
                sprNmbr = x + y * Constants.GameDataSizes.SpriteSheetColumns;
            }
        }
        else if (sprcnvsarea.Contains(mouse.x, mouse.y))
        {
            if (_api.mousel())
            {
                int x = ((mouse.x - sprcnvsarea.X)) * Zooms[SprSclIdx] / Constants.GameDataSizes.TileSize + (sprNmbr % Constants.GameDataSizes.SpriteSheetColumns) * Constants.GameDataSizes.TileSize;
                int y = ((mouse.y - sprcnvsarea.Y)) * Zooms[SprSclIdx] / Constants.GameDataSizes.TileSize + (sprNmbr / Constants.GameDataSizes.SpriteSheetColumns) * Constants.GameDataSizes.TileSize;
                _api.SetPixel(x, y, ColorSelected);
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
    }

    public void Draw()
    {
        _api.rectfill(0,0,Constants.Screen.ResolutionX,Constants.GameDataSizes.TileSize,Constants.Colors.Orange);

        _api.spr(0, sprvwrarea.X,
            sprvwrarea.Y,
            Constants.GameDataSizes.SpriteSheetColumns,
            Constants.GameDataSizes.SpriteSheetRows);

        if (SprX > -1 && SprY > -1)
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

        eventNotifier.Draw();
    }
}
