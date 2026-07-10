namespace mono8.core.graphics;

public class PixelledSpriteBatch
{
    private SpriteBatch _spriteBatch;

    public static Texture2D PixelTexture;

    public PixelledSpriteBatch(GraphicsDevice gd)
    {
        _spriteBatch = new SpriteBatch(gd);
        PixelTexture = new Texture2D(gd, 1, 1);
        PixelTexture.SetData(new Color[] { Color.White });
    }

    public void Begin(SpriteSortMode sort, BlendState blendState, SamplerState sampleState, Effect effect)
    {
        _spriteBatch.Begin(sort, blendState, sampleState, null, null, effect);
    }

    public void Begin(SamplerState sampleState)
    {
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
    }

    public void Begin()
    {
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: Camera2D.GetViewMatrix());
    }

    public void End()
    {
        _spriteBatch.End();
    }

    public void DrawPixel(int x, int y, int colorIndex, float colorOpaqueness = 1f)
    {
        _spriteBatch.Draw(PixelTexture, new Rectangle(x, y, 1, 1), ColorPalette.GetColor(colorIndex, colorOpaqueness));
    }

    public void DrawRect(int x, int y, int width, int height, int colorIndex, float colorOpaqueness = 1f)
    {
        RectMath.Outline(x, y, width, height,
            (rx, ry, rw, rh) => DrawRectFill(rx, ry, rw, rh, colorIndex, colorOpaqueness));
    }

    public void DrawBaseBox(int colorIndex)
    {
        _spriteBatch.Draw(PixelTexture, 
            new Rectangle(
                Screen.BaseBox.X,
                Screen.BaseBox.Y,
                Screen.BaseBox.Width,
                Screen.BaseBox.Height),
            ColorPalette.GetColor(colorIndex));
    }

    public void DrawRectFill(int x, int y, int width, int height, int colorIndex, float colorOpaqueness = 1f)
    {
        _spriteBatch.Draw(PixelTexture, new Rectangle(x, y, width, height), ColorPalette.GetColor(colorIndex, colorOpaqueness));
    }

    public void Draw(Texture2D texture, Rectangle destination, Rectangle source, SpriteEffects effects, int colorId, float colorOpaqueness = 1f)
    {
        _spriteBatch.Draw(
            texture, destination, source, ColorPalette.GetColor(colorId, colorOpaqueness), 0f, Vector2.Zero, effects, 0f);
    }

    public void Draw(RenderTarget2D sceneTarget, Rectangle boxToDraw, int colorIndex)
    {
        _spriteBatch.Draw(sceneTarget, boxToDraw, ColorPalette.GetColor(colorIndex));
    }

    public void DrawLine(int x0, int y0, int x1, int y1, int colorIndex)
    {
        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        while (true)
        {
            DrawPixel(x0, y0, colorIndex);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    public void DrawCirc(int cx, int cy, int r, int colorIndex, float colorOpaqueness = 1f)
    {
        int x = 0, y = r, d = 3 - 2 * r;
        while (y >= x)
        {
            DrawPixel(cx + x, cy + y, colorIndex, colorOpaqueness); DrawPixel(cx - x, cy + y, colorIndex, colorOpaqueness);
            DrawPixel(cx + x, cy - y, colorIndex, colorOpaqueness); DrawPixel(cx - x, cy - y, colorIndex, colorOpaqueness);
            DrawPixel(cx + y, cy + x, colorIndex, colorOpaqueness); DrawPixel(cx - y, cy + x, colorIndex, colorOpaqueness);
            DrawPixel(cx + y, cy - x, colorIndex, colorOpaqueness); DrawPixel(cx - y, cy - x, colorIndex, colorOpaqueness);
            if (d > 0) { y--; d += 4 * (x - y) + 10; } else { d += 4 * x + 6; }
            x++;
        }
    }

    public void DrawCircFill(int cx, int cy, int r, int colorIndex, float colorOpaqueness = 1f)
    {
        int[] minX = new int[r * 2 + 1];
        int[] maxX = new int[r * 2 + 1];
        for (int i = 0; i < minX.Length; i++) { minX[i] = int.MaxValue; maxX[i] = int.MinValue; }

        void Mark(int px, int py)
        {
            int row = py - (cy - r);
            if (row < 0 || row >= minX.Length) return;
            if (px < minX[row]) minX[row] = px;
            if (px > maxX[row]) maxX[row] = px;
        }

        int x = 0, y = r, d = 3 - 2 * r;
        while (y >= x)
        {
            Mark(cx + x, cy + y); Mark(cx - x, cy + y);
            Mark(cx + x, cy - y); Mark(cx - x, cy - y);
            Mark(cx + y, cy + x); Mark(cx - y, cy + x);
            Mark(cx + y, cy - x); Mark(cx - y, cy - x);
            if (d > 0) { y--; d += 4 * (x - y) + 10; } else { d += 4 * x + 6; }
            x++;
        }

        for (int row = 0; row < minX.Length; row++)
            if (maxX[row] >= minX[row])
                DrawRectFill(minX[row], cy - r + row, maxX[row] - minX[row] + 1, 1, colorIndex, colorOpaqueness);
    }

    public void DrawOval(int x0, int y0, int x1, int y1, int colorIndex, float colorOpaqueness = 1f)
    {
        OvalMath.DrawOutline(x0, y0, x1, y1, (px, py) => DrawPixel(px, py, colorIndex, colorOpaqueness));
    }

    public void DrawOvalFill(int x0, int y0, int x1, int y1, int colorIndex, float colorOpaqueness = 1f)
    {
        OvalMath.DrawFill(x0, y0, x1, y1, (row, leftX, rightX) =>
            DrawRectFill(leftX, row, rightX - leftX + 1, 1, colorIndex, colorOpaqueness));
    }

    public void Draw(Texture2D texture, Vector2 vector, int colorIndex, float colorOpaqueness = 1f)
    {
        _spriteBatch.Draw(texture, vector, null, ColorPalette.GetColor(colorIndex, colorOpaqueness), 0f, new Vector2(0, 0), 1.0f, SpriteEffects.None, 0f);
    }
}