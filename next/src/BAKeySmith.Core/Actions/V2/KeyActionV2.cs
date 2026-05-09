using BAKeySmith.Core.Input.V2;

namespace BAKeySmith.Core.Actions.V2;

public sealed record KeyActionV2(
    ActionKindV2 Kind,
    InputSpec Key,
    TimeSpan? Duration = null) : ActionModelV2(Kind)
{
    public override ActionOwnershipHintV2 OwnershipHint =>
        Kind switch
        {
            ActionKindV2.TapKey => ActionOwnershipHintV2.TransientKey,
            ActionKindV2.DownKey => ActionOwnershipHintV2.AcquireKey,
            ActionKindV2.UpKey => ActionOwnershipHintV2.ReleaseKey,
            ActionKindV2.HoldKey => ActionOwnershipHintV2.TimedHoldKey,
            _ => ActionOwnershipHintV2.None
        };

    public static KeyActionV2 Tap(InputSpec key, TimeSpan? duration = null) =>
        new(ActionKindV2.TapKey, key, duration);

    public static KeyActionV2 Down(InputSpec key) =>
        new(ActionKindV2.DownKey, key);

    public static KeyActionV2 Up(InputSpec key) =>
        new(ActionKindV2.UpKey, key);

    public static KeyActionV2 Hold(InputSpec key, TimeSpan duration) =>
        new(ActionKindV2.HoldKey, key, duration);
}
