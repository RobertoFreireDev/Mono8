namespace mono8.core.graphics;

internal static class Screen
{
    public static Rectangle BoxToDraw;
    public static float ScaleX;
    public static float ScaleY;
    public static Rectangle BaseBox = new Rectangle(0, 0, Constants.Screen.ResolutionX, Constants.Screen.ResolutionY);
    private static Point GameResolution;
    public static bool IsFocused { private set; get; }

    public static void SetResolution(GraphicsDeviceManager graphics, GraphicsDevice graphicsDevice, int w = 0, int h = 0)
    {
        GameResolution = new Point(Math.Max(w, BaseBox.Width), Math.Max(h, BaseBox.Height));
        ApplyChanges(graphics, graphicsDevice);
    }

    private static void ApplyChanges(GraphicsDeviceManager graphics, GraphicsDevice graphicsDevice)
    {
        if (graphics.IsFullScreen)
        {
            graphics.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
            graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
        }
        else
        {
            graphics.PreferredBackBufferWidth = GameResolution.X;
            graphics.PreferredBackBufferHeight = GameResolution.Y;
        }
        graphics.ApplyChanges();
        SetBoxToDraw(graphicsDevice);
    }

    public static void ToggleFullScreen(GraphicsDeviceManager graphics, GraphicsDevice graphicsDevice)
    {
        graphics.IsFullScreen = !graphics.IsFullScreen;
        ApplyChanges(graphics, graphicsDevice);
    }

    public static void UpdateIsFocused(bool isActive, bool isFullScreen)
    {
        IsFocused = isFullScreen || isActive;
    }

    public static void SetBoxToDraw(GraphicsDevice graphicsDevice)
    {
        var viewPort = graphicsDevice.Viewport;
        float screenAspectRatio = (float)viewPort.Width / viewPort.Height;
        float targetAspectRatio = (float)BaseBox.Width / BaseBox.Height;

        if (screenAspectRatio == targetAspectRatio)
        {
            BoxToDraw = new Rectangle(0, 0, BaseBox.Width, BaseBox.Height);
        }
        else
        {
            float multx = (float)viewPort.Width / BaseBox.Width;
            float multy = (float)viewPort.Height / BaseBox.Height;
            float mult = (float)Math.Floor(Math.Min(multx, multy) * 16f) / 16f;
            int scaleWidth = (int)Math.Round(mult * BaseBox.Width);
            int scaleHeight = (int)Math.Round(mult * BaseBox.Height);
            int offsetX = (viewPort.Width - scaleWidth) / 2;
            int offsetY = (viewPort.Height - scaleHeight) / 2;
            BoxToDraw = new Rectangle(offsetX, offsetY, scaleWidth, scaleHeight);
        }

        ScaleX = (float)BoxToDraw.Width / BaseBox.Width;
        ScaleY = (float)BoxToDraw.Height / BaseBox.Height;
    }

    public static Rectangle ScaleRectangle(Rectangle bounds)
    {
        var boxToDraw = BoxToDraw;
        var scaleX = ScaleX;
        var scaleY = ScaleY;

        return new Rectangle(
            boxToDraw.X + (int)Math.Floor(bounds.X * scaleX),
            boxToDraw.Y + (int)Math.Floor(bounds.Y * scaleY),
            (int)Math.Ceiling(bounds.Width * scaleX),
            (int)Math.Ceiling(bounds.Height * scaleY));
    }
}
