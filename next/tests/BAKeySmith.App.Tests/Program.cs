using System.IO;
using System.Text.Json;
using System.Windows.Input;
using BAKeySmith.App.Controls;
using BAKeySmith.App.Models;
using BAKeySmith.App.Services;
using BAKeySmith.App.ViewModels;
using BAKeySmith.Core.Configuration;
using BAKeySmith.Core.Contracts;
using BAKeySmith.Core.Foreground;
using BAKeySmith.Core.Scripting;
using BAKeySmith.Core.Triggers;

var tests = new (string Name, Func<Task> Run)[]
{
    ("config_load_save_round_trip_preserves_unknown_fields", ConfigLoadSaveRoundTripPreservesUnknownFieldsAsync),
    ("default_config_path_uses_user_appdata_location", DefaultConfigPathUsesUserAppDataLocation),
    ("missing_default_user_config_loads_in_memory_default_with_warning", MissingDefaultUserConfigLoadsInMemoryDefaultWithWarning),
    ("missing_default_user_config_save_creates_user_config_file", MissingDefaultUserConfigSaveCreatesUserConfigFileAsync),
    ("root_legacy_config_example_is_not_auto_selected", RootLegacyConfigExampleIsNotAutoSelectedAsync),
    ("manual_selected_config_loads_normally", ManualSelectedConfigLoadsNormallyAsync),
    ("selected_config_save_writes_selected_path_and_preserves_unknown_fields", SelectedConfigSaveWritesSelectedPathAndPreservesUnknownFieldsAsync),
    ("mapping_update_preserves_id_and_unknown_fields", MappingUpdatePreservesIdAndUnknownFieldsAsync),
    ("invalid_config_validation_reports_errors", InvalidConfigValidationReportsErrors),
    ("control_hotkey_conflict_validation_surfaces_error", ControlHotkeyConflictValidationSurfacesError),
    ("runtime_controller_start_rejects_control_hotkey_conflict", RuntimeControllerStartRejectsControlHotkeyConflictAsync),
    ("key_capture_formatter_keyboard_names", KeyCaptureFormatterKeyboardNames),
    ("key_capture_formatter_mouse_buttons", KeyCaptureFormatterMouseButtons),
    ("key_capture_formatter_rejects_ambiguous_and_unsupported_names", KeyCaptureFormatterRejectsAmbiguousAndUnsupportedNames),
    ("hotkey_capture_formatter_ctrl_shift_f12", HotkeyCaptureFormatterCtrlShiftF12),
    ("hotkey_capture_formatter_does_not_complete_on_modifier_only", HotkeyCaptureFormatterDoesNotCompleteOnModifierOnly),
    ("mapping_trigger_capture_updates_editor", MappingTriggerCaptureUpdatesEditor),
    ("hotkey_capture_updates_hotkey_after_non_modifier", HotkeyCaptureUpdatesHotkeyAfterNonModifier),
    ("running_capture_allows_mapping_trigger_hotkey_and_target", RunningCaptureAllowsMappingTriggerHotkeyAndTarget),
    ("mapping_target_capture_updates_editor", MappingTargetCaptureUpdatesEditor),
    ("mapping_target_capture_formatter_keyboard_and_mouse_names", MappingTargetCaptureFormatterKeyboardAndMouseNames),
    ("mapping_target_capture_rejects_wheel", MappingTargetCaptureRejectsWheel),
    ("macro_completion_policy_empty_command_position_opens_commands", MacroCompletionPolicyEmptyCommandPositionOpensCommands),
    ("macro_completion_policy_prefix_and_full_command", MacroCompletionPolicyPrefixAndFullCommand),
    ("macro_completion_policy_argument_slots", MacroCompletionPolicyArgumentSlots),
    ("macro_completion_policy_suppresses_completion_apply_reopen", MacroCompletionPolicySuppressesCompletionApplyReopen),
    ("macro_completion_policy_esc_dismissed_context_does_not_reopen", MacroCompletionPolicyEscDismissedContextDoesNotReopen),
    ("valid_macro_script_updates_diagnostics_to_valid", ValidMacroScriptUpdatesDiagnosticsToValid),
    ("invalid_macro_script_updates_diagnostics_to_invalid", InvalidMacroScriptUpdatesDiagnosticsToInvalid),
    ("macro_compiler_diagnostics_display_model", MacroCompilerDiagnosticsDisplayModel),
    ("mapping_editor_macro_script_change_updates_diagnostics_display_model", MappingEditorMacroScriptChangeUpdatesDiagnosticsDisplayModel),
    ("simple_mapping_does_not_require_macro_diagnostics_for_save", SimpleMappingDoesNotRequireMacroDiagnosticsForSave),
    ("mapping_editor_dirty_state", MappingEditorDirtyState),
    ("mapping_editor_commands_update_preserves_unknown_fields", MappingEditorCommandsUpdatePreservesUnknownFields),
    ("mapping_editor_commands_surface_validation_errors", MappingEditorCommandsSurfaceValidationErrors),
    ("diagnostics_append_caps_at_600", DiagnosticsAppendCapsAt600),
    ("clear_diagnostics_command_clears_entries", ClearDiagnosticsCommandClearsEntries),
    ("runtime_status_null_snapshot_is_stopped_unknown", RuntimeStatusNullSnapshotIsStoppedUnknown),
    ("runtime_status_running_snapshot_fields_are_populated", RuntimeStatusRunningSnapshotFieldsArePopulatedAsync),
    ("foreground_probe_display_formats_result", ForegroundProbeDisplayFormatsResult),
    ("main_view_model_defaults_to_dry_run", MainViewModelDefaultsToDryRun),
    ("elevation_status_display_model_formats_state", ElevationStatusDisplayModelFormatsState),
    ("dry_run_start_does_not_request_live_confirmation", DryRunStartDoesNotRequestLiveConfirmationAsync),
    ("live_start_non_elevated_blocks_before_confirmation", LiveStartNonElevatedBlocksBeforeConfirmationAsync),
    ("live_start_confirmed_invokes_start_callback", LiveStartConfirmedInvokesStartCallbackAsync),
    ("live_start_cancelled_skips_start_and_records_diagnostic", LiveStartCancelledSkipsStartAndRecordsDiagnosticAsync),
    ("live_mode_does_not_change_config_save_behavior", LiveModeDoesNotChangeConfigSaveBehaviorAsync),
    ("gui_self_foreground_gate_blocks_bakeysmith_app_foreground", GuiSelfForegroundGateBlocksBakeysmithAppForeground),
    ("runtime_controller_live_path_passes_self_names_to_hook_source", RuntimeControllerLivePathPassesSelfNamesToHookSourceAsync),
    ("runtime_controller_blocks_dispatch_when_foreground_is_app", RuntimeControllerBlocksDispatchWhenForegroundIsAppAsync),
    ("runtime_controller_allows_dispatch_when_foreground_is_target", RuntimeControllerAllowsDispatchWhenForegroundIsTargetAsync),
    ("runtime_control_commands_route_callbacks", RuntimeControlCommandsRouteCallbacks),
    ("backup_before_save_writes_previous_content", BackupBeforeSaveWritesPreviousContentAsync),
    ("runtime_reload_rejects_invalid_config_and_keeps_previous_runtime_config", RuntimeReloadRejectsInvalidConfigAndKeepsPreviousRuntimeConfigAsync),
    ("runtime_controller_start_stop_reload_dry_wiring", RuntimeControllerStartStopReloadDryWiringAsync)
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{test.Name}: {ex.Message}");
        Console.WriteLine($"FAIL {test.Name}: {ex}");
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine(string.Join(Environment.NewLine, failures));
    return 1;
}

return 0;

