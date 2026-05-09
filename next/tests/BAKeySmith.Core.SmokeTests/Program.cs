using System.Diagnostics;
using BAKeySmith.Core.Actions.V2;
using BAKeySmith.Core.Configuration;
using BAKeySmith.Core.Configuration.V2;
using BAKeySmith.Core.Contracts;
using BAKeySmith.Core.Diagnostics;
using BAKeySmith.Core.Foreground;
using BAKeySmith.Core.Hosting;
using BAKeySmith.Core.Input;
using BAKeySmith.Core.Input.V2;
using BAKeySmith.Core.Input.V2.Conflicts;
using BAKeySmith.Core.Input.V2.Capture;
using BAKeySmith.Core.Mappings.V2;
using BAKeySmith.Core.Runtime;
using BAKeySmith.Core.Runtime.V2;
using BAKeySmith.Core.Runtime.V2.Configuration;
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

static Task InputNameResolverV2ResolvesCanonicalAndAliases()
{
    AssertTrue(InputNameResolverV2.Resolve(" ctrl ").CanonicalName == "ctrl",
        "Generic ctrl should resolve case-insensitively with trimming.");
    AssertTrue(InputNameResolverV2.Resolve("LEFT_CTRL").CanonicalName == "left_ctrl",
        "Canonical side-specific modifier should resolve case-insensitively.");
    AssertTrue(InputNameResolverV2.Resolve("ctrl_l").CanonicalName == "left_ctrl",
        "V2 ctrl_l must resolve to left_ctrl, not generic ctrl.");
    AssertTrue(InputNameResolverV2.Resolve("ralt").CanonicalName == "right_alt",
        "V2 ralt should resolve to right_alt.");
    AssertTrue(InputNameResolverV2.Resolve("esc").CanonicalName == "escape",
        "esc should normalize to escape.");
    AssertTrue(InputNameResolverV2.Resolve("escape").CanonicalName == "escape",
        "escape should remain canonical.");
    AssertTrue(InputNameResolverV2.Resolve("page up").CanonicalName == "page_up",
        "page up should normalize to page_up.");
    AssertTrue(InputNameResolverV2.Resolve("page_up").CanonicalName == "page_up",
        "page_up should remain canonical.");
    AssertTrue(InputNameResolverV2.Resolve("left arrow").CanonicalName == "arrow_left",
        "left arrow should normalize to arrow_left.");
    AssertTrue(InputNameResolverV2.Resolve("arrow-left").CanonicalName == "arrow_left",
        "Hyphenated aliases should normalize when safe.");
    return Task.CompletedTask;
}

static Task InputNameResolverV2ResolvesOemPunctuation()
{
    AssertTrue(InputNameResolverV2.Resolve("`").CanonicalName == "key_grave",
        "Backtick should resolve to key_grave.");
    AssertTrue(InputNameResolverV2.Resolve("~").CanonicalName == "key_grave",
        "Tilde should resolve to key_grave.");
    AssertTrue(InputNameResolverV2.Resolve("-").CanonicalName == "key_minus",
        "Minus symbol should resolve to key_minus.");
    AssertTrue(InputNameResolverV2.Resolve("_").CanonicalName == "key_minus",
        "Underscore should resolve to key_minus.");
    AssertTrue(InputNameResolverV2.Resolve("+").CanonicalName == "key_equal",
        "Plus should resolve to key_equal.");
    AssertTrue(InputNameResolverV2.Resolve("{").CanonicalName == "key_left_bracket",
        "Left brace should resolve to key_left_bracket.");
    AssertTrue(InputNameResolverV2.Resolve("|").CanonicalName == "key_backslash",
        "Pipe should resolve to key_backslash.");
    AssertTrue(InputNameResolverV2.Resolve(":").CanonicalName == "key_semicolon",
        "Colon should resolve to key_semicolon.");
    AssertTrue(InputNameResolverV2.Resolve("\"").CanonicalName == "key_quote",
        "Double quote should resolve to key_quote.");
    AssertTrue(InputNameResolverV2.Resolve("<").CanonicalName == "key_comma",
        "Less-than should resolve to key_comma.");
    AssertTrue(InputNameResolverV2.Resolve(">").CanonicalName == "key_period",
        "Greater-than should resolve to key_period.");
    AssertTrue(InputNameResolverV2.Resolve("?").CanonicalName == "key_slash",
        "Question mark should resolve to key_slash.");
    return Task.CompletedTask;
}

static Task InputNameResolverV2ReportsDisplayAndCapabilities()
{
    var leftCtrl = InputNameResolverV2.Resolve("left_ctrl");
    AssertTrue(leftCtrl.DisplayName == "Left Ctrl", "left_ctrl should expose an English display label.");
    AssertTrue(leftCtrl.ChineseDisplayName == "左 Ctrl", "left_ctrl should expose a Chinese display label.");
    AssertTrue(leftCtrl.IsModifier, "left_ctrl should be marked as a modifier.");
    AssertTrue(leftCtrl.CanBeHotkeyModifier, "left_ctrl should be usable as a hotkey modifier.");
    AssertTrue(!leftCtrl.CanBeHotkeyMainKey, "Modifier-only input must not be a hotkey main key.");

    var keyGrave = InputNameResolverV2.Resolve("key_grave");
    AssertTrue(keyGrave.DisplayName == "` / ~", "key_grave should display symbol pair.");
    AssertTrue(keyGrave.CanBeKeyOutput, "key_grave should be eligible for key output in the v2 model.");
    AssertTrue(keyGrave.CanBeHotkeyMainKey, "key_grave should be eligible as a hotkey main key.");

    var mouseX1 = InputNameResolverV2.Resolve("mouse_x1");
    AssertTrue(mouseX1.Kind == InputKind.MouseButton, "mouse_x1 should be a mouse button.");
    AssertTrue(mouseX1.ChineseDisplayName == "鼠标侧键 1", "mouse_x1 should expose a Chinese display label.");
    AssertTrue(mouseX1.CanBeMouseOutput, "mouse_x1 should be eligible for mouse output.");

    var wheel = InputNameResolverV2.Resolve("mouse_wheel_up");
    AssertTrue(wheel.IsWheel, "mouse_wheel_up should be marked as wheel input.");
    AssertTrue(wheel.CanBeMappingTrigger, "mouse_wheel_up should be usable as a mapping trigger.");
    AssertTrue(!wheel.CanBeMouseOutput, "mouse_wheel_up must not be a mouse output in the v2 skeleton.");
    return Task.CompletedTask;
}

static Task InputOverlapV2DetectsModifierOverlap()
{
    AssertTrue(InputOverlap.Overlaps("ctrl", "left_ctrl"),
        "Generic ctrl should overlap left_ctrl.");
    AssertTrue(InputOverlap.Overlaps("ctrl", "right_ctrl"),
        "Generic ctrl should overlap right_ctrl.");
    AssertTrue(InputOverlap.Overlaps("alt", "left_alt"),
        "Generic alt should overlap left_alt.");
    AssertTrue(InputOverlap.Overlaps("shift", "right_shift"),
        "Generic shift should overlap right_shift.");
    AssertTrue(InputOverlap.Overlaps("win", "left_win"),
        "Generic win should overlap left_win.");
    AssertTrue(!InputOverlap.Overlaps("left_ctrl", "right_ctrl"),
        "left_ctrl must not overlap right_ctrl.");
    AssertTrue(!InputOverlap.Overlaps("left_alt", "right_alt"),
        "left_alt must not overlap right_alt.");
    AssertTrue(!InputOverlap.Overlaps("left_shift", "right_shift"),
        "left_shift must not overlap right_shift.");
    AssertTrue(!InputOverlap.Overlaps("left_win", "right_win"),
        "left_win must not overlap right_win.");
    AssertTrue(InputOverlap.Overlaps("key_minus", "-"),
        "key_minus should overlap its own aliases.");
    AssertTrue(!InputOverlap.Overlaps("key_minus", "key_equal"),
        "key_minus should not overlap other OEM keys.");
    AssertTrue(!InputOverlap.Overlaps("mouse_wheel_up", "mouse_wheel_down"),
        "Wheel up and wheel down should not overlap.");
    return Task.CompletedTask;
}

static Task InputNameResolverV2DoesNotChangeV1Resolver()
{
    AssertTrue(InputNameResolverV2.Resolve("ctrl_l").CanonicalName == "left_ctrl",
        "V2 should preserve side-specific modifier identity.");
    AssertTrue(KeyNameResolver.ResolveKeyboardKey("ctrl_l").Name == "ctrl",
        "V1 KeyNameResolver behavior must remain unchanged.");
    AssertTrue(KeyNameResolver.ResolveKeyboardKey("-").Name == "-",
        "V1 OEM punctuation behavior must remain literal.");
    AssertTrue(InputNameResolverV2.Resolve("-").CanonicalName == "key_minus",
        "V2 OEM punctuation should use canonical physical names.");
    return Task.CompletedTask;
}

static Task InputNameResolverV2RegistryInvariantsHold()
{
    var errors = InputNameResolverV2.ValidateRegistryInvariants();
    AssertTrue(errors.Count == 0, $"V2 input registry invariant errors:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");

    var canonicalNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var definition in InputNameResolverV2.AllDefinitions)
    {
        AssertTrue(canonicalNames.Add(definition.CanonicalName),
            $"{definition.CanonicalName} should be defined only once.");
        AssertTrue(!string.IsNullOrWhiteSpace(definition.DisplayName),
            $"{definition.CanonicalName} should expose a display label.");
        AssertTrue(!string.IsNullOrWhiteSpace(definition.ChineseDisplayName),
            $"{definition.CanonicalName} should expose a Chinese display label.");
        AssertTrue(definition.Capabilities != InputCapabilities.None,
            $"{definition.CanonicalName} should expose capabilities.");

        var canonical = InputNameResolverV2.Resolve(definition.CanonicalName);
        AssertTrue(canonical.CanonicalName == definition.CanonicalName,
            $"{definition.CanonicalName} canonical lookup should resolve to itself.");

        foreach (var alias in definition.Aliases)
        {
            var aliasSpec = InputNameResolverV2.Resolve(alias);
            AssertTrue(aliasSpec.CanonicalName == definition.CanonicalName,
                $"{definition.CanonicalName} alias {alias} should resolve to the definition canonical name.");
        }
    }

    return Task.CompletedTask;
}

static Task ConflictModelV2DetectsMappingTriggerConflicts()
{
    var analyzer = new InputConflictAnalyzerV2();
    var exact = analyzer.Analyze(
    [
        InputBindingV2.Single("map:z:1", InputBindingRole.MappingTrigger, "z"),
        InputBindingV2.Single("map:z:2", InputBindingRole.MappingTrigger, "z")
    ]);
    AssertConflict(exact, InputConflictCode.DuplicateMappingTrigger, "map:z:1", "map:z:2");

    var genericSide = analyzer.Analyze(
    [
        InputBindingV2.Single("map:ctrl", InputBindingRole.MappingTrigger, "ctrl"),
        InputBindingV2.Single("map:left_ctrl", InputBindingRole.MappingTrigger, "left_ctrl")
    ]);
    AssertConflict(genericSide, InputConflictCode.DuplicateMappingTrigger, "map:ctrl", "map:left_ctrl");

    var leftRight = analyzer.Analyze(
    [
        InputBindingV2.Single("map:left_ctrl", InputBindingRole.MappingTrigger, "left_ctrl"),
        InputBindingV2.Single("map:right_ctrl", InputBindingRole.MappingTrigger, "right_ctrl")
    ]);
    AssertTrue(leftRight.Count == 0, "left_ctrl trigger must not conflict with right_ctrl trigger.");
    return Task.CompletedTask;
}

static Task ConflictModelV2DetectsControlHotkeyConflicts()
{
    var analyzer = new InputConflictAnalyzerV2();
    AssertConflict(
        analyzer.Analyze(
        [
            InputBindingV2.HotkeyCombo("control:f5", InputBindingRole.ControlHotkey, "f5"),
            InputBindingV2.Single("map:f5", InputBindingRole.MappingTrigger, "f5")
        ]),
        InputConflictCode.ControlHotkeyConflictsWithMappingTrigger,
        "control:f5",
        "map:f5");

    AssertConflict(
        analyzer.Analyze(
        [
            InputBindingV2.HotkeyCombo("control:left_ctrl_f8", InputBindingRole.ControlHotkey, "left_ctrl+f8"),
            InputBindingV2.Single("map:left_ctrl", InputBindingRole.MappingTrigger, "left_ctrl")
        ]),
        InputConflictCode.ControlHotkeyConflictsWithMappingTrigger,
        "control:left_ctrl_f8",
        "map:left_ctrl");

    AssertConflict(
        analyzer.Analyze(
        [
            InputBindingV2.HotkeyCombo("control:ctrl_f8", InputBindingRole.ControlHotkey, "ctrl+f8"),
            InputBindingV2.Single("map:left_ctrl", InputBindingRole.MappingTrigger, "left_ctrl")
        ]),
        InputConflictCode.ControlHotkeyConflictsWithMappingTrigger,
        "control:ctrl_f8",
        "map:left_ctrl");

    var noConflict = analyzer.Analyze(
    [
        InputBindingV2.HotkeyCombo("control:left_ctrl_f8", InputBindingRole.ControlHotkey, "left_ctrl+f8"),
        InputBindingV2.Single("map:right_ctrl", InputBindingRole.MappingTrigger, "right_ctrl")
    ]);
    AssertTrue(noConflict.Count == 0, "left_ctrl+f8 should not conflict with right_ctrl trigger.");
    return Task.CompletedTask;
}

static Task ConflictModelV2DetectsCoordinateAndEmergencyConflicts()
{
    var analyzer = new InputConflictAnalyzerV2();
    AssertConflict(
        analyzer.Analyze(
        [
            InputBindingV2.HotkeyCombo("record:f8", InputBindingRole.CoordinateRecordHotkey, "f8"),
            InputBindingV2.Single("map:f8", InputBindingRole.MappingTrigger, "f8")
        ]),
        InputConflictCode.CoordinateRecordHotkeyConflictsWithMappingTrigger,
        "record:f8",
        "map:f8");

    AssertConflict(
        analyzer.Analyze(
        [
            InputBindingV2.HotkeyCombo("emergency:f12", InputBindingRole.EmergencyStopHotkey, "f12"),
            InputBindingV2.Single("map:f12", InputBindingRole.MappingTrigger, "f12")
        ]),
        InputConflictCode.EmergencyStopHotkeyConflictsWithBinding,
        "emergency:f12",
        "map:f12");

    AssertConflict(
        analyzer.Analyze(
        [
            InputBindingV2.HotkeyCombo("emergency:left_ctrl_f12", InputBindingRole.EmergencyStopHotkey, "left_ctrl+f12"),
            InputBindingV2.HotkeyCombo("control:left_ctrl_f12", InputBindingRole.ControlHotkey, "left_ctrl+f12")
        ]),
        InputConflictCode.EmergencyStopHotkeyConflictsWithBinding,
        "emergency:left_ctrl_f12",
        "control:left_ctrl_f12");

    var noConflict = analyzer.Analyze(
    [
        InputBindingV2.HotkeyCombo("emergency:left_ctrl_f12", InputBindingRole.EmergencyStopHotkey, "left_ctrl+f12"),
        InputBindingV2.HotkeyCombo("control:right_ctrl_f12", InputBindingRole.ControlHotkey, "right_ctrl+f12")
    ]);
    AssertTrue(noConflict.Count == 0,
        "left_ctrl+f12 emergency stop should not conflict with right_ctrl+f12 control hotkey.");
    return Task.CompletedTask;
}

static Task ConflictModelV2HandlesWheelAndHotkeyMainRules()
{
    var analyzer = new InputConflictAnalyzerV2();
    AssertConflict(
        analyzer.Analyze(
        [
            InputBindingV2.Single("map:wheel_up:1", InputBindingRole.MappingTrigger, "mouse_wheel_up"),
            InputBindingV2.Single("map:wheel_up:2", InputBindingRole.MappingTrigger, "mouse_wheel_up")
        ]),
        InputConflictCode.DuplicateMappingTrigger,
        "map:wheel_up:1",
        "map:wheel_up:2");

    var noConflict = analyzer.Analyze(
    [
        InputBindingV2.Single("map:wheel_up", InputBindingRole.MappingTrigger, "mouse_wheel_up"),
        InputBindingV2.Single("map:wheel_down", InputBindingRole.MappingTrigger, "mouse_wheel_down")
    ]);
    AssertTrue(noConflict.Count == 0, "mouse_wheel_up must not conflict with mouse_wheel_down.");

    var ex = AssertThrows<ArgumentException>(
        () => HotkeySpecV2.Parse("ctrl"),
        "Modifier-only hotkey must not be accepted without a main key.");
    AssertTrue(ex.Message.Contains("main key", StringComparison.OrdinalIgnoreCase),
        "Modifier-only hotkey error should mention the missing main key.");
    return Task.CompletedTask;
}

static Task ConflictModelV2UsesExactModifierSetForHotkeys()
{
    var analyzer = new InputConflictAnalyzerV2();
    AssertConflict(
        analyzer.Analyze(
        [
            InputBindingV2.HotkeyCombo("emergency:ctrl_f8", InputBindingRole.EmergencyStopHotkey, "ctrl+f8"),
            InputBindingV2.HotkeyCombo("control:left_ctrl_f8", InputBindingRole.ControlHotkey, "left_ctrl+f8")
        ]),
        InputConflictCode.EmergencyStopHotkeyConflictsWithBinding,
        "emergency:ctrl_f8",
        "control:left_ctrl_f8");

    var noConflict = analyzer.Analyze(
    [
        InputBindingV2.HotkeyCombo("emergency:ctrl_shift_f8", InputBindingRole.EmergencyStopHotkey, "ctrl+shift+f8"),
        InputBindingV2.HotkeyCombo("control:ctrl_f8", InputBindingRole.ControlHotkey, "ctrl+f8")
    ]);
    AssertTrue(noConflict.Count == 0,
        "Skeleton hotkey-vs-hotkey semantics use exact modifier set size, so ctrl+shift+f8 does not conflict with ctrl+f8.");
    return Task.CompletedTask;
}

static Task ConflictModelV2RejectsOverlappingHotkeyModifiers()
{
    foreach (var hotkey in new[]
    {
        "ctrl+left_ctrl+f8",
        "ctrl+right_ctrl+f8",
        "shift+left_shift+f8",
        "alt+right_alt+f8",
        "win+left_win+f8"
    })
    {
        var ex = AssertThrows<ArgumentException>(
            () => HotkeySpecV2.Parse(hotkey),
            $"{hotkey} should reject overlapping modifiers inside the same hotkey.");
        AssertTrue(ex.Message.Contains("overlap", StringComparison.OrdinalIgnoreCase),
            $"{hotkey} should report an overlap error.");
    }

    var allowed = HotkeySpecV2.Parse("left_ctrl+right_ctrl+f8");
    AssertTrue(allowed.CanonicalText == "left_ctrl+right_ctrl+f8",
        "left_ctrl+right_ctrl+f8 remains legal because side-specific siblings do not overlap.");
    return Task.CompletedTask;
}

static Task CoordinateRecordHotkeyV2ValidatesFullHotkeys()
{
    AssertTrue(CoordinateRecordHotkeySpecV2.Parse("f8").CanonicalText == "f8",
        "F8 should be a valid coordinate record hotkey.");
    AssertTrue(CoordinateRecordHotkeySpecV2.Parse("mouse_x1").CanonicalText == "mouse_x1",
        "mouse_x1 should be a valid coordinate record hotkey.");
    AssertTrue(CoordinateRecordHotkeySpecV2.Parse("left_ctrl+f8").CanonicalText == "left_ctrl+f8",
        "left_ctrl+f8 should be a valid coordinate record hotkey.");

    AssertThrows<ArgumentException>(
        () => CoordinateRecordHotkeySpecV2.Parse("left_ctrl"),
        "Modifier-only coordinate record hotkey should be rejected.");
    AssertThrows<ArgumentException>(
        () => CoordinateRecordHotkeySpecV2.Parse("mouse_wheel_up"),
        "mouse_wheel_up should not be a coordinate record hotkey in this skeleton.");
    AssertThrows<ArgumentException>(
        () => CoordinateRecordHotkeySpecV2.Parse("ctrl+left_ctrl+f8"),
        "Coordinate record hotkey should inherit internal modifier overlap validation.");

    var f8 = WindowsInputNormalizerV2.NormalizeKey(KeyEvent(0x77));
    var mouseX1 = WindowsInputNormalizerV2.NormalizeMouse(MouseEvent(WindowsMouseEventKindV2.XButton1));
    var leftCtrl = WindowsInputNormalizerV2.NormalizeKey(KeyEvent(0xA2));
    var wheel = WindowsInputNormalizerV2.NormalizeMouse(MouseEvent(WindowsMouseEventKindV2.Wheel, 120));
    AssertTrue(f8.CanUseAsCoordinateRecordHotkeyMainInput(),
        "f8 capture should be valid as a coordinate record hotkey main input.");
    AssertTrue(mouseX1.CanUseAsCoordinateRecordHotkeyMainInput(),
        "mouse_x1 capture should be valid as a coordinate record hotkey main input.");
    AssertTrue(!leftCtrl.CanUseAsCoordinateRecordHotkeyMainInput(),
        "left_ctrl capture should be a modifier component, not a coordinate record main input.");
    AssertTrue(!wheel.CanUseAsCoordinateRecordHotkeyMainInput(),
        "wheel capture should not be a coordinate record main input.");
    return Task.CompletedTask;
}

static Task InputCaptureV2NormalizesSideSpecificModifiers()
{
    AssertCapturedKey(0xA2, 0, false, "left_ctrl");
    AssertCapturedKey(0xA3, 0, true, "right_ctrl");
    AssertCapturedKey(0x11, 0, false, "left_ctrl");
    AssertCapturedKey(0x11, 0, true, "right_ctrl");
    AssertCapturedKey(0xA4, 0, false, "left_alt");
    AssertCapturedKey(0xA5, 0, true, "right_alt");
    AssertCapturedKey(0x12, 0, false, "left_alt");
    AssertCapturedKey(0x12, 0, true, "right_alt");
    AssertCapturedKey(0xA0, 0x2A, false, "left_shift");
    AssertCapturedKey(0xA1, 0x36, false, "right_shift");
    AssertCapturedKey(0x10, 0x2A, false, "left_shift");
    AssertCapturedKey(0x10, 0x36, false, "right_shift");
    AssertCapturedKey(0x5B, 0, true, "left_win");
    AssertCapturedKey(0x5C, 0, true, "right_win");
    AssertTrue(WindowsInputNormalizerV2.NormalizeKey(KeyEvent(0xA2)).Input?.CanonicalName != "ctrl",
        "Physical left ctrl capture must not silently fold to generic ctrl.");
    return Task.CompletedTask;
}

static Task InputCaptureV2NormalizesOemPunctuation()
{
    AssertCapturedKey(0xC0, 0, false, "key_grave");
    AssertCapturedKey(0xBD, 0, false, "key_minus");
    AssertCapturedKey(0xBB, 0, false, "key_equal");
    AssertCapturedKey(0xDB, 0, false, "key_left_bracket");
    AssertCapturedKey(0xDD, 0, false, "key_right_bracket");
    AssertCapturedKey(0xDC, 0, false, "key_backslash");
    AssertCapturedKey(0xBA, 0, false, "key_semicolon");
    AssertCapturedKey(0xDE, 0, false, "key_quote");
    AssertCapturedKey(0xBC, 0, false, "key_comma");
    AssertCapturedKey(0xBE, 0, false, "key_period");
    AssertCapturedKey(0xBF, 0, false, "key_slash");
    return Task.CompletedTask;
}

static Task InputCaptureV2NormalizesCommonKeys()
{
    AssertCapturedKey(0x1B, 0, false, "escape");
    AssertCapturedKey(0x0D, 0, false, "enter");
    AssertCapturedKey(0x09, 0, false, "tab");
    AssertCapturedKey(0x20, 0, false, "space");
    AssertCapturedKey(0x08, 0, false, "backspace");
    AssertCapturedKey(0x2E, 0, true, "delete");
    AssertCapturedKey(0x2D, 0, true, "insert");
    AssertCapturedKey(0x24, 0, true, "home");
    AssertCapturedKey(0x23, 0, true, "end");
    AssertCapturedKey(0x21, 0, true, "page_up");
    AssertCapturedKey(0x22, 0, true, "page_down");
    AssertCapturedKey(0x26, 0, true, "arrow_up");
    AssertCapturedKey(0x28, 0, true, "arrow_down");
    AssertCapturedKey(0x25, 0, true, "arrow_left");
    AssertCapturedKey(0x27, 0, true, "arrow_right");
    AssertCapturedKey(0x70, 0, false, "f1");
    AssertCapturedKey(0x7B, 0, false, "f12");
    AssertCapturedKey((uint)'A', 0, false, "a");
    AssertCapturedKey((uint)'Z', 0, false, "z");
    AssertCapturedKey((uint)'0', 0, false, "0");
    AssertCapturedKey((uint)'9', 0, false, "9");
    return Task.CompletedTask;
}

static Task InputCaptureV2NormalizesMouseAndWheel()
{
    AssertCapturedMouse(WindowsMouseEventKindV2.LeftButton, 0, "mouse_left");
    AssertCapturedMouse(WindowsMouseEventKindV2.RightButton, 0, "mouse_right");
    AssertCapturedMouse(WindowsMouseEventKindV2.MiddleButton, 0, "mouse_middle");
    AssertCapturedMouse(WindowsMouseEventKindV2.XButton1, 0, "mouse_x1");
    AssertCapturedMouse(WindowsMouseEventKindV2.XButton2, 0, "mouse_x2");
    var wheelUp = AssertCapturedMouse(WindowsMouseEventKindV2.Wheel, 120, "mouse_wheel_up");
    var wheelDown = AssertCapturedMouse(WindowsMouseEventKindV2.Wheel, -120, "mouse_wheel_down");
    AssertTrue(wheelUp.Input?.IsWheel == true, "mouse_wheel_up should carry wheel capability.");
    AssertTrue(wheelDown.Input?.IsWheel == true, "mouse_wheel_down should carry wheel capability.");
    AssertTrue(wheelUp.Input?.CanBeMouseOutput == false, "mouse_wheel_up must not be mouse output.");
    AssertTrue(wheelDown.Input?.CanBeMouseOutput == false, "mouse_wheel_down must not be mouse output.");
    return Task.CompletedTask;
}

