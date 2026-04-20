using BAKeySmith.Core.Contracts;

namespace BAKeySmith.Core.Triggers;

public sealed class ManualTriggerSource : ITriggerSource
{
    public event EventHandler<TriggerEvent>? Triggered;

    public bool IsRunning { get; private set; }

    public ValueTask StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IsRunning = true;
        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IsRunning = false;
        return ValueTask.CompletedTask;
    }

    public void Emit(TriggerEvent triggerEvent)
    {
        if (IsRunning)
        {
            Triggered?.Invoke(this, triggerEvent);
        }
    }

    public void KeyDown(string key) => Emit(TriggerEvent.Down(TriggerSpec.Keyboard(key)));
    public void KeyUp(string key) => Emit(TriggerEvent.Up(TriggerSpec.Keyboard(key)));
    public void MouseDown(string button) => Emit(TriggerEvent.Down(TriggerSpec.Mouse(button)));
    public void MouseUp(string button) => Emit(TriggerEvent.Up(TriggerSpec.Mouse(button)));

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
    }
}
