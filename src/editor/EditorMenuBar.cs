namespace mono8.editor;

internal class EditorMenuBar
{
    private readonly IMono8API _api;
    private readonly EditorRegistry _registry;
    private readonly Button[] _buttons;

    public Rectangle Bounds { get; }

    public EditorMenuBar(IMono8API api, EditorRegistry registry)
    {
        _api = api;
        _registry = registry;

        int size = Constants.GameDataSizes.TileSize;
        Bounds = new Rectangle(0, 0, Constants.Screen.ResolutionX, size);

        int count = registry.Entries.Count;
        int startX = Constants.Screen.ResolutionX - count * size;
        _buttons = new Button[count];
        for (int i = 0; i < count; i++)
        {
            _buttons[i] = new Button(startX + i * size, 0, size, registry.Entries[i].IconIndex);
        }
    }

    public void Update()
    {
        var mouse = _api.mousexy();
        for (int i = 0; i < _buttons.Length; i++)
        {
            if (_buttons[i].IsClicked(_api, mouse))
            {
                _registry.SwitchTo(i);
                break;
            }
        }
    }

    public void Draw()
    {
        _api.rectfill(0, 0, Constants.Screen.ResolutionX, Bounds.Height, Constants.Colors.Orange);

        for (int i = 0; i < _buttons.Length; i++)
        {
            _buttons[i].Draw(_api, i == _registry.ActiveIndex);
        }
    }
}
