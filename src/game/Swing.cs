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
///
/// X (C) backs out of a swing that has not been taken yet — addressed or pulled back — putting the
/// club away and dropping the meter. From the swing-through on there is nothing left to cancel.
/// </summary>
internal static class Swing
{
    private const string AnimPull = "GOLFPULL";
    private const string AnimHit = "GOLFHIT";

    private const string PowerGroup = "SWING";
    private const string PowerObject = "POWER";
    private const string ClubObject = "CLUB";

    // B (X). Button 4 is the jump, so the swing sits on the next one.
    private const int BtnSwing = 5;

    // X (C). Backs out of an addressed or pulled-back swing.
    private const int BtnCancel = 6;

    // A miss threshold of a whole bar would leave no shot to scale, so the read is kept under one.
    private const float MaxMiss = 0.99f;

    // Club on ball. Only on contact that actually sends the ball off — a whiff is silent.
    private const int SfxHit = 0;

    private enum Phase { Idle, Ready, Pull, Hit }

    private static readonly Anim Clip = new Anim();

    private static Phase Current;
    private static float Seconds;
    private static bool Armed;
    private static bool Launched;

    // Under MISS of the bar the club goes through the ball and the swing counts as a miss rather
    // than a very soft hit; MINHIT is what the weakest reading that does count is worth.
    private static float Miss;
    private static float MinHit;

    // The club at rest, before the pull.
    private static int SprReady;

    // Shortest gap between two presses that both count, whatever state they land in.
    private static float PressSeconds;

    // How long the finished hit stays on screen before the club is put away, and how long a whiff
    // does — there is nothing to watch in a whiff and the player wants another go.
    private static float HitSeconds;
    private static float FailSeconds;

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
        Miss = 0f;
        MinHit = 0f;
        SprReady = 0;
        PressSeconds = 0f;
        HitSeconds = 0f;
        FailSeconds = 0f;

        var api = YourGame.API;

        // Re-read every Init: Ctrl+S in the JSON editor rebuilds the data without a restart.
        var power = api.gjson(PowerGroup, PowerObject);
        if (power != null)
        {
            Miss = (float)api.mid(0f, power.GetDec("MISS"), MaxMiss);
            MinHit = (float)api.mid(0f, power.GetDec("MINHIT"), 1f);
        }

        var club = api.gjson(PowerGroup, ClubObject);
        if (club != null)
        {
            SprReady = club.GetInt("SPR");
            PressSeconds = (float)club.GetDec("PRESS");
            HitSeconds = (float)club.GetDec("HITSEC");
            FailSeconds = (float)club.GetDec("FAILSEC");
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
                api.sfx(SfxHit);
                Hud.CountHit();
            }
        }

        if (Current == Phase.Hit && Seconds >= (Failed ? FailSeconds : HitSeconds))
        {
            Current = Phase.Idle;
            Seconds = 0f;
            Failed = false;    // the shout goes away with the club
        }

        // Backing out. Only before anything is committed: once the swing-through is running the
        // shot has been taken and it plays itself out.
        if ((Current == Phase.Ready || Current == Phase.Pull) && api.btnp(BtnCancel))
        {
            Current = Phase.Idle;
            Seconds = 0f;    // the gap still applies, so the club cannot be flicked straight back out
            Power = 0f;
            Failed = false;
            Meter.Stop();
            return;
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
                Meter.Show();
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
