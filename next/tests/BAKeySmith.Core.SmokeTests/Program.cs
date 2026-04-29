using System.Diagnostics;
using BAKeySmith.Core.Configuration;
using BAKeySmith.Core.Contracts;
using BAKeySmith.Core.Diagnostics;
using BAKeySmith.Core.Foreground;
using BAKeySmith.Core.Hosting;
using BAKeySmith.Core.Input;
using BAKeySmith.Core.Runtime;
using BAKeySmith.Core.Scripting;
using BAKeySmith.Core.Triggers;

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static TException AssertThrows<TException>(Action action, string message)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException ex)
    {
        return ex;
    }

    throw new InvalidOperationException(message);
}

static async Task WaitForInputEventsAsync(DryRunInputBackend backend, int expectedCount)
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
    while (backend.Events.Count < expectedCount)
    {
        await Task.Delay(1, timeout.Token);
    }
}

static async Task WaitForConditionAsync(Func<bool> condition, string message)
{
    var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(2);
    while (DateTimeOffset.UtcNow < deadline)
    {
        if (condition())
        {
            return;
        }

        await Task.Delay(1);
    }

    throw new InvalidOperationException(message);
}

static int CountMoves(DryRunInputBackend backend)
{
    return backend.Events.Count(evt => string.Equals(evt.Kind, "move", StringComparison.OrdinalIgnoreCase));
}

static double ElapsedMilliseconds(long startTicks, long endTicks)
{
    return (endTicks - startTicks) * 1000.0 / Stopwatch.Frequency;
}

static IReadOnlyList<MacroInstruction> CompileMacro(string script)
{
    var compiler = new MacroScriptCompiler();
    var result = compiler.Compile(script);
    AssertTrue(result.Success, string.Join(Environment.NewLine, result.Errors));
    return result.Instructions;
}

