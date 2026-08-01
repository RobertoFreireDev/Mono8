using System.Globalization;

namespace mono8.core.data;

/// <summary>The six kinds of value a <c>data.json</c> field can hold.</summary>
public enum DataValueType
{
    Text,
    Int,
    Decimal,
    Money,
    PosXY,
    Bool
}

/// <summary>
/// Sole authority on what each <see cref="DataValueType"/> accepts, how it is stored, and how it
/// is written to <c>data.json</c>. Values live as strings so the editor can hold a half-typed
/// entry; this class is what turns a half-typed entry into a canonical one.
/// <para>
/// Every parse and format goes through <see cref="CultureInfo.InvariantCulture"/> so the file
/// stays byte-identical across machines and diffs cleanly.
/// </para>
/// </summary>
public static class DataValue
{
    private const int IntChars = 11;     // -2147483648
    private const int DecimalChars = 24; // round-trip double, e.g. -1.7976931348623157E+308
    private const int MoneyChars = 20;
    private const int PosXYChars = 23;   // -2147483648,-2147483648
    private const int BoolChars = 5;     // "false"

    /// <summary>Longest raw entry accepted for <paramref name="type"/>, in characters.</summary>
    public static int MaxLength(DataValueType type) => type switch
    {
        DataValueType.Text => Constants.JsonData.MaxTextChars,
        DataValueType.Int => IntChars,
        DataValueType.Decimal => DecimalChars,
        DataValueType.Money => MoneyChars,
        DataValueType.PosXY => PosXYChars,
        _ => BoolChars
    };

    /// <summary>
    /// True when <paramref name="c"/> may be appended to <paramref name="current"/> in a field of
    /// <paramref name="type"/>. This is the per-keystroke gate: it is what stops a second '.' in a
    /// Money field or a third decimal digit, before anything reaches <see cref="TryNormalize"/>.
    /// </summary>
    public static bool IsCharAllowed(DataValueType type, char c, string current)
    {
        current ??= string.Empty;
        if (current.Length >= MaxLength(type)) return false;
        if (!Text.IsValidChar(c)) return false;

        switch (type)
        {
            case DataValueType.Text:
                return true;

            case DataValueType.Int:
                return char.IsDigit(c) || (c == '-' && current.Length == 0);

            case DataValueType.Decimal:
                if (c == '-') return current.Length == 0;
                if (c == '.') return current.IndexOf('.') < 0;
                return char.IsDigit(c);

            case DataValueType.Money:
                if (c == '-') return current.Length == 0;
                if (c == '.') return current.IndexOf('.') < 0;
                if (!char.IsDigit(c)) return false;
                int dot = current.IndexOf('.');
                return dot < 0 || current.Length - dot - 1 < 2;

            case DataValueType.PosXY:
                int comma = current.IndexOf(',');
                // ',' closes the x part, so it needs an x to close and may only appear once.
                if (c == ',') return comma < 0 && current.Length > 0 && current != "-";
                if (c == '-') return current.Length == 0 || comma == current.Length - 1;
                return char.IsDigit(c);

            default:
                return false; // Bool is toggled, never typed.
        }
    }

    /// <summary>
    /// Turns a raw entry into its stored form: <c>5</c> becomes <c>5.00</c> for Money, leading
    /// zeros disappear from an Int, <c>8 , 40</c> becomes <c>8,40</c>. Returns false when the entry
    /// cannot be read as <paramref name="type"/> at all, in which case the caller keeps the old value.
    /// <para>
    /// Text is sanitised and truncated rather than rejected — a value is data, and clipping a
    /// hand-edited over-long string loses less than dropping the whole field would.
    /// </para>
    /// </summary>
    public static bool TryNormalize(DataValueType type, string raw, out string normalized)
    {
        normalized = string.Empty;
        if (raw == null) return false;

        switch (type)
        {
            case DataValueType.Text:
                string clean = Text.Sanitize(raw);
                int max = MaxLength(type);
                normalized = clean.Length > max ? clean.Substring(0, max) : clean;
                return true;

            case DataValueType.Int:
                if (!int.TryParse(raw.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int i))
                    return false;
                normalized = i.ToString(CultureInfo.InvariantCulture);
                return true;

            case DataValueType.Decimal:
                if (!double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d)
                    || double.IsNaN(d) || double.IsInfinity(d))
                    return false;
                normalized = d.ToString("R", CultureInfo.InvariantCulture);
                return true;

            case DataValueType.Money:
                if (!decimal.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out decimal m))
                    return false;
                normalized = Math.Round(m, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture);
                return true;

