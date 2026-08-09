namespace mono8.game;

/// <summary>
/// The cast the daylight hours carry, drawn the way the <see cref="Night"/> is drawn: one flat
/// rectangle over the screenful, in the colour and at the opacity the hour asks for. Two bands, and
/// each is bounded at both ends rather than the two meeting at one hour — warm from DAWNHR to DAWNTO,
/// cool from DUSKFROM to DUSKHR — so the hours between them can be left plain, which is a midday with
/// no cast on it at all. Outside both there is nothing, which is where the night takes over.
///
/// It shares DAYCYCLE / DAY with the <see cref="Sun"/>: the sun owns the sprite, the sky's geometry and
/// its halo, this owns the two inner hours and the two colours, and DAWNHR / DUSKHR are the pair they
/// are both read off — there is one day and it is authored once. DAWNTO and DUSKFROM are named for the
/// <see cref="Night"/>'s, which bound its own twilights the same way.
///
/// Loaded once from <see cref="YourGame.Init"/> for the <see cref="Night"/>'s reason: the hours are the
/// game's and not a level's, and the <see cref="LevelSelect"/> draws the same cast over its previews
/// while running no room to load it. That is also why it re-reads DAWNHR and DUSKHR itself rather than
/// borrowing them from the sun, which is loaded per room entry.
/// </summary>
internal static class Daylight
{
    private const string JsonGroup = "DAYCYCLE";
    private const string JsonObject = "DAY";

    private const string FieldDawn = "DAWNHR";
    private const string FieldDawnTo = "DAWNTO";
    private const string FieldDuskFrom = "DUSKFROM";
    private const string FieldDusk = "DUSKHR";
    private const string FieldDawnColor = "DAWNCOL";
    private const string FieldDawnOpacity = "DAWNOPA";
    private const string FieldDuskColor = "DUSKCOL";
    private const string FieldDuskOpacity = "DUSKOPA";

    // The outer two are the sun's own hours, the inner two are where each cast lets go and picks up.
    // Left apart by default, so the middle of the day is the one stretch with no colour over it.
    private const int DefaultDawnHour = 6;
    private const int DefaultDawnToHour = 10;
    private const int DefaultDuskFromHour = 12;
    private const int DefaultDuskHour = 15;

    private const int DefaultDawnColor = Constants.Colors.Orange;
    private const int DefaultDuskColor = Constants.Colors.DarkPurple;

    private const float DefaultDawnOpacity = 0.2f;
    private const float DefaultDuskOpacity = 0.2f;

    // The same wall clock the sun is placed by: 4 is the hour.
    private const int StatHour = 4;

    private static int DawnHour;
    private static int DawnToHour;
    private static int DuskFromHour;
    private static int DuskHour;
    private static int DawnColor;
    private static int DuskColor;
    private static float DawnOpacity;
    private static float DuskOpacity;

    /// <summary>
    /// What this hour casts, and an opacity of 0 for an hour that is not daylight at all. Read off the
    /// clock on every access, like <see cref="Night.Dim"/>, so a player who stays on a hole past the
    /// turn of an hour watches the morning go over into the afternoon.
    ///
    /// The bands take their first hour and let go of their last, which is how the night's are read too —
    /// so dawn picks up on the hour the night's DAWNTO drops and no hour is cast twice. The two are read
    /// in order, so a pair of hours authored to overlap is the morning's rather than an error.
    /// </summary>
    public static (int Color, float Opacity) Tint
    {
        get
        {
            int hour = YourGame.API.stat(StatHour);

            if (hour >= DawnHour && hour < DawnToHour)
            {
                return (DawnColor, DawnOpacity);
            }

            if (hour >= DuskFromHour && hour < DuskHour)
            {
                return (DuskColor, DuskOpacity);
            }

            return (DawnColor, 0f);
        }
    }

    /// <summary>
    /// The hours and what each of them casts. Called once, from <see cref="YourGame.Init"/>: a room entry
    /// is not what makes it morning. A retune in the JSON editor lands on the next Ctrl+R rather than on
    /// the Ctrl+S — and the sun, which re-reads the same object per room entry, will have moved to the
    /// new hours before this has, until the restart.
    /// </summary>
    public static void Init()
    {
        DawnHour = DefaultDawnHour;
        DawnToHour = DefaultDawnToHour;
        DuskFromHour = DefaultDuskFromHour;
        DuskHour = DefaultDuskHour;
        DawnColor = DefaultDawnColor;
        DuskColor = DefaultDuskColor;
        DawnOpacity = DefaultDawnOpacity;
        DuskOpacity = DefaultDuskOpacity;

        // Re-read every Init: Ctrl+S in the JSON editor rebuilds the data without a restart.
        var data = YourGame.API.gjson(JsonGroup, JsonObject);
        if (data != null)
        {
            DawnHour = data.GetInt(FieldDawn, 0, DefaultDawnHour);
            DawnToHour = data.GetInt(FieldDawnTo, 0, DefaultDawnToHour);
            DuskFromHour = data.GetInt(FieldDuskFrom, 0, DefaultDuskFromHour);
            DuskHour = data.GetInt(FieldDusk, 0, DefaultDuskHour);
            DawnColor = data.GetInt(FieldDawnColor, 0, DefaultDawnColor);
            DuskColor = data.GetInt(FieldDuskColor, 0, DefaultDuskColor);
            DawnOpacity = (float)data.GetDec(FieldDawnOpacity, 0, DefaultDawnOpacity);
            DuskOpacity = (float)data.GetDec(FieldDuskOpacity, 0, DefaultDuskOpacity);
        }

        // Both inner hours pinned inside the day: an edge authored outside it would put a cast on hours
        // the night already owns, and a band that ends before it starts is simply an hour short — no
        // hour falls in it, which is a colour switched off rather than an error.
        DawnToHour = (int)YourGame.API.mid(DawnHour, DawnToHour, DuskHour);
        DuskFromHour = (int)YourGame.API.mid(DawnHour, DuskFromHour, DuskHour);
    }

    /// <summary>
    /// One screenful of the hour's colour from <paramref name="originX"/>, <paramref name="originY"/> —
    /// the corner in whatever space the caller's camera is up in, exactly as <see cref="Night.Draw"/>
    /// takes it. A room hands its own corner, anything drawn straight on the screen hands (0, 0).
    /// </summary>
    public static void Draw(int originX, int originY)
    {
        var tint = Tint;
        if (tint.Opacity <= 0f)
        {
            return;
        }

        // rectfill takes the far corner rather than a size.
        YourGame.API.rectfill(originX, originY,
            originX + Constants.Screen.ResolutionX - 1, originY + Constants.Screen.ResolutionY - 1,
            tint.Color, tint.Opacity);
    }
}
