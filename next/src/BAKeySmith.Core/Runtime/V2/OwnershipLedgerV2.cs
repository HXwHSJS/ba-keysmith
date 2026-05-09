using BAKeySmith.Core.Input.V2;

namespace BAKeySmith.Core.Runtime.V2;

public sealed class OwnershipLedgerV2
{
    private readonly List<OwnershipResourceV2> _held = [];

    public IReadOnlyList<OwnershipResourceV2> HeldResources => _held;

    public bool Acquire(
        OwnershipResourceV2 resource,
        string path,
        ICollection<RuntimeV2Diagnostic> diagnostics)
    {
        if (_held.Contains(resource))
        {
            diagnostics.Add(new RuntimeV2Diagnostic(
                RuntimeV2DiagnosticSeverity.Warning,
                RuntimeV2DiagnosticCode.DuplicateAcquire,
                $"{resource.DisplayName} is already held by this Runtime v2 sandbox session.",
                path));
            return false;
        }

        _held.Add(resource);
        return true;
    }

    public bool Release(
        OwnershipResourceV2 resource,
        string path,
        ICollection<RuntimeV2Diagnostic> diagnostics)
    {
        if (!_held.Remove(resource))
        {
            diagnostics.Add(new RuntimeV2Diagnostic(
                RuntimeV2DiagnosticSeverity.Warning,
                RuntimeV2DiagnosticCode.ReleaseWithoutAcquire,
                $"{resource.DisplayName} was released without a matching acquire.",
                path));
            return false;
        }

        return true;
    }

    public void ReleaseAll(
        FakeInputBackendV2 backend,
        ICollection<RuntimeV2Diagnostic> diagnostics,
        string path = "cleanup")
    {
        foreach (var resource in _held.ToArray().Reverse())
        {
            ReleaseForCleanup(backend, resource, path);
        }

        _held.Clear();
    }

    private static void ReleaseForCleanup(
        FakeInputBackendV2 backend,
        OwnershipResourceV2 resource,
        string path)
    {
        switch (resource.Kind)
        {
            case OwnershipResourceKindV2.Key when resource.Input is not null:
                backend.KeyUp(resource.Input, path, isCleanup: true);
                break;
            case OwnershipResourceKindV2.MouseButton when resource.Input is not null:
                backend.MouseUp(resource.Input, path, isCleanup: true);
                break;
            case OwnershipResourceKindV2.CoordinateContact when resource.Coordinate is not null:
                backend.CoordinateUnsupported(resource.Coordinate, path, isCleanup: true);
                break;
        }
    }
}
