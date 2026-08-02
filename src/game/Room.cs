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
    // A room is one screenful of the console's 8x8 cells.
    private const int TileSize = 8;

    public const int CellW = Constants.Screen.ResolutionX / TileSize;
    public const int CellH = Constants.Screen.ResolutionY / TileSize;

    private const string JsonGroup = "ROOMS";
    private const string FieldCell = "CELLPOS";
    private const string FieldPlayer = "PLYRPOS";
    private const string FieldFlag = "FLAGPOS";
    private const string FieldBall = "BALLPOS";

    public string Name { get; private set; }
    public int CellX { get; private set; }
    public int CellY { get; private set; }

    public int PlayerX { get; private set; }
    public int PlayerY { get; private set; }

    public bool HasFlag { get; private set; }
    public int FlagX { get; private set; }
    public int FlagY { get; private set; }

    public int BallX { get; private set; }
    public int BallY { get; private set; }

    /// <summary>
    /// <paramref name="name"/> is the object name under ROOMS. An unknown room, or one missing a
    /// field, loads as an empty room at the top-left of the map sheet rather than failing — a
    /// half-authored room still runs.
    /// </summary>
    public void Enter(string name)
    {
        Load(name);

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
    }

    public void Draw()
    {
        YourGame.API.map(CellX, CellY, 0, 0, CellW, CellH);
        Flag.Draw();

        // Over the flag it is measured from, so the outline is readable against the sprite.
        Ball.DrawHoleDebug();

        Player.Draw();

        // Last, so two pixels are never lost behind the body the swing lines them up against.
        Ball.Draw();

        // HUD, over the room.
        Meter.Draw();
        Hud.Draw();
    }

    private void Load(string name)
    {
        Name = name;
        CellX = 0;
        CellY = 0;

        // Re-read every load: Ctrl+S in the JSON editor rebuilds the data without a restart.
        var data = string.IsNullOrEmpty(name) ? null : YourGame.API.gjson(JsonGroup, name);

        // The origin has to be settled before anything measured from it, so CELLPOS is read first.
        // (0, 0) is a legitimate origin — the top-left room — so an unauthored one costs nothing.
        if (data != null && data.Has(FieldCell))
        {
            (CellX, CellY) = data.GetXY(FieldCell);
        }

        int originX = CellX * TileSize;
        int originY = CellY * TileSize;

        PlayerX = originX;
        PlayerY = originY;
        HasFlag = false;
        FlagX = originX;
        FlagY = originY;
        BallX = originX;
        BallY = originY;

        if (data != null)
        {
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
}
