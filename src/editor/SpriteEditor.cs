namespace mono8.editor;

internal class SpriteEditor : IEditor
{
    private readonly IMono8API _api;
    private readonly SpriteViewer SprVwr;

    public SpriteEditor(IMono8API api)
    {
        _api = api;
        SprVwr = new SpriteViewer(_api);
    }

    public void Init()
    {
    }

    public void Update(float elapsedSeconds)
    {
    }

    public void Draw()
    {
        _api.rectfill(0,0,Constants.Screen.ResolutionX,Constants.GameDataSizes.TileSize,Constants.Colors.Red);
        SprVwr.Draw();
        _api.rectfill(0,Constants.Screen.ResolutionY - Constants.GameDataSizes.TileSize, Constants.Screen.ResolutionX, Constants.Screen.ResolutionY -1,Constants.Colors.Red);
    }
}
