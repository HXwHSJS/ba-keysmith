using BAKeySmith.Core.Contracts;

namespace BAKeySmith.Core.Foreground;

public sealed class ManualForegroundGate : IForegroundGate
{
    public bool IsAllowed { get; set; } = true;
    public string ForegroundProcess { get; set; } = "BlueArchive.exe";

    public ValueTask<ForegroundGateResult> CheckAsync(
        RuntimeConfig config,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new ForegroundGateResult(
            IsAllowed,
            TargetProcess: config.TargetProcess,
            ForegroundProcess: ForegroundProcess));
    }
}
