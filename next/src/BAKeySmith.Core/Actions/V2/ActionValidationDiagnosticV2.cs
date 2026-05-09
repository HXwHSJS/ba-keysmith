namespace BAKeySmith.Core.Actions.V2;

public enum ActionValidationSeverityV2
{
    Info,
    Warning,
    Error
}

public enum ActionValidationCodeV2
{
    UnknownActionType,
    ActionKindMismatch,
    EmptyWhileHeldBody,
    NegativeWhileHeldInterval,
    InvalidKeyActionInput,
    InvalidMouseButtonActionInput,
    InvalidWheelActionSteps,
    InvalidWaitDuration,
    InvalidActionDuration,
    MissingActionDuration,
    DisallowedActionDuration,
    InvalidRepeatCount,
    EmptyRepeatBody,
    InvalidCoordinate,
    InvalidCoordinateProfile,
    InvalidCoordinateButton,
    MissingDragEndPoint,
    UnexpectedDragEndPoint,
    CoordinateActionNotLiveReady
}

public sealed record ActionValidationDiagnosticV2(
    ActionValidationSeverityV2 Severity,
    ActionValidationCodeV2 Code,
    string Message,
    string Path);