static async Task ConfigLoadSaveRoundTripPreservesUnknownFieldsAsync()
{
    var directory = CreateTempDirectory();
    var path = Path.Combine(directory, "config.json");
    await File.WriteAllTextAsync(
        path,
        """
        {
          "version": 1,
          "target_process": "BlueArchive.exe",
          "hotkey": "ctrl+shift+f12",
          "tap_hold_ms": 20,
          "root_unknown": "keep-me",
          "mappings": [
            {
              "id": "m1",
              "trigger": "q",
              "type": "simple",
              "target": "esc",
              "mode": "tap",
              "mapping_unknown": 42
            }
          ]
        }
        """);

    var viewModel = new MainWindowViewModel(new ConfigDocumentService());
    var document = viewModel.LoadConfig(path);
    Assert(document.Success, "expected config load success");
    Assert(!viewModel.MappingEditor.IsDirty, "load should mark editor clean");

    var config = viewModel.BuildConfigFromEditor();
    var output = Path.Combine(directory, "saved.json");
    await viewModel.SaveConfigAsync(output, config, createBackup: false, CancellationToken.None);

    using var json = JsonDocument.Parse(await File.ReadAllTextAsync(output));
    var root = json.RootElement;
    Assert(root.TryGetProperty("root_unknown", out var rootUnknown), "root unknown field missing");
    Assert(rootUnknown.GetString() == "keep-me", "root unknown field changed");
    var mapping = root.GetProperty("mappings")[0];
    Assert(mapping.TryGetProperty("mapping_unknown", out var mappingUnknown), "mapping unknown field missing");
    Assert(mappingUnknown.GetInt32() == 42, "mapping unknown field changed");
    Assert(!viewModel.MappingEditor.IsDirty, "save should mark editor clean");
}

static Task DefaultConfigPathUsesUserAppDataLocation()
{
    var userConfigDirectory = CreateTempDirectory();
    var service = new ConfigDocumentService(userConfigDirectory);
    var viewModel = new MainWindowViewModel(service);
    var expected = Path.Combine(userConfigDirectory, "config.json");

    Assert(service.DefaultConfigPath == expected, "service should use injected user config path");
    Assert(viewModel.ConfigPath == expected, "view model should show user config path by default");
    return Task.CompletedTask;
}

static Task MissingDefaultUserConfigLoadsInMemoryDefaultWithWarning()
{
    var userConfigDirectory = CreateTempDirectory();
    var service = new ConfigDocumentService(userConfigDirectory);
    var viewModel = new MainWindowViewModel(service);
    var document = viewModel.LoadConfig(viewModel.ConfigPath);

    Assert(document.Success, "missing default user config should load in-memory default config");
    Assert(document.Warnings.Any(warning => warning.Contains("配置文件不存在", StringComparison.Ordinal)),
        "missing default user config should report a visible warning");
    Assert(!File.Exists(viewModel.ConfigPath), "missing default load must not silently write user config");
    Assert(viewModel.TargetProcess == "BlueArchive.exe", "default target process should remain in memory");
    Assert(viewModel.MappingEditor.Mappings.Count == 0, "missing default should not import legacy example mappings");
    return Task.CompletedTask;
}

static async Task MissingDefaultUserConfigSaveCreatesUserConfigFileAsync()
{
    var workspace = CreateTempDirectory();
    var legacyExample = Path.Combine(workspace, "config.example.json");
    await File.WriteAllTextAsync(
        legacyExample,
        """
        {
          "version": 1,
          "target_process": "LegacyRoot.exe",
          "mappings": [
            {
              "trigger": "q",
              "type": "simple",
              "target": "esc",
              "mode": "tap"
            }
          ]
        }
        """);
    var userConfigDirectory = Path.Combine(workspace, "appdata", "BAKeySmith");
    var service = new ConfigDocumentService(userConfigDirectory);
    var viewModel = new MainWindowViewModel(service);
    var defaultPath = Path.Combine(userConfigDirectory, "config.json");

    Assert(!Directory.Exists(userConfigDirectory), "test should start without user config directory");
    Assert(!File.Exists(defaultPath), "test should start without default user config");

    var document = viewModel.LoadConfig(viewModel.ConfigPath);
    Assert(document.Success, "missing default user config should load in-memory default");
    Assert(document.Warnings.Any(warning => warning.Contains("配置文件不存在", StringComparison.Ordinal)),
        "missing default user config should report warning before save");
    Assert(!Directory.Exists(userConfigDirectory), "load should not silently create user config directory");
    Assert(viewModel.TargetProcess != "LegacyRoot.exe", "load should not read legacy root config.example.json");

    viewModel.TargetProcess = "SavedTarget.exe";
    viewModel.MappingEditor.Add(new MappingEditorRow
    {
        Trigger = "q",
        Type = "simple",
        Target = "esc",
        Mode = "tap"
    });

    await viewModel.SaveConfigAsync(
        viewModel.ConfigPath,
        viewModel.BuildConfigFromEditor(),
        createBackup: false,
        CancellationToken.None);

    Assert(Directory.Exists(userConfigDirectory), "save should create user config directory");
    Assert(File.Exists(defaultPath), "save should create default user config file");

    var reloaded = viewModel.LoadConfig(defaultPath);
    Assert(reloaded.Success, "saved default user config should reload");
    Assert(viewModel.TargetProcess == "SavedTarget.exe", "reloaded default user config should use saved content");
    Assert(viewModel.MappingEditor.Mappings.Count == 1, "reloaded default user config should include saved mapping");
    Assert(viewModel.TargetProcess != "LegacyRoot.exe", "reload should still not use legacy root config.example.json");
}

static async Task RootLegacyConfigExampleIsNotAutoSelectedAsync()
{
    var workspace = CreateTempDirectory();
    var legacyExample = Path.Combine(workspace, "config.example.json");
    await File.WriteAllTextAsync(
        legacyExample,
        """
        {
          "version": 1,
          "target_process": "LegacyRoot.exe",
          "mappings": [
            {
              "trigger": "q",
              "type": "simple",
              "target": "esc",
              "mode": "tap"
            }
          ]
        }
        """);

    var userConfigDirectory = Path.Combine(workspace, "appdata", "BAKeySmith");
    var viewModel = new MainWindowViewModel(new ConfigDocumentService(userConfigDirectory));
    var document = viewModel.LoadConfig(viewModel.ConfigPath);

    Assert(document.Success, "missing user config should still load default config");
    Assert(viewModel.ConfigPath == Path.Combine(userConfigDirectory, "config.json"),
        "default path should remain the user config path");
    Assert(viewModel.ConfigPath != legacyExample, "root legacy config.example.json must not be auto-selected");
    Assert(viewModel.TargetProcess != "LegacyRoot.exe", "legacy root config should not be loaded implicitly");
    Assert(viewModel.MappingEditor.Mappings.Count == 0, "legacy root mappings should not be imported implicitly");
}

static async Task ManualSelectedConfigLoadsNormallyAsync()
{
    var directory = CreateTempDirectory();
    var manualPath = Path.Combine(directory, "manual.config.json");
    await File.WriteAllTextAsync(
        manualPath,
        """
        {
          "version": 1,
          "target_process": "ManualTarget.exe",
          "hotkey": "f9",
          "tap_hold_ms": 30,
          "mappings": [
            {
              "trigger": "q",
              "type": "simple",
              "target": "esc",
              "mode": "tap"
            }
          ]
        }
        """);

    var viewModel = new MainWindowViewModel(new ConfigDocumentService(Path.Combine(directory, "appdata")));
    var document = viewModel.LoadConfig(manualPath);

    Assert(document.Success, "manual selected config should load normally");
    Assert(viewModel.ConfigPath == manualPath, "manual selected config should become current config path");
    Assert(viewModel.TargetProcess == "ManualTarget.exe", "manual config target should load");
    Assert(viewModel.Hotkey == "f9", "manual config hotkey should load");
    Assert(viewModel.TapHoldMilliseconds == "30", "manual config tap timing should load");
    Assert(viewModel.MappingEditor.Mappings.Count == 1, "manual config mappings should load");
}

