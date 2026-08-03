namespace mono8.editor;

/// <summary>
/// The neighbourhood overlay both editors lay over an 8x6 autotile block - the map editor over the
/// block on the sheet, the sprite editor over the block on its canvas. A blob piece is decided by
/// which of the eight cells around it hold terrain rather than by what part of its own tile it
/// covers, so a cell draws that neighbourhood: a 3x3 diagram with the tile itself in the middle and
/// a square on every neighbour its piece is drawn for. Cell 0 - the block's empty tile - is no piece
/// at all and draws nothing.
/// </summary>
internal static class AutotileOverlay
{
    /// <summary>The icon on the autotile button of either editor.</summary>
    public const int Icon = 16;

    /// <summary>Half-transparent, so the art under the overlay stays readable.</summary>
    private const float Opacity = 0.5f;

    /// <summary>The 3x3 diagram, in reading order; the middle is the tile itself and names no neighbour.</summary>
    private static readonly int[] Directions =
    {
        AutotileSheet.NW, AutotileSheet.N, AutotileSheet.NE,
        AutotileSheet.W,  0,               AutotileSheet.E,
        AutotileSheet.SW, AutotileSheet.S, AutotileSheet.SE,
    };

    /// <summary>Green once the block is an autotile, blue while it is not.</summary>
    public static int Fill(int blockX, int blockY) =>
        Mono8API.AutotileSheet.IsEnabled(blockX, blockY)
            ? Constants.Colors.Green
            : Constants.Colors.Blue;

    /// <summary>One cell of the overlay, <paramref name="tilePx"/> pixels to a side.</summary>
    public static void DrawCell(IMono8API api, int x, int y, int tilePx, int cell, int fill)
    {
        int mask = AutotileSheet.CellMask[cell];
        if (mask == AutotileSheet.NoTerrain) return;

        // A quarter of the tile per square leaves the 3x3 a half-square margin all round, and divides
        // exactly at every size the editors draw at: 8px on the sheet, 16 to 64 on the sprite canvas.
        int square = tilePx / 4;
        if (square < 1) return;

        int left = x + (tilePx - 3 * square) / 2;
        int top = y + (tilePx - 3 * square) / 2;

        for (int i = 0; i < Directions.Length; i++)
        {
            // The tile itself is always there, and drawn apart from its neighbours so the diagram
            // reads as "this cell, surrounded by these".
            bool centre = Directions[i] == 0;
            if (!centre && (mask & Directions[i]) == 0) continue;

            int px = left + i % 3 * square;
            int py = top + i / 3 * square;
            api.rectfill(px, py, px + square - 1, py + square - 1,
                centre ? Constants.Colors.White : fill, Opacity);
        }
    }
}
