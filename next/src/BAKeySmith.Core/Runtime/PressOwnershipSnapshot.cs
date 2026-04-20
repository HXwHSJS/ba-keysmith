namespace BAKeySmith.Core.Runtime;

public sealed record PressOwnershipSnapshot(
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> KeyOwners,
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> OwnerKeys)
{
    public bool IsEmpty => KeyOwners.Count == 0 && OwnerKeys.Count == 0;
}
