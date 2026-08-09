namespace mono8.game;

/// <summary>
/// The one switch every debug overlay reads. It has no pause-menu entry and nothing turns it on at
/// runtime — the game is finished, and a shipped build has no debug row — so the overlays stay in the
/// code permanently and are switched on by flipping <see cref="Enabled"/> here while working.
/// </summary>
internal static class Debug
{
    // Assigned here and nowhere else, so switching the overlays on is this one line.
    public static bool Enabled = false;

    /// <summary>
    /// The overlay that belongs to no one in particular. Drawn last, so it sits over the room.
    /// </summary>
    public static void Draw()
    {
        if (Enabled)
        {
            YourGame.API.print($"FPS {YourGame.API.stat(7)}", 1, 1, Constants.Colors.White);
            YourGame.API.print(Swing.State, 1, 9, Constants.Colors.White);
            YourGame.API.print($"PWR {(int)(Swing.Power * 100)}", 1, 17, Constants.Colors.White);
        }
    }
}
