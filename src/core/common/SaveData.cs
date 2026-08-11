namespace mono8.core.common;

internal static class SaveData
{
    private static readonly int[] _slots = new int[Constants.GameDataSizes.SaveDataSlotCount];
#if BLAZORGL
    /// <summary>The localStorage key the save file becomes in the browser; see <see cref="Persist"/>.</summary>
    private const string StorageKey = Constants.File.Name + "." + Constants.File.Extensions.Save;
#else
    private static string _savePath = string.Empty;
#endif

    internal static void Load(string folderPath)
    {
        Array.Clear(_slots, 0, Constants.GameDataSizes.SaveDataSlotCount);

#if BLAZORGL
        var raw = WebStorage.Read(StorageKey);
#else
        _savePath = FileIO.BuildPath(Constants.File.Name, Constants.File.Extensions.Save, folderPath);
        var raw = FileIO.Read(Constants.File.Name, Constants.File.Extensions.Save, folderPath);
#endif
        if (string.IsNullOrWhiteSpace(raw)) return;

        var lines = raw.Split('\n');
        for (int i = 0; i < lines.Length && i < Constants.GameDataSizes.SaveDataSlotCount; i++)
        {
            if (int.TryParse(lines[i].Trim(), out var val))
                _slots[i] = val;
        }
    }

    internal static int Get(int index)
    {
        if (index < 0 || index >= Constants.GameDataSizes.SaveDataSlotCount) return 0;
        return _slots[index];
    }

    internal static void Set(int index, int value)
    {
        if (index < 0 || index >= Constants.GameDataSizes.SaveDataSlotCount) return;
        _slots[index] = value;
        Persist();
    }

    private static void Persist()
    {
#if BLAZORGL
        // The browser's file system is a page's worth of memory — a save written to it is gone on
        // reload, which is the one thing dset must not be. localStorage is what survives.
        WebStorage.Write(StorageKey, string.Join("\n", _slots));
#else
        if (string.IsNullOrWhiteSpace(_savePath)) return;
        File.WriteAllText(_savePath, string.Join("\n", _slots));
#endif
    }
}
