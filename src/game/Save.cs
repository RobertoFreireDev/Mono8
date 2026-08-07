namespace mono8.game;

/// <summary>
/// What the player has finished, kept in the engine's persistence slots (data.save) so it outlives
/// the run — one slot per level, read once when the game starts.
///
/// A slot holds the strokes the level was sunk in, or <see cref="NotPlayed"/> for a level with no
/// result behind it. Only a finished hole writes a count: leaving a level, losing it to spent
/// strokes or walking out of bounds all leave the slot exactly as it was.
///
/// Level N is slot N, and N is the room's NUMBER — not its object name, which is the developer's to
/// change. <see cref="Levels"/> is what turns one into the other. Slot 0 is <see cref="Debug"/>'s,
/// which is why the levels start at 1.
/// </summary>
internal static class Save
{
    /// <summary>A level with no result behind it. What every slot reads as on a fresh save.</summary>
    public const int NotPlayed = -1;

    // dget/dset hold 64 ints and drop anything past them, which is where Levels.MaxNumber comes
    // from: the last level that can be recorded is the last slot there is.
    private const int SlotCount = Levels.MaxNumber + 1;

    // Read once at Init and kept here, so asking after a level is an array read rather than a trip
    // through the save file.
    private static readonly int[] Hits = new int[SlotCount];

    public static void Init()
    {
        var api = YourGame.API;

        for (int slot = Levels.MinNumber; slot < SlotCount; slot++)
        {
            int stored = api.dget(slot);

            // A fresh save reads 0 in every slot, so 0 is "nothing written here yet" rather than a
            // hole finished in no strokes. A level with a room behind it is written back out as
            // NotPlayed so the file says as much in its own terms; the rest are only mapped in
            // memory, since there is no level there to record anything for.
            if (stored == 0)
            {
                Hits[slot] = NotPlayed;

                if (Levels.Exists(slot))
                {
                    api.dset(slot, NotPlayed);
                }

                continue;
            }

            Hits[slot] = stored;
        }
    }

    /// <summary>
    /// The strokes level <paramref name="number"/> was sunk in, or <see cref="NotPlayed"/> for a
    /// level never finished — and for anything that is not a level at all.
    /// </summary>
    public static int Get(int number)
    {
        return number >= Levels.MinNumber && number < SlotCount ? Hits[number] : NotPlayed;
    }

    /// <summary>Whether the level has ever been sunk.</summary>
    public static bool Played(int number)
    {
        return Get(number) != NotPlayed;
    }

    /// <summary>
    /// A hole sunk, in <paramref name="hits"/> strokes. Called when the ball drops into the cup and
    /// nowhere else, so a level lost or walked away from keeps whatever was there. A room that
    /// authors no NUMBER has no slot and is not recorded.
    /// </summary>
    public static void Complete(int number, int hits)
    {
        if (number < Levels.MinNumber || number >= SlotCount)
        {
            return;
        }

        // dset writes the whole file every call, so replaying a level to the same score is not
        // written out again.
        if (Hits[number] == hits)
        {
            return;
        }

        Hits[number] = hits;
        YourGame.API.dset(number, hits);
    }
}
