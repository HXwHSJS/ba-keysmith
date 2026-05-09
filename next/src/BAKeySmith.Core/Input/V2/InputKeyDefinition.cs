namespace BAKeySmith.Core.Input.V2;

public sealed record InputKeyDefinition(
    string CanonicalName,
    InputKind Kind,
    InputCapabilities Capabilities,
    string DisplayName,
    string ChineseDisplayName,
    IReadOnlyList<string> Aliases)
{
    public InputSpec ToSpec() => new(
        CanonicalName,
        Kind,
        Capabilities,
        DisplayName,
        ChineseDisplayName);
}
