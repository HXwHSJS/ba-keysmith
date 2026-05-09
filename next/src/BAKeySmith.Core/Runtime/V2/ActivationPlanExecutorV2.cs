using BAKeySmith.Core.Actions.V2;
using BAKeySmith.Core.Input.V2;

namespace BAKeySmith.Core.Runtime.V2;

public sealed class ActivationPlanExecutorV2
{
    private readonly FakeInputBackendV2 _backend;
    private readonly OwnershipLedgerV2 _ledger;
    private readonly ActivationPlanValidatorV2 _validator;
    private readonly RuntimeV2ExecutionOptions _options;

    public ActivationPlanExecutorV2(
        FakeInputBackendV2 backend,
        OwnershipLedgerV2 ledger,
        RuntimeV2ExecutionOptions? options = null,
        ActivationPlanValidatorV2? validator = null)
    {
        _backend = backend;
        _ledger = ledger;
        _options = options ?? RuntimeV2ExecutionOptions.Default;
        _validator = validator ?? new ActivationPlanValidatorV2();
    }

    public RuntimeV2ExecutionResult ValidateForExecution(ActivationPlanV2 plan)
    {
        var optionDiagnostics = _options.Validate();
        if (optionDiagnostics.Any(diagnostic => diagnostic.IsError))
        {
            return RuntimeV2ExecutionResult.FromDiagnostics(optionDiagnostics);
        }

        var diagnostics = _validator.Validate(plan)
            .Select(diagnostic => new RuntimeV2Diagnostic(
                ToRuntimeSeverity(diagnostic.Severity),
                ToRuntimeCode(diagnostic),
                $"{diagnostic.Code}: {diagnostic.Message}",
                diagnostic.Path))
            .ToArray();

        return RuntimeV2ExecutionResult.FromDiagnostics(diagnostics);
    }

    public RuntimeV2ExecutionResult ExecuteOnDown(ActivationPlanV2 plan)
    {
        var validation = ValidateForExecution(plan);
        if (validation.HasErrors)
        {
            return validation;
        }

        return RuntimeV2ExecutionResult.Combine(validation, ExecuteSequence(plan.OnDown, "on_down"));
    }

    public RuntimeV2ExecutionResult ExecuteOnUp(ActivationPlanV2 plan)
    {
        var validation = ValidateForExecution(plan);
        if (validation.HasErrors)
        {
            return validation;
        }

        return RuntimeV2ExecutionResult.Combine(validation, ExecuteSequence(plan.OnUp, "on_up"));
    }

    public RuntimeV2ExecutionResult ExecuteWhileHeldIteration(
        ActivationPlanV2 plan,
        int iterationIndex)
    {
        var validation = ValidateForExecution(plan);
        if (validation.HasErrors)
        {
            return validation;
        }

        if (plan.WhileHeld is null)
        {
            return validation;
        }

        var diagnostics = new List<RuntimeV2Diagnostic>();
        var state = new ExecutionState(_options.MaxActionSteps);
        ExecuteSequence(
            plan.WhileHeld.Body,
            $"while_held[{iterationIndex}].body",
            state,
            diagnostics);

        if (plan.WhileHeld.Interval > TimeSpan.Zero && !diagnostics.Any(item => item.IsError))
        {
            _backend.Wait(plan.WhileHeld.Interval, $"while_held[{iterationIndex}].interval");
        }

        return RuntimeV2ExecutionResult.Combine(
            validation,
            RuntimeV2ExecutionResult.FromDiagnostics(diagnostics, state.StepsExecuted));
    }

    public RuntimeV2ExecutionResult Cleanup()
    {
        var diagnostics = new List<RuntimeV2Diagnostic>();
        _ledger.ReleaseAll(_backend, diagnostics);
        return RuntimeV2ExecutionResult.FromDiagnostics(diagnostics);
    }

    private RuntimeV2ExecutionResult ExecuteSequence(
        IReadOnlyList<ActionModelV2> actions,
        string path)
    {
        var diagnostics = new List<RuntimeV2Diagnostic>();
        var state = new ExecutionState(_options.MaxActionSteps);
        ExecuteSequence(actions, path, state, diagnostics);
        return RuntimeV2ExecutionResult.FromDiagnostics(diagnostics, state.StepsExecuted);
    }

    private void ExecuteSequence(
        IReadOnlyList<ActionModelV2> actions,
        string path,
        ExecutionState state,
        List<RuntimeV2Diagnostic> diagnostics)
    {
        for (var index = 0; index < actions.Count; index++)
        {
            if (!ExecuteAction(actions[index], $"{path}[{index}]", state, diagnostics))
            {
                return;
            }
        }
    }

    private static RuntimeV2DiagnosticSeverity ToRuntimeSeverity(ActionValidationSeverityV2 severity)
    {
        return severity switch
        {
            ActionValidationSeverityV2.Info => RuntimeV2DiagnosticSeverity.Info,
            ActionValidationSeverityV2.Warning => RuntimeV2DiagnosticSeverity.Warning,
            ActionValidationSeverityV2.Error => RuntimeV2DiagnosticSeverity.Error,
            _ => RuntimeV2DiagnosticSeverity.Error
        };
    }

    private static RuntimeV2DiagnosticCode ToRuntimeCode(ActionValidationDiagnosticV2 diagnostic)
    {
        return diagnostic.Severity == ActionValidationSeverityV2.Error
            ? RuntimeV2DiagnosticCode.ActionPlanInvalid
            : RuntimeV2DiagnosticCode.ActionValidationDiagnostic;
    }

