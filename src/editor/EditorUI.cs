namespace mono8.editor;

/// <summary>Layout constants and small widgets shared by every editor.</summary>
internal static class EditorUI
{
    /// <summary>Top of the orange status bar that every editor reserves along the bottom.</summary>
    public static int BottomBarY => Constants.Screen.ResolutionY - Constants.GameDataSizes.TileSize;

    /// <summary>A filled box with a label in its top-left corner.</summary>
    public static void Box(IMono8API api, Rectangle b, string text, int bg, int fg)
    {
        api.rectfill(b.X, b.Y, b.X + b.Width - 1, b.Y + b.Height - 1, bg);
        api.print(text, b.X + 1, b.Y + 1, fg);
    }

    /// <summary>A <see cref="Box"/> in the default light-grey-on-indigo button colours.</summary>
    public static void TextButton(IMono8API api, Rectangle b, string text) =>
        Box(api, b, text, Constants.Colors.LightGray, Constants.Colors.Indigo);

    /// <summary>
    /// Steps <paramref name="index"/> forward on left-click and backward on right-click,
    /// wrapping within <paramref name="length"/>. Returns true when the click was consumed.
    /// </summary>
    public static bool CycleOnClick(IMono8API api, Rectangle bounds, (int x, int y) mouse, ref int index, int length)
    {
        if (!bounds.Contains(mouse.x, mouse.y)) return false;

        if (api.mouselp()) index = (index + 1) % length;
        else if (api.mouserp()) index = (index - 1 + length) % length;
        else return false;

        return true;
    }
}
