using BAKeySmith.Core.Diagnostics;
using BAKeySmith.Core.Input;

namespace BAKeySmith.Core.Runtime;

public sealed class PressOwnershipTracker
{
    private readonly object _gate = new();
    private readonly IInputBackend _inputBackend;
    private readonly IDiagnosticsSink _diagnostics;
    private readonly Dictionary<string, HashSet<string>> _keyOwners = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _ownerKeys = new(StringComparer.OrdinalIgnoreCase);

    public PressOwnershipTracker(IInputBackend inputBackend, IDiagnosticsSink? diagnostics = null)
    {
        _inputBackend = inputBackend;
        _diagnostics = diagnostics ?? NoOpDiagnosticsSink.Instance;
    }

    public async ValueTask HoldKeyAsync(string owner, string key, CancellationToken cancellationToken)
    {
        owner = Normalize(owner);
        key = Normalize(key);
        var shouldSendDown = false;

        lock (_gate)
        {
            if (!_keyOwners.TryGetValue(key, out var owners))
            {
                owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _keyOwners[key] = owners;
            }

            if (!owners.Add(owner))
            {
                return;
            }

            shouldSendDown = owners.Count == 1;

            if (!_ownerKeys.TryGetValue(owner, out var keys))
            {
                keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _ownerKeys[owner] = keys;
            }
            keys.Add(key);
        }

        if (shouldSendDown)
        {
            await SendDownAsync(key, cancellationToken);
            _diagnostics.Emit(DiagnosticEvent.Create(
                "presses",
                "key_down",
                fields: new Dictionary<string, string>
                {
                    ["key"] = key,
                    ["owner"] = owner
                }));
        }
    }

    public async ValueTask ReleaseKeyAsync(string owner, string key, CancellationToken cancellationToken)
    {
        owner = Normalize(owner);
        key = Normalize(key);
        var shouldSendUp = false;

        lock (_gate)
        {
            if (!_keyOwners.TryGetValue(key, out var owners) || !owners.Remove(owner))
            {
                return;
            }

            if (owners.Count == 0)
            {
                _keyOwners.Remove(key);
                shouldSendUp = true;
            }

            if (_ownerKeys.TryGetValue(owner, out var keys))
            {
                keys.Remove(key);
                if (keys.Count == 0)
                {
                    _ownerKeys.Remove(owner);
                }
            }
        }

        if (shouldSendUp)
        {
            await SendUpAsync(key, cancellationToken);
            _diagnostics.Emit(DiagnosticEvent.Create(
                "presses",
                "key_up",
                fields: new Dictionary<string, string>
                {
                    ["key"] = key,
                    ["owner"] = owner
                }));
        }
    }

    public async ValueTask ReleaseOwnerAsync(string owner, CancellationToken cancellationToken)
    {
        owner = Normalize(owner);
        List<string> keys;

        lock (_gate)
        {
            if (!_ownerKeys.TryGetValue(owner, out var ownedKeys))
            {
                return;
            }

            keys = ownedKeys.ToList();
        }

        foreach (var key in keys)
        {
            await ReleaseKeyAsync(owner, key, cancellationToken);
        }
    }

    public async ValueTask ReleaseAllAsync(CancellationToken cancellationToken)
    {
        List<string> keys;

        lock (_gate)
        {
            keys = _keyOwners.Keys.ToList();
            _keyOwners.Clear();
            _ownerKeys.Clear();
        }

        foreach (var key in keys)
        {
            await SendUpAsync(key, cancellationToken);
            _diagnostics.Emit(DiagnosticEvent.Create(
                "presses",
                "force_key_up",
                fields: new Dictionary<string, string>
                {
                    ["key"] = key
                }));
        }
    }

    private ValueTask SendDownAsync(string code, CancellationToken cancellationToken)
    {
        return KeyNameResolver.IsMouseButton(code)
            ? _inputBackend.MouseDownAsync(code, cancellationToken)
            : _inputBackend.KeyDownAsync(code, cancellationToken);
    }

    private ValueTask SendUpAsync(string code, CancellationToken cancellationToken)
    {
        return KeyNameResolver.IsMouseButton(code)
            ? _inputBackend.MouseUpAsync(code, cancellationToken)
            : _inputBackend.KeyUpAsync(code, cancellationToken);
    }

    public PressOwnershipSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new PressOwnershipSnapshot(
                _keyOwners.ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlyCollection<string>)pair.Value.ToArray(),
                    StringComparer.OrdinalIgnoreCase),
                _ownerKeys.ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlyCollection<string>)pair.Value.ToArray(),
                    StringComparer.OrdinalIgnoreCase));
        }
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Owner and key names must not be empty.", nameof(value));
        }
        return value.Trim().ToLowerInvariant();
    }
}
