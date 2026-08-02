namespace mono8.editor;

internal class EventNotifier
{
    private readonly IMono8API _api;
    private readonly float displaySeconds;
    private readonly int x;
    private readonly int y;
    private string eventLabel = null;
    private float eventTimeLeft = 0f;
    private string hoverLabel = null;
    private float hoverTimeLeft = 0f;

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

    /// <summary>
    /// What the cursor is resting on. Refreshed every frame the cursor stays there, so it only
    /// starts fading once the cursor leaves. An <see cref="AddEvent"/> label outranks it while
    /// that one is still up.
    /// </summary>
    public void SetHover(string label)
    {
        hoverLabel = label;
        hoverTimeLeft = 0f;
    }

    /// <summary>
    /// Drops the event label, letting the hover label through again. For a control whose own label
    /// changes as it is clicked: the new one goes up at once instead of waiting the event out.
    /// </summary>
    public void ClearEvent()
    {
        eventLabel = null;
        eventTimeLeft = 0f;
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

        if (hoverLabel != null)
        {
            hoverTimeLeft -= elapsedSeconds;
            if (hoverTimeLeft <= 0f)
            {
                hoverLabel = null;
            }
        }
    }

    public void Draw()
    {
        string label = eventLabel ?? hoverLabel;
        if (label != null)
        {
            _api.print(label, x, y, Constants.Colors.White);
        }
    }
}
