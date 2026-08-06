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

    // The authoring limit on one array.
    private const int MaxSounds = 16;

    private static readonly int[] Sounds = new int[MaxSounds];

    private static int Count;
    private static float Interval;

    // Seconds left before the next step. 0 fires on the frame it is read, which is what makes a
    // walk start on a footfall rather than a quarter second of silence.
    private static float Left;

    public static void Init()
    {
        Count = 0;
        Interval = 0f;
        Left = 0f;

        // Re-read every Init: Ctrl+S in the JSON editor rebuilds the data without a restart.
        var data = YourGame.API.gjson(JsonGroup, JsonObject);
        if (data == null)
        {
            return;
        }

        int listed = data.Count(FieldSfx);
        for (int i = 0; i < listed && Count < MaxSounds; i++)
        {
            // A negative id stops channels rather than playing anything, so an unauthored or
            // wrong-typed entry is dropped instead of loaded.
            int id = data.GetInt(FieldSfx, i, -1);
            if (id >= 0)
            {
                Sounds[Count] = id;
                Count++;
            }
        }

        Interval = (float)data.GetDec(FieldInterval);
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

        if (Count == 0 || Interval <= 0f)
        {
            return;
        }

        Left -= elapsedSeconds;
        if (Left > 0f)
        {
            return;
        }

        Left = Interval;
        YourGame.API.sfx(Sounds[YourGame.API.rnd(Count)]);
    }
}
