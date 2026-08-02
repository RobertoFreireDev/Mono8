namespace mono8.game;

/// <summary>
/// The bag. One club is selected at a time and Y (V) cycles to the next; what the club changes is
/// how the <see cref="Ball"/> leaves the club face — the launch angle it adds, the share of the
/// authored distance it keeps, and how hard it has to be swung before the ball leaves the ground at
/// all. The driver adds nothing to any of the three, so it is the shot BALL / HITX and HITY were
/// authored for.
///
/// The bag itself is authored: CLUBS / ORDER lists the objects to load, in the order the button
/// walks them, so adding a club is a matter of authoring one and naming it in the list. An
/// unauthored bag leaves the ball hitting exactly as it did before there were clubs.
///
/// The label is HUD — screen pixels, sitting over the <see cref="Meter"/> bar, and drawn whether or
/// not the bar is up so the club can be picked while walking.
/// </summary>
internal static class Club
{
    private const string ClubGroup = "CLUBS";
    private const string OrderObject = "ORDER";
    private const string FieldOrder = "LIST";

    private const string LabelGroup = "HUD";
    private const string LabelObject = "CLUB";

    // Y (V). The last face button free — A jumps, B swings, X backs out of a swing.
    private const int BtnClub = 7;

    // The authoring limit on one array is what the bag can hold.
    private const int MaxClubs = 16;

    // The engine's font is 7 px tall, which is what the label is lifted off the bar by.
    private const int FontHeight = 7;

    // Straight up. Past it the shot would come back over the player's shoulder.
    private const double MaxAngle = 0.25;

    private static readonly string[] Names = new string[MaxClubs];
    private static readonly float[] Angles = new float[MaxClubs];
    private static readonly float[] Distances = new float[MaxClubs];
    private static readonly float[] GroundPowers = new float[MaxClubs];

    private static int Loaded;
    private static int Index;

    private static int LabelGap;

    /// <summary>Turns added to the launch angle BALL / HITX and HITY work out to.</summary>
    public static float Angle => Loaded > 0 ? Angles[Index] : 0f;

    /// <summary>Share of the authored launch speed this club keeps.</summary>
    public static float Distance => Loaded > 0 ? Distances[Index] : 1f;

    /// <summary>At or under this power the shot stays flat, however steep the club is otherwise —
    /// what makes a putter roll rather than loft.</summary>
    public static float GroundPower => Loaded > 0 ? GroundPowers[Index] : 0f;

    /// <summary>For the debug overlay, and what the label prints.</summary>
    public static string Name => Loaded > 0 ? Names[Index] : string.Empty;

    public static void Init()
    {
        var api = YourGame.API;

        Loaded = 0;
        Index = 0;
        LabelGap = 0;

        // Re-read every Init: Ctrl+S in the JSON editor rebuilds the data without a restart.
        var order = api.gjson(ClubGroup, OrderObject);
        if (order != null)
        {
            int listed = order.Count(FieldOrder);
            for (int i = 0; i < listed && Loaded < MaxClubs; i++)
            {
                Load(order.GetStr(FieldOrder, i));
            }
        }

        var label = api.gjson(LabelGroup, LabelObject);
        if (label != null)
        {
            LabelGap = label.GetInt("GAP");
        }
    }

    public static void Update()
    {
        // Not mid-swing: the club that was addressed is the club that hits.
        if (Loaded > 1 && !Swing.Active && YourGame.API.btnp(BtnClub))
        {
            Index = (Index + 1) % Loaded;
        }
    }

    public static void Draw()
    {
        string name = Name;
        if (name.Length == 0)
        {
            return;
        }

        // Outlined, since the bar is only up during a swing and the label is over the room the rest
        // of the time.
        Hud.PrintOutlined(name, Meter.LeftX, Meter.TopY - LabelGap - FontHeight,
            Constants.Colors.White);
    }

    // A club named in ORDER but never authored is skipped rather than loaded as a zero-distance one
    // that cannot move the ball.
    private static void Load(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        var club = YourGame.API.gjson(ClubGroup, name);
        if (club == null)
        {
            return;
        }

        // Authored in degrees, which is how a lofted club is talked about; turns are the engine's.
        Names[Loaded] = club.GetStr("NAME", 0, name);
        Angles[Loaded] = (float)(club.GetDec("ANGLE") / 360d);
        Distances[Loaded] = (float)club.GetDec("DIST", 0, 1d);
        GroundPowers[Loaded] = (float)club.GetDec("GNDPWR");
        Loaded++;
    }

    /// <summary>The club's shot as an angle in turns, clamped to the quarter turn between flat and
    /// straight up. <paramref name="baseAngle"/> is what the authored hit speeds work out to.</summary>
    public static float AngleFrom(double baseAngle, float power)
    {
        // A putt only leaves the ground when it is struck harder than the club's threshold.
        if (power <= GroundPower)
        {
            return 0f;
        }

        return (float)YourGame.API.mid(0d, baseAngle + Angle, MaxAngle);
    }
}
