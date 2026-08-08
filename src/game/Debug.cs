namespace mono8.game;

/// <summary>
/// The one switch every debug overlay reads, flipped from the pause menu (Enter, then X on the
/// entry). Off by default, so a debug draw can be left in the code permanently.
/// </summary>
internal static class Debug
{
    private const int MenuIndex = 0;

    // Persistence slot. A fresh save reads 0, so the stored value is offset by one and 0 means
    // "never toggled" — which lands on off, the default.
    private const int SaveSlot = 0;
    private const int SavedOff = 1;
    private const int SavedOn = 2;

    public static bool Enabled;

    // Whether the entry is up, so a re-register that comes from somewhere other than the toggle —
    // Clear, when the save is deleted under it — cannot put it back on a screen that took it down.
    private static bool Shown;

    public static void Init()
    {
        // The entry itself is not registered here: Init lands on the level select, which puts it up
        // and takes it down with the room.
        Enabled = YourGame.API.dget(SaveSlot) == SavedOn;
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
            YourGame.API.print($"PWR {(int)(Swing.Power * 100)}", 1, 17, Constants.Colors.White);
        }
    }

    /// <summary>
    /// The entry is a room's, not the level select's — there is nothing to overlay on a menu. Both
    /// called by <see cref="LevelSelect"/>, which is what knows which of the two is on screen.
    /// </summary>
    public static void Show()
    {
        Shown = true;
        Register();
    }

    public static void Hide()
    {
        Shown = false;
        YourGame.API.menuitem(MenuIndex);
    }

    /// <summary>
    /// Back to the default, for a save being deleted: slot 0 is persistence data like any other, and
    /// one that still remembered the toggle would not have been deleted. The slot itself is zeroed by
    /// <see cref="Save"/>, which reads as off — this is the in-memory half catching up.
    /// </summary>
    public static void Clear()
    {
        Enabled = false;
        Register();
    }

    private static void Toggle()
    {
        Enabled = !Enabled;
        YourGame.API.dset(SaveSlot, Enabled ? SavedOn : SavedOff);
        Register();
    }

    // The label carries the state, so it has to be rewritten on every flip. 16 chars max.
    private static void Register()
    {
        if (Shown)
        {
            YourGame.API.menuitem(MenuIndex, Enabled ? "DEBUG: ON" : "DEBUG: OFF", Toggle);
        }
    }
}