static Task InputCaptureV2PurposeChecksUseCapabilities()
{
    var modifier = WindowsInputNormalizerV2.NormalizeKey(KeyEvent(0xA2));
    AssertTrue(modifier.CanUseFor(InputCapturePurpose.ControlHotkey),
        "left_ctrl should be accepted as a control hotkey component.");
    AssertTrue(!modifier.CanUseAsHotkeyMainKey(),
        "modifier-only capture must not be valid as a hotkey main key.");

    var f8 = WindowsInputNormalizerV2.NormalizeKey(KeyEvent(0x77));
    AssertTrue(f8.Input?.CanonicalName == "f8", "F8 should normalize to f8.");
    AssertTrue(f8.CanUseFor(InputCapturePurpose.CoordinateRecordHotkey),
        "f8 should be usable as a coordinate record hotkey.");

    var mouseX1 = WindowsInputNormalizerV2.NormalizeMouse(MouseEvent(WindowsMouseEventKindV2.XButton1));
    AssertTrue(mouseX1.CanUseFor(InputCapturePurpose.CoordinateRecordHotkey),
        "mouse_x1 should be usable as a coordinate record hotkey.");

    var wheel = WindowsInputNormalizerV2.NormalizeMouse(MouseEvent(WindowsMouseEventKindV2.Wheel, 120));
    AssertTrue(wheel.CanUseFor(InputCapturePurpose.MappingTrigger),
        "wheel should be usable as a mapping trigger.");
    AssertTrue(!wheel.CanUseFor(InputCapturePurpose.SimpleTarget),
        "wheel must not be usable as a simple target output.");
    AssertTrue(!wheel.CanUseFor(InputCapturePurpose.CoordinateRecordHotkey),
        "wheel must not be usable as a coordinate record hotkey in this skeleton.");
    return Task.CompletedTask;
}

static Task InputCaptureV2PreservesPhaseAndSourceKindMetadata()
{
    var keyUpEvent = KeyEvent(
        0x77,
        isDown: false,
        sourceKind: InputCaptureSourceKind.Focused);
    var keyUp = WindowsInputNormalizerV2.NormalizeKey(keyUpEvent);
    AssertTrue(!keyUpEvent.IsDown, "Synthetic key-up phase should remain available on event metadata.");
    AssertTrue(keyUp.Success, "F8 key-up metadata should still normalize.");
    AssertTrue(keyUp.SourceKind == InputCaptureSourceKind.Focused,
        "Key capture result should preserve source kind.");

    var mouseUpEvent = MouseEvent(
        WindowsMouseEventKindV2.XButton1,
        isDown: false,
        sourceKind: InputCaptureSourceKind.LowLevelHook);
    var mouseUp = WindowsInputNormalizerV2.NormalizeMouse(mouseUpEvent);
    AssertTrue(!mouseUpEvent.IsDown, "Synthetic mouse-up phase should remain available on event metadata.");
    AssertTrue(mouseUp.Success, "XButton1 mouse-up metadata should still normalize.");
    AssertTrue(mouseUp.SourceKind == InputCaptureSourceKind.LowLevelHook,
        "Mouse capture result should preserve source kind.");
    return Task.CompletedTask;
}

static Task InputCaptureV2RejectsAmbiguousShiftAndZeroWheel()
{
    var ambiguousShift = WindowsInputNormalizerV2.NormalizeKey(KeyEvent(0x10, scanCode: 0));
    AssertTrue(!ambiguousShift.Success,
        "Generic VK_SHIFT without a distinguishing scan code should be unsupported instead of guessed.");

    var leftShift = WindowsInputNormalizerV2.NormalizeKey(KeyEvent(0x10, scanCode: 0x2A));
    var rightShift = WindowsInputNormalizerV2.NormalizeKey(KeyEvent(0x10, scanCode: 0x36));
    AssertTrue(leftShift.Input?.CanonicalName == "left_shift",
        "Generic VK_SHIFT with left scan code should normalize to left_shift.");
    AssertTrue(rightShift.Input?.CanonicalName == "right_shift",
        "Generic VK_SHIFT with right scan code should normalize to right_shift.");

    var zeroWheel = WindowsInputNormalizerV2.NormalizeMouse(MouseEvent(WindowsMouseEventKindV2.Wheel, 0));
    AssertTrue(!zeroWheel.Success, "Zero-delta wheel event should be unsupported.");
    return Task.CompletedTask;
}

static Task ActionModelV2ValidatesTapAndHoldPlans()
{
    var validator = new ActivationPlanValidatorV2();
    var tapPlan = new ActivationPlanV2(
    [
        KeyActionV2.Tap(InputNameResolverV2.Resolve("a"), TimeSpan.FromMilliseconds(10))
    ]);
    AssertNoActionErrors(validator.Validate(tapPlan), "on_down tap_key a should validate.");

    var holdPlan = new ActivationPlanV2(
    [
        KeyActionV2.Down(InputNameResolverV2.Resolve("q"))
    ],
    OnUp:
    [
        KeyActionV2.Up(InputNameResolverV2.Resolve("q"))
    ]);
    AssertNoActionErrors(validator.Validate(holdPlan), "simple hold equivalent should validate.");
    AssertTrue(holdPlan.OnDown[0].OwnershipHint == ActionOwnershipHintV2.AcquireKey,
        "down_key should expose an acquire key ownership hint.");
    AssertTrue(holdPlan.OnUp[0].OwnershipHint == ActionOwnershipHintV2.ReleaseKey,
        "up_key should expose a release key ownership hint.");

    var modifierPlan = new ActivationPlanV2(
    [
        KeyActionV2.Down(InputNameResolverV2.Resolve("left_ctrl"))
    ]);
    AssertNoActionErrors(validator.Validate(modifierPlan), "down_key left_ctrl should validate.");
    return Task.CompletedTask;
}

static Task ActionModelV2ValidatesWhileHeldZeroIntervalAndOnUp()
{
    var validator = new ActivationPlanValidatorV2();
    var turboPlan = new ActivationPlanV2(
        OnDown: [],
        WhileHeld: new WhileHeldBlockV2(
            TimeSpan.Zero,
            [KeyActionV2.Tap(InputNameResolverV2.Resolve("1"), TimeSpan.FromMilliseconds(10))]),
        OnUp:
        [
            KeyActionV2.Tap(InputNameResolverV2.Resolve("escape"))
        ]);

    AssertNoActionErrors(validator.Validate(turboPlan),
        "while_held interval 0ms and on_up tap_key escape should validate.");

    var emptyWhileHeld = new ActivationPlanV2(
        OnDown: [],
        WhileHeld: new WhileHeldBlockV2(TimeSpan.Zero, []));
    var diagnostics = validator.Validate(emptyWhileHeld);
    AssertTrue(diagnostics.Any(item =>
            item.Severity == ActionValidationSeverityV2.Warning &&
            item.Code == ActionValidationCodeV2.EmptyWhileHeldBody &&
            item.Path == "while_held.body"),
        "Empty while_held body should produce a warning with a stable path.");
    AssertNoActionErrors(diagnostics, "Empty while_held body is a warning, not an error.");
    return Task.CompletedTask;
}

static Task ActionModelV2ValidatesRepeatAndDiagnostics()
{
    var validator = new ActivationPlanValidatorV2();
    var repeatPlan = new ActivationPlanV2(
    [
        new RepeatActionV2(
            3,
            [KeyActionV2.Tap(InputNameResolverV2.Resolve("a"))])
    ]);
    AssertNoActionErrors(validator.Validate(repeatPlan), "Finite repeat should validate.");

    var invalidRepeat = new ActivationPlanV2(
    [
        new RepeatActionV2(
            0,
            [KeyActionV2.Tap(InputNameResolverV2.Resolve("a"))])
    ]);
    var diagnostics = validator.Validate(invalidRepeat);
    var diagnostic = diagnostics.FirstOrDefault(item => item.Code == ActionValidationCodeV2.InvalidRepeatCount);
    AssertTrue(diagnostic is not null, "repeat count 0 should produce a diagnostic.");
    AssertTrue(diagnostic!.Severity == ActionValidationSeverityV2.Error,
        "repeat count 0 should be an error.");
    AssertTrue(!string.IsNullOrWhiteSpace(diagnostic.Message),
        "Action diagnostic should include a message.");
    AssertTrue(diagnostic.Path == "on_down[0]",
        "Action diagnostic should include a stable action path.");
    return Task.CompletedTask;
}

static Task ActionModelV2RejectsWrongInputKinds()
{
    var validator = new ActivationPlanValidatorV2();
    var mouseAsKey = new ActivationPlanV2(
    [
        KeyActionV2.Tap(InputNameResolverV2.Resolve("mouse_left"))
    ]);
    AssertActionError(
        validator.Validate(mouseAsKey),
        ActionValidationCodeV2.InvalidKeyActionInput,
        "key action should reject mouse button input.");

    var keyAsMouse = new ActivationPlanV2(
    [
        MouseButtonActionV2.Tap(InputNameResolverV2.Resolve("a"))
    ]);
    AssertActionError(
        validator.Validate(keyAsMouse),
        ActionValidationCodeV2.InvalidMouseButtonActionInput,
        "mouse action should reject keyboard input.");

    var wheelAsMouse = new ActivationPlanV2(
    [
        MouseButtonActionV2.Tap(InputNameResolverV2.Resolve("mouse_wheel_up"))
    ]);
    AssertActionError(
        validator.Validate(wheelAsMouse),
        ActionValidationCodeV2.InvalidMouseButtonActionInput,
        "wheel must not be treated as a mouse button action.");
    return Task.CompletedTask;
}

static Task ActionModelV2RepresentsWheelAndCoordinateSkeleton()
{
    var validator = new ActivationPlanValidatorV2();
    var wheelPlan = new ActivationPlanV2(
    [
        new WheelActionV2(WheelDirectionV2.Up, Steps: 2),
        new WheelActionV2(WheelDirectionV2.Down)
    ]);
    AssertNoActionErrors(validator.Validate(wheelPlan),
        "Wheel up/down should validate as dedicated wheel actions.");

    var coordinatePoint = new CoordinatePointV2(120, 240, "ba16_1920x1080");
    var coordinatePlan = new ActivationPlanV2(
    [
        CoordinateActionV2.TapAt(coordinatePoint),
        CoordinateActionV2.DragAt(
            coordinatePoint,
            new CoordinatePointV2(200, 300, "ba16_1920x1080"),
            InputNameResolverV2.Resolve("mouse_left"),
            TimeSpan.FromMilliseconds(80))
    ]);

    var diagnostics = validator.Validate(coordinatePlan);
    AssertNoActionErrors(diagnostics, "Coordinate tap/drag skeleton should pass data validation.");
    AssertTrue(diagnostics.Any(item =>
            item.Severity == ActionValidationSeverityV2.Info &&
            item.Code == ActionValidationCodeV2.CoordinateActionNotLiveReady),
        "Coordinate action should explicitly report that live readiness is not implemented.");
    AssertTrue(!coordinatePlan.OnDown[0].ClaimsLiveReadiness,
        "Coordinate action model must not claim live readiness.");

    var downAt = CoordinateActionV2.DownAt(coordinatePoint);
    AssertTrue(downAt.OwnershipHint == ActionOwnershipHintV2.AcquireCoordinateContact,
        "down_at should expose an acquire coordinate contact ownership hint.");
    return Task.CompletedTask;
}

static Task ActionModelV2RejectsKindMismatchAndUnknownActions()
{
    var validator = new ActivationPlanValidatorV2();
    var keyMismatch = new ActivationPlanV2(
    [
        new KeyActionV2(ActionKindV2.TapMouse, InputNameResolverV2.Resolve("a"))
    ]);
    AssertActionError(
        validator.Validate(keyMismatch),
        ActionValidationCodeV2.ActionKindMismatch,
        "KeyActionV2 with a mouse action kind should be rejected.");

    var coordinateMismatch = new ActivationPlanV2(
    [
        new CoordinateActionV2(
            ActionKindV2.TapKey,
            new CoordinatePointV2(1, 1, "ba16_1920x1080"))
    ]);
    AssertActionError(
        validator.Validate(coordinateMismatch),
        ActionValidationCodeV2.ActionKindMismatch,
        "CoordinateActionV2 with a key action kind should be rejected.");

    var unknownPlan = new ActivationPlanV2([new UnknownActionModelV2()]);
    AssertActionError(
        validator.Validate(unknownPlan),
        ActionValidationCodeV2.UnknownActionType,
        "Unknown ActionModelV2 subtype must not validate silently.");
    return Task.CompletedTask;
}

static Task ActionModelV2ValidatesDurationApplicability()
{
    var validator = new ActivationPlanValidatorV2();
    AssertNoActionErrors(
        validator.Validate(new ActivationPlanV2(
        [
            new WaitActionV2(TimeSpan.Zero),
            KeyActionV2.Tap(InputNameResolverV2.Resolve("a"), TimeSpan.Zero),
            MouseButtonActionV2.Tap(InputNameResolverV2.Resolve("mouse_left"), TimeSpan.Zero),
            CoordinateActionV2.TapAt(new CoordinatePointV2(1, 1, "ba16_1920x1080"), duration: TimeSpan.Zero),
            KeyActionV2.Hold(InputNameResolverV2.Resolve("a"), TimeSpan.Zero),
            MouseButtonActionV2.Hold(InputNameResolverV2.Resolve("mouse_left"), TimeSpan.Zero),
            CoordinateActionV2.HoldAt(new CoordinatePointV2(1, 1, "ba16_1920x1080"), TimeSpan.Zero)
        ])),
        "0ms wait/tap/hold durations are explicitly legal in the skeleton.");

    AssertActionError(
        validator.Validate(new ActivationPlanV2([new WaitActionV2(TimeSpan.FromMilliseconds(-1))])),
        ActionValidationCodeV2.InvalidWaitDuration,
        "Negative wait duration should be rejected.");
    AssertActionError(
        validator.Validate(new ActivationPlanV2([KeyActionV2.Tap(InputNameResolverV2.Resolve("a"), TimeSpan.FromMilliseconds(-1))])),
        ActionValidationCodeV2.InvalidActionDuration,
        "Negative key tap duration should be rejected.");
    AssertActionError(
        validator.Validate(new ActivationPlanV2([MouseButtonActionV2.Tap(InputNameResolverV2.Resolve("mouse_left"), TimeSpan.FromMilliseconds(-1))])),
        ActionValidationCodeV2.InvalidActionDuration,
        "Negative mouse tap duration should be rejected.");
    AssertActionError(
        validator.Validate(new ActivationPlanV2(
        [
            CoordinateActionV2.TapAt(
                new CoordinatePointV2(1, 1, "ba16_1920x1080"),
                duration: TimeSpan.FromMilliseconds(-1))
        ])),
        ActionValidationCodeV2.InvalidActionDuration,
        "Negative coordinate tap duration should be rejected.");

    AssertActionError(
        validator.Validate(new ActivationPlanV2([new KeyActionV2(ActionKindV2.DownKey, InputNameResolverV2.Resolve("a"), TimeSpan.Zero)])),
        ActionValidationCodeV2.DisallowedActionDuration,
        "down_key duration should be rejected.");
    AssertActionError(
        validator.Validate(new ActivationPlanV2([new KeyActionV2(ActionKindV2.UpKey, InputNameResolverV2.Resolve("a"), TimeSpan.Zero)])),
        ActionValidationCodeV2.DisallowedActionDuration,
        "up_key duration should be rejected.");
    AssertActionError(
        validator.Validate(new ActivationPlanV2([KeyActionV2.Hold(InputNameResolverV2.Resolve("a"), TimeSpan.FromMilliseconds(-1))])),
        ActionValidationCodeV2.InvalidActionDuration,
        "Negative hold duration should be rejected.");
    AssertActionError(
        validator.Validate(new ActivationPlanV2([new KeyActionV2(ActionKindV2.HoldKey, InputNameResolverV2.Resolve("a"))])),
        ActionValidationCodeV2.MissingActionDuration,
        "hold_key missing duration should be rejected.");
    return Task.CompletedTask;
}

static Task ActionModelV2ValidatesWhileHeldRepeatAndNestedPaths()
{
    var validator = new ActivationPlanValidatorV2();
    var negativeInterval = new ActivationPlanV2(
        OnDown: [],
        WhileHeld: new WhileHeldBlockV2(TimeSpan.FromMilliseconds(-1), []));
    AssertActionError(
        validator.Validate(negativeInterval),
        ActionValidationCodeV2.NegativeWhileHeldInterval,
        "Negative while_held interval should be rejected.");

    var nestedRepeat = new ActivationPlanV2(
    [
        new RepeatActionV2(
            2,
            [
                new RepeatActionV2(
                    3,
                    [KeyActionV2.Tap(InputNameResolverV2.Resolve("a"))])
            ])
    ]);
    AssertNoActionErrors(validator.Validate(nestedRepeat), "Nested finite repeat should validate.");

    var nestedInvalid = new ActivationPlanV2(
    [
        new RepeatActionV2(
            2,
            [
                new RepeatActionV2(
                    0,
                    [KeyActionV2.Tap(InputNameResolverV2.Resolve("a"))])
            ])
    ]);
    var nestedDiagnostics = validator.Validate(nestedInvalid);
    AssertTrue(nestedDiagnostics.Any(item =>
            item.Code == ActionValidationCodeV2.InvalidRepeatCount &&
            item.Path == "on_down[0].body[0]"),
        "Nested repeat diagnostic should preserve a nested action path.");

    var emptyRepeat = new ActivationPlanV2([new RepeatActionV2(2, [])]);
    var emptyDiagnostics = validator.Validate(emptyRepeat);
    AssertTrue(emptyDiagnostics.Any(item =>
            item.Severity == ActionValidationSeverityV2.Warning &&
            item.Code == ActionValidationCodeV2.EmptyRepeatBody &&
            item.Path == "on_down[0].body"),
        "Empty repeat body should produce a warning diagnostic.");
    AssertNoActionErrors(emptyDiagnostics, "Empty repeat body warning should not block skeleton validation.");
    return Task.CompletedTask;
}

static Task ActionModelV2ValidatesCoordinateInvariants()
{
    var validator = new ActivationPlanValidatorV2();
    AssertActionError(
        validator.Validate(new ActivationPlanV2(
        [
            CoordinateActionV2.TapAt(new CoordinatePointV2(-1, 2, "ba16_1920x1080"))
        ])),
        ActionValidationCodeV2.InvalidCoordinate,
        "Negative coordinate x/y should be rejected.");
    AssertActionError(
        validator.Validate(new ActivationPlanV2(
        [
            CoordinateActionV2.TapAt(new CoordinatePointV2(1, 2, " "))
        ])),
        ActionValidationCodeV2.InvalidCoordinateProfile,
        "Empty coordinate profile id should be rejected.");
    AssertActionError(
        validator.Validate(new ActivationPlanV2(
        [
            CoordinateActionV2.TapAt(
                new CoordinatePointV2(1, 2, "ba16_1920x1080"),
                InputNameResolverV2.Resolve("mouse_wheel_up"))
        ])),
        ActionValidationCodeV2.InvalidCoordinateButton,
        "Coordinate action should reject wheel as a button.");
    AssertActionError(
        validator.Validate(new ActivationPlanV2(
        [
            new CoordinateActionV2(
                ActionKindV2.DownAt,
                new CoordinatePointV2(1, 2, "ba16_1920x1080"),
                Duration: TimeSpan.Zero)
        ])),
        ActionValidationCodeV2.DisallowedActionDuration,
        "down_at duration should be rejected.");
    AssertActionError(
        validator.Validate(new ActivationPlanV2(
        [
            new CoordinateActionV2(
                ActionKindV2.DragAt,
                new CoordinatePointV2(1, 2, "ba16_1920x1080"),
                new CoordinatePointV2(3, 4, "ba16_1920x1080"))
        ])),
        ActionValidationCodeV2.MissingActionDuration,
        "drag_at missing duration should be rejected.");
    return Task.CompletedTask;
}

static Task ActionModelV2ReportsOwnershipEffects()
{
    var point = new CoordinatePointV2(1, 1, "ba16_1920x1080");
    AssertTrue(KeyActionV2.Down(InputNameResolverV2.Resolve("a")).OwnershipHint == ActionOwnershipHintV2.AcquireKey,
        "down_key should acquire key ownership.");
    AssertTrue(KeyActionV2.Up(InputNameResolverV2.Resolve("a")).OwnershipHint == ActionOwnershipHintV2.ReleaseKey,
        "up_key should release key ownership.");
    AssertTrue(KeyActionV2.Tap(InputNameResolverV2.Resolve("a")).OwnershipHint == ActionOwnershipHintV2.TransientKey,
        "tap_key should be transient, not session-held.");
    AssertTrue(KeyActionV2.Hold(InputNameResolverV2.Resolve("a"), TimeSpan.Zero).OwnershipHint == ActionOwnershipHintV2.TimedHoldKey,
        "hold_key should be a timed hold, not session-held.");

    AssertTrue(MouseButtonActionV2.Down(InputNameResolverV2.Resolve("mouse_left")).OwnershipHint == ActionOwnershipHintV2.AcquireMouseButton,
        "down_mouse should acquire mouse button ownership.");
    AssertTrue(MouseButtonActionV2.Up(InputNameResolverV2.Resolve("mouse_left")).OwnershipHint == ActionOwnershipHintV2.ReleaseMouseButton,
        "up_mouse should release mouse button ownership.");
    AssertTrue(CoordinateActionV2.DownAt(point).OwnershipHint == ActionOwnershipHintV2.AcquireCoordinateContact,
        "down_at should acquire coordinate contact ownership.");
    AssertTrue(CoordinateActionV2.UpAt(point).OwnershipHint == ActionOwnershipHintV2.ReleaseCoordinateContact,
        "up_at should release coordinate contact ownership.");
    AssertTrue(CoordinateActionV2.HoldAt(point, TimeSpan.Zero).OwnershipHint == ActionOwnershipHintV2.TimedHoldCoordinateContact,
        "hold_at should be timed hold, not session-held.");
    return Task.CompletedTask;
}

static Task ActionModelV2DoesNotReferenceV1CompilerOrRuntime()
{
    var actionTypes = typeof(ActionModelV2).Assembly.GetTypes()
        .Where(type => string.Equals(type.Namespace, "BAKeySmith.Core.Actions.V2", StringComparison.Ordinal))
        .ToArray();

    foreach (var type in actionTypes)
    {
        var referencedTypes = type.GetConstructors()
            .SelectMany(constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType))
            .Concat(type.GetProperties().Select(property => property.PropertyType))
            .Concat(type.GetFields().Select(field => field.FieldType));

        foreach (var referencedType in referencedTypes)
        {
            var fullName = referencedType.FullName ?? string.Empty;
            AssertTrue(!fullName.Contains("MacroScriptCompiler", StringComparison.Ordinal),
                $"{type.Name} must not reference MacroScriptCompiler.");
            AssertTrue(!fullName.Contains("Runtime", StringComparison.Ordinal),
                $"{type.Name} must not reference runtime implementation types.");
        }
    }

    return Task.CompletedTask;
}

static async Task RuntimeV2ExecutesOnDownTapKeyOrder()
{
    var (backend, _, session) = RuntimeV2Session(new ActivationPlanV2(
    [
        KeyActionV2.Tap(InputNameResolverV2.Resolve("a")),
        KeyActionV2.Tap(InputNameResolverV2.Resolve("b"))
    ]));

    var result = await session.TriggerDownAsync();

    AssertRuntimeV2NoErrors(result, "on_down tap_key actions should execute in the fake sandbox.");
    AssertTrue(backend.Events.Count == 2, "on_down should record two fake events.");
    AssertRuntimeV2Event(
        backend.Events[0],
        RuntimeV2EventKind.KeyTap,
        "a",
        "first on_down event should tap a.");
    AssertRuntimeV2Event(
        backend.Events[1],
        RuntimeV2EventKind.KeyTap,
        "b",
        "second on_down event should tap b.");
    AssertRuntimeV2SequenceIsStable(backend.Events);
}

static async Task RuntimeV2SimpleHoldExecutesDownAndUpLifecycle()
{
    var (backend, ledger, session) = RuntimeV2Session(new ActivationPlanV2(
    [
        KeyActionV2.Down(InputNameResolverV2.Resolve("q"))
    ],
    OnUp:
    [
        KeyActionV2.Up(InputNameResolverV2.Resolve("q"))
    ]));

    var down = await session.TriggerDownAsync();
    AssertRuntimeV2NoErrors(down, "trigger down should execute simple hold on_down.");
    AssertTrue(session.IsHeld, "session should be held after trigger down.");
    AssertTrue(ledger.HeldResources.Count == 1, "down_key should acquire ownership.");
    AssertRuntimeV2Event(backend.Events.Single(), RuntimeV2EventKind.KeyDown, "q",
        "trigger down should record key_down q.");

    var up = await session.TriggerUpAsync();
    AssertRuntimeV2NoErrors(up, "trigger up should execute on_up and cleanup.");
    AssertTrue(session.State == MappingActivationSessionStateV2.Completed,
        "trigger up should complete the session.");
    AssertTrue(ledger.HeldResources.Count == 0, "up_key should release ownership.");
    AssertRuntimeV2Event(backend.Events[1], RuntimeV2EventKind.KeyUp, "q",
        "trigger up should record key_up q.");
}

static async Task RuntimeV2OnUpRunsOnlyOnTriggerUp()
{
    var (backend, _, session) = RuntimeV2Session(new ActivationPlanV2(
        OnDown: [],
        OnUp:
        [
            KeyActionV2.Tap(InputNameResolverV2.Resolve("escape"))
        ]));

    var down = await session.TriggerDownAsync();
    AssertRuntimeV2NoErrors(down, "empty on_down should start the session.");
    AssertTrue(backend.Events.Count == 0, "on_up should not execute during trigger down.");

    var up = await session.TriggerUpAsync();
    AssertRuntimeV2NoErrors(up, "trigger up should execute on_up.");
    AssertRuntimeV2Event(backend.Events.Single(), RuntimeV2EventKind.KeyTap, "escape",
        "on_up tap_key escape should execute only on trigger up.");
}

static async Task RuntimeV2CancelCleanupDoesNotRunOnUp()
{
    var (backend, ledger, session) = RuntimeV2Session(new ActivationPlanV2(
    [
        KeyActionV2.Down(InputNameResolverV2.Resolve("q"))
    ],
    OnUp:
    [
        KeyActionV2.Tap(InputNameResolverV2.Resolve("escape"))
    ]));

    await session.TriggerDownAsync();
    var cancel = await session.CancelAsync(MappingActivationCancelReasonV2.Stop);

    AssertRuntimeV2NoErrors(cancel, "cancel cleanup should complete without errors.");
    AssertTrue(session.State == MappingActivationSessionStateV2.Cancelled,
        "cancel should mark the session cancelled.");
    AssertTrue(ledger.HeldResources.Count == 0, "cancel cleanup should release held resources.");
    AssertTrue(!backend.Events.Any(evt => evt.Kind == RuntimeV2EventKind.KeyTap &&
        evt.CanonicalInputName == "escape"), "cancel must not execute on_up.");
    var cleanup = backend.Events.Single(evt => evt.Kind == RuntimeV2EventKind.KeyUp);
    AssertTrue(cleanup.IsCleanup, "cancel release should be marked as cleanup.");
    AssertRuntimeV2Event(cleanup, RuntimeV2EventKind.KeyUp, "q",
        "cancel cleanup should release held key q.");
}