static int CountMacroDiagnostics(
    InMemoryDiagnosticsSink diagnostics,
    string name,
    string? helper = null,
    string? phase = null,
    string? reason = null)
{
    return diagnostics.Events.Count(evt =>
        string.Equals(evt.Source, "macro", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(evt.Name, name, StringComparison.OrdinalIgnoreCase) &&
        (helper is null ||
            (evt.Fields.TryGetValue("helper", out var helperValue) &&
             string.Equals(helperValue, helper, StringComparison.OrdinalIgnoreCase))) &&
        (phase is null ||
            (evt.Fields.TryGetValue("phase", out var phaseValue) &&
             string.Equals(phaseValue, phase, StringComparison.OrdinalIgnoreCase))) &&
        (reason is null ||
            (evt.Fields.TryGetValue("reason", out var reasonValue) &&
             string.Equals(reasonValue, reason, StringComparison.OrdinalIgnoreCase))));
}

static string? FirstMacroDiagnosticField(
    InMemoryDiagnosticsSink diagnostics,
    string name,
    string field,
    string? helper = null)
{
    return diagnostics.Events
        .Where(evt =>
            string.Equals(evt.Source, "macro", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(evt.Name, name, StringComparison.OrdinalIgnoreCase) &&
            (helper is null ||
                (evt.Fields.TryGetValue("helper", out var helperValue) &&
                 string.Equals(helperValue, helper, StringComparison.OrdinalIgnoreCase))))
        .Select(evt => evt.Fields.TryGetValue(field, out var value) ? value : null)
        .FirstOrDefault(value => value is not null);
}

static async Task OwnerRefCountKeepsKeyDownUntilLastOwnerReleases()
{
    var backend = new DryRunInputBackend();
    var presses = new PressOwnershipTracker(backend);

    await presses.HoldKeyAsync("simple:q", "1", CancellationToken.None);
    await presses.HoldKeyAsync("macro:x", "1", CancellationToken.None);

    AssertTrue(backend.Count("key", "1", true) == 1, "Key down should be sent only once.");

    await presses.ReleaseKeyAsync("simple:q", "1", CancellationToken.None);
    AssertTrue(backend.Count("key", "1", false) == 0, "First owner release must not send key up.");

    await presses.ReleaseKeyAsync("macro:x", "1", CancellationToken.None);
    AssertTrue(backend.Count("key", "1", false) == 1, "Last owner release must send key up.");
    AssertTrue(presses.Snapshot().IsEmpty, "Ownership state should be empty.");
}

static async Task ReleaseOwnerOnlyReleasesOwnedKeys()
{
    var backend = new DryRunInputBackend();
    var presses = new PressOwnershipTracker(backend);

    await presses.HoldKeyAsync("owner:a", "1", CancellationToken.None);
    await presses.HoldKeyAsync("owner:a", "2", CancellationToken.None);
    await presses.HoldKeyAsync("owner:b", "1", CancellationToken.None);

    await presses.ReleaseOwnerAsync("owner:a", CancellationToken.None);

    AssertTrue(backend.Count("key", "2", false) == 1, "Owner release should release owner-exclusive key.");
    AssertTrue(backend.Count("key", "1", false) == 0, "Shared key must remain down after owner A releases.");

    await presses.ReleaseOwnerAsync("owner:b", CancellationToken.None);
    AssertTrue(backend.Count("key", "1", false) == 1, "Shared key should release after final owner releases.");
    AssertTrue(presses.Snapshot().IsEmpty, "Ownership state should be empty.");
}

static async Task PressOwnershipRollsBackFailedDownWithoutCleanupUp()
{
    var keyBackend = new ThrowingDownInputBackend("1");
    var keyPresses = new PressOwnershipTracker(keyBackend);
    var keyFailed = false;
    try
    {
        await keyPresses.HoldKeyAsync("owner:key", "1", CancellationToken.None);
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("simulated down failure", StringComparison.OrdinalIgnoreCase))
    {
        keyFailed = true;
    }

    AssertTrue(keyFailed, "Key down failure should be surfaced to the caller.");
    AssertTrue(keyPresses.Snapshot().IsEmpty, "Failed key down must roll back owner state.");
    await keyPresses.ReleaseOwnerAsync("owner:key", CancellationToken.None);
    await keyPresses.ReleaseAllAsync(CancellationToken.None);
    AssertTrue(keyBackend.KeyUpCalls == 0, "Cleanup after failed key down must not emit a key up.");

    var mouseBackend = new ThrowingDownInputBackend("mouse_x1");
    var mousePresses = new PressOwnershipTracker(mouseBackend);
    var mouseFailed = false;
    try
    {
        await mousePresses.HoldKeyAsync("owner:mouse", "mouse_x1", CancellationToken.None);
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("simulated down failure", StringComparison.OrdinalIgnoreCase))
    {
        mouseFailed = true;
    }

    AssertTrue(mouseFailed, "Mouse down failure should be surfaced to the caller.");
    AssertTrue(mousePresses.Snapshot().IsEmpty, "Failed mouse down must roll back owner state.");
    await mousePresses.ReleaseOwnerAsync("owner:mouse", CancellationToken.None);
    await mousePresses.ReleaseAllAsync(CancellationToken.None);
    AssertTrue(mouseBackend.MouseUpCalls == 0, "Cleanup after failed mouse down must not emit a mouse up.");
}

static async Task TapUsesOwnershipTracker()
{
    var backend = new DryRunInputBackend();
    var presses = new PressOwnershipTracker(backend);
    var sequencer = new InputSequencer(presses, TimeSpan.FromMilliseconds(1));

    await sequencer.TapKeyAsync("tap:q", "escape", CancellationToken.None);

    AssertTrue(backend.Count("key", "escape", true) == 1, "Tap should send key down.");
    AssertTrue(backend.Count("key", "escape", false) == 1, "Tap should send key up.");
    AssertTrue(presses.Snapshot().IsEmpty, "Tap should not leave ownership state.");
}

static async Task RuntimeSessionStopCancelsWorkersAndReleasesHeldKeys()
{
    var backend = new DryRunInputBackend();
    var presses = new PressOwnershipTracker(backend);
    await using var session = new RuntimeSession(presses);

    session.StartWorker(async token =>
    {
        await presses.HoldKeyAsync("worker:macro", "1", token);
        while (!token.IsCancellationRequested)
        {
            await Task.Delay(10, token);
        }
    });

    await Task.Delay(30);
    await session.StopAsync(TimeSpan.FromMilliseconds(200));

    AssertTrue(backend.Count("key", "1", true) == 1, "Worker should press key.");
    AssertTrue(backend.Count("key", "1", false) == 1, "Stop should release held key.");
    AssertTrue(presses.Snapshot().IsEmpty, "Stop should clear ownership state.");
}

static RuntimeConfig Config(params MappingDefinition[] mappings)
{
    return new RuntimeConfig("BlueArchive.exe", 1, mappings);
}

static async Task RuntimeContractHoldRespectsForegroundGate()
{
    var backend = new DryRunInputBackend();
    var gate = new ManualForegroundGate { IsAllowed = false };
    await using var runtime = new InProcessRuntimeCore(backend, gate);
    await runtime.LoadAsync(Config(
        new MappingDefinition("hold:q", TriggerSpec.Keyboard("q"), RuntimeAction.Hold("1"))),
        CancellationToken.None);
    await runtime.EnableAsync(CancellationToken.None);

    await runtime.HandleTriggerAsync(TriggerEvent.Down(TriggerSpec.Keyboard("q")), CancellationToken.None);
    AssertTrue(backend.Count("key", "1", true) == 0, "Inactive foreground must block mapping.");

    gate.IsAllowed = true;
    await runtime.HandleTriggerAsync(TriggerEvent.Down(TriggerSpec.Keyboard("q")), CancellationToken.None);
    await runtime.HandleTriggerAsync(TriggerEvent.Up(TriggerSpec.Keyboard("q")), CancellationToken.None);

    AssertTrue(backend.Count("key", "1", true) == 1, "Active foreground should allow key down.");
    AssertTrue(backend.Count("key", "1", false) == 1, "Trigger up should release hold mapping.");
}

static async Task RuntimeContractTapUsesUnifiedTapSemantics()
{
    var backend = new DryRunInputBackend();
    await using var runtime = new InProcessRuntimeCore(backend);
    await runtime.LoadAsync(Config(
        new MappingDefinition("tap:q", TriggerSpec.Keyboard("q"), RuntimeAction.Tap("escape"))),
        CancellationToken.None);
    await runtime.EnableAsync(CancellationToken.None);

    await runtime.HandleTriggerAsync(TriggerEvent.Down(TriggerSpec.Keyboard("q")), CancellationToken.None);
    await Task.Delay(20);

    AssertTrue(backend.Count("key", "escape", true) == 1, "Runtime tap should send key down.");
    AssertTrue(backend.Count("key", "escape", false) == 1, "Runtime tap should send key up.");
    AssertTrue(runtime.Snapshot().Presses.IsEmpty, "Runtime tap must not leave held keys.");
}

static async Task RuntimeContractReloadStopsOldMapping()
{
    var backend = new DryRunInputBackend();
    await using var runtime = new InProcessRuntimeCore(backend);
    await runtime.LoadAsync(Config(
        new MappingDefinition("hold:q", TriggerSpec.Keyboard("q"), RuntimeAction.Hold("1"))),
        CancellationToken.None);
    await runtime.EnableAsync(CancellationToken.None);

    await runtime.HandleTriggerAsync(TriggerEvent.Down(TriggerSpec.Keyboard("q")), CancellationToken.None);
    await runtime.ReloadAsync(Config(
        new MappingDefinition("hold:w", TriggerSpec.Keyboard("w"), RuntimeAction.Hold("2"))),
        CancellationToken.None);

    await runtime.HandleTriggerAsync(TriggerEvent.Down(TriggerSpec.Keyboard("q")), CancellationToken.None);
    await runtime.HandleTriggerAsync(TriggerEvent.Down(TriggerSpec.Keyboard("w")), CancellationToken.None);
    await runtime.HandleTriggerAsync(TriggerEvent.Up(TriggerSpec.Keyboard("w")), CancellationToken.None);

    AssertTrue(backend.Count("key", "1", true) == 1, "Old mapping should have triggered before reload.");
    AssertTrue(backend.Count("key", "1", false) == 1, "Reload should release old held key.");
    AssertTrue(backend.Count("key", "2", true) == 1, "New mapping should trigger after reload.");
    AssertTrue(backend.Count("key", "2", false) == 1, "New mapping should release normally.");
}

static async Task RuntimeContractReloadDuringBurstDropsOldQueueCleanly()
{
    var backend = new DryRunInputBackend();
    await using var runtime = new InProcessRuntimeCore(backend);
    await runtime.LoadAsync(new RuntimeConfig(
        "BlueArchive.exe",
        TapHoldMilliseconds: 50,
        Mappings:
        [
            new MappingDefinition("tap:q", TriggerSpec.Keyboard("q"), RuntimeAction.Tap("1"))
        ]),
        CancellationToken.None);
    await runtime.EnableAsync(CancellationToken.None);

    for (var index = 0; index < 12; index++)
    {
        await runtime.HandleTriggerAsync(TriggerEvent.Down(TriggerSpec.Keyboard("q")), CancellationToken.None);
    }

    await WaitForConditionAsync(
        () =>
        {
            var snapshot = runtime.Snapshot();
            return snapshot.PendingActionCount > 0 || snapshot.RunningActionCount > 0;
        },
        "Old tap queue should become busy before reload.");

    await runtime.ReloadAsync(new RuntimeConfig(
        "BlueArchive.exe",
        TapHoldMilliseconds: 1,
        Mappings:
        [
            new MappingDefinition("tap:w", TriggerSpec.Keyboard("w"), RuntimeAction.Tap("2"))
        ]),
        CancellationToken.None);

    var afterReload = runtime.Snapshot();
    AssertTrue(afterReload.PendingActionCount == 0, "Reload should drop pending old tap actions.");
    AssertTrue(afterReload.RunningActionCount == 0, "Reload should stop running old tap action.");
    AssertTrue(afterReload.Presses.IsEmpty, "Reload should leave no held inputs from old queue.");

    var oldOutputCount = backend.Count("key", "1");
    await runtime.HandleTriggerAsync(TriggerEvent.Down(TriggerSpec.Keyboard("q")), CancellationToken.None);
    await Task.Delay(20);
    AssertTrue(backend.Count("key", "1") == oldOutputCount, "Old q mapping must not produce input after reload.");

    await runtime.HandleTriggerAsync(TriggerEvent.Down(TriggerSpec.Keyboard("w")), CancellationToken.None);
    await WaitForInputEventsAsync(backend, oldOutputCount + 2);
    AssertTrue(backend.Count("key", "2", true) == 1, "New w mapping should produce key down after reload.");
    AssertTrue(backend.Count("key", "2", false) == 1, "New w mapping should produce key up after reload.");
}

static async Task RuntimeContractReloadDuringLongMacroReleasesOldOwner()
{
    var backend = new DryRunInputBackend();
    var compiler = new MacroScriptCompiler();
    var result = compiler.Compile("""
        press a
        wait 5000
        release a
        """);
    AssertTrue(result.Success, string.Join(Environment.NewLine, result.Errors));

    await using var runtime = new InProcessRuntimeCore(backend);
    await runtime.LoadAsync(new RuntimeConfig(
        "BlueArchive.exe",
        TapHoldMilliseconds: 20,
        Mappings:
        [
            new MappingDefinition(
                "macro:q",
                TriggerSpec.Keyboard("q"),
                RuntimeAction.MacroPlan(result.Instructions))
        ]),
        CancellationToken.None);
    await runtime.EnableAsync(CancellationToken.None);

    await runtime.HandleTriggerAsync(TriggerEvent.Down(TriggerSpec.Keyboard("q")), CancellationToken.None);
    await WaitForConditionAsync(
        () => backend.Count("key", "a", true) == 1,
        "Long macro should press key a before reload.");

    await runtime.ReloadAsync(new RuntimeConfig(
        "BlueArchive.exe",
        TapHoldMilliseconds: 1,
        Mappings:
        [
            new MappingDefinition("tap:w", TriggerSpec.Keyboard("w"), RuntimeAction.Tap("2"))
        ]),
        CancellationToken.None);

    var afterReload = runtime.Snapshot();
    AssertTrue(backend.Count("key", "a", true) == 1, "Long macro should press key a once before reload.");
    AssertTrue(backend.Count("key", "a", false) == 1, "Reload should release long macro key a once.");
    AssertTrue(afterReload.PendingActionCount == 0, "Reload should clear pending long macro actions.");
    AssertTrue(afterReload.RunningActionCount == 0, "Reload should stop running long macro action.");
    AssertTrue(afterReload.Presses.IsEmpty, "Reload should leave no held macro owner.");

    var oldMacroEvents = backend.Count("key", "a");
    await runtime.HandleTriggerAsync(TriggerEvent.Down(TriggerSpec.Keyboard("q")), CancellationToken.None);
    await Task.Delay(20);
    AssertTrue(backend.Count("key", "a") == oldMacroEvents, "Old macro trigger must not produce input after reload.");

    await runtime.HandleTriggerAsync(TriggerEvent.Down(TriggerSpec.Keyboard("w")), CancellationToken.None);
    await WaitForInputEventsAsync(backend, oldMacroEvents + 2);
    AssertTrue(backend.Count("key", "2", true) == 1, "New w mapping should produce key down after long macro reload.");
    AssertTrue(backend.Count("key", "2", false) == 1, "New w mapping should produce key up after long macro reload.");
}

static async Task RuntimeContractDisableCancelsDelayedTap()
{
    var backend = new DryRunInputBackend();
    await using var runtime = new InProcessRuntimeCore(backend);
    await runtime.LoadAsync(new RuntimeConfig(
        "BlueArchive.exe",
        TapHoldMilliseconds: 500,
        Mappings:
        [
            new MappingDefinition("tap:q", TriggerSpec.Keyboard("q"), RuntimeAction.Tap("1"))
        ]),
        CancellationToken.None);
    await runtime.EnableAsync(CancellationToken.None);

    await runtime.HandleTriggerAsync(TriggerEvent.Down(TriggerSpec.Keyboard("q")), CancellationToken.None);
    await Task.Delay(30);
    await runtime.DisableAsync(CancellationToken.None);

    var eventsAfterDisable = backend.Events.Count;
    await Task.Delay(80);

    AssertTrue(backend.Count("key", "1", true) == 1, "Delayed tap should press before disable.");
    AssertTrue(backend.Count("key", "1", false) == 1, "Disable must force release delayed tap.");
    AssertTrue(backend.Events.Count == eventsAfterDisable, "Disable must prevent delayed actions after stop.");
    AssertTrue(runtime.Snapshot().Presses.IsEmpty, "Disable should clear held keys.");
}

static async Task RuntimeContractStopDuringLongMacroReleasesHeldInput()
{
    var backend = new DryRunInputBackend();
    var compiler = new MacroScriptCompiler();
    var result = compiler.Compile("""
        press a
        wait 5000
        release a
        """);
    AssertTrue(result.Success, string.Join(Environment.NewLine, result.Errors));

    await using var runtime = new InProcessRuntimeCore(backend);
    await runtime.LoadAsync(Config(
        new MappingDefinition(
            "macro:q",
            TriggerSpec.Keyboard("q"),
            RuntimeAction.MacroPlan(result.Instructions))),
        CancellationToken.None);
    await runtime.EnableAsync(CancellationToken.None);

    await runtime.HandleTriggerAsync(TriggerEvent.Down(TriggerSpec.Keyboard("q")), CancellationToken.None);
    await WaitForConditionAsync(
        () => backend.Count("key", "a", true) == 1,
        "Long macro should press key a before stop.");

    await runtime.StopAsync(CancellationToken.None);
    var eventsAfterStop = backend.Events.Count;
    await Task.Delay(80);

    AssertTrue(backend.Count("key", "a", true) == 1, "Long macro should press key a once.");
    AssertTrue(backend.Count("key", "a", false) == 1, "Stop should release key a once.");
    AssertTrue(backend.Events.Count == eventsAfterStop, "Stopped long macro must not send delayed input.");
    AssertTrue(runtime.Snapshot().Presses.IsEmpty, "Stop should leave no held macro inputs.");
}

static async Task MacroActivePointerWaitForegroundLossInterruptsBeforeWaitCompletes()
{
    const double waitMs = 5000;
    const double cleanupLatencyBoundMs = 250;
    var backend = new DryRunInputBackend();
    var gate = new ManualForegroundGate
    {
        IsAllowed = true,
        ForegroundProcess = "BlueArchive.exe"
    };
    var diagnostics = new InMemoryDiagnosticsSink();
    var instructions = CompileMacro($"""
        press mouse_middle
        setpos_rel 18 10
        wait {waitMs.ToString("0", System.Globalization.CultureInfo.InvariantCulture)}
        setpos_rel 12 6
        release mouse_middle
        """);

    await using var runtime = new InProcessRuntimeCore(backend, gate, diagnostics);
    await runtime.LoadAsync(Config(
        new MappingDefinition(
            "macro:q",
            TriggerSpec.Keyboard("q"),
            RuntimeAction.MacroPlan(instructions))),
        CancellationToken.None);
    await runtime.EnableAsync(CancellationToken.None);

    await runtime.HandleTriggerAsync(TriggerEvent.Down(TriggerSpec.Keyboard("q")), CancellationToken.None);
    await WaitForConditionAsync(
        () => diagnostics.Count("macro", "active_pointer_wait_started") == 1 &&
              backend.Count("mouse", "mouse_middle", true) == 1 &&
              CountMoves(backend) >= 1 &&
              backend.Count("mouse", "mouse_middle", false) == 0 &&
              runtime.Snapshot().Presses.OwnerKeys.Count > 0,
        "Active pointer macro should reach the wait barrier with mouse_middle held and at least one move emitted.");

    var waitStarted = diagnostics.Events.Single(evt =>
        string.Equals(evt.Source, "macro", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(evt.Name, "active_pointer_wait_started", StringComparison.OrdinalIgnoreCase));
    var moveEventsAtForegroundLoss = CountMoves(backend);
    var inputEventsAtForegroundLoss = backend.Events.Count;

    gate.IsAllowed = false;
    gate.ForegroundProcess = "Notepad.exe";
    var foregroundLossTicks = Stopwatch.GetTimestamp();

    await WaitForConditionAsync(
        () => diagnostics.Count("macro", "foreground_loss_detected_during_wait") == 1 &&
              diagnostics.Count("macro", "active_pointer_wait_interrupted") == 1 &&
              diagnostics.Count("macro", "foreground_lost_during_active_pointer_sequence") == 1 &&
              diagnostics.Count("macro", "foreground_cleanup_completed") == 1 &&
              backend.Count("mouse", "mouse_middle", false) == 1 &&
              runtime.Snapshot().Presses.IsEmpty,
        "Foreground loss during active pointer wait should interrupt the wait and cleanup held mouse owner.");

    var mouseUpEvents = backend.Events
        .Where(evt =>
            string.Equals(evt.Kind, "mouse", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(evt.Code, "mouse_middle", StringComparison.OrdinalIgnoreCase) &&
            evt.IsDown == false)
        .ToArray();
    var cleanupUp = mouseUpEvents.Single();
    var waitStartToCleanupMs = ElapsedMilliseconds(waitStarted.TimestampTicks, cleanupUp.TimestampTicks);
    var foregroundLossToCleanupMs = ElapsedMilliseconds(foregroundLossTicks, cleanupUp.TimestampTicks);

    AssertTrue(waitStartToCleanupMs < waitMs, "Foreground cleanup must happen before the full active pointer wait duration elapses.");
    AssertTrue(foregroundLossToCleanupMs <= cleanupLatencyBoundMs, "Dry active pointer wait cleanup should satisfy the harness latency bound.");
    AssertTrue(CountMoves(backend) == moveEventsAtForegroundLoss, "Foreground cleanup must prevent post-loss move instructions.");
    AssertTrue(backend.Events.Count == inputEventsAtForegroundLoss + 1, "Foreground cleanup should only add the cleanup mouse up event.");
    AssertTrue(backend.Count("mouse", "mouse_middle", true) == 1, "Active pointer macro should press mouse_middle once.");
    AssertTrue(backend.Count("mouse", "mouse_middle", false) == 1, "Active pointer cleanup must release mouse_middle exactly once.");
    AssertTrue(diagnostics.Count("macro", "wait_completed") == 0, "Interrupted active pointer wait must not be recorded as completed.");

    await runtime.StopAsync(CancellationToken.None);
    AssertTrue(backend.Count("mouse", "mouse_middle", false) == 1, "Stop after foreground cleanup must not emit a duplicate mouse up.");
}

static async Task MacroKeyboardOnlyWaitIgnoresForegroundLossHardening()
{
    var backend = new DryRunInputBackend();
    var gate = new ManualForegroundGate
    {
        IsAllowed = true,
        ForegroundProcess = "BlueArchive.exe"
    };
    var diagnostics = new InMemoryDiagnosticsSink();
    var instructions = CompileMacro("""
        press a
        wait 120
        release a
        """);

    await using var runtime = new InProcessRuntimeCore(backend, gate, diagnostics);
    await runtime.LoadAsync(Config(
        new MappingDefinition(
            "macro:q",
            TriggerSpec.Keyboard("q"),
            RuntimeAction.MacroPlan(instructions))),
        CancellationToken.None);
    await runtime.EnableAsync(CancellationToken.None);

    await runtime.HandleTriggerAsync(TriggerEvent.Down(TriggerSpec.Keyboard("q")), CancellationToken.None);
    await WaitForConditionAsync(
        () => diagnostics.Events.Any(evt =>
            string.Equals(evt.Source, "macro", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(evt.Name, "wait_started", StringComparison.OrdinalIgnoreCase) &&
            evt.Fields.TryGetValue("active_pointer_held", out var activePointerHeld) &&
            string.Equals(activePointerHeld, bool.FalseString, StringComparison.OrdinalIgnoreCase)),
        "Keyboard-only macro should reach its wait barrier without active pointer ownership.");

    gate.IsAllowed = false;
    gate.ForegroundProcess = "Notepad.exe";

    await WaitForConditionAsync(
        () => backend.Count("key", "a", false) == 1,
        "Keyboard-only wait should complete and release even if foreground changes during the wait.");

    AssertTrue(backend.Count("key", "a", true) == 1, "Keyboard-only macro should press key a once.");
    AssertTrue(backend.Count("key", "a", false) == 1, "Keyboard-only macro should release key a once.");
    AssertTrue(diagnostics.Count("macro", "foreground_loss_detected_during_wait") == 0, "Keyboard-only wait must not emit active pointer foreground-loss diagnostics.");
    AssertTrue(diagnostics.Count("macro", "foreground_lost_during_active_pointer_sequence") == 0, "Keyboard-only wait must not be cancelled by active pointer foreground-loss hardening.");
    AssertTrue(diagnostics.Count("macro", "wait_completed") == 1, "Keyboard-only wait should complete normally.");
    AssertTrue(runtime.Snapshot().Presses.IsEmpty, "Keyboard-only macro should leave no held owners.");
}

static async Task MacroActivePointerWaitCancellationIsNotForegroundLoss()
{
    var backend = new DryRunInputBackend();
    var gate = new ManualForegroundGate
    {
        IsAllowed = true,
        ForegroundProcess = "BlueArchive.exe"
    };
    var diagnostics = new InMemoryDiagnosticsSink();
    var instructions = CompileMacro("""
        press mouse_middle
        setpos_rel 18 10
        wait 5000
        setpos_rel 12 6
        release mouse_middle
        """);

    await using var runtime = new InProcessRuntimeCore(backend, gate, diagnostics);
    await runtime.LoadAsync(Config(
        new MappingDefinition(
            "macro:q",
            TriggerSpec.Keyboard("q"),
            RuntimeAction.MacroPlan(instructions))),
        CancellationToken.None);
    await runtime.EnableAsync(CancellationToken.None);

    await runtime.HandleTriggerAsync(TriggerEvent.Down(TriggerSpec.Keyboard("q")), CancellationToken.None);
    await WaitForConditionAsync(
        () => diagnostics.Count("macro", "active_pointer_wait_started") == 1 &&
              backend.Count("mouse", "mouse_middle", true) == 1 &&
              CountMoves(backend) >= 1 &&
              backend.Count("mouse", "mouse_middle", false) == 0,
        "Active pointer macro should reach wait before cancellation probe.");

    await runtime.StopAsync(CancellationToken.None);

    AssertTrue(backend.Count("mouse", "mouse_middle", true) == 1, "Cancellation probe should press mouse_middle once.");
    AssertTrue(backend.Count("mouse", "mouse_middle", false) == 1, "Runtime stop should cleanup mouse_middle exactly once.");
    AssertTrue(CountMoves(backend) == 1, "Runtime stop during wait should prevent post-cancellation moves.");
    AssertTrue(diagnostics.Count("macro", "foreground_loss_detected_during_wait") == 0, "Runtime cancellation must not be reported as foreground loss during wait.");
    AssertTrue(diagnostics.Count("macro", "active_pointer_wait_interrupted") == 0, "Runtime cancellation must not be reported as active pointer foreground interruption.");
    AssertTrue(diagnostics.Count("macro", "foreground_lost_during_active_pointer_sequence") == 0, "Runtime cancellation must not emit foreground-loss sequence diagnostics.");
    AssertTrue(runtime.Snapshot().Presses.IsEmpty, "Runtime stop should leave no held active pointer owners.");
}

static async Task MacroBuiltInDragForegroundLossDuringPreMoveDelaySkipsMoveAndCleansUp()
{
    var backend = new DryRunInputBackend();
    var presses = new PressOwnershipTracker(backend);
    var gate = new ManualForegroundGate { IsAllowed = true };
    var diagnostics = new InMemoryDiagnosticsSink();
    var instructions = CompileMacro("""
        drag 100 200 middle
        setpos_rel 7 7
        """);
    var executor = new MacroExecutor(
        presses,
        backend,
        gate,
        Config(),
        diagnostics,
        pointerDelay: TimeSpan.FromSeconds(1));

    var execution = executor.ExecuteAsync("macro:drag-pre", instructions, CancellationToken.None).AsTask();
    await WaitForConditionAsync(
        () => CountMacroDiagnostics(diagnostics, "builtin_drag_pointer_delay_started", "drag", "pre_move_delay") == 1 &&
              backend.Count("mouse", "mouse_middle", true) == 1 &&
              backend.Count("mouse", "mouse_middle", false) == 0 &&
              CountMoves(backend) == 0 &&
              presses.Snapshot().OwnerKeys.Count > 0,
        "Built-in drag should reach the pre-move active helper barrier with mouse_middle held.");

    gate.IsAllowed = false;
    gate.ForegroundProcess = "Notepad.exe";

    await WaitForConditionAsync(
        () => CountMacroDiagnostics(diagnostics, "builtin_drag_foreground_loss_detected", "drag", "pre_move_delay") == 1 &&
              CountMacroDiagnostics(diagnostics, "builtin_drag_interrupted", "drag", "pre_move_delay", "foreground_loss") == 1 &&
              CountMacroDiagnostics(diagnostics, "builtin_drag_cleanup_completed", "drag", reason: "foreground_loss") == 1 &&
              backend.Count("mouse", "mouse_middle", false) == 1 &&
              presses.Snapshot().IsEmpty,
        "Built-in drag foreground loss during pre-move delay should cleanup the helper drag owner.");

    await execution;

    AssertTrue(CountMoves(backend) == 0, "Built-in drag foreground loss before move must skip helper move and later outer moves.");
    AssertTrue(backend.Count("mouse", "mouse_middle", true) == 1, "Built-in drag should press mouse_middle once.");
    AssertTrue(backend.Count("mouse", "mouse_middle", false) == 1, "Built-in drag cleanup should release mouse_middle exactly once.");
    AssertTrue(CountMacroDiagnostics(diagnostics, "builtin_drag_completed", "drag") == 0, "Interrupted built-in drag must not be recorded as normal completion.");
    AssertTrue(FirstMacroDiagnosticField(diagnostics, "builtin_drag_cleanup_completed", "cleanup_source", "drag") == "helper_drag_owner",
        "Built-in drag cleanup must identify helper drag owner cleanup source.");
    AssertTrue(FirstMacroDiagnosticField(diagnostics, "builtin_drag_cleanup_completed", "drag_owner", "drag") == "macro:drag-pre:drag:mouse_middle",
        "Built-in drag cleanup must report the helper drag owner.");
}

static async Task MacroBuiltInDragRelForegroundLossDuringPreMoveDelaySkipsMoveAndCleansUp()
{
    var backend = new DryRunInputBackend();
    var presses = new PressOwnershipTracker(backend);
    var gate = new ManualForegroundGate { IsAllowed = true };
    var diagnostics = new InMemoryDiagnosticsSink();
    var instructions = CompileMacro("""
        drag_rel 10 5 middle
        setpos_rel 7 7
        """);
    var executor = new MacroExecutor(
        presses,
        backend,
        gate,
        Config(),
        diagnostics,
        pointerDelay: TimeSpan.FromSeconds(5));

    var execution = executor.ExecuteAsync("macro:dragrel-pre", instructions, CancellationToken.None).AsTask();
    await WaitForConditionAsync(
        () => CountMacroDiagnostics(diagnostics, "builtin_drag_pointer_delay_started", "drag_rel", "pre_move_delay") == 1 &&
              backend.Count("mouse", "mouse_middle", true) == 1 &&
              backend.Count("mouse", "mouse_middle", false) == 0 &&
              CountMoves(backend) == 0 &&
              presses.Snapshot().OwnerKeys.Count > 0,
        "Built-in drag_rel should reach the pre-move active helper barrier with mouse_middle held.");

    gate.IsAllowed = false;
    gate.ForegroundProcess = "Notepad.exe";

    await WaitForConditionAsync(
        () => CountMacroDiagnostics(diagnostics, "builtin_drag_foreground_loss_detected", "drag_rel", "pre_move_delay") == 1 &&
              CountMacroDiagnostics(diagnostics, "builtin_drag_cleanup_completed", "drag_rel", reason: "foreground_loss") == 1 &&
              backend.Count("mouse", "mouse_middle", false) == 1 &&
              presses.Snapshot().IsEmpty,
        "Built-in drag_rel foreground loss during pre-move delay should cleanup the helper drag owner.");

    await execution;

    AssertTrue(CountMoves(backend) == 0, "Built-in drag_rel foreground loss before move must skip helper move and later outer moves.");
    AssertTrue(backend.Count("mouse", "mouse_middle", true) == 1, "Built-in drag_rel should press mouse_middle once.");
    AssertTrue(backend.Count("mouse", "mouse_middle", false) == 1, "Built-in drag_rel cleanup should release mouse_middle exactly once.");
    AssertTrue(FirstMacroDiagnosticField(diagnostics, "builtin_drag_cleanup_completed", "drag_owner", "drag_rel") == "macro:dragrel-pre:drag:mouse_middle",
        "Built-in drag_rel cleanup must report the helper drag owner.");
}

static async Task MacroBuiltInDragForegroundLossDuringPostMoveDelayStopsOuterInstructions()
{
    var backend = new DryRunInputBackend();
    var presses = new PressOwnershipTracker(backend);
    var gate = new ManualForegroundGate { IsAllowed = true };
    var diagnostics = new InMemoryDiagnosticsSink();
    var instructions = CompileMacro("""
        drag_rel 10 5 middle
        setpos_rel 7 7
        """);
    var executor = new MacroExecutor(
        presses,
        backend,
        gate,
        Config(),
        diagnostics,
        pointerDelay: TimeSpan.FromSeconds(1));

    var execution = executor.ExecuteAsync("macro:dragrel-post", instructions, CancellationToken.None).AsTask();
    await WaitForConditionAsync(
        () => CountMacroDiagnostics(diagnostics, "builtin_drag_pointer_delay_started", "drag_rel", "post_move_delay") == 1 &&
              backend.Count("mouse", "mouse_middle", true) == 1 &&
              backend.Count("mouse", "mouse_middle", false) == 0 &&
              CountMoves(backend) == 1 &&
              presses.Snapshot().OwnerKeys.Count > 0,
        "Built-in drag_rel should reach the post-move active helper barrier after exactly one helper move.");

    var moveEventsBeforeForegroundLoss = CountMoves(backend);
    gate.IsAllowed = false;
    gate.ForegroundProcess = "Notepad.exe";

    await WaitForConditionAsync(
        () => CountMacroDiagnostics(diagnostics, "builtin_drag_foreground_loss_detected", "drag_rel", "post_move_delay") == 1 &&
              CountMacroDiagnostics(diagnostics, "builtin_drag_cleanup_completed", "drag_rel", reason: "foreground_loss") == 1 &&
              backend.Count("mouse", "mouse_middle", false) == 1 &&
              presses.Snapshot().IsEmpty,
        "Built-in drag_rel foreground loss during post-move delay should cleanup the helper drag owner.");

    await execution;

    AssertTrue(moveEventsBeforeForegroundLoss == 1, "Post-move probe should have one helper move before foreground loss.");
    AssertTrue(CountMoves(backend) == moveEventsBeforeForegroundLoss, "Post-move foreground loss must prevent later outer move instructions.");
    AssertTrue(backend.Count("mouse", "mouse_middle", false) == 1, "Post-move foreground cleanup should release mouse_middle exactly once.");
}

static async Task MacroBuiltInDragCancellationIsNotForegroundLoss()
{
    var backend = new DryRunInputBackend();
    var presses = new PressOwnershipTracker(backend);
    var gate = new ManualForegroundGate { IsAllowed = true };
    var diagnostics = new InMemoryDiagnosticsSink();
    var instructions = CompileMacro("""
        drag_rel 10 5 middle
        setpos_rel 7 7
        """);
    var executor = new MacroExecutor(
        presses,
        backend,
        gate,
        Config(),
        diagnostics,
        pointerDelay: TimeSpan.FromSeconds(5));
    using var cts = new CancellationTokenSource();

    var execution = executor.ExecuteAsync("macro:drag-cancel", instructions, cts.Token).AsTask();
    await WaitForConditionAsync(
        () => CountMacroDiagnostics(diagnostics, "builtin_drag_pointer_delay_started", "drag_rel", "pre_move_delay") == 1 &&
              backend.Count("mouse", "mouse_middle", true) == 1 &&
              backend.Count("mouse", "mouse_middle", false) == 0 &&
              presses.Snapshot().OwnerKeys.Count > 0,
        "Built-in drag_rel should reach pre-move delay before cancellation probe.");

    cts.Cancel();
    var cancelled = false;
    try
    {
        await execution;
    }
    catch (OperationCanceledException)
    {
        cancelled = true;
    }

    AssertTrue(cancelled, "Runtime cancellation should surface as OperationCanceledException.");
    AssertTrue(backend.Count("mouse", "mouse_middle", true) == 1, "Cancellation probe should press mouse_middle once.");
    AssertTrue(backend.Count("mouse", "mouse_middle", false) == 1, "Cancellation cleanup should release mouse_middle exactly once.");
    AssertTrue(CountMoves(backend) == 0, "Cancellation during pre-move delay should prevent helper and outer moves.");
    AssertTrue(CountMacroDiagnostics(diagnostics, "builtin_drag_foreground_loss_detected") == 0, "Cancellation must not emit built-in drag foreground-loss diagnostics.");
    AssertTrue(CountMacroDiagnostics(diagnostics, "builtin_drag_interrupted", reason: "foreground_loss") == 0, "Cancellation must not emit built-in drag foreground-loss interruption diagnostics.");
    AssertTrue(CountMacroDiagnostics(diagnostics, "builtin_drag_cleanup_completed", "drag_rel", reason: "cancellation") == 1, "Cancellation cleanup should be recorded separately.");
    AssertTrue(presses.Snapshot().IsEmpty, "Cancellation cleanup should leave no helper drag owner.");
}

static async Task MacroBuiltInDragAndDragRelCompleteNormallyWithoutForegroundLoss()
{
    var backend = new DryRunInputBackend();
    var presses = new PressOwnershipTracker(backend);
    var gate = new ManualForegroundGate { IsAllowed = true };
    var diagnostics = new InMemoryDiagnosticsSink();
    var instructions = CompileMacro("""
        drag 100 200 middle
        drag_rel 10 5 right
        """);
    var executor = new MacroExecutor(
        presses,
        backend,
        gate,
        Config(),
        diagnostics,
        pointerDelay: TimeSpan.Zero);

    await executor.ExecuteAsync("macro:drag-normal", instructions, CancellationToken.None);

    AssertTrue(backend.Count("mouse", "mouse_middle", true) == 1, "Normal built-in drag should press mouse_middle once.");
    AssertTrue(backend.Count("mouse", "mouse_middle", false) == 1, "Normal built-in drag should release mouse_middle once.");
    AssertTrue(backend.Count("mouse", "mouse_right", true) == 1, "Normal built-in drag_rel should press mouse_right once.");
    AssertTrue(backend.Count("mouse", "mouse_right", false) == 1, "Normal built-in drag_rel should release mouse_right once.");
    AssertTrue(CountMoves(backend) == 2, "Normal built-in drag and drag_rel should each emit one move.");
    AssertTrue(CountMacroDiagnostics(diagnostics, "builtin_drag_completed", "drag") == 1, "Normal built-in drag should record completion.");
    AssertTrue(CountMacroDiagnostics(diagnostics, "builtin_drag_completed", "drag_rel") == 1, "Normal built-in drag_rel should record completion.");
    AssertTrue(CountMacroDiagnostics(diagnostics, "builtin_drag_foreground_loss_detected") == 0, "Normal built-in drag helpers should not emit foreground-loss diagnostics.");
    AssertTrue(presses.Snapshot().IsEmpty, "Normal built-in drag helpers should leave no held owners.");
}

static Task MacroXButton2CompleteDragNormalCompletionLeavesNoResidue()
{
    return MacroXButton2ExplicitDragNormalCompletionLeavesNoResidue(
        "macro:xbutton2-complete-drag",
        [(24, 12)]);
}

static Task MacroXButton2MultisegmentDragNormalCompletionLeavesNoResidue()
{
    return MacroXButton2ExplicitDragNormalCompletionLeavesNoResidue(
        "macro:xbutton2-multisegment-drag",
        [(12, 6), (8, 4), (4, 2)]);
}

static async Task MacroXButton2ExplicitDragNormalCompletionLeavesNoResidue(
    string mappingId,
    (int Dx, int Dy)[] segments)
{
    var backend = new DryRunInputBackend();
    var diagnostics = new InMemoryDiagnosticsSink();
    var instructions = CompileMacro(BuildExplicitCompleteDragScript(segments));
    var expectedMoveDeltaX = segments.Sum(segment => segment.Dx);
    var expectedMoveDeltaY = segments.Sum(segment => segment.Dy);

    await using var runtime = new InProcessRuntimeCore(
        backend,
        new ManualForegroundGate { IsAllowed = true },
        diagnostics);
    await runtime.LoadAsync(Config(
        new MappingDefinition(
            mappingId,
            TriggerSpec.Mouse("mouse_x2"),
            RuntimeAction.MacroPlan(instructions))),
        CancellationToken.None);
    await runtime.EnableAsync(CancellationToken.None);

    var start = await backend.GetMousePositionAsync(CancellationToken.None);
    await runtime.HandleTriggerAsync(TriggerEvent.Down(TriggerSpec.Mouse("mouse_x2")), CancellationToken.None);
    await WaitForConditionAsync(
        () => backend.Count("mouse", "mouse_middle", true) == 1 &&
              backend.Count("mouse", "mouse_middle", false) == 1 &&
              CountMoves(backend) == segments.Length &&
              diagnostics.Count("macro", "finished") == 1 &&
              runtime.Snapshot().PendingActionCount == 0 &&
              runtime.Snapshot().RunningActionCount == 0,
        "XButton2-triggered explicit complete drag should finish normally.");
    await runtime.HandleTriggerAsync(TriggerEvent.Up(TriggerSpec.Mouse("mouse_x2")), CancellationToken.None);

    var end = await backend.GetMousePositionAsync(CancellationToken.None);
    var eventsAtCompletion = backend.Events.Count;
    await Task.Delay(100);

    AssertTrue(backend.Count("mouse", "mouse_middle", true) == 1, "Complete drag should press mouse_middle once.");
    AssertTrue(backend.Count("mouse", "mouse_middle", false) == 1, "Complete drag should release mouse_middle once.");
    AssertTrue(CountMoves(backend) == segments.Length, "Complete drag should emit the expected move segment count.");
    AssertTrue(end.X - start.X == expectedMoveDeltaX, "Complete drag actual X delta should match expected delta.");
    AssertTrue(end.Y - start.Y == expectedMoveDeltaY, "Complete drag actual Y delta should match expected delta.");
    AssertTrue(backend.Events.Count == eventsAtCompletion, "Complete drag should not emit output after completion drain.");
    AssertTrue(runtime.Snapshot().Presses.IsEmpty, "Complete drag should leave no held owners after completion.");

    await runtime.StopAsync(CancellationToken.None);
    var eventsAfterStop = backend.Events.Count;
    await Task.Delay(20);
    AssertTrue(backend.Events.Count == eventsAfterStop, "Stop after complete drag completion must not emit extra output.");
    AssertTrue(runtime.Snapshot().State == RuntimeState.Stopped, "Runtime should stop cleanly after complete drag completion.");

    static string BuildExplicitCompleteDragScript((int Dx, int Dy)[] segments)
    {
        var lines = new List<string> { "press mouse_middle" };
        foreach (var (dx, dy) in segments)
        {
            lines.Add($"setpos_rel {dx} {dy}");
            lines.Add("wait 5");
        }

        lines.Add("release mouse_middle");
        return string.Join(Environment.NewLine, lines);
    }
}

static async Task RuntimeContractForegroundGateBurstBlocksInput()
{
    var backend = new DryRunInputBackend();
    var gate = new ManualForegroundGate
    {
        IsAllowed = false,
        ForegroundProcess = "Notepad.exe"
    };
    await using var runtime = new InProcessRuntimeCore(backend, gate);
    await runtime.LoadAsync(new RuntimeConfig(
        "BlueArchive.exe",
        TapHoldMilliseconds: 1,
        Mappings:
        [
            new MappingDefinition("tap:q", TriggerSpec.Keyboard("q"), RuntimeAction.Tap("1"))
        ]),
        CancellationToken.None);
    await runtime.EnableAsync(CancellationToken.None);

    for (var index = 0; index < 20; index++)
    {
        await runtime.HandleTriggerAsync(TriggerEvent.Down(TriggerSpec.Keyboard("q")), CancellationToken.None);
    }

    AssertTrue(backend.Events.Count == 0, "Blocked foreground burst must not emit input.");
    AssertTrue(runtime.Snapshot().LastForegroundAllowed == false, "Snapshot should record blocked foreground result.");

    gate.IsAllowed = true;
    gate.ForegroundProcess = "BlueArchive.exe";
    await runtime.HandleTriggerAsync(TriggerEvent.Down(TriggerSpec.Keyboard("q")), CancellationToken.None);
    await WaitForInputEventsAsync(backend, 2);

    AssertTrue(backend.Count("key", "1", true) == 1, "Allowed foreground should send tap down.");
    AssertTrue(backend.Count("key", "1", false) == 1, "Allowed foreground should send tap up.");
    AssertTrue(runtime.Snapshot().Presses.IsEmpty, "Foreground burst test should leave no held inputs.");
}

static async Task RuntimeContractDiagnosticsAreEmitted()
{
    var backend = new DryRunInputBackend();
    var diagnostics = new InMemoryDiagnosticsSink();
    await using var runtime = new InProcessRuntimeCore(
        backend,
        AlwaysForegroundGate.Instance,
        diagnostics);
    await runtime.LoadAsync(Config(
        new MappingDefinition("hold:q", TriggerSpec.Keyboard("q"), RuntimeAction.Hold("1"))),
        CancellationToken.None);
    await runtime.EnableAsync(CancellationToken.None);

    await runtime.HandleTriggerAsync(TriggerEvent.Down(TriggerSpec.Keyboard("q")), CancellationToken.None);
    await runtime.DisableAsync(CancellationToken.None);

    AssertTrue(diagnostics.Count("runtime", "config_loaded") == 1, "Diagnostics should include config load.");
    AssertTrue(diagnostics.Count("runtime", "trigger_received") == 1, "Diagnostics should include trigger receipt.");
    AssertTrue(diagnostics.Count("presses", "key_down") == 1, "Diagnostics should include input ownership events.");
}

static Task KeyResolverSupportsProductKeyNames()
{
    AssertTrue(KeyNameResolver.ResolveKeyboardKey("esc").Name == "escape", "esc should normalize to escape.");
    AssertTrue(KeyNameResolver.ResolveKeyboardKey("ctrl").Name == "ctrl", "ctrl should resolve.");
    AssertTrue(KeyNameResolver.ResolveKeyboardKey("alt").Name == "alt", "alt should resolve.");
    AssertTrue(KeyNameResolver.ResolveKeyboardKey("shift").Name == "shift", "shift should resolve.");
    AssertTrue(KeyNameResolver.ResolveKeyboardKey("caps_lock").Name == "caps lock", "caps_lock should resolve.");
    AssertTrue(KeyNameResolver.ResolveKeyboardKey("arrow_left").Name == "arrow left", "arrow_left should resolve.");
    AssertTrue(KeyNameResolver.NormalizeMouseButton("right") == "mouse_right", "Mouse aliases should resolve.");
    return Task.CompletedTask;
}

static Task MouseWheelTriggersAreNotMouseButtons()
{
    foreach (var button in new[] { "mouse_left", "mouse_right", "mouse_middle", "mouse_x1", "mouse_x2" })
    {
        AssertTrue(KeyNameResolver.NormalizeMouseButton(button) == button, $"{button} should remain a valid mouse button.");
    }

    foreach (var wheel in new[] { "mouse_wheel_up", "mouse_wheel_down" })
    {
        var ex = AssertThrows<ArgumentException>(
            () => KeyNameResolver.NormalizeMouseButton(wheel),
            $"{wheel} must not normalize as a mouse button.");
        AssertTrue(ex.Message.Contains("Unsupported mouse button", StringComparison.OrdinalIgnoreCase),
            $"{wheel} should fail with a clear mouse-button error.");

        AssertTrue(KeyNameResolver.NormalizeMouseTrigger(wheel) == wheel,
            $"{wheel} should remain a valid mouse trigger.");

        AssertThrows<ArgumentException>(
            () => AppConfigSerializer.Parse($$"""
                {
                  "mappings": [
                    { "trigger": "q", "type": "simple", "target": "{{wheel}}", "mode": "hold" }
                  ]
                }
                """),
            $"{wheel} must not be accepted as a simple hold target.");

        var pressResult = new MacroScriptCompiler().Compile($"press {wheel}");
        AssertTrue(!pressResult.Success, $"{wheel} must not be accepted as a press/hold macro button.");
        AssertTrue(pressResult.Errors.Any(error => error.Contains("Unsupported mouse button", StringComparison.OrdinalIgnoreCase)),
            $"{wheel} press should report a clear mouse-button error.");

        var dragResult = new MacroScriptCompiler().Compile($"drag_rel 1 2 {wheel}");
        AssertTrue(!dragResult.Success, $"{wheel} must not be accepted as a drag button.");
        AssertTrue(dragResult.Errors.Any(error => error.Contains("Unsupported mouse button", StringComparison.OrdinalIgnoreCase)),
            $"{wheel} drag should report a clear mouse-button error.");
    }

    var triggerConfig = AppConfigSerializer.Parse("""
        {
          "mappings": [
            { "trigger": "mouse_wheel_up", "type": "simple", "target": "mouse_left", "mode": "hold" },
            { "trigger": "mouse_wheel_down", "type": "simple", "target": "mouse_right", "mode": "hold" }
          ]
        }
        """);
    AssertTrue(triggerConfig.Success, string.Join(Environment.NewLine, triggerConfig.Errors));
    AssertTrue(triggerConfig.RuntimeConfig.Mappings[0].Trigger.Key == "mouse:mouse_wheel_up",
        "mouse_wheel_up should remain accepted as a trigger.");
    AssertTrue(triggerConfig.RuntimeConfig.Mappings[1].Trigger.Key == "mouse:mouse_wheel_down",
        "mouse_wheel_down should remain accepted as a trigger.");
    return Task.CompletedTask;
}

static Task MacroScriptCompilerSupportsDslV1()
{
    var compiler = new MacroScriptCompiler();
    var result = compiler.Compile("""
        loop 2
          tap esc
          wait 0.5
          combo ctrl alt delete
        end
        drag_rel 10 -5 right
        """);

    AssertTrue(result.Success, string.Join(Environment.NewLine, result.Errors));
    AssertTrue(result.Instructions.Count == 6, "Compiler should produce loop, 3 body instructions, end, drag_rel.");
    AssertTrue(result.Instructions[0].Op == "loop_start", "loop should compile to loop_start.");
    AssertTrue(result.Instructions[0].Args[1] == "4", "loop_start should point to matching loop_end.");
    AssertTrue(result.Instructions[4].Args[0] == "0", "loop_end should point back to loop_start.");
    AssertTrue(result.Instructions[2].Args[0] == "0.5", "wait should preserve decimal milliseconds.");
    AssertTrue(result.Instructions[5].Args[2] == "mouse_right", "drag_rel should normalize mouse button.");
    return Task.CompletedTask;
}

static Task MacroScriptCompletionMatchesDslSemantics()
{
    var provider = new MacroScriptCompletionProvider();

    var endResult = provider.Complete("en", 2);
    AssertTrue(endResult.Items.First().Text == "end", "Command completion should complete en -> end.");

    var waitResult = provider.Complete("wait ", 5);
    AssertTrue(waitResult.Items.Count > 0, "wait should provide time suggestions.");
    AssertTrue(waitResult.Items.All(item => item.Kind == "time"), "wait suggestions must be time values, not variables or keys.");

    var tapResult = provider.Complete("tap e", 5);
    AssertTrue(tapResult.Items.Any(item => item.Text == "escape"), "tap key suggestions should include escape.");
    AssertTrue(tapResult.Items.All(item => item.Kind is "key" or "mouse"), "tap suggestions should be input names.");

    var capsResult = provider.Complete("tap caps", "tap caps".Length);
    AssertTrue(capsResult.Items.Any(item => item.Text == "caps_lock"), "Completion should use script-safe caps_lock.");
    AssertTrue(capsResult.Items.All(item => !item.Text.Contains(' ')), "Completion must not emit space-containing key tokens.");

    var arrowResult = provider.Complete("tap arrow_", "tap arrow_".Length);
    AssertTrue(arrowResult.Items.Any(item => item.Text == "arrow_left"), "Completion should use script-safe arrow_left.");

    var dragResult = provider.Complete("drag_rel 10 5 r", "drag_rel 10 5 r".Length);
    AssertTrue(dragResult.Items.Any(item => item.Text == "right"), "drag_rel button suggestions should include right alias.");

    return Task.CompletedTask;
}

static Task MacroScriptCompilerSupportsScriptSafeKeyNames()
{
    var result = new MacroScriptCompiler().Compile("""
        tap caps_lock
        tap arrow_left
        """);

    AssertTrue(result.Success, string.Join(Environment.NewLine, result.Errors));
    AssertTrue(result.Instructions[0].Args[0] == "caps lock", "caps_lock should compile to the canonical key name.");
    AssertTrue(result.Instructions[1].Args[0] == "arrow left", "arrow_left should compile to the canonical key name.");
    return Task.CompletedTask;
}

static Task MacroScriptCompilerReportsStructuredDiagnostics()
{
    var result = new MacroScriptCompiler().Compile("""
        tap esc
        wait nope
        end
        """);

    AssertTrue(!result.Success, "Invalid script should fail.");
    AssertTrue(result.Diagnostics.Any(diagnostic => diagnostic.Line == 2),
        "Compiler should report the wait parse error on line 2.");
    AssertTrue(result.Diagnostics.Any(diagnostic => diagnostic.Line == 3),
        "Compiler should report the unmatched end on line 3.");
    return Task.CompletedTask;
}

static Task MacroScriptLanguageServiceClassifiesTokens()
{
    var service = new MacroScriptLanguageService();
    var tokens = service.TokenizeLine("wait nope # bad wait", 7);

    AssertTrue(tokens.Any(token => token.Kind == MacroScriptTokenKind.Command && token.Text == "wait"),
        "Language service should classify command tokens.");
    AssertTrue(tokens.Any(token => token.Kind == MacroScriptTokenKind.Invalid && token.Text == "nope"),
        "Language service should mark invalid wait values.");
    AssertTrue(tokens.Any(token => token.Kind == MacroScriptTokenKind.Comment && token.Line == 7),
        "Language service should preserve comment tokens with line numbers.");

    var keyTokens = service.TokenizeLine("tap caps_lock", 1);
    AssertTrue(keyTokens.Any(token => token.Kind == MacroScriptTokenKind.Key && token.Text == "caps_lock"),
        "Language service should classify script-safe key names.");
    return Task.CompletedTask;
}

static async Task MacroExecutorRunsDslCoreCommands()
{
    var backend = new DryRunInputBackend();
    var presses = new PressOwnershipTracker(backend);
    var compiler = new MacroScriptCompiler();
    var result = compiler.Compile("""
        press a
        release a
        tap esc
        wait 0.1
        combo ctrl alt delete
        setpos 100 200
        setpos_rel 5 -5
        drag 10 20 left
        drag_rel 3 4 right
        loop 2
          tap b
        end
        """);
    AssertTrue(result.Success, string.Join(Environment.NewLine, result.Errors));

    var executor = new MacroExecutor(
        presses,
        backend,
        tapHold: TimeSpan.Zero,
        pointerDelay: TimeSpan.Zero,
        comboKeyGap: TimeSpan.Zero,
        comboHold: TimeSpan.Zero);
    await executor.ExecuteAsync("macro:test", result.Instructions, CancellationToken.None);

    AssertTrue(backend.Count("key", "a", true) == 1, "press a should send key down.");
    AssertTrue(backend.Count("key", "a", false) == 1, "release a should send key up.");
    AssertTrue(backend.Count("key", "escape", true) == 1, "tap esc should press escape.");
    AssertTrue(backend.Count("key", "escape", false) == 1, "tap esc should release escape.");
    AssertTrue(backend.Count("key", "b", true) == 2, "loop 2 should tap b twice.");
    AssertTrue(backend.Count("mouse", "mouse_left", true) == 1, "drag should press left mouse.");
    AssertTrue(backend.Count("mouse", "mouse_right", false) == 1, "drag_rel should release right mouse.");
    AssertTrue(backend.Count("move", "cursor") == 4, "setpos/setpos_rel/drag/drag_rel should move cursor.");
    AssertTrue(presses.Snapshot().IsEmpty, "Macro execution should leave no held inputs.");
}

static async Task RuntimeContractMacroExecutesThroughQueue()
{
    var backend = new DryRunInputBackend();
    var compiler = new MacroScriptCompiler();
    var result = compiler.Compile("""
        loop 2
          tap esc
        end
        """);
    AssertTrue(result.Success, string.Join(Environment.NewLine, result.Errors));

    await using var runtime = new InProcessRuntimeCore(backend);
    await runtime.LoadAsync(Config(
        new MappingDefinition(
            "macro:q",
            TriggerSpec.Keyboard("q"),
            RuntimeAction.MacroPlan(result.Instructions))),
        CancellationToken.None);
    await runtime.EnableAsync(CancellationToken.None);

    await runtime.HandleTriggerAsync(TriggerEvent.Down(TriggerSpec.Keyboard("q")), CancellationToken.None);
    await WaitForInputEventsAsync(backend, 4);

    AssertTrue(backend.Count("key", "escape", true) == 2, "Runtime macro should tap escape twice.");
    AssertTrue(backend.Count("key", "escape", false) == 2, "Runtime macro should release escape twice.");
    AssertTrue(runtime.Snapshot().Presses.IsEmpty, "Runtime macro should leave no held inputs.");
}

static async Task RuntimeSnapshotTracksActionQueueDepth()
{
    var backend = new DryRunInputBackend();
    await using var runtime = new InProcessRuntimeCore(backend);
    await runtime.LoadAsync(new RuntimeConfig(
        "BlueArchive.exe",
        TapHoldMilliseconds: 50,
        Mappings:
        [
            new MappingDefinition("tap:q", TriggerSpec.Keyboard("q"), RuntimeAction.Tap("1"))
        ]),
        CancellationToken.None);
    await runtime.EnableAsync(CancellationToken.None);

    for (var index = 0; index < 6; index++)
    {
        await runtime.HandleTriggerAsync(TriggerEvent.Down(TriggerSpec.Keyboard("q")), CancellationToken.None);
    }

    var sawBusyQueue = false;
    var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(1);
    while (DateTimeOffset.UtcNow < deadline)
    {
        var snapshot = runtime.Snapshot();
        if (snapshot.PendingActionCount > 0 || snapshot.RunningActionCount > 0)
        {
            sawBusyQueue = true;
            break;
        }

        await Task.Delay(1);
    }

    AssertTrue(sawBusyQueue, "Snapshot should expose queued or running tap actions.");
    await WaitForInputEventsAsync(backend, 12);
    var drained = runtime.Snapshot();
    AssertTrue(drained.PendingActionCount == 0, "Pending action count should return to zero.");
    AssertTrue(drained.RunningActionCount == 0, "Running action count should return to zero.");
}

static Task ConfigSerializerLoadsLegacyConfig()
{
    var result = AppConfigSerializer.Parse("""
        {
          "mappings": [
            { "trigger": "q", "type": "simple", "target": "1", "mode": "tap" },
            { "trigger": "mouse_right", "type": "macro", "script": "loop 2\n tap esc\nend" }
          ],
          "hotkey": "f5"
        }
        """);

    AssertTrue(result.Success, string.Join(Environment.NewLine, result.Errors));
    AssertTrue(result.Config.Hotkey == "f5", "Legacy hotkey should be preserved.");
    AssertTrue(result.RuntimeConfig.TargetProcess == "BlueArchive.exe", "Missing target process should default.");
    AssertTrue(result.RuntimeConfig.Mappings.Count == 2, "Legacy mappings should convert to runtime mappings.");
    AssertTrue(result.RuntimeConfig.Mappings[0].Action.NormalizedKind == "tap", "Simple tap should convert.");
    AssertTrue(result.RuntimeConfig.Mappings[1].Trigger.Key == "mouse:mouse_right", "Mouse trigger should convert.");
    AssertTrue(result.RuntimeConfig.Mappings[1].Action.Macro?.Count == 3, "Macro script should compile.");
    return Task.CompletedTask;
}

static Task ConfigSerializerRejectsDuplicateTriggers()
{
    var result = AppConfigSerializer.Parse("""
        {
          "mappings": [
            { "trigger": "q", "type": "simple", "target": "1", "mode": "hold" },
            { "trigger": "q", "type": "simple", "target": "2", "mode": "hold" }
          ]
        }
        """);

    AssertTrue(!result.Success, "Duplicate triggers should be rejected.");
    AssertTrue(result.Errors.Any(error => error.Contains("触发键重复", StringComparison.OrdinalIgnoreCase)),
        "Duplicate trigger error should be reported.");
    return Task.CompletedTask;
}

static Task ConfigSerializerRejectsControlHotkeyTriggerConflicts()
{
    AssertHotkeyConflict("f5", "f5", "f5");
    AssertHotkeyConflict("ctrl+shift+f12", "ctrl", "ctrl");
    AssertHotkeyConflict("ctrl+shift+f12", "shift", "shift");
    AssertHotkeyConflict("ctrl+shift+f12", "f12", "f12");
    return Task.CompletedTask;
}

static Task ConfigSerializerAllowsNonConflictingControlHotkeyTriggers()
{
    var single = ParseConfigWithHotkeyAndTrigger("f5", "esc");
    AssertTrue(single.Success, string.Join(Environment.NewLine, single.Errors));

    var combo = ParseConfigWithHotkeyAndTrigger("ctrl+shift+f12", "q");
    AssertTrue(combo.Success, string.Join(Environment.NewLine, combo.Errors));
    return Task.CompletedTask;
}

static Task ConfigSerializerRejectsControlHotkeyAliasConflicts()
{
    AssertHotkeyConflict("escape", "esc", "escape");
    AssertHotkeyConflict("page_up", "page up", "page up", expectedHotkeyDisplay: "page up");
    AssertHotkeyConflict("ctrl+f12", "ctrl_l", "ctrl");
    return Task.CompletedTask;
}

static async Task RuntimeHostReloadRejectsControlHotkeyConflictKeepsOldConfig()
{
    await using var host = new RuntimeHost(
        new AppConfigV1
        {
            Hotkey = "f5",
            Mappings =
            [
                new MappingConfigV1
                {
                    Trigger = "q",
                    Type = "simple",
                    Target = "1",
                    Mode = "tap"
                }
            ]
        },
        new DryRunInputBackend(),
        AlwaysForegroundGate.Instance,
        new ManualTriggerSource());
    await host.StartAsync(CancellationToken.None);

    var oldTrigger = host.RuntimeConfig.Mappings.Single().Trigger.Key;
    var oldHotkey = host.AppConfig.Hotkey;
    var threw = false;
    try
    {
        await host.ReloadAsync(
            new AppConfigV1
            {
                Hotkey = "f5",
                Mappings =
                [
                    new MappingConfigV1
                    {
                        Trigger = "f5",
                        Type = "simple",
                        Target = "2",
                        Mode = "tap"
                    }
                ]
            },
            CancellationToken.None);
    }
    catch (InvalidOperationException ex)
    {
        threw = ex.Message.Contains("f5", StringComparison.OrdinalIgnoreCase) &&
            ex.Message.Contains("控制热键", StringComparison.OrdinalIgnoreCase);
    }

    AssertTrue(threw, "Reload should reject control hotkey / trigger conflict.");
    AssertTrue(host.AppConfig.Hotkey == oldHotkey, "Rejected reload should keep old app config.");
    AssertTrue(host.RuntimeConfig.Mappings.Single().Trigger.Key == oldTrigger,
        "Rejected reload should keep old runtime config.");
    await host.StopAsync(CancellationToken.None);
}

static void AssertHotkeyConflict(
    string hotkey,
    string trigger,
    string conflictKey,
    string? expectedHotkeyDisplay = null)
{
    var result = ParseConfigWithHotkeyAndTrigger(hotkey, trigger);
    AssertTrue(!result.Success, $"{hotkey} / {trigger} should be rejected.");
    var hotkeyDisplay = expectedHotkeyDisplay ?? hotkey;
    AssertTrue(result.Errors.Any(error =>
            error.Contains(conflictKey, StringComparison.OrdinalIgnoreCase) &&
            error.Contains(hotkeyDisplay, StringComparison.OrdinalIgnoreCase) &&
            error.Contains("控制热键", StringComparison.OrdinalIgnoreCase)),
        $"Expected conflict error for {hotkey} / {trigger}. Actual: {string.Join(Environment.NewLine, result.Errors)}");
}

static ConfigLoadResult ParseConfigWithHotkeyAndTrigger(string hotkey, string trigger)
{
    return AppConfigSerializer.Parse($$"""
        {
          "hotkey": "{{hotkey}}",
          "mappings": [
            { "trigger": "{{trigger}}", "type": "simple", "target": "1", "mode": "tap" }
          ]
        }
        """);
}

static async Task TriggerPipelineDeliversManualTriggerToRuntime()
{
    var backend = new DryRunInputBackend();
    var source = new ManualTriggerSource();
    await using var runtime = new InProcessRuntimeCore(backend);
    await runtime.LoadAsync(Config(
        new MappingDefinition("hold:q", TriggerSpec.Keyboard("q"), RuntimeAction.Hold("1"))),
        CancellationToken.None);
    await runtime.EnableAsync(CancellationToken.None);

    await using var pipeline = new TriggerPipeline(source, runtime);
    await pipeline.StartAsync(CancellationToken.None);
    source.KeyDown("q");
    await WaitForInputEventsAsync(backend, 1);
    source.KeyUp("q");
    await WaitForInputEventsAsync(backend, 2);
    await pipeline.StopAsync(CancellationToken.None);

    AssertTrue(backend.Count("key", "1", true) == 1, "Trigger pipeline should deliver key down.");
    AssertTrue(backend.Count("key", "1", false) == 1, "Trigger pipeline should deliver key up.");
    AssertTrue(runtime.Snapshot().Presses.IsEmpty, "Trigger pipeline should leave no held input.");
}

static async Task TriggerPipelineRestartDoesNotReplayStoppedEvents()
{
    var backend = new DryRunInputBackend();
    var source = new ManualTriggerSource();
    await using var runtime = new InProcessRuntimeCore(backend);
    await runtime.LoadAsync(Config(
        new MappingDefinition("hold:q", TriggerSpec.Keyboard("q"), RuntimeAction.Hold("1"))),
        CancellationToken.None);
    await runtime.EnableAsync(CancellationToken.None);

    await using var pipeline = new TriggerPipeline(source, runtime);
    await pipeline.StartAsync(CancellationToken.None);
    await pipeline.StopAsync(CancellationToken.None);

    source.KeyDown("q");
    await Task.Delay(20);
    AssertTrue(backend.Events.Count == 0, "Stopped pipeline must not queue trigger events.");

    await pipeline.StartAsync(CancellationToken.None);
    source.KeyDown("q");
    await WaitForInputEventsAsync(backend, 1);
    source.KeyUp("q");
    await WaitForInputEventsAsync(backend, 2);

    AssertTrue(backend.Count("key", "1", true) == 1, "Restarted pipeline should process new events.");
    AssertTrue(backend.Count("key", "1", false) == 1, "Restarted pipeline should release new events.");
}

static Task HookSelfForegroundGuardBlocksNewSelfTargetCapture()
{
    var captured = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var policy = TriggerCapturePolicySnapshot.FromRuntimeConfig(
        "BAKeySmith.App.exe",
        ["keyboard:q"],
        isEnabled: true);
    var foregroundAllowed = WindowsHookTriggerSource.IsForegroundAllowedForCapture(
        "BAKeySmith.App.exe",
        "bakeysmith.app",
        ["BAKeySmith.App"]);

    var down = WindowsHookTriggerSource.EvaluateCaptureDecision(
        TriggerEvent.Down(TriggerSpec.Keyboard("q")),
        policy,
        createsCapturedSession: true,
        foregroundAllowed,
        captured);

    AssertTrue(!foregroundAllowed, "Configured self foreground must be blocked before hook capture.");
    AssertTrue(!down.Suppress && !down.Dispatch, "Self target + self foreground must pass through a new trigger down.");
    AssertTrue(captured.Count == 0, "Self foreground guard must not create a new captured session.");
    return Task.CompletedTask;
}

static Task HookSelfForegroundGuardPreservesNormalTargetCapture()
{
    var captured = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var policy = TriggerCapturePolicySnapshot.FromRuntimeConfig(
        "BlueArchive.exe",
        ["keyboard:q"],
        isEnabled: true);
    var foregroundAllowed = WindowsHookTriggerSource.IsForegroundAllowedForCapture(
        "BlueArchive.exe",
        "bluearchive",
        ["BAKeySmith.App"]);

    var down = WindowsHookTriggerSource.EvaluateCaptureDecision(
        TriggerEvent.Down(TriggerSpec.Keyboard("q")),
        policy,
        createsCapturedSession: true,
        foregroundAllowed,
        captured);

    AssertTrue(foregroundAllowed, "Normal target foreground should still be allowed.");
    AssertTrue(down.Suppress && down.Dispatch, "Allowed target foreground should still suppress and dispatch trigger down.");
    AssertTrue(captured.Count == 1, "Allowed target foreground should create a captured session.");
    return Task.CompletedTask;
}

static Task HookSelfForegroundGuardLeavesNonTargetForegroundPassThrough()
{
    var captured = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var policy = TriggerCapturePolicySnapshot.FromRuntimeConfig(
        "BlueArchive.exe",
        ["keyboard:q"],
        isEnabled: true);
    var selfForegroundAllowed = WindowsHookTriggerSource.IsForegroundAllowedForCapture(
        "BlueArchive.exe",
        "BAKeySmith.App",
        ["BAKeySmith.App"]);
    var nonTargetForegroundAllowed = WindowsHookTriggerSource.IsForegroundAllowedForCapture(
        "BlueArchive.exe",
        "Notepad.exe",
        ["BAKeySmith.App"]);

    var selfForegroundDown = WindowsHookTriggerSource.EvaluateCaptureDecision(
        TriggerEvent.Down(TriggerSpec.Keyboard("q")),
        policy,
        createsCapturedSession: true,
        foregroundAllowed: selfForegroundAllowed,
        captured);
    var nonTargetDown = WindowsHookTriggerSource.EvaluateCaptureDecision(
        TriggerEvent.Down(TriggerSpec.Keyboard("q")),
        policy,
        createsCapturedSession: true,
        foregroundAllowed: nonTargetForegroundAllowed,
        captured);

    AssertTrue(!selfForegroundAllowed, "Self foreground should be blocked for a normal target mismatch.");
    AssertTrue(!nonTargetForegroundAllowed, "Non-target foreground should remain blocked.");
    AssertTrue(!selfForegroundDown.Suppress && !selfForegroundDown.Dispatch,
        "Self foreground with normal target mismatch should pass through.");
    AssertTrue(!nonTargetDown.Suppress && !nonTargetDown.Dispatch,
        "Non-target foreground should pass through.");
    AssertTrue(captured.Count == 0, "Blocked foreground paths must not create captured sessions.");
    return Task.CompletedTask;
}

static Task HookDefaultDoesNotBlockCurrentProcessWithoutExplicitList()
{
    var selfAllowedByDefault = WindowsHookTriggerSource.IsForegroundAllowedForCapture(
        "BAKeySmith.App",
        "BAKeySmith.App",
        blockedForegroundProcessNames: null);
    var similarNameAllowed = WindowsHookTriggerSource.IsForegroundAllowedForCapture(
        "MyBAKeySmith.AppHelper.exe",
        "MyBAKeySmith.AppHelper.exe",
        ["BAKeySmith.App"]);

    AssertTrue(selfAllowedByDefault, "Core hook default must not block current/self process without an explicit list.");
    AssertTrue(similarNameAllowed, "Blocked process matching must be exact, not contains or suffix based.");
    return Task.CompletedTask;
}

static Task HookSelfForegroundGuardPreservesCapturedRelease()
{
    var captured = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "mouse:mouse_x2"
    };
    var policy = TriggerCapturePolicySnapshot.FromRuntimeConfig(
        "BlueArchive.exe",
        ["mouse:mouse_x2"],
        isEnabled: true);
    var foregroundAllowed = WindowsHookTriggerSource.IsForegroundAllowedForCapture(
        "BlueArchive.exe",
        "BAKeySmith.App",
        ["BAKeySmith.App"]);

    var release = WindowsHookTriggerSource.EvaluateCaptureDecision(
        TriggerEvent.Up(TriggerSpec.Mouse("mouse_x2")),
        policy,
        createsCapturedSession: true,
        foregroundAllowed,
        captured);

    AssertTrue(!foregroundAllowed, "Self foreground should be blocked for new capture checks.");
    AssertTrue(release.Suppress && release.Dispatch,
        "Existing captured session release must still suppress and dispatch under self foreground.");
    AssertTrue(captured.Count == 0, "Captured release under self foreground should clear the session.");
    return Task.CompletedTask;
}

static Task CapturedRepeatDownDoesNotRedispatch()
{
    var captured = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var policy = TriggerCapturePolicySnapshot.FromRuntimeConfig(
        "BlueArchive.exe",
        ["keyboard:f13"],
        isEnabled: true);

    var firstDown = WindowsHookTriggerSource.EvaluateCaptureDecision(
        TriggerEvent.Down(TriggerSpec.Keyboard("f13")),
        policy,
        createsCapturedSession: true,
        foregroundAllowed: true,
        captured);
    var repeatDown = WindowsHookTriggerSource.EvaluateCaptureDecision(
        TriggerEvent.Down(TriggerSpec.Keyboard("f13")),
        policy,
        createsCapturedSession: true,
        foregroundAllowed: true,
        captured);
    var release = WindowsHookTriggerSource.EvaluateCaptureDecision(
        TriggerEvent.Up(TriggerSpec.Keyboard("f13")),
        policy,
        createsCapturedSession: true,
        foregroundAllowed: false,
        captured);

    AssertTrue(firstDown.Suppress && firstDown.Dispatch, "First captured down should suppress and dispatch.");
    AssertTrue(repeatDown.Suppress && !repeatDown.Dispatch, "Captured repeat down must suppress without redispatch.");
    AssertTrue(release.Suppress && release.Dispatch, "Captured release should suppress and dispatch exactly once.");
    AssertTrue(captured.Count == 0, "Captured session should be cleared after matching up.");
    return Task.CompletedTask;
}

static Task CapturedMouseHoldReleaseMatchesSessionAfterForegroundChange()
{
    var captured = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var policy = TriggerCapturePolicySnapshot.FromRuntimeConfig(
        "BlueArchive.exe",
        ["mouse:mouse_x1"],
        isEnabled: true);

    var down = WindowsHookTriggerSource.EvaluateCaptureDecision(
        TriggerEvent.Down(TriggerSpec.Mouse("mouse_x1")),
        policy,
        createsCapturedSession: true,
        foregroundAllowed: true,
        captured);
    var releaseAfterForegroundChange = WindowsHookTriggerSource.EvaluateCaptureDecision(
        TriggerEvent.Up(TriggerSpec.Mouse("mouse_x1")),
        policy,
        createsCapturedSession: true,
        foregroundAllowed: false,
        captured);

    AssertTrue(down.Suppress && down.Dispatch, "Allowed xbutton hold down should suppress and dispatch.");
    AssertTrue(releaseAfterForegroundChange.Suppress && releaseAfterForegroundChange.Dispatch,
        "Release for a captured xbutton session should still suppress and dispatch after foreground changes.");
    AssertTrue(captured.Count == 0, "Matched xbutton release should clear the captured session.");
    return Task.CompletedTask;
}

static Task BlockedMouseHoldDoesNotRetroactivelyCaptureOnForegroundReturn()
{
    var captured = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var policy = TriggerCapturePolicySnapshot.FromRuntimeConfig(
        "BlueArchive.exe",
        ["mouse:mouse_x1"],
        isEnabled: true);

    var blockedDown = WindowsHookTriggerSource.EvaluateCaptureDecision(
        TriggerEvent.Down(TriggerSpec.Mouse("mouse_x1")),
        policy,
        createsCapturedSession: true,
        foregroundAllowed: false,
        captured);
    var releaseAfterForegroundReturn = WindowsHookTriggerSource.EvaluateCaptureDecision(
        TriggerEvent.Up(TriggerSpec.Mouse("mouse_x1")),
        policy,
        createsCapturedSession: true,
        foregroundAllowed: true,
        captured);

    AssertTrue(!blockedDown.Suppress && !blockedDown.Dispatch,
        "Blocked xbutton hold down must remain pass-through and must not dispatch.");
    AssertTrue(!releaseAfterForegroundReturn.Suppress && !releaseAfterForegroundReturn.Dispatch,
        "Returning to the target foreground must not retroactively capture or dispatch the held xbutton release.");
    AssertTrue(captured.Count == 0, "Blocked xbutton hold must leave no captured session behind.");
    return Task.CompletedTask;
}

static async Task TriggerPipelineStartFailureRollsBackState()
{
    var backend = new DryRunInputBackend();
    var source = new FailingTriggerSource();
    await using var runtime = new InProcessRuntimeCore(backend);
    await runtime.LoadAsync(Config(
        new MappingDefinition("tap:q", TriggerSpec.Keyboard("q"), RuntimeAction.Tap("1"))),
        CancellationToken.None);
    await runtime.EnableAsync(CancellationToken.None);

    await using var pipeline = new TriggerPipeline(source, runtime);
    try
    {
        await pipeline.StartAsync(CancellationToken.None);
        throw new InvalidOperationException("TriggerPipeline.StartAsync should fail when the source fails to start.");
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("simulated trigger source start failure", StringComparison.Ordinal))
    {
    }

    var snapshot = pipeline.Snapshot();
    AssertTrue(!pipeline.IsRunning, "Failed pipeline start must not leave pipeline running.");
    AssertTrue(!source.IsRunning, "Failed pipeline start must stop the partially started source.");
    AssertTrue(source.StopCalls == 1, "Failed pipeline start should roll back the source once.");
    AssertTrue(snapshot.PendingCount == 0, "Failed pipeline start must not leave pending triggers.");
    AssertTrue(snapshot.QueuedCount == 0 && snapshot.HandledCount == 0 && snapshot.DroppedCount == 0,
        "Failed pipeline start must not mutate trigger counters.");

    source.Emit(TriggerEvent.Down(TriggerSpec.Keyboard("q")));
    await Task.Delay(20);
    AssertTrue(backend.Events.Count == 0, "Failed pipeline start must not leave a subscribed worker behind.");
}

static async Task RuntimeHostStartFailureRollsBackRuntimeAndCaptureState()
{
    var backend = new DryRunInputBackend();
    var source = new FailingTriggerSource();
    var config = new AppConfigV1
    {
        TargetProcess = "BlueArchive.exe",
        Mappings =
        [
            new MappingConfigV1
            {
                Trigger = "q",
                Type = "simple",
                Target = "1",
                Mode = "tap"
            }
        ]
    };

    await using var host = new RuntimeHost(
        config,
        backend,
        AlwaysForegroundGate.Instance,
        source);

    try
    {
        await host.StartAsync(CancellationToken.None);
        throw new InvalidOperationException("RuntimeHost.StartAsync should fail when the trigger source fails to start.");
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("simulated trigger source start failure", StringComparison.Ordinal))
    {
    }

    var snapshot = host.Snapshot();
    AssertTrue(!snapshot.IsStarted, "Failed host start must not mark the host as started.");
    AssertTrue(!snapshot.IsPipelineRunning, "Failed host start must not leave the pipeline running.");
    AssertTrue(snapshot.Runtime.State == RuntimeState.Stopped, "Failed host start must roll runtime state back to stopped.");
    AssertTrue(source.StopCalls == 1, "Failed host start should roll back the partially started source.");
    AssertTrue(source.CapturePolicies.Count >= 2, "Failed host start should apply and then clear capture policy.");
    AssertTrue(source.CapturePolicies.Any(policy => policy.IsEnabled), "Failed host start should have attempted to enable capture.");
    AssertTrue(!source.CapturePolicies[^1].IsEnabled, "Failed host start must leave capture policy disabled.");

    source.Emit(TriggerEvent.Down(TriggerSpec.Keyboard("q")));
    await Task.Delay(20);
    AssertTrue(backend.Events.Count == 0, "Failed host start must not route triggers after rollback.");
}

static async Task RuntimeHostComposesConfigRuntimeAndPipeline()
{
    var backend = new DryRunInputBackend();
    var source = new ManualTriggerSource();
    var config = new AppConfigV1
    {
        Mappings =
        [
            new MappingConfigV1
            {
                Trigger = "q",
                Type = "simple",
                Target = "1",
                Mode = "tap"
            }
        ]
    };

    await using var host = new RuntimeHost(
        config,
        backend,
        AlwaysForegroundGate.Instance,
        source);
    await host.StartAsync(CancellationToken.None);

    source.KeyDown("q");
    await WaitForInputEventsAsync(backend, 2);
    var routed = host.Snapshot();
    await host.StopAsync(CancellationToken.None);

    AssertTrue(host.RuntimeConfig.Mappings.Count == 1, "Host should compile config mappings.");
    AssertTrue(routed.Pipeline.QueuedCount == 1, "Host snapshot should expose queued trigger count.");
    AssertTrue(routed.Pipeline.HandledCount == 1, "Host snapshot should expose handled trigger count.");
    AssertTrue(backend.Count("key", "1", true) == 1, "Host should route trigger to runtime.");
    AssertTrue(backend.Count("key", "1", false) == 1, "Host should complete tap action.");
    AssertTrue(host.Runtime.Snapshot().Presses.IsEmpty, "Host stop should leave no held inputs.");
}

static async Task RuntimeHostSnapshotExposesLifecycleState()
{
    var backend = new DryRunInputBackend();
    var source = new ManualTriggerSource();
    var config = new AppConfigV1
    {
        Mappings =
        [
            new MappingConfigV1
            {
                Trigger = "q",
                Type = "simple",
                Target = "1",
                Mode = "hold"
            }
        ]
    };

    await using var host = new RuntimeHost(
        config,
        backend,
        AlwaysForegroundGate.Instance,
        source);

    var initial = host.Snapshot();
    AssertTrue(!initial.IsStarted, "Initial host snapshot should report stopped.");
    AssertTrue(!initial.IsPipelineRunning, "Initial host snapshot should report stopped pipeline.");
    AssertTrue(initial.Pipeline.PendingCount == 0, "Initial host snapshot should report no pending triggers.");
    AssertTrue(initial.Runtime.State == RuntimeState.Stopped, "Initial runtime snapshot should report stopped.");

    await host.StartAsync(CancellationToken.None);
    var started = host.Snapshot();
    AssertTrue(started.IsStarted, "Started host snapshot should report started.");
    AssertTrue(started.IsPipelineRunning, "Started host snapshot should report running pipeline.");
    AssertTrue(started.Pipeline.PendingCount == 0, "Started host snapshot should expose pending trigger count.");
    AssertTrue(started.Runtime.State == RuntimeState.Enabled, "Started runtime snapshot should report enabled.");
    AssertTrue(started.Runtime.MappingCount == 1, "Snapshot should expose mapping count.");
    AssertTrue(started.Runtime.PendingActionCount == 0, "Snapshot should expose pending action count.");
    AssertTrue(started.Runtime.RunningActionCount == 0, "Snapshot should expose running action count.");

    await host.StopAsync(CancellationToken.None);
    var stopped = host.Snapshot();
    AssertTrue(!stopped.IsStarted, "Stopped host snapshot should report stopped.");
    AssertTrue(!stopped.IsPipelineRunning, "Stopped host snapshot should report stopped pipeline.");
    AssertTrue(stopped.Pipeline.PendingCount == 0, "Stopped host snapshot should report no pending triggers.");
    AssertTrue(stopped.Runtime.State == RuntimeState.Stopped, "Stopped runtime snapshot should report stopped.");
}

static Task RuntimeHostRejectsInvalidConfig()
{
    var backend = new DryRunInputBackend();
    var source = new ManualTriggerSource();
    var config = new AppConfigV1
    {
        Mappings =
        [
            new MappingConfigV1
            {
                Trigger = "q",
                Type = "simple",
                Mode = "tap"
            }
        ]
    };

    try
    {
        _ = new RuntimeHost(config, backend, AlwaysForegroundGate.Instance, source);
    }
    catch (InvalidOperationException)
    {
        return Task.CompletedTask;
    }

    throw new InvalidOperationException("RuntimeHost should reject invalid config.");
}

var tests = new (string Name, Func<Task> Test)[]
{
    ("owner refcount keeps key down until final release", OwnerRefCountKeepsKeyDownUntilLastOwnerReleases),
    ("release owner only releases owned keys", ReleaseOwnerOnlyReleasesOwnedKeys),
    ("press ownership rolls back failed down without cleanup up", PressOwnershipRollsBackFailedDownWithoutCleanupUp),
    ("tap uses ownership tracker", TapUsesOwnershipTracker),
    ("runtime session stop cancels workers and releases held keys", RuntimeSessionStopCancelsWorkersAndReleasesHeldKeys),
    ("runtime contract hold respects foreground gate", RuntimeContractHoldRespectsForegroundGate),
    ("runtime contract tap uses unified tap semantics", RuntimeContractTapUsesUnifiedTapSemantics),
    ("runtime contract reload stops old mapping", RuntimeContractReloadStopsOldMapping),
    ("runtime contract reload during burst drops old queue cleanly", RuntimeContractReloadDuringBurstDropsOldQueueCleanly),
    ("runtime contract reload during long macro releases old owner", RuntimeContractReloadDuringLongMacroReleasesOldOwner),
    ("runtime contract disable cancels delayed tap", RuntimeContractDisableCancelsDelayedTap),
    ("runtime contract stop during long macro releases held input", RuntimeContractStopDuringLongMacroReleasesHeldInput),
    ("macro active pointer wait foreground loss interrupts before wait completes", MacroActivePointerWaitForegroundLossInterruptsBeforeWaitCompletes),
    ("macro keyboard-only wait ignores foreground-loss hardening", MacroKeyboardOnlyWaitIgnoresForegroundLossHardening),
    ("macro active pointer wait cancellation is not foreground loss", MacroActivePointerWaitCancellationIsNotForegroundLoss),
    ("macro built-in drag foreground loss during pre-move delay skips move and cleans up", MacroBuiltInDragForegroundLossDuringPreMoveDelaySkipsMoveAndCleansUp),
    ("macro built-in drag_rel foreground loss during pre-move delay skips move and cleans up", MacroBuiltInDragRelForegroundLossDuringPreMoveDelaySkipsMoveAndCleansUp),
    ("macro built-in drag foreground loss during post-move delay stops outer instructions", MacroBuiltInDragForegroundLossDuringPostMoveDelayStopsOuterInstructions),
    ("macro built-in drag cancellation is not foreground loss", MacroBuiltInDragCancellationIsNotForegroundLoss),
    ("macro built-in drag and drag_rel complete normally without foreground loss", MacroBuiltInDragAndDragRelCompleteNormallyWithoutForegroundLoss),
    ("macro xbutton2 complete drag normal completion leaves no residue", MacroXButton2CompleteDragNormalCompletionLeavesNoResidue),
    ("macro xbutton2 multisegment drag normal completion leaves no residue", MacroXButton2MultisegmentDragNormalCompletionLeavesNoResidue),
    ("runtime contract foreground gate burst blocks input", RuntimeContractForegroundGateBurstBlocksInput),
    ("runtime contract diagnostics are emitted", RuntimeContractDiagnosticsAreEmitted),
    ("key resolver supports product key names", KeyResolverSupportsProductKeyNames),
    ("mouse wheel triggers are not mouse buttons", MouseWheelTriggersAreNotMouseButtons),
    ("macro script compiler supports DSL v1", MacroScriptCompilerSupportsDslV1),
    ("macro script completion matches DSL semantics", MacroScriptCompletionMatchesDslSemantics),
    ("macro script compiler supports script-safe key names", MacroScriptCompilerSupportsScriptSafeKeyNames),
    ("macro script compiler reports structured diagnostics", MacroScriptCompilerReportsStructuredDiagnostics),
    ("macro script language service classifies tokens", MacroScriptLanguageServiceClassifiesTokens),
    ("macro executor runs DSL core commands", MacroExecutorRunsDslCoreCommands),
    ("runtime contract macro executes through queue", RuntimeContractMacroExecutesThroughQueue),
    ("runtime snapshot tracks action queue depth", RuntimeSnapshotTracksActionQueueDepth),
    ("config serializer loads legacy config", ConfigSerializerLoadsLegacyConfig),
    ("config serializer rejects duplicate triggers", ConfigSerializerRejectsDuplicateTriggers),
    ("config serializer rejects control hotkey trigger conflicts", ConfigSerializerRejectsControlHotkeyTriggerConflicts),
    ("config serializer allows non-conflicting control hotkey triggers", ConfigSerializerAllowsNonConflictingControlHotkeyTriggers),
    ("config serializer rejects control hotkey alias conflicts", ConfigSerializerRejectsControlHotkeyAliasConflicts),
    ("runtime host reload rejects control hotkey conflict keeps old config", RuntimeHostReloadRejectsControlHotkeyConflictKeepsOldConfig),
    ("trigger pipeline delivers manual trigger to runtime", TriggerPipelineDeliversManualTriggerToRuntime),
    ("trigger pipeline restart does not replay stopped events", TriggerPipelineRestartDoesNotReplayStoppedEvents),
    ("hook self foreground guard blocks new self target capture", HookSelfForegroundGuardBlocksNewSelfTargetCapture),
    ("hook self foreground guard preserves normal target capture", HookSelfForegroundGuardPreservesNormalTargetCapture),
    ("hook self foreground guard leaves non-target foreground pass-through", HookSelfForegroundGuardLeavesNonTargetForegroundPassThrough),
    ("hook default does not block current process without explicit list", HookDefaultDoesNotBlockCurrentProcessWithoutExplicitList),
    ("hook self foreground guard preserves captured release", HookSelfForegroundGuardPreservesCapturedRelease),
    ("captured repeat down does not redispatch", CapturedRepeatDownDoesNotRedispatch),
    ("captured mouse hold release matches session after foreground change", CapturedMouseHoldReleaseMatchesSessionAfterForegroundChange),
    ("blocked mouse hold does not retroactively capture on foreground return", BlockedMouseHoldDoesNotRetroactivelyCaptureOnForegroundReturn),
    ("trigger pipeline start failure rolls back state", TriggerPipelineStartFailureRollsBackState),
    ("runtime host start failure rolls back runtime and capture state", RuntimeHostStartFailureRollsBackRuntimeAndCaptureState),
    ("runtime host composes config runtime and pipeline", RuntimeHostComposesConfigRuntimeAndPipeline),
    ("runtime host snapshot exposes lifecycle state", RuntimeHostSnapshotExposesLifecycleState),
    ("runtime host rejects invalid config", RuntimeHostRejectsInvalidConfig),
};

foreach (var (name, test) in tests)
{
    await test();
    Console.WriteLine($"PASS {name}");
}

internal sealed class FailingTriggerSource : ITriggerSource, ITriggerCapturePolicySink
{
    public event EventHandler<TriggerEvent>? Triggered;

    public bool IsRunning { get; private set; }
    public int StopCalls { get; private set; }
    public List<TriggerCapturePolicySnapshot> CapturePolicies { get; } = [];

    public ValueTask StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IsRunning = true;
        throw new InvalidOperationException("simulated trigger source start failure");
    }

    public ValueTask StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StopCalls++;
        IsRunning = false;
        return ValueTask.CompletedTask;
    }

    public void UpdateCapturePolicy(TriggerCapturePolicySnapshot policy)
    {
        CapturePolicies.Add(policy);
    }

    public void Emit(TriggerEvent triggerEvent)
    {
        Triggered?.Invoke(this, triggerEvent);
    }

    public ValueTask DisposeAsync()
    {
        IsRunning = false;
        return ValueTask.CompletedTask;
    }
}

internal sealed class ThrowingDownInputBackend : IInputBackend
{
    private readonly string _failCode;

    public ThrowingDownInputBackend(string failCode)
    {
        _failCode = failCode.Trim().ToLowerInvariant();
    }

    public int KeyUpCalls { get; private set; }
    public int MouseUpCalls { get; private set; }

    public async ValueTask SendAsync(IReadOnlyList<InputCommand> commands, CancellationToken cancellationToken)
    {
        foreach (var command in commands)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (command.Kind.Trim().ToLowerInvariant())
            {
                case "key":
                    if (command.IsDown == true)
                    {
                        await KeyDownAsync(command.Code, cancellationToken);
                    }
                    else
                    {
                        await KeyUpAsync(command.Code, cancellationToken);
                    }

                    break;
                case "mouse":
                    if (command.IsDown == true)
                    {
                        await MouseDownAsync(command.Code, cancellationToken);
                    }
                    else
                    {
                        await MouseUpAsync(command.Code, cancellationToken);
                    }

                    break;
            }
        }
    }

    public ValueTask KeyDownAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.Equals(key, _failCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("simulated down failure");
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask KeyUpAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        KeyUpCalls++;
        return ValueTask.CompletedTask;
    }

    public ValueTask MouseDownAsync(string button, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.Equals(KeyNameResolver.NormalizeMouseButton(button), _failCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("simulated down failure");
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask MouseUpAsync(string button, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        MouseUpCalls++;
        return ValueTask.CompletedTask;
    }

    public ValueTask MoveMouseToAsync(int x, int y, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }

    public ValueTask<(int X, int Y)> GetMousePositionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult((0, 0));
    }

    public ValueTask<(int Width, int Height)> GetScreenSizeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult((1920, 1080));
    }
}
