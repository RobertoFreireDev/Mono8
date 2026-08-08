namespace mono8.game;

/// <summary>
/// The screen furniture: the strokes the room has left, at the left end of the row over the
/// <see cref="Meter"/> bar with the <see cref="Club"/> label set in beside it. Screen pixels, drawn
/// after everything in the room, through <see cref="Font"/> like every other caption in the game.
///
/// The room says how many strokes it allows (ROOMS/&lt;name&gt;.HITMAX) and this counts them off; at
/// zero the hole cannot be finished and the room starts over. Only strokes that actually send the
/// ball count — a whiff is not a shot.
/// </summary>
internal static class Hud
{
    private const string JsonGroup = "HUD";
    private const string JsonObject = "HITS";

    // The count is always two digits, zero-padded — 01, 04, 20 — so it takes the same width whatever
    // it reads and nothing on the row is placed off the number of the moment.
    private const int SlotDigits = 2;
    private const int MaxShown = 99;

    private static int Margin;

    private static int Max;
    private static int Left;
    private static string HitCaption;
    private static string HitPlain;

    /// <summary>
    /// Screen x just past the count, which is where the <see cref="Club"/> label lines itself up
    /// from. Fixed, since the slot is.
    /// </summary>
    public static int RightX => Margin + SlotDigits * Font.Advance;

    /// <summary>
    /// Whether the strokes are spent, which is what ends the attempt once the last ball has stopped.
    /// A room allowing none of them is read as unlimited rather than lost on its first frame — an
    /// unauthored HITMAX must not restart the level forever.
    /// </summary>
    public static bool OutOfShots => Max > 0 && Left <= 0;

    /// <summary>
    /// Strokes actually taken this attempt — what a hole is recorded as once it is sunk. Counted up
    /// rather than read off the count left, which stops at zero and says nothing at all in a room
    /// that allows unlimited strokes.
    /// </summary>
    public static int Taken { get; private set; }

    /// <param name="max">The room's HITMAX — the strokes it allows.</param>
    public static void Init(int max)
    {
        Margin = 0;

        // Re-read every Init: Ctrl+S in the JSON editor rebuilds the data without a restart.
        // HUD/HITS.LABEL is still authored but no longer read — the corner shows the number alone.
        var data = YourGame.API.gjson(JsonGroup, JsonObject);
        if (data != null)
        {
            Margin = data.GetInt("MARGIN");
        }

        Max = max;
        Taken = 0;

        Left = max - 1;    // anything but max, so the first SetLeft builds the caption
        SetLeft(max);
    }

    /// <summary>One more ball actually struck, so one stroke fewer to play it with.</summary>
    public static void CountHit()
    {
        Taken++;

        // Floored, so a stroke taken on the frame the level is already lost cannot read as strokes
        // still in hand.
        SetLeft(Left > 0 ? Left - 1 : 0);
    }

    public static void Draw()
    {
        Font.PrintOutlined(HitCaption, Margin, Club.LabelY, Constants.Colors.White, plain: HitPlain);
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

        // A room authored above the two digits reads 99 rather than being allowed to print a third
        // and run into the club label.
        int shown = Left < MaxShown ? Left : MaxShown;
        HitPlain = shown < 10 ? "0" + shown : shown.ToString();

        // print's inline colour marker: '#' plus the palette index as two digits, so the count is
        // recoloured without the caller having to know which colour it ended up being.
        int color = CaptionColor();
        HitCaption = "#" + (color < 10 ? "0" : string.Empty) + color + HitPlain;
    }
}
