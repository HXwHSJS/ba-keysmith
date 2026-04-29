using System.Collections.ObjectModel;
using System.ComponentModel;
using BAKeySmith.App.Commands;
using BAKeySmith.App.Models;
using BAKeySmith.App.Services;
using BAKeySmith.Core.Configuration;
using BAKeySmith.Core.Contracts;
using BAKeySmith.Core.Foreground;
using BAKeySmith.Core.Hosting;

namespace BAKeySmith.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    public const string LiveStartCancelledMessage = "Live mode start cancelled by user.";
    public const string LiveStartRequiresAdministratorMessage =
        "Blue Archive live mode requires BA KeySmith to run as administrator. Close BA KeySmith and choose Run as administrator, then start live mode again.";
    public const string KeyCaptureRunningSafeMessage =
        "Runtime 可保持运行；BAKeySmith.App 前台输入不会触发映射。";
    public const string MappingTargetCaptureRequiresSimpleMessage =
        "simple target capture 只适用于 simple 映射；macro 映射请编辑宏脚本。";

    private readonly ConfigDocumentService _configService;
    private readonly IElevationStatusService _elevationStatusService;
    private AppConfigV1 _currentConfig = new();
    private string _configPath;
    private string _targetProcess = "BlueArchive.exe";
    private string _hotkey = "ctrl+shift+f12";
    private string _tapHoldMilliseconds = "20";
    private bool _isDryRun = true;
    private bool _allowAnyForeground = true;
    private bool _isElevated;
    private bool _isRunning;
    private bool _isBusy;
    private string _simulateCode = "q";
    private string _lastError = string.Empty;
    private KeyCaptureMode _keyCaptureMode = KeyCaptureMode.None;
    private string _keyCaptureStatusText = string.Empty;
    private ForegroundProbeDisplay _foregroundProbe = ForegroundProbeDisplay.Unknown;

    public MainWindowViewModel(
        ConfigDocumentService? configService = null,
        IElevationStatusService? elevationStatusService = null)
    {
        _configService = configService ?? new ConfigDocumentService();
        _elevationStatusService = elevationStatusService ?? new WindowsElevationStatusService();
        _configPath = _configService.DefaultConfigPath;
        RefreshElevationStatus();
        ClearDiagnosticsCommand = new RelayCommand(ClearDiagnostics);
        MappingEditor.PropertyChanged += MappingEditor_PropertyChanged;
        UpdateMacroDiagnostics();
    }

    public MappingEditorViewModel MappingEditor { get; } = new();
    public MacroDiagnosticsViewModel MacroDiagnostics { get; } = new();
    public RuntimeStatusViewModel RuntimeStatus { get; } = new();
    public DiagnosticsLogViewModel DiagnosticsLog { get; } = new();
    public ObservableCollection<string> Diagnostics { get; } = new();
    public RelayCommand ClearDiagnosticsCommand { get; }
    public AsyncRelayCommand StartRuntimeCommand { get; private set; } = new(() => Task.CompletedTask);
    public AsyncRelayCommand StopRuntimeCommand { get; private set; } = new(() => Task.CompletedTask);
    public AsyncRelayCommand ReloadConfigCommand { get; private set; } = new(() => Task.CompletedTask);
    public RelayCommand RefreshStatusCommand { get; private set; } = new(() => { });
    public AsyncRelayCommand ProbeForegroundCommand { get; private set; } = new(() => Task.CompletedTask);
    public RelayCommand SimulateDownCommand { get; private set; } = new(() => { });
    public RelayCommand SimulateUpCommand { get; private set; } = new(() => { });

    public AppConfigV1 CurrentConfig => _currentConfig;

    public string ConfigPath
    {
        get => _configPath;
        set => SetProperty(ref _configPath, value);
    }

    public string TargetProcess
    {
        get => _targetProcess;
        set
        {
            if (SetProperty(ref _targetProcess, value))
            {
                MappingEditor.MarkDirty();
            }
        }
    }

    public string Hotkey
    {
        get => _hotkey;
        set
        {
            if (SetProperty(ref _hotkey, value))
            {
                MappingEditor.MarkDirty();
            }
        }
    }

    public string TapHoldMilliseconds
    {
        get => _tapHoldMilliseconds;
        set
        {
            if (SetProperty(ref _tapHoldMilliseconds, value))
            {
                MappingEditor.MarkDirty();
            }
        }
    }

    public bool IsDryRun
    {
        get => _isDryRun;
        set => SetProperty(ref _isDryRun, value);
    }

    public bool AllowAnyForeground
    {
        get => _allowAnyForeground;
        set => SetProperty(ref _allowAnyForeground, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        set => SetProperty(ref _isRunning, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string SimulateCode
    {
        get => _simulateCode;
        set => SetProperty(ref _simulateCode, value);
    }

    public string LastError
    {
        get => _lastError;
        private set => SetProperty(ref _lastError, value);
    }

    public bool IsCapturingKey => _keyCaptureMode != KeyCaptureMode.None;

    public bool IsCapturingMappingTrigger => _keyCaptureMode == KeyCaptureMode.MappingTrigger;

    public bool IsCapturingHotkey => _keyCaptureMode == KeyCaptureMode.Hotkey;

    public bool IsCapturingMappingTarget => _keyCaptureMode == KeyCaptureMode.MappingTarget;

    public string KeyCaptureStatusText
    {
        get => _keyCaptureStatusText;
        private set => SetProperty(ref _keyCaptureStatusText, value);
    }

    public bool IsElevated
    {
        get => _isElevated;
        private set
        {
            if (SetProperty(ref _isElevated, value))
            {
                OnPropertyChanged(nameof(ElevationStatusText));
                OnPropertyChanged(nameof(ElevationHelpText));
            }
        }
    }

    public string ElevationStatusText => IsElevated ? "Administrator: Yes" : "Administrator: No";

    public string ElevationHelpText => IsElevated
        ? "Blue Archive live mode can start after the live confirmation."
        : "Dry-run and config editing are available. Blue Archive live mode requires Run as administrator.";

    public ForegroundProbeDisplay ForegroundProbe
    {
        get => _foregroundProbe;
        private set => SetProperty(ref _foregroundProbe, value);
    }

    public ConfigDocument LoadConfig(string path)
    {
        ConfigPath = path;
        var document = _configService.Load(path);
        if (!document.Success)
        {
            return document;
        }

        ApplyConfig(document.Config);
        foreach (var warning in document.Warnings)
        {
            AppendDiagnostic("config.warning", warning);
        }

        return document;
    }

    public AppConfigV1 BuildConfigFromEditor()
    {
        return _currentConfig with
        {
            Version = 1,
            TargetProcess = string.IsNullOrWhiteSpace(TargetProcess)
                ? "BlueArchive.exe"
                : TargetProcess.Trim(),
            Hotkey = string.IsNullOrWhiteSpace(Hotkey)
                ? "ctrl+shift+f12"
                : Hotkey.Trim(),
            TapHoldMilliseconds = ParseTapHoldMilliseconds(),
            Mappings = MappingEditor.ToConfigMappings()
        };
    }

    public ConfigValidationResult ValidateConfig(AppConfigV1 config)
    {
        return _configService.Validate(config);
    }

    public async Task SaveConfigAsync(
        string path,
        AppConfigV1 config,
        bool createBackup,
        CancellationToken cancellationToken)
    {
        await _configService.SaveAsync(path, config, createBackup, cancellationToken);
        _currentConfig = config;
        MappingEditor.MarkClean();
        OnPropertyChanged(nameof(CurrentConfig));
    }

    public RuntimeConfig BuildFallbackRuntimeConfig()
    {
        var validation = ValidateConfig(BuildConfigFromEditor());
        return validation.RuntimeConfig;
    }

    public async Task<bool> ExecuteStartWithLiveConfirmationAsync(
        Func<bool> confirmLiveStart,
        Func<Task> startRuntime)
    {
        if (!ConfirmLiveStartIfNeeded(confirmLiveStart))
        {
            return false;
        }

        await startRuntime();
        return true;
    }

    public bool ConfirmLiveStartIfNeeded(Func<bool> confirmLiveStart)
    {
        if (IsDryRun)
        {
            return true;
        }

        RefreshElevationStatus();
        if (!IsElevated)
        {
            SetLastError(LiveStartRequiresAdministratorMessage);
            AppendDiagnostic("host.start.blocked.elevation", LiveStartRequiresAdministratorMessage);
            return false;
        }

        if (confirmLiveStart())
        {
            return true;
        }

        SetLastError(LiveStartCancelledMessage);
        AppendDiagnostic("host.start.cancelled", LiveStartCancelledMessage);
        return false;
    }

    public void ConfigureRuntimeCommands(
        Func<Task> startRuntime,
        Func<Task> stopRuntime,
        Func<Task> reloadConfig,
        Action refreshStatus,
        Func<Task> probeForeground,
        Action simulateDown,
        Action simulateUp)
    {
        StartRuntimeCommand = new AsyncRelayCommand(startRuntime);
        StopRuntimeCommand = new AsyncRelayCommand(stopRuntime);
        ReloadConfigCommand = new AsyncRelayCommand(reloadConfig);
        RefreshStatusCommand = new RelayCommand(refreshStatus);
        ProbeForegroundCommand = new AsyncRelayCommand(probeForeground);
        SimulateDownCommand = new RelayCommand(simulateDown);
        SimulateUpCommand = new RelayCommand(simulateUp);
        OnPropertyChanged(nameof(StartRuntimeCommand));
        OnPropertyChanged(nameof(StopRuntimeCommand));
        OnPropertyChanged(nameof(ReloadConfigCommand));
        OnPropertyChanged(nameof(RefreshStatusCommand));
        OnPropertyChanged(nameof(ProbeForegroundCommand));
        OnPropertyChanged(nameof(SimulateDownCommand));
        OnPropertyChanged(nameof(SimulateUpCommand));
    }

    public bool BeginMappingTriggerCapture()
    {
        return BeginKeyCapture(
            KeyCaptureMode.MappingTrigger,
            $"正在捕获 mapping trigger。按下要绑定的键或鼠标按钮；Esc 会绑定为 escape。{KeyCaptureRunningSafeMessage}");
    }

    public bool BeginHotkeyCapture()
    {
        return BeginKeyCapture(
            KeyCaptureMode.Hotkey,
            $"正在捕获控制热键。请按一个非修饰键，可同时按住 Ctrl / Shift / Alt。{KeyCaptureRunningSafeMessage}");
    }

    public bool BeginMappingTargetCapture()
    {
        if (MappingEditor.IsEditingMacro)
        {
            SetLastError(MappingTargetCaptureRequiresSimpleMessage);
            AppendDiagnostic("key_capture.blocked.macro_target", MappingTargetCaptureRequiresSimpleMessage);
            KeyCaptureStatusText = MappingTargetCaptureRequiresSimpleMessage;
            return false;
        }

        return BeginKeyCapture(
            KeyCaptureMode.MappingTarget,
            $"正在捕获 simple target。按下输出键或鼠标按钮；Esc 会绑定为 escape。{KeyCaptureRunningSafeMessage}");
    }

    public void CancelKeyCapture()
    {
        if (!IsCapturingKey)
        {
            KeyCaptureStatusText = string.Empty;
            return;
        }

        _keyCaptureMode = KeyCaptureMode.None;
        KeyCaptureStatusText = "Key capture cancelled.";
        NotifyKeyCaptureStateChanged();
    }

    public bool ApplyKeyCaptureResult(KeyCaptureResult result)
    {
        if (!IsCapturingKey)
        {
            return false;
        }

        if (result.Kind == KeyCaptureResultKind.Incomplete)
        {
            KeyCaptureStatusText = result.Message;
            return false;
        }

        if (result.Kind == KeyCaptureResultKind.Unsupported)
        {
            SetLastError(result.Message);
            AppendDiagnostic("key_capture.unsupported", result.Message);
            KeyCaptureStatusText = result.Message;
            return false;
        }

        if (IsCapturingMappingTrigger)
        {
            MappingEditor.EditingTrigger = result.Value;
            FinishKeyCapture($"Captured mapping trigger: {result.Value}");
            return true;
        }

        if (IsCapturingHotkey)
        {
            Hotkey = result.Value;
            FinishKeyCapture($"Captured hotkey: {result.Value}");
            return true;
        }

        if (IsCapturingMappingTarget)
        {
            MappingEditor.EditingTarget = result.Value;
            FinishKeyCapture($"Captured simple target: {result.Value}");
            return true;
        }

        return false;
    }

    public void ApplySnapshot(RuntimeHostSnapshot? snapshot, int inputEventCount)
    {
        IsRunning = snapshot?.IsStarted == true;
        InputEventCount = inputEventCount;
        RuntimeStatus.ApplySnapshot(
            snapshot,
            inputEventCount,
            TargetProcess,
            MappingEditor.Mappings.Count);
    }

    private int _inputEventCount;

    public int InputEventCount
    {
        get => _inputEventCount;
        private set => SetProperty(ref _inputEventCount, value);
    }

    public void AppendDiagnostic(string name, string message)
    {
        DiagnosticsLog.Append(name, message);
        Diagnostics.Add(DiagnosticsLog.Entries[^1].Display);
        if (Diagnostics.Count > 600)
        {
            Diagnostics.RemoveAt(0);
        }
    }

    public void ClearDiagnostics()
    {
        DiagnosticsLog.Clear();
        Diagnostics.Clear();
    }

    public void ApplyForegroundProbe(ForegroundGateResult result)
    {
        ForegroundProbe = ForegroundProbeDisplay.FromResult(result);
        RuntimeStatus.ApplyForegroundProbe(ForegroundProbe);
    }

    public void RefreshElevationStatus()
    {
        IsElevated = _elevationStatusService.IsElevated;
    }

    public void SetLastError(string message)
    {
        LastError = message;
    }

    public void ClearLastError()
    {
        LastError = string.Empty;
    }

    public void UpdateMacroDiagnostics()
    {
        MacroDiagnostics.UpdateForMapping(
            MappingEditor.IsEditingMacro,
            MappingEditor.EditingScript);
    }

    public double ParseTapHoldMilliseconds()
    {
        if (!double.TryParse(TapHoldMilliseconds.Trim(), out var tapHold) || tapHold < 0)
        {
            throw new InvalidOperationException("Tap 持续时间必须是非负数字。");
        }

        return tapHold;
    }

    private void ApplyConfig(AppConfigV1 config)
    {
        _currentConfig = config;
        TargetProcess = config.TargetProcess;
        Hotkey = config.Hotkey;
        TapHoldMilliseconds = config.TapHoldMilliseconds.ToString("0.###");
        MappingEditor.LoadFromConfig(config);
        OnPropertyChanged(nameof(CurrentConfig));
        UpdateMacroDiagnostics();
    }

    private void MappingEditor_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MappingEditorViewModel.EditingScript)
            or nameof(MappingEditorViewModel.EditingType)
            or nameof(MappingEditorViewModel.IsEditingMacro))
        {
            UpdateMacroDiagnostics();
        }
    }

    private bool BeginKeyCapture(KeyCaptureMode mode, string statusText)
    {
        ClearLastError();
        _keyCaptureMode = mode;
        KeyCaptureStatusText = statusText;
        NotifyKeyCaptureStateChanged();
        return true;
    }

    private void FinishKeyCapture(string statusText)
    {
        _keyCaptureMode = KeyCaptureMode.None;
        KeyCaptureStatusText = statusText;
        NotifyKeyCaptureStateChanged();
    }

    private void NotifyKeyCaptureStateChanged()
    {
        OnPropertyChanged(nameof(IsCapturingKey));
        OnPropertyChanged(nameof(IsCapturingMappingTrigger));
        OnPropertyChanged(nameof(IsCapturingHotkey));
        OnPropertyChanged(nameof(IsCapturingMappingTarget));
    }

    private enum KeyCaptureMode
    {
        None,
        MappingTrigger,
        Hotkey,
        MappingTarget
    }
}
