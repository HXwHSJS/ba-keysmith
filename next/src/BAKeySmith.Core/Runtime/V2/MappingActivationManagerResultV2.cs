namespace BAKeySmith.Core.Runtime.V2;

public sealed record MappingActivationManagerResultV2(
    IReadOnlyList<RuntimeV2Diagnostic> Diagnostics,
    string? MappingId = null,
    IReadOnlyList<string>? ActiveMappingIds = null,
    int EventsBefore = 0,
    int EventsAfter = 0)
{
    public bool HasErrors => Diagnostics.Any(diagnostic => diagnostic.IsError);
    public bool Succeeded => !HasErrors;
    public int EventsRecorded => EventsAfter - EventsBefore;

    public static MappingActivationManagerResultV2 FromExecution(
        RuntimeV2ExecutionResult result,
        string mappingId,
        IReadOnlyList<string> activeMappingIds,
        int eventsBefore,
        int eventsAfter)
    {
        return new MappingActivationManagerResultV2(
            StampMappingId(result.Diagnostics, mappingId),
            mappingId,
            activeMappingIds,
            eventsBefore,
            eventsAfter);
    }

    public static MappingActivationManagerResultV2 FromDiagnostics(
        IEnumerable<RuntimeV2Diagnostic> diagnostics,
        string? mappingId,
        IReadOnlyList<string> activeMappingIds,
        int eventCount)
    {
        return new MappingActivationManagerResultV2(
            StampMappingId(diagnostics, mappingId),
            mappingId,
            activeMappingIds,
            eventCount,
            eventCount);
    }

    public static IReadOnlyList<RuntimeV2Diagnostic> StampMappingId(
        IEnumerable<RuntimeV2Diagnostic> diagnostics,
        string? mappingId)
    {
        return diagnostics
            .Select(diagnostic => mappingId is null || diagnostic.MappingId is not null
                ? diagnostic
                : diagnostic with { MappingId = mappingId })
            .ToArray();
    }
}
