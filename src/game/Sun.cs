namespace mono8.game;

/// <summary>
/// The sun, hung by the clock rather than by the room: <see cref="Room"/> only says where the
/// screen is on the map sheet, and the hour of the day says where across that screen the sun sits.
/// It is drawn between the backdrop and the room's own cells so it stays in the sky and the terrain
/// passes in front of it.
///
/// Outside daylight there is no sun — and no sun is also no shadow under the player, which is the
/// only other thing anything asks it.
///
/// Its half of DAYCYCLE / DAY is the sprite, its halo and the sky itself — <see cref="Margin"/>,
/// <see cref="Tiles"/> and the <see cref="Span"/> measured off the two — since the <see cref="Moon"/>
/// crosses the same line and there is only one sky to author. The other half is the
/// <see cref="Daylight"/>'s: the two share DAWNHR and DUSKHR, which is what the object is named for.
/// </summary>
internal static class Sun
{
    private const string JsonGroup = "DAYCYCLE";
    private const string JsonObject = "DAY";

    private const string FieldSpr = "SPR";
    private const string FieldTiles = "TILES";
    private const string FieldMargin = "MARGIN";
    private const string FieldDawn = "DAWNHR";
    private const string FieldDusk = "DUSKHR";
    private const string FieldGlowRadius = "GLOWRAD";
    private const string FieldGlowColor = "GLOWCOL";
    private const string FieldGlowOpacity = "GLOWOPA";

    // One fixed block on the sheet rather than a per-room choice: every room's sun is the same sun.
    private const int DefaultSpr = 1;
    private const int DefaultTiles = 2;

    // Two tiles of clearance on each side and above, so the sun never touches an edge of the screen
    // at either end of the day.
    private const int DefaultMargin = 2 * Terrain.TileSize;

    // Daylight, in whole hours off the player's own clock: at the dawn hour the sun is against the
    // left margin, at the dusk hour against the right one, and outside the two there is none at all.
    private const int DefaultDawnHour = 6;
    private const int DefaultDuskHour = 18;

    private const float DefaultGlowOpacity = 0.2f;

    // The glow: translucent discs over the sprite, drawn widest last so they layer into a halo
    // rather than one flat disc. These radii are what midday draws — the rest of the day scales them
    // down with the sun's height, so dawn and dusk have none at all.
    private static readonly int[] DefaultGlowRadius = { 16, 20, 24 };
    private static readonly int[] DefaultGlowColor =
    {
        Constants.Colors.BrightOrange,
        Constants.Colors.Orange,
        Constants.Colors.Yellow,
    };

    // What a disc authored with no colour beside it takes, rather than dropping the ring entirely.
    private const int FallbackGlowColor = Constants.Colors.Yellow;

    // The authoring limit on one array.
    private const int MaxGlow = 16;

    // stat ids for the wall clock — 4 is the hour.
    private const int StatHour = 4;

    /// <summary>
    /// Whether the sun is up, and where it hangs in map-sheet pixels. <see cref="X"/> is also what
    /// says which side of a body the light comes from, and so which way the shadow under it leans —
    /// which is why it is kept in world space rather than as the screen position it is derived from.
    /// </summary>
    public static bool Present;
    public static int X;
    public static int Y;

    /// <summary>
    /// The sky both bodies cross, in screen pixels: <see cref="Margin"/> in from the left and down
    /// from the top, <see cref="Span"/> across. Public because the <see cref="Moon"/> hangs off the
    /// same three — there is one sky, and the two bodies cross it on the same line.
    /// </summary>
    public static int Margin;
    public static int Tiles;

    /// <summary>
    /// How far a body travels across that sky. Margin to margin, less its own width — a 2x2 sprite
    /// is placed by its left edge, so the far end has to stop a sprite short of the margin rather
    /// than on it. Derived, never authored.
    /// </summary>
    public static int Span;

    private static int Spr;
    private static int DawnHour;
    private static int DuskHour;

    private static readonly int[] GlowRadius = new int[MaxGlow];
    private static readonly int[] GlowColor = new int[MaxGlow];
    private static int GlowCount;
    private static float GlowOpacity;

    // How high the sun is, 0 at either end of the day and 1 at the hour midway between them. The
    // sprite itself does not move up the sky — this is only what the glow is sized off.
    private static float Height;

