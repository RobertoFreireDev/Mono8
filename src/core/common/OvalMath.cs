namespace mono8.core.common;

// Trigonometric oval outline/fill, sampled by angle so the border stays
// gapless for any aspect ratio. A square bounding box produces a circle.
internal static class OvalMath
{
    public static void DrawOutline(int x0, int y0, int x1, int y1, Action<int, int> setPixel)
    {
        int rx0 = Math.Min(x0, x1);
        int ry0 = Math.Min(y0, y1);
        int rx1 = Math.Max(x0, x1);
        int ry1 = Math.Max(y0, y1);

        if (rx1 - rx0 <= 1 && ry1 - ry0 <= 1)
        {
            for (int x = rx0; x <= rx1; x++)
                for (int y = ry0; y <= ry1; y++)
                    setPixel(x, y);
            return;
        }

        int xC = (int)Math.Ceiling((rx0 + rx1) / 2.0);
        int yC = (int)Math.Ceiling((ry0 + ry1) / 2.0);
        int evenX = (rx0 + rx1) % 2;
        int evenY = (ry0 + ry1) % 2;
        int rX = rx1 - xC;
        int rY = ry1 - yC;

        var pixels = new List<Point>();
        for (int x = rx0; x <= xC; x++)
        {
            double angle = Math.Acos((x - xC) / (double)rX);
            int y = (int)Math.Round(rY * Math.Sin(angle) + yC);

            pixels.Add(new Point(x - evenX, y));
            pixels.Add(new Point(x - evenX, 2 * yC - y - evenY));
            pixels.Add(new Point(2 * xC - x, y));
            pixels.Add(new Point(2 * xC - x, 2 * yC - y - evenY));
        }
        for (int y = ry0; y <= yC; y++)
        {
            double angle = Math.Asin((y - yC) / (double)rY);
            int x = (int)Math.Round(rX * Math.Cos(angle) + xC);

            pixels.Add(new Point(x, y - evenY));
            pixels.Add(new Point(2 * xC - x - evenX, y - evenY));
            pixels.Add(new Point(x, 2 * yC - y));
            pixels.Add(new Point(2 * xC - x - evenX, 2 * yC - y));
        }

        foreach (var p in pixels)
        {
            if (p.X < rx0 || p.X > rx1 || p.Y < ry0 || p.Y > ry1) continue;
            setPixel(p.X, p.Y);
        }
    }

    private static readonly RowSpans FillSpans = new RowSpans();

    public static void DrawFill(int x0, int y0, int x1, int y1, Action<int, int, int> fillRow)
    {
        int ry0 = Math.Min(y0, y1);
        int ry1 = Math.Max(y0, y1);

        ComputeSpans(x0, y0, x1, y1, ry0, ry1, FillSpans);

        for (int y = ry0; y <= ry1; y++)
            if (FillSpans.TryGet(y, out int left, out int right))
                fillRow(y, left, right);
    }

    /// <summary>
    /// Writes the inclusive x span of every scanline of the filled oval into <paramref name="spans"/>,
    /// keeping only rows within <paramref name="clipTop"/>..<paramref name="clipBottom"/> so an oval far
    /// larger than the screen still costs one span per visible row. Sampling matches
    /// <see cref="DrawFill"/> exactly, so a span lines up with the pixels ovalfill would paint.
    /// </summary>
    public static void ComputeSpans(int x0, int y0, int x1, int y1, int clipTop, int clipBottom, RowSpans spans)
    {
        int rx0 = Math.Min(x0, x1);
        int ry0 = Math.Min(y0, y1);
        int rx1 = Math.Max(x0, x1);
        int ry1 = Math.Max(y0, y1);

        int top = Math.Max(ry0, clipTop);
        int bottom = Math.Min(ry1, clipBottom);

        spans.Reset(top, bottom - top + 1);
        if (spans.Count == 0) return;

        if (rx1 - rx0 <= 1 || ry1 - ry0 <= 1)
        {
            for (int y = top; y <= bottom; y++)
                spans.Set(y, rx0, rx1);
            return;
        }

        int xC = (int)Math.Ceiling((rx0 + rx1) / 2.0);
        int yC = (int)Math.Ceiling((ry0 + ry1) / 2.0);
        int evenX = (rx0 + rx1) % 2;
        int evenY = (ry0 + ry1) % 2;
        int rX = rx1 - xC;
        int rY = ry1 - yC;

        for (int x = rx0; x <= xC; x++)
        {
            double angle = Math.Acos((x - xC) / (double)rX);
            int y = (int)Math.Round(rY * Math.Sin(angle) + yC);

            spans.Add(y, x - evenX);
            spans.Add(2 * yC - y - evenY, x - evenX);
            spans.Add(y, 2 * xC - x);
            spans.Add(2 * yC - y - evenY, 2 * xC - x);
        }
        for (int y = ry0; y <= yC; y++)
        {
            double angle = Math.Asin((y - yC) / (double)rY);
            int x = (int)Math.Round(rX * Math.Cos(angle) + xC);

            spans.Add(y - evenY, x);
            spans.Add(y - evenY, 2 * xC - x - evenX);
            spans.Add(2 * yC - y, x);
            spans.Add(2 * yC - y, 2 * xC - x - evenX);
        }

        // A row whose leftmost sample landed outside the box is one the sampling never
        // reached properly; the rest are clamped to the box.
        for (int i = 0; i < spans.Count; i++)
        {
            int left = spans.Left[i];
            int right = spans.Right[i];

            // Empty rows are normalised so that callers merging equal rows see them as one.
            if (left > right || left < rx0 || left > rx1) spans.Clear(spans.Top + i);
            else spans.Set(spans.Top + i, Math.Max(left, rx0), Math.Min(right, rx1));
        }
    }
}
