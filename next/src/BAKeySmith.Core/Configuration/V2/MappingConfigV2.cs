namespace BAKeySmith.Core.Configuration.V2;

public sealed record MappingConfigV2(
    string Id,
    string Name,
    bool Enabled,
    TriggerConfigV2? Trigger,
    MappingActionSourceV2? ActionSource,
    TimingSettingsV2? TimingOverride = null,
    CoordinateSettingsV2? CoordinateSettingsOverride = null);