static async Task SelectedConfigSaveWritesSelectedPathAndPreservesUnknownFieldsAsync()
{
    var directory = CreateTempDirectory();
    var selectedPath = Path.Combine(directory, "selected.config.json");
    await File.WriteAllTextAsync(
        selectedPath,
        """
        {
          "version": 1,
          "target_process": "BlueArchive.exe",
          "root_unknown": "root-stays",
          "mappings": [
            {
              "id": "m1",
              "trigger": "q",
              "type": "simple",
              "target": "esc",
              "mode": "tap",
              "mapping_unknown": "mapping-stays"
            }
          ]
        }
        """);

    var viewModel = new MainWindowViewModel(new ConfigDocumentService(Path.Combine(directory, "appdata")));
    var document = viewModel.LoadConfig(selectedPath);
    Assert(document.Success, "selected config should load before save");
    viewModel.TargetProcess = "ChangedTarget.exe";

    await viewModel.SaveConfigAsync(
        viewModel.ConfigPath,
        viewModel.BuildConfigFromEditor(),
        createBackup: false,
        CancellationToken.None);

    using var json = JsonDocument.Parse(await File.ReadAllTextAsync(selectedPath));
    var root = json.RootElement;
    Assert(root.GetProperty("target_process").GetString() == "ChangedTarget.exe",
        "save should write to selected config path");
    Assert(root.GetProperty("root_unknown").GetString() == "root-stays",
        "save should preserve root unknown field");
    var mapping = root.GetProperty("mappings")[0];
    Assert(mapping.GetProperty("id").GetString() == "m1", "save should preserve mapping id");
    Assert(mapping.GetProperty("mapping_unknown").GetString() == "mapping-stays",
        "save should preserve mapping unknown field");
}

static async Task MappingUpdatePreservesIdAndUnknownFieldsAsync()
{
    var directory = CreateTempDirectory();
    var path = Path.Combine(directory, "config.json");
    await File.WriteAllTextAsync(
        path,
        """
        {
          "version": 1,
          "target_process": "BlueArchive.exe",
          "root_unknown": "root-stays",
          "mappings": [
            {
              "id": "keep-this-id",
              "trigger": "q",
              "type": "simple",
              "target": "esc",
              "mode": "tap",
              "mapping_unknown": "mapping-stays"
            }
          ]
        }
        """);

    var viewModel = new MainWindowViewModel(new ConfigDocumentService());
    var document = viewModel.LoadConfig(path);
    Assert(document.Success, "expected config load success");
    var selected = viewModel.MappingEditor.Mappings.Single();
    viewModel.MappingEditor.Update(
        selected,
        new MappingEditorRow
        {
            Trigger = "w",
            Type = "simple",
            Target = "enter",
            Mode = "hold"
        });

    var output = Path.Combine(directory, "updated.json");
    await viewModel.SaveConfigAsync(
        output,
        viewModel.BuildConfigFromEditor(),
        createBackup: false,
        CancellationToken.None);

    using var json = JsonDocument.Parse(await File.ReadAllTextAsync(output));
    var root = json.RootElement;
    Assert(root.TryGetProperty("root_unknown", out var rootUnknown), "root unknown field missing");
    Assert(rootUnknown.GetString() == "root-stays", "root unknown field changed");
    var mapping = root.GetProperty("mappings")[0];
    Assert(mapping.GetProperty("id").GetString() == "keep-this-id", "mapping id should be preserved");
    Assert(mapping.TryGetProperty("mapping_unknown", out var mappingUnknown), "mapping unknown field missing");
    Assert(mappingUnknown.GetString() == "mapping-stays", "mapping unknown field changed");
    Assert(mapping.GetProperty("trigger").GetString() == "w", "trigger should be updated");
    Assert(mapping.GetProperty("target").GetString() == "enter", "target should be updated");
    Assert(mapping.GetProperty("mode").GetString() == "hold", "mode should be updated");
}

static Task MappingEditorCommandsUpdatePreservesUnknownFields()
{
    var editor = new MappingEditorViewModel();
    editor.LoadFromConfig(new AppConfigV1
    {
        Mappings =
        [
            new MappingConfigV1
            {
                Id = "existing-id",
                Trigger = "q",
                Type = "simple",
                Target = "esc",
                Mode = "tap",
                ExtraFields = new Dictionary<string, object>
                {
                    ["mapping_unknown"] = "keep"
                }
            }
        ]
    });

    editor.SelectedMapping = editor.Mappings.Single();
    editor.EditingTrigger = "w";
    editor.EditingTarget = "enter";
    editor.EditingMode = "hold";
    editor.UpdateCommand.Execute(null);

    var updated = editor.Mappings.Single();
    Assert(updated.Id == "existing-id", "command update should preserve id");
    var extraFields = updated.ExtraFields;
    if (extraFields is null)
    {
        throw new InvalidOperationException("command update should preserve extra fields");
    }

    Assert(extraFields.ContainsKey("mapping_unknown"), "mapping unknown field missing after command update");
    Assert(updated.Trigger == "w", "trigger should update through command");
    Assert(updated.Target == "enter", "target should update through command");
    Assert(updated.Mode == "hold", "mode should update through command");
    Assert(editor.IsDirty, "command update should mark dirty");
    Assert(string.IsNullOrWhiteSpace(editor.LastError), "command update should not surface an error");
    return Task.CompletedTask;
}

static Task MappingEditorCommandsSurfaceValidationErrors()
{
    var editor = new MappingEditorViewModel();
    editor.EditingTrigger = "";
    editor.AddCommand.Execute(null);
    Assert(editor.Mappings.Count == 0, "invalid command should not add mapping");
    Assert(editor.LastError.Contains("触发键不能为空", StringComparison.Ordinal), "expected command validation error");
    Assert(!editor.IsDirty, "invalid command should not mark dirty");
    return Task.CompletedTask;
}

static Task DiagnosticsAppendCapsAt600()
{
    var log = new DiagnosticsLogViewModel();
    for (var i = 0; i < DiagnosticsLogViewModel.MaxEntries + 5; i++)
    {
        log.Append("test.event", i.ToString());
    }

    Assert(log.Entries.Count == DiagnosticsLogViewModel.MaxEntries, "diagnostics should cap at 600 entries");
    Assert(log.Entries[0].Message == "5", "oldest diagnostics entries should be trimmed");
    Assert(log.Entries[^1].Message == "604", "newest diagnostics entry should be retained");
    return Task.CompletedTask;
}

static Task ClearDiagnosticsCommandClearsEntries()
{
    var viewModel = new MainWindowViewModel();
    viewModel.AppendDiagnostic("test.one", "first");
    viewModel.AppendDiagnostic("test.two", "second");
    Assert(viewModel.DiagnosticsLog.Entries.Count == 2, "expected diagnostics entries before clear");
    Assert(viewModel.Diagnostics.Count == 2, "legacy diagnostics mirror should have entries before clear");

    viewModel.ClearDiagnosticsCommand.Execute(null);

    Assert(viewModel.DiagnosticsLog.Entries.Count == 0, "command should clear diagnostics log entries");
    Assert(viewModel.Diagnostics.Count == 0, "command should clear diagnostics mirror");
    return Task.CompletedTask;
}

static Task RuntimeStatusNullSnapshotIsStoppedUnknown()
{
    var viewModel = new MainWindowViewModel();
    viewModel.TargetProcess = "BlueArchive.exe";
    viewModel.MappingEditor.Add(new MappingEditorRow
    {
        Trigger = "q",
        Type = "simple",
        Target = "esc",
        Mode = "tap"
    });

    viewModel.ApplySnapshot(null, inputEventCount: 7);

    Assert(viewModel.RuntimeStatus.StatusText == "Stopped", "null snapshot should display stopped status");
    Assert(viewModel.RuntimeStatus.HostStartedText == "stopped", "null snapshot should display stopped host");
    Assert(viewModel.RuntimeStatus.ForegroundStateText == "unknown", "null snapshot foreground should be unknown");
    Assert(viewModel.RuntimeStatus.InputEventCountText == "7", "input count should still display");
    Assert(viewModel.RuntimeStatus.SnapshotDetailsText.Contains("editor_mappings=1", StringComparison.Ordinal),
        "null snapshot details should include editor mapping count");
    return Task.CompletedTask;
}

