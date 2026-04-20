namespace BAKeySmith.Core.Diagnostics;

public sealed class EventDiagnosticsSink : IDiagnosticsSink
{
    public event EventHandler<DiagnosticEvent>? Emitted;

    public void Emit(DiagnosticEvent diagnosticEvent)
    {
        Emitted?.Invoke(this, diagnosticEvent);
    }
}
