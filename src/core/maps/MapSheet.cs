namespace mono8.core.maps;

internal class MapSheet
{
    public int[,] Data;

    public void SetTile(int x, int y, int tileIndex)
    {
        if (InvalidGridPos(x, y))
        {
            return;
        }
        Data[y, x] = tileIndex;
    }

    public int GetTile(int x, int y)
    {
        if (InvalidGridPos(x, y))
        {
            return 0;
        }

        return Data[y, x];
    }

    public bool InvalidGridPos(int x, int y)
    {
        return x < 0 || y < 0 || 
            x >= Constants.GameDataSizes.MapSheetX ||
            y >= Constants.GameDataSizes.MapSheetY;
    }

    public void LoadMaps(string[] sheet)
    {
        Data = new int[Constants.GameDataSizes.MapSheetY, Constants.GameDataSizes.MapSheetX];

        for (int r = 0; r < Constants.GameDataSizes.MapSheetY; r++)
        {
            string row = sheet != null && r < sheet.Length ? sheet[r] : null;
            for (int c = 0; c < Constants.GameDataSizes.MapSheetX; c++)
            {
                int charIndex = c * 2;
                if (row == null || charIndex + 1 >= row.Length)
                {
                    Data[r, c] = 0;
                    continue;
                }

                int value = 0;
                for (int i = 0; i < 2; i++)
                {
                    char ch = row[charIndex + i];
                    int nibble = ch >= '0' && ch <= '9' ? ch - '0'
                               : ch >= 'a' && ch <= 'f' ? ch - 'a' + 10
                               : ch >= 'A' && ch <= 'F' ? ch - 'A' + 10
                               : 0;
                    value = value * 16 + nibble;
                }

                Data[r, c] = value > Constants.GameDataSizes.MaxSpriteIndex ? 0 : value;
            }
        }
    }

    public void DrawMap(
        int mapX, int mapY,   // starting tile in map
        int px, int py,       // screen position to draw at
        int width, int height, // how many tiles wide/tall to draw
        int layerMax,
        int color)
    {
        for (int y = 0; y < height; y++)
        {
            int mapYIndex = mapY + y;
            if (mapYIndex < 0 || mapYIndex >= Constants.GameDataSizes.MapSheetY) continue;

            for (int x = 0; x < width; x++)
            {
                int mapXIndex = mapX + x;
                if (mapXIndex < 0 || mapXIndex >= Constants.GameDataSizes.MapSheetX) continue;

                int tileIndex = Data[mapYIndex, mapXIndex];
                if (tileIndex <= 0) continue;

                if (layerMax != 0 && (Mono8API.SpriteSheet.GetFlags(tileIndex) & layerMax) == 0) continue;

                Mono8API.SpriteSheet.Draw(
                    tileIndex,
                    px + x * Constants.GameDataSizes.TileSize,
                    py + y * Constants.GameDataSizes.TileSize);
            }
        }
    }
}