static async Task RuntimeV2WhileHeldZeroIntervalRunsBoundedIterations()
{
    var (backend, _, session) = RuntimeV2Session(
        new ActivationPlanV2(
            OnDown: [],
            WhileHeld: new WhileHeldBlockV2(
                TimeSpan.Zero,
                [KeyActionV2.Tap(InputNameResolverV2.Resolve("1"))])),
        new RuntimeV2ExecutionOptions
        {
            MaxWhileHeldIterations = 3,
            MaxActionSteps = 20
        });

    await session.TriggerDownAsync();
    var whileHeld = await session.RunWhileHeldIterationsAsync(3);

    AssertRuntimeV2NoErrors(whileHeld, "while_held interval 0ms should run bounded iterations.");
    AssertTrue(backend.Events.Count(evt => evt.Kind == RuntimeV2EventKind.KeyTap &&
            evt.CanonicalInputName == "1") == 3,
        "three bounded while_held iterations should record three tap events.");
    AssertTrue(!backend.Events.Any(evt => evt.Kind == RuntimeV2EventKind.Wait),
        "0ms while_held interval should not require a fake wait event.");

    var overBudget = await session.RunWhileHeldIterationsAsync(4);
    AssertRuntimeV2Diagnostic(overBudget.Diagnostics, RuntimeV2DiagnosticCode.InvalidWhileHeldIterationCount,
        "while_held iteration requests over the sandbox option should be rejected.");
}

static async Task RuntimeV2FiniteRepeatRecordsDeterministicOrder()
{
    var (backend, _, session) = RuntimeV2Session(new ActivationPlanV2(
    [
        new RepeatActionV2(
            3,
            [KeyActionV2.Tap(InputNameResolverV2.Resolve("a"))])
    ]));

    var result = await session.TriggerDownAsync();

    AssertRuntimeV2NoErrors(result, "finite repeat should execute in the fake sandbox.");
    AssertTrue(backend.Events.Count == 3, "repeat count 3 should record three events.");
    AssertTrue(backend.Events.All(evt => evt.Kind == RuntimeV2EventKind.KeyTap &&
            evt.CanonicalInputName == "a"),
        "finite repeat should record key_tap a for every iteration.");
    AssertRuntimeV2SequenceIsStable(backend.Events);
}

static async Task RuntimeV2WaitUsesFakeVirtualTime()
{
    var (backend, _, session) = RuntimeV2Session(new ActivationPlanV2(
    [
        new WaitActionV2(TimeSpan.FromMilliseconds(25)),
        KeyActionV2.Tap(InputNameResolverV2.Resolve("a"))
    ]));

    var result = await session.TriggerDownAsync();

    AssertRuntimeV2NoErrors(result, "wait should execute through the fake scheduler model.");
    AssertTrue(backend.VirtualTime == TimeSpan.FromMilliseconds(25),
        "fake backend should advance virtual time for wait without real delay.");
    AssertTrue(backend.Events[0].Kind == RuntimeV2EventKind.Wait &&
            backend.Events[0].Duration == TimeSpan.FromMilliseconds(25),
        "wait action should record a fake wait event.");
    AssertRuntimeV2Event(backend.Events[1], RuntimeV2EventKind.KeyTap, "a",
        "action after wait should still execute deterministically.");
}

static async Task RuntimeV2CleanupReleasesHeldKeyAndMouse()
{
    var (backend, ledger, session) = RuntimeV2Session(new ActivationPlanV2(
    [
        KeyActionV2.Down(InputNameResolverV2.Resolve("q")),
        MouseButtonActionV2.Down(InputNameResolverV2.Resolve("mouse_left"))
    ]));

    await session.TriggerDownAsync();
    AssertTrue(ledger.HeldResources.Count == 2, "key and mouse down should be held before cleanup.");

    var cancel = await session.CancelAsync(MappingActivationCancelReasonV2.Reload);

    AssertRuntimeV2NoErrors(cancel, "cleanup should release held key and mouse resources.");
    AssertTrue(ledger.HeldResources.Count == 0, "cleanup should clear the ledger.");
    AssertTrue(backend.Events.Any(evt => evt.Kind == RuntimeV2EventKind.KeyUp &&
            evt.CanonicalInputName == "q" &&
            evt.IsCleanup),
        "cleanup should record key_up q.");
    AssertTrue(backend.Events.Any(evt => evt.Kind == RuntimeV2EventKind.MouseUp &&
            evt.CanonicalInputName == "mouse_left" &&
            evt.IsCleanup),
        "cleanup should record mouse_up mouse_left.");
}

static async Task RuntimeV2DuplicateAcquireWarnsWithoutDuplicateDown()
{
    var (backend, ledger, session) = RuntimeV2Session(new ActivationPlanV2(
    [
        KeyActionV2.Down(InputNameResolverV2.Resolve("q")),
        KeyActionV2.Down(InputNameResolverV2.Resolve("q"))
    ]));

    var result = await session.TriggerDownAsync();

    AssertRuntimeV2Diagnostic(result.Diagnostics, RuntimeV2DiagnosticCode.DuplicateAcquire,
        "duplicate acquire should produce a warning diagnostic.");
    AssertTrue(!result.HasErrors, "duplicate acquire warning should not be an execution error.");
    AssertTrue(backend.Events.Count(evt => evt.Kind == RuntimeV2EventKind.KeyDown &&
            evt.CanonicalInputName == "q") == 1,
        "duplicate acquire should not record a second key_down.");
    AssertTrue(ledger.HeldResources.Count == 1, "duplicate acquire should keep one held resource.");
}

static async Task RuntimeV2ReleaseWithoutAcquireWarnsWithoutFakeUp()
{
    var (backend, _, session) = RuntimeV2Session(new ActivationPlanV2(
    [
        KeyActionV2.Up(InputNameResolverV2.Resolve("q"))
    ]));

    var result = await session.TriggerDownAsync();

    AssertRuntimeV2Diagnostic(result.Diagnostics, RuntimeV2DiagnosticCode.ReleaseWithoutAcquire,
        "release without acquire should produce a warning diagnostic.");
    AssertTrue(!result.HasErrors, "release without acquire warning should not be an execution error.");
    AssertTrue(!backend.Events.Any(evt => evt.Kind == RuntimeV2EventKind.KeyUp),
        "release without acquire should not record a fake key_up event.");
}

static async Task RuntimeV2CoordinateActionRecordsUnsupported()
{
    var point = new CoordinatePointV2(120, 240, "ba16_1920x1080");
    var (backend, _, session) = RuntimeV2Session(new ActivationPlanV2(
    [
        CoordinateActionV2.TapAt(point)
    ]));

    var result = await session.TriggerDownAsync();

    AssertRuntimeV2Diagnostic(result.Diagnostics, RuntimeV2DiagnosticCode.CoordinateUnsupported,
        "coordinate actions should report unsupported in the Core-only sandbox.");
    var evt = backend.Events.Single();
    AssertTrue(evt.Kind == RuntimeV2EventKind.CoordinateUnsupported &&
            evt.CoordinateProfileId == "ba16_1920x1080" &&
            evt.LogicalX == 120 &&
            evt.LogicalY == 240,
        "coordinate action should record an unsupported coordinate event without live execution.");
}

static async Task RuntimeV2InvalidPlanDoesNotExecute()
{
    var (backend, _, session) = RuntimeV2Session(new ActivationPlanV2(
    [
        KeyActionV2.Tap(InputNameResolverV2.Resolve("mouse_left"))
    ]));

    var result = await session.TriggerDownAsync();

    AssertRuntimeV2Diagnostic(result.Diagnostics, RuntimeV2DiagnosticCode.ActionPlanInvalid,
        "invalid action plan should be rejected before fake execution.");
    AssertTrue(result.HasErrors, "invalid action plan should be an execution error.");
    AssertTrue(backend.Events.Count == 0, "invalid action plan must not record fake backend events.");
    AssertTrue(session.State == MappingActivationSessionStateV2.Created,
        "invalid trigger down should not start the session.");
}

static async Task RuntimeV2PreservesValidationWarningAndInfoDiagnostics()
{
    var emptyWhileHeld = RuntimeV2Session(new ActivationPlanV2(
        OnDown:
        [
            KeyActionV2.Tap(InputNameResolverV2.Resolve("a"))
        ],
        WhileHeld: new WhileHeldBlockV2(TimeSpan.Zero, [])));

    var emptyResult = await emptyWhileHeld.Session.TriggerDownAsync();
    AssertRuntimeV2NoErrors(emptyResult, "validation warning-only plan should still execute.");
    AssertRuntimeV2Diagnostic(
        emptyResult.Diagnostics,
        RuntimeV2DiagnosticCode.ActionValidationDiagnostic,
        "empty while_held body warning should be preserved by Runtime v2 execution result.");
    AssertRuntimeV2Event(emptyWhileHeld.Backend.Events.Single(), RuntimeV2EventKind.KeyTap, "a",
        "warning-only plan should still record on_down fake event.");

    var coordinatePoint = new CoordinatePointV2(10, 20, "ba16_1920x1080");
    var coordinate = RuntimeV2Session(new ActivationPlanV2(
    [
        CoordinateActionV2.TapAt(coordinatePoint)
    ]));
    var coordinateResult = await coordinate.Session.TriggerDownAsync();
    AssertRuntimeV2Diagnostic(
        coordinateResult.Diagnostics,
        RuntimeV2DiagnosticCode.ActionValidationDiagnostic,
        "CoordinateActionNotLiveReady info should be preserved by Runtime v2 execution result.");
    AssertRuntimeV2Diagnostic(
        coordinateResult.Diagnostics,
        RuntimeV2DiagnosticCode.CoordinateUnsupported,
        "coordinate execution-time unsupported warning should still be emitted.");
}

static async Task RuntimeV2ExecutionOptionsRejectInvalidValues()
{
    AssertTrue(RuntimeV2ExecutionOptions.Default.IsValid, "default Runtime v2 execution options should be valid.");

    foreach (var maxSteps in new[] { 0, -1 })
    {
        var options = new RuntimeV2ExecutionOptions { MaxActionSteps = maxSteps };
        var (backend, _, session) = RuntimeV2Session(
            new ActivationPlanV2([KeyActionV2.Tap(InputNameResolverV2.Resolve("a"))]),
            options);

        var result = await session.TriggerDownAsync();
        AssertRuntimeV2Diagnostic(
            result.Diagnostics,
            RuntimeV2DiagnosticCode.InvalidMaxActionSteps,
            "invalid MaxActionSteps should produce an options diagnostic.");
        AssertTrue(result.HasErrors, "invalid MaxActionSteps should be an error.");
        AssertTrue(backend.Events.Count == 0, "invalid options must not execute fake events.");
    }

    var invalidIterations = new RuntimeV2ExecutionOptions { MaxWhileHeldIterations = -1 };
    var invalidIterationSession = RuntimeV2Session(
        new ActivationPlanV2([KeyActionV2.Tap(InputNameResolverV2.Resolve("a"))]),
        invalidIterations);
    var iterationResult = await invalidIterationSession.Session.TriggerDownAsync();
    AssertRuntimeV2Diagnostic(
        iterationResult.Diagnostics,
        RuntimeV2DiagnosticCode.InvalidMaxWhileHeldIterations,
        "negative MaxWhileHeldIterations should produce a distinct options diagnostic.");
    AssertTrue(invalidIterationSession.Backend.Events.Count == 0,
        "invalid while-held iteration option must not execute fake events.");
}

static async Task RuntimeV2NestedRepeatBudgetExhaustionStopsAndCleanupReleases()
{
    var (backend, ledger, session) = RuntimeV2Session(
        new ActivationPlanV2(
        [
            KeyActionV2.Down(InputNameResolverV2.Resolve("q")),
            new RepeatActionV2(
                5,
                [KeyActionV2.Tap(InputNameResolverV2.Resolve("a"))])
        ]),
        new RuntimeV2ExecutionOptions { MaxActionSteps = 3 });

    var down = await session.TriggerDownAsync();
    AssertRuntimeV2Diagnostic(
        down.Diagnostics,
        RuntimeV2DiagnosticCode.ActionStepBudgetExceeded,
        "nested repeat should report budget exhaustion.");
    AssertTrue(down.HasErrors, "budget exhaustion should be an execution error.");
    AssertTrue(backend.Events.Count(evt => evt.Kind == RuntimeV2EventKind.KeyDown &&
            evt.CanonicalInputName == "q") == 1,
        "budget test should acquire q once before exhaustion.");
    AssertTrue(backend.Events.Count(evt => evt.Kind == RuntimeV2EventKind.KeyTap &&
            evt.CanonicalInputName == "a") == 1,
        "event log should stop at the action budget boundary.");
    AssertTrue(ledger.HeldResources.Count == 1, "held key should remain in ledger until cleanup.");

    var cleanup = await session.CancelAsync(MappingActivationCancelReasonV2.Cancel);
    AssertRuntimeV2NoErrors(cleanup, "cleanup after budget exhaustion should complete.");
    AssertTrue(ledger.HeldResources.Count == 0, "cleanup should release the held key after budget exhaustion.");
    AssertTrue(backend.Events.Any(evt => evt.Kind == RuntimeV2EventKind.KeyUp &&
            evt.CanonicalInputName == "q" &&
            evt.IsCleanup),
        "cleanup after budget exhaustion should record cleanup key_up q.");
}

static async Task RuntimeV2AlreadyCancelledExecutionDoesNotEmitNormalEvents()
{
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    var (backend, _, session) = RuntimeV2Session(
        new ActivationPlanV2([KeyActionV2.Tap(InputNameResolverV2.Resolve("a"))]),
        new RuntimeV2ExecutionOptions { CancellationToken = cancellation.Token });

    var result = await session.TriggerDownAsync();

    AssertRuntimeV2Diagnostic(
        result.Diagnostics,
        RuntimeV2DiagnosticCode.ExecutionCancelled,
        "already-cancelled token should produce a cancellation diagnostic.");
    AssertTrue(result.HasErrors, "already-cancelled execution should not be treated as successful.");
    AssertTrue(backend.Events.Count == 0, "already-cancelled execution must not emit normal fake events.");
    AssertTrue(session.State == MappingActivationSessionStateV2.Created,
        "already-cancelled trigger down should not start the session.");
}

static async Task RuntimeV2PositiveWhileHeldIntervalRecordsFakeWait()
{
    var (backend, _, session) = RuntimeV2Session(new ActivationPlanV2(
        OnDown: [],
        WhileHeld: new WhileHeldBlockV2(
            TimeSpan.FromMilliseconds(15),
            [KeyActionV2.Tap(InputNameResolverV2.Resolve("1"))])));

    await session.TriggerDownAsync();
    var result = await session.RunWhileHeldIterationsAsync(2);

    AssertRuntimeV2NoErrors(result, "positive interval while_held should execute bounded iterations.");
    AssertTrue(backend.Events.Count(evt => evt.Kind == RuntimeV2EventKind.KeyTap &&
            evt.CanonicalInputName == "1") == 2,
        "positive interval while_held should record each body iteration.");
    AssertTrue(backend.Events.Count(evt => evt.Kind == RuntimeV2EventKind.Wait &&
            evt.Duration == TimeSpan.FromMilliseconds(15)) == 2,
        "positive interval while_held should record one fake wait after each bounded iteration.");
    AssertTrue(backend.VirtualTime == TimeSpan.FromMilliseconds(30),
        "positive while_held interval should advance deterministic fake virtual time.");
}

static async Task RuntimeV2CoordinateContactCleanupRemainsUnsupportedButDeterministic()
{
    var point = new CoordinatePointV2(120, 240, "ba16_1920x1080");
    var (backend, ledger, session) = RuntimeV2Session(new ActivationPlanV2(
    [
        CoordinateActionV2.DownAt(point)
    ]));

    var down = await session.TriggerDownAsync();

    AssertRuntimeV2Diagnostic(down.Diagnostics, RuntimeV2DiagnosticCode.CoordinateUnsupported,
        "down_at should remain unsupported in Runtime v2 sandbox execution.");
    AssertTrue(ledger.HeldResources.Count == 1, "down_at should acquire a coordinate contact in the sandbox ledger.");
    AssertTrue(backend.Events.Any(evt => evt.Kind == RuntimeV2EventKind.CoordinateUnsupported &&
            !evt.IsCleanup &&
            evt.LogicalX == 120 &&
            evt.LogicalY == 240),
        "down_at should record an unsupported coordinate event.");

    var cleanup = await session.CancelAsync(MappingActivationCancelReasonV2.ForegroundLost);
    AssertRuntimeV2NoErrors(cleanup, "coordinate contact cleanup should complete in the fake sandbox.");
    AssertTrue(ledger.HeldResources.Count == 0, "coordinate contact cleanup should clear the ledger.");
    AssertTrue(backend.Events.Any(evt => evt.Kind == RuntimeV2EventKind.CoordinateUnsupported &&
            evt.IsCleanup &&
            evt.LogicalX == 120 &&
            evt.LogicalY == 240),
        "coordinate contact cleanup should record a deterministic cleanup unsupported event.");
}

static async Task RuntimeV2TriggerUpCleanupRunsAfterOnUpBudgetFailure()
{
    var (backend, ledger, session) = RuntimeV2Session(
        new ActivationPlanV2(
            OnDown:
            [
                KeyActionV2.Down(InputNameResolverV2.Resolve("q"))
            ],
            OnUp:
            [
                new RepeatActionV2(
                    5,
                    [KeyActionV2.Tap(InputNameResolverV2.Resolve("a"))])
            ]),
        new RuntimeV2ExecutionOptions { MaxActionSteps = 2 });

    var down = await session.TriggerDownAsync();
    AssertRuntimeV2NoErrors(down, "trigger down should acquire q before on_up budget failure test.");

    var up = await session.TriggerUpAsync();

    AssertRuntimeV2Diagnostic(up.Diagnostics, RuntimeV2DiagnosticCode.ActionStepBudgetExceeded,
        "trigger up should report on_up budget exhaustion.");
    AssertTrue(up.HasErrors, "on_up budget exhaustion should be visible in trigger up result.");
    AssertTrue(ledger.HeldResources.Count == 0, "trigger up cleanup should run even when on_up fails.");
    AssertTrue(backend.Events.Any(evt => evt.Kind == RuntimeV2EventKind.KeyUp &&
            evt.CanonicalInputName == "q" &&
            evt.IsCleanup),
        "trigger up cleanup should release q even after on_up budget exhaustion.");
}

static async Task RuntimeV2ManagerTriggerDownStartsSession()
{
    var manager = new MappingActivationSessionManagerV2();
    var result = await manager.TriggerDownAsync(
        "skill_1",
        new ActivationPlanV2([KeyActionV2.Tap(InputNameResolverV2.Resolve("a"))]));

    AssertRuntimeV2ManagerNoErrors(result, "trigger down should start a Runtime v2 manager session.");
    AssertTrue(manager.ActiveSessionCount == 1, "trigger down should create one active session.");
    AssertTrue(manager.ActiveMappingIds.SequenceEqual(["skill_1"]),
        "manager should expose active mapping ids.");
    AssertRuntimeV2Event(manager.Backend.Events.Single(), RuntimeV2EventKind.KeyTap, "a",
        "trigger down should execute on_down.");
    AssertTrue(manager.Backend.Events.Single().MappingId == "skill_1",
        "manager event log should tag the mapping id.");
}

static async Task RuntimeV2ManagerIgnoresSameMappingTriggerDown()
{
    var manager = new MappingActivationSessionManagerV2();
    var plan = new ActivationPlanV2([KeyActionV2.Tap(InputNameResolverV2.Resolve("a"))]);

    await manager.TriggerDownAsync("skill_1", plan);
    var duplicate = await manager.TriggerDownAsync("skill_1", plan);

    AssertRuntimeV2Diagnostic(
        duplicate.Diagnostics,
        RuntimeV2DiagnosticCode.MappingAlreadyRunning,
        "same mapping second trigger down should return a diagnostic.");
    AssertTrue(!duplicate.HasErrors, "same mapping ignore_when_running should be warning/info, not error.");
    AssertTrue(manager.ActiveSessionCount == 1, "same mapping second trigger down must not create another session.");
    AssertTrue(manager.Backend.Events.Count(evt => evt.Kind == RuntimeV2EventKind.KeyTap &&
            evt.CanonicalInputName == "a") == 1,
        "same mapping second trigger down must not repeat on_down.");
}

static async Task RuntimeV2ManagerTriggerUpEndsSessionAndUnknownWarns()
{
    var manager = new MappingActivationSessionManagerV2();
    var plan = new ActivationPlanV2(
        OnDown: [],
        OnUp:
        [
            KeyActionV2.Tap(InputNameResolverV2.Resolve("escape"))
        ]);

    await manager.TriggerDownAsync("skill_1", plan);
    var up = await manager.TriggerUpAsync("skill_1");

    AssertRuntimeV2ManagerNoErrors(up, "trigger up should execute on_up and remove the active session.");
    AssertTrue(manager.ActiveSessionCount == 0, "trigger up should remove session from active set.");
    AssertRuntimeV2Event(manager.Backend.Events.Single(), RuntimeV2EventKind.KeyTap, "escape",
        "trigger up should execute on_up.");

    var unknown = await manager.TriggerUpAsync("missing");
    AssertRuntimeV2Diagnostic(unknown.Diagnostics, RuntimeV2DiagnosticCode.NoActiveSession,
        "trigger up for unknown mapping should return a controlled warning.");
}

static async Task RuntimeV2ManagerCancelMappingDoesNotExecuteOnUpAndCleansHeldKey()
{
    var manager = new MappingActivationSessionManagerV2();
    var plan = new ActivationPlanV2(
        OnDown:
        [
            KeyActionV2.Down(InputNameResolverV2.Resolve("q"))
        ],
        OnUp:
        [
            KeyActionV2.Tap(InputNameResolverV2.Resolve("escape"))
        ]);

    await manager.TriggerDownAsync("skill_1", plan);
    var cancel = await manager.CancelAsync("skill_1", MappingActivationCancelReasonV2.Cancel);

    AssertRuntimeV2ManagerNoErrors(cancel, "cancel mapping should cleanup without errors.");
    AssertTrue(manager.ActiveSessionCount == 0, "cancel should remove active session.");
    AssertTrue(!manager.Backend.Events.Any(evt => evt.Kind == RuntimeV2EventKind.KeyTap &&
            evt.CanonicalInputName == "escape"),
        "cancel mapping must not execute on_up.");
    AssertTrue(manager.Backend.Events.Any(evt => evt.Kind == RuntimeV2EventKind.KeyUp &&
            evt.CanonicalInputName == "q" &&
            evt.MappingId == "skill_1" &&
            evt.IsCleanup),
        "cancel mapping should release held key as cleanup.");

    var unknown = await manager.CancelAsync("missing", MappingActivationCancelReasonV2.Cancel);
    AssertRuntimeV2Diagnostic(unknown.Diagnostics, RuntimeV2DiagnosticCode.NoActiveSession,
        "cancel unknown mapping should return a controlled warning.");
}

static async Task RuntimeV2ManagerCancelAllCleansMultipleSessions()
{
    var manager = new MappingActivationSessionManagerV2();
    await manager.TriggerDownAsync("skill_a", new ActivationPlanV2(
    [
        KeyActionV2.Down(InputNameResolverV2.Resolve("q"))
    ]));
    await manager.TriggerDownAsync("skill_b", new ActivationPlanV2(
    [
        MouseButtonActionV2.Down(InputNameResolverV2.Resolve("mouse_left"))
    ]));

    var cancelAll = await manager.CancelAllAsync(MappingActivationCancelReasonV2.Reload);

    AssertRuntimeV2ManagerNoErrors(cancelAll, "cancel all should cleanup all active sessions.");
    AssertTrue(manager.ActiveSessionCount == 0, "cancel all should remove every active session.");
    AssertTrue(manager.Backend.Events.Any(evt => evt.Kind == RuntimeV2EventKind.KeyUp &&
            evt.CanonicalInputName == "q" &&
            evt.MappingId == "skill_a" &&
            evt.IsCleanup),
        "cancel all should cleanup skill_a key.");
    AssertTrue(manager.Backend.Events.Any(evt => evt.Kind == RuntimeV2EventKind.MouseUp &&
            evt.CanonicalInputName == "mouse_left" &&
            evt.MappingId == "skill_b" &&
            evt.IsCleanup),
        "cancel all should cleanup skill_b mouse button.");
}

static async Task RuntimeV2ManagerKeepsDifferentMappingsIsolated()
{
    var manager = new MappingActivationSessionManagerV2();
    await manager.TriggerDownAsync(
        "skill_a",
        new ActivationPlanV2(
            OnDown:
            [
                KeyActionV2.Down(InputNameResolverV2.Resolve("q"))
            ],
            OnUp:
            [
                KeyActionV2.Up(InputNameResolverV2.Resolve("q"))
            ]));
    await manager.TriggerDownAsync(
        "skill_b",
        new ActivationPlanV2(
            OnDown:
            [
                MouseButtonActionV2.Down(InputNameResolverV2.Resolve("mouse_left"))
            ],
            OnUp:
            [
                MouseButtonActionV2.Up(InputNameResolverV2.Resolve("mouse_left"))
            ]));

    AssertTrue(manager.ActiveSessionCount == 2, "different mappings should run concurrently in the sandbox manager.");
    var upA = await manager.TriggerUpAsync("skill_a");

    AssertRuntimeV2ManagerNoErrors(upA, "trigger up A should complete only A.");
    AssertTrue(manager.ActiveSessionCount == 1 && manager.ActiveMappingIds.Single() == "skill_b",
        "trigger up A should leave B active.");
    AssertTrue(manager.Backend.Events.Any(evt => evt.Kind == RuntimeV2EventKind.KeyUp &&
            evt.CanonicalInputName == "q" &&
            evt.MappingId == "skill_a"),
        "trigger up A should release A key.");
    AssertTrue(!manager.Backend.Events.Any(evt => evt.Kind == RuntimeV2EventKind.MouseUp &&
            evt.CanonicalInputName == "mouse_left"),
        "trigger up A must not cleanup B mouse button.");
}

