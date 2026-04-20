namespace BAKeySmith.Core.Runtime;

public sealed class InputSequencer
{
    private readonly PressOwnershipTracker _presses;
    private readonly TimeSpan _tapHold;

    public InputSequencer(PressOwnershipTracker presses, TimeSpan? tapHold = null)
    {
        _presses = presses;
        _tapHold = tapHold ?? TimeSpan.FromMilliseconds(20);
    }

    public async ValueTask TapKeyAsync(string owner, string key, CancellationToken cancellationToken)
    {
        var held = false;
        try
        {
            await _presses.HoldKeyAsync(owner, key, cancellationToken);
            held = true;
            await Task.Delay(_tapHold, cancellationToken);
        }
        finally
        {
            if (held)
            {
                await _presses.ReleaseKeyAsync(owner, key, CancellationToken.None);
            }
        }
    }
}
