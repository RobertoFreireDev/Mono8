namespace mono8.game;

/// <summary>
/// The engine's font as the two numbers laying text out needs. Every caption in the game is placed
/// off these rather than off a literal, so nothing drifts if the font ever changes.
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
}
