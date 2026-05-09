namespace BAKeySmith.Core.Configuration.V2;

public sealed record TimingSettingsV2(
    TimeSpan? TapKeyDuration = null,
    TimeSpan? TapMouseDuration = null,
    TimeSpan? CoordinateTapDuration = null,
    TimeSpan? DefaultWhileHeldInterval = null);
