namespace mono8.game;

/// <summary>
/// The engine's font: the two numbers laying text out needs, and the one call every caption in the
/// game is drawn with. Nothing is placed off a literal, so nothing drifts if the font ever changes.
/// </summary>
internal static class Font
{
    /// <summary>Pixels one character advances the cursor.</summary>
    public const int Advance = 4;

    /// <summary>Height of one printed line in pixels.</summary>
    public const int Height = 7;

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
    public static void PrintOutlined(string text, int x, int y, int color, float opacity = 1f)
    {
        var api = YourGame.API;

        api.print(text, x - 1, y, Constants.Colors.Black, opacity);
        api.print(text, x + 1, y, Constants.Colors.Black, opacity);
        api.print(text, x, y - 1, Constants.Colors.Black, opacity);
        api.print(text, x, y + 1, Constants.Colors.Black, opacity);
        api.print(text, x, y, color, opacity);
    }
}
