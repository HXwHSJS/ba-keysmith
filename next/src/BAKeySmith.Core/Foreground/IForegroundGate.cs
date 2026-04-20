using BAKeySmith.Core.Contracts;

namespace BAKeySmith.Core.Foreground;

public interface IForegroundGate
{
    ValueTask<ForegroundGateResult> CheckAsync(RuntimeConfig config, CancellationToken cancellationToken);
}
