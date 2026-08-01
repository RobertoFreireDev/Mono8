namespace mono8.game;

/// <summary>
/// ============================================================================
///  MONO8 — TUTORIAL GAME
/// ============================================================================
///
///  This class IS the tutorial. Read it top to bottom, then start deleting and
///  changing things — that is the fastest way to learn the engine.
///
///  A Mono8 game is three methods, called by the engine every frame:
///
///     Init()                  runs once, before the first frame
///     Update(elapsedSeconds)  runs once per frame — game logic goes here
///     Draw()                  runs once per frame — drawing goes here
///
///  Everything the engine can do is on the `API` object (see IMono8API.cs).
///  The screen is 256 x 144 pixels, and colors are palette indices 0..31
///  (use Constants.Colors.* so you don't have to memorise the numbers).
///
///  CONTROLS
///     Arrow keys  move            (btn 0..3)
///     Z           change color    (btn 4)
///     X           toggle shape    (btn 5)
///     C           spawn particles (btn 6)
///
/// ============================================================================
/// </summary>
internal class YourGame : IEditor
{
    public static IMono8API API;

    public YourGame(IMono8API api)
    {
        API = api;
    }

    // ------------------------------------------------------------------------
    // 1. BUTTONS
    // ------------------------------------------------------------------------
    // Buttons are plain integers. Player 1 uses 0..7, player 2 uses 8..15.
    // Naming them like this keeps the input code readable.

    private const int BtnLeft  = 0;
    private const int BtnRight = 1;
    private const int BtnUp    = 2;
    private const int BtnDown  = 3;
    private const int BtnA     = 4;  // keyboard Z / gamepad A
    private const int BtnB     = 5;  // keyboard X / gamepad B
    private const int BtnX     = 6;  // keyboard C / gamepad X

    // ------------------------------------------------------------------------
    // 2. GAME STATE
    // ------------------------------------------------------------------------
    // Position is stored as float so movement is smooth and frame-rate
    // independent. Only the final pixel coordinates get rounded to int.

    private const float MoveSpeed  = 70f;  // pixels per second
    private const int   PlayerSize = 6;    // radius, in pixels

    private float _playerX;
    private float _playerY;
    private bool  _playerIsCircle = true;

    // The colors the player cycles through when you press Z.
    private static readonly int[] Palette =
    [
        Constants.Colors.Red,
        Constants.Colors.Orange,
        Constants.Colors.Yellow,
        Constants.Colors.Green,
        Constants.Colors.Blue,
        Constants.Colors.Indigo,
        Constants.Colors.Pink,
        Constants.Colors.White,
    ];

    private int _colorIndex;
    private int PlayerColor => Palette[_colorIndex];

    // ------------------------------------------------------------------------
    // 3. PARTICLES
    // ------------------------------------------------------------------------
    // A particle is just a position, a velocity and a countdown. When the
    // countdown reaches zero the particle is removed from the list.

    private const float ParticleLife  = 0.9f;  // seconds
    private const int   ParticleBurst = 12;    // particles spawned per press

    private class Particle
    {
        public float X, Y;          // current position
        public float VelocityX;     // pixels per second
        public float VelocityY;
        public float Life;          // seconds remaining
        public int   Color;
        public bool  IsCircle;      // matches the player's shape when spawned
    }

    private readonly List<Particle> _particles = [];

    // ------------------------------------------------------------------------
    // 4. INIT — runs once
    // ------------------------------------------------------------------------

    public void Init()
    {
        // Start in the middle of the screen.
        _playerX = Constants.Screen.ResolutionX / 2f;
        _playerY = Constants.Screen.ResolutionY / 2f;

        _colorIndex = 0;
        _playerIsCircle = true;
        _particles.Clear();
    }

    // ------------------------------------------------------------------------
    // 5. UPDATE — runs every frame, before Draw
    // ------------------------------------------------------------------------
    // `elapsedSeconds` is the time since the previous frame. Multiplying by it
    // ("delta time") makes movement run at the same real-world speed no matter
    // how fast the machine is.

    public void Update(float elapsedSeconds)
    {
        UpdatePlayer(elapsedSeconds);
        UpdateActions();
        UpdateParticles(elapsedSeconds);
    }

    private void UpdatePlayer(float elapsedSeconds)
    {
        // btn() is true for as long as the button is HELD down — that is what
        // you want for movement.
        float dx = 0f;
        float dy = 0f;

        if (API.btn(BtnLeft))  dx -= 1f;
        if (API.btn(BtnRight)) dx += 1f;
        if (API.btn(BtnUp))    dy -= 1f;
        if (API.btn(BtnDown))  dy += 1f;

        // Y grows DOWNWARD: y = 0 is the top of the screen.
        _playerX += dx * MoveSpeed * elapsedSeconds;
        _playerY += dy * MoveSpeed * elapsedSeconds;

        // mid() returns the middle of three values, which is a neat way to
        // clamp: never below PlayerSize, never past the far edge.
        _playerX = (float)API.mid(PlayerSize, _playerX, Constants.Screen.ResolutionX - PlayerSize);
        _playerY = (float)API.mid(PlayerSize, _playerY, Constants.Screen.ResolutionY - PlayerSize);
    }

