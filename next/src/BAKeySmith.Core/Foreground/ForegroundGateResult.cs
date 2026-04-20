namespace BAKeySmith.Core.Foreground;

public sealed record ForegroundGateResult(
    bool IsAllowed,
    string? TargetProcess = null,
    string? ForegroundProcess = null,
    string? ForegroundWindowTitle = null);
