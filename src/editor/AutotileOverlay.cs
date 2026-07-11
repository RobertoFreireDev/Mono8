namespace mono8.editor;

/// <summary>
/// The terrain overlay both editors lay over a 4x4 autotile block - the map editor over the block on
/// the sheet, the sprite editor over the block on its canvas. A cell draws a quarter-tile of colour on
/// every quadrant its piece covers with terrain, and nothing on the ones it leaves empty, so cell 0 -
/// the block's empty tile - draws nothing at all.
/// </summary>
internal static class AutotileOverlay
{
    /// <summary>The icon on the autotile button of either editor.</summary>
    public const int Icon = 16;

    /// <summary>Half-transparent, so the art under the overlay stays readable.</summary>
    private const float Opacity = 0.5f;

    /// <summary>Green once the block is an autotile, blue while it is not.</summary>
    public static int Fill(int blockX, int blockY) =>
        Mono8API.AutotileSheet.IsEnabled(blockX, blockY)
            ? Constants.Colors.Green
            : Constants.Colors.Blue;

    /// <summary>One cell of the overlay, <paramref name="tilePx"/> pixels to a side.</summary>
    public static void DrawCell(IMono8API api, int x, int y, int tilePx, int cell, int fill)
    {
        int half = tilePx / 2;
        int quadrants = AutotileSheet.CellQuadrants[cell];

        DrawQuadrant(api, quadrants, AutotileSheet.TopLeft, x, y, half, fill);
        DrawQuadrant(api, quadrants, AutotileSheet.TopRight, x + half, y, half, fill);
        DrawQuadrant(api, quadrants, AutotileSheet.BottomLeft, x, y + half, half, fill);
        DrawQuadrant(api, quadrants, AutotileSheet.BottomRight, x + half, y + half, half, fill);
    }

    private static void DrawQuadrant(IMono8API api, int quadrants, int quadrant, int x, int y, int half, int fill)
    {
        if ((quadrants & quadrant) == 0) return;

        api.rectfill(x, y, x + half - 1, y + half - 1, fill, Opacity);
    }
}