    public static void Init(Room room)
    {
        Load();

        int hour = YourGame.API.stat(StatHour);

        // A dusk authored at or before dawn is a day with no hours in it, which would divide by zero
        // below — it is read as an overcast day rather than as an error.
        int daylight = DuskHour - DawnHour;

        Present = daylight > 0 && hour >= DawnHour && hour <= DuskHour;

        // Along the sky over the daylight hours, measured from the room's corner because that corner
        // is where the screen starts.
        //
        // Computed whether or not the sun is up: nothing draws while it is down, and the shadow that
        // reads X is off for the same reason, so the value only has to be somewhere sane.
        int hoursIn = daylight > 0 ? (int)YourGame.API.mid(0, hour - DawnHour, daylight) : 0;

        X = room.OriginX + Margin + (daylight > 0 ? Span * hoursIn / daylight : 0);
        Y = room.OriginY + Margin;

        // Distance from the middle of the day, as a fraction of half of it — so the two ends are 0
        // and the hour between them is 1. Off the clamp above rather than the raw hour, so a sun
        // that is down cannot come out negative.
        int halfDay = daylight / 2;
        Height = halfDay > 0 ? 1f - (float)YourGame.API.abs(hoursIn - halfDay) / halfDay : 1f;
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

        for (int i = 0; i < GlowCount; i++)
        {
            int radius = (int)(GlowRadius[i] * Height);

            if (radius > Terrain.TileSize)
            {
                api.circfill(centerX, centerY, radius, GlowColor[i], GlowOpacity);
            }
        }
    }

    private static void Load()
    {
        Spr = DefaultSpr;
        Tiles = DefaultTiles;
        Margin = DefaultMargin;
        DawnHour = DefaultDawnHour;
        DuskHour = DefaultDuskHour;
        GlowOpacity = DefaultGlowOpacity;
        GlowCount = 0;

        // Re-read every Init: Ctrl+S in the JSON editor rebuilds the data without a restart.
        var data = YourGame.API.gjson(JsonGroup, JsonObject);
        if (data != null)
        {
            Spr = data.GetInt(FieldSpr, 0, DefaultSpr);
            Tiles = data.GetInt(FieldTiles, 0, DefaultTiles);
            Margin = data.GetInt(FieldMargin, 0, DefaultMargin);
            DawnHour = data.GetInt(FieldDawn, 0, DefaultDawnHour);
            DuskHour = data.GetInt(FieldDusk, 0, DefaultDuskHour);
            GlowOpacity = (float)data.GetDec(FieldGlowOpacity, 0, DefaultGlowOpacity);

            LoadGlow(data);
        }

        // A sun of no size is a sun that cannot be seen, and a Span measured off it would be wider
        // than the sky. One tile is the floor rather than the authored 0.
        if (Tiles < 1)
        {
            Tiles = 1;
        }

        if (GlowCount == 0)
        {
            // An unauthored halo is the code's, not none: the discs are the only thing that says the
            // sun is a light rather than a sticker.
            for (int i = 0; i < DefaultGlowRadius.Length; i++)
            {
                GlowRadius[i] = DefaultGlowRadius[i];
                GlowColor[i] = DefaultGlowColor[i];
            }

            GlowCount = DefaultGlowRadius.Length;
        }

        Span = Constants.Screen.ResolutionX - 2 * Margin - Tiles * Terrain.TileSize;

        // A margin wide enough to close the sky leaves both bodies pinned at it rather than swinging
        // back across it, which a negative Span would do.
        if (Span < 0)
        {
            Span = 0;
        }
    }

    private static void LoadGlow(Mono8JsonObject data)
    {
        int listed = data.Count(FieldGlowRadius);

        for (int i = 0; i < listed && GlowCount < MaxGlow; i++)
        {
            // A disc of nothing is dropped rather than loaded: circfill throws on a negative radius,
            // and the scaling below zero would take an authored 0 there on most of the day.
            int radius = data.GetInt(FieldGlowRadius, i, 0);
            if (radius <= 0)
            {
                continue;
            }

            GlowRadius[GlowCount] = radius;
            GlowColor[GlowCount] = data.GetInt(FieldGlowColor, i, FallbackGlowColor);
            GlowCount++;
        }
    }
}
