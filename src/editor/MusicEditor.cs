namespace mono8.editor;

internal class MusicEditor : IEditor
{
    private readonly IMono8API _api;
    private readonly EventNotifier eventNotifier;

    public MusicEditor(IMono8API api)
    {
        _api = api;
        eventNotifier = new EventNotifier(api, 2f, 1, Constants.Screen.ResolutionY - Constants.GameDataSizes.TileSize + 1);
    }

    public void Init()
    {
    }

    public void Update(float elapsedSeconds)
    {
        eventNotifier.Update(elapsedSeconds);

        if (KeybrdInput.IsSaveShortcutPressed())
        {
            mono8.GameAPI.Save();
            eventNotifier.AddEvent("SAVED");
        }
    }

    public void Draw()
    {
        _api.rectfill(0, Constants.Screen.ResolutionY - Constants.GameDataSizes.TileSize, Constants.Screen.ResolutionX, Constants.Screen.ResolutionY - 1, Constants.Colors.Orange);
        eventNotifier.Draw();
    }
}
