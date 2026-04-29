using System.Windows.Input;
using BAKeySmith.App.Models;
using BAKeySmith.Core.Input;

namespace BAKeySmith.App.Services;

public static class KeyCaptureFormatter
{
    public static KeyCaptureResult FormatMappingTrigger(Key key)
    {
        if (!TryFormatKeyboardKey(key, out var keyName))
        {
            return UnsupportedKey(key);
        }

        return KeyNameResolver.TryResolveKeyboardKey(keyName, out _)
            ? KeyCaptureResult.Captured(keyName)
            : KeyCaptureResult.Unsupported($"Unsupported captured key: {key}. Please type the key name manually.");
    }

    public static KeyCaptureResult FormatMappingTrigger(MouseButton button)
    {
        return FormatMouseButton(button, trigger: true);
    }

    public static KeyCaptureResult FormatMappingTarget(Key key)
    {
        return FormatMappingTrigger(key);
    }

    public static KeyCaptureResult FormatMappingTarget(MouseButton button)
    {
        return FormatMouseButton(button, trigger: false);
    }

    public static KeyCaptureResult FormatMappingTargetWheel()
    {
        return KeyCaptureResult.Unsupported(
            "Mouse wheel capture is not supported for simple targets. Wheel remains trigger-only; type a supported target manually.");
    }

    public static KeyCaptureResult FormatMappingTriggerWheel()
    {
        return KeyCaptureResult.Unsupported(
            "Mouse wheel focused capture is not supported in v1. Type mouse_wheel_up or mouse_wheel_down manually for triggers.");
    }

    private static KeyCaptureResult FormatMouseButton(MouseButton button, bool trigger)
    {
        var keyName = button switch
        {
            MouseButton.Left => "mouse_left",
            MouseButton.Right => "mouse_right",
            MouseButton.Middle => "mouse_middle",
            MouseButton.XButton1 => "mouse_x1",
            MouseButton.XButton2 => "mouse_x2",
            _ => string.Empty
        };

        var isSupported = trigger
            ? KeyNameResolver.IsMouseTrigger(keyName)
            : KeyNameResolver.IsMouseButton(keyName);

        return !string.IsNullOrWhiteSpace(keyName) && isSupported
            ? KeyCaptureResult.Captured(keyName)
            : KeyCaptureResult.Unsupported($"Unsupported captured mouse button: {button}. Please type the key name manually.");
    }

    public static KeyCaptureResult FormatHotkey(Key key, ModifierKeys modifiers)
    {
        if (IsModifierKey(key))
        {
            return KeyCaptureResult.Incomplete("Hotkey capture needs a non-modifier key. Press one more key or type the hotkey manually.");
        }

        if ((modifiers & ModifierKeys.Windows) != 0)
        {
            return KeyCaptureResult.Unsupported("Windows-key hotkey capture is not supported in focused capture v1. Please type a supported hotkey manually.");
        }

        if (!TryFormatKeyboardKey(key, out var keyName))
        {
            return UnsupportedKey(key);
        }

        var parts = new List<string>(capacity: 4);
        if ((modifiers & ModifierKeys.Control) != 0)
        {
            parts.Add("ctrl");
        }

        if ((modifiers & ModifierKeys.Shift) != 0)
        {
            parts.Add("shift");
        }

        if ((modifiers & ModifierKeys.Alt) != 0)
        {
            parts.Add("alt");
        }

        parts.Add(keyName);
        if (parts.Any(part => !KeyNameResolver.TryResolveKeyboardKey(part, out _)))
        {
            return KeyCaptureResult.Unsupported("Captured hotkey contains an unsupported key. Please type the hotkey manually.");
        }

        return KeyCaptureResult.Captured(string.Join("+", parts));
    }

    public static Key GetEffectiveKey(
        Key key,
        Key systemKey,
        Key imeProcessedKey,
        Key deadCharProcessedKey)
    {
        return key switch
        {
            Key.System => systemKey,
            Key.ImeProcessed => imeProcessedKey == Key.None ? Key.ImeProcessed : imeProcessedKey,
            Key.DeadCharProcessed => deadCharProcessedKey == Key.None ? Key.DeadCharProcessed : deadCharProcessedKey,
            _ => key
        };
    }

    private static bool TryFormatKeyboardKey(Key key, out string keyName)
    {
        keyName = key switch
        {
            >= Key.A and <= Key.Z => key.ToString().ToLowerInvariant(),
            >= Key.D0 and <= Key.D9 => ((int)key - (int)Key.D0).ToString(),
            >= Key.F1 and <= Key.F24 => $"f{(int)key - (int)Key.F1 + 1}",
            >= Key.NumPad0 and <= Key.NumPad9 => $"numpad{(int)key - (int)Key.NumPad0}",
            Key.LeftCtrl or Key.RightCtrl => "ctrl",
            Key.LeftAlt or Key.RightAlt => "alt",
            Key.LeftShift or Key.RightShift => "shift",
            Key.Back => "backspace",
            Key.Tab => "tab",
            Key.Enter or Key.Return => "enter",
            Key.Escape => "escape",
            Key.Space => "space",
            Key.Up => "arrow_up",
            Key.Down => "arrow_down",
            Key.Left => "arrow_left",
            Key.Right => "arrow_right",
            Key.Home => "home",
            Key.End => "end",
            Key.PageUp or Key.Prior => "page_up",
            Key.PageDown or Key.Next => "page_down",
            Key.Insert => "insert",
            Key.Delete => "delete",
            Key.CapsLock or Key.Capital => "caps_lock",
            Key.NumLock => "num_lock",
            Key.Scroll => "scroll_lock",
            Key.Add => "add",
            Key.Subtract => "subtract",
            Key.Multiply => "multiply",
            Key.Divide => "divide",
            Key.Decimal => "decimal",
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(keyName);
    }

    private static bool IsModifierKey(Key key)
    {
        return key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift;
    }

    private static KeyCaptureResult UnsupportedKey(Key key)
    {
        return KeyCaptureResult.Unsupported($"Unsupported captured key: {key}. Please type the key name manually.");
    }
}
