namespace mono8.game;

/// <summary>
/// The moon. The other half of the clock the <see cref="Sun"/> is hung by, and it crosses the same
/// sky — <see cref="Sun.Margin"/> down from the top, <see cref="Sun.Span"/> across — but on the month
/// rather than the hour, so it moves a little every night instead of over an evening.
///
/// Out only while there is a <see cref="Night"/> for it to be out in, which is the one thing it asks
/// anything else. It draws the sprite and nothing more: the dark it comes out in is the
/// <see cref="Night"/>'s, and goes on after it — and after the <see cref="Clouds"/> that pass in
/// front of it — so the moon is under the same dim as everything else rather than a hole in it.
///
/// Its half of DAYCYCLE / NIGHT is SPR and MONTHDAY. The room lends it nothing but its corner, the
/// same as the <see cref="Sun"/> and the <see cref="Clouds"/>.
/// </summary>
internal static class Moon
{
    private const string JsonGroup = "DAYCYCLE";
    private const string JsonObject = "NIGHT";

    private const string FieldSpr = "SPR";
    private const string FieldMonthDays = "MONTHDAY";

    private const int DefaultSpr = 129;

    // The longest month, so the first of any month is against the left margin and the last of a full
    // one against the right. A short month simply stops before it gets there.
    private const int DefaultMonthDays = 31;

    // The same wall clock the sun is placed by: 3 is the day of the month.
    private const int StatDay = 3;

    private static int Spr;
    private static int MonthDays;

    private static int OriginX;
    private static int OriginY;

    /// <summary>
    /// The size of the sky is the <see cref="Sun"/>'s, since <see cref="Sun.Span"/> is measured off
    /// it — a moon of its own size would overrun the line it shares with the sun.
    /// </summary>
    private static int Tiles => Sun.Tiles;

    /// <summary>
    /// <paramref name="room"/> lends nothing but its corner — which screenful of the sheet the sky is
    /// drawn over. Everything else about the moon is the NIGHT object's.
    /// </summary>
    public static void Init(Room room)
    {
        OriginX = room.OriginX;
        OriginY = room.OriginY;

        Spr = DefaultSpr;
        MonthDays = DefaultMonthDays;

        // Re-read every Init: Ctrl+S in the JSON editor rebuilds the data without a restart.
        var data = YourGame.API.gjson(JsonGroup, JsonObject);
        if (data == null)
        {
            return;
        }

        Spr = data.GetInt(FieldSpr, 0, DefaultSpr);
        MonthDays = data.GetInt(FieldMonthDays, 0, DefaultMonthDays);
    }

    public static void Draw()
    {
        // No dark to come out in, no moon. Read through the night rather than off a clock of its own,
        // so the two can never disagree about whether it is night.
        if (Night.Dim <= 0f)
        {
            return;
        }

        // A month of one day or less has nowhere to move the moon along, so it hangs at the margin
        // rather than dividing by zero.
        int lastDay = MonthDays - 1;
        int daysIn = lastDay > 0 ? (int)YourGame.API.mid(0, YourGame.API.stat(StatDay) - 1, lastDay) : 0;
        int acrossSky = lastDay > 0 ? Sun.Span * daysIn / lastDay : 0;

        // The sky is measured in screen pixels, and the call site has the room's camera up — so the
        // room's corner is what turns that into the map-sheet pixels everything in a room is drawn in.
        YourGame.API.spr(Spr, OriginX + Sun.Margin + acrossSky, OriginY + Sun.Margin, Tiles, Tiles);
    }
}
