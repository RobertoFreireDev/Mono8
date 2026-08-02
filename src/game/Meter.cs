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
    private const string StatsGroup = "SWING";
    private const string StatsObject = "POWER";

    // Seconds for one full sweep out and back. Fallback until SWING / POWER is authored.
    private const float DefaultSweepSeconds = 1.2f;

    // Bar geometry, in screen pixels. The background is the fill area plus a one-pixel border.
    private const int Margin = 6;
    private const int BarW = 64;
    private const int BarH = 2;
    private const int Border = 1;

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

    /// <summary>How hard the ball would be hit right now, 0 to 1.</summary>
    public static float Value => Level;

    public static bool Active => Running;

    public static void Init()
    {
        float sweep = DefaultSweepSeconds;

        // Re-read every Init: Ctrl+S in the JSON editor rebuilds the data without a restart.
        var stats = YourGame.API.gjson(StatsGroup, StatsObject);
        if (stats != null)
        {
            sweep = (float)stats.GetDec("SWEEP", 0, DefaultSweepSeconds);
        }

        // Out and back is two traversals of the bar, hence the 2.
        SweepSpeed = sweep > 0f ? 2f / sweep : 0f;

        Stop();
    }

    /// <summary>Starts the sweep from empty. Called when the club goes back.</summary>
    public static void Start()
    {
        Level = 0f;
        Direction = 1;
        Running = true;
    }

    public static void Stop()
    {
        Running = false;
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
        if (!Running)
        {
            return;
        }

        var api = YourGame.API;

        int x0 = Margin;
        int y0 = Constants.Screen.ResolutionY - Margin - BarH - Border * 2;
        int x1 = x0 + BarW + Border * 2 - 1;
        int y1 = y0 + BarH + Border * 2 - 1;

        api.rectfill(x0, y0, x1, y1, Constants.Colors.DarkBlue);

        int filled = (int)api.round(Level * BarW);
        if (filled > 0)
        {
            // A full bar lands on 10, which is past the last band — keep it in the top one.
            int band = (int)api.mid(0, (int)(Level * FillColors.Length), FillColors.Length - 1);

            api.rectfill(x0 + Border, y0 + Border, x0 + Border + filled - 1, y1 - Border,
                FillColors[band]);
        }
    }
}
