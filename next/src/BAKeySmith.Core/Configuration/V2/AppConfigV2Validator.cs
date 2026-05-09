using BAKeySmith.Core.Input.V2.Conflicts;
using BAKeySmith.Core.Mappings.V2;

namespace BAKeySmith.Core.Configuration.V2;

public sealed class AppConfigV2Validator
{
    private readonly SimpleMappingPlanBuilderV2 _simpleBuilder = new();

    public IReadOnlyList<AppConfigV2Diagnostic> Validate(AppConfigV2 config)
    {
        var diagnostics = new List<AppConfigV2Diagnostic>();

        if (config.Version != AppConfigV2.CurrentVersion)
        {
            diagnostics.Add(Error(
                AppConfigV2DiagnosticCode.InvalidVersion,
                $"AppConfigV2 version must be {AppConfigV2.CurrentVersion}.",
                "version"));
        }

        ValidateTiming(config.DefaultTiming, "defaults.timing", diagnostics);
        ValidateCoordinateSettings(config.DefaultCoordinateSettings, "defaults.coordinate", diagnostics);
        ValidateMappings(config, diagnostics);
        ValidateInputConflicts(config, diagnostics);

        return diagnostics;
    }

    private void ValidateMappings(
        AppConfigV2 config,
        List<AppConfigV2Diagnostic> diagnostics)
    {
        var ids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < config.Mappings.Count; index++)
        {
            var mapping = config.Mappings[index];
            var path = $"mappings[{index}]";

            if (string.IsNullOrWhiteSpace(mapping.Id))
            {
                diagnostics.Add(Error(
                    AppConfigV2DiagnosticCode.MissingMappingId,
                    "Mapping id must not be empty.",
                    $"{path}.id"));
            }
            else if (!ids.TryAdd(mapping.Id.Trim(), index))
            {
                diagnostics.Add(Error(
                    AppConfigV2DiagnosticCode.DuplicateMappingId,
                    $"Mapping id '{mapping.Id}' is already used.",
                    $"{path}.id"));
            }

            if (mapping.Enabled && mapping.Trigger is null)
            {
                diagnostics.Add(Error(
                    AppConfigV2DiagnosticCode.MissingTrigger,
                    "Enabled mapping must have a trigger.",
                    $"{path}.trigger"));
            }

            if (mapping.Trigger is not null)
            {
                ValidateTrigger(mapping.Trigger, $"{path}.trigger", diagnostics);
            }

            ValidateActionSource(mapping.ActionSource, $"{path}.action", diagnostics);
            ValidateTiming(mapping.TimingOverride, $"{path}.timing", diagnostics);
            ValidateCoordinateSettings(mapping.CoordinateSettingsOverride, $"{path}.coordinate", diagnostics);
        }
    }

    private static void ValidateTrigger(
        TriggerConfigV2 trigger,
        string path,
        List<AppConfigV2Diagnostic> diagnostics)
    {
        if (!Enum.IsDefined(trigger.Kind))
        {
            diagnostics.Add(Error(
                AppConfigV2DiagnosticCode.InvalidTrigger,
                $"{trigger.Kind} is not a supported trigger kind.",
                $"{path}.kind"));
            return;
        }

        if (trigger.Kind != TriggerKindV2.SingleInput)
        {
            diagnostics.Add(Error(
                AppConfigV2DiagnosticCode.InvalidTrigger,
                $"{trigger.Kind} is not supported by the AppConfigV2 skeleton.",
                $"{path}.kind"));
            return;
        }

        if (trigger.Input is null)
        {
            diagnostics.Add(Error(
                AppConfigV2DiagnosticCode.InvalidTrigger,
                "Single input trigger is missing InputSpec.",
                $"{path}.input"));
            return;
        }

        if (!trigger.Input.CanBeMappingTrigger)
        {
            diagnostics.Add(Error(
                AppConfigV2DiagnosticCode.InvalidTrigger,
                $"{trigger.Input.CanonicalName} cannot be used as a mapping trigger.",
                $"{path}.input"));
        }
    }

    private void ValidateActionSource(
        MappingActionSourceV2? actionSource,
        string path,
        List<AppConfigV2Diagnostic> diagnostics)
    {
        switch (actionSource)
        {
            case null:
                diagnostics.Add(Error(
                    AppConfigV2DiagnosticCode.InvalidActionSource,
                    "Mapping action source is missing.",
                    path));
                break;
            case MappingActionSourceV2.SimpleMapping simple when simple.Kind != MappingActionSourceKindV2.Simple:
                diagnostics.Add(Error(
                    AppConfigV2DiagnosticCode.InvalidActionSourceKind,
                    $"Action source kind {simple.Kind} does not match SimpleMapping subtype.",
                    $"{path}.kind"));
                break;
            case MappingActionSourceV2.SimpleMapping simple:
                ValidateSimpleActionSource(simple, path, diagnostics);
                break;
            case MappingActionSourceV2.MacroDslV2Source macro when macro.Kind != MappingActionSourceKindV2.MacroDslV2:
                diagnostics.Add(Error(
                    AppConfigV2DiagnosticCode.InvalidActionSourceKind,
                    $"Action source kind {macro.Kind} does not match MacroDslV2Source subtype.",
                    $"{path}.kind"));
                break;
            case MappingActionSourceV2.MacroDslV2Source macro:
                ValidateMacroActionSource(macro, path, diagnostics);
                break;
            default:
                diagnostics.Add(Error(
                    AppConfigV2DiagnosticCode.InvalidActionSource,
                    $"Unsupported action source type {actionSource.GetType().Name}.",
                    path));
                break;
        }
    }

    private static void ValidateMacroActionSource(
        MappingActionSourceV2.MacroDslV2Source macro,
        string path,
        List<AppConfigV2Diagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(macro.SourceText))
        {
            diagnostics.Add(Warning(
                AppConfigV2DiagnosticCode.EmptyMacroSource,
                "Macro DSL v2 source shell is empty. This is allowed for draft editing, but it cannot build a plan.",
                $"{path}.macro_dsl_v2.source"));
        }
    }

    private void ValidateSimpleActionSource(
        MappingActionSourceV2.SimpleMapping simple,
        string path,
        List<AppConfigV2Diagnostic> diagnostics)
    {
        var result = _simpleBuilder.Build(simple.Definition);
        foreach (var diagnostic in result.Diagnostics.Where(IsSimpleError))
        {
            diagnostics.Add(Error(
                AppConfigV2DiagnosticCode.InvalidSimpleMappingSource,
                $"{diagnostic.Code}: {diagnostic.Message}",
                $"{path}.{diagnostic.Path}"));
        }
    }

    private static void ValidateTiming(
        TimingSettingsV2? timing,
        string path,
        List<AppConfigV2Diagnostic> diagnostics)
    {
        if (timing is null)
        {
            return;
        }

        ValidateOptionalDuration(timing.TapKeyDuration, $"{path}.tap_key_duration", diagnostics);
        ValidateOptionalDuration(timing.TapMouseDuration, $"{path}.tap_mouse_duration", diagnostics);
        ValidateOptionalDuration(timing.CoordinateTapDuration, $"{path}.coordinate_tap_duration", diagnostics);
        ValidateOptionalDuration(timing.DefaultWhileHeldInterval, $"{path}.default_while_held_interval", diagnostics);
    }

    private static void ValidateOptionalDuration(
        TimeSpan? duration,
        string path,
        List<AppConfigV2Diagnostic> diagnostics)
    {
        if (duration is { } value && value < TimeSpan.Zero)
        {
            diagnostics.Add(Error(
                AppConfigV2DiagnosticCode.InvalidTiming,
                "Timing duration must not be negative.",
                path));
        }
    }

    private static void ValidateCoordinateSettings(
        CoordinateSettingsV2? settings,
        string path,
        List<AppConfigV2Diagnostic> diagnostics)
    {
        if (settings is null)
        {
            return;
        }

        if (settings.ProfileId is not null && string.IsNullOrWhiteSpace(settings.ProfileId))
        {
            diagnostics.Add(Error(
                AppConfigV2DiagnosticCode.InvalidCoordinateSettings,
                "Coordinate profile id must not be empty when supplied.",
                $"{path}.profile_id"));
        }

        if (!Enum.IsDefined(settings.ExecutionPolicy))
        {
            diagnostics.Add(Error(
                AppConfigV2DiagnosticCode.InvalidCoordinateSettings,
                $"{settings.ExecutionPolicy} is not a supported coordinate execution policy.",
                $"{path}.execution_policy"));
        }

        if (!Enum.IsDefined(settings.TransformPolicy))
        {
            diagnostics.Add(Error(
                AppConfigV2DiagnosticCode.InvalidCoordinateSettings,
                $"{settings.TransformPolicy} is not a supported coordinate transform policy.",
                $"{path}.transform_policy"));
        }
    }

    private static void ValidateInputConflicts(
        AppConfigV2 config,
        List<AppConfigV2Diagnostic> diagnostics)
    {
        var bindings = new List<BindingWithPath>();

        for (var index = 0; index < config.Mappings.Count; index++)
        {
            var mapping = config.Mappings[index];
            if (!mapping.Enabled)
            {
                continue;
            }

            if (mapping.Trigger is { Kind: TriggerKindV2.SingleInput, Input: { } input } &&
                input.CanBeMappingTrigger)
            {
                bindings.Add(new BindingWithPath(
                    new InputBindingV2(
                        $"mapping:{mapping.Id}:trigger",
                        string.IsNullOrWhiteSpace(mapping.Name) ? mapping.Id : mapping.Name,
                        InputBindingRole.MappingTrigger,
                        input,
                        null),
                    $"mappings[{index}].trigger"));
            }
        }

        if (config.ControlHotkey is not null)
        {
            bindings.Add(new BindingWithPath(
                new InputBindingV2(
                    "control_hotkey",
                    "control hotkey",
                    InputBindingRole.ControlHotkey,
                    null,
                    config.ControlHotkey),
                "control_hotkey"));
        }

        if (config.CoordinateRecordHotkey is not null)
        {
            AddCoordinateRecordHotkeyBinding(bindings, config.CoordinateRecordHotkey);
        }

        if (config.EmergencyStopHotkey is not null)
        {
            bindings.Add(new BindingWithPath(
                new InputBindingV2(
                    "emergency_stop_hotkey",
                    "emergency stop hotkey",
                    InputBindingRole.EmergencyStopHotkey,
                    null,
                    config.EmergencyStopHotkey),
                "emergency_stop_hotkey"));
        }

        var pathById = bindings
            .GroupBy(item => item.Binding.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First().Path,
                StringComparer.OrdinalIgnoreCase);
        var rawBindings = bindings.Select(item => item.Binding).ToArray();

        foreach (var report in new InputConflictAnalyzerV2().Analyze(rawBindings))
        {
            var affectedPaths = report.AffectedBindingIds
                .Select(id => pathById.TryGetValue(id, out var path) ? path : null)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            diagnostics.Add(new AppConfigV2Diagnostic(
                MapSeverity(report.Severity),
                AppConfigV2DiagnosticCode.InputConflict,
                $"{report.Code}: {report.Message}",
                affectedPaths.FirstOrDefault() ?? "bindings",
                report.Code,
                report.AffectedBindingIds,
                report.AffectedBindingNames,
                affectedPaths,
                report.BlocksSave,
                report.BlocksLive));
        }
    }

    private static AppConfigV2DiagnosticSeverity MapSeverity(InputConflictSeverity severity)
    {
        return severity switch
        {
            InputConflictSeverity.Error => AppConfigV2DiagnosticSeverity.Error,
            InputConflictSeverity.Warning => AppConfigV2DiagnosticSeverity.Warning,
            InputConflictSeverity.LiveBlocker => AppConfigV2DiagnosticSeverity.LiveBlocker,
            _ => AppConfigV2DiagnosticSeverity.Info
        };
    }

    private static void AddCoordinateRecordHotkeyBinding(
        List<BindingWithPath> bindings,
        CoordinateRecordHotkeySpecV2 hotkey)
    {
        if (hotkey.Modifiers.Count == 0)
        {
            bindings.Add(new BindingWithPath(
                new InputBindingV2(
                    "coordinate_record_hotkey",
                    "coordinate record hotkey",
                    InputBindingRole.CoordinateRecordHotkey,
                    hotkey.MainInput,
                    null),
                "coordinate_record_hotkey"));
            return;
        }

        if (hotkey.MainInput.CanBeHotkeyMainKey)
        {
            bindings.Add(new BindingWithPath(
                new InputBindingV2(
                    "coordinate_record_hotkey",
                    "coordinate record hotkey",
                    InputBindingRole.CoordinateRecordHotkey,
                    null,
                    new HotkeySpecV2(hotkey.Modifiers, hotkey.MainInput)),
                "coordinate_record_hotkey"));
            return;
        }

        // Conflict Model v2 skeleton does not yet model mouse-main combos as
        // first-class hotkeys. Represent each component conservatively so an
        // AppConfigV2 draft cannot hide mapping-trigger overlap.
        foreach (var component in hotkey.Components)
        {
            bindings.Add(new BindingWithPath(
                new InputBindingV2(
                    $"coordinate_record_hotkey:{component.CanonicalName}",
                    $"coordinate record hotkey {component.CanonicalName}",
                    InputBindingRole.CoordinateRecordHotkey,
                    component,
                    null),
                "coordinate_record_hotkey"));
        }
    }

    private static bool IsSimpleError(SimpleMappingValidationDiagnosticV2 diagnostic)
    {
        return diagnostic.Severity == SimpleMappingValidationSeverityV2.Error;
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

    private static AppConfigV2Diagnostic Warning(
        AppConfigV2DiagnosticCode code,
        string message,
        string path)
    {
        return new AppConfigV2Diagnostic(
            AppConfigV2DiagnosticSeverity.Warning,
            code,
            message,
            path);
    }

    private sealed record BindingWithPath(InputBindingV2 Binding, string Path);
}
