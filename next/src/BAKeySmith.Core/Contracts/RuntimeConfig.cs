namespace BAKeySmith.Core.Contracts;

public sealed record RuntimeConfig(
    string TargetProcess,
    double TapHoldMilliseconds,
    IReadOnlyList<MappingDefinition> Mappings)
{
    public static RuntimeConfig Empty { get; } = new(
        "BlueArchive.exe",
        20,
        Array.Empty<MappingDefinition>());

    public TimeSpan TapHold => TimeSpan.FromMilliseconds(Math.Max(0, TapHoldMilliseconds));
}
