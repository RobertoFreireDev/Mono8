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
        PrintOutlined(HitCaption,
            Constants.Screen.ResolutionX - Margin - HitCaption.Length * FontAdvance, Margin,
            Constants.Colors.White);
    }

    /// <summary>
    /// Screen text with a one-pixel black outline, which is what keeps the HUD readable over a room
    /// the game has no say in. Five passes: the four neighbours, then the text itself over them.
    /// </summary>
    public static void PrintOutlined(string text, int x, int y, int color)
    {
        var api = YourGame.API;

        api.print(text, x - 1, y, Constants.Colors.Black);
        api.print(text, x + 1, y, Constants.Colors.Black);
        api.print(text, x, y - 1, Constants.Colors.Black);
        api.print(text, x, y + 1, Constants.Colors.Black);
        api.print(text, x, y, color);
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
