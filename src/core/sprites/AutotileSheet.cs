namespace mono8.core.sprites;

/// <summary>
/// Marks which 4x4 blocks of the sprite sheet hold a 15-piece autotile.
///
/// A block is a 4x4 run of sprites aligned to a multiple of four on both axes. Its top-left cell
/// is the block's empty tile - which puts the first block's on sprite 0, the sheet's reserved
/// "no sprite" sentinel - and the other fifteen hold one piece each.
///
/// Terrain is tracked per quadrant rather than per edge: each piece covers some subset of its
/// tile's four 4x4 quadrants, and <see cref="CellQuadrants"/> fixes which subset belongs in which
/// cell of the block.
///
/// The sheet is 30 rows tall, so its last two rows cannot form a block and sprites there belong
/// to none.
/// </summary>
internal class AutotileSheet
{
    /// <summary>A block is BlockSize x BlockSize sprites.</summary>
    public const int BlockSize = 4;

    /// <summary>A quadrant is a quarter of a tile, so terrain is read to this many pixels.</summary>
    public const int QuadrantSize = Constants.GameDataSizes.TileSize / 2;

    // The four quadrants of a tile, each covering a quarter of it.
    public const int TopLeft = 1;
    public const int TopRight = 2;
    public const int BottomLeft = 4;
    public const int BottomRight = 8;

    /// <summary>
    /// Terrain each cell of a block covers, in reading order (cell 0 is the top-left, empty one).
    /// The layout is fixed by the tileset, not derived: it is neither a quadrant bitmask nor a
    /// neighbour one, so it is spelled out here and read straight off by the editor's preview.
    /// </summary>
    public static readonly int[] CellQuadrants =
    {
        0,                                              // the empty tile
        BottomRight,
        TopRight | BottomLeft,
        TopLeft,

        BottomLeft,
        TopRight | BottomRight,
        TopLeft | BottomLeft | BottomRight,
        BottomLeft | BottomRight,

        TopLeft | BottomRight,
        TopRight | BottomLeft | BottomRight,
        TopLeft | TopRight | BottomLeft | BottomRight, // fully solid
        TopLeft | TopRight | BottomLeft,

        TopRight,
        TopLeft | TopRight,
        TopLeft | TopRight | BottomRight,
        TopLeft | BottomLeft,
    };

    /// <summary>
    /// Inverse of <see cref="CellQuadrants"/>: the cell of a block covering a given set of
    /// quadrants. The sixteen cells cover the sixteen possible subsets exactly once, so every
    /// subset names one cell and painting can pick a piece straight from the terrain it must show.
    /// </summary>
    public static readonly int[] CellForQuadrants = BuildCellForQuadrants();

    private static int[] BuildCellForQuadrants()
    {
        var cells = new int[CellQuadrants.Length];
        for (int cell = 0; cell < CellQuadrants.Length; cell++)
            cells[CellQuadrants[cell]] = cell;
        return cells;
    }

    public const int BlocksX = Constants.GameDataSizes.SpriteSheetColumns / BlockSize; // 8
    public const int BlocksY = Constants.GameDataSizes.SpriteSheetRows / BlockSize;    // 7

    private bool[,] _enabled = new bool[BlocksY, BlocksX];

    /// <summary>
    /// The block a sprite belongs to. False when the sprite sits in the sheet's leftover rows,
    /// which are too short to form one.
    /// </summary>
    public static bool TryGetBlock(int spriteId, out int blockX, out int blockY)
    {
        int columns = Constants.GameDataSizes.SpriteSheetColumns;

        blockX = 0;
        blockY = 0;
        if (spriteId < 0 || spriteId > Constants.GameDataSizes.MaxSpriteIndex) return false;

        blockX = spriteId % columns / BlockSize;
        blockY = spriteId / columns / BlockSize;
        return blockX < BlocksX && blockY < BlocksY;
    }

    /// <summary>
    /// The cell of a given block a sprite holds, in the same reading order as
    /// <see cref="CellQuadrants"/>. False when the sprite belongs to some other block.
    /// </summary>
    public static bool TryGetCell(int spriteId, int blockX, int blockY, out int cell)
    {
        cell = 0;
        if (!TryGetBlock(spriteId, out int bx, out int by) || bx != blockX || by != blockY) return false;

        int columns = Constants.GameDataSizes.SpriteSheetColumns;
        cell = spriteId / columns % BlockSize * BlockSize + spriteId % columns % BlockSize;
        return true;
    }

    /// <summary>The sprite holding one cell of a block, the inverse of <see cref="TryGetCell"/>.</summary>
    public static int SpriteFor(int blockX, int blockY, int cell) =>
        (blockY * BlockSize + cell / BlockSize) * Constants.GameDataSizes.SpriteSheetColumns
            + blockX * BlockSize + cell % BlockSize;

    private static bool InBounds(int blockX, int blockY) =>
        blockX >= 0 && blockX < BlocksX && blockY >= 0 && blockY < BlocksY;

    public bool IsEnabled(int blockX, int blockY) =>
        InBounds(blockX, blockY) && _enabled[blockY, blockX];

    public void SetEnabled(int blockX, int blockY, bool value)
    {
        if (InBounds(blockX, blockY)) _enabled[blockY, blockX] = value;
    }

    public void Toggle(int blockX, int blockY) => SetEnabled(blockX, blockY, !IsEnabled(blockX, blockY));

