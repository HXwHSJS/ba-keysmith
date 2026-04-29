using BAKeySmith.Core.Diagnostics;

internal sealed class CountingDiagnosticsSink : IDiagnosticsSink
{
    private const int MaxRecentEvents = 4096;
    private readonly object _gate = new();
    private readonly Queue<DiagnosticEvent> _events = new();
    private long _count;

    public long Count => Interlocked.Read(ref _count);
    public IReadOnlyList<DiagnosticEvent> Events
    {
        get
        {
            lock (_gate)
            {
                return _events.ToArray();
            }
        }
    }

    public void Emit(DiagnosticEvent diagnosticEvent)
    {
        lock (_gate)
        {
            _events.Enqueue(diagnosticEvent);
            while (_events.Count > MaxRecentEvents)
            {
                _events.Dequeue();
            }
        }

        Interlocked.Increment(ref _count);
    }

    public int CountWhere(Func<DiagnosticEvent, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return Events.Count(predicate);
    }

    public void Reset()
    {
        lock (_gate)
        {
            _events.Clear();
        }

        Interlocked.Exchange(ref _count, 0);
    }
}
