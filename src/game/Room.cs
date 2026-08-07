namespace mono8.game;

/// <summary>
/// One room, loaded from its object under the ROOMS group in data.json, and everything that
/// lives in it — the room runs its occupants, so entering one is what starts them.
///
/// A room is exactly one screen cut out of the map sheet at <see cref="CellX"/>,
/// <see cref="CellY"/>, which the room authors itself as CELLPOS.
///
/// The authored positions (PLYRPOS, FLAGPOS, BALLPOS) are in pixels *within* the room; this class
/// turns them into map-sheet pixels once, on entry, so everything downstream works in one space.
/// </summary>
internal class Room
{
    // A room is one screenful of the console's cells.
    public const int CellW = Constants.Screen.ResolutionX / Terrain.TileSize;
    public const int CellH = Constants.Screen.ResolutionY / Terrain.TileSize;

    // Where a room's backdrop sits when it authors no BACKPOS — the start of map layer 2, which
    // begins 256 cells to the right of layer 1. Screen is never cleared, so there is always one.
    private const int DefaultBackX = 256;
    private const int DefaultBackY = 0;

    private const string JsonGroup = "ROOMS";
    private const string FieldCell = "CELLPOS";
    private const string FieldBack = "BACKPOS";
    private const string FieldPlayer = "PLYRPOS";
    private const string FieldFlag = "FLAGPOS";
    private const string FieldBall = "BALLPOS";

    public string Name { get; private set; }
    public int CellX { get; private set; }
    public int CellY { get; private set; }

    public int BackCellX { get; private set; }
    public int BackCellY { get; private set; }

    public int PlayerX { get; private set; }
    public int PlayerY { get; private set; }

    public bool HasFlag { get; private set; }
    public int FlagX { get; private set; }
    public int FlagY { get; private set; }

    public int BallX { get; private set; }
    public int BallY { get; private set; }

    // The room's edges in map-sheet pixels. A room is exactly one screen, so these are the screen
    // edges too, and anything past them is off the map as far as the game is concerned.
    private int Left => CellX * Terrain.TileSize;
    private int Top => CellY * Terrain.TileSize;
    private int Right => Left + CellW * Terrain.TileSize - 1;
    private int Bottom => Top + CellH * Terrain.TileSize - 1;

    /// <summary>
    /// Whether a room is authored under ROOMS. A room that is not there is not a level, which is
    /// what the <see cref="LevelSelect"/> draws a number for.
    /// </summary>
    public static bool Exists(string name)
    {
        return !string.IsNullOrEmpty(name) && YourGame.API.gjson(JsonGroup, name) != null;
    }

    /// <summary>
    /// <paramref name="name"/> is the object name under ROOMS. An unknown room, or one missing a
    /// field, loads as an empty room at the top-left of the map sheet rather than failing — a
    /// half-authored room still runs.
    /// </summary>
    public void Enter(string name)
    {
        Load(name);

        // The bag first: the ball leaves the club face, so a hit on the first frame already has one.
        Club.Init();

        // The ball before the player: the swing reads it the frame it starts.
        Ball.Init(this);
        Player.Init(this);
        Flag.Init(this);
        Hud.Init();
    }

    public void Update(float elapsedSeconds)
    {
        Player.Update(elapsedSeconds);
        Ball.Update(elapsedSeconds);
        Flag.Update(elapsedSeconds);

        // After the player, so the swing state the toggle checks is this frame's.
        Club.Update(elapsedSeconds);

        // Last, on the positions the frame settled on: a body that has left the room is never
        // coming back — there is nothing outside one screen — so the hole is unplayable and the
        // level starts over. A ball on its way into the cup is excluded: it is leaving on purpose.
        if (Escaped(Player.X, Player.Y, Player.SprSize)
            || (Ball.InPlay && Escaped(Ball.X, Ball.Y, Ball.Size)))
        {
            Enter(Name);
        }
    }

    /// <summary>
    /// Whether a body has left the room entirely — every pixel of it past one edge, so clipping a
    /// corner on the way past is not a loss. The top is deliberately open: a lofted shot arcs over
    /// the screen and gravity brings it back, and losing the ball for that would be the shot the
    /// game is about.
    /// </summary>
    private bool Escaped(int x, int y, int size)
    {
        // An unauthored SPRSIZE / SIZE is 0, which would read a body flush against the left edge as
        // already gone and restart the room every frame.
        int right = x + (size > 0 ? size : 1) - 1;

        return right < Left || x > Right || y > Bottom;
    }

    public void Draw()
    {
        // Backdrop first — it replaces the cleared screen, so it draws before the room itself.
        YourGame.API.map(BackCellX, BackCellY, 0, 0, CellW, CellH);
        YourGame.API.palt(7, true);
        YourGame.API.ovalinv(8, 32, 256 - 8, 144, 17, 117, 0.4f);
        YourGame.API.palt();
        YourGame.API.map(CellX, CellY, 0, 0, CellW, CellH);
        Flag.Draw();

        // Over the flag it is measured from, so the outline is readable against the sprite.
        Ball.DrawHoleDebug();

        Player.Draw();

        // Last, so two pixels are never lost behind the body the swing lines them up against.
        Ball.Draw();

        // HUD, over the room.
        Meter.Draw();
        Club.Draw();
        Hud.Draw();
    }

    private void Load(string name)
    {
        Name = name;
        CellX = 0;
        CellY = 0;
        BackCellX = DefaultBackX;
        BackCellY = DefaultBackY;

        // Re-read every load: Ctrl+S in the JSON editor rebuilds the data without a restart.
        var data = string.IsNullOrEmpty(name) ? null : YourGame.API.gjson(JsonGroup, name);

        if (data != null)
        {
            // The origin has to be settled before anything measured from it, so CELLPOS is read
            // first. (0, 0) is a legitimate origin — the top-left room — so an unauthored one costs
            // nothing.
            if (data.Has(FieldCell))
            {
                (CellX, CellY) = data.GetXY(FieldCell);
            }

            // Absolute cells on the map sheet, not room-relative: the backdrop is its own layer, so
            // it is free to sit anywhere the developer parked it.
            if (data.Has(FieldBack))
            {
                (BackCellX, BackCellY) = data.GetXY(FieldBack);
            }
        }

        int originX = CellX * Terrain.TileSize;
        int originY = CellY * Terrain.TileSize;

        // Whatever the room does not place goes in its top-left corner.
        PlayerX = originX;
        PlayerY = originY;
        HasFlag = false;
        FlagX = originX;
        FlagY = originY;
        BallX = originX;
        BallY = originY;

        if (data == null)
        {
            return;
        }

        if (data.Has(FieldPlayer))
        {
            var (px, py) = data.GetXY(FieldPlayer);
            PlayerX = originX + px;
            PlayerY = originY + py;
        }

        if (data.Has(FieldFlag))
        {
            var (fx, fy) = data.GetXY(FieldFlag);
            FlagX = originX + fx;
            FlagY = originY + fy;
            HasFlag = true;
        }

        if (data.Has(FieldBall))
        {
            var (bx, by) = data.GetXY(FieldBall);
            BallX = originX + bx;
            BallY = originY + by;
        }
    }
}
