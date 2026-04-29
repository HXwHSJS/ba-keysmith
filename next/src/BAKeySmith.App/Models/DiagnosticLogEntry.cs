namespace BAKeySmith.App.Models;

public sealed record DiagnosticLogEntry(
    DateTime Timestamp,
    string Name,
    string Message)
{
    public string Display => $"[{Timestamp:HH:mm:ss.fff}] {Name} {Message}";
}
