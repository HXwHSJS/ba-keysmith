using System.Text.Json;
using System.Diagnostics;
using BAKeySmith.Core.Configuration;
using BAKeySmith.Core.Diagnostics;
using BAKeySmith.Core.Foreground;
using BAKeySmith.Core.Hosting;
using BAKeySmith.Core.Input;
using BAKeySmith.Core.Triggers;

static string? ValueAfter(string[] args, string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

static bool Has(string[] args, string name) =>
    args.Any(arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));

static void PrintUsage()
{
    Console.WriteLine("""
        BAKeySmith.Headless

        Usage:
          dotnet run --project .\tools\BAKeySmith.Headless\BAKeySmith.Headless.csproj -- --config config.json --dry-run --duration 10
          dotnet run --project .\tools\BAKeySmith.Headless\BAKeySmith.Headless.csproj -- --config config.json --live --duration 10

        Options:
          --config <path>           Config JSON path. Defaults to config.example.json.
          --scenario <name>         Built-in scenario: all, burst-drain, reload-during-burst,
                                    stop-during-long-macro, reload-during-long-macro,
                                    foreground-gate-during-burst.
          --dry-run                 Use dry-run backend and manual trigger source. Default.
          --live                    Use Windows hooks, foreground gate, and SendInput backend.
          --allow-any-foreground    In dry-run mode, use an always-allowed foreground gate.
          --simulate <events>       Dry-run events, comma separated. Example: q:down,q:up,mouse_right:down
          --burst <count>           Dry-run only: emit N trigger events as fast as possible.
          --burst-trigger <code>    Trigger code for --burst. Default q.
          --burst-phase <phase>     down or up for --burst. Default down.
          --duration <seconds>      Stop automatically after N seconds. Default 5.
          --drain-timeout <seconds> Max wait for --assert-drained. Default 5.
          --snapshot                Print running/stopped RuntimeHostSnapshot JSON.
          --assert-drained          Return non-zero if trigger/action queues do not drain before stop.
          --assert-input-events <n> Return non-zero if dry-run input event count differs from n.
          --assert-clean            Return non-zero if stopped snapshot still has workers or held inputs.
        """);
}

static void PrintSnapshot(string name, RuntimeHostSnapshot snapshot)
{
    Console.WriteLine($"SNAPSHOT {name}");
    Console.WriteLine(JsonSerializer.Serialize(
        snapshot,
        new JsonSerializerOptions { WriteIndented = true }));
}

static bool IsCleanlyStopped(RuntimeHostSnapshot snapshot)
{
    return !snapshot.IsStarted &&
        !snapshot.IsPipelineRunning &&
        snapshot.Pipeline.PendingCount == 0 &&
        snapshot.Runtime.State == BAKeySmith.Core.Contracts.RuntimeState.Stopped &&
        snapshot.Runtime.ActiveWorkerCount == 0 &&
        snapshot.Runtime.PendingActionCount == 0 &&
        snapshot.Runtime.RunningActionCount == 0 &&
        snapshot.Runtime.Presses.IsEmpty;
}

static void EmitManualEvent(ManualTriggerSource source, string code, string phase)
{
    var isMouse = code.StartsWith("mouse_", StringComparison.OrdinalIgnoreCase);
    if (phase == "down")
    {
        if (isMouse)
        {
            source.MouseDown(code);
        }
        else
        {
            source.KeyDown(code);
        }

        return;
    }

    if (phase == "up")
    {
        if (isMouse)
        {
            source.MouseUp(code);
        }
        else
        {
            source.KeyUp(code);
        }

        return;
    }

    throw new ArgumentException($"Invalid simulated phase: {phase}");
}

static async Task<bool> WaitForDrainedAsync(
    RuntimeHost host,
    TimeSpan timeout,
    CancellationToken cancellationToken)
{
    var start = Stopwatch.StartNew();
    while (start.Elapsed < timeout)
    {
        var snapshot = host.Snapshot();
        if (snapshot.Pipeline.PendingCount == 0 &&
            snapshot.Runtime.PendingActionCount == 0 &&
            snapshot.Runtime.RunningActionCount == 0)
        {
            return true;
        }

        await Task.Delay(5, cancellationToken);
    }

    return false;
}

