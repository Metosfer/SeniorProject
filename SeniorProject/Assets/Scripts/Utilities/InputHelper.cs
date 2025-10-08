using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

/// <summary>
/// Bridges legacy Input API usage with the new Input System so gameplay code
/// can rely on a single helper that works in both Editor and builds.
/// </summary>
public static class InputHelper
{
    public static bool GetKeyDown(KeyCode keyCode)
    {
        bool legacy = Input.GetKeyDown(keyCode);
#if ENABLE_INPUT_SYSTEM
        if (!legacy && TryGetKeyControl(keyCode, out var control))
        {
            return control.wasPressedThisFrame;
        }
#endif
        return legacy;
    }

    public static bool GetKey(KeyCode keyCode)
    {
        bool legacy = Input.GetKey(keyCode);
#if ENABLE_INPUT_SYSTEM
        if (!legacy && TryGetKeyControl(keyCode, out var control))
        {
            return control.isPressed;
        }
#endif
        return legacy;
    }

    public static bool GetKeyUp(KeyCode keyCode)
    {
        bool legacy = Input.GetKeyUp(keyCode);
#if ENABLE_INPUT_SYSTEM
        if (!legacy && TryGetKeyControl(keyCode, out var control))
        {
            return control.wasReleasedThisFrame;
        }
#endif
        return legacy;
    }

#if ENABLE_INPUT_SYSTEM
    private static bool TryGetKeyControl(KeyCode keyCode, out KeyControl control)
    {
        control = null;
        var keyboard = Keyboard.current;
        if (keyboard == null) return false;

        if (TryMapToInputSystemKey(keyCode, out var key))
        {
            control = keyboard[key];
            return control != null;
        }

        return false;
    }

    private static bool TryMapToInputSystemKey(KeyCode keyCode, out Key key)
    {
        switch (keyCode)
        {
            case KeyCode.Alpha0: key = Key.Digit0; return true;
            case KeyCode.Alpha1: key = Key.Digit1; return true;
            case KeyCode.Alpha2: key = Key.Digit2; return true;
            case KeyCode.Alpha3: key = Key.Digit3; return true;
            case KeyCode.Alpha4: key = Key.Digit4; return true;
            case KeyCode.Alpha5: key = Key.Digit5; return true;
            case KeyCode.Alpha6: key = Key.Digit6; return true;
            case KeyCode.Alpha7: key = Key.Digit7; return true;
            case KeyCode.Alpha8: key = Key.Digit8; return true;
            case KeyCode.Alpha9: key = Key.Digit9; return true;
            case KeyCode.Keypad0: key = Key.Numpad0; return true;
            case KeyCode.Keypad1: key = Key.Numpad1; return true;
            case KeyCode.Keypad2: key = Key.Numpad2; return true;
            case KeyCode.Keypad3: key = Key.Numpad3; return true;
            case KeyCode.Keypad4: key = Key.Numpad4; return true;
            case KeyCode.Keypad5: key = Key.Numpad5; return true;
            case KeyCode.Keypad6: key = Key.Numpad6; return true;
            case KeyCode.Keypad7: key = Key.Numpad7; return true;
            case KeyCode.Keypad8: key = Key.Numpad8; return true;
            case KeyCode.Keypad9: key = Key.Numpad9; return true;
            case KeyCode.KeypadEnter: key = Key.NumpadEnter; return true;
            case KeyCode.Return: key = Key.Enter; return true;
            case KeyCode.BackQuote: key = Key.Backquote; return true;
        }

        if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
        {
            key = Key.A + (keyCode - KeyCode.A);
            return true;
        }

        if (keyCode >= KeyCode.F1 && keyCode <= KeyCode.F12)
        {
            key = Key.F1 + (keyCode - KeyCode.F1);
            return true;
        }

        if (Enum.TryParse(keyCode.ToString(), true, out key))
        {
            return true;
        }

        key = Key.None;
        return false;
    }
#endif
}
