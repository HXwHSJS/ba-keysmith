namespace BAKeySmith.Core.Runtime.V2;

public enum SameMappingActivationModeV2
{
    IgnoreWhenRunning
}

public sealed record MappingActivationPolicyV2(
    SameMappingActivationModeV2 SameMappingMode = SameMappingActivationModeV2.IgnoreWhenRunning)
{
    // This skeleton intentionally supports only ignore-when-running until more modes are designed.
    public static MappingActivationPolicyV2 Default { get; } = new();
}