static async Task<bool> WaitUntilAsync(
    Func<bool> condition,
    TimeSpan timeout,
    CancellationToken cancellationToken)
{
    var start = Stopwatch.StartNew();
    while (start.Elapsed < timeout)
    {
        if (condition())
        {
            return true;
        }

        await Task.Delay(5, cancellationToken);
    }

    return false;
}

static AppConfigV1 TapConfig(string trigger, string target, double tapHoldMs = 20)
{
    return new AppConfigV1
    {
        TapHoldMilliseconds = tapHoldMs,
        Mappings =
        [
            new MappingConfigV1
            {
                Trigger = trigger,
                Type = "simple",
                Target = target,
                Mode = "tap"
            }
        ]
    };
}

static AppConfigV1 LongMacroConfig()
{
    return new AppConfigV1
    {
        TapHoldMilliseconds = 20,
        Mappings =
        [
            new MappingConfigV1
            {
                Trigger = "q",
                Type = "macro",
                Script = "press a\nwait 5000\nrelease a"
            }
        ]
    };
}

static async Task<int> RunBurstDrainScenarioAsync(
    int requestedBurstCount,
    double drainTimeoutSeconds,
    bool printSnapshot)
{
    var burst = requestedBurstCount > 0 ? requestedBurstCount : 50;
    var dryRun = new DryRunInputBackend();
    var source = new ManualTriggerSource();
    await using var host = new RuntimeHost(
        TapConfig("q", "1"),
        dryRun,
        AlwaysForegroundGate.Instance,
        source,
        new InMemoryDiagnosticsSink());

    await host.StartAsync(CancellationToken.None);
    var burstWatch = Stopwatch.StartNew();
    for (var index = 0; index < burst; index++)
    {
        source.KeyDown("q");
    }

    burstWatch.Stop();
    var drained = await WaitForDrainedAsync(
        host,
        TimeSpan.FromSeconds(Math.Max(0.1, drainTimeoutSeconds)),
        CancellationToken.None);
    if (!drained)
    {
        Console.Error.WriteLine("ERROR burst-drain did not drain.");
        return 8;
    }

    var expectedEvents = burst * 2;
    if (dryRun.Events.Count != expectedEvents)
    {
        Console.Error.WriteLine(
            $"ERROR burst-drain input event count mismatch. expected={expectedEvents} actual={dryRun.Events.Count}");
        return 9;
    }

    var runningSnapshot = host.Snapshot();
    await host.StopAsync(CancellationToken.None);
    var stoppedSnapshot = host.Snapshot();
    if (!IsCleanlyStopped(stoppedSnapshot))
    {
        Console.Error.WriteLine("ERROR burst-drain stopped runtime is not clean.");
        return 16;
    }

    Console.WriteLine(
        $"SCENARIO burst-drain passed. burst={burst} input_events={dryRun.Events.Count} emit_ms={burstWatch.Elapsed.TotalMilliseconds:0.###}");
    if (printSnapshot)
    {
        PrintSnapshot("running", runningSnapshot);
        PrintSnapshot("stopped", stoppedSnapshot);
    }

    return 0;
}

