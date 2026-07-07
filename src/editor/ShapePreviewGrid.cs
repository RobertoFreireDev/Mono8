namespace mono8.editor;

// Holds an uncommitted shape preview as a sparse pixel buffer, so shape tools
// can be drawn on screen while dragging without touching the real spritesheet.
internal class ShapePreviewGrid
{
    private readonly Dictionary<(int x, int y), int> pixels = new();

    public bool HasPixels => pixels.Count > 0;

    public void Clear() => pixels.Clear();

    public void SetPixel(int x, int y, int colorIndex)
    {
        if (x < 0 || x >= Constants.GameDataSizes.SpriteSheetX ||
            y < 0 || y >= Constants.GameDataSizes.SpriteSheetY) return;

        pixels[(x, y)] = colorIndex;
    }

    public void SetRectFill(int x, int y, int w, int h, int colorIndex)
    {
        for (int yy = y; yy < y + h; yy++)
            for (int xx = x; xx < x + w; xx++)
                SetPixel(xx, yy, colorIndex);
    }

    public void SetRect(int x, int y, int w, int h, int colorIndex)
    {
        const int thickness = 1;
        SetRectFill(x, y, w, thickness, colorIndex);
        SetRectFill(x, y + h - thickness, w, thickness, colorIndex);
        SetRectFill(x, y + 1, thickness, h - 2, colorIndex);
        SetRectFill(x + w - thickness, y + 1, thickness, h - 2, colorIndex);
    }

    public void SetOval(int x0, int y0, int x1, int y1, int colorIndex) =>
        OvalMath.DrawOutline(x0, y0, x1, y1, (px, py) => SetPixel(px, py, colorIndex));

    public void SetOvalFill(int x0, int y0, int x1, int y1, int colorIndex) =>
        OvalMath.DrawFill(x0, y0, x1, y1, (row, leftX, rightX) =>
            SetRectFill(leftX, row, rightX - leftX + 1, 1, colorIndex));

    public void Draw(IMono8API api, int regionX, int regionY, int screenX, int screenY, int scale)
    {
        foreach (var ((x, y), colorIndex) in pixels)
        {
            int px = screenX + (x - regionX) * scale;
            int py = screenY + (y - regionY) * scale;
            api.rectfill(px, py, px + scale - 1, py + scale - 1, colorIndex);
        }
    }
}
