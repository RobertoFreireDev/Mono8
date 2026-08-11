namespace mono8;

public class Mono8Game : Game
{
    internal static Mono8Game Instance;
    private GraphicsDeviceManager _graphics;
    public static PixelledSpriteBatch SpriteBatch;
    internal static Mono8API GameAPI;
    private RenderTarget2D sceneTarget;
    public static GraphicsDevice GraphicsDeviceRef;
    public static int DisplayFps = 0;
    private const double TargetFps = 60.0;
    private const float UnfocusedDimOpacity = 0.3f;
    private double _elapsedTime = 0;
    private int _fpsCounter = 0;
    private Intro _intro = new Intro();
    private bool _unfocusedDimDrawn;
#if BLAZORGL
    // The page owns the canvas and nothing raises ClientSizeChanged for it, so the size is watched
    // rather than subscribed to; see SyncCanvasSize.
    private Point _canvasSize;
#endif

    public Mono8Game()
    {
        Instance = this;
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        Window.AllowUserResizing = true;
        // The console draws its own pointer; see MousePointer.
        IsMouseVisible = false;
        ColorPalette.SetColorPalette();
        IsFixedTimeStep = true;
#if !BLAZORGL
        Window.ClientSizeChanged += OnResize;
#endif
    }

    public void LoadFiles()
    {
        ErrorHandler.Reset();
        GameAPI = new Mono8API();
    }

    protected override void Initialize()
    {
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / TargetFps);
        base.Initialize();
    }

#if !BLAZORGL
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
#endif

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
        TextEntry.Attach(Window);
        LoadFiles();

#if BLAZORGL
        // There is no window to size or move: the canvas is whatever the page gave it, and going
        // fullscreen is the browser's to grant and only on a user gesture (F2, below). The console
        // just fills what it was handed and follows it from there.
        SyncCanvasSize();
#else
        if (Mono8API.PublishGame)
        {
            Screen.ToggleFullScreen(_graphics, GraphicsDevice);
        }
        else
        {
            Screen.MaximizeWindowed(_graphics, GraphicsDevice);
            Window.Position = Screen.WindowedCenter(
                _graphics.PreferredBackBufferWidth,
                _graphics.PreferredBackBufferHeight);
        }
#endif
    }

#if BLAZORGL
    /// <summary>
    /// Matches the back buffer to the canvas the page is holding. The desktop gets this from
    /// <c>ClientSizeChanged</c>; in the browser the canvas is resized by the host page's own resize
    /// handler, which raises nothing, so the size is compared once a frame instead.
    /// </summary>
    private void SyncCanvasSize()
    {
        var bounds = Window.ClientBounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        if (bounds.Width == _canvasSize.X && bounds.Height == _canvasSize.Y) return;

        _canvasSize = new Point(bounds.Width, bounds.Height);
        Screen.SetResolution(_graphics, GraphicsDevice, bounds.Width, bounds.Height);
    }
#endif

    protected override void Update(GameTime gameTime)
    {
#if BLAZORGL
        SyncCanvasSize();
#endif

        // First thing in the frame: the intro path and the early return below both depend on it being current.
        bool wasFocused = Screen.IsFocused;
#if BLAZORGL
        // The browser stops calling the game loop for a tab that is not visible, so the frame is
        // already held for us — and IsActive on a canvas is not the window focus the dim is about.
        Screen.UpdateIsFocused(true, _graphics.IsFullScreen);
#else
        Screen.UpdateIsFocused(IsActive, _graphics.IsFullScreen);
#endif

        // Unfocused holds the last frame, so a drawn pointer would sit frozen on it — hand the OS one
        // back for as long as the window is not ours to draw into.
        IsMouseVisible = !Screen.IsFocused;

        if (!Screen.IsFocused)
        {
            // Present() runs from Game.EndDraw, not from Draw, so an early return there still swaps
            // the buffers every tick and flips the window between the last two frames drawn. This is
            // what actually holds the screen on the frame it stopped at. The first unfocused frame
            // is let through so Draw can lay the dim over the scene; every one after holds it.
            if (_unfocusedDimDrawn)
                SuppressDraw();

            // Input still samples, so the press/release edges are not a frame stale when focus comes back.
            InputStateManager.Update();
            base.Update(gameTime);
            return;
        }

        if (!wasFocused)
        {
            // The click that raised the window only dismisses the dim; the next one is the one that
            // reaches whatever sits under the cursor.
            MouseInput.SwallowUntilRelease();
            _unfocusedDimDrawn = false;
        }

#if !BLAZORGL
        if (KeybrdInput.IsAltF4Pressed())
            Exit();
#endif

        if (KeybrdInput.IsF2Released())
        {
            Screen.ToggleFullScreen(_graphics, GraphicsDevice);
        }

        if (!_intro.IsFinished)
        {
            _intro.Update(gameTime);
            base.Update(gameTime);
            return;
        }

        if (GameAPI.IsPlayingGame)
        {
            Menu.Update();

        }
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
        if (_intro.IsFinished)
        {
            GameAPI.Draw();
        }
        else
        {
            _intro.Draw(GameAPI);
        }
        SpriteBatch.End();
        // Last, and in screen space: the frame it sits on top of may have left the camera anywhere.
        // The intro takes no input, so it gets no pointer.
        if (_intro.IsFinished)
        {
            Camera2D.Camera(0, 0);
            SpriteBatch.Begin();
            MousePointer.Draw();
            SpriteBatch.End();
        }
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);
        SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.PointClamp, effect: null);
        SpriteBatch.Draw(sceneTarget, Screen.BoxToDraw, -1);
        SpriteBatch.End();
        SpriteBatch.Begin(SamplerState.PointClamp);
        DrawGameBorder();
        if (!Screen.IsFocused)
        {
            DrawUnfocusedDim();
            _unfocusedDimDrawn = true;
        }
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

    private void DrawUnfocusedDim()
    {
        var viewport = GraphicsDevice.Viewport.Bounds;
        SpriteBatch.DrawRectFill(viewport.X, viewport.Y, viewport.Width, viewport.Height, ColorPalette.BlackColorIndex, UnfocusedDimOpacity);
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