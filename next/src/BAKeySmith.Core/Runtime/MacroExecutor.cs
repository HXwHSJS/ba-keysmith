using System.Globalization;
using BAKeySmith.Core.Contracts;
using BAKeySmith.Core.Diagnostics;
using BAKeySmith.Core.Input;

namespace BAKeySmith.Core.Runtime;

public sealed class MacroExecutor
{
    private readonly PressOwnershipTracker _presses;
    private readonly IInputBackend _inputBackend;
    private readonly IDiagnosticsSink _diagnostics;
    private readonly TimeSpan _tapHold;
    private readonly TimeSpan _pointerDelay;
    private readonly TimeSpan _comboKeyGap;
    private readonly TimeSpan _comboHold;

    public MacroExecutor(
        PressOwnershipTracker presses,
        IInputBackend inputBackend,
        IDiagnosticsSink? diagnostics = null,
        TimeSpan? tapHold = null,
        TimeSpan? pointerDelay = null,
        TimeSpan? comboKeyGap = null,
        TimeSpan? comboHold = null)
    {
        _presses = presses;
        _inputBackend = inputBackend;
        _diagnostics = diagnostics ?? NoOpDiagnosticsSink.Instance;
        _tapHold = tapHold ?? TimeSpan.FromMilliseconds(20);
        _pointerDelay = pointerDelay ?? TimeSpan.Zero;
        _comboKeyGap = comboKeyGap ?? TimeSpan.Zero;
        _comboHold = comboHold ?? TimeSpan.FromMilliseconds(1);
    }

