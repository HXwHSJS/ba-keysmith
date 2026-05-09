namespace BAKeySmith.Core.Input.V2;

public static class InputNameResolverV2
{
    private static readonly IReadOnlyList<InputKeyDefinition> Definitions = BuildDefinitions();
    private static readonly Dictionary<string, InputKeyDefinition> CanonicalDefinitions =
        Definitions.ToDictionary(
            definition => definition.CanonicalName,
            definition => definition,
            StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> Aliases = BuildAliases(Definitions);

    public static IReadOnlyList<InputKeyDefinition> AllDefinitions => Definitions;

    public static IReadOnlyList<string> ValidateRegistryInvariants()
    {
        var errors = new List<string>();
        var canonicalNames = new Dictionary<string, InputKeyDefinition>(StringComparer.OrdinalIgnoreCase);
        var lookupOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in Definitions)
        {
            if (string.IsNullOrWhiteSpace(definition.CanonicalName))
            {
                errors.Add("Input definition has an empty canonical name.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(definition.DisplayName))
            {
                errors.Add($"{definition.CanonicalName} has an empty display name.");
            }

            if (string.IsNullOrWhiteSpace(definition.ChineseDisplayName))
            {
                errors.Add($"{definition.CanonicalName} has an empty Chinese display name.");
            }

            if (definition.Capabilities == InputCapabilities.None)
            {
                errors.Add($"{definition.CanonicalName} has no capabilities.");
            }

            if (!canonicalNames.TryAdd(definition.CanonicalName, definition))
            {
                errors.Add($"{definition.CanonicalName} is defined more than once.");
            }

            RegisterLookup(definition.CanonicalName, definition.CanonicalName, errors, lookupOwners);
            RegisterLookup(NormalizeSeparators(definition.CanonicalName), definition.CanonicalName, errors, lookupOwners);
            foreach (var alias in definition.Aliases)
            {
                if (string.IsNullOrWhiteSpace(alias))
                {
                    errors.Add($"{definition.CanonicalName} has an empty alias.");
                    continue;
                }

                RegisterLookup(alias, definition.CanonicalName, errors, lookupOwners);
                RegisterLookup(NormalizeSeparators(alias), definition.CanonicalName, errors, lookupOwners);
            }
        }

        foreach (var definition in Definitions)
        {
            if (!TryResolve(definition.CanonicalName, out var canonicalSpec) ||
                !string.Equals(canonicalSpec.CanonicalName, definition.CanonicalName, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{definition.CanonicalName} canonical lookup does not resolve to itself.");
            }

            foreach (var alias in definition.Aliases)
            {
                if (!TryResolve(alias, out var aliasSpec) ||
                    !string.Equals(aliasSpec.CanonicalName, definition.CanonicalName, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"{definition.CanonicalName} alias '{alias}' does not resolve to the definition canonical name.");
                }
            }
        }

        return errors;
    }

    public static bool TryResolve(string name, out InputSpec spec)
    {
        spec = default!;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var normalized = NormalizeLookupKey(name);
        if (!TryGetCanonicalName(normalized, out var canonical))
        {
            return false;
        }

        if (!CanonicalDefinitions.TryGetValue(canonical, out var definition))
        {
            return false;
        }

        spec = definition.ToSpec();
        return true;
    }

    public static InputSpec Resolve(string name)
    {
        if (TryResolve(name, out var spec))
        {
            return spec;
        }

        throw new ArgumentException($"Unsupported input name: {name}", nameof(name));
    }

    private static bool TryGetCanonicalName(string normalized, out string canonical)
    {
        if (Aliases.TryGetValue(normalized, out canonical!))
        {
            return true;
        }

        if (CanonicalDefinitions.ContainsKey(normalized))
        {
            canonical = normalized;
            return true;
        }

        var separatorNormalized = NormalizeSeparators(normalized);
        if (Aliases.TryGetValue(separatorNormalized, out canonical!))
        {
            return true;
        }

        if (CanonicalDefinitions.ContainsKey(separatorNormalized))
        {
            canonical = separatorNormalized;
            return true;
        }

        canonical = string.Empty;
        return false;
    }

    private static IReadOnlyList<InputKeyDefinition> BuildDefinitions()
    {
        var definitions = new List<InputKeyDefinition>();
        for (var c = 'a'; c <= 'z'; c++)
        {
            var key = c.ToString();
            definitions.Add(KeyboardMain(key, key.ToUpperInvariant(), key.ToUpperInvariant()));
        }

        for (var c = '0'; c <= '9'; c++)
        {
            var key = c.ToString();
            definitions.Add(KeyboardMain(key, key, key));
        }

        for (var i = 1; i <= 24; i++)
        {
            definitions.Add(KeyboardMain($"f{i}", $"F{i}", $"F{i}"));
        }

        definitions.AddRange(
        [
            GenericModifier("ctrl", "Ctrl", "Ctrl"),
            GenericModifier("alt", "Alt", "Alt"),
            GenericModifier("shift", "Shift", "Shift"),
            GenericModifier("win", "Win", "Win"),
            SideModifier("left_ctrl", "Left Ctrl", "左 Ctrl", ["ctrl_l", "lctrl", "left ctrl", "left_control", "control_l"]),
            SideModifier("right_ctrl", "Right Ctrl", "右 Ctrl", ["ctrl_r", "rctrl", "right ctrl", "right_control", "control_r"]),
            SideModifier("left_alt", "Left Alt", "左 Alt", ["alt_l", "lalt", "left alt"]),
            SideModifier("right_alt", "Right Alt", "右 Alt", ["alt_r", "ralt", "right alt"]),
            SideModifier("left_shift", "Left Shift", "左 Shift", ["shift_l", "lshift", "left shift"]),
            SideModifier("right_shift", "Right Shift", "右 Shift", ["shift_r", "rshift", "right shift"]),
            SideModifier("left_win", "Left Win", "左 Win", ["win_l", "lwin", "left win", "left_windows"]),
            SideModifier("right_win", "Right Win", "右 Win", ["win_r", "rwin", "right win", "right_windows"]),
            KeyboardMain("escape", "Escape", "Esc", ["esc"]),
            KeyboardMain("enter", "Enter", "回车", ["return"]),
            KeyboardMain("tab", "Tab", "Tab"),
            KeyboardMain("space", "Space", "空格"),
            KeyboardMain("backspace", "Backspace", "退格"),
            KeyboardMain("delete", "Delete", "Delete", ["del"]),
            KeyboardMain("insert", "Insert", "Insert", ["ins"]),
            KeyboardMain("home", "Home", "Home"),
            KeyboardMain("end", "End", "End"),
            KeyboardMain("page_up", "Page Up", "Page Up", ["page up", "pgup", "pageup"]),
            KeyboardMain("page_down", "Page Down", "Page Down", ["page down", "pgdn", "pagedown"]),
            KeyboardMain("arrow_up", "Arrow Up", "上方向键", ["up", "up arrow", "arrow up"]),
            KeyboardMain("arrow_down", "Arrow Down", "下方向键", ["down", "down arrow", "arrow down"]),
            KeyboardMain("arrow_left", "Arrow Left", "左方向键", ["left", "left arrow", "arrow left"]),
            KeyboardMain("arrow_right", "Arrow Right", "右方向键", ["right", "right arrow", "arrow right"]),
            KeyboardMain("caps_lock", "Caps Lock", "Caps Lock", ["caps lock", "capslock"]),
            KeyboardMain("num_lock", "Num Lock", "Num Lock", ["num lock", "numlock"]),
            KeyboardMain("scroll_lock", "Scroll Lock", "Scroll Lock", ["scroll lock", "scrolllock"]),
            KeyboardMain("key_grave", "` / ~", "` / ~", ["`", "~", "grave", "backtick", "tilde"]),
            KeyboardMain("key_minus", "- / _", "- / _", ["-", "_", "minus", "hyphen"]),
            KeyboardMain("key_equal", "= / +", "= / +", ["=", "+", "equal", "plus"]),
            KeyboardMain("key_left_bracket", "[ / {", "[ / {", ["[", "{", "left bracket", "lbracket"]),
            KeyboardMain("key_right_bracket", "] / }", "] / }", ["]", "}", "right bracket", "rbracket"]),
            KeyboardMain("key_backslash", "\\ / |", "\\ / |", ["\\", "|", "backslash"]),
            KeyboardMain("key_semicolon", "; / :", "; / :", [";", ":", "semicolon"]),
            KeyboardMain("key_quote", "' / \"", "' / \"", ["'", "\"", "quote", "apostrophe"]),
            KeyboardMain("key_comma", ", / <", ", / <", [",", "<", "comma"]),
            KeyboardMain("key_period", ". / >", ". / >", [".", ">", "period", "dot"]),
            KeyboardMain("key_slash", "/ / ?", "/ / ?", ["/", "?", "slash"]),
            MouseButton("mouse_left", "Mouse Left", "鼠标左键", ["left mouse", "mouse1"]),
            MouseButton("mouse_right", "Mouse Right", "鼠标右键", ["right mouse", "mouse2"]),
            MouseButton("mouse_middle", "Mouse Middle", "鼠标中键", ["middle mouse", "mouse3"]),
            MouseButton("mouse_x1", "Mouse X1", "鼠标侧键 1", ["x1", "mouse4"]),
            MouseButton("mouse_x2", "Mouse X2", "鼠标侧键 2", ["x2", "mouse5"]),
            MouseWheel("mouse_wheel_up", "Mouse Wheel Up", "鼠标滚轮上", ["wheel_up", "wheelup"]),
            MouseWheel("mouse_wheel_down", "Mouse Wheel Down", "鼠标滚轮下", ["wheel_down", "wheeldown"])
        ]);

        for (var i = 0; i <= 9; i++)
        {
            definitions.Add(KeyboardMain($"numpad{i}", $"Numpad {i}", $"小键盘 {i}", [$"num{i}", $"num {i}"]));
        }

        definitions.AddRange(
        [
            KeyboardMain("numpad_add", "Numpad +", "小键盘 +", ["add", "num_add", "numpad plus"]),
            KeyboardMain("numpad_subtract", "Numpad -", "小键盘 -", ["subtract", "num_subtract", "numpad minus"]),
            KeyboardMain("numpad_multiply", "Numpad *", "小键盘 *", ["multiply", "num_multiply"]),
            KeyboardMain("numpad_divide", "Numpad /", "小键盘 /", ["divide", "num_divide"]),
            KeyboardMain("numpad_decimal", "Numpad .", "小键盘 .", ["decimal", "num_decimal"])
        ]);

        return definitions;
    }

    private static InputKeyDefinition KeyboardMain(
        string canonical,
        string display,
        string chineseDisplay,
        IReadOnlyList<string>? aliases = null)
    {
        return new InputKeyDefinition(
            canonical,
            InputKind.KeyboardKey,
            InputCapabilities.MappingTrigger |
                InputCapabilities.KeyOutput |
                InputCapabilities.HotkeyMainKey |
                InputCapabilities.CoordinateRecordHotkey,
            display,
            chineseDisplay,
            aliases ?? []);
    }

    private static InputKeyDefinition GenericModifier(
        string canonical,
        string display,
        string chineseDisplay)
    {
        return Modifier(canonical, display, chineseDisplay, []);
    }

    private static InputKeyDefinition SideModifier(
        string canonical,
        string display,
        string chineseDisplay,
        IReadOnlyList<string> aliases)
    {
        return Modifier(canonical, display, chineseDisplay, aliases);
    }

    private static InputKeyDefinition Modifier(
        string canonical,
        string display,
        string chineseDisplay,
        IReadOnlyList<string> aliases)
    {
        return new InputKeyDefinition(
            canonical,
            InputKind.KeyboardKey,
            InputCapabilities.MappingTrigger |
                InputCapabilities.KeyOutput |
                InputCapabilities.HotkeyModifier |
                InputCapabilities.CoordinateRecordHotkey |
                InputCapabilities.Modifier,
            display,
            chineseDisplay,
            aliases);
    }

    private static InputKeyDefinition MouseButton(
        string canonical,
        string display,
        string chineseDisplay,
        IReadOnlyList<string> aliases)
    {
        return new InputKeyDefinition(
            canonical,
            InputKind.MouseButton,
            InputCapabilities.MappingTrigger |
                InputCapabilities.MouseOutput |
                InputCapabilities.CoordinateRecordHotkey,
            display,
            chineseDisplay,
            aliases);
    }

    private static InputKeyDefinition MouseWheel(
        string canonical,
        string display,
        string chineseDisplay,
        IReadOnlyList<string> aliases)
    {
        return new InputKeyDefinition(
            canonical,
            InputKind.MouseWheel,
            InputCapabilities.MappingTrigger |
                InputCapabilities.Wheel,
            display,
            chineseDisplay,
            aliases);
    }

    private static Dictionary<string, string> BuildAliases(IReadOnlyList<InputKeyDefinition> definitions)
    {
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
        {
            aliases[NormalizeLookupKey(definition.CanonicalName)] = definition.CanonicalName;
            aliases[NormalizeSeparators(definition.CanonicalName)] = definition.CanonicalName;
            foreach (var alias in definition.Aliases)
            {
                aliases[NormalizeLookupKey(alias)] = definition.CanonicalName;
                aliases[NormalizeSeparators(alias)] = definition.CanonicalName;
            }
        }

        return aliases;
    }

    private static void RegisterLookup(
        string lookup,
        string canonical,
        List<string> errors,
        Dictionary<string, string> lookupOwners)
    {
        var normalized = NormalizeLookupKey(lookup);
        if (lookupOwners.TryGetValue(normalized, out var existingCanonical) &&
            !string.Equals(existingCanonical, canonical, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(
                $"Lookup '{lookup}' maps to both {existingCanonical} and {canonical}.");
            return;
        }

        lookupOwners[normalized] = canonical;
    }

    private static string NormalizeLookupKey(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private static string NormalizeSeparators(string value)
    {
        return NormalizeLookupKey(value)
            .Replace(' ', '_')
            .Replace('-', '_');
    }
}
