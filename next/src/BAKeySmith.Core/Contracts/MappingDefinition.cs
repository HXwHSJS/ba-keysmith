namespace BAKeySmith.Core.Contracts;

public sealed record MappingDefinition(string Id, TriggerSpec Trigger, RuntimeAction Action)
{
    public string NormalizedId
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Id))
            {
                throw new ArgumentException("Mapping id must not be empty.", nameof(Id));
            }

            return Id.Trim().ToLowerInvariant();
        }
    }
}
