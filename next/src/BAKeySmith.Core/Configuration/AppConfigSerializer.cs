using System.Text.Json;
using BAKeySmith.Core.Contracts;
using BAKeySmith.Core.Input;
using BAKeySmith.Core.Scripting;

namespace BAKeySmith.Core.Configuration;

public static class AppConfigSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true
    };

    public static ConfigLoadResult Parse(string json)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        AppConfigV1 config;
        try
        {
            config = JsonSerializer.Deserialize<AppConfigV1>(json, JsonOptions) ?? new AppConfigV1();
        }
        catch (JsonException ex)
        {
            config = new AppConfigV1();
            errors.Add($"配置 JSON 格式错误: {ex.Message}");
            return new ConfigLoadResult(config, RuntimeConfig.Empty, errors, warnings);
        }

        var runtimeConfig = ToRuntimeConfig(config, errors, warnings);
        return new ConfigLoadResult(config, runtimeConfig, errors, warnings);
    }

    public static async Task<ConfigLoadResult> LoadFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            var config = new AppConfigV1();
            return new ConfigLoadResult(
                config,
                ToRuntimeConfig(config, [], []),
                [],
                [$"配置文件不存在，使用默认配置: {path}"]);
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return Parse(json);
    }

    public static string Save(AppConfigV1 config)
    {
        return JsonSerializer.Serialize(config, JsonOptions);
    }

    public static RuntimeConfig ToRuntimeConfig(
        AppConfigV1 config,
        List<string> errors,
        List<string> warnings)
    {
        var compiler = new MacroScriptCompiler();
        var mappings = new List<MappingDefinition>();
        var seenTriggers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var controlHotkey = NormalizeControlHotkey(config.Hotkey, errors);

        foreach (var mapping in config.Mappings)
        {
            if (string.IsNullOrWhiteSpace(mapping.Trigger))
            {
                warnings.Add("已跳过缺少 trigger 的映射。");
                continue;
            }

            var trigger = ToTriggerSpec(mapping.Trigger);
            if (controlHotkey.TryGetConflict(trigger, out var conflictKey))
            {
                errors.Add($"{conflictKey} 与控制热键 {controlHotkey.Display} 冲突。");
                continue;
            }

            if (!seenTriggers.Add(trigger.Key))
            {
                errors.Add($"触发键重复: {mapping.Trigger}");
                continue;
            }

            var id = string.IsNullOrWhiteSpace(mapping.Id)
                ? $"mapping:{trigger.Key}"
                : mapping.Id.Trim();
            var type = (mapping.Type ?? "simple").Trim().ToLowerInvariant();
            if (type == "macro")
            {
                var compileResult = compiler.Compile(mapping.Script ?? string.Empty);
                if (!compileResult.Success)
                {
                    errors.Add($"触发键 {mapping.Trigger} 的宏脚本有误:\n{string.Join("\n", compileResult.Errors)}");
                    continue;
                }

                mappings.Add(new MappingDefinition(
                    id,
                    trigger,
                    RuntimeAction.MacroPlan(compileResult.Instructions)));
                continue;
            }

            var target = mapping.Target;
            if (string.IsNullOrWhiteSpace(target))
            {
                errors.Add($"触发键 {mapping.Trigger} 缺少目标键。");
                continue;
            }

            var mode = (mapping.Mode ?? "hold").Trim().ToLowerInvariant();
            RuntimeAction action = mode switch
            {
                "tap" => RuntimeAction.Tap(NormalizeInputName(target)),
                "hold" or "" => RuntimeAction.Hold(NormalizeInputName(target)),
                _ => RuntimeAction.Hold(NormalizeInputName(target))
            };
            if (mode is not ("tap" or "hold" or ""))
            {
                warnings.Add($"触发键 {mapping.Trigger} 的 mode 无效，已按 hold 处理: {mode}");
            }

            mappings.Add(new MappingDefinition(id, trigger, action));
        }

        var targetProcess = string.IsNullOrWhiteSpace(config.TargetProcess)
            ? "BlueArchive.exe"
            : config.TargetProcess.Trim();

        return new RuntimeConfig(
            targetProcess,
            config.TapHoldMilliseconds,
            mappings);
    }

    private static TriggerSpec ToTriggerSpec(string trigger)
    {
        var normalized = trigger.Trim().ToLowerInvariant();
        if (KeyNameResolver.IsMouseTrigger(normalized) ||
            normalized.StartsWith("mouse_", StringComparison.OrdinalIgnoreCase) ||
            normalized is "left" or "right" or "middle" or "x1" or "x2")
        {
            return TriggerSpec.Mouse(KeyNameResolver.NormalizeMouseTrigger(normalized));
        }

        return TriggerSpec.Keyboard(KeyNameResolver.ResolveKeyboardKey(normalized).Name);
    }

    private static string NormalizeInputName(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.StartsWith("mouse_", StringComparison.OrdinalIgnoreCase) ||
            normalized is "left" or "right" or "middle" or "x1" or "x2")
        {
            return KeyNameResolver.NormalizeMouseButton(normalized);
        }

        return KeyNameResolver.ResolveKeyboardKey(normalized).Name;
    }

    private static ControlHotkey NormalizeControlHotkey(string? hotkey, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(hotkey))
        {
            return ControlHotkey.Empty;
        }

        var components = new List<ControlHotkeyComponent>();
        foreach (var rawPart in hotkey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.IsNullOrWhiteSpace(rawPart))
            {
                continue;
            }

            if (!KeyNameResolver.TryResolveKeyboardKey(rawPart, out var keyInfo))
            {
                errors.Add($"控制热键包含不支持的键: {rawPart}");
                continue;
            }

            components.Add(new ControlHotkeyComponent(
                TriggerSpec.Keyboard(keyInfo.Name).Key,
                keyInfo.Name));
        }

        return components.Count == 0
            ? ControlHotkey.Empty
            : new ControlHotkey(components);
    }

    private sealed record ControlHotkeyComponent(string TriggerKey, string Name);

    private sealed class ControlHotkey(IReadOnlyList<ControlHotkeyComponent> components)
    {
        public static ControlHotkey Empty { get; } = new([]);

        public string Display { get; } = string.Join("+", components.Select(component => component.Name));

        public bool TryGetConflict(TriggerSpec trigger, out string conflictKey)
        {
            var component = components.FirstOrDefault(component =>
                string.Equals(component.TriggerKey, trigger.Key, StringComparison.OrdinalIgnoreCase));
            if (component is null)
            {
                conflictKey = string.Empty;
                return false;
            }

            conflictKey = component.Name;
            return true;
        }
    }
}
