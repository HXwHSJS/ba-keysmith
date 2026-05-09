using BAKeySmith.Core.Input.V2;

namespace BAKeySmith.Core.Configuration.V2;

public enum TriggerKindV2
{
    SingleInput
}

public sealed record TriggerConfigV2(
    TriggerKindV2 Kind,
    InputSpec? Input)
{
    public static TriggerConfigV2 SingleInput(InputSpec input) =>
        new(TriggerKindV2.SingleInput, input);
}
