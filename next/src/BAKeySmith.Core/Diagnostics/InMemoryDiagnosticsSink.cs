using System.Collections.Concurrent;

namespace BAKeySmith.Core.Diagnostics;

public sealed class InMemoryDiagnosticsSink : IDiagnosticsSink
{
    private readonly ConcurrentQueue<DiagnosticEvent> _events = new();

    public IReadOnlyList<DiagnosticEvent> Events => _events.ToArray();

    public void Emit(DiagnosticEvent diagnosticEvent)
    {
        _events.Enqueue(diagnosticEvent);
    }

    public int Count(string source, string name)
    {
        return Events.Count(e =>
            string.Equals(e.Source, source, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
