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
    ("tap uses ownership tracker", TapUsesOwnershipTracker),
    ("runtime session stop cancels workers and releases held keys", RuntimeSessionStopCancelsWorkersAndReleasesHeldKeys),
    ("runtime contract hold respects foreground gate", RuntimeContractHoldRespectsForegroundGate),
    ("runtime contract tap uses unified tap semantics", RuntimeContractTapUsesUnifiedTapSemantics),
    ("runtime contract reload stops old mapping", RuntimeContractReloadStopsOldMapping),
    ("runtime contract reload during burst drops old queue cleanly", RuntimeContractReloadDuringBurstDropsOldQueueCleanly),
    ("runtime contract reload during long macro releases old owner", RuntimeContractReloadDuringLongMacroReleasesOldOwner),
    ("runtime contract disable cancels delayed tap", RuntimeContractDisableCancelsDelayedTap),
    ("runtime contract stop during long macro releases held input", RuntimeContractStopDuringLongMacroReleasesHeldInput),
    ("runtime contract foreground gate burst blocks input", RuntimeContractForegroundGateBurstBlocksInput),
    ("runtime contract diagnostics are emitted", RuntimeContractDiagnosticsAreEmitted),
    ("key resolver supports product key names", KeyResolverSupportsProductKeyNames),
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
    ("trigger pipeline delivers manual trigger to runtime", TriggerPipelineDeliversManualTriggerToRuntime),
    ("trigger pipeline restart does not replay stopped events", TriggerPipelineRestartDoesNotReplayStoppedEvents),
    ("runtime host composes config runtime and pipeline", RuntimeHostComposesConfigRuntimeAndPipeline),
    ("runtime host snapshot exposes lifecycle state", RuntimeHostSnapshotExposesLifecycleState),
    ("runtime host rejects invalid config", RuntimeHostRejectsInvalidConfig),
};

foreach (var (name, test) in tests)
{
    await test();
    Console.WriteLine($"PASS {name}");
}
