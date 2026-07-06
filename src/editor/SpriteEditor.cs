namespace mono8.editor;

internal class SpriteEditor : IEditor
{
    private readonly IMono8API _api;
    private Rectangle sprvwrarea;
    private Rectangle sprcnvsarea;
    private int sprNmbr = 0;
    public int SprX = -1;
    public int SprY = -1;
    public int SprSclIdx = 0;
    public int[] Zooms = { 1, 2, 4 };
    public int[] CnvScale = { 8, 4, 2 };

    public SpriteEditor(IMono8API api)
    {
        _api = api;
        sprvwrarea = new Rectangle(0,
            Constants.Screen.ResolutionY - 1 - Constants.GameDataSizes.SpriteSheetY - Constants.GameDataSizes.TileSize,
            Constants.GameDataSizes.SpriteSheetX,
            Constants.GameDataSizes.SpriteSheetY);
        sprcnvsarea = new Rectangle(8, 12, 8*8, 8 * 8);
    }

    public void Init()
    {
    }

    public void Update(float elapsedSeconds)
    {
        var mouse = _api.mousexy();

        if (_api.mousedown())
        {
            SprSclIdx += 1;
        }

        if (_api.mouseup())
        {
            SprSclIdx -= 1;
        }

        SprSclIdx = Math.Clamp(SprSclIdx, 0, Zooms.Length - 1);

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

        if (SprX > -1 && SprY > -1)
        {
            _api.rect(SprX - 1, SprY - 1,
             SprX + Constants.GameDataSizes.TileSize * Zooms[SprSclIdx],
             SprY + Constants.GameDataSizes.TileSize * Zooms[SprSclIdx],
             Constants.Colors.White);
            _api.rect(SprX -2, SprY - 2,
             SprX + 1 + Constants.GameDataSizes.TileSize * Zooms[SprSclIdx],
             SprY + 1 + Constants.GameDataSizes.TileSize * Zooms[SprSclIdx],
             Constants.Colors.Black);
        }
        _api.rectfill(0, Constants.GameDataSizes.TileSize + 1,
            Constants.Screen.ResolutionX, 85, Constants.Colors.DarkGray);
        _api.rectfill(sprcnvsarea.X -1, sprcnvsarea.Y - 1, 
            sprcnvsarea.X + sprcnvsarea.Width,
            sprcnvsarea.Y + sprcnvsarea.Height, Constants.Colors.Black);
        _api.spr(sprNmbr, sprcnvsarea.X, sprcnvsarea.Y,
             Zooms[SprSclIdx],
             Zooms[SprSclIdx],
             CnvScale[SprSclIdx]);
        _api.rectfill(0,Constants.Screen.ResolutionY - Constants.GameDataSizes.TileSize, Constants.Screen.ResolutionX, Constants.Screen.ResolutionY -1,Constants.Colors.Orange);
    }
}
