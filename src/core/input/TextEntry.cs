namespace mono8.core.input;

/// <summary>
/// The characters typed this frame, taken from the OS rather than reconstructed from
/// <see cref="Keys"/>. Nothing in the project read typed text before the JSON editor; the editors
/// only test discrete keys, and rebuilding shift/layout/dead-key handling from a
/// <see cref="KeyboardState"/> is a trap that gets a different answer on every keyboard layout.
/// <para>
/// Characters are filtered by <see cref="Text.IsValidChar"/> as they arrive, which is what
/// guarantees no editor can ever store something the font cannot draw. Per-field rules (digits
/// only, the name charset, a length cap) layer on top in <c>TextField</c>.
/// </para>
/// <para>
/// The buffer is double-banked: the window raises <c>TextInput</c> while the platform pumps its
/// message queue, which happens before <c>Update</c> runs, so <see cref="Update"/> promotes what
/// arrived into <see cref="Typed"/> and starts a fresh bank. <see cref="Typed"/> therefore stays
/// readable for the whole of the frame's update, the same way the keyboard and mouse snapshots do.
/// </para>
/// </summary>
public static class TextEntry
{
    // At the fixed 60 fps this is a ~0.33 s delay and then ~15 deletions a second.
    private const int BackspaceDelayFrames = 20;
    private const int BackspaceRepeatFrames = 4;

    private static readonly System.Text.StringBuilder _pending = new System.Text.StringBuilder();
    private static GameWindow _window;
    private static int _backspaceFrames;

    /// <summary>Characters typed since the last <see cref="Update"/>, already font-filtered.</summary>
    public static string Typed { get; private set; } = string.Empty;

    /// <summary>True on the frame a backspace should delete one character, key repeat included.</summary>
    public static bool Backspace { get; private set; }

    /// <summary>Subscribes to <paramref name="window"/>; call once, from <c>LoadContent</c>.</summary>
    public static void Attach(GameWindow window)
    {
        if (ReferenceEquals(_window, window)) return;

        if (_window != null) _window.TextInput -= OnTextInput;
        _window = window;
        if (_window != null) _window.TextInput += OnTextInput;

        Clear();
    }

    /// <summary>Promotes the characters that arrived since the last call. Runs once per frame.</summary>
    public static void Update()
    {
        Typed = _pending.Length == 0 ? string.Empty : _pending.ToString();
        _pending.Clear();

        if (!KeybrdInput.Pressed(Keys.Back))
        {
            _backspaceFrames = 0;
            Backspace = false;
            return;
        }

        Backspace = _backspaceFrames == 0
            || (_backspaceFrames >= BackspaceDelayFrames
                && (_backspaceFrames - BackspaceDelayFrames) % BackspaceRepeatFrames == 0);
        _backspaceFrames++;
    }

    /// <summary>
    /// Drops the current frame's input. Opening a field calls this so the keystroke that opened it
    /// is not also typed into it.
    /// </summary>
    public static void Clear()
    {
        _pending.Clear();
        Typed = string.Empty;
        Backspace = false;
    }

    private static void OnTextInput(object sender, TextInputEventArgs e)
    {
        if (Text.IsValidChar(e.Character)) _pending.Append(e.Character);
    }
}