    /// <summary>
    /// Whether a point of the map is covered by autotile terrain. Coordinates are pixels over the
    /// whole map sheet - map cell coordinates times the tile size, the same space mget reads - so a
    /// game applies its own camera and layer offsets before asking.
    ///
    /// Terrain is read off the tiles at quadrant precision, a quarter of a tile, because that is how
    /// finely a piece describes it: an edge piece covers half its tile and a diagonal two opposite
    /// quarters of it, which no per-sprite flag can say. Only tiles belonging to a block marked as an
    /// autotile carry terrain; loose art and unmarked blocks carry none.
    ///
    /// <paramref name="spriteId"/> narrows the question to the terrain of one block - the one that
    /// sprite belongs to - so a game can ask about its walls without its water answering. The default
    /// of -1 asks about every autotile alike.
    /// </summary>
    public bool Collides(int x, int y, int spriteId = -1)
    {
        int tileSize = Constants.GameDataSizes.TileSize;

        // Negative coordinates are off the map, and saying so here keeps them out of the division
        // below, which truncates towards zero and would otherwise fold them into the first cell.
        if (x < 0 || y < 0) return false;

        int quadrants = QuadrantsAt(x / tileSize, y / tileSize, spriteId);
        return (quadrants & QuadrantOf(x % tileSize, y % tileSize)) != 0;
    }

    /// <summary>
    /// Whether a rectangle of the map meets autotile terrain anywhere: the same question as the
    /// point overload, asked of every quadrant the rectangle overlaps. The rectangle runs from
    /// (<paramref name="x"/>, <paramref name="y"/>) to (x + w - 1, y + h - 1), and an empty one -
    /// either side zero or negative - meets nothing.
    /// </summary>
    /// <inheritdoc cref="Collides(int, int, int)"/>
    public bool Collides(int x, int y, int w, int h, int spriteId = -1)
    {
        if (w <= 0 || h <= 0) return false;

        int tileSize = Constants.GameDataSizes.TileSize;

        // Clipped to the map, so a rectangle far larger than it costs no more than covering it.
        int left = Math.Max(x, 0);
        int top = Math.Max(y, 0);
        int right = Math.Min(x + w - 1, Constants.GameDataSizes.MapSheetX * tileSize - 1);
        int bottom = Math.Min(y + h - 1, Constants.GameDataSizes.MapSheetY * tileSize - 1);
        if (right < left || bottom < top) return false;

        for (int cellY = top / tileSize; cellY <= bottom / tileSize; cellY++)
        {
            for (int cellX = left / tileSize; cellX <= right / tileSize; cellX++)
            {
                int quadrants = QuadrantsAt(cellX, cellY, spriteId);
                if (quadrants == 0) continue;

                if ((quadrants & Coverage(cellX, cellY, left, top, right, bottom)) != 0) return true;
            }
        }
        return false;
    }

    /// <summary>
    /// The terrain the tile at a map cell covers, and none at all for anything that is not a piece of
    /// a block marked as an autotile. An empty cell needs no special case: sprite 0 is the first
    /// block's empty tile, which covers no quadrant.
    /// </summary>
    private int QuadrantsAt(int cellX, int cellY, int spriteId)
    {
        int tile = Mono8API.MapSheet.GetTile(cellX, cellY);

        if (!TryGetBlock(tile, out int blockX, out int blockY)) return 0;
        if (!IsEnabled(blockX, blockY)) return 0;

        if (spriteId >= 0
            && (!TryGetBlock(spriteId, out int askedX, out int askedY)
                || askedX != blockX || askedY != blockY))
        {
            return 0;
        }

        TryGetCell(tile, blockX, blockY, out int cell);
        return CellQuadrants[cell];
    }

    /// <summary>The quadrant a pixel sits on, given its position within its tile.</summary>
    private static int QuadrantOf(int tileX, int tileY) => tileY < QuadrantSize
        ? tileX < QuadrantSize ? TopLeft : TopRight
        : tileX < QuadrantSize ? BottomLeft : BottomRight;

    /// <summary>
    /// The quadrants of one cell a rectangle overlaps. Only cells the rectangle already reaches are
    /// asked, so each half of the cell needs one comparison: it is covered unless the rectangle stops
    /// short of it.
    /// </summary>
    private static int Coverage(int cellX, int cellY, int left, int top, int right, int bottom)
    {
        int tileSize = Constants.GameDataSizes.TileSize;
        int midX = cellX * tileSize + QuadrantSize;
        int midY = cellY * tileSize + QuadrantSize;

        int mask = 0;
        if (top < midY)
        {
            if (left < midX) mask |= TopLeft;
            if (right >= midX) mask |= TopRight;
        }
        if (bottom >= midY)
        {
            if (left < midX) mask |= BottomLeft;
            if (right >= midX) mask |= BottomRight;
        }
        return mask;
    }

    /// <summary>One line per block row, one '0' / '1' per block. A missing or short file reads as all-off.</summary>
    public void LoadAutotiles(string[] sheet)
    {
        _enabled = new bool[BlocksY, BlocksX];

        for (int by = 0; by < BlocksY; by++)
        {
            string row = sheet != null && by < sheet.Length ? sheet[by] : null;
            for (int bx = 0; bx < BlocksX; bx++)
                _enabled[by, bx] = row != null && bx < row.Length && row[bx] == '1';
        }
    }

    public string[] ToAutotileLines()
    {
        var lines = new string[BlocksY];
        for (int by = 0; by < BlocksY; by++)
        {
            var chars = new char[BlocksX];
            for (int bx = 0; bx < BlocksX; bx++)
                chars[bx] = _enabled[by, bx] ? '1' : '0';
            lines[by] = new string(chars);
        }
        return lines;
    }
}
