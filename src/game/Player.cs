namespace mono8.game;

/// <summary>
/// Platformer player: left/right movement, gravity and a small jump, colliding
/// with autotile terrain through an axis-separated pixel-stepped hitbox.
///
/// Everything about it is authored in data.json — the stats under PLAYER / STATS, the spawn on
/// the <see cref="Room"/>. Nothing is tuned in code.
/// </summary>
internal static class Player
{
    private const string StatsGroup = "PLAYER";
    private const string StatsObject = "STATS";

    private const int BtnLeft = 0;
    private const int BtnRight = 1;
    private const int BtnJump = 4;

    public static int X;
    public static int Y;
    public static bool OnGround;

    private static int Spr;
    private static int HitX;
    private static int HitY;
    private static int HitW;
    private static int HitH;
    private static float MoveSpeed;
    private static float Gravity;
    private static float JumpSpeed;
    private static float MaxFallSpeed;

    private static float VelX;
    private static float VelY;
    private static float RemX;
    private static float RemY;
    private static bool FacingLeft;

    public static void Init(Room room)
    {
        Spr = 0;
        HitX = 0;
        HitY = 0;
        HitW = 0;
        HitH = 0;
        MoveSpeed = 0f;
        Gravity = 0f;
        JumpSpeed = 0f;
        MaxFallSpeed = 0f;

        // Re-read every Init: Ctrl+S in the JSON editor rebuilds the data without a restart.
        var stats = YourGame.API.gjson(StatsGroup, StatsObject);
        if (stats != null)
        {
            Spr = stats.GetInt("SPR");
            (HitX, HitY) = stats.GetXY("HITPOS");
            (HitW, HitH) = stats.GetXY("HITSIZE");
            MoveSpeed = (float)stats.GetDec("SPEED");
            Gravity = (float)stats.GetDec("GRAVITY");
            JumpSpeed = (float)stats.GetDec("JUMP");
            MaxFallSpeed = (float)stats.GetDec("MAXFALL");
        }

        X = room.PlayerX;
        Y = room.PlayerY;
        VelX = 0f;
        VelY = 0f;
        RemX = 0f;
        RemY = 0f;
        OnGround = false;
        FacingLeft = false;
    }

    public static void Update(float elapsedSeconds)
    {
        var api = YourGame.API;

        VelX = 0f;
        if (api.btn(BtnLeft))
        {
            VelX = -MoveSpeed;
            FacingLeft = true;
        }
        if (api.btn(BtnRight))
        {
            VelX = MoveSpeed;
            FacingLeft = false;
        }

        OnGround = SolidAt(X, Y + 1);

        if (OnGround && api.btnp(BtnJump))
        {
            VelY = -JumpSpeed;
            OnGround = false;
        }

        VelY += Gravity * elapsedSeconds;
        if (VelY > MaxFallSpeed)
        {
            VelY = MaxFallSpeed;
        }

        MoveX(VelX * elapsedSeconds);
        MoveY(VelY * elapsedSeconds);

        OnGround = SolidAt(X, Y + 1);
    }

    public static void Draw()
    {
        YourGame.API.spr(Spr, X, Y, 1, 1, 1f, FacingLeft);

        // The rect SolidAt actually tests. Skipped when HITSIZE is unauthored, since an empty rect
        // would draw inverted.
        if (Debug.Enabled && HitW > 0 && HitH > 0)
        {
            YourGame.API.rect(X + HitX, Y + HitY, X + HitX + HitW - 1, Y + HitY + HitH - 1,
                Constants.Colors.Red);
        }
    }

    // One pixel at a time so a fast mover can never step over a thin wall, and so the stop lands
    // flush against the terrain — autotile edges are quadrant-precise, with no tile edge to snap to.
    private static void MoveX(float amount)
    {
        RemX += amount;
        int steps = (int)YourGame.API.round(RemX);
        RemX -= steps;

        int step = steps < 0 ? -1 : 1;
        while (steps != 0)
        {
            if (SolidAt(X + step, Y))
            {
                RemX = 0f;
                VelX = 0f;
                return;
            }

            X += step;
            steps -= step;
        }
    }

    private static void MoveY(float amount)
    {
        RemY += amount;
        int steps = (int)YourGame.API.round(RemY);
        RemY -= steps;

        int step = steps < 0 ? -1 : 1;
        while (steps != 0)
        {
            if (SolidAt(X, Y + step))
            {
                RemY = 0f;
                VelY = 0f;
                return;
            }

            Y += step;
            steps -= step;
        }
    }

    // An unauthored HITSIZE is empty, and an empty rect meets nothing.
    private static bool SolidAt(int x, int y)
    {
        return YourGame.API.acol(x + HitX, y + HitY, HitW, HitH);
    }
}
