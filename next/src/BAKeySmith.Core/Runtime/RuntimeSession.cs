using BAKeySmith.Core.Diagnostics;

namespace BAKeySmith.Core.Runtime;

public sealed class RuntimeSession : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly List<Task> _workers = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly PressOwnershipTracker _presses;
    private readonly IDiagnosticsSink _diagnostics;
    private bool _stopping;

    public RuntimeSession(PressOwnershipTracker presses, IDiagnosticsSink? diagnostics = null)
    {
        _presses = presses;
        _diagnostics = diagnostics ?? NoOpDiagnosticsSink.Instance;
    }

    public CancellationToken Token => _stop.Token;
    public bool IsStopping => _stopping;
    public int ActiveWorkerCount
    {
        get
        {
            lock (_gate)
            {
                _workers.RemoveAll(worker => worker.IsCompleted);
                return _workers.Count;
            }
        }
    }

    public void StartWorker(Func<CancellationToken, Task> worker)
    {
        StartWorker("worker", worker);
    }

    public void StartWorker(string name, Func<CancellationToken, Task> worker)
    {
        lock (_gate)
        {
            if (_stopping)
            {
                throw new InvalidOperationException("Cannot start a worker while the session is stopping.");
            }

            _workers.RemoveAll(existingWorker => existingWorker.IsCompleted);
            _workers.Add(Task.Run(async () =>
            {
                _diagnostics.Emit(DiagnosticEvent.Create(
                    "session",
                    "worker_started",
                    fields: new Dictionary<string, string> { ["name"] = name }));
                try
                {
                    await worker(_stop.Token);
                    _diagnostics.Emit(DiagnosticEvent.Create(
                        "session",
                        "worker_completed",
                        fields: new Dictionary<string, string> { ["name"] = name }));
                }
                catch (OperationCanceledException) when (_stop.IsCancellationRequested)
                {
                    _diagnostics.Emit(DiagnosticEvent.Create(
                        "session",
                        "worker_cancelled",
                        fields: new Dictionary<string, string> { ["name"] = name }));
                }
                catch (Exception ex)
                {
                    _diagnostics.Emit(DiagnosticEvent.Create(
                        "session",
                        "worker_failed",
                        DiagnosticLevel.Error,
                        ex.Message,
                        new Dictionary<string, string> { ["name"] = name }));
                }
            }, CancellationToken.None));
        }
    }

    public async Task StopAsync(TimeSpan? workerTimeout = null)
    {
        List<Task> workers;

        lock (_gate)
        {
            if (_stopping)
            {
                return;
            }

            _stopping = true;
            _stop.Cancel();
            workers = _workers.ToList();
        }

        _diagnostics.Emit(DiagnosticEvent.Create(
            "session",
            "stop_requested",
            fields: new Dictionary<string, string>
            {
                ["worker_count"] = workers.Count.ToString()
            }));

        var timeout = workerTimeout ?? TimeSpan.FromMilliseconds(500);
        var allWorkers = Task.WhenAll(workers);
        try
        {
            var completed = await Task.WhenAny(allWorkers, Task.Delay(timeout));
            if (completed == allWorkers)
            {
                try
                {
                    await allWorkers;
                }
                catch
                {
                    // Worker wrappers emit failures; stop should still continue to cleanup.
                }
            }
            else
            {
                _diagnostics.Emit(DiagnosticEvent.Create(
                    "session",
                    "stop_timeout",
                    DiagnosticLevel.Warning,
                    fields: new Dictionary<string, string>
                    {
                        ["timeout_ms"] = timeout.TotalMilliseconds.ToString("0.###")
                    }));
            }
        }
        finally
        {
            await _presses.ReleaseAllAsync(CancellationToken.None);
            _diagnostics.Emit(DiagnosticEvent.Create("session", "stop_completed"));
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _stop.Dispose();
    }
}
