namespace BAKeySmith.Core.Runtime.V2;

public sealed record RuntimeV2ExecutionOptions
{
    public int MaxActionSteps { get; init; } = 1_000;
    public int MaxWhileHeldIterations { get; init; } = 100;
    public CancellationToken CancellationToken { get; init; } = CancellationToken.None;

    public static RuntimeV2ExecutionOptions Default { get; } = new();

    public IReadOnlyList<RuntimeV2Diagnostic> Validate()
    {
        var diagnostics = new List<RuntimeV2Diagnostic>();
        if (MaxActionSteps <= 0)
        {
            diagnostics.Add(new RuntimeV2Diagnostic(
                RuntimeV2DiagnosticSeverity.Error,
                RuntimeV2DiagnosticCode.InvalidMaxActionSteps,
                "Runtime v2 sandbox MaxActionSteps must be greater than zero.",
                "options.max_action_steps"));
        }

        if (MaxWhileHeldIterations < 0)
        {
            diagnostics.Add(new RuntimeV2Diagnostic(
                RuntimeV2DiagnosticSeverity.Error,
                RuntimeV2DiagnosticCode.InvalidMaxWhileHeldIterations,
                "Runtime v2 sandbox MaxWhileHeldIterations must not be negative.",
                "options.max_while_held_iterations"));
        }

        return diagnostics;
    }

    public bool IsValid => !Validate().Any(diagnostic => diagnostic.IsError);
}
