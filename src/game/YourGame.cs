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
    // A room is exactly one screen: 256x144 px = 32x18 cells. The first room sits at the
    // top-left of map layer 1, so room pixels and map-sheet pixels are the same space.
    private const int RoomCellW = 32;
    private const int RoomCellH = 18;

    private string _roomName;   // the object name under the ROOMS group in data.json
    private int _roomCellX;
    private int _roomCellY;

    public static IMono8API API;

    public YourGame(IMono8API api)
    {
        API = api;
    }

    public void Init()
    {
        _roomName = "1";
        _roomCellX = 0;
        _roomCellY = 0;

        Player.Init(_roomCellX, _roomCellY);
        Flag.Init(_roomName, _roomCellX, _roomCellY);
    }

    public void Update(float elapsedSeconds)
    {
        Player.Update(elapsedSeconds);
        Flag.Update(elapsedSeconds);
    }

    public void Draw()
    {
        API.cls(Constants.Colors.Blue);
        API.map(_roomCellX, _roomCellY, 0, 0, RoomCellW, RoomCellH);
        Flag.Draw();
        Player.Draw();
    }
}
