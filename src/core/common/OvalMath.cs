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

    public static void DrawFill(int x0, int y0, int x1, int y1, Action<int, int, int> fillRow)
    {
        int rx0 = Math.Min(x0, x1);
        int ry0 = Math.Min(y0, y1);
        int rx1 = Math.Max(x0, x1);
        int ry1 = Math.Max(y0, y1);

        if (rx1 - rx0 <= 1 || ry1 - ry0 <= 1)
        {
            for (int y = ry0; y <= ry1; y++)
                fillRow(y, rx0, rx1);
            return;
        }

        int xC = (int)Math.Ceiling((rx0 + rx1) / 2.0);
        int yC = (int)Math.Ceiling((ry0 + ry1) / 2.0);
        int evenX = (rx0 + rx1) % 2;
        int evenY = (ry0 + ry1) % 2;
        int rX = rx1 - xC;
        int rY = ry1 - yC;

        var linePixels = new Dictionary<int, List<int>>();

        void AddPixel(int x, int y)
        {
            if (!linePixels.TryGetValue(y, out var xs))
            {
                xs = new List<int>();
                linePixels[y] = xs;
            }
            xs.Add(x);
        }

        for (int x = rx0; x <= xC; x++)
        {
            double angle = Math.Acos((x - xC) / (double)rX);
            int y = (int)Math.Round(rY * Math.Sin(angle) + yC);

            AddPixel(x - evenX, y);
            AddPixel(x - evenX, 2 * yC - y - evenY);
            AddPixel(2 * xC - x, y);
            AddPixel(2 * xC - x, 2 * yC - y - evenY);
        }
        for (int y = ry0; y <= yC; y++)
        {
            double angle = Math.Asin((y - yC) / (double)rY);
            int x = (int)Math.Round(rX * Math.Cos(angle) + xC);

            AddPixel(x, y - evenY);
            AddPixel(2 * xC - x - evenX, y - evenY);
            AddPixel(x, 2 * yC - y);
            AddPixel(2 * xC - x - evenX, 2 * yC - y);
        }

        foreach (var (y, xs) in linePixels)
        {
            if (y < ry0 || y > ry1) continue;
            xs.Sort();
            int minX = xs[0];
            int maxX = xs[^1];
            if (minX < rx0 || minX > rx1) continue;

            fillRow(y, Math.Max(minX, rx0), Math.Min(maxX, rx1));
        }
    }
}
