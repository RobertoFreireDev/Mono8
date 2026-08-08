namespace mono8.game;

/// <summary>
/// ============================================================================
///  MONO8 — GOLF
/// ============================================================================
///
///  A Mono8 game is three methods, called by the engine every frame:
///
///     Init()                  runs once, before the first frame
///     Update(elapsedSeconds)  runs once per frame — game logic goes here
///     Draw()                  runs once per frame — drawing goes here
///
///  Everything the engine can do is on the `API` object (see IMono8API.cs).
///
/// ============================================================================
/// </summary>
internal class YourGame : IEditor
{
    private readonly Room _room = new Room();

    public static IMono8API API;

    public YourGame(IMono8API api)
    {
        API = api;
    }

    // The game is either the level select or the room it picked, so the three methods are a forward
    // to one or the other. Everything in play lives inside a room, and every room is a level the
    // menu can open.
    public void Init()
    {
        // Nothing here is aimed — the level select and the shot are both on the buttons — so the
        // pointer is only in the way. Esc back to the editors brings it straight back.
        API.mouse(false);

        Debug.Init();

        // First: which room is which level is what the save slots and the grid are both indexed by.
        Levels.Init();

        Save.Init();
        Wipe.Init();
        LevelSelect.Init();
    }

    public void Update(float elapsedSeconds)
    {
        if (LevelSelect.Active)
        {
            LevelSelect.Update(elapsedSeconds);

            // Entered from here rather than from the menu, so Room.Enter still runs from one place.
            // The room does not update the frame it is entered — its first frame is the next one.
            if (LevelSelect.Picked != null)
            {
                LevelSelect.Close();
                Enter(LevelSelect.Picked);
            }

            return;
        }

        _room.Update(elapsedSeconds);

        // Sinking the ball ends the hole: the screen closes onto the player, the next level is
        // loaded behind it, and the same oval opens back out. The room keeps running underneath —
        // only its controls are off — so this asks after it rather than instead of it.
        if (Ball.Holed && !Wipe.Active)
        {
            // The one frame a hole is finished on, so it is the one place a result is recorded —
            // losing the room or leaving it never reaches here and never writes.
            Save.Complete(_room.Number, Hud.Taken);

            Wipe.Start();
        }

        // The wipe is drawn with the camera back at the origin, so the player is handed to it in
        // screen pixels — a room anywhere but the top-left of the sheet would otherwise close the
        // iris onto a point off the screen entirely.
        Wipe.Update(elapsedSeconds, Player.CenterX - _room.OriginX, Player.CenterY - _room.OriginY);

        if (Wipe.Closed)
        {
            Advance();
        }
    }

    // The frame the screen is covered, which is the one frame a room can be swapped without it
    // being seen. Out of levels the game goes back to the menu, and the mask comes off with the
    // room it was covering: the menu is its own screen and there is nothing left to reveal.
    private void Advance()
    {
        string next = LevelSelect.Next(_room.Name);

        if (next == null)
        {
            Wipe.Stop();
            LevelSelect.Show();
            return;
        }

        Enter(next);
        Wipe.Open();
    }

    // Every room entry goes through here, so the menu's cursor never falls behind the level actually
    // being played: pausing out of the third hole comes back to the menu on 3, not on the 1 picked
    // to start the run.
    private void Enter(string name)
    {
        LevelSelect.Focus(name);
        _room.Enter(name);
    }

    public void Draw()
    {
        if (LevelSelect.Active)
        {
            LevelSelect.Draw();
        }
        else
        {
            _room.Draw();
        }

        // Over the room and its HUD, under the debug readout.
        Wipe.Draw();

        Debug.Draw();
    }
}
