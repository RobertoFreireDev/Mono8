namespace mono8.editor;

/// <summary>
/// An inline editor: text plus a blinking caret inside a <see cref="Rectangle"/>, fed by
/// <see cref="TextEntry"/>. This is where "you can only type what this field accepts" is actually
/// enforced — every character is gated by <see cref="DataValue.IsCharAllowed"/> for a value or
/// <see cref="JsonNames.IsValidNameChar"/> for a name, so a rejected key simply does nothing.
/// <para>
/// Every field carries a caret the mouse can place and the arrow keys can move, with characters
/// inserted and both deletes taken where that caret is. A Text value opens in multiline mode
/// (<see cref="BeginText"/>), because it is the one kind of value that wraps: it is laid out with
/// the very same wrap the inspector draws it with, so its caret moves by line as well. Everything
/// else — a name, a number, a position — is one line that scrolls sideways under its caret.
/// </para>
/// <para>
/// <c>Enter</c> commits, <c>Tab</c> commits and asks the caller to move on, <c>Esc</c> cancels and
/// the caller keeps the value it had.
/// </para>
/// </summary>
internal sealed class TextField
{
    private const int BlinkFrames = 15;   // half a second at the fixed 30 fps

    // The caret reads as the caret and not as another character: it is the one thing in the field
    // that is not the white the text is drawn in.
    private const int CaretColor = Constants.Colors.Orange;

    // A held arrow repeats on the cadence TextEntry gives backspace: ~0.33 s, then ~15 a second.
    // Without it a 256-character value would have to be crossed one key press at a time.
    private const int RepeatDelayFrames = 10;
    private const int RepeatRateFrames = 2;

    private readonly IMono8API _api;

    private Rectangle _bounds;
    private string _text = string.Empty;
    private DataValueType _type;
    private bool _isName;
    private int _maxLength;
    private int _blink;

    private int _columns;      // characters per line while multiline; 0 while the field is single-line
    private int _caret;        // an index into _text, from 0 to its length
    private int _window;       // first character shown while single-line; the sideways scroll
    private int _clipTop = int.MinValue;
    private int _clipBottom = int.MaxValue;
    private int _leftFrames;
    private int _rightFrames;
    private int _upFrames;
    private int _downFrames;
    private int _deleteFrames;

    public TextField(IMono8API api) => _api = api;

    public bool Active { get; private set; }

    /// <summary>True when the commit that just ended the edit was a <c>Tab</c>.</summary>
    public bool Advance { get; private set; }

    /// <summary>
    /// What is in the buffer right now. A multiline value grows and shrinks by whole lines as it is
    /// typed, so the caller needs it to lay out what sits under the field.
    /// </summary>
    public string Value => _text;

    /// <summary>Opens the field on a single-line value of <paramref name="type"/>.</summary>
    public void Begin(Rectangle bounds, string initial, DataValueType type, int maxLength)
    {
        Open(bounds, initial);
        _type = type;
        _isName = false;
        _maxLength = maxLength;
        _columns = 0;
    }

    /// <summary>
    /// Opens the field on a Text value that wraps at <paramref name="columns"/> characters —
    /// <paramref name="bounds"/> is the whole block it occupies, not just its first line.
    /// </summary>
    public void BeginText(Rectangle bounds, string initial, int columns, int maxLength)
    {
        Open(bounds, initial);
        _type = DataValueType.Text;
        _isName = false;
        _maxLength = maxLength;
        _columns = Math.Max(1, columns);
    }

    /// <summary>Opens the field on a group, object or field name — §1.3's rules, 8 characters.</summary>
    public void BeginName(Rectangle bounds, string initial)
    {
        Open(bounds, initial);
        _type = DataValueType.Text;
        _isName = true;
        _maxLength = JsonNames.MaxChars;
        _columns = 0;
    }

    /// <summary>
    /// Moves the open field. A multiline value changes height as it is typed, and the rows under it
    /// move with it, so the caller re-states where the block is every frame.
    /// </summary>
    public void SetBounds(Rectangle bounds) => _bounds = bounds;

    /// <summary>
    /// Keeps drawing inside the band from <paramref name="top"/> to <paramref name="bottom"/>. A
    /// wrapped value can run taller than the panel showing it, and nothing in the project clips, so
    /// a line that does not fit entirely between the two is dropped rather than left to bleed over
    /// whatever borders the panel — the rule the inspector already draws its stored values by.
    /// </summary>
    public void Clip(int top, int bottom)
    {
        _clipTop = top;
        _clipBottom = bottom;
    }

    /// <summary>
    /// Puts the caret under (<paramref name="x"/>, <paramref name="y"/>) — how a click inside a
    /// value picks where the next character lands, including the click that opened it. The point is
    /// clamped onto a line and then into that line, so a click past the last line or past the end of
    /// a short one still leaves the caret somewhere the text actually has.
    /// </summary>
    public void PlaceCaret(int x, int y) => PlaceCaretIn(x - _bounds.X, y - _bounds.Y);

