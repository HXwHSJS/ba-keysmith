using System.Text.Json;
using System.Text.Json.Nodes;
using BAKeySmith.Core.Actions.V2;
using BAKeySmith.Core.Input.V2;
using BAKeySmith.Core.Input.V2.Conflicts;
using BAKeySmith.Core.Mappings.V2;

namespace BAKeySmith.Core.Configuration.V2;

public sealed class AppConfigV2JsonSerializer
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };

    private readonly AppConfigV2Validator _validator = new();

    public AppConfigV2SerializationResult Parse(string json)
    {
        var diagnostics = new List<AppConfigV2Diagnostic>();
        JsonNode? rootNode;
        try
        {
            rootNode = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            return Failure(
                Error(
                    AppConfigV2DiagnosticCode.InvalidJson,
                    $"AppConfigV2 JSON could not be parsed: {ex.Message}",
                    "$"));
        }

        if (rootNode is not JsonObject root)
        {
            return Failure(Error(
                AppConfigV2DiagnosticCode.InvalidJson,
                "AppConfigV2 JSON root must be an object.",
                "$"));
        }

        var config = ParseRoot(root, diagnostics);
        diagnostics.AddRange(_validator.Validate(config).Select(ProjectValidationDiagnosticForJson));
        return new AppConfigV2SerializationResult(config, diagnostics);
    }

    public string Serialize(AppConfigV2 config)
    {
        var root = new JsonObject
        {
            ["version"] = config.Version
        };

        if (config.ControlHotkey is not null)
        {
            root["control_hotkey"] = config.ControlHotkey.CanonicalText;
        }

        if (config.CoordinateRecordHotkey is not null)
        {
            root["coordinate_record_hotkey"] = config.CoordinateRecordHotkey.CanonicalText;
        }

        if (config.EmergencyStopHotkey is not null)
        {
            root["emergency_stop_hotkey"] = config.EmergencyStopHotkey.CanonicalText;
        }

        var defaults = WriteDefaults(config.DefaultTiming, config.DefaultCoordinateSettings);
        if (defaults.Count > 0)
        {
            root["defaults"] = defaults;
        }

        var mappings = new JsonArray();
        foreach (var mapping in config.Mappings)
        {
            mappings.Add(WriteMapping(mapping));
        }

        root["mappings"] = mappings;
        return root.ToJsonString(WriteOptions);
    }

    private static AppConfigV2 ParseRoot(
        JsonObject root,
        List<AppConfigV2Diagnostic> diagnostics)
    {
        // Draft behavior: unknown JSON fields are intentionally ignored on
        // parse and not emitted on serialize. Production AppConfigV2 migration
        // needs a separate unknown-field preservation policy before this can be
        // treated as a stable document serializer.
        var defaults = ParseDefaults(root["defaults"], diagnostics);
        var version = ReadInt(root["version"], "version", diagnostics) ?? 0;
        var mappings = ParseMappings(root["mappings"], diagnostics);
        return new AppConfigV2(
            version,
            mappings,
            ParseHotkey(root["control_hotkey"], "control_hotkey", diagnostics),
            ParseCoordinateRecordHotkey(root["coordinate_record_hotkey"], "coordinate_record_hotkey", diagnostics),
            ParseHotkey(root["emergency_stop_hotkey"], "emergency_stop_hotkey", diagnostics),
            ParseDefaultTiming(defaults, diagnostics),
            ParseDefaultCoordinate(defaults, diagnostics));
    }

    private static JsonObject? ParseDefaults(
        JsonNode? defaultsNode,
        List<AppConfigV2Diagnostic> diagnostics)
    {
        if (defaultsNode is null)
        {
            return null;
        }

        if (defaultsNode is JsonObject defaults)
        {
            return defaults;
        }

        diagnostics.Add(Error(
            AppConfigV2DiagnosticCode.InvalidJson,
            "defaults must be an object.",
            "defaults"));
        return null;
    }

    private static IReadOnlyList<MappingConfigV2> ParseMappings(
        JsonNode? mappingsNode,
        List<AppConfigV2Diagnostic> diagnostics)
    {
        if (mappingsNode is null)
        {
            return [];
        }

        if (mappingsNode is not JsonArray mappingsArray)
        {
            diagnostics.Add(Error(
                AppConfigV2DiagnosticCode.InvalidJson,
                "mappings must be an array.",
                "mappings"));
            return [];
        }

        var mappings = new List<MappingConfigV2>();
        for (var index = 0; index < mappingsArray.Count; index++)
        {
            var path = $"mappings[{index}]";
            if (mappingsArray[index] is not JsonObject mappingObject)
            {
                diagnostics.Add(Error(
                    AppConfigV2DiagnosticCode.InvalidJson,
                    "mapping entry must be an object.",
                    path));
                continue;
            }

            mappings.Add(ParseMapping(mappingObject, path, diagnostics));
        }

        return mappings;
    }

    private static MappingConfigV2 ParseMapping(
        JsonObject mapping,
        string path,
        List<AppConfigV2Diagnostic> diagnostics)
    {
        var id = ReadString(mapping["id"], $"{path}.id", diagnostics) ?? string.Empty;
        var name = ReadString(mapping["name"], $"{path}.name", diagnostics) ?? id;
        var enabled = ReadBool(mapping["enabled"], $"{path}.enabled", diagnostics) ?? true;

        return new MappingConfigV2(
            id,
            name,
            enabled,
            ParseTrigger(mapping["trigger"], $"{path}.trigger", diagnostics),
            ParseActionSource(mapping["action"], $"{path}.action", id, diagnostics),
            ParseTiming(mapping["timing"], $"{path}.timing", diagnostics),
            ParseCoordinateSettings(mapping["coordinate"], $"{path}.coordinate", diagnostics));
    }

    private static TriggerConfigV2? ParseTrigger(
        JsonNode? triggerNode,
        string path,
        List<AppConfigV2Diagnostic> diagnostics)
    {
        if (triggerNode is null)
        {
            return null;
        }

        if (triggerNode is not JsonObject triggerObject)
        {
            diagnostics.Add(Error(
                AppConfigV2DiagnosticCode.InvalidTrigger,
                "trigger must be an object.",
                path));
            return null;
        }

        var kind = ReadString(triggerObject["kind"], $"{path}.kind", diagnostics);
        if (!string.Equals(kind, "single_input", StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(Error(
                AppConfigV2DiagnosticCode.InvalidTrigger,
                "Only single_input trigger kind is supported by the AppConfigV2 JSON draft.",
                $"{path}.kind"));
            return null;
        }

        var input = ParseInput(triggerObject["input"], $"{path}.input", diagnostics);
        return new TriggerConfigV2(TriggerKindV2.SingleInput, input);
    }

    private static MappingActionSourceV2? ParseActionSource(
        JsonNode? actionNode,
        string path,
        string mappingId,
        List<AppConfigV2Diagnostic> diagnostics)
    {
        if (actionNode is null)
        {
            return null;
        }

        if (actionNode is not JsonObject actionObject)
        {
            diagnostics.Add(Error(
                AppConfigV2DiagnosticCode.InvalidActionSource,
                "action must be an object.",
                path));
            return null;
        }

        var kind = ReadString(actionObject["kind"], $"{path}.kind", diagnostics);
        return NormalizeToken(kind) switch
        {
            "simple" => ParseSimpleActionSource(actionObject, path, mappingId, diagnostics),
            "macro_dsl_v2" => new MappingActionSourceV2.MacroDslV2Source(
                ReadString(actionObject["source"], $"{path}.macro_dsl_v2.source", diagnostics) ?? string.Empty),
            _ => UnknownActionSource(kind, path, diagnostics)
        };
    }

    private static MappingActionSourceV2? UnknownActionSource(
        string? kind,
        string path,
        List<AppConfigV2Diagnostic> diagnostics)
    {
        diagnostics.Add(Error(
            AppConfigV2DiagnosticCode.InvalidActionSource,
            $"Unsupported action source kind '{kind ?? "<missing>"}'.",
            $"{path}.kind"));
        return null;
    }

    private static MappingActionSourceV2.SimpleMapping ParseSimpleActionSource(
        JsonObject actionObject,
        string path,
        string mappingId,
        List<AppConfigV2Diagnostic> diagnostics)
    {
        var modeText = ReadString(actionObject["mode"], $"{path}.mode", diagnostics);
        var mode = NormalizeToken(modeText) switch
        {
            "tap" => SimpleMappingModeV2.Tap,
            "hold" => SimpleMappingModeV2.Hold,
            _ => InvalidSimpleMode(modeText, $"{path}.mode", diagnostics)
        };

        var duration = ParseDuration(actionObject["duration"], $"{path}.duration", diagnostics);
        var target = ParseSimpleTarget(actionObject["target"], $"{path}.target", diagnostics);
        return new MappingActionSourceV2.SimpleMapping(
            new SimpleMappingDefinitionV2(
                string.IsNullOrWhiteSpace(mappingId) ? "json-simple-mapping" : mappingId,
                mode,
                target,
                duration));
    }

    private static SimpleMappingModeV2 InvalidSimpleMode(
        string? mode,
        string path,
        List<AppConfigV2Diagnostic> diagnostics)
    {
        diagnostics.Add(Error(
            AppConfigV2DiagnosticCode.InvalidSimpleMappingSource,
            $"Unsupported simple mapping mode '{mode ?? "<missing>"}'.",
            path));
        return (SimpleMappingModeV2)999;
    }

    private static SimpleMappingTargetV2 ParseSimpleTarget(
        JsonNode? targetNode,
        string path,
        List<AppConfigV2Diagnostic> diagnostics)
    {
        if (targetNode is not JsonObject targetObject)
        {
            diagnostics.Add(Error(
                AppConfigV2DiagnosticCode.InvalidSimpleMappingSource,
                "simple mapping target must be an object.",
                path));
            return new SimpleMappingTargetV2((SimpleMappingTargetKindV2)999);
        }

        var kind = ReadString(targetObject["kind"], $"{path}.kind", diagnostics);
        return NormalizeToken(kind) switch
        {
            "input" => new SimpleMappingTargetV2(
                SimpleMappingTargetKindV2.Input,
                Input: ParseInput(targetObject["input"], $"{path}.input", diagnostics)),
            "coordinate" => ParseCoordinateTarget(targetObject, path, diagnostics),
            _ => InvalidSimpleTarget(kind, path, diagnostics)
        };
    }

    private static SimpleMappingTargetV2 ParseCoordinateTarget(
        JsonObject targetObject,
        string path,
        List<AppConfigV2Diagnostic> diagnostics)
    {
        var x = ReadInt(targetObject["x"], $"{path}.x", diagnostics) ?? 0;
        var y = ReadInt(targetObject["y"], $"{path}.y", diagnostics) ?? 0;
        var profile = ReadString(targetObject["profile"], $"{path}.profile", diagnostics) ?? string.Empty;
        return SimpleMappingTargetV2.FromCoordinate(new CoordinatePointV2(x, y, profile));
    }

    private static SimpleMappingTargetV2 InvalidSimpleTarget(
        string? kind,
        string path,
        List<AppConfigV2Diagnostic> diagnostics)
    {
        diagnostics.Add(Error(
            AppConfigV2DiagnosticCode.InvalidSimpleMappingSource,
            $"Unsupported simple mapping target kind '{kind ?? "<missing>"}'.",
            $"{path}.kind"));
        return new SimpleMappingTargetV2((SimpleMappingTargetKindV2)999);
    }

    private static TimingSettingsV2? ParseDefaultTiming(
        JsonObject? defaults,
        List<AppConfigV2Diagnostic> diagnostics)
    {
        if (defaults is null ||
            defaults["timing"] is null)
        {
            return null;
        }

        return ParseTiming(defaults["timing"], "defaults.timing", diagnostics);
    }

    private static CoordinateSettingsV2? ParseDefaultCoordinate(
        JsonObject? defaults,
        List<AppConfigV2Diagnostic> diagnostics)
    {
        if (defaults is null ||
            defaults["coordinate"] is null)
        {
            return null;
        }

        return ParseCoordinateSettings(defaults["coordinate"], "defaults.coordinate", diagnostics);
    }

    private static TimingSettingsV2? ParseTiming(
        JsonNode? timingNode,
        string path,
        List<AppConfigV2Diagnostic> diagnostics)
    {
        if (timingNode is null)
        {
            return null;
        }

        if (timingNode is not JsonObject timing)
        {
            diagnostics.Add(Error(
                AppConfigV2DiagnosticCode.InvalidTiming,
                "timing must be an object.",
                path));
            return null;
        }

        return new TimingSettingsV2(
            ParseDuration(timing["tap_key_duration"], $"{path}.tap_key_duration", diagnostics),
            ParseDuration(timing["tap_mouse_duration"], $"{path}.tap_mouse_duration", diagnostics),
            ParseDuration(timing["coordinate_tap_duration"], $"{path}.coordinate_tap_duration", diagnostics),
            ParseDuration(timing["default_while_held_interval"], $"{path}.default_while_held_interval", diagnostics));
    }

    private static CoordinateSettingsV2? ParseCoordinateSettings(
        JsonNode? coordinateNode,
        string path,
        List<AppConfigV2Diagnostic> diagnostics)
    {
        if (coordinateNode is null)
        {
            return null;
        }

        if (coordinateNode is not JsonObject coordinate)
        {
            diagnostics.Add(Error(
                AppConfigV2DiagnosticCode.InvalidCoordinateSettings,
                "coordinate settings must be an object.",
                path));
            return null;
        }

        var profile = ReadString(coordinate["profile"], $"{path}.profile", diagnostics);
        var executionPolicy = ParseExecutionPolicy(
            coordinate["execution_policy"],
            $"{path}.execution_policy",
            diagnostics);
        var transformPolicy = ParseTransformPolicy(
            coordinate["transform_policy"],
            $"{path}.transform_policy",
            diagnostics);

        return new CoordinateSettingsV2(profile, executionPolicy, transformPolicy);
    }

    private static HotkeySpecV2? ParseHotkey(
        JsonNode? node,
        string path,
        List<AppConfigV2Diagnostic> diagnostics)
    {
        var text = ReadString(node, path, diagnostics);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            return HotkeySpecV2.Parse(text);
        }
        catch (ArgumentException ex)
        {
            diagnostics.Add(Error(AppConfigV2DiagnosticCode.InvalidHotkey, ex.Message, path));
            return null;
        }
    }

    private static CoordinateRecordHotkeySpecV2? ParseCoordinateRecordHotkey(
        JsonNode? node,
        string path,
        List<AppConfigV2Diagnostic> diagnostics)
    {
        var text = ReadString(node, path, diagnostics);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            return CoordinateRecordHotkeySpecV2.Parse(text);
        }
        catch (ArgumentException ex)
        {
            diagnostics.Add(Error(AppConfigV2DiagnosticCode.InvalidHotkey, ex.Message, path));
            return null;
        }
    }

    private static InputSpec? ParseInput(
        JsonNode? node,
        string path,
        List<AppConfigV2Diagnostic> diagnostics)
    {
        var text = ReadString(node, path, diagnostics);
        if (string.IsNullOrWhiteSpace(text))
        {
            diagnostics.Add(Error(
                AppConfigV2DiagnosticCode.InvalidInputName,
                "Input name must not be empty.",
                path));
            return null;
        }

        if (InputNameResolverV2.TryResolve(text, out var input))
        {
            return input;
        }

        diagnostics.Add(Error(
            AppConfigV2DiagnosticCode.InvalidInputName,
            $"Unsupported input name '{text}'.",
            path));
        return null;
    }

    private static TimeSpan? ParseDuration(
        JsonNode? node,
        string path,
        List<AppConfigV2Diagnostic> diagnostics)
    {
        if (node is null)
        {
            return null;
        }

        var text = ReadString(node, path, diagnostics);
        if (text is null)
        {
            return null;
        }

        if (AppConfigV2DurationParser.TryParse(text, out var duration))
        {
            return duration;
        }

        diagnostics.Add(Error(
            AppConfigV2DiagnosticCode.InvalidTiming,
            $"Duration '{text}' is not a supported AppConfigV2 duration.",
            path));
        return null;
    }

    private static CoordinateExecutionPolicyV2 ParseExecutionPolicy(
        JsonNode? node,
        string path,
        List<AppConfigV2Diagnostic> diagnostics)
    {
        var text = ReadString(node, path, diagnostics);
        if (string.IsNullOrWhiteSpace(text))
        {
            return CoordinateExecutionPolicyV2.StrictCursorPreserving;
        }

        return NormalizeToken(text) switch
        {
            "strict_cursor_preserving" => CoordinateExecutionPolicyV2.StrictCursorPreserving,
            "prefer_cursor_preserving" => CoordinateExecutionPolicyV2.PreferCursorPreserving,
            "allow_real_cursor" => CoordinateExecutionPolicyV2.AllowRealCursor,
            _ => InvalidCoordinateEnum<CoordinateExecutionPolicyV2>(text, path, diagnostics)
        };
    }

    private static CoordinateTransformPolicyV2 ParseTransformPolicy(
        JsonNode? node,
        string path,
        List<AppConfigV2Diagnostic> diagnostics)
    {
        var text = ReadString(node, path, diagnostics);
        if (string.IsNullOrWhiteSpace(text))
        {
            return CoordinateTransformPolicyV2.SnapshotPerGesture;
        }

        return NormalizeToken(text) switch
        {
            "snapshot_per_gesture" => CoordinateTransformPolicyV2.SnapshotPerGesture,
            "dynamic_per_step" => CoordinateTransformPolicyV2.DynamicPerStep,
            _ => InvalidCoordinateEnum<CoordinateTransformPolicyV2>(text, path, diagnostics)
        };
    }

    private static TEnum InvalidCoordinateEnum<TEnum>(
        string text,
        string path,
        List<AppConfigV2Diagnostic> diagnostics)
        where TEnum : struct, Enum
    {
        diagnostics.Add(Error(
            AppConfigV2DiagnosticCode.InvalidCoordinateSettings,
            $"Unsupported coordinate policy value '{text}'.",
            path));
        return default;
    }

    private static JsonObject WriteDefaults(
        TimingSettingsV2? timing,
        CoordinateSettingsV2? coordinate)
    {
        var defaults = new JsonObject();
        if (timing is not null)
        {
            defaults["timing"] = WriteTiming(timing);
        }

        if (coordinate is not null)
        {
            defaults["coordinate"] = WriteCoordinateSettings(coordinate);
        }

        return defaults;
    }

    private static JsonObject WriteMapping(MappingConfigV2 mapping)
    {
        var json = new JsonObject
        {
            ["id"] = mapping.Id,
            ["name"] = mapping.Name,
            ["enabled"] = mapping.Enabled
        };

        if (mapping.Trigger is not null)
        {
            json["trigger"] = WriteTrigger(mapping.Trigger);
        }

        if (mapping.ActionSource is not null)
        {
            json["action"] = WriteActionSource(mapping.ActionSource);
        }

        if (mapping.TimingOverride is not null)
        {
            json["timing"] = WriteTiming(mapping.TimingOverride);
        }

        if (mapping.CoordinateSettingsOverride is not null)
        {
            json["coordinate"] = WriteCoordinateSettings(mapping.CoordinateSettingsOverride);
        }

        return json;
    }

    private static JsonObject WriteTrigger(TriggerConfigV2 trigger)
    {
        var json = new JsonObject
        {
            ["kind"] = "single_input"
        };

        if (trigger.Input is not null)
        {
            json["input"] = trigger.Input.CanonicalName;
        }

        return json;
    }

    private static JsonObject WriteActionSource(MappingActionSourceV2 actionSource)
    {
        return actionSource switch
        {
            MappingActionSourceV2.SimpleMapping simple => WriteSimpleActionSource(simple),
            MappingActionSourceV2.MacroDslV2Source macro => new JsonObject
            {
                ["kind"] = "macro_dsl_v2",
                ["source"] = macro.SourceText
            },
            _ => new JsonObject
            {
                ["kind"] = "unsupported"
            }
        };
    }

    private static JsonObject WriteSimpleActionSource(MappingActionSourceV2.SimpleMapping simple)
    {
        var json = new JsonObject
        {
            ["kind"] = "simple",
            ["mode"] = simple.Definition.Mode switch
            {
                SimpleMappingModeV2.Tap => "tap",
                SimpleMappingModeV2.Hold => "hold",
                _ => "unsupported"
            },
            ["target"] = WriteSimpleTarget(simple.Definition.Target)
        };

        if (simple.Definition.Duration is { } duration)
        {
            json["duration"] = AppConfigV2DurationParser.Format(duration);
        }

        return json;
    }

    private static JsonObject WriteSimpleTarget(SimpleMappingTargetV2 target)
    {
        return target.Kind switch
        {
            SimpleMappingTargetKindV2.Input => new JsonObject
            {
                ["kind"] = "input",
                ["input"] = target.Input?.CanonicalName
            },
            SimpleMappingTargetKindV2.Coordinate => new JsonObject
            {
                ["kind"] = "coordinate",
                ["x"] = target.Coordinate?.LogicalX,
                ["y"] = target.Coordinate?.LogicalY,
                ["profile"] = target.Coordinate?.ProfileId
            },
            _ => new JsonObject
            {
                ["kind"] = "unsupported"
            }
        };
    }

    private static JsonObject WriteTiming(TimingSettingsV2 timing)
    {
        var json = new JsonObject();
        WriteDuration(json, "tap_key_duration", timing.TapKeyDuration);
        WriteDuration(json, "tap_mouse_duration", timing.TapMouseDuration);
        WriteDuration(json, "coordinate_tap_duration", timing.CoordinateTapDuration);
        WriteDuration(json, "default_while_held_interval", timing.DefaultWhileHeldInterval);
        return json;
    }

    private static JsonObject WriteCoordinateSettings(CoordinateSettingsV2 settings)
    {
        var json = new JsonObject();
        if (settings.ProfileId is not null)
        {
            json["profile"] = settings.ProfileId;
        }

        json["execution_policy"] = settings.ExecutionPolicy switch
        {
            CoordinateExecutionPolicyV2.StrictCursorPreserving => "strict_cursor_preserving",
            CoordinateExecutionPolicyV2.PreferCursorPreserving => "prefer_cursor_preserving",
            CoordinateExecutionPolicyV2.AllowRealCursor => "allow_real_cursor",
            _ => "unsupported"
        };
        json["transform_policy"] = settings.TransformPolicy switch
        {
            CoordinateTransformPolicyV2.SnapshotPerGesture => "snapshot_per_gesture",
            CoordinateTransformPolicyV2.DynamicPerStep => "dynamic_per_step",
            _ => "unsupported"
        };
        return json;
    }

    private static void WriteDuration(JsonObject json, string name, TimeSpan? duration)
    {
        if (duration is not null)
        {
            json[name] = AppConfigV2DurationParser.Format(duration.Value);
        }
    }

    private static string? ReadString(
        JsonNode? node,
        string path,
        List<AppConfigV2Diagnostic> diagnostics)
    {
        if (node is null)
        {
            return null;
        }

        if (node is JsonValue value &&
            value.TryGetValue<string>(out var text))
        {
            return text;
        }

        diagnostics.Add(Error(
            AppConfigV2DiagnosticCode.InvalidJson,
            "Expected JSON string.",
            path));
        return null;
    }

    private static bool? ReadBool(
        JsonNode? node,
        string path,
        List<AppConfigV2Diagnostic> diagnostics)
    {
        if (node is null)
        {
            return null;
        }

        if (node is JsonValue value &&
            value.TryGetValue<bool>(out var result))
        {
            return result;
        }

        diagnostics.Add(Error(
            AppConfigV2DiagnosticCode.InvalidJson,
            "Expected JSON boolean.",
            path));
        return null;
    }

    private static int? ReadInt(
        JsonNode? node,
        string path,
        List<AppConfigV2Diagnostic> diagnostics)
    {
        if (node is null)
        {
            return null;
        }

        if (node is JsonValue value &&
            value.TryGetValue<int>(out var result))
        {
            return result;
        }

        diagnostics.Add(Error(
            AppConfigV2DiagnosticCode.InvalidJson,
            "Expected JSON integer.",
            path));
        return null;
    }

    private static string? NormalizeToken(string? value)
    {
        return value?.Trim().ToLowerInvariant();
    }

    private static AppConfigV2Diagnostic ProjectValidationDiagnosticForJson(
        AppConfigV2Diagnostic diagnostic)
    {
        var path = ToJsonPath(diagnostic.Path);
        var affectedPaths = diagnostic.AffectedPaths
            .Select(ToJsonPath)
            .ToArray();

        return diagnostic with
        {
            Path = path,
            AffectedPaths = affectedPaths
        };
    }

    private static string ToJsonPath(string path)
    {
        if (path.EndsWith(".coordinate.profile_id", StringComparison.Ordinal))
        {
            return $"{path[..^".profile_id".Length]}.profile";
        }

        if (string.Equals(path, "defaults.coordinate.profile_id", StringComparison.Ordinal))
        {
            return "defaults.coordinate.profile";
        }

        return path;
    }

    private static AppConfigV2SerializationResult Failure(AppConfigV2Diagnostic diagnostic)
    {
        return new AppConfigV2SerializationResult(null, [diagnostic]);
    }

    private static AppConfigV2Diagnostic Error(
        AppConfigV2DiagnosticCode code,
        string message,
        string path)
    {
        return new AppConfigV2Diagnostic(
            AppConfigV2DiagnosticSeverity.Error,
            code,
            message,
            path,
            BlocksSave: true,
            BlocksLive: true);
    }
}
