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

    // The player is one 8x8 sprite, which is what the facing flip mirrors about.
    private const int SprSize = 8;

    // The miss, shouted over the head. The font advances 4 px a character, so half the string is
    // what centres it on the sprite.
    private const string FailText = "FAILED!";
    private const int FontAdvance = 4;
    private const int FailTextY = 10;

    // Fallbacks for CLUBX and REACH under PLAYER / STATS, used only until those fields are
    // authored. CLUBX is the sprite-local x of the club head at address facing right — where the
    // ball has to be for the swing to look like it connects, mirrored for facing left, and past
    // the sprite edge is fine. REACH is how far off that point the ball can still be addressed.
    private const int DefaultClubX = 9;
    private const int DefaultReach = 4;

    public static int X;
    public static int Y;
    public static bool OnGround;
    public static bool FacingLeft;

    private static int Spr;
    private static int HitX;
    private static int HitY;
    private static int HitW;
    private static int HitH;
    private static float MoveSpeed;
    private static float Gravity;
    private static float JumpSpeed;
    private static float MaxFallSpeed;
    private static int ClubX;
    private static int Reach;

    private static float VelX;
    private static float VelY;
    private static float RemX;
    private static float RemY;

    /// <summary>Where the club head sits right now, in map-sheet pixels — the ball's target.</summary>
    public static int ClubPointX => X + (FacingLeft ? SprSize - 1 - ClubX : ClubX);

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
        ClubX = DefaultClubX;
        Reach = DefaultReach;

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
            ClubX = stats.GetInt("CLUBX", 0, DefaultClubX);
            Reach = stats.GetInt("REACH", 0, DefaultReach);
        }

        X = room.PlayerX;
        Y = room.PlayerY;
        VelX = 0f;
        VelY = 0f;
        RemX = 0f;
        RemY = 0f;
        OnGround = false;
        FacingLeft = false;

        Swing.Init();
    }

    /// <summary>
    /// Whether a swing may start here: both feet down and the ball within reach of the club head.
    /// The <see cref="Swing"/> asks before leaving Idle, so a press over open ground does nothing.
    /// </summary>
    public static bool CanStartSwing()
    {
        if (!OnGround || !Ball.InPlay)
        {
            return false;
        }

        // Vertically the ball is measured against the whole body, not its centre: a ball resting by
        // the player's feet is level with them, and at this reach a centre-to-centre test would
        // read that as out of range.
        int dy = 0;
        if (Ball.CenterY < Y)
        {
            dy = Y - Ball.CenterY;
        }
        else if (Ball.CenterY > Y + SprSize - 1)
        {
            dy = Ball.CenterY - (Y + SprSize - 1);
        }

        return YourGame.API.abs(Ball.CenterX - ClubPointX) <= Reach && dy <= Reach;
    }

    /// <summary>
    /// Slides the player so the club head lands on the ball, keeping the facing they addressed it
    /// with. Walked a pixel at a time rather than assigned, so the snap can never push them into a
    /// wall — it stops flush against one and the swing plays from wherever it got to.
    /// </summary>
    public static void AlignToBall()
    {
        if (!Ball.Present)
        {
            return;
        }

        int target = Ball.CenterX - (FacingLeft ? SprSize - 1 - ClubX : ClubX);
        int step = target < X ? -1 : 1;

        while (X != target && !SolidAt(X + step, Y))
        {
            X += step;
        }

        RemX = 0f;
        VelX = 0f;
    }

    public static void Update(float elapsedSeconds)
    {
        var api = YourGame.API;

        OnGround = SolidAt(X, Y + 1);

        VelX = 0f;

        // Addressing the ball commits the player: no walking off it, no jumping out of it, until
        // the swing has run itself back to Idle.
        if (!Swing.Active)
        {
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

            if (OnGround && api.btnp(BtnJump))
            {
                VelY = -JumpSpeed;
                OnGround = false;
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

        Swing.Update(elapsedSeconds);
    }

    public static void Draw()
    {
        YourGame.API.spr(Spr, X, Y, 1, 1, 1f, FacingLeft);

        // The club is its own sprite over the player, so it swings without the body animating.
        if (Swing.Active)
        {
            YourGame.API.spr(Swing.Sprite, X, Y, 1, 1, 1f, FacingLeft);
        }

        if (Swing.Failed)
        {
            YourGame.API.print(FailText, X + SprSize / 2 - FailText.Length * FontAdvance / 2,
                Y - FailTextY, Constants.Colors.Red);
        }

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
