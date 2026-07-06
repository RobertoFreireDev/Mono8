namespace mono8.editor;

internal class EventNotifier
{
    private readonly IMono8API _api;
    private readonly float displaySeconds;
    private readonly int x;
    private readonly int y;
    private string eventLabel = null;
    private float eventTimeLeft = 0f;

    public EventNotifier(IMono8API api, float displaySeconds, int x, int y)
    {
        _api = api;
        this.displaySeconds = displaySeconds;
        this.x = x;
        this.y = y;
    }

    public void AddEvent(string label)
    {
        eventLabel = label;
        eventTimeLeft = displaySeconds;
    }

    public void Update(float elapsedSeconds)
    {
        if (eventLabel != null)
        {
            eventTimeLeft -= elapsedSeconds;
            if (eventTimeLeft <= 0f)
            {
                eventLabel = null;
            }
        }
    }

    public void Draw()
    {
        if (eventLabel != null)
        {
            _api.print(eventLabel, x, y, Constants.Colors.White);
        }
    }
}