static async Task RuntimeV2ManagerWhileHeldRoutingOnlyAdvancesSelectedMapping()
{
    var manager = new MappingActivationSessionManagerV2();
    await manager.TriggerDownAsync(
        "skill_a",
        new ActivationPlanV2(
            OnDown: [],
            WhileHeld: new WhileHeldBlockV2(
                TimeSpan.Zero,
                [KeyActionV2.Tap(InputNameResolverV2.Resolve("1"))])));
    await manager.TriggerDownAsync(
        "skill_b",
        new ActivationPlanV2(
            OnDown: [],
            WhileHeld: new WhileHeldBlockV2(
                TimeSpan.Zero,
                [KeyActionV2.Tap(InputNameResolverV2.Resolve("2"))])));

    var result = await manager.RunWhileHeldIterationsAsync("skill_a", 2);

    AssertRuntimeV2ManagerNoErrors(result, "manager should route while_held iterations to the selected mapping.");
    AssertTrue(manager.ActiveSessionCount == 2, "while_held routing should keep both sessions active.");
    AssertTrue(manager.Backend.Events.Count(evt => evt.Kind == RuntimeV2EventKind.KeyTap &&
            evt.CanonicalInputName == "1" &&
            evt.MappingId == "skill_a") == 2,
        "selected mapping A should emit its while_held body.");
    AssertTrue(!manager.Backend.Events.Any(evt => evt.Kind == RuntimeV2EventKind.KeyTap &&
            evt.CanonicalInputName == "2"),
        "mapping B should not advance when only mapping A while_held is routed.");
}

static async Task RuntimeV2ManagerLifecycleCancellationStillCleansUp()
{
    var whileHeldManager = new MappingActivationSessionManagerV2();
    await whileHeldManager.TriggerDownAsync(
        "skill_while",
        new ActivationPlanV2(
            OnDown: [],
            WhileHeld: new WhileHeldBlockV2(
                TimeSpan.Zero,
                [KeyActionV2.Tap(InputNameResolverV2.Resolve("1"))])));
    using var whileHeldCancellation = new CancellationTokenSource();
    whileHeldCancellation.Cancel();

    var whileHeld = await whileHeldManager.RunWhileHeldIterationsAsync(
        "skill_while",
        1,
        whileHeldCancellation.Token);

    AssertRuntimeV2Diagnostic(whileHeld.Diagnostics, RuntimeV2DiagnosticCode.ExecutionCancelled,
        "cancelled while_held routing should return a controlled cancellation diagnostic.");
    AssertTrue(whileHeld.Diagnostics.All(diagnostic => diagnostic.MappingId == "skill_while"),
        "cancelled while_held diagnostic should carry mapping id.");
    AssertTrue(!whileHeldManager.Backend.Events.Any(evt => evt.Kind == RuntimeV2EventKind.KeyTap),
        "cancelled while_held routing should not emit normal while_held events.");
    AssertTrue(whileHeldManager.ActiveSessionCount == 1,
        "cancelled while_held routing should leave the active session in place.");

    var triggerUpManager = new MappingActivationSessionManagerV2();
    await triggerUpManager.TriggerDownAsync(
        "skill_up",
        new ActivationPlanV2(
            OnDown:
            [
                KeyActionV2.Down(InputNameResolverV2.Resolve("q"))
            ],
            OnUp:
            [
                KeyActionV2.Tap(InputNameResolverV2.Resolve("escape"))
            ]));
    using var triggerUpCancellation = new CancellationTokenSource();
    triggerUpCancellation.Cancel();

    var triggerUp = await triggerUpManager.TriggerUpAsync("skill_up", triggerUpCancellation.Token);

    AssertRuntimeV2Diagnostic(triggerUp.Diagnostics, RuntimeV2DiagnosticCode.ExecutionCancelled,
        "cancelled trigger up should return a controlled cancellation diagnostic.");
    AssertTrue(triggerUp.ActiveMappingIds is { Count: 0 },
        "cancelled trigger up should still remove the active session after cleanup.");
    AssertTrue(!triggerUpManager.Backend.Events.Any(evt => evt.Kind == RuntimeV2EventKind.KeyTap &&
            evt.CanonicalInputName == "escape"),
        "cancelled trigger up must not execute on_up.");
    AssertTrue(triggerUpManager.Backend.Events.Any(evt => evt.Kind == RuntimeV2EventKind.KeyUp &&
            evt.CanonicalInputName == "q" &&
            evt.MappingId == "skill_up" &&
            evt.IsCleanup),
        "cancelled trigger up should still cleanup held key q.");

    var cancelManager = new MappingActivationSessionManagerV2();
    await cancelManager.TriggerDownAsync(
        "skill_cancel",
        new ActivationPlanV2(
            OnDown:
            [
                KeyActionV2.Down(InputNameResolverV2.Resolve("e"))
            ],
            OnUp:
            [
                KeyActionV2.Tap(InputNameResolverV2.Resolve("escape"))
            ]));
    using var cancelCancellation = new CancellationTokenSource();
    cancelCancellation.Cancel();

    var cancel = await cancelManager.CancelAsync(
        "skill_cancel",
        MappingActivationCancelReasonV2.Cancel,
        cancelCancellation.Token);

    AssertRuntimeV2Diagnostic(cancel.Diagnostics, RuntimeV2DiagnosticCode.ExecutionCancelled,
        "cancelled cancel request should surface cancellation diagnostic.");
    AssertTrue(cancel.ActiveMappingIds is { Count: 0 },
        "cancel should remove the active session even when the lifecycle token is already cancelled.");
    AssertTrue(!cancelManager.Backend.Events.Any(evt => evt.Kind == RuntimeV2EventKind.KeyTap &&
            evt.CanonicalInputName == "escape"),
        "cancel must not execute on_up when the lifecycle token is already cancelled.");
    AssertTrue(cancelManager.Backend.Events.Any(evt => evt.Kind == RuntimeV2EventKind.KeyUp &&
            evt.CanonicalInputName == "e" &&
            evt.MappingId == "skill_cancel" &&
            evt.IsCleanup),
        "cancel should still cleanup held key e.");
}

static async Task RuntimeV2ManagerCancelAllCancellationPreservesMappingDiagnostics()
{
    var manager = new MappingActivationSessionManagerV2();
    await manager.TriggerDownAsync("skill_a", new ActivationPlanV2(
    [
        KeyActionV2.Down(InputNameResolverV2.Resolve("q"))
    ]));
    await manager.TriggerDownAsync("skill_b", new ActivationPlanV2(
    [
        KeyActionV2.Down(InputNameResolverV2.Resolve("e"))
    ]));
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();

    var cancelAll = await manager.CancelAllAsync(MappingActivationCancelReasonV2.Reload, cancellation.Token);

    AssertRuntimeV2Diagnostic(cancelAll.Diagnostics, RuntimeV2DiagnosticCode.ExecutionCancelled,
        "cancel all should preserve cancellation diagnostics.");
    AssertTrue(cancelAll.Diagnostics.Any(diagnostic =>
            diagnostic.Code == RuntimeV2DiagnosticCode.ExecutionCancelled &&
            diagnostic.MappingId == "skill_a") &&
        cancelAll.Diagnostics.Any(diagnostic =>
            diagnostic.Code == RuntimeV2DiagnosticCode.ExecutionCancelled &&
            diagnostic.MappingId == "skill_b"),
        "cancel all aggregate diagnostics should preserve per-mapping attribution.");
    AssertTrue(manager.ActiveSessionCount == 0, "cancel all should cleanup and remove every active session.");
    AssertTrue(manager.Backend.Events.Any(evt => evt.Kind == RuntimeV2EventKind.KeyUp &&
            evt.CanonicalInputName == "q" &&
            evt.MappingId == "skill_a" &&
            evt.IsCleanup),
        "cancel all should cleanup skill_a key q.");
    AssertTrue(manager.Backend.Events.Any(evt => evt.Kind == RuntimeV2EventKind.KeyUp &&
            evt.CanonicalInputName == "e" &&
            evt.MappingId == "skill_b" &&
            evt.IsCleanup),
        "cancel all should cleanup skill_b key e.");
}

static async Task RuntimeV2ManagerUnknownWhileHeldMappingWarns()
{
    var manager = new MappingActivationSessionManagerV2();

    var result = await manager.RunWhileHeldIterationsAsync("missing", 1);

    AssertRuntimeV2Diagnostic(result.Diagnostics, RuntimeV2DiagnosticCode.NoActiveSession,
        "unknown while_held routing should return a controlled warning.");
    AssertTrue(result.Diagnostics.Single().MappingId == "missing",
        "unknown while_held diagnostic should include attempted mapping id.");
    AssertTrue(manager.ActiveSessionCount == 0, "unknown while_held routing should not change active sessions.");
    AssertTrue(manager.Backend.Events.Count == 0, "unknown while_held routing should not emit fake events.");
}

static async Task RuntimeV2ManagerHeldKeyIsolationUsesIndependentLedgers()
{
    var manager = new MappingActivationSessionManagerV2();
    await manager.TriggerDownAsync("skill_a", new ActivationPlanV2(
    [
        KeyActionV2.Down(InputNameResolverV2.Resolve("q"))
    ]));
    await manager.TriggerDownAsync("skill_b", new ActivationPlanV2(
    [
        KeyActionV2.Down(InputNameResolverV2.Resolve("e"))
    ]));

    var upA = await manager.TriggerUpAsync("skill_a");

    AssertRuntimeV2ManagerNoErrors(upA, "trigger up A should cleanup only A.");
    AssertTrue(manager.ActiveSessionCount == 1 && manager.ActiveMappingIds.Single() == "skill_b",
        "trigger up A should leave B active.");
    AssertTrue(manager.Backend.Events.Any(evt => evt.Kind == RuntimeV2EventKind.KeyUp &&
            evt.CanonicalInputName == "q" &&
            evt.MappingId == "skill_a" &&
            evt.IsCleanup),
        "A cleanup should release q with A mapping context.");
    AssertTrue(!manager.Backend.Events.Any(evt => evt.Kind == RuntimeV2EventKind.KeyUp &&
            evt.CanonicalInputName == "e"),
        "A cleanup must not release B key e.");

    var upB = await manager.TriggerUpAsync("skill_b");

    AssertRuntimeV2ManagerNoErrors(upB, "trigger up B should cleanup B after A has completed.");
    AssertTrue(manager.Backend.Events.Any(evt => evt.Kind == RuntimeV2EventKind.KeyUp &&
            evt.CanonicalInputName == "e" &&
            evt.MappingId == "skill_b" &&
            evt.IsCleanup),
        "B cleanup should release e only when B ends.");
}

static async Task RuntimeV2ManagerInvalidPlanOrOptionsDoNotCreateSession()
{
    var manager = new MappingActivationSessionManagerV2();
    var invalidPlan = await manager.TriggerDownAsync(
        "bad_plan",
        new ActivationPlanV2([KeyActionV2.Tap(InputNameResolverV2.Resolve("mouse_left"))]));

    AssertRuntimeV2Diagnostic(invalidPlan.Diagnostics, RuntimeV2DiagnosticCode.ActionPlanInvalid,
        "manager should surface invalid plan diagnostics.");
    AssertTrue(invalidPlan.Diagnostics.All(diagnostic => diagnostic.MappingId == "bad_plan"),
        "manager should stamp mapping id onto forwarded invalid-plan diagnostics.");
    AssertTrue(manager.ActiveSessionCount == 0, "invalid plan must not create active session.");
    AssertTrue(manager.Backend.Events.Count == 0, "invalid plan must not emit fake events.");

    var invalidOptions = await manager.TriggerDownAsync(
        "bad_options",
        new ActivationPlanV2([KeyActionV2.Tap(InputNameResolverV2.Resolve("a"))]),
        new RuntimeV2ExecutionOptions { MaxActionSteps = 0 });

    AssertRuntimeV2Diagnostic(invalidOptions.Diagnostics, RuntimeV2DiagnosticCode.InvalidMaxActionSteps,
        "manager should surface invalid options diagnostics.");
    AssertTrue(invalidOptions.Diagnostics.All(diagnostic => diagnostic.MappingId == "bad_options"),
        "manager should stamp mapping id onto forwarded invalid-options diagnostics.");
    AssertTrue(manager.ActiveSessionCount == 0, "invalid options must not create active session.");
    AssertTrue(manager.Backend.Events.Count == 0, "invalid options must not emit fake events.");

    var invalidId = await manager.TriggerDownAsync(
        " ",
        new ActivationPlanV2([KeyActionV2.Tap(InputNameResolverV2.Resolve("a"))]));
    AssertRuntimeV2Diagnostic(invalidId.Diagnostics, RuntimeV2DiagnosticCode.InvalidMappingId,
        "manager should reject empty mapping id.");
}

static async Task RuntimeV2ManagerEventLogIsDeterministic()
{
    var manager = new MappingActivationSessionManagerV2();
    await manager.TriggerDownAsync("skill_a", new ActivationPlanV2(
    [
        KeyActionV2.Tap(InputNameResolverV2.Resolve("a"))
    ]));
    await manager.TriggerDownAsync("skill_b", new ActivationPlanV2(
    [
        KeyActionV2.Tap(InputNameResolverV2.Resolve("b"))
    ]));

    AssertRuntimeV2SequenceIsStable(manager.Backend.Events);
    AssertTrue(manager.Backend.Events[0].MappingId == "skill_a" &&
            manager.Backend.Events[1].MappingId == "skill_b",
        "manager fake event log should preserve deterministic mapping context order.");
}

static Task RuntimeV2ConfigAdapterBuildsSimpleTapEntry()
{
    var result = new RuntimeV2ConfigAdapter().Build(AppConfigV2.Create(
    [
        SimpleMappingConfig("tap-a", "q", "a")
    ]));

    AssertRuntimeV2ConfigAdapterNoRuntimeBlocks(result, "valid simple tap config should build adapter entries.");
    AssertTrue(result.CanBuildSandboxEntries, "adapter should expose sandbox entries for valid simple mappings.");
    AssertTrue(result.CanBuildCompleteRuntime, "adapter should consider all-simple valid config complete for sandbox.");
    AssertTrue(result.Registry.Count == 1, "adapter should build one runtime entry.");
    AssertTrue(result.Registry.TryGet("tap-a", out var entry), "adapter registry should lookup by mapping id.");
    AssertTrue(entry!.MappingId == "tap-a" &&
            entry.Name == "tap-a" &&
            entry.MappingPath == "mappings[0]" &&
            entry.TriggerInput.CanonicalName == "q",
        "runtime entry should preserve mapping id, name, path, and canonical trigger InputSpec.");
    AssertTrue(entry.Plan.OnDown.Single() is KeyActionV2
    {
        Kind: ActionKindV2.TapKey,
        Key.CanonicalName: "a"
    }, "simple tap entry should contain on_down tap_key a.");
    return Task.CompletedTask;
}

static Task RuntimeV2ConfigAdapterPreservesSideSpecificHold()
{
    var result = new RuntimeV2ConfigAdapter().Build(AppConfigV2.Create(
    [
        SimpleMappingConfig(
            "hold-left-ctrl",
            "q",
            "left_ctrl",
            SimpleMappingModeV2.Hold)
    ]));

    AssertRuntimeV2ConfigAdapterNoRuntimeBlocks(result, "valid simple hold config should build adapter entry.");
    var entry = result.Entries.Single();
    AssertTrue(entry.TriggerInput.CanonicalName == "q", "entry trigger should remain canonical InputSpec q.");
    AssertTrue(entry.Plan.OnDown.Single() is KeyActionV2
    {
        Kind: ActionKindV2.DownKey,
        Key.CanonicalName: "left_ctrl"
    }, "simple hold entry should preserve side-specific down_key left_ctrl.");
    AssertTrue(entry.Plan.OnUp.Single() is KeyActionV2
    {
        Kind: ActionKindV2.UpKey,
        Key.CanonicalName: "left_ctrl"
    }, "simple hold entry should preserve side-specific up_key left_ctrl.");
    return Task.CompletedTask;
}

static Task RuntimeV2ConfigAdapterSkipsDisabledAndAllowsWarnings()
{
    var result = new RuntimeV2ConfigAdapter().Build(AppConfigV2.Create(
    [
        SimpleMappingConfig("enabled", "q", "a"),
        new MappingConfigV2(
            "disabled",
            "Disabled",
            Enabled: false,
            Trigger: null,
            ActionSource: SimpleActionSource("b")),
        new MappingConfigV2(
            "macro-draft",
            "Macro draft",
            Enabled: false,
            Trigger: null,
            ActionSource: new MappingActionSourceV2.MacroDslV2Source(""))
    ]));

    AssertTrue(!result.BlocksRuntime, "warning-only config should not block sandbox entry construction.");
    AssertTrue(result.CanBuildSandboxEntries, "warning-only config should still build enabled simple entries.");
    AssertTrue(result.CanBuildCompleteRuntime, "disabled macro warning should not make enabled runtime entries incomplete.");
    AssertTrue(result.Entries.Count == 1 && result.Entries.Single().MappingId == "enabled",
        "disabled mappings should not produce runtime entries.");
    AssertTrue(result.Diagnostics.Any(diagnostic =>
            diagnostic.OriginalAppConfigCode == AppConfigV2DiagnosticCode.EmptyMacroSource &&
            diagnostic.MappingId == "macro-draft" &&
            diagnostic.Severity == RuntimeV2ConfigAdapterDiagnosticSeverity.Warning),
        "adapter should preserve warning diagnostics for disabled macro drafts.");
    return Task.CompletedTask;
}

static Task RuntimeV2ConfigAdapterReportsMacroSourcesWithoutEntries()
{
    var result = new RuntimeV2ConfigAdapter().Build(AppConfigV2.Create(
    [
        new MappingConfigV2(
            "macro-empty",
            "Macro empty",
            Enabled: true,
            TriggerConfigV2.SingleInput(InputNameResolverV2.Resolve("q")),
            new MappingActionSourceV2.MacroDslV2Source("")),
        new MappingConfigV2(
            "macro-script",
            "Macro script",
            Enabled: true,
            TriggerConfigV2.SingleInput(InputNameResolverV2.Resolve("w")),
            new MappingActionSourceV2.MacroDslV2Source("on_down\nend"))
    ]));

    AssertTrue(result.Entries.Count == 0, "macro source shells should not produce runtime entries.");
    AssertTrue(result.BlocksRuntime, "macro source shell should make complete runtime unavailable.");
    AssertTrue(!result.CanBuildSandboxEntries, "macro-only config should have no sandbox entries.");
    AssertTrue(!result.CanBuildCompleteRuntime, "macro-only config is not complete runtime-ready.");
    AssertTrue(result.Diagnostics.Count(diagnostic =>
            diagnostic.Code == RuntimeV2ConfigAdapterDiagnosticCode.MacroCompilerMissing &&
            diagnostic.BlocksRuntime) == 2,
        "each enabled macro source should report MacroCompilerMissing and no runtime entry.");
    AssertTrue(result.Diagnostics.Any(diagnostic =>
            diagnostic.OriginalAppConfigCode == AppConfigV2DiagnosticCode.EmptyMacroSource &&
            diagnostic.MappingId == "macro-empty"),
        "empty macro warning from AppConfigV2 validation should be preserved.");
    return Task.CompletedTask;
}

static Task RuntimeV2ConfigAdapterSupportsPartialSimpleEntriesWithMacroBlocker()
{
    var result = new RuntimeV2ConfigAdapter().Build(AppConfigV2.Create(
    [
        SimpleMappingConfig("simple-tap", "q", "a"),
        new MappingConfigV2(
            "macro-enabled",
            "Macro enabled",
            Enabled: true,
            TriggerConfigV2.SingleInput(InputNameResolverV2.Resolve("w")),
            new MappingActionSourceV2.MacroDslV2Source("on_down\nend"))
    ]));

    AssertTrue(result.Entries.Count == 1 && result.Entries.Single().MappingId == "simple-tap",
        "enabled simple mapping should still produce a sandbox entry when another enabled macro cannot run.");
    AssertTrue(result.CanBuildSandboxEntries,
        "partial result should expose that at least one sandbox entry can be built.");
    AssertTrue(result.BlocksRuntime,
        "enabled macro compiler-missing diagnostic should block complete Runtime v2 execution.");
    AssertTrue(!result.CanBuildCompleteRuntime,
        "partial sandbox entries must not be reported as complete runtime runnable.");
    AssertTrue(result.Diagnostics.Any(diagnostic =>
            diagnostic.Code == RuntimeV2ConfigAdapterDiagnosticCode.MacroCompilerMissing &&
            diagnostic.MappingId == "macro-enabled" &&
            diagnostic.Path == "mappings[1].action.macro_dsl_v2" &&
            diagnostic.BlocksRuntime),
        "enabled macro source should report compiler-missing diagnostic with mapping id and action path.");
    return Task.CompletedTask;
}

static Task RuntimeV2ConfigAdapterPreservesGlobalHotkeyConflictMetadata()
{
    var result = new RuntimeV2ConfigAdapter().Build(AppConfigV2.Create(
        [SimpleMappingConfig("trigger-f5", "f5", "a")],
        controlHotkey: HotkeySpecV2.Parse("f5")));

    AssertTrue(result.BlocksRuntime, "control hotkey conflict should block Runtime v2 entry construction.");
    AssertTrue(result.Entries.Count == 0, "blocking global hotkey conflict must prevent entries.");
    var conflict = result.Diagnostics.FirstOrDefault(diagnostic =>
        diagnostic.OriginalAppConfigCode == AppConfigV2DiagnosticCode.InputConflict &&
        diagnostic.OriginalConflictCode == InputConflictCode.ControlHotkeyConflictsWithMappingTrigger);
    AssertTrue(conflict is not null, "adapter should preserve original AppConfigV2 input conflict diagnostic.");
    AssertTrue(conflict!.AffectedBindingIds.Contains("mapping:trigger-f5:trigger") &&
            conflict.AffectedBindingIds.Contains("control_hotkey"),
        "adapter should preserve affected ids for control hotkey conflict.");
    AssertTrue(conflict.AffectedBindingNames.Contains("trigger-f5") &&
            conflict.AffectedBindingNames.Contains("control hotkey"),
        "adapter should preserve affected names for control hotkey conflict.");
    AssertTrue(conflict.AffectedPaths.Contains("mappings[0].trigger") &&
            conflict.AffectedPaths.Contains("control_hotkey"),
        "adapter should preserve affected paths for control hotkey conflict.");
    AssertTrue(conflict.BlocksRuntime, "adapter conflict projection should block runtime.");
    AssertTrue(conflict.OriginalAppConfigDiagnostic is { BlocksSave: true, BlocksLive: true },
        "adapter should retain original save/live blocking metadata from AppConfigV2 diagnostic.");
    return Task.CompletedTask;
}

static Task RuntimeV2ConfigAdapterAllDisabledConfigHasNoEntriesWithoutRuntimeBlock()
{
    var result = new RuntimeV2ConfigAdapter().Build(AppConfigV2.Create(
    [
        new MappingConfigV2(
            "disabled-a",
            "Disabled A",
            Enabled: false,
            Trigger: null,
            ActionSource: SimpleActionSource("a")),
        new MappingConfigV2(
            "disabled-b",
            "Disabled B",
            Enabled: false,
            TriggerConfigV2.SingleInput(InputNameResolverV2.Resolve("q")),
            SimpleActionSource("b"))
    ]));

    AssertTrue(result.Entries.Count == 0, "all-disabled config should not produce runtime entries.");
    AssertTrue(!result.BlocksRuntime, "valid disabled-only config should not have runtime-blocking diagnostics.");
    AssertTrue(!result.CanBuildSandboxEntries,
        "CanBuildSandboxEntries is entry-count based and should be false when no enabled entries exist.");
    AssertTrue(result.CanBuildCompleteRuntime,
        "CanBuildCompleteRuntime means no adapter blockers in the sandbox model, not live-ready production execution.");
    return Task.CompletedTask;
}

static Task RuntimeV2ConfigAdapterDisabledStructuralErrorsStillBlockRuntime()
{
    var result = new RuntimeV2ConfigAdapter().Build(AppConfigV2.Create(
    [
        new MappingConfigV2(
            "disabled-bad",
            "Disabled bad",
            Enabled: false,
            Trigger: null,
            ActionSource: SimpleActionSource("a"),
            TimingOverride: new TimingSettingsV2(TapKeyDuration: TimeSpan.FromMilliseconds(-1)))
    ]));

    AssertTrue(result.Entries.Count == 0, "disabled invalid mapping should not produce runtime entries.");
    AssertTrue(result.BlocksRuntime, "disabled mapping structural error should still block runtime adapter result.");
    AssertTrue(!result.CanBuildCompleteRuntime,
        "structural error on disabled mapping should make complete runtime unavailable.");
    AssertTrue(result.Diagnostics.Any(diagnostic =>
            diagnostic.OriginalAppConfigCode == AppConfigV2DiagnosticCode.InvalidTiming &&
            diagnostic.MappingId == "disabled-bad" &&
            diagnostic.Path == "mappings[0].timing.tap_key_duration" &&
            diagnostic.BlocksRuntime),
        "adapter should preserve disabled mapping structural diagnostic with mapping id and field path.");
    return Task.CompletedTask;
}

static Task RuntimeV2ConfigAdapterBlocksInvalidConfigAndPreservesConflicts()
{
    var result = new RuntimeV2ConfigAdapter().Build(AppConfigV2.Create(
    [
        SimpleMappingConfig("first", "q", "a"),
        SimpleMappingConfig("second", "q", "b")
    ]));

    AssertTrue(result.BlocksRuntime, "duplicate trigger config should block runtime entries.");
    AssertTrue(result.Entries.Count == 0, "blocking AppConfigV2 errors must prevent entry generation.");
    var conflict = result.Diagnostics.FirstOrDefault(diagnostic =>
        diagnostic.OriginalConflictCode == InputConflictCode.DuplicateMappingTrigger);
    AssertTrue(conflict is not null, "adapter should preserve duplicate trigger conflict metadata.");
    AssertTrue(conflict!.AffectedBindingIds.Contains("mapping:first:trigger") &&
            conflict.AffectedBindingIds.Contains("mapping:second:trigger"),
        "adapter conflict diagnostic should preserve affected binding ids.");
    AssertTrue(conflict.AffectedPaths.Contains("mappings[0].trigger") &&
            conflict.AffectedPaths.Contains("mappings[1].trigger"),
        "adapter conflict diagnostic should preserve affected binding paths.");
    return Task.CompletedTask;
}

static Task RuntimeV2ConfigAdapterReportsSimpleBuildFailure()
{
    var result = new RuntimeV2ConfigAdapter().Build(AppConfigV2.Create(
    [
        SimpleMappingConfig("bad-wheel", "q", "mouse_wheel_up")
    ]));

    AssertTrue(result.BlocksRuntime, "invalid simple mapping source should block runtime entries.");
    AssertTrue(result.Entries.Count == 0, "invalid simple mapping should not produce an entry.");
    AssertTrue(result.Diagnostics.Any(diagnostic =>
            diagnostic.Code == RuntimeV2ConfigAdapterDiagnosticCode.InvalidSimpleMappingSource &&
            diagnostic.MappingId == "bad-wheel" &&
            diagnostic.Path == "mappings[0].action.target"),
        "simple build failure should project a mapping diagnostic with stable action path.");
    return Task.CompletedTask;
}

