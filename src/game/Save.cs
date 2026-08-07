namespace mono8.game;

/// <summary>
/// What the player has finished, kept in the engine's persistence slots (data.save) so it outlives
/// the run — one slot per level, read once when the game starts.
///
/// A slot holds the strokes the level was sunk in, or <see cref="NotPlayed"/> for a level with no
/// result behind it. Only a finished hole writes a count: leaving a level, losing it to spent
/// strokes or walking out of bounds all leave the slot exactly as it was.
///
/// Level N is slot N — the <see cref="LevelSelect"/> names its rooms with the number they stand for,
/// so the room name is the slot. Slot 0 is <see cref="Debug"/>'s, which is why the levels start at 1.
/// </summary>
internal static class Save
{
    /// <summary>A level with no result behind it. What every slot reads as on a fresh save.</summary>
    public const int NotPlayed = -1;

    // Debug owns slot 0. dget/dset hold 64 ints and drop anything past them, so 63 is the highest
    // level that can be recorded at all — well past the 20 the grid offers.
    private const int FirstSlot = 1;
    private const int SlotCount = 64;

    // Read once at Init and kept here, so asking after a level is an array read rather than a trip
    // through the save file.
    private static readonly int[] Hits = new int[SlotCount];

    public static void Init()
    {
        var api = YourGame.API;

        for (int slot = FirstSlot; slot < SlotCount; slot++)
        {
            int stored = api.dget(slot);

            // A fresh save reads 0 in every slot, so 0 is "nothing written here yet" rather than a
            // hole finished in no strokes. A level with a room behind it is written back out as
            // NotPlayed so the file says as much in its own terms; the rest are only mapped in
            // memory, since there is no level there to record anything for.
            if (stored == 0)
            {
                Hits[slot] = NotPlayed;

                if (Room.Exists(slot.ToString()))
                {
                    api.dset(slot, NotPlayed);
                }

                continue;
            }

            Hits[slot] = stored;
        }
    }

    /// <summary>
    /// The strokes <paramref name="name"/> was sunk in, or <see cref="NotPlayed"/> for a level never
    /// finished — and for anything that is not a level at all.
    /// </summary>
    public static int Get(string name)
    {
        int slot = SlotOf(name);

        return slot < 0 ? NotPlayed : Hits[slot];
    }

    /// <summary>Whether the level has ever been sunk.</summary>
    public static bool Played(string name)
    {
        return Get(name) != NotPlayed;
    }

    /// <summary>
    /// A hole sunk, in <paramref name="hits"/> strokes. Called when the ball drops into the cup and
    /// nowhere else, so a level lost or walked away from keeps whatever was there.
    /// </summary>
    public static void Complete(string name, int hits)
    {
        int slot = SlotOf(name);

        if (slot < 0)
        {
            return;
        }

        // dset writes the whole file every call, so replaying a level to the same score is not
        // written out again.
        if (Hits[slot] == hits)
        {
            return;
        }

        Hits[slot] = hits;
        YourGame.API.dset(slot, hits);
    }

    // Level N is slot N. Room takes any object under ROOMS, but only the numbered ones are levels —
    // anything else has no slot and is not recorded.
    private static int SlotOf(string name)
    {
        if (!int.TryParse(name, out int level))
        {
            return -1;
        }

        return level >= FirstSlot && level < SlotCount ? level : -1;
    }
}
