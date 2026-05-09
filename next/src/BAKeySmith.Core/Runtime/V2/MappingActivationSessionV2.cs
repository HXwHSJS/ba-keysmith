using BAKeySmith.Core.Actions.V2;

namespace BAKeySmith.Core.Runtime.V2;

public enum MappingActivationSessionStateV2
{
    Created,
    Running,
    Completed,
    Cancelled
}

public enum MappingActivationCancelReasonV2
{
    Cancel,
    Stop,
    Reload,
    ForegroundLost,
    EmergencyStop
}

public sealed class MappingActivationSessionV2
{
    private readonly ActivationPlanV2 _plan;
    private readonly ActivationPlanExecutorV2 _executor;
    private readonly RuntimeV2ExecutionOptions _options;
    private int _whileHeldIterations;

    public MappingActivationSessionV2(
        string mappingId,
        ActivationPlanV2 plan,
        ActivationPlanExecutorV2 executor,
        RuntimeV2ExecutionOptions? options = null)
    {
        MappingId = mappingId;
        _plan = plan;
        _executor = executor;
        _options = options ?? RuntimeV2ExecutionOptions.Default;
    }

    public string MappingId { get; }
    public MappingActivationSessionStateV2 State { get; private set; } = MappingActivationSessionStateV2.Created;
    public bool IsHeld => State == MappingActivationSessionStateV2.Running;

    public Task<RuntimeV2ExecutionResult> TriggerDownAsync()
    {
        if (State != MappingActivationSessionStateV2.Created)
        {
            return Task.FromResult(InvalidState("trigger_down"));
        }

        var result = _executor.ExecuteOnDown(_plan);
        if (!result.HasErrors)
        {
            State = MappingActivationSessionStateV2.Running;
        }

        return Task.FromResult(result);
    }

    public Task<RuntimeV2ExecutionResult> RunWhileHeldIterationsAsync(int count)
    {
        if (count < 0)
        {
            return Task.FromResult(RuntimeV2ExecutionResult.FromDiagnostics(
            [
                new RuntimeV2Diagnostic(
                    RuntimeV2DiagnosticSeverity.Error,
                    RuntimeV2DiagnosticCode.InvalidWhileHeldIterationCount,
                    "while_held iteration count must not be negative.",
                    "while_held")
            ]));
        }

        if (count > _options.MaxWhileHeldIterations)
        {
            return Task.FromResult(RuntimeV2ExecutionResult.FromDiagnostics(
            [
                new RuntimeV2Diagnostic(
                    RuntimeV2DiagnosticSeverity.Error,
                    RuntimeV2DiagnosticCode.InvalidWhileHeldIterationCount,
                    "while_held iteration count exceeds the Runtime v2 sandbox limit.",
                    "while_held")
            ]));
        }

        if (State != MappingActivationSessionStateV2.Running)
        {
            return Task.FromResult(InvalidState("while_held"));
        }

        var results = new List<RuntimeV2ExecutionResult>();
        for (var index = 0; index < count; index++)
        {
            results.Add(_executor.ExecuteWhileHeldIteration(_plan, _whileHeldIterations++));
            if (results[^1].HasErrors)
            {
                break;
            }
        }

        return Task.FromResult(RuntimeV2ExecutionResult.Combine(results.ToArray()));
    }

    public Task<RuntimeV2ExecutionResult> TriggerUpAsync()
    {
        if (State != MappingActivationSessionStateV2.Running)
        {
            return Task.FromResult(InvalidState("trigger_up"));
        }

        var onUp = _executor.ExecuteOnUp(_plan);
        var cleanup = _executor.Cleanup();
        State = MappingActivationSessionStateV2.Completed;
        return Task.FromResult(RuntimeV2ExecutionResult.Combine(onUp, cleanup));
    }

    public Task<RuntimeV2ExecutionResult> CancelAsync(MappingActivationCancelReasonV2 reason)
    {
        if (State is MappingActivationSessionStateV2.Completed or MappingActivationSessionStateV2.Cancelled)
        {
            return Task.FromResult(InvalidState(reason.ToString()));
        }

        var cleanup = _executor.Cleanup();
        State = MappingActivationSessionStateV2.Cancelled;
        return Task.FromResult(cleanup);
    }

    private static RuntimeV2ExecutionResult InvalidState(string path)
    {
        return RuntimeV2ExecutionResult.FromDiagnostics(
        [
            new RuntimeV2Diagnostic(
                RuntimeV2DiagnosticSeverity.Error,
                RuntimeV2DiagnosticCode.InvalidSessionState,
                "Runtime v2 sandbox session is not in a valid state for this lifecycle operation.",
                path)
        ]);
    }
}
