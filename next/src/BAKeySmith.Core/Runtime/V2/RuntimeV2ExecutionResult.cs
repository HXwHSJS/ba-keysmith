namespace BAKeySmith.Core.Runtime.V2;

public sealed record RuntimeV2ExecutionResult(
    IReadOnlyList<RuntimeV2Diagnostic> Diagnostics,
    int StepsExecuted = 0)
{
    public bool HasErrors => Diagnostics.Any(diagnostic => diagnostic.IsError);
    public bool Succeeded => !HasErrors;

    public static RuntimeV2ExecutionResult Empty { get; } = new([]);

    public static RuntimeV2ExecutionResult FromDiagnostics(
        IEnumerable<RuntimeV2Diagnostic> diagnostics,
        int stepsExecuted = 0)
    {
        return new RuntimeV2ExecutionResult(diagnostics.ToArray(), stepsExecuted);
    }

    public static RuntimeV2ExecutionResult Combine(params RuntimeV2ExecutionResult[] results)
    {
        return new RuntimeV2ExecutionResult(
            results.SelectMany(result => result.Diagnostics).ToArray(),
            results.Sum(result => result.StepsExecuted));
    }
}
