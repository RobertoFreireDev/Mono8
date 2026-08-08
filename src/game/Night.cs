namespace mono8.game;

/// <summary>
/// The wash of dark the night falls in: one flat rectangle over the whole screen at the opacity the
/// hour asks for, and the hours themselves. Nothing is placed in it — the body in the night sky is
/// the <see cref="Moon"/>, which draws before this and is dimmed by it like everything else.
///
/// Drawn last of the room's own layers, over the terrain, the bodies and the <see cref="Clouds"/>
/// alike, so a cloud at midnight is as dark as the ground under it. Its half of DAYCYCLE / NIGHT is
/// the bands and their opacities; it reads the clock and nothing else — no room, no state.
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

    public static void Draw()
    {
        float dim = Dim;
        if (dim <= 0f)
        {
            return;
        }

        // Screen pixels, and rectfill takes the far corner rather than a size. See the known mismatch
        // in GAME.md: the call site still has the room's camera up, so the dark only covers the
        // screen on a room whose CELLPOS is (0, 0).
        YourGame.API.rectfill(0, 0, Constants.Screen.ResolutionX - 1, Constants.Screen.ResolutionY - 1,
            Constants.Colors.Black, dim);
    }
}
