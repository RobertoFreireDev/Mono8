namespace mono8.game;

/// <summary>
/// The screen furniture: the shot counter in the top-right corner. Screen pixels, drawn after
/// everything in the room.
///
/// Only strokes that actually send the ball count — a whiff is not a shot.
/// </summary>
internal static class Hud
{
    private const string JsonGroup = "HUD";
    private const string JsonObject = "HITS";

    // The engine's font advances 4 px a character and prints upper-case, which is how the caption
    // is right-aligned.
    private const int FontAdvance = 4;

    private static string HitLabel;
    private static int Margin;

    private static int Hits;
    private static string HitCaption;

    public static void Init()
    {
        HitLabel = string.Empty;
        Margin = 0;

        // Re-read every Init: Ctrl+S in the JSON editor rebuilds the data without a restart.
        var data = YourGame.API.gjson(JsonGroup, JsonObject);
        if (data != null)
        {
            HitLabel = data.GetStr("LABEL");
            Margin = data.GetInt("MARGIN");
        }

        Hits = -1;    // anything but 0, so the first Count builds the caption
        Count(0);
    }

    /// <summary>One more ball actually struck.</summary>
    public static void CountHit()
    {
        Count(Hits + 1);
    }

    public static void Draw()
    {
        var api = YourGame.API;

        api.print(HitCaption,
            Constants.Screen.ResolutionX - Margin - HitCaption.Length * FontAdvance, Margin,
            Constants.Colors.Yellow);
    }

    // Rebuilt only when the count moves, so drawing the caption allocates nothing.
    private static void Count(int hits)
    {
        if (hits == Hits)
        {
            return;
        }

        Hits = hits;

        // The gap is code's, not the label's: the editor trims a Text value, so a trailing space
        // authored in LABEL would not survive the save.
        HitCaption = HitLabel + " " + Hits;
    }
}
