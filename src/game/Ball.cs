namespace mono8.game;

/// <summary>
/// The golf ball: a small rect rather than a sprite, blinking between white and light gray so it
/// stays readable against any terrain. It falls, bounces off autotile terrain and rolls to a stop,
/// and the <see cref="Swing"/> is the only thing that ever puts it back in the air.
///
/// Physics is the same axis-separated pixel step the <see cref="Player"/> uses: at this size a
/// single frame of travel can be many pixels, and stepping is what keeps it from tunnelling
/// through a thin wall.
///
/// Everything about it is authored under BALL / STATS in data.json. Nothing is tuned in code.
/// </summary>
internal static class Ball
{
    private const string StatsGroup = "BALL";
    private const string StatsObject = "STATS";

    /// <summary>Sprite flag marking solid ground; set it on every tile the ball must not pass through.</summary>
    private const int FlagSolid = 1;

    /// <summary>Side of the ball in pixels. The rect runs X..X+Size-1.</summary>
    public static int Size;

    public static bool Present;
    public static int X;
    public static int Y;
    public static bool OnGround;

    /// <summary>Set the moment the ball has sunk out of sight, and stays set until the room is
    /// re-entered — the hole is finished, whatever the game decides to do about that.</summary>
    public static bool Holed;

    private static float Gravity;
    private static float MaxFallSpeed;
    private static float Bounce;
    private static float Friction;
    private static float HitSpeedX;
    private static float HitSpeedY;
    private static float BlinkSeconds;

    // Below RestSpeed the ball is treated as stopped rather than jittering out a last few bounces.
    private static float RestSpeed;

    // The cup's hit box, in pixels from the flag sprite's top-left: the ball's centre is over the
    // hole while it is inside that rect. Alongside it, how slowly the ball has to be moving to drop
    // in rather than roll past, and how far it sinks before it is gone.
    private static int HoleX;
    private static int HoleY;
    private static int HoleW;
    private static int HoleH;
    private static float HoleSpeed;
    private static int SinkDepth;
    private static float SinkSpeed;

    private static float VelX;
    private static float VelY;
    private static float RemX;
    private static float RemY;
    private static float BlinkTimer;
    private static bool BlinkOn;
    private static bool Sinking;
    private static int Sunk;
    private static float SinkRem;

    public static int CenterX => X + Size / 2;
    public static int CenterY => Y + Size / 2;

    /// <summary>Whether the ball can still be addressed and struck — not once it is going down.</summary>
    public static bool InPlay => Present && !Sinking;

