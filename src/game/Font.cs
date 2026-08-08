namespace mono8.game;

/// <summary>
/// The engine's font: the two numbers laying text out needs, and the one call every caption in the
/// game is drawn with. Nothing is placed off a literal, so nothing drifts if the font ever changes.
/// </summary>
internal static class Font
{
    /// <summary>Pixels one character advances the cursor.</summary>
    public const int Advance = 4;

    /// <summary>
    /// Height of one glyph in pixels — what centring a caption in a box measures by. Not the
    /// engine's line advance, which is 9: nothing here prints a '\n'.
    /// </summary>
    public const int Height = 7;

    /// <summary>
    /// Rows down from the y a line prints at to the middle of its ink. The engine draws the glyph
    /// cell one pixel above the y it is given, so a line's centre is two rows down and not three —
    /// what anything centred *on* a caption rather than on its box has to measure by.
    /// </summary>
    public const int Middle = 2;

    /// <summary>What a string takes on screen — what centring and right-aligning measure.</summary>
    public static int Width(string text)
    {
        return text == null ? 0 : text.Length * Advance;
    }

    /// <summary>
    /// Text with a one-pixel black outline, which is what keeps a caption readable over a background
    /// the game has no say in — the room, the meter, the map. Five passes: the four neighbours, then
    /// the text itself over them.
    /// </summary>
    /// <param name="plain">
    /// What the four black passes draw, when <paramref name="text"/> carries <c>print</c>'s inline
    /// <c>#XX</c> colour markers — a marker would recolour the outline too, so the caller hands over
    /// the marker-free version of the same caption. Both lay out identically, since a marker draws
    /// nothing. Null is right for every caption that is a single colour.
    /// </param>
    public static void PrintOutlined(string text, int x, int y, int color, float opacity = 1f, string plain = null)
    {
        var api = YourGame.API;
        string outline = plain ?? text;

        api.print(outline, x - 1, y, Constants.Colors.Black, opacity);
        api.print(outline, x + 1, y, Constants.Colors.Black, opacity);
        api.print(outline, x, y - 1, Constants.Colors.Black, opacity);
        api.print(outline, x, y + 1, Constants.Colors.Black, opacity);
        api.print(text, x, y, color, opacity);
    }
}