static async Task RuntimeStatusRunningSnapshotFieldsArePopulatedAsync()
{
    await using var controller = new RuntimeHostController();
    var config = new AppConfigV1
    {
        TargetProcess = "BlueArchive.exe",
        Mappings =
        [
            new MappingConfigV1
            {
                Trigger = "q",
                Type = "simple",
                Target = "esc",
                Mode = "tap"
            }
        ]
    };

    await controller.StartAsync(config, dryRun: true, allowAnyForeground: true, CancellationToken.None);
    var viewModel = new MainWindowViewModel();
    viewModel.ApplySnapshot(controller.Snapshot(), controller.InputEventCount);

    Assert(viewModel.RuntimeStatus.StatusText == "Running", "running snapshot should display running status");
    Assert(viewModel.RuntimeStatus.HostStartedText == "started", "running snapshot should display started host");
    Assert(viewModel.RuntimeStatus.PipelineStateText == "running", "running snapshot should display running pipeline");
    Assert(viewModel.RuntimeStatus.TargetText == "BlueArchive.exe", "running snapshot should display target process");
    Assert(viewModel.RuntimeStatus.SnapshotDetailsText.Contains("state=", StringComparison.Ordinal),
        "snapshot details should include runtime state");
    Assert(viewModel.RuntimeStatus.SnapshotDetailsText.Contains("trigger_pipeline", StringComparison.Ordinal),
        "snapshot details should include trigger pipeline details");

    await controller.StopAsync(CancellationToken.None);
}

static Task ForegroundProbeDisplayFormatsResult()
{
    var viewModel = new MainWindowViewModel();
    var result = new ForegroundGateResult(
        false,
        "BlueArchive.exe",
        "notepad.exe",
        "notes");

    viewModel.ApplyForegroundProbe(result);

    Assert(viewModel.ForegroundProbe.Display.Contains("allowed=False", StringComparison.Ordinal),
        "foreground probe display should include allowed state");
    Assert(viewModel.ForegroundProbe.Display.Contains("target=BlueArchive.exe", StringComparison.Ordinal),
        "foreground probe display should include target process");
    Assert(viewModel.RuntimeStatus.ForegroundStateText == "blocked",
        "foreground probe should update foreground status display");
    return Task.CompletedTask;
}

static Task MainViewModelDefaultsToDryRun()
{
    var viewModel = new MainWindowViewModel();
    Assert(viewModel.IsDryRun, "live mode should not be enabled by default");
    Assert(viewModel.AllowAnyForeground, "dry-run foreground bypass should remain explicit GUI state");
    return Task.CompletedTask;
}

static Task ElevationStatusDisplayModelFormatsState()
{
    var elevatedViewModel = new MainWindowViewModel(
        elevationStatusService: new FakeElevationStatusService(isElevated: true));
    Assert(elevatedViewModel.IsElevated, "fake elevated service should mark view model elevated");
    Assert(elevatedViewModel.ElevationStatusText == "Administrator: Yes",
        "elevated status text should be explicit");
    Assert(elevatedViewModel.ElevationHelpText.Contains("live confirmation", StringComparison.OrdinalIgnoreCase),
        "elevated help text should keep live confirmation visible");

    var nonElevatedViewModel = new MainWindowViewModel(
        elevationStatusService: new FakeElevationStatusService(isElevated: false));
    Assert(!nonElevatedViewModel.IsElevated, "fake non-elevated service should mark view model non-elevated");
    Assert(nonElevatedViewModel.ElevationStatusText == "Administrator: No",
        "non-elevated status text should be explicit");
    Assert(nonElevatedViewModel.ElevationHelpText.Contains("Run as administrator", StringComparison.Ordinal),
        "non-elevated help text should tell the user how to proceed");
    return Task.CompletedTask;
}

static async Task DryRunStartDoesNotRequestLiveConfirmationAsync()
{
    var viewModel = new MainWindowViewModel(
        elevationStatusService: new FakeElevationStatusService(isElevated: false));
    var confirmationCalls = 0;
    var startCalls = 0;

    var started = await viewModel.ExecuteStartWithLiveConfirmationAsync(
        () =>
        {
            confirmationCalls++;
            return false;
        },
        () =>
        {
            startCalls++;
            return Task.CompletedTask;
        });

    Assert(started, "dry-run start should continue");
    Assert(confirmationCalls == 0, "dry-run start should not request live confirmation");
    Assert(startCalls == 1, "dry-run start should invoke start callback");
}

static async Task LiveStartNonElevatedBlocksBeforeConfirmationAsync()
{
    var viewModel = new MainWindowViewModel(
        elevationStatusService: new FakeElevationStatusService(isElevated: false))
    {
        IsDryRun = false
    };
    var confirmationCalls = 0;
    var startCalls = 0;

    var started = await viewModel.ExecuteStartWithLiveConfirmationAsync(
        () =>
        {
            confirmationCalls++;
            return true;
        },
        () =>
        {
            startCalls++;
            return Task.CompletedTask;
        });

    Assert(!started, "non-elevated live start should be blocked");
    Assert(confirmationCalls == 0, "non-elevated live start should not continue to live confirmation");
    Assert(startCalls == 0, "non-elevated live start should not invoke start callback");
    Assert(viewModel.LastError == MainWindowViewModel.LiveStartRequiresAdministratorMessage,
        "non-elevated live start should surface administrator requirement");
    Assert(viewModel.DiagnosticsLog.Entries.Any(entry =>
            entry.Name == "host.start.blocked.elevation" &&
            entry.Message == MainWindowViewModel.LiveStartRequiresAdministratorMessage),
        "non-elevated live start should record visible diagnostics");
}

static async Task LiveStartConfirmedInvokesStartCallbackAsync()
{
    var viewModel = new MainWindowViewModel(
        elevationStatusService: new FakeElevationStatusService(isElevated: true))
    {
        IsDryRun = false
    };
    var confirmationCalls = 0;
    var startCalls = 0;

    var started = await viewModel.ExecuteStartWithLiveConfirmationAsync(
        () =>
        {
            confirmationCalls++;
            return true;
        },
        () =>
        {
            startCalls++;
            return Task.CompletedTask;
        });

    Assert(started, "confirmed live start should continue");
    Assert(confirmationCalls == 1, "live start should request confirmation once");
    Assert(startCalls == 1, "confirmed live start should invoke start callback");
    Assert(!viewModel.DiagnosticsLog.Entries.Any(entry => entry.Name == "host.start.cancelled"),
        "confirmed live start should not record cancellation diagnostic");
}

static async Task LiveStartCancelledSkipsStartAndRecordsDiagnosticAsync()
{
    var viewModel = new MainWindowViewModel(
        elevationStatusService: new FakeElevationStatusService(isElevated: true))
    {
        IsDryRun = false
    };
    var confirmationCalls = 0;
    var startCalls = 0;

    var started = await viewModel.ExecuteStartWithLiveConfirmationAsync(
        () =>
        {
            confirmationCalls++;
            return false;
        },
        () =>
        {
            startCalls++;
            return Task.CompletedTask;
        });

    Assert(!started, "cancelled live start should not continue");
    Assert(confirmationCalls == 1, "live start should request confirmation once");
    Assert(startCalls == 0, "cancelled live start should not invoke start callback");
    Assert(viewModel.LastError == MainWindowViewModel.LiveStartCancelledMessage,
        "cancelled live start should surface LastError");
    Assert(viewModel.DiagnosticsLog.Entries.Any(entry =>
            entry.Name == "host.start.cancelled" &&
            entry.Message == MainWindowViewModel.LiveStartCancelledMessage),
        "cancelled live start should record visible diagnostics");
}

