using BAKeySmith.Core.Configuration;
using BAKeySmith.Core.Contracts;
using BAKeySmith.Core.Diagnostics;
using BAKeySmith.Core.Foreground;
using BAKeySmith.Core.Hosting;
using BAKeySmith.Core.Input;
using BAKeySmith.Core.Triggers;

namespace BAKeySmith.App.Services;

public sealed class RuntimeHostController : IAsyncDisposable
{
    private readonly EventDiagnosticsSink _diagnostics = new();
    private readonly Func<bool, IForegroundGate> _dryRunForegroundGateFactory;
    private readonly Func<IForegroundGate> _liveForegroundGateFactory;
    private readonly Func<IReadOnlyList<string>, ITriggerSource> _liveTriggerSourceFactory;
    private readonly IReadOnlyList<string> _selfForegroundProcessNames;
    private RuntimeHost? _host;
    private ManualTriggerSource? _manualSource;
    private DryRunInputBackend? _dryRunBackend;

    public RuntimeHostController(
        Func<bool, IForegroundGate>? dryRunForegroundGateFactory = null,
        Func<IForegroundGate>? liveForegroundGateFactory = null,
        Func<IReadOnlyList<string>, ITriggerSource>? liveTriggerSourceFactory = null,
        IEnumerable<string>? selfForegroundProcessNames = null)
    {
        _dryRunForegroundGateFactory = dryRunForegroundGateFactory ?? CreateDefaultDryRunForegroundGate;
        _liveForegroundGateFactory = liveForegroundGateFactory ?? (() => new WindowsForegroundGate());
        _liveTriggerSourceFactory = liveTriggerSourceFactory ?? CreateDefaultLiveTriggerSource;
        _selfForegroundProcessNames = (selfForegroundProcessNames ?? GuiSelfForegroundGate.DefaultSelfProcessNames()).ToArray();
        _diagnostics.Emitted += (_, diagnostic) => DiagnosticEmitted?.Invoke(this, diagnostic);
    }

    public event EventHandler<DiagnosticEvent>? DiagnosticEmitted;

    public bool IsRunning => _host?.IsStarted == true;
    public RuntimeConfig? RuntimeConfig => _host?.RuntimeConfig;
    public int InputEventCount => _dryRunBackend?.Events.Count ?? 0;

    public async Task StartAsync(
        AppConfigV1 appConfig,
        bool dryRun,
        bool allowAnyForeground,
        CancellationToken cancellationToken)
    {
        await StopAsync(CancellationToken.None);

        IInputBackend inputBackend;
        IForegroundGate foregroundGate;
        ITriggerSource triggerSource;
        if (dryRun)
        {
            _dryRunBackend = new DryRunInputBackend();
            _manualSource = new ManualTriggerSource();
            inputBackend = _dryRunBackend;
            foregroundGate = WrapGuiEditingSafety(_dryRunForegroundGateFactory(allowAnyForeground));
            triggerSource = _manualSource;
        }
        else
        {
            _dryRunBackend = null;
            _manualSource = null;
            inputBackend = new WindowsInputBackend();
            foregroundGate = WrapGuiEditingSafety(_liveForegroundGateFactory());
            triggerSource = _liveTriggerSourceFactory(_selfForegroundProcessNames);
        }

        _host = new RuntimeHost(
            appConfig,
            inputBackend,
            foregroundGate,
            triggerSource,
            _diagnostics);
        await _host.StartAsync(cancellationToken);
    }

    public async Task ReloadAsync(
        AppConfigV1 appConfig,
        CancellationToken cancellationToken)
    {
        if (_host is null || !_host.IsStarted)
        {
            return;
        }

        await _host.ReloadAsync(appConfig, cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_host is null)
        {
            return;
        }

        await _host.StopAsync(cancellationToken);
        await _host.DisposeAsync();
        _host = null;
        _manualSource = null;
        _dryRunBackend = null;
    }

    public RuntimeHostSnapshot? Snapshot()
    {
        return _host?.Snapshot();
    }

    public async ValueTask<ForegroundGateResult> CheckForegroundAsync(
        RuntimeConfig fallbackConfig,
        CancellationToken cancellationToken)
    {
        if (_host is not null)
        {
            return await _host.CheckForegroundAsync(cancellationToken);
        }

        return await WrapGuiEditingSafety(_liveForegroundGateFactory())
            .CheckAsync(fallbackConfig, cancellationToken);
    }

    public void Simulate(string code, string phase)
    {
        if (_manualSource is null || !IsRunning)
        {
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

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
    }

    private IForegroundGate WrapGuiEditingSafety(IForegroundGate foregroundGate)
    {
        return new GuiSelfForegroundGate(foregroundGate, _selfForegroundProcessNames);
    }

    private static IForegroundGate CreateDefaultDryRunForegroundGate(bool allowAnyForeground)
    {
        return allowAnyForeground
            ? AlwaysForegroundGate.Instance
            : new ManualForegroundGate { IsAllowed = true };
    }

    private static ITriggerSource CreateDefaultLiveTriggerSource(IReadOnlyList<string> selfForegroundProcessNames)
    {
        return new WindowsHookTriggerSource(blockedForegroundProcessNames: selfForegroundProcessNames);
    }
}
