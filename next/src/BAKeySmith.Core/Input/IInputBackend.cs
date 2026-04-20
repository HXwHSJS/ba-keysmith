namespace BAKeySmith.Core.Input;

public interface IInputBackend
{
    ValueTask SendAsync(IReadOnlyList<InputCommand> commands, CancellationToken cancellationToken);
    ValueTask KeyDownAsync(string key, CancellationToken cancellationToken);
    ValueTask KeyUpAsync(string key, CancellationToken cancellationToken);
    ValueTask MouseDownAsync(string button, CancellationToken cancellationToken);
    ValueTask MouseUpAsync(string button, CancellationToken cancellationToken);
    ValueTask MoveMouseToAsync(int x, int y, CancellationToken cancellationToken);
    ValueTask<(int X, int Y)> GetMousePositionAsync(CancellationToken cancellationToken);
    ValueTask<(int Width, int Height)> GetScreenSizeAsync(CancellationToken cancellationToken);
}