static async Task RuntimeV2ConfigAdapterEntryCanExecuteInManagerSandbox()
{
    var result = new RuntimeV2ConfigAdapter().Build(AppConfigV2.Create(
    [
        SimpleMappingConfig("tap-a", "q", "a")
    ]));
    var entry = result.Entries.Single();
    var manager = new MappingActivationSessionManagerV2();

    var down = await manager.TriggerDownAsync(entry.MappingId, entry.Plan);

    AssertRuntimeV2ManagerNoErrors(down, "adapter output entry should execute manually in fake manager sandbox.");
    AssertRuntimeV2Event(manager.Backend.Events.Single(), RuntimeV2EventKind.KeyTap, "a",
        "manual manager execution should tap the adapter plan target.");
    AssertTrue(manager.Backend.Events.Single().MappingId == "tap-a",
        "manual manager execution should preserve adapter mapping id as event context.");
}

static Task RuntimeV2ConfigAdapterDoesNotReferenceV1RuntimeHookGuiCompilerOrManager()
{
    var root = FindRuntimeV2SourceRoot();
    AssertTrue(root is not null, "Runtime v2 source root should be discoverable for adapter scan.");
    var adapterRoot = Path.Combine(root!, "Configuration");
    AssertTrue(Directory.Exists(adapterRoot), "Runtime v2 configuration adapter source root should exist.");

    foreach (var file in Directory.EnumerateFiles(adapterRoot, "*.cs"))
    {
        var text = File.ReadAllText(file);
        foreach (var forbiddenName in new[]
        {
            "RuntimeHost",
            "InProcessRuntimeCore",
            "RuntimeHostController",
            "TriggerPipeline",
            "WindowsInputBackend",
            "SendInput",
            "MacroScriptCompiler",
            "BAKeySmith.App.",
            "ConfigDocumentService",
            "AppConfigSerializer",
            "MappingActivationSessionManagerV2"
        })
        {
            AssertTrue(!text.Contains(forbiddenName, StringComparison.Ordinal),
                $"{Path.GetFileName(file)} must not reference {forbiddenName}.");
        }
    }

    return Task.CompletedTask;
}

static Task RuntimeV2SandboxDoesNotReferenceV1RuntimeHookGuiOrRealInput()
{
    var runtimeV2Types = typeof(ActivationPlanExecutorV2).Assembly.GetTypes()
        .Where(type => string.Equals(type.Namespace, "BAKeySmith.Core.Runtime.V2", StringComparison.Ordinal))
        .ToArray();

    var forbidden = new[]
    {
        "BAKeySmith.Core.Hosting.RuntimeHost",
        "BAKeySmith.Core.Runtime.InProcessRuntimeCore",
        "BAKeySmith.Core.Triggers.",
        "BAKeySmith.App.",
        "WindowsInputBackend"
    };

    foreach (var type in runtimeV2Types)
    {
        var referencedTypes = type.GetConstructors()
            .SelectMany(constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType))
            .Concat(type.GetProperties().Select(property => property.PropertyType))
            .Concat(type.GetFields().Select(field => field.FieldType))
            .Concat(type.GetMethods().Select(method => method.ReturnType));

        foreach (var referencedType in referencedTypes)
        {
            var fullName = referencedType.FullName ?? string.Empty;
            foreach (var forbiddenName in forbidden)
            {
                AssertTrue(!fullName.Contains(forbiddenName, StringComparison.Ordinal),
                    $"{type.Name} must not reference {forbiddenName}.");
            }
        }
    }

    var sourceRoot = FindRuntimeV2SourceRoot();
    AssertTrue(sourceRoot is not null, "Runtime v2 source root should be discoverable for source-level isolation scan.");
    foreach (var file in Directory.EnumerateFiles(sourceRoot!, "*.cs"))
    {
        var text = File.ReadAllText(file);
        foreach (var forbiddenName in forbidden.Concat(
            [
                "RuntimeHost",
                "InProcessRuntimeCore",
                "RuntimeHostController",
                "TriggerPipeline",
                "WindowsInputBackend",
                "SendInput",
                "MacroScriptCompiler",
                "MainWindow"
            ]))
        {
            AssertTrue(!text.Contains(forbiddenName, StringComparison.Ordinal),
                $"{Path.GetFileName(file)} must not reference {forbiddenName}.");
        }
    }

    return Task.CompletedTask;
}

static Task SimpleMappingV2BuildsTapKeyPlan()
{
    var result = BuildSimpleMapping(SimpleMappingModeV2.Tap, "a");
    AssertSimpleMappingSuccess(result, "simple tap key a should build a valid plan.");
    AssertTrue(result.Plan!.OnDown.Count == 1, "simple tap key should produce one on_down action.");
    AssertTrue(result.Plan.OnDown[0] is KeyActionV2
        {
            Kind: ActionKindV2.TapKey,
            Key.CanonicalName: "a",
            Duration: null
        }, "simple tap key should build on_down tap_key a without hard-coded duration.");
    AssertTrue(result.Plan.OnUp.Count == 0, "simple tap key should not produce on_up actions.");

    var zeroDuration = BuildSimpleMapping(SimpleMappingModeV2.Tap, "a", TimeSpan.Zero);
    AssertSimpleMappingSuccess(zeroDuration, "simple tap key 0ms duration should build a valid plan.");
    AssertTrue(zeroDuration.Plan!.OnDown[0] is KeyActionV2
        {
            Kind: ActionKindV2.TapKey,
            Key.CanonicalName: "a",
            Duration: { TotalMilliseconds: 0 }
        }, "simple tap key should preserve explicit 0ms duration.");

    var leftCtrl = BuildSimpleMapping(SimpleMappingModeV2.Tap, "left_ctrl", TimeSpan.FromMilliseconds(5));
    AssertSimpleMappingSuccess(leftCtrl, "simple tap left_ctrl should preserve side-specific InputSpec.");
    AssertTrue(leftCtrl.Plan!.OnDown[0] is KeyActionV2
        {
            Kind: ActionKindV2.TapKey,
            Key.CanonicalName: "left_ctrl",
            Duration: { TotalMilliseconds: 5 }
        }, "simple tap left_ctrl should keep side-specific canonical target and duration.");
    return Task.CompletedTask;
}

static Task SimpleMappingV2BuildsTapMousePlan()
{
    var result = BuildSimpleMapping(SimpleMappingModeV2.Tap, "mouse_left");
    AssertSimpleMappingSuccess(result, "simple tap mouse_left should build a valid plan.");
    AssertTrue(result.Plan!.OnDown.Count == 1, "simple tap mouse should produce one on_down action.");
    AssertTrue(result.Plan.OnDown[0] is MouseButtonActionV2
        {
            Kind: ActionKindV2.TapMouse,
            Button.CanonicalName: "mouse_left",
            Duration: null
        }, "simple tap mouse_left should build on_down tap_mouse mouse_left.");

    var zeroDuration = BuildSimpleMapping(SimpleMappingModeV2.Tap, "mouse_left", TimeSpan.Zero);
    AssertSimpleMappingSuccess(zeroDuration, "simple tap mouse 0ms duration should build a valid plan.");
    AssertTrue(zeroDuration.Plan!.OnDown[0] is MouseButtonActionV2
        {
            Kind: ActionKindV2.TapMouse,
            Button.CanonicalName: "mouse_left",
            Duration: { TotalMilliseconds: 0 }
        }, "simple tap mouse should preserve explicit 0ms duration.");
    return Task.CompletedTask;
}

static Task SimpleMappingV2BuildsHoldPlans()
{
    var keyHold = BuildSimpleMapping(SimpleMappingModeV2.Hold, "q");
    AssertSimpleMappingSuccess(keyHold, "simple hold q should build a valid plan.");
    AssertTrue(keyHold.Plan!.OnDown.Count == 1 && keyHold.Plan.OnUp.Count == 1,
        "simple hold key should produce one down action and one up action.");
    AssertTrue(keyHold.Plan.OnDown[0] is KeyActionV2
        {
            Kind: ActionKindV2.DownKey,
            Key.CanonicalName: "q"
        }, "simple hold key should build on_down down_key q.");
    AssertTrue(keyHold.Plan.OnUp[0] is KeyActionV2
        {
            Kind: ActionKindV2.UpKey,
            Key.CanonicalName: "q"
        }, "simple hold key should build on_up up_key q.");

    var leftCtrlHold = BuildSimpleMapping(SimpleMappingModeV2.Hold, "left_ctrl");
    AssertSimpleMappingSuccess(leftCtrlHold, "simple hold left_ctrl should preserve side-specific target.");
    AssertTrue(leftCtrlHold.Plan!.OnDown[0] is KeyActionV2
        {
            Kind: ActionKindV2.DownKey,
            Key.CanonicalName: "left_ctrl"
        }, "simple hold left_ctrl should build down_key left_ctrl.");
    AssertTrue(leftCtrlHold.Plan.OnUp[0] is KeyActionV2
        {
            Kind: ActionKindV2.UpKey,
            Key.CanonicalName: "left_ctrl"
        }, "simple hold left_ctrl should build up_key left_ctrl.");

    var mouseHold = BuildSimpleMapping(SimpleMappingModeV2.Hold, "mouse_left");
    AssertSimpleMappingSuccess(mouseHold, "simple hold mouse_left should build a valid plan.");
    AssertTrue(mouseHold.Plan!.OnDown[0] is MouseButtonActionV2
        {
            Kind: ActionKindV2.DownMouse,
            Button.CanonicalName: "mouse_left"
        }, "simple hold mouse should build on_down down_mouse mouse_left.");
    AssertTrue(mouseHold.Plan.OnUp[0] is MouseButtonActionV2
        {
            Kind: ActionKindV2.UpMouse,
            Button.CanonicalName: "mouse_left"
        }, "simple hold mouse should build on_up up_mouse mouse_left.");
    return Task.CompletedTask;
}

static Task SimpleMappingV2RejectsUnsupportedTargetsAndDurations()
{
    var wheel = BuildSimpleMapping(SimpleMappingModeV2.Tap, "mouse_wheel_up");
    AssertSimpleMappingError(
        wheel,
        SimpleMappingValidationCodeV2.UnsupportedTarget,
        "wheel target should be rejected by Simple Mapping v2 skeleton.",
        "target");

    var coordinate = new SimpleMappingPlanBuilderV2().Build(new SimpleMappingDefinitionV2(
        "coord",
        SimpleMappingModeV2.Tap,
        SimpleMappingTargetV2.FromCoordinate(new CoordinatePointV2(10, 20, "ba16_1920x1080"))));
    AssertSimpleMappingError(
        coordinate,
        SimpleMappingValidationCodeV2.UnsupportedTarget,
        "coordinate target should be rejected by Simple Mapping v2 skeleton.",
        "target");

    var negativeTap = BuildSimpleMapping(
        SimpleMappingModeV2.Tap,
        "a",
        TimeSpan.FromMilliseconds(-1));
    AssertSimpleMappingError(
        negativeTap,
        SimpleMappingValidationCodeV2.InvalidDuration,
        "negative simple tap duration should be rejected.",
        "duration");

    var holdWithDuration = BuildSimpleMapping(
        SimpleMappingModeV2.Hold,
        "q",
        TimeSpan.Zero);
    AssertSimpleMappingError(
        holdWithDuration,
        SimpleMappingValidationCodeV2.InvalidDuration,
        "simple hold mode must not accept tap duration in this skeleton.",
        "duration");

    var unsupportedMode = BuildSimpleMapping((SimpleMappingModeV2)999, "q");
    AssertSimpleMappingError(
        unsupportedMode,
        SimpleMappingValidationCodeV2.UnsupportedMode,
        "unknown simple mapping mode should be rejected.",
        "mode");
    return Task.CompletedTask;
}

static Task SimpleMappingV2HandlesMalformedTargetsAndDiagnosticPaths()
{
    var builder = new SimpleMappingPlanBuilderV2();
    var malformedKind = builder.Build(new SimpleMappingDefinitionV2(
        "bad-kind",
        SimpleMappingModeV2.Tap,
        new SimpleMappingTargetV2((SimpleMappingTargetKindV2)999)));
    AssertSimpleMappingError(
        malformedKind,
        SimpleMappingValidationCodeV2.InvalidTargetKind,
        "undefined simple mapping target kind should return a diagnostic.",
        "target.kind");
    AssertTrue(malformedKind.Plan is null, "malformed target kind must not generate a plan.");

    var missingInput = builder.Build(new SimpleMappingDefinitionV2(
        "missing-input",
        SimpleMappingModeV2.Tap,
        new SimpleMappingTargetV2(SimpleMappingTargetKindV2.Input)));
    AssertSimpleMappingError(
        missingInput,
        SimpleMappingValidationCodeV2.MissingInputSpec,
        "input target missing InputSpec should return a diagnostic.",
        "target.input");
    AssertTrue(missingInput.Plan is null, "missing InputSpec must not generate a plan.");

    var inputWithCoordinate = builder.Build(new SimpleMappingDefinitionV2(
        "input-with-coordinate",
        SimpleMappingModeV2.Tap,
        new SimpleMappingTargetV2(
            SimpleMappingTargetKindV2.Input,
            InputNameResolverV2.Resolve("a"),
            new CoordinatePointV2(1, 2, "ba16_1920x1080"))));
    AssertSimpleMappingError(
        inputWithCoordinate,
        SimpleMappingValidationCodeV2.InvalidTargetPayload,
        "input target carrying coordinate payload should return a diagnostic.",
        "target.coordinate");

    var coordinateMissingPayload = builder.Build(new SimpleMappingDefinitionV2(
        "coordinate-missing",
        SimpleMappingModeV2.Tap,
        new SimpleMappingTargetV2(SimpleMappingTargetKindV2.Coordinate)));
    AssertSimpleMappingError(
        coordinateMissingPayload,
        SimpleMappingValidationCodeV2.InvalidTargetPayload,
        "coordinate target missing coordinate payload should return a diagnostic.",
        "target.coordinate");

    var coordinateWithInput = builder.Build(new SimpleMappingDefinitionV2(
        "coordinate-with-input",
        SimpleMappingModeV2.Tap,
        new SimpleMappingTargetV2(
            SimpleMappingTargetKindV2.Coordinate,
            InputNameResolverV2.Resolve("a"),
            new CoordinatePointV2(1, 2, "ba16_1920x1080"))));
    AssertSimpleMappingError(
        coordinateWithInput,
        SimpleMappingValidationCodeV2.InvalidTargetPayload,
        "coordinate target carrying InputSpec should return a diagnostic.",
        "target.input");

    var nullTarget = builder.Build(new SimpleMappingDefinitionV2(
        "null-target",
        SimpleMappingModeV2.Tap,
        null!));
    AssertSimpleMappingError(
        nullTarget,
        SimpleMappingValidationCodeV2.InvalidTargetKind,
        "null target should return a diagnostic.",
        "target");
    return Task.CompletedTask;
}

static Task SimpleMappingV2DoesNotReferenceV1ConfigRuntimeGuiOrCompiler()
{
    var mappingTypes = typeof(SimpleMappingDefinitionV2).Assembly.GetTypes()
        .Where(type => string.Equals(type.Namespace, "BAKeySmith.Core.Mappings.V2", StringComparison.Ordinal))
        .ToArray();

    foreach (var type in mappingTypes)
    {
        var referencedTypes = type.GetConstructors()
            .SelectMany(constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType))
            .Concat(type.GetProperties().Select(property => property.PropertyType))
            .Concat(type.GetFields().Select(field => field.FieldType));

        foreach (var referencedType in referencedTypes)
        {
            var fullName = referencedType.FullName ?? string.Empty;
            AssertTrue(!fullName.Contains("AppConfigV1", StringComparison.Ordinal),
                $"{type.Name} must not reference AppConfigV1.");
            AssertTrue(!fullName.Contains("MappingConfigV1", StringComparison.Ordinal),
                $"{type.Name} must not reference MappingConfigV1.");
            AssertTrue(!fullName.Contains("Runtime", StringComparison.Ordinal),
                $"{type.Name} must not reference runtime implementation types.");
            AssertTrue(!fullName.Contains("MacroScriptCompiler", StringComparison.Ordinal),
                $"{type.Name} must not reference MacroScriptCompiler.");
            AssertTrue(!fullName.Contains("MainWindow", StringComparison.Ordinal),
                $"{type.Name} must not reference GUI types.");
        }
    }

    return Task.CompletedTask;
}

static SimpleMappingPlanBuildResultV2 BuildSimpleMapping(
    SimpleMappingModeV2 mode,
    string target,
    TimeSpan? duration = null)
{
    return new SimpleMappingPlanBuilderV2().Build(new SimpleMappingDefinitionV2(
        $"simple:{mode}:{target}",
        mode,
        SimpleMappingTargetV2.FromInput(InputNameResolverV2.Resolve(target)),
        duration));
}

static void AssertSimpleMappingSuccess(SimpleMappingPlanBuildResultV2 result, string message)
{
    var errors = result.Diagnostics
        .Where(diagnostic => diagnostic.Severity == SimpleMappingValidationSeverityV2.Error)
        .Select(diagnostic => $"{diagnostic.Code} at {diagnostic.Path}: {diagnostic.Message}")
        .ToArray();

    AssertTrue(result.Success && result.Plan is not null,
        $"{message}{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
    var plan = result.Plan!;
    AssertNoActionErrors(new ActivationPlanValidatorV2().Validate(plan), message);
}

static void AssertSimpleMappingError(
    SimpleMappingPlanBuildResultV2 result,
    SimpleMappingValidationCodeV2 code,
    string message,
    string? path = null)
{
    AssertTrue(!result.Success, $"{message} Build result should fail.");
    AssertTrue(result.Diagnostics.Any(diagnostic =>
            diagnostic.Severity == SimpleMappingValidationSeverityV2.Error &&
            diagnostic.Code == code &&
            (path is null || diagnostic.Path == path) &&
            !string.IsNullOrWhiteSpace(diagnostic.Message) &&
            !string.IsNullOrWhiteSpace(diagnostic.Path)),
        message);
}

static Task AppConfigV2ValidSimpleMappingsValidate()
{
    var tapMapping = SimpleMappingConfig("tap-a", "q", "a");
    var holdMapping = SimpleMappingConfig(
        "hold-left-ctrl",
        "w",
        "left_ctrl",
        SimpleMappingModeV2.Hold);
    var config = AppConfigV2.Create(
    [
        tapMapping,
        holdMapping
    ]);

    AssertAppConfigV2NoErrors(new AppConfigV2Validator().Validate(config),
        "valid AppConfigV2 with simple tap and hold mappings should validate.");

    var tapPlan = new MappingActionSourcePlanBuilderV2().Build(tapMapping.ActionSource!);
    AssertTrue(tapPlan.Success && tapPlan.Plan is not null,
        "simple mapping source should produce ActivationPlanV2 through the v2 source builder.");
    AssertTrue(tapPlan.Plan!.OnDown[0] is KeyActionV2
    {
        Kind: ActionKindV2.TapKey,
        Key.CanonicalName: "a"
    }, "simple tap mapping source should produce on_down tap_key a.");

    var holdPlan = new MappingActionSourcePlanBuilderV2().Build(holdMapping.ActionSource!);
    AssertTrue(holdPlan.Success && holdPlan.Plan is not null,
        "simple hold mapping source should produce ActivationPlanV2.");
    AssertTrue(holdPlan.Plan!.OnDown[0] is KeyActionV2
    {
        Kind: ActionKindV2.DownKey,
        Key.CanonicalName: "left_ctrl"
    }, "simple hold left_ctrl should preserve side-specific InputSpec in on_down.");
    AssertTrue(holdPlan.Plan.OnUp[0] is KeyActionV2
    {
        Kind: ActionKindV2.UpKey,
        Key.CanonicalName: "left_ctrl"
    }, "simple hold left_ctrl should preserve side-specific InputSpec in on_up.");

    return Task.CompletedTask;
}

static Task AppConfigV2MacroSourceShellDoesNotCompile()
{
    var macroSource = new MappingActionSourceV2.MacroDslV2Source("""
        on_down
          tap_key a
        end
        """);
    var config = AppConfigV2.Create(
    [
        new MappingConfigV2(
            "macro-shell",
            "Macro shell",
            Enabled: true,
            TriggerConfigV2.SingleInput(InputNameResolverV2.Resolve("q")),
            macroSource)
    ]);

    AssertTrue(macroSource.SourceText.Contains("on_down", StringComparison.Ordinal),
        "Macro DSL v2 source shell should store source text.");
    AssertAppConfigV2NoErrors(new AppConfigV2Validator().Validate(config),
        "Macro DSL v2 source shell should be storable without compiling in AppConfigV2 draft validation.");

    var build = new MappingActionSourcePlanBuilderV2().Build(macroSource);
    AssertTrue(!build.Success && build.Plan is null,
        "Macro DSL v2 source shell must not produce ActivationPlanV2 in this skeleton.");
    AssertTrue(build.Diagnostics.Any(diagnostic =>
            diagnostic.Severity == AppConfigV2DiagnosticSeverity.Error &&
            diagnostic.Code == AppConfigV2DiagnosticCode.MacroCompilerMissing &&
            diagnostic.Path == "action.macro_dsl_v2"),
        "Macro DSL v2 build-plan attempt should return MacroCompilerMissing diagnostic.");
    return Task.CompletedTask;
}

static Task AppConfigV2RejectsDuplicateIdsAndTriggers()
{
    AssertAppConfigV2Error(
        AppConfigV2.Create(
        [
            SimpleMappingConfig("duplicate", "q", "a"),
            SimpleMappingConfig("duplicate", "w", "b")
        ]),
        AppConfigV2DiagnosticCode.DuplicateMappingId,
        "duplicate mapping ids should be rejected.",
        "mappings[1].id");

    AssertAppConfigV2Error(
        AppConfigV2.Create(
        [
            SimpleMappingConfig("first", "q", "a"),
            SimpleMappingConfig("second", "q", "b")
        ]),
        AppConfigV2DiagnosticCode.InputConflict,
        "duplicate mapping triggers should be rejected through Conflict Model v2.");

    AssertAppConfigV2Error(
        AppConfigV2.Create(
        [
            SimpleMappingConfig("generic", "ctrl", "a"),
            SimpleMappingConfig("side", "left_ctrl", "b")
        ]),
        AppConfigV2DiagnosticCode.InputConflict,
        "generic ctrl trigger should conflict with left_ctrl trigger.");

    return Task.CompletedTask;
}

static Task AppConfigV2DetectsGlobalHotkeyConflicts()
{
    AssertAppConfigV2Error(
        AppConfigV2.Create(
            [SimpleMappingConfig("trigger-f5", "f5", "a")],
            controlHotkey: HotkeySpecV2.Parse("f5")),
        AppConfigV2DiagnosticCode.InputConflict,
        "control hotkey f5 should conflict with trigger f5.");

    AssertAppConfigV2Error(
        AppConfigV2.Create(
            [SimpleMappingConfig("trigger-f8", "f8", "a")],
            coordinateRecordHotkey: CoordinateRecordHotkeySpecV2.Parse("f8")),
        AppConfigV2DiagnosticCode.InputConflict,
        "coordinate record hotkey f8 should conflict with trigger f8.");

    AssertAppConfigV2Error(
        AppConfigV2.Create(
            [SimpleMappingConfig("trigger-f12", "f12", "a")],
            emergencyStopHotkey: HotkeySpecV2.Parse("f12")),
        AppConfigV2DiagnosticCode.InputConflict,
        "emergency stop hotkey f12 should conflict with trigger f12.");

    return Task.CompletedTask;
}

static Task AppConfigV2RejectsInvalidTriggerTimingAndCoordinateSettings()
{
    var invalidTrigger = new InputSpec(
        "not_a_trigger",
        InputKind.KeyboardKey,
        InputCapabilities.KeyOutput,
        "Not A Trigger",
        "Not A Trigger");

    AssertAppConfigV2Error(
        AppConfigV2.Create(
        [
            new MappingConfigV2(
                "invalid-trigger",
                "Invalid trigger",
                Enabled: true,
                TriggerConfigV2.SingleInput(invalidTrigger),
                SimpleActionSource("a"))
        ]),
        AppConfigV2DiagnosticCode.InvalidTrigger,
        "trigger input without mapping-trigger capability should be rejected.",
        "mappings[0].trigger.input");

    AssertAppConfigV2Error(
        AppConfigV2.Create(
            [SimpleMappingConfig("timing", "q", "a")],
            defaultTiming: new TimingSettingsV2(TapKeyDuration: TimeSpan.FromMilliseconds(-1))),
        AppConfigV2DiagnosticCode.InvalidTiming,
        "negative default timing override should be rejected.",
        "defaults.timing.tap_key_duration");

    AssertAppConfigV2Error(
        AppConfigV2.Create(
            [SimpleMappingConfig("coordinate", "q", "a")],
            defaultCoordinateSettings: new CoordinateSettingsV2("")),
        AppConfigV2DiagnosticCode.InvalidCoordinateSettings,
        "empty coordinate profile should be rejected when supplied.",
        "defaults.coordinate.profile_id");

    var realCursorFallback = new CoordinateSettingsV2(
        "ba16_1920x1080",
        CoordinateExecutionPolicyV2.AllowRealCursor);
    AssertTrue(!realCursorFallback.ClaimsLiveReadiness,
        "Coordinate execution policy is data only and must not claim live readiness.");

    return Task.CompletedTask;
}

static Task AppConfigV2DoesNotReferenceV1SerializerRuntimeGuiOrCompiler()
{
    var configTypes = typeof(AppConfigV2).Assembly.GetTypes()
        .Where(type => string.Equals(type.Namespace, "BAKeySmith.Core.Configuration.V2", StringComparison.Ordinal))
        .ToArray();

    foreach (var type in configTypes)
    {
        var referencedTypes = type.GetConstructors()
            .SelectMany(constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType))
            .Concat(type.GetProperties().Select(property => property.PropertyType))
            .Concat(type.GetFields().Select(field => field.FieldType));

        foreach (var referencedType in referencedTypes)
        {
            var fullName = referencedType.FullName ?? string.Empty;
            AssertTrue(!fullName.Contains("AppConfigV1", StringComparison.Ordinal),
                $"{type.Name} must not reference AppConfigV1.");
            AssertTrue(!fullName.Contains("MappingConfigV1", StringComparison.Ordinal),
                $"{type.Name} must not reference MappingConfigV1.");
            AssertTrue(!fullName.Contains("AppConfigSerializer", StringComparison.Ordinal),
                $"{type.Name} must not reference AppConfigSerializer.");
            AssertTrue(!fullName.Contains("RuntimeHost", StringComparison.Ordinal),
                $"{type.Name} must not reference RuntimeHost.");
            AssertTrue(!fullName.Contains("InProcessRuntimeCore", StringComparison.Ordinal),
                $"{type.Name} must not reference runtime implementation.");
            AssertTrue(!fullName.Contains("MacroScriptCompiler", StringComparison.Ordinal),
                $"{type.Name} must not reference MacroScriptCompiler.");
            AssertTrue(!fullName.Contains("MainWindow", StringComparison.Ordinal),
                $"{type.Name} must not reference GUI types.");
        }
    }

    return Task.CompletedTask;
}