static async Task LiveModeDoesNotChangeConfigSaveBehaviorAsync()
{
    var directory = CreateTempDirectory();
    var output = Path.Combine(directory, "live-mode-save.json");
    var viewModel = new MainWindowViewModel(new ConfigDocumentService())
    {
        IsDryRun = false,
        TargetProcess = "BlueArchive.exe"
    };
    viewModel.MappingEditor.Add(new MappingEditorRow
    {
        Trigger = "q",
        Type = "simple",
        Target = "esc",
        Mode = "tap"
    });

    await viewModel.SaveConfigAsync(
        output,
        viewModel.BuildConfigFromEditor(),
        createBackup: false,
        CancellationToken.None);

    using var json = JsonDocument.Parse(await File.ReadAllTextAsync(output));
    var root = json.RootElement;
    Assert(!root.TryGetProperty("dry_run", out _), "live mode should not add dry_run config field");
    Assert(!root.TryGetProperty("live_mode", out _), "live mode should not add live_mode config field");
    Assert(root.GetProperty("mappings").GetArrayLength() == 1, "config save should preserve mapping output");
}

static Task RuntimeControlCommandsRouteCallbacks()
{
    var viewModel = new MainWindowViewModel();
    var start = 0;
    var stop = 0;
    var reload = 0;
    var refresh = 0;
    var probe = 0;
    var down = 0;
    var up = 0;
    viewModel.ConfigureRuntimeCommands(
        () =>
        {
            start++;
            return Task.CompletedTask;
        },
        () =>
        {
            stop++;
            return Task.CompletedTask;
        },
        () =>
        {
            reload++;
            return Task.CompletedTask;
        },
        () => refresh++,
        () =>
        {
            probe++;
            return Task.CompletedTask;
        },
        () => down++,
        () => up++);

    viewModel.StartRuntimeCommand.Execute(null);
    viewModel.StopRuntimeCommand.Execute(null);
    viewModel.ReloadConfigCommand.Execute(null);
    viewModel.RefreshStatusCommand.Execute(null);
    viewModel.ProbeForegroundCommand.Execute(null);
    viewModel.SimulateDownCommand.Execute(null);
    viewModel.SimulateUpCommand.Execute(null);

    Assert(start == 1, "start runtime command should route callback");
    Assert(stop == 1, "stop runtime command should route callback");
    Assert(reload == 1, "reload config command should route callback");
    Assert(refresh == 1, "refresh status command should route callback");
    Assert(probe == 1, "foreground probe command should route callback");
    Assert(down == 1, "simulate down command should route callback");
    Assert(up == 1, "simulate up command should route callback");
    return Task.CompletedTask;
}

static Task InvalidConfigValidationReportsErrors()
{
    var viewModel = new MainWindowViewModel(new ConfigDocumentService());
    viewModel.MappingEditor.Add(new MappingEditorRow
    {
        Trigger = "q",
        Type = "simple",
        Target = "esc",
        Mode = "tap"
    });
    viewModel.MappingEditor.Add(new MappingEditorRow
    {
        Trigger = "w",
        Type = "simple",
        Target = "not_a_key_name",
        Mode = "tap"
    });

    var validation = viewModel.ValidateConfig(viewModel.BuildConfigFromEditor());
    Assert(!validation.Success, "invalid target should fail validation");
    Assert(validation.Errors.Count > 0, "expected validation errors");
    return Task.CompletedTask;
}

static Task ControlHotkeyConflictValidationSurfacesError()
{
    var viewModel = new MainWindowViewModel(new ConfigDocumentService())
    {
        Hotkey = "ctrl+shift+f12"
    };
    viewModel.MappingEditor.Add(new MappingEditorRow
    {
        Trigger = "ctrl",
        Type = "simple",
        Target = "esc",
        Mode = "tap"
    });

    var validation = viewModel.ValidateConfig(viewModel.BuildConfigFromEditor());

    Assert(!validation.Success, "control hotkey conflict should fail validation");
    Assert(validation.Errors.Any(error =>
            error.Contains("ctrl", StringComparison.OrdinalIgnoreCase) &&
            error.Contains("ctrl+shift+f12", StringComparison.OrdinalIgnoreCase) &&
            error.Contains("控制热键", StringComparison.OrdinalIgnoreCase)),
        "control hotkey conflict should surface visible error text");
    return Task.CompletedTask;
}

static async Task RuntimeControllerStartRejectsControlHotkeyConflictAsync()
{
    await using var controller = new RuntimeHostController();
    var config = new AppConfigV1
    {
        Hotkey = "f5",
        Mappings =
        [
            new MappingConfigV1
            {
                Trigger = "f5",
                Type = "simple",
                Target = "esc",
                Mode = "tap"
            }
        ]
    };

    var rejected = false;
    try
    {
        await controller.StartAsync(
            config,
            dryRun: true,
            allowAnyForeground: true,
            CancellationToken.None);
    }
    catch (InvalidOperationException ex)
    {
        rejected = ex.Message.Contains("f5", StringComparison.OrdinalIgnoreCase) &&
            ex.Message.Contains("控制热键", StringComparison.OrdinalIgnoreCase);
    }

    Assert(rejected, "runtime controller start should reject control hotkey conflict");
    Assert(!controller.IsRunning, "rejected start should not leave runtime running");
}

static async Task GuiSelfForegroundGateBlocksBakeysmithAppForeground()
{
    var gate = new GuiSelfForegroundGate(
        new FakeForegroundGate(true, "BAKeySmith.App", "Editor window"),
        ["BAKeySmith.App"]);

    var result = await gate.CheckAsync(RuntimeConfig.Empty, CancellationToken.None);

    Assert(!result.IsAllowed, "BAKeySmith.App foreground should be forced blocked");
    Assert(result.ForegroundProcess == "BAKeySmith.App", "foreground process should be preserved for diagnostics");
    Assert(result.ForegroundWindowTitle?.Contains("gui_self_foreground_blocked", StringComparison.Ordinal) == true,
        "blocked self foreground should include a diagnostic title suffix");
}

static async Task RuntimeControllerLivePathPassesSelfNamesToHookSourceAsync()
{
    IReadOnlyList<string>? receivedSelfNames = null;
    await using var controller = new RuntimeHostController(
        liveForegroundGateFactory: () => new FakeForegroundGate(true, "BlueArchive.exe", "Target window"),
        liveTriggerSourceFactory: selfNames =>
        {
            receivedSelfNames = selfNames.ToArray();
            return new ManualTriggerSource();
        },
        selfForegroundProcessNames: ["BAKeySmith.App", "BAKeySmith"]);

    await controller.StartAsync(
        CreateSingleTapConfig(),
        dryRun: false,
        allowAnyForeground: false,
        CancellationToken.None);

    if (receivedSelfNames is null)
    {
        throw new InvalidOperationException("live trigger source factory should receive self foreground names");
    }

    var selfNames = receivedSelfNames;
    Assert(selfNames.Contains("BAKeySmith.App"), "live hook wiring should include BAKeySmith.App self process name");
    Assert(selfNames.Contains("BAKeySmith"), "live hook wiring should include BAKeySmith self process name");
    Assert(controller.IsRunning, "controller should start with injected live trigger source");

    await controller.StopAsync(CancellationToken.None);
}

