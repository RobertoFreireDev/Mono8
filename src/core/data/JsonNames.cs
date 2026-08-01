namespace mono8.core.data;

/// <summary>
/// The single naming rule behind every identifier in <c>data.json</c> — group names, object
/// names and field names all obey it.
/// <para>
/// Names are upper-cased because <see cref="Text.DrawText"/> draws everything upper-case: without
/// normalisation <c>hp</c> and <c>HP</c> would be indistinguishable on screen while being two
/// different keys in the file. <c>:</c> is excluded because it separates the name from the type
/// code in the file format, and <c>"</c> / <c>\</c> so a name can never need JSON escaping.
/// </para>
/// </summary>
internal static class JsonNames
{
    public const int MaxChars = Constants.JsonData.MaxNameChars;

    /// <summary>True when <paramref name="c"/> may appear in a name; the per-key gate for text entry.</summary>
    public static bool IsValidNameChar(char c) =>
        Text.IsValidChar(c) && c != '"' && c != '\\' && c != ':' && c != ',' && c != ' ';

    /// <summary>
    /// Trims and upper-cases <paramref name="raw"/>. Fails on an empty name, one longer than
    /// <see cref="MaxChars"/>, or one holding a character <see cref="IsValidNameChar"/> rejects.
    /// </summary>
    public static bool TryNormalize(string raw, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        string trimmed = raw.Trim().ToUpperInvariant();
        if (trimmed.Length > MaxChars) return false;

        foreach (char c in trimmed)
        {
            if (!IsValidNameChar(c)) return false;
        }

        normalized = trimmed;
        return true;
    }
}
