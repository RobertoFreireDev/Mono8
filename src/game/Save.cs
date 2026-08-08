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

    // The one pause-menu entry that is up on both screens: the grid is where progress is looked at,
    // so the menu is where a wipe is most wanted. LevelSelect owns 1, Debug 0, YourGame 2.
    private const int MenuIndex = 3;
    private const string MenuLabel = "DELETE SAVE";

    // Read once at Init and kept here, so asking after a level is an array read rather than a trip
    // through the save file.
    private static readonly int[] Hits = new int[SlotCount];

    public static void Init()
    {
        Read();

        // Registered once and never taken down: unlike the room entries there is no screen this makes
        // no sense on.
        YourGame.API.menuitem(MenuIndex, MenuLabel, Delete);
    }

    private static void Read()
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

    /// <summary>
    /// The pause menu's DELETE SAVE: back to a save that has never been written. Every slot goes, not
    /// just the levels — slot 0 is <see cref="Debug"/>'s and is persistence like any other — and then
    /// the file is read back in, so what is left says "not played" in the same terms a fresh save does
    /// rather than in terms of its own.
    /// </summary>
    private static void Delete()
    {
        var api = YourGame.API;

        // dset rewrites the whole file on every call, so a slot already empty is left alone.
        for (int slot = 0; slot < SlotCount; slot++)
        {
            if (api.dget(slot) != 0)
            {
                api.dset(slot, 0);
            }
        }

        Read();

        // The two things holding a copy of what was just deleted: the toggle slot 0 carried, and the
        // grid, which takes the results when it comes up and may be the screen this was chosen from.
        Debug.Clear();
        LevelSelect.Refresh();
    }
}
