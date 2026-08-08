namespace mono8.game;

/// <summary>
/// The night: the moon, and the wash of dark it comes out in. The other half of the clock the
/// <see cref="Sun"/> is hung by, and the moon crosses the same sky — <see cref="Sun.Margin"/> down
/// from the top, <see cref="Sun.Span"/> across — but on the month rather than the hour, so it moves
/// a little every night instead of over an evening.
///
/// Drawn over everything the game puts down, so the level select falls dark with a room: both are
/// the same outdoors. Nothing else reads it, and it reads nothing but the clock — no room, no state,
/// no <c>Init</c>.
/// </summary>
internal static class Moon
{
    private const int Spr = 129;
    private const int Tiles = 2;

    // The same wall clock the sun is placed by: 3 is the day of the month, 4 the hour.
    private const int StatDay = 3;
    private const int StatHour = 4;

    // The longest month, so the first of any month is against the left margin and the last of a full
    // one against the right. A short month simply stops before it gets there.
    private const int MonthDays = 31;

    // Deep night wraps midnight, so it is the one band read as two halves; the twilights either side
    // of it are dimmed half as far. Every other hour is daylight — no dark, and no moon.
    private const int DeepFromHour = 22;
    private const int DeepToHour = 2;
    private const int DuskFromHour = 18;
    private const int DawnToHour = 6;

    private const float DeepOpacity = 0.4f;
    private const float TwilightOpacity = 0.2f;

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
        int daysIn = (int)YourGame.API.mid(0, YourGame.API.stat(StatDay) - 1, MonthDays - 1);

        YourGame.API.spr(Spr, Sun.Margin + Sun.Span * daysIn / (MonthDays - 1), Sun.Margin, Tiles, Tiles);

        // Over the moon, so it is behind the same dark as everything else rather than a hole in it.
        // rectfill takes the far corner rather than a size.
        YourGame.API.rectfill(0, 0, Constants.Screen.ResolutionX - 1, Constants.Screen.ResolutionY - 1,
            Constants.Colors.Black, dim);
    }
}
