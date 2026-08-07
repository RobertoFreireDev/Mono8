namespace mono8.game;

/// <summary>
/// The screen furniture: the strokes the room has left, in the top-right corner. Screen pixels,
/// drawn after everything in the room, through <see cref="Font"/> like every other caption in the
/// game.
///
/// The room says how many strokes it allows (ROOMS/&lt;name&gt;.HITMAX) and this counts them off; at
/// zero the hole cannot be finished and the room starts over. Only strokes that actually send the
/// ball count — a whiff is not a shot.
/// </summary>
internal static class Hud
{
    private const string JsonGroup = "HUD";
    private const string JsonObject = "HITS";

    private static string HitLabel;
    private static int Margin;

    private static int Max;
    private static int Left;
    private static string HitCaption;

    /// <summary>
    /// Whether the strokes are spent, which is what ends the attempt once the last ball has stopped.
    /// A room allowing none of them is read as unlimited rather than lost on its first frame — an
    /// unauthored HITMAX must not restart the level forever.
    /// </summary>
    public static bool OutOfShots => Max > 0 && Left <= 0;

    /// <param name="max">The room's HITMAX — the strokes it allows.</param>
    public static void Init(int max)
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

        Max = max;

        Left = max - 1;    // anything but max, so the first SetLeft builds the caption
        SetLeft(max);
    }

    /// <summary>One more ball actually struck, so one stroke fewer to play it with.</summary>
    public static void CountHit()
    {
        // Floored, so a stroke taken on the frame the level is already lost cannot read as strokes
        // still in hand.
        SetLeft(Left > 0 ? Left - 1 : 0);
    }

    public static void Draw()
    {
        // Right-aligned, which is what the caption has to be measured for.
        Font.PrintOutlined(HitCaption, Constants.Screen.ResolutionX - Margin - Font.Width(HitCaption),
            Margin, CaptionColor());
    }

    // The last strokes are called out in colour, since a number in the corner is easy to miss while
    // lining up a shot. Zero stays red rather than falling back to white: the frames before the room
    // restarts are the most urgent the count ever is, not the least.
    private static int CaptionColor()
    {
        if (Left <= 1)
        {
            return Constants.Colors.Red;
        }

        if (Left == 2)
        {
            return Constants.Colors.Yellow;
        }

        return Constants.Colors.White;
    }

    // Rebuilt only when the count moves, so drawing the caption allocates nothing.
    private static void SetLeft(int left)
    {
        if (left == Left)
        {
            return;
        }

        Left = left;

        // The gap is code's, not the label's: the editor trims a Text value, so a trailing space
        // authored in LABEL would not survive the save.
        HitCaption = HitLabel + " " + Left;
    }
}
