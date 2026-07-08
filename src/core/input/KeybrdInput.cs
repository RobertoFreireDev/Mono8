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

    public static bool IsSaveShortcutPressed()
    {
        return (Pressed(Keys.LeftControl) || Pressed(Keys.RightControl)) && JustPressed(Keys.S);
    }

    public static bool IsUndoShortcutPressed()
    {
        return (Pressed(Keys.LeftControl) || Pressed(Keys.RightControl))
            && !(Pressed(Keys.LeftShift) || Pressed(Keys.RightShift))
            && JustPressed(Keys.Z);
    }

    public static bool IsRedoShortcutPressed()
    {
        return (Pressed(Keys.LeftControl) || Pressed(Keys.RightControl))
            && (Pressed(Keys.LeftShift) || Pressed(Keys.RightShift))
            && JustPressed(Keys.Z);
    }

    public static bool IsCopyShortcutPressed()
    {
        return (Pressed(Keys.LeftControl) || Pressed(Keys.RightControl)) && JustPressed(Keys.C);
    }

    public static bool IsPasteShortcutPressed()
    {
        return (Pressed(Keys.LeftControl) || Pressed(Keys.RightControl)) && JustPressed(Keys.V);
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
