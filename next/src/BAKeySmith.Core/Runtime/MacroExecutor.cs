using System.Diagnostics;
using System.Globalization;
using BAKeySmith.Core.Contracts;
using BAKeySmith.Core.Diagnostics;
using BAKeySmith.Core.Foreground;
using BAKeySmith.Core.Input;

namespace BAKeySmith.Core.Runtime;

public sealed class MacroExecutor
{
    private static readonly TimeSpan ActivePointerWaitPollInterval = TimeSpan.FromMilliseconds(25);

    private readonly PressOwnershipTracker _presses;
    private readonly IInputBackend _inputBackend;
    private readonly IForegroundGate? _foregroundGate;
    private readonly IDiagnosticsSink _diagnostics;
    private readonly RuntimeConfig? _runtimeConfig;
    private readonly TimeSpan _tapHold;
    private readonly TimeSpan _pointerDelay;
    private readonly TimeSpan _comboKeyGap;
    private readonly TimeSpan _comboHold;

    public MacroExecutor(
        PressOwnershipTracker presses,
        IInputBackend inputBackend,
        IForegroundGate? foregroundGate = null,
        RuntimeConfig? runtimeConfig = null,
        IDiagnosticsSink? diagnostics = null,
        TimeSpan? tapHold = null,
        TimeSpan? pointerDelay = null,
        TimeSpan? comboKeyGap = null,
        TimeSpan? comboHold = null)
    {
        _presses = presses;
        _inputBackend = inputBackend;
        _foregroundGate = foregroundGate;
        _diagnostics = diagnostics ?? NoOpDiagnosticsSink.Instance;
        _runtimeConfig = runtimeConfig;
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

        var foregroundCleanupTriggered = false;
        try
        {
            var pc = 0;
            var remainingLoops = new Dictionary<int, int>();
            var activePointerButtons = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (pc < instructions.Count)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (activePointerButtons.Count > 0)
                {
                    var gateResult = await CheckForegroundForActivePointerSequenceAsync(cancellationToken);
                    if (!gateResult.IsAllowed)
                    {
                        foregroundCleanupTriggered = true;
                        EmitForegroundLostDuringActivePointerSequence(
                            owner,
                            activePointerButtons,
                            gateResult,
                            "instruction_boundary");
                        break;
                    }
                }

                var instruction = instructions[pc];
                var shouldStop = false;
                switch (instruction.NormalizedOp)
                {
                    case "press":
                    {
                        var key = Arg(instruction, 0);
                        await _presses.HoldKeyAsync(owner, key, cancellationToken);
                        TrackPointerHold(activePointerButtons, key, isDown: true);
                        break;
                    }
                    case "release":
                    {
                        var key = Arg(instruction, 0);
                        await _presses.ReleaseKeyAsync(owner, key, CancellationToken.None);
                        TrackPointerHold(activePointerButtons, key, isDown: false);
                        break;
                    }
                    case "tap":
                        await new InputSequencer(_presses, _tapHold)
                            .TapKeyAsync($"{owner}:tap:{pc}", Arg(instruction, 0), cancellationToken);
                        break;
                    case "wait":
                    {
                        var waitMs = ParseDouble(Arg(instruction, 0));
                        EmitWaitStarted(owner, waitMs, activePointerButtons);
                        if (activePointerButtons.Count == 0)
                        {
                            await DelayAsync(waitMs, cancellationToken);
                            EmitWaitCompleted(owner, waitMs, activePointerButtons);
                            break;
                        }

                        var waitResult = await DelayActivePointerWaitAsync(
                            owner,
                            waitMs,
                            activePointerButtons,
                            cancellationToken);
                        if (waitResult.Status == MacroWaitStatus.ForegroundLoss)
                        {
                            foregroundCleanupTriggered = true;
                            EmitForegroundLostDuringActivePointerSequence(
                                owner,
                                activePointerButtons,
                                waitResult.GateResult!,
                                "wait");
                            shouldStop = true;
                        }
                        else
                        {
                            EmitWaitCompleted(owner, waitMs, activePointerButtons);
                        }

                        break;
                    }
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
                    {
                        var dragResult = await ExecuteDragAsync(owner, instruction, cancellationToken);
                        shouldStop = dragResult.Status == BuiltInDragStatus.ForegroundLoss;
                        break;
                    }
                    case "drag_rel":
                    {
                        var dragResult = await ExecuteDragRelAsync(owner, instruction, cancellationToken);
                        shouldStop = dragResult.Status == BuiltInDragStatus.ForegroundLoss;
                        break;
                    }
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

                if (shouldStop)
                {
                    break;
                }

                pc++;
            }
        }
        finally
        {
            await _presses.ReleaseOwnerAsync(owner, CancellationToken.None);
            if (foregroundCleanupTriggered)
            {
                _diagnostics.Emit(DiagnosticEvent.Create(
                    "macro",
                    "foreground_cleanup_completed",
                    fields: new Dictionary<string, string>
                    {
                        ["owner"] = owner
                    }));
            }

            _diagnostics.Emit(DiagnosticEvent.Create(
                "macro",
                "finished",
                fields: new Dictionary<string, string> { ["owner"] = owner }));
        }
    }

