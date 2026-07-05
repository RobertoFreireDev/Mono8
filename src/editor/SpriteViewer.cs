namespace mono8.editor;

internal class SpriteViewer
{
    private readonly IMono8API _api;

    public SpriteViewer(IMono8API api)
    {
        _api = api;
    }

    public void Update() 
    {
    
    }

    public void Draw()
    {
        _api.spr(0, 0, 
            Constants.Screen.ResolutionY - Constants.GameDataSizes.SpriteSheetY - Constants.GameDataSizes.TileSize, 
            Constants.GameDataSizes.SpriteSheetColumns, 
            Constants.GameDataSizes.SpriteSheetRows);
    }
}
