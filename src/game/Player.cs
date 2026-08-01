namespace mono8.game;

/// <summary>
/// Platformer player: left/right movement, gravity and a small jump, colliding
/// with autotile terrain through an axis-separated pixel-stepped hitbox.
/// </summary>
internal static class Player
{
    // data.json: PLAYER / STATS. The consts below are the fallbacks, so an unauthored — or
    // half-authored — object still runs the tuning the game shipped with.
    private const string JsonGroup = "PLAYER";
    private const string JsonObject = "STATS";

    private const int DefaultSpr = 8;          // 1x1
    private const int DefaultSpawnCellX = 7;   // spawn cell, relative to the room's origin
    private const int DefaultSpawnCellY = 15;
    private const int DefaultHitX = 0;         // hitbox, relative to the sprite's top-left
    private const int DefaultHitY = 0;
    private const int DefaultHitW = 8;
    private const int DefaultHitH = 8;
    private const float DefaultSpeed = 60f;    // px/s
    private const float DefaultGravity = 450f; // px/s^2
    private const float DefaultJump = 130f;    // px/s, upwards
    private const float DefaultMaxFall = 200f;

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

    public static void Init(int roomCellX, int roomCellY)
    {
        int spawnCellX = DefaultSpawnCellX;
        int spawnCellY = DefaultSpawnCellY;

        Spr = DefaultSpr;
        HitX = DefaultHitX;
        HitY = DefaultHitY;
        HitW = DefaultHitW;
        HitH = DefaultHitH;
        MoveSpeed = DefaultSpeed;
        Gravity = DefaultGravity;
        JumpSpeed = DefaultJump;
        MaxFallSpeed = DefaultMaxFall;

        // Re-read every Init: Ctrl+S in the JSON editor rebuilds the data without a restart.
        var stats = YourGame.API.gjson(JsonGroup, JsonObject);
        if (stats != null)
        {
            Spr = stats.GetInt("SPR", 0, DefaultSpr);

            if (stats.Has("SPAWN"))
            {
                (spawnCellX, spawnCellY) = stats.GetXY("SPAWN");
            }
            if (stats.Has("HITPOS"))
            {
                (HitX, HitY) = stats.GetXY("HITPOS");
            }
            // A zero-sized hitbox meets nothing and would drop the player through the floor, so a
            // missing or mistyped HITSIZE keeps the default rather than what GetXY falls back to.
            var (hitW, hitH) = stats.GetXY("HITSIZE");
            if (hitW > 0 && hitH > 0)
            {
                HitW = hitW;
                HitH = hitH;
            }

            MoveSpeed = (float)stats.GetDec("SPEED", 0, DefaultSpeed);
            Gravity = (float)stats.GetDec("GRAVITY", 0, DefaultGravity);
            JumpSpeed = (float)stats.GetDec("JUMP", 0, DefaultJump);
            MaxFallSpeed = (float)stats.GetDec("MAXFALL", 0, DefaultMaxFall);
        }

        X = (roomCellX + spawnCellX) * 8;
        Y = (roomCellY + spawnCellY) * 8;
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

    private static bool SolidAt(int x, int y)
    {
        return YourGame.API.acol(x + HitX, y + HitY, HitW, HitH);
    }
}
