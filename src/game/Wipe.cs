namespace mono8.game;

/// <summary>
/// The iris between levels. Sinking the ball ends the hole, so the screen closes onto the player
/// through an <c>ovalinv</c> mask shrunk down onto them, the next level is loaded behind it, and the
/// same oval opens back out on whoever is standing in the room that came up.
///
/// It outlives the <see cref="Room"/> — it is what carries one into the next — so it is driven by
/// <see cref="YourGame"/> rather than by anything inside a room, and nothing a room does to itself
/// resets it. Everything in the room keeps running underneath: the body still falls, the club still
/// finishes the swing it was mid-way through. What stops is the controls, which the
/// <see cref="Player"/> drops for as long as this is <see cref="Active"/> — the hole is over and
/// nothing pressed can change it.
///
/// Tuned under GAME / WIPE in data.json, with a default for every field so it runs unauthored.
/// </summary>
internal static class Wipe
{
    private const string JsonGroup = "GAME";
    private const string JsonObject = "WIPE";
    private const string FieldWait = "WAITSEC";
    private const string FieldClose = "OUTSEC";
    private const string FieldOpen = "INSEC";
    private const string FieldColor = "COLOR";
    private const string FieldDither = "DITHER";

    // GAME/WIPE is not authored yet, so everything it holds has a default here. The colour and the
    // dither sprite are a pair rather than two choices: 117 is a checkerboard of DarkerBlue and
    // White, so the ring only reads as the mask thinning out while the mask is that same DarkerBlue.
    private const float DefaultWaitSeconds = 0.5f;
    private const float DefaultCloseSeconds = 0.6f;
    private const float DefaultOpenSeconds = 0.6f;
    private const int DefaultColor = Constants.Colors.DarkerBlue;
    private const int DefaultDither = 117;

    // The holes in that sprite are authored in white rather than in colour 0, so white is what has
    // to be knocked out for the room to show through the ring.
    private const int DitherHole = Constants.Colors.White;

    private enum Phase { None, Wait, Close, Held, Open }

    private static Phase Current;
    private static float Seconds;

    private static float WaitSeconds;
    private static float CloseSeconds;
    private static float OpenSeconds;
    private static int Color;
    private static int Dither;

    // What the oval closes onto and opens out of, in screen pixels — the caller takes the room's
    // origin off, since the mask is drawn with the camera home. Followed every frame rather than
    // taken once, so a body still falling when the ball dropped keeps the iris on it.
    private static int FocusX;
    private static int FocusY;

    /// <summary>The wipe has the screen. The room underneath runs on, but its controls are off.</summary>
    public static bool Active => Current != Phase.None;

    /// <summary>
    /// The close has run out and the screen is covered — the frame to swap rooms on. Held rather
    /// than handed over, so whoever drives it decides what comes back up: <see cref="Open"/> onto
    /// the level that was loaded, or <see cref="Stop"/> when there is no level and the game is
    /// going back to the menu, which is its own screen and wants no mask over it.
    /// </summary>
    public static bool Closed => Current == Phase.Held;

    public static void Init() => Stop();

    public static void Stop()
    {
        Current = Phase.None;
        Seconds = 0f;
    }

    /// <summary>
    /// Starts the close. The wait in front of it is what leaves the ball in the cup long enough to
    /// be seen going in, rather than the screen shutting on the frame it lands.
    /// </summary>
    public static void Start()
    {
        // Read on every start rather than once, so a Ctrl+S retune lands on the next hole.
        Load();

        Current = Phase.Wait;
        Seconds = 0f;
    }

    public static void Open()
    {
        Current = Phase.Open;
        Seconds = 0f;
    }

    public static void Update(float elapsedSeconds, int focusX, int focusY)
    {
        if (Current == Phase.None)
        {
            return;
        }

        FocusX = focusX;
        FocusY = focusY;
        Seconds += elapsedSeconds;

        switch (Current)
        {
            case Phase.Wait:
                if (Seconds >= WaitSeconds)
                {
                    Current = Phase.Close;
                    Seconds = 0f;
                }
                break;

            case Phase.Close:
                if (Seconds >= CloseSeconds)
                {
                    Current = Phase.Held;
                    Seconds = 0f;
                }
                break;

            case Phase.Open:
                if (Seconds >= OpenSeconds)
                {
                    Stop();
                }
                break;
        }
    }

    public static void Draw()
    {
        // The wait is the room still fully on screen: there is nothing to mask yet.
        if (Current == Phase.None || Current == Phase.Wait)
        {
            return;
        }

        var api = YourGame.API;

        float open = (float)api.mid(0f, Openness(), 1f);
        float full = FullWidth() * open;

        int w = (int)api.round(full);
        int h = (int)api.round(full * Constants.Screen.ResolutionY / Constants.Screen.ResolutionX);

        // The ring is tiled from the sheet the way spr draws, so palt reaches it — and this sprite
        // marks its holes in white. A hole of nothing is what ovalinv reads as the covered screen,
        // so the held frame needs no case of its own.
        api.palt(DitherHole, true);
        api.ovalinv(FocusX - w / 2, FocusY - h / 2, w, h, Color, Dither);
        api.palt();
    }

    /// <summary>1 is the screen wide open, 0 is covered.</summary>
    private static float Openness()
    {
        switch (Current)
        {
            // An unauthored duration would divide by zero, and an instant wipe is the honest reading
            // of one: covered the frame the close starts, open the frame the reveal does.
            case Phase.Close:
                return CloseSeconds > 0f ? 1f - Seconds / CloseSeconds : 0f;
            case Phase.Open:
                return OpenSeconds > 0f ? Seconds / OpenSeconds : 1f;
            default:
                return 0f;
        }
    }

    // The oval that just swallows the screen from wherever the focus is — the corner furthest from
    // it is the one that has to sit inside. The hole keeps the screen's proportions so it closes
    // evenly instead of pinching, and solving (dx/a)² + (dy/b)² = 1 with b tied to a by that ratio
    // is what leaves the width below.
    private static float FullWidth()
    {
        var api = YourGame.API;

        double dx = api.max(FocusX, Constants.Screen.ResolutionX - FocusX);
        double dy = api.max(FocusY, Constants.Screen.ResolutionY - FocusY)
            * ((double)Constants.Screen.ResolutionX / Constants.Screen.ResolutionY);

        return 2f * (float)api.sqrt(dx * dx + dy * dy);
    }

    // Zeroed to the defaults first, then read, so a half-authored WIPE still runs on the fields it
    // does carry.
    private static void Load()
    {
        WaitSeconds = DefaultWaitSeconds;
        CloseSeconds = DefaultCloseSeconds;
        OpenSeconds = DefaultOpenSeconds;
        Color = DefaultColor;
        Dither = DefaultDither;

        var wipe = YourGame.API.gjson(JsonGroup, JsonObject);
        if (wipe == null)
        {
            return;
        }

        WaitSeconds = (float)wipe.GetDec(FieldWait, 0, DefaultWaitSeconds);
        CloseSeconds = (float)wipe.GetDec(FieldClose, 0, DefaultCloseSeconds);
        OpenSeconds = (float)wipe.GetDec(FieldOpen, 0, DefaultOpenSeconds);
        Color = wipe.GetInt(FieldColor, 0, DefaultColor);
        Dither = wipe.GetInt(FieldDither, 0, DefaultDither);
    }
}
