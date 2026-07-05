namespace mono8.core.input;

public static class InputStateManager
{
    private static KeyboardState _currentKeyboardState;
    private static KeyboardState _previousKeyboardState;
    private static MouseState _currentMouseState;
    private static MouseState _previousMouseState;
    private static readonly Dictionary<PlayerIndex, GamePadState> _previousGamePadStates = new();
    private static readonly Dictionary<PlayerIndex, GamePadState> _currentGamePadStates = new();

    public static KeyboardState CurrentKeyboardState()
    {
        return _currentKeyboardState;
    }

    public static KeyboardState PreviousKeyboardState()
    {
        return _previousKeyboardState;
    }

    public static MouseState CurrentMouseState()
    {
        return _currentMouseState;
    }

    public static MouseState PreviousMouseState()
    {
        return _previousMouseState;
    }

    public static GamePadState CurrentGamePadState(PlayerIndex index)
    {
        if (_currentGamePadStates.TryGetValue(index, out var state))
            return state;

        return GamePad.GetState(index);
    }

    public static GamePadState PreviousGamePadState(PlayerIndex index)
    {
        if (_previousGamePadStates.TryGetValue(index, out var state))
            return state;

        return GamePad.GetState(index);
    }

    public static bool IsGamePadConnected(PlayerIndex index)
    {
        return _currentGamePadStates.ContainsKey(index) && _currentGamePadStates[index].IsConnected;
    }

    public static void Update()
    {
        _previousKeyboardState = _currentKeyboardState;
        _currentKeyboardState = Keyboard.GetState();

        _previousMouseState = _currentMouseState;
        _currentMouseState = Mouse.GetState();

        foreach (var index in Enum.GetValues<PlayerIndex>())
        {
            _previousGamePadStates[index] = _currentGamePadStates.ContainsKey(index)
                ? _currentGamePadStates[index]
                : GamePad.GetState(index);
        }

        foreach (var index in Enum.GetValues<PlayerIndex>())
        {
            _currentGamePadStates[index] = GamePad.GetState(index);
        }
    }
}
