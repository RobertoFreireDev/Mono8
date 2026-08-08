namespace mono8.core.graphics;

internal static class Screen
{
    public static Rectangle BoxToDraw;
    public static float ScaleX;
    public static float ScaleY;
    public static Rectangle BaseBox = new Rectangle(0, 0, Constants.Screen.ResolutionX, Constants.Screen.ResolutionY);
    private static Point GameResolution;
    public static bool IsFocused { private set; get; }
    // The desktop keeps some of the display to itself — title bar, borders, taskbar. SDL does not hand
    // us the usable bounds, so reserve enough that the largest windowed step still lands clear of them.
    private const int DesktopChromeX = 32;
    private const int DesktopChromeY = 96;

    public static void SetResolution(GraphicsDeviceManager graphics, GraphicsDevice graphicsDevice, int w = 0, int h = 0)
    {
        GameResolution = new Point(Math.Max(w, BaseBox.Width), Math.Max(h, BaseBox.Height));
        ApplyChanges(graphics, graphicsDevice);
    }

    // Biggest windowed size that is still a whole multiple of the console — a fractional one would
    // blur the pixels, which is the whole point of BoxToDraw stepping in integers.
    public static void MaximizeWindowed(GraphicsDeviceManager graphics, GraphicsDevice graphicsDevice)
    {
        var display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        int mult = Math.Min(
            (display.Width - DesktopChromeX) / BaseBox.Width,
            (display.Height - DesktopChromeY) / BaseBox.Height);
        mult = Math.Max(mult, 1);
        SetResolution(graphics, graphicsDevice, mult * BaseBox.Width, mult * BaseBox.Height);
    }

    public static Point WindowedCenter(int width, int height)
    {
        var display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        return new Point(
            Math.Max((display.Width - width) / 2, 0),
            Math.Max((display.Height - height - DesktopChromeY) / 2, 0));
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
        // Same integer step whatever the aspect: a matching one just lands with no letterbox.
        int multx = viewPort.Width / BaseBox.Width;
        int multy = viewPort.Height / BaseBox.Height;
        int mult = Math.Max(Math.Min(multx, multy), 1);
        int scaleWidth = mult * BaseBox.Width;
        int scaleHeight = mult * BaseBox.Height;
        int offsetX = (viewPort.Width - scaleWidth) / 2;
        int offsetY = (viewPort.Height - scaleHeight) / 2;
        BoxToDraw = new Rectangle(offsetX, offsetY, scaleWidth, scaleHeight);

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