static async Task<int> RunReloadDuringBurstScenarioAsync(
    int requestedBurstCount,
    double drainTimeoutSeconds,
    bool printSnapshot)
{
    var burst = requestedBurstCount > 0 ? requestedBurstCount : 50;
    var dryRun = new DryRunInputBackend();
    var source = new ManualTriggerSource();
    await using var host = new RuntimeHost(
        TapConfig("q", "1", tapHoldMs: 40),
        dryRun,
        AlwaysForegroundGate.Instance,
        source,
        new InMemoryDiagnosticsSink());

    await host.StartAsync(CancellationToken.None);
    for (var index = 0; index < burst; index++)
    {
        source.KeyDown("q");
    }

    await host.ReloadAsync(TapConfig("w", "2", tapHoldMs: 20), CancellationToken.None);
    var drained = await WaitForDrainedAsync(
        host,
        TimeSpan.FromSeconds(Math.Max(0.1, drainTimeoutSeconds)),
        CancellationToken.None);
    if (!drained)
    {
        Console.Error.WriteLine("ERROR reload-during-burst did not drain after reload.");
        return 10;
    }

    var qOutputBeforeProbe = dryRun.Count("key", "1");
    source.KeyDown("q");
    drained = await WaitForDrainedAsync(
        host,
        TimeSpan.FromSeconds(Math.Max(0.1, drainTimeoutSeconds)),
        CancellationToken.None);
    if (!drained)
    {
        Console.Error.WriteLine("ERROR reload-during-burst did not drain after q probe.");
        return 11;
    }

    if (dryRun.Count("key", "1") != qOutputBeforeProbe)
    {
        Console.Error.WriteLine("ERROR old q mapping still produced input after reload.");
        return 12;
    }

    source.KeyDown("w");
    drained = await WaitForDrainedAsync(
        host,
        TimeSpan.FromSeconds(Math.Max(0.1, drainTimeoutSeconds)),
        CancellationToken.None);
    if (!drained)
    {
        Console.Error.WriteLine("ERROR reload-during-burst did not drain after w probe.");
        return 13;
    }

    if (dryRun.Count("key", "2", true) != 1 || dryRun.Count("key", "2", false) != 1)
    {
        Console.Error.WriteLine("ERROR new w mapping did not produce one complete tap after reload.");
        return 14;
    }

    var runningSnapshot = host.Snapshot();
    await host.StopAsync(CancellationToken.None);
    var stoppedSnapshot = host.Snapshot();
    if (!IsCleanlyStopped(stoppedSnapshot))
    {
        Console.Error.WriteLine("ERROR reload-during-burst stopped runtime is not clean.");
        return 15;
    }

    Console.WriteLine(
        $"SCENARIO reload-during-burst passed. burst={burst} old_input_events={qOutputBeforeProbe} new_input_events={dryRun.Count("key", "2")}");
    if (printSnapshot)
    {
        PrintSnapshot("running", runningSnapshot);
        PrintSnapshot("stopped", stoppedSnapshot);
    }

    return 0;
}

static async Task<int> RunStopDuringLongMacroScenarioAsync(
    double drainTimeoutSeconds,
    bool printSnapshot)
{
    var dryRun = new DryRunInputBackend();
    var source = new ManualTriggerSource();
    await using var host = new RuntimeHost(
        LongMacroConfig(),
        dryRun,
        AlwaysForegroundGate.Instance,
        source,
        new InMemoryDiagnosticsSink());

    await host.StartAsync(CancellationToken.None);
    source.KeyDown("q");
    var pressed = await WaitUntilAsync(
        () => dryRun.Count("key", "a", true) > 0,
        TimeSpan.FromSeconds(Math.Max(0.1, drainTimeoutSeconds)),
        CancellationToken.None);
    if (!pressed)
    {
        Console.Error.WriteLine("ERROR stop-during-long-macro did not press the macro key before timeout.");
        return 20;
    }

    var runningSnapshot = host.Snapshot();
    await host.StopAsync(CancellationToken.None);
    var stoppedSnapshot = host.Snapshot();
    var eventCountAfterStop = dryRun.Events.Count;
    await Task.Delay(100);

    if (dryRun.Count("key", "a", true) != 1 || dryRun.Count("key", "a", false) != 1)
    {
        Console.Error.WriteLine("ERROR stop-during-long-macro did not release the held macro key exactly once.");
        return 21;
    }

    if (dryRun.Events.Count != eventCountAfterStop)
    {
        Console.Error.WriteLine("ERROR stop-during-long-macro produced input after stop.");
        return 22;
    }

    if (!IsCleanlyStopped(stoppedSnapshot))
    {
        Console.Error.WriteLine("ERROR stop-during-long-macro stopped runtime is not clean.");
        return 23;
    }

    Console.WriteLine("SCENARIO stop-during-long-macro passed. key_a_down=1 key_a_up=1 post_stop_events=0");
    if (printSnapshot)
    {
        PrintSnapshot("running", runningSnapshot);
        PrintSnapshot("stopped", stoppedSnapshot);
    }

    return 0;
}