static Task AppConfigV2ConflictDiagnosticsExposeMetadata()
{
    var duplicate = ValidateAppConfigV2(AppConfigV2.Create(
    [
        SimpleMappingConfig("first", "q", "a"),
        SimpleMappingConfig("second", "q", "b")
    ]));
    AssertAppConfigV2Conflict(
        duplicate,
        InputConflictCode.DuplicateMappingTrigger,
        ["mapping:first:trigger", "mapping:second:trigger"],
        ["mappings[0].trigger", "mappings[1].trigger"],
        "duplicate trigger conflict should expose binding ids and paths.");

    var controlConflict = ValidateAppConfigV2(AppConfigV2.Create(
        [SimpleMappingConfig("trigger-f5", "f5", "a")],
        controlHotkey: HotkeySpecV2.Parse("f5")));
    AssertAppConfigV2Conflict(
        controlConflict,
        InputConflictCode.ControlHotkeyConflictsWithMappingTrigger,
        ["mapping:trigger-f5:trigger", "control_hotkey"],
        ["mappings[0].trigger", "control_hotkey"],
        "control hotkey conflict should expose mapping and control paths.");

    var emergencyConflict = ValidateAppConfigV2(AppConfigV2.Create(
        [],
        controlHotkey: HotkeySpecV2.Parse("left_ctrl+f12"),
        emergencyStopHotkey: HotkeySpecV2.Parse("left_ctrl+f12")));
    AssertAppConfigV2Conflict(
        emergencyConflict,
        InputConflictCode.EmergencyStopHotkeyConflictsWithBinding,
        ["control_hotkey", "emergency_stop_hotkey"],
        ["control_hotkey", "emergency_stop_hotkey"],
        "emergency stop conflict should expose global hotkey paths.");

    return Task.CompletedTask;
}

static Task AppConfigV2DisabledMappingSemanticsAreExplicit()
{
    var disabledMissingTrigger = AppConfigV2.Create(
    [
        new MappingConfigV2(
            "disabled-missing-trigger",
            "Disabled missing trigger",
            Enabled: false,
            Trigger: null,
            SimpleActionSource("a"))
    ]);
    AssertAppConfigV2NoErrors(
        ValidateAppConfigV2(disabledMissingTrigger),
        "disabled mapping may omit trigger.");

    var disabledConflict = AppConfigV2.Create(
    [
        SimpleMappingConfig("enabled", "q", "a"),
        new MappingConfigV2(
            "disabled-same-trigger",
            "Disabled same trigger",
            Enabled: false,
            TriggerConfigV2.SingleInput(InputNameResolverV2.Resolve("q")),
            SimpleActionSource("b"))
    ]);
    AssertAppConfigV2NoErrors(
        ValidateAppConfigV2(disabledConflict),
        "disabled mapping trigger should not participate in trigger conflict checks.");

    AssertAppConfigV2Error(
        AppConfigV2.Create(
        [
            new MappingConfigV2(
                "disabled-invalid-timing",
                "Disabled invalid timing",
                Enabled: false,
                Trigger: null,
                SimpleActionSource("a"),
                TimingOverride: new TimingSettingsV2(TapKeyDuration: TimeSpan.FromMilliseconds(-1)))
        ]),
        AppConfigV2DiagnosticCode.InvalidTiming,
        "disabled mapping still validates timing override.",
        "mappings[0].timing.tap_key_duration");

    AssertAppConfigV2Error(
        AppConfigV2.Create(
        [
            new MappingConfigV2(
                "disabled-invalid-coordinate",
                "Disabled invalid coordinate",
                Enabled: false,
                Trigger: null,
                SimpleActionSource("a"),
                CoordinateSettingsOverride: new CoordinateSettingsV2(""))
        ]),
        AppConfigV2DiagnosticCode.InvalidCoordinateSettings,
        "disabled mapping still validates coordinate settings.",
        "mappings[0].coordinate.profile_id");

    var invalidTrigger = new InputSpec(
        "not_a_trigger",
        InputKind.KeyboardKey,
        InputCapabilities.KeyOutput,
        "Not A Trigger",
        "Not A Trigger");
    AssertAppConfigV2Error(
        AppConfigV2.Create(
        [
            new MappingConfigV2(
                "disabled-invalid-trigger",
                "Disabled invalid trigger",
                Enabled: false,
                TriggerConfigV2.SingleInput(invalidTrigger),
                SimpleActionSource("a"))
        ]),
        AppConfigV2DiagnosticCode.InvalidTrigger,
        "disabled mapping still validates a provided trigger.",
        "mappings[0].trigger.input");

    return Task.CompletedTask;
}

static Task AppConfigV2RejectsActionSourceKindMismatchAndUnknownSource()
{
    var mismatchedSimple = new MappingActionSourceV2.SimpleMapping(SimpleDefinition("a"))
    {
        Kind = MappingActionSourceKindV2.MacroDslV2
    };
    AssertAppConfigV2Error(
        AppConfigV2.Create(
        [
            new MappingConfigV2(
                "mismatched-simple",
                "Mismatched simple",
                Enabled: true,
                TriggerConfigV2.SingleInput(InputNameResolverV2.Resolve("q")),
                mismatchedSimple)
        ]),
        AppConfigV2DiagnosticCode.InvalidActionSourceKind,
        "simple source carrying macro kind should be rejected.",
        "mappings[0].action.kind");

    var mismatchedMacro = new MappingActionSourceV2.MacroDslV2Source("on_down")
    {
        Kind = MappingActionSourceKindV2.Simple
    };
    AssertAppConfigV2Error(
        AppConfigV2.Create(
        [
            new MappingConfigV2(
                "mismatched-macro",
                "Mismatched macro",
                Enabled: true,
                TriggerConfigV2.SingleInput(InputNameResolverV2.Resolve("q")),
                mismatchedMacro)
        ]),
        AppConfigV2DiagnosticCode.InvalidActionSourceKind,
        "macro source carrying simple kind should be rejected.",
        "mappings[0].action.kind");

    AssertAppConfigV2Error(
        AppConfigV2.Create(
        [
            new MappingConfigV2(
                "unknown-source",
                "Unknown source",
                Enabled: true,
                TriggerConfigV2.SingleInput(InputNameResolverV2.Resolve("q")),
                new UnknownMappingActionSourceV2())
        ]),
        AppConfigV2DiagnosticCode.InvalidActionSource,
        "unknown action source subtype should be rejected.",
        "mappings[0].action");

    return Task.CompletedTask;
}

static Task AppConfigV2MacroSourceEmptyPolicyIsWarning()
{
    var emptyMacro = new MappingActionSourceV2.MacroDslV2Source("   ");
    var config = AppConfigV2.Create(
    [
        new MappingConfigV2(
            "empty-macro",
            "Empty macro",
            Enabled: true,
            TriggerConfigV2.SingleInput(InputNameResolverV2.Resolve("q")),
            emptyMacro)
    ]);

    var diagnostics = ValidateAppConfigV2(config);
    AssertAppConfigV2Diagnostic(
        diagnostics,
        AppConfigV2DiagnosticSeverity.Warning,
        AppConfigV2DiagnosticCode.EmptyMacroSource,
        "empty macro source should be a warning in the draft model.",
        "mappings[0].action.macro_dsl_v2.source");
    AssertAppConfigV2NoErrors(
        diagnostics,
        "empty macro source warning should not block draft config validation.");

    var build = new MappingActionSourcePlanBuilderV2().Build(emptyMacro);
    AssertTrue(build.Diagnostics.Any(diagnostic =>
            diagnostic.Severity == AppConfigV2DiagnosticSeverity.Error &&
            diagnostic.Code == AppConfigV2DiagnosticCode.MacroCompilerMissing),
        "empty macro build-plan attempt should still return MacroCompilerMissing.");

    return Task.CompletedTask;
}

static Task AppConfigV2GlobalHotkeyValidationBoundaries()
{
    AssertThrows<ArgumentException>(
        () => HotkeySpecV2.Parse("left_ctrl"),
        "modifier-only control hotkey should be rejected by HotkeySpecV2.");
    AssertThrows<ArgumentException>(
        () => CoordinateRecordHotkeySpecV2.Parse("left_ctrl"),
        "modifier-only coordinate record hotkey should be rejected.");
    AssertThrows<ArgumentException>(
        () => HotkeySpecV2.Parse("right_alt"),
        "modifier-only emergency stop hotkey should be rejected by HotkeySpecV2.");

    var emergencyVsCoordinate = ValidateAppConfigV2(AppConfigV2.Create(
        [],
        coordinateRecordHotkey: CoordinateRecordHotkeySpecV2.Parse("f8"),
        emergencyStopHotkey: HotkeySpecV2.Parse("f8")));
    AssertAppConfigV2Conflict(
        emergencyVsCoordinate,
        InputConflictCode.EmergencyStopHotkeyConflictsWithBinding,
        ["coordinate_record_hotkey", "emergency_stop_hotkey"],
        ["coordinate_record_hotkey", "emergency_stop_hotkey"],
        "emergency stop should conflict with coordinate record hotkey.");

    AssertAppConfigV2NoErrors(
        ValidateAppConfigV2(AppConfigV2.Create(
            [SimpleMappingConfig("normal", "q", "a")],
            coordinateRecordHotkey: CoordinateRecordHotkeySpecV2.Parse("mouse_x1"))),
        "coordinate record hotkey may use mouse_x1 as main input.");

    AssertAppConfigV2NoErrors(
        ValidateAppConfigV2(AppConfigV2.Create(
            [SimpleMappingConfig("normal", "q", "a")],
            coordinateRecordHotkey: CoordinateRecordHotkeySpecV2.Parse("left_ctrl+mouse_x1"))),
        "coordinate record hotkey may use side-specific modifier plus mouse_x1 in this skeleton.");

    return Task.CompletedTask;
}

static Task AppConfigV2TimingAndCoordinateSettingsBoundaries()
{
    AssertAppConfigV2NoErrors(
        ValidateAppConfigV2(AppConfigV2.Create(
            [SimpleMappingConfig("null-timing", "q", "a")],
            defaultTiming: new TimingSettingsV2())),
        "null timing settings mean no override.");

    AssertAppConfigV2NoErrors(
        ValidateAppConfigV2(AppConfigV2.Create(
            [SimpleMappingConfig("zero-timing", "q", "a")],
            defaultTiming: new TimingSettingsV2(
                TapKeyDuration: TimeSpan.Zero,
                TapMouseDuration: TimeSpan.Zero,
                CoordinateTapDuration: TimeSpan.Zero,
                DefaultWhileHeldInterval: TimeSpan.Zero))),
        "0ms timing overrides are allowed and align with Action Model v2 duration policy.");

    AssertAppConfigV2NoErrors(
        ValidateAppConfigV2(AppConfigV2.Create(
        [
            new MappingConfigV2(
                "mapping-overrides",
                "Mapping overrides",
                Enabled: true,
                TriggerConfigV2.SingleInput(InputNameResolverV2.Resolve("q")),
                SimpleActionSource("a"),
                TimingOverride: new TimingSettingsV2(TapKeyDuration: TimeSpan.Zero),
                CoordinateSettingsOverride: new CoordinateSettingsV2(
                    "ba16_1920x1080",
                    CoordinateExecutionPolicyV2.PreferCursorPreserving))
        ],
        defaultCoordinateSettings: new CoordinateSettingsV2("ba43_1440x1080"))),
        "valid default and mapping-level coordinate/timing overrides should validate.");

    AssertAppConfigV2Error(
        AppConfigV2.Create(
            [SimpleMappingConfig("bad-policy", "q", "a")],
            defaultCoordinateSettings: new CoordinateSettingsV2(
                "ba16_1920x1080",
                (CoordinateExecutionPolicyV2)999)),
        AppConfigV2DiagnosticCode.InvalidCoordinateSettings,
        "invalid coordinate execution policy enum should be rejected.",
        "defaults.coordinate.execution_policy");

    var allowRealCursor = new CoordinateSettingsV2(
        "ba16_1920x1080",
        CoordinateExecutionPolicyV2.AllowRealCursor,
        CoordinateTransformPolicyV2.DynamicPerStep);
    AssertTrue(!allowRealCursor.ClaimsLiveReadiness,
        "allow_real_cursor and transform policy are data-only and do not execute.");

    return Task.CompletedTask;
}

static Task AppConfigV2JsonSerializerParsesDraftShape()
{
    const string json = """
        {
          "version": 2,
          "control_hotkey": "f5",
          "coordinate_record_hotkey": "mouse_x1",
          "emergency_stop_hotkey": "f12",
          "defaults": {
            "timing": {
              "tap_key_duration": "30ms",
              "tap_mouse_duration": "30ms",
              "coordinate_tap_duration": "30ms",
              "default_while_held_interval": "50ms"
            },
            "coordinate": {
              "profile": "ba16_1920x1080",
              "execution_policy": "strict_cursor_preserving",
              "transform_policy": "snapshot_per_gesture"
            }
          },
          "mappings": [
            {
              "id": "skill_1",
              "name": "Skill 1",
              "enabled": true,
              "trigger": {
                "kind": "single_input",
                "input": "ctrl_l"
              },
              "action": {
                "kind": "simple",
                "mode": "hold",
                "target": {
                  "kind": "input",
                  "input": "q"
                }
              }
            },
            {
              "id": "macro_draft",
              "name": "Macro draft",
              "enabled": false,
              "action": {
                "kind": "macro_dsl_v2",
                "source": ""
              }
            }
          ]
        }
        """;

    var result = new AppConfigV2JsonSerializer().Parse(json);

    AssertTrue(result.Config is not null, "valid AppConfigV2 JSON should produce a config.");
    AssertAppConfigV2NoErrors(result.Diagnostics, "valid AppConfigV2 JSON draft should have no error diagnostics.");
    AssertTrue(result.Config!.Version == AppConfigV2.CurrentVersion, "JSON version should parse as AppConfigV2 version 2.");
    AssertTrue(result.Config.ControlHotkey?.CanonicalText == "f5", "control hotkey should parse.");
    AssertTrue(result.Config.CoordinateRecordHotkey?.CanonicalText == "mouse_x1", "coordinate record hotkey should parse mouse_x1.");
    AssertTrue(result.Config.EmergencyStopHotkey?.CanonicalText == "f12", "emergency stop hotkey should parse.");
    AssertTrue(result.Config.DefaultTiming?.TapKeyDuration == TimeSpan.FromMilliseconds(30),
        "duration strings should parse into TimeSpan values.");
    AssertTrue(result.Config.DefaultCoordinateSettings?.ProfileId == "ba16_1920x1080",
        "coordinate profile should parse.");
    AssertTrue(result.Config.DefaultCoordinateSettings?.ClaimsLiveReadiness == false,
        "coordinate settings parsed from JSON remain data-only and do not claim live readiness.");

    var mapping = result.Config.Mappings[0];
    AssertTrue(mapping.Trigger?.Input?.CanonicalName == "left_ctrl",
        "JSON aliases should normalize through InputNameResolverV2.");
    AssertTrue(mapping.ActionSource is MappingActionSourceV2.SimpleMapping simple &&
            simple.Definition.Mode == SimpleMappingModeV2.Hold &&
            simple.Definition.Target.Input?.CanonicalName == "q",
        "simple mapping JSON source should parse into SimpleMappingDefinitionV2.");
    AssertAppConfigV2Diagnostic(
        result.Diagnostics,
        AppConfigV2DiagnosticSeverity.Warning,
        AppConfigV2DiagnosticCode.EmptyMacroSource,
        "empty macro source shell should remain a warning after JSON parse.",
        "mappings[1].action.macro_dsl_v2.source");
    AssertTrue(result.Success, "warning-only AppConfigV2 JSON parse should be successful.");
    AssertTrue(result.CanSaveDraft, "warning-only AppConfigV2 JSON parse should be saveable as a draft.");
    AssertTrue(!result.BlocksSave, "warning-only AppConfigV2 JSON parse should not block draft save.");

    return Task.CompletedTask;
}

static Task AppConfigV2JsonSerializerRoundTripsCanonicalNames()
{
    var config = AppConfigV2.Create(
    [
        new MappingConfigV2(
            "hold-left-ctrl",
            "Hold Left Ctrl",
            Enabled: true,
            TriggerConfigV2.SingleInput(InputNameResolverV2.Resolve("page up")),
            new MappingActionSourceV2.SimpleMapping(new SimpleMappingDefinitionV2(
                "hold-left-ctrl",
                SimpleMappingModeV2.Tap,
                SimpleMappingTargetV2.FromInput(InputNameResolverV2.Resolve("ctrl_l")),
                TimeSpan.Zero)),
            TimingOverride: new TimingSettingsV2(TapKeyDuration: TimeSpan.Zero),
            CoordinateSettingsOverride: new CoordinateSettingsV2(
                "ba43_1440x1080",
                CoordinateExecutionPolicyV2.PreferCursorPreserving,
                CoordinateTransformPolicyV2.DynamicPerStep))
    ],
    controlHotkey: HotkeySpecV2.Parse("left_alt+f8"),
    coordinateRecordHotkey: CoordinateRecordHotkeySpecV2.Parse("left_ctrl+mouse_x1"),
    emergencyStopHotkey: HotkeySpecV2.Parse("f12"),
    defaultTiming: new TimingSettingsV2(DefaultWhileHeldInterval: TimeSpan.FromMilliseconds(50)),
    defaultCoordinateSettings: new CoordinateSettingsV2("ba16_1920x1080"));

    var serializer = new AppConfigV2JsonSerializer();
    var json = serializer.Serialize(config);

    AssertTrue(json.Contains("\"page_up\"", StringComparison.Ordinal),
        "serializer should emit canonical trigger input names.");
    AssertTrue(json.Contains("\"left_ctrl\"", StringComparison.Ordinal),
        "serializer should emit side-specific canonical target names.");
    AssertTrue(json.Contains("\"0ms\"", StringComparison.Ordinal),
        "serializer should emit zero millisecond duration explicitly.");

    var roundTrip = serializer.Parse(json);
    AssertTrue(roundTrip.Config is not null, "serialized AppConfigV2 JSON should parse back.");
    AssertAppConfigV2NoErrors(roundTrip.Diagnostics, "round-tripped AppConfigV2 JSON should validate.");
    AssertTrue(roundTrip.Config!.Mappings[0].Trigger?.Input?.CanonicalName == "page_up",
        "roundtrip should preserve canonical trigger input.");
    AssertTrue(roundTrip.Config.Mappings[0].ActionSource is MappingActionSourceV2.SimpleMapping simple &&
            simple.Definition.Duration == TimeSpan.Zero &&
            simple.Definition.Target.Input?.CanonicalName == "left_ctrl",
        "roundtrip should preserve simple mapping duration and side-specific target.");
    AssertTrue(roundTrip.Config.CoordinateRecordHotkey?.CanonicalText == "left_ctrl+mouse_x1",
        "roundtrip should preserve coordinate record hotkey canonical text.");

    return Task.CompletedTask;
}

static Task AppConfigV2JsonSerializerReportsParseDiagnostics()
{
    const string json = """
        {
          "version": 2,
          "control_hotkey": "left_ctrl",
          "defaults": {
            "timing": {
              "tap_key_duration": "not-a-duration"
            }
          },
          "mappings": [
            {
              "id": "bad",
              "enabled": true,
              "trigger": {
                "kind": "single_input",
                "input": "not_a_key"
              },
              "action": {
                "kind": "simple",
                "mode": "tap",
                "target": {
                  "kind": "input",
                  "input": "q"
                }
              }
            }
          ]
        }
        """;

    var result = new AppConfigV2JsonSerializer().Parse(json);

    AssertTrue(result.Config is not null, "parse diagnostics should still return a draft config when possible.");
    AssertAppConfigV2Diagnostic(
        result.Diagnostics,
        AppConfigV2DiagnosticSeverity.Error,
        AppConfigV2DiagnosticCode.InvalidHotkey,
        "modifier-only control hotkey should be reported as a JSON parse diagnostic.",
        "control_hotkey");
    AssertAppConfigV2Diagnostic(
        result.Diagnostics,
        AppConfigV2DiagnosticSeverity.Error,
        AppConfigV2DiagnosticCode.InvalidTiming,
        "bad duration text should be reported with a timing path.",
        "defaults.timing.tap_key_duration");
    AssertAppConfigV2Diagnostic(
        result.Diagnostics,
        AppConfigV2DiagnosticSeverity.Error,
        AppConfigV2DiagnosticCode.InvalidInputName,
        "bad trigger input name should be reported with an input path.",
        "mappings[0].trigger.input");
    AssertTrue(result.HasErrors, "parse errors should be visible through result helper.");
    AssertTrue(result.BlocksSave, "parse errors should block save.");
    AssertTrue(result.BlocksLive, "parse errors should block live.");
    AssertTrue(!result.CanSaveDraft, "parse errors should not be saveable as draft.");

    return Task.CompletedTask;
}

static Task AppConfigV2JsonSerializerHardensDurationGrammar()
{
    foreach (var duration in new[] { "30ms", "1s", "0ms", "0.5s" })
    {
        var result = new AppConfigV2JsonSerializer().Parse(JsonWithDefaultTapKeyDuration(duration));
        AssertAppConfigV2NoErrors(result.Diagnostics, $"{duration} should be accepted as an explicit AppConfigV2 duration.");
    }

    foreach (var duration in new[] { "30", "00:00:01", "-1ms", "" })
    {
        var result = new AppConfigV2JsonSerializer().Parse(JsonWithDefaultTapKeyDuration(duration));
        AssertAppConfigV2Diagnostic(
            result.Diagnostics,
            AppConfigV2DiagnosticSeverity.Error,
            AppConfigV2DiagnosticCode.InvalidTiming,
            $"{duration} should be rejected by explicit AppConfigV2 duration grammar.",
            "defaults.timing.tap_key_duration");
    }

    return Task.CompletedTask;
}

static Task AppConfigV2JsonSerializerReportsMalformedStructure()
{
    var invalidRoot = new AppConfigV2JsonSerializer().Parse("[]");
    AssertTrue(invalidRoot.Config is null, "non-object AppConfigV2 JSON root should not produce a config.");
    AssertAppConfigV2Diagnostic(
        invalidRoot.Diagnostics,
        AppConfigV2DiagnosticSeverity.Error,
        AppConfigV2DiagnosticCode.InvalidJson,
        "invalid root should produce InvalidJson.",
        "$");

    var missingVersion = new AppConfigV2JsonSerializer().Parse("""
        {
          "mappings": []
        }
        """);
    AssertAppConfigV2Diagnostic(
        missingVersion.Diagnostics,
        AppConfigV2DiagnosticSeverity.Error,
        AppConfigV2DiagnosticCode.InvalidVersion,
        "missing version should be reported through validator.",
        "version");

    var invalidVersion = new AppConfigV2JsonSerializer().Parse("""
        {
          "version": 1,
          "mappings": []
        }
        """);
    AssertAppConfigV2Diagnostic(
        invalidVersion.Diagnostics,
        AppConfigV2DiagnosticSeverity.Error,
        AppConfigV2DiagnosticCode.InvalidVersion,
        "version other than 2 should be rejected.",
        "version");

    var malformedMapping = new AppConfigV2JsonSerializer().Parse("""
        {
          "version": 2,
          "mappings": [123]
        }
        """);
    AssertAppConfigV2Diagnostic(
        malformedMapping.Diagnostics,
        AppConfigV2DiagnosticSeverity.Error,
        AppConfigV2DiagnosticCode.InvalidJson,
        "malformed mapping entry should be diagnosed.",
        "mappings[0]");

    var malformedDefaults = new AppConfigV2JsonSerializer().Parse("""
        {
          "version": 2,
          "defaults": true,
          "mappings": []
        }
        """);
    AssertAppConfigV2Diagnostic(
        malformedDefaults.Diagnostics,
        AppConfigV2DiagnosticSeverity.Error,
        AppConfigV2DiagnosticCode.InvalidJson,
        "malformed defaults should be diagnosed.",
        "defaults");

    var malformedTiming = new AppConfigV2JsonSerializer().Parse("""
        {
          "version": 2,
          "defaults": {
            "timing": true
          },
          "mappings": []
        }
        """);
    AssertAppConfigV2Diagnostic(
        malformedTiming.Diagnostics,
        AppConfigV2DiagnosticSeverity.Error,
        AppConfigV2DiagnosticCode.InvalidTiming,
        "malformed defaults.timing should be diagnosed.",
        "defaults.timing");

    var malformedCoordinate = new AppConfigV2JsonSerializer().Parse("""
        {
          "version": 2,
          "defaults": {
            "coordinate": true
          },
          "mappings": []
        }
        """);
    AssertAppConfigV2Diagnostic(
        malformedCoordinate.Diagnostics,
        AppConfigV2DiagnosticSeverity.Error,
        AppConfigV2DiagnosticCode.InvalidCoordinateSettings,
        "malformed defaults.coordinate should be diagnosed.",
        "defaults.coordinate");

    return Task.CompletedTask;
}

