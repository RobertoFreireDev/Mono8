using System.Globalization;

namespace mono8.editor;

/// <summary>
/// The one value a right-click copies, held as the text a <c>data.json</c> field would have had
/// typed into it. It is deliberately its own clipboard and not the one Ctrl+C fills: those hold a
/// region of sheet pixels or of map cells, this holds a number the developer read off one editor
/// and wants in another, and neither may ever overwrite the other.
/// <para>
/// Static because carrying a value from the sprite sheet or the map to the JSON inspector means
/// carrying it across an editor switch, which outlives any one editor instance.
/// </para>
/// </summary>
internal static class ValueClipboard
{
    /// <summary>What was copied, or null while nothing has been.</summary>
    public static string Value { get; private set; }

    public static bool HasValue => Value != null;

    /// <summary>The bottom-bar label for the paste that took <see cref="Value"/>.</summary>
    public static string PasteLabel => "PASTED: " + Value;

    /// <summary>
    /// Copies a plain number — a sprite index, a colour index. Returns the bottom-bar label for
    /// the copy, so the caller says what it took rather than restating the wording.
    /// </summary>
    public static string CopyInt(int value) => Copy(value.ToString(CultureInfo.InvariantCulture));

    /// <summary>Copies a position in the <c>x,y</c> form a PosXY field stores.</summary>
    public static string CopyPos(int x, int y) =>
        Copy(x.ToString(CultureInfo.InvariantCulture) + "," + y.ToString(CultureInfo.InvariantCulture));

    private static string Copy(string text)
    {
        Value = text;
        return "COPIED: " + text;
    }
}
