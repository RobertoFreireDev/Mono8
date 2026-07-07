namespace mono8.core.sprites;

internal class SpriteSheet
{
    private static readonly int TotalSprites =
        Constants.GameDataSizes.SpriteSheetColumns * Constants.GameDataSizes.SpriteSheetRows;

    public int[,] Data;
    public Texture2D[] ColorTextures = new Texture2D[Constants.GameDataSizes.ColorPalette];

    public Rectangle[] TileRects;

    public byte[] Flags = new byte[TotalSprites];

    private int[,] _copy;

    private const int MaxUndoSteps = 50;
    private readonly Stack<int[,]> _undoStack = new Stack<int[,]>();
    private readonly Stack<int[,]> _redoStack = new Stack<int[,]>();

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    private void SaveSnapshot()
    {
        _undoStack.Push((int[,])Data.Clone());
        _redoStack.Clear();

        if (_undoStack.Count > MaxUndoSteps)
        {
            var items = _undoStack.ToArray();
            _undoStack.Clear();
            for (int i = MaxUndoSteps - 1; i >= 0; i--)
                _undoStack.Push(items[i]);
        }
    }

    public void Undo()
    {
        if (!CanUndo) return;

        _redoStack.Push((int[,])Data.Clone());
        Data = _undoStack.Pop();
        DataToTexture();
    }

    public void Redo()
    {
        if (!CanRedo) return;

        _undoStack.Push((int[,])Data.Clone());
        Data = _redoStack.Pop();
        DataToTexture();
    }

    public int GetFlags(int spriteId) =>
        spriteId >= 0 && spriteId < TotalSprites ? Flags[spriteId] : 0;

    public bool GetFlag(int spriteId, int flag) =>
        spriteId >= 0 && spriteId < TotalSprites && (Flags[spriteId] & (1 << flag)) != 0;

    public void SetFlag(int spriteId, int flag, bool value)
    {
        if (spriteId < 0 || spriteId >= TotalSprites) return;
        if (value) Flags[spriteId] |= (byte)(1 << flag);
        else Flags[spriteId] &= (byte)~(1 << flag);
    }

    public void SetFlags(int spriteId, int value)
    {
        if (spriteId >= 0 && spriteId < TotalSprites) Flags[spriteId] = (byte)value;
    }

    public void LoadSprites(string[] sheet, string[] flags)
    {
        LoadData(sheet);
        var defaultFlags = new string('0', TotalSprites);
        string line0 = flags != null && flags.Length > 0
        ? flags[0]
        : defaultFlags;

        string line1 = flags != null && flags.Length > 1
            ? flags[1]
            : defaultFlags;
        LoadFlags(line0, line1);
        CalculateTileRects();
        DataToTexture();
    }

    public void LoadFlags(string line0, string line1 = null)
    {
        LoadFlagsLine(line0, 0);
        if (line1 != null) LoadFlagsLine(line1, TotalSprites / 2);
    }

    private void LoadFlagsLine(string line, int spriteOffset)
    {
        for (int i = 0; i < TotalSprites / 2 && i * 2 + 1 < line.Length; i++)
        {
            int hi = HexNibble(line[i * 2]);
            int lo = HexNibble(line[i * 2 + 1]);
            Flags[spriteOffset + i] = (byte)(hi * 16 + lo);
        }
    }

    private static int HexNibble(char c) =>
        c >= '0' && c <= '9' ? c - '0' : c >= 'a' && c <= 'f' ? c - 'a' + 10 : c >= 'A' && c <= 'F' ? c - 'A' + 10 : 0;

    public string[] ToSheetLines()
    {
        var lines = new string[Constants.GameDataSizes.SpriteSheetY];
        for (int r = 0; r < Constants.GameDataSizes.SpriteSheetY; r++)
        {
            var chars = new char[Constants.GameDataSizes.SpriteSheetX];
            for (int c = 0; c < Constants.GameDataSizes.SpriteSheetX; c++)
                chars[c] = HexChar(Data[r, c]);
            lines[r] = new string(chars);
        }
        return lines;
    }