    private bool ExecuteAction(
        ActionModelV2 action,
        string path,
        ExecutionState state,
        List<RuntimeV2Diagnostic> diagnostics)
    {
        if (_options.CancellationToken.IsCancellationRequested)
        {
            diagnostics.Add(new RuntimeV2Diagnostic(
                RuntimeV2DiagnosticSeverity.Error,
                RuntimeV2DiagnosticCode.ExecutionCancelled,
                "Runtime v2 sandbox execution was cancelled.",
                path));
            return false;
        }

        if (!state.TryConsumeStep(path, diagnostics))
        {
            return false;
        }

        switch (action)
        {
            case KeyActionV2 keyAction:
                ExecuteKeyAction(keyAction, path, diagnostics);
                return true;
            case MouseButtonActionV2 mouseAction:
                ExecuteMouseAction(mouseAction, path, diagnostics);
                return true;
            case WheelActionV2 wheelAction:
                _backend.Wheel(wheelAction.Direction, wheelAction.Steps, path);
                return true;
            case WaitActionV2 waitAction:
                _backend.Wait(waitAction.Duration, path);
                return true;
            case RepeatActionV2 repeatAction:
                for (var repeat = 0; repeat < repeatAction.Count; repeat++)
                {
                    ExecuteSequence(
                        repeatAction.Body,
                        $"{path}.body[{repeat}]",
                        state,
                        diagnostics);
                    if (diagnostics.Any(item => item.IsError))
                    {
                        return false;
                    }
                }

                return true;
            case CoordinateActionV2 coordinateAction:
                ExecuteCoordinateAction(coordinateAction, path, diagnostics);
                return true;
            default:
                diagnostics.Add(new RuntimeV2Diagnostic(
                    RuntimeV2DiagnosticSeverity.Error,
                    RuntimeV2DiagnosticCode.ActionPlanInvalid,
                    $"Unknown Runtime v2 action type {action.GetType().Name}.",
                    path));
                return false;
        }
    }

    private void ExecuteKeyAction(
        KeyActionV2 action,
        string path,
        List<RuntimeV2Diagnostic> diagnostics)
    {
        var resource = OwnershipResourceV2.Key(action.Key);
        switch (action.Kind)
        {
            case ActionKindV2.TapKey:
                _backend.KeyTap(action.Key, path, action.Duration);
                break;
            case ActionKindV2.DownKey:
                if (_ledger.Acquire(resource, path, diagnostics))
                {
                    _backend.KeyDown(action.Key, path);
                }

                break;
            case ActionKindV2.UpKey:
                if (_ledger.Release(resource, path, diagnostics))
                {
                    _backend.KeyUp(action.Key, path);
                }

                break;
            case ActionKindV2.HoldKey:
                _backend.KeyDown(action.Key, path);
                _backend.Wait(action.Duration ?? TimeSpan.Zero, $"{path}.duration");
                _backend.KeyUp(action.Key, path);
                break;
        }
    }

    private void ExecuteMouseAction(
        MouseButtonActionV2 action,
        string path,
        List<RuntimeV2Diagnostic> diagnostics)
    {
        var resource = OwnershipResourceV2.MouseButton(action.Button);
        switch (action.Kind)
        {
            case ActionKindV2.TapMouse:
                _backend.MouseTap(action.Button, path, action.Duration);
                break;
            case ActionKindV2.DownMouse:
                if (_ledger.Acquire(resource, path, diagnostics))
                {
                    _backend.MouseDown(action.Button, path);
                }

                break;
            case ActionKindV2.UpMouse:
                if (_ledger.Release(resource, path, diagnostics))
                {
                    _backend.MouseUp(action.Button, path);
                }

                break;
            case ActionKindV2.HoldMouse:
                _backend.MouseDown(action.Button, path);
                _backend.Wait(action.Duration ?? TimeSpan.Zero, $"{path}.duration");
                _backend.MouseUp(action.Button, path);
                break;
        }
    }

    private void ExecuteCoordinateAction(
        CoordinateActionV2 action,
        string path,
        List<RuntimeV2Diagnostic> diagnostics)
    {
        diagnostics.Add(new RuntimeV2Diagnostic(
            RuntimeV2DiagnosticSeverity.Warning,
            RuntimeV2DiagnosticCode.CoordinateUnsupported,
            "Coordinate action live execution is not implemented in the Runtime v2 Core-only sandbox.",
            path));
        _backend.CoordinateUnsupported(action.Start, path);

        var button = action.Button ?? InputNameResolverV2.Resolve("mouse_left");
        var resource = OwnershipResourceV2.CoordinateContact(action.Start, button);
        switch (action.Kind)
        {
            case ActionKindV2.DownAt:
                _ledger.Acquire(resource, path, diagnostics);
                break;
            case ActionKindV2.UpAt:
                _ledger.Release(resource, path, diagnostics);
                break;
        }
    }

    private sealed class ExecutionState
    {
        private int _remainingSteps;

        public ExecutionState(int maxSteps)
        {
            _remainingSteps = maxSteps;
        }

        public int StepsExecuted { get; private set; }

        public bool TryConsumeStep(
            string path,
            List<RuntimeV2Diagnostic> diagnostics)
        {
            if (_remainingSteps <= 0)
            {
                diagnostics.Add(new RuntimeV2Diagnostic(
                    RuntimeV2DiagnosticSeverity.Error,
                    RuntimeV2DiagnosticCode.ActionStepBudgetExceeded,
                    "Runtime v2 sandbox action step budget was exceeded.",
                    path));
                return false;
            }

            _remainingSteps--;
            StepsExecuted++;
            return true;
        }
    }
}
