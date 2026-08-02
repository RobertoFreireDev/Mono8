namespace mono8.game;

/// <summary>
/// The screen furniture: the shot counter in the top-right corner. Screen pixels, drawn after
/// everything in the room.
///
/// Only strokes that actually send the ball count — a whiff is not a shot.
/// </summary>
internal static class Hud
{
    private const string HitLabel = "HIT ";

    // The font advances 4 px a character and prints upper-case, which is how the caption is
    // right-aligned.
    private const int FontAdvance = 4;
    private const int Margin = 6;

    private static int Hits;
    private static string HitCaption;

    public static void Init()
    {
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
        HitCaption = HitLabel + Hits;
    }
}
