namespace BAKeySmith.Core.Actions.V2;

public sealed record WhileHeldBlockV2(
    TimeSpan Interval,
    IReadOnlyList<ActionModelV2> Body)
{
    public IReadOnlyList<ActionModelV2> Body { get; } = Body.ToArray();
}

public sealed record ActivationPlanV2(
    IReadOnlyList<ActionModelV2> OnDown,
    WhileHeldBlockV2? WhileHeld = null,
    IReadOnlyList<ActionModelV2>? OnUp = null)
{
    public IReadOnlyList<ActionModelV2> OnDown { get; } = OnDown.ToArray();
    public IReadOnlyList<ActionModelV2> OnUp { get; } = (OnUp ?? []).ToArray();
}
