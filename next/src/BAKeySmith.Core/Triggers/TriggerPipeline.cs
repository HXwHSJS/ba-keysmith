using System.Threading.Channels;
using BAKeySmith.Core.Contracts;
using BAKeySmith.Core.Diagnostics;

namespace BAKeySmith.Core.Triggers;

public sealed class TriggerPipeline : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly ITriggerSource _source;
    private readonly IRuntimeCore _runtime;
    private readonly IDiagnosticsSink _diagnostics;
    private Channel<TriggerEvent>? _channel;
    private CancellationTokenSource? _stop;
    private Task? _worker;
    private bool _running;
    private long _queuedCount;
    private long _handledCount;
    private long _droppedCount;

    public TriggerPipeline(
        ITriggerSource source,
        IRuntimeCore runtime,
        IDiagnosticsSink? diagnostics = null)
    {
        _source = source;
        _runtime = runtime;
        _diagnostics = diagnostics ?? NoOpDiagnosticsSink.Instance;
    }

    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _running;
            }
        }
    }

    public TriggerPipelineSnapshot Snapshot()
    {
        var queued = Interlocked.Read(ref _queuedCount);
        var handled = Interlocked.Read(ref _handledCount);
        var dropped = Interlocked.Read(ref _droppedCount);
        return new TriggerPipelineSnapshot(
            IsRunning,
            queued,
            handled,
            dropped,
            Math.Max(0, queued - handled - dropped));
    }

    public async ValueTask StartAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource? stop = null;
        Channel<TriggerEvent>? channel = null;
        Task? worker = null;
        lock (_gate)
        {
            if (_running)
            {
                return;
            }

            stop = new CancellationTokenSource();
            channel = CreateChannel();
            _source.Triggered += OnTriggered;
            worker = Task.Run(() => PumpAsync(channel, stop.Token), CancellationToken.None);
            _stop = stop;
            _channel = channel;
            _worker = worker;
            _running = true;
        }

        try
        {
            await _source.StartAsync(cancellationToken);
            _diagnostics.Emit(DiagnosticEvent.Create("trigger_pipeline", "started"));
        }
        catch
        {
            await RollbackStartAsync(stop, channel, worker);
            throw;
        }
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource? stop;
        Task? worker;
        Channel<TriggerEvent>? channel;
        lock (_gate)
        {
            if (!_running)
            {
                return;
            }

            _running = false;
            _source.Triggered -= OnTriggered;
            stop = _stop;
            worker = _worker;
            channel = _channel;
            _stop = null;
            _worker = null;
            _channel = null;
        }

        await _source.StopAsync(cancellationToken);
        channel?.Writer.TryComplete();
        stop?.Cancel();
        if (worker is not null)
        {
            await Task.WhenAny(worker, Task.Delay(TimeSpan.FromMilliseconds(500), CancellationToken.None));
        }

        MarkPendingDropped();
        stop?.Dispose();
        _diagnostics.Emit(DiagnosticEvent.Create("trigger_pipeline", "stopped"));
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
        await _source.DisposeAsync();
    }

    private void OnTriggered(object? sender, TriggerEvent triggerEvent)
    {
        Channel<TriggerEvent>? channel;
        lock (_gate)
        {
            channel = _running ? _channel : null;
        }

        if (channel is not null && channel.Writer.TryWrite(triggerEvent))
        {
            var queued = Interlocked.Increment(ref _queuedCount);
            _diagnostics.Emit(DiagnosticEvent.Create(
                "trigger_pipeline",
                "queued",
                fields: new Dictionary<string, string>
                {
                    ["trigger"] = triggerEvent.Trigger.Key,
                    ["phase"] = triggerEvent.NormalizedPhase,
                    ["queued"] = queued.ToString()
                }));
        }
    }

    private async Task PumpAsync(
        Channel<TriggerEvent> channel,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var triggerEvent in channel.Reader.ReadAllAsync(cancellationToken))
            {
                await _runtime.HandleTriggerAsync(triggerEvent, cancellationToken);
                Interlocked.Increment(ref _handledCount);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static Channel<TriggerEvent> CreateChannel()
    {
        return Channel.CreateUnbounded<TriggerEvent>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
    }

    private void MarkPendingDropped()
    {
        var queued = Interlocked.Read(ref _queuedCount);
        var handled = Interlocked.Read(ref _handledCount);
        var dropped = Interlocked.Read(ref _droppedCount);
        var pending = Math.Max(0, queued - handled - dropped);
        if (pending <= 0)
        {
            return;
        }

        Interlocked.Add(ref _droppedCount, pending);
        _diagnostics.Emit(DiagnosticEvent.Create(
            "trigger_pipeline",
            "dropped_pending",
            fields: new Dictionary<string, string>
            {
                ["dropped"] = pending.ToString()
            }));
    }

    private async ValueTask RollbackStartAsync(
        CancellationTokenSource? stop,
        Channel<TriggerEvent>? channel,
        Task? worker)
    {
        lock (_gate)
        {
            _running = false;
            _source.Triggered -= OnTriggered;
            _stop = null;
            _channel = null;
            _worker = null;
        }

        try
        {
            await _source.StopAsync(CancellationToken.None);
        }
        catch
        {
            // Best-effort rollback: the original start failure remains the primary error.
        }

        channel?.Writer.TryComplete();
        stop?.Cancel();
        if (worker is not null)
        {
            await Task.WhenAny(worker, Task.Delay(TimeSpan.FromMilliseconds(500), CancellationToken.None));
        }

        MarkPendingDropped();
        stop?.Dispose();
    }
}
