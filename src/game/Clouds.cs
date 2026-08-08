namespace mono8.game;

/// <summary>
/// The clouds drifting across a room's sky. Every object under CLOUDS except CONFIG is one *kind* of
/// cloud — a sprite id and its size in tiles — and CONFIG says how many are up at once, so the sheet
/// decides what a cloud looks like and the config decides how busy the sky is. A kind can be up more
/// than once; there is no relation between how many are authored and how many are flying.
///
/// CONFIG holds the bands everything about a cloud is drawn from: MINCLOUD and MAXCLOUD as how many
/// are alive at a time, STRTPOSX and STRTPOSY as a min and a max to start along and hang at, SPEED
/// as the list of drift speeds to pick one of, and MINDISTX / MINDISTY as the clearance one cloud
/// keeps from another. Kind, mirroring, row and speed are all re-rolled when a cloud has crossed the
/// screen and comes back on at the left, so a sky watched long enough never settles into the same
/// row twice.
///
/// Drift is always to the right — one wind for the whole sky, which is why the speed is a single
/// number and not a vector.
///
/// Positions are screen pixels taken off the room's corner, exactly as the <see cref="Sun"/>'s are:
/// the clouds are drawn with the room's camera up, so that corner is what turns a screen position
/// into the map-sheet pixels everything inside a room is measured in.
/// </summary>
internal static class Clouds
{
    private const string JsonGroup = "CLOUDS";

    /// <summary>The one object under CLOUDS that is not a cloud.</summary>
    private const string ConfigObject = "CONFIG";

    private const string FieldSpr = "SPRIDX";
    private const string FieldTilesX = "TILESX";
    private const string FieldTilesY = "TILESY";
    private const string FieldMinAlive = "MINCLOUD";
    private const string FieldMaxAlive = "MAXCLOUD";
    private const string FieldStartX = "STRTPOSX";
    private const string FieldStartY = "STRTPOSY";
    private const string FieldSpeed = "SPEED";
    private const string FieldMinDistX = "MINDISTX";
    private const string FieldMinDistY = "MINDISTY";

    // Past this the sky is weather rather than clouds, and the pool is walked every frame. Well
    // clear of anything MAXCLOUD is likely to ask for, and what that field is clamped to.
    private const int PoolLimit = 32;

    // The authoring limits: 64 objects to a group, 16 items to an array.
    private const int KindLimit = 64;
    private const int SpeedLimit = 16;

    // Rolls of the row before a placement gives up and queues the cloud off the left instead. A
    // handful is enough to find a gap in any sky worth looking at, and the fallback is always clear,
    // so this is what bounds the search rather than what makes it succeed.
    private const int PlaceTries = 8;

    // A cloud that authors no size is one tile rather than none, so a half-authored object still
    // shows something to fix rather than nothing at all.
    private const int DefaultTiles = 1;

    // What an unauthored CONFIG runs on: a handful of clouds started anywhere along the width of the
    // screen, hanging in its top half, drifting slowly enough to read as weather and not as traffic,
    // and keeping a couple of tiles of air between them.
    private const int DefaultMinAlive = 3;
    private const int DefaultMaxAlive = 6;
    private const int DefaultStartMinX = 0;
    private const int DefaultStartMaxX = Constants.Screen.ResolutionX;
    private const int DefaultStartMinY = 0;
    private const int DefaultStartMaxY = Constants.Screen.ResolutionY / 2;
    private const int DefaultSpeed = 4;
    private const int DefaultMinDistX = 2 * Terrain.TileSize;
    private const int DefaultMinDistY = Terrain.TileSize;

    /// <summary>One authored cloud object — what a cloud can look like, not a cloud in the sky.</summary>
    private struct Kind
    {
        public int Spr;
        public int TilesX;
        public int TilesY;
    }

    private struct Cloud
    {
        public int Spr;
        public int TilesX;
        public int TilesY;

