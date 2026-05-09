using BAKeySmith.Core.Actions.V2;

namespace BAKeySmith.Core.Runtime.V2;

public sealed class MappingActivationSessionManagerV2
{
    private readonly Dictionary<string, ActiveSession> _activeSessions = new(StringComparer.Ordinal);
    private readonly MappingActivationPolicyV2 _policy;

    public MappingActivationSessionManagerV2(
        FakeInputBackendV2? backend = null,
        MappingActivationPolicyV2? policy = null)
    {
        Backend = backend ?? new FakeInputBackendV2();
        _policy = policy ?? MappingActivationPolicyV2.Default;
    }

    public FakeInputBackendV2 Backend { get; }
    public int ActiveSessionCount => _activeSessions.Count;
    public IReadOnlyList<string> ActiveMappingIds => _activeSessions.Keys.Order(StringComparer.Ordinal).ToArray();

    public Task<MappingActivationManagerResultV2> TriggerDownAsync(
        string mappingId,
        ActivationPlanV2 plan,
        RuntimeV2ExecutionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (!ValidateMappingId(mappingId, out var invalid))
        {
            return Task.FromResult(ManagerDiagnostic(invalid!, mappingId));
        }

        if (_activeSessions.ContainsKey(mappingId))
        {
            if (_policy.SameMappingMode != SameMappingActivationModeV2.IgnoreWhenRunning)
            {
                return Task.FromResult(ManagerDiagnostic(
                    new RuntimeV2Diagnostic(
                        RuntimeV2DiagnosticSeverity.Error,
                        RuntimeV2DiagnosticCode.ManagerInvariantViolation,
                        "Runtime v2 sandbox manager received an unsupported same-mapping policy mode.",
                        "policy.same_mapping_mode",
                        mappingId),
                    mappingId));
            }

            return Task.FromResult(ManagerDiagnostic(
                new RuntimeV2Diagnostic(
                    RuntimeV2DiagnosticSeverity.Warning,
                    RuntimeV2DiagnosticCode.MappingAlreadyRunning,
                    "Mapping activation is already running; same mapping trigger down is ignored.",
                    "trigger_down",
                    mappingId),
                mappingId));
        }

        var effectiveOptions = ApplyCancellation(options, cancellationToken);
        var ledger = new OwnershipLedgerV2();
        var executor = new ActivationPlanExecutorV2(Backend, ledger, effectiveOptions);
        var session = new MappingActivationSessionV2(mappingId, plan, executor, effectiveOptions);
        var eventsBefore = Backend.Events.Count;
        RuntimeV2ExecutionResult result;
        using (Backend.BeginMappingContext(mappingId))
        {
            result = session.TriggerDownAsync().GetAwaiter().GetResult();
        }

        if (!result.HasErrors)
        {
            _activeSessions.Add(mappingId, new ActiveSession(session, ledger, effectiveOptions));
        }

        return Task.FromResult(MappingActivationManagerResultV2.FromExecution(
            result,
            mappingId,
            ActiveMappingIds,
            eventsBefore,
            Backend.Events.Count));
    }

    public Task<MappingActivationManagerResultV2> TriggerDownAsync(
        MappingActivationRequestV2 request,
        CancellationToken cancellationToken = default)
    {
        return TriggerDownAsync(request.MappingId, request.Plan, request.Options, cancellationToken);
    }

    public Task<MappingActivationManagerResultV2> RunWhileHeldIterationsAsync(
        string mappingId,
        int count,
        CancellationToken cancellationToken = default)
    {
        if (!ValidateMappingId(mappingId, out var invalid))
        {
            return Task.FromResult(ManagerDiagnostic(invalid!, mappingId));
        }

        if (!_activeSessions.TryGetValue(mappingId, out var active))
        {
            return Task.FromResult(NoActiveSession(mappingId, "while_held"));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ManagerDiagnostic(
                CancellationDiagnostic(mappingId, "while_held"),
                mappingId));
        }

        var eventsBefore = Backend.Events.Count;
        RuntimeV2ExecutionResult result;
        using (Backend.BeginMappingContext(mappingId))
        {
            result = active.Session.RunWhileHeldIterationsAsync(count).GetAwaiter().GetResult();
        }

