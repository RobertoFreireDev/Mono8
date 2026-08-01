namespace mono8.core.maps;

internal class MapSheet
{
    public int[,] Data;

    private int[,] _copy;

    private const int MaxUndoSteps = 50;
    private readonly Stack<int[,]> _undoStack = new Stack<int[,]>();
    private readonly Stack<int[,]> _redoStack = new Stack<int[,]>();

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    public bool HasClipboard => _copy != null;

    // Public (unlike SpriteSheet's) so MapEditor can snapshot once at the start of a
    // paint stroke and then paint many tiles under that single undo step.
    public void SaveSnapshot()
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
    }

    public void Redo()
    {
        if (!CanRedo) return;

        _undoStack.Push((int[,])Data.Clone());
        Data = _redoStack.Pop();
    }

    public static bool IsValidTile(int tileIndex) =>
        tileIndex >= 0 && tileIndex <= Constants.GameDataSizes.MaxSpriteIndex;

    public void SetTile(int x, int y, int tileIndex)
    {
        if (InvalidGridPos(x, y) || !IsValidTile(tileIndex))
        {
            return;
        }
        Data[y, x] = tileIndex;
    }

    public int GetTile(int x, int y)
    {
        if (InvalidGridPos(x, y))
        {
            return 0;
        }

        return Data[y, x];
    }

    public bool InvalidGridPos(int x, int y)
    {
        return x < 0 || y < 0 ||
            x >= Constants.GameDataSizes.MapSheetX ||
            y >= Constants.GameDataSizes.MapSheetY;
    }

    /// <summary>
    /// Whether the tile under a point carries a flag. Coordinates are pixels over the whole map
    /// sheet - map cell coordinates times the tile size, the same space GetTile reads - so a game
    /// applies its own camera and room offsets before asking.
    ///
    /// The flag is a bit index, 0-7, read off the tile's sprite exactly as fget does, which leaves
    /// what each one means entirely to the game: flag 0 for solid ground, another for ice, another
    /// for hazards. Empty cells hold sprite 0, which map never draws, so they never collide however
    /// that sprite is flagged.
    /// </summary>
    public bool Collides(int x, int y, int flag = 0)
    {
        int tileSize = Constants.GameDataSizes.TileSize;

        // Negative coordinates are off the map, and saying so here keeps them out of the division
        // below, which truncates towards zero and would otherwise fold them into the first cell.
        if (x < 0 || y < 0) return false;
        if (!TryFlagMask(flag, out int mask)) return false;

        return HasFlag(x / tileSize, y / tileSize, mask);
    }

    /// <summary>
    /// Whether any tile under a rectangle carries a flag: the same question as the point overload,
    /// asked of every cell the rectangle overlaps. The rectangle runs from (<paramref name="x"/>,
    /// <paramref name="y"/>) to (x + w - 1, y + h - 1), and an empty one - either side zero or
    /// negative - meets nothing.
    /// </summary>
    /// <inheritdoc cref="Collides(int, int, int)"/>
    public bool Collides(int x, int y, int w, int h, int flag = 0)
    {
        if (w <= 0 || h <= 0) return false;
        if (!TryFlagMask(flag, out int mask)) return false;

        int tileSize = Constants.GameDataSizes.TileSize;

        // Clipped to the map, so a rectangle far larger than it costs no more than covering it.
        int left = Math.Max(x, 0);
        int top = Math.Max(y, 0);
        int right = Math.Min(x + w - 1, Constants.GameDataSizes.MapSheetX * tileSize - 1);
        int bottom = Math.Min(y + h - 1, Constants.GameDataSizes.MapSheetY * tileSize - 1);
        if (right < left || bottom < top) return false;

        for (int cellY = top / tileSize; cellY <= bottom / tileSize; cellY++)
            for (int cellX = left / tileSize; cellX <= right / tileSize; cellX++)
                if (HasFlag(cellX, cellY, mask)) return true;

        return false;
    }

    /// <summary>
    /// The bit a flag index names. False for an index outside 0-7, which is worth catching rather
    /// than shifting by: C# shifts by the count modulo 32, so flag 32 would otherwise read as flag 0.
    /// </summary>
    private static bool TryFlagMask(int flag, out int mask)
    {
        mask = 0;
        if (flag < 0 || flag >= Constants.GameDataSizes.SpriteFlags) return false;

        mask = 1 << flag;
        return true;
    }

    private bool HasFlag(int cellX, int cellY, int mask)
    {
        int tile = GetTile(cellX, cellY);
        return tile > 0 && (Mono8API.SpriteSheet.GetFlags(tile) & mask) != 0;
    }

    /// <summary>
    /// Clips an arbitrary rectangle to the map's bounds. Returns false when nothing
    /// of it remains, which every region operation treats as "nothing to do".
    /// </summary>
    private static bool TryClampRegion(int x, int y, int w, int h, out Rectangle region)
    {
        int left = Math.Max(x, 0);
        int top = Math.Max(y, 0);
        int right = Math.Min(x + w, Constants.GameDataSizes.MapSheetX);
        int bottom = Math.Min(y + h, Constants.GameDataSizes.MapSheetY);

        region = new Rectangle(left, top, right - left, bottom - top);
        return region.Width > 0 && region.Height > 0;
    }

    public void FillRegion(int x, int y, int w, int h, int tileIndex)
    {
        if (!IsValidTile(tileIndex) || !TryClampRegion(x, y, w, h, out var r)) return;

        SaveSnapshot();
        for (int row = 0; row < r.Height; row++)
            for (int col = 0; col < r.Width; col++)
                Data[r.Y + row, r.X + col] = tileIndex;
    }

    public void ClearRegion(int x, int y, int w, int h) => FillRegion(x, y, w, h, 0);

    public void CopyRegion(int x, int y, int w, int h)
    {
        if (!TryClampRegion(x, y, w, h, out var r)) return;

        _copy = new int[r.Height, r.Width];
        for (int row = 0; row < r.Height; row++)
            for (int col = 0; col < r.Width; col++)
                _copy[row, col] = Data[r.Y + row, r.X + col];
    }

    /// <summary>
    /// Pastes the clipboard with its top-left at (<paramref name="x"/>, <paramref name="y"/>).
    /// The optional bounds restrict where tiles may land (the map editor passes the active
    /// layer's quarter so a paste never bleeds into a neighbouring layer). The paste is only
    /// ever trimmed on the right/bottom here, so the clipboard stays aligned to its top-left.
    /// </summary>
    public void PasteRegion(int x, int y,
        int boundX = 0, int boundY = 0,
        int boundW = Constants.GameDataSizes.MapSheetX,
        int boundH = Constants.GameDataSizes.MapSheetY)
    {
        if (_copy == null) return;
        if (!TryClampRegion(x, y, _copy.GetLength(1), _copy.GetLength(0), out var r)) return;

        int left = Math.Max(r.X, boundX);
        int top = Math.Max(r.Y, boundY);
        int right = Math.Min(r.X + r.Width, boundX + boundW);
        int bottom = Math.Min(r.Y + r.Height, boundY + boundH);
        if (right <= left || bottom <= top) return;
        r = new Rectangle(left, top, right - left, bottom - top);

        SaveSnapshot();
        WriteRegion(r, _copy);
    }

    private void WriteRegion(Rectangle region, int[,] source)
    {
        for (int row = 0; row < region.Height; row++)
            for (int col = 0; col < region.Width; col++)
                Data[region.Y + row, region.X + col] = source[row, col];
    }

    public void LoadMaps(string[] sheet)
    {
        _undoStack.Clear();
        _redoStack.Clear();
        _copy = null;

        Data = new int[Constants.GameDataSizes.MapSheetY, Constants.GameDataSizes.MapSheetX];

        for (int r = 0; r < Constants.GameDataSizes.MapSheetY; r++)
        {
            string row = sheet != null && r < sheet.Length ? sheet[r] : null;
            for (int c = 0; c < Constants.GameDataSizes.MapSheetX; c++)
            {
                int charIndex = c * 2;
                if (row == null || charIndex + 1 >= row.Length)
                {
                    Data[r, c] = 0;
                    continue;
                }

                int value = Hex.Pair(row, charIndex);
                Data[r, c] = value > Constants.GameDataSizes.MaxSpriteIndex ? 0 : value;
            }
        }
    }

    public string[] ToMapLines()
    {
        var lines = new string[Constants.GameDataSizes.MapSheetY];
        for (int r = 0; r < Constants.GameDataSizes.MapSheetY; r++)
        {
            var sb = new System.Text.StringBuilder(Constants.GameDataSizes.MapSheetX * 2);
            for (int c = 0; c < Constants.GameDataSizes.MapSheetX; c++)
                sb.Append(Data[r, c].ToString("x2"));
            lines[r] = sb.ToString();
        }
        return lines;
    }

    public void DrawMap(
        int mapX, int mapY,   // starting tile in map
        int px, int py,       // screen position to draw at
        int width, int height, // how many tiles wide/tall to draw
        float scale = 1f,
        float colorOpaqueness = 1f,
        int layerMax = 0)
    {
        int tileSize = Constants.GameDataSizes.TileSize;
        int columns = Constants.GameDataSizes.SpriteSheetColumns;

        // Tile edges are snapped from the unscaled grid so neighbours share an edge:
        // rounding each tile's size independently would leave seams or overlaps at
        // fractional scales.
        int Edge(int slot) => (int)Math.Round(slot * tileSize * scale);

        for (int y = 0; y < height; y++)
        {
            int mapYIndex = mapY + y;
            if (mapYIndex < 0 || mapYIndex >= Constants.GameDataSizes.MapSheetY) continue;

            int top = py + Edge(y);
            int destHeight = Math.Max(1, Edge(y + 1) - Edge(y));

            for (int x = 0; x < width; x++)
            {
                int mapXIndex = mapX + x;
                if (mapXIndex < 0 || mapXIndex >= Constants.GameDataSizes.MapSheetX) continue;

                int tileIndex = Data[mapYIndex, mapXIndex];
                if (tileIndex <= 0) continue;

                if (layerMax != 0 && (Mono8API.SpriteSheet.GetFlags(tileIndex) & layerMax) == 0) continue;

                int left = px + Edge(x);
                int destWidth = Math.Max(1, Edge(x + 1) - Edge(x));

                Mono8API.SpriteSheet.DrawSub(true,
                    (tileIndex % columns) * tileSize,
                    (tileIndex / columns) * tileSize,
                    tileSize, tileSize,
                    left, top,
                    destWidth, destHeight,
                    flipX: false, flipY: false,
                    colorOpaqueness: colorOpaqueness);
            }
        }
    }
}
