using BAKeySmith.Core.Actions.V2;
using BAKeySmith.Core.Input.V2;

namespace BAKeySmith.Core.Runtime.V2;

public sealed class FakeInputBackendV2
{
    private readonly List<RuntimeV2Event> _events = [];
    private long _nextSequence;
    private string? _currentMappingId;

    public IReadOnlyList<RuntimeV2Event> Events => _events;
    public TimeSpan VirtualTime { get; private set; }

    public IDisposable BeginMappingContext(string mappingId)
    {
        var previous = _currentMappingId;
        _currentMappingId = mappingId;
        return new MappingContext(this, previous);
    }

    public void KeyDown(InputSpec key, string path, bool isCleanup = false)
    {
        Add(RuntimeV2EventKind.KeyDown, path, key.CanonicalName, isCleanup: isCleanup);
    }

    public void KeyUp(InputSpec key, string path, bool isCleanup = false)
    {
        Add(RuntimeV2EventKind.KeyUp, path, key.CanonicalName, isCleanup: isCleanup);
    }

    public void KeyTap(InputSpec key, string path, TimeSpan? duration = null)
    {
        Add(RuntimeV2EventKind.KeyTap, path, key.CanonicalName, duration);
    }

    public void MouseDown(InputSpec button, string path, bool isCleanup = false)
    {
        Add(RuntimeV2EventKind.MouseDown, path, button.CanonicalName, isCleanup: isCleanup);
    }

    public void MouseUp(InputSpec button, string path, bool isCleanup = false)
    {
        Add(RuntimeV2EventKind.MouseUp, path, button.CanonicalName, isCleanup: isCleanup);
    }

    public void MouseTap(InputSpec button, string path, TimeSpan? duration = null)
    {
        Add(RuntimeV2EventKind.MouseTap, path, button.CanonicalName, duration);
    }

    public void Wheel(WheelDirectionV2 direction, int steps, string path)
    {
        _events.Add(new RuntimeV2Event(
            Sequence: _nextSequence++,
            Kind: RuntimeV2EventKind.Wheel,
            Path: path,
            MappingId: _currentMappingId,
            WheelDirection: direction,
            WheelSteps: steps));
    }

    public void Wait(TimeSpan duration, string path)
    {
        VirtualTime += duration;
        Add(RuntimeV2EventKind.Wait, path, duration: duration);
    }

    public void CoordinateUnsupported(
        CoordinatePointV2 point,
        string path,
        bool isCleanup = false)
    {
        _events.Add(new RuntimeV2Event(
            Sequence: _nextSequence++,
            Kind: RuntimeV2EventKind.CoordinateUnsupported,
            Path: path,
            MappingId: _currentMappingId,
            CoordinateProfileId: point.ProfileId,
            LogicalX: point.LogicalX,
            LogicalY: point.LogicalY,
            IsCleanup: isCleanup));
    }

    private void Add(
        RuntimeV2EventKind kind,
        string path,
        string? canonicalInputName = null,
        TimeSpan? duration = null,
        bool isCleanup = false)
    {
        _events.Add(new RuntimeV2Event(
            Sequence: _nextSequence++,
            Kind: kind,
            Path: path,
            MappingId: _currentMappingId,
            CanonicalInputName: canonicalInputName,
            Duration: duration,
            IsCleanup: isCleanup));
    }

    private sealed class MappingContext : IDisposable
    {
        private readonly FakeInputBackendV2 _backend;
        private readonly string? _previous;
        private bool _disposed;

        public MappingContext(FakeInputBackendV2 backend, string? previous)
        {
            _backend = backend;
            _previous = previous;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _backend._currentMappingId = _previous;
            _disposed = true;
        }
    }
}
