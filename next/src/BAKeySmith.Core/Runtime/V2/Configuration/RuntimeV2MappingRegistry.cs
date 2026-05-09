namespace BAKeySmith.Core.Runtime.V2.Configuration;

public sealed class RuntimeV2MappingRegistry
{
    private readonly Dictionary<string, RuntimeV2MappingEntry> _byMappingId;

    public RuntimeV2MappingRegistry(IEnumerable<RuntimeV2MappingEntry> entries)
    {
        Entries = entries.ToArray();
        _byMappingId = Entries
            .GroupBy(entry => entry.MappingId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.Ordinal);
    }

    public IReadOnlyList<RuntimeV2MappingEntry> Entries { get; }
    public int Count => Entries.Count;

    public bool TryGet(string mappingId, out RuntimeV2MappingEntry? entry)
    {
        return _byMappingId.TryGetValue(mappingId, out entry);
    }
}
