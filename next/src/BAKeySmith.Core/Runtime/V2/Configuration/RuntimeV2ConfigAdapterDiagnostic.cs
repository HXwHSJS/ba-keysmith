using BAKeySmith.Core.Configuration.V2;
using BAKeySmith.Core.Input.V2.Conflicts;

namespace BAKeySmith.Core.Runtime.V2.Configuration;

public enum RuntimeV2ConfigAdapterDiagnosticSeverity
{
    Info,
    Warning,
    Error,
    LiveBlocker
}

public sealed record RuntimeV2ConfigAdapterDiagnostic(
    RuntimeV2ConfigAdapterDiagnosticSeverity Severity,
    RuntimeV2ConfigAdapterDiagnosticCode Code,
    string Message,
    string Path,
    string? MappingId = null,
    bool BlocksRuntime = false,
    AppConfigV2Diagnostic? OriginalAppConfigDiagnostic = null,
    AppConfigV2DiagnosticCode? OriginalAppConfigCode = null,
    InputConflictCode? OriginalConflictCode = null,
    IReadOnlyList<string>? AffectedBindingIds = null,
    IReadOnlyList<string>? AffectedBindingNames = null,
    IReadOnlyList<string>? AffectedPaths = null)
{
    public IReadOnlyList<string> AffectedBindingIds { get; init; } =
        AffectedBindingIds?.ToArray() ?? [];

    public IReadOnlyList<string> AffectedBindingNames { get; init; } =
        AffectedBindingNames?.ToArray() ?? [];

    public IReadOnlyList<string> AffectedPaths { get; init; } =
        AffectedPaths?.ToArray() ?? [];

    public static RuntimeV2ConfigAdapterDiagnostic FromAppConfig(
        AppConfigV2Diagnostic diagnostic,
        string? mappingId = null)
    {
        return new RuntimeV2ConfigAdapterDiagnostic(
            MapSeverity(diagnostic.Severity),
            MapCode(diagnostic.Code),
            diagnostic.Message,
            diagnostic.Path,
            mappingId,
            BlocksRuntime: diagnostic.BlocksLive ||
                diagnostic.Severity is AppConfigV2DiagnosticSeverity.Error or AppConfigV2DiagnosticSeverity.LiveBlocker,
            diagnostic,
            diagnostic.Code,
            diagnostic.OriginalConflictCode,
            diagnostic.AffectedBindingIds,
            diagnostic.AffectedBindingNames,
            diagnostic.AffectedPaths);
    }

    private static RuntimeV2ConfigAdapterDiagnosticSeverity MapSeverity(
        AppConfigV2DiagnosticSeverity severity)
    {
        return severity switch
        {
            AppConfigV2DiagnosticSeverity.Info => RuntimeV2ConfigAdapterDiagnosticSeverity.Info,
            AppConfigV2DiagnosticSeverity.Warning => RuntimeV2ConfigAdapterDiagnosticSeverity.Warning,
            AppConfigV2DiagnosticSeverity.LiveBlocker => RuntimeV2ConfigAdapterDiagnosticSeverity.LiveBlocker,
            _ => RuntimeV2ConfigAdapterDiagnosticSeverity.Error
        };
    }

    private static RuntimeV2ConfigAdapterDiagnosticCode MapCode(
        AppConfigV2DiagnosticCode code)
    {
        return code switch
        {
            AppConfigV2DiagnosticCode.InvalidActionSource or
            AppConfigV2DiagnosticCode.InvalidActionSourceKind => RuntimeV2ConfigAdapterDiagnosticCode.InvalidActionSource,
            AppConfigV2DiagnosticCode.InvalidSimpleMappingSource => RuntimeV2ConfigAdapterDiagnosticCode.InvalidSimpleMappingSource,
            AppConfigV2DiagnosticCode.MacroCompilerMissing => RuntimeV2ConfigAdapterDiagnosticCode.MacroCompilerMissing,
            _ => RuntimeV2ConfigAdapterDiagnosticCode.ConfigValidationDiagnostic
        };
    }
}
