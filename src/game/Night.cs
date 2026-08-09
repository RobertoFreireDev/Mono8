namespace mono8.game;

/// <summary>
/// The wash of dark the night falls in: one flat rectangle over the room's screenful at the opacity
/// the hour asks for, and the hours themselves. Nothing is placed in it — the body in the night sky
/// is the <see cref="Moon"/>, which draws before this and is dimmed by it like everything else.
///
/// Drawn last of the room's own layers, over the terrain, the bodies and the <see cref="Clouds"/>
/// alike, so a cloud at midnight is as dark as the ground under it — and before the camera goes back,
/// so the HUD stays out of it. Its half of DAYCYCLE / NIGHT is the bands and their opacities; beyond
/// them it reads the clock and the corner it is handed, and nothing else.
///
/// The hours belong to the game rather than to a room — the night does not start over because a level
/// did — so unlike the other three sky bodies this is loaded once, from <see cref="YourGame.Init"/>,
/// and no room initialises it. That is also what lets the <see cref="LevelSelect"/> draw it: the menu
/// runs no room, so a night that only existed inside one could not fall over the menu's preview.
/// </summary>
internal static class Night
{
    private const string JsonGroup = "DAYCYCLE";
    private const string JsonObject = "NIGHT";

    private const string FieldDeepFrom = "DEEPFROM";
    private const string FieldDeepTo = "DEEPTO";
    private const string FieldDuskFrom = "DUSKFROM";
    private const string FieldDawnTo = "DAWNTO";
    private const string FieldDeepOpacity = "DEEPOPA";
    private const string FieldTwilightOpacity = "TWILOPA";

    // Deep night wraps midnight, so it is the one band read as two halves; the twilights either side
    // of it are dimmed half as far. Every other hour is daylight — no dark, and no moon.
    private const int DefaultDeepFromHour = 22;
    private const int DefaultDeepToHour = 2;
    private const int DefaultDuskFromHour = 18;
    private const int DefaultDawnToHour = 6;

    private const float DefaultDeepOpacity = 0.4f;
    private const float DefaultTwilightOpacity = 0.2f;

    // The same wall clock the sun is placed by: 4 is the hour.
    private const int StatHour = 4;

    private static int DeepFromHour;
    private static int DeepToHour;
    private static int DuskFromHour;
    private static int DawnToHour;
    private static float DeepOpacity;
    private static float TwilightOpacity;

    /// <summary>
    /// How dark this hour is, and 0 for an hour that is not night at all — which is also how the
    /// <see cref="Moon"/> asks whether it is out.
    ///
    /// The clock is read here rather than cached: it costs one <c>stat</c> call, and a player who
    /// stays on a hole past the turn of an hour watches it fall dark.
    /// </summary>
    public static float Dim
    {
        get
        {
            int hour = YourGame.API.stat(StatHour);

            if (hour >= DeepFromHour || hour < DeepToHour)
            {
                return DeepOpacity;
            }

            if ((hour >= DuskFromHour && hour < DeepFromHour) || (hour >= DeepToHour && hour < DawnToHour))
            {
                return TwilightOpacity;
            }

            return 0f;
        }
    }

    /// <summary>
    /// The hours and how dark each of them is, and nothing else — the corner that falls dark is the
    /// caller's, handed to <see cref="Draw"/>. Called once, from <see cref="YourGame.Init"/>: a room
    /// entry is not what makes it night, so re-reading this per entry would only cost the same file
    /// read twenty times a run. A retune in the JSON editor lands on the next Ctrl+R rather than on
    /// the Ctrl+S, which is the price of loading it once.
    /// </summary>
    public static void Init()
    {
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

        DeepFromHour = data.GetInt(FieldDeepFrom, 0, DefaultDeepFromHour);
        DeepToHour = data.GetInt(FieldDeepTo, 0, DefaultDeepToHour);
        DuskFromHour = data.GetInt(FieldDuskFrom, 0, DefaultDuskFromHour);
        DawnToHour = data.GetInt(FieldDawnTo, 0, DefaultDawnToHour);
        DeepOpacity = (float)data.GetDec(FieldDeepOpacity, 0, DefaultDeepOpacity);
        TwilightOpacity = (float)data.GetDec(FieldTwilightOpacity, 0, DefaultTwilightOpacity);
    }

    /// <summary>
    /// One screenful of dark from <paramref name="originX"/>, <paramref name="originY"/> — the corner
    /// in whatever space the caller's camera is up in. A room hands its own corner, since everything
    /// it draws is in map-sheet pixels; anything drawn straight on the screen hands (0, 0). A room is
    /// exactly one screen either way, so this covers it and nothing of the room beside it.
    /// </summary>
    public static void Draw(int originX, int originY)
    {
        float dim = Dim;
        if (dim <= 0f)
        {
            return;
        }

        // rectfill takes the far corner rather than a size.
        YourGame.API.rectfill(originX, originY,
            originX + Constants.Screen.ResolutionX - 1, originY + Constants.Screen.ResolutionY - 1,
            Constants.Colors.Black, dim);
    }
}
