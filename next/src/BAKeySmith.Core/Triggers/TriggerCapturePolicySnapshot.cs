namespace BAKeySmith.Core.Triggers;

public sealed record TriggerCapturePolicySnapshot(
    bool IsEnabled,
    string TargetProcess,
    IReadOnlySet<string> TriggerKeys)
{
    public static TriggerCapturePolicySnapshot Disabled { get; } =
        new(false, string.Empty, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    public bool Hits(string triggerKey)
    {
        return TriggerKeys.Contains(triggerKey);
    }

    public static TriggerCapturePolicySnapshot FromRuntimeConfig(
        string targetProcess,
        IEnumerable<string> triggerKeys,
        bool isEnabled)
    {
        return new TriggerCapturePolicySnapshot(
            isEnabled,
            targetProcess,
            triggerKeys.ToHashSet(StringComparer.OrdinalIgnoreCase));
    }
}
