namespace BAKeySmith.Core.Input.V2;

public static class InputOverlap
{
    private static readonly Dictionary<string, IReadOnlySet<string>> GenericModifierOverlaps =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ctrl"] = new HashSet<string>(["left_ctrl", "right_ctrl"], StringComparer.OrdinalIgnoreCase),
            ["alt"] = new HashSet<string>(["left_alt", "right_alt"], StringComparer.OrdinalIgnoreCase),
            ["shift"] = new HashSet<string>(["left_shift", "right_shift"], StringComparer.OrdinalIgnoreCase),
            ["win"] = new HashSet<string>(["left_win", "right_win"], StringComparer.OrdinalIgnoreCase)
        };

    public static bool Overlaps(string left, string right)
    {
        var leftSpec = InputNameResolverV2.Resolve(left);
        var rightSpec = InputNameResolverV2.Resolve(right);
        return Overlaps(leftSpec, rightSpec);
    }

    public static bool Overlaps(InputSpec left, InputSpec right)
    {
        if (string.Equals(left.CanonicalName, right.CanonicalName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return GenericOverlapsSpecific(left.CanonicalName, right.CanonicalName) ||
            GenericOverlapsSpecific(right.CanonicalName, left.CanonicalName);
    }

    private static bool GenericOverlapsSpecific(string generic, string specific)
    {
        return GenericModifierOverlaps.TryGetValue(generic, out var specifics) &&
            specifics.Contains(specific);
    }
}
