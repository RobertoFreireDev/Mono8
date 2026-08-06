namespace mono8.game;

/// <summary>
/// A handful of sfx ids authored as one array field — the footsteps under ANIM / PLRWALK, the
/// rummage under CLUBS / ORDER — played one at a time, picked at random, so a sound that repeats
/// often never repeats the same way twice.
///
/// Fixed array, filled in <c>Init</c>: picking a sound allocates nothing.
/// </summary>
internal class SfxList
{
    // The authoring limit on one array.
    private const int MaxSounds = 16;

    private readonly int[] _ids = new int[MaxSounds];
    private int _count;

    /// <summary>Whether there is anything to play — an unauthored list is silent, not sfx 0.</summary>
    public bool Any => _count > 0;

    public void Load(string group, string obj, string field)
    {
        _count = 0;

        var data = YourGame.API.gjson(group, obj);
        if (data == null)
        {
            return;
        }

        int listed = data.Count(field);
        for (int i = 0; i < listed && _count < MaxSounds; i++)
        {
            // A negative id stops channels rather than playing anything, so an unauthored or
            // wrong-typed entry is dropped instead of loaded.
            int id = data.GetInt(field, i, -1);
            if (id >= 0)
            {
                _ids[_count] = id;
                _count++;
            }
        }
    }

    public void PlayRandom()
    {
        if (_count > 0)
        {
            YourGame.API.sfx(_ids[YourGame.API.rnd(_count)]);
        }
    }
}
