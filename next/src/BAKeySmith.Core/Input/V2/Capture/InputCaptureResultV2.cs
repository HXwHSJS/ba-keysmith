namespace BAKeySmith.Core.Input.V2.Capture;

public sealed record InputCaptureResultV2(
    bool Success,
    InputSpec? Input,
    string Message,
    InputCaptureSourceKind SourceKind)
{
    public static InputCaptureResultV2 Captured(
        InputSpec input,
        InputCaptureSourceKind sourceKind)
    {
        return new InputCaptureResultV2(true, input, string.Empty, sourceKind);
    }

    public static InputCaptureResultV2 Unsupported(
        string message,
        InputCaptureSourceKind sourceKind)
    {
        return new InputCaptureResultV2(false, null, message, sourceKind);
    }

    public bool CanUseFor(InputCapturePurpose purpose)
    {
        if (!Success || Input is null)
        {
            return false;
        }

        return purpose switch
        {
            InputCapturePurpose.MappingTrigger => Input.CanBeMappingTrigger,
            InputCapturePurpose.SimpleTarget => Input.CanBeKeyOutput || Input.CanBeMouseOutput,
            InputCapturePurpose.ControlHotkey => Input.CanBeHotkeyModifier || Input.CanBeHotkeyMainKey,
            InputCapturePurpose.CoordinateRecordHotkey => Input.CanBeCoordinateRecordHotkey,
            InputCapturePurpose.EmergencyStopHotkey => Input.CanBeHotkeyModifier || Input.CanBeHotkeyMainKey,
            _ => false
        };
    }

    public bool CanUseAsHotkeyMainKey()
    {
        return Success && Input?.CanBeHotkeyMainKey == true;
    }

    public bool CanUseAsCoordinateRecordHotkeyMainInput()
    {
        return Success &&
            Input is not null &&
            Input.CanBeCoordinateRecordHotkey &&
            (Input.CanBeHotkeyMainKey || Input.Kind == InputKind.MouseButton);
    }
}
