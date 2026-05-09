using BAKeySmith.Core.Input.V2;

namespace BAKeySmith.Core.Actions.V2;

public sealed class ActivationPlanValidatorV2
{
    public IReadOnlyList<ActionValidationDiagnosticV2> Validate(ActivationPlanV2 plan)
    {
        var diagnostics = new List<ActionValidationDiagnosticV2>();
        ValidateSequence(plan.OnDown, "on_down", diagnostics);

        if (plan.WhileHeld is not null)
        {
            if (plan.WhileHeld.Interval < TimeSpan.Zero)
            {
                diagnostics.Add(Error(
                    ActionValidationCodeV2.NegativeWhileHeldInterval,
                    "while_held interval must not be negative.",
                    "while_held.interval"));
            }

            if (plan.WhileHeld.Body.Count == 0)
            {
                diagnostics.Add(new ActionValidationDiagnosticV2(
                    ActionValidationSeverityV2.Warning,
                    ActionValidationCodeV2.EmptyWhileHeldBody,
                    "while_held body is empty.",
                    "while_held.body"));
            }

            ValidateSequence(plan.WhileHeld.Body, "while_held.body", diagnostics);
        }

        ValidateSequence(plan.OnUp, "on_up", diagnostics);
        return diagnostics;
    }

    public bool IsValid(ActivationPlanV2 plan)
    {
        return !Validate(plan).Any(diagnostic => diagnostic.Severity == ActionValidationSeverityV2.Error);
    }

    private static void ValidateSequence(
        IReadOnlyList<ActionModelV2> actions,
        string path,
        List<ActionValidationDiagnosticV2> diagnostics)
    {
        for (var index = 0; index < actions.Count; index++)
        {
            ValidateAction(actions[index], $"{path}[{index}]", diagnostics);
        }
    }

    private static void ValidateAction(
        ActionModelV2 action,
        string path,
        List<ActionValidationDiagnosticV2> diagnostics)
    {
        switch (action)
        {
            case KeyActionV2 keyAction:
                ValidateKeyAction(keyAction, path, diagnostics);
                break;
            case MouseButtonActionV2 mouseAction:
                ValidateMouseButtonAction(mouseAction, path, diagnostics);
                break;
            case WheelActionV2 wheelAction:
                ValidateWheelAction(wheelAction, path, diagnostics);
                break;
            case WaitActionV2 waitAction:
                ValidateWaitAction(waitAction, path, diagnostics);
                break;
            case RepeatActionV2 repeatAction:
                ValidateRepeatAction(repeatAction, path, diagnostics);
                break;
            case CoordinateActionV2 coordinateAction:
                ValidateCoordinateAction(coordinateAction, path, diagnostics);
                break;
            default:
                diagnostics.Add(Error(
                    ActionValidationCodeV2.UnknownActionType,
                    $"Unknown action type {action.GetType().Name}.",
                    path));
                break;
        }
    }

    private static void ValidateKeyAction(
        KeyActionV2 action,
        string path,
        List<ActionValidationDiagnosticV2> diagnostics)
    {
        if (!IsKeyActionKind(action.Kind))
        {
            diagnostics.Add(Error(
                ActionValidationCodeV2.ActionKindMismatch,
                $"{action.Kind} is not a key action kind.",
                path));
        }

        if (!action.Key.CanBeKeyOutput || action.Key.Kind != InputKind.KeyboardKey)
        {
            diagnostics.Add(Error(
                ActionValidationCodeV2.InvalidKeyActionInput,
                $"{action.Key.CanonicalName} cannot be used as a key action input.",
                path));
        }

        ValidateInputActionDuration(
            action.Kind,
            action.Duration,
            path,
            diagnostics,
            tapKind: ActionKindV2.TapKey,
            downKind: ActionKindV2.DownKey,
            upKind: ActionKindV2.UpKey,
            holdKind: ActionKindV2.HoldKey);
    }

