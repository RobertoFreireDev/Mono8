namespace mono8.game;

/// <summary>
/// The map read as terrain — sprite flags turned into the questions a moving body asks of it. No
/// state, and every coordinate is map-sheet pixels, the same space <c>mcol</c> works in.
///
/// Two flags, both authored in the sprite editor. Solid is what nothing passes through. Stair marks
/// a climbable column, and the tiles capping one carry both: floor to anyone walking over them, a
/// doorway to anyone on the stair. <see cref="Blocked"/> is that second reading.
/// </summary>
internal static class Terrain
{
    /// <summary>Side of one map cell in pixels — what turns a pixel coordinate into a cell one.</summary>
    public const int TileSize = 8;

    /// <summary>Sprite flag marking solid ground; set it on every tile a body must not pass through.</summary>
    public const int FlagSolid = 1;

    /// <summary>Sprite flag marking a stair. A tile carrying it and <see cref="FlagSolid"/> caps one.</summary>
    public const int FlagStair = 0;

    public static bool Solid(int x, int y, int w, int h)
    {
        return YourGame.API.mcol(x, y, w, h, FlagSolid);
    }

    /// <summary>
    /// Solid that a body on a stair is not stopped by — the floor a descent ends on, the ceiling a
    /// climb stops under — with the stair's own cap tiles read as open, since passing through them
    /// is the only way off the top.
    /// </summary>
    public static bool Blocked(int x, int y, int w, int h)
    {
        if (w <= 0 || h <= 0)
        {
            return false;
        }

        var api = YourGame.API;
        int cx1 = CellOf(x + w - 1);
        int cy1 = CellOf(y + h - 1);
        int cx0 = CellOf(x);

        for (int cy = CellOf(y); cy <= cy1; cy++)
        {
            for (int cx = cx0; cx <= cx1; cx++)
            {
                int spriteId = api.mget(cx, cy);
                if (spriteId != 0 && api.fget(spriteId, FlagSolid) && !api.fget(spriteId, FlagStair))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// The cell column of the stair the rect stands in — the nearest one when the body straddles
    /// two — or false when it meets no stair at all.
    /// </summary>
    public static bool StairColumn(int x, int y, int w, int h, out int cellX)
    {
        cellX = 0;

        if (w <= 0 || h <= 0)
        {
            return false;
        }

        var api = YourGame.API;
        int mid = x + w / 2;
        int best = int.MaxValue;
        int cx1 = CellOf(x + w - 1);
        int cy1 = CellOf(y + h - 1);
        int cx0 = CellOf(x);

        for (int cy = CellOf(y); cy <= cy1; cy++)
        {
            for (int cx = cx0; cx <= cx1; cx++)
            {
                int spriteId = api.mget(cx, cy);
                if (spriteId == 0 || !api.fget(spriteId, FlagStair))
                {
                    continue;
                }

                int distance = (int)api.abs(cx * TileSize + TileSize / 2 - mid);
                if (distance < best)
                {
                    best = distance;
                    cellX = cx;
                }
            }
        }

        return best != int.MaxValue;
    }

    /// <summary>The x a rect <paramref name="w"/> wide needs to sit centred in column <paramref name="cellX"/>.</summary>
    public static int CenterOnColumn(int cellX, int w)
    {
        return cellX * TileSize + (TileSize - w) / 2;
    }

    // Floored, not truncated: integer division would fold the pixels just off the left edge of the
    // sheet onto column 0, reporting terrain the body is nowhere near.
    private static int CellOf(int pixel)
    {
        return (int)YourGame.API.flr(pixel / (double)TileSize);
    }
}
