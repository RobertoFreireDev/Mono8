namespace mono8.editor;

internal class EditorRegistry
{
    private readonly List<(IEditor Editor, int IconIndex, string Label)> _entries = new();
    private readonly List<bool> _initialized = new();
    private int _activeIndex;

    public int ActiveIndex => _activeIndex;
    public IEditor Active => _entries[_activeIndex].Editor;
    public IReadOnlyList<(IEditor Editor, int IconIndex, string Label)> Entries => _entries;

    public void Register(IEditor editor, int iconIndex, string label)
    {
        _entries.Add((editor, iconIndex, label));
        _initialized.Add(false);
    }

    public void SwitchTo(int index)
    {
        if (index < 0 || index >= _entries.Count || index == _activeIndex) return;
        _activeIndex = index;
        EnsureActiveInitialized();
    }

    public void EnsureActiveInitialized()
    {
        if (_initialized[_activeIndex]) return;
        _entries[_activeIndex].Editor.Init();
        _initialized[_activeIndex] = true;
    }

    public void ReinitActive()
    {
        _entries[_activeIndex].Editor.Init();
    }
}
