namespace BAKeySmith.Core.Input.V2;

[Flags]
public enum InputCapabilities
{
    None = 0,
    MappingTrigger = 1 << 0,
    KeyOutput = 1 << 1,
    MouseOutput = 1 << 2,
    HotkeyModifier = 1 << 3,
    HotkeyMainKey = 1 << 4,
    CoordinateRecordHotkey = 1 << 5,
    Modifier = 1 << 6,
    Wheel = 1 << 7
}
