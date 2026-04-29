using System.Diagnostics;
using System.IO;
using BAKeySmith.Core.Contracts;
using BAKeySmith.Core.Foreground;

namespace BAKeySmith.App.Services;

public sealed class GuiSelfForegroundGate : IForegroundGate
{
    private const string BlockedTitleSuffix = "gui_self_foreground_blocked";
    private readonly IForegroundGate _inner;
    private readonly HashSet<string> _selfProcessNames;

    public GuiSelfForegroundGate(
        IForegroundGate inner,
        IEnumerable<string>? selfProcessNames = null)
    {
        _inner = inner;
        _selfProcessNames = new HashSet<string>(
            (selfProcessNames ?? DefaultSelfProcessNames()).Select(NormalizeProcessName),
            StringComparer.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<string> DefaultSelfProcessNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "BAKeySmith",
            "BAKeySmith.App"
        };

        AddProcessName(names, Process.GetCurrentProcess().ProcessName);
        AddProcessName(names, Path.GetFileNameWithoutExtension(Environment.ProcessPath));
        return names.ToArray();
    }

    public async ValueTask<ForegroundGateResult> CheckAsync(
        RuntimeConfig config,
        CancellationToken cancellationToken)
    {
        var result = await _inner.CheckAsync(config, cancellationToken);
        return IsSelfProcess(result.ForegroundProcess)
            ? result with
            {
                IsAllowed = false,
                ForegroundWindowTitle = AppendBlockedReason(result.ForegroundWindowTitle)
            }
            : result;
    }

    public bool IsSelfProcess(string? processName)
    {
        var normalized = NormalizeProcessName(processName);
        return normalized.Length > 0 && _selfProcessNames.Contains(normalized);
    }

    private static void AddProcessName(HashSet<string> names, string? processName)
    {
        var normalized = NormalizeProcessName(processName);
        if (normalized.Length > 0)
        {
            names.Add(normalized);
        }
    }

    private static string NormalizeProcessName(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return string.Empty;
        }

        var normalized = processName.Trim();
        return normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? normalized[..^4]
            : normalized;
    }

    private static string AppendBlockedReason(string? title)
    {
        return string.IsNullOrWhiteSpace(title)
            ? BlockedTitleSuffix
            : $"{title} | {BlockedTitleSuffix}";
    }
}
