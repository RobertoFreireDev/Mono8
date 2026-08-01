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
///  The screen is 256 x 144 pixels, and colors are palette indices 0..31
///  (use Constants.Colors.* so you don't have to memorise the numbers).
///
///  `elapsedSeconds` is the time since the previous frame. Multiplying by it
///  ("delta time") makes movement run at the same real-world speed no matter
///  how fast the machine is.
///
///  Buttons are plain integers: player 1 uses 0..7, player 2 uses 8..15.
///  API.btn(b) is true while the button is HELD, API.btnp(b) only on the frame
///  it goes down.
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

    public void Init()
    {
    }

    public void Update(float elapsedSeconds)
    {
    }

    public void Draw()
    {
        API.cls(Constants.Colors.DarkBlue);
    }
}
