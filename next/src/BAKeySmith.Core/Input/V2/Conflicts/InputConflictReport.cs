namespace BAKeySmith.Core.Input.V2.Conflicts;

public sealed record InputConflictReport(
    InputConflictSeverity Severity,
    InputConflictCode Code,
    string Message,
    IReadOnlyList<string> AffectedBindingIds,
    IReadOnlyList<string> AffectedBindingNames,
    bool BlocksSave,
    bool BlocksLive);
