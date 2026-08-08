namespace mono8.core.graphics;

public static class Text
{
    public static char DefaultKey = '?';

    private const int Columns = Constants.GameDataSizes.FontSheetColumns;
    private const int CharWidth = Constants.GameDataSizes.FontCharX;
    private const int CharHeight = Constants.GameDataSizes.FontCharY;

    private static readonly Dictionary<char, Texture2D> CharTextures = new Dictionary<char, Texture2D>();

    /// <summary>
    /// The glyph cells of <c>data.font</c> in reading order: cell <c>i</c> is at column
    /// <c>i % Columns</c>, row <c>i / Columns</c>. The sheet has room for
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

    /// <summary>Width in pixels of <paramref name="s"/> drawn on one line.</summary>
    public static int Width(string s) => string.IsNullOrEmpty(s) ? 0 : s.Length * CharAdvance;

    /// <summary>
    /// Cuts <c>data.font</c> into one texture per character. The sheet is a
    /// <see cref="PixelGrid"/> like the sprite and icon sheets, but its cell is 5x7 rather than the
    /// 8x8 tile everything else uses, so it is sliced on <see cref="CharWidth"/>/<see cref="CharHeight"/>
    /// and not on <c>TileSize</c>. Each glyph becomes a white-on-transparent mask that
    /// <see cref="DrawText"/> tints, so only whether a pixel is colour 0 matters — the digit the
    /// developer authored a lit pixel with does not.
    /// </summary>
    public static void LoadFont(string[] sheet)
    {
        var data = PixelGrid.Load(sheet, Constants.GameDataSizes.FontSheetX, Constants.GameDataSizes.FontSheetY);
        var pixels = new Color[CharWidth * CharHeight];

        for (int i = 0; i < _charIndexes.Count && i <= Constants.GameDataSizes.MaxFontIndex; i++)
        {
            int x = (i % Columns) * CharWidth;
            int y = (i / Columns) * CharHeight;

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
    /// </summary>
    public static void DrawText(string text, Vector2 position, int colorIndex, bool wraptext = false, int wrapLimit = 0, float colorOpaqueness = 1f, bool preserveCase = false)
    {
        string[] lines = (preserveCase ? text : text.ToUpper()).Split('\n');
        var copyPos = new Vector2(position.X, position.Y);
        int additionalLines = 0;

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
                    colorIndex,
                    colorOpaqueness);

                position += new Vector2(CharAdvance, 0);
            }
        }
    }
}
