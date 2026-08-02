namespace mono8.game;

/// <summary>
/// The golf ball: a 2x2 rect rather than a sprite, blinking between white and light gray so it
/// stays readable against any terrain. It falls, bounces off autotile terrain and rolls to a stop,
/// and the <see cref="Swing"/> is the only thing that ever puts it back in the air.
///
/// Physics is the same axis-separated pixel step the <see cref="Player"/> uses: at this size a
/// single frame of travel can be many pixels, and stepping is what keeps it from tunnelling
/// through a thin wall.
/// </summary>
internal static class Ball
{
    /// <summary>Side of the ball in pixels. The rect runs X..X+Size-1.</summary>
    public const int Size = 2;

    private const string StatsGroup = "BALL";
    private const string StatsObject = "STATS";

    // Fallbacks, so an unauthored BALL / STATS still plays. Speeds are px/s, gravity px/s².
    private const float DefaultGravity = 450f;
    private const float DefaultMaxFall = 200f;
    private const float DefaultBounce = 0.5f;
    private const float DefaultFriction = 120f;
    private const float DefaultHitX = 90f;
    private const float DefaultHitY = 110f;
    private const int DefaultBlink = 8;

    // Below this the ball is treated as stopped rather than jittering out a last few bounces.
    private const float RestSpeed = 12f;

    public static bool Present;
    public static int X;
    public static int Y;
    public static bool OnGround;

    private static float Gravity;
    private static float MaxFallSpeed;
    private static float Bounce;
    private static float Friction;
    private static float HitSpeedX;
    private static float HitSpeedY;
    private static float BlinkSeconds;

    private static float VelX;
    private static float VelY;
    private static float RemX;
    private static float RemY;
    private static float BlinkTimer;
    private static bool BlinkOn;

    public static int CenterX => X + Size / 2;
    public static int CenterY => Y + Size / 2;

    public static void Init(Room room)
    {
        Gravity = DefaultGravity;
        MaxFallSpeed = DefaultMaxFall;
        Bounce = DefaultBounce;
        Friction = DefaultFriction;
        HitSpeedX = DefaultHitX;
        HitSpeedY = DefaultHitY;
        int blink = DefaultBlink;

        // Re-read every Init: Ctrl+S in the JSON editor rebuilds the data without a restart.
        var stats = YourGame.API.gjson(StatsGroup, StatsObject);
        if (stats != null)
        {
            Gravity = (float)stats.GetDec("GRAVITY", 0, DefaultGravity);
            MaxFallSpeed = (float)stats.GetDec("MAXFALL", 0, DefaultMaxFall);
            Bounce = (float)stats.GetDec("BOUNCE", 0, DefaultBounce);
            Friction = (float)stats.GetDec("FRICTION", 0, DefaultFriction);
            HitSpeedX = (float)stats.GetDec("HITX", 0, DefaultHitX);
            HitSpeedY = (float)stats.GetDec("HITY", 0, DefaultHitY);
            blink = stats.GetInt("BLINK", 0, DefaultBlink);
        }

        BlinkSeconds = blink > 0 ? 1f / blink : 0f;

        Present = true;
        X = room.BallX;
        Y = room.BallY;
        VelX = 0f;
        VelY = 0f;
        RemX = 0f;
        RemY = 0f;
        OnGround = false;
        BlinkTimer = 0f;
        BlinkOn = true;
    }

    /// <summary>
    /// Sends the ball off the club. <paramref name="toLeft"/> is the player's facing, so the ball
    /// always leaves in front of them; <paramref name="power"/> is the strength meter reading, 0 to
    /// 1, scaling HITX / HITY — a full bar is the authored speed, and a dead one barely nudges it.
    /// </summary>
    public static void Hit(bool toLeft, float power)
    {
        if (!Present)
        {
            return;
        }

        power = (float)YourGame.API.mid(0f, power, 1f);

        VelX = (toLeft ? -HitSpeedX : HitSpeedX) * power;
        VelY = -HitSpeedY * power;
        OnGround = false;
    }

    public static void Update(float elapsedSeconds)
    {
        if (!Present)
        {
            return;
        }

        var api = YourGame.API;

        if (BlinkSeconds > 0f)
        {
            BlinkTimer += elapsedSeconds;
            while (BlinkTimer >= BlinkSeconds)
            {
                BlinkTimer -= BlinkSeconds;
                BlinkOn = !BlinkOn;
            }
        }

        OnGround = SolidAt(X, Y + 1);

        // Rolling only sheds speed on the ground; in the air the ball keeps its horizontal throw.
        if (OnGround)
        {
            float drop = Friction * elapsedSeconds;
            if (api.abs(VelX) <= drop)
            {
                VelX = 0f;
            }
            else
            {
                VelX -= VelX < 0f ? -drop : drop;
            }
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
        if (!Present)
        {
            return;
        }

        YourGame.API.rectfill(X, Y, X + Size - 1, Y + Size - 1,
            BlinkOn ? Constants.Colors.White : Constants.Colors.LightGray);
    }

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
                VelX = -VelX * Bounce;
                if (YourGame.API.abs(VelX) < RestSpeed)
                {
                    VelX = 0f;
                }
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
                VelY = -VelY * Bounce;

                // A bounce this shallow would only buzz against the floor, so the ball settles.
                if (YourGame.API.abs(VelY) < RestSpeed)
                {
                    VelY = 0f;
                }
                return;
            }

            Y += step;
            steps -= step;
        }
    }

    private static bool SolidAt(int x, int y)
    {
        return YourGame.API.acol(x, y, Size, Size);
    }
}
