namespace mono8.game;

/// <summary>
/// The one switch every debug overlay reads, flipped from the pause menu (Enter, then X on the
/// entry). Off by default, so a debug draw can be left in the code permanently.
/// </summary>
internal static class Debug
{
    private const int MenuIndex = 0;

    public static bool Enabled;

    public static void Init()
    {
        Enabled = false;
        Register();
    }

    /// <summary>
    /// The overlay that belongs to no one in particular. Drawn last, so it sits over the room.
    /// </summary>
    public static void Draw()
    {
        if (Enabled)
        {
            YourGame.API.print($"FPS {YourGame.API.stat(7)}", 1, 1, Constants.Colors.White);
            YourGame.API.print(Swing.State, 1, 9, Constants.Colors.White);
        }
    }

    private static void Toggle()
    {
        Enabled = !Enabled;
        Register();
    }

    // The label carries the state, so it has to be rewritten on every flip. 16 chars max.
    private static void Register()
    {
        YourGame.API.menuitem(MenuIndex, Enabled ? "DEBUG: ON" : "DEBUG: OFF", Toggle);
    }
}
