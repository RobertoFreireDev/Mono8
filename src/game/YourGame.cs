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
///     V           tune the data   (btn 7)
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
    private const int BtnY     = 7;  // keyboard V / gamepad Y

    // ------------------------------------------------------------------------
    // 2. GAME STATE
    // ------------------------------------------------------------------------
    // Position is stored as float so movement is smooth and frame-rate
    // independent. Only the final pixel coordinates get rounded to int.

    private const int DefaultSpeed = 70;  // pixels per second, when data.json has none
    private const int PlayerSize   = 6;   // radius, in pixels

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
    // 4. GAME DATA — data.json, via gjson / sjson
    // ------------------------------------------------------------------------
    // The numbers you want to tune without recompiling belong in data.json,
    // authored in the Json editor. It is a tree of group -> object -> field,
    // so everything below reads the "PLAYER" object of the "DEMO" group.
    //
    // gjson() hands you that object with every value ALREADY PARSED into its
    // real C# type — reading one is an array lookup, no parsing and no garbage,
    // so it is fine to call inside Update. It returns null when the group or
    // the object is not there, which is why every read below has a fallback.

    private const string DataGroup  = "DEMO";
    private const string DataObject = "PLAYER";

    // One getter per type, and one sjson overload per type. That is how the
    // data stays typed all the way from the editor to your game: no casts, no
    // `object`, no "is this string a number?".
    //
    //     s String   GetStr    "MONO8 TUTORIAL"
    //     t Text     GetStr    a longer string, up to 256 chars
    //     i Int      GetInt    70
    //     d Decimal  GetDec    0.7          (double)
    //     m Money    GetMoney  7.00         (decimal)
    //     p PosXY    GetXY     (128, 72)    (tuple)
    //     b Bool     GetBool   false
    //
    // Any field can hold an ARRAY of its own type instead of one value. Then
    // the second argument picks the item, or IntArray/DecArray/BoolArray give
    // you the whole thing as a span with no copy.

    /// Press V: writes one value of every type back into the object.
    private void TuneData()
    {
        Mono8JsonObject data = API.gjson(DataGroup, DataObject);
        if (data == null) return;   // no data.json, or the object was renamed

        // Step the speed 70 -> 100 -> 130 -> 40 -> 70 ...
        int speed = data.GetInt("SPEED", 0, DefaultSpeed) + 30;
        if (speed > 130) speed = 40;

        // The compiler picks the overload from the value you pass. Pass the
        // wrong type for the field and sjson returns false and changes
        // nothing — it never converts behind your back and never throws.
        API.sjson(DataGroup, DataObject, "SPEED", speed);                            // int
        API.sjson(DataGroup, DataObject, "SCALE", speed / 100.0);                    // double
        API.sjson(DataGroup, DataObject, "COST",  speed / 10m);                      // decimal
        API.sjson(DataGroup, DataObject, "SOLID", speed >= 100);                     // bool
        API.sjson(DataGroup, DataObject, "NAME",  speed >= 100 ? "QUICK" : "STEADY");// string
        API.sjson(DataGroup, DataObject, "START", ((int)_playerX, (int)_playerY));   // PosXY

        // A trailing index writes ONE item of an array and leaves the rest be.
        API.sjson(DataGroup, DataObject, "COLORS", PlayerColor, 0);

        // These writes live in memory only: data.json is the editor's to write,
        // so pressing Esc and coming back gives you the authored values again.
    }

    // ------------------------------------------------------------------------
    // 5. INIT — runs once
    // ------------------------------------------------------------------------

    public void Init()
    {
        // Where the player starts is authored, not hard-coded. A PosXY field
        // comes back as a tuple, so it deconstructs straight into two ints.
        Mono8JsonObject data = API.gjson(DataGroup, DataObject);
        (int startX, int startY) = data?.GetXY("START")
            ?? (Constants.Screen.ResolutionX / 2, Constants.Screen.ResolutionY / 2);

        _playerX = startX;
        _playerY = startY;

        _colorIndex = 0;
        _playerIsCircle = true;
        _particles.Clear();
    }

    // ------------------------------------------------------------------------
    // 6. UPDATE — runs every frame, before Draw
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

        // Read straight out of data.json, every frame. Change SPEED in the Json
        // editor (or press V) and the player really does move faster, with no
        // recompile — that is the whole point of authoring data outside code.
        int speed = API.gjson(DataGroup, DataObject)?.GetInt("SPEED", 0, DefaultSpeed) ?? DefaultSpeed;

        // Y grows DOWNWARD: y = 0 is the top of the screen.
        _playerX += dx * speed * elapsedSeconds;
        _playerY += dy * speed * elapsedSeconds;

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

        if (API.btnp(BtnY))
        {
            TuneData();
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
    // 7. DRAW — runs every frame, after Update
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
        DrawData();
    }

    private void DrawTitle()
    {
        PrintCentered("MONO8 TUTORIAL", 10, Constants.Colors.White);

        // A Text field (up to 256 chars) reads back with the same GetStr as a
        // String field — both are just a string once they reach your game.
        string subtitle = API.gjson(DataGroup, DataObject)?.GetStr("DESC", 0, "THE CODE IS THE TUTORIAL")
            ?? "THE CODE IS THE TUTORIAL";

        PrintCentered(subtitle, 21, Constants.Colors.Blue);
    }

    // Everything in DEMO/PLAYER, one line per type, so you can watch the values
    // change as you press V and see what the Json editor is actually storing.
    private static void DrawData()
    {
        const int PanelX = 148;
        const int LineHeight = 9;
        int y = 32;

        Mono8JsonObject data = API.gjson(DataGroup, DataObject);
        if (data == null)
        {
            API.print("NO DEMO/PLAYER", PanelX, y, Constants.Colors.Red);
            return;
        }

        API.print("DEMO/PLAYER", PanelX, y, Constants.Colors.White);

        API.print($"NAME  {data.GetStr("NAME")}",        PanelX, y += LineHeight, Constants.Colors.LightGray);
        API.print($"SPEED {data.GetInt("SPEED")}",       PanelX, y += LineHeight, Constants.Colors.LightGray);
        API.print($"SCALE {Num(data.GetDec("SCALE"))}",  PanelX, y += LineHeight, Constants.Colors.LightGray);
        API.print($"COST  {Num(data.GetMoney("COST"))}", PanelX, y += LineHeight, Constants.Colors.LightGray);
        API.print($"SOLID {data.GetBool("SOLID")}",      PanelX, y += LineHeight, Constants.Colors.LightGray);

        // PosXY is a tuple — deconstruct it, or use .x / .y.
        (int startX, int startY) = data.GetXY("START");
        API.print($"START {startX},{startY}", PanelX, y += LineHeight, Constants.Colors.LightGray);

        // An array field. Count() says how many items it holds, and IntArray()
        // is a view straight onto them: no copy, no allocation, so drawing a
        // swatch per item costs nothing.
        y += LineHeight;
        API.print($"COLORS[{data.Count("COLORS")}]", PanelX, y, Constants.Colors.LightGray);

        ReadOnlySpan<int> colors = data.IntArray("COLORS");
        for (int i = 0; i < colors.Length; i++)
        {
            int x = PanelX + 40 + i * 6;
            API.rectfill(x, y, x + 4, y + 4, colors[i]);
        }
    }

    // The file stores numbers the invariant way (a dot, always two decimals for
    // money), so print them the same way rather than in the machine's locale.
    private static string Num(double value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Num(decimal value) =>
        value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

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
        int y = Constants.Screen.ResolutionY - 4 - LineHeight * 5;

        API.print("ARROWS  MOVE",         8, y,                  Constants.Colors.LightGray);
        API.print("Z       COLOR",        8, y + LineHeight,     Constants.Colors.LightGray);
        API.print("X       SHAPE",        8, y + LineHeight * 2, Constants.Colors.LightGray);
        API.print("C       PARTICLES",    8, y + LineHeight * 3, Constants.Colors.LightGray);
        API.print("V       TUNE DATA",    8, y + LineHeight * 4, Constants.Colors.LightGray);

        // A tiny live readout, so you can see the state you are editing.
        string shape = _playerIsCircle ? "CIRCLE" : "SQUARE";
        API.print($"{shape}  PARTICLES:{_particles.Count}", 150, y + LineHeight * 4, PlayerColor);
    }

    // The font is fixed-width: every character advances 4 pixels.
    private const int CharAdvance = 4;

    private static void PrintCentered(string text, int y, int color)
    {
        int x = (Constants.Screen.ResolutionX - text.Length * CharAdvance) / 2;
        API.print(text, x, y, color);
    }
}
