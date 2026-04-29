using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

internal sealed class LiveHarnessWindow : IDisposable
{
    private const int CwUseDefault = unchecked((int)0x80000000);
    private const int SwShow = 5;
    private const int SwRestore = 9;
    private const int WmClose = 0x0010;
    private const int WmDestroy = 0x0002;
    private const int WmKeydown = 0x0100;
    private const int WmKeyup = 0x0101;
    private const int WmLbuttondown = 0x0201;
    private const int WmLbuttonup = 0x0202;
    private const int WmRbuttondown = 0x0204;
    private const int WmRbuttonup = 0x0205;
    private const int WmMbuttondown = 0x0207;
    private const int WmMbuttonup = 0x0208;
    private const int WmMousewheel = 0x020A;
    private const int WmXbuttondown = 0x020B;
    private const int WmXbuttonup = 0x020C;
    private const uint WsOverlappedWindow = 0x00CF0000;
    private const int Xbutton1 = 0x0001;
    private const int Xbutton2 = 0x0002;

    private readonly ManualResetEventSlim _ready = new();
    private readonly ManualResetEventSlim _closed = new();
    private readonly WndProc _wndProc;
    private readonly Thread _thread;
    private readonly string _className;
    private Exception? _error;
    private IntPtr _hwnd;
    private long _keyDownCount;
    private long _keyUpCount;
    private long _mouseDownCount;
    private long _mouseUpCount;
    private long _wheelCount;
    private long _wheelUpCount;
    private long _wheelDownCount;
    private long _xButton1DownCount;
    private long _xButton1UpCount;
    private long _xButton2DownCount;
    private long _xButton2UpCount;
    private readonly long[] _keyDownByVirtualKey = new long[256];
    private readonly long[] _keyUpByVirtualKey = new long[256];

    public LiveHarnessWindow(string title)
    {
        Title = title;
        _className = $"BAKeySmithLiveHarness{Guid.NewGuid():N}";
        _wndProc = WindowProc;
        _thread = new Thread(WindowThreadMain)
        {
            IsBackground = true,
            Name = "BAKeySmith.LiveHarnessWindow"
        };
    }

    public string Title { get; }
    public IntPtr Handle => _hwnd;
    public long KeyDownCount => Interlocked.Read(ref _keyDownCount);
    public long KeyUpCount => Interlocked.Read(ref _keyUpCount);
    public long MouseDownCount => Interlocked.Read(ref _mouseDownCount);
    public long MouseUpCount => Interlocked.Read(ref _mouseUpCount);
    public long WheelCount => Interlocked.Read(ref _wheelCount);
    public long WheelUpCount => Interlocked.Read(ref _wheelUpCount);
    public long WheelDownCount => Interlocked.Read(ref _wheelDownCount);
    public long XButton1DownCount => Interlocked.Read(ref _xButton1DownCount);
    public long XButton1UpCount => Interlocked.Read(ref _xButton1UpCount);
    public long XButton2DownCount => Interlocked.Read(ref _xButton2DownCount);
    public long XButton2UpCount => Interlocked.Read(ref _xButton2UpCount);

    public void Reset()
    {
        Interlocked.Exchange(ref _keyDownCount, 0);
        Interlocked.Exchange(ref _keyUpCount, 0);
        Interlocked.Exchange(ref _mouseDownCount, 0);
        Interlocked.Exchange(ref _mouseUpCount, 0);
        Interlocked.Exchange(ref _wheelCount, 0);
        Interlocked.Exchange(ref _wheelUpCount, 0);
        Interlocked.Exchange(ref _wheelDownCount, 0);
        Interlocked.Exchange(ref _xButton1DownCount, 0);
        Interlocked.Exchange(ref _xButton1UpCount, 0);
        Interlocked.Exchange(ref _xButton2DownCount, 0);
        Interlocked.Exchange(ref _xButton2UpCount, 0);

        for (var index = 0; index < _keyDownByVirtualKey.Length; index++)
        {
            Interlocked.Exchange(ref _keyDownByVirtualKey[index], 0);
            Interlocked.Exchange(ref _keyUpByVirtualKey[index], 0);
        }
    }

