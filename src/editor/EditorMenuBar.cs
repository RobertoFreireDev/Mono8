namespace mono8.editor;

internal class EditorMenuBar
{
    private const int MapViewSplitIcon = 13;
    private const int MapViewFullIcon = 14;
    private const int SfxViewPrimaryIcon = 30;
    private const int SfxViewAltIcon = 31;

    private readonly IMono8API _api;
    private readonly EditorRegistry _registry;
    private readonly Button[] _buttons;
    private readonly Button _mapViewToggle;
    private readonly Button _sfxViewToggle;

    public Rectangle Bounds { get; }

    /// <summary>
    /// Name of the bar button under the cursor, or null. The bar is shared by every editor, so
    /// the active editor picks this up in its own Update and hands it to its event notifier.
    /// </summary>
    public string HoverLabel { get; private set; }

    public EditorMenuBar(IMono8API api, EditorRegistry registry)
    {
        _api = api;
        _registry = registry;

        int size = Constants.GameDataSizes.TileSize;
        Bounds = new Rectangle(0, 0, Constants.Screen.ResolutionX, size);
        _mapViewToggle = new Button(2, 0, size, MapViewSplitIcon);
        _sfxViewToggle = new Button(2, 0, size, SfxViewPrimaryIcon);

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

        HoverLabel = HoverLabelAt(mouse);

        if (_registry.Active is MapEditor mapEditor && _mapViewToggle.IsClicked(_api, mouse))
        {
            mapEditor.FullMapView = !mapEditor.FullMapView;
            return;
        }

        if (_registry.Active is SfxEditor sfxEditor && _sfxViewToggle.IsClicked(_api, mouse))
        {
            sfxEditor.AltView = !sfxEditor.AltView;
            return;
        }

        for (int i = 0; i < _buttons.Length; i++)
        {
            if (_buttons[i].IsClicked(_api, mouse))
            {
                _registry.SwitchTo(i);
                break;
            }
        }
    }

    // The two view toggles share the same spot and only one of them is on screen at a time, so
    // each is only a candidate while its own editor is active.
    private string HoverLabelAt((int x, int y) mouse)
    {
        if (_registry.Active is MapEditor && _mapViewToggle.Bounds.Contains(mouse.x, mouse.y)) return "MAP VIEW";
        if (_registry.Active is SfxEditor && _sfxViewToggle.Bounds.Contains(mouse.x, mouse.y)) return "SFX VIEW";

        for (int i = 0; i < _buttons.Length; i++)
        {
            if (_buttons[i].Bounds.Contains(mouse.x, mouse.y)) return _registry.Entries[i].Label.ToUpperInvariant();
        }

        return null;
    }

    public void Draw()
    {
        _api.rectfill(0, 0, Constants.Screen.ResolutionX, Bounds.Height - 1, Constants.Colors.Orange);

        for (int i = 0; i < _buttons.Length; i++)
        {
            _buttons[i].Draw(_api, i == _registry.ActiveIndex);
        }

        if (_registry.Active is MapEditor mapEditor)
        {
            _mapViewToggle.IconIndex = mapEditor.FullMapView ? MapViewFullIcon : MapViewSplitIcon;
            _mapViewToggle.Draw(_api, mapEditor.FullMapView);
        }
        else if (_registry.Active is SfxEditor sfxEditor)
        {
            _sfxViewToggle.IconIndex = sfxEditor.AltView ? SfxViewAltIcon : SfxViewPrimaryIcon;
            _sfxViewToggle.Draw(_api, sfxEditor.AltView);
        }
    }
}
