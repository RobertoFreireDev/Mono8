namespace mono8.core.graphics;

/// <summary>
/// A screen-covering mask with a hole punched through it: everything is painted in one colour
/// except a rect or an oval, and the one-tile ring just outside the hole is stippled with a
/// dither sprite so the edge fades instead of cutting.
///
/// The fill follows the camera rather than the world, so the mask covers the screen wherever
/// the camera is; the hole and the ring are in world space like every other shape call.
/// </summary>
internal static class InverseMask
{
    /// <summary>Thickness of the dither ring. One tile, so the pattern reads as a single band.</summary>
    private const int Band = Constants.GameDataSizes.TileSize;

    private static readonly RowSpans Outer = new RowSpans();
    private static readonly RowSpans Inner = new RowSpans();
    private static readonly int[] DitherColors = new int[Constants.GameDataSizes.ColorPalette];

    private readonly struct Bounds
    {
        public readonly int X0;
        public readonly int Y0;
        public readonly int X1;
        public readonly int Y1;

        public Bounds(int x0, int y0, int x1, int y1)
        {
            X0 = x0;
            Y0 = y0;
            X1 = x1;
            Y1 = y1;
        }
    }

    public static void DrawRect(int x, int y, int w, int h, int colorIndex, int ditherSpriteId, float colorOpaqueness)
    {
        Bounds screen = ScreenBounds();
        if (w <= 0 || h <= 0)
        {
            Fill(screen.X0, screen.Y0, screen.X1, screen.Y1, colorIndex, colorOpaqueness);
            return;
        }

        int band = BandFor(ditherSpriteId);
        int hx0 = x, hy0 = y, hx1 = x + w - 1, hy1 = y + h - 1;
        int ox0 = hx0 - band, oy0 = hy0 - band, ox1 = hx1 + band, oy1 = hy1 + band;

        Fill(screen.X0, screen.Y0, screen.X1, Math.Min(screen.Y1, oy0 - 1), colorIndex, colorOpaqueness);
        Fill(screen.X0, Math.Max(screen.Y0, oy1 + 1), screen.X1, screen.Y1, colorIndex, colorOpaqueness);

        int middleTop = Math.Max(screen.Y0, oy0);
        int middleBottom = Math.Min(screen.Y1, oy1);
        Fill(screen.X0, middleTop, Math.Min(screen.X1, ox0 - 1), middleBottom, colorIndex, colorOpaqueness);
        Fill(Math.Max(screen.X0, ox1 + 1), middleTop, screen.X1, middleBottom, colorIndex, colorOpaqueness);

        if (band == 0) return;

        int colors = Mono8API.SpriteSheet.TileColors(ditherSpriteId, DitherColors);
        if (colors == 0) return;

        // The four sides are disjoint, so a tile clipped by one is never redrawn by another.
        int gridX = x + w / 2, gridY = y + h / 2;
        Dither(ditherSpriteId, colors, ox0, oy0, ox1, hy0 - 1, gridX, gridY, screen, colorOpaqueness);
        Dither(ditherSpriteId, colors, ox0, hy1 + 1, ox1, oy1, gridX, gridY, screen, colorOpaqueness);
        Dither(ditherSpriteId, colors, ox0, hy0, hx0 - 1, hy1, gridX, gridY, screen, colorOpaqueness);
        Dither(ditherSpriteId, colors, hx1 + 1, hy0, ox1, hy1, gridX, gridY, screen, colorOpaqueness);
    }

    public static void DrawOval(int x, int y, int w, int h, int colorIndex, int ditherSpriteId, float colorOpaqueness)
    {
        Bounds screen = ScreenBounds();
        if (w <= 0 || h <= 0)
        {
            Fill(screen.X0, screen.Y0, screen.X1, screen.Y1, colorIndex, colorOpaqueness);
            return;
        }

        int band = BandFor(ditherSpriteId);
        int hx0 = x, hy0 = y, hx1 = x + w - 1, hy1 = y + h - 1;
        int ox0 = hx0 - band, oy0 = hy0 - band, ox1 = hx1 + band, oy1 = hy1 + band;

        // Rows past either end of the oval are solid all the way across.
        Fill(screen.X0, screen.Y0, screen.X1, Math.Min(screen.Y1, oy0 - 1), colorIndex, colorOpaqueness);
        Fill(screen.X0, Math.Max(screen.Y0, oy1 + 1), screen.X1, screen.Y1, colorIndex, colorOpaqueness);

        int top = Math.Max(screen.Y0, oy0);
        int bottom = Math.Min(screen.Y1, oy1);
        if (top > bottom) return;

        OvalMath.ComputeSpans(ox0, oy0, ox1, oy1, top, bottom, Outer);
        if (band > 0) OvalMath.ComputeSpans(hx0, hy0, hx1, hy1, top, bottom, Inner);

        FillAroundOval(top, bottom, screen, colorIndex, colorOpaqueness);

        if (band == 0) return;

        int colors = Mono8API.SpriteSheet.TileColors(ditherSpriteId, DitherColors);
        if (colors == 0) return;

        DitherRing(ditherSpriteId, colors, top, bottom, x + w / 2, y + h / 2, screen, colorOpaqueness);
    }