static Task KeyCaptureFormatterKeyboardNames()
{
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(Key.Q), "q");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(Key.D1), "1");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(Key.F8), "f8");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(Key.Escape), "escape");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(Key.Enter), "enter");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(Key.Space), "space");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(Key.Tab), "tab");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(Key.Back), "backspace");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(Key.Up), "arrow_up");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(Key.Down), "arrow_down");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(Key.Left), "arrow_left");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(Key.Right), "arrow_right");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(Key.Home), "home");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(Key.End), "end");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(Key.PageUp), "page_up");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(Key.PageDown), "page_down");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(Key.Insert), "insert");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(Key.Delete), "delete");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(Key.CapsLock), "caps_lock");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(Key.NumLock), "num_lock");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(Key.Scroll), "scroll_lock");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(Key.NumPad0), "numpad0");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(Key.Add), "add");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(Key.Subtract), "subtract");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(Key.Multiply), "multiply");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(Key.Divide), "divide");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(Key.Decimal), "decimal");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(Key.LeftCtrl), "ctrl");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(Key.RightCtrl), "ctrl");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(Key.LeftAlt), "alt");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(Key.RightAlt), "alt");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(Key.LeftShift), "shift");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(Key.RightShift), "shift");
    return Task.CompletedTask;
}

static Task KeyCaptureFormatterMouseButtons()
{
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(MouseButton.Left), "mouse_left");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(MouseButton.Right), "mouse_right");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(MouseButton.Middle), "mouse_middle");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(MouseButton.XButton1), "mouse_x1");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTrigger(MouseButton.XButton2), "mouse_x2");
    return Task.CompletedTask;
}

static Task KeyCaptureFormatterRejectsAmbiguousAndUnsupportedNames()
{
    var left = KeyCaptureFormatter.FormatMappingTrigger(Key.Left);
    var right = KeyCaptureFormatter.FormatMappingTrigger(Key.Right);
    Assert(left.Value == "arrow_left", "left arrow capture must not output ambiguous bare left");
    Assert(right.Value == "arrow_right", "right arrow capture must not output ambiguous bare right");

    Assert(KeyCaptureFormatter.FormatMappingTrigger(Key.LWin).Kind == KeyCaptureResultKind.Unsupported,
        "focused capture v1 should not pretend to support Windows key capture");
    Assert(KeyCaptureFormatter.FormatMappingTrigger(Key.PrintScreen).Kind == KeyCaptureResultKind.Unsupported,
        "focused capture v1 should not pretend to support PrintScreen capture");
    Assert(KeyCaptureFormatter.FormatMappingTrigger(Key.Apps).Kind == KeyCaptureResultKind.Unsupported,
        "focused capture v1 should not pretend to support Apps/Menu capture");
    return Task.CompletedTask;
}

static Task HotkeyCaptureFormatterCtrlShiftF12()
{
    var result = KeyCaptureFormatter.FormatHotkey(
        Key.F12,
        ModifierKeys.Control | ModifierKeys.Shift);

    AssertCaptured(result, "ctrl+shift+f12");
    return Task.CompletedTask;
}

static Task HotkeyCaptureFormatterDoesNotCompleteOnModifierOnly()
{
    var result = KeyCaptureFormatter.FormatHotkey(Key.LeftCtrl, ModifierKeys.Control);
    Assert(result.Kind == KeyCaptureResultKind.Incomplete,
        "hotkey capture should wait for a non-modifier key");
    Assert(string.IsNullOrWhiteSpace(result.Value),
        "modifier-only hotkey capture should not emit a config value");
    return Task.CompletedTask;
}

static Task MappingTriggerCaptureUpdatesEditor()
{
    var viewModel = new MainWindowViewModel();
    Assert(viewModel.BeginMappingTriggerCapture(), "mapping capture should start while runtime is stopped");

    var applied = viewModel.ApplyKeyCaptureResult(KeyCaptureFormatter.FormatMappingTrigger(Key.Escape));

    Assert(applied, "mapping capture should apply captured key");
    Assert(viewModel.MappingEditor.EditingTrigger == "escape", "mapping trigger capture should update editor");
    Assert(!viewModel.IsCapturingKey, "successful capture should exit capture mode");
    return Task.CompletedTask;
}

static Task HotkeyCaptureUpdatesHotkeyAfterNonModifier()
{
    var viewModel = new MainWindowViewModel();
    Assert(viewModel.BeginHotkeyCapture(), "hotkey capture should start while runtime is stopped");

    var incomplete = viewModel.ApplyKeyCaptureResult(
        KeyCaptureFormatter.FormatHotkey(Key.LeftCtrl, ModifierKeys.Control));

    Assert(!incomplete, "modifier-only hotkey capture should not complete");
    Assert(viewModel.IsCapturingHotkey, "modifier-only hotkey capture should remain active");

    var applied = viewModel.ApplyKeyCaptureResult(
        KeyCaptureFormatter.FormatHotkey(Key.F12, ModifierKeys.Control | ModifierKeys.Shift));

    Assert(applied, "hotkey capture should apply non-modifier key");
    Assert(viewModel.Hotkey == "ctrl+shift+f12", "hotkey capture should update hotkey text");
    Assert(!viewModel.IsCapturingKey, "successful hotkey capture should exit capture mode");
    return Task.CompletedTask;
}

static Task RunningCaptureAllowsMappingTriggerHotkeyAndTarget()
{
    var viewModel = new MainWindowViewModel
    {
        IsRunning = true
    };

    Assert(viewModel.BeginMappingTriggerCapture(), "running runtime should allow mapping trigger capture");
    Assert(viewModel.IsCapturingMappingTrigger, "mapping trigger capture should enter capture mode while running");
    Assert(viewModel.KeyCaptureStatusText.Contains(MainWindowViewModel.KeyCaptureRunningSafeMessage, StringComparison.Ordinal),
        "running capture status should explain GUI foreground safety");
    viewModel.CancelKeyCapture();

    Assert(viewModel.BeginHotkeyCapture(), "running runtime should allow hotkey capture");
    Assert(viewModel.IsCapturingHotkey, "hotkey capture should enter capture mode while running");
    viewModel.CancelKeyCapture();

    Assert(viewModel.BeginMappingTargetCapture(), "running runtime should allow simple target capture");
    Assert(viewModel.IsCapturingMappingTarget, "target capture should enter capture mode while running");
    return Task.CompletedTask;
}

static Task MappingTargetCaptureUpdatesEditor()
{
    var viewModel = new MainWindowViewModel();
    Assert(viewModel.BeginMappingTargetCapture(), "target capture should start for simple mappings");

    var applied = viewModel.ApplyKeyCaptureResult(KeyCaptureFormatter.FormatMappingTarget(Key.Escape));

    Assert(applied, "target capture should apply captured key");
    Assert(viewModel.MappingEditor.EditingTarget == "escape", "target capture should update simple target editor");
    Assert(!viewModel.IsCapturingKey, "successful target capture should exit capture mode");
    return Task.CompletedTask;
}

static Task MappingTargetCaptureFormatterKeyboardAndMouseNames()
{
    AssertCaptured(KeyCaptureFormatter.FormatMappingTarget(Key.Escape), "escape");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTarget(Key.LeftCtrl), "ctrl");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTarget(Key.Left), "arrow_left");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTarget(Key.Right), "arrow_right");
    AssertCaptured(KeyCaptureFormatter.FormatMappingTarget(MouseButton.Right), "mouse_right");

    var viewModel = new MainWindowViewModel(new ConfigDocumentService());
    viewModel.MappingEditor.EditingTrigger = "q";
    viewModel.MappingEditor.EditingType = "simple";
    viewModel.MappingEditor.EditingTarget = KeyCaptureFormatter.FormatMappingTarget(MouseButton.Right).Value;
    viewModel.MappingEditor.EditingMode = "tap";
    viewModel.MappingEditor.AddCommand.Execute(null);

    var validation = viewModel.ValidateConfig(viewModel.BuildConfigFromEditor());
    Assert(validation.Success, "target capture output should be accepted by Core config validation");
    Assert(!KeyCaptureFormatter.FormatMappingTarget(Key.Left).Value.Equals("left", StringComparison.OrdinalIgnoreCase),
        "target capture must not output ambiguous bare left");
    return Task.CompletedTask;
}