    public string[] ToFlagLines()
    {
        var line0 = new char[TotalSprites];
        var line1 = new char[TotalSprites];
        for (int i = 0; i < TotalSprites / 2; i++)
        {
            var hex0 = Flags[i].ToString("x2");
            line0[i * 2] = hex0[0];
            line0[i * 2 + 1] = hex0[1];

            var hex1 = Flags[TotalSprites / 2 + i].ToString("x2");
            line1[i * 2] = hex1[0];
            line1[i * 2 + 1] = hex1[1];
        }
        return new[] { new string(line0), new string(line1) };
    }

    private static char HexChar(int value) => (char)(value < 10 ? '0' + value : 'a' + value - 10);

    private void CalculateTileRects()
    {
        int columns = Constants.GameDataSizes.SpriteSheetColumns;
        int rows = Constants.GameDataSizes.SpriteSheetRows;
        int size = Constants.GameDataSizes.TileSize;
        int total = columns * rows;
        TileRects = new Rectangle[total];
        for (int i = 0; i < total; i++)
        {
            int x = (i % columns) * size;
            int y = (i / columns) * size;
            TileRects[i] = new Rectangle(x, y, size, size);
        }
    }

    private void LoadData(string[] sheet)
    {
        // + 1 to avoid drawing issues in the border 
        Data = new int[Constants.GameDataSizes.SpriteSheetY + 1, Constants.GameDataSizes.SpriteSheetX + 1];

        for (int r = 0; r < Constants.GameDataSizes.SpriteSheetY; r++)
        {
            for (int c = 0; c < Constants.GameDataSizes.SpriteSheetX; c++)
            {
                char ch = '0';

                if (sheet != null &&
                    r < sheet.Length &&
                    sheet[r] != null &&
                    c < sheet[r].Length)
                {
                    ch = char.ToLowerInvariant(sheet[r][c]);
                }

                if (ch >= '0' && ch <= '9')
                    Data[r, c] = ch - '0';
                else if (ch >= 'a' && ch <= 'f')
                    Data[r, c] = ch - 'a' + 10;
                else
                    Data[r, c] = 0;
            }
        }
    }

    public void DataToTexture()
    {
        int width = Data.GetLength(1);
        int height = Data.GetLength(0);
        int pixelCount = width * height;

        for (int ci = 0; ci < Constants.GameDataSizes.ColorPalette; ci++)
        {
            var maskData = new Color[pixelCount];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    maskData[y * width + x] = Data[y, x] == ci ? Color.White : ColorPalette.TransparentColor;

            ColorTextures[ci] ??= new Texture2D(mono8.GraphicsDeviceRef, width, height);
            ColorTextures[ci].SetData(maskData);
        }
    }

    private static bool IsValidColor(int colorIndex) =>
        colorIndex >= 0 && colorIndex < Constants.GameDataSizes.ColorPalette;

    private static bool IsValidPos(int x, int y) =>
        x >= 0 && x < Constants.GameDataSizes.SpriteSheetX &&
        y >= 0 && y < Constants.GameDataSizes.SpriteSheetY;

    private bool TrySetPixelData(int x, int y, int colorIndex)
    {
        if (!IsValidPos(x, y)) return false;

        Data[y, x] = colorIndex;
        return true;
    }

    private void SetRectFillData(int x, int y, int w, int h, int colorIndex)
    {
        for (int yy = y; yy < y + h; yy++)
            for (int xx = x; xx < x + w; xx++)
                TrySetPixelData(xx, yy, colorIndex);
    }

    private void UpdateTextureRegion(int x, int y, int w, int h)
    {
        int width = Data.GetLength(1);
        int height = Data.GetLength(0);

        int left = Math.Max(x, 0);
        int top = Math.Max(y, 0);
        int right = Math.Min(x + w, width);
        int bottom = Math.Min(y + h, height);

        int regionWidth = right - left;
        int regionHeight = bottom - top;
        if (regionWidth <= 0 || regionHeight <= 0) return;

        var rect = new Rectangle(left, top, regionWidth, regionHeight);
        var maskData = new Color[regionWidth * regionHeight];

        for (int ci = 0; ci < Constants.GameDataSizes.ColorPalette; ci++)
        {
            if (ColorTextures[ci] == null) continue;

            for (int ry = 0; ry < regionHeight; ry++)
                for (int rx = 0; rx < regionWidth; rx++)
                    maskData[ry * regionWidth + rx] =
                        Data[top + ry, left + rx] == ci ? Color.White : ColorPalette.TransparentColor;

            ColorTextures[ci].SetData(0, rect, maskData, 0, maskData.Length);
        }
    }

