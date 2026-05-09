namespace BAKeySmith.Core.Actions.V2;

public sealed record RepeatActionV2(
    int Count,
    IReadOnlyList<ActionModelV2> Body) : ActionModelV2(ActionKindV2.Repeat)
{
    public IReadOnlyList<ActionModelV2> Body { get; } = Body.ToArray();
}
