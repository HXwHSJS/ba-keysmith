using BAKeySmith.Core.Runtime;

namespace BAKeySmith.Core.Contracts;

public sealed record RuntimeSnapshot(
    RuntimeState State,
    long Generation,
    int MappingCount,
    int ActiveWorkerCount,
    int PendingActionCount,
    int RunningActionCount,
    bool LastForegroundAllowed,
    PressOwnershipSnapshot Presses);