static async Task<int> RunReloadDuringLongMacroScenarioAsync(
    double drainTimeoutSeconds,
    bool printSnapshot)
{
    var dryRun = new DryRunInputBackend();
    var source = new ManualTriggerSource();
    await using var host = new RuntimeHost(
        LongMacroConfig(),
        dryRun,
        AlwaysForegroundGate.Instance,
        source,
        new InMemoryDiagnosticsSink());

    await host.StartAsync(CancellationToken.None);
    source.KeyDown("q");
    var pressed = await WaitUntilAsync(
        () => dryRun.Count("key", "a", true) > 0,
        TimeSpan.FromSeconds(Math.Max(0.1, drainTimeoutSeconds)),
        CancellationToken.None);
    if (!pressed)
    {
        Console.Error.WriteLine("ERROR reload-during-long-macro did not press the macro key before timeout.");
        return 30;
    }

    await host.ReloadAsync(TapConfig("w", "2"), CancellationToken.None);
    var afterReload = host.Snapshot();
    if (dryRun.Count("key", "a", true) != 1 || dryRun.Count("key", "a", false) != 1)
    {
        Console.Error.WriteLine("ERROR reload-during-long-macro did not release the old macro key exactly once.");
        return 31;
    }

    if (!afterReload.Runtime.Presses.IsEmpty ||
        afterReload.Runtime.PendingActionCount != 0 ||
        afterReload.Runtime.RunningActionCount != 0)
    {
        Console.Error.WriteLine("ERROR reload-during-long-macro left old macro runtime state after reload.");
        return 32;
    }

    var oldMacroEvents = dryRun.Count("key", "a");
    source.KeyDown("q");
    var drained = await WaitForDrainedAsync(
        host,
        TimeSpan.FromSeconds(Math.Max(0.1, drainTimeoutSeconds)),
        CancellationToken.None);
    if (!drained)
    {
        Console.Error.WriteLine("ERROR reload-during-long-macro did not drain after old q probe.");
        return 33;
    }

    if (dryRun.Count("key", "a") != oldMacroEvents)
    {
        Console.Error.WriteLine("ERROR old long macro trigger still produced input after reload.");
        return 34;
    }

    source.KeyDown("w");
    drained = await WaitForDrainedAsync(
        host,
        TimeSpan.FromSeconds(Math.Max(0.1, drainTimeoutSeconds)),
        CancellationToken.None);
    if (!drained)
    {
        Console.Error.WriteLine("ERROR reload-during-long-macro did not drain after new w probe.");
        return 35;
    }

    if (dryRun.Count("key", "2", true) != 1 || dryRun.Count("key", "2", false) != 1)
    {
        Console.Error.WriteLine("ERROR new w mapping did not produce one complete tap after long macro reload.");
        return 36;
    }

    var runningSnapshot = host.Snapshot();
    await host.StopAsync(CancellationToken.None);
    var stoppedSnapshot = host.Snapshot();
    if (!IsCleanlyStopped(stoppedSnapshot))
    {
        Console.Error.WriteLine("ERROR reload-during-long-macro stopped runtime is not clean.");
        return 37;
    }

    Console.WriteLine("SCENARIO reload-during-long-macro passed. old_key_a_down=1 old_key_a_up=1 new_key_2_tap=1");
    if (printSnapshot)
    {
        PrintSnapshot("running", runningSnapshot);
        PrintSnapshot("stopped", stoppedSnapshot);
    }

    return 0;
}

