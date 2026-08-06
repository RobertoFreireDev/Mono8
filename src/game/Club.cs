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
    private const string FieldSwapSfx = "SFX";

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

    // The sfx a swap picks from, authored on the bag itself — the sound is the bag being rummaged
    // through, not any one club's.
    private static readonly int[] SwapSounds = new int[MaxClubs];
    private static int SwapSoundCount;

    private static int Loaded;
    private static int Index;

    private static int LabelGap;

    // The swap: how long the two labels are on screen together, and how far apart they are at the
    // ends of it. The club that is leaving drops away and the one arriving comes down from above,
    // both of them set back in x at their extremes so the pair reads as one face turning rather
    // than two labels sliding past each other.
    private static float SwapSeconds;
    private static int SwapX;
    private static int SwapY;

    // The club being left behind, held only for as long as it is still on screen.
    private static string Outgoing;
    private static float SwapLeft;

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
        SwapSoundCount = 0;
        LabelGap = 0;
        SwapSeconds = 0f;
        SwapX = 0;
        SwapY = 0;
        Outgoing = string.Empty;
        SwapLeft = 0f;

        // Re-read every Init: Ctrl+S in the JSON editor rebuilds the data without a restart.
        var order = api.gjson(ClubGroup, OrderObject);
        if (order != null)
        {
            int listed = order.Count(FieldOrder);
            for (int i = 0; i < listed && Loaded < MaxClubs; i++)
            {
                Load(order.GetStr(FieldOrder, i));
            }

            int sounds = order.Count(FieldSwapSfx);
            for (int i = 0; i < sounds && SwapSoundCount < MaxClubs; i++)
            {
                // A negative id stops channels rather than playing anything, so an unauthored or
                // wrong-typed entry is dropped instead of loaded.
                int id = order.GetInt(FieldSwapSfx, i, -1);
                if (id >= 0)
                {
                    SwapSounds[SwapSoundCount] = id;
                    SwapSoundCount++;
                }
            }
        }

        var label = api.gjson(LabelGroup, LabelObject);
        if (label != null)
        {
            LabelGap = label.GetInt("GAP");
            SwapSeconds = (float)label.GetDec("SWAPSEC");
            SwapX = label.GetInt("SWAPX");
            SwapY = label.GetInt("SWAPY");
        }
    }

    public static void Update(float elapsedSeconds)
    {
        if (SwapLeft > 0f)
        {
            SwapLeft -= elapsedSeconds;
            if (SwapLeft <= 0f)
            {
                SwapLeft = 0f;
                Outgoing = string.Empty;
            }
        }

        // Not mid-swing: the club that was addressed is the club that hits.
        if (Loaded > 1 && !Swing.Active && YourGame.API.btnp(BtnClub))
        {
            // Whatever is showing is what leaves, so a second press part way through a swap picks
            // the turn up from where it got to rather than snapping back.
            Outgoing = Name;
            SwapLeft = SwapSeconds;
            Index = (Index + 1) % Loaded;

            if (SwapSoundCount > 0)
            {
                YourGame.API.sfx(SwapSounds[YourGame.API.rnd(SwapSoundCount)]);
            }
        }
    }

    public static void Draw()
    {
        string name = Name;
        if (name.Length == 0)
        {
            return;
        }

        int x = Meter.LeftX;
        int y = Meter.TopY - LabelGap - FontHeight;

        // How far through the turn: 1 once it is over, which is also what an unauthored SWAPSEC
        // leaves it at, so the label just changes.
        float t = SwapLeft > 0f && SwapSeconds > 0f ? 1f - SwapLeft / SwapSeconds : 1f;

        if (t < 1f && Outgoing.Length > 0)
        {
            Hud.PrintOutlined(Outgoing, x - Offset(SwapX, t), y + Offset(SwapY, t),
                Constants.Colors.White, 1f - t);
        }

        // Outlined, since the bar is only up during a swing and the label is over the room the rest
        // of the time.
        Hud.PrintOutlined(name, x - Offset(SwapX, 1f - t), y - Offset(SwapY, 1f - t),
            Constants.Colors.White, t);
    }

    private static int Offset(int distance, float t)
    {
        return (int)YourGame.API.round(distance * t);
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
