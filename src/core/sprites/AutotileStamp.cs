namespace mono8.core.sprites;

/// <summary>
/// The grid an autotile stroke paints on, in whatever cell space the caller works in. The map
/// editor's layers are quarters of the map sheet, so its grid is one quarter and it offsets every
/// cell into the enabled layer on the way through.
/// </summary>
internal interface IAutotileGrid
{
    /// <summary>False past the edge of the grid, where the stroke may neither read nor write.</summary>
    bool Contains(int cellX, int cellY);

    int GetTile(int cellX, int cellY);

    void SetTile(int cellX, int cellY, int spriteId);
}

/// <summary>
/// Stamps one piece of a 47-piece autotile block onto a grid, and refits the tiles around it.
///
/// Terrain sits on the cells themselves: a cell either holds it or it does not, and which of the
/// forty-seven pieces it shows is decided by the eight cells around it. The stamped piece says only
/// whether its own cell holds terrain - every piece but the block's empty cell lays it down, and the
/// empty cell clears it - so the piece that comes back out of the fit is the one the neighbourhood
/// calls for, not the one that happened to be picked off the sheet. The eight cells around the stamp
/// count it among their own neighbours, so they are refitted too and end up joining it: they grow
/// edges up against the terrain it lays and retract from the terrain it clears.
///
/// Nothing stores the terrain: away from the stamp it is read back off the tiles already on the grid,
/// so a stroke composes with whatever terrain it grows into.
/// </summary>
internal readonly struct AutotileStamp
{
    /// <summary>The stamp and the eight cells it neighbours.</summary>
    private const int Neighbourhood = 9;

    /// <summary>Marks a cell of the neighbourhood the stroke leaves exactly as it found it.</summary>
    private const int Untouched = -1;

    private readonly IAutotileGrid _grid;
    private readonly int _blockX;
    private readonly int _blockY;
    private readonly int _cellX;
    private readonly int _cellY;

    /// <summary>Whether the stamped piece lays terrain on its cell or clears it off.</summary>
    private readonly bool _fill;

    private AutotileStamp(IAutotileGrid grid, int blockX, int blockY, int cellX, int cellY, int cell)
    {
        _grid = grid;
        _blockX = blockX;
        _blockY = blockY;
        _cellX = cellX;
        _cellY = cellY;
        _fill = cell != AutotileSheet.EmptyCell;
    }

    /// <summary>Stamps <paramref name="cell"/> of the block at (<paramref name="cellX"/>, <paramref name="cellY"/>).</summary>
    public static void Paint(IAutotileGrid grid, int blockX, int blockY, int cellX, int cellY, int cell)
    {
        if (!grid.Contains(cellX, cellY)) return;

        new AutotileStamp(grid, blockX, blockY, cellX, cellY, cell).Apply();
    }

    private void Apply()
    {
        // The 3x3 around the stamp holds every cell that neighbours it, so nothing outside it can be
        // disturbed. A fit reads two cells out - its own neighbours' neighbours - so every tile is
        // worked out against the pre-paint grid before any of them is written, and the order they are
        // visited in can't feed a half-updated grid back into the next fit.
        Span<int> tiles = stackalloc int[Neighbourhood];
        for (int i = 0; i < Neighbourhood; i++)
        {
            var (x, y) = Neighbour(i);
            tiles[i] = Fit(x, y);
        }

        for (int i = 0; i < Neighbourhood; i++)
        {
            if (tiles[i] == Untouched) continue;

            var (x, y) = Neighbour(i);
            _grid.SetTile(x, y, tiles[i]);
        }
    }

    private (int x, int y) Neighbour(int i) => (_cellX - 1 + i % 3, _cellY - 1 + i / 3);

    /// <summary>The tile a cell must end up holding, or <see cref="Untouched"/> for one the stroke leaves alone.</summary>
    private int Fit(int x, int y)
    {
        if (!_grid.Contains(x, y)) return Untouched;

        if (Terrain(x, y))
        {
            return AutotileSheet.SpriteFor(_blockX, _blockY, AutotileSheet.CellForMask[Mask(x, y)]);
        }

        // The pencil owns the tile it marks - whatever was there before gives way to the stamp,
        // terrain or loose art alike - and so does any neighbour the terrain has retracted off, which
        // falls back to the block's empty cell. A cell holding no piece of this block is loose art the
        // stroke never reached, so it stays as it is.
        bool isStamp = x == _cellX && y == _cellY;
        return isStamp || IsPiece(x, y)
            ? AutotileSheet.SpriteFor(_blockX, _blockY, AutotileSheet.EmptyCell)
            : Untouched;
    }

    /// <summary>The neighbourhood a cell of terrain must be drawn for, in canonical form.</summary>
    private int Mask(int x, int y)
    {
        int mask = 0;
        if (Terrain(x, y - 1)) mask |= AutotileSheet.N;
        if (Terrain(x + 1, y)) mask |= AutotileSheet.E;
        if (Terrain(x, y + 1)) mask |= AutotileSheet.S;
        if (Terrain(x - 1, y)) mask |= AutotileSheet.W;
        if (Terrain(x + 1, y - 1)) mask |= AutotileSheet.NE;
        if (Terrain(x + 1, y + 1)) mask |= AutotileSheet.SE;
        if (Terrain(x - 1, y + 1)) mask |= AutotileSheet.SW;
        if (Terrain(x - 1, y - 1)) mask |= AutotileSheet.NW;
        return AutotileSheet.Canonicalize(mask);
    }

    /// <summary>
    /// The stamp names its own cell outright. Every other cell holds terrain when it holds a piece of
    /// this block other than its empty one - another block's tile, loose art and the far side of the
    /// grid's edge hold none, so the stroke treats them as bare ground and fits its own edge against
    /// them.
    /// </summary>
    private bool Terrain(int x, int y)
    {
        if (x == _cellX && y == _cellY) return _fill;

        return TryGetCell(x, y, out int cell) && cell != AutotileSheet.EmptyCell;
    }

    private bool IsPiece(int x, int y) => TryGetCell(x, y, out _);

    /// <summary>The cell of this block held by the tile at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    private bool TryGetCell(int x, int y, out int cell)
    {
        cell = 0;
        return _grid.Contains(x, y)
            && AutotileSheet.TryGetCell(_grid.GetTile(x, y), _blockX, _blockY, out cell);
    }
}
