namespace mono8;

public class mono8 : Game
{
    internal static mono8 Instance;
    private GraphicsDeviceManager _graphics;
    public static PixelledSpriteBatch SpriteBatch;
    internal static Mono8API GameAPI;
    private RenderTarget2D sceneTarget;
    public static GraphicsDevice GraphicsDeviceRef;
    public static int DisplayFps = 0;
    private double _elapsedTime = 0;
    public int _fpsCounter = 0;
    public double FPS30 = 30.0;

    public mono8()
    {
        Instance = this;
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        Window.AllowUserResizing = true;
        IsMouseVisible = true;
        ColorPalette.SetColorPalette();
        IsFixedTimeStep = true;
        Window.ClientSizeChanged += OnResize;
    }

    public void LoadFiles()
    {
        ErrorHandler.Reset();
        GameAPI = new Mono8API();
    }

    protected override void Initialize()
    {
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / FPS30);
        base.Initialize();
    }

    private void OnResize(Object sender, EventArgs e)
    {
        if (sender is not GameWindow)
        {
            return;
        }

        var window = (GameWindow)sender;

        if (window.ClientBounds.Width == _graphics.PreferredBackBufferWidth && window.ClientBounds.Height == _graphics.PreferredBackBufferHeight)
        {
            return;
        }

        Screen.SetResolution(_graphics, GraphicsDevice, window.ClientBounds.Width, window.ClientBounds.Height);
        Window.Position = new Point(window.ClientBounds.X, window.ClientBounds.Y);
    }

    protected override void LoadContent()
    {
        GraphicsDeviceRef = GraphicsDevice;
        Screen.SetResolution(_graphics, GraphicsDevice);
        SpriteBatch = new PixelledSpriteBatch(GraphicsDevice);
        _graphics.SynchronizeWithVerticalRetrace = true;
        sceneTarget = new RenderTarget2D(
            GraphicsDevice,
            GraphicsDevice.PresentationParameters.BackBufferWidth,
            GraphicsDevice.PresentationParameters.BackBufferHeight,
            false,
            SurfaceFormat.Color,
            DepthFormat.None);
        Text.GetCharacterTextures(GraphicsDevice);
        LoadFiles();
    }

    protected override void Update(GameTime gameTime)
    {
        if (KeybrdInput.IsAltF4Pressed())
            Exit();

        if (KeybrdInput.IsF2Released())
        {
            Screen.ToggleFullScreen(_graphics, GraphicsDevice);
        }

        if (KeybrdInput.IsSaveShortcutPressed())
        {
            GameAPI.Save();
        }

        Menu.Update();
        Screen.UpdateIsFocused(IsActive, _graphics.IsFullScreen);        
        InputStateManager.Update();
        GameAPI.Update(gameTime);      
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.SetRenderTarget(sceneTarget);
        GraphicsDevice.Clear(Color.Black);
        Camera2D.Camera(0, 0);
        SpriteBatch.Begin();
        GameAPI.Draw();
        SpriteBatch.End();
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);
        SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.PointClamp, effect: null);
        SpriteBatch.Draw(sceneTarget, Screen.BoxToDraw, -1);
        SpriteBatch.End();
        SpriteBatch.Begin(SamplerState.PointClamp);
        DrawGameBorder();
        SpriteBatch.End();

        _elapsedTime += gameTime.ElapsedGameTime.TotalSeconds;
        _fpsCounter++;

        if (_elapsedTime >= 1.0)
        {
            DisplayFps = _fpsCounter;
            _fpsCounter = 0;
            _elapsedTime -= 1.0;
        }

        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        GameAPI.Unload();
        base.UnloadContent();
    }

    public void DrawGameBorder()
    {
        var viewport = GraphicsDevice.Viewport.Bounds;
        var hole = Screen.ScaleRectangle(Screen.BaseBox);
        var colorIndex = ColorPalette.BlackColorIndex;
        SpriteBatch.DrawRectFill(viewport.X, viewport.Y, viewport.Width, viewport.Y + hole.Y, colorIndex);
        SpriteBatch.DrawRectFill(viewport.X, hole.Bottom, viewport.Width, viewport.Bottom - hole.Bottom, colorIndex);
        SpriteBatch.DrawRectFill(viewport.X, hole.Y, hole.X - viewport.X, hole.Height, colorIndex);
        SpriteBatch.DrawRectFill(hole.Right, hole.Y, viewport.Right - hole.Right, hole.Height, colorIndex);
    }
}