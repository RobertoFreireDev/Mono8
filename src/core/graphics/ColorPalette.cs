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

    private static string[] Palettes = {
        "#000000,#20315a,#74285a,#0a7e56,#9b583d,#625b53,#bebfc4,#f8ede5,#e61b5c,#f0a029,#f2dd3f,#22d24f,#3baeff,#827798,#f285b0,#f0bf9f",
        "#210000,#00174d,#8f005e,#006945,#c73900,#8f4c00,#93a5ff,#fff6f0,#f5004e,#ff9800,#ffe947,#00c733,#47b3ff,#5200f5,#ff93be,#ffe0cc",
        "#222323,#2f256b,#c23a73,#788374,#aa644d,#7c6d80,#c7c6c6,#ffffff,#f42e1f,#dca456,#f5e9bf,#3dff98,#2c1e74,#382843,#ff4adc,#f0f6f0",
        "#3f1919,#282f3f,#743960,#32735d,#815d4f,#9e6728,#627cff,#ffffff,#b85071,#e5a13b,#f4dd2c,#04d138,#2ba5fb,#754dc6,#e4729f,#d2bdaf",
        "#320000,#3a4560,#933974,#577067,#a2664e,#877866,#9da1b7,#ecd4c2,#b25e79,#c69c5e,#c3b96d,#38c35c,#5bade7,#6f3ed2,#f75494,#e8b492",
        "#081820,#372a39,#7a1c4b,#346856,#aa644d,#788374,#9f9f9f,#e0f8d0,#7e1f23,#f89020,#f8f088,#88c070,#545c7e,#a3a29a,#c56981,#f5e9bf",
        "#573434,#1e3d88,#c11185,#37715d,#c95324,#817260,#8c9df4,#ffffff,#cb4d75,#e5ad5a,#f9e552,#5bb372,#6dbef7,#8b829c,#f776a9,#f7c19e",
        "#181010,#294257,#7c3f58,#6b8c42,#5a3921,#555568,#a0a08b,#ffefff,#eb6b6f,#f9a875,#fff6d3,#7bc67b,#579c9a,#84739c,#f7b58c,#ffffb5"
        };

    private static string Palette = Palettes[0];

    public static void SetTransparentColorIndex(int id)
    {
        if (id < Constants.GameDataSizes.ColorPaletteMin || id > Constants.GameDataSizes.ColorPaletteMax)
        {
            return;
        }

        TransparentColorIndex = id;
    }

    public static Color GetColor(int id) => GetColor(id, 0);

    public static Color GetColor(int id, int transparency)
    {
        Color color;

        if (id == BlackColorIndex)
        {
            color = Color.Black;
        }
        else if (id == WhiteColorIndex)
        {
            color = Color.White;
        }
        else if (id < Constants.GameDataSizes.ColorPaletteMin || id > Constants.GameDataSizes.ColorPaletteMax)
        {
            color = TransparentColor;
        }
        else
        {
            color = Colors[_drawPalette[id]];
        }

        if (transparency <= 0) return color;

        transparency = Math.Min(transparency, 10);
        int alpha = color.A * (10 - transparency) / 10;
        return new Color(color.R, color.G, color.B, alpha);
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