    /// <summary>
    /// The same from a point already measured from the field's top-left corner. The click that
    /// opens an edit is one of those: the panel may scroll the value into view as it opens, which
    /// moves where the text is on screen without moving where in it the click pointed.
    /// </summary>
    public void PlaceCaretIn(int offsetX, int offsetY)
    {
        if (!Active) return;

        if (_columns <= 0)
        {
            // One line, so only the x matters — measured from the pixel the single-line view insets
            // its text by, and read against the window rather than the buffer, since a long value
            // may have been scrolled sideways under the caret.
            _caret = Math.Clamp(_window + Column(offsetX - 1), 0, _text.Length);
            _blink = 0;
            return;
        }

        var spans = EditorUI.WrapSpans(_text, _columns);
        int line = Math.Clamp(offsetY / Text.LineHeight, 0, spans.Count - 1);
        SetCaret(spans, line, Column(offsetX));
        _blink = 0;   // show it where it just landed instead of mid-blink
    }

    /// <summary>
    /// Which gap between characters <paramref name="offsetX"/> points at. It rounds to the nearest
    /// gap rather than truncating into a glyph, so the half of a character nearer the next gap puts
    /// the caret there — a caret sits between characters, not on one.
    /// </summary>
    private static int Column(int offsetX) =>
        offsetX <= 0 ? 0 : (offsetX + Text.CharAdvance / 2) / Text.CharAdvance;

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

        // The buffer is the only authority on where a caret can be, and it changed under it on any
        // frame that typed or deleted, so every pass starts by putting the caret back inside it.
        _caret = Math.Clamp(_caret, 0, _text.Length);

        // Ctrl is held for shortcuts, not for typing, so its key presses never reach the buffer.
        if (!KeybrdInput.IsCtrlPressed())
        {
            foreach (char c in TextEntry.Typed)
            {
                if (_text.Length >= _maxLength) break;
                if (!IsAllowed(c)) continue;
                _text = _text.Insert(_caret, c.ToString());
                _caret++;
            }

            if (TextEntry.Backspace && _caret > 0)
            {
                _text = _text.Remove(_caret - 1, 1);
                _caret--;
            }
        }

        UpdateCaretKeys();

        if (KeybrdInput.JustPressed(Keys.Escape))
        {
            Active = false;
            cancelled = true;
            return true;
        }

        // Clicking away commits, the same way the sprite editor's reference-number field ends its
        // edit, so an entry is never stranded by a key that did not land. A click that lands inside
        // a multiline value is not a click away — it is the caret being aimed.
        var mouse = _api.mousexy();
        bool clickedAway = false;
        if (_api.mouselp())
        {
            if (_bounds.Contains(mouse.x, mouse.y)) PlaceCaret(mouse.x, mouse.y);
            else clickedAway = true;
        }

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

        if (_columns > 0)
        {
            DrawWrapped();
            return;
        }

        _api.rectfill(_bounds.X, _bounds.Y, _bounds.Right - 1, _bounds.Bottom - 1, Constants.Colors.Indigo);

        ScrollToCaret();

        int columns = SingleLineColumns();
        _api.print(_text.Substring(_window, Math.Min(columns, _text.Length - _window)),
            _bounds.X + 1, _bounds.Y + 1, Constants.Colors.White);

        if ((_blink / BlinkFrames) % 2 != 0) return;

