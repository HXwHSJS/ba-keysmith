using System.Collections.Concurrent;
using System.Diagnostics;

namespace BAKeySmith.Core.Input;

public sealed class DryRunInputBackend : IInputBackend
{
    private readonly ConcurrentQueue<InputEvent> _events = new();
    private int _mouseX;
    private int _mouseY;

    public IReadOnlyList<InputEvent> Events => _events.ToArray();
    public (int Width, int Height) ScreenSize { get; set; } = (1920, 1080);

    public ValueTask SendAsync(IReadOnlyList<InputCommand> commands, CancellationToken cancellationToken)
    {
        foreach (var command in commands)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (command.Kind.Trim().ToLowerInvariant())
            {
                case "key":
                case "mouse":
                    if (command.IsDown is null)
                    {
                        throw new ArgumentException("Key and mouse commands require IsDown.");
                    }

                    Record(command.Kind, command.Code, command.IsDown.Value);
                    break;
                case "move":
                    _mouseX = command.X ?? _mouseX;
                    _mouseY = command.Y ?? _mouseY;
                    _events.Enqueue(new InputEvent(
                        Stopwatch.GetTimestamp(),
                        "move",
                        "cursor",
                        X: command.X,
                        Y: command.Y));
                    break;
                case "wheel":
                    if (command.Delta is null)
                    {
                        throw new ArgumentException("Wheel commands require Delta.");
                    }

                    _events.Enqueue(new InputEvent(
                        Stopwatch.GetTimestamp(),
                        "wheel",
                        command.Delta.Value >= 0 ? "mouse_wheel_up" : "mouse_wheel_down"));
                    break;
                default:
                    throw new ArgumentException($"Unsupported input command kind: {command.Kind}");
            }
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask KeyDownAsync(string key, CancellationToken cancellationToken)
    {
        return SendAsync([InputCommand.KeyDown(key)], cancellationToken);
    }

    public ValueTask KeyUpAsync(string key, CancellationToken cancellationToken)
    {
        return SendAsync([InputCommand.KeyUp(key)], cancellationToken);
    }

    public ValueTask MouseDownAsync(string button, CancellationToken cancellationToken)
    {
        return SendAsync([InputCommand.MouseDown(button)], cancellationToken);
    }

    public ValueTask MouseUpAsync(string button, CancellationToken cancellationToken)
    {
        return SendAsync([InputCommand.MouseUp(button)], cancellationToken);
    }

    public ValueTask MoveMouseToAsync(int x, int y, CancellationToken cancellationToken)
    {
        return SendAsync([InputCommand.MoveMouseTo(x, y)], cancellationToken);
    }

    public ValueTask<(int X, int Y)> GetMousePositionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult((_mouseX, _mouseY));
    }

    public ValueTask<(int Width, int Height)> GetScreenSizeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(ScreenSize);
    }

    public int Count(string kind, string code, bool? isDown = null)
    {
        return Events.Count(e =>
            string.Equals(e.Kind, kind, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.Code, code, StringComparison.OrdinalIgnoreCase) &&
            (isDown is null || e.IsDown == isDown));
    }

    private void Record(string kind, string code, bool isDown)
    {
        _events.Enqueue(new InputEvent(
            Stopwatch.GetTimestamp(),
            kind,
            Normalize(code),
            isDown));
    }

    private static string Normalize(string code) => code.Trim().ToLowerInvariant();
}