static Task AppConfigV2JsonSerializerReportsFieldPaths()
{
    var unsupportedAction = new AppConfigV2JsonSerializer().Parse("""
        {
          "version": 2,
          "mappings": [
            {
              "id": "bad-action",
              "enabled": true,
              "trigger": {
                "kind": "single_input",
                "input": "q"
              },
              "action": {
                "kind": "unknown"
              }
            }
          ]
        }
        """);
    AssertAppConfigV2Diagnostic(
        unsupportedAction.Diagnostics,
        AppConfigV2DiagnosticSeverity.Error,
        AppConfigV2DiagnosticCode.InvalidActionSource,
        "unsupported action kind should point at action.kind.",
        "mappings[0].action.kind");

    var unknownTrigger = new AppConfigV2JsonSerializer().Parse(JsonWithTriggerAndTarget("not_a_key", "q"));
    AssertAppConfigV2Diagnostic(
        unknownTrigger.Diagnostics,
        AppConfigV2DiagnosticSeverity.Error,
        AppConfigV2DiagnosticCode.InvalidInputName,
        "unknown trigger input should point at trigger input JSON field.",
        "mappings[0].trigger.input");

    var unknownTarget = new AppConfigV2JsonSerializer().Parse(JsonWithTriggerAndTarget("q", "not_a_key"));
    AssertAppConfigV2Diagnostic(
        unknownTarget.Diagnostics,
        AppConfigV2DiagnosticSeverity.Error,
        AppConfigV2DiagnosticCode.InvalidInputName,
        "unknown simple target input should point at action target input JSON field.",
        "mappings[0].action.target.input");

    var unsupportedTarget = new AppConfigV2JsonSerializer().Parse("""
        {
          "version": 2,
          "mappings": [
            {
              "id": "bad-target",
              "enabled": true,
              "trigger": {
                "kind": "single_input",
                "input": "q"
              },
              "action": {
                "kind": "simple",
                "mode": "tap",
                "target": {
                  "kind": "unknown"
                }
              }
            }
          ]
        }
        """);
    AssertAppConfigV2Diagnostic(
        unsupportedTarget.Diagnostics,
        AppConfigV2DiagnosticSeverity.Error,
        AppConfigV2DiagnosticCode.InvalidSimpleMappingSource,
        "unsupported simple target kind should point at action target kind JSON field.",
        "mappings[0].action.target.kind");

    var hotkeys = new AppConfigV2JsonSerializer().Parse("""
        {
          "version": 2,
          "control_hotkey": "not_a_key+f8",
          "coordinate_record_hotkey": "left_ctrl",
          "emergency_stop_hotkey": "right_shift",
          "mappings": []
        }
        """);
    AssertAppConfigV2Diagnostic(
        hotkeys.Diagnostics,
        AppConfigV2DiagnosticSeverity.Error,
        AppConfigV2DiagnosticCode.InvalidHotkey,
        "malformed control hotkey should point at control_hotkey.",
        "control_hotkey");
    AssertAppConfigV2Diagnostic(
        hotkeys.Diagnostics,
        AppConfigV2DiagnosticSeverity.Error,
        AppConfigV2DiagnosticCode.InvalidHotkey,
        "modifier-only coordinate record hotkey should point at coordinate_record_hotkey.",
        "coordinate_record_hotkey");
    AssertAppConfigV2Diagnostic(
        hotkeys.Diagnostics,
        AppConfigV2DiagnosticSeverity.Error,
        AppConfigV2DiagnosticCode.InvalidHotkey,
        "modifier-only emergency stop hotkey should point at emergency_stop_hotkey.",
        "emergency_stop_hotkey");

    var coordinate = new AppConfigV2JsonSerializer().Parse("""
        {
          "version": 2,
          "defaults": {
            "coordinate": {
              "profile": "",
              "execution_policy": "nope"
            }
          },
          "mappings": []
        }
        """);
    AssertAppConfigV2Diagnostic(
        coordinate.Diagnostics,
        AppConfigV2DiagnosticSeverity.Error,
        AppConfigV2DiagnosticCode.InvalidCoordinateSettings,
        "empty coordinate profile should point at JSON profile field.",
        "defaults.coordinate.profile");
    AssertAppConfigV2Diagnostic(
        coordinate.Diagnostics,
        AppConfigV2DiagnosticSeverity.Error,
        AppConfigV2DiagnosticCode.InvalidCoordinateSettings,
        "invalid coordinate execution policy should point at JSON execution_policy field.",
        "defaults.coordinate.execution_policy");

    return Task.CompletedTask;
}

static Task AppConfigV2JsonSerializerKeepsUnknownFieldsDraftOnly()
{
    const string json = """
        {
          "version": 2,
          "unknown_root": "ignored",
          "mappings": [
            {
              "id": "disabled-simple",
              "name": "Disabled simple",
              "enabled": false,
              "unknown_mapping": "ignored",
              "action": {
                "kind": "simple",
                "mode": "tap",
                "target": {
                  "kind": "input",
                  "input": "q"
                }
              }
            },
            {
              "id": "macro",
              "name": "Macro",
              "enabled": false,
              "action": {
                "kind": "macro_dsl_v2",
                "source": "on_down\nend"
              }
            }
          ]
        }
        """;

    var serializer = new AppConfigV2JsonSerializer();
    var result = serializer.Parse(json);
    AssertTrue(result.Config is not null, "unknown fields should not crash the draft parser.");
    AssertAppConfigV2NoErrors(result.Diagnostics, "unknown fields should be ignored in the draft serializer.");
    AssertTrue(result.Config!.Mappings[0].Trigger is null,
        "disabled mapping without trigger should remain valid after JSON parse.");

    var emitted = serializer.Serialize(result.Config);
    AssertTrue(!emitted.Contains("unknown_root", StringComparison.Ordinal),
        "draft serializer should not preserve unknown root fields.");
    AssertTrue(!emitted.Contains("unknown_mapping", StringComparison.Ordinal),
        "draft serializer should not preserve unknown mapping fields.");
    AssertTrue(emitted.Contains("on_down\\nend", StringComparison.Ordinal),
        "macro source text should still be emitted.");

    var roundTrip = serializer.Parse(emitted);
    AssertAppConfigV2NoErrors(roundTrip.Diagnostics, "emitted known-field JSON should parse back cleanly.");
    AssertTrue(roundTrip.Config?.Mappings[0].Trigger is null,
        "disabled mapping without trigger should roundtrip.");
    AssertTrue(roundTrip.Config?.Mappings[1].ActionSource is MappingActionSourceV2.MacroDslV2Source macro &&
            macro.SourceText == "on_down\nend",
        "macro source text should roundtrip.");

    return Task.CompletedTask;
}

static Task AppConfigV2JsonSerializerPreservesConflictMetadata()
{
    var duplicate = new AppConfigV2JsonSerializer().Parse("""
        {
          "version": 2,
          "mappings": [
            {
              "id": "first",
              "enabled": true,
              "trigger": { "kind": "single_input", "input": "q" },
              "action": { "kind": "simple", "mode": "tap", "target": { "kind": "input", "input": "a" } }
            },
            {
              "id": "second",
              "enabled": true,
              "trigger": { "kind": "single_input", "input": "q" },
              "action": { "kind": "simple", "mode": "tap", "target": { "kind": "input", "input": "b" } }
            }
          ]
        }
        """);
    AssertAppConfigV2Conflict(
        duplicate.Diagnostics,
        InputConflictCode.DuplicateMappingTrigger,
        ["mapping:first:trigger", "mapping:second:trigger"],
        ["mappings[0].trigger", "mappings[1].trigger"],
        "duplicate trigger conflict metadata should survive JSON parse.");

    var emergencyVsControl = new AppConfigV2JsonSerializer().Parse("""
        {
          "version": 2,
          "control_hotkey": "left_ctrl+f12",
          "emergency_stop_hotkey": "left_ctrl+f12",
          "mappings": []
        }
        """);
    AssertAppConfigV2Conflict(
        emergencyVsControl.Diagnostics,
        InputConflictCode.EmergencyStopHotkeyConflictsWithBinding,
        ["control_hotkey", "emergency_stop_hotkey"],
        ["control_hotkey", "emergency_stop_hotkey"],
        "emergency vs control conflict metadata should survive JSON parse.");

    var emergencyVsCoordinate = new AppConfigV2JsonSerializer().Parse("""
        {
          "version": 2,
          "coordinate_record_hotkey": "f8",
          "emergency_stop_hotkey": "f8",
          "mappings": []
        }
        """);
    AssertAppConfigV2Conflict(
        emergencyVsCoordinate.Diagnostics,
        InputConflictCode.EmergencyStopHotkeyConflictsWithBinding,
        ["coordinate_record_hotkey", "emergency_stop_hotkey"],
        ["coordinate_record_hotkey", "emergency_stop_hotkey"],
        "emergency vs coordinate record conflict metadata should survive JSON parse.");

    return Task.CompletedTask;
}

static Task AppConfigV2JsonSerializerResultHelpersHandleWarningsAndErrors()
{
    var serializer = new AppConfigV2JsonSerializer();

    var warningOnly = serializer.Parse("""
        {
          "version": 2,
          "mappings": [
            {
              "id": "macro-draft",
              "enabled": false,
              "action": {
                "kind": "macro_dsl_v2",
                "source": ""
              }
            }
          ]
        }
        """);
    AssertTrue(!warningOnly.HasErrors, "warning-only serializer result should not report errors.");
    AssertTrue(!warningOnly.BlocksSave, "warning-only serializer result should not block draft save.");
    AssertTrue(warningOnly.CanSaveDraft, "warning-only serializer result should be saveable as a draft.");
    AssertAppConfigV2Diagnostic(
        warningOnly.Diagnostics,
        AppConfigV2DiagnosticSeverity.Warning,
        AppConfigV2DiagnosticCode.EmptyMacroSource,
        "empty macro source should remain a warning-only draft diagnostic.",
        "mappings[0].action.macro_dsl_v2.source");

    var errorOnly = serializer.Parse(JsonWithTriggerAndTarget("not_a_key", "q"));
    AssertTrue(errorOnly.HasErrors, "error serializer result should report errors.");
    AssertTrue(errorOnly.BlocksSave, "error serializer result should block draft save.");
    AssertTrue(!errorOnly.CanSaveDraft, "error serializer result should not be saveable as a draft.");

    var warningAndError = serializer.Parse("""
        {
          "version": 2,
          "control_hotkey": "left_ctrl",
          "mappings": [
            {
              "id": "macro-draft",
              "enabled": false,
              "action": {
                "kind": "macro_dsl_v2",
                "source": ""
              }
            }
          ]
        }
        """);
    AssertAppConfigV2Diagnostic(
        warningAndError.Diagnostics,
        AppConfigV2DiagnosticSeverity.Warning,
        AppConfigV2DiagnosticCode.EmptyMacroSource,
        "mixed result should retain macro warning.",
        "mappings[0].action.macro_dsl_v2.source");
    AssertAppConfigV2Diagnostic(
        warningAndError.Diagnostics,
        AppConfigV2DiagnosticSeverity.Error,
        AppConfigV2DiagnosticCode.InvalidHotkey,
        "mixed result should retain control hotkey error.",
        "control_hotkey");
    AssertTrue(warningAndError.HasErrors, "warning plus error result should report errors.");
    AssertTrue(warningAndError.BlocksSave, "warning plus error result should block draft save.");
    AssertTrue(!warningAndError.CanSaveDraft, "warning plus error result should not be saveable.");

    return Task.CompletedTask;
}

static Task AppConfigV2JsonSerializerRetainsMultipleDiagnostics()
{
    var parseDiagnostics = new AppConfigV2JsonSerializer().Parse("""
        {
          "version": 2,
          "control_hotkey": "left_ctrl",
          "mappings": [
            {
              "id": "bad",
              "enabled": true,
              "trigger": {
                "kind": "single_input",
                "input": "not_a_key"
              },
              "action": {
                "kind": "simple",
                "mode": "tap",
                "target": {
                  "kind": "input",
                  "input": "q"
                }
              }
            }
          ]
        }
        """);
    AssertAppConfigV2Diagnostic(
        parseDiagnostics.Diagnostics,
        AppConfigV2DiagnosticSeverity.Error,
        AppConfigV2DiagnosticCode.InvalidHotkey,
        "multiple parse diagnostics should retain malformed hotkey.",
        "control_hotkey");
    AssertAppConfigV2Diagnostic(
        parseDiagnostics.Diagnostics,
        AppConfigV2DiagnosticSeverity.Error,
        AppConfigV2DiagnosticCode.InvalidInputName,
        "multiple parse diagnostics should retain unknown trigger input.",
        "mappings[0].trigger.input");

    var validationConflicts = new AppConfigV2JsonSerializer().Parse("""
        {
          "version": 2,
          "control_hotkey": "f12",
          "emergency_stop_hotkey": "f12",
          "mappings": [
            {
              "id": "first",
              "enabled": true,
              "trigger": { "kind": "single_input", "input": "q" },
              "action": { "kind": "simple", "mode": "tap", "target": { "kind": "input", "input": "a" } }
            },
            {
              "id": "second",
              "enabled": true,
              "trigger": { "kind": "single_input", "input": "q" },
              "action": { "kind": "simple", "mode": "tap", "target": { "kind": "input", "input": "b" } }
            }
          ]
        }
        """);
    AssertAppConfigV2Conflict(
        validationConflicts.Diagnostics,
        InputConflictCode.DuplicateMappingTrigger,
        ["mapping:first:trigger", "mapping:second:trigger"],
        ["mappings[0].trigger", "mappings[1].trigger"],
        "multiple validation conflicts should retain duplicate trigger metadata.");
    AssertAppConfigV2Conflict(
        validationConflicts.Diagnostics,
        InputConflictCode.EmergencyStopHotkeyConflictsWithBinding,
        ["control_hotkey", "emergency_stop_hotkey"],
        ["control_hotkey", "emergency_stop_hotkey"],
        "multiple validation conflicts should retain emergency stop metadata.");

    return Task.CompletedTask;
}

static Task AppConfigV2JsonSerializerReportsNestedMalformedObjectPaths()
{
    var malformedAction = new AppConfigV2JsonSerializer().Parse("""
        {
          "version": 2,
          "mappings": [
            {
              "id": "bad-action",
              "enabled": true,
              "trigger": { "kind": "single_input", "input": "q" },
              "action": true
            }
          ]
        }
        """);
    AssertAppConfigV2Diagnostic(
        malformedAction.Diagnostics,
        AppConfigV2DiagnosticSeverity.Error,
        AppConfigV2DiagnosticCode.InvalidActionSource,
        "non-object action should point at mappings[0].action.",
        "mappings[0].action");

    var malformedTarget = new AppConfigV2JsonSerializer().Parse("""
        {
          "version": 2,
          "mappings": [
            {
              "id": "bad-target",
              "enabled": true,
              "trigger": { "kind": "single_input", "input": "q" },
              "action": {
                "kind": "simple",
                "mode": "tap",
                "target": true
              }
            }
          ]
        }
        """);
    AssertAppConfigV2Diagnostic(
        malformedTarget.Diagnostics,
        AppConfigV2DiagnosticSeverity.Error,
        AppConfigV2DiagnosticCode.InvalidSimpleMappingSource,
        "non-object simple target should point at mappings[0].action.target.",
        "mappings[0].action.target");

    var malformedTrigger = new AppConfigV2JsonSerializer().Parse("""
        {
          "version": 2,
          "mappings": [
            {
              "id": "bad-trigger",
              "enabled": true,
              "trigger": true,
              "action": { "kind": "simple", "mode": "tap", "target": { "kind": "input", "input": "q" } }
            }
          ]
        }
        """);
    AssertAppConfigV2Diagnostic(
        malformedTrigger.Diagnostics,
        AppConfigV2DiagnosticSeverity.Error,
        AppConfigV2DiagnosticCode.InvalidTrigger,
        "non-object trigger should point at mappings[0].trigger.",
        "mappings[0].trigger");

    var malformedMappingTiming = new AppConfigV2JsonSerializer().Parse("""
        {
          "version": 2,
          "mappings": [
            {
              "id": "bad-timing",
              "enabled": true,
              "trigger": { "kind": "single_input", "input": "q" },
              "timing": true,
              "action": { "kind": "simple", "mode": "tap", "target": { "kind": "input", "input": "a" } }
            }
          ]
        }
        """);
    AssertAppConfigV2Diagnostic(
        malformedMappingTiming.Diagnostics,
        AppConfigV2DiagnosticSeverity.Error,
        AppConfigV2DiagnosticCode.InvalidTiming,
        "non-object mapping timing should point at mappings[0].timing.",
        "mappings[0].timing");

    var malformedMappingCoordinate = new AppConfigV2JsonSerializer().Parse("""
        {
          "version": 2,
          "mappings": [
            {
              "id": "bad-coordinate",
              "enabled": true,
              "trigger": { "kind": "single_input", "input": "q" },
              "coordinate": true,
              "action": { "kind": "simple", "mode": "tap", "target": { "kind": "input", "input": "a" } }
            }
          ]
        }
        """);
    AssertAppConfigV2Diagnostic(
        malformedMappingCoordinate.Diagnostics,
        AppConfigV2DiagnosticSeverity.Error,
        AppConfigV2DiagnosticCode.InvalidCoordinateSettings,
        "non-object mapping coordinate override should point at mappings[0].coordinate.",
        "mappings[0].coordinate");

    return Task.CompletedTask;
}

static Task AppConfigV2JsonSerializerIgnoresNestedUnknownFieldsDraftOnly()
{
    const string json = """
        {
          "version": 2,
          "unknown_root": true,
          "defaults": {
            "unknown_defaults": true,
            "timing": {
              "tap_key_duration": "30ms",
              "unknown_timing": true
            },
            "coordinate": {
              "profile": "ba16_1920x1080",
              "unknown_coordinate": true
            }
          },
          "mappings": [
            {
              "id": "unknowns",
              "name": "Unknowns",
              "enabled": true,
              "unknown_mapping": true,
              "trigger": { "kind": "single_input", "input": "q" },
              "action": {
                "kind": "simple",
                "mode": "tap",
                "unknown_action": true,
                "target": {
                  "kind": "input",
                  "input": "a",
                  "unknown_target": true
                }
              }
            }
          ]
        }
        """;

    // Draft behavior only: unknown fields are ignored on parse and not emitted.
    // Production AppConfigV2 migration still needs an unknown-field
    // preservation policy before this serializer can be used as the GUI
    // document source of truth.
    var serializer = new AppConfigV2JsonSerializer();
    var result = serializer.Parse(json);
    AssertTrue(result.Config is not null, "nested unknown fields should not crash the draft parser.");
    AssertAppConfigV2NoErrors(result.Diagnostics, "nested unknown fields should be ignored in the draft serializer.");

    var emitted = serializer.Serialize(result.Config!);
    foreach (var field in new[]
    {
        "unknown_root",
        "unknown_defaults",
        "unknown_timing",
        "unknown_coordinate",
        "unknown_mapping",
        "unknown_action",
        "unknown_target"
    })
    {
        AssertTrue(!emitted.Contains(field, StringComparison.Ordinal),
            $"draft serializer should not preserve unknown field {field}.");
    }

    return Task.CompletedTask;
}

static Task AppConfigV2JsonSerializerNormalizesCoordinateProfilePaths()
{
    var defaultCoordinate = new AppConfigV2JsonSerializer().Parse("""
        {
          "version": 2,
          "defaults": {
            "coordinate": {
              "profile": ""
            }
          },
          "mappings": []
        }
        """);
    AssertAppConfigV2Diagnostic(
        defaultCoordinate.Diagnostics,
        AppConfigV2DiagnosticSeverity.Error,
        AppConfigV2DiagnosticCode.InvalidCoordinateSettings,
        "empty default coordinate profile should expose JSON-facing profile path.",
        "defaults.coordinate.profile");
    AssertTrue(!defaultCoordinate.Diagnostics.Any(diagnostic => diagnostic.Path == "defaults.coordinate.profile_id"),
        "serializer-facing diagnostics should not expose internal ProfileId path for default coordinate settings.");

    var mappingCoordinate = new AppConfigV2JsonSerializer().Parse("""
        {
          "version": 2,
          "mappings": [
            {
              "id": "bad-coordinate",
              "enabled": true,
              "trigger": { "kind": "single_input", "input": "q" },
              "coordinate": {
                "profile": ""
              },
              "action": { "kind": "simple", "mode": "tap", "target": { "kind": "input", "input": "a" } }
            }
          ]
        }
        """);
    AssertAppConfigV2Diagnostic(
        mappingCoordinate.Diagnostics,
        AppConfigV2DiagnosticSeverity.Error,
        AppConfigV2DiagnosticCode.InvalidCoordinateSettings,
        "empty mapping coordinate profile should expose JSON-facing profile path.",
        "mappings[0].coordinate.profile");
    AssertTrue(!mappingCoordinate.Diagnostics.Any(diagnostic => diagnostic.Path == "mappings[0].coordinate.profile_id"),
        "serializer-facing diagnostics should not expose internal ProfileId path for mapping coordinate settings.");

    return Task.CompletedTask;
}

static Task AppConfigV2JsonSerializerDisabledMappingConflictSemantics()
{
    var disabledDuplicateTrigger = new AppConfigV2JsonSerializer().Parse("""
        {
          "version": 2,
          "mappings": [
            {
              "id": "enabled",
              "enabled": true,
              "trigger": { "kind": "single_input", "input": "q" },
              "action": { "kind": "simple", "mode": "tap", "target": { "kind": "input", "input": "a" } }
            },
            {
              "id": "disabled",
              "enabled": false,
              "trigger": { "kind": "single_input", "input": "q" },
              "action": { "kind": "simple", "mode": "tap", "target": { "kind": "input", "input": "b" } }
            }
          ]
        }
        """);
    AssertAppConfigV2NoErrors(
        disabledDuplicateTrigger.Diagnostics,
        "disabled mapping trigger should not participate in duplicate trigger conflicts.");

    var disabledGlobalHotkeyOverlap = new AppConfigV2JsonSerializer().Parse("""
        {
          "version": 2,
          "control_hotkey": "f5",
          "mappings": [
            {
              "id": "enabled",
              "enabled": true,
              "trigger": { "kind": "single_input", "input": "q" },
              "action": { "kind": "simple", "mode": "tap", "target": { "kind": "input", "input": "a" } }
            },
            {
              "id": "disabled",
              "enabled": false,
              "trigger": { "kind": "single_input", "input": "f5" },
              "action": { "kind": "simple", "mode": "tap", "target": { "kind": "input", "input": "b" } }
            }
          ]
        }
        """);
    AssertAppConfigV2NoErrors(
        disabledGlobalHotkeyOverlap.Diagnostics,
        "disabled mapping trigger should not conflict with global hotkeys.");

    var disabledStructureErrors = new AppConfigV2JsonSerializer().Parse("""
        {
          "version": 2,
          "mappings": [
            {
              "id": "disabled-bad",
              "enabled": false,
              "trigger": { "kind": "single_input", "input": "q" },
              "timing": { "tap_key_duration": "-1ms" },
              "coordinate": { "profile": "" },
              "action": { "kind": "simple", "mode": "tap", "target": { "kind": "input", "input": "a" } }
            }
          ]
        }
        """);
    AssertAppConfigV2Diagnostic(
        disabledStructureErrors.Diagnostics,
        AppConfigV2DiagnosticSeverity.Error,
        AppConfigV2DiagnosticCode.InvalidTiming,
        "disabled mapping should still report invalid timing structure.",
        "mappings[0].timing.tap_key_duration");
    AssertAppConfigV2Diagnostic(
        disabledStructureErrors.Diagnostics,
        AppConfigV2DiagnosticSeverity.Error,
        AppConfigV2DiagnosticCode.InvalidCoordinateSettings,
        "disabled mapping should still report invalid coordinate structure.",
        "mappings[0].coordinate.profile");

    return Task.CompletedTask;
}

static Task AppConfigV2JsonSerializerReportsMissingActionEdges()
{
    var enabledMissingAction = new AppConfigV2JsonSerializer().Parse("""
        {
          "version": 2,
          "mappings": [
            {
              "id": "enabled-missing-action",
              "enabled": true,
              "trigger": { "kind": "single_input", "input": "q" }
            }
          ]
        }
        """);
    AssertAppConfigV2Diagnostic(
        enabledMissingAction.Diagnostics,
        AppConfigV2DiagnosticSeverity.Error,
        AppConfigV2DiagnosticCode.InvalidActionSource,
        "enabled mapping missing action should point at mappings[0].action.",
        "mappings[0].action");

    var disabledMissingAction = new AppConfigV2JsonSerializer().Parse("""
        {
          "version": 2,
          "mappings": [
            {
              "id": "disabled-missing-action",
              "enabled": false
            }
          ]
        }
        """);
    AssertAppConfigV2Diagnostic(
        disabledMissingAction.Diagnostics,
        AppConfigV2DiagnosticSeverity.Error,
        AppConfigV2DiagnosticCode.InvalidActionSource,
        "disabled mapping still performs structural validation and reports missing action.",
        "mappings[0].action");

    return Task.CompletedTask;
}

static Task AppConfigV2JsonSerializerDuplicateTriggerAffectedPathsAreStable()
{
    var result = new AppConfigV2JsonSerializer().Parse("""
        {
          "version": 2,
          "mappings": [
            {
              "id": "first",
              "enabled": true,
              "trigger": { "kind": "single_input", "input": "q" },
              "action": { "kind": "simple", "mode": "tap", "target": { "kind": "input", "input": "a" } }
            },
            {
              "id": "second",
              "enabled": true,
              "trigger": { "kind": "single_input", "input": "q" },
              "action": { "kind": "simple", "mode": "tap", "target": { "kind": "input", "input": "b" } }
            },
            {
              "id": "third",
              "enabled": true,
              "trigger": { "kind": "single_input", "input": "q" },
              "action": { "kind": "simple", "mode": "tap", "target": { "kind": "input", "input": "c" } }
            }
          ]
        }
        """);

    var duplicateDiagnostics = result.Diagnostics
        .Where(diagnostic => diagnostic.OriginalConflictCode == InputConflictCode.DuplicateMappingTrigger)
        .ToArray();
    AssertTrue(duplicateDiagnostics.Length > 0,
        "three duplicate triggers should produce duplicate trigger conflict diagnostics.");

    foreach (var id in new[] { "mapping:first:trigger", "mapping:second:trigger", "mapping:third:trigger" })
    {
        AssertTrue(duplicateDiagnostics.Any(diagnostic => diagnostic.AffectedBindingIds.Contains(id)),
            $"duplicate trigger diagnostics should preserve affected id {id}.");
    }

    foreach (var path in new[] { "mappings[0].trigger", "mappings[1].trigger", "mappings[2].trigger" })
    {
        AssertTrue(duplicateDiagnostics.Any(diagnostic => diagnostic.AffectedPaths.Contains(path)),
            $"duplicate trigger diagnostics should preserve affected path {path}.");
    }

    AssertTrue(duplicateDiagnostics.All(diagnostic => diagnostic.BlocksSave && diagnostic.BlocksLive),
        "duplicate trigger diagnostics should keep blocks save/live metadata.");

    return Task.CompletedTask;
}

static Task AppConfigV2JsonSerializerDurationEdgesRemainStable()
{
    foreach (var duration in new[] { "00:00:01", "30", "-1ms" })
    {
        var result = new AppConfigV2JsonSerializer().Parse(JsonWithDefaultTapKeyDuration(duration));
        AssertAppConfigV2Diagnostic(
            result.Diagnostics,
            AppConfigV2DiagnosticSeverity.Error,
            AppConfigV2DiagnosticCode.InvalidTiming,
            $"{duration} should remain invalid after serializer hardening.",
            "defaults.timing.tap_key_duration");
    }

    var zero = new AppConfigV2JsonSerializer().Parse(JsonWithDefaultTapKeyDuration("0ms"));
    AssertAppConfigV2NoErrors(zero.Diagnostics, "0ms should remain a valid explicit duration.");

    var nonIntegerMillisecond = AppConfigV2.Create(
        [],
        defaultTiming: new TimingSettingsV2(
            TapKeyDuration: TimeSpan.FromTicks(TimeSpan.TicksPerMillisecond / 2)));
    var serializer = new AppConfigV2JsonSerializer();
    var emitted = serializer.Serialize(nonIntegerMillisecond);
    AssertTrue(emitted.Contains("\"0.0005s\"", StringComparison.Ordinal),
        "non-integer millisecond duration should emit deterministically as a seconds unit string.");
    var roundTrip = serializer.Parse(emitted);
    AssertAppConfigV2NoErrors(roundTrip.Diagnostics,
        "deterministic non-integer millisecond duration output should parse back.");
    AssertTrue(roundTrip.Config?.DefaultTiming?.TapKeyDuration == TimeSpan.FromTicks(TimeSpan.TicksPerMillisecond / 2),
        "non-integer millisecond duration should roundtrip exactly at tick precision used by the draft serializer.");

    return Task.CompletedTask;
}