static async Task<int> RunForegroundGateDuringBurstScenarioAsync(
    int requestedBurstCount,
    double drainTimeoutSeconds,
    bool printSnapshot)
{
    var burst = requestedBurstCount > 0 ? requestedBurstCount : 50;
    var dryRun = new DryRunInputBackend();
    var source = new ManualTriggerSource();
    var gate = new ManualForegroundGate
    {
        IsAllowed = false,
        ForegroundProcess = "Notepad.exe"
    };
    await using var host = new RuntimeHost(
        TapConfig("q", "1"),
        dryRun,
        gate,
        source,
        new InMemoryDiagnosticsSink());

    await host.StartAsync(CancellationToken.None);
    for (var index = 0; index < burst; index++)
    {
        source.KeyDown("q");
    }

    var drained = await WaitForDrainedAsync(
        host,
        TimeSpan.FromSeconds(Math.Max(0.1, drainTimeoutSeconds)),
        CancellationToken.None);
    if (!drained)
    {
        Console.Error.WriteLine("ERROR foreground-gate-during-burst did not drain while blocked.");
        return 40;
    }

    if (dryRun.Events.Count != 0)
    {
        Console.Error.WriteLine($"ERROR foreground-gate-during-burst emitted input while blocked. events={dryRun.Events.Count}");
        return 41;
    }

    var blockedSnapshot = host.Snapshot();
    if (blockedSnapshot.Runtime.LastForegroundAllowed)
    {
        Console.Error.WriteLine("ERROR foreground-gate-during-burst did not record blocked foreground state.");
        return 42;
    }

    gate.IsAllowed = true;
    gate.ForegroundProcess = "BlueArchive.exe";
    source.KeyDown("q");
    drained = await WaitForDrainedAsync(
        host,
        TimeSpan.FromSeconds(Math.Max(0.1, drainTimeoutSeconds)),
        CancellationToken.None);
    if (!drained)
    {
        Console.Error.WriteLine("ERROR foreground-gate-during-burst did not drain after foreground allowed.");
        return 43;
    }

    if (dryRun.Count("key", "1", true) != 1 || dryRun.Count("key", "1", false) != 1)
    {
        Console.Error.WriteLine("ERROR foreground-gate-during-burst allowed foreground did not produce one tap.");
        return 44;
    }

    var runningSnapshot = host.Snapshot();
    await host.StopAsync(CancellationToken.None);
    var stoppedSnapshot = host.Snapshot();
    if (!IsCleanlyStopped(stoppedSnapshot))
    {
        Console.Error.WriteLine("ERROR foreground-gate-during-burst stopped runtime is not clean.");
        return 45;
    }

    Console.WriteLine($"SCENARIO foreground-gate-during-burst passed. blocked_burst={burst} blocked_input_events=0 allowed_tap=1");
    if (printSnapshot)
    {
        PrintSnapshot("blocked", blockedSnapshot);
        PrintSnapshot("running", runningSnapshot);
        PrintSnapshot("stopped", stoppedSnapshot);
    }

    return 0;
}

static async Task<int> RunAllScenariosAsync(
    int burstCount,
    double drainTimeoutSeconds,
    bool printSnapshot)
{
    var scenarios = new (string Name, Func<Task<int>> Run)[]
    {
        ("burst-drain", () => RunBurstDrainScenarioAsync(burstCount, drainTimeoutSeconds, printSnapshot)),
        ("reload-during-burst", () => RunReloadDuringBurstScenarioAsync(burstCount, drainTimeoutSeconds, printSnapshot)),
        ("stop-during-long-macro", () => RunStopDuringLongMacroScenarioAsync(drainTimeoutSeconds, printSnapshot)),
        ("reload-during-long-macro", () => RunReloadDuringLongMacroScenarioAsync(drainTimeoutSeconds, printSnapshot)),
        ("foreground-gate-during-burst", () => RunForegroundGateDuringBurstScenarioAsync(burstCount, drainTimeoutSeconds, printSnapshot))
    };

    var failures = new List<(string Name, int ExitCode)>();
    var watch = Stopwatch.StartNew();
    foreach (var (name, run) in scenarios)
    {
        Console.WriteLine($"SCENARIO all running: {name}");
        var exitCode = await run();
        if (exitCode != 0)
        {
            failures.Add((name, exitCode));
        }
    }

    watch.Stop();
    if (failures.Count > 0)
    {
        foreach (var (name, exitCode) in failures)
        {
            Console.Error.WriteLine($"ERROR scenario failed: {name} exit={exitCode}");
        }

        return failures[0].ExitCode;
    }

    Console.WriteLine($"SCENARIO all passed. count={scenarios.Length} elapsed_ms={watch.Elapsed.TotalMilliseconds:0.###}");
    return 0;
}

