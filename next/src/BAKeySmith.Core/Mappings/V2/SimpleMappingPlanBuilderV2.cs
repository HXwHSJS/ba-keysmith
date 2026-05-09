using BAKeySmith.Core.Actions.V2;
using BAKeySmith.Core.Input.V2;

namespace BAKeySmith.Core.Mappings.V2;

public sealed record SimpleMappingPlanBuildResultV2(
    ActivationPlanV2? Plan,
    IReadOnlyList<SimpleMappingValidationDiagnosticV2> Diagnostics)
{
    public bool Success =>
        Plan is not null &&
        !Diagnostics.Any(diagnostic => diagnostic.Severity == SimpleMappingValidationSeverityV2.Error);
}

public sealed class SimpleMappingPlanBuilderV2
{
    private readonly ActivationPlanValidatorV2 _actionValidator = new();

    public SimpleMappingPlanBuildResultV2 Build(SimpleMappingDefinitionV2 definition)
    {
        var diagnostics = new List<SimpleMappingValidationDiagnosticV2>();
        ValidateMode(definition.Mode, diagnostics);
        ValidateDuration(definition, diagnostics);

        var plan = diagnostics.Any(IsError)
            ? null
            : BuildPlan(definition, diagnostics);

        if (plan is not null)
        {
            // Supported builder paths should generate valid plans. Keep this
            // defensive conversion so future builder branches cannot bypass
            // ActivationPlanValidatorV2 when they start emitting richer plans.
            var actionDiagnostics = _actionValidator.Validate(plan);
            foreach (var actionDiagnostic in actionDiagnostics.Where(diagnostic =>
                         diagnostic.Severity == ActionValidationSeverityV2.Error))
            {
                diagnostics.Add(Error(
                    SimpleMappingValidationCodeV2.GeneratedPlanInvalid,
                    $"Generated ActivationPlanV2 is invalid: {actionDiagnostic.Code} {actionDiagnostic.Message}",
                    actionDiagnostic.Path));
            }

            if (diagnostics.Any(IsError))
            {
                plan = null;
            }
        }

        return new SimpleMappingPlanBuildResultV2(plan, diagnostics);
    }

    private static ActivationPlanV2? BuildPlan(
        SimpleMappingDefinitionV2 definition,
        List<SimpleMappingValidationDiagnosticV2> diagnostics)
    {
        if (definition.Target is null)
        {
            diagnostics.Add(Error(
                SimpleMappingValidationCodeV2.InvalidTargetKind,
                "Simple mapping target is missing.",
                "target"));
            return null;
        }

        return definition.Target.Kind switch
        {
            SimpleMappingTargetKindV2.Input => BuildInputPlan(definition, diagnostics),
            SimpleMappingTargetKindV2.Coordinate => BuildCoordinatePlan(definition, diagnostics),
            _ => InvalidTargetKind(definition.Target.Kind, diagnostics)
        };
    }

    private static ActivationPlanV2? BuildInputPlan(
        SimpleMappingDefinitionV2 definition,
        List<SimpleMappingValidationDiagnosticV2> diagnostics)
    {
        if (definition.Target.Input is null)
        {
            diagnostics.Add(Error(
                SimpleMappingValidationCodeV2.MissingInputSpec,
                "Simple mapping input target is missing InputSpec.",
                "target.input"));
            return null;
        }

        if (definition.Target.Coordinate is not null)
        {
            diagnostics.Add(Error(
                SimpleMappingValidationCodeV2.InvalidTargetPayload,
                "Simple mapping input target must not include a coordinate payload.",
                "target.coordinate"));
            return null;
        }

        var target = definition.Target.Input;
        return target.Kind switch
        {
            InputKind.KeyboardKey when target.CanBeKeyOutput => BuildKeyPlan(definition.Mode, target, definition.Duration),
            InputKind.KeyboardKey => UnsupportedInputTarget(target, diagnostics),
            InputKind.MouseButton when target.CanBeMouseOutput => BuildMouseButtonPlan(definition.Mode, target, definition.Duration),
            InputKind.MouseButton => UnsupportedInputTarget(target, diagnostics),
            InputKind.MouseWheel => UnsupportedWheelTarget(target, diagnostics),
            _ => InvalidTargetKind(definition.Target.Kind, diagnostics)
        };
    }

    private static ActivationPlanV2? BuildCoordinatePlan(
        SimpleMappingDefinitionV2 definition,
        List<SimpleMappingValidationDiagnosticV2> diagnostics)
    {
        if (definition.Target.Coordinate is null)
        {
            diagnostics.Add(Error(
                SimpleMappingValidationCodeV2.InvalidTargetPayload,
                "Simple mapping coordinate target is missing coordinate payload.",
                "target.coordinate"));
            return null;
        }

        if (definition.Target.Input is not null)
        {
            diagnostics.Add(Error(
                SimpleMappingValidationCodeV2.InvalidTargetPayload,
                "Simple mapping coordinate target must not include an input payload.",
                "target.input"));
            return null;
        }

        return UnsupportedCoordinateTarget(diagnostics);
    }

