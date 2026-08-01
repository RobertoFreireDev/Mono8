namespace mono8.editor;

/// <summary>
/// A single-line inline editor: text plus a blinking caret inside a <see cref="Rectangle"/>, fed by
/// <see cref="TextEntry"/>. This is where "you can only type what this field accepts" is actually
/// enforced — every character is gated by <see cref="DataValue.IsCharAllowed"/> for a value or
/// <see cref="JsonNames.IsValidNameChar"/> for a name, so a rejected key simply does nothing.
/// <para>
/// <c>Enter</c> commits, <c>Tab</c> commits and asks the caller to move on, <c>Esc</c> cancels and
/// the caller keeps the value it had.
/// </para>
/// </summary>
internal sealed class TextField
{
    private const int BlinkFrames = 15;   // half a second at the fixed 30 fps

    private readonly IMono8API _api;

    private Rectangle _bounds;
    private string _text = string.Empty;
    private DataValueType _type;
    private bool _isName;
    private int _maxLength;
    private int _blink;

    public TextField(IMono8API api) => _api = api;

    public bool Active { get; private set; }

    /// <summary>True when the commit that just ended the edit was a <c>Tab</c>.</summary>
    public bool Advance { get; private set; }

    /// <summary>Opens the field on a value of <paramref name="type"/>.</summary>
    public void Begin(Rectangle bounds, string initial, DataValueType type, int maxLength)
    {
        Open(bounds, initial);
        _type = type;
        _isName = false;
        _maxLength = maxLength;
    }

    /// <summary>Opens the field on a group, object or field name — §1.3's rules, 8 characters.</summary>
    public void BeginName(Rectangle bounds, string initial)
    {
        Open(bounds, initial);
        _type = DataValueType.String;
        _isName = true;
        _maxLength = JsonNames.MaxChars;
    }

    public void Cancel() => Active = false;

    /// <summary>
    /// True on the frame the edit ends. <paramref name="committed"/> holds the entry when it was
    /// accepted and is null when <paramref name="cancelled"/>.
    /// </summary>
    public bool Update(out string committed, out bool cancelled)
    {
        committed = null;
        cancelled = false;
        if (!Active) return false;

        _blink++;

        // Ctrl is held for shortcuts, not for typing, so its key presses never reach the buffer.
        if (!KeybrdInput.IsCtrlPressed())
        {
            foreach (char c in TextEntry.Typed)
            {
                if (_text.Length >= _maxLength) break;
                if (!IsAllowed(c)) continue;
                _text += c;
            }

            if (TextEntry.Backspace && _text.Length > 0) _text = _text.Substring(0, _text.Length - 1);
        }

        if (KeybrdInput.JustPressed(Keys.Escape))
        {
            Active = false;
            cancelled = true;
            return true;
        }

        // Clicking away commits, the same way the sprite editor's reference-number field ends its
        // edit, so an entry is never stranded by a key that did not land.
        var mouse = _api.mousexy();
        bool clickedAway = _api.mouselp() && !_bounds.Contains(mouse.x, mouse.y);

        bool tab = KeybrdInput.JustPressed(Keys.Tab);
        if (!tab && !clickedAway && !KeybrdInput.JustPressed(Keys.Enter)) return false;

        Active = false;
        Advance = tab;
        committed = _text;
        return true;
    }

    public void Draw()
    {
        if (!Active) return;

        _api.rectfill(_bounds.X, _bounds.Y, _bounds.Right - 1, _bounds.Bottom - 1, Constants.Colors.Indigo);

        // A value can be far longer than the field is wide, so the window follows the caret at the end.
        int columns = Math.Max(1, (_bounds.Width - 2) / Text.CharAdvance);
        string shown = _text.Length <= columns ? _text : _text.Substring(_text.Length - columns);
        _api.print(shown, _bounds.X + 1, _bounds.Y + 1, Constants.Colors.White);

        if ((_blink / BlinkFrames) % 2 != 0) return;

        int caret = Math.Min(_bounds.X + 1 + Text.Width(shown), _bounds.Right - 1);
        _api.rectfill(caret, _bounds.Y + 1, caret, _bounds.Bottom - 2, Constants.Colors.White);
    }

    private void Open(Rectangle bounds, string initial)
    {
        _bounds = bounds;
        _text = initial ?? string.Empty;
        _blink = 0;
        Advance = false;
        Active = true;

        // Whatever opened the field was typed this frame; it must not also land inside it.
        TextEntry.Clear();
    }

    private bool IsAllowed(char c) =>
        _isName ? JsonNames.IsValidNameChar(c) : DataValue.IsCharAllowed(_type, c, _text);
}
