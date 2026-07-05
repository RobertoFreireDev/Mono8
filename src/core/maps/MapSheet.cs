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

    // PICO-8 map rows 32-63 share memory with sprite sheet pixel rows 64-127.
    // For tile at (col, row): byte offset B = (row-32)*128 + col,
    // which maps to sprite pixel row 64 + B/64, columns (B%64)*2 and (B%64)*2+1
    // (low nibble first, high nibble second — little-endian nibble order).
    public void PopulateSharedRegion()
    {
        var spriteData = Mono8API.SpriteSheet.Data;
        for (int mapRow = 32; mapRow < Constants.GameDataSizes.MapSheetY; mapRow++)
        {
            for (int mapCol = 0; mapCol < Constants.GameDataSizes.MapSheetX; mapCol++)
            {
                int b = (mapRow - 32) * 128 + mapCol;
                int spriteRow = 64 + b / 64;
                int spriteCol = (b % 64) * 2;
                int low = spriteData[spriteRow, spriteCol];
                int high = spriteData[spriteRow, spriteCol + 1];
                Data[mapRow, mapCol] = low | (high << 4);
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
