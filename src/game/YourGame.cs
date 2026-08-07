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
        Debug.Init();
        LevelSelect.Init();
    }

    public void Update(float elapsedSeconds)
    {
        if (LevelSelect.Active)
        {
            LevelSelect.Update();

            // Entered from here rather than from the menu, so Room.Enter still runs from one place.
            // The room does not update the frame it is entered — its first frame is the next one.
            if (LevelSelect.Picked != null)
            {
                LevelSelect.Close();
                _room.Enter(LevelSelect.Picked);
            }

            return;
        }

        _room.Update(elapsedSeconds);
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

        Debug.Draw();
    }
}
