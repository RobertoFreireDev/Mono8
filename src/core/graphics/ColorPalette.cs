namespace mono8.core.graphics;

public static class ColorPalette
{
    private static Color[] Colors = new Color[Constants.GameDataSizes.ColorPalette];
    public static readonly Color TransparentColor = new Color(0, 0, 0, 0);
    public static int TransparentColorIndex = 0;
    public static int BlackColorIndex = -2;
    public static int WhiteColorIndex = -1;

    private static readonly int[] _drawPalette = new int[Constants.GameDataSizes.ColorPalette];
    private static readonly int[] _screenPalette = new int[Constants.GameDataSizes.ColorPalette];
    private static readonly bool[] _paltFlags = new bool[Constants.GameDataSizes.ColorPalette];

    static ColorPalette() { ResetPalettes(); ResetPaltFlags(); }

    private static void ResetPalettes()
    {
        for (int i = 0; i < Constants.GameDataSizes.ColorPalette; i++) { _drawPalette[i] = i; _screenPalette[i] = i; }
    }

    private static void ResetPaltFlags()
    {
        for (int i = 0; i < _paltFlags.Length; i++) _paltFlags[i] = (i == 0);
    }

    public static void Pal()
    {
        TransparentColorIndex = 0;
        ResetPalettes();
        ResetPaltFlags();
    }

    public static void Pal(int color1, int color2, int paletteType = 0)
    {
        if (color1 < 0 || color1 > Constants.GameDataSizes.ColorPaletteMax) return;
        if (color2 < 0 || color2 > Constants.GameDataSizes.ColorPaletteMax) return;
        if (paletteType == 0) _drawPalette[color1] = color2;
        else if (paletteType == 1) _screenPalette[color1] = color2;
    }

    public static void PaltReset() => ResetPaltFlags();

    public static void Palt(int colorIndex, bool transparent)
    {
        if (colorIndex >= 0 && colorIndex < _paltFlags.Length)
            _paltFlags[colorIndex] = transparent;
    }

    public static bool IsDrawTransparent(int colorIndex)
    {
        if (colorIndex < 0 || colorIndex > Constants.GameDataSizes.ColorPaletteMax) return true;
        return _paltFlags[_drawPalette[colorIndex]];
    }

    private static string Palette = "#000000,#1d2b53,#7e2553,#008751,#ab5236,#5f574f,#c2c3c7,#fff1e8,#ff004d,#ffa300,#ffec27,#00e436,#29adff,#83769c,#ff77a8,#ffccaa,#291814,#111d35,#422136,#125359,#742f29,#49333b,#a28879,#f3ef7d,#be1250,#ff6c24,#a8e72e,#00b543,#065ab5,#754665,#ff6e59,#ff9d81";

    public static void SetTransparentColorIndex(int id)
    {
        if (id < Constants.GameDataSizes.ColorPaletteMin || id > Constants.GameDataSizes.ColorPaletteMax)
        {
            return;
        }

        TransparentColorIndex = id;
    }

    public static Color GetColor(int id, float colorOpaqueness = 1f)
    {
        if (id == BlackColorIndex)
        {
            return Color.Black;
        }

        if (id == WhiteColorIndex)
        {
            return Color.White;
        }

        if (id < Constants.GameDataSizes.ColorPaletteMin || id > Constants.GameDataSizes.ColorPaletteMax)
        {
            return TransparentColor;
        }

        return Colors[_drawPalette[id]] * colorOpaqueness;
    }

    public static void SetColorPalette()
    {
        string[] colors = Palette.Split(',');
        for (int i = 0; i <= Constants.GameDataSizes.ColorPaletteMax; i++)
        {
            Colors[i] = GetColor(colors[i].Trim());
        }

        Color GetColor(string hexColor)
        {
            try
            {
                hexColor = hexColor.Substring(1);
                int r = Convert.ToInt32(hexColor.Substring(0, 2), 16);
                int g = Convert.ToInt32(hexColor.Substring(2, 2), 16);
                int b = Convert.ToInt32(hexColor.Substring(4, 2), 16);
                return new Color(r, g, b);
            }
            catch
            {
                return Colors[0];
            }
        }
    }
}