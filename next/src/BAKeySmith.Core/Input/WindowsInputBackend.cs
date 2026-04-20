using System.ComponentModel;
using System.Runtime.InteropServices;

namespace BAKeySmith.Core.Input;

public sealed class WindowsInputBackend : IInputBackend
{
    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;
    private const uint KeyeventfExtendedkey = 0x0001;
    private const uint KeyeventfKeyup = 0x0002;
    private const uint KeyeventfScancode = 0x0008;
    private const uint MouseeventfLeftdown = 0x0002;
    private const uint MouseeventfLeftup = 0x0004;
    private const uint MouseeventfRightdown = 0x0008;
    private const uint MouseeventfRightup = 0x0010;
    private const uint MouseeventfMiddledown = 0x0020;
    private const uint MouseeventfMiddleup = 0x0040;
    private const uint MouseeventfXdown = 0x0080;
    private const uint MouseeventfXup = 0x0100;
    private const uint MouseeventfWheel = 0x0800;
    private const uint Xbutton1 = 0x0001;
    private const uint Xbutton2 = 0x0002;
    private readonly bool _markInjected;

    public WindowsInputBackend(bool markInjected = true)
    {
        _markInjected = markInjected;
    }

    public ValueTask SendAsync(IReadOnlyList<InputCommand> commands, CancellationToken cancellationToken)
    {
        var pending = new List<INPUT>();
        foreach (var command in commands)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.Equals(command.Kind, "move", StringComparison.OrdinalIgnoreCase))
            {
                Flush(pending);
                if (command.X is null || command.Y is null)
                {
                    throw new ArgumentException("Move command requires X and Y.");
                }

                if (!SetCursorPos(command.X.Value, command.Y.Value))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                continue;
            }

            pending.Add(ToInput(command));
        }

        Flush(pending);
        return ValueTask.CompletedTask;
    }

    public ValueTask KeyDownAsync(string key, CancellationToken cancellationToken)
    {
        return SendAsync([InputCommand.KeyDown(key)], cancellationToken);
    }

    public ValueTask KeyUpAsync(string key, CancellationToken cancellationToken)
    {
        return SendAsync([InputCommand.KeyUp(key)], cancellationToken);
    }

    public ValueTask MouseDownAsync(string button, CancellationToken cancellationToken)
    {
        return SendAsync([InputCommand.MouseDown(KeyNameResolver.NormalizeMouseButton(button))], cancellationToken);
    }

    public ValueTask MouseUpAsync(string button, CancellationToken cancellationToken)
    {
        return SendAsync([InputCommand.MouseUp(KeyNameResolver.NormalizeMouseButton(button))], cancellationToken);
    }

    public ValueTask MoveMouseToAsync(int x, int y, CancellationToken cancellationToken)
    {
        return SendAsync([InputCommand.MoveMouseTo(x, y)], cancellationToken);
    }

    public ValueTask<(int X, int Y)> GetMousePositionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!GetCursorPos(out var point))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return ValueTask.FromResult((point.X, point.Y));
    }

    public ValueTask<(int Width, int Height)> GetScreenSizeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult((GetSystemMetrics(0), GetSystemMetrics(1)));
    }

    private INPUT ToInput(InputCommand command)
    {
        var kind = command.Kind.Trim().ToLowerInvariant();
        if (kind != "wheel" && command.IsDown is null)
        {
            throw new ArgumentException("Key and mouse commands require IsDown.");
        }

        return kind switch
        {
            "key" => KeyboardInput(command.Code, command.IsDown!.Value, ExtraInfo),
            "mouse" => MouseInput(command.Code, command.IsDown!.Value, ExtraInfo),
            "wheel" => WheelInput(command, ExtraInfo),
            _ => throw new ArgumentException($"Unsupported input command kind: {command.Kind}")
        };
    }

    private UIntPtr ExtraInfo => _markInjected ? InjectedInputMarker.ExtraInfo : UIntPtr.Zero;

    private static INPUT KeyboardInput(string key, bool isDown, UIntPtr extraInfo)
    {
        var info = KeyNameResolver.ResolveKeyboardKey(key);
        var scanCode = (ushort)MapVirtualKeyW(info.VirtualKey, 0);
        var flags = KeyeventfScancode;
        if (info.IsExtended)
        {
            flags |= KeyeventfExtendedkey;
        }

        if (!isDown)
        {
            flags |= KeyeventfKeyup;
        }

        return new INPUT
        {
            type = InputKeyboard,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = scanCode,
                    dwFlags = flags,
                    dwExtraInfo = extraInfo
                }
            }
        };
    }

    private static INPUT MouseInput(string button, bool isDown, UIntPtr extraInfo)
    {
        var normalized = KeyNameResolver.NormalizeMouseButton(button);
        var (down, up, data) = normalized switch
        {
            "mouse_left" => (MouseeventfLeftdown, MouseeventfLeftup, 0u),
            "mouse_right" => (MouseeventfRightdown, MouseeventfRightup, 0u),
            "mouse_middle" => (MouseeventfMiddledown, MouseeventfMiddleup, 0u),
            "mouse_x1" => (MouseeventfXdown, MouseeventfXup, Xbutton1),
            "mouse_x2" => (MouseeventfXdown, MouseeventfXup, Xbutton2),
            _ => throw new ArgumentException($"Unsupported mouse button: {button}", nameof(button))
        };

        return new INPUT
        {
            type = InputMouse,
            U = new InputUnion
            {
                mi = new MOUSEINPUT
                {
                    mouseData = data,
                    dwFlags = isDown ? down : up,
                    dwExtraInfo = extraInfo
                }
            }
        };
    }

    private static INPUT WheelInput(InputCommand command, UIntPtr extraInfo)
    {
        if (command.Delta is null)
        {
            throw new ArgumentException("Wheel command requires Delta.", nameof(command));
        }

        return new INPUT
        {
            type = InputMouse,
            U = new InputUnion
            {
                mi = new MOUSEINPUT
                {
                    mouseData = unchecked((uint)command.Delta.Value),
                    dwFlags = MouseeventfWheel,
                    dwExtraInfo = extraInfo
                }
            }
        };
    }

    private static void Flush(List<INPUT> inputs)
    {
        if (inputs.Count == 0)
        {
            return;
        }

        var expected = inputs.Count;
        var sent = SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
        inputs.Clear();
        if (sent == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        if (sent != expected)
        {
            throw new Win32Exception($"SendInput sent {sent} inputs, expected {expected}.");
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint cInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKeyW(uint uCode, uint uMapType);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;

        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }
}
