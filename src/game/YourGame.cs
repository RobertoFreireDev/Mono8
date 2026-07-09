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

    public void Draw()
    {
        API.cls(Constants.Colors.DarkBlue);
        API.print("CREATE YOUR GAME HERE", 8, 8, Constants.Colors.Blue);

        // Example: colorOpaqueness = 0.5 (50% transparent) on every draw function that supports it.
        API.pixel(20, 20, Constants.Colors.White, colorOpaqueness: 0.5f);
        API.rect(30, 20, 40, 30, Constants.Colors.Red, colorOpaqueness: 0.5f);
        API.rectfill(50, 20, 60, 30, Constants.Colors.Red, colorOpaqueness: 0.5f);
        API.circ(75, 25, 8, Constants.Colors.Yellow, colorOpaqueness: 0.5f);
        API.circfill(95, 25, 8, Constants.Colors.Yellow, colorOpaqueness: 0.5f);
        API.oval(20, 40, 40, 55, Constants.Colors.Green, colorOpaqueness: 0.5f);
        API.ovalfill(50, 40, 70, 55, Constants.Colors.Green, colorOpaqueness: 0.5f);
        API.spr(1, 80, 40, colorOpaqueness: 0.5f);
        API.sspr(0, 0, 8, 8, 100, 40, colorOpaqueness: 0.5f);
        API.map(0, 0, 0, 60, 5, 3, colorOpaqueness: 0.5f);
    }
}