using BAKeySmith.Core.Configuration;
using BAKeySmith.Core.Contracts;
using BAKeySmith.Core.Diagnostics;
using BAKeySmith.Core.Foreground;
using BAKeySmith.Core.Input;
using BAKeySmith.Core.Runtime;
using BAKeySmith.Core.Triggers;

namespace BAKeySmith.Core.Hosting;

public sealed class RuntimeHost : IAsyncDisposable
{
    private readonly IDiagnosticsSink _diagnostics;
    private readonly IForegroundGate _foregroundGate;
    private readonly ITriggerSource _triggerSource;
    private bool _started;

    public RuntimeHost(
        AppConfigV1 appConfig,
        IInputBackend inputBackend,
        IForegroundGate foregroundGate,
        ITriggerSource triggerSource,
        IDiagnosticsSink? diagnostics = null)
    {
        _diagnostics = diagnostics ?? NoOpDiagnosticsSink.Instance;
        _foregroundGate = foregroundGate;
        _triggerSource = triggerSource;
        AppConfig = appConfig;
        var errors = new List<string>();
        var warnings = new List<string>();
        RuntimeConfig = AppConfigSerializer.ToRuntimeConfig(appConfig, errors, warnings);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        foreach (var warning in warnings)
        {
            _diagnostics.Emit(DiagnosticEvent.Create(
                "host",
                "config_warning",
                DiagnosticLevel.Warning,
                warning));
        }

        Runtime = new InProcessRuntimeCore(inputBackend, foregroundGate, _diagnostics);
        Pipeline = new TriggerPipeline(triggerSource, Runtime, _diagnostics);
    }

    public AppConfigV1 AppConfig { get; private set; }
    public RuntimeConfig RuntimeConfig { get; private set; }
    public IRuntimeCore Runtime { get; }
    public TriggerPipeline Pipeline { get; }
    public bool IsStarted => _started;

    public RuntimeHostSnapshot Snapshot()
    {
        return new RuntimeHostSnapshot(
            _started,
            Pipeline.IsRunning,
            Pipeline.Snapshot(),
            RuntimeConfig,
            Runtime.Snapshot());
    }

    public ValueTask<ForegroundGateResult> CheckForegroundAsync(CancellationToken cancellationToken)
    {
        return _foregroundGate.CheckAsync(RuntimeConfig, cancellationToken);
    }

    public async ValueTask StartAsync(CancellationToken cancellationToken)
    {
        if (_started)
        {
            return;
        }

        try
        {
            await Runtime.LoadAsync(RuntimeConfig, cancellationToken);
            await Runtime.EnableAsync(cancellationToken);
            ApplyTriggerCapturePolicy(isEnabled: true);
            await Pipeline.StartAsync(cancellationToken);
            _started = true;
            _diagnostics.Emit(DiagnosticEvent.Create("host", "started"));
        }
        catch
        {
            ApplyTriggerCapturePolicy(isEnabled: false);
            try
            {
                await Pipeline.StopAsync(CancellationToken.None);
            }
            catch
            {
                // Best-effort rollback: preserve the original start failure.
            }

            try
            {
                await Runtime.StopAsync(CancellationToken.None);
            }
            catch
            {
                // Best-effort rollback: preserve the original start failure.
            }

            _started = false;
            throw;
        }
    }

    public async ValueTask ReloadAsync(
        AppConfigV1 appConfig,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var runtimeConfig = AppConfigSerializer.ToRuntimeConfig(appConfig, errors, warnings);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        ApplyTriggerCapturePolicy(isEnabled: false);
        AppConfig = appConfig;
        RuntimeConfig = runtimeConfig;
        await Runtime.ReloadAsync(RuntimeConfig, cancellationToken);
        ApplyTriggerCapturePolicy(isEnabled: Runtime.State == RuntimeState.Enabled && _started);
        _diagnostics.Emit(DiagnosticEvent.Create(
            "host",
            "reloaded",
            fields: new Dictionary<string, string>
            {
                ["warnings"] = warnings.Count.ToString()
            }));
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken)
    {
        if (!_started)
        {
            return;
        }

        ApplyTriggerCapturePolicy(isEnabled: false);
        await Pipeline.StopAsync(cancellationToken);
        await Runtime.StopAsync(cancellationToken);
        _started = false;
        _diagnostics.Emit(DiagnosticEvent.Create("host", "stopped"));
    }

    public async ValueTask DisableAsync(CancellationToken cancellationToken)
    {
        ApplyTriggerCapturePolicy(isEnabled: false);
        await Runtime.DisableAsync(cancellationToken);
        _diagnostics.Emit(DiagnosticEvent.Create("host", "disabled"));
    }

    public async ValueTask EnableRuntimeAsync(CancellationToken cancellationToken)
    {
        await Runtime.EnableAsync(cancellationToken);
        ApplyTriggerCapturePolicy(isEnabled: _started && Runtime.State == RuntimeState.Enabled);
        _diagnostics.Emit(DiagnosticEvent.Create("host", "runtime_enabled"));
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
        await Pipeline.DisposeAsync();
        await Runtime.DisposeAsync();
    }

    private void ApplyTriggerCapturePolicy(bool isEnabled)
    {
        if (_triggerSource is not ITriggerCapturePolicySink capturePolicySink)
        {
            return;
        }

        var triggerKeys = isEnabled
            ? RuntimeConfig.Mappings.Select(mapping => mapping.Trigger.Key)
            : Enumerable.Empty<string>();
        capturePolicySink.UpdateCapturePolicy(TriggerCapturePolicySnapshot.FromRuntimeConfig(
            RuntimeConfig.TargetProcess,
            triggerKeys,
            isEnabled));
    }
}
