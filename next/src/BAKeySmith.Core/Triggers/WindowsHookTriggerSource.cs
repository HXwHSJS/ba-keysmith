using System.Diagnostics;
using System.Runtime.InteropServices;
using BAKeySmith.Core.Contracts;
using BAKeySmith.Core.Diagnostics;
using BAKeySmith.Core.Input;

namespace BAKeySmith.Core.Triggers;

public sealed class WindowsHookTriggerSource : ITriggerSource, ITriggerCapturePolicySink
{
    private const int WhKeyboardLl = 13;
    private const int WhMouseLl = 14;
    private const int WmKeydown = 0x0100;
    private const int WmKeyup = 0x0101;
    private const int WmSyskeydown = 0x0104;
    private const int WmSyskeyup = 0x0105;
    private const int WmLbuttondown = 0x0201;
    private const int WmLbuttonup = 0x0202;
    private const int WmRbuttondown = 0x0204;
    private const int WmRbuttonup = 0x0205;
    private const int WmMbuttondown = 0x0207;
    private const int WmMbuttonup = 0x0208;
    private const int WmXbuttondown = 0x020B;
    private const int WmXbuttonup = 0x020C;
    private const int WmMousewheel = 0x020A;
    private const int WmQuit = 0x0012;
    private const int Xbutton1 = 0x0001;
    private const int Xbutton2 = 0x0002;

    private readonly object _gate = new();
    private readonly HashSet<string> _capturedTriggers = new(StringComparer.OrdinalIgnoreCase);
    private readonly IDiagnosticsSink _diagnostics;
    private readonly IReadOnlySet<string> _blockedForegroundProcessNames;
    private readonly LowLevelProc _keyboardProc;
    private readonly LowLevelProc _mouseProc;
    private TriggerCapturePolicySnapshot _capturePolicy = TriggerCapturePolicySnapshot.Disabled;
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;
    private Thread? _hookThread;
    private uint _hookThreadId;
    private Exception? _startError;

    public WindowsHookTriggerSource(
        IDiagnosticsSink? diagnostics = null,
        IEnumerable<string>? blockedForegroundProcessNames = null)
    {
        _diagnostics = diagnostics ?? NoOpDiagnosticsSink.Instance;
        _blockedForegroundProcessNames = NormalizeProcessNames(blockedForegroundProcessNames);
        _keyboardProc = KeyboardHookCallback;
        _mouseProc = MouseHookCallback;
    }

    public event EventHandler<TriggerEvent>? Triggered;
    public bool IsRunning { get; private set; }

    public void UpdateCapturePolicy(TriggerCapturePolicySnapshot policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        Volatile.Write(ref _capturePolicy, policy);
        if (!policy.IsEnabled)
        {
            ClearCapturedTriggers();
        }
    }

    public ValueTask StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (IsRunning || _hookThread is not null)
            {
                return ValueTask.CompletedTask;
            }

