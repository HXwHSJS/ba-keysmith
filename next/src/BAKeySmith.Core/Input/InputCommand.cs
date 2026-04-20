namespace BAKeySmith.Core.Input;

public sealed record InputCommand(
    string Kind,
    string Code,
    bool? IsDown = null,
    int? X = null,
    int? Y = null,
    int? Delta = null)
{
    public static InputCommand KeyDown(string key) => new("key", Normalize(key), true);
    public static InputCommand KeyUp(string key) => new("key", Normalize(key), false);
    public static InputCommand MouseDown(string button) => new("mouse", Normalize(button), true);
    public static InputCommand MouseUp(string button) => new("mouse", Normalize(button), false);
    public static InputCommand MouseWheel(int delta) => new("wheel", "mouse_wheel", Delta: delta);
    public static InputCommand MoveMouseTo(int x, int y) => new("move", "cursor", X: x, Y: y);

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Input code must not be empty.", nameof(value));
        }

        return value.Trim().ToLowerInvariant();
    }
}
