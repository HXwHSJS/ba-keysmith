namespace BAKeySmith.Core.Input.V2.Capture;

public enum WindowsMouseEventKindV2
{
    LeftButton,
    RightButton,
    MiddleButton,
    XButton1,
    XButton2,
    Wheel
}

public sealed record WindowsMouseEventV2(
    WindowsMouseEventKindV2 Kind,
    bool IsDown,
    int WheelDelta = 0,
    InputCaptureSourceKind SourceKind = InputCaptureSourceKind.ManualSynthetic);