            _startError = null;
        }

        using var started = new ManualResetEventSlim(false);
        var thread = new Thread(() => HookThreadMain(started))
        {
            IsBackground = true,
            Name = "BAKeySmith.Win32Hook"
        };

        lock (_gate)
        {
            _hookThread = thread;
        }

        thread.Start();
        if (!started.Wait(TimeSpan.FromSeconds(3), cancellationToken))
        {
            StopAsync(CancellationToken.None).GetAwaiter().GetResult();
            throw new TimeoutException("Timed out while starting global input hook thread.");
        }

        if (_startError is not null)
        {
            StopAsync(CancellationToken.None).GetAwaiter().GetResult();
            throw new InvalidOperationException("Failed to install global input hooks.", _startError);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Thread? thread;
        uint threadId;
        lock (_gate)
        {
            thread = _hookThread;
            threadId = _hookThreadId;
        }

        if (thread is null)
        {
            return ValueTask.CompletedTask;
        }

        if (threadId != 0)
        {
            _ = PostThreadMessageW(threadId, WmQuit, IntPtr.Zero, IntPtr.Zero);
        }

        if (!thread.Join(TimeSpan.FromSeconds(2)))
        {
            throw new TimeoutException("Timed out while stopping global input hook thread.");
        }

        lock (_gate)
        {
            _hookThread = null;
            _hookThreadId = 0;
            IsRunning = false;
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var message = wParam.ToInt32();
            var keyboard = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            if (InjectedInputMarker.IsMarked(keyboard.dwExtraInfo))
            {
                return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
            }

            if (KeyNameResolver.TryGetNameFromVirtualKey(keyboard.vkCode, out var key))
            {
                TriggerEvent? triggerEvent = null;
                if (message is WmKeydown or WmSyskeydown)
                {
                    triggerEvent = TriggerEvent.Down(TriggerSpec.Keyboard(key));
                }
                else if (message is WmKeyup or WmSyskeyup)
                {
                    triggerEvent = TriggerEvent.Up(TriggerSpec.Keyboard(key));
                }

                if (triggerEvent is not null)
                {
                    var decision = Decide(triggerEvent, createsCapturedSession: true);
                    EmitTriggerReceived(triggerEvent, decision);
                    if (decision.Dispatch)
                    {
                        Triggered?.Invoke(this, triggerEvent);
                    }

                    if (decision.Suppress)
                    {
                        return (IntPtr)1;
                    }
                }
            }
        }

        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private void HookThreadMain(ManualResetEventSlim started)
    {
        try
        {
            _hookThreadId = GetCurrentThreadId();
            _keyboardHook = SetWindowsHookExW(WhKeyboardLl, _keyboardProc, IntPtr.Zero, 0);
            _mouseHook = SetWindowsHookExW(WhMouseLl, _mouseProc, IntPtr.Zero, 0);
            if (_keyboardHook == IntPtr.Zero || _mouseHook == IntPtr.Zero)
            {
                _startError = new InvalidOperationException("SetWindowsHookEx returned zero.");
                started.Set();
                return;
            }

            IsRunning = true;
            started.Set();

            while (GetMessageW(out var message, IntPtr.Zero, 0, 0) > 0)
            {
                _ = TranslateMessage(ref message);
                _ = DispatchMessageW(ref message);
            }
        }
        catch (Exception ex)
        {
            _startError = ex;
            started.Set();
        }
        finally
        {
            if (_keyboardHook != IntPtr.Zero)
            {
                _ = UnhookWindowsHookEx(_keyboardHook);
                _keyboardHook = IntPtr.Zero;
            }

            if (_mouseHook != IntPtr.Zero)
            {
                _ = UnhookWindowsHookEx(_mouseHook);
                _mouseHook = IntPtr.Zero;
            }

            IsRunning = false;
        }
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var message = wParam.ToInt32();
            var mouse = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            if (InjectedInputMarker.IsMarked(mouse.dwExtraInfo))
            {
                return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
            }

            var mouseTrigger = MouseTriggerFromMessage(
                message,
                mouse.mouseData,
                out var isDown,
                out var createsCapturedSession);
            if (mouseTrigger is not null)
            {
                var triggerEvent = isDown
                    ? TriggerEvent.Down(TriggerSpec.Mouse(mouseTrigger))
                    : TriggerEvent.Up(TriggerSpec.Mouse(mouseTrigger));
                var decision = Decide(triggerEvent, createsCapturedSession);
                EmitTriggerReceived(triggerEvent, decision);
                if (decision.Dispatch)
                {
                    Triggered?.Invoke(this, triggerEvent);
                }

                if (decision.Suppress)
                {
                    return (IntPtr)1;
                }
            }
        }

        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private TriggerHookDecision Decide(TriggerEvent triggerEvent, bool createsCapturedSession)
    {
        var policy = Volatile.Read(ref _capturePolicy);
        var key = triggerEvent.Trigger.Key;
        bool alreadyCaptured;
        lock (_capturedTriggers)
        {
            alreadyCaptured = _capturedTriggers.Contains(key);
        }

        var requiresForegroundCheck =
            triggerEvent.IsDown &&
            !alreadyCaptured &&
            policy.IsEnabled &&
            policy.Hits(key);
        var foregroundAllowed = requiresForegroundCheck && IsForegroundAllowed(policy.TargetProcess);
        var policyHit = policy.IsEnabled && policy.Hits(key);
        TriggerHookDecision decision;
        lock (_capturedTriggers)
        {
            decision = EvaluateCaptureDecision(
                triggerEvent,
                policy,
                createsCapturedSession,
                foregroundAllowed,
                _capturedTriggers);
        }

        EmitCaptureLifecycle(
            triggerEvent,
            decision,
            policyHit,
            alreadyCaptured,
            foregroundAllowed,
            createsCapturedSession);
        return decision;
    }

    private void EmitTriggerReceived(TriggerEvent triggerEvent, TriggerHookDecision decision)
    {
        _diagnostics.Emit(DiagnosticEvent.Create(
            "windows_hook",
            "trigger_received",
            fields: new Dictionary<string, string>
            {
                ["trigger"] = triggerEvent.Trigger.Key,
                ["phase"] = triggerEvent.NormalizedPhase,
                ["suppress"] = decision.Suppress.ToString(),
                ["dispatch"] = decision.Dispatch.ToString()
            }));
    }

    private void EmitCaptureLifecycle(
        TriggerEvent triggerEvent,
        TriggerHookDecision decision,
        bool policyHit,
        bool alreadyCaptured,
        bool foregroundAllowed,
        bool createsCapturedSession)
    {
        if (triggerEvent.IsDown &&
            policyHit &&
            !alreadyCaptured &&
            foregroundAllowed &&
            createsCapturedSession &&
            decision == TriggerHookDecision.SuppressAndDispatch)
        {
            EmitCaptureEvent("captured_session_entered", triggerEvent);
            return;
        }

        if (triggerEvent.IsUp &&
            alreadyCaptured &&
            decision == TriggerHookDecision.SuppressAndDispatch)
        {
            EmitCaptureEvent("release_matched_captured_session", triggerEvent);
            return;
        }

        if (triggerEvent.IsDown &&
            policyHit &&
            !alreadyCaptured &&
            !foregroundAllowed &&
            decision == TriggerHookDecision.PassThrough)
        {
            EmitCaptureEvent("capture_blocked_by_foreground", triggerEvent);
            return;
        }

        if (triggerEvent.IsUp &&
            !alreadyCaptured &&
            policyHit &&
            decision == TriggerHookDecision.PassThrough)
        {
            EmitCaptureEvent("uncaptured_release_pass_through", triggerEvent);
        }
    }

    private void EmitCaptureEvent(string name, TriggerEvent triggerEvent)
    {
        _diagnostics.Emit(DiagnosticEvent.Create(
            "windows_hook",
            name,
            fields: new Dictionary<string, string>
            {
                ["trigger"] = triggerEvent.Trigger.Key,
                ["phase"] = triggerEvent.NormalizedPhase
            }));
    }

    internal static TriggerHookDecision EvaluateCaptureDecision(
        TriggerEvent triggerEvent,
        TriggerCapturePolicySnapshot policy,
        bool createsCapturedSession,
        bool foregroundAllowed,
        ISet<string> capturedTriggers)
    {
        var key = triggerEvent.Trigger.Key;
        if (triggerEvent.IsUp && capturedTriggers.Remove(key))
        {
            return TriggerHookDecision.SuppressAndDispatch;
        }

        if (triggerEvent.IsDown && capturedTriggers.Contains(key))
        {
            return TriggerHookDecision.SuppressOnly;
        }

        if (!policy.IsEnabled || !policy.Hits(key) || !triggerEvent.IsDown)
        {
            return TriggerHookDecision.PassThrough;
        }

        if (!foregroundAllowed)
        {
            return TriggerHookDecision.PassThrough;
        }

        if (createsCapturedSession)
        {
            capturedTriggers.Add(key);
        }

        return TriggerHookDecision.SuppressAndDispatch;
    }

    private void ClearCapturedTriggers()
    {
        lock (_capturedTriggers)
        {
            _capturedTriggers.Clear();
        }
    }

    private bool IsForegroundAllowed(string targetProcess)
    {
        if (string.IsNullOrWhiteSpace(targetProcess))
        {
            return false;
        }

        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        _ = GetWindowThreadProcessId(hwnd, out var processId);
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return IsForegroundAllowedForCapture(
                targetProcess,
                process.ProcessName,
                _blockedForegroundProcessNames);
        }
        catch
        {
            return false;
        }
    }

    internal static bool IsForegroundAllowedForCapture(
        string targetProcess,
        string? foregroundProcess,
        IEnumerable<string>? blockedForegroundProcessNames = null)
    {
        var normalizedTarget = NormalizeProcessName(targetProcess);
        if (normalizedTarget.Length == 0)
        {
            return false;
        }

        var normalizedForeground = NormalizeProcessName(foregroundProcess);
        if (normalizedForeground.Length == 0)
        {
            return false;
        }

        if (IsBlockedForegroundProcess(normalizedForeground, blockedForegroundProcessNames))
        {
            return false;
        }

        return string.Equals(
            normalizedForeground,
            normalizedTarget,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBlockedForegroundProcess(
        string normalizedForegroundProcess,
        IEnumerable<string>? blockedForegroundProcessNames)
    {
        if (blockedForegroundProcessNames is null)
        {
            return false;
        }

        foreach (var blockedProcessName in blockedForegroundProcessNames)
        {
            if (string.Equals(
                normalizedForegroundProcess,
                NormalizeProcessName(blockedProcessName),
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlySet<string> NormalizeProcessNames(IEnumerable<string>? processNames)
    {
        var normalizedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (processNames is null)
        {
            return normalizedNames;
        }

        foreach (var processName in processNames)
        {
            var normalized = NormalizeProcessName(processName);
            if (normalized.Length > 0)
            {
                normalizedNames.Add(normalized);
            }
        }

        return normalizedNames;
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

    private static string? MouseTriggerFromMessage(
        int message,
        uint mouseData,
        out bool isDown,
        out bool createsCapturedSession)
    {
        isDown = message is not (WmLbuttonup or WmRbuttonup or WmMbuttonup or WmXbuttonup);
        createsCapturedSession = message is not WmMousewheel;
        if (message == WmMousewheel)
        {
            var delta = unchecked((short)((mouseData >> 16) & 0xffff));
            return delta >= 0 ? "mouse_wheel_up" : "mouse_wheel_down";
        }

        return message switch
        {
            WmLbuttondown or WmLbuttonup => "mouse_left",
            WmRbuttondown or WmRbuttonup => "mouse_right",
            WmMbuttondown or WmMbuttonup => "mouse_middle",
            WmXbuttondown or WmXbuttonup => (((mouseData >> 16) & 0xffff) == Xbutton2 ? "mouse_x2" : "mouse_x1"),
            _ => null
        };
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookExW(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostThreadMessageW(uint idThread, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetMessageW(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessageW(ref MSG lpMsg);

    private delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public UIntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    internal readonly record struct TriggerHookDecision(bool Suppress, bool Dispatch)
    {
        public static TriggerHookDecision PassThrough { get; } = new(Suppress: false, Dispatch: false);
        public static TriggerHookDecision SuppressOnly { get; } = new(Suppress: true, Dispatch: false);
        public static TriggerHookDecision SuppressAndDispatch { get; } = new(Suppress: true, Dispatch: true);
    }
}
