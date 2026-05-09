using BAKeySmith.Core.Input.V2;

namespace BAKeySmith.Core.Actions.V2;

public sealed record MouseButtonActionV2(
    ActionKindV2 Kind,
    InputSpec Button,
    TimeSpan? Duration = null) : ActionModelV2(Kind)
{
    public override ActionOwnershipHintV2 OwnershipHint =>
        Kind switch
        {
            ActionKindV2.TapMouse => ActionOwnershipHintV2.TransientMouseButton,
            ActionKindV2.DownMouse => ActionOwnershipHintV2.AcquireMouseButton,
            ActionKindV2.UpMouse => ActionOwnershipHintV2.ReleaseMouseButton,
            ActionKindV2.HoldMouse => ActionOwnershipHintV2.TimedHoldMouseButton,
            _ => ActionOwnershipHintV2.None
        };

    public static MouseButtonActionV2 Tap(InputSpec button, TimeSpan? duration = null) =>
        new(ActionKindV2.TapMouse, button, duration);

    public static MouseButtonActionV2 Down(InputSpec button) =>
        new(ActionKindV2.DownMouse, button);

    public static MouseButtonActionV2 Up(InputSpec button) =>
        new(ActionKindV2.UpMouse, button);

    public static MouseButtonActionV2 Hold(InputSpec button, TimeSpan duration) =>
        new(ActionKindV2.HoldMouse, button, duration);
}
