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
/// Stamps one piece of a 15-piece autotile block onto a grid, and refits the tiles around it.
///
/// Terrain sits on the corners of the cell grid, offset half a tile from the tiles that show it:
/// corner (cx, cy) is the top-left corner of cell (cx, cy), and a cell shows terrain in a quadrant
/// exactly when the corner at that quadrant carries it. The stamped piece names the terrain of the
/// cell it marks - its four corners are set to exactly the quadrants the piece covers, laying terrain
/// on them and clearing it off them alike - which is what makes the stamp come back out of the fit as
/// the very piece that was picked, be that the solid tile, an edge, a diagonal, or the block's empty
/// cell. The eight cells around it share those corners, so they are refitted against them and end up
/// joining the stamp: they grow edges up against the terrain it lays and retract from the terrain it
/// clears.
///
/// Nothing stores the corners: away from the stamp they are read back off the tiles already on the
/// grid, so a stroke composes with whatever terrain it grows into.
/// </summary>
internal readonly struct AutotileStamp
{
    /// <summary>The stamp and the eight cells sharing a corner with it.</summary>
    private const int Neighbourhood = 9;

    private readonly IAutotileGrid _grid;
    private readonly int _blockX;
    private readonly int _blockY;
    private readonly int _cellX;
    private readonly int _cellY;

    /// <summary>The terrain the stamped piece covers, which fixes the stamp's own four corners.</summary>
    private readonly int _quadrants;

    private AutotileStamp(IAutotileGrid grid, int blockX, int blockY, int cellX, int cellY, int cell)
    {
        _grid = grid;
        _blockX = blockX;
        _blockY = blockY;
        _cellX = cellX;
        _cellY = cellY;
        _quadrants = AutotileSheet.CellQuadrants[cell];
    }

    /// <summary>Stamps <paramref name="cell"/> of the block at (<paramref name="cellX"/>, <paramref name="cellY"/>).</summary>
    public static void Paint(IAutotileGrid grid, int blockX, int blockY, int cellX, int cellY, int cell)
    {
        if (!grid.Contains(cellX, cellY)) return;

        new AutotileStamp(grid, blockX, blockY, cellX, cellY, cell).Apply();
    }

    private void Apply()
    {
        // The 3x3 around the stamp holds every cell sharing a corner with it, so nothing outside it
        // can be disturbed. The masks are all worked out against the pre-paint grid before any tile is
        // written, so the order they are visited in can't feed a half-updated grid back into the next
        // fit.
        Span<int> masks = stackalloc int[Neighbourhood];
        for (int i = 0; i < Neighbourhood; i++)
        {
            var (x, y) = Neighbour(i);
            masks[i] = TerrainMask(x, y);
        }

        for (int i = 0; i < Neighbourhood; i++)
        {
            var (x, y) = Neighbour(i);
            if (!_grid.Contains(x, y)) continue;

            // The pencil owns the tile it marks - whatever was there before gives way to the stamp,
            // terrain or loose art alike - and so does any neighbour the terrain has grown into. A
            // neighbour left with no terrain at all is only worth writing when it is a piece of this
            // block, retracting to the block's empty cell; anything else there is loose art the stroke
            // never reached, so it stays as it is.
            bool isStamp = x == _cellX && y == _cellY;
            if (!isStamp && masks[i] == 0 && !IsPiece(x, y)) continue;

            _grid.SetTile(x, y, AutotileSheet.SpriteFor(_blockX, _blockY, AutotileSheet.CellForQuadrants[masks[i]]));
        }
    }

    private (int x, int y) Neighbour(int i) => (_cellX - 1 + i % 3, _cellY - 1 + i / 3);

    /// <summary>The terrain a cell must show: a quadrant for each of its four corners carrying terrain.</summary>
    private int TerrainMask(int x, int y)
    {
        int mask = 0;
        if (Terrain(x, y)) mask |= AutotileSheet.TopLeft;
        if (Terrain(x + 1, y)) mask |= AutotileSheet.TopRight;
        if (Terrain(x, y + 1)) mask |= AutotileSheet.BottomLeft;
        if (Terrain(x + 1, y + 1)) mask |= AutotileSheet.BottomRight;
        return mask;
    }

    /// <summary>
    /// The stamp names its own four corners outright. Every other corner carries terrain when any of
    /// the four cells meeting there already covers the quadrant it shares with it - and the stamp is
    /// never one of those four, so it cannot speak for a corner it does not touch.
    /// </summary>
    private bool Terrain(int cornerX, int cornerY)
    {
        int quadrant = StampQuadrant(cornerX, cornerY);
        if (quadrant != 0) return (_quadrants & quadrant) != 0;

        return (Quadrants(cornerX - 1, cornerY - 1) & AutotileSheet.BottomRight) != 0
            || (Quadrants(cornerX, cornerY - 1) & AutotileSheet.BottomLeft) != 0
            || (Quadrants(cornerX - 1, cornerY) & AutotileSheet.TopRight) != 0
            || (Quadrants(cornerX, cornerY) & AutotileSheet.TopLeft) != 0;
    }

    /// <summary>The quadrant of the stamped cell a corner sits on, and 0 for a corner that is none of its four.</summary>
    private int StampQuadrant(int cornerX, int cornerY) => (cornerX - _cellX, cornerY - _cellY) switch
    {
        (0, 0) => AutotileSheet.TopLeft,
        (1, 0) => AutotileSheet.TopRight,
        (0, 1) => AutotileSheet.BottomLeft,
        (1, 1) => AutotileSheet.BottomRight,
        _ => 0,
    };

    /// <summary>
    /// The terrain a cell of the grid already covers. Anything that is not a piece of this block -
    /// another block's tile, loose art, the far side of the grid's edge - covers none, so the stroke
    /// treats it as bare ground and fits its own edge against it.
    /// </summary>
    private int Quadrants(int x, int y) =>
        TryGetCell(x, y, out int cell) ? AutotileSheet.CellQuadrants[cell] : 0;

    private bool IsPiece(int x, int y) => TryGetCell(x, y, out _);

    /// <summary>The cell of this block held by the tile at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    private bool TryGetCell(int x, int y, out int cell)
    {
        cell = 0;
        return _grid.Contains(x, y)
            && AutotileSheet.TryGetCell(_grid.GetTile(x, y), _blockX, _blockY, out cell);
    }
}
