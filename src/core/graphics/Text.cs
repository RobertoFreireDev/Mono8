namespace mono8.core.graphics;

public static class Text
{
    public static char DefaultKey = '?';

    private const int Columns = Constants.GameDataSizes.FontSheetColumns;
    private const int CharWidth = Constants.GameDataSizes.FontCharX;
    private const int CharHeight = Constants.GameDataSizes.FontCharY;
    private const int OriginY = Constants.GameDataSizes.FontOriginY;

    private static readonly Dictionary<char, Texture2D> CharTextures = new Dictionary<char, Texture2D>();

    /// <summary>
    /// The glyph cells of <c>data.font</c> in reading order: cell <c>i</c> is at column
    /// <c>i % Columns</c>, row <c>i / Columns</c>, so the 51 cells of a row are read across before
    /// the next row starts. The sheet has room for
    /// <see cref="Constants.GameDataSizes.MaxFontIndex"/> + 1 cells, so a new character is a glyph
    /// drawn in the next free cell plus its entry appended here.
    /// </summary>
    private static readonly List<char> _charIndexes = new List<char>()
    {
        '0','1','2','3','4','5','6','7','8','9',
        'A','B','C','D','E','F','G','H','I','J','K',
        'L','M','N','O','P','Q','R','S','T','U','V',
        'W','X','Y','Z',
        'a','b','c','d','e','f','g','h','i','j','k',
        'l','m','n','o','p','q','r','s','t','u','v',
        'w','x','y','z',
        ',','.',':',';','[',']','{','}',
        '|','#','$','%','(',')','!','?',
        '"','\'','_','+','-','=','*','/','\\',
        '<','>',' ','~','Ꮖ'
    };

    private static readonly HashSet<char> _validChars = new HashSet<char>(_charIndexes);

    /// <summary>Horizontal advance of one character: <see cref="CharWidth"/> minus the 1 px overlap.</summary>
    public const int CharAdvance = CharWidth - 1;

    /// <summary>Vertical advance of one text line, as used by <see cref="DrawText"/>.</summary>
    public const int LineHeight = CharHeight + 2;

    /// <summary>True when the font atlas can draw <paramref name="c"/>; anything else prints as '?'.</summary>
    public static bool IsValidChar(char c) => _validChars.Contains(c);

    /// <summary>Drops every character the font cannot draw, so stored data is always renderable.</summary>
    public static string Sanitize(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;

        var sb = new System.Text.StringBuilder(s.Length);
        foreach (char c in s)
        {
            if (IsValidChar(c)) sb.Append(c);
        }
        return sb.Length == s.Length ? s : sb.ToString();
    }

    /// <summary>
    /// Width in pixels of <paramref name="s"/> drawn on one line, with the colour markers
    /// <see cref="DrawText"/> eats discounted so a marked-up string still centres and still sizes a
    /// button correctly.
    /// </summary>
    public static int Width(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0;

        int drawn = 0;
        int unused = 0;

        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == ColorMarker)
            {
                int consumed = ReadColorMarker(s, i, 0, ref unused);
                if (consumed > 0)
                {
                    i += consumed - 1;
                    continue;
                }

                if (i + 1 < s.Length && s[i + 1] == ColorMarker) i++;
            }

