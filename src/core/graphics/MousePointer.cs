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
    private const int PointerIcon = 66;

    public static bool Visible = true;

    public static void Draw()
    {
        if (!Visible) return;

        var (x, y) = MouseInput.MouseVirtualPosition();

        // Off the virtual screen is letterbox or outside the window: no pixel to point at there.
        if (x < 0 || y < 0 || x >= Screen.BaseBox.Width || y >= Screen.BaseBox.Height) return;

        // The pointer is chrome, not part of the scene, so it ignores the pal/palt the frame left set
        // rather than being tinted or erased by it.
        ColorPalette.SuspendDrawPalette();
        IconSheet.Draw(PointerIcon, x, y);
        ColorPalette.ResumeDrawPalette();
    }
}