static string JsonWithDefaultTapKeyDuration(string duration)
{
    return $$"""
        {
          "version": 2,
          "defaults": {
            "timing": {
              "tap_key_duration": "{{duration}}"
            }
          },
          "mappings": []
        }
        """;
}

static string JsonWithTriggerAndTarget(string trigger, string target)
{
    return $$"""
        {
          "version": 2,
          "mappings": [
            {
              "id": "mapping",
              "enabled": true,
              "trigger": {
                "kind": "single_input",
                "input": "{{trigger}}"
              },
              "action": {
                "kind": "simple",
                "mode": "tap",
                "target": {
                  "kind": "input",
                  "input": "{{target}}"
                }
              }
            }
          ]
        }
        """;
}

static MappingConfigV2 SimpleMappingConfig(
    string id,
    string trigger,
    string target,
    SimpleMappingModeV2 mode = SimpleMappingModeV2.Tap,
    TimeSpan? duration = null)
{
    return new MappingConfigV2(
        id,
        id,
        Enabled: true,
        TriggerConfigV2.SingleInput(InputNameResolverV2.Resolve(trigger)),
        SimpleActionSource(target, mode, duration));
}

static MappingActionSourceV2 SimpleActionSource(
    string target,
    SimpleMappingModeV2 mode = SimpleMappingModeV2.Tap,
    TimeSpan? duration = null)
{
    return new MappingActionSourceV2.SimpleMapping(SimpleDefinition(target, mode, duration));
}

static SimpleMappingDefinitionV2 SimpleDefinition(
    string target,
    SimpleMappingModeV2 mode = SimpleMappingModeV2.Tap,
    TimeSpan? duration = null)
{
    return new SimpleMappingDefinitionV2(
        $"simple:{target}:{mode}",
        mode,
        SimpleMappingTargetV2.FromInput(InputNameResolverV2.Resolve(target)),
        duration);
}

static void AssertAppConfigV2NoErrors(
    IReadOnlyList<AppConfigV2Diagnostic> diagnostics,
    string message)
{
    var errors = diagnostics
        .Where(diagnostic => diagnostic.Severity == AppConfigV2DiagnosticSeverity.Error)
        .Select(diagnostic => $"{diagnostic.Code} at {diagnostic.Path}: {diagnostic.Message}")
        .ToArray();

    AssertTrue(errors.Length == 0, $"{message}{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
}

static void AssertAppConfigV2Error(
    AppConfigV2 config,
    AppConfigV2DiagnosticCode code,
    string message,
    string? path = null)
{
    var diagnostics = ValidateAppConfigV2(config);
    AssertTrue(diagnostics.Any(diagnostic =>
            diagnostic.Severity == AppConfigV2DiagnosticSeverity.Error &&
            diagnostic.Code == code &&
            (path is null || diagnostic.Path == path) &&
            !string.IsNullOrWhiteSpace(diagnostic.Message)),
        message);
}

static IReadOnlyList<AppConfigV2Diagnostic> ValidateAppConfigV2(AppConfigV2 config)
{
    return new AppConfigV2Validator().Validate(config);
}

static void AssertAppConfigV2Diagnostic(
    IReadOnlyList<AppConfigV2Diagnostic> diagnostics,
    AppConfigV2DiagnosticSeverity severity,
    AppConfigV2DiagnosticCode code,
    string message,
    string? path = null)
{
    AssertTrue(diagnostics.Any(diagnostic =>
            diagnostic.Severity == severity &&
            diagnostic.Code == code &&
            (path is null || diagnostic.Path == path) &&
            !string.IsNullOrWhiteSpace(diagnostic.Message)),
        message);
}

static void AssertAppConfigV2Conflict(
    IReadOnlyList<AppConfigV2Diagnostic> diagnostics,
    InputConflictCode conflictCode,
    IReadOnlyList<string> expectedIds,
    IReadOnlyList<string> expectedPaths,
    string message)
{
    var diagnostic = diagnostics.FirstOrDefault(item =>
        item.Severity == AppConfigV2DiagnosticSeverity.Error &&
        item.Code == AppConfigV2DiagnosticCode.InputConflict &&
        item.OriginalConflictCode == conflictCode);

    AssertTrue(diagnostic is not null, message);
    AssertTrue(diagnostic!.BlocksSave, "AppConfigV2 conflict diagnostic should block save.");
    AssertTrue(diagnostic.BlocksLive, "AppConfigV2 conflict diagnostic should block live.");
    AssertTrue(diagnostic.AffectedBindingNames.Count > 0, "AppConfigV2 conflict diagnostic should expose affected names.");
    foreach (var id in expectedIds)
    {
        AssertTrue(diagnostic.AffectedBindingIds.Contains(id),
            $"AppConfigV2 conflict diagnostic should expose affected id {id}.");
    }

    foreach (var path in expectedPaths)
    {
        AssertTrue(diagnostic.AffectedPaths.Contains(path),
            $"AppConfigV2 conflict diagnostic should expose affected path {path}.");
    }
}

static WindowsKeyEventV2 KeyEvent(
    uint virtualKeyCode,
    uint scanCode = 0,
    bool isExtended = false,
    bool isDown = true,
    InputCaptureSourceKind sourceKind = InputCaptureSourceKind.ManualSynthetic)
{
    return new WindowsKeyEventV2(
        virtualKeyCode,
        scanCode,
        isExtended,
        isDown,
        sourceKind);
}

static WindowsMouseEventV2 MouseEvent(
    WindowsMouseEventKindV2 kind,
    int wheelDelta = 0,
    bool isDown = true,
    InputCaptureSourceKind sourceKind = InputCaptureSourceKind.ManualSynthetic)
{
    return new WindowsMouseEventV2(
        kind,
        kind == WindowsMouseEventKindV2.Wheel ? false : isDown,
        wheelDelta,
        sourceKind);
}

static InputCaptureResultV2 AssertCapturedKey(
    uint virtualKeyCode,
    uint scanCode,
    bool isExtended,
    string expectedCanonicalName)
{
    var result = WindowsInputNormalizerV2.NormalizeKey(KeyEvent(virtualKeyCode, scanCode, isExtended));
    AssertTrue(result.Success, $"Expected key 0x{virtualKeyCode:X} to normalize.");
    AssertTrue(result.Input?.CanonicalName == expectedCanonicalName,
        $"Expected key 0x{virtualKeyCode:X} to normalize to {expectedCanonicalName}, got {result.Input?.CanonicalName}.");
    return result;
}

static InputCaptureResultV2 AssertCapturedMouse(
    WindowsMouseEventKindV2 kind,
    int wheelDelta,
    string expectedCanonicalName)
{
    var result = WindowsInputNormalizerV2.NormalizeMouse(MouseEvent(kind, wheelDelta));
    AssertTrue(result.Success, $"Expected mouse event {kind} to normalize.");
    AssertTrue(result.Input?.CanonicalName == expectedCanonicalName,
        $"Expected mouse event {kind} to normalize to {expectedCanonicalName}, got {result.Input?.CanonicalName}.");
    return result;
}

static void AssertConflict(
    IReadOnlyList<InputConflictReport> reports,
    InputConflictCode code,
    string leftId,
    string rightId)
{
    var report = reports.FirstOrDefault(item => item.Code == code);
    if (report is null)
    {
        throw new InvalidOperationException($"Expected conflict code {code}.");
    }

    AssertTrue(report.Severity == InputConflictSeverity.Error, "Input conflict should be an Error in the skeleton.");
    AssertTrue(report.BlocksSave, "Input conflict should block save in the skeleton.");
    AssertTrue(report.BlocksLive, "Input conflict should block live in the skeleton.");
    AssertTrue(report.AffectedBindingIds.Contains(leftId), $"Conflict should include affected id {leftId}.");
    AssertTrue(report.AffectedBindingIds.Contains(rightId), $"Conflict should include affected id {rightId}.");
    AssertTrue(report.AffectedBindingNames.Count == 2, "Conflict should include affected binding names.");
}

static void AssertNoActionErrors(
    IReadOnlyList<ActionValidationDiagnosticV2> diagnostics,
    string message)
{
    var errors = diagnostics
        .Where(diagnostic => diagnostic.Severity == ActionValidationSeverityV2.Error)
        .Select(diagnostic => $"{diagnostic.Code} at {diagnostic.Path}: {diagnostic.Message}")
        .ToArray();
    AssertTrue(errors.Length == 0, $"{message}{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
}

static void AssertActionError(
    IReadOnlyList<ActionValidationDiagnosticV2> diagnostics,
    ActionValidationCodeV2 code,
    string message)
{
    AssertTrue(diagnostics.Any(diagnostic =>
            diagnostic.Severity == ActionValidationSeverityV2.Error &&
            diagnostic.Code == code &&
            !string.IsNullOrWhiteSpace(diagnostic.Message) &&
            !string.IsNullOrWhiteSpace(diagnostic.Path)),
        message);
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

static (
    FakeInputBackendV2 Backend,
    OwnershipLedgerV2 Ledger,
    MappingActivationSessionV2 Session) RuntimeV2Session(
        ActivationPlanV2 plan,
        RuntimeV2ExecutionOptions? options = null)
{
    var backend = new FakeInputBackendV2();
    var ledger = new OwnershipLedgerV2();
    var executor = new ActivationPlanExecutorV2(backend, ledger, options);
    var session = new MappingActivationSessionV2("mapping:test", plan, executor, options);
    return (backend, ledger, session);
}

static string? FindRuntimeV2SourceRoot()
{
    var current = new DirectoryInfo(Environment.CurrentDirectory);
    while (current is not null)
    {
        var candidate = Path.Combine(
            current.FullName,
            "next",
            "src",
            "BAKeySmith.Core",
            "Runtime",
            "V2");
        if (Directory.Exists(candidate))
        {
            return candidate;
        }

        current = current.Parent;
    }

    return null;
}

static void AssertRuntimeV2NoErrors(RuntimeV2ExecutionResult result, string message)
{
    var errors = result.Diagnostics
        .Where(diagnostic => diagnostic.IsError)
        .Select(diagnostic => $"{diagnostic.Code} at {diagnostic.Path}: {diagnostic.Message}")
        .ToArray();
    AssertTrue(errors.Length == 0, $"{message}{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
}

static void AssertRuntimeV2ManagerNoErrors(MappingActivationManagerResultV2 result, string message)
{
    var errors = result.Diagnostics
        .Where(diagnostic => diagnostic.IsError)
        .Select(diagnostic => $"{diagnostic.Code} at {diagnostic.Path}: {diagnostic.Message}")
        .ToArray();
    AssertTrue(errors.Length == 0, $"{message}{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
}

static void AssertRuntimeV2ConfigAdapterNoRuntimeBlocks(
    RuntimeV2ConfigAdapterResult result,
    string message)
{
    var blockers = result.Diagnostics
        .Where(diagnostic => diagnostic.BlocksRuntime)
        .Select(diagnostic => $"{diagnostic.Code} at {diagnostic.Path}: {diagnostic.Message}")
        .ToArray();
    AssertTrue(blockers.Length == 0, $"{message}{Environment.NewLine}{string.Join(Environment.NewLine, blockers)}");
}

static void AssertRuntimeV2Diagnostic(
    IReadOnlyList<RuntimeV2Diagnostic> diagnostics,
    RuntimeV2DiagnosticCode code,
    string message)
{
    AssertTrue(diagnostics.Any(diagnostic => diagnostic.Code == code), message);
}

static void AssertRuntimeV2Event(
    RuntimeV2Event evt,
    RuntimeV2EventKind kind,
    string canonicalInputName,
    string message)
{
    AssertTrue(evt.Kind == kind && evt.CanonicalInputName == canonicalInputName, message);
}

static void AssertRuntimeV2SequenceIsStable(IReadOnlyList<RuntimeV2Event> events)
{
    for (var index = 0; index < events.Count; index++)
    {
        AssertTrue(events[index].Sequence == index,
            "Runtime v2 fake backend event sequence should be deterministic.");
    }
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
    ("input name resolver v2 resolves canonical and aliases", InputNameResolverV2ResolvesCanonicalAndAliases),
    ("input name resolver v2 resolves OEM punctuation", InputNameResolverV2ResolvesOemPunctuation),
    ("input name resolver v2 reports display and capabilities", InputNameResolverV2ReportsDisplayAndCapabilities),
    ("input overlap v2 detects modifier overlap", InputOverlapV2DetectsModifierOverlap),
    ("input name resolver v2 does not change v1 resolver", InputNameResolverV2DoesNotChangeV1Resolver),
    ("input name resolver v2 registry invariants hold", InputNameResolverV2RegistryInvariantsHold),
    ("conflict model v2 detects mapping trigger conflicts", ConflictModelV2DetectsMappingTriggerConflicts),
    ("conflict model v2 detects control hotkey conflicts", ConflictModelV2DetectsControlHotkeyConflicts),
    ("conflict model v2 detects coordinate and emergency conflicts", ConflictModelV2DetectsCoordinateAndEmergencyConflicts),
    ("conflict model v2 handles wheel and hotkey main rules", ConflictModelV2HandlesWheelAndHotkeyMainRules),
    ("conflict model v2 uses exact modifier set for hotkeys", ConflictModelV2UsesExactModifierSetForHotkeys),
    ("conflict model v2 rejects overlapping hotkey modifiers", ConflictModelV2RejectsOverlappingHotkeyModifiers),
    ("coordinate record hotkey v2 validates full hotkeys", CoordinateRecordHotkeyV2ValidatesFullHotkeys),
    ("input capture v2 normalizes side-specific modifiers", InputCaptureV2NormalizesSideSpecificModifiers),
    ("input capture v2 normalizes OEM punctuation", InputCaptureV2NormalizesOemPunctuation),
    ("input capture v2 normalizes common keys", InputCaptureV2NormalizesCommonKeys),
    ("input capture v2 normalizes mouse and wheel", InputCaptureV2NormalizesMouseAndWheel),
    ("input capture v2 purpose checks use capabilities", InputCaptureV2PurposeChecksUseCapabilities),
    ("input capture v2 preserves phase and source kind metadata", InputCaptureV2PreservesPhaseAndSourceKindMetadata),
    ("input capture v2 rejects ambiguous shift and zero wheel", InputCaptureV2RejectsAmbiguousShiftAndZeroWheel),
    ("action model v2 validates tap and hold plans", ActionModelV2ValidatesTapAndHoldPlans),
    ("action model v2 validates while-held zero interval and on-up", ActionModelV2ValidatesWhileHeldZeroIntervalAndOnUp),
    ("action model v2 validates repeat and diagnostics", ActionModelV2ValidatesRepeatAndDiagnostics),
    ("action model v2 rejects wrong input kinds", ActionModelV2RejectsWrongInputKinds),
    ("action model v2 represents wheel and coordinate skeleton", ActionModelV2RepresentsWheelAndCoordinateSkeleton),
    ("action model v2 rejects kind mismatch and unknown actions", ActionModelV2RejectsKindMismatchAndUnknownActions),
    ("action model v2 validates duration applicability", ActionModelV2ValidatesDurationApplicability),
    ("action model v2 validates while-held repeat and nested paths", ActionModelV2ValidatesWhileHeldRepeatAndNestedPaths),
    ("action model v2 validates coordinate invariants", ActionModelV2ValidatesCoordinateInvariants),
    ("action model v2 reports ownership effects", ActionModelV2ReportsOwnershipEffects),
    ("action model v2 does not reference v1 compiler or runtime", ActionModelV2DoesNotReferenceV1CompilerOrRuntime),
    ("runtime v2 executes on_down tap key order", RuntimeV2ExecutesOnDownTapKeyOrder),
    ("runtime v2 simple hold executes down and up lifecycle", RuntimeV2SimpleHoldExecutesDownAndUpLifecycle),
    ("runtime v2 on_up runs only on trigger up", RuntimeV2OnUpRunsOnlyOnTriggerUp),
    ("runtime v2 cancel cleanup does not run on_up", RuntimeV2CancelCleanupDoesNotRunOnUp),
    ("runtime v2 while_held zero interval runs bounded iterations", RuntimeV2WhileHeldZeroIntervalRunsBoundedIterations),
    ("runtime v2 finite repeat records deterministic order", RuntimeV2FiniteRepeatRecordsDeterministicOrder),
    ("runtime v2 wait uses fake virtual time", RuntimeV2WaitUsesFakeVirtualTime),
    ("runtime v2 cleanup releases held key and mouse", RuntimeV2CleanupReleasesHeldKeyAndMouse),
    ("runtime v2 duplicate acquire warns without duplicate down", RuntimeV2DuplicateAcquireWarnsWithoutDuplicateDown),
    ("runtime v2 release without acquire warns without fake up", RuntimeV2ReleaseWithoutAcquireWarnsWithoutFakeUp),
    ("runtime v2 coordinate action records unsupported", RuntimeV2CoordinateActionRecordsUnsupported),
    ("runtime v2 invalid plan does not execute", RuntimeV2InvalidPlanDoesNotExecute),
    ("runtime v2 preserves validation warning and info diagnostics", RuntimeV2PreservesValidationWarningAndInfoDiagnostics),
    ("runtime v2 execution options reject invalid values", RuntimeV2ExecutionOptionsRejectInvalidValues),
    ("runtime v2 nested repeat budget exhaustion stops and cleanup releases", RuntimeV2NestedRepeatBudgetExhaustionStopsAndCleanupReleases),
    ("runtime v2 already cancelled execution does not emit normal events", RuntimeV2AlreadyCancelledExecutionDoesNotEmitNormalEvents),
    ("runtime v2 positive while held interval records fake wait", RuntimeV2PositiveWhileHeldIntervalRecordsFakeWait),
    ("runtime v2 coordinate contact cleanup remains unsupported but deterministic", RuntimeV2CoordinateContactCleanupRemainsUnsupportedButDeterministic),
    ("runtime v2 trigger up cleanup runs after on up budget failure", RuntimeV2TriggerUpCleanupRunsAfterOnUpBudgetFailure),
    ("runtime v2 manager trigger down starts session", RuntimeV2ManagerTriggerDownStartsSession),
    ("runtime v2 manager ignores same mapping trigger down", RuntimeV2ManagerIgnoresSameMappingTriggerDown),
    ("runtime v2 manager trigger up ends session and unknown warns", RuntimeV2ManagerTriggerUpEndsSessionAndUnknownWarns),
    ("runtime v2 manager cancel mapping does not execute on_up and cleans held key", RuntimeV2ManagerCancelMappingDoesNotExecuteOnUpAndCleansHeldKey),
    ("runtime v2 manager cancel all cleans multiple sessions", RuntimeV2ManagerCancelAllCleansMultipleSessions),
    ("runtime v2 manager keeps different mappings isolated", RuntimeV2ManagerKeepsDifferentMappingsIsolated),
    ("runtime v2 manager while_held routing only advances selected mapping", RuntimeV2ManagerWhileHeldRoutingOnlyAdvancesSelectedMapping),
    ("runtime v2 manager lifecycle cancellation still cleans up", RuntimeV2ManagerLifecycleCancellationStillCleansUp),
    ("runtime v2 manager cancel all cancellation preserves mapping diagnostics", RuntimeV2ManagerCancelAllCancellationPreservesMappingDiagnostics),
    ("runtime v2 manager unknown while_held mapping warns", RuntimeV2ManagerUnknownWhileHeldMappingWarns),
    ("runtime v2 manager held key isolation uses independent ledgers", RuntimeV2ManagerHeldKeyIsolationUsesIndependentLedgers),
    ("runtime v2 manager invalid plan or options do not create session", RuntimeV2ManagerInvalidPlanOrOptionsDoNotCreateSession),
    ("runtime v2 manager event log is deterministic", RuntimeV2ManagerEventLogIsDeterministic),
    ("runtime v2 config adapter builds simple tap entry", RuntimeV2ConfigAdapterBuildsSimpleTapEntry),
    ("runtime v2 config adapter preserves side-specific hold", RuntimeV2ConfigAdapterPreservesSideSpecificHold),
    ("runtime v2 config adapter skips disabled and allows warnings", RuntimeV2ConfigAdapterSkipsDisabledAndAllowsWarnings),
    ("runtime v2 config adapter reports macro sources without entries", RuntimeV2ConfigAdapterReportsMacroSourcesWithoutEntries),
    ("runtime v2 config adapter supports partial simple entries with macro blocker", RuntimeV2ConfigAdapterSupportsPartialSimpleEntriesWithMacroBlocker),
    ("runtime v2 config adapter preserves global hotkey conflict metadata", RuntimeV2ConfigAdapterPreservesGlobalHotkeyConflictMetadata),
    ("runtime v2 config adapter all disabled config has no entries without runtime block", RuntimeV2ConfigAdapterAllDisabledConfigHasNoEntriesWithoutRuntimeBlock),
    ("runtime v2 config adapter disabled structural errors still block runtime", RuntimeV2ConfigAdapterDisabledStructuralErrorsStillBlockRuntime),
    ("runtime v2 config adapter blocks invalid config and preserves conflicts", RuntimeV2ConfigAdapterBlocksInvalidConfigAndPreservesConflicts),
    ("runtime v2 config adapter reports simple build failure", RuntimeV2ConfigAdapterReportsSimpleBuildFailure),
    ("runtime v2 config adapter entry can execute in manager sandbox", RuntimeV2ConfigAdapterEntryCanExecuteInManagerSandbox),
    ("runtime v2 config adapter does not reference v1 runtime hook gui compiler or manager", RuntimeV2ConfigAdapterDoesNotReferenceV1RuntimeHookGuiCompilerOrManager),
    ("runtime v2 sandbox does not reference v1 runtime hook gui or real input", RuntimeV2SandboxDoesNotReferenceV1RuntimeHookGuiOrRealInput),
    ("simple mapping v2 builds tap key plan", SimpleMappingV2BuildsTapKeyPlan),
    ("simple mapping v2 builds tap mouse plan", SimpleMappingV2BuildsTapMousePlan),
    ("simple mapping v2 builds hold plans", SimpleMappingV2BuildsHoldPlans),
    ("simple mapping v2 rejects unsupported targets and durations", SimpleMappingV2RejectsUnsupportedTargetsAndDurations),
    ("simple mapping v2 handles malformed targets and diagnostic paths", SimpleMappingV2HandlesMalformedTargetsAndDiagnosticPaths),
    ("simple mapping v2 does not reference v1 config runtime gui or compiler", SimpleMappingV2DoesNotReferenceV1ConfigRuntimeGuiOrCompiler),
    ("app config v2 valid simple mappings validate", AppConfigV2ValidSimpleMappingsValidate),
    ("app config v2 macro source shell does not compile", AppConfigV2MacroSourceShellDoesNotCompile),
    ("app config v2 rejects duplicate ids and triggers", AppConfigV2RejectsDuplicateIdsAndTriggers),
    ("app config v2 detects global hotkey conflicts", AppConfigV2DetectsGlobalHotkeyConflicts),
    ("app config v2 rejects invalid trigger timing and coordinate settings", AppConfigV2RejectsInvalidTriggerTimingAndCoordinateSettings),
    ("app config v2 does not reference v1 serializer runtime gui or compiler", AppConfigV2DoesNotReferenceV1SerializerRuntimeGuiOrCompiler),
    ("app config v2 conflict diagnostics expose metadata", AppConfigV2ConflictDiagnosticsExposeMetadata),
    ("app config v2 disabled mapping semantics are explicit", AppConfigV2DisabledMappingSemanticsAreExplicit),
    ("app config v2 rejects action source kind mismatch and unknown source", AppConfigV2RejectsActionSourceKindMismatchAndUnknownSource),
    ("app config v2 macro source empty policy is warning", AppConfigV2MacroSourceEmptyPolicyIsWarning),
    ("app config v2 global hotkey validation boundaries", AppConfigV2GlobalHotkeyValidationBoundaries),
    ("app config v2 timing and coordinate settings boundaries", AppConfigV2TimingAndCoordinateSettingsBoundaries),
    ("app config v2 json serializer parses draft shape", AppConfigV2JsonSerializerParsesDraftShape),
    ("app config v2 json serializer roundtrips canonical names", AppConfigV2JsonSerializerRoundTripsCanonicalNames),
    ("app config v2 json serializer reports parse diagnostics", AppConfigV2JsonSerializerReportsParseDiagnostics),
    ("app config v2 json serializer hardens duration grammar", AppConfigV2JsonSerializerHardensDurationGrammar),
    ("app config v2 json serializer reports malformed structure", AppConfigV2JsonSerializerReportsMalformedStructure),
    ("app config v2 json serializer reports field paths", AppConfigV2JsonSerializerReportsFieldPaths),
    ("app config v2 json serializer keeps unknown fields draft only", AppConfigV2JsonSerializerKeepsUnknownFieldsDraftOnly),
    ("app config v2 json serializer preserves conflict metadata", AppConfigV2JsonSerializerPreservesConflictMetadata),
    ("app config v2 json serializer result helpers handle warnings and errors", AppConfigV2JsonSerializerResultHelpersHandleWarningsAndErrors),
    ("app config v2 json serializer retains multiple diagnostics", AppConfigV2JsonSerializerRetainsMultipleDiagnostics),
    ("app config v2 json serializer reports nested malformed object paths", AppConfigV2JsonSerializerReportsNestedMalformedObjectPaths),
    ("app config v2 json serializer ignores nested unknown fields draft only", AppConfigV2JsonSerializerIgnoresNestedUnknownFieldsDraftOnly),
    ("app config v2 json serializer normalizes coordinate profile paths", AppConfigV2JsonSerializerNormalizesCoordinateProfilePaths),
    ("app config v2 json serializer disabled mapping conflict semantics", AppConfigV2JsonSerializerDisabledMappingConflictSemantics),
    ("app config v2 json serializer reports missing action edges", AppConfigV2JsonSerializerReportsMissingActionEdges),
    ("app config v2 json serializer duplicate trigger affected paths are stable", AppConfigV2JsonSerializerDuplicateTriggerAffectedPathsAreStable),
    ("app config v2 json serializer duration edges remain stable", AppConfigV2JsonSerializerDurationEdgesRemainStable),
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

internal sealed record UnknownActionModelV2() : ActionModelV2(ActionKindV2.Wait);

internal sealed record UnknownMappingActionSourceV2() :
    MappingActionSourceV2((MappingActionSourceKindV2)999);

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
