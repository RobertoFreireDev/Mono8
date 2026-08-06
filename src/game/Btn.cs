namespace mono8.game;

/// <summary>
/// The console's buttons by name — the index each <c>btn</c> call reads. Player 0's, which is every
/// button this game asks for.
///
/// Named Btn rather than Buttons: every type here is in scope engine-wide, and MonoGame already has
/// a Buttons.
/// </summary>
internal static class Btn
{
    public const int Left = 0;
    public const int Right = 1;

    /// <summary>Up takes a stair, Down takes the one under the feet.</summary>
    public const int Up = 2;
    public const int Down = 3;

    /// <summary>A (Z).</summary>
    public const int Jump = 4;

    /// <summary>B (X) — one press per state: address the ball, pull back, swing through.</summary>
    public const int Swing = 5;

    /// <summary>X (C) — back out of a swing that has not been taken yet.</summary>
    public const int Cancel = 6;

    /// <summary>Y (V) — next club. The last face button free.</summary>
    public const int Club = 7;
}