    private void UpdateActions()
    {
        // btnp() is true only on the FRAME the button goes down — that is what
        // you want for one-shot actions, otherwise a single press would fire
        // sixty times a second.

        if (API.btnp(BtnA))
        {
            // Wrapping with % keeps the index inside the array.
            _colorIndex = (_colorIndex + 1) % Palette.Length;
        }

        if (API.btnp(BtnB))
        {
            _playerIsCircle = !_playerIsCircle;
        }

        if (API.btnp(BtnX))
        {
            SpawnParticles();
        }
    }

    private void SpawnParticles()
    {
        for (int i = 0; i < ParticleBurst; i++)
        {
            // Angles are TURNS, not radians: 0 = right, 0.25 = up, 1 = full
            // circle. sin()/cos() take turns too, so a random angle is just a
            // random number between 0 and 1.
            double angle = API.rnd(1f);
            float speed = 25f + API.rnd(45f);

            _particles.Add(new Particle
            {
                X = _playerX,
                Y = _playerY,
                VelocityX = (float)API.cos(angle) * speed,
                VelocityY = (float)API.sin(angle) * speed,
                Life = ParticleLife,
                Color = PlayerColor,          // same color as the player...
                IsCircle = _playerIsCircle,   // ...and the same shape
            });
        }
    }

    private void UpdateParticles(float elapsedSeconds)
    {
        // Walk the list BACKWARDS so removing an item doesn't skip the next one.
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            Particle p = _particles[i];

            p.X += p.VelocityX * elapsedSeconds;
            p.Y += p.VelocityY * elapsedSeconds;
            p.Life -= elapsedSeconds;

            if (p.Life <= 0f)
            {
                _particles.RemoveAt(i);
            }
        }
    }

    // ------------------------------------------------------------------------
    // 6. DRAW — runs every frame, after Update
    // ------------------------------------------------------------------------
    // Draw calls paint on top of each other, so order matters: background
    // first, then particles, then the player, then the interface.

    public void Draw()
    {
        API.cls(Constants.Colors.DarkBlue);

        DrawTitle();
        DrawParticles();
        DrawPlayer();
        DrawControls();
    }

    private void DrawTitle()
    {
        PrintCentered("MONO8 TUTORIAL", 10, Constants.Colors.White);
        PrintCentered("THE CODE IS THE TUTORIAL", 21, Constants.Colors.Blue);
    }

    private void DrawParticles()
    {
        foreach (Particle p in _particles)
        {
            // Every drawing call takes an optional opaqueness (0 = invisible,
            // 1 = solid). Tying it to the remaining life makes particles fade
            // out instead of blinking away.
            float fade = p.Life / ParticleLife;

            DrawShape((int)p.X, (int)p.Y, 1, p.Color, p.IsCircle, fade);
        }
    }

    private void DrawPlayer()
    {
        DrawShape((int)_playerX, (int)_playerY, PlayerSize, PlayerColor, _playerIsCircle);
    }

    // One helper for both shapes, so the player and its particles always match.
    private static void DrawShape(int x, int y, int radius, int color, bool isCircle, float opaqueness = 1f)
    {
        if (isCircle)
        {
            // circfill takes a CENTER and a radius.
            API.circfill(x, y, radius, color, opaqueness);
        }
        else
        {
            // rectfill takes two CORNERS, so build them from the center.
            API.rectfill(x - radius, y - radius, x + radius, y + radius, color, opaqueness);
        }
    }

    private void DrawControls()
    {
        const int LineHeight = 9;
        int y = Constants.Screen.ResolutionY - 4 - LineHeight * 4;

        API.print("ARROWS  MOVE",         8, y,                  Constants.Colors.LightGray);
        API.print("Z       COLOR",        8, y + LineHeight,     Constants.Colors.LightGray);
        API.print("X       SHAPE",        8, y + LineHeight * 2, Constants.Colors.LightGray);
        API.print("C       PARTICLES",    8, y + LineHeight * 3, Constants.Colors.LightGray);

        // A tiny live readout, so you can see the state you are editing.
        string shape = _playerIsCircle ? "CIRCLE" : "SQUARE";
        API.print($"{shape}  PARTICLES:{_particles.Count}", 150, y + LineHeight * 3, PlayerColor);
    }

    // The font is fixed-width: every character advances 4 pixels.
    private const int CharAdvance = 4;

    private static void PrintCentered(string text, int y, int color)
    {
        int x = (Constants.Screen.ResolutionX - text.Length * CharAdvance) / 2;
        API.print(text, x, y, color);
    }
}