    private static ActivationPlanV2? BuildKeyPlan(
        SimpleMappingModeV2 mode,
        InputSpec target,
        TimeSpan? duration)
    {
        return mode switch
        {
            SimpleMappingModeV2.Tap => new ActivationPlanV2(
            [
                KeyActionV2.Tap(target, duration)
            ]),
            SimpleMappingModeV2.Hold => new ActivationPlanV2(
            [
                KeyActionV2.Down(target)
            ],
            OnUp:
            [
                KeyActionV2.Up(target)
            ]),
            _ => null
        };
    }

    private static ActivationPlanV2? BuildMouseButtonPlan(
        SimpleMappingModeV2 mode,
        InputSpec target,
        TimeSpan? duration)
    {
        return mode switch
        {
            SimpleMappingModeV2.Tap => new ActivationPlanV2(
            [
                MouseButtonActionV2.Tap(target, duration)
            ]),
            SimpleMappingModeV2.Hold => new ActivationPlanV2(
            [
                MouseButtonActionV2.Down(target)
            ],
            OnUp:
            [
                MouseButtonActionV2.Up(target)
            ]),
            _ => null
        };
    }

    private static void ValidateMode(
        SimpleMappingModeV2 mode,
        List<SimpleMappingValidationDiagnosticV2> diagnostics)
    {
        if (!Enum.IsDefined(mode))
        {
            diagnostics.Add(Error(
                SimpleMappingValidationCodeV2.UnsupportedMode,
                $"{mode} is not a supported simple mapping mode.",
                "mode"));
        }
    }

    private static void ValidateDuration(
        SimpleMappingDefinitionV2 definition,
        List<SimpleMappingValidationDiagnosticV2> diagnostics)
    {
        if (definition.Duration is { } duration && duration < TimeSpan.Zero)
        {
            diagnostics.Add(Error(
                SimpleMappingValidationCodeV2.InvalidDuration,
                "Simple mapping duration must not be negative.",
                "duration"));
        }

        if (definition.Mode == SimpleMappingModeV2.Hold && definition.Duration is not null)
        {
            diagnostics.Add(Error(
                SimpleMappingValidationCodeV2.InvalidDuration,
                "Simple hold mapping does not accept tap duration in the v2 skeleton.",
                "duration"));
        }
    }

    private static ActivationPlanV2? UnsupportedInputTarget(
        InputSpec target,
        List<SimpleMappingValidationDiagnosticV2> diagnostics)
    {
        diagnostics.Add(Error(
            SimpleMappingValidationCodeV2.UnsupportedTarget,
            $"{target.CanonicalName} is not supported as a simple mapping output target.",
            "target"));
        return null;
    }

    private static ActivationPlanV2? UnsupportedWheelTarget(
        InputSpec target,
        List<SimpleMappingValidationDiagnosticV2> diagnostics)
    {
        diagnostics.Add(Error(
            SimpleMappingValidationCodeV2.UnsupportedTarget,
            $"{target.CanonicalName} is a wheel input and is not supported as a simple mapping output target.",
            "target"));
        return null;
    }

    private static ActivationPlanV2? UnsupportedCoordinateTarget(
        List<SimpleMappingValidationDiagnosticV2> diagnostics)
    {
        diagnostics.Add(Error(
            SimpleMappingValidationCodeV2.UnsupportedTarget,
            "Coordinate target is not supported by Simple Mapping v2 skeleton.",
            "target"));
        return null;
    }

    private static ActivationPlanV2? InvalidTargetKind(
        SimpleMappingTargetKindV2 kind,
        List<SimpleMappingValidationDiagnosticV2> diagnostics)
    {
        diagnostics.Add(Error(
            SimpleMappingValidationCodeV2.InvalidTargetKind,
            $"{kind} is not a valid simple mapping target kind.",
            "target.kind"));
        return null;
    }

    private static bool IsError(SimpleMappingValidationDiagnosticV2 diagnostic)
    {
        return diagnostic.Severity == SimpleMappingValidationSeverityV2.Error;
    }

    private static SimpleMappingValidationDiagnosticV2 Error(
        SimpleMappingValidationCodeV2 code,
        string message,
        string path)
    {
        return new SimpleMappingValidationDiagnosticV2(
            SimpleMappingValidationSeverityV2.Error,
            code,
            message,
            path);
    }
}
