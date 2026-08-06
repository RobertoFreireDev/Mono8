namespace mono8.game;

/// <summary>
/// The strength meter: a value that sweeps 0 → 1 → 0 for as long as the <see cref="Swing"/> holds
/// the club back, and the bar that shows it in the bottom-left corner. The press that swings
/// through reads <see cref="Value"/> once and that is how hard the ball is hit, so the whole shot
/// is a matter of stopping the sweep in the right place.
///
/// The bar is HUD — screen pixels, drawn after everything in the room.
/// </summary>
internal static class Meter
{
    private const string SweepGroup = "SWING";
    private const string SweepObject = "POWER";

    private const string BarGroup = "HUD";
    private const string BarObject = "METER";

    // Fill color per tenth of the bar, weakest shot first. Indexed by (int)(Level * 10).
    private static readonly int[] FillColors =
    {
        Constants.Colors.Red,           // 0-10
        Constants.Colors.DarkCrimson,   // 10-20
        Constants.Colors.BrightOrange,  // 20-30
        Constants.Colors.Yellow,        // 30-40
        Constants.Colors.LightYellow,   // 40-50
        Constants.Colors.LimeGreen,     // 50-60
        Constants.Colors.Green,         // 60-70
        Constants.Colors.MediumGreen,   // 70-80
        Constants.Colors.DarkGreen,     // 80-90
        Constants.Colors.DarkTeal,      // 90-100
    };

    private static float SweepSpeed;    // fraction per second
    private static float Level;
    private static int Direction;
    private static bool Running;

    // Drawn without sweeping while the club is only addressed, so the bar is already in place when
    // the pull starts filling it.
    private static bool Shown;

    // Bar geometry, in screen pixels. The background is the fill area plus the border.
    private static int Margin;
    private static int BarW;
    private static int BarH;
    private static int Border;

    /// <summary>How hard the ball would be hit right now, 0 to 1.</summary>
    public static float Value => Level;

    /// <summary>Top-left of the bar's background in screen pixels, border included — what the
    /// <see cref="Club"/> label lines itself up on.</summary>
    public static int LeftX => Margin;

    public static int TopY => Constants.Screen.ResolutionY - Margin - BarH - Border * 2;

    public static bool Active => Running;

    public static void Init()
    {
        var api = YourGame.API;

        float sweep = 0f;
        Margin = 0;
        BarW = 0;
        BarH = 0;
        Border = 0;

        // Re-read every Init: Ctrl+S in the JSON editor rebuilds the data without a restart.
        var stats = api.gjson(SweepGroup, SweepObject);
        if (stats != null)
        {
            sweep = (float)stats.GetDec("SWEEP");
        }

        var bar = api.gjson(BarGroup, BarObject);
        if (bar != null)
        {
            Margin = bar.GetInt("MARGIN");
            BarW = bar.GetInt("BARW");
            BarH = bar.GetInt("BARH");
            Border = bar.GetInt("BORDER");
        }

        // Out and back is two traversals of the bar, hence the 2.
        SweepSpeed = sweep > 0f ? 2f / sweep : 0f;

        Stop();
    }

    /// <summary>Puts the empty bar on screen without sweeping it. Called when the club comes out.</summary>
    public static void Show()
    {
        Reset(false);
    }

    /// <summary>Starts the sweep from empty. Called when the club goes back.</summary>
    public static void Start()
    {
        Reset(true);
    }

    private static void Reset(bool running)
    {
        Level = 0f;
        Direction = 1;
        Running = running;
        Shown = true;
    }

    public static void Stop()
    {
        Running = false;
        Shown = false;
    }

    public static void Update(float elapsedSeconds)
    {
        if (!Running || SweepSpeed <= 0f)
        {
            return;
        }

        Level += Direction * SweepSpeed * elapsedSeconds;

        // Reflect rather than clamp, so a full bar turns straight back down and the sweep keeps its
        // rate across the bounce.
        if (Level >= 1f)
        {
            Level = 2f - Level;
            Direction = -1;
        }
        else if (Level <= 0f)
        {
            Level = -Level;
            Direction = 1;
        }
    }

    public static void Draw()
    {
        // Skipped when the bar is unauthored, since an empty rect would draw inverted.
        if (!Shown || BarW <= 0 || BarH <= 0)
        {
            return;
        }

        var api = YourGame.API;

        int x0 = LeftX;
        int y0 = TopY;
        int x1 = x0 + BarW + Border * 2 - 1;
        int y1 = y0 + BarH + Border * 2 - 1;

        api.rectfill(x0, y0, x1, y1, Constants.Colors.DarkBlue);

        int filled = (int)api.round(Level * BarW);
        if (filled > 0)
        {
            api.rectfill(x0 + Border, y0 + Border, x0 + Border + filled - 1, y1 - Border, FillColor());
        }
    }

    // A full bar lands on one past the last band — keep it in the top one.
    private static int FillColor()
    {
        int band = (int)YourGame.API.mid(0, (int)(Level * FillColors.Length), FillColors.Length - 1);
        return FillColors[band];
    }
}
