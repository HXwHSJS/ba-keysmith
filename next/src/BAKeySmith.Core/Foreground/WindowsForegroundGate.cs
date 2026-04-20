using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using BAKeySmith.Core.Contracts;

namespace BAKeySmith.Core.Foreground;

public sealed class WindowsForegroundGate : IForegroundGate
{
    public ValueTask<ForegroundGateResult> CheckAsync(
        RuntimeConfig config,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ValueTask.FromResult(new ForegroundGateResult(
                false,
                TargetProcess: config.TargetProcess,
                ForegroundProcess: null,
                ForegroundWindowTitle: "non-windows"));
        }

        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return ValueTask.FromResult(new ForegroundGateResult(
                false,
                TargetProcess: config.TargetProcess));
        }

        _ = GetWindowThreadProcessId(hwnd, out var processId);
        var processName = GetProcessName(processId);
        var normalizedTarget = NormalizeProcessName(config.TargetProcess);
        var normalizedForeground = NormalizeProcessName(processName);
        var allowed = string.Equals(
            normalizedTarget,
            normalizedForeground,
            StringComparison.OrdinalIgnoreCase);

        return ValueTask.FromResult(new ForegroundGateResult(
            allowed,
            TargetProcess: config.TargetProcess,
            ForegroundProcess: processName,
            ForegroundWindowTitle: GetWindowTitle(hwnd)));
    }

    private static string? GetProcessName(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return null;
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

    private static string GetWindowTitle(IntPtr hwnd)
    {
        var length = GetWindowTextLengthW(hwnd);
        if (length <= 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(length + 1);
        _ = GetWindowTextW(hwnd, builder, builder.Capacity);
        return builder.ToString();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLengthW(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextW(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
}
