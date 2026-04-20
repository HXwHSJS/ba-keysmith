using System.Text.Json.Serialization;

namespace BAKeySmith.Core.Configuration;

public sealed record AppConfigV1
{
    [JsonPropertyName("version")]
    public int Version { get; init; } = 1;

    [JsonPropertyName("target_process")]
    public string TargetProcess { get; init; } = "BlueArchive.exe";

    [JsonPropertyName("hotkey")]
    public string Hotkey { get; init; } = "ctrl+shift+f12";

    [JsonPropertyName("tap_hold_ms")]
    public double TapHoldMilliseconds { get; init; } = 20;

    [JsonPropertyName("mappings")]
    public IReadOnlyList<MappingConfigV1> Mappings { get; init; } = Array.Empty<MappingConfigV1>();

    [JsonExtensionData]
    public Dictionary<string, object>? ExtraFields { get; init; }
}

public sealed record MappingConfigV1
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("trigger")]
    public string Trigger { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = "simple";

    [JsonPropertyName("target")]
    public string? Target { get; init; }

    [JsonPropertyName("mode")]
    public string? Mode { get; init; }

    [JsonPropertyName("script")]
    public string? Script { get; init; }

    [JsonExtensionData]
    public Dictionary<string, object>? ExtraFields { get; init; }
}
