using BAKeySmith.Core.Diagnostics;

internal sealed class CountingDiagnosticsSink : IDiagnosticsSink
{
    private long _count;

    public long Count => Interlocked.Read(ref _count);

    public void Emit(DiagnosticEvent diagnosticEvent)
    {
        _ = diagnosticEvent;
        Interlocked.Increment(ref _count);
    }
}
