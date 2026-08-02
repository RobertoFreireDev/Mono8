namespace mono8.game;

/// <summary>
/// The golf swing, owned and run by the <see cref="Player"/> and drawn over it. One press at a
/// time: the club comes out ready, the next press pulls it back (ANIM / GOLFPULL), the next swings
/// it through (ANIM / GOLFHIT). Two seconds after the hit the club is put away, and the press after
/// that starts the whole thing over.
///
/// Neither clip loops — each holds the frame it ends on. One press moves the swing exactly one
/// state: the button has to be let go and the state has to have settled before the next one counts,
/// so the swing cannot be mashed through.
/// </summary>
internal static class Swing
{
    private const string AnimPull = "GOLFPULL";
    private const string AnimHit = "GOLFHIT";

    // The club at rest, before the pull.
    private const int SprReady = 9;

    // B (X). Button 4 is the jump, so the swing sits on the next one.
    private const int BtnSwing = 5;

    // Shortest gap between two presses that both count, whatever state they land in.
    private const float PressSeconds = 0.25f;

    // How long the finished hit stays on screen before the club is put away.
    private const float HitSeconds = 2f;

    private enum Phase { Idle, Ready, Pull, Hit }

    private static readonly Anim Clip = new Anim();

    private static Phase Current;
    private static float Seconds;
    private static bool Armed;

    /// <summary>While true the club is on screen and <see cref="Sprite"/> is the frame to draw.</summary>
    public static bool Active => Current != Phase.Idle;

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
    }

    public static void Update(float elapsedSeconds)
    {
        var api = YourGame.API;

        Seconds += elapsedSeconds;

        if (Current == Phase.Pull || Current == Phase.Hit)
        {
            Clip.Update(elapsedSeconds);
        }

        if (Current == Phase.Hit && Seconds >= HitSeconds)
        {
            Current = Phase.Idle;
            Seconds = 0f;
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
                break;
            case Phase.Ready:
                Current = Phase.Pull;
                Clip.Load(AnimPull, false);
                break;
            default:
                Current = Phase.Hit;
                Clip.Load(AnimHit, false);
                break;
        }
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
            case Phase.Pull:
                return Clip.Done;    // no hitting before the pull has finished
            case Phase.Hit:
                return false;        // the hit runs itself out
            default:
                return true;
        }
    }
}
