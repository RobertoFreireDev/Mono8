namespace mono8.game;

/// <summary>
/// ============================================================================
///  MONO8 — YOUR GAME
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
    public static IMono8API API;

    public YourGame(IMono8API api)
    {
        API = api;
    }

    // Runs again on every Ctrl+R and on the pause menu's Restart Game, so everything the game
    // holds has to be reset here rather than at its declaration.
    public void Init()
    {
    }

    public void Update(float elapsedSeconds)
    {
        if (API.btnp(0)) API.music(-1);
        if (API.btnp(1)) API.music(10, 0, 7);
        if (API.btnp(2)) API.music(30, 500, 7);
        if (API.btnp(3)) API.music(20, 500, 7);
        if (API.btnp(4)) API.sfx(1);
        if (API.btnp(5)) API.sfx(2);
        if (API.btnp(6)) API.sfx(38);
        if (API.btn(7)) API.sfx(1);
    }

    public void Draw()
    {
        API.cls(Constants.Colors.DarkBlue);
    }
}
