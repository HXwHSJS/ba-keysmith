using System.Diagnostics;

namespace BAKeySmith.Core.Contracts;

public sealed record TriggerEvent(TriggerSpec Trigger, string Phase, long TimestampTicks)
{
    public string NormalizedPhase => Normalize(Phase);

    public static TriggerEvent Down(TriggerSpec trigger) =>
        new(trigger, "down", Stopwatch.GetTimestamp());

    public static TriggerEvent Up(TriggerSpec trigger) =>
        new(trigger, "up", Stopwatch.GetTimestamp());

    public bool IsDown => string.Equals(NormalizedPhase, "down", StringComparison.OrdinalIgnoreCase);
    public bool IsUp => string.Equals(NormalizedPhase, "up", StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Trigger phase must not be empty.", nameof(value));
        }

        return value.Trim().ToLowerInvariant();
    }
}
