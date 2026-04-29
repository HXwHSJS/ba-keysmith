namespace BAKeySmith.App.Models;

public enum KeyCaptureResultKind
{
    Captured,
    Incomplete,
    Unsupported
}

public sealed record KeyCaptureResult(
    KeyCaptureResultKind Kind,
    string Value,
    string Message)
{
    public static KeyCaptureResult Captured(string value, string message = "")
    {
        return new KeyCaptureResult(KeyCaptureResultKind.Captured, value, message);
    }

    public static KeyCaptureResult Incomplete(string message)
    {
        return new KeyCaptureResult(KeyCaptureResultKind.Incomplete, string.Empty, message);
    }

    public static KeyCaptureResult Unsupported(string message)
    {
        return new KeyCaptureResult(KeyCaptureResultKind.Unsupported, string.Empty, message);
    }
}