static Task MappingTargetCaptureRejectsWheel()
{
    var viewModel = new MainWindowViewModel();
    Assert(viewModel.BeginMappingTargetCapture(), "target capture should start before wheel rejection");

    var result = KeyCaptureFormatter.FormatMappingTargetWheel();
    var applied = viewModel.ApplyKeyCaptureResult(result);

    Assert(result.Kind == KeyCaptureResultKind.Unsupported, "simple target capture should not accept wheel");
    Assert(!applied, "unsupported wheel capture should not apply to target");
    Assert(viewModel.IsCapturingMappingTarget, "unsupported capture should keep target capture active");
    Assert(viewModel.MappingEditor.EditingTarget != "mouse_wheel_up" &&
        viewModel.MappingEditor.EditingTarget != "mouse_wheel_down",
        "target capture should not write wheel names");
    return Task.CompletedTask;
}

static Task MacroCompletionPolicyEmptyCommandPositionOpensCommands()
{
    var decision = CompletionDecision(string.Empty);

    Assert(decision.ShouldOpen, "empty command position should show command candidates");
    Assert(decision.Context.IsCommandPosition, "empty script should be command position");
    Assert(CompletionItems(string.Empty).Any(item => item.Text == "tap"), "command candidates should include tap");
    return Task.CompletedTask;
}

static Task MacroCompletionPolicyPrefixAndFullCommand()
{
    var prefix = CompletionDecision("ta");
    Assert(prefix.ShouldOpen, "command prefix should show matching commands");
    Assert(CompletionItems("ta").Single().Text == "tap", "ta should complete to tap");

    var full = CompletionDecision("tap");
    Assert(!full.ShouldOpen, "complete unique command should close popup");
    return Task.CompletedTask;
}

static Task MacroCompletionPolicyArgumentSlots()
{
    var loopSlot = CompletionDecision("loop ");
    Assert(loopSlot.ShouldOpen, "loop argument slot should show argument candidates");
    var loopItems = CompletionItems("loop ");
    Assert(loopItems.Any(item => item.Text == "0"), "loop slot should include 0");
    Assert(loopItems.Any(item => item.Text == "infinite"), "loop slot should include infinite");

    var loopPrefix = CompletionDecision("loop i");
    Assert(loopPrefix.ShouldOpen, "loop argument prefix should show matching candidates");
    Assert(CompletionItems("loop i").Any(item => item.Text == "infinite"), "loop i should include infinite");

    var loopComplete = CompletionDecision("loop 0");
    Assert(!loopComplete.ShouldOpen, "complete unique loop argument should close popup");

    var tapSlot = CompletionDecision("tap ");
    Assert(tapSlot.ShouldOpen, "tap argument slot should show key candidates");
    var tapPrefix = CompletionDecision("tap e");
    Assert(tapPrefix.ShouldOpen, "tap key prefix should show matching key candidates");
    Assert(CompletionItems("tap e").Any(item => item.Text == "escape"), "tap e should include escape");
    return Task.CompletedTask;
}

static Task MacroCompletionPolicySuppressesCompletionApplyReopen()
{
    var decision = CompletionDecision("lo", suppressOpen: true);

    Assert(!decision.ShouldOpen, "completion apply suppression should prevent immediate popup reopen");
    return Task.CompletedTask;
}

static Task MacroCompletionPolicyEscDismissedContextDoesNotReopen()
{
    var initial = CompletionDecision("ta");
    Assert(initial.ShouldOpen, "test setup should produce an open popup decision");

    var dismissed = CompletionDecision("ta", dismissedContext: initial.Context);
    Assert(!dismissed.ShouldOpen, "Esc-dismissed same context should not immediately reopen");

    var changed = CompletionDecision("tap ");
    Assert(changed.ShouldOpen, "new completion context should be allowed after dismissal");
    return Task.CompletedTask;
}

static Task ValidMacroScriptUpdatesDiagnosticsToValid()
{
    var macro = new MacroDiagnosticsViewModel();
    var result = macro.Validate("tap esc");

    Assert(result.Success, "valid macro should compile");
    Assert(macro.IsActive, "macro diagnostics should be active after validation");
    Assert(macro.IsValid, "valid macro should update view model to valid");
    Assert(macro.InstructionCount == 1, "valid macro should expose instruction count");
    Assert(macro.DiagnosticCount == 0, "valid macro should have no diagnostics");
    Assert(macro.Summary.Contains("macro valid", StringComparison.OrdinalIgnoreCase),
        "valid macro should expose valid summary");
    return Task.CompletedTask;
}

static Task InvalidMacroScriptUpdatesDiagnosticsToInvalid()
{
    var macro = new MacroDiagnosticsViewModel();
    var result = macro.Validate("loop 2\ntap\nend");

    Assert(!result.Success, "invalid macro should fail");
    Assert(macro.IsActive, "macro diagnostics should be active after validation");
    Assert(!macro.IsValid, "invalid macro should update view model to invalid");
    Assert(macro.DiagnosticCount > 0, "invalid macro should expose diagnostics");
    Assert(!string.IsNullOrWhiteSpace(macro.FirstError), "invalid macro should expose first error");
    Assert(macro.Summary.Contains("macro invalid", StringComparison.OrdinalIgnoreCase),
        "invalid macro should expose invalid summary");
    return Task.CompletedTask;
}

static Task MacroCompilerDiagnosticsDisplayModel()
{
    var macro = new MacroDiagnosticsViewModel();
    var result = macro.Validate("loop 2\ntap\nend");
    Assert(!result.Success, "invalid macro should fail");
    Assert(!macro.IsValid, "view model should record invalid macro");
    Assert(macro.Diagnostics.Any(item => item.Line == 2), "expected line 2 diagnostic");
    Assert(macro.Diagnostics.Any(item => !string.IsNullOrWhiteSpace(item.Message)),
        "expected diagnostic message from compiler");
    Assert(macro.Diagnostics.Any(item => item.Display.Contains("line 2", StringComparison.OrdinalIgnoreCase)),
        "expected display text with line number");
    return Task.CompletedTask;
}

static Task MappingEditorMacroScriptChangeUpdatesDiagnosticsDisplayModel()
{
    var viewModel = new MainWindowViewModel();
    viewModel.MappingEditor.EditingType = "macro";
    viewModel.MappingEditor.EditingScript = "tap esc";

    Assert(viewModel.MacroDiagnostics.IsActive, "macro diagnostics should activate for macro mapping");
    Assert(viewModel.MacroDiagnostics.IsValid, "valid editor script should update diagnostics to valid");
    Assert(viewModel.MacroDiagnostics.InstructionCount == 1, "valid editor script should expose instruction count");

    viewModel.MappingEditor.EditingScript = "tap";

    Assert(!viewModel.MacroDiagnostics.IsValid, "invalid editor script should update diagnostics to invalid");
    Assert(viewModel.MacroDiagnostics.Diagnostics.Any(item => item.Line > 0),
        "invalid editor script should expose line diagnostics");
    return Task.CompletedTask;
}

static Task SimpleMappingDoesNotRequireMacroDiagnosticsForSave()
{
    var viewModel = new MainWindowViewModel(new ConfigDocumentService());
    viewModel.MappingEditor.EditingType = "simple";
    viewModel.MappingEditor.EditingTrigger = "q";
    viewModel.MappingEditor.EditingTarget = "esc";
    viewModel.MappingEditor.EditingMode = "tap";
    viewModel.MappingEditor.EditingScript = "tap";
    viewModel.MappingEditor.AddCommand.Execute(null);

    var validation = viewModel.ValidateConfig(viewModel.BuildConfigFromEditor());

    Assert(validation.Success, "simple mapping should not be blocked by invalid macro editor text");
    Assert(!viewModel.MacroDiagnostics.IsActive, "simple mapping should keep macro diagnostics inactive");
    return Task.CompletedTask;
}

