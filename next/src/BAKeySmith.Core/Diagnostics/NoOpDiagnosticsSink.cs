namespace BAKeySmith.Core.Diagnostics;

public sealed class NoOpDiagnosticsSink : IDiagnosticsSink
{
    public static readonly NoOpDiagnosticsSink Instance = new();

    private NoOpDiagnosticsSink()
    {
    }

    public void Emit(DiagnosticEvent diagnosticEvent)
    {
    }
}
