namespace BAKeySmith.Core.Input;

public sealed record KeyInfo(string Name, ushort VirtualKey, bool IsExtended = false);

public static class KeyNameResolver
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["key.esc"] = "escape",
        ["esc"] = "escape",
        ["return"] = "enter",
        ["caps_lock"] = "caps lock",
        ["capslock"] = "caps lock",
        ["capital"] = "caps lock",
        ["ins"] = "insert",
        ["del"] = "delete",
        ["pageup"] = "page up",
        ["page_up"] = "page up",
        ["pgup"] = "page up",
        ["pagedown"] = "page down",
        ["page_down"] = "page down",
        ["pgdn"] = "page down",
        ["num_lock"] = "num lock",
        ["numlock"] = "num lock",
        ["scroll_lock"] = "scroll lock",
        ["scrolllock"] = "scroll lock",
        ["control"] = "ctrl",
        ["ctl"] = "ctrl",
        ["ctrl_l"] = "ctrl",
        ["ctrl_r"] = "ctrl",
        ["control_l"] = "ctrl",
        ["control_r"] = "ctrl",
        ["left ctrl"] = "ctrl",
        ["right ctrl"] = "ctrl",
        ["left control"] = "ctrl",
        ["right control"] = "ctrl",
        ["alt_l"] = "alt",
        ["alt_r"] = "alt",
        ["alt_gr"] = "alt",
        ["left alt"] = "alt",
        ["right alt"] = "alt",
        ["option"] = "alt",
        ["shift_l"] = "shift",
        ["shift_r"] = "shift",
        ["left shift"] = "shift",
        ["right shift"] = "shift",
        ["up"] = "arrow up",
        ["down"] = "arrow down",
        ["left"] = "arrow left",
        ["right"] = "arrow right"
    };

    private static readonly Dictionary<string, KeyInfo> Keys = BuildKeys();
    private static readonly Dictionary<uint, string> VirtualKeyNames = BuildVirtualKeyNames();

    public static string Normalize(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key name must not be empty.", nameof(key));
        }

        var normalized = key.Trim().ToLowerInvariant();
        if (normalized.StartsWith("key.", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[4..];
        }

        return Aliases.TryGetValue(normalized, out var alias) ? alias : normalized;
    }

    public static bool TryResolveKeyboardKey(string key, out KeyInfo keyInfo)
    {
        var normalized = Normalize(key);
        if (Keys.TryGetValue(normalized, out keyInfo!))
        {
            return true;
        }

        if (normalized.Contains('_') &&
            Keys.TryGetValue(normalized.Replace('_', ' '), out keyInfo!))
        {
            return true;
        }

        return false;
    }

    public static KeyInfo ResolveKeyboardKey(string key)
    {
        if (TryResolveKeyboardKey(key, out var keyInfo))
        {
            return keyInfo;
        }

        throw new ArgumentException($"Unsupported keyboard key: {key}", nameof(key));
    }

    public static bool TryGetNameFromVirtualKey(uint virtualKey, out string name)
    {
        return VirtualKeyNames.TryGetValue(virtualKey, out name!);
    }

    public static IReadOnlyList<string> KeyboardKeyNames { get; } =
        Keys.Keys.Order(StringComparer.OrdinalIgnoreCase).ToArray();

    public static IReadOnlyList<string> ScriptKeyboardKeyNames { get; } =
        Keys.Keys
            .Select(key => key.Replace(' ', '_'))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static IReadOnlyList<string> MouseButtonNames { get; } =
    [
        "mouse_left",
        "mouse_right",
        "mouse_middle",
        "mouse_x1",
        "mouse_x2"
    ];

    public static IReadOnlyList<string> MouseTriggerNames { get; } =
    [
        "mouse_left",
        "mouse_right",
        "mouse_middle",
        "mouse_x1",
        "mouse_x2",
        "mouse_wheel_up",
        "mouse_wheel_down"
    ];

    public static bool IsMouseButton(string key)
    {
        var normalized = Normalize(key);
        return normalized is "mouse_left" or "mouse_right" or "mouse_middle" or "mouse_x1" or "mouse_x2";
    }

    public static bool IsMouseTrigger(string key)
    {
        try
        {
            var normalized = NormalizeMouseTrigger(key);
            return MouseTriggerNames.Contains(normalized, StringComparer.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public static string NormalizeMouseButton(string button)
    {
        if (string.IsNullOrWhiteSpace(button))
        {
            throw new ArgumentException("Mouse button must not be empty.", nameof(button));
        }

        var normalized = button.Trim().ToLowerInvariant();
        return normalized switch
        {
            "mouse_left" or "left" => "mouse_left",
            "mouse_right" or "right" => "mouse_right",
            "mouse_middle" or "middle" => "mouse_middle",
            "mouse_x1" or "x1" => "mouse_x1",
            "mouse_x2" or "x2" => "mouse_x2",
            _ => throw new ArgumentException($"Unsupported mouse button: {button}", nameof(button))
        };
    }

    public static string NormalizeMouseTrigger(string trigger)
    {
        if (string.IsNullOrWhiteSpace(trigger))
        {
            throw new ArgumentException("Mouse trigger must not be empty.", nameof(trigger));
        }

        var normalized = trigger.Trim().ToLowerInvariant();
        if (IsMouseButtonName(normalized))
        {
            return NormalizeMouseButton(normalized);
        }

        return normalized switch
        {
            "wheel_up" or "wheelup" or "mouse_wheel_up" => "mouse_wheel_up",
            "wheel_down" or "wheeldown" or "mouse_wheel_down" => "mouse_wheel_down",
            _ => throw new ArgumentException($"Unsupported mouse trigger: {trigger}", nameof(trigger))
        };
    }

    private static bool IsMouseButtonName(string value)
    {
        return value.StartsWith("mouse_", StringComparison.OrdinalIgnoreCase)
            ? value is "mouse_left" or "mouse_right" or "mouse_middle" or "mouse_x1" or "mouse_x2"
            : value is "left" or "right" or "middle" or "x1" or "x2";
    }

    private static Dictionary<string, KeyInfo> BuildKeys()
    {
        var keys = new Dictionary<string, KeyInfo>(StringComparer.OrdinalIgnoreCase);
        for (var c = 'a'; c <= 'z'; c++)
        {
            keys[c.ToString()] = new KeyInfo(c.ToString(), (ushort)char.ToUpperInvariant(c));
        }

        for (var c = '0'; c <= '9'; c++)
        {
            keys[c.ToString()] = new KeyInfo(c.ToString(), (ushort)c);
        }

        for (var i = 1; i <= 24; i++)
        {
            keys[$"f{i}"] = new KeyInfo($"f{i}", (ushort)(0x70 + i - 1));
        }

        foreach (var item in new[]
        {
            new KeyInfo("backspace", 0x08),
            new KeyInfo("tab", 0x09),
            new KeyInfo("enter", 0x0D),
            new KeyInfo("shift", 0xA0),
            new KeyInfo("ctrl", 0xA2),
            new KeyInfo("alt", 0xA4),
            new KeyInfo("pause", 0x13),
            new KeyInfo("caps lock", 0x14),
            new KeyInfo("escape", 0x1B),
            new KeyInfo("space", 0x20),
            new KeyInfo("page up", 0x21, true),
            new KeyInfo("page down", 0x22, true),
            new KeyInfo("end", 0x23, true),
            new KeyInfo("home", 0x24, true),
            new KeyInfo("arrow left", 0x25, true),
            new KeyInfo("arrow up", 0x26, true),
            new KeyInfo("arrow right", 0x27, true),
            new KeyInfo("arrow down", 0x28, true),
            new KeyInfo("insert", 0x2D, true),
            new KeyInfo("delete", 0x2E, true),
            new KeyInfo("num lock", 0x90),
            new KeyInfo("scroll lock", 0x91),
            new KeyInfo("numpad0", 0x60),
            new KeyInfo("numpad1", 0x61),
            new KeyInfo("numpad2", 0x62),
            new KeyInfo("numpad3", 0x63),
            new KeyInfo("numpad4", 0x64),
            new KeyInfo("numpad5", 0x65),
            new KeyInfo("numpad6", 0x66),
            new KeyInfo("numpad7", 0x67),
            new KeyInfo("numpad8", 0x68),
            new KeyInfo("numpad9", 0x69),
            new KeyInfo("multiply", 0x6A),
            new KeyInfo("add", 0x6B),
            new KeyInfo("subtract", 0x6D),
            new KeyInfo("decimal", 0x6E),
            new KeyInfo("divide", 0x6F, true),
            new KeyInfo(";", 0xBA),
            new KeyInfo("=", 0xBB),
            new KeyInfo(",", 0xBC),
            new KeyInfo("-", 0xBD),
            new KeyInfo(".", 0xBE),
            new KeyInfo("/", 0xBF),
            new KeyInfo("`", 0xC0),
            new KeyInfo("[", 0xDB),
            new KeyInfo("\\", 0xDC),
            new KeyInfo("]", 0xDD),
            new KeyInfo("'", 0xDE)
        })
        {
            keys[item.Name] = item;
        }

        return keys;
    }

    private static Dictionary<uint, string> BuildVirtualKeyNames()
    {
        var names = Keys.Values
            .GroupBy(key => (uint)key.VirtualKey)
            .ToDictionary(
                group => group.Key,
                group => group.First().Name);

        names[0x10] = "shift";
        names[0xA0] = "shift";
        names[0xA1] = "shift";
        names[0x11] = "ctrl";
        names[0xA2] = "ctrl";
        names[0xA3] = "ctrl";
        names[0x12] = "alt";
        names[0xA4] = "alt";
        names[0xA5] = "alt";
        return names;
    }
}