if (Has(args, "--help") || Has(args, "-h"))
{
    PrintUsage();
    return 0;
}

var configPath = ValueAfter(args, "--config") ?? "config.example.json";
var scenario = ValueAfter(args, "--scenario");
var live = Has(args, "--live");
var durationSeconds = double.TryParse(ValueAfter(args, "--duration"), out var parsedDuration)
    ? parsedDuration
    : 5;
var simulate = ValueAfter(args, "--simulate");
var printSnapshot = Has(args, "--snapshot");
var assertClean = Has(args, "--assert-clean");
var burstCount = int.TryParse(ValueAfter(args, "--burst"), out var parsedBurst)
    ? parsedBurst
    : 0;
var burstTrigger = ValueAfter(args, "--burst-trigger") ?? "q";
var burstPhase = (ValueAfter(args, "--burst-phase") ?? "down").ToLowerInvariant();
var drainTimeoutSeconds = double.TryParse(ValueAfter(args, "--drain-timeout"), out var parsedDrainTimeout)
    ? parsedDrainTimeout
    : 5;
var assertDrained = Has(args, "--assert-drained");
var expectedInputEvents = int.TryParse(ValueAfter(args, "--assert-input-events"), out var parsedExpectedInputEvents)
    ? parsedExpectedInputEvents
    : (int?)null;

if (!string.IsNullOrWhiteSpace(scenario))
{
    if (live)
    {
        Console.Error.WriteLine("ERROR built-in scenarios are dry-run only.");
        return 6;
    }

    var normalizedScenario = scenario.Trim().ToLowerInvariant();
    if (normalizedScenario == "all")
    {
        return await RunAllScenariosAsync(
            burstCount,
            drainTimeoutSeconds,
            printSnapshot);
    }

    if (normalizedScenario == "burst-drain")
    {
        return await RunBurstDrainScenarioAsync(
            burstCount,
            drainTimeoutSeconds,
            printSnapshot);
    }

    if (normalizedScenario == "reload-during-burst")
    {
        return await RunReloadDuringBurstScenarioAsync(
            burstCount,
            drainTimeoutSeconds,
            printSnapshot);
    }

    if (normalizedScenario == "stop-during-long-macro")
    {
        return await RunStopDuringLongMacroScenarioAsync(
            drainTimeoutSeconds,
            printSnapshot);
    }

    if (normalizedScenario == "reload-during-long-macro")
    {
        return await RunReloadDuringLongMacroScenarioAsync(
            drainTimeoutSeconds,
            printSnapshot);
    }

    if (normalizedScenario == "foreground-gate-during-burst")
    {
        return await RunForegroundGateDuringBurstScenarioAsync(
            burstCount,
            drainTimeoutSeconds,
            printSnapshot);
    }

    Console.Error.WriteLine($"ERROR unknown scenario: {scenario}");
    return 7;
}

var loadResult = await AppConfigSerializer.LoadFileAsync(configPath, CancellationToken.None);
foreach (var warning in loadResult.Warnings)
{
    Console.WriteLine($"WARN {warning}");
}

if (!loadResult.Success)
{
    foreach (var error in loadResult.Errors)
    {
        Console.Error.WriteLine($"ERROR {error}");
    }

    return 2;
}

var diagnostics = new InMemoryDiagnosticsSink();
IInputBackend inputBackend;
IForegroundGate foregroundGate;
ITriggerSource triggerSource;
ManualTriggerSource? manualSource = null;

