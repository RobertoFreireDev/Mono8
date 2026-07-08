namespace mono8.editor;

internal class MusicEditor : IEditor
{
    private readonly IMono8API _api;
    private readonly EventNotifier eventNotifier;
    private static readonly int BottomBarY = Constants.Screen.ResolutionY - Constants.GameDataSizes.TileSize;

    private int patternIndex = 0;

    public MusicEditor(IMono8API api)
    {
        _api = api;
        eventNotifier = new EventNotifier(api, 2f, 1, Constants.Screen.ResolutionY - Constants.GameDataSizes.TileSize + 1);
    }

    public void Init()
    {
    }

    private MusicSheet Music => Mono8API.MusicSheet;
    private SfxSheet Sfx => Mono8API.SfxSheet;

    private void SyncPattern() => mono8.GameAPI.SyncMusic(patternIndex);
    private void SyncSfx(int sfx) => mono8.GameAPI.SyncSfx(sfx);

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
        _api.rectfill(0, BottomBarY, Constants.Screen.ResolutionX, Constants.Screen.ResolutionY - 1, Constants.Colors.Orange);
        eventNotifier.Draw();
    }
}
