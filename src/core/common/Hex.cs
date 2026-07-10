namespace mono8.core.common;

/// <summary>
/// Hex decoding shared by every on-disk format (sprite flags, map cells, sfx and music rows).
/// Unrecognised characters decode as 0 so a hand-edited data file can never throw.
/// </summary>
internal static class Hex
{
    public static int Nibble(char c) =>
        c >= '0' && c <= '9' ? c - '0' :
        c >= 'a' && c <= 'f' ? c - 'a' + 10 :
        c >= 'A' && c <= 'F' ? c - 'A' + 10 : 0;

    /// <summary>The single hex digit at <paramref name="charIndex"/>.</summary>
    public static int Nibble(string s, int charIndex) => Nibble(s[charIndex]);

    /// <summary>The two hex digits starting at <paramref name="charIndex"/>.</summary>
    public static int Pair(string s, int charIndex) => Nibble(s[charIndex]) * 16 + Nibble(s[charIndex + 1]);

    /// <summary>The <paramref name="byteIndex"/>'th byte, i.e. the pair at char offset <c>byteIndex * 2</c>.</summary>
    public static int Byte(string s, int byteIndex) => Pair(s, byteIndex * 2);
}
