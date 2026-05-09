namespace BAKeySmith.Core.Mappings.V2;

public enum SimpleMappingValidationSeverityV2
{
    Info,
    Warning,
    Error
}

public enum SimpleMappingValidationCodeV2
{
    InvalidTargetKind,
    InvalidTargetPayload,
    MissingInputSpec,
    UnsupportedTarget,
    UnsupportedMode,
    InvalidDuration,
    GeneratedPlanInvalid
}

public sealed record SimpleMappingValidationDiagnosticV2(
    SimpleMappingValidationSeverityV2 Severity,
    SimpleMappingValidationCodeV2 Code,
    string Message,
    string Path);
