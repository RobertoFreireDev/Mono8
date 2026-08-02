namespace mono8.game;

/// <summary>
/// One room, loaded from its object under the ROOMS group in data.json, and everything that
/// lives in it — the room runs its occupants, so entering one is what starts them.
///
/// A room is exactly one screen — 256x144 px = 32x18 cells — cut out of the map sheet at
/// <see cref="CellX"/>, <see cref="CellY"/>. Where each room sits on the sheet is the game's
/// call, not the data's, so the origin is passed to <see cref="Enter"/>.
///
/// The authored positions (PLYRPOS, FLAGPOS) are in pixels *within* the room; this class turns
/// them into map-sheet pixels once, on entry, so everything downstream works in one space.
/// </summary>
internal class Room
{
    public const int CellW = 32;
    public const int CellH = 18;

    private const string JsonGroup = "ROOMS";
    private const string FieldPlayer = "PLYRPOS";
    private const string FieldFlag = "FLAGPOS";
    private const string FieldBall = "BALLPOS";

    // Where the ball sits when the room has no BALLPOS: a short walk in front of the tee, so an
    // unauthored room is still playable. TODO: developer to author BALLPOS per room.
    private const int DefaultBallOffsetX = 16;

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
    /// field, loads as an empty room at the given origin rather than failing — a half-authored
    /// room still runs.
    /// </summary>
    public void Enter(string name, int cellX, int cellY)
    {
        Load(name, cellX, cellY);

        // The ball before the player: the swing reads it the frame it starts.
        Ball.Init(this);
        Player.Init(this);
        Flag.Init(this);
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
        Player.Draw();

        // Last, so two pixels are never lost behind the body the swing lines them up against.
        Ball.Draw();
    }

    private void Load(string name, int cellX, int cellY)
    {
        Name = name;
        CellX = cellX;
        CellY = cellY;

        int originX = cellX * 8;
        int originY = cellY * 8;

        PlayerX = originX;
        PlayerY = originY;
        HasFlag = false;
        FlagX = originX;
        FlagY = originY;

        // Re-read every load: Ctrl+S in the JSON editor rebuilds the data without a restart.
        var data = YourGame.API.gjson(JsonGroup, name);
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
        }

        // Falls in relative to the tee, so the ball lands somewhere sane in an unauthored room.
        BallX = PlayerX + DefaultBallOffsetX;
        BallY = PlayerY;

        if (data != null && data.Has(FieldBall))
        {
            var (bx, by) = data.GetXY(FieldBall);
            BallX = originX + bx;
            BallY = originY + by;
        }
    }
}
