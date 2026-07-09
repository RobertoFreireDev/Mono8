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

    private const int Sprite = 1;

    // Every scale spr() honours: 8x/4x/2x smaller through 2x/4x/8x larger.
    private static readonly float[] Scales = { 0.125f, 0.25f, 0.5f, 1f, 2f, 4f, 8f };
    private static readonly string[] Labels = { "1/8", "1/4", "1/2", "1", "2", "4", "8" };

    public void Draw()
    {
        API.cls(Constants.Colors.DarkBlue);
        API.print("SPR SCALE TEST", 8, 2, Constants.Colors.Blue);

        // Shrunk sprites on the top row, grown ones below: an 8x sprite is 64px
        // tall and would swallow the small end of the range on a shared baseline.
        DrawRow(0, 4, 8, 48, 40);
        DrawRow(4, 7, 8, 130, 50);
    }

    /// <summary>Draws <c>Scales[from..to)</c> bottom-aligned on <paramref name="baselineY"/>, labelled underneath.</summary>
    private static void DrawRow(int from, int to, int startX, int baselineY, int columnWidth)
    {
        for (int i = from; i < to; i++)
        {
            int size = (int)(Constants.GameDataSizes.TileSize * Scales[i]);
            int x = startX + (i - from) * columnWidth;

            API.spr(Sprite, x, baselineY - size, 1, 1, scale: Scales[i]);
            API.print(Labels[i], x, baselineY + 3, Constants.Colors.White);
        }
    }
}