using BAKeySmith.Core.Contracts;
using BAKeySmith.Core.Diagnostics;
using BAKeySmith.Core.Foreground;
using BAKeySmith.Core.Input;

namespace BAKeySmith.Core.Runtime;

public sealed class InProcessRuntimeCore : IRuntimeCore
{
    private readonly object _gate = new();
    private readonly IInputBackend _inputBackend;
    private readonly IForegroundGate _foregroundGate;
    private readonly IDiagnosticsSink _diagnostics;
    private readonly PressOwnershipTracker _presses;
    private RuntimeConfig _config = RuntimeConfig.Empty;
    private Dictionary<string, List<MappingDefinition>> _triggerIndex = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, RuntimeActionQueue> _actionQueues = new(StringComparer.OrdinalIgnoreCase);
    private RuntimeSession? _session;
    private RuntimeState _state = RuntimeState.Stopped;
    private long _generation;
    private long _ownerSequence;
    private bool _lastForegroundAllowed;

    public InProcessRuntimeCore(
        IInputBackend inputBackend,
        IForegroundGate? foregroundGate = null,
        IDiagnosticsSink? diagnostics = null)
    {
        _inputBackend = inputBackend;
        _diagnostics = diagnostics ?? NoOpDiagnosticsSink.Instance;
        _foregroundGate = foregroundGate ?? AlwaysForegroundGate.Instance;
        _presses = new PressOwnershipTracker(inputBackend, _diagnostics);
    }

    public RuntimeState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    public RuntimeSnapshot Snapshot()
    {
        lock (_gate)
        {
            var pendingActions = _actionQueues.Values.Sum(queue => queue.PendingCount);
            var runningActions = _actionQueues.Values.Sum(queue => queue.RunningCount);
            return new RuntimeSnapshot(
                _state,
                _generation,
                _config.Mappings.Count,
                _session?.ActiveWorkerCount ?? 0,
                pendingActions,
                runningActions,
                _lastForegroundAllowed,
                _presses.Snapshot());
        }
    }

    public ValueTask LoadAsync(RuntimeConfig config, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_state == RuntimeState.Enabled || _state == RuntimeState.Stopping)
            {
                throw new InvalidOperationException("Use ReloadAsync to replace config while runtime is active.");
            }

