namespace mono8.core.input;

public static class GamepadInput
{
    public static bool JustPressed(Buttons button, PlayerIndex playerIndex = PlayerIndex.One)
    {
        return InputStateManager.CurrentGamePadState(playerIndex).IsButtonDown(button) &&
               InputStateManager.PreviousGamePadState(playerIndex).IsButtonUp(button);
    }

    public static bool Released(Buttons button, PlayerIndex playerIndex = PlayerIndex.One)
    {
        return InputStateManager.CurrentGamePadState(playerIndex).IsButtonUp(button) &&
               InputStateManager.PreviousGamePadState(playerIndex).IsButtonDown(button);
    }

    public static bool Pressed(Buttons button, PlayerIndex playerIndex = PlayerIndex.One)
    {
        return InputStateManager.CurrentGamePadState(playerIndex).IsButtonDown(button);
    }

    public static Vector2 LeftStick(PlayerIndex playerIndex = PlayerIndex.One)
    {
        return InputStateManager.CurrentGamePadState(playerIndex).ThumbSticks.Left;
    }

    public static Vector2 RightStick(PlayerIndex playerIndex = PlayerIndex.One)
    {
        return InputStateManager.CurrentGamePadState(playerIndex).ThumbSticks.Right;
    }

    public static float LeftTrigger(PlayerIndex playerIndex = PlayerIndex.One)
    {
        return InputStateManager.CurrentGamePadState(playerIndex).Triggers.Left;
    }

    public static float RightTrigger(PlayerIndex playerIndex = PlayerIndex.One)
    {
        return InputStateManager.CurrentGamePadState(playerIndex).Triggers.Right;
    }
}

