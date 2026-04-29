using BAKeySmith.Core.Configuration;

namespace BAKeySmith.App.Models;

public sealed class MappingEditorRow
{
    public string? Id { get; init; }
    public string Trigger { get; init; } = string.Empty;
    public string Type { get; init; } = "simple";
    public string Target { get; init; } = string.Empty;
    public string Mode { get; init; } = "hold";
    public string Script { get; init; } = string.Empty;
    public Dictionary<string, object>? ExtraFields { get; init; }

    public string ScriptSummary
    {
        get
        {
            if (!string.Equals(Type, "macro", StringComparison.OrdinalIgnoreCase))
            {
                return "-";
            }

            var compact = Script.Replace("\r", " ").Replace("\n", " ").Trim();
            return compact.Length <= 42 ? compact : compact[..42] + "...";
        }
    }

    public static MappingEditorRow FromConfig(MappingConfigV1 mapping)
    {
        return new MappingEditorRow
        {
            Id = mapping.Id,
            Trigger = mapping.Trigger,
            Type = string.IsNullOrWhiteSpace(mapping.Type) ? "simple" : mapping.Type,
            Target = mapping.Target ?? string.Empty,
            Mode = mapping.Mode ?? string.Empty,
            Script = mapping.Script ?? string.Empty,
            ExtraFields = mapping.ExtraFields
        };
    }

    public MappingEditorRow PreserveIdentityFrom(MappingEditorRow previous)
    {
        return new MappingEditorRow
        {
            Id = string.IsNullOrWhiteSpace(Id) ? previous.Id : Id,
            Trigger = Trigger,
            Type = Type,
            Target = Target,
            Mode = Mode,
            Script = Script,
            ExtraFields = ExtraFields ?? previous.ExtraFields
        };
    }

    public MappingConfigV1 ToConfig()
    {
        return new MappingConfigV1
        {
            Id = Id,
            Trigger = Trigger,
            Type = Type,
            Target = string.Equals(Type, "simple", StringComparison.OrdinalIgnoreCase) ? Target : null,
            Mode = string.Equals(Type, "simple", StringComparison.OrdinalIgnoreCase) ? Mode : null,
            Script = string.Equals(Type, "macro", StringComparison.OrdinalIgnoreCase) ? Script : null,
            ExtraFields = ExtraFields
        };
    }
}
