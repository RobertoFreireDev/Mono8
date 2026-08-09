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
    // Static so the pause-menu entry can reach it: the engine builds one YourGame for the lifetime of
    // the process, and every room entry already goes through Enter.
    private static readonly Room _room = new Room();

    // LevelSelect owns entry 2, Save 3. Entry 1 was the debug toggle and is now free.
    private const int RestartIndex = 0;
    private const string RestartLabel = "RESTART LEVEL";

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

        // First: which room is which level is what the save slots and the grid are both indexed by.
        Levels.Init();

        Save.Init();
        Wipe.Init();

        // The two washes loaded here rather than by the room that draws them: the hours that are night
        // — and the hours that are morning — are the game's, not a level's, and the menu falls under
        // them too, so they outlive every room and are read once. Before the menu, which draws them on
        // its first frame.
        Night.Init();
        Daylight.Init();

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
                Enter(LevelSelect.Picked, arrived: true);
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

        Enter(next, arrived: true);
        Wipe.Open();
    }

    // Every room entry goes through here, so the menu's cursor never falls behind the level actually
    // being played: pausing out of the third hole comes back to the menu on 3, not on the 1 picked
    // to start the run.
    //
    // <paramref name="arrived"/> is a level reached — picked off the menu, or come up behind the wipe
    // — as against one started over, which is the same load without having got anywhere. Only this
    // method can tell the two apart: Room.Enter is what a lost room restarts itself with, and it
    // reads the same from the inside either way. The call-out goes after the load, which clears it.
    private static void Enter(string name, bool arrived)
    {
        LevelSelect.Focus(name);
        _room.Enter(name);

        if (arrived)
        {
            LevelHud.Highlight();
        }
    }

    /// <summary>
    /// The pause menu's RESTART LEVEL, put up with the room and taken down with it — there is no level
    /// to restart while the level select is on screen. Both called by <see cref="LevelSelect"/>, which
    /// is what knows which of the two is up.
    /// </summary>
    public static void ShowRestart() => API.menuitem(RestartIndex, RestartLabel, RestartLevel);

    public static void HideRestart() => API.menuitem(RestartIndex);

    // The level over again — spawns, strokes and all — which is the same reading a body walking out of
    // the room gets. The wipe goes with it: a hole sunk and then restarted is no longer on its way to
    // the next level, and leaving the iris closing would advance past the level just restarted.
    private static void RestartLevel()
    {
        if (LevelSelect.Active)
        {
            return;
        }

        Wipe.Stop();
        Enter(_room.Name, arrived: false);
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
