namespace BAKeySmith.Core.Diagnostics;

public interface IDiagnosticsSink
{
    void Emit(DiagnosticEvent diagnosticEvent);
}
