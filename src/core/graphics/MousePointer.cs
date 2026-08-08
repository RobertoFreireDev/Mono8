namespace mono8.core.graphics;

/// <summary>
/// The pointer the console draws for itself, in place of the OS one.
/// <para>
/// It is drawn into the scene target, so it lives in virtual screen pixels: the icon's top-left
/// corner is the pixel a click resolves to at every window scale and in fullscreen, and the render
/// target clips whatever hangs past the edge, so the letterbox never gets painted on.
/// </para>
/// </summary>
internal static class MousePointer
{
    public const int None = -1;

    private const int DefaultIcon = 66;
    private const int IconCount =
        Constants.GameDataSizes.IconSheetX / Constants.GameDataSizes.TileSize *
        (Constants.GameDataSizes.IconSheetY / Constants.GameDataSizes.TileSize);

    public static bool Visible = true;

    // Unlike Visible, this outlives the game session: the pointer the developer chose stays chosen
    // until the process ends, editors included.
    private static int _icon = DefaultIcon;

    // What the editor chrome wants the pointer to be right now — over a menu bar button, say. Kept
    // apart from _icon so that letting go of it returns to whatever the game chose, not to the
    // built-in pointer.
    private static int _overrideIcon = None;

    /// <summary>Anything outside the sheet restores the built-in pointer rather than drawing garbage.</summary>
    public static void SetIcon(int n) => _icon = InSheet(n) ? n : DefaultIcon;

    /// <summary>The chrome pointer, or <see cref="None"/> to hand the pointer back to the game's.</summary>
    public static void SetOverrideIcon(int n) => _overrideIcon = InSheet(n) ? n : None;

    private static bool InSheet(int n) => n >= 0 && n < IconCount;

    public static void Draw()
    {
        if (!Visible) return;

        var (x, y) = MouseInput.MouseVirtualPosition();

        // Off the virtual screen is letterbox or outside the window: no pixel to point at there.
        if (x < -7 || y < -7 || x >= Screen.BaseBox.Width || y >= Screen.BaseBox.Height) return;

        // The pointer is chrome, not part of the scene, so it ignores the pal/palt the frame left set
        // rather than being tinted or erased by it.
        ColorPalette.SuspendDrawPalette();
        IconSheet.Draw(_overrideIcon != None ? _overrideIcon : _icon, x, y);
        ColorPalette.ResumeDrawPalette();
    }
}