    /// <summary>
    /// Paints the screen either side of the oval. Rows sharing a span are emitted as one
    /// rect, which collapses the near-vertical flanks into a handful of draws.
    /// </summary>
    private static void FillAroundOval(int top, int bottom, in Bounds screen, int colorIndex, float colorOpaqueness)
    {
        int runTop = top;
        Outer.TryGet(top, out int runLeft, out int runRight);

        for (int row = top + 1; row <= bottom + 1; row++)
        {
            bool past = row > bottom;
            int left = 1, right = 0;
            if (!past) Outer.TryGet(row, out left, out right);

            if (!past && left == runLeft && right == runRight) continue;

            if (runLeft > runRight)
            {
                Fill(screen.X0, runTop, screen.X1, row - 1, colorIndex, colorOpaqueness);
            }
            else
            {
                Fill(screen.X0, runTop, Math.Min(screen.X1, runLeft - 1), row - 1, colorIndex, colorOpaqueness);
                Fill(Math.Max(screen.X0, runRight + 1), runTop, screen.X1, row - 1, colorIndex, colorOpaqueness);
            }

            runTop = row;
            runLeft = left;
            runRight = right;
        }
    }

    /// <summary>Stipples the annulus between the outer oval and the hole, run-merged the same way.</summary>
    private static void DitherRing(int spriteId, int colors, int top, int bottom,
        int gridX, int gridY, in Bounds screen, float colorOpaqueness)
    {
        int runTop = top;
        Outer.TryGet(top, out int runOuterLeft, out int runOuterRight);
        Inner.TryGet(top, out int runInnerLeft, out int runInnerRight);

        for (int row = top + 1; row <= bottom + 1; row++)
        {
            bool past = row > bottom;
            int outerLeft = 1, outerRight = 0, innerLeft = 1, innerRight = 0;
            if (!past)
            {
                Outer.TryGet(row, out outerLeft, out outerRight);
                Inner.TryGet(row, out innerLeft, out innerRight);
            }

            if (!past && outerLeft == runOuterLeft && outerRight == runOuterRight
                && innerLeft == runInnerLeft && innerRight == runInnerRight) continue;

            if (runOuterLeft <= runOuterRight)
            {
                if (runInnerLeft > runInnerRight)
                {
                    Dither(spriteId, colors, runOuterLeft, runTop, runOuterRight, row - 1,
                        gridX, gridY, screen, colorOpaqueness);
                }
                else
                {
                    Dither(spriteId, colors, runOuterLeft, runTop, Math.Min(runOuterRight, runInnerLeft - 1), row - 1,
                        gridX, gridY, screen, colorOpaqueness);
                    Dither(spriteId, colors, Math.Max(runOuterLeft, runInnerRight + 1), runTop, runOuterRight, row - 1,
                        gridX, gridY, screen, colorOpaqueness);
                }
            }

            runTop = row;
            runOuterLeft = outerLeft;
            runOuterRight = outerRight;
            runInnerLeft = innerLeft;
            runInnerRight = innerRight;
        }
    }

    /// <summary>
    /// Tiles the dither sprite over an inclusive block. The lattice is anchored on the hole's
    /// centre rather than on the block, so every block cuts the same grid: neighbouring tiles
    /// meet exactly once, with no seam and no doubled pixel, and the two sides of the ring
    /// mirror each other. Tiles hanging over the block are trimmed by taking a sub-rect of the
    /// sprite instead of the whole tile, which keeps the pattern's phase across the cut.
    /// </summary>
    private static void Dither(int spriteId, int colors, int x0, int y0, int x1, int y1,
        int gridX, int gridY, in Bounds screen, float colorOpaqueness)
    {
        if (x0 < screen.X0) x0 = screen.X0;
        if (y0 < screen.Y0) y0 = screen.Y0;
        if (x1 > screen.X1) x1 = screen.X1;
        if (y1 > screen.Y1) y1 = screen.Y1;
        if (x0 > x1 || y0 > y1) return;

        const int tile = Constants.GameDataSizes.TileSize;
        int sheetX = (spriteId % Constants.GameDataSizes.SpriteSheetColumns) * tile;
        int sheetY = (spriteId / Constants.GameDataSizes.SpriteSheetColumns) * tile;

        int firstX = gridX + FloorDiv(x0 - gridX, tile) * tile;
        int firstY = gridY + FloorDiv(y0 - gridY, tile) * tile;

        for (int ty = firstY; ty <= y1; ty += tile)
        {
            int dy0 = Math.Max(ty, y0);
            int dy1 = Math.Min(ty + tile - 1, y1);

            for (int tx = firstX; tx <= x1; tx += tile)
            {
                int dx0 = Math.Max(tx, x0);
                int dx1 = Math.Min(tx + tile - 1, x1);

                Mono8API.SpriteSheet.DrawSubColors(
                    sheetX + (dx0 - tx), sheetY + (dy0 - ty),
                    dx1 - dx0 + 1, dy1 - dy0 + 1,
                    dx0, dy0,
                    DitherColors, colors, colorOpaqueness);
            }
        }
    }

    private static void Fill(int x0, int y0, int x1, int y1, int colorIndex, float colorOpaqueness)
    {
        if (x1 < x0 || y1 < y0) return;

        Mono8Game.SpriteBatch.DrawRectFill(x0, y0, x1 - x0 + 1, y1 - y0 + 1, colorIndex, colorOpaqueness);
    }

    private static int BandFor(int ditherSpriteId) =>
        ditherSpriteId > 0 && ditherSpriteId <= Constants.GameDataSizes.MaxSpriteIndex ? Band : 0;

    // The mask covers the viewport, and every draw here goes through the camera transform,
    // so the screen rect has to be expressed in the camera's own space.
    private static Bounds ScreenBounds()
    {
        int x = (int)Math.Round(Camera2D.Position.X);
        int y = (int)Math.Round(Camera2D.Position.Y);

        return new Bounds(x, y,
            x + Constants.Screen.ResolutionX - 1,
            y + Constants.Screen.ResolutionY - 1);
    }

    private static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((b - 1 - a) / b);
}
