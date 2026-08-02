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
    // The room the game opens on: the object name under ROOMS in data.json, and where that room
    // sits on the map sheet — the first one at the top-left of map layer 1.
    private const string StartRoom = "1";
    private const int StartRoomCellX = 0;
    private const int StartRoomCellY = 0;

    private readonly Room _room = new Room();

    public static IMono8API API;

    public YourGame(IMono8API api)
    {
        API = api;
    }

    // Everything in play lives inside a room, so the three methods are a forward to the current
    // one. Adding rooms is a matter of calling Enter again.
    public void Init()
    {
        Debug.Init();
        _room.Enter(StartRoom, StartRoomCellX, StartRoomCellY);
    }

    public void Update(float elapsedSeconds)
    {
        _room.Update(elapsedSeconds);
    }

    public void Draw()
    {
        API.cls(Constants.Colors.Blue);
        _room.Draw();
        Debug.Draw();
    }
}
