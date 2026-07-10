namespace mono8.core.graphics;

/// <summary>
/// Shared loading and texturing for the two character-encoded pixel grids on disk:
/// the sprite sheet and the icon sheet.
/// </summary>
internal static class PixelGrid
{
    /// <summary>
    /// Decodes a grid of base-32 palette digits. Rows and columns missing from
    /// <paramref name="sheet"/> decode as colour 0, so a short or absent file still loads.
    /// <paramref name="padding"/> adds blank cells past the right and bottom edges.
    /// </summary>
    public static int[,] Load(string[] sheet, int width, int height, int padding = 0)
    {
        var data = new int[height + padding, width + padding];

        for (int r = 0; r < height; r++)
        {
            for (int c = 0; c < width; c++)
            {
                char ch = '0';

                if (sheet != null && r < sheet.Length && sheet[r] != null && c < sheet[r].Length)
                    ch = char.ToLowerInvariant(sheet[r][c]);

                data[r, c] = ColorPalette.CharToIndex(ch);
            }
        }

        return data;
    }

    /// <summary>
    /// Rebuilds one white-on-transparent mask texture per palette colour. Drawing a mask tinted
    /// with its colour is what lets <c>pal</c> and <c>palt</c> remap colours at draw time.
    /// </summary>
    public static void WriteColorMasks(int[,] data, Texture2D[] textures)
    {
        int width = data.GetLength(1);
        int height = data.GetLength(0);
        var maskData = new Color[width * height];

        for (int ci = 0; ci < Constants.GameDataSizes.ColorPalette; ci++)
        {
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    maskData[y * width + x] = data[y, x] == ci ? Color.White : ColorPalette.TransparentColor;

            textures[ci] ??= new Texture2D(Mono8Game.GraphicsDeviceRef, width, height);
            textures[ci].SetData(maskData);
        }
    }
}
