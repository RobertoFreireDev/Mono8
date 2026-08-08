namespace mono8.game;

/// <summary>
/// The sun, hung by the clock rather than by the room: <see cref="Room"/> only says where the
/// screen is on the map sheet, and the hour of the day says where across that screen the sun sits.
/// It is drawn between the backdrop and the room's own cells so it stays in the sky and the terrain
/// passes in front of it.
///
/// Outside daylight there is no sun — and no sun is also no shadow under the player, which is the
/// only other thing anything asks it.
/// </summary>
internal static class Sun
{
    // One fixed block on the sheet rather than a per-room choice: every room's sun is the same sun.
    private const int Spr = 1;
    private const int Tiles = 2;

    // Two tiles of clearance on each side and above, so the sun never touches an edge of the screen
    // at either end of the day.
    private const int Margin = 2 * Terrain.TileSize;

    // Daylight, in whole hours off the player's own clock: at DawnHour the sun is against the left
    // margin, at DuskHour against the right one, and outside the two there is none at all.
    private const int DawnHour = 6;
    private const int DuskHour = 18;

    // stat ids for the wall clock — 4 is the hour.
    private const int StatHour = 4;

    // The glow: three translucent discs over the sprite, drawn widest last so they layer into a
    // halo rather than one flat disc. These radii are what midday draws — the rest of the day scales
    // them down with the sun's height, so dawn and dusk have none at all.
    private static readonly int[] GlowRadius = { 14, 16, 18 };
    private static readonly int[] GlowColor =
    {
        Constants.Colors.BrightOrange,
        Constants.Colors.Orange,
        Constants.Colors.Yellow,
    };
    private const float GlowOpacity = 0.2f;

    /// <summary>
    /// Whether the sun is up, and where it hangs in map-sheet pixels. <see cref="X"/> is also what
    /// says which side of a body the light comes from, and so which way the shadow under it leans —
    /// which is why it is kept in world space rather than as the screen position it is derived from.
    /// </summary>
    public static bool Present;
    public static int X;
    public static int Y;

    // How high the sun is, 0 at either end of the day and 1 at the hour midway between them. The
    // sprite itself does not move up the sky — this is only what the glow is sized off.
    private static float Height;

    public static void Init(Room room)
    {
        int hour = YourGame.API.stat(StatHour);

        Present = hour >= DawnHour && hour <= DuskHour;

        // Left margin to right margin over the daylight hours, measured from the room's corner
        // because that corner is where the screen starts. The sprite is placed by its own left edge,
        // so the right end of the sweep stops a sprite width short of the margin.
        //
        // Computed whether or not the sun is up: nothing draws while it is down, and the shadow that
        // reads X is off for the same reason, so the value only has to be somewhere sane.
        int span = Constants.Screen.ResolutionX - 2 * Margin - Tiles * Terrain.TileSize;
        int hoursIn = (int)YourGame.API.mid(0, hour - DawnHour, DuskHour - DawnHour);

        X = room.OriginX + Margin + span * hoursIn / (DuskHour - DawnHour);
        Y = room.OriginY + Margin;

        // Distance from the middle of the day, as a fraction of half of it — so the two ends are 0
        // and the hour between them is 1. Off the clamp above rather than the raw hour, so a sun
        // that is down cannot come out negative.
        int halfDay = (DuskHour - DawnHour) / 2;
        Height = 1f - (float)YourGame.API.abs(hoursIn - halfDay) / halfDay;
    }

    public static void Draw()
    {
        if (!Present)
        {
            return;
        }

        var api = YourGame.API;

        api.spr(Spr, X, Y, Tiles, Tiles);

        // The glow is centred on the sprite; X, Y are its corner.
        int centerX = X + Tiles * Terrain.TileSize / 2;
        int centerY = Y + Tiles * Terrain.TileSize / 2;

        for (int i = 0; i < GlowRadius.Length; i++)
        {
            int radius = (int)(GlowRadius[i] * Height);

            if (radius > Terrain.TileSize * Tiles)
            {
                api.circfill(centerX, centerY, radius, GlowColor[i], GlowOpacity);
            }
        }
    }
}