        // Fractional, so a speed that does not divide the frame still drifts at the authored rate
        // rather than rounding down to a standstill.
        public float X;
        public int Y;
        public float Speed;

        /// <summary>
        /// Mirrored on the way in, so three authored kinds read as six. X only — a cloud upside down
        /// is a different cloud, but one facing the other way is the same weather.
        /// </summary>
        public bool FlipX;
    }

    private static readonly Kind[] Kinds = new Kind[KindLimit];
    private static readonly Cloud[] Pool = new Cloud[PoolLimit];
    private static readonly int[] Speeds = new int[SpeedLimit];

    private static int KindCount;
    private static int SpeedCount;

    /// <summary>How many of the pool are flying. Drawn between MINCLOUD and MAXCLOUD on room entry.</summary>
    private static int Count;

    private static int MinAlive;
    private static int MaxAlive;
    private static int StartMinX;
    private static int StartMaxX;
    private static int StartMinY;
    private static int StartMaxY;
    private static int MinDistX;
    private static int MinDistY;

    private static int OriginX;
    private static int OriginY;

    /// <summary>
    /// <paramref name="room"/> lends nothing but its corner — which screenful of the sheet the sky
    /// is drawn over. Everything else about a cloud is the CLOUDS group's.
    /// </summary>
    public static void Init(Room room)
    {
        OriginX = room.OriginX;
        OriginY = room.OriginY;

        Load();

        // How busy this room's sky is, settled once on the way in: a cloud that leaves comes back
        // rather than being retired, so the count holds for as long as the level is up.
        Count = KindCount > 0 ? Between(MinAlive, MaxAlive) : 0;

        // Started across the whole band rather than all at the left edge: a room is entered with a
        // sky already in it, not with one that fills up over the first minute of play. Each is
        // placed against the ones already down — hence the limit of i rather than Count, since the
        // slots above this one still hold the last room's clouds.
        for (int i = 0; i < Count; i++)
        {
            Place(ref Pool[i], i, i, spread: true);
        }
    }

    public static void Update(float elapsedSeconds)
    {
        for (int i = 0; i < Count; i++)
        {
            ref Cloud cloud = ref Pool[i];
            cloud.X += cloud.Speed * elapsedSeconds;

            // Fully past the right edge, so a cloud is never cut off mid-sprite by being sent back.
            if (cloud.X > Constants.Screen.ResolutionX)
            {
                Place(ref cloud, i, Count, spread: false);
            }
        }
    }

    public static void Draw()
    {
        var api = YourGame.API;

        for (int i = 0; i < Count; i++)
        {
            ref Cloud cloud = ref Pool[i];
            api.spr(cloud.Spr, OriginX + (int)api.flr(cloud.X), OriginY + cloud.Y, cloud.TilesX, cloud.TilesY,
                flipX: cloud.FlipX);
        }
    }

    /// <summary>
    /// Rolls a cloud fresh and finds it somewhere clear of the others. <paramref name="self"/> is its
    /// own slot and <paramref name="limit"/> how far up the pool is worth comparing against — the
    /// whole of it once the sky is flying, and only the slots already placed while one is being
    /// filled. <paramref name="spread"/> starts it anywhere along STRTPOSX, which is what a room is
    /// entered with; without it the cloud comes back on at the left, which is what a crossing ends
    /// in.
    /// </summary>
    private static void Place(ref Cloud cloud, int self, int limit, bool spread)
    {
        for (int attempt = 0; attempt < PlaceTries; attempt++)
        {
            Reroll(ref cloud);
            cloud.X = spread ? Between(StartMinX, StartMaxX) : ReturnX(cloud);

            if (!Crowded(ref cloud, self, limit))
            {
                return;
            }
        }

        // Every row it tried was taken — a sky asking for more clouds than the clearance leaves room
        // for. Rather than dropping the cloud on top of another it queues off the left of the lot,
        // which is clear by construction and drifts in as the ones ahead of it move on.
        cloud.X = Queued(ref cloud, self, limit);
    }

