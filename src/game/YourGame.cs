namespace mono8.game;

internal class YourGame : IEditor
{
    public static IMono8API API;

    public YourGame(IMono8API api)
    {
        API = api;
    }

    public void Init()
    {
    }

    public void Update(float elapsedSeconds)
    {
    }

    // The corner of the map this demo draws, in tiles.
    private const int ChunkWidth = 6;
    private const int ChunkHeight = 4;

    public void Draw()
    {
        API.cls(Constants.Colors.DarkBlue);
        API.print("SMAP FLIP", 8, 2, Constants.Colors.Blue);

        // The same chunk in all four mirrorings. Each sprite keeps its own
        // orientation; only its position within the chunk is mirrored.
        DrawChunk(8, 12, "--", flipX: false, flipY: false);
        DrawChunk(72, 12, "FX", flipX: true, flipY: false);
        DrawChunk(136, 12, "FY", flipX: false, flipY: true);
        DrawChunk(200, 12, "XY", flipX: true, flipY: true);

        API.print("SMAP SCALE", 8, 68, Constants.Colors.Blue);

        // Bottom-aligned on a shared baseline so the sizes are comparable.
        DrawScaled(8, 0.5f, "1/2");
        DrawScaled(40, 1f, "1");
        DrawScaled(96, 2f, "2");
    }

    private static void DrawChunk(int x, int y, string label, bool flipX, bool flipY)
    {
        API.smap(0, 0, x, y, ChunkWidth, ChunkHeight, flipX: flipX, flipY: flipY);
        API.print(label, x, y + ChunkHeight * Constants.GameDataSizes.TileSize + 3, Constants.Colors.White);
    }

    private static void DrawScaled(int x, float scale, string label)
    {
        const int BaselineY = 132;
        int height = (int)(ChunkHeight * Constants.GameDataSizes.TileSize * scale);

        API.smap(0, 0, x, BaselineY - height, ChunkWidth, ChunkHeight, scale: scale);
        API.print(label, x, BaselineY + 3, Constants.Colors.White);
    }
}