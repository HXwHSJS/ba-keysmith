using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using BAKeySmith.App.Services;
using BAKeySmith.App.ViewModels;
using BAKeySmith.Core.Configuration;
using BAKeySmith.Core.Diagnostics;
using Microsoft.Win32;

namespace BAKeySmith.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel = new();
    private readonly RuntimeHostController _runtimeController = new();
    private readonly DispatcherTimer _statusTimer;
    private bool _keyCaptureArmed;

    public MainWindow()
    {
        InitializeComponent();
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        PreviewMouseDown += MainWindow_PreviewMouseDown;
        _viewModel.ConfigureRuntimeCommands(
            () => RunGuardedAsync(async () =>
            {
                await _viewModel.ExecuteStartWithLiveConfirmationAsync(
                    ConfirmLiveStart,
                    StartRuntimeAsync);
            }),
            () => RunGuardedAsync(StopHostAsync),
            () => RunGuardedAsync(ReloadConfigAsync),
            () =>
            {
                UpdateStats();
                AppendLog("snapshot.refreshed", "Runtime snapshot refreshed.");
            },
            () => RunGuardedAsync(ProbeForegroundAsync),
            () => Simulate("down"),
            () => Simulate("up"));
        DataContext = _viewModel;
        _runtimeController.DiagnosticEmitted += RuntimeController_DiagnosticEmitted;
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

    private bool ConfirmLiveStart()
    {
        var result = MessageBox.Show(
            this,
            "Live 模式会安装全局 hook，并向系统发送真实输入。\n\n" +
            "只应在你已经准备测试时启用。\n\n" +
            "继续前请确认目标进程和当前前台窗口状态正确。",
            "确认启动 Live Runtime",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Cancel);

        return result == MessageBoxResult.OK;
    }

    private async Task StartRuntimeAsync()
    {
        var appConfig = BuildConfigFromEditor(validate: true);
        await _runtimeController.StartAsync(
            appConfig,
            _viewModel.IsDryRun,
            _viewModel.AllowAnyForeground,
            CancellationToken.None);
        SetRunningState(true);
        UpdateStats();
    }

    private async Task ReloadConfigAsync()
    {
        var appConfig = BuildConfigFromEditor(validate: true);

        if (!_runtimeController.IsRunning)
        {
            _viewModel.ValidateConfig(appConfig);
            AppendLog("host.reload.skipped", "Runtime is not running; config will load on next start.");
            UpdateStats();
            return;
        }

        await _runtimeController.ReloadAsync(appConfig, CancellationToken.None);
        UpdateStats();
    }

    private async Task ProbeForegroundAsync()
    {
        var result = await _runtimeController.CheckForegroundAsync(
            _viewModel.BuildFallbackRuntimeConfig(),
            CancellationToken.None);

        _viewModel.ApplyForegroundProbe(result);
        AppendLog("foreground.probe",
            $"allowed={result.IsAllowed} target={result.TargetProcess ?? "-"} foreground={result.ForegroundProcess ?? "-"}");
    }

    private void BrowseConfig_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON config (*.json)|*.json|All files (*.*)|*.*",
            FileName = Path.GetFileName(_viewModel.ConfigPath),
            InitialDirectory = Directory.Exists(Path.GetDirectoryName(_viewModel.ConfigPath))
                ? Path.GetDirectoryName(_viewModel.ConfigPath)
                : Environment.CurrentDirectory
        };

        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.ConfigPath = dialog.FileName;
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
            var path = _viewModel.ConfigPath.Trim();
            await _viewModel.SaveConfigAsync(
                path,
                config,
                createBackup: true,
                CancellationToken.None);
            AppendLog("config.saved", path);

            if (_runtimeController.IsRunning)
            {
                await _runtimeController.ReloadAsync(config, CancellationToken.None);
                AppendLog("host.reloaded", "Runtime reloaded from saved config.");
            }

            UpdateStats();
        });
    }

    private void ModeCheck_Changed(object sender, RoutedEventArgs e)
    {
        UpdateModeVisuals();
    }

    private void MappingTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateMappingFormVisibility();
    }

    private void MacroDiagnosticsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (MacroDiagnosticsList.SelectedItem is MacroDiagnosticDisplayItem { Line: > 0 } item)
        {
            MacroEditor.GoToLine(item.Line);
        }
    }

    private void BeginMappingTriggerCapture_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.BeginMappingTriggerCapture())
        {
            ArmKeyCaptureAfterCurrentInput();
        }
    }

    private void BeginHotkeyCapture_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.BeginHotkeyCapture())
        {
            ArmKeyCaptureAfterCurrentInput();
        }
    }

    private void BeginMappingTargetCapture_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.BeginMappingTargetCapture())
        {
            ArmKeyCaptureAfterCurrentInput();
        }
    }

    private void CancelKeyCapture_Click(object sender, RoutedEventArgs e)
    {
        _keyCaptureArmed = false;
        _viewModel.CancelKeyCapture();
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_keyCaptureArmed || !_viewModel.IsCapturingKey)
        {
            return;
        }

        var effectiveKey = KeyCaptureFormatter.GetEffectiveKey(
            e.Key,
            e.SystemKey,
            e.ImeProcessedKey,
            e.DeadCharProcessedKey);
        var result = _viewModel.IsCapturingHotkey
            ? KeyCaptureFormatter.FormatHotkey(effectiveKey, Keyboard.Modifiers)
            : _viewModel.IsCapturingMappingTarget
                ? KeyCaptureFormatter.FormatMappingTarget(effectiveKey)
                : KeyCaptureFormatter.FormatMappingTrigger(effectiveKey);

        _viewModel.ApplyKeyCaptureResult(result);
        _keyCaptureArmed = _viewModel.IsCapturingKey;
        e.Handled = true;
    }

    private void MainWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_keyCaptureArmed ||
            (!_viewModel.IsCapturingMappingTrigger && !_viewModel.IsCapturingMappingTarget) ||
            IsFromCaptureControl(e.OriginalSource))
        {
            return;
        }

        var result = _viewModel.IsCapturingMappingTarget
            ? KeyCaptureFormatter.FormatMappingTarget(e.ChangedButton)
            : KeyCaptureFormatter.FormatMappingTrigger(e.ChangedButton);
        _viewModel.ApplyKeyCaptureResult(result);
        _keyCaptureArmed = _viewModel.IsCapturingKey;
        e.Handled = true;
    }

    protected override async void OnClosed(EventArgs e)
    {
        _statusTimer.Stop();
        PreviewKeyDown -= MainWindow_PreviewKeyDown;
        PreviewMouseDown -= MainWindow_PreviewMouseDown;
        _runtimeController.DiagnosticEmitted -= RuntimeController_DiagnosticEmitted;
        await _runtimeController.DisposeAsync();
        base.OnClosed(e);
    }

    private async Task StopHostAsync()
    {
        await _runtimeController.StopAsync(CancellationToken.None);
        SetRunningState(false);
        UpdateStats();
    }

    private void LoadConfigIntoEditor(bool logResult)
    {
        var path = _viewModel.ConfigPath.Trim();
        var document = _viewModel.LoadConfig(path);
        if (!document.Success)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, document.Errors));
        }

        foreach (var warning in document.Warnings)
        {
            AppendLog("config.warning", warning);
        }

        if (logResult)
        {
            AppendLog("config.loaded", $"{path} mappings={_viewModel.MappingEditor.Mappings.Count}");
        }

        _viewModel.MappingEditor.ClearEditorForm();
        UpdateStats();
    }

    private AppConfigV1 BuildConfigFromEditor(bool validate)
    {
        var config = _viewModel.BuildConfigFromEditor();

        if (validate)
        {
            var validation = _viewModel.ValidateConfig(config);
            if (validation.Errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, validation.Errors));
            }

            foreach (var warning in validation.Warnings)
            {
                AppendLog("config.warning", warning);
            }
        }

        return config;
    }

    private void UpdateMappingFormVisibility()
    {
        if (MappingTargetBox is null || MacroEditor is null)
        {
            return;
        }

        var macro = _viewModel.MappingEditor.IsEditingMacro;
        MappingTargetBox.IsEnabled = !macro;
        MappingTargetCaptureButton.IsEnabled = !macro;
        MappingModeCombo.IsEnabled = !macro;
        MacroEditor.IsEnabled = macro;
    }

    private void Simulate(string phase)
    {
        var code = _viewModel.SimulateCode.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            AppendLog("simulate.ignored", "Empty trigger code.");
            return;
        }

        if (!_runtimeController.IsRunning)
        {
            AppendLog("simulate.ignored", "Dry-run runtime is not running.");
            return;
        }

        _runtimeController.Simulate(code, phase);
    }

    private async Task RunGuardedAsync(Func<Task> action)
    {
        try
        {
            SetBusy(true);
            _viewModel.ClearLastError();
            await action();
        }
        catch (Exception ex)
        {
            _viewModel.SetLastError(ex.Message);
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
            _viewModel.ClearLastError();
            action();
        }
        catch (Exception ex)
        {
            _viewModel.SetLastError(ex.Message);
            AppendLog("error", ex.Message);
            MessageBox.Show(this, ex.Message, "BA KeySmith Next", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            UpdateStats();
        }
    }

    private void RuntimeController_DiagnosticEmitted(object? sender, DiagnosticEvent diagnosticEvent)
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
        _viewModel.AppendDiagnostic(name, message);

        if (_viewModel.DiagnosticsLog.Entries.Count > 0)
        {
            DiagnosticsList.ScrollIntoView(_viewModel.DiagnosticsLog.Entries[^1]);
        }
    }

    private void SetBusy(bool isBusy)
    {
        _viewModel.IsBusy = isBusy;
        StartButton.IsEnabled = !isBusy && !_runtimeController.IsRunning;
        StopButton.IsEnabled = !isBusy && _runtimeController.IsRunning;
    }

    private void SetRunningState(bool running)
    {
        _viewModel.IsRunning = running;
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

        LiveWarningText.Visibility = _viewModel.IsDryRun ? Visibility.Collapsed : Visibility.Visible;
        AllowForegroundCheck.IsEnabled = !_runtimeController.IsRunning && _viewModel.IsDryRun;
    }

    private void UpdateStats()
    {
        var snapshot = _runtimeController.Snapshot();
        _viewModel.ApplySnapshot(snapshot, _runtimeController.InputEventCount);
    }

    private void ArmKeyCaptureAfterCurrentInput()
    {
        _keyCaptureArmed = false;
        Dispatcher.BeginInvoke(() =>
        {
            if (_viewModel.IsCapturingKey)
            {
                _keyCaptureArmed = true;
                Focus();
                Keyboard.Focus(this);
            }
        }, DispatcherPriority.Background);
    }

    private bool IsFromCaptureControl(object source)
    {
        if (source is not DependencyObject dependencyObject)
        {
            return false;
        }

        while (dependencyObject is not null)
        {
            if (ReferenceEquals(dependencyObject, MappingTriggerCaptureButton) ||
                ReferenceEquals(dependencyObject, MappingTriggerCancelCaptureButton) ||
                ReferenceEquals(dependencyObject, MappingTargetCaptureButton) ||
                ReferenceEquals(dependencyObject, MappingTargetCancelCaptureButton) ||
                ReferenceEquals(dependencyObject, HotkeyCaptureButton) ||
                ReferenceEquals(dependencyObject, HotkeyCancelCaptureButton))
            {
                return true;
            }

            dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
        }

        return false;
    }
}
