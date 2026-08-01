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
    private const int MoneyChars = 20;   // -9999999999999999.99
    private const int PosXYChars = 23;   // -2147483648,-2147483648
    private const int BoolChars = 5;     // "false"

    /// <summary>
    /// The largest magnitude Money holds. Every other type's range is the range of the .NET type
    /// behind it, and each character cap above is exactly the widest form its range can print to —
    /// so a value clamped by <see cref="TryNormalize"/> always fits back in the field it came from.
    /// </summary>
    private const decimal MoneyLimit = 9999999999999999.99m;

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
    /// True when <paramref name="c"/> may be inserted into <paramref name="current"/> at
    /// <paramref name="caret"/> in a field of <paramref name="type"/>. This is the per-keystroke
    /// gate: it is what stops a second '.' in a Money field or a third decimal digit, before
    /// anything reaches <see cref="TryNormalize"/>.
    /// <para>
    /// Where the caret is matters, because a value is edited in the middle and not only at its end:
    /// a '-' belongs at the front of a number and nowhere else, and a ',' typed before the x of a
    /// PosXY would leave it without one. So what is tested is the whole entry the keystroke would
    /// produce, rather than the character on its own.
    /// </para>
    /// </summary>
    public static bool IsCharAllowed(DataValueType type, char c, string current, int caret)
    {
        current ??= string.Empty;
        if (current.Length >= MaxLength(type)) return false;
        if (!Text.IsValidChar(c)) return false;

        return IsPartial(type, current.Insert(Math.Clamp(caret, 0, current.Length), c.ToString()));
    }

    /// <summary>
    /// True when <paramref name="s"/> is an entry of <paramref name="type"/> or the start of one.
    /// Half-typed entries have to pass — <c>-</c>, <c>1.</c> and <c>8,</c> are each on the way to
    /// something valid — so this is deliberately weaker than <see cref="TryNormalize"/>: it says
    /// what may still be typed, not what may be stored.
    /// </summary>
    private static bool IsPartial(DataValueType type, string s)
    {
        switch (type)
        {
            case DataValueType.Text:
                return true;

            case DataValueType.Int:
                return IsPartialInt(s);

            case DataValueType.Decimal:
                return IsPartialNumber(s, -1);

            case DataValueType.Money:
                return IsPartialNumber(s, 2);

            case DataValueType.PosXY:
                int comma = s.IndexOf(',');
                if (comma < 0) return IsPartialInt(s);

                // ',' closes the x part, so it needs an x to close and may only appear once.
                string x = s.Substring(0, comma);
                return x.Length > 0 && x != "-"
                    && s.IndexOf(',', comma + 1) < 0
                    && IsPartialInt(x)
                    && IsPartialInt(s.Substring(comma + 1));

            default:
                return s.Length == 0;   // Bool is toggled, never typed.
        }
    }

    /// <summary>An optional leading '-' and then digits, with any number of either still to come.</summary>
    private static bool IsPartialInt(string s)
    {
        for (int i = s.Length > 0 && s[0] == '-' ? 1 : 0; i < s.Length; i++)
        {
            if (!char.IsDigit(s[i])) return false;
        }
        return true;
    }

    /// <summary>
    /// The same, plus at most one '.' — and at most <paramref name="maxDecimals"/> digits after it
    /// when that is not negative.
    /// </summary>
    private static bool IsPartialNumber(string s, int maxDecimals)
    {
        int dot = -1;
        for (int i = s.Length > 0 && s[0] == '-' ? 1 : 0; i < s.Length; i++)
        {
            if (s[i] == '.')
            {
                if (dot >= 0) return false;
                dot = i;
                continue;
            }
            if (!char.IsDigit(s[i])) return false;
        }

        return maxDecimals < 0 || dot < 0 || s.Length - dot - 1 <= maxDecimals;
    }

    /// <summary>
    /// Turns a raw entry into its stored form: <c>5</c> becomes <c>5.00</c> for Money, leading
    /// zeros disappear from an Int, <c>8 , 40</c> becomes <c>8,40</c>, and surrounding spaces come
    /// off every type. Returns false when the entry cannot be read as <paramref name="type"/> at
    /// all, in which case the caller keeps the old value.
    /// <para>
    /// A number that reads as its type but falls outside its range is clamped to the nearest edge
    /// rather than refused. A hand-edited <c>99999999999</c> is an Int the file cannot hold, not an
    /// entry that failed to be one, and the nearest Int says more than putting the old value back.
    /// </para>
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
                string clean = Text.Sanitize(raw).Trim();
                int max = MaxLength(type);
                normalized = clean.Length > max ? clean.Substring(0, max) : clean;
                return true;

            case DataValueType.Int:
                if (!TryReadInt(raw, out int i)) return false;
                normalized = i.ToString(CultureInfo.InvariantCulture);
                return true;

            case DataValueType.Decimal:
                if (!double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d)
                    || double.IsNaN(d))
                    return false;

                // Overflow parses as an infinity, which has no stored form. The edge of the range has.
                if (double.IsInfinity(d)) d = d > 0 ? double.MaxValue : double.MinValue;
                normalized = d.ToString("R", CultureInfo.InvariantCulture);
                return true;

            case DataValueType.Money:
                if (!TryReadMoney(raw, out decimal m)) return false;
                normalized = m.ToString("0.00", CultureInfo.InvariantCulture);
                return true;

            case DataValueType.PosXY:
                string[] parts = raw.Split(',');
                if (parts.Length != 2) return false;
                if (!TryReadInt(parts[0], out int x) || !TryReadInt(parts[1], out int y)) return false;
                normalized = x.ToString(CultureInfo.InvariantCulture) + "," + y.ToString(CultureInfo.InvariantCulture);
                return true;

            default:
                string b = raw.Trim();
                if (string.Equals(b, "true", StringComparison.OrdinalIgnoreCase)) { normalized = "true"; return true; }
                if (string.Equals(b, "false", StringComparison.OrdinalIgnoreCase)) { normalized = "false"; return true; }
                return false;
        }
    }

    /// <summary>
    /// Reads a whole number, holding one that is too big for an Int at the edge of the range rather
    /// than refusing it. Anything that is not a whole number at all — a stray letter, a lone '-', the
    /// empty half of a PosXY — still fails, because there is no nearest Int to those to hold it at.
    /// </summary>
    private static bool TryReadInt(string raw, out int value)
    {
        string s = raw.Trim();
        if (int.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value)) return true;

        if (!IsPartialInt(s) || !HasDigit(s)) return false;
        value = s[0] == '-' ? int.MinValue : int.MaxValue;
        return true;
    }

    /// <summary>
    /// The same for Money, which is clamped to <see cref="MoneyLimit"/> and then rounded to the two
    /// decimals it stores. Clamping comes first so the rounding can never overflow the decimal.
    /// </summary>
    private static bool TryReadMoney(string raw, out decimal value)
    {
        value = 0m;
        string s = raw.Trim();

        if (!decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            // More digits than a decimal itself can hold. It is still a number, so it still clamps.
            if (!IsPartialNumber(s, -1) || !HasDigit(s)) return false;
            value = s[0] == '-' ? -MoneyLimit : MoneyLimit;
        }

        value = Math.Round(Math.Clamp(value, -MoneyLimit, MoneyLimit), 2, MidpointRounding.AwayFromZero);
        return true;
    }

    private static bool HasDigit(string s)
    {
        foreach (char c in s)
        {
            if (char.IsDigit(c)) return true;
        }
        return false;
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
