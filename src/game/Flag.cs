namespace mono8.game;

/// <summary>
/// The room's flag: placed by the <see cref="Room"/>, animated by ANIM / FLAG. Both come from
/// data.json — nothing about it is authored in code.
/// </summary>
internal static class Flag
{
    private const string AnimName = "FLAG";

    private static readonly Anim Wave = new Anim();

    public static bool Present;
    public static int X;
    public static int Y;

    /// <summary>
    /// A room without a FLAGPOS simply has no flag.
    /// </summary>
    public static void Init(Room room)
    {
        Present = room.HasFlag;
        X = room.FlagX;
        Y = room.FlagY;
        Wave.Load(AnimName);
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