        return Task.FromResult(MappingActivationManagerResultV2.FromExecution(
            result,
            mappingId,
            ActiveMappingIds,
            eventsBefore,
            Backend.Events.Count));
    }

    public Task<MappingActivationManagerResultV2> TriggerUpAsync(
        string mappingId,
        CancellationToken cancellationToken = default)
    {
        if (!ValidateMappingId(mappingId, out var invalid))
        {
            return Task.FromResult(ManagerDiagnostic(invalid!, mappingId));
        }

        if (!_activeSessions.TryGetValue(mappingId, out var active))
        {
            return Task.FromResult(NoActiveSession(mappingId, "trigger_up"));
        }

        var eventsBefore = Backend.Events.Count;
        RuntimeV2ExecutionResult result;
        using (Backend.BeginMappingContext(mappingId))
        {
            result = cancellationToken.IsCancellationRequested
                ? RuntimeV2ExecutionResult.Combine(
                    RuntimeV2ExecutionResult.FromDiagnostics([CancellationDiagnostic(mappingId, "trigger_up")]),
                    active.Session.CancelAsync(MappingActivationCancelReasonV2.Cancel).GetAwaiter().GetResult())
                : active.Session.TriggerUpAsync().GetAwaiter().GetResult();
        }

        _activeSessions.Remove(mappingId);
        return Task.FromResult(MappingActivationManagerResultV2.FromExecution(
            result,
            mappingId,
            ActiveMappingIds,
            eventsBefore,
            Backend.Events.Count));
    }

    public Task<MappingActivationManagerResultV2> CancelAsync(
        string mappingId,
        MappingActivationCancelReasonV2 reason,
        CancellationToken cancellationToken = default)
    {
        if (!ValidateMappingId(mappingId, out var invalid))
        {
            return Task.FromResult(ManagerDiagnostic(invalid!, mappingId));
        }

        if (!_activeSessions.TryGetValue(mappingId, out var active))
        {
            return Task.FromResult(NoActiveSession(mappingId, "cancel"));
        }

        var eventsBefore = Backend.Events.Count;
        RuntimeV2ExecutionResult result;
        using (Backend.BeginMappingContext(mappingId))
        {
            result = cancellationToken.IsCancellationRequested
                ? RuntimeV2ExecutionResult.Combine(
                    RuntimeV2ExecutionResult.FromDiagnostics([CancellationDiagnostic(mappingId, "cancel")]),
                    active.Session.CancelAsync(reason).GetAwaiter().GetResult())
                : active.Session.CancelAsync(reason).GetAwaiter().GetResult();
        }

        _activeSessions.Remove(mappingId);
        return Task.FromResult(MappingActivationManagerResultV2.FromExecution(
            result,
            mappingId,
            ActiveMappingIds,
            eventsBefore,
            Backend.Events.Count));
    }

    public Task<MappingActivationManagerResultV2> CancelAllAsync(
        MappingActivationCancelReasonV2 reason,
        CancellationToken cancellationToken = default)
    {
        var eventsBefore = Backend.Events.Count;
        var diagnostics = new List<RuntimeV2Diagnostic>();
        foreach (var mappingId in _activeSessions.Keys.Order(StringComparer.Ordinal).ToArray())
        {
            if (!_activeSessions.TryGetValue(mappingId, out var active))
            {
                continue;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                diagnostics.Add(CancellationDiagnostic(mappingId, "cancel_all"));
            }

            using (Backend.BeginMappingContext(mappingId))
            {
                var result = active.Session.CancelAsync(reason).GetAwaiter().GetResult();
                diagnostics.AddRange(MappingActivationManagerResultV2.StampMappingId(
                    result.Diagnostics,
                    mappingId));
            }

            _activeSessions.Remove(mappingId);
        }

        return Task.FromResult(new MappingActivationManagerResultV2(
            diagnostics,
            MappingId: null,
            ActiveMappingIds,
            eventsBefore,
            Backend.Events.Count));
    }

    private MappingActivationManagerResultV2 NoActiveSession(string mappingId, string path)
    {
        return ManagerDiagnostic(
            new RuntimeV2Diagnostic(
                RuntimeV2DiagnosticSeverity.Warning,
                RuntimeV2DiagnosticCode.NoActiveSession,
                "No active Runtime v2 sandbox session exists for the mapping.",
                path,
                mappingId),
            mappingId);
    }

    private MappingActivationManagerResultV2 ManagerDiagnostic(
        RuntimeV2Diagnostic diagnostic,
        string? mappingId)
    {
        return MappingActivationManagerResultV2.FromDiagnostics(
            [diagnostic],
            mappingId,
            ActiveMappingIds,
            Backend.Events.Count);
    }

    private static bool ValidateMappingId(string mappingId, out RuntimeV2Diagnostic? diagnostic)
    {
        if (string.IsNullOrWhiteSpace(mappingId))
        {
            diagnostic = new RuntimeV2Diagnostic(
                RuntimeV2DiagnosticSeverity.Error,
                RuntimeV2DiagnosticCode.InvalidMappingId,
                "Runtime v2 sandbox mapping id must not be empty.",
                "mapping_id",
                mappingId);
            return false;
        }

        diagnostic = null;
        return true;
    }

    private static RuntimeV2Diagnostic CancellationDiagnostic(string mappingId, string path)
    {
        return new RuntimeV2Diagnostic(
            RuntimeV2DiagnosticSeverity.Error,
            RuntimeV2DiagnosticCode.ExecutionCancelled,
            "Runtime v2 sandbox manager lifecycle operation was cancelled; cleanup remains best-effort.",
            path,
            mappingId);
    }

    private static RuntimeV2ExecutionOptions ApplyCancellation(
        RuntimeV2ExecutionOptions? options,
        CancellationToken cancellationToken)
    {
        var effective = options ?? RuntimeV2ExecutionOptions.Default;
        return cancellationToken.CanBeCanceled
            ? effective with { CancellationToken = cancellationToken }
            : effective;
    }

    private sealed record ActiveSession(
        MappingActivationSessionV2 Session,
        OwnershipLedgerV2 Ledger,
        RuntimeV2ExecutionOptions Options);
}
