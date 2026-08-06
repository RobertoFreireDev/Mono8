namespace mono8.game;

/// <summary>
/// Footsteps: one of the sfx listed under ANIM / PLRWALK, drawn at random, every SFXSEC of walking
/// along the ground. Authored beside the walk clip and the dust it kicks up, so the whole stride is
/// retuned in one place.
///
/// The interval is wall time rather than a frame of the clip — SPEED is what the legs look like,
/// SFXSEC is what they sound like, and the two are tuned against each other rather than locked.
/// </summary>
internal static class Steps
{
    private const string JsonGroup = "ANIM";
    private const string JsonObject = "PLRWALK";

    private const string FieldSfx = "SFX";
    private const string FieldInterval = "SFXSEC";

    private static readonly SfxList Sounds = new SfxList();

    private static float Interval;

    // Seconds left before the next step. 0 fires on the frame it is read, which is what makes a
    // walk start on a footfall rather than a quarter second of silence.
    private static float Left;

    public static void Init()
    {
        Interval = 0f;
        Left = 0f;

        // Re-read every Init: Ctrl+S in the JSON editor rebuilds the data without a restart.
        Sounds.Load(JsonGroup, JsonObject, FieldSfx);

        var data = YourGame.API.gjson(JsonGroup, JsonObject);
        if (data != null)
        {
            Interval = (float)data.GetDec(FieldInterval);
        }
    }

    /// <summary>
    /// <paramref name="walking"/> is feet on the ground and actually travelling — a stair, a jump
    /// and a walk into a wall are all silent.
    /// </summary>
    public static void Update(float elapsedSeconds, bool walking)
    {
        // The count is dropped rather than paused, so the first step after a stop lands on the frame
        // the feet start moving again instead of finishing the interval it was interrupted in.
        if (!walking)
        {
            Left = 0f;
            return;
        }

        if (!Sounds.Any || Interval <= 0f)
        {
            return;
        }

        Left -= elapsedSeconds;
        if (Left > 0f)
        {
            return;
        }

        Left = Interval;
        Sounds.PlayRandom();
    }
}
