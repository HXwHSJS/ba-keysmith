namespace BAKeySmith.Core.Input.V2.Capture;

public sealed record WindowsKeyEventV2(
    uint VirtualKeyCode,
    uint ScanCode,
    bool IsExtended,
    bool IsDown,
    InputCaptureSourceKind SourceKind = InputCaptureSourceKind.ManualSynthetic);
