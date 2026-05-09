namespace BAKeySmith.Core.Input.V2.Capture;

public static class WindowsInputNormalizerV2
{
    private const uint VkBack = 0x08;
    private const uint VkTab = 0x09;
    private const uint VkReturn = 0x0D;
    private const uint VkShift = 0x10;
    private const uint VkControl = 0x11;
    private const uint VkMenu = 0x12;
    private const uint VkEscape = 0x1B;
    private const uint VkSpace = 0x20;
    private const uint VkPageUp = 0x21;
    private const uint VkPageDown = 0x22;
    private const uint VkEnd = 0x23;
    private const uint VkHome = 0x24;
    private const uint VkLeft = 0x25;
    private const uint VkUp = 0x26;
    private const uint VkRight = 0x27;
    private const uint VkDown = 0x28;
    private const uint VkInsert = 0x2D;
    private const uint VkDelete = 0x2E;
    private const uint VkLWin = 0x5B;
    private const uint VkRWin = 0x5C;
    private const uint VkNumpad0 = 0x60;
    private const uint VkNumpad9 = 0x69;
    private const uint VkF1 = 0x70;
    private const uint VkF24 = 0x87;
    private const uint VkLShift = 0xA0;
    private const uint VkRShift = 0xA1;
    private const uint VkLControl = 0xA2;
    private const uint VkRControl = 0xA3;
    private const uint VkLMenu = 0xA4;
    private const uint VkRMenu = 0xA5;
    private const uint VkOem1 = 0xBA;
    private const uint VkOemPlus = 0xBB;
    private const uint VkOemComma = 0xBC;
    private const uint VkOemMinus = 0xBD;
    private const uint VkOemPeriod = 0xBE;
    private const uint VkOem2 = 0xBF;
    private const uint VkOem3 = 0xC0;
    private const uint VkOem4 = 0xDB;
    private const uint VkOem5 = 0xDC;
    private const uint VkOem6 = 0xDD;
    private const uint VkOem7 = 0xDE;

    public static InputCaptureResultV2 NormalizeKey(WindowsKeyEventV2 keyEvent)
    {
        var canonicalName = TryGetCanonicalKeyboardName(keyEvent);
        if (canonicalName is null)
        {
            return InputCaptureResultV2.Unsupported(
                $"Unsupported Windows virtual key: 0x{keyEvent.VirtualKeyCode:X}",
                keyEvent.SourceKind);
        }

        return InputCaptureResultV2.Captured(
            InputNameResolverV2.Resolve(canonicalName),
            keyEvent.SourceKind);
    }

    public static InputCaptureResultV2 NormalizeMouse(WindowsMouseEventV2 mouseEvent)
    {
        var canonicalName = mouseEvent.Kind switch
        {
            WindowsMouseEventKindV2.LeftButton => "mouse_left",
            WindowsMouseEventKindV2.RightButton => "mouse_right",
            WindowsMouseEventKindV2.MiddleButton => "mouse_middle",
            WindowsMouseEventKindV2.XButton1 => "mouse_x1",
            WindowsMouseEventKindV2.XButton2 => "mouse_x2",
            WindowsMouseEventKindV2.Wheel when mouseEvent.WheelDelta > 0 => "mouse_wheel_up",
            WindowsMouseEventKindV2.Wheel when mouseEvent.WheelDelta < 0 => "mouse_wheel_down",
            _ => null
        };

        if (canonicalName is null)
        {
            return InputCaptureResultV2.Unsupported(
                "Unsupported Windows mouse event.",
                mouseEvent.SourceKind);
        }

        return InputCaptureResultV2.Captured(
            InputNameResolverV2.Resolve(canonicalName),
            mouseEvent.SourceKind);
    }

    private static string? TryGetCanonicalKeyboardName(WindowsKeyEventV2 keyEvent)
    {
        var virtualKey = keyEvent.VirtualKeyCode;
        if (virtualKey is >= (uint)'A' and <= (uint)'Z')
        {
            return ((char)virtualKey).ToString().ToLowerInvariant();
        }

        if (virtualKey is >= (uint)'0' and <= (uint)'9')
        {
            return ((char)virtualKey).ToString();
        }

        if (virtualKey is >= VkNumpad0 and <= VkNumpad9)
        {
            return $"numpad{virtualKey - VkNumpad0}";
        }

        if (virtualKey is >= VkF1 and <= VkF24)
        {
            return $"f{virtualKey - VkF1 + 1}";
        }

        return virtualKey switch
        {
            VkLControl => "left_ctrl",
            VkRControl => "right_ctrl",
            VkControl when keyEvent.IsExtended => "right_ctrl",
            VkControl => "left_ctrl",
            VkLMenu => "left_alt",
            VkRMenu => "right_alt",
            VkMenu when keyEvent.IsExtended => "right_alt",
            VkMenu => "left_alt",
            VkLShift => "left_shift",
            VkRShift => "right_shift",
            VkShift when keyEvent.ScanCode == 0x2A => "left_shift",
            VkShift when keyEvent.ScanCode == 0x36 => "right_shift",
            VkShift => null,
            VkLWin => "left_win",
            VkRWin => "right_win",
            VkEscape => "escape",
            VkReturn => "enter",
            VkTab => "tab",
            VkSpace => "space",
            VkBack => "backspace",
            VkDelete => "delete",
            VkInsert => "insert",
            VkHome => "home",
            VkEnd => "end",
            VkPageUp => "page_up",
            VkPageDown => "page_down",
            VkUp => "arrow_up",
            VkDown => "arrow_down",
            VkLeft => "arrow_left",
            VkRight => "arrow_right",
            VkOem3 => "key_grave",
            VkOemMinus => "key_minus",
            VkOemPlus => "key_equal",
            VkOem4 => "key_left_bracket",
            VkOem6 => "key_right_bracket",
            VkOem5 => "key_backslash",
            VkOem1 => "key_semicolon",
            VkOem7 => "key_quote",
            VkOemComma => "key_comma",
            VkOemPeriod => "key_period",
            VkOem2 => "key_slash",
            _ => null
        };
    }
}
