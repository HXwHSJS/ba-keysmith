namespace BAKeySmith.Core.Triggers;

public sealed record TriggerPipelineSnapshot(
    bool IsRunning,
    long QueuedCount,
    long HandledCount,
    long DroppedCount,
    long PendingCount);