            case DataValueType.PosXY:
                string[] parts = raw.Split(',');
                if (parts.Length != 2) return false;
                if (!int.TryParse(parts[0].Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int x)) return false;
                if (!int.TryParse(parts[1].Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int y)) return false;
                normalized = x.ToString(CultureInfo.InvariantCulture) + "," + y.ToString(CultureInfo.InvariantCulture);
                return true;

            default:
                string b = raw.Trim();
                if (string.Equals(b, "true", StringComparison.OrdinalIgnoreCase)) { normalized = "true"; return true; }
                if (string.Equals(b, "false", StringComparison.OrdinalIgnoreCase)) { normalized = "false"; return true; }
                return false;
        }
    }

    /// <summary>Display form of a stored value. Only Bool differs: it reads as a word, not a literal.</summary>
    public static string Format(DataValueType type, string stored)
    {
        if (string.IsNullOrEmpty(stored)) return string.Empty;
        return type == DataValueType.Bool ? (IsTrue(stored) ? "TRUE" : "FALSE") : stored;
    }

    /// <summary>True when the stored form is the JSON literal <c>true</c>.</summary>
    public static bool IsTrue(string stored) =>
        string.Equals(stored, "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True when the value is written to the file inside quotes. Money is quoted despite being a
    /// number so its trailing zeros survive the round trip ("3.50" would come back as 3.5).
    /// </summary>
    public static bool IsQuoted(DataValueType type) =>
        type == DataValueType.Text || type == DataValueType.Money;

    /// <summary>The one-character code that suffixes the key in the file, as in <c>"HP:i"</c>.</summary>
    public static string Code(DataValueType type) => type switch
    {
        DataValueType.Text => "t",
        DataValueType.Int => "i",
        DataValueType.Decimal => "d",
        DataValueType.Money => "m",
        DataValueType.PosXY => "p",
        _ => "b"
    };

    /// <summary>
    /// Reads a key suffix back into a type. Unknown codes fail so the loader can drop the field.
    /// <para>
    /// <c>s</c> was the old 16-character String type. It reads as a Text so a file authored before
    /// the two were merged keeps its fields; the key no longer matches <see cref="Code"/>, so the
    /// load is reported as repaired and the next save writes it back as <c>:t</c>.
    /// </para>
    /// </summary>
    public static bool TryParse(string code, out DataValueType type)
    {
        type = DataValueType.Text;
        if (string.IsNullOrEmpty(code) || code.Length != 1) return false;

        switch (code[0])
        {
            case 's': type = DataValueType.Text; return true;
            case 't': type = DataValueType.Text; return true;
            case 'i': type = DataValueType.Int; return true;
            case 'd': type = DataValueType.Decimal; return true;
            case 'm': type = DataValueType.Money; return true;
            case 'p': type = DataValueType.PosXY; return true;
            case 'b': type = DataValueType.Bool; return true;
            default: return false;
        }
    }

    /// <summary>Same as <see cref="TryParse"/>, falling back to Text for an unknown code.</summary>
    public static DataValueType Parse(string code) =>
        TryParse(code, out DataValueType type) ? type : DataValueType.Text;

    /// <summary>The value a newly created field of <paramref name="type"/> starts with.</summary>
    public static string Default(DataValueType type) => type switch
    {
        DataValueType.Text => "",
        DataValueType.Int => "0",
        DataValueType.Decimal => "0",
        DataValueType.Money => "0.00",
        DataValueType.PosXY => "0,0",
        _ => "false"
    };
}
