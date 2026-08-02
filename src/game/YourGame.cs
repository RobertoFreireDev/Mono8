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
    // Which room the game opens on, authored under GAME / START as the object name to look up in
    // ROOMS. Where that room sits on the map sheet is the room's own CELLPOS.
    private const string StartGroup = "GAME";
    private const string StartObject = "START";
    private const string FieldRoom = "ROOM";

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

        // Re-read every Init: Ctrl+S in the JSON editor rebuilds the data without a restart.
        var start = API.gjson(StartGroup, StartObject);
        _room.Enter(start != null ? start.GetStr(FieldRoom) : string.Empty);
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
