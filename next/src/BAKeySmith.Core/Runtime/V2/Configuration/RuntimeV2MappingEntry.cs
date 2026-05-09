using BAKeySmith.Core.Actions.V2;
using BAKeySmith.Core.Input.V2;

namespace BAKeySmith.Core.Runtime.V2.Configuration;

public sealed record RuntimeV2MappingEntry(
    string MappingId,
    string Name,
    InputSpec TriggerInput,
    ActivationPlanV2 Plan,
    string MappingPath = "");
