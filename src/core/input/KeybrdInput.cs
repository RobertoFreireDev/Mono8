namespace mono8.core.input;

public static class KeybrdInput
{
    public static bool IsAltF4Pressed()
    {
        return Pressed(Keys.LeftAlt) && JustPressed(Keys.F4);
    }

    public static bool IsEscJustPressed()
    {
        return JustPressed(Keys.Escape);
    }

    public static bool IsF2Released()
    {
        return Released(Keys.F2);
    }

    public static bool IsCtrlPressed()
    {
        return Pressed(Keys.LeftControl) || Pressed(Keys.RightControl);
    }

    public static bool IsShiftPressed()
    {
        return Pressed(Keys.LeftShift) || Pressed(Keys.RightShift);
    }

    public static bool IsAltPressed()
    {
        return Pressed(Keys.LeftAlt) || Pressed(Keys.RightAlt);
    }

    /// <summary>True when no Ctrl/Shift/Alt modifier is held, so a bare key press is unambiguous.</summary>
    public static bool NoModifiersPressed()
    {
        return !IsCtrlPressed() && !IsShiftPressed() && !IsAltPressed();
    }

    private static bool IsCtrlShortcut(Keys key) => IsCtrlPressed() && JustPressed(key);

    public static bool IsSaveShortcutPressed() => IsCtrlShortcut(Keys.S);

    public static bool IsUndoShortcutPressed() => IsCtrlShortcut(Keys.Z) && !IsShiftPressed();

    public static bool IsRedoShortcutPressed() => IsCtrlShortcut(Keys.Z) && IsShiftPressed();

    public static bool IsCopyShortcutPressed() => IsCtrlShortcut(Keys.C);

    public static bool IsPasteShortcutPressed() => IsCtrlShortcut(Keys.V);

    public static bool IsRunGameShortcutPressed() => IsCtrlShortcut(Keys.R);

    /// <summary>Number-row or numpad digit just pressed, or -1 if none.</summary>
    public static int JustPressedDigit()
    {
        for (int d = 0; d <= 9; d++)
            if (JustPressed(Keys.D0 + d) || JustPressed(Keys.NumPad0 + d))
                return d;
        return -1;
    }

    public static bool JustPressed(Keys key)
    {
        return InputStateManager.CurrentKeyboardState()[key] == KeyState.Down && InputStateManager.PreviousKeyboardState()[key] == KeyState.Up;
    }

    public static bool Released(Keys key)
    {
        return InputStateManager.CurrentKeyboardState()[key] == KeyState.Up && InputStateManager.PreviousKeyboardState()[key] == KeyState.Down;
    }

    public static bool Pressed(Keys key)
    {
        return InputStateManager.CurrentKeyboardState()[key] == KeyState.Down;
    }
}