            drawn++;
        }

        return drawn * CharAdvance;
    }

    /// <summary>Introduces an inline colour change; doubled it is a literal '#'.</summary>
    private const char ColorMarker = '#';

    /// <summary>
    /// Reads the colour marker starting at <paramref name="i"/>, which is known to be a
    /// <see cref="ColorMarker"/>: <c>#XX</c> switches to palette index <c>XX</c> (two digits, always
    /// zero-padded, 00 to <see cref="Constants.GameDataSizes.ColorPaletteMax"/>) and <c>#--</c> goes
    /// back to the colour the draw was called with. Returns how many characters the marker spans, or
    /// 0 when this '#' is not one — an out-of-range or malformed index is text like any other, so
    /// nothing a developer types can be silently swallowed.
    /// </summary>
    private static int ReadColorMarker(string s, int i, int baseColor, ref int color)
    {
        if (i + 2 >= s.Length) return 0;

        char high = s[i + 1];
        char low = s[i + 2];

        if (high == '-' && low == '-')
        {
            color = baseColor;
            return 3;
        }

        if (high < '0' || high > '9' || low < '0' || low > '9') return 0;

        int index = (high - '0') * 10 + (low - '0');
        if (index > Constants.GameDataSizes.ColorPaletteMax) return 0;

        color = index;
        return 3;
    }

    /// <summary>
    /// Cuts <c>data.font</c> into one texture per character. The sheet is a <see cref="PixelGrid"/>
    /// the same 256x240 as the sprite sheet — so it can be pasted over <c>data.gfx</c> and drawn in
    /// the Sprite Editor — but its cell is 5x7 rather than the 8x8 tile everything else uses, so it
    /// is sliced on <see cref="CharWidth"/>/<see cref="CharHeight"/> and not on <c>TileSize</c>.
    /// The grid starts at <see cref="OriginY"/> to clear sprite 0's tile, which that editor holds
    /// blank and will not let anything be drawn into. Each glyph becomes a white-on-transparent
    /// mask that <see cref="DrawText"/> tints, so only whether a pixel is colour 0 matters — the
    /// digit the developer authored a lit pixel with does not.
    /// </summary>
    public static void LoadFont(string[] sheet)
    {
        var data = PixelGrid.Load(sheet, Constants.GameDataSizes.FontSheetX, Constants.GameDataSizes.FontSheetY);
        var pixels = new Color[CharWidth * CharHeight];

        for (int i = 0; i < _charIndexes.Count && i <= Constants.GameDataSizes.MaxFontIndex; i++)
        {
            int x = (i % Columns) * CharWidth;
            int y = OriginY + (i / Columns) * CharHeight;

            for (int py = 0; py < CharHeight; py++)
                for (int px = 0; px < CharWidth; px++)
                    pixels[py * CharWidth + px] =
                        data[y + py, x + px] == 0 ? ColorPalette.TransparentColor : Color.White;

            char key = _charIndexes[i];
            if (!CharTextures.TryGetValue(key, out var texture))
            {
                texture = new Texture2D(Mono8Game.GraphicsDeviceRef, CharWidth, CharHeight);
                CharTextures[key] = texture;
            }

            texture.SetData(pixels);
        }
    }

    /// <summary>
    /// Draws <paramref name="text"/>, folded to upper case unless <paramref name="preserveCase"/>
    /// says otherwise. The atlas carries a full lower-case alphabet, but the fold is what every
    /// caller has always got and what <c>print</c> is documented to give a game, so it stays the
    /// default and only the JSON editor's Text values opt out — those are the one thing on screen
    /// whose case the developer typed and the file keeps.
    ///
    /// <para><paramref name="text"/> may recolour itself as it goes: <c>#XX</c> switches to palette
    /// index <c>XX</c> for everything after it, <c>#--</c> goes back to <paramref name="colorIndex"/>,
    /// and <c>##</c> draws a literal '#'. The switch carries across a '\n', so a whole wrapped
    /// paragraph can be tinted by one marker. See <see cref="ReadColorMarker"/>.</para>
    /// </summary>
    public static void DrawText(string text, Vector2 position, int colorIndex, bool wraptext = false, int wrapLimit = 0, float colorOpaqueness = 1f, bool preserveCase = false)
    {
        string[] lines = (preserveCase ? text : text.ToUpper()).Split('\n');
        var copyPos = new Vector2(position.X, position.Y);
        int additionalLines = 0;
        int color = colorIndex;

        if (wrapLimit == 0)
        {
            wrapLimit = Screen.BaseBox.Width - CharWidth * 4;
        }

        for (int i = 0; i < lines.Length; i++)
        {
            position = new Vector2(copyPos.X, copyPos.Y + (i + additionalLines) * LineHeight);

            for (int j = 0; j < lines[i].Length; j++)
            {
                char key = lines[i][j];

                if (wraptext && position.X >= wrapLimit)
                {
                    additionalLines++;
                    position = new Vector2(copyPos.X, copyPos.Y + (i + additionalLines) * LineHeight);
                }

                if (key == '\t')
                {
                    position += new Vector2(CharWidth * 4, 0);
                    continue;
                }

                if (key == '\r')
                {
                    continue;
                }

                if (key == ColorMarker)
                {
                    int consumed = ReadColorMarker(lines[i], j, colorIndex, ref color);
                    if (consumed > 0)
                    {
                        j += consumed - 1;
                        continue;
                    }

                    // Not a marker, so this is the '##' escape or a lone '#'; either way one glyph.
                    if (j + 1 < lines[i].Length && lines[i][j + 1] == ColorMarker) j++;
                }

                // A font sheet the developer has not drawn a glyph into still has its (blank)
                // texture, so this only misses when the character is not in the index at all.
                if (!CharTextures.TryGetValue(key, out var charTexture) &&
                    !CharTextures.TryGetValue(DefaultKey, out charTexture))
                {
                    position += new Vector2(CharAdvance, 0);
                    continue;
                }

                Mono8Game.SpriteBatch.Draw(
                    charTexture,
                    new Vector2((int)position.X, (int)position.Y),
                    color,
                    colorOpaqueness);

                position += new Vector2(CharAdvance, 0);
            }
        }
    }
}
