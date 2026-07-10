namespace mono8.core.icons;

internal class IconSheet
{
    private const int Columns = Constants.GameDataSizes.IconSheetX / Constants.GameDataSizes.TileSize;

    public static int[,] Data;
    public static Texture2D[] ColorTextures = new Texture2D[Constants.GameDataSizes.ColorPalette];

    public static void LoadIcons(string[] sheet)
    {
        Data = PixelGrid.Load(sheet, Constants.GameDataSizes.IconSheetX, Constants.GameDataSizes.IconSheetY);
        PixelGrid.WriteColorMasks(Data, ColorTextures);
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
            Mono8Game.SpriteBatch.Draw(ColorTextures[ci], destination, source, SpriteEffects.None, ci);
        }
    }
}
