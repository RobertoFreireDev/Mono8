namespace mono8.core.common;

internal static class RectMath
{
    /// <summary>
    /// Walks the four one-pixel edges of a rectangle, handing each to <paramref name="fillRect"/>
    /// as (x, y, width, height). The left and right edges stop short of the corners, which the
    /// top and bottom edges already cover.
    /// </summary>
    public static void Outline(int x, int y, int w, int h, Action<int, int, int, int> fillRect)
    {
        const int thickness = 1;
        fillRect(x, y, w, thickness);                          // top
        fillRect(x, y + h - thickness, w, thickness);          // bottom
        fillRect(x, y + 1, thickness, h - 2);                  // left
        fillRect(x + w - thickness, y + 1, thickness, h - 2);  // right
    }
}
