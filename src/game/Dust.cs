namespace mono8.game;

/// <summary>
/// Foot dust: white flecks, one or two pixels a side, kicked up behind the <see cref="Player"/>
/// while they walk along the ground. Authored under ANIM / PLRWALK alongside the walk clip itself,
/// so the dust is retuned with the animation that throws it.
///
/// The pool is fixed and allocated once. A spawn with no free slot is dropped rather than growing
/// the array, which is what keeps the effect free of per-frame allocation.
/// </summary>
internal static class Dust
{
    private const string JsonGroup = "ANIM";
    private const string JsonObject = "PLRWALK";

    // Past this the effect is a fog rather than a few flecks, and the pool is walked every frame.
    private const int PoolLimit = 64;

    private struct Fleck
    {
        public float X;
        public float Y;
        public float VelX;
        public float VelY;

        /// <summary>Life left in seconds; 0 is a free slot.</summary>
        public float Seconds;

        public bool Big;
    }

    private static readonly Fleck[] Flecks = new Fleck[PoolLimit];

    private static int Count;
    private static float Rate;
    private static float Life;
    private static int SpawnX;
    private static int SpawnY;
    private static float SpeedX;
    private static float SpeedY;
    private static float Gravity;
    private static float BigChance;

    // Fractional particle carried between frames, so a rate that does not divide the frame still
    // spawns at the authored average rather than rounding down to nothing.
    private static float SpawnRem;

    public static void Init()
    {
        Count = 0;
        Rate = 0f;
        Life = 0f;
        SpawnX = 0;
        SpawnY = 0;
        SpeedX = 0f;
        SpeedY = 0f;
        Gravity = 0f;
        BigChance = 0f;
        SpawnRem = 0f;

        // Re-read every Init: Ctrl+S in the JSON editor rebuilds the data without a restart.
        var data = YourGame.API.gjson(JsonGroup, JsonObject);
        if (data != null)
        {
            Count = data.GetInt("PRTMAX");
            Rate = (float)data.GetDec("PRTRATE");
            Life = (float)data.GetDec("PRTLIFE");
            (SpawnX, SpawnY) = data.GetXY("PRTPOS");

            // Both speeds are the upper end of a random draw, so PosXY holds the pair.
            var (speedX, speedY) = data.GetXY("PRTVEL");
            SpeedX = speedX;
            SpeedY = speedY;

            Gravity = (float)data.GetDec("PRTGRAV");
            BigChance = (float)data.GetDec("PRTBIG");
        }

        Count = (int)YourGame.API.mid(0, Count, PoolLimit);

        for (int i = 0; i < PoolLimit; i++)
        {
            Flecks[i].Seconds = 0f;
        }
    }

    /// <summary>
    /// Kicks up this frame's share of dust. <paramref name="originX"/> and <paramref name="originY"/>
    /// are the player sprite's top-left in map-sheet pixels; PRTPOS is mirrored about
    /// <paramref name="sprSize"/> the same way the sprite is, so the flecks stay behind the feet
    /// whichever way they face.
    /// </summary>
    public static void Emit(float elapsedSeconds, int originX, int originY, int sprSize, bool facingLeft)
    {
        if (Count <= 0 || Rate <= 0f || Life <= 0f)
        {
            return;
        }

        SpawnRem += Rate * elapsedSeconds;
        while (SpawnRem >= 1f)
        {
            SpawnRem -= 1f;
            Spawn(originX + (facingLeft ? sprSize - 1 - SpawnX : SpawnX), originY + SpawnY, facingLeft);
        }
    }

    public static void Update(float elapsedSeconds)
    {
        for (int i = 0; i < Count; i++)
        {
            ref Fleck fleck = ref Flecks[i];
            if (fleck.Seconds <= 0f)
            {
                continue;
            }

            fleck.Seconds -= elapsedSeconds;
            if (fleck.Seconds <= 0f)
            {
                fleck.Seconds = 0f;
                continue;
            }

            fleck.VelY += Gravity * elapsedSeconds;
            fleck.X += fleck.VelX * elapsedSeconds;
            fleck.Y += fleck.VelY * elapsedSeconds;
        }
    }

    public static void Draw()
    {
        var api = YourGame.API;

        for (int i = 0; i < Count; i++)
        {
            ref Fleck fleck = ref Flecks[i];
            if (fleck.Seconds <= 0f)
            {
                continue;
            }

            int x = (int)api.flr(fleck.X);
            int y = (int)api.flr(fleck.Y);

            if (fleck.Big)
            {
                api.rectfill(x, y, x + 1, y + 1, Constants.Colors.White);
            }
            else
            {
                api.pixel(x, y, Constants.Colors.White);
            }
        }
    }

    private static void Spawn(int x, int y, bool facingLeft)
    {
        int slot = FreeSlot();
        if (slot < 0)
        {
            return;
        }

        var api = YourGame.API;

        // Half the authored speed is the floor, so a fleck always trails visibly rather than
        // sometimes being dropped on the spot.
        float away = SpeedX * (0.5f + api.rnd(0.5f));

        ref Fleck fleck = ref Flecks[slot];
        fleck.X = x;
        fleck.Y = y;
        fleck.VelX = facingLeft ? away : -away;
        fleck.VelY = -api.rnd(SpeedY);
        fleck.Seconds = Life;
        fleck.Big = api.rnd(1f) < BigChance;
    }

    private static int FreeSlot()
    {
        for (int i = 0; i < Count; i++)
        {
            if (Flecks[i].Seconds <= 0f)
            {
                return i;
            }
        }

        return -1;
    }
}
