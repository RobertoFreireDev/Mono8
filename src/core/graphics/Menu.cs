namespace mono8.core.graphics;

internal static class Menu
{
    private const int MaxCustomItems = 3;
    private const int MaxLabelLength = 16;

    /// <summary>Built-in entries; <see cref="Builtin.Custom"/> defers to the item's own callback.</summary>
    private enum Builtin { Custom, Continue, Restart, Exit }

    private record struct CustomMenuItem(string Label, Action Callback);
    private static CustomMenuItem?[] _customItems = new CustomMenuItem?[MaxCustomItems];

    private record struct MenuEntry(string Label, Action Callback, Builtin Builtin);

    // Rebuilt in place each frame rather than reallocated: Update and Draw both need it.
    private static readonly List<MenuEntry> _items = new();

    private static bool Paused = false;
    private static bool _restartVisible = true;
    private static int _selectedIndex = 0;
    private static int _continueCountdown = 0;

    public static bool IsPaused() => Paused;

    /// <summary>
    /// Shows or hides the built-in Restart entry. For screens a re-run of Init is meaningless on —
    /// a title or a level select, which Init lands on anyway.
    /// </summary>
    public static void SetRestartVisible(bool visible) => _restartVisible = visible;

    public static void SetItem(int index, string label, Action callback)
    {
        if (index < 0 || index >= MaxCustomItems) return;
        string truncated = label.Length > MaxLabelLength ? label[..MaxLabelLength] : label;
        _customItems[index] = new CustomMenuItem(truncated, callback);
    }

    public static void ClearItem(int index)
    {
        if (index < 0 || index >= MaxCustomItems) return;
        _customItems[index] = null;
    }

    private static List<MenuEntry> BuildMenu()
    {
        _items.Clear();
        _items.Add(new MenuEntry("Continue", null, Builtin.Continue));
        foreach (var item in _customItems)
            if (item.HasValue) _items.Add(new MenuEntry(item.Value.Label, item.Value.Callback, Builtin.Custom));
        if (_restartVisible) _items.Add(new MenuEntry("Restart", null, Builtin.Restart));
        _items.Add(new MenuEntry("Exit", null, Builtin.Exit));
        return _items;
    }

    public static void Update()
    {
        if (StartInputBinding.JustPressed(PlayerIndex.One) || StartInputBinding.JustPressed(PlayerIndex.Two))
        {
            Paused = !Paused;
            if (Paused) _selectedIndex = 0;
        }

        if (_continueCountdown > 0)
        {
            _continueCountdown--;
            if (_continueCountdown == 0)
                Paused = false;
            return;
        }

        if (!Paused) return;

        var items = BuildMenu();

        // The entry count is the game's to change, so the selection can outlive the row it was on.
        _selectedIndex = Math.Min(_selectedIndex, items.Count - 1);

        if (ButtonInput.JustPressed(3)) // Down
            _selectedIndex = Math.Min(_selectedIndex + 1, items.Count - 1);

        if (ButtonInput.JustPressed(2)) // Up
            _selectedIndex = Math.Max(_selectedIndex - 1, 0);

        if (ButtonInput.JustPressed(5)) // B(X) — confirm
        {
            var entry = items[_selectedIndex];

            switch (entry.Builtin)
            {
                case Builtin.Custom:
                    entry.Callback();
                    Paused = false;
                    break;
                case Builtin.Continue:
                    _continueCountdown = 5;
                    break;
                case Builtin.Restart:
                    Mono8Game.GameAPI.InitGame();
                    Paused = false;
                    break;
                case Builtin.Exit:
                    Mono8Game.GameAPI.Unload();
                    Mono8Game.Instance.Exit();
                    break;
            }
        }
    }

    public static void Draw()
    {
        if (!Paused) return;

        var items = BuildMenu();

        int mX = 8;
        int mY = 8;
        int msX = 5;
        int tY = 8;
        int bottomPad = 6;
        int charWidth = 4;

        int maxLabelLen = 0;
        foreach (var item in items)
            if (item.Label.Length > maxLabelLen) maxLabelLen = item.Label.Length;
        int w = Math.Max(56, mX + msX + maxLabelLen * charWidth + 4);
        int h = mY + items.Count * tY + bottomPad;

        int x0 = Constants.Screen.ResolutionX / 2 - (w / 2);
        int y0 = Constants.Screen.ResolutionY / 2 - (h / 2);

        Mono8Game.SpriteBatch.DrawRectFill(x0, y0, w, h, ColorPalette.BlackColorIndex);
        Mono8Game.SpriteBatch.DrawRect(x0 + 1, y0 + 1, w - 2, h - 2, ColorPalette.WhiteColorIndex);

        for (int i = 0; i < items.Count; i++)
        {
            string label = items[i].Label;

            if (i == _selectedIndex)
            {
                Text.DrawText(">", new Vector2(x0 + mX, y0 + mY + (i * tY)), ColorPalette.WhiteColorIndex);
                Text.DrawText(label, new Vector2(x0 + msX + mX, y0 + mY + (i * tY)), ColorPalette.WhiteColorIndex);
            }
            else
            {
                Text.DrawText(label, new Vector2(x0 + mX, y0 + mY + (i * tY)), ColorPalette.WhiteColorIndex);
            }
        }
    }
}
