namespace mono8.core.graphics;

public static class Text
{
    public static char DefaultKey = '?';
    private static int Columns = 19;
    private static int CharWidth = 5;
    private static int CharHeight = 7;

    private static readonly string Base64Font = "iVBORw0KGgoAAAANSUhEUgAAAF8AAAAjCAYAAADyrNZPAAAAAXNSR0IArs4c6QAAAoJJREFUaIHtWsuOwyAMhGj//5fZS1MhrwdmbGijFXNqHPwCYztWSzn4GmoppbTW2ptQa71/t9aafbbrEK3WWnt+hXcHjfUj6m90D97E/mV7wZ6WXefRkDxGjiovQ/P0RWxm5dnnCznfR0DP6AlTDGL5WXkszfOHQcbnmV9w8xHqC+hZoRVwyEhvVN59/Rk9Hi+rm7Glx8/9A50ayvteHhtBSWHMOmUzR1E6qnFoHasDrcncxIODf4Zol2ALkFKQEO9MnsKr+BalsY1DD7ngsrBFrnV9r13n8c7kFVD8Ef9OeAWYCaLh5n+iMCgFFzm4yxb7kYTWIfvQ3t38FxJWhBYtc0BK29bbwvJl4N1W73ahIPX2rl93jRYigyLRhqII6WBaO8XmqC0sPHvYA3kUVqeTg4ODJ2HZKJahZcbMHu+oCxn54fGrvkV5ez+ufoHXKs0q9oh31qJ5nQPq1TMF8hPfHKwfPUJTTUtDBZJxTtG7u+1lfChOINyBpjYJf6aakSlftKXqP9XVKSmyhQFqNdHzrFWN2Hxw8HzsitZtg7XdGA2u7GbNBlxFyPcevU+fit4LMWdpn/hK9aaaKP/PahQCs9Gjmufpvd9Jkc9O+bIzEyUKI7IUmcxh3rJUv6cjZU+Bd+pel2Rps2uJHEM0BiM+dsjn8XjfDaptVxlEdAaZtKP027Nc7vErds1uOXqHDsmTuR2Z/P+t3pgtuFGcvy+ImBXakECUj2edjfe8GkivpbEpIWvvKn+HfxdcnZsYo71DZ7usLC1jdxioUK1Uim4SU5jROsbmkW8r9T4e0cj3eJVNRYcetXfp5q+OfDaiFXnWHja/e7Sn5PzT7YiwtSgj6xcaOqWkv64FWwAAAABJRU5ErkJggg==";
    private static Dictionary<char, Texture2D> CharTextures = new Dictionary<char, Texture2D>();
    private static List<char> _charIndexes = new List<char>()
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

    public static void GetCharacterTextures(GraphicsDevice graphicsDevice)
    {
        int columns = Columns;
        int charWidth = CharWidth;
        int charHeight = CharHeight;
        CharTextures = new Dictionary<char, Texture2D>();
        var texture = Convert64ToTexture(Base64Font);
        foreach (var charIndex in _charIndexes)
        {
            int row = (_charIndexes.IndexOf(charIndex) / columns);
            int column = (_charIndexes.IndexOf(charIndex) % columns);
            int x = column * charWidth;
            int y = row * charHeight;
            var sourceRect = new Rectangle(x, y, charWidth, charHeight);
            var characterTexture = new Texture2D(graphicsDevice, charWidth, charHeight);
            Color[] data = new Color[charWidth * charHeight];
            texture.GetData(0, sourceRect, data, 0, data.Length);
            characterTexture.SetData(data);
            CharTextures[charIndex] = characterTexture;
        }

        Texture2D Convert64ToTexture(string imageBase64)
        {
            byte[] imageBytes = Convert.FromBase64String(imageBase64);
            using var ms = new MemoryStream(imageBytes);
            return Texture2D.FromStream(graphicsDevice, ms);
        }
    }

    public static void DrawText(string text, Vector2 position, int colorIndex, bool wraptext = false, int wrapLimit = 0)
    {
        string[] lines = text.ToUpper().Split('\n');
        var copyPos = new Vector2(position.X, position.Y);
        int additionalLines = 0;

        if (wrapLimit == 0)
        {
            wrapLimit = Screen.BaseBox.Width - CharTextures[DefaultKey].Width * 4;
        }

        for (int i = 0; i < lines.Length; i++)
        {
            position = new Vector2(copyPos.X, copyPos.Y + (i + additionalLines) * 9);

            for (int j = 0; j < lines[i].Length; j++)
            {
                char key = lines[i][j];

                var charTexture = CharTextures.ContainsKey(key) ? CharTextures[key] : CharTextures[DefaultKey];

                if (wraptext && position.X >= wrapLimit)
                {
                    additionalLines++;
                    position = new Vector2(copyPos.X, copyPos.Y + (i + additionalLines) * 9);
                }

                if (key == '\t')
                {
                    position += new Vector2(charTexture.Width * 4, 0);
                    continue;
                }

                if (key == '\r')
                {
                    continue;
                }

                mono8.SpriteBatch.Draw(
                    charTexture,
                    new Vector2((int)position.X, (int)position.Y),
                    colorIndex);

                position += new Vector2((charTexture.Width - 1), 0);
            }
        }
    }
}
