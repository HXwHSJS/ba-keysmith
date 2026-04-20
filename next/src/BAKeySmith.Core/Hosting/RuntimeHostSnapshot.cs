using BAKeySmith.Core.Contracts;
using BAKeySmith.Core.Triggers;

namespace BAKeySmith.Core.Hosting;

public sealed record RuntimeHostSnapshot(
    bool IsStarted,
    bool IsPipelineRunning,
    TriggerPipelineSnapshot Pipeline,
    RuntimeConfig RuntimeConfig,
    RuntimeSnapshot Runtime);
