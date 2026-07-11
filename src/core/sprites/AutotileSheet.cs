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

    private static bool InBounds(int blockX, int blockY) =>
        blockX >= 0 && blockX < BlocksX && blockY >= 0 && blockY < BlocksY;

    public bool IsEnabled(int blockX, int blockY) =>
        InBounds(blockX, blockY) && _enabled[blockY, blockX];

    public void SetEnabled(int blockX, int blockY, bool value)
    {
        if (InBounds(blockX, blockY)) _enabled[blockY, blockX] = value;
    }

    public void Toggle(int blockX, int blockY) => SetEnabled(blockX, blockY, !IsEnabled(blockX, blockY));

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
