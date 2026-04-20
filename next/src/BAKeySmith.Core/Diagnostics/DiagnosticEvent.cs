using System.Diagnostics;

namespace BAKeySmith.Core.Diagnostics;

public enum DiagnosticLevel
{
    Trace,
    Info,
    Warning,
    Error
}

public sealed record DiagnosticEvent(
    long TimestampTicks,
    string Source,
    string Name,
    DiagnosticLevel Level,
    string? Message,
    IReadOnlyDictionary<string, string> Fields)
{
    private static readonly IReadOnlyDictionary<string, string> EmptyFields =
        new Dictionary<string, string>();

    public double TimestampMilliseconds => TimestampTicks * 1000.0 / Stopwatch.Frequency;

    public static DiagnosticEvent Create(
        string source,
        string name,
        DiagnosticLevel level = DiagnosticLevel.Info,
        string? message = null,
        IReadOnlyDictionary<string, string>? fields = null)
    {
        return new DiagnosticEvent(
            Stopwatch.GetTimestamp(),
            source,
            name,
            level,
            message,
            fields ?? EmptyFields);
    }
}