    public long KeyDownCountFor(int virtualKey)
    {
        return virtualKey is >= 0 and < 256
            ? Interlocked.Read(ref _keyDownByVirtualKey[virtualKey])
            : 0;
    }

    public long KeyUpCountFor(int virtualKey)
    {
        return virtualKey is >= 0 and < 256
            ? Interlocked.Read(ref _keyUpByVirtualKey[virtualKey])
            : 0;
    }

    public void Start(TimeSpan timeout)
    {
        _thread.Start();
        if (!_ready.Wait(timeout))
        {
            throw new TimeoutException("Timed out while creating live harness window.");
        }

        if (_error is not null)
        {
            throw new InvalidOperationException("Failed to create live harness window.", _error);
        }
    }

    public async Task<bool> FocusAsync(TimeSpan timeout)
    {
        if (_hwnd == IntPtr.Zero)
        {
            return false;
        }

        return await LiveWindowTools.FocusAsync(_hwnd, timeout);
    }

    public void Dispose()
    {
        if (_hwnd != IntPtr.Zero)
        {
            _ = PostMessageW(_hwnd, WmClose, IntPtr.Zero, IntPtr.Zero);
        }

        _ = _closed.Wait(TimeSpan.FromSeconds(2));
        _ready.Dispose();
        _closed.Dispose();
    }

    private void WindowThreadMain()
    {
        try
        {
            var instance = GetModuleHandleW(null);
            var windowClass = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
                lpfnWndProc = _wndProc,
                hInstance = instance,
                lpszClassName = _className
            };

            if (RegisterClassExW(ref windowClass) == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            _hwnd = CreateWindowExW(
                0,
                _className,
                Title,
                WsOverlappedWindow,
                CwUseDefault,
                CwUseDefault,
                420,
                180,
                IntPtr.Zero,
                IntPtr.Zero,
                instance,
                IntPtr.Zero);

            if (_hwnd == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            _ = ShowWindow(_hwnd, SwShow);
            _ = UpdateWindow(_hwnd);
            _ready.Set();

            while (GetMessageW(out var message, IntPtr.Zero, 0, 0) > 0)
            {
                _ = TranslateMessage(ref message);
                _ = DispatchMessageW(ref message);
            }
        }
        catch (Exception ex)
        {
            _error = ex;
            _ready.Set();
        }
        finally
        {
            _hwnd = IntPtr.Zero;
            _closed.Set();
        }
    }

    private IntPtr WindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == WmDestroy)
        {
            PostQuitMessage(0);
            return IntPtr.Zero;
        }

        switch ((int)message)
        {
            case WmKeydown:
                Interlocked.Increment(ref _keyDownCount);
                IncrementVirtualKey(_keyDownByVirtualKey, wParam);
                break;
            case WmKeyup:
                Interlocked.Increment(ref _keyUpCount);
                IncrementVirtualKey(_keyUpByVirtualKey, wParam);
                break;
            case WmLbuttondown:
            case WmRbuttondown:
            case WmMbuttondown:
                Interlocked.Increment(ref _mouseDownCount);
                break;
            case WmLbuttonup:
            case WmRbuttonup:
            case WmMbuttonup:
                Interlocked.Increment(ref _mouseUpCount);
                break;
            case WmXbuttondown:
                Interlocked.Increment(ref _mouseDownCount);
                IncrementXButtonCount(wParam, down: true);
                break;
            case WmXbuttonup:
                Interlocked.Increment(ref _mouseUpCount);
                IncrementXButtonCount(wParam, down: false);
                break;
            case WmMousewheel:
                Interlocked.Increment(ref _wheelCount);
                if (ExtractHighWordSigned(wParam) >= 0)
                {
                    Interlocked.Increment(ref _wheelUpCount);
                }
                else
                {
                    Interlocked.Increment(ref _wheelDownCount);
                }
                break;
        }

