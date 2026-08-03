namespace mono8.core.sprites;

/// <summary>
/// Marks which 8x6 blocks of the sprite sheet hold a 47-piece autotile.
///
/// A block is an 8-wide, 6-tall run of sprites aligned to a multiple of its own size on both axes.
/// Its top-left cell is the block's empty tile - which puts the first block's on sprite 0, the
/// sheet's reserved "no sprite" sentinel - and the other forty-seven hold one piece each.
///
/// Terrain is tracked per cell: a cell either holds terrain or it does not, and which piece it shows
/// is decided by its eight neighbours - the four cardinals always, and each diagonal only when both
/// of the cardinals beside it hold terrain too. Forty-seven neighbourhoods survive that rule, and
/// <see cref="CellMask"/> fixes which one belongs in which cell of the block.
///
/// 8x6 divides the 32x30 sheet exactly, so unlike a square block every sprite belongs to one.
/// </summary>
internal class AutotileSheet
{
    /// <summary>A block is BlockW x BlockH sprites: one empty tile and the 47 pieces.</summary>
    public const int BlockW = 8;
    public const int BlockH = 6;
    public const int CellCount = BlockW * BlockH;

    /// <summary>The block's top-left cell, which holds no terrain and so erases where it is stamped.</summary>
    public const int EmptyCell = 0;

    // The eight neighbours of a cell. A diagonal only counts when both of the cardinals beside it
    // count too, which is what cuts the 256 raw neighbourhoods down to 47.
    public const int N = 1;
    public const int E = 2;
    public const int S = 4;
    public const int W = 8;
    public const int NE = 16;
    public const int SE = 32;
    public const int SW = 64;
    public const int NW = 128;

    private const int Cardinals = N | E | S | W;

    /// <summary>Marks <see cref="CellMask"/>'s empty cell, which stands for no terrain at all.</summary>
    public const int NoTerrain = -1;

    /// <summary>
    /// The neighbourhood each cell of a block is drawn for, in reading order (cell 0 is the top-left,
    /// empty one). The layout is fixed by the tileset, not derived: the rows group the pieces by how
    /// many cardinals they meet, so it is spelled out here and read straight off by the editors'
    /// guide overlay.
    /// </summary>
    public static readonly int[] CellMask =
    {
        // Nothing, a lone cell, the four end caps and the two straights.
        NoTerrain,
        0,
        N,
        E,
        S,
        W,
        N | S,
        E | W,

        // Two cardinals side by side, each without and with the diagonal that closes it.
        N | E,
        N | E | NE,
        E | S,
        E | S | SE,
        S | W,
        S | W | SW,
        W | N,
        W | N | NW,

        // Three cardinals, over the two diagonals each of them leaves open.
        N | E | S,
        N | E | S | NE,
        N | E | S | SE,
        N | E | S | NE | SE,
        E | S | W,
        E | S | W | SE,
        E | S | W | SW,
        E | S | W | SE | SW,

        S | W | N,
        S | W | N | SW,
        S | W | N | NW,
        S | W | N | SW | NW,
        W | N | E,
        W | N | E | NW,
        W | N | E | NE,
        W | N | E | NW | NE,

        // All four cardinals, over the sixteen subsets of the diagonals, counted in binary.
        Cardinals,
        Cardinals | NE,
        Cardinals | SE,
        Cardinals | NE | SE,
        Cardinals | SW,
        Cardinals | NE | SW,
        Cardinals | SE | SW,
        Cardinals | NE | SE | SW,

        Cardinals | NW,
        Cardinals | NE | NW,
        Cardinals | SE | NW,
        Cardinals | NE | SE | NW,
        Cardinals | SW | NW,
        Cardinals | NE | SW | NW,
        Cardinals | SE | SW | NW,
        Cardinals | NE | SE | SW | NW,   // fully surrounded: the plain interior tile
    };

    /// <summary>
    /// Inverse of <see cref="CellMask"/>: the cell of a block drawn for a given neighbourhood. The
    /// forty-seven pieces cover the forty-seven canonical neighbourhoods exactly once, so every one
    /// names a cell and painting can pick a piece straight from what surrounds it. Index it with a
    /// mask that has already been through <see cref="Canonicalize"/>.
    /// </summary>
    public static readonly int[] CellForMask = BuildCellForMask();

    private static int[] BuildCellForMask()
    {
        var cells = new int[NW * 2];
        for (int cell = 0; cell < CellMask.Length; cell++)
        {
            if (CellMask[cell] != NoTerrain) cells[CellMask[cell]] = cell;
        }
        return cells;
    }

    /// <summary>
    /// Drops the diagonals a neighbourhood cannot show: one only reads as a neighbour when both of
    /// the cardinals beside it are there, since otherwise the gap between them already cuts the
    /// corner off. Every raw neighbourhood folds onto one of the forty-seven this leaves.
    /// </summary>
    public static int Canonicalize(int mask)
    {
        if ((mask & (N | E)) != (N | E)) mask &= ~NE;
        if ((mask & (S | E)) != (S | E)) mask &= ~SE;
        if ((mask & (S | W)) != (S | W)) mask &= ~SW;
        if ((mask & (N | W)) != (N | W)) mask &= ~NW;
        return mask;
    }

    public const int BlocksX = Constants.GameDataSizes.SpriteSheetColumns / BlockW; // 4
    public const int BlocksY = Constants.GameDataSizes.SpriteSheetRows / BlockH;    // 5

    private bool[,] _enabled = new bool[BlocksY, BlocksX];

    /// <summary>The block a sprite belongs to. False only for a sprite id off the sheet.</summary>
    public static bool TryGetBlock(int spriteId, out int blockX, out int blockY)
    {
        int columns = Constants.GameDataSizes.SpriteSheetColumns;

        blockX = 0;
        blockY = 0;
        if (spriteId < 0 || spriteId > Constants.GameDataSizes.MaxSpriteIndex) return false;

        blockX = spriteId % columns / BlockW;
        blockY = spriteId / columns / BlockH;
        return blockX < BlocksX && blockY < BlocksY;
    }

    /// <summary>
    /// The cell of a given block a sprite holds, in the same reading order as
    /// <see cref="CellMask"/>. False when the sprite belongs to some other block.
    /// </summary>
    public static bool TryGetCell(int spriteId, int blockX, int blockY, out int cell)
    {
        cell = 0;
        if (!TryGetBlock(spriteId, out int bx, out int by) || bx != blockX || by != blockY) return false;

        int columns = Constants.GameDataSizes.SpriteSheetColumns;
        cell = spriteId / columns % BlockH * BlockW + spriteId % columns % BlockW;
        return true;
    }

    /// <summary>The sprite holding one cell of a block, the inverse of <see cref="TryGetCell"/>.</summary>
    public static int SpriteFor(int blockX, int blockY, int cell) =>
        (blockY * BlockH + cell / BlockW) * Constants.GameDataSizes.SpriteSheetColumns
            + blockX * BlockW + cell % BlockW;

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
