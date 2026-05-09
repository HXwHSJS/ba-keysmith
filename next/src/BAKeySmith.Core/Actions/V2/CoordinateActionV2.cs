using BAKeySmith.Core.Input.V2;

namespace BAKeySmith.Core.Actions.V2;

public sealed record CoordinatePointV2(
    int LogicalX,
    int LogicalY,
    string ProfileId);

public sealed record CoordinateActionV2(
    ActionKindV2 Kind,
    CoordinatePointV2 Start,
    CoordinatePointV2? End = null,
    InputSpec? Button = null,
    TimeSpan? Duration = null) : ActionModelV2(Kind)
{
    public override ActionOwnershipHintV2 OwnershipHint =>
        Kind switch
        {
            ActionKindV2.TapAt => ActionOwnershipHintV2.TransientCoordinateContact,
            ActionKindV2.DownAt => ActionOwnershipHintV2.AcquireCoordinateContact,
            ActionKindV2.UpAt => ActionOwnershipHintV2.ReleaseCoordinateContact,
            ActionKindV2.HoldAt => ActionOwnershipHintV2.TimedHoldCoordinateContact,
            ActionKindV2.DragAt => ActionOwnershipHintV2.TransientCoordinateContact,
            _ => ActionOwnershipHintV2.None
        };

    public static CoordinateActionV2 TapAt(
        CoordinatePointV2 point,
        InputSpec? button = null,
        TimeSpan? duration = null) =>
        new(ActionKindV2.TapAt, point, Button: button, Duration: duration);

    public static CoordinateActionV2 DownAt(CoordinatePointV2 point, InputSpec? button = null) =>
        new(ActionKindV2.DownAt, point, Button: button);

    public static CoordinateActionV2 UpAt(CoordinatePointV2 point, InputSpec? button = null) =>
        new(ActionKindV2.UpAt, point, Button: button);

    public static CoordinateActionV2 HoldAt(CoordinatePointV2 point, TimeSpan duration, InputSpec? button = null) =>
        new(ActionKindV2.HoldAt, point, Button: button, Duration: duration);

    public static CoordinateActionV2 DragAt(
        CoordinatePointV2 start,
        CoordinatePointV2 end,
        InputSpec? button = null,
        TimeSpan duration = default) =>
        new(ActionKindV2.DragAt, start, end, button, duration);
}