    public static void Init(Room room)
    {
        Size = 0;
        Gravity = 0f;
        MaxFallSpeed = 0f;
        Bounce = 0f;
        Friction = 0f;
        HitSpeedX = 0f;
        HitSpeedY = 0f;
        RestSpeed = 0f;
        HoleX = 0;
        HoleY = 0;
        HoleW = 0;
        HoleH = 0;
        HoleSpeed = 0f;
        SinkDepth = 0;
        SinkSpeed = 0f;
        int blink = 0;

        // Re-read every Init: Ctrl+S in the JSON editor rebuilds the data without a restart.
        var stats = YourGame.API.gjson(StatsGroup, StatsObject);
        if (stats != null)
        {
            Size = stats.GetInt("SIZE");
            Gravity = (float)stats.GetDec("GRAVITY");
            MaxFallSpeed = (float)stats.GetDec("MAXFALL");
            Bounce = (float)stats.GetDec("BOUNCE");
            Friction = (float)stats.GetDec("FRICTION");
            HitSpeedX = (float)stats.GetDec("HITX");
            HitSpeedY = (float)stats.GetDec("HITY");
            blink = stats.GetInt("BLINK");
            RestSpeed = (float)stats.GetDec("REST");
            (HoleX, HoleY) = stats.GetXY("HOLEPOS");
            (HoleW, HoleH) = stats.GetXY("HOLESIZE");
            HoleSpeed = (float)stats.GetDec("HOLESPD");
            SinkDepth = stats.GetInt("SINKDEP");
            SinkSpeed = (float)stats.GetDec("SINKSPD");
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
        Holed = false;
        Sinking = false;
        Sunk = 0;
        SinkRem = 0f;
    }

    /// <summary>
    /// Sends the ball off the club. <paramref name="toLeft"/> is the player's facing, so the ball
    /// always leaves in front of them; <paramref name="power"/> is the strength meter reading, 0 to
    /// 1, scaling HITX / HITY — a full bar is the authored speed, and a dead one barely nudges it.
    /// Returns whether there was a ball there to send, which is what the HUD counts.
    ///
    /// HITX and HITY are read as a speed and a launch angle rather than two velocities, so the
    /// selected <see cref="Club"/> can loft the shot or shorten it without either number being
    /// re-authored per club.
    /// </summary>
    public static bool Hit(bool toLeft, float power)
    {
        if (!InPlay)
        {
            return false;
        }

        var api = YourGame.API;

        power = (float)api.mid(0f, power, 1f);

        float speed = (float)api.sqrt(HitSpeedX * HitSpeedX + HitSpeedY * HitSpeedY)
            * power * Club.Distance;
        float angle = Club.AngleFrom(api.atan2(HitSpeedY, HitSpeedX), power);

        // sin is negated to match the downward y-axis, so a positive angle already lifts the ball.
        VelX = (toLeft ? -speed : speed) * (float)api.cos(angle);
        VelY = speed * (float)api.sin(angle);
        OnGround = false;
        return true;
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

        if (Sinking)
        {
            Sink(elapsedSeconds);
            return;
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

        // Asked after the move, so the velocities read are what the ball came to rest with rather
        // than what it started the frame on.
        if (OnGround && api.abs(VelX) <= HoleSpeed && api.abs(VelY) <= HoleSpeed && OverHole())
        {
            Sinking = true;
            VelX = 0f;
            VelY = 0f;
            RemX = 0f;
            RemY = 0f;
        }
    }

    public static void Draw()
    {
        // Skipped when SIZE is unauthored, since an empty rect would draw inverted.
        if (!Present || Size <= 0)
        {
            return;
        }

        YourGame.API.rectfill(X, Y, X + Size - 1, Y + Size - 1,
            BlinkOn ? Constants.Colors.White : Constants.Colors.LightGray);
    }

    /// <summary>
    /// The cup's hit box over the flag it is measured from. Its own call rather than part of
    /// <see cref="Draw"/>, because the hole outlives the ball — it still wants drawing once the ball
    /// has sunk — and because it has to land over the flag sprite, not under it.
    /// </summary>
    public static void DrawHoleDebug()
    {
        // Skipped when HOLESIZE is unauthored, since an empty rect would draw inverted.
        if (!Debug.Enabled || !Flag.Present || HoleW <= 0 || HoleH <= 0)
        {
            return;
        }

        int left = Flag.X + HoleX;
        int top = Flag.Y + HoleY;

        YourGame.API.rect(left, top, left + HoleW - 1, top + HoleH - 1, Constants.Colors.Green);
    }

    /// <summary>
    /// The cup, which the game reads off the flag rather than the map: the flag marks the hole, so
    /// the ball is in it once its centre has settled inside the authored hit box.
    /// </summary>
    private static bool OverHole()
    {
        if (!Flag.Present || HoleW <= 0 || HoleH <= 0)
        {
            return false;
        }

        int left = Flag.X + HoleX;
        int top = Flag.Y + HoleY;

        return CenterX >= left && CenterX <= left + HoleW - 1
            && CenterY >= top && CenterY <= top + HoleH - 1;
    }

    // Straight down through the green, terrain ignored — the cup is a hole in ground the map still
    // reads as solid, so collision here would only stop the ball on its lip.
    private static void Sink(float elapsedSeconds)
    {
        // An unauthored or zeroed SINKSPD would leave the ball hanging over the cup forever, so it
        // drops in the frame it starts instead.
        SinkRem += SinkSpeed > 0f ? SinkSpeed * elapsedSeconds : SinkDepth;

        while (SinkRem >= 1f && Sunk < SinkDepth)
        {
            SinkRem -= 1f;
            Y++;
            Sunk++;
        }

        if (Sunk >= SinkDepth)
        {
            Present = false;
            Holed = true;
        }
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
        return YourGame.API.mcol(x, y, Size, Size, FlagSolid);
    }
}
