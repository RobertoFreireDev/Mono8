namespace mono8.core.input;

internal static class MouseInput
{
    private static bool _swallowingClick;

    // The click that brings the window back from unfocused must only restore focus. Position and
    // wheel keep working, so hover feedback is still live under the dimmed screen.
    public static void SwallowUntilRelease()
    {
        _swallowingClick = true;
    }

    public static void Update()
    {
        if (!_swallowingClick)
            return;

        var current = InputStateManager.CurrentMouseState();
        var previous = InputStateManager.PreviousMouseState();

        // Both frames must be up before the gate opens: clearing on the release edge itself would
        // still let the swallowed click fire anything that acts on release.
        if (current.LeftButton == ButtonState.Released && previous.LeftButton == ButtonState.Released &&
            current.RightButton == ButtonState.Released && previous.RightButton == ButtonState.Released)
        {
            _swallowingClick = false;
        }
    }

    public static (int x, int y) MouseVirtualPosition(int offsetX = 0)
    {
        return (
                (int)((-Screen.BoxToDraw.X + InputStateManager.CurrentMouseState().Position.X - offsetX * Screen.ScaleX) / Screen.ScaleX),
                (int)((-Screen.BoxToDraw.Y + InputStateManager.CurrentMouseState().Position.Y) / Screen.ScaleY)
            );
    }

    public static bool ScrollUp()
    {
        return InputStateManager.CurrentMouseState().ScrollWheelValue >
            InputStateManager.PreviousMouseState().ScrollWheelValue;
    }

    public static bool ScrollDown()
    {
        return InputStateManager.CurrentMouseState().ScrollWheelValue <
             InputStateManager.PreviousMouseState().ScrollWheelValue;
    }

    public static bool LeftButton_JustPressed()
    {
        if (_swallowingClick) return false;
        return JustPressed(InputStateManager.CurrentMouseState().LeftButton, InputStateManager.PreviousMouseState().LeftButton);
    }

    public static bool LeftButton_Released()
    {
        if (_swallowingClick) return false;
        return Released(InputStateManager.CurrentMouseState().LeftButton, InputStateManager.PreviousMouseState().LeftButton);
    }

    public static bool LeftButton_Pressed()
    {
        if (_swallowingClick) return false;
        return Pressed(InputStateManager.CurrentMouseState().LeftButton);
    }

    public static bool RightButton_JustPressed()
    {
        if (_swallowingClick) return false;
        return JustPressed(InputStateManager.CurrentMouseState().RightButton, InputStateManager.PreviousMouseState().RightButton);
    }

    public static bool RightButton_Released()
    {
        if (_swallowingClick) return false;
        return Released(InputStateManager.CurrentMouseState().RightButton, InputStateManager.PreviousMouseState().RightButton);
    }

    public static bool RightButton_Pressed()
    {
        if (_swallowingClick) return false;
        return Pressed(InputStateManager.CurrentMouseState().RightButton);
    }

    private static bool JustPressed(ButtonState currentButtonState, ButtonState previousButtonState)
    {
        return currentButtonState == ButtonState.Pressed && previousButtonState == ButtonState.Released;
    }

    private static bool Released(ButtonState currentButtonState, ButtonState previousButtonState)
    {
        return currentButtonState == ButtonState.Released && previousButtonState == ButtonState.Pressed;
    }

    private static bool Pressed(ButtonState currentButtonState)
    {
        return currentButtonState == ButtonState.Pressed;
    }
}