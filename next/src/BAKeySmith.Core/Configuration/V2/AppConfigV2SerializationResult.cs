namespace BAKeySmith.Core.Configuration.V2;

public sealed record AppConfigV2SerializationResult(
    AppConfigV2? Config,
    IReadOnlyList<AppConfigV2Diagnostic> Diagnostics)
{
    public IReadOnlyList<AppConfigV2Diagnostic> Diagnostics { get; init; } =
        Diagnostics.ToArray();

    public bool Success =>
        Config is not null &&
        !HasErrors;

    public bool HasErrors =>
        Diagnostics.Any(diagnostic => diagnostic.Severity == AppConfigV2DiagnosticSeverity.Error);

    public bool BlocksSave =>
        Diagnostics.Any(diagnostic =>
            diagnostic.BlocksSave ||
            diagnostic.Severity == AppConfigV2DiagnosticSeverity.Error);

    public bool BlocksLive =>
        Diagnostics.Any(diagnostic =>
            diagnostic.BlocksLive ||
            diagnostic.Severity is AppConfigV2DiagnosticSeverity.Error or AppConfigV2DiagnosticSeverity.LiveBlocker);

    public bool CanSaveDraft =>
        Config is not null &&
        !BlocksSave;
}
