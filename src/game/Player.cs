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
    private const int BtnUp = 2;
    private const int BtnDown = 3;
    private const int BtnJump = 4;

    // The engine's font advances 4 px a character, which is how the miss is centred on the sprite.
    private const int FontAdvance = 4;

    public static int X;
    public static int Y;
    public static bool OnGround;
    public static bool FacingLeft;

    /// <summary>On a stair — no gravity, no walking, no jumping, until one of its ends lets go.</summary>
    public static bool Climbing;

    private static int Spr;

    // Side of the player sprite in pixels, which is what the facing flip mirrors about.
    private static int SprSize;

    private static int HitX;
    private static int HitY;
    private static int HitW;
    private static int HitH;
    private static float MoveSpeed;
    private static float ClimbSpeed;
    private static float Gravity;
    private static float JumpSpeed;
    private static float MaxFallSpeed;

    // CLUBX is the sprite-local x of the club head at address facing right — where the ball has to
    // be for the swing to look like it connects, mirrored for facing left, and past the sprite edge
    // is fine. REACH is how far off that point the ball can still be addressed, x and y apart: x is
    // measured from the club head, y from the whole body.
    private static int ClubX;
    private static int ReachX;
    private static int ReachY;

    // The miss, shouted over the head.
    private static string FailText;
    private static int FailTextY;

    private static float VelX;
    private static float VelY;
    private static float RemX;
    private static float RemY;

    /// <summary>Where the club head sits right now, in map-sheet pixels — the ball's target.</summary>
    public static int ClubPointX => X + (FacingLeft ? SprSize - 1 - ClubX : ClubX);

    public static void Init(Room room)
    {
        Spr = 0;
        SprSize = 0;
        HitX = 0;
        HitY = 0;
        HitW = 0;
        HitH = 0;
        MoveSpeed = 0f;
        ClimbSpeed = 0f;
        Gravity = 0f;
        JumpSpeed = 0f;
        MaxFallSpeed = 0f;
        ClubX = 0;
        ReachX = 0;
        ReachY = 0;
        FailText = string.Empty;
        FailTextY = 0;

        // Re-read every Init: Ctrl+S in the JSON editor rebuilds the data without a restart.
        var stats = YourGame.API.gjson(StatsGroup, StatsObject);
        if (stats != null)
        {
            Spr = stats.GetInt("SPR");
            SprSize = stats.GetInt("SPRSIZE");
            (HitX, HitY) = stats.GetXY("HITPOS");
            (HitW, HitH) = stats.GetXY("HITSIZE");
            MoveSpeed = (float)stats.GetDec("SPEED");
            ClimbSpeed = (float)stats.GetDec("CLIMB");
            Gravity = (float)stats.GetDec("GRAVITY");
            JumpSpeed = (float)stats.GetDec("JUMP");
            MaxFallSpeed = (float)stats.GetDec("MAXFALL");
            ClubX = stats.GetInt("CLUBX");
            (ReachX, ReachY) = stats.GetXY("REACH");
            FailText = stats.GetStr("FAILTXT");
            FailTextY = stats.GetInt("FAILY");
        }

        X = room.PlayerX;
        Y = room.PlayerY;
        VelX = 0f;
        VelY = 0f;
        RemX = 0f;
        RemY = 0f;
        OnGround = false;
        FacingLeft = false;
        Climbing = false;

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

        return YourGame.API.abs(Ball.CenterX - ClubPointX) <= ReachX && dy <= ReachY;
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

        // A climber is never on the ground, which is what keeps the jump and the swing — both of
        // which ask — from firing off a stair.
        OnGround = !Climbing && SolidAt(X, Y + 1);

        // Addressing the ball commits the player just as the stair does: neither is left mid-swing.
        if (!Swing.Active && !Climbing)
        {
            TryGrabStair(api);
        }

        if (Climbing)
        {
            UpdateClimb(api, elapsedSeconds);
        }
        else
        {
            UpdateWalk(api, elapsedSeconds);
        }

        OnGround = !Climbing && SolidAt(X, Y + 1);

        Swing.Update(elapsedSeconds);
    }

    private static void UpdateWalk(IMono8API api, float elapsedSeconds)
    {
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
    }

    /// <summary>
    /// Up takes any stair the body is already standing in; Down takes the one under its feet, which
    /// is how a stair capping a platform is entered — from above those tiles are plain floor until
    /// they are asked for.
    /// </summary>
    private static void TryGrabStair(IMono8API api)
    {
        bool up = api.btn(BtnUp);
        bool down = api.btn(BtnDown);

        if (!up && !down)
        {
            return;
        }

        if (up && Terrain.StairColumn(X + HitX, Y + HitY, HitW, HitH, out int cellX))
        {
            Grab(cellX, 0);
            return;
        }

        if (!down || !OnGround)
        {
            return;
        }

        // The step down has to have somewhere to go. Standing on the floor at the foot of a stair
        // the body still overlaps its tiles, so without the floor test the grab would take there and
        // its one-pixel step in would drive the player into the ground, a pixel a frame, for as long
        // as Down was held.
        int feet = Y + 1 + HitY;
        if (!Terrain.Blocked(X + HitX, feet, HitW, HitH)
            && Terrain.StairColumn(X + HitX, feet, HitW, HitH, out cellX))
        {
            // Stepping in from above starts a pixel down, inside the cap tile. Not cosmetic: a
            // frame's descent can round to no movement at all through RemY, and a body still level
            // with the platform reads as having finished the stair — so without the nudge the stair
            // would be released on the frame it was taken. The floor test above is what makes the
            // pixel safe to take.
            Grab(cellX, 1);
        }
    }

    private static void Grab(int cellX, int drop)
    {
        X = Terrain.CenterOnColumn(cellX, HitW) - HitX;
        Y += drop;
        Climbing = true;
        VelX = 0f;
        VelY = 0f;
        RemX = 0f;
        RemY = 0f;
    }

    /// <summary>
    /// One frame on the stair: up and down only, no gravity, and no way off except its two ends.
    /// <see cref="SolidAt"/> ignores solid the stair itself carries while this is running, so the
    /// cap tiles are climbed through rather than stopped at.
    /// </summary>
    private static void UpdateClimb(IMono8API api, float elapsedSeconds)
    {
        bool up = api.btn(BtnUp);

        VelX = 0f;
        VelY = 0f;
        if (up)
        {
            VelY -= ClimbSpeed;
        }
        if (api.btn(BtnDown))
        {
            VelY += ClimbSpeed;
        }

        MoveY(VelY * elapsedSeconds);

        // Off the top: the body has cleared every stair tile, which after climbing up through the
        // cap means standing on the platform it caps. Also catches a stair ending in mid-air, where
        // letting go is the same as falling.
        if (!Terrain.StairColumn(X + HitX, Y + HitY, HitW, HitH, out _))
        {
            Release();
            return;
        }

        // Down onto the floor. Gated on the climb not still pushing up, since at the foot of a
        // stair the floor is right there from the first frame.
        if (!up && Terrain.Blocked(X + HitX, Y + 1 + HitY, HitW, HitH))
        {
            Release();
        }
    }

    private static void Release()
    {
        Climbing = false;
        VelY = 0f;
        RemY = 0f;
    }

    public static void Draw()
    {
        YourGame.API.spr(Spr, X, Y, 1, 1, 1f, FacingLeft);

        // The club is its own sprite over the player, so it swings without the body animating.
        if (Swing.Active)
        {
            YourGame.API.spr(Swing.Sprite, X, Y, 1, 1, 1f, FacingLeft);
        }

        if (Swing.Failed && FailText.Length > 0)
        {
            YourGame.API.print(FailText, X + SprSize / 2 - FailText.Length * FontAdvance / 2,
                Y - FailTextY, Constants.Colors.Red);
        }

        // The rect SolidAt actually tests. Skipped when HITSIZE is unauthored, since an empty rect
        // would draw inverted.
        if (Debug.Enabled && HitW > 0 && HitH > 0)
        {
            YourGame.API.rect(X + HitX, Y + HitY, X + HitX + HitW - 1, Y + HitY + HitH - 1,
                Climbing ? Constants.Colors.Yellow : Constants.Colors.Red);
        }

        DrawAddressDebug();
    }

    /// <summary>
    /// Where the ball's centre has to be for <see cref="CanStartSwing"/> to say yes — CLUBX and
    /// REACH drawn as the box they add up to, green once the ball is actually inside it. There is no
    /// tuning those two blind, since the window is a relationship between the player and the ball
    /// rather than anything visible on either sprite.
    /// </summary>
    private static void DrawAddressDebug()
    {
        if (!Debug.Enabled)
        {
            return;
        }

        int mid = ClubPointX;

        YourGame.API.rect(mid - ReachX, Y - ReachY, mid + ReachX, Y + SprSize - 1 + ReachY,
            CanStartSwing() ? Constants.Colors.Green : Constants.Colors.Pink);
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
    //
    // The branch is the whole stair feature: MoveX and MoveY ask this and nothing else, so a climb
    // passes through the tiles the stair caps itself with while a real ceiling or floor still stops
    // it dead.
    private static bool SolidAt(int x, int y)
    {
        return Climbing
            ? Terrain.Blocked(x + HitX, y + HitY, HitW, HitH)
            : Terrain.Solid(x + HitX, y + HitY, HitW, HitH);
    }
}
