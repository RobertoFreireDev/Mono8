namespace mono8.core.icons;

internal class IconSheet
{
    private const int Columns = Constants.GameDataSizes.IconSheetX / Constants.GameDataSizes.TileSize;

    public static int[,] Data;
    public static Texture2D[] ColorTextures = new Texture2D[Constants.GameDataSizes.ColorPalette];

    public static void LoadIcons(string[] sheet)
    {
        LoadData(sheet);
        DataToTexture();
    }

    private static void LoadData(string[] sheet)
    {
        Data = new int[Constants.GameDataSizes.IconSheetY, Constants.GameDataSizes.IconSheetX];

        for (int r = 0; r < Constants.GameDataSizes.IconSheetY; r++)
        {
            for (int c = 0; c < Constants.GameDataSizes.IconSheetX; c++)
            {
                char ch = '0';

                if (sheet != null &&
                    r < sheet.Length &&
                    sheet[r] != null &&
                    c < sheet[r].Length)
                {
                    ch = char.ToLowerInvariant(sheet[r][c]);
                }

                Data[r, c] = ColorPalette.CharToIndex(ch);
            }
        }
    }

    private static void DataToTexture()
    {
        int width = Data.GetLength(1);
        int height = Data.GetLength(0);
        int pixelCount = width * height;

        for (int ci = 0; ci < Constants.GameDataSizes.ColorPalette; ci++)
        {
            var maskData = new Color[pixelCount];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    maskData[y * width + x] = Data[y, x] == ci ? Color.White : ColorPalette.TransparentColor;

            ColorTextures[ci] ??= new Texture2D(mono8.GraphicsDeviceRef, width, height);
            ColorTextures[ci].SetData(maskData);
        }
    }

    public static void Draw(int n, int x, int y)
    {
        // ALWAYS int w = 1, int h = 1, scale = 1
        int size = Constants.GameDataSizes.TileSize;
        var source = new Rectangle(
            (n % Columns) * size,
            (n / Columns) * size,
            size,
            size);
        var destination = new Rectangle(x, y, size, size);

        for (int ci = 0; ci < Constants.GameDataSizes.ColorPalette; ci++)
        {
            if (ColorPalette.IsDrawTransparent(ci)) continue;
            if (ColorTextures[ci] == null) continue;
            mono8.SpriteBatch.Draw(ColorTextures[ci], destination, source, SpriteEffects.None, ci);
        }
    }
}
