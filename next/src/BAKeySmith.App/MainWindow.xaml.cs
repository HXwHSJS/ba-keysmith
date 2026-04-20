using System.IO;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using BAKeySmith.Core.Configuration;
using BAKeySmith.Core.Contracts;
using BAKeySmith.Core.Diagnostics;
using BAKeySmith.Core.Foreground;
using BAKeySmith.Core.Hosting;
using BAKeySmith.Core.Input;
using BAKeySmith.Core.Triggers;
using Microsoft.Win32;

namespace BAKeySmith.App;

public partial class MainWindow : Window
{
    private readonly EventDiagnosticsSink _diagnostics = new();
    private readonly ObservableCollection<MappingEditorRow> _mappingRows = new();
    private readonly DispatcherTimer _statusTimer;
    private RuntimeHost? _host;
    private ManualTriggerSource? _manualSource;
    private DryRunInputBackend? _dryRunBackend;
    private AppConfigV1 _currentConfig = new();
    private bool _isRunning;

    public MainWindow()
    {
        InitializeComponent();
        ConfigPathBox.Text = FindDefaultConfigPath();
        MappingsGrid.ItemsSource = _mappingRows;
        _diagnostics.Emitted += Diagnostics_Emitted;
        _statusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _statusTimer.Tick += (_, _) => UpdateStats();
        _statusTimer.Start();
        UpdateModeVisuals();
        LoadConfigIntoEditor(logResult: false);
        AppendLog("app.ready", "GUI shell initialized.");
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        await RunGuardedAsync(async () =>
        {
            await StopHostAsync();

            var appConfig = BuildConfigFromEditor(validate: true);

            var dryRun = DryRunCheck.IsChecked == true;
            IInputBackend inputBackend;
            IForegroundGate foregroundGate;
            ITriggerSource triggerSource;
            if (dryRun)
            {
                _dryRunBackend = new DryRunInputBackend();
                _manualSource = new ManualTriggerSource();
                inputBackend = _dryRunBackend;
                foregroundGate = AllowForegroundCheck.IsChecked == true
                    ? AlwaysForegroundGate.Instance
                    : new ManualForegroundGate { IsAllowed = true };
                triggerSource = _manualSource;
            }
            else
            {
                _dryRunBackend = null;
                _manualSource = null;
                inputBackend = new WindowsInputBackend();
                foregroundGate = new WindowsForegroundGate();
                triggerSource = new WindowsHookTriggerSource();
            }

            _host = new RuntimeHost(
                appConfig,
                inputBackend,
                foregroundGate,
                triggerSource,
                _diagnostics);
            await _host.StartAsync(CancellationToken.None);
            _isRunning = true;
            SetRunningState(true);
            UpdateStats();
        });
    }

    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        await RunGuardedAsync(StopHostAsync);
    }

    private async void Reload_Click(object sender, RoutedEventArgs e)
    {
        await RunGuardedAsync(async () =>
        {
            var appConfig = BuildConfigFromEditor(validate: true);

            if (_host is null || !_isRunning)
            {
                var runtimeConfig = AppConfigSerializer.ToRuntimeConfig(appConfig, [], []);
                AppendLog("host.reload.skipped", "Runtime is not running; config will load on next start.");
                MappingCountText.Text = runtimeConfig.Mappings.Count.ToString();
                TargetText.Text = runtimeConfig.TargetProcess;
                return;
            }

            await _host.ReloadAsync(appConfig, CancellationToken.None);
            UpdateStats();
        });
    }

    private void BrowseConfig_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON config (*.json)|*.json|All files (*.*)|*.*",
            FileName = Path.GetFileName(ConfigPathBox.Text),
            InitialDirectory = Directory.Exists(Path.GetDirectoryName(ConfigPathBox.Text))
                ? Path.GetDirectoryName(ConfigPathBox.Text)
                : Environment.CurrentDirectory
        };

        if (dialog.ShowDialog(this) == true)
        {
            ConfigPathBox.Text = dialog.FileName;
            LoadConfigIntoEditor(logResult: true);
        }
    }

    private void LoadConfig_Click(object sender, RoutedEventArgs e)
    {
        RunGuardedSync(() => LoadConfigIntoEditor(logResult: true));
    }

    private async void SaveConfig_Click(object sender, RoutedEventArgs e)
    {
        await RunGuardedAsync(async () =>
        {
            var config = BuildConfigFromEditor(validate: true);
            var path = ConfigPathBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidOperationException("请先选择配置文件路径。");
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(path, AppConfigSerializer.Save(config));
            _currentConfig = config;
            AppendLog("config.saved", path);

            if (_host is not null && _isRunning)
            {
                await _host.ReloadAsync(config, CancellationToken.None);
                AppendLog("host.reloaded", "Runtime reloaded from saved config.");
            }

            UpdateStats();
        });
    }

    private void SimulateDown_Click(object sender, RoutedEventArgs e)
    {
        Simulate("down");
    }

    private void SimulateUp_Click(object sender, RoutedEventArgs e)
    {
        Simulate("up");
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e)
    {
        DiagnosticsList.Items.Clear();
    }

    private void RefreshStatus_Click(object sender, RoutedEventArgs e)
    {
        UpdateStats();
        AppendLog("snapshot.refreshed", "Runtime snapshot refreshed.");
    }

    private async void ProbeForeground_Click(object sender, RoutedEventArgs e)
    {
        await RunGuardedAsync(async () =>
        {
            var result = _host is not null
                ? await _host.CheckForegroundAsync(CancellationToken.None)
                : await new WindowsForegroundGate().CheckAsync(
                    new RuntimeConfig(
                        string.IsNullOrWhiteSpace(TargetProcessBox.Text)
                            ? "BlueArchive.exe"
                            : TargetProcessBox.Text.Trim(),
                        ParseTapHoldMilliseconds(),
                        Array.Empty<MappingDefinition>()),
                    CancellationToken.None);

            ForegroundProbeText.Text =
                $"allowed={result.IsAllowed} target={result.TargetProcess ?? "-"} foreground={result.ForegroundProcess ?? "-"} title={result.ForegroundWindowTitle ?? "-"}";
            ForegroundStateText.Text = result.IsAllowed ? "allowed" : "blocked";
            AppendLog("foreground.probe",
                $"allowed={result.IsAllowed} target={result.TargetProcess ?? "-"} foreground={result.ForegroundProcess ?? "-"}");
        });
    }

    private void ModeCheck_Changed(object sender, RoutedEventArgs e)
    {
        UpdateModeVisuals();
    }

    private void MappingsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MappingsGrid.SelectedItem is MappingEditorRow row)
        {
            FillMappingForm(row);
        }
    }

    private void MappingTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateMappingFormVisibility();
    }

    private void AddMapping_Click(object sender, RoutedEventArgs e)
    {
        RunGuardedSync(() =>
        {
            var row = ReadMappingForm();
            if (_mappingRows.Any(existing =>
                    string.Equals(existing.Trigger, row.Trigger, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"触发键重复: {row.Trigger}");
            }

            _mappingRows.Add(row);
            MappingsGrid.SelectedItem = row;
            UpdateStats();
        });
    }

    private void UpdateMapping_Click(object sender, RoutedEventArgs e)
    {
        RunGuardedSync(() =>
        {
            if (MappingsGrid.SelectedItem is not MappingEditorRow selected)
            {
                throw new InvalidOperationException("请先选择要更新的映射。");
            }

            var row = ReadMappingForm();
            if (_mappingRows.Any(existing =>
                    !ReferenceEquals(existing, selected) &&
                    string.Equals(existing.Trigger, row.Trigger, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"触发键重复: {row.Trigger}");
            }

            var index = _mappingRows.IndexOf(selected);
            _mappingRows[index] = row;
            MappingsGrid.SelectedItem = row;
            UpdateStats();
        });
    }

    private void RemoveMapping_Click(object sender, RoutedEventArgs e)
    {
        RunGuardedSync(() =>
        {
            if (MappingsGrid.SelectedItem is not MappingEditorRow selected)
            {
                throw new InvalidOperationException("请先选择要删除的映射。");
            }

            _mappingRows.Remove(selected);
            ClearMappingForm();
            UpdateStats();
        });
    }

    private void ClearMappingForm_Click(object sender, RoutedEventArgs e)
    {
        ClearMappingForm();
    }

    protected override async void OnClosed(EventArgs e)
    {
        _statusTimer.Stop();
        _diagnostics.Emitted -= Diagnostics_Emitted;
        await StopHostAsync();
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }

        base.OnClosed(e);
    }

    private async Task StopHostAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync(CancellationToken.None);
            await _host.DisposeAsync();
            _host = null;
        }

        _isRunning = false;
        _manualSource = null;
        _dryRunBackend = null;
        SetRunningState(false);
        UpdateStats();
    }

    private void LoadConfigIntoEditor(bool logResult)
    {
        var path = ConfigPathBox.Text.Trim();
        if (!File.Exists(path))
        {
            _currentConfig = new AppConfigV1();
            TargetProcessBox.Text = _currentConfig.TargetProcess;
            HotkeyBox.Text = _currentConfig.Hotkey;
            TapHoldBox.Text = _currentConfig.TapHoldMilliseconds.ToString("0.###");
            _mappingRows.Clear();
            if (logResult)
            {
                AppendLog("config.default", $"Config does not exist; editing an empty config: {path}");
            }

            UpdateStats();
            return;
        }

        var result = AppConfigSerializer.Parse(File.ReadAllText(path));
        if (!result.Success)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Errors));
        }

        _currentConfig = result.Config;
        TargetProcessBox.Text = _currentConfig.TargetProcess;
        HotkeyBox.Text = _currentConfig.Hotkey;
        TapHoldBox.Text = _currentConfig.TapHoldMilliseconds.ToString("0.###");
        _mappingRows.Clear();
        foreach (var mapping in _currentConfig.Mappings)
        {
            _mappingRows.Add(MappingEditorRow.FromConfig(mapping));
        }

        foreach (var warning in result.Warnings)
        {
            AppendLog("config.warning", warning);
        }

        if (logResult)
        {
            AppendLog("config.loaded", $"{path} mappings={_mappingRows.Count}");
        }

        ClearMappingForm();
        UpdateStats();
    }

    private AppConfigV1 BuildConfigFromEditor(bool validate)
    {
        var config = _currentConfig with
        {
            Version = 1,
            TargetProcess = string.IsNullOrWhiteSpace(TargetProcessBox.Text)
                ? "BlueArchive.exe"
                : TargetProcessBox.Text.Trim(),
            Hotkey = string.IsNullOrWhiteSpace(HotkeyBox.Text)
                ? "ctrl+shift+f12"
                : HotkeyBox.Text.Trim(),
            TapHoldMilliseconds = ParseTapHoldMilliseconds(),
            Mappings = _mappingRows.Select(row => row.ToConfig()).ToArray()
        };

        if (validate)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            _ = AppConfigSerializer.ToRuntimeConfig(config, errors, warnings);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
            }

            foreach (var warning in warnings)
            {
                AppendLog("config.warning", warning);
            }
        }

        return config;
    }

    private double ParseTapHoldMilliseconds()
    {
        if (!double.TryParse(TapHoldBox.Text.Trim(), out var tapHold) || tapHold < 0)
        {
            throw new InvalidOperationException("Tap 持续时间必须是非负数字。");
        }

        return tapHold;
    }

    private MappingEditorRow ReadMappingForm()
    {
        var trigger = MappingTriggerBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(trigger))
        {
            throw new InvalidOperationException("触发键不能为空。");
        }

        var type = ComboText(MappingTypeCombo, "simple");
        var mode = ComboText(MappingModeCombo, "hold");
        if (type == "macro")
        {
            var script = MacroEditor.ScriptText;
            var result = MacroEditor.ValidateScript();
            if (!result.Success)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, result.Errors));
            }

            return new MappingEditorRow
            {
                Trigger = trigger,
                Type = "macro",
                Mode = string.Empty,
                Target = string.Empty,
                Script = script
            };
        }

        var target = MappingTargetBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new InvalidOperationException("simple 映射需要目标键。");
        }

        return new MappingEditorRow
        {
            Trigger = trigger,
            Type = "simple",
            Mode = mode,
            Target = target,
            Script = string.Empty
        };
    }

    private void FillMappingForm(MappingEditorRow row)
    {
        MappingTriggerBox.Text = row.Trigger;
        MappingTargetBox.Text = row.Target;
        MacroEditor.ScriptText = string.IsNullOrWhiteSpace(row.Script)
            ? "loop 2\ntap esc\nend"
            : row.Script;
        SetComboText(MappingTypeCombo, string.IsNullOrWhiteSpace(row.Type) ? "simple" : row.Type);
        SetComboText(MappingModeCombo, string.IsNullOrWhiteSpace(row.Mode) ? "hold" : row.Mode);
        UpdateMappingFormVisibility();
    }

    private void ClearMappingForm()
    {
        MappingsGrid.SelectedItem = null;
        MappingTriggerBox.Text = "q";
        MappingTargetBox.Text = "1";
        MacroEditor.ScriptText = "loop 2\ntap esc\nend";
        SetComboText(MappingTypeCombo, "simple");
        SetComboText(MappingModeCombo, "hold");
        UpdateMappingFormVisibility();
    }

    private void UpdateMappingFormVisibility()
    {
        if (MappingTargetBox is null || MacroEditor is null)
        {
            return;
        }

        var macro = ComboText(MappingTypeCombo, "simple") == "macro";
        MappingTargetBox.IsEnabled = !macro;
        MappingModeCombo.IsEnabled = !macro;
        MacroEditor.IsEnabled = macro;
    }

    private void Simulate(string phase)
    {
        if (_manualSource is null || !_isRunning)
        {
            AppendLog("simulate.ignored", "Dry-run runtime is not running.");
            return;
        }

        var code = SimulateCodeBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            AppendLog("simulate.ignored", "Empty trigger code.");
            return;
        }

        var isMouse = code.StartsWith("mouse_", StringComparison.OrdinalIgnoreCase);
        if (phase == "down")
        {
            if (isMouse)
            {
                _manualSource.MouseDown(code);
            }
            else
            {
                _manualSource.KeyDown(code);
            }
        }
        else
        {
            if (isMouse)
            {
                _manualSource.MouseUp(code);
            }
            else
            {
                _manualSource.KeyUp(code);
            }
        }
    }

    private async Task RunGuardedAsync(Func<Task> action)
    {
        try
        {
            SetBusy(true);
            await action();
        }
        catch (Exception ex)
        {
            AppendLog("error", ex.Message);
            MessageBox.Show(this, ex.Message, "BA KeySmith Next", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
            UpdateStats();
        }
    }

    private void RunGuardedSync(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            AppendLog("error", ex.Message);
            MessageBox.Show(this, ex.Message, "BA KeySmith Next", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            UpdateStats();
        }
    }

    private void Diagnostics_Emitted(object? sender, DiagnosticEvent diagnosticEvent)
    {
        Dispatcher.Invoke(() =>
        {
            var fields = diagnosticEvent.Fields.Count == 0
                ? string.Empty
                : " " + string.Join(" ", diagnosticEvent.Fields.Select(pair => $"{pair.Key}={pair.Value}"));
            AppendLog($"{diagnosticEvent.Source}.{diagnosticEvent.Name}", $"{diagnosticEvent.Level}{fields}");
        });
    }

    private void AppendLog(string name, string message)
    {
        DiagnosticsList.Items.Add($"[{DateTime.Now:HH:mm:ss.fff}] {name} {message}");
        if (DiagnosticsList.Items.Count > 600)
        {
            DiagnosticsList.Items.RemoveAt(0);
        }

        DiagnosticsList.ScrollIntoView(DiagnosticsList.Items[^1]);
    }

    private void SetBusy(bool isBusy)
    {
        StartButton.IsEnabled = !isBusy && !_isRunning;
        StopButton.IsEnabled = !isBusy && _isRunning;
    }

    private void SetRunningState(bool running)
    {
        StatusText.Text = running ? "Running" : "Stopped";
        StartButton.IsEnabled = !running;
        StopButton.IsEnabled = running;
        DryRunCheck.IsEnabled = !running;
        AllowForegroundCheck.IsEnabled = !running && DryRunCheck.IsChecked == true;
    }

    private void UpdateModeVisuals()
    {
        if (LiveWarningText is null)
        {
            return;
        }

        var dryRun = DryRunCheck.IsChecked == true;
        LiveWarningText.Visibility = dryRun ? Visibility.Collapsed : Visibility.Visible;
        AllowForegroundCheck.IsEnabled = !_isRunning && dryRun;
    }

    private void UpdateStats()
    {
        MappingCountText.Text = _host?.RuntimeConfig.Mappings.Count.ToString() ?? _mappingRows.Count.ToString();
        TargetText.Text = _host?.RuntimeConfig.TargetProcess ?? _currentConfig.TargetProcess;
        InputEventCountText.Text = _dryRunBackend?.Events.Count.ToString() ?? "0";

        if (HostStartedText is null)
        {
            return;
        }

        if (_host is null)
        {
            HostStartedText.Text = "stopped";
            RuntimeStateText.Text = "Stopped";
            PipelineStateText.Text = "stopped";
            ForegroundStateText.Text = "unknown";
            GenerationText.Text = "0";
            WorkerCountText.Text = "0";
            PendingActionCountText.Text = "0";
            RunningActionCountText.Text = "0";
            HeldKeyCountText.Text = "0";
            OwnerCountText.Text = "0";
            SnapshotDetailsBox.Text =
                $"not running{Environment.NewLine}" +
                $"editor_mappings={_mappingRows.Count}{Environment.NewLine}" +
                $"target={TargetProcessBox.Text.Trim()}";
            return;
        }

        var snapshot = _host.Snapshot();
        var runtime = snapshot.Runtime;
        var presses = runtime.Presses;
        HostStartedText.Text = snapshot.IsStarted ? "started" : "stopped";
        RuntimeStateText.Text = runtime.State.ToString();
        PipelineStateText.Text = snapshot.IsPipelineRunning ? "running" : "stopped";
        ForegroundStateText.Text = runtime.LastForegroundAllowed ? "last allowed" : "last blocked";
        GenerationText.Text = runtime.Generation.ToString();
        WorkerCountText.Text = runtime.ActiveWorkerCount.ToString();
        PendingActionCountText.Text = runtime.PendingActionCount.ToString();
        RunningActionCountText.Text = runtime.RunningActionCount.ToString();
        HeldKeyCountText.Text = presses.KeyOwners.Count.ToString();
        OwnerCountText.Text = presses.OwnerKeys.Count.ToString();
        SnapshotDetailsBox.Text = FormatSnapshotDetails(snapshot);
    }

    private static string FormatSnapshotDetails(RuntimeHostSnapshot snapshot)
    {
        var runtime = snapshot.Runtime;
        var presses = runtime.Presses;
        var heldKeys = presses.KeyOwners.Count == 0
            ? "held_keys=none"
            : "held_keys=" + string.Join("; ", presses.KeyOwners.Select(pair =>
                $"{pair.Key}<-{string.Join(",", pair.Value)}"));
        var owners = presses.OwnerKeys.Count == 0
            ? "owners=none"
            : "owners=" + string.Join("; ", presses.OwnerKeys.Select(pair =>
                $"{pair.Key}->{string.Join(",", pair.Value)}"));

        return string.Join(
            Environment.NewLine,
            $"state={runtime.State} host_started={snapshot.IsStarted} pipeline={snapshot.IsPipelineRunning}",
            $"target={snapshot.RuntimeConfig.TargetProcess} mappings={runtime.MappingCount} generation={runtime.Generation}",
            $"trigger_pipeline queued={snapshot.Pipeline.QueuedCount} handled={snapshot.Pipeline.HandledCount} pending={snapshot.Pipeline.PendingCount}",
            $"workers={runtime.ActiveWorkerCount} pending_actions={runtime.PendingActionCount} running_actions={runtime.RunningActionCount} last_foreground_allowed={runtime.LastForegroundAllowed}",
            heldKeys,
            owners);
    }

    private static string ComboText(ComboBox comboBox, string fallback)
    {
        return comboBox.SelectedItem is ComboBoxItem item && item.Content is not null
            ? item.Content.ToString() ?? fallback
            : fallback;
    }

    private static void SetComboText(ComboBox comboBox, string value)
    {
        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }
    }

    private static string FindDefaultConfigPath()
    {
        var current = Environment.CurrentDirectory;
        for (var i = 0; i < 6; i++)
        {
            var candidate = Path.Combine(current, "config.example.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            var parent = Directory.GetParent(current);
            if (parent is null)
            {
                break;
            }

            current = parent.FullName;
        }

        return Path.Combine(Environment.CurrentDirectory, "config.example.json");
    }
}

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
