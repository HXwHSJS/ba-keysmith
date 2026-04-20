using BAKeySmith.Core.Contracts;

namespace BAKeySmith.Core.Triggers;

public interface ITriggerSource : IAsyncDisposable
{
    event EventHandler<TriggerEvent>? Triggered;
    bool IsRunning { get; }
    ValueTask StartAsync(CancellationToken cancellationToken);
    ValueTask StopAsync(CancellationToken cancellationToken);
}
