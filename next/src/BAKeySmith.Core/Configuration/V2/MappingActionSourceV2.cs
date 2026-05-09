using BAKeySmith.Core.Actions.V2;
using BAKeySmith.Core.Mappings.V2;

namespace BAKeySmith.Core.Configuration.V2;

public enum MappingActionSourceKindV2
{
    Simple,
    MacroDslV2
}

public abstract record MappingActionSourceV2(MappingActionSourceKindV2 Kind)
{
    public sealed record SimpleMapping(SimpleMappingDefinitionV2 Definition) :
        MappingActionSourceV2(MappingActionSourceKindV2.Simple);

    public sealed record MacroDslV2Source(string SourceText) :
        MappingActionSourceV2(MappingActionSourceKindV2.MacroDslV2);
}

public sealed record MappingActionPlanBuildResultV2(
    ActivationPlanV2? Plan,
    IReadOnlyList<AppConfigV2Diagnostic> Diagnostics)
{
    public bool Success =>
        Plan is not null &&
        !Diagnostics.Any(diagnostic => diagnostic.Severity == AppConfigV2DiagnosticSeverity.Error);
}

public sealed class MappingActionSourcePlanBuilderV2
{
    private readonly SimpleMappingPlanBuilderV2 _simpleBuilder = new();

    public MappingActionPlanBuildResultV2 Build(
        MappingActionSourceV2 actionSource,
        string path = "action")
    {
        switch (actionSource)
        {
            case MappingActionSourceV2.SimpleMapping simple when simple.Kind != MappingActionSourceKindV2.Simple:
                return InvalidKind(
                    actionSource.Kind,
                    MappingActionSourceKindV2.Simple,
                    $"{path}.kind");
            case MappingActionSourceV2.SimpleMapping simple:
                return BuildSimple(simple, path);
            case MappingActionSourceV2.MacroDslV2Source macro when macro.Kind != MappingActionSourceKindV2.MacroDslV2:
                return InvalidKind(
                    macro.Kind,
                    MappingActionSourceKindV2.MacroDslV2,
                    $"{path}.kind");
            case MappingActionSourceV2.MacroDslV2Source:
                return new MappingActionPlanBuildResultV2(
                    null,
                    [
                        Error(
                            AppConfigV2DiagnosticCode.MacroCompilerMissing,
                            "Macro DSL v2 source shell is stored but not compiled by this skeleton.",
                            $"{path}.macro_dsl_v2")
                    ]);
            default:
                return new MappingActionPlanBuildResultV2(
                    null,
                    [
                        Error(
                            AppConfigV2DiagnosticCode.InvalidActionSource,
                            $"Unsupported action source type {actionSource.GetType().Name}.",
                            path)
                    ]);
        }
    }

    private static MappingActionPlanBuildResultV2 InvalidKind(
        MappingActionSourceKindV2 actual,
        MappingActionSourceKindV2 expected,
        string path)
    {
        return new MappingActionPlanBuildResultV2(
            null,
            [
                Error(
                    AppConfigV2DiagnosticCode.InvalidActionSourceKind,
                    $"Action source kind {actual} does not match expected subtype kind {expected}.",
                    path)
            ]);
    }

    private MappingActionPlanBuildResultV2 BuildSimple(
        MappingActionSourceV2.SimpleMapping simple,
        string path)
    {
        var result = _simpleBuilder.Build(simple.Definition);
        var diagnostics = result.Diagnostics
            .Where(diagnostic => diagnostic.Severity == SimpleMappingValidationSeverityV2.Error)
            .Select(diagnostic => Error(
                AppConfigV2DiagnosticCode.InvalidSimpleMappingSource,
                $"{diagnostic.Code}: {diagnostic.Message}",
                $"{path}.{diagnostic.Path}"))
            .ToArray();

        return new MappingActionPlanBuildResultV2(
            diagnostics.Length == 0 ? result.Plan : null,
            diagnostics);
    }

    private static AppConfigV2Diagnostic Error(
        AppConfigV2DiagnosticCode code,
        string message,
        string path)
    {
        return new AppConfigV2Diagnostic(
            AppConfigV2DiagnosticSeverity.Error,
            code,
            message,
            path);
    }
}
