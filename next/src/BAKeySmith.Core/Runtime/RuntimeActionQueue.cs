using System.Threading.Channels;
using BAKeySmith.Core.Diagnostics;

namespace BAKeySmith.Core.Runtime;

public sealed class RuntimeActionQueue
{
    private readonly Channel<Func<CancellationToken, ValueTask>> _channel;
    private readonly IDiagnosticsSink _diagnostics;
    private readonly string _name;
    private int _pendingCount;
    private int _runningCount;

    public RuntimeActionQueue(
        RuntimeSession session,
        string name,
        IDiagnosticsSink? diagnostics = null)
    {
        _name = name;
        _diagnostics = diagnostics ?? NoOpDiagnosticsSink.Instance;
        _channel = Channel.CreateUnbounded<Func<CancellationToken, ValueTask>>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });

        session.StartWorker($"queue:{name}", RunAsync);
    }

    public int PendingCount => Volatile.Read(ref _pendingCount);
    public int RunningCount => Volatile.Read(ref _runningCount);

    public bool TryEnqueue(Func<CancellationToken, ValueTask> action)
    {
        if (!_channel.Writer.TryWrite(action))
        {
            return false;
        }

        var pending = Interlocked.Increment(ref _pendingCount);
        _diagnostics.Emit(DiagnosticEvent.Create(
            "queue",
            "action_enqueued",
            fields: new Dictionary<string, string>
            {
                ["name"] = _name,
                ["pending"] = pending.ToString()
            }));
        return true;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        await foreach (var action in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            Interlocked.Decrement(ref _pendingCount);
            Interlocked.Increment(ref _runningCount);
            try
            {
                await action(cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref _runningCount);
            }
        }
    }
}