    public void SetPixel(int x, int y, int colorIndex)
    {
        if (!IsValidColor(colorIndex) || !IsValidPos(x, y)) return;
        if (Data[y, x] == colorIndex) return;

        SaveSnapshot();
        TrySetPixelData(x, y, colorIndex);
        UpdateTextureRegion(x, y, 1, 1);
    }

    public void SetRectFill(int x, int y, int w, int h, int colorIndex)
    {
        if (!IsValidColor(colorIndex)) return;

        SaveSnapshot();
        SetRectFillData(x, y, w, h, colorIndex);
        UpdateTextureRegion(x, y, w, h);
    }

    public void SetRect(int x, int y, int w, int h, int colorIndex)
    {
        if (!IsValidColor(colorIndex)) return;

        SaveSnapshot();
        var thickness = 1;
        // Top
        SetRectFillData(x, y, w, thickness, colorIndex);
        // Bottom
        SetRectFillData(x, y + h - thickness, w, thickness, colorIndex);
        // Left
        SetRectFillData(x, y + 1, thickness, h - 2, colorIndex);
        // Right
        SetRectFillData(x + w - thickness, y + 1, thickness, h - 2, colorIndex);

        UpdateTextureRegion(x, y, w, h);
    }

    public void SetOval(int x0, int y0, int x1, int y1, int colorIndex)
    {
        if (!IsValidColor(colorIndex)) return;

        SaveSnapshot();
        OvalMath.DrawOutline(x0, y0, x1, y1, (px, py) => TrySetPixelData(px, py, colorIndex));

        int x = Math.Min(x0, x1), y = Math.Min(y0, y1);
        UpdateTextureRegion(x, y, Math.Abs(x1 - x0) + 1, Math.Abs(y1 - y0) + 1);
    }

    public void SetOvalFill(int x0, int y0, int x1, int y1, int colorIndex)
    {
        if (!IsValidColor(colorIndex)) return;

        SaveSnapshot();
        OvalMath.DrawFill(x0, y0, x1, y1, (row, leftX, rightX) =>
            SetRectFillData(leftX, row, rightX - leftX + 1, 1, colorIndex));

        int x = Math.Min(x0, x1), y = Math.Min(y0, y1);
        UpdateTextureRegion(x, y, Math.Abs(x1 - x0) + 1, Math.Abs(y1 - y0) + 1);
    }

