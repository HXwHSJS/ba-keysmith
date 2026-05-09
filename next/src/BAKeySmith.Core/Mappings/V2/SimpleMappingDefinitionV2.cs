namespace BAKeySmith.Core.Mappings.V2;

public sealed record SimpleMappingDefinitionV2(
    string Id,
    SimpleMappingModeV2 Mode,
    SimpleMappingTargetV2 Target,
    TimeSpan? Duration = null);
