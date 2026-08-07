namespace mono8.game;

/// <summary>
/// The screen furniture: the shot counter in the top-right corner. Screen pixels, drawn after
/// everything in the room, through <see cref="Font"/> like every other caption in the game.
///
/// Only strokes that actually send the ball count — a whiff is not a shot.
/// </summary>
internal static class Hud
{
    private const string JsonGroup = "HUD";
    private const string JsonObject = "HITS";

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

        Hits = -1;    // anything but 0, so the first SetHits builds the caption
        SetHits(0);
    }

    /// <summary>One more ball actually struck.</summary>
    public static void CountHit()
    {
        SetHits(Hits + 1);
    }

    public static void Draw()
    {
        // Right-aligned, which is what the caption has to be measured for.
        Font.PrintOutlined(HitCaption, Constants.Screen.ResolutionX - Margin - Font.Width(HitCaption),
            Margin, Constants.Colors.White);
    }

    // Rebuilt only when the count moves, so drawing the caption allocates nothing.
    private static void SetHits(int hits)
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