        return DefWindowProcW(hwnd, message, wParam, lParam);
    }

    private static void IncrementVirtualKey(long[] counters, IntPtr wParam)
    {
        var virtualKey = wParam.ToInt32();
        if (virtualKey is >= 0 and < 256)
        {
            Interlocked.Increment(ref counters[virtualKey]);
        }
    }

    private void IncrementXButtonCount(IntPtr wParam, bool down)
    {
        var button = ExtractHighWordUnsigned(wParam);
        if (button == Xbutton2)
        {
            if (down)
            {
                Interlocked.Increment(ref _xButton2DownCount);
            }
            else
            {
                Interlocked.Increment(ref _xButton2UpCount);
            }

            return;
        }

        if (down)
        {
            Interlocked.Increment(ref _xButton1DownCount);
        }
        else
        {
            Interlocked.Increment(ref _xButton1UpCount);
        }
    }

    private static int ExtractHighWordUnsigned(IntPtr value)
    {
        return (int)((value.ToInt64() >> 16) & 0xffff);
    }

    private static short ExtractHighWordSigned(IntPtr value)
    {
        return unchecked((short)((value.ToInt64() >> 16) & 0xffff));
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassExW(ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowExW(
        uint dwExStyle,
        string lpClassName,
        string lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UpdateWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessageW(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetMessageW(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessageW(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int nExitCode);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? lpModuleName);

    private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public WndProc lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

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
    private struct POINT
    {
        public int x;
        public int y;
    }
}

internal static class LiveWindowTools
{
    private const int SwRestore = 9;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpShowWindow = 0x0040;
    private static readonly IntPtr HwndTopMost = new(-1);
    private static readonly IntPtr HwndNoTopMost = new(-2);

    public static async Task<bool> FocusAsync(IntPtr hwnd, TimeSpan timeout)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < timeout)
        {
            ForceForeground(hwnd);
            if (await WaitForForegroundAsync(hwnd, TimeSpan.FromMilliseconds(100)))
            {
                return true;
            }

            await Task.Delay(50);
        }

        return IsForeground(hwnd);
    }

    public static async Task<bool> WaitForForegroundAsync(IntPtr hwnd, TimeSpan timeout)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < timeout)
        {
            if (GetForegroundWindow() == hwnd)
            {
                return true;
            }

            await Task.Delay(25);
        }

        return false;
    }

    public static bool IsForeground(IntPtr hwnd)
    {
        return hwnd != IntPtr.Zero && GetForegroundWindow() == hwnd;
    }

    public static bool MoveCursorToCenter(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var rect))
        {
            return false;
        }

        return SetCursorPos((rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2);
    }

    private static void ForceForeground(IntPtr hwnd)
    {
        var currentThread = GetCurrentThreadId();
        var targetThread = GetWindowThreadProcessId(hwnd, out _);
        var foreground = GetForegroundWindow();
        var foregroundThread = foreground == IntPtr.Zero
            ? 0
            : GetWindowThreadProcessId(foreground, out _);

        var attachedTarget = false;
        var attachedForeground = false;
        try
        {
            if (targetThread != 0 && targetThread != currentThread)
            {
                attachedTarget = AttachThreadInput(currentThread, targetThread, true);
            }

            if (foregroundThread != 0 &&
                foregroundThread != currentThread &&
                foregroundThread != targetThread)
            {
                attachedForeground = AttachThreadInput(currentThread, foregroundThread, true);
            }

            _ = ShowWindow(hwnd, SwRestore);
            _ = SetWindowPos(hwnd, HwndTopMost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpShowWindow);
            _ = SetWindowPos(hwnd, HwndNoTopMost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpShowWindow);
            _ = BringWindowToTop(hwnd);
            _ = SetActiveWindow(hwnd);
            _ = SetFocus(hwnd);
            _ = SetForegroundWindow(hwnd);
        }
        finally
        {
            if (attachedForeground)
            {
                _ = AttachThreadInput(currentThread, foregroundThread, false);
            }

            if (attachedTarget)
            {
                _ = AttachThreadInput(currentThread, targetThread, false);
            }
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SetActiveWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
