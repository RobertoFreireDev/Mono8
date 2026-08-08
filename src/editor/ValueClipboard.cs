using System.Globalization;

namespace mono8.editor;

/// <summary>
/// The one value carried between editors, held as the text a <c>data.json</c> field would have had
/// typed into it. It is deliberately its own clipboard and not the one Ctrl+C fills in the sheet
/// editors: those hold a region of sheet pixels or of map cells, this holds a value the developer
/// read off one editor and wants in another, and neither may ever overwrite the other.
/// <para>
/// The sprite and map editors fill it by right-click, since Ctrl+C is already their region's; the
/// JSON inspector, which has no region to copy, fills it with Ctrl+C beside the Ctrl+V that reads
/// it. So what arrives here is any of the six kinds of value and not only a number — a Text value
/// among them, which is why the labels below have a length to worry about at all.
/// </para>
/// <para>
/// Static because carrying a value from the sprite sheet or the map to the JSON inspector means
/// carrying it across an editor switch, which outlives any one editor instance.
/// </para>
/// </summary>
internal static class ValueClipboard
{
    /// <summary>
    /// Characters of the value a bottom-bar label echoes. The bar starts at x=1 on a 256 px screen
    /// and the font advances 4, so 63 fit before the text runs off the right edge — nothing clips
    /// it, it is simply drawn where no one can read it. Eight of those go to the word and three to
    /// the mark, and this is what is left.
    /// </summary>
    private const int LabelChars = 52;

    private const string Ellipsis = "...";

    /// <summary>What was copied, or null while nothing has been.</summary>
    public static string Value { get; private set; }

    public static bool HasValue => Value != null;

    /// <summary>The bottom-bar label for the paste that took <see cref="Value"/>.</summary>
    public static string PasteLabel => "PASTED: " + Echo(Value);

    /// <summary>
    /// Copies a plain number — a sprite index, a colour index. Returns the bottom-bar label for
    /// the copy, so the caller says what it took rather than restating the wording.
    /// </summary>
    public static string CopyInt(int value) => Copy(value.ToString(CultureInfo.InvariantCulture));

    /// <summary>Copies a position in the <c>x,y</c> form a PosXY field stores.</summary>
    public static string CopyPos(int x, int y) =>
        Copy(x.ToString(CultureInfo.InvariantCulture) + "," + y.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Copies a value already in the form its field stores it in — the JSON inspector's own copy,
    /// which has nothing to convert because what it reads is what a paste would write back. A null
    /// leaves the clipboard alone and says nothing, so a copy aimed at no field cannot empty it.
    /// </summary>
    public static string CopyValue(string stored) => stored == null ? null : Copy(stored);

    private static string Copy(string text)
    {
        Value = text;
        return "COPIED: " + Echo(text);
    }

    /// <summary>
    /// The value as a label says it back. Only a Text value ever runs long enough to be cut, and it
    /// is cut with a mark rather than silently, since a label that stops mid-word otherwise reads as
    /// the copy itself having stopped there.
    /// </summary>
    private static string Echo(string text) =>
        text == null || text.Length <= LabelChars ? text : text.Substring(0, LabelChars) + Ellipsis;
}
