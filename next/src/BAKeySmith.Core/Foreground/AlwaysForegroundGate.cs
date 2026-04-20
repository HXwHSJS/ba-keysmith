using BAKeySmith.Core.Contracts;

namespace BAKeySmith.Core.Foreground;

public sealed class AlwaysForegroundGate : IForegroundGate
{
    public static readonly AlwaysForegroundGate Instance = new();

    private AlwaysForegroundGate()
    {
    }

    public ValueTask<ForegroundGateResult> CheckAsync(
        RuntimeConfig config,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new ForegroundGateResult(
            true,
            TargetProcess: config.TargetProcess,
            ForegroundProcess: config.TargetProcess));
    }
}