    public void PaintBucket(int x, int y, int regionX, int regionY, int regionW, int regionH, int colorIndex)
    {
        if (!IsValidColor(colorIndex) || !IsValidPos(x, y)) return;

        int regionMinX = Math.Max(regionX, 0);
        int regionMinY = Math.Max(regionY, 0);
        int regionMaxX = Math.Min(regionX + regionW, Constants.GameDataSizes.SpriteSheetX) - 1;
        int regionMaxY = Math.Min(regionY + regionH, Constants.GameDataSizes.SpriteSheetY) - 1;
        if (x < regionMinX || x > regionMaxX || y < regionMinY || y > regionMaxY) return;

        int targetColor = Data[y, x];
        if (targetColor == colorIndex) return;

        SaveSnapshot();

        int minX = x, maxX = x, minY = y, maxY = y;
        var queue = new Queue<(int x, int y)>();

        void TryEnqueue(int px, int py)
        {
            if (px < regionMinX || px > regionMaxX || py < regionMinY || py > regionMaxY) return;
            if (!IsValidPos(px, py) || Data[py, px] != targetColor) return;
            Data[py, px] = colorIndex;
            queue.Enqueue((px, py));
        }

        Data[y, x] = colorIndex;
        queue.Enqueue((x, y));

        while (queue.Count > 0)
        {
            var (cx, cy) = queue.Dequeue();

            if (cx < minX) minX = cx;
            if (cx > maxX) maxX = cx;
            if (cy < minY) minY = cy;
            if (cy > maxY) maxY = cy;

            TryEnqueue(cx + 1, cy);
            TryEnqueue(cx - 1, cy);
            TryEnqueue(cx, cy + 1);
            TryEnqueue(cx, cy - 1);
        }

        UpdateTextureRegion(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    public void MoveGrid(int x, int y, int w, int h, int deltaX, int deltaY)
    {
        int x1 = Math.Max(x, 0);
        int y1 = Math.Max(y, 0);
        int x2 = Math.Min(x + w, Constants.GameDataSizes.SpriteSheetX);
        int y2 = Math.Min(y + h, Constants.GameDataSizes.SpriteSheetY);
        int regionW = x2 - x1;
        int regionH = y2 - y1;
        if (regionW <= 0 || regionH <= 0) return;

        SaveSnapshot();

        var temp = new int[regionH, regionW];
        for (int row = 0; row < regionH; row++)
            for (int col = 0; col < regionW; col++)
            {
                int newRow = ((row + deltaY) % regionH + regionH) % regionH;
                int newCol = ((col + deltaX) % regionW + regionW) % regionW;
                temp[newRow, newCol] = Data[y1 + row, x1 + col];
            }

        for (int row = 0; row < regionH; row++)
            for (int col = 0; col < regionW; col++)
                Data[y1 + row, x1 + col] = temp[row, col];

        UpdateTextureRegion(x1, y1, regionW, regionH);
    }

    public void FlipRegionHorizontal(int x, int y, int w, int h)
    {
        int x1 = Math.Max(x, 0);
        int y1 = Math.Max(y, 0);
        int x2 = Math.Min(x + w, Constants.GameDataSizes.SpriteSheetX);
        int y2 = Math.Min(y + h, Constants.GameDataSizes.SpriteSheetY);
        int regionW = x2 - x1;
        int regionH = y2 - y1;
        if (regionW <= 0 || regionH <= 0) return;

        SaveSnapshot();

        for (int row = 0; row < regionH; row++)
            for (int col = 0; col < regionW / 2; col++)
            {
                int leftCol = x1 + col;
                int rightCol = x1 + regionW - 1 - col;
                (Data[y1 + row, leftCol], Data[y1 + row, rightCol]) = (Data[y1 + row, rightCol], Data[y1 + row, leftCol]);
            }

        UpdateTextureRegion(x1, y1, regionW, regionH);
    }

    public void FlipRegionVertical(int x, int y, int w, int h)
    {
        int x1 = Math.Max(x, 0);
        int y1 = Math.Max(y, 0);
        int x2 = Math.Min(x + w, Constants.GameDataSizes.SpriteSheetX);
        int y2 = Math.Min(y + h, Constants.GameDataSizes.SpriteSheetY);
        int regionW = x2 - x1;
        int regionH = y2 - y1;
        if (regionW <= 0 || regionH <= 0) return;

        SaveSnapshot();

        for (int row = 0; row < regionH / 2; row++)
        {
            int topRow = y1 + row;
            int bottomRow = y1 + regionH - 1 - row;
            for (int col = 0; col < regionW; col++)
                (Data[topRow, x1 + col], Data[bottomRow, x1 + col]) = (Data[bottomRow, x1 + col], Data[topRow, x1 + col]);
        }

        UpdateTextureRegion(x1, y1, regionW, regionH);
    }

    public void RotateRegion90Clockwise(int x, int y, int w, int h)
    {
        int x1 = Math.Max(x, 0);
        int y1 = Math.Max(y, 0);
        int x2 = Math.Min(x + w, Constants.GameDataSizes.SpriteSheetX);
        int y2 = Math.Min(y + h, Constants.GameDataSizes.SpriteSheetY);
        int regionW = x2 - x1;
        int regionH = y2 - y1;
        if (regionW <= 0 || regionH <= 0 || regionW != regionH) return;

        SaveSnapshot();

        int size = regionW;
        var temp = new int[size, size];
        for (int row = 0; row < size; row++)
            for (int col = 0; col < size; col++)
                temp[col, size - 1 - row] = Data[y1 + row, x1 + col];

        for (int row = 0; row < size; row++)
            for (int col = 0; col < size; col++)
                Data[y1 + row, x1 + col] = temp[row, col];

        UpdateTextureRegion(x1, y1, size, size);
    }

    public void CopyRegion(int x, int y, int w, int h)
    {
        int x1 = Math.Max(x, 0);
        int y1 = Math.Max(y, 0);
        int x2 = Math.Min(x + w, Constants.GameDataSizes.SpriteSheetX);
        int y2 = Math.Min(y + h, Constants.GameDataSizes.SpriteSheetY);
        int regionW = x2 - x1;
        int regionH = y2 - y1;
        if (regionW <= 0 || regionH <= 0) return;

        _copy = new int[regionH, regionW];
        for (int row = 0; row < regionH; row++)
            for (int col = 0; col < regionW; col++)
                _copy[row, col] = Data[y1 + row, x1 + col];
    }

    public void PasteRegion(int x, int y)
    {
        if (_copy == null) return;

        int copyH = _copy.GetLength(0);
        int copyW = _copy.GetLength(1);

        int x1 = Math.Max(x, 0);
        int y1 = Math.Max(y, 0);
        int x2 = Math.Min(x + copyW, Constants.GameDataSizes.SpriteSheetX);
        int y2 = Math.Min(y + copyH, Constants.GameDataSizes.SpriteSheetY);
        int regionW = x2 - x1;
        int regionH = y2 - y1;
        if (regionW <= 0 || regionH <= 0) return;

        SaveSnapshot();

        for (int row = 0; row < regionH; row++)
            for (int col = 0; col < regionW; col++)
                Data[y1 + row, x1 + col] = _copy[row, col];

        UpdateTextureRegion(x1, y1, regionW, regionH);
    }

    public void ClearGrid(int x, int y, int w, int h, int colorIndex = 0)
    {
        if (!IsValidColor(colorIndex)) return;

        SaveSnapshot();
        SetRectFillData(x, y, w, h, colorIndex);
        UpdateTextureRegion(x, y, w, h);
    }

    public void DrawSub(int sx, int sy, int sw, int sh, int dx, int dy, int dw, int dh, bool flipX, bool flipY)
    {
        var source = new Rectangle(sx, sy, sw, sh);
        var destination = new Rectangle(dx, dy, dw, dh);

        SpriteEffects effects = SpriteEffects.None;
        if (flipX) effects |= SpriteEffects.FlipHorizontally;
        if (flipY) effects |= SpriteEffects.FlipVertically;

        for (int ci = 0; ci < Constants.GameDataSizes.ColorPalette; ci++)
        {
            if (ColorPalette.IsDrawTransparent(ci)) continue;
            if (ColorTextures[ci] == null) continue;
            mono8.SpriteBatch.Draw(ColorTextures[ci], destination, source, effects, ci);
        }
    }

    public void Draw(
        int n, int x, int y, int w = 1, int h = 1,
        int scale = 1, bool flipX = false, bool flipY = false)
    {
        var source = new Rectangle(
            (n % Constants.GameDataSizes.SpriteSheetColumns) * Constants.GameDataSizes.TileSize,
            (n / Constants.GameDataSizes.SpriteSheetColumns) * Constants.GameDataSizes.TileSize,
            w * Constants.GameDataSizes.TileSize,
            h * Constants.GameDataSizes.TileSize);
        var destination = new Rectangle(
            x, y,
            w * Constants.GameDataSizes.TileSize * scale,
            h * Constants.GameDataSizes.TileSize * scale);

        SpriteEffects effects = SpriteEffects.None;
        if (flipX) effects |= SpriteEffects.FlipHorizontally;
        if (flipY) effects |= SpriteEffects.FlipVertically;

        for (int ci = 0; ci < Constants.GameDataSizes.ColorPalette; ci++)
        {
            if (ColorPalette.IsDrawTransparent(ci)) continue;
            if (ColorTextures[ci] == null) continue;
            mono8.SpriteBatch.Draw(ColorTextures[ci], destination, source, effects, ci);
        }
    }
}