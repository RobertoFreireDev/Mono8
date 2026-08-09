namespace mono8.game;

/// <summary>
/// Which level is being played, called out in the bottom-left corner above the strokes count for
/// <see cref="ShownSeconds"/> and then gone — it says where a run has got to on the way in, and after
/// that the corner belongs to the shot.
///
/// It is up only for a level *arrived at*: picked off the menu, or come up behind the wipe. A level
/// started over is not arrived at, so <see cref="Init"/> — which every room entry runs, restarts
/// included — clears the call-out rather than raising it, and raising it is <see cref="YourGame"/>'s,
/// the one place that knows which of the two an entry was.
///
/// Drawn like every other caption in the game — white on a one-pixel black outline. What makes it a
/// call-out is that it goes, not that it is set apart while it is up.
///
/// Screen pixels, drawn with the rest of the HUD after the room's camera is back at the origin.
/// </summary>
internal static class LevelHud
{
    private const string Caption = "LEVEL ";

    /// <summary>How long an arrival is called out for.</summary>
    private const float ShownSeconds = 3f;

    // One line above the strokes count, which is itself measured off the meter bar — so the whole
    // bottom-left stack still moves together with HUD / METER's margin. A HUD / LEVEL object with a
    // POS is where the placement would go if it ever wants retuning without a rebuild.
    private const int RowGap = 2;

    private static string Text;
    private static float ShownLeft;

    private static int Y => Club.LabelY - RowGap - Font.Height;

    /// <param name="number">
    /// The room's NUMBER. A room that authors none is not a level and gets no caption — there is no
    /// number to print and nothing in the grid it would agree with.
    /// </param>
    public static void Init(int number)
    {
        // Built once per entry: the number cannot change while the room is being played, so drawing
        // the caption allocates nothing.
        Text = number >= Levels.MinNumber ? Caption + number : null;

        // Cleared, never raised. A room lost or restarted comes back through here too, and a
        // call-out left running across that would be announcing an arrival that did not happen.
        ShownLeft = 0f;
    }

    /// <summary>
    /// Puts the caption up for the next few seconds — a level arrived at, whether picked off the
    /// menu or come up behind the wipe. Never a level started over.
    /// </summary>
    public static void Highlight()
    {
        ShownLeft = ShownSeconds;
    }

    public static void Update(float elapsedSeconds)
    {
        if (ShownLeft > 0f)
        {
            ShownLeft -= elapsedSeconds;
        }
    }

    public static void Draw()
    {
        if (Text == null || ShownLeft <= 0f)
        {
            return;
        }

        // White on a black outline, like every other caption in the game: the corner is over the
        // room, which is a background it has no say in.
        Font.PrintOutlined(Text, Hud.LeftX, Y, Constants.Colors.White);
    }
}
