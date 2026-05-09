using BAKeySmith.Core.Input.V2.Conflicts;

namespace BAKeySmith.Core.Configuration.V2;

public enum AppConfigV2DiagnosticSeverity
{
    Info,
    Warning,
    Error,
    LiveBlocker
}

public enum AppConfigV2DiagnosticCode
{
    InvalidJson,
    InvalidInputName,
    InvalidHotkey,
    InvalidVersion,
    MissingMappingId,
    DuplicateMappingId,
    MissingTrigger,
    InvalidTrigger,
    InvalidActionSource,
    InvalidActionSourceKind,
    InvalidSimpleMappingSource,
    EmptyMacroSource,
    MacroCompilerMissing,
    InputConflict,
    InvalidCoordinateSettings,
    InvalidTiming,
    DraftFileMissing,
    DraftDocumentIoError
}

public sealed record AppConfigV2Diagnostic(
    AppConfigV2DiagnosticSeverity Severity,
    AppConfigV2DiagnosticCode Code,
    string Message,
    string Path,
    InputConflictCode? OriginalConflictCode = null,
    IReadOnlyList<string>? AffectedBindingIds = null,
    IReadOnlyList<string>? AffectedBindingNames = null,
    IReadOnlyList<string>? AffectedPaths = null,
    bool BlocksSave = false,
    bool BlocksLive = false)
{
    public IReadOnlyList<string> AffectedBindingIds { get; init; } =
        AffectedBindingIds?.ToArray() ?? [];

    public IReadOnlyList<string> AffectedBindingNames { get; init; } =
        AffectedBindingNames?.ToArray() ?? [];

    public IReadOnlyList<string> AffectedPaths { get; init; } =
        AffectedPaths?.ToArray() ?? [];
}
