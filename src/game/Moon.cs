namespace mono8.game;

/// <summary>
/// The night: the moon, and the wash of dark it comes out in. The other half of the clock the
/// <see cref="Sun"/> is hung by, and the moon crosses the same sky — <see cref="Sun.Margin"/> down
/// from the top, <see cref="Sun.Span"/> across — but on the month rather than the hour, so it moves
/// a little every night instead of over an evening.
///
/// Drawn over everything the game puts down, so the level select falls dark with a room: both are
/// the same outdoors. Nothing else reads it, and it reads nothing but the clock and its own object
/// under DAYCYCLE / NIGHT — no room, no state.
/// </summary>
internal static class Moon
{
    private const string JsonGroup = "DAYCYCLE";
    private const string JsonObject = "NIGHT";

    private const string FieldSpr = "SPR";
    private const string FieldMonthDays = "MONTHDAY";
    private const string FieldDeepFrom = "DEEPFROM";
    private const string FieldDeepTo = "DEEPTO";
    private const string FieldDuskFrom = "DUSKFROM";
    private const string FieldDawnTo = "DAWNTO";
    private const string FieldDeepOpacity = "DEEPOPA";
    private const string FieldTwilightOpacity = "TWILOPA";

    private const int DefaultSpr = 129;

    // The longest month, so the first of any month is against the left margin and the last of a full
    // one against the right. A short month simply stops before it gets there.
    private const int DefaultMonthDays = 31;

    // Deep night wraps midnight, so it is the one band read as two halves; the twilights either side
    // of it are dimmed half as far. Every other hour is daylight — no dark, and no moon.
    private const int DefaultDeepFromHour = 22;
    private const int DefaultDeepToHour = 2;
    private const int DefaultDuskFromHour = 18;
    private const int DefaultDawnToHour = 6;

    private const float DefaultDeepOpacity = 0.4f;
    private const float DefaultTwilightOpacity = 0.2f;

    // The same wall clock the sun is placed by: 3 is the day of the month, 4 the hour.
    private const int StatDay = 3;
    private const int StatHour = 4;

    private static int Spr;
    private static int MonthDays;
    private static int DeepFromHour;
    private static int DeepToHour;
    private static int DuskFromHour;
    private static int DawnToHour;
    private static float DeepOpacity;
    private static float TwilightOpacity;

    /// <summary>
    /// The size of the sky is the <see cref="Sun"/>'s, since <see cref="Sun.Span"/> is measured off
    /// it — a moon of its own size would overrun the line it shares with the sun.
    /// </summary>
    private static int Tiles => Sun.Tiles;

    public static void Init()
    {
        Spr = DefaultSpr;
        MonthDays = DefaultMonthDays;
        DeepFromHour = DefaultDeepFromHour;
        DeepToHour = DefaultDeepToHour;
        DuskFromHour = DefaultDuskFromHour;
        DawnToHour = DefaultDawnToHour;
        DeepOpacity = DefaultDeepOpacity;
        TwilightOpacity = DefaultTwilightOpacity;

        // Re-read every Init: Ctrl+S in the JSON editor rebuilds the data without a restart.
        var data = YourGame.API.gjson(JsonGroup, JsonObject);
        if (data == null)
        {
            return;
        }

        Spr = data.GetInt(FieldSpr, 0, DefaultSpr);
        MonthDays = data.GetInt(FieldMonthDays, 0, DefaultMonthDays);
        DeepFromHour = data.GetInt(FieldDeepFrom, 0, DefaultDeepFromHour);
        DeepToHour = data.GetInt(FieldDeepTo, 0, DefaultDeepToHour);
        DuskFromHour = data.GetInt(FieldDuskFrom, 0, DefaultDuskFromHour);
        DawnToHour = data.GetInt(FieldDawnTo, 0, DefaultDawnToHour);
        DeepOpacity = (float)data.GetDec(FieldDeepOpacity, 0, DefaultDeepOpacity);
        TwilightOpacity = (float)data.GetDec(FieldTwilightOpacity, 0, DefaultTwilightOpacity);
    }

    /// <summary>
    /// The clock is read here rather than cached: it costs two <c>stat</c> calls, and a player who
    /// stays on a hole past the turn of an hour watches it fall dark.
    /// </summary>
    public static void Draw()
    {
        int hour = YourGame.API.stat(StatHour);

        float dim;
        if (hour >= DeepFromHour || hour < DeepToHour)
        {
            dim = DeepOpacity;
        }
        else if ((hour >= DuskFromHour && hour < DeepFromHour) || (hour >= DeepToHour && hour < DawnToHour))
        {
            dim = TwilightOpacity;
        }
        else
        {
            return;
        }

        // Screen pixels, both of them: the camera is back at the origin by the time anything gets
        // here, so the sky is measured from the screen's own corner rather than the room's.
        //
        // A month of one day or less has nowhere to move the moon along, so it hangs at the margin
        // rather than dividing by zero.
        int lastDay = MonthDays - 1;
        int daysIn = lastDay > 0 ? (int)YourGame.API.mid(0, YourGame.API.stat(StatDay) - 1, lastDay) : 0;
        int acrossSky = lastDay > 0 ? Sun.Span * daysIn / lastDay : 0;

        YourGame.API.spr(Spr, Sun.Margin + acrossSky, Sun.Margin, Tiles, Tiles);

        // Over the moon, so it is behind the same dark as everything else rather than a hole in it.
        // rectfill takes the far corner rather than a size.
        YourGame.API.rectfill(0, 0, Constants.Screen.ResolutionX - 1, Constants.Screen.ResolutionY - 1,
            Constants.Colors.Black, dim);
    }
}
