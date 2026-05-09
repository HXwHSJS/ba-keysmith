namespace BAKeySmith.Core.Input.V2;

public sealed record InputSpec(
    string CanonicalName,
    InputKind Kind,
    InputCapabilities Capabilities,
    string DisplayName,
    string ChineseDisplayName)
{
    public bool CanBeMappingTrigger => Capabilities.HasFlag(InputCapabilities.MappingTrigger);
    public bool CanBeKeyOutput => Capabilities.HasFlag(InputCapabilities.KeyOutput);
    public bool CanBeMouseOutput => Capabilities.HasFlag(InputCapabilities.MouseOutput);
    public bool CanBeHotkeyModifier => Capabilities.HasFlag(InputCapabilities.HotkeyModifier);
    public bool CanBeHotkeyMainKey => Capabilities.HasFlag(InputCapabilities.HotkeyMainKey);
    public bool CanBeCoordinateRecordHotkey => Capabilities.HasFlag(InputCapabilities.CoordinateRecordHotkey);
    public bool IsModifier => Capabilities.HasFlag(InputCapabilities.Modifier);
    public bool IsWheel => Capabilities.HasFlag(InputCapabilities.Wheel);
}
