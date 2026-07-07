namespace mono8.editor;

internal class MapEditor : IEditor
{
    private readonly IMono8API _api;

    public MapEditor(IMono8API api)
    {
        _api = api;
    }

    public void Init()
    {
    }

    public void Update(float elapsedSeconds)
    {
    }

    public void Draw()
    {
        _api.rectfill(0, Constants.GameDataSizes.TileSize, Constants.Screen.ResolutionX, Constants.Screen.ResolutionY, Constants.Colors.DarkGray);
        _api.print("MAP EDITOR", 100, 70, Constants.Colors.White);
    }
}