    public async ValueTask ExecuteAsync(
        string owner,
        IReadOnlyList<MacroInstruction> instructions,
        CancellationToken cancellationToken)
    {
        owner = NormalizeOwner(owner);
        _diagnostics.Emit(DiagnosticEvent.Create(
            "macro",
            "started",
            fields: new Dictionary<string, string>
            {
                ["owner"] = owner,
                ["instruction_count"] = instructions.Count.ToString()
            }));

        try
        {
            var pc = 0;
            var remainingLoops = new Dictionary<int, int>();
            while (pc < instructions.Count)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var instruction = instructions[pc];
                switch (instruction.NormalizedOp)
                {
                    case "press":
                        await _presses.HoldKeyAsync(owner, Arg(instruction, 0), cancellationToken);
                        break;
                    case "release":
                        await _presses.ReleaseKeyAsync(owner, Arg(instruction, 0), CancellationToken.None);
                        break;
                    case "tap":
                        await new InputSequencer(_presses, _tapHold)
                            .TapKeyAsync($"{owner}:tap:{pc}", Arg(instruction, 0), cancellationToken);
                        break;
                    case "wait":
                        await DelayAsync(ParseDouble(Arg(instruction, 0)), cancellationToken);
                        break;
                    case "combo":
                        await ExecuteComboAsync(owner, instruction.Args, cancellationToken);
                        break;
                    case "setpos":
                        await ExecuteSetPosAsync(instruction, cancellationToken);
                        break;
                    case "setpos_rel":
                        await ExecuteSetPosRelAsync(instruction, cancellationToken);
                        break;
                    case "drag":
                        await ExecuteDragAsync(owner, instruction, cancellationToken);
                        break;
                    case "drag_rel":
                        await ExecuteDragRelAsync(owner, instruction, cancellationToken);
                        break;
                    case "loop_start":
                        if (HandleLoopStart(instruction, pc, remainingLoops, out var nextPc))
                        {
                            pc = nextPc;
                            continue;
                        }

                        break;
                    case "loop_end":
                        if (HandleLoopEnd(instruction, remainingLoops, out var loopPc))
                        {
                            pc = loopPc;
                            continue;
                        }

                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported macro instruction: {instruction.Op}");
                }

                pc++;
            }
        }
        finally
        {
            await _presses.ReleaseOwnerAsync(owner, CancellationToken.None);
            _diagnostics.Emit(DiagnosticEvent.Create(
                "macro",
                "finished",
                fields: new Dictionary<string, string> { ["owner"] = owner }));
        }
    }

    private async ValueTask ExecuteComboAsync(
        string owner,
        IReadOnlyList<string> keys,
        CancellationToken cancellationToken)
    {
        var comboOwner = $"{owner}:combo";
        try
        {
            foreach (var key in keys)
            {
                await _presses.HoldKeyAsync(comboOwner, key, cancellationToken);
                await DelayAsync(_comboKeyGap, cancellationToken);
            }

            await DelayAsync(_comboHold, cancellationToken);
        }
        finally
        {
            for (var i = keys.Count - 1; i >= 0; i--)
            {
                await _presses.ReleaseKeyAsync(comboOwner, keys[i], CancellationToken.None);
                await DelayAsync(_comboKeyGap, CancellationToken.None);
            }
        }
    }

    private async ValueTask ExecuteSetPosAsync(
        MacroInstruction instruction,
        CancellationToken cancellationToken)
    {
        var x = ParseDouble(Arg(instruction, 0));
        var y = ParseDouble(Arg(instruction, 1));
        var useMouseScale = bool.Parse(Arg(instruction, 2));
        if (useMouseScale)
        {
            var screen = await _inputBackend.GetScreenSizeAsync(cancellationToken);
            x *= screen.Width;
            y *= screen.Height;
        }

        await _inputBackend.MoveMouseToAsync((int)x, (int)y, cancellationToken);
    }

    private async ValueTask ExecuteSetPosRelAsync(
        MacroInstruction instruction,
        CancellationToken cancellationToken)
    {
        var dx = ParseInt(Arg(instruction, 0));
        var dy = ParseInt(Arg(instruction, 1));
        var current = await _inputBackend.GetMousePositionAsync(cancellationToken);
        await _inputBackend.MoveMouseToAsync(current.X + dx, current.Y + dy, cancellationToken);
    }

    private async ValueTask ExecuteDragAsync(
        string owner,
        MacroInstruction instruction,
        CancellationToken cancellationToken)
    {
        var x = ParseDouble(Arg(instruction, 0));
        var y = ParseDouble(Arg(instruction, 1));
        var useMouseScale = bool.Parse(Arg(instruction, 2));
        var button = Arg(instruction, 3);
        if (useMouseScale)
        {
            var screen = await _inputBackend.GetScreenSizeAsync(cancellationToken);
            x *= screen.Width;
            y *= screen.Height;
        }

        var dragOwner = $"{owner}:drag:{button}";
        await _presses.HoldKeyAsync(dragOwner, button, cancellationToken);
        try
        {
            await DelayAsync(_pointerDelay, cancellationToken);
            await _inputBackend.MoveMouseToAsync((int)x, (int)y, cancellationToken);
            await DelayAsync(_pointerDelay, cancellationToken);
        }
        finally
        {
            await _presses.ReleaseKeyAsync(dragOwner, button, CancellationToken.None);
        }
    }

    private async ValueTask ExecuteDragRelAsync(
        string owner,
        MacroInstruction instruction,
        CancellationToken cancellationToken)
    {
        var dx = ParseInt(Arg(instruction, 0));
        var dy = ParseInt(Arg(instruction, 1));
        var button = Arg(instruction, 2);
        var current = await _inputBackend.GetMousePositionAsync(cancellationToken);

        var dragOwner = $"{owner}:drag:{button}";
        await _presses.HoldKeyAsync(dragOwner, button, cancellationToken);
        try
        {
            await DelayAsync(_pointerDelay, cancellationToken);
            await _inputBackend.MoveMouseToAsync(current.X + dx, current.Y + dy, cancellationToken);
            await DelayAsync(_pointerDelay, cancellationToken);
        }
        finally
        {
            await _presses.ReleaseKeyAsync(dragOwner, button, CancellationToken.None);
        }
    }

    private static bool HandleLoopStart(
        MacroInstruction instruction,
        int pc,
        Dictionary<int, int> remainingLoops,
        out int nextPc)
    {
        var count = ParseInt(Arg(instruction, 0));
        var endIndex = ParseInt(Arg(instruction, 1));
        nextPc = pc;
        if (count == 0)
        {
            return false;
        }

        if (!remainingLoops.ContainsKey(pc))
        {
            remainingLoops[pc] = count;
        }

        if (remainingLoops[pc] <= 0)
        {
            remainingLoops.Remove(pc);
            nextPc = endIndex + 1;
            return true;
        }

        return false;
    }

    private static bool HandleLoopEnd(
        MacroInstruction instruction,
        Dictionary<int, int> remainingLoops,
        out int nextPc)
    {
        var startIndex = ParseInt(Arg(instruction, 0));
        nextPc = startIndex + 1;
        if (!remainingLoops.TryGetValue(startIndex, out var remaining))
        {
            return true;
        }

        remaining--;
        if (remaining > 0)
        {
            remainingLoops[startIndex] = remaining;
            return true;
        }

        remainingLoops.Remove(startIndex);
        return false;
    }

    private static string Arg(MacroInstruction instruction, int index)
    {
        if (instruction.Args.Count <= index)
        {
            throw new InvalidOperationException(
                $"Instruction {instruction.Op} at line {instruction.Line} is missing argument {index}.");
        }

        return instruction.Args[index];
    }

    private static async ValueTask DelayAsync(double milliseconds, CancellationToken cancellationToken)
    {
        await DelayAsync(TimeSpan.FromMilliseconds(milliseconds), cancellationToken);
    }

    private static async ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        if (delay <= TimeSpan.Zero)
        {
            return;
        }

        await Task.Delay(delay, cancellationToken);
    }

    private static int ParseInt(string value) =>
        int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);

    private static double ParseDouble(string value) =>
        double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static string NormalizeOwner(string owner)
    {
        if (string.IsNullOrWhiteSpace(owner))
        {
            throw new ArgumentException("Macro owner must not be empty.", nameof(owner));
        }

        return owner.Trim().ToLowerInvariant();
    }
}
