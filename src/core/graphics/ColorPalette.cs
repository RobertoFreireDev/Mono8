namespace mono8.core.graphics;

public static class ColorPalette
{
    private static Color[] Colors = new Color[Constants.GameDataSizes.ColorPalette];
    public static readonly Color TransparentColor = new Color(0, 0, 0, 0);
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

    // A pixel's palette index is stored as a single base-32 digit: '0'-'9' then 'a'-'v'.
    public static char IndexToChar(int value) => (char)(value < 10 ? '0' + value : 'a' + value - 10);

    public static int CharToIndex(char c) =>
        c >= '0' && c <= '9' ? c - '0' : c >= 'a' && c <= 'v' ? c - 'a' + 10 : 0;

    public static void PaltReset() => ResetPaltFlags();

    private static readonly int[] _suspendedDrawPalette = new int[Constants.GameDataSizes.ColorPalette];
    private static readonly bool[] _suspendedPaltFlags = new bool[Constants.GameDataSizes.ColorPalette];

    /// <summary>
    /// Stashes the remap and transparency flags and puts the defaults back, so engine chrome drawn
    /// after a frame is not tinted or erased by whatever <c>pal</c>/<c>palt</c> that frame left set.
    /// Not nestable: exactly one <see cref="ResumeDrawPalette"/> per call.
    /// </summary>
    public static void SuspendDrawPalette()
    {
        Array.Copy(_drawPalette, _suspendedDrawPalette, _drawPalette.Length);
        Array.Copy(_paltFlags, _suspendedPaltFlags, _paltFlags.Length);
        for (int i = 0; i < _drawPalette.Length; i++) _drawPalette[i] = i;
        ResetPaltFlags();
    }

    public static void ResumeDrawPalette()
    {
        Array.Copy(_suspendedDrawPalette, _drawPalette, _drawPalette.Length);
        Array.Copy(_suspendedPaltFlags, _paltFlags, _paltFlags.Length);
    }

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
    
    private static string Palette = "#000000,#243553,#6f3450,#346856,#92563c,#55554d,#b3bbb3,#e0f8d0,#c15c5b,#e0a237,#efe666,#88c070,#3ea4d7,#737188,#dd8096,#e9c9a5,#322824,#1e2c3f,#432d3c,#1e5254,#673931,#463a3f,#8e826f,#e6e98e,#9c3c4f,#d67840,#a7d865,#3ca34f,#165a9a,#674a5d,#db775a,#e09e7b";

    public static Color GetColor(int id, float colorOpaqueness = 1f)
    {
        if (id == BlackColorIndex)
        {
            return Color.Black * colorOpaqueness;
        }

        if (id == WhiteColorIndex)
        {
            return Color.White * colorOpaqueness;
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