namespace mono8.core.common;

public static class ErrorHandler
{
    private static string _message = string.Empty;
    private static bool _error = false;

    public static void Reset()
    {
        _message = string.Empty;
        _error = false;
    }

    public static bool HasError()
    {
        return _error;
    }

    public static void SetError(Exception ex)
    {
        var message = string.Empty;

        if (!string.IsNullOrWhiteSpace(ex?.Source))
        {
            message += ex.Source + "\n";
        }

        message += ex.Message;

        SetError(message);
    }

    public static void SetError(string message)
    {
        mono8.GameAPI?.StopSounds();
        _error = true;
        _message = message;
    }

    public static void Draw()
    {
        mono8.SpriteBatch.DrawBaseBox(-2);
        Text.DrawText(_message, new Vector2(2, 2), -1, true);
    }
}