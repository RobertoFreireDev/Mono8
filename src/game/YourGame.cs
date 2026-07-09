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
    }
}