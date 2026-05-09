namespace BAKeySmith.Core.Runtime.V2.Configuration;

public sealed record RuntimeV2ConfigAdapterResult(
    RuntimeV2MappingRegistry Registry,
    IReadOnlyList<RuntimeV2ConfigAdapterDiagnostic> Diagnostics)
{
    public IReadOnlyList<RuntimeV2MappingEntry> Entries => Registry.Entries;

    public bool HasErrors => Diagnostics.Any(diagnostic =>
        diagnostic.Severity == RuntimeV2ConfigAdapterDiagnosticSeverity.Error);

    public bool BlocksRuntime => Diagnostics.Any(diagnostic =>
        diagnostic.BlocksRuntime ||
        diagnostic.Severity is RuntimeV2ConfigAdapterDiagnosticSeverity.Error
            or RuntimeV2ConfigAdapterDiagnosticSeverity.LiveBlocker);

    public bool CanBuildSandboxEntries => Entries.Count > 0;

    public bool CanBuildCompleteRuntime => !BlocksRuntime;
}
