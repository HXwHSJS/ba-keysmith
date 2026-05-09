namespace BAKeySmith.Core.Actions.V2;

public enum WheelDirectionV2
{
    Up,
    Down
}

public sealed record WheelActionV2(
    WheelDirectionV2 Direction,
    int Steps = 1) : ActionModelV2(ActionKindV2.Wheel);