    private static void ValidateMouseButtonAction(
        MouseButtonActionV2 action,
        string path,
        List<ActionValidationDiagnosticV2> diagnostics)
    {
        if (!IsMouseButtonActionKind(action.Kind))
        {
            diagnostics.Add(Error(
                ActionValidationCodeV2.ActionKindMismatch,
                $"{action.Kind} is not a mouse button action kind.",
                path));
        }

        if (!action.Button.CanBeMouseOutput || action.Button.Kind != InputKind.MouseButton)
        {
            diagnostics.Add(Error(
                ActionValidationCodeV2.InvalidMouseButtonActionInput,
                $"{action.Button.CanonicalName} cannot be used as a mouse button action input.",
                path));
        }

        ValidateInputActionDuration(
            action.Kind,
            action.Duration,
            path,
            diagnostics,
            tapKind: ActionKindV2.TapMouse,
            downKind: ActionKindV2.DownMouse,
            upKind: ActionKindV2.UpMouse,
            holdKind: ActionKindV2.HoldMouse);
    }

    private static void ValidateWheelAction(
        WheelActionV2 action,
        string path,
        List<ActionValidationDiagnosticV2> diagnostics)
    {
        if (action.Steps <= 0)
        {
            diagnostics.Add(Error(
                ActionValidationCodeV2.InvalidWheelActionSteps,
                "Wheel action steps must be greater than zero.",
                path));
        }
    }

    private static void ValidateWaitAction(
        WaitActionV2 action,
        string path,
        List<ActionValidationDiagnosticV2> diagnostics)
    {
        if (action.Duration < TimeSpan.Zero)
        {
            diagnostics.Add(Error(
                ActionValidationCodeV2.InvalidWaitDuration,
                "Wait duration must not be negative.",
                path));
        }
    }

    private static void ValidateRepeatAction(
        RepeatActionV2 action,
        string path,
        List<ActionValidationDiagnosticV2> diagnostics)
    {
        if (action.Count <= 0)
        {
            diagnostics.Add(Error(
                ActionValidationCodeV2.InvalidRepeatCount,
                "Repeat count must be greater than zero.",
                path));
        }

        if (action.Body.Count == 0)
        {
            diagnostics.Add(new ActionValidationDiagnosticV2(
                ActionValidationSeverityV2.Warning,
                ActionValidationCodeV2.EmptyRepeatBody,
                "Repeat body must not be empty.",
                $"{path}.body"));
        }

        ValidateSequence(action.Body, $"{path}.body", diagnostics);
    }

    private static void ValidateCoordinateAction(
        CoordinateActionV2 action,
        string path,
        List<ActionValidationDiagnosticV2> diagnostics)
    {
        if (!IsCoordinateActionKind(action.Kind))
        {
            diagnostics.Add(Error(
                ActionValidationCodeV2.ActionKindMismatch,
                $"{action.Kind} is not a coordinate action kind.",
                path));
        }

        ValidateCoordinatePoint(action.Start, $"{path}.start", diagnostics);
        if (action.Kind == ActionKindV2.DragAt)
        {
            if (action.End is null)
            {
                diagnostics.Add(Error(
                    ActionValidationCodeV2.MissingDragEndPoint,
                    "drag_at requires an end coordinate.",
                    $"{path}.end"));
            }
            else
            {
                ValidateCoordinatePoint(action.End, $"{path}.end", diagnostics);
            }
        }
        else if (action.End is not null)
        {
            diagnostics.Add(Error(
                ActionValidationCodeV2.UnexpectedDragEndPoint,
                $"{action.Kind} must not include an end coordinate.",
                $"{path}.end"));
        }

        var button = action.Button ?? InputNameResolverV2.Resolve("mouse_left");
        if (!button.CanBeMouseOutput || button.Kind != InputKind.MouseButton)
        {
            diagnostics.Add(Error(
                ActionValidationCodeV2.InvalidCoordinateButton,
                $"{button.CanonicalName} cannot be used as a coordinate action button.",
                $"{path}.button"));
        }

        ValidateCoordinateActionDuration(action.Kind, action.Duration, path, diagnostics);
        diagnostics.Add(new ActionValidationDiagnosticV2(
            ActionValidationSeverityV2.Info,
            ActionValidationCodeV2.CoordinateActionNotLiveReady,
            "Coordinate action is a data skeleton only; capture, transform, backend, and live execution are not implemented.",
            path));
    }

