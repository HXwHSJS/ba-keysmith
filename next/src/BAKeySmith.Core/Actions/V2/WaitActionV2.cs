namespace BAKeySmith.Core.Actions.V2;

public sealed record WaitActionV2(TimeSpan Duration) : ActionModelV2(ActionKindV2.Wait);
