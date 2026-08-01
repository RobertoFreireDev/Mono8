namespace mono8.game;

/// <summary>
/// The room's flag: placed at ROOMS / &lt;room&gt; / FLAGPOS, animated by ANIM / FLAG. Both come
/// from data.json — nothing about it is authored in code.
/// </summary>
internal static class Flag
{
    private const string JsonGroup = "ROOMS";
    private const string FieldPos = "FLAGPOS";
    private const string AnimName = "FLAG";

    private static readonly Anim Wave = new Anim();

    public static bool Present;
    public static int X;
    public static int Y;

    /// <summary>
    /// <paramref name="roomName"/> is the object name under ROOMS. A room without a FLAGPOS simply
    /// has no flag.
    /// </summary>
    public static void Init(string roomName, int roomCellX, int roomCellY)
    {
        Present = false;
        X = 0;
        Y = 0;
        Wave.Load(AnimName);

        var room = YourGame.API.gjson(JsonGroup, roomName);
        if (room == null || !room.Has(FieldPos))
        {
            return;
        }

        // FLAGPOS is in pixels within the room, so a room's flag keeps its place when the room moves.
        var (px, py) = room.GetXY(FieldPos);
        X = roomCellX * 8 + px;
        Y = roomCellY * 8 + py;
        Present = true;
    }

    public static void Update(float elapsedSeconds)
    {
        if (Present)
        {
            Wave.Update(elapsedSeconds);
        }
    }

    public static void Draw()
    {
        if (Present)
        {
            YourGame.API.spr(Wave.Sprite, X, Y);
        }
    }
}
