namespace mono8.editor;

/// <summary>Layout constants and small widgets shared by every editor.</summary>
internal static class EditorUI
{
    /// <summary>Top of the orange status bar that every editor reserves along the bottom.</summary>
    public static int BottomBarY => Constants.Screen.ResolutionY - Constants.GameDataSizes.TileSize;

    /// <summary>Event labels for an undo/redo shortcut that had no history left to walk.</summary>
    public const string NothingToUndoLabel = "NOTHING TO UNDO";
    public const string NothingToRedoLabel = "NOTHING TO REDO";

    /// <summary>The pointer shown over anything in the editors that answers to a click.</summary>
    private const int HoverPointerIcon = 67;

    /// <summary>
    /// Puts the hover pointer up while <paramref name="over"/> says the cursor rests on a control.
    /// Only ever raises: the pointer is lowered once at the top of the editor frame, so each piece
    /// of chrome speaks for its own controls alone and none can pull another's down again.
    /// </summary>
    public static void HoverPointer(bool over)
    {
        if (over) MousePointer.SetOverrideIcon(HoverPointerIcon);
    }

    /// <summary>A filled box with a label in its top-left corner.</summary>
    public static void Box(IMono8API api, Rectangle b, string text, int bg, int fg)
    {
        api.rectfill(b.X, b.Y, b.X + b.Width - 1, b.Y + b.Height - 1, bg);
        api.print(text, b.X + 1, b.Y + 1, fg);
    }

    /// <summary>
    /// Prints in the case the string is stored in, which <c>api.print</c> cannot do — it folds to
    /// upper case, the one form a game is documented to get. The 1 px lift matches the one
    /// <c>print</c> applies, so a cased line sits exactly where a folded one would have.
    /// <para>
    /// This is for a value the developer typed and the file keeps. Labels, names and numbers stay
    /// on the folded path: they read as the console's own text rather than as authored content.
    /// </para>
    /// </summary>
    public static void PrintCased(string text, int x, int y, int color, float opacity = 1f) =>
        Text.DrawText(text, new Vector2(x, y - 1), color, colorOpaqueness: opacity, preserveCase: true);

    /// <summary>A <see cref="Box"/> in the default light-grey-on-indigo button colours.</summary>
    public static void TextButton(IMono8API api, Rectangle b, string text) =>
        Box(api, b, text, Constants.Colors.LightGray, Constants.Colors.Indigo);

    /// <summary>
    /// A saved index brought back onto a table of <paramref name="length"/>: anything outside it
    /// falls back to the first entry rather than to the nearest, since a stale index says nothing
    /// about which end of the table the developer meant.
    /// </summary>
    public static int ClampIndex(int value, int length) => value < 0 || value >= length ? 0 : value;

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

    /// <summary>
    /// Lays <paramref name="s"/> out over lines of <paramref name="columns"/> characters, cut on
    /// the count rather than between words. Each line is returned as its <c>(Start, Length)</c>
    /// inside <paramref name="s"/> rather than as a substring, which is what lets a caret be turned
    /// into a line and a column and back.
    /// <para>
    /// The spans are contiguous and cover the string whole, so every character — a space on a break
    /// included — holds a cell of its own and the lines read as the one string they are: a
    /// character typed anywhere pushes every character after it along by one cell, wrapping the end
    /// of each line onto the next, and a deleted one pulls them all back the same way. Word-wrap
    /// cannot do that, because the space it breaks on has nowhere to be drawn and a single
    /// character inserted early on throws whole words across a line at a time.
    /// </para>
    /// Always returns at least one span.
    /// </summary>
    public static List<(int Start, int Length)> WrapSpans(string s, int columns)
    {
        var spans = new List<(int Start, int Length)>();
        if (columns <= 0 || string.IsNullOrEmpty(s))
        {
            spans.Add((0, s == null ? 0 : s.Length));
            return spans;
        }

        for (int i = 0; i < s.Length; i += columns) spans.Add((i, Math.Min(columns, s.Length - i)));
        return spans;
    }

    /// <summary>The lines <see cref="WrapSpans"/> describes, as text.</summary>
    public static List<string> Wrap(string s, int columns)
    {
        var spans = WrapSpans(s, columns);
        var lines = new List<string>(spans.Count);
        foreach (var (start, length) in spans)
        {
            lines.Add(length == 0 ? string.Empty : s.Substring(start, length));
        }
        return lines;
    }
}