static Task MappingEditorDirtyState()
{
    var editor = new MappingEditorViewModel();
    editor.LoadFromConfig(new AppConfigV1());
    Assert(!editor.IsDirty, "fresh load should be clean");
    editor.Add(new MappingEditorRow
    {
        Trigger = "q",
        Type = "simple",
        Target = "esc",
        Mode = "tap"
    });
    Assert(editor.IsDirty, "add should mark dirty");
    editor.MarkClean();
    Assert(!editor.IsDirty, "mark clean failed");
    editor.Update(editor.Mappings[0], new MappingEditorRow
    {
        Trigger = "q",
        Type = "macro",
        Script = "tap esc"
    });
    Assert(editor.IsDirty, "update should mark dirty");
    return Task.CompletedTask;
}

static async Task BackupBeforeSaveWritesPreviousContentAsync()
{
    var directory = CreateTempDirectory();
    var path = Path.Combine(directory, "config.json");
    await File.WriteAllTextAsync(path, "{\"version\":1,\"root_unknown\":\"old\"}");

    var service = new ConfigDocumentService();
    await service.SaveAsync(
        path,
        new AppConfigV1
        {
            TargetProcess = "BlueArchive.exe",
            Mappings =
            [
                new MappingConfigV1
                {
                    Trigger = "q",
                    Type = "simple",
                    Target = "esc",
                    Mode = "tap"
                }
            ]
        },
        createBackup: true,
        CancellationToken.None);

    var backup = await File.ReadAllTextAsync($"{path}.bak");
    Assert(backup.Contains("root_unknown", StringComparison.Ordinal), "backup should contain previous content");
}

static async Task RuntimeReloadRejectsInvalidConfigAndKeepsPreviousRuntimeConfigAsync()
{
    await using var controller = new RuntimeHostController();
    var validConfig = new AppConfigV1
    {
        Hotkey = "ctrl+shift+f12",
        Mappings =
        [
            new MappingConfigV1
            {
                Trigger = "q",
                Type = "simple",
                Target = "esc",
                Mode = "tap"
            }
        ]
    };
    var invalidConfig = validConfig with
    {
        Hotkey = "f5",
        Mappings =
        [
            new MappingConfigV1
            {
                Trigger = "f5",
                Type = "simple",
                Target = "enter",
                Mode = "tap"
            }
        ]
    };

    await controller.StartAsync(validConfig, dryRun: true, allowAnyForeground: true, CancellationToken.None);
    var rejected = false;
    try
    {
        await controller.ReloadAsync(invalidConfig, CancellationToken.None);
    }
    catch (InvalidOperationException ex)
    {
        rejected = ex.Message.Contains("控制热键", StringComparison.OrdinalIgnoreCase) &&
            ex.Message.Contains("f5", StringComparison.OrdinalIgnoreCase);
    }

    Assert(rejected, "invalid running reload should be rejected by validation");
    Assert(controller.IsRunning, "rejected reload should keep runtime running");
    Assert(controller.RuntimeConfig?.Mappings.Single().Trigger.NormalizedCode == "q",
        "rejected reload should keep previous runtime mapping active");
    await controller.StopAsync(CancellationToken.None);
}

static async Task RuntimeControllerStartStopReloadDryWiringAsync()
{
    await using var controller = new RuntimeHostController();
    var config = new AppConfigV1
    {
        Mappings =
        [
            new MappingConfigV1
            {
                Trigger = "q",
                Type = "simple",
                Target = "esc",
                Mode = "tap"
            }
        ]
    };

    await controller.StartAsync(config, dryRun: true, allowAnyForeground: true, CancellationToken.None);
    Assert(controller.IsRunning, "controller should be running");
    controller.Simulate("q", "down");
    await WaitUntilAsync(() => controller.InputEventCount >= 2, "dry-run output after simulated trigger");

    await controller.ReloadAsync(config with { Hotkey = "ctrl+shift+f11" }, CancellationToken.None);
    Assert(controller.Snapshot()?.IsStarted == true, "snapshot should remain started after reload");

    await controller.StopAsync(CancellationToken.None);
    Assert(!controller.IsRunning, "controller should stop cleanly");
    Assert(controller.Snapshot() is null, "snapshot should be null after stop/dispose");
}

static async Task RuntimeControllerBlocksDispatchWhenForegroundIsAppAsync()
{
    await using var controller = new RuntimeHostController(
        dryRunForegroundGateFactory: _ => new FakeForegroundGate(true, "BAKeySmith.App", "Editor window"),
        selfForegroundProcessNames: ["BAKeySmith.App"]);

    await controller.StartAsync(CreateSingleTapConfig(), dryRun: true, allowAnyForeground: true, CancellationToken.None);
    controller.Simulate("q", "down");
    await WaitUntilAsync(
        () => controller.Snapshot()?.Pipeline.HandledCount >= 1,
        "blocked self-foreground trigger handled");

    Assert(controller.InputEventCount == 0, "self foreground should block mapping output");
    Assert(controller.Snapshot()?.Runtime.LastForegroundAllowed == false,
        "runtime snapshot should record self foreground as blocked");

    await controller.StopAsync(CancellationToken.None);
}

static async Task RuntimeControllerAllowsDispatchWhenForegroundIsTargetAsync()
{
    await using var controller = new RuntimeHostController(
        dryRunForegroundGateFactory: _ => new FakeForegroundGate(true, "BlueArchive.exe", "Target window"),
        selfForegroundProcessNames: ["BAKeySmith.App"]);

    await controller.StartAsync(CreateSingleTapConfig(), dryRun: true, allowAnyForeground: true, CancellationToken.None);
    controller.Simulate("q", "down");
    await WaitUntilAsync(() => controller.InputEventCount >= 2, "allowed foreground output");

    Assert(controller.Snapshot()?.Runtime.LastForegroundAllowed == true,
        "runtime snapshot should record target foreground as allowed");

    await controller.StopAsync(CancellationToken.None);
}

static AppConfigV1 CreateSingleTapConfig()
{
    return new AppConfigV1
    {
        Mappings =
        [
            new MappingConfigV1
            {
                Trigger = "q",
                Type = "simple",
                Target = "esc",
                Mode = "tap"
            }
        ]
    };
}

static MacroCompletionPopupDecision CompletionDecision(
    string script,
    MacroCompletionPopupContext? dismissedContext = null,
    bool suppressOpen = false)
{
    var provider = new MacroScriptCompletionProvider();
    var completion = provider.Complete(script, script.Length);
    return MacroCompletionPopupPolicy.Evaluate(
        script,
        script.Length,
        completion,
        dismissedContext,
        suppressOpen);
}

static IReadOnlyList<MacroScriptCompletionItem> CompletionItems(string script)
{
    var provider = new MacroScriptCompletionProvider();
    return provider.Complete(script, script.Length).Items;
}

static void AssertCaptured(KeyCaptureResult result, string expected)
{
    Assert(result.Kind == KeyCaptureResultKind.Captured, $"expected captured result for {expected}, got {result.Kind}: {result.Message}");
    Assert(result.Value == expected, $"expected captured value {expected}, got {result.Value}");
}

static async Task WaitUntilAsync(Func<bool> condition, string description)
{
    var deadline = DateTime.UtcNow.AddSeconds(3);
    while (DateTime.UtcNow < deadline)
    {
        if (condition())
        {
            return;
        }

        await Task.Delay(20);
    }

    throw new InvalidOperationException($"Timed out waiting for {description}.");
}

static string CreateTempDirectory()
{
    var path = Path.Combine(Path.GetTempPath(), $"bakeysmith-app-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(path);
    return path;
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

sealed class FakeElevationStatusService(bool isElevated) : IElevationStatusService
{
    public bool IsElevated { get; set; } = isElevated;
}

sealed class FakeForegroundGate(
    bool isAllowed,
    string foregroundProcess,
    string foregroundWindowTitle) : IForegroundGate
{
    public ValueTask<ForegroundGateResult> CheckAsync(
        RuntimeConfig config,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new ForegroundGateResult(
            isAllowed,
            config.TargetProcess,
            foregroundProcess,
            foregroundWindowTitle));
    }
}
