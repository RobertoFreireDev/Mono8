using System.Linq;

namespace mono8.core.input;

/// <summary>
/// Maps PICO-8-style button indices to keyboard/gamepad inputs.
///
/// Index layout (0–7 = player 1, 8–15 = player 2):
///   +0 Left   +1 Right   +2 Up   +3 Down
///   +4 A(O)   +5 B(X)    +6 X    +7 Y
///
/// Player 1 keyboard: Left/Right/Up/Down arrows, Z, X, C, V
/// Player 2 keyboard: A/D/W/S,                  G, H, J, K
/// Gamepad P1/P2:     DPad + LeftStick,          A, B, X, Y
/// </summary>
public static class ButtonInput
{
    // ── Keyboard maps ────────────────────────────────────────────────────

    private static readonly Keys[] _p1Keys =
    [
        Keys.Left,  Keys.Right, Keys.Up,   Keys.Down,  // 0-3
        Keys.Z,     Keys.X,     Keys.C,    Keys.V      // 4-7
    ];

    private static readonly Keys[] _p2Keys =
    [
        Keys.A,     Keys.D,     Keys.W,    Keys.S,     // 0-3
        Keys.G,     Keys.H,     Keys.J,    Keys.K      // 4-7
    ];

    // ── Gamepad maps ─────────────────────────────────────────────────────

    private static readonly Buttons[] _gamepadButtons =
    [
        Buttons.DPadLeft,  Buttons.DPadRight, Buttons.DPadUp,   Buttons.DPadDown,  // 0-3
        Buttons.A,         Buttons.B,         Buttons.X,        Buttons.Y          // 4-7
    ];

    // Analog stick threshold for treating stick as a digital press
    private const float StickDeadzone = 0.5f;

    // ── Public API ───────────────────────────────────────────────────────

    public static bool Pressed(int index)     => CheckButton(index, ButtonCheckMode.Held);
    public static bool JustPressed(int index) => CheckButton(index, ButtonCheckMode.JustPressed);
    public static bool Released(int index)    => CheckButton(index, ButtonCheckMode.Released);

    // ── Core logic ───────────────────────────────────────────────────────

    private enum ButtonCheckMode { Held, JustPressed, Released }

    private static bool CheckButton(int index, ButtonCheckMode mode)
    {
        if (index < 0 || index > 15) return false;

        bool isP2     = index >= 8;
        int  btnIndex = index % 8;

        var playerIndex = isP2 ? PlayerIndex.Two : PlayerIndex.One;
        var keys        = isP2 ? _p2Keys         : _p1Keys;

        // Keyboard
        bool keyResult = mode switch
        {
            ButtonCheckMode.Held        => KeybrdInput.Pressed(keys[btnIndex]),
            ButtonCheckMode.JustPressed => KeybrdInput.JustPressed(keys[btnIndex]),
            ButtonCheckMode.Released    => KeybrdInput.Released(keys[btnIndex]),
            _                           => false
        };

        if (keyResult) return true;

        // Gamepad digital buttons (DPad + face)
        bool padResult = mode switch
        {
            ButtonCheckMode.Held        => GamepadInput.Pressed(_gamepadButtons[btnIndex], playerIndex),
            ButtonCheckMode.JustPressed => GamepadInput.JustPressed(_gamepadButtons[btnIndex], playerIndex),
            ButtonCheckMode.Released    => GamepadInput.Released(_gamepadButtons[btnIndex], playerIndex),
            _                           => false
        };

        if (padResult) return true;

        // Gamepad left stick (indices 0-3 only)
        if (btnIndex <= 3)
            return CheckStick(btnIndex, playerIndex, mode);

        return false;
    }

    private static bool CheckStick(int dirIndex, PlayerIndex playerIndex, ButtonCheckMode mode)
    {
        // dirIndex: 0=left, 1=right, 2=up, 3=down
        float currentAxis  = GetStickAxis(dirIndex, InputStateManager.CurrentGamePadState(playerIndex));
        float previousAxis = GetStickAxis(dirIndex, InputStateManager.PreviousGamePadState(playerIndex));

        bool currentDown  = currentAxis  > StickDeadzone;
        bool previousDown = previousAxis > StickDeadzone;

        return mode switch
        {
            ButtonCheckMode.Held        => currentDown,
            ButtonCheckMode.JustPressed => currentDown && !previousDown,
            ButtonCheckMode.Released    => !currentDown && previousDown,
            _                           => false
        };
    }

    private static float GetStickAxis(int dirIndex, GamePadState state)
    {
        var stick = state.ThumbSticks.Left;
        return dirIndex switch
        {
            0 => -stick.X,  // left  = negative X
            1 =>  stick.X,  // right = positive X
            2 =>  stick.Y,  // up    = positive Y (MonoGame convention)
            3 => -stick.Y,  // down  = negative Y
            _ =>  0f
        };
    }
}

/// <summary>
/// Start / Enter input detection for both keyboard and gamepad.
///
/// Keyboard: Enter or Escape
/// Gamepad P1/P2: Start button
/// </summary>
public static class StartInputBinding
{
    private static readonly Keys[] _startKeys = [Keys.Enter, Keys.Escape];

    public static bool JustPressed(PlayerIndex player = PlayerIndex.One)
        => _startKeys.Any(KeybrdInput.JustPressed)
        || GamepadInput.JustPressed(Buttons.Start, player);

    public static bool Pressed(PlayerIndex player = PlayerIndex.One)
        => _startKeys.Any(KeybrdInput.Pressed)
        || GamepadInput.Pressed(Buttons.Start, player);

    public static bool Released(PlayerIndex player = PlayerIndex.One)
        => _startKeys.Any(KeybrdInput.Released)
        || GamepadInput.Released(Buttons.Start, player);
}

/// <summary>
/// PICO-8-style mouse input functions.
///
/// Scroll:   mouse_scrollup(), mouse_scrolldown()
/// Left btn: mouse_lp()  mouse_lr()  mouse_lheld()
/// Right btn: mouse_rp() mouse_rr()  mouse_rheld()
/// Position: mouse_x(), mouse_y()
/// </summary>
public static class MouseInputBinding
{
    public static bool ScrollUp()    => MouseInput.ScrollUp();
    public static bool ScrollDown()  => MouseInput.ScrollDown();
    public static bool LeftJustPressed()  => MouseInput.LeftButton_JustPressed();
    public static bool LeftReleased()     => MouseInput.LeftButton_Released();
    public static bool LeftPressed()      => MouseInput.LeftButton_Pressed();
    public static bool RightJustPressed() => MouseInput.RightButton_JustPressed();
    public static bool RightReleased()    => MouseInput.RightButton_Released();
    public static bool RightPressed()     => MouseInput.RightButton_Pressed();
    public static (int x, int y) PosXY() => MouseInput.MouseVirtualPosition();
}