if (live)
{
    inputBackend = new WindowsInputBackend();
    foregroundGate = new WindowsForegroundGate();
    triggerSource = new WindowsHookTriggerSource();
    Console.WriteLine("LIVE mode: Windows hooks and SendInput are enabled.");
}
else
{
    var dryRunBackend = new DryRunInputBackend();
    inputBackend = dryRunBackend;
    foregroundGate = Has(args, "--allow-any-foreground")
        ? AlwaysForegroundGate.Instance
        : new ManualForegroundGate { IsAllowed = true };
    manualSource = new ManualTriggerSource();
    triggerSource = manualSource;
    Console.WriteLine("DRY-RUN mode: no real keyboard or mouse input will be sent.");
}

await using var host = new RuntimeHost(
    loadResult.Config,
    inputBackend,
    foregroundGate,
    triggerSource,
    diagnostics);

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    _ = Task.Run(async () => await host.StopAsync(CancellationToken.None));
};

await host.StartAsync(CancellationToken.None);
Console.WriteLine($"Runtime started. mappings={host.RuntimeConfig.Mappings.Count}, target={host.RuntimeConfig.TargetProcess}");

if (!live && manualSource is not null && !string.IsNullOrWhiteSpace(simulate))
{
    foreach (var item in simulate.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        var parts = item.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            Console.WriteLine($"WARN ignored invalid simulated event: {item}");
            continue;
        }

        var code = parts[0];
        var phase = parts[1].ToLowerInvariant();
        try
        {
            EmitManualEvent(manualSource, code, phase);
        }
        catch (ArgumentException)
        {
            Console.WriteLine($"WARN ignored invalid simulated phase: {item}");
        }
    }
}

if (live && burstCount > 0)
{
    Console.WriteLine("WARN --burst is dry-run only and was ignored in live mode.");
}
else if (!live && manualSource is not null && burstCount > 0)
{
    var burstWatch = Stopwatch.StartNew();
    for (var index = 0; index < burstCount; index++)
    {
        EmitManualEvent(manualSource, burstTrigger, burstPhase);
    }

    burstWatch.Stop();
    Console.WriteLine(
        $"Burst emitted. trigger={burstTrigger}:{burstPhase} count={burstCount} elapsed_ms={burstWatch.Elapsed.TotalMilliseconds:0.###}");
}

if (assertDrained)
{
    var drained = await WaitForDrainedAsync(
        host,
        TimeSpan.FromSeconds(Math.Max(0.1, drainTimeoutSeconds)),
        CancellationToken.None);
    if (!drained)
    {
        var snapshot = host.Snapshot();
        Console.Error.WriteLine(
            $"ERROR queues did not drain. trigger_pending={snapshot.Pipeline.PendingCount} action_pending={snapshot.Runtime.PendingActionCount} action_running={snapshot.Runtime.RunningActionCount}");
        return 4;
    }
}

await Task.Delay(TimeSpan.FromSeconds(Math.Max(0.1, durationSeconds)));
var runningSnapshot = host.Snapshot();
await host.StopAsync(CancellationToken.None);
var stoppedSnapshot = host.Snapshot();

Console.WriteLine($"Runtime stopped. diagnostics={diagnostics.Events.Count}");
if (inputBackend is DryRunInputBackend dryRun)
{
    Console.WriteLine($"Dry-run input events={dryRun.Events.Count}");
    if (expectedInputEvents is not null && dryRun.Events.Count != expectedInputEvents.Value)
    {
        Console.Error.WriteLine(
            $"ERROR dry-run input event count mismatch. expected={expectedInputEvents.Value} actual={dryRun.Events.Count}");
        return 5;
    }
}

if (printSnapshot)
{
    PrintSnapshot("running", runningSnapshot);
    PrintSnapshot("stopped", stoppedSnapshot);
}

if (assertClean && !IsCleanlyStopped(stoppedSnapshot))
{
    Console.Error.WriteLine("ERROR stopped runtime is not clean.");
    return 3;
}

return 0;
