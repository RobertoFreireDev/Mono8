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

    // "#000000,#20315a,#74285a,#0a7e56,#9b583d,#625b53,#bebfc4,#f8ede5,#e61b5c,#f0a029,#f2dd3f,#22d24f,#3baeff,#827798,#f285b0,#f0bf9f"
    private static string Palette = "#000000,#23345d,#7b2e61,#117d58,#95583f,#645d55,#bfc0c8,#f5eae2,#db2962,#e9a135,#ecd949,#31c757,#46aaf1,#84789a,#ed88af,#edc0a2";

    public static void SetTransparentColorIndex(int id)
    {
        if (id < Constants.GameDataSizes.ColorPaletteMin || id > Constants.GameDataSizes.ColorPaletteMax)
        {
            return;
        }

        TransparentColorIndex = id;
    }

    public static Color GetColor(int id)
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

        return Colors[_drawPalette[id]];
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