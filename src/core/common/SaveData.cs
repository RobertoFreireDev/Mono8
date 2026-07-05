namespace mono8.core.common;

internal static class SaveData
{
    private static readonly int[] _slots = new int[Constants.GameDataSizes.SaveDataSlotCount];
    private static string _savePath = string.Empty;

    internal static void Load(string folderPath)
    {
        _savePath = FileIO.BuildPath(Constants.File.Name, Constants.File.Extensions.Save, folderPath);
        Array.Clear(_slots, 0, Constants.GameDataSizes.SaveDataSlotCount);

        var raw = FileIO.Read(Constants.File.Name, Constants.File.Extensions.Save, folderPath);
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
        if (string.IsNullOrWhiteSpace(_savePath)) return;
        File.WriteAllText(_savePath, string.Join("\n", _slots));
    }
}
