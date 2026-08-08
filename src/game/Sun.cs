namespace mono8.game;

/// <summary>
/// The room's sun: placed by the <see cref="Room"/> as SUN, drawn between the backdrop and the
/// room's own cells so it sits in the sky and the terrain passes in front of it. A room that
/// authors no SUN has none — and no sun is also no shadow under the player, which is the only
/// thing anything else asks it.
/// </summary>
internal static class Sun
{
    // One fixed block on the sheet rather than a per-room choice: every room's sun is the same sun.
    private const int Spr = 1;
    private const int Tiles = 2;

    /// <summary>
    /// Where SUN puts it, taken exactly as authored — <see cref="X"/> is also what says which side
    /// of a body the light comes from, and so which way the shadow under it leans.
    /// </summary>
    public static bool Present;
    public static int X;
    public static int Y;

    public static void Init(Room room)
    {
        Present = room.HasSun;
        X = room.SunX;
        Y = room.SunY;
    }

    public static void Draw()
    {
        if (Present)
        {
            YourGame.API.spr(Spr, X, Y, Tiles, Tiles);
        }
    }
}