        int caret = Math.Min(_bounds.X + 1 + (_caret - _window) * Text.CharAdvance, _bounds.Right - 1);
        _api.rectfill(caret, _bounds.Y + 1, caret, _bounds.Bottom - 2, CaretColor);
    }

    /// <summary>Characters of the buffer the single-line view has room for.</summary>
    private int SingleLineColumns() => Math.Max(1, (_bounds.Width - 2) / Text.CharAdvance);

    /// <summary>
    /// Slides the single-line window so the caret is always inside it. A value can be far longer
    /// than the field is wide, and now that the caret walks it, the window follows the caret rather
    /// than sitting at the end of the text — otherwise arrowing back into a long value would move a
    /// caret nobody can see.
    /// </summary>
    private void ScrollToCaret()
    {
        int columns = SingleLineColumns();

        if (_window > _caret) _window = _caret;
        else if (_caret - _window > columns) _window = _caret - columns;

        // Deleting can leave the window past the end of what is left; it never raises it past the
        // caret, since it only ever lowers a window that is already at or before it.
        _window = Math.Clamp(_window, 0, Math.Max(0, _text.Length - columns));
    }

    /// <summary>
    /// The multiline view. Nothing is inset by the single line's pixel: an open edit has to sit
    /// exactly where the inspector drew the stored value, or the text would step sideways the
    /// moment it is clicked.
    /// </summary>
    private void DrawWrapped()
    {
        var spans = EditorUI.WrapSpans(_text, _columns);

        for (int i = 0; i < spans.Count; i++)
        {
            int y = _bounds.Y + i * Text.LineHeight;
            if (!IsVisible(y)) continue;

            _api.rectfill(_bounds.X, y, _bounds.Right - 1, y + Text.LineHeight - 1, Constants.Colors.Indigo);

            var (start, length) = spans[i];
            if (length > 0) _api.print(_text.Substring(start, length), _bounds.X, y + 1, Constants.Colors.White);
        }

        if ((_blink / BlinkFrames) % 2 != 0) return;

        var (line, column) = CaretLineColumn(spans);
        int caretY = _bounds.Y + line * Text.LineHeight;
        if (!IsVisible(caretY)) return;

        int caretX = Math.Min(_bounds.X + column * Text.CharAdvance, _bounds.Right - 1);
        _api.rectfill(caretX, caretY + 1, caretX, caretY + Text.LineHeight - 2, CaretColor);
    }

    /// <summary>
    /// Arrow keys and forward delete. Left and right walk the buffer and Delete takes the character
    /// they are sitting in front of, in any field — the backspace <see cref="Update"/> handles is
    /// the same thing pointed the other way. Up and down only exist while a value is multiline: they
    /// keep the column and change the line, both clamped, so the top line has nowhere above it and a
    /// short line takes the caret to its end.
    /// </summary>
    private void UpdateCaretKeys()
    {
        if (Repeats(Keys.Left, ref _leftFrames)) _caret--;
        if (Repeats(Keys.Right, ref _rightFrames)) _caret++;
        _caret = Math.Clamp(_caret, 0, _text.Length);

        if (_columns > 0)
        {
            bool up = Repeats(Keys.Up, ref _upFrames);
            bool down = Repeats(Keys.Down, ref _downFrames);
            if (up || down)
            {
                var spans = EditorUI.WrapSpans(_text, _columns);
                var (line, column) = CaretLineColumn(spans);
                SetCaret(spans, line + (down ? 1 : 0) - (up ? 1 : 0), column);
            }
        }

        if (Repeats(Keys.Delete, ref _deleteFrames) && _caret < _text.Length) _text = _text.Remove(_caret, 1);

        _caret = Math.Clamp(_caret, 0, _text.Length);
    }

    /// <summary>True when all 9 pixels of a line starting at <paramref name="y"/> are inside the clip band.</summary>
    private bool IsVisible(int y) => y >= _clipTop && y + Text.LineHeight <= _clipBottom;

    /// <summary>True on the frame <paramref name="key"/> should act: once on the press, then repeating.</summary>
    private static bool Repeats(Keys key, ref int frames)
    {
        if (!KeybrdInput.Pressed(key))
        {
            frames = 0;
            return false;
        }

        bool fire = frames == 0
            || (frames >= RepeatDelayFrames && (frames - RepeatDelayFrames) % RepeatRateFrames == 0);
        frames++;
        return fire;
    }

    /// <summary>
    /// Where the caret sits in the wrapped text: the last line that starts at or before it, and how
    /// far into that line it is. A caret at the very end of a line that filled its width has no line
    /// after it yet, so it stays at that line's end rather than jumping to a line that is not there.
    /// </summary>
    private (int Line, int Column) CaretLineColumn(List<(int Start, int Length)> spans)
    {
        for (int i = spans.Count - 1; i >= 0; i--)
        {
            if (_caret < spans[i].Start) continue;
            return (i, Math.Min(_caret - spans[i].Start, spans[i].Length));
        }
        return (0, 0);
    }

    /// <summary>The reverse: a line and a column, both clamped to what the text has, become a caret.</summary>
    private void SetCaret(List<(int Start, int Length)> spans, int line, int column)
    {
        line = Math.Clamp(line, 0, spans.Count - 1);
        column = Math.Clamp(column, 0, spans[line].Length);
        _caret = Math.Clamp(spans[line].Start + column, 0, _text.Length);
    }

    private void Open(Rectangle bounds, string initial)
    {
        _bounds = bounds;
        _text = initial ?? string.Empty;
        _caret = _text.Length;
        _window = 0;
        _blink = 0;
        _leftFrames = 0;
        _rightFrames = 0;
        _upFrames = 0;
        _downFrames = 0;
        _deleteFrames = 0;
        Advance = false;
        Active = true;

        // Whatever opened the field was typed this frame; it must not also land inside it.
        TextEntry.Clear();
    }

    private bool IsAllowed(char c) =>
        _isName ? JsonNames.IsValidNameChar(c) : DataValue.IsCharAllowed(_type, c, _text, _caret);
}
