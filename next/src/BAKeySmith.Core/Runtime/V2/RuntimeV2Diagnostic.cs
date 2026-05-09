namespace BAKeySmith.Core.Runtime.V2;

public enum RuntimeV2DiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public enum RuntimeV2DiagnosticCode
{
    ActionPlanInvalid,
    ActionValidationDiagnostic,
    ActionStepBudgetExceeded,
    ExecutionCancelled,
    InvalidMaxActionSteps,
    InvalidMaxWhileHeldIterations,
    InvalidMappingId,
    MappingAlreadyRunning,
    NoActiveSession,
    SessionStartFailed,
    SessionCancelFailed,
    ManagerInvariantViolation,
    DuplicateAcquire,
    ReleaseWithoutAcquire,
    CoordinateUnsupported,
    InvalidSessionState,
    InvalidWhileHeldIterationCount
}

public sealed record RuntimeV2Diagnostic(
    RuntimeV2DiagnosticSeverity Severity,
    RuntimeV2DiagnosticCode Code,
    string Message,
    string Path = "",
    string? MappingId = null)
{
    public bool IsError => Severity == RuntimeV2DiagnosticSeverity.Error;
}
