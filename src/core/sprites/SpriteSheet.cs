namespace mono8.core.sprites;

internal class SpriteSheet
{
    public int[,] Data;
    public Texture2D[] ColorTextures = new Texture2D[Constants.GameDataSizes.ColorPalette];

    public Rectangle[] TileRects;

    public byte[] Flags = new byte[256];

    public int GetFlags(int spriteId) =>
        spriteId >= 0 && spriteId < 256 ? Flags[spriteId] : 0;

    public bool GetFlag(int spriteId, int flag) =>
        spriteId >= 0 && spriteId < 256 && (Flags[spriteId] & (1 << flag)) != 0;

    public void SetFlag(int spriteId, int flag, bool value)
    {
        if (spriteId < 0 || spriteId >= 256) return;
        if (value) Flags[spriteId] |= (byte)(1 << flag);
        else Flags[spriteId] &= (byte)~(1 << flag);
    }

    public void SetFlags(int spriteId, int value)
    {
        if (spriteId >= 0 && spriteId < 256) Flags[spriteId] = (byte)value;
    }

    public void LoadSprites(string[] sheet, string[] flags)
    {
        LoadData(sheet);
        var defaultFlags = new string('0', 256);
        string line0 = flags != null && flags.Length > 0
        ? flags[0]
        : defaultFlags;

        string line1 = flags != null && flags.Length > 1
            ? flags[1]
            : defaultFlags;
        LoadFlags(line0, line1);
        CalculateTileRects();
        DataToTexture();
    }

    public void LoadFlags(string line0, string line1 = null)
    {
        LoadFlagsLine(line0, 0);
        if (line1 != null) LoadFlagsLine(line1, 128);
    }

    private void LoadFlagsLine(string line, int spriteOffset)
    {
        for (int i = 0; i < 128 && i * 2 + 1 < line.Length; i++)
        {
            int hi = HexNibble(line[i * 2]);
            int lo = HexNibble(line[i * 2 + 1]);
            Flags[spriteOffset + i] = (byte)(hi * 16 + lo);
        }
    }

    private static int HexNibble(char c) =>
        c >= '0' && c <= '9' ? c - '0' : c >= 'a' && c <= 'f' ? c - 'a' + 10 : c >= 'A' && c <= 'F' ? c - 'A' + 10 : 0;

    private void CalculateTileRects()
    {
        int columns = Constants.GameDataSizes.SpriteSheetColumns;
        int rows = Constants.GameDataSizes.SpriteSheetRows;
        int size = Constants.GameDataSizes.TileSize;
        int total = columns * rows;
        TileRects = new Rectangle[total];
        for (int i = 0; i < total; i++)
        {
            int x = (i % columns) * size;
            int y = (i / columns) * size;
            TileRects[i] = new Rectangle(x, y, size, size);
        }
    }

    private void LoadData(string[] sheet)
    {
        Data = new int[Constants.GameDataSizes.SpriteSheetY, Constants.GameDataSizes.SpriteSheetX];

        for (int r = 0; r < 128; r++)
        {
            for (int c = 0; c < 128; c++)
            {
                char ch = '0';

                if (sheet != null &&
                    r < sheet.Length &&
                    sheet[r] != null &&
                    c < sheet[r].Length)
                {
                    ch = char.ToLowerInvariant(sheet[r][c]);
                }

                if (ch >= '0' && ch <= '9')
                    Data[r, c] = ch - '0';
                else if (ch >= 'a' && ch <= 'f')
                    Data[r, c] = ch - 'a' + 10;
                else
                    Data[r, c] = 0;
            }
        }
    }

    public void DataToTexture()
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

    public void DrawSub(int sx, int sy, int sw, int sh, int dx, int dy, int dw, int dh, bool flipX, bool flipY)
    {
        var source = new Rectangle(sx, sy, sw, sh);
        var destination = new Rectangle(dx, dy, dw, dh);

        SpriteEffects effects = SpriteEffects.None;
        if (flipX) effects |= SpriteEffects.FlipHorizontally;
        if (flipY) effects |= SpriteEffects.FlipVertically;

        for (int ci = 0; ci < Constants.GameDataSizes.ColorPalette; ci++)
        {
            if (ColorPalette.IsDrawTransparent(ci)) continue;
            if (ColorTextures[ci] == null) continue;
            mono8.SpriteBatch.Draw(ColorTextures[ci], destination, source, effects, ci);
        }
    }

    public void Draw(
        int n, int x, int y, int w = 1, int h = 1,
        int scale = 1, bool flipX = false, bool flipY = false)
    {
        var source = new Rectangle(
            (n % Constants.GameDataSizes.SpriteSheetColumns) * Constants.GameDataSizes.TileSize,
            (n / Constants.GameDataSizes.SpriteSheetColumns) * Constants.GameDataSizes.TileSize,
            w * Constants.GameDataSizes.TileSize,
            h * Constants.GameDataSizes.TileSize);
        var destination = new Rectangle(
            x, y,
            w * Constants.GameDataSizes.TileSize * scale,
            h * Constants.GameDataSizes.TileSize * scale);

        SpriteEffects effects = SpriteEffects.None;
        if (flipX) effects |= SpriteEffects.FlipHorizontally;
        if (flipY) effects |= SpriteEffects.FlipVertically;

        for (int ci = 0; ci < Constants.GameDataSizes.ColorPalette; ci++)
        {
            if (ColorPalette.IsDrawTransparent(ci)) continue;
            if (ColorTextures[ci] == null) continue;
            mono8.SpriteBatch.Draw(ColorTextures[ci], destination, source, effects, ci);
        }
    }
}