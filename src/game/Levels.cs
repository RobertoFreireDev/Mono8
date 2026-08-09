namespace mono8.game;

/// <summary>
/// Which room is which level. The one place the two names for a hole meet: the object under ROOMS,
/// which is what <see cref="Room"/> loads, and the number the developer authored as its NUMBER,
/// which is what the <see cref="LevelSelect"/> prints and what <see cref="Save"/> keys a slot on.
///
/// The object name is the developer's — it can be anything the JSON editor accepts, and re-ordering
/// or renaming rooms is not supposed to renumber the game. NUMBER is what says where a room sits in
/// the run, so the mapping is built by reading every object under ROOMS once, at Init.
///
/// A room with no NUMBER, or one outside <see cref="MinNumber"/>-<see cref="MaxNumber"/>, is not a
/// level: it loads perfectly well by name, it just has no place in the grid and no save slot. Two
/// rooms claiming one number is an authoring mistake with no right answer, so the first one authored
/// keeps it.
/// </summary>
internal static class Levels
{
    /// <summary>The lowest level number. Slot 0 in <see cref="Save"/> belongs to no level.</summary>
    public const int MinNumber = 1;

    /// <summary>
    /// The highest. dget/dset hold 64 ints, so 63 is the last number a result can be recorded for —
    /// well past the 12 the grid offers.
    /// </summary>
    public const int MaxNumber = 63;

    private const string JsonGroup = "ROOMS";
    private const string FieldNumber = "NUMBER";

    // Indexed by level number, so slot 0 goes unused for the same reason Save's does.
    private static readonly string[] Rooms = new string[MaxNumber + 1];

    /// <summary>The highest number a room was found for, or 0 when none was.</summary>
    public static int Highest { get; private set; }

    /// <summary>
    /// Reads ROOMS. Re-run every Init: Ctrl+S in the JSON editor rebuilds the data without a
    /// restart, so a room renumbered — or authored outright — lands without one either.
    /// </summary>
    public static void Init()
    {
        var api = YourGame.API;

        for (int i = 0; i < Rooms.Length; i++)
        {
            Rooms[i] = null;
        }

        Highest = 0;

        int objects = api.gjsoncount(JsonGroup);

        for (int i = 0; i < objects; i++)
        {
            string name = api.gjsonobj(JsonGroup, i);
            var data = string.IsNullOrEmpty(name) ? null : api.gjson(JsonGroup, name);

            if (data == null)
            {
                continue;
            }

            // 0 is the fallback and is below MinNumber, so a room that authors no NUMBER falls out
            // here rather than needing a Has check of its own.
            int number = data.GetInt(FieldNumber, 0, 0);

            if (number < MinNumber || number > MaxNumber || Rooms[number] != null)
            {
                continue;
            }

            Rooms[number] = name;

            if (number > Highest)
            {
                Highest = number;
            }
        }
    }

    /// <summary>The ROOMS object level <paramref name="number"/> is, or null when there is no room for it.</summary>
    public static string Name(int number)
    {
        return number >= MinNumber && number <= MaxNumber ? Rooms[number] : null;
    }

    /// <summary>
    /// The level <paramref name="name"/> stands for, or 0 for a room that is not a level at all.
    /// The lookup is by name because that is what a room carries around; there are at most 63 of
    /// them and this is never asked per frame.
    /// </summary>
    public static int Number(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return 0;
        }

        for (int i = MinNumber; i <= MaxNumber; i++)
        {
            // Ordinal-insensitive to match gjson, which finds an object whatever case it is asked in.
            if (string.Equals(Rooms[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return 0;
    }

    /// <summary>Whether a room is authored for that level number.</summary>
    public static bool Exists(int number)
    {
        return Name(number) != null;
    }
}