    /// <summary>
    /// Whether a cloud is inside another's clearance. Both axes have to be short of it: two clouds
    /// on the same row with a screen between them are not crowded, and neither are two stacked at
    /// opposite ends of the sky. Measured between the sprite boxes rather than their corners, so a
    /// wide kind keeps its distance as honestly as a narrow one.
    /// </summary>
    private static bool Crowded(ref Cloud cloud, int self, int limit)
    {
        int x = (int)YourGame.API.flr(cloud.X);
        int w = cloud.TilesX * Terrain.TileSize;
        int h = cloud.TilesY * Terrain.TileSize;

        for (int i = 0; i < limit; i++)
        {
            if (i == self)
            {
                continue;
            }

            ref Cloud other = ref Pool[i];
            int otherX = (int)YourGame.API.flr(other.X);

            if (Gap(x, w, otherX, other.TilesX * Terrain.TileSize) < MinDistX
                && Gap(cloud.Y, h, other.Y, other.TilesY * Terrain.TileSize) < MinDistY)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Where a cloud that has crossed the screen comes back on: the left end of the authored band,
    /// or a sprite's width off the screen when that band starts on screen — one returning into the
    /// middle of the sky would pop into view rather than drift in.
    /// </summary>
    private static float ReturnX(in Cloud cloud)
    {
        int offScreen = -cloud.TilesX * Terrain.TileSize;

        return StartMinX < offScreen ? StartMinX : offScreen;
    }

    /// <summary>
    /// A full clearance behind the leftmost cloud there is, so the placement always has an answer
    /// however tight the sky is authored.
    /// </summary>
    private static float Queued(ref Cloud cloud, int self, int limit)
    {
        float x = ReturnX(cloud);
        float width = cloud.TilesX * Terrain.TileSize;

        for (int i = 0; i < limit; i++)
        {
            if (i == self)
            {
                continue;
            }

            float behind = Pool[i].X - width - MinDistX;
            if (behind < x)
            {
                x = behind;
            }
        }

        return x;
    }

    /// <summary>
    /// Everything about a cloud that is drawn fresh — which kind it is, its row and its speed.
    /// Called on the way in and again every time one comes back on at the left, so the sky keeps
    /// shuffling instead of looping the arrangement it entered with.
    /// </summary>
    private static void Reroll(ref Cloud cloud)
    {
        var api = YourGame.API;

        ref Kind kind = ref Kinds[api.rnd(KindCount)];
        cloud.Spr = kind.Spr;
        cloud.TilesX = kind.TilesX;
        cloud.TilesY = kind.TilesY;

        cloud.Y = Between(StartMinY, StartMaxY);
        cloud.Speed = Speeds[api.rnd(SpeedCount)];
        cloud.FlipX = api.rnd(2) == 0;
    }

    private static void Load()
    {
        KindCount = 0;
        SpeedCount = 0;
        MinAlive = DefaultMinAlive;
        MaxAlive = DefaultMaxAlive;
        StartMinX = DefaultStartMinX;
        StartMaxX = DefaultStartMaxX;
        StartMinY = DefaultStartMinY;
        StartMaxY = DefaultStartMaxY;
        MinDistX = DefaultMinDistX;
        MinDistY = DefaultMinDistY;

        var api = YourGame.API;

        // Re-read every Init: Ctrl+S in the JSON editor rebuilds the data without a restart, so a
        // cloud authored — or deleted — lands without one either.
        int objects = api.gjsoncount(JsonGroup);

        for (int i = 0; i < objects && KindCount < KindLimit; i++)
        {
            string name = api.gjsonobj(JsonGroup, i);
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            // Matched the way gjson matches, since the object names are the developer's to case.
            if (string.Equals(name, ConfigObject, StringComparison.OrdinalIgnoreCase))
            {
                LoadConfig(api.gjson(JsonGroup, name));
                continue;
            }

            var data = api.gjson(JsonGroup, name);
            if (data == null)
            {
                continue;
            }

            // Sprite 0 is the empty one and draws nothing, so an object with no SPRIDX behind it is
            // dropped rather than kept as a kind that crosses the sky invisibly.
            int spr = data.GetInt(FieldSpr, 0, 0);
            if (spr <= 0)
            {
                continue;
            }

            ref Kind kind = ref Kinds[KindCount];
            kind.Spr = spr;
            kind.TilesX = data.GetInt(FieldTilesX, 0, DefaultTiles);
            kind.TilesY = data.GetInt(FieldTilesY, 0, DefaultTiles);

            if (kind.TilesX < 1)
            {
                kind.TilesX = DefaultTiles;
            }

            if (kind.TilesY < 1)
            {
                kind.TilesY = DefaultTiles;
            }

            KindCount++;
        }

        if (SpeedCount == 0)
        {
            Speeds[0] = DefaultSpeed;
            SpeedCount = 1;
        }
    }

    /// <summary>
    /// CONFIG's bands. STRTPOSX and STRTPOSY are read as a pair by index — item 0 the low end, item
    /// 1 the high — so a field authored as a scalar reads as a band starting there and ending at the
    /// code's own far end rather than as an error.
    /// </summary>
    private static void LoadConfig(Mono8JsonObject data)
    {
        if (data == null)
        {
            return;
        }

        MinAlive = data.GetInt(FieldMinAlive, 0, DefaultMinAlive);
        MaxAlive = data.GetInt(FieldMaxAlive, 0, DefaultMaxAlive);
        StartMinX = data.GetInt(FieldStartX, 0, DefaultStartMinX);
        StartMaxX = data.GetInt(FieldStartX, 1, DefaultStartMaxX);
        StartMinY = data.GetInt(FieldStartY, 0, DefaultStartMinY);
        StartMaxY = data.GetInt(FieldStartY, 1, DefaultStartMaxY);
        MinDistX = data.GetInt(FieldMinDistX, 0, DefaultMinDistX);
        MinDistY = data.GetInt(FieldMinDistY, 0, DefaultMinDistY);

        // A band authored the other way round is still a band — read rather than thrown on, since
        // nothing downstream can say which end was meant. An empty sky is a legitimate MINCLOUD, so
        // only the negative is corrected, and the pool is the ceiling however high MAXCLOUD is.
        Order(ref MinAlive, ref MaxAlive);
        Order(ref StartMinX, ref StartMaxX);
        Order(ref StartMinY, ref StartMaxY);

        MinAlive = (int)YourGame.API.mid(0, MinAlive, PoolLimit);
        MaxAlive = (int)YourGame.API.mid(MinAlive, MaxAlive, PoolLimit);

        // A negative clearance is no clearance: it would read every pair as far enough apart, which
        // is what an unauthored 0 means anyway.
        if (MinDistX < 0)
        {
            MinDistX = 0;
        }

        if (MinDistY < 0)
        {
            MinDistY = 0;
        }

        int listed = data.Count(FieldSpeed);

        for (int i = 0; i < listed && SpeedCount < SpeedLimit; i++)
        {
            // A speed of nothing is a cloud pinned to the sky, which would sit there for the whole
            // level: dropped rather than loaded, and a negative one would never reach the edge that
            // sends it back.
            int speed = data.GetInt(FieldSpeed, i, 0);
            if (speed <= 0)
            {
                continue;
            }

            Speeds[SpeedCount] = speed;
            SpeedCount++;
        }
    }

    /// <summary>Air between two extents on one axis. Negative while they overlap.</summary>
    private static int Gap(int a, int aSize, int b, int bSize)
    {
        return a < b ? b - (a + aSize) : a - (b + bSize);
    }

    private static int Between(int min, int max)
    {
        return max > min ? min + YourGame.API.rnd(max - min + 1) : min;
    }

    private static void Order(ref int min, ref int max)
    {
        if (min > max)
        {
            (min, max) = (max, min);
        }
    }
}
