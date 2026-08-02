namespace mono8.game;

/// <summary>
/// The golf swing, owned and run by the <see cref="Player"/> and drawn over it. One press at a
/// time: the club comes out ready, the next press pulls it back (ANIM / GOLFPULL), the next swings
/// it through (ANIM / GOLFHIT). Two seconds after the hit the club is put away, and the press after
/// that starts the whole thing over.
///
/// The pull is where the shot is decided: the <see cref="Meter"/> sweeps for as long as the club is
/// back, and the press that swings through reads it once. Under the authored MISS the swing misses
/// outright; anything above it is spread over MINHIT to full, so even a swing caught early moves
/// the ball.
///
/// Neither clip loops — each holds the frame it ends on. One press moves the swing exactly one
/// state: the button has to be let go and the state has to have settled before the next one counts,
/// so the swing cannot be mashed through.
/// </summary>
internal static class Swing
{
    private const string AnimPull = "GOLFPULL";
    private const string AnimHit = "GOLFHIT";

    private const string StatsGroup = "SWING";
    private const string StatsObject = "POWER";

    // The club at rest, before the pull.
    private const int SprReady = 9;

    // B (X). Button 4 is the jump, so the swing sits on the next one.
    private const int BtnSwing = 5;

    // Shortest gap between two presses that both count, whatever state they land in.
    private const float PressSeconds = 0.25f;

    // How long the finished hit stays on screen before the club is put away.
    private const float HitSeconds = 2f;

    // A whiff is over quickly — there is nothing to watch and the player wants another go.
    private const float FailSeconds = 0.5f;

    // Fallbacks for SWING / POWER, used until MISS and MINHIT are authored. Under MISS of the bar
    // the club goes through the ball and the swing counts as a miss rather than a very soft hit;
    // MINHIT is what the weakest reading that does count is worth, since at the authored hit speeds
    // anything under about a third of the bar barely leaves the tile the ball is on.
    private const float DefaultMiss = 0.1f;
    private const float DefaultMinHit = 0.35f;

    // A miss threshold of a whole bar would leave no shot to scale, so the read is kept under one.
    private const float MaxMiss = 0.99f;

    private enum Phase { Idle, Ready, Pull, Hit }

    private static readonly Anim Clip = new Anim();

    private static Phase Current;
    private static float Seconds;
    private static bool Armed;
    private static bool Launched;
    private static float Miss;
    private static float MinHit;

    /// <summary>While true the club is on screen and <see cref="Sprite"/> is the frame to draw.</summary>
    public static bool Active => Current != Phase.Idle;

    /// <summary>How hard the ball is struck, 0 to 1 — the meter reading the swing-through was
    /// started on, run through <see cref="Strength"/>. 0 on a miss.</summary>
    public static float Power { get; private set; }

    /// <summary>
    /// The swing was started under the MISS threshold: the ball stays put, the club is put away
    /// after <see cref="FailSeconds"/>, and the <see cref="Player"/> draws the miss over the head.
    /// </summary>
    public static bool Failed { get; private set; }

    public static int Sprite => Current == Phase.Ready ? SprReady : Clip.Sprite;

    /// <summary>For the debug overlay. Literals, so reading it every frame allocates nothing.</summary>
    public static string State => Current switch
    {
        Phase.Ready => "READY",
        Phase.Pull => "PULL",
        Phase.Hit => "HIT",
        _ => "IDLE",
    };

    public static void Init()
    {
        Current = Phase.Idle;
        Seconds = 0f;
        Armed = true;
        Launched = true;
        Power = 0f;
        Failed = false;
        Miss = DefaultMiss;
        MinHit = DefaultMinHit;

        // Re-read every Init: Ctrl+S in the JSON editor rebuilds the data without a restart.
        var stats = YourGame.API.gjson(StatsGroup, StatsObject);
        if (stats != null)
        {
            var api = YourGame.API;
            Miss = (float)api.mid(0f, stats.GetDec("MISS", 0, DefaultMiss), MaxMiss);
            MinHit = (float)api.mid(0f, stats.GetDec("MINHIT", 0, DefaultMinHit), 1f);
        }

        Meter.Init();
    }

    public static void Update(float elapsedSeconds)
    {
        var api = YourGame.API;

        Seconds += elapsedSeconds;

        if (Current == Phase.Pull || Current == Phase.Hit)
        {
            Clip.Update(elapsedSeconds);
        }

        Meter.Update(elapsedSeconds);

        // Contact is the end of the swing-through, not its start, so the ball leaves when the club
        // has actually come round. Once per hit.
        if (Current == Phase.Hit && !Launched && Clip.Done)
        {
            Launched = true;
            if (!Failed && Ball.Hit(Player.FacingLeft, Power))
            {
                Hud.CountHit();
            }
        }

        if (Current == Phase.Hit && Seconds >= (Failed ? FailSeconds : HitSeconds))
        {
            Current = Phase.Idle;
            Seconds = 0f;
            Failed = false;    // the shout goes away with the club
        }

        // Letting go is what re-arms the button, so a press can never be spent twice.
        if (!Armed && !api.btn(BtnSwing))
        {
            Armed = true;
        }

        if (!Armed || !api.btnp(BtnSwing) || !Accepts())
        {
            return;
        }

        Armed = false;
        Seconds = 0f;

        switch (Current)
        {
            case Phase.Idle:
                Current = Phase.Ready;
                Failed = false;
                Player.AlignToBall();
                break;
            case Phase.Ready:
                Current = Phase.Pull;
                Clip.Load(AnimPull, false);
                Meter.Start();
                break;
            default:
                // The press that swings through is also what stops the meter, so the reading is
                // taken before anything else this frame can move it.
                float reading = Meter.Value;
                Failed = reading < Miss;
                Power = Failed ? 0f : Strength(reading);
                Meter.Stop();
                Current = Phase.Hit;
                Launched = false;
                Clip.Load(AnimHit, false);
                break;
        }
    }

    // The bar is not what the ball leaves with: MISS..1 is stretched onto MINHIT..1, so every swing
    // that connects at all connects hard enough to see, and a full bar is still the authored hit
    // speed.
    private static float Strength(float reading)
    {
        float over = (float)YourGame.API.mid(0f, (reading - Miss) / (1f - Miss), 1f);
        return MinHit + over * (1f - MinHit);
    }

    // What makes a press too fast: the club has to be settled where the last one left it.
    private static bool Accepts()
    {
        if (Seconds < PressSeconds)
        {
            return false;
        }

        switch (Current)
        {
            case Phase.Idle:
                return Player.CanStartSwing();    // nothing to address, nothing to swing at
            case Phase.Pull:
                return Clip.Done;    // no hitting before the pull has finished
            case Phase.Hit:
                return false;        // the hit runs itself out
            default:
                return true;
        }
    }
}
