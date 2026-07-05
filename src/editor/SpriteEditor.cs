namespace mono8.editor;

internal class SpriteEditor : IEditor
{
    private readonly IMono8API _api;
    private Rectangle _area;
    private int sprNmbr = 0;
    public int SprX = -1;
    public int SprY = -1;
    public int SprScl = 1;

    public SpriteEditor(IMono8API api)
    {
        _api = api;
        _area = new Rectangle(
            0,
            Constants.Screen.ResolutionY - Constants.GameDataSizes.SpriteSheetY - Constants.GameDataSizes.TileSize,
            Constants.GameDataSizes.SpriteSheetX,
            Constants.GameDataSizes.SpriteSheetY);
    }

    public void Init()
    {
    }

    public void Update(float elapsedSeconds)
    {
        var mouse = _api.mousexy();

        if (!_area.Contains(mouse.x, mouse.y))
        {
            return;
        }

        if (_api.mouselp())
        {
            int x = (mouse.x - _area.X) / Constants.GameDataSizes.TileSize;
            int y = (mouse.y - _area.Y) / Constants.GameDataSizes.TileSize;
            SprX = x * Constants.GameDataSizes.TileSize + _area.X;
            SprY = y * Constants.GameDataSizes.TileSize + _area.Y;
            sprNmbr = x + y * Constants.GameDataSizes.SpriteSheetColumns;
        }
    }

    public void Draw()
    {
        _api.rectfill(0,0,Constants.Screen.ResolutionX,Constants.GameDataSizes.TileSize,Constants.Colors.Red);

        _api.spr(0, _area.X,
            _area.Y,
            Constants.GameDataSizes.SpriteSheetColumns,
            Constants.GameDataSizes.SpriteSheetRows);

        _api.print(sprNmbr.ToString(),0,0);

        if (SprX > -1 && SprY > -1)
        {
            _api.rect(SprX, SprY,
             SprX + Constants.GameDataSizes.TileSize * SprScl,
             SprY + Constants.GameDataSizes.TileSize * SprScl,
             Constants.Colors.White);
            _api.rect(SprX -1, SprY - 1,
             SprX + 1 + Constants.GameDataSizes.TileSize * SprScl,
             SprY + 1 + Constants.GameDataSizes.TileSize * SprScl,
             Constants.Colors.Black);
        }
        _api.rectfill(0,Constants.Screen.ResolutionY - Constants.GameDataSizes.TileSize, Constants.Screen.ResolutionX, Constants.Screen.ResolutionY -1,Constants.Colors.Red);
    }
}
