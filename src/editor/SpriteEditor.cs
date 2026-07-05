namespace mono8.editor;

internal class SpriteEditor : IEditor
{
    private readonly IMono8API _api;
    private Rectangle sprvwrarea;
    private Rectangle sprcnvsarea;
    private int sprNmbr = 0;
    public int SprX = -1;
    public int SprY = -1;
    public int SprScl = 1;

    public SpriteEditor(IMono8API api)
    {
        _api = api;
        sprvwrarea = new Rectangle(0,
            Constants.Screen.ResolutionY - 1 - Constants.GameDataSizes.SpriteSheetY - Constants.GameDataSizes.TileSize,
            Constants.GameDataSizes.SpriteSheetX,
            Constants.GameDataSizes.SpriteSheetY);
        sprcnvsarea = new Rectangle(8, 12, 72, 72);
    }

    public void Init()
    {
    }

    public void Update(float elapsedSeconds)
    {
        var mouse = _api.mousexy();

        if (!sprvwrarea.Contains(mouse.x, mouse.y))
        {
            return;
        }

        if (_api.mouselp())
        {
            int x = (mouse.x - sprvwrarea.X) / Constants.GameDataSizes.TileSize;
            int y = (mouse.y - sprvwrarea.Y) / Constants.GameDataSizes.TileSize;
            SprX = x * Constants.GameDataSizes.TileSize + sprvwrarea.X;
            SprY = y * Constants.GameDataSizes.TileSize + sprvwrarea.Y;
            sprNmbr = x + y * Constants.GameDataSizes.SpriteSheetColumns;
        }
    }

    public void Draw()
    {
        _api.rectfill(0,0,Constants.Screen.ResolutionX,Constants.GameDataSizes.TileSize,Constants.Colors.Orange);

        _api.spr(0, sprvwrarea.X,
            sprvwrarea.Y,
            Constants.GameDataSizes.SpriteSheetColumns,
            Constants.GameDataSizes.SpriteSheetRows);

        _api.print(sprNmbr.ToString(),0,0);

        if (SprX > -1 && SprY > -1)
        {
            _api.rect(SprX - 1, SprY - 1,
             SprX + Constants.GameDataSizes.TileSize * SprScl,
             SprY + Constants.GameDataSizes.TileSize * SprScl,
             Constants.Colors.White);
            _api.rect(SprX -2, SprY - 2,
             SprX + 1 + Constants.GameDataSizes.TileSize * SprScl,
             SprY + 1 + Constants.GameDataSizes.TileSize * SprScl,
             Constants.Colors.Black);
        }
        _api.rectfill(0, Constants.GameDataSizes.TileSize + 1,
            Constants.Screen.ResolutionX, 85, Constants.Colors.DarkGray);
        _api.rectfill(sprcnvsarea.X, sprcnvsarea.Y, sprcnvsarea.Width, sprcnvsarea.Height,Constants.Colors.Black);
        _api.rectfill(0,Constants.Screen.ResolutionY - Constants.GameDataSizes.TileSize, Constants.Screen.ResolutionX, Constants.Screen.ResolutionY -1,Constants.Colors.Orange);
    }
}
