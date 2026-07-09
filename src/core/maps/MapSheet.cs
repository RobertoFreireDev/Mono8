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

    public string[] ToMapLines()
    {
        var lines = new string[Constants.GameDataSizes.MapSheetY];
        for (int r = 0; r < Constants.GameDataSizes.MapSheetY; r++)
        {
            var sb = new System.Text.StringBuilder(Constants.GameDataSizes.MapSheetX * 2);
            for (int c = 0; c < Constants.GameDataSizes.MapSheetX; c++)
                sb.Append(Data[r, c].ToString("x2"));
            lines[r] = sb.ToString();
        }
        return lines;
    }

    public void DrawMap(
        int mapX, int mapY,   // starting tile in map
        int px, int py,       // screen position to draw at
        int width, int height, // how many tiles wide/tall to draw
        int layerMax,
        float colorOpaqueness = 1f)
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
                    py + y * Constants.GameDataSizes.TileSize,
                    colorOpaqueness: colorOpaqueness);
            }
        }
    }

    public void DrawMap(
        int mapX, int mapY,   // starting tile in map
        int px, int py,       // screen position to draw at
        int width, int height, // how many tiles wide/tall to draw
        int layerMax,
        float scale,
        bool flipX,           // mirrors tile placement across the map, not the tiles themselves
        bool flipY,
        float colorOpaqueness = 1f)
    {
        scale = Math.Clamp(scale, SpriteSheet.MinScale, SpriteSheet.MaxScale);

        int tileSize = Constants.GameDataSizes.TileSize;
        int columns = Constants.GameDataSizes.SpriteSheetColumns;

        // Tile edges are snapped from the unscaled grid so neighbours share an edge:
        // rounding each tile's size independently would leave seams or overlaps at
        // fractional scales.
        int Edge(int slot) => (int)Math.Round(slot * tileSize * scale);

        for (int y = 0; y < height; y++)
        {
            int mapYIndex = mapY + y;
            if (mapYIndex < 0 || mapYIndex >= Constants.GameDataSizes.MapSheetY) continue;

            int slotY = flipY ? height - 1 - y : y;
            int top = py + Edge(slotY);
            int destHeight = Math.Max(1, Edge(slotY + 1) - Edge(slotY));

            for (int x = 0; x < width; x++)
            {
                int mapXIndex = mapX + x;
                if (mapXIndex < 0 || mapXIndex >= Constants.GameDataSizes.MapSheetX) continue;

                int tileIndex = Data[mapYIndex, mapXIndex];
                if (tileIndex <= 0) continue;

                if (layerMax != 0 && (Mono8API.SpriteSheet.GetFlags(tileIndex) & layerMax) == 0) continue;

                int slotX = flipX ? width - 1 - x : x;
                int left = px + Edge(slotX);
                int destWidth = Math.Max(1, Edge(slotX + 1) - Edge(slotX));

                Mono8API.SpriteSheet.DrawSub(
                    (tileIndex % columns) * tileSize,
                    (tileIndex / columns) * tileSize,
                    tileSize, tileSize,
                    left, top,
                    destWidth, destHeight,
                    flipX: false, flipY: false,
                    colorOpaqueness: colorOpaqueness);
            }
        }
    }
}
