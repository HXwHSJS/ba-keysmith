using BAKeySmith.Core.Actions.V2;

namespace BAKeySmith.Core.Runtime.V2;

public enum RuntimeV2EventKind
{
    KeyDown,
    KeyUp,
    KeyTap,
    MouseDown,
    MouseUp,
    MouseTap,
    Wheel,
    Wait,
    CoordinateUnsupported
}

public sealed record RuntimeV2Event(
    long Sequence,
    RuntimeV2EventKind Kind,
    string Path = "",
    string? MappingId = null,
    string? CanonicalInputName = null,
    TimeSpan? Duration = null,
    WheelDirectionV2? WheelDirection = null,
    int? WheelSteps = null,
    string? CoordinateProfileId = null,
    int? LogicalX = null,
    int? LogicalY = null,
    bool IsCleanup = false);