            ApplyConfigLocked(config);
            _state = RuntimeState.Disabled;
        }

        _diagnostics.Emit(DiagnosticEvent.Create("runtime", "config_loaded"));
        return ValueTask.CompletedTask;
    }

    public ValueTask EnableAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_state == RuntimeState.Enabled)
            {
                return ValueTask.CompletedTask;
            }

            if (_state == RuntimeState.Stopping)
            {
                throw new InvalidOperationException("Cannot enable runtime while it is stopping.");
            }

            _session = CreateSessionLocked();
            _state = RuntimeState.Enabled;
        }

        _diagnostics.Emit(DiagnosticEvent.Create("runtime", "enabled"));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisableAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var session = TakeSession(RuntimeState.Disabled);
        if (session is not null)
        {
            await session.DisposeAsync();
        }

        _diagnostics.Emit(DiagnosticEvent.Create("runtime", "disabled"));
    }

    public async ValueTask ReloadAsync(RuntimeConfig config, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var shouldReenable = State == RuntimeState.Enabled;
        var oldSession = TakeSession(shouldReenable ? RuntimeState.Disabled : State);
        if (oldSession is not null)
        {
            await oldSession.DisposeAsync();
        }

        lock (_gate)
        {
            ApplyConfigLocked(config);
            if (shouldReenable)
            {
                _session = CreateSessionLocked();
                _state = RuntimeState.Enabled;
            }
        }

        _diagnostics.Emit(DiagnosticEvent.Create("runtime", "reloaded"));
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var session = TakeSession(RuntimeState.Stopping);
        if (session is not null)
        {
            await session.DisposeAsync();
        }

        lock (_gate)
        {
            _state = RuntimeState.Stopped;
        }

        _diagnostics.Emit(DiagnosticEvent.Create("runtime", "stopped"));
    }

    public async ValueTask HandleTriggerAsync(
        TriggerEvent triggerEvent,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RuntimeConfig config;
        RuntimeSession? session;
        List<MappingDefinition> mappings;
        RuntimeState state;

        lock (_gate)
        {
            state = _state;
            config = _config;
            session = _session;
            _triggerIndex.TryGetValue(triggerEvent.Trigger.Key, out mappings!);
            mappings ??= [];
        }

        _diagnostics.Emit(DiagnosticEvent.Create(
            "runtime",
            "trigger_received",
            fields: new Dictionary<string, string>
            {
                ["trigger"] = triggerEvent.Trigger.Key,
                ["phase"] = triggerEvent.NormalizedPhase,
                ["state"] = state.ToString()
            }));

        if (state != RuntimeState.Enabled || session is null)
        {
            EmitIgnored(triggerEvent, "runtime_not_enabled");
            return;
        }

        if (mappings.Count == 0)
        {
            EmitIgnored(triggerEvent, "mapping_not_found");
            return;
        }

        if (triggerEvent.IsDown)
        {
            var gateResult = await _foregroundGate.CheckAsync(config, cancellationToken);
            lock (_gate)
            {
                _lastForegroundAllowed = gateResult.IsAllowed;
            }

            _diagnostics.Emit(DiagnosticEvent.Create(
                "runtime",
                "foreground_checked",
                fields: new Dictionary<string, string>
                {
                    ["allowed"] = gateResult.IsAllowed.ToString(),
                    ["target_process"] = gateResult.TargetProcess ?? string.Empty,
                    ["foreground_process"] = gateResult.ForegroundProcess ?? string.Empty
                }));

            if (!gateResult.IsAllowed)
            {
                EmitIgnored(triggerEvent, "foreground_not_allowed");
                return;
            }
        }
        else
        {
            _diagnostics.Emit(DiagnosticEvent.Create(
                "runtime",
                "foreground_skipped",
                fields: new Dictionary<string, string>
                {
                    ["phase"] = triggerEvent.NormalizedPhase,
                    ["reason"] = "release_event"
                }));
        }

        foreach (var mapping in mappings)
        {
            await DispatchMappingAsync(mapping, triggerEvent, session, config, cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
    }

    private async ValueTask DispatchMappingAsync(
        MappingDefinition mapping,
        TriggerEvent triggerEvent,
        RuntimeSession session,
        RuntimeConfig config,
        CancellationToken cancellationToken)
    {
        var kind = mapping.Action.NormalizedKind;
        var key = mapping.Action.Key;
        if (string.IsNullOrWhiteSpace(key) && kind != "macro")
        {
            EmitIgnored(triggerEvent, "action_key_missing");
            return;
        }

        if (kind == "hold")
        {
            if (triggerEvent.IsDown)
            {
                await _presses.HoldKeyAsync(mapping.NormalizedId, key!, session.Token);
                return;
            }

            if (triggerEvent.IsUp)
            {
                await _presses.ReleaseKeyAsync(mapping.NormalizedId, key!, CancellationToken.None);
                return;
            }
        }

        if (kind == "tap")
        {
            if (!triggerEvent.IsDown)
            {
                return;
            }

            var ownerId = $"{mapping.NormalizedId}:tap:{Interlocked.Increment(ref _ownerSequence)}";
            RuntimeActionQueue? queue;
            lock (_gate)
            {
                _actionQueues.TryGetValue(mapping.NormalizedId, out queue);
            }

            if (queue is null)
            {
                EmitIgnored(triggerEvent, "action_queue_missing");
                return;
            }

            if (!queue.TryEnqueue(token =>
            {
                var sequencer = new InputSequencer(_presses, config.TapHold);
                return sequencer.TapKeyAsync(ownerId, key!, token);
            }))
            {
                EmitIgnored(triggerEvent, "action_queue_closed");
            }

            return;
        }

        if (kind == "macro")
        {
            if (!triggerEvent.IsDown)
            {
                return;
            }

            RuntimeActionQueue? queue;
            lock (_gate)
            {
                _actionQueues.TryGetValue(mapping.NormalizedId, out queue);
            }

            if (queue is null)
            {
                EmitIgnored(triggerEvent, "action_queue_missing");
                return;
            }

            var macro = mapping.Action.Macro ?? [];
            var ownerId = $"{mapping.NormalizedId}:macro:{Interlocked.Increment(ref _ownerSequence)}";
            if (!queue.TryEnqueue(token =>
            {
                var executor = new MacroExecutor(
                    _presses,
                    _inputBackend,
                    _diagnostics,
                    tapHold: config.TapHold);
                return executor.ExecuteAsync(ownerId, macro, token);
            }))
            {
                EmitIgnored(triggerEvent, "action_queue_closed");
            }

            return;
        }

        EmitIgnored(triggerEvent, $"unsupported_action:{kind}");
    }

    private RuntimeSession? TakeSession(RuntimeState nextState)
    {
        lock (_gate)
        {
            var session = _session;
            _session = null;
            _actionQueues = new Dictionary<string, RuntimeActionQueue>(StringComparer.OrdinalIgnoreCase);
            _state = nextState;
            return session;
        }
    }

    private RuntimeSession CreateSessionLocked()
    {
        var session = new RuntimeSession(_presses, _diagnostics);
        _actionQueues = new Dictionary<string, RuntimeActionQueue>(StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in _config.Mappings)
        {
            if (mapping.Action.NormalizedKind == "tap" || mapping.Action.NormalizedKind == "macro")
            {
                _actionQueues[mapping.NormalizedId] = new RuntimeActionQueue(
                    session,
                    mapping.NormalizedId,
                    _diagnostics);
            }
        }

        return session;
    }

    private void ApplyConfigLocked(RuntimeConfig config)
    {
        var mappings = config.Mappings.ToArray();
        _config = config with { Mappings = mappings };
        _triggerIndex = mappings
            .GroupBy(mapping => mapping.Trigger.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.ToList(),
                StringComparer.OrdinalIgnoreCase);
        _generation++;
    }

    private void EmitIgnored(TriggerEvent triggerEvent, string reason)
    {
        _diagnostics.Emit(DiagnosticEvent.Create(
            "runtime",
            "trigger_ignored",
            fields: new Dictionary<string, string>
            {
                ["trigger"] = triggerEvent.Trigger.Key,
                ["phase"] = triggerEvent.NormalizedPhase,
                ["reason"] = reason
            }));
    }
}
