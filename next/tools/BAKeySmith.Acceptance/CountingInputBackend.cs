using System.Collections.Concurrent;
using System.Diagnostics;
using BAKeySmith.Core.Input;

internal sealed class CountingInputBackend : IInputBackend
{
    private readonly IInputBackend _inner;
    private readonly ConcurrentQueue<InputEvent> _events = new();

    public CountingInputBackend(IInputBackend inner)
    {
        _inner = inner;
    }

    public IReadOnlyList<InputEvent> Events => _events.ToArray();

    public async ValueTask SendAsync(IReadOnlyList<InputCommand> commands, CancellationToken cancellationToken)
    {
        await _inner.SendAsync(commands, cancellationToken);
        foreach (var command in commands)
        {
            switch (command.Kind.Trim().ToLowerInvariant())
            {
                case "key":
                case "mouse":
                    if (command.IsDown is not null)
                    {
                        _events.Enqueue(new InputEvent(
                            Stopwatch.GetTimestamp(),
                            command.Kind,
                            NormalizeCode(command),
                            command.IsDown.Value));
                    }

                    break;
                case "move":
                    _events.Enqueue(new InputEvent(
                        Stopwatch.GetTimestamp(),
                        "move",
                        "cursor",
                        X: command.X,
                        Y: command.Y));
                    break;
                case "wheel":
                    _events.Enqueue(new InputEvent(
                        Stopwatch.GetTimestamp(),
                        "wheel",
                        (command.Delta ?? 0) >= 0 ? "mouse_wheel_up" : "mouse_wheel_down"));
                    break;
            }
        }
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
        return _inner.GetMousePositionAsync(cancellationToken);
    }

    public ValueTask<(int Width, int Height)> GetScreenSizeAsync(CancellationToken cancellationToken)
    {
        return _inner.GetScreenSizeAsync(cancellationToken);
    }

    public int Count(string kind, string code, bool? isDown = null)
    {
        return Events.Count(e =>
            string.Equals(e.Kind, kind, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.Code, code, StringComparison.OrdinalIgnoreCase) &&
            (isDown is null || e.IsDown == isDown));
    }

    public void Reset()
    {
        while (_events.TryDequeue(out _))
        {
        }
    }

    private static string NormalizeCode(InputCommand command)
    {
        return command.Kind.Trim().Equals("mouse", StringComparison.OrdinalIgnoreCase)
            ? KeyNameResolver.NormalizeMouseButton(command.Code)
            : KeyNameResolver.Normalize(command.Code);
    }
}
