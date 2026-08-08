namespace mono8.game;

/// <summary>
/// One room, loaded from its object under the ROOMS group in data.json, and everything that
/// lives in it — the room runs its occupants, so entering one is what starts them.
///
/// A room is exactly one screen cut out of the map sheet at <see cref="CellX"/>,
/// <see cref="CellY"/>, which the room authors itself as CELLPOS.
///
/// The authored positions (PLYRPOS, FLAGPOS, BALLPOS) are map-sheet pixels, the same space
/// everything downstream works in — so a room laid out in the map editor is authored with the
/// coordinates that editor shows, not with an offset from the room's own corner.
/// </summary>
internal class Room
{
    // A room is one screenful of the console's cells.
    public const int CellW = Constants.Screen.ResolutionX / Terrain.TileSize;
    public const int CellH = Constants.Screen.ResolutionY / Terrain.TileSize;

    // Where a room's backdrop sits when it authors no BACKPOS — the start of map layer 2, which
    // begins 256 cells to the right of layer 1. Screen is never cleared, so there is always one.
    // Public because the level select draws the same backdrop behind its grid and has to fall back
    // to the same place a room would.
    public const int DefaultBackX = 256;
    public const int DefaultBackY = 0;

    // What a room that authors no HITMAX allows. Enough to finish a hole rather than 0, which the HUD
    // would otherwise have to read as a level lost before it starts.
    private const int DefaultHitMax = 5;

    private const string JsonGroup = "ROOMS";
    private const string FieldCell = "CELLPOS";
    private const string FieldBack = "BACKPOS";
    private const string FieldPlayer = "PLYRPOS";
    private const string FieldFlag = "FLAGPOS";
    private const string FieldBall = "BALLPOS";
    private const string FieldHitMax = "HITMAX";
    private const string FieldNumber = "NUMBER";

    public string Name { get; private set; }

    /// <summary>
    /// The room's NUMBER — which level it is, and so which <see cref="Save"/> slot records it. 0 for
    /// a room that authors none, which is a room that can still be played but not recorded.
    /// </summary>
    public int Number { get; private set; }

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

    /// <summary>Strokes the room allows, which the <see cref="Hud"/> counts down.</summary>
    public int HitMax { get; private set; }

    /// <summary>
    /// The room's top-left corner in map-sheet pixels — CELLPOS in cells, so times the tile size.
    /// Everything the room places is measured from it, and it is what the camera subtracts to put
    /// the room on screen: world minus origin is screen, which is the one conversion anything
    /// outside a room needs.
    /// </summary>
    public int OriginX => CellX * Terrain.TileSize;
    public int OriginY => CellY * Terrain.TileSize;

    // The far edges, also in map-sheet pixels. A room is exactly one screen, so anything past them
    // is off the map as far as the game is concerned.
    //
    // The sides are public because the player walks into them rather than out of them: with nothing
    // beyond a room they are terrain, and <see cref="Player"/> stops flush against them. The bottom
    // stays private — a fall out of a room is still a loss.
    public int Left => OriginX;
    public int Right => OriginX + CellW * Terrain.TileSize - 1;
    private int Bottom => OriginY + CellH * Terrain.TileSize - 1;

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
        Hud.Init(HitMax);
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
        //
        // Spent strokes end it the same way, but only once the ball has stopped: the count runs out
        // as the last shot leaves the club, and that shot is still the one that can drop in.
        //
        // Not while the wipe is up. The hole is already won by then, and a player left in mid-air
        // when the ball dropped is falling out of a room that is on its way out anyway — restarting
        // it under the mask would only reset a level nobody is going to see again.
        if (!Wipe.Active
            && (Escaped(Player.X, Player.Y, Player.SprSize)
                || (Ball.InPlay && Escaped(Ball.X, Ball.Y, Ball.Size))
                || (Hud.OutOfShots && Ball.AtRest)))
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

        return right < OriginX || x > Right || y > Bottom;
    }

    public void Draw()
    {
        var api = YourGame.API;

        // Backdrop first — it replaces the cleared screen, so it draws before the room itself. It is
        // its own patch of sheet rather than part of the room, so it goes straight on the screen,
        // before the camera moves off the origin.
        api.map(BackCellX, BackCellY, 0, 0, CellW, CellH);

        // Everything in the room — its cells and everything standing on them — is in map-sheet
        // pixels, so the camera is what shows it: the room's corner lands on (0, 0) and a body draws
        // where it actually stands, whichever room on the sheet that happens to be.
        api.camera(OriginX, OriginY);

        api.map(CellX, CellY, OriginX, OriginY, CellW, CellH);
        Flag.Draw();

        // Over the flag it is measured from, so the outline is readable against the sprite.
        Ball.DrawHoleDebug();

        Player.Draw();

        // Last, so two pixels are never lost behind the body the swing lines them up against.
        Ball.Draw();

        // HUD, over the room and back in screen pixels.
        api.camera();
        Meter.Draw();
        Club.Draw();
        Hud.Draw();
    }

    private void Load(string name)
    {
        Name = name;
        Number = 0;
        CellX = 0;
        CellY = 0;
        BackCellX = DefaultBackX;
        BackCellY = DefaultBackY;
        HitMax = DefaultHitMax;

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

            HitMax = data.GetInt(FieldHitMax, 0, DefaultHitMax);

            // Which level this room is. Read here rather than taken off Levels, so a room entered by
            // name knows what it is whether or not it is one the grid offers.
            Number = data.GetInt(FieldNumber, 0, 0);
        }

        // Whatever the room does not place goes in its own top-left corner rather than the sheet's,
        // so an unauthored body is at least somewhere inside the room it belongs to.
        int originX = OriginX;
        int originY = OriginY;

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

        // Taken as authored: these are map-sheet pixels, so CELLPOS is the room's cut of the sheet
        // and nothing more — it does not shift what is standing on it.
        if (data.Has(FieldPlayer))
        {
            (PlayerX, PlayerY) = data.GetXY(FieldPlayer);
        }

        if (data.Has(FieldFlag))
        {
            (FlagX, FlagY) = data.GetXY(FieldFlag);
            HasFlag = true;
        }

        if (data.Has(FieldBall))
        {
            (BallX, BallY) = data.GetXY(FieldBall);
        }
    }
}