    private async ValueTask<ForegroundGateResult> CheckForegroundForActivePointerSequenceAsync(CancellationToken cancellationToken)
    {
        if (_foregroundGate is null || _runtimeConfig is null)
        {
            return new ForegroundGateResult(true, string.Empty, string.Empty, string.Empty);
        }

        return await _foregroundGate.CheckAsync(_runtimeConfig, cancellationToken);
    }

    private async ValueTask<MacroWaitResult> DelayActivePointerWaitAsync(
        string owner,
        double milliseconds,
        HashSet<string> activePointerButtons,
        CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromMilliseconds(milliseconds);
        if (delay <= TimeSpan.Zero)
        {
            return MacroWaitResult.Completed();
        }

        var heldButtons = FormatActivePointerButtons(activePointerButtons);
        _diagnostics.Emit(DiagnosticEvent.Create(
            "macro",
            "active_pointer_wait_started",
            fields: new Dictionary<string, string>
            {
                ["owner"] = owner,
                ["wait_ms"] = FormatMilliseconds(delay.TotalMilliseconds),
                ["poll_interval_ms"] = FormatMilliseconds(ActivePointerWaitPollInterval.TotalMilliseconds),
                ["held_buttons"] = heldButtons
            }));

        var startedAt = Stopwatch.GetTimestamp();
        var delayTicks = (long)Math.Ceiling(delay.TotalSeconds * Stopwatch.Frequency);
        var deadline = startedAt + delayTicks;
        while (true)
        {
            var remainingTicks = deadline - Stopwatch.GetTimestamp();
            if (remainingTicks <= 0)
            {
                break;
            }

            var remaining = TimeSpan.FromSeconds(remainingTicks / (double)Stopwatch.Frequency);
            var slice = remaining < ActivePointerWaitPollInterval
                ? remaining
                : ActivePointerWaitPollInterval;
            await DelayAsync(slice, cancellationToken);

            var gateResult = await CheckForegroundForActivePointerSequenceAsync(cancellationToken);
            if (!gateResult.IsAllowed)
            {
                var elapsedMs = ElapsedMilliseconds(startedAt, Stopwatch.GetTimestamp());
                _diagnostics.Emit(DiagnosticEvent.Create(
                    "macro",
                    "foreground_loss_detected_during_wait",
                    fields: new Dictionary<string, string>
                    {
                        ["owner"] = owner,
                        ["wait_ms"] = FormatMilliseconds(delay.TotalMilliseconds),
                        ["wait_elapsed_ms"] = FormatMilliseconds(elapsedMs),
                        ["held_buttons"] = heldButtons,
                        ["target_process"] = gateResult.TargetProcess ?? string.Empty,
                        ["foreground_process"] = gateResult.ForegroundProcess ?? string.Empty,
                        ["foreground_window_title"] = gateResult.ForegroundWindowTitle ?? string.Empty
                    }));
                _diagnostics.Emit(DiagnosticEvent.Create(
                    "macro",
                    "active_pointer_wait_interrupted",
                    fields: new Dictionary<string, string>
                    {
                        ["owner"] = owner,
                        ["wait_ms"] = FormatMilliseconds(delay.TotalMilliseconds),
                        ["wait_elapsed_ms"] = FormatMilliseconds(elapsedMs),
                        ["reason"] = "foreground_loss",
                        ["held_buttons"] = heldButtons
                    }));
                return MacroWaitResult.ForegroundLoss(gateResult);
            }
        }

        return MacroWaitResult.Completed();
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

    private async ValueTask<BuiltInDragResult> ExecuteDragAsync(
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

        return await ExecuteBuiltInDragSequenceAsync(
            owner,
            "drag",
            button,
            moveAction: token => _inputBackend.MoveMouseToAsync((int)x, (int)y, token),
            cancellationToken);
    }

    private async ValueTask<BuiltInDragResult> ExecuteDragRelAsync(
        string owner,
        MacroInstruction instruction,
        CancellationToken cancellationToken)
    {
        var dx = ParseInt(Arg(instruction, 0));
        var dy = ParseInt(Arg(instruction, 1));
        var button = Arg(instruction, 2);
        var current = await _inputBackend.GetMousePositionAsync(cancellationToken);

        return await ExecuteBuiltInDragSequenceAsync(
            owner,
            "drag_rel",
            button,
            moveAction: token => _inputBackend.MoveMouseToAsync(current.X + dx, current.Y + dy, token),
            cancellationToken);
    }

    private async ValueTask<BuiltInDragResult> ExecuteBuiltInDragSequenceAsync(
        string owner,
        string helper,
        string button,
        Func<CancellationToken, ValueTask> moveAction,
        CancellationToken cancellationToken)
    {
        var normalizedButton = KeyNameResolver.NormalizeMouseButton(button);
        var dragOwner = $"{owner}:drag:{normalizedButton}";
        var held = false;
        var cleanupReason = "normal";
        await _presses.HoldKeyAsync(dragOwner, normalizedButton, cancellationToken);
        held = true;
        EmitBuiltInDragStarted(owner, dragOwner, helper, normalizedButton);

        try
        {
            var preDelayResult = await DelayBuiltInDragPointerDelayAsync(
                owner,
                dragOwner,
                helper,
                normalizedButton,
                "pre_move_delay",
                cancellationToken);
            if (preDelayResult.Status == BuiltInDragStatus.ForegroundLoss)
            {
                cleanupReason = "foreground_loss";
                return preDelayResult;
            }

            var beforeMoveResult = await CheckBuiltInDragForegroundAsync(
                owner,
                dragOwner,
                helper,
                normalizedButton,
                "before_move",
                cancellationToken);
            if (beforeMoveResult.Status == BuiltInDragStatus.ForegroundLoss)
            {
                cleanupReason = "foreground_loss";
                return beforeMoveResult;
            }

            await moveAction(cancellationToken);

            var afterMoveResult = await CheckBuiltInDragForegroundAsync(
                owner,
                dragOwner,
                helper,
                normalizedButton,
                "after_move",
                cancellationToken);
            if (afterMoveResult.Status == BuiltInDragStatus.ForegroundLoss)
            {
                cleanupReason = "foreground_loss";
                return afterMoveResult;
            }

            var postDelayResult = await DelayBuiltInDragPointerDelayAsync(
                owner,
                dragOwner,
                helper,
                normalizedButton,
                "post_move_delay",
                cancellationToken);
            if (postDelayResult.Status == BuiltInDragStatus.ForegroundLoss)
            {
                cleanupReason = "foreground_loss";
                return postDelayResult;
            }

            var beforeReleaseResult = await CheckBuiltInDragForegroundAsync(
                owner,
                dragOwner,
                helper,
                normalizedButton,
                "before_release",
                cancellationToken);
            if (beforeReleaseResult.Status == BuiltInDragStatus.ForegroundLoss)
            {
                cleanupReason = "foreground_loss";
                return beforeReleaseResult;
            }

            EmitBuiltInDragCompleted(owner, dragOwner, helper, normalizedButton);
            return BuiltInDragResult.Completed();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            cleanupReason = "cancellation";
            throw;
        }
        catch
        {
            cleanupReason = "exception";
            throw;
        }
        finally
        {
            if (held)
            {
                await _presses.ReleaseKeyAsync(dragOwner, normalizedButton, CancellationToken.None);
                EmitBuiltInDragCleanupCompleted(owner, dragOwner, helper, normalizedButton, cleanupReason);
            }
        }
    }

    private async ValueTask<BuiltInDragResult> DelayBuiltInDragPointerDelayAsync(
        string owner,
        string dragOwner,
        string helper,
        string button,
        string phase,
        CancellationToken cancellationToken)
    {
        EmitBuiltInDragPointerDelayStarted(owner, dragOwner, helper, button, phase);
        if (_pointerDelay <= TimeSpan.Zero)
        {
            return BuiltInDragResult.Completed();
        }

        var startedAt = Stopwatch.GetTimestamp();
        var delayTicks = (long)Math.Ceiling(_pointerDelay.TotalSeconds * Stopwatch.Frequency);
        var deadline = startedAt + delayTicks;
        while (true)
        {
            var remainingTicks = deadline - Stopwatch.GetTimestamp();
            if (remainingTicks <= 0)
            {
                break;
            }

            var remaining = TimeSpan.FromSeconds(remainingTicks / (double)Stopwatch.Frequency);
            var slice = remaining < ActivePointerWaitPollInterval
                ? remaining
                : ActivePointerWaitPollInterval;
            await DelayAsync(slice, cancellationToken);

            var gateResult = await CheckForegroundForActivePointerSequenceAsync(cancellationToken);
            if (!gateResult.IsAllowed)
            {
                EmitBuiltInDragForegroundLossDetected(
                    owner,
                    dragOwner,
                    helper,
                    button,
                    phase,
                    gateResult,
                    ElapsedMilliseconds(startedAt, Stopwatch.GetTimestamp()));
                return BuiltInDragResult.ForegroundLoss(gateResult);
            }
        }

        return BuiltInDragResult.Completed();
    }

    private async ValueTask<BuiltInDragResult> CheckBuiltInDragForegroundAsync(
        string owner,
        string dragOwner,
        string helper,
        string button,
        string phase,
        CancellationToken cancellationToken)
    {
        var gateResult = await CheckForegroundForActivePointerSequenceAsync(cancellationToken);
        if (gateResult.IsAllowed)
        {
            return BuiltInDragResult.Completed();
        }

        EmitBuiltInDragForegroundLossDetected(
            owner,
            dragOwner,
            helper,
            button,
            phase,
            gateResult,
            elapsedMs: null);
        return BuiltInDragResult.ForegroundLoss(gateResult);
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

    private void EmitWaitStarted(
        string owner,
        double waitMs,
        HashSet<string> activePointerButtons)
    {
        _diagnostics.Emit(DiagnosticEvent.Create(
            "macro",
            "wait_started",
            fields: new Dictionary<string, string>
            {
                ["owner"] = owner,
                ["wait_ms"] = FormatMilliseconds(waitMs),
                ["active_pointer_held"] = (activePointerButtons.Count > 0).ToString(),
                ["held_buttons"] = FormatActivePointerButtons(activePointerButtons)
            }));
    }

    private void EmitWaitCompleted(
        string owner,
        double waitMs,
        HashSet<string> activePointerButtons)
    {
        _diagnostics.Emit(DiagnosticEvent.Create(
            "macro",
            "wait_completed",
            fields: new Dictionary<string, string>
            {
                ["owner"] = owner,
                ["wait_ms"] = FormatMilliseconds(waitMs),
                ["active_pointer_held"] = (activePointerButtons.Count > 0).ToString(),
                ["held_buttons"] = FormatActivePointerButtons(activePointerButtons)
            }));
    }

    private void EmitForegroundLostDuringActivePointerSequence(
        string owner,
        HashSet<string> activePointerButtons,
        ForegroundGateResult gateResult,
        string detectionPhase)
    {
        _diagnostics.Emit(DiagnosticEvent.Create(
            "macro",
            "foreground_lost_during_active_pointer_sequence",
            fields: new Dictionary<string, string>
            {
                ["owner"] = owner,
                ["held_buttons"] = FormatActivePointerButtons(activePointerButtons),
                ["target_process"] = gateResult.TargetProcess ?? string.Empty,
                ["foreground_process"] = gateResult.ForegroundProcess ?? string.Empty,
                ["foreground_window_title"] = gateResult.ForegroundWindowTitle ?? string.Empty,
                ["detection_phase"] = detectionPhase
            }));
    }

    private void EmitBuiltInDragStarted(
        string owner,
        string dragOwner,
        string helper,
        string button)
    {
        _diagnostics.Emit(DiagnosticEvent.Create(
            "macro",
            "builtin_drag_started",
            fields: BuiltInDragFields(owner, dragOwner, helper, button)));
    }

    private void EmitBuiltInDragPointerDelayStarted(
        string owner,
        string dragOwner,
        string helper,
        string button,
        string phase)
    {
        var fields = BuiltInDragFields(owner, dragOwner, helper, button);
        fields["phase"] = phase;
        fields["pointer_delay_ms"] = FormatMilliseconds(_pointerDelay.TotalMilliseconds);
        _diagnostics.Emit(DiagnosticEvent.Create(
            "macro",
            "builtin_drag_pointer_delay_started",
            fields: fields));
    }

    private void EmitBuiltInDragForegroundLossDetected(
        string owner,
        string dragOwner,
        string helper,
        string button,
        string phase,
        ForegroundGateResult gateResult,
        double? elapsedMs)
    {
        var fields = BuiltInDragFields(owner, dragOwner, helper, button);
        fields["phase"] = phase;
        fields["target_process"] = gateResult.TargetProcess ?? string.Empty;
        fields["foreground_process"] = gateResult.ForegroundProcess ?? string.Empty;
        fields["foreground_window_title"] = gateResult.ForegroundWindowTitle ?? string.Empty;
        if (elapsedMs is not null)
        {
            fields["elapsed_ms"] = FormatMilliseconds(elapsedMs.Value);
        }

        _diagnostics.Emit(DiagnosticEvent.Create(
            "macro",
            "builtin_drag_foreground_loss_detected",
            fields: fields));
        var interruptedFields = fields.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.OrdinalIgnoreCase);
        interruptedFields["reason"] = "foreground_loss";
        _diagnostics.Emit(DiagnosticEvent.Create(
            "macro",
            "builtin_drag_interrupted",
            fields: interruptedFields));
    }

    private void EmitBuiltInDragCompleted(
        string owner,
        string dragOwner,
        string helper,
        string button)
    {
        _diagnostics.Emit(DiagnosticEvent.Create(
            "macro",
            "builtin_drag_completed",
            fields: BuiltInDragFields(owner, dragOwner, helper, button)));
    }

    private void EmitBuiltInDragCleanupCompleted(
        string owner,
        string dragOwner,
        string helper,
        string button,
        string reason)
    {
        var fields = BuiltInDragFields(owner, dragOwner, helper, button);
        fields["cleanup_source"] = "helper_drag_owner";
        fields["reason"] = reason;
        _diagnostics.Emit(DiagnosticEvent.Create(
            "macro",
            "builtin_drag_cleanup_completed",
            fields: fields));
    }

    private static Dictionary<string, string> BuiltInDragFields(
        string owner,
        string dragOwner,
        string helper,
        string button)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["owner"] = owner,
            ["drag_owner"] = dragOwner,
            ["helper"] = helper,
            ["button"] = button
        };
    }

    private static int ParseInt(string value) =>
        int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);

    private static double ParseDouble(string value) =>
        double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static string FormatActivePointerButtons(HashSet<string> activePointerButtons) =>
        string.Join(",", activePointerButtons.OrderBy(button => button, StringComparer.OrdinalIgnoreCase));

    private static string FormatMilliseconds(double milliseconds) =>
        milliseconds.ToString("0.###", CultureInfo.InvariantCulture);

    private static double ElapsedMilliseconds(long startTicks, long endTicks) =>
        (endTicks - startTicks) * 1000.0 / Stopwatch.Frequency;

    private static string NormalizeOwner(string owner)
    {
        if (string.IsNullOrWhiteSpace(owner))
        {
            throw new ArgumentException("Macro owner must not be empty.", nameof(owner));
        }

        return owner.Trim().ToLowerInvariant();
    }

    private static void TrackPointerHold(HashSet<string> activePointerButtons, string key, bool isDown)
    {
        if (!KeyNameResolver.IsMouseButton(key))
        {
            return;
        }

        var normalized = KeyNameResolver.NormalizeMouseButton(key);
        if (isDown)
        {
            activePointerButtons.Add(normalized);
            return;
        }

        activePointerButtons.Remove(normalized);
    }

    private enum MacroWaitStatus
    {
        Completed,
        ForegroundLoss
    }

    private sealed record MacroWaitResult(MacroWaitStatus Status, ForegroundGateResult? GateResult)
    {
        public static MacroWaitResult Completed() => new(MacroWaitStatus.Completed, null);

        public static MacroWaitResult ForegroundLoss(ForegroundGateResult gateResult) =>
            new(MacroWaitStatus.ForegroundLoss, gateResult);
    }

    private enum BuiltInDragStatus
    {
        Completed,
        ForegroundLoss
    }

    private sealed record BuiltInDragResult(BuiltInDragStatus Status, ForegroundGateResult? GateResult)
    {
        public static BuiltInDragResult Completed() => new(BuiltInDragStatus.Completed, null);

        public static BuiltInDragResult ForegroundLoss(ForegroundGateResult gateResult) =>
            new(BuiltInDragStatus.ForegroundLoss, gateResult);
    }
}
