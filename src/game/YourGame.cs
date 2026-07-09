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
        API.print("MAP SCALE", 8, 2, Constants.Colors.Blue);

        // Bottom-aligned on a shared baseline so the sizes are comparable.
        DrawScaled(8, 0.5f, "1/2");
        DrawScaled(40, 1f, "1");
        DrawScaled(96, 2f, "2");
    }

    private static void DrawScaled(int x, float scale, string label)
    {
        const int BaselineY = 66;
        int height = (int)(ChunkHeight * Constants.GameDataSizes.TileSize * scale);

        API.map(0, 0, x, BaselineY - height, ChunkWidth, ChunkHeight, scale: scale);
        API.print(label, x, BaselineY + 3, Constants.Colors.White);
    }
}