    private static void ValidateCoordinatePoint(
        CoordinatePointV2 point,
        string path,
        List<ActionValidationDiagnosticV2> diagnostics)
    {
        if (point.LogicalX < 0 || point.LogicalY < 0)
        {
            diagnostics.Add(Error(
                ActionValidationCodeV2.InvalidCoordinate,
                "Coordinate logical x/y must be non-negative.",
                path));
        }

        if (string.IsNullOrWhiteSpace(point.ProfileId))
        {
            diagnostics.Add(Error(
                ActionValidationCodeV2.InvalidCoordinateProfile,
                "Coordinate profile id must not be empty.",
                $"{path}.profile"));
        }
    }

    private static void ValidateOptionalDuration(
        TimeSpan? duration,
        string path,
        List<ActionValidationDiagnosticV2> diagnostics)
    {
        if (duration < TimeSpan.Zero)
        {
            diagnostics.Add(Error(
                ActionValidationCodeV2.InvalidActionDuration,
                "Duration must not be negative.",
                $"{path}.duration"));
        }
    }

    private static void ValidateInputActionDuration(
        ActionKindV2 kind,
        TimeSpan? duration,
        string path,
        List<ActionValidationDiagnosticV2> diagnostics,
        ActionKindV2 tapKind,
        ActionKindV2 downKind,
        ActionKindV2 upKind,
        ActionKindV2 holdKind)
    {
        if (kind == tapKind)
        {
            ValidateOptionalDuration(duration, path, diagnostics);
            return;
        }

        if (kind == holdKind)
        {
            ValidateRequiredDuration(duration, path, diagnostics);
            return;
        }

        if ((kind == downKind || kind == upKind) && duration is not null)
        {
            diagnostics.Add(Error(
                ActionValidationCodeV2.DisallowedActionDuration,
                $"{kind} must not include a duration.",
                $"{path}.duration"));
        }
    }

    private static void ValidateCoordinateActionDuration(
        ActionKindV2 kind,
        TimeSpan? duration,
        string path,
        List<ActionValidationDiagnosticV2> diagnostics)
    {
        switch (kind)
        {
            case ActionKindV2.TapAt:
                ValidateOptionalDuration(duration, path, diagnostics);
                break;
            case ActionKindV2.HoldAt:
            case ActionKindV2.DragAt:
                ValidateRequiredDuration(duration, path, diagnostics);
                break;
            case ActionKindV2.DownAt:
            case ActionKindV2.UpAt:
                if (duration is not null)
                {
                    diagnostics.Add(Error(
                        ActionValidationCodeV2.DisallowedActionDuration,
                        $"{kind} must not include a duration.",
                        $"{path}.duration"));
                }

                break;
        }
    }

    private static void ValidateRequiredDuration(
        TimeSpan? duration,
        string path,
        List<ActionValidationDiagnosticV2> diagnostics)
    {
        if (duration is null)
        {
            diagnostics.Add(Error(
                ActionValidationCodeV2.MissingActionDuration,
                "Duration is required.",
                $"{path}.duration"));
            return;
        }

        ValidateOptionalDuration(duration, path, diagnostics);
    }

    private static bool IsKeyActionKind(ActionKindV2 kind)
    {
        return kind is ActionKindV2.TapKey or ActionKindV2.DownKey or ActionKindV2.UpKey or ActionKindV2.HoldKey;
    }

    private static bool IsMouseButtonActionKind(ActionKindV2 kind)
    {
        return kind is ActionKindV2.TapMouse or ActionKindV2.DownMouse or ActionKindV2.UpMouse or ActionKindV2.HoldMouse;
    }

    private static bool IsCoordinateActionKind(ActionKindV2 kind)
    {
        return kind is ActionKindV2.TapAt or ActionKindV2.DownAt or ActionKindV2.UpAt or ActionKindV2.HoldAt or ActionKindV2.DragAt;
    }

    private static ActionValidationDiagnosticV2 Error(
        ActionValidationCodeV2 code,
        string message,
        string path)
    {
        return new ActionValidationDiagnosticV2(
            ActionValidationSeverityV2.Error,
            code,
            message,
            path);
    }
}
