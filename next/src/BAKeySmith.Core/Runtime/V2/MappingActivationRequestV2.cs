using BAKeySmith.Core.Actions.V2;

namespace BAKeySmith.Core.Runtime.V2;

public sealed record MappingActivationRequestV2(
    string MappingId,
    ActivationPlanV2 Plan,
    RuntimeV2ExecutionOptions? Options = null);
