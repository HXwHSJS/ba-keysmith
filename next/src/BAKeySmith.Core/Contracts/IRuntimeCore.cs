namespace BAKeySmith.Core.Contracts;

public interface IRuntimeCore : IAsyncDisposable
{
    RuntimeState State { get; }
    RuntimeSnapshot Snapshot();
    ValueTask LoadAsync(RuntimeConfig config, CancellationToken cancellationToken);
    ValueTask EnableAsync(CancellationToken cancellationToken);
    ValueTask DisableAsync(CancellationToken cancellationToken);
    ValueTask ReloadAsync(RuntimeConfig config, CancellationToken cancellationToken);
    ValueTask StopAsync(CancellationToken cancellationToken);
    ValueTask HandleTriggerAsync(TriggerEvent triggerEvent, CancellationToken cancellationToken);
}
