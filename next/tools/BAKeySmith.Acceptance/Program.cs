using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using BAKeySmith.Core.Configuration;
using BAKeySmith.Core.Contracts;
using BAKeySmith.Core.Diagnostics;
using BAKeySmith.Core.Foreground;
using BAKeySmith.Core.Hosting;
using BAKeySmith.Core.Input;
using BAKeySmith.Core.Runtime;
using BAKeySmith.Core.Scripting;
using BAKeySmith.Core.Triggers;

static string? ValueAfter(string[] args, string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

static bool Has(string[] args, string name) =>
    args.Any(arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));

static int IntOption(string[] args, string name, int fallback, int minimum)
{
    return int.TryParse(
            ValueAfter(args, name),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsed)
        ? Math.Max(minimum, parsed)
        : fallback;
}

static double DoubleOption(string[] args, string name, double fallback, double minimum)
{
    return double.TryParse(
            ValueAfter(args, name),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsed) && double.IsFinite(parsed)
        ? Math.Max(minimum, parsed)
        : fallback;
}

static void PrintUsage()
{
    Console.WriteLine("""
        BAKeySmith.Acceptance

        Usage:
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario all --burst 50 --drain-timeout 5
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario lifecycle-stress --burst 50 --drain-timeout 5
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario active-pointer-wait-foreground-loss-interrupts
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario built-in-drag-foreground-loss-contract
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario xbutton2-triggered-complete-drag-normal-completion
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario xbutton2-triggered-multisegment-drag-normal-completion
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario dry-run-soak --soak-seconds 300 --soak-rate 20
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario live-safe --allow-live-input
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario live-soak --allow-live-input --soak-seconds 60 --soak-rate 20
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario wheel-trigger-boundaries --allow-live-input
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario xbutton-trigger-boundaries --allow-live-input
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario xbutton-trigger-reload-disable --allow-live-input
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario xbutton-self-injected-pass-through --allow-live-input
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario xbutton1-hold-then-foreground-change-before-release --allow-live-input
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario xbutton1-blocked-hold-then-foreground-return-before-release --allow-live-input
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario xbutton2-hold-then-foreground-change-before-release --allow-live-input
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario xbutton2-blocked-hold-then-foreground-return-before-release --allow-live-input
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario xbutton2-triggered-drag-minimal --allow-live-input
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario xbutton2-triggered-multisegment-move-minimal --allow-live-input
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario xbutton2-triggered-drag-stop-during-active-drag --allow-live-input
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario xbutton2-triggered-drag-disable-during-active-drag --allow-live-input
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario xbutton2-triggered-drag-reload-during-active-drag-old-trigger-blocked --allow-live-input
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario xbutton2-triggered-drag-reload-during-active-drag-new-trigger-allowed --allow-live-input
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario xbutton2-triggered-drag-foreground-loss-during-active-drag --allow-live-input
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario xbutton2-triggered-drag-foreground-loss-then-return-before-release --allow-live-input
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario bluearchive-manual --allow-live-input --target-process BlueArchive.exe
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario bluearchive-manual-wheel --allow-live-input --target-process BlueArchive.exe
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario bluearchive-manual-xbutton --allow-live-input --target-process BlueArchive.exe
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario bluearchive-manual-xbutton-reload --allow-live-input --target-process BlueArchive.exe
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario bluearchive-manual-xbutton-disable --allow-live-input --target-process BlueArchive.exe
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario bluearchive-manual-xbutton-hold-foreground-change --allow-live-input --target-process BlueArchive.exe
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario bluearchive-manual-xbutton-blocked-hold-return --allow-live-input --target-process BlueArchive.exe
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario bluearchive-manual-xbutton2-hold-foreground-change --allow-live-input --target-process BlueArchive.exe
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario bluearchive-manual-xbutton2-blocked-hold-return --allow-live-input --target-process BlueArchive.exe
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario bluearchive-manual-xbutton2-drag-minimal --allow-live-input --target-process BlueArchive.exe
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario bluearchive-manual-xbutton2-multisegment-move-minimal --allow-live-input --target-process BlueArchive.exe
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario bluearchive-manual-xbutton2-drag-stop-during-active-drag --allow-live-input --target-process BlueArchive.exe
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario bluearchive-manual-xbutton2-drag-disable-during-active-drag --allow-live-input --target-process BlueArchive.exe
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario bluearchive-manual-xbutton2-drag-reload-during-active-drag-old-trigger-blocked --allow-live-input --target-process BlueArchive.exe
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario bluearchive-manual-xbutton2-drag-reload-during-active-drag-new-trigger-allowed --allow-live-input --target-process BlueArchive.exe
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario bluearchive-manual-xbutton2-drag-foreground-loss-during-active-drag --allow-live-input --target-process BlueArchive.exe
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario bluearchive-manual-xbutton2-drag-foreground-loss-then-return-before-release --allow-live-input --target-process BlueArchive.exe
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario bluearchive-manual-phase2a --allow-live-input --target-process BlueArchive.exe

        Options:
          --scenario <name>         all, burst-drain, reload-during-burst, stop-during-long-macro,
                                    reload-during-long-macro, foreground-gate-during-burst,
                                    active-pointer-wait-foreground-loss-interrupts,
                                    built-in-drag-foreground-loss-contract,
                                    xbutton2-triggered-complete-drag-normal-completion,
                                    xbutton2-triggered-multisegment-drag-normal-completion,
                                    dry-run-soak, live-safe, live-soak,
                                    lifecycle-start-stop-loop, lifecycle-reload-loop,
                                    lifecycle-enable-disable-loop,
                                    lifecycle-burst-reload-stop-interleave,
                                    lifecycle-stress,
                                    mouse-trigger-suppressed, mouse-trigger-foreground-blocked,
                                    mouse-trigger-reload-disable-stop-clean,
                                    wheel-trigger-boundaries,
                                    xbutton-trigger-boundaries,
                                    xbutton-trigger-reload-disable,
                                    xbutton-self-injected-pass-through,
                                    xbutton1-hold-then-foreground-change-before-release,
                                    xbutton1-blocked-hold-then-foreground-return-before-release,
                                    xbutton2-hold-then-foreground-change-before-release,
                                    xbutton2-blocked-hold-then-foreground-return-before-release,
                                    xbutton2-triggered-drag-minimal,
                                    xbutton2-triggered-multisegment-move-minimal,
                                    xbutton2-triggered-drag-stop-during-active-drag,
                                    xbutton2-triggered-drag-disable-during-active-drag,
                                    xbutton2-triggered-drag-reload-during-active-drag-old-trigger-blocked,
                                    xbutton2-triggered-drag-reload-during-active-drag-new-trigger-allowed,
                                    xbutton2-triggered-drag-foreground-loss-during-active-drag,
                                    xbutton2-triggered-drag-foreground-loss-then-return-before-release,
                                    trigger-captured-then-foreground-changes-before-release,
                                    captured-repeat-down-does-not-redispatch,
                                    self-injected-pass-through, keyboard-trigger-gated-suppress,
                                    bluearchive-manual, bluearchive-manual-wheel, bluearchive-manual-xbutton,
                                    bluearchive-manual-xbutton-reload, bluearchive-manual-xbutton-disable,
                                    bluearchive-manual-xbutton-hold-foreground-change,
                                    bluearchive-manual-xbutton-blocked-hold-return,
                                    bluearchive-manual-xbutton2-hold-foreground-change,
                                    bluearchive-manual-xbutton2-blocked-hold-return,
                                    bluearchive-manual-xbutton2-drag-minimal,
                                    bluearchive-manual-xbutton2-multisegment-move-minimal,
                                    bluearchive-manual-xbutton2-drag-stop-during-active-drag,
                                    bluearchive-manual-xbutton2-drag-disable-during-active-drag,
                                    bluearchive-manual-xbutton2-drag-reload-during-active-drag-old-trigger-blocked,
                                    bluearchive-manual-xbutton2-drag-reload-during-active-drag-new-trigger-allowed,
                                    bluearchive-manual-xbutton2-drag-foreground-loss-during-active-drag,
                                    bluearchive-manual-xbutton2-drag-foreground-loss-then-return-before-release,
                                    bluearchive-manual-phase2a,
                                    trigger-suppress.
          --burst <count>           Burst size for burst scenarios. Default 50.
          --drain-timeout <seconds> Max wait for trigger/action queues to drain. Default 5.
          --soak-seconds <seconds>  Duration for dry-run-soak. Default 300.
          --soak-rate <hz>          Trigger rate for dry-run-soak. Default 20.
          --allow-live-input        Required for live-safe and live-soak. Sends real F13/F14-style input.
          --target-process <name>   Real target process for bluearchive-manual. Default BlueArchive.exe.
          --manual-trigger <key>    Physical keyboard trigger for bluearchive-manual. Default f8.
          --manual-wheel-trigger    Physical wheel trigger for bluearchive-manual-wheel. Default mouse_wheel_up.
          --manual-xbutton-trigger  Physical xbutton trigger for bluearchive-manual-xbutton. Default mouse_x1.
          --manual-xbutton-reload-trigger
                                   Reload target xbutton for bluearchive-manual-xbutton-reload. Default mouse_x2.
          --manual-mouse-trigger    Physical mouse trigger for bluearchive-manual-phase2a. Default mouse_middle.
          --manual-reload-trigger   Reload keyboard trigger for bluearchive-manual-phase2a. Default f9.
          --manual-long-trigger     Long-macro keyboard trigger for bluearchive-manual-phase2a. Default f10.
          --manual-long-only        Restrict bluearchive-manual-phase2a to the isolated F10 long-macro step only.
          --manual-timeout <sec>    Max wait per bluearchive-manual step. Default 15.
          --manual-confirm <mode>   yes, no, or prompt for bluearchive-manual observation. Default prompt.
          --include-snapshots       Include runtime snapshots in JSON metrics.
          --output <path>           Write JSON report to file. If omitted, JSON is written to stdout.
        """);
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

static AppConfigV1 LiveTapConfig(
    string targetProcess,
    string trigger,
    string target,
    double tapHoldMs = 5)
{
    return new AppConfigV1
    {
        TargetProcess = targetProcess,
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

static AppConfigV1 LiveHoldConfig(
    string targetProcess,
    string trigger,
    string target,
    double tapHoldMs = 5)
{
    return new AppConfigV1
    {
        TargetProcess = targetProcess,
        TapHoldMilliseconds = tapHoldMs,
        Mappings =
        [
            new MappingConfigV1
            {
                Trigger = trigger,
                Type = "simple",
                Target = target,
                Mode = "hold"
            }
        ]
    };
}

static AppConfigV1 LiveLongMacroConfig(
    string targetProcess,
    string trigger,
    string heldKey)
{
    return new AppConfigV1
    {
        TargetProcess = targetProcess,
        TapHoldMilliseconds = 5,
        Mappings =
        [
            new MappingConfigV1
            {
                Trigger = trigger,
                Type = "macro",
                Script = $"press {heldKey}\nwait 5000\nrelease {heldKey}"
            }
        ]
    };
}

static AppConfigV1 LiveEscTapConfig(
    string targetProcess,
    string trigger,
    double tapHoldMs = 5)
{
    return LiveTapConfig(targetProcess, trigger, "escape", tapHoldMs);
}

static AppConfigV1 LiveEscHoldConfig(
    string targetProcess,
    string trigger,
    double tapHoldMs = 5)
{
    return LiveHoldConfig(targetProcess, trigger, "escape", tapHoldMs);
}

static AppConfigV1 LiveMappingsConfig(
    string targetProcess,
    params MappingConfigV1[] mappings)
{
    return new AppConfigV1
    {
        TargetProcess = targetProcess,
        TapHoldMilliseconds = 5,
        Mappings = mappings
    };
}

static AppConfigV1 LiveMacroConfig(
    string targetProcess,
    string mappingId,
    string trigger,
    string script)
{
    return LiveMappingsConfig(
        targetProcess,
        new MappingConfigV1
        {
            Id = mappingId,
            Trigger = trigger,
            Type = "macro",
            Script = script
        });
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

static AcceptanceScenarioResult Passed(
    string name,
    Stopwatch stopwatch,
    Dictionary<string, object> metrics)
{
    stopwatch.Stop();
    return new AcceptanceScenarioResult(
        name,
        true,
        stopwatch.Elapsed.TotalMilliseconds,
        null,
        metrics);
}

static AcceptanceScenarioResult Failed(
    string name,
    Stopwatch stopwatch,
    string error,
    Dictionary<string, object>? metrics = null)
{
    stopwatch.Stop();
    return new AcceptanceScenarioResult(
        name,
        false,
        stopwatch.Elapsed.TotalMilliseconds,
        error,
        metrics ?? []);
}

static void AddSnapshots(
    Dictionary<string, object> metrics,
    AcceptanceOptions options,
    params (string Name, RuntimeHostSnapshot Snapshot)[] snapshots)
{
    if (!options.IncludeSnapshots)
    {
        return;
    }

    foreach (var (name, snapshot) in snapshots)
    {
        metrics[$"snapshot_{name}"] = snapshot;
    }
}

static ResourceSample CaptureResourceSample(Process process)
{
    process.Refresh();
    return new ResourceSample(
        process.WorkingSet64,
        process.PrivateMemorySize64,
        process.Threads.Count,
        process.TotalProcessorTime);
}

static void AddResourceMetrics(
    Dictionary<string, object> metrics,
    ResourceSample start,
    ResourceSample end,
    TimeSpan elapsed)
{
    var elapsedMilliseconds = Math.Max(elapsed.TotalMilliseconds, 0.001);
    var cpuPercent = (end.TotalProcessorTime - start.TotalProcessorTime).TotalMilliseconds /
        (elapsedMilliseconds * Environment.ProcessorCount) * 100.0;

    metrics["working_set_start_mb"] = Math.Round(start.WorkingSetBytes / 1024.0 / 1024.0, 3);
    metrics["working_set_end_mb"] = Math.Round(end.WorkingSetBytes / 1024.0 / 1024.0, 3);
    metrics["working_set_delta_mb"] = Math.Round((end.WorkingSetBytes - start.WorkingSetBytes) / 1024.0 / 1024.0, 3);
    metrics["private_memory_start_mb"] = Math.Round(start.PrivateMemoryBytes / 1024.0 / 1024.0, 3);
    metrics["private_memory_end_mb"] = Math.Round(end.PrivateMemoryBytes / 1024.0 / 1024.0, 3);
    metrics["private_memory_delta_mb"] = Math.Round((end.PrivateMemoryBytes - start.PrivateMemoryBytes) / 1024.0 / 1024.0, 3);
    metrics["thread_count_start"] = start.ThreadCount;
    metrics["thread_count_end"] = end.ThreadCount;
    metrics["thread_count_delta"] = end.ThreadCount - start.ThreadCount;
    metrics["cpu_percent_estimate"] = Math.Round(cpuPercent, 3);
}

static void TrackPeak(RuntimeHostSnapshot snapshot, Dictionary<string, long> peaks)
{
    peaks["pipeline_pending_max"] = Math.Max(peaks["pipeline_pending_max"], snapshot.Pipeline.PendingCount);
    peaks["runtime_pending_actions_max"] = Math.Max(peaks["runtime_pending_actions_max"], snapshot.Runtime.PendingActionCount);
    peaks["runtime_running_actions_max"] = Math.Max(peaks["runtime_running_actions_max"], snapshot.Runtime.RunningActionCount);
    peaks["active_workers_max"] = Math.Max(peaks["active_workers_max"], snapshot.Runtime.ActiveWorkerCount);
    peaks["held_key_count_max"] = Math.Max(peaks["held_key_count_max"], snapshot.Runtime.Presses.KeyOwners.Count);
    peaks["press_owner_count_max"] = Math.Max(peaks["press_owner_count_max"], snapshot.Runtime.Presses.OwnerKeys.Count);
}

static async Task SendTriggerTapAsync(IInputBackend sender, string key)
{
    await sender.SendAsync(
        [InputCommand.KeyDown(key), InputCommand.KeyUp(key)],
        CancellationToken.None);
}

static async Task SendMouseClickAsync(IInputBackend sender, string button)
{
    await sender.SendAsync(
        [InputCommand.MouseDown(button), InputCommand.MouseUp(button)],
        CancellationToken.None);
}

static Task SendMouseDownAsync(IInputBackend sender, string button)
{
    return sender.SendAsync([InputCommand.MouseDown(button)], CancellationToken.None).AsTask();
}

static Task SendMouseUpAsync(IInputBackend sender, string button)
{
    return sender.SendAsync([InputCommand.MouseUp(button)], CancellationToken.None).AsTask();
}

static Task SendMouseWheelAsync(IInputBackend sender, int delta)
{
    return sender.SendAsync([InputCommand.MouseWheel(delta)], CancellationToken.None).AsTask();
}

static int CountSessionEvent(CountingDiagnosticsSink diagnostics, string eventName)
{
    return diagnostics.CountWhere(evt =>
        string.Equals(evt.Source, "session", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(evt.Name, eventName, StringComparison.OrdinalIgnoreCase));
}

static int CountDiagnosticEvent(
    CountingDiagnosticsSink diagnostics,
    string source,
    string eventName)
{
    return diagnostics.CountWhere(evt =>
        string.Equals(evt.Source, source, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(evt.Name, eventName, StringComparison.OrdinalIgnoreCase));
}

static DiagnosticEvent? FirstDiagnosticEvent(
    CountingDiagnosticsSink diagnostics,
    string source,
    string eventName)
{
    return diagnostics.Events.FirstOrDefault(evt =>
        string.Equals(evt.Source, source, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(evt.Name, eventName, StringComparison.OrdinalIgnoreCase));
}

static double ElapsedMilliseconds(long startTicks, long endTicks)
{
    return (endTicks - startTicks) * 1000.0 / Stopwatch.Frequency;
}

static int CountMacroEvent(CountingDiagnosticsSink diagnostics, string eventName, string ownerPrefix)
{
    return diagnostics.CountWhere(evt =>
        string.Equals(evt.Source, "macro", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(evt.Name, eventName, StringComparison.OrdinalIgnoreCase) &&
        evt.Fields.TryGetValue("owner", out var owner) &&
        owner.StartsWith(ownerPrefix, StringComparison.OrdinalIgnoreCase));
}

static int CountMacroDiagnostic(
    CountingDiagnosticsSink diagnostics,
    string eventName,
    string? helper = null,
    string? phase = null,
    string? reason = null)
{
    return diagnostics.CountWhere(evt =>
        string.Equals(evt.Source, "macro", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(evt.Name, eventName, StringComparison.OrdinalIgnoreCase) &&
        (helper is null ||
            (evt.Fields.TryGetValue("helper", out var eventHelper) &&
                string.Equals(eventHelper, helper, StringComparison.OrdinalIgnoreCase))) &&
        (phase is null ||
            (evt.Fields.TryGetValue("phase", out var eventPhase) &&
                string.Equals(eventPhase, phase, StringComparison.OrdinalIgnoreCase))) &&
        (reason is null ||
            (evt.Fields.TryGetValue("reason", out var eventReason) &&
                string.Equals(eventReason, reason, StringComparison.OrdinalIgnoreCase))));
}

static string? FirstMacroDiagnosticField(
    CountingDiagnosticsSink diagnostics,
    string eventName,
    string field,
    string? helper = null)
{
    return diagnostics.Events
        .FirstOrDefault(evt =>
            string.Equals(evt.Source, "macro", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(evt.Name, eventName, StringComparison.OrdinalIgnoreCase) &&
            (helper is null ||
                (evt.Fields.TryGetValue("helper", out var eventHelper) &&
                    string.Equals(eventHelper, helper, StringComparison.OrdinalIgnoreCase))))?
        .Fields
        .GetValueOrDefault(field);
}

static IReadOnlyList<MacroInstruction> CompileAcceptanceMacro(string script)
{
    var result = new MacroScriptCompiler().Compile(script);
    if (!result.Success)
    {
        throw new InvalidOperationException(string.Join(Environment.NewLine, result.Errors));
    }

    return result.Instructions;
}

static IReadOnlyList<InputEvent> MoveEvents(CountingInputBackend backend)
{
    return backend.Events
        .Where(evt => string.Equals(evt.Kind, "move", StringComparison.OrdinalIgnoreCase))
        .ToArray();
}

static (int Dx, int Dy) TotalMoveDelta(IReadOnlyList<InputEvent> moveEvents, int startX, int startY)
{
    if (moveEvents.Count == 0)
    {
        return (0, 0);
    }

    var last = moveEvents[^1];
    return ((last.X ?? startX) - startX, (last.Y ?? startY) - startY);
}

static async Task<bool> TryRestoreCursorAsync(IInputBackend backend, int x, int y)
{
    try
    {
        await backend.MoveMouseToAsync(x, y, CancellationToken.None);
        var restored = await backend.GetMousePositionAsync(CancellationToken.None);
        return restored.X == x && restored.Y == y;
    }
    catch
    {
        return false;
    }
}

static string InterruptibleDragScript(
    string mappedDragButton,
    int firstDx = 18,
    int firstDy = 10,
    int waitMs = 2000,
    int secondDx = 12,
    int secondDy = 6)
{
    return string.Join(
        "\n",
        $"press {mappedDragButton}",
        $"setpos_rel {firstDx} {firstDy}",
        $"wait {waitMs}",
        $"setpos_rel {secondDx} {secondDy}",
        $"wait {waitMs}",
        $"release {mappedDragButton}");
}

static async Task<(RuntimeHostSnapshot Snapshot,
    int TriggerDownReceivedCount,
    int TriggerUpReceivedCount,
    int PipelineEnqueuedCount,
    int RuntimeDispatchedCount,
    int MacroStartedCount,
    int MacroFinishedCount,
    int DragButtonDownCount,
    int DragButtonUpCount,
    int MoveEventCount,
    int TotalEventCount,
    int CursorX,
    int CursorY)?> WaitForActiveDragBarrierAsync(
    RuntimeHost host,
    CountingInputBackend runtimeInput,
    CountingDiagnosticsSink diagnostics,
    string triggerKey,
    string mappedDragButton,
    string mappingId,
    TimeSpan timeout,
    CancellationToken cancellationToken)
{
    var start = Stopwatch.StartNew();
    while (start.Elapsed < timeout)
    {
        var snapshot = host.Snapshot();
        var triggerDownReceivedCount = CountHookTriggerReceived(diagnostics, triggerKey, "down");
        var triggerUpReceivedCount = CountHookTriggerReceived(diagnostics, triggerKey, "up");
        var pipelineEnqueuedCount = CountPipelineQueued(diagnostics, triggerKey, "down");
        var runtimeDispatchedCount = CountRuntimeDispatched(diagnostics, triggerKey, "down");
        var macroStartedCount = CountMacroEvent(diagnostics, "started", $"{mappingId}:macro:");
        var macroFinishedCount = CountMacroEvent(diagnostics, "finished", $"{mappingId}:macro:");
        var dragButtonDownCount = runtimeInput.Count("mouse", mappedDragButton, true);
        var dragButtonUpCount = runtimeInput.Count("mouse", mappedDragButton, false);
        var moveEventCount = MoveEvents(runtimeInput).Count;

        if (dragButtonDownCount > 0 &&
            moveEventCount > 0 &&
            dragButtonUpCount == 0 &&
            macroStartedCount > 0 &&
            macroFinishedCount == 0 &&
            snapshot.Runtime.Presses.OwnerKeys.Count > 0)
        {
            var cursor = await runtimeInput.GetMousePositionAsync(cancellationToken);
            return (
                snapshot,
                triggerDownReceivedCount,
                triggerUpReceivedCount,
                pipelineEnqueuedCount,
                runtimeDispatchedCount,
                macroStartedCount,
                macroFinishedCount,
                dragButtonDownCount,
                dragButtonUpCount,
                moveEventCount,
                runtimeInput.Events.Count,
                cursor.X,
                cursor.Y);
        }

        await Task.Delay(5, cancellationToken);
    }

    return null;
}

static async Task<(RuntimeHostSnapshot Snapshot,
    int ForegroundLostWhileDragActiveCount,
    int MappedOwnerCleanupCompletedCount,
    int MacroFinishedCount,
    int DragButtonUpTotalCount,
    int MoveEventTotalCount,
    int TotalEventCount,
    int CursorX,
    int CursorY)?> WaitForForegroundLossCleanupBarrierAsync(
    RuntimeHost host,
    CountingInputBackend runtimeInput,
    CountingDiagnosticsSink diagnostics,
    string mappingId,
    string mappedDragButton,
    int baselineDragButtonUpCount,
    TimeSpan timeout,
    CancellationToken cancellationToken)
{
    var start = Stopwatch.StartNew();
    while (start.Elapsed < timeout)
    {
        var snapshot = host.Snapshot();
        var foregroundLostWhileDragActiveCount = CountDiagnosticEvent(
            diagnostics,
            "macro",
            "foreground_lost_during_active_pointer_sequence");
        var mappedOwnerCleanupCompletedCount = CountDiagnosticEvent(
            diagnostics,
            "macro",
            "foreground_cleanup_completed");
        var macroFinishedCount = CountMacroEvent(diagnostics, "finished", $"{mappingId}:macro:");
        var dragButtonUpTotalCount = runtimeInput.Count("mouse", mappedDragButton, false);
        var moveEventTotalCount = MoveEvents(runtimeInput).Count;

        if (foregroundLostWhileDragActiveCount > 0 &&
            mappedOwnerCleanupCompletedCount > 0 &&
            macroFinishedCount > 0 &&
            dragButtonUpTotalCount > baselineDragButtonUpCount &&
            snapshot.Runtime.Presses.OwnerKeys.Count == 0 &&
            snapshot.Runtime.PendingActionCount == 0 &&
            snapshot.Runtime.RunningActionCount == 0)
        {
            var cursor = await runtimeInput.GetMousePositionAsync(cancellationToken);
            return (
                snapshot,
                foregroundLostWhileDragActiveCount,
                mappedOwnerCleanupCompletedCount,
                macroFinishedCount,
                dragButtonUpTotalCount,
                moveEventTotalCount,
                runtimeInput.Events.Count,
                cursor.X,
                cursor.Y);
        }

        await Task.Delay(5, cancellationToken);
    }

    return null;
}

static string CurrentProcessTargetName()
{
    return $"{Process.GetCurrentProcess().ProcessName}.exe";
}

const uint ProcessQueryLimitedInformation = 0x1000;
const uint TokenQuery = 0x0008;
const int TokenElevation = 20;

static ElevationProbeResult ProbeCurrentProcessElevation()
{
    return ProbeProcessElevation(Process.GetCurrentProcess().Id, CurrentProcessTargetName());
}

static ElevationProbeResult ProbeProcessElevation(int processId, string processName)
{
    var processHandle = NativeMethods.OpenProcess(ProcessQueryLimitedInformation, false, (uint)processId);
    if (processHandle == IntPtr.Zero)
    {
        return new ElevationProbeResult(processId, processName, IsKnown: false, IsElevated: false);
    }

    try
    {
        if (!NativeMethods.OpenProcessToken(processHandle, TokenQuery, out var tokenHandle))
        {
            return new ElevationProbeResult(processId, processName, IsKnown: false, IsElevated: false);
        }

        try
        {
            var buffer = Marshal.AllocHGlobal(sizeof(int));
            try
            {
                if (!NativeMethods.GetTokenInformation(tokenHandle, TokenElevation, buffer, sizeof(int), out _))
                {
                    return new ElevationProbeResult(processId, processName, IsKnown: false, IsElevated: false);
                }

                return new ElevationProbeResult(
                    processId,
                    processName,
                    IsKnown: true,
                    IsElevated: Marshal.ReadInt32(buffer) != 0);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        finally
        {
            _ = NativeMethods.CloseHandle(tokenHandle);
        }
    }
    finally
    {
        _ = NativeMethods.CloseHandle(processHandle);
    }
}

static string ElevationStateLabel(ElevationProbeResult result)
{
    return !result.IsKnown
        ? "unknown"
        : result.IsElevated
            ? "elevated"
            : "not_elevated";
}

static bool? PromptForManualYesNo(string prompt)
{
    if (Console.IsInputRedirected)
    {
        return null;
    }

    Console.Error.Write($"{prompt} [yes/no]: ");
    var response = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(response))
    {
        return false;
    }

    return response.Equals("y", StringComparison.OrdinalIgnoreCase) ||
        response.Equals("yes", StringComparison.OrdinalIgnoreCase);
}

static bool? ResolveManualConfirmation(AcceptanceOptions options, string prompt)
{
    var mode = options.ManualConfirmationMode.Trim();
    if (mode.Length != 0)
    {
        if (string.Equals(mode, "yes", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mode, "y", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mode, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(mode, "no", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mode, "n", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mode, "false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
    }

    return PromptForManualYesNo(prompt);
}

static Process? FindTargetProcessWithWindow(string processName)
{
    var normalized = NormalizeProcessName(processName);
    return Process.GetProcesses()
        .Where(process =>
        {
            try
            {
                return string.Equals(
                    NormalizeProcessName(process.ProcessName),
                    normalized,
                    StringComparison.OrdinalIgnoreCase) &&
                    process.MainWindowHandle != IntPtr.Zero;
            }
            catch
            {
                return false;
            }
        })
        .OrderByDescending(process =>
        {
            try
            {
                return process.StartTime;
            }
            catch
            {
                return DateTime.MinValue;
            }
        })
        .FirstOrDefault();
}

static string NormalizeProcessName(string? processName)
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

const int VirtualKeyF13 = 0x7C;

static async Task<bool> WaitForInputCountAsync(
    CountingInputBackend backend,
    string kind,
    string code,
    int expectedCount,
    TimeSpan timeout)
{
    var watch = Stopwatch.StartNew();
    while (watch.Elapsed < timeout)
    {
        if (backend.Count(kind, code) >= expectedCount)
        {
            return true;
        }

        await Task.Delay(10);
    }

    return false;
}

static async Task<Process?> StartPowerShellWindowAsync(string title, TimeSpan timeout)
{
    var script = string.Join(
        " ",
        "Add-Type -AssemblyName System.Windows.Forms;",
        "$f = New-Object System.Windows.Forms.Form;",
        $"$f.Text = '{title.Replace("'", "''")}';",
        "$f.Width = 420;",
        "$f.Height = 180;",
        "$f.TopMost = $true;",
        "[System.Windows.Forms.Application]::Run($f)");

    Process? process = null;
    try
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(script);
        process = Process.Start(startInfo);
    }
    catch
    {
        return null;
    }

    if (process is null)
    {
        return null;
    }

    var watch = Stopwatch.StartNew();
    while (watch.Elapsed < timeout)
    {
        try
        {
            process.Refresh();
            if (process.HasExited)
            {
                return null;
            }

            if (process.MainWindowHandle != IntPtr.Zero)
            {
                return process;
            }
        }
        catch
        {
            return null;
        }

        await Task.Delay(50);
    }

    CloseProcessWindow(process);
    return null;
}

static void CloseProcessWindow(Process? process)
{
    if (process is null)
    {
        return;
    }

    try
    {
        if (process.HasExited)
        {
            return;
        }

        _ = process.CloseMainWindow();
        if (!process.WaitForExit(2000))
        {
            process.Kill(entireProcessTree: true);
        }
    }
    catch
    {
        // Best-effort cleanup for the temporary blocker process.
    }
    finally
    {
        process.Dispose();
    }
}

static int CountHookTriggerReceived(
    CountingDiagnosticsSink diagnostics,
    string triggerKey,
    string phase = "down",
    bool? suppress = null,
    bool? dispatch = null)
{
    return diagnostics.CountWhere(evt =>
        string.Equals(evt.Source, "windows_hook", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(evt.Name, "trigger_received", StringComparison.OrdinalIgnoreCase) &&
        evt.Fields.TryGetValue("trigger", out var trigger) &&
        string.Equals(trigger, triggerKey, StringComparison.OrdinalIgnoreCase) &&
        evt.Fields.TryGetValue("phase", out var phaseValue) &&
        string.Equals(phaseValue, phase, StringComparison.OrdinalIgnoreCase) &&
        (!suppress.HasValue ||
            (evt.Fields.TryGetValue("suppress", out var suppressValue) &&
             string.Equals(suppressValue, suppress.Value.ToString(), StringComparison.OrdinalIgnoreCase))) &&
        (!dispatch.HasValue ||
            (evt.Fields.TryGetValue("dispatch", out var dispatchValue) &&
             string.Equals(dispatchValue, dispatch.Value.ToString(), StringComparison.OrdinalIgnoreCase))));
}

static int CountHookLifecycleEvent(
    CountingDiagnosticsSink diagnostics,
    string eventName,
    string triggerKey,
    string? phase = null)
{
    return diagnostics.CountWhere(evt =>
        string.Equals(evt.Source, "windows_hook", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(evt.Name, eventName, StringComparison.OrdinalIgnoreCase) &&
        evt.Fields.TryGetValue("trigger", out var trigger) &&
        string.Equals(trigger, triggerKey, StringComparison.OrdinalIgnoreCase) &&
        (phase is null ||
            (evt.Fields.TryGetValue("phase", out var phaseValue) &&
             string.Equals(phaseValue, phase, StringComparison.OrdinalIgnoreCase))));
}

static int CountPipelineQueued(
    CountingDiagnosticsSink diagnostics,
    string triggerKey,
    string phase = "down")
{
    return diagnostics.CountWhere(evt =>
        string.Equals(evt.Source, "trigger_pipeline", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(evt.Name, "queued", StringComparison.OrdinalIgnoreCase) &&
        evt.Fields.TryGetValue("trigger", out var trigger) &&
        string.Equals(trigger, triggerKey, StringComparison.OrdinalIgnoreCase) &&
        evt.Fields.TryGetValue("phase", out var phaseValue) &&
        string.Equals(phaseValue, phase, StringComparison.OrdinalIgnoreCase));
}

static int CountRuntimeDispatched(
    CountingDiagnosticsSink diagnostics,
    string triggerKey,
    string phase = "down")
{
    return diagnostics.CountWhere(evt =>
        string.Equals(evt.Source, "runtime", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(evt.Name, "trigger_received", StringComparison.OrdinalIgnoreCase) &&
        evt.Fields.TryGetValue("trigger", out var trigger) &&
        string.Equals(trigger, triggerKey, StringComparison.OrdinalIgnoreCase) &&
        evt.Fields.TryGetValue("phase", out var phaseValue) &&
        string.Equals(phaseValue, phase, StringComparison.OrdinalIgnoreCase));
}

static int CountRuntimeForegroundAllowed(CountingDiagnosticsSink diagnostics)
{
    return diagnostics.CountWhere(evt =>
        string.Equals(evt.Source, "runtime", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(evt.Name, "foreground_checked", StringComparison.OrdinalIgnoreCase) &&
        evt.Fields.TryGetValue("allowed", out var allowed) &&
        string.Equals(allowed, bool.TrueString, StringComparison.OrdinalIgnoreCase));
}

static DiagnosticEvent[] CollectRuntimeIgnored(CountingDiagnosticsSink diagnostics, string triggerKey)
{
    return diagnostics.Events
        .Where(evt =>
            string.Equals(evt.Source, "runtime", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(evt.Name, "trigger_ignored", StringComparison.OrdinalIgnoreCase) &&
            evt.Fields.TryGetValue("trigger", out var trigger) &&
            string.Equals(trigger, triggerKey, StringComparison.OrdinalIgnoreCase))
        .ToArray();
}

static string JoinIgnoredReasons(IReadOnlyList<DiagnosticEvent> ignoredEvents)
{
    return string.Join(",",
        ignoredEvents
            .Select(evt => evt.Fields.TryGetValue("reason", out var reason) ? reason : string.Empty)
            .Where(reason => !string.IsNullOrWhiteSpace(reason))
            .Distinct(StringComparer.OrdinalIgnoreCase));
}

static async Task<AcceptanceScenarioResult> RunBurstDrainAsync(AcceptanceOptions options)
{
    const string name = "burst-drain";
    var watch = Stopwatch.StartNew();
    var dryRun = new DryRunInputBackend();
    var source = new ManualTriggerSource();
    await using var host = new RuntimeHost(
        TapConfig("q", "1"),
        dryRun,
        AlwaysForegroundGate.Instance,
        source,
        new InMemoryDiagnosticsSink());

    await host.StartAsync(CancellationToken.None);
    var emitWatch = Stopwatch.StartNew();
    for (var index = 0; index < options.BurstCount; index++)
    {
        source.KeyDown("q");
    }

    emitWatch.Stop();
    if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
    {
        return Failed(name, watch, "trigger/action queues did not drain");
    }

    var expectedEvents = options.BurstCount * 2;
    if (dryRun.Events.Count != expectedEvents)
    {
        return Failed(
            name,
            watch,
            $"input event count mismatch: expected={expectedEvents} actual={dryRun.Events.Count}");
    }

    var running = host.Snapshot();
    await host.StopAsync(CancellationToken.None);
    var stopped = host.Snapshot();
    if (!IsCleanlyStopped(stopped))
    {
        return Failed(name, watch, "stopped runtime is not clean");
    }

    var metrics = new Dictionary<string, object>
    {
        ["burst_count"] = options.BurstCount,
        ["input_events"] = dryRun.Events.Count,
        ["emit_ms"] = emitWatch.Elapsed.TotalMilliseconds,
        ["pipeline_queued"] = running.Pipeline.QueuedCount,
        ["pipeline_handled"] = running.Pipeline.HandledCount
    };
    AddSnapshots(metrics, options, ("running", running), ("stopped", stopped));
    return Passed(name, watch, metrics);
}

static async Task<AcceptanceScenarioResult> RunReloadDuringBurstAsync(AcceptanceOptions options)
{
    const string name = "reload-during-burst";
    var watch = Stopwatch.StartNew();
    var dryRun = new DryRunInputBackend();
    var source = new ManualTriggerSource();
    await using var host = new RuntimeHost(
        TapConfig("q", "1", tapHoldMs: 40),
        dryRun,
        AlwaysForegroundGate.Instance,
        source,
        new InMemoryDiagnosticsSink());

    await host.StartAsync(CancellationToken.None);
    for (var index = 0; index < options.BurstCount; index++)
    {
        source.KeyDown("q");
    }

    await host.ReloadAsync(TapConfig("w", "2"), CancellationToken.None);
    if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
    {
        return Failed(name, watch, "queues did not drain after reload");
    }

    var oldOutputBeforeProbe = dryRun.Count("key", "1");
    source.KeyDown("q");
    if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
    {
        return Failed(name, watch, "queues did not drain after old trigger probe");
    }

    if (dryRun.Count("key", "1") != oldOutputBeforeProbe)
    {
        return Failed(name, watch, "old trigger produced input after reload");
    }

    source.KeyDown("w");
    if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
    {
        return Failed(name, watch, "queues did not drain after new trigger probe");
    }

    if (dryRun.Count("key", "2", true) != 1 || dryRun.Count("key", "2", false) != 1)
    {
        return Failed(name, watch, "new trigger did not produce one complete tap");
    }

    var running = host.Snapshot();
    await host.StopAsync(CancellationToken.None);
    var stopped = host.Snapshot();
    if (!IsCleanlyStopped(stopped))
    {
        return Failed(name, watch, "stopped runtime is not clean");
    }

    var metrics = new Dictionary<string, object>
    {
        ["burst_count"] = options.BurstCount,
        ["old_input_events_before_probe"] = oldOutputBeforeProbe,
        ["new_input_events"] = dryRun.Count("key", "2"),
        ["generation"] = running.Runtime.Generation
    };
    AddSnapshots(metrics, options, ("running", running), ("stopped", stopped));
    return Passed(name, watch, metrics);
}

static async Task<AcceptanceScenarioResult> RunStopDuringLongMacroAsync(AcceptanceOptions options)
{
    const string name = "stop-during-long-macro";
    var watch = Stopwatch.StartNew();
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
    if (!await WaitUntilAsync(
            () => dryRun.Count("key", "a", true) > 0,
            options.DrainTimeout,
            CancellationToken.None))
    {
        return Failed(name, watch, "macro key was not pressed before timeout");
    }

    var running = host.Snapshot();
    await host.StopAsync(CancellationToken.None);
    var stopped = host.Snapshot();
    var eventCountAfterStop = dryRun.Events.Count;
    await Task.Delay(100);

    if (dryRun.Count("key", "a", true) != 1 || dryRun.Count("key", "a", false) != 1)
    {
        return Failed(name, watch, "held macro key was not released exactly once");
    }

    if (dryRun.Events.Count != eventCountAfterStop)
    {
        return Failed(name, watch, "input was emitted after stop");
    }

    if (!IsCleanlyStopped(stopped))
    {
        return Failed(name, watch, "stopped runtime is not clean");
    }

    var metrics = new Dictionary<string, object>
    {
        ["key_a_down"] = dryRun.Count("key", "a", true),
        ["key_a_up"] = dryRun.Count("key", "a", false),
        ["post_stop_events"] = dryRun.Events.Count - eventCountAfterStop
    };
    AddSnapshots(metrics, options, ("running", running), ("stopped", stopped));
    return Passed(name, watch, metrics);
}

static async Task<AcceptanceScenarioResult> RunReloadDuringLongMacroAsync(AcceptanceOptions options)
{
    const string name = "reload-during-long-macro";
    var watch = Stopwatch.StartNew();
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
    if (!await WaitUntilAsync(
            () => dryRun.Count("key", "a", true) > 0,
            options.DrainTimeout,
            CancellationToken.None))
    {
        return Failed(name, watch, "macro key was not pressed before reload timeout");
    }

    await host.ReloadAsync(TapConfig("w", "2"), CancellationToken.None);
    var afterReload = host.Snapshot();
    if (dryRun.Count("key", "a", true) != 1 || dryRun.Count("key", "a", false) != 1)
    {
        return Failed(name, watch, "old macro key was not released exactly once during reload");
    }

    if (!afterReload.Runtime.Presses.IsEmpty ||
        afterReload.Runtime.PendingActionCount != 0 ||
        afterReload.Runtime.RunningActionCount != 0)
    {
        return Failed(name, watch, "old macro runtime state survived reload");
    }

    var oldMacroEvents = dryRun.Count("key", "a");
    source.KeyDown("q");
    if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
    {
        return Failed(name, watch, "queues did not drain after old macro trigger probe");
    }

    if (dryRun.Count("key", "a") != oldMacroEvents)
    {
        return Failed(name, watch, "old macro trigger produced input after reload");
    }

    source.KeyDown("w");
    if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
    {
        return Failed(name, watch, "queues did not drain after new trigger probe");
    }

    if (dryRun.Count("key", "2", true) != 1 || dryRun.Count("key", "2", false) != 1)
    {
        return Failed(name, watch, "new trigger did not produce one complete tap");
    }

    var running = host.Snapshot();
    await host.StopAsync(CancellationToken.None);
    var stopped = host.Snapshot();
    if (!IsCleanlyStopped(stopped))
    {
        return Failed(name, watch, "stopped runtime is not clean");
    }

    var metrics = new Dictionary<string, object>
    {
        ["key_a_down"] = dryRun.Count("key", "a", true),
        ["key_a_up"] = dryRun.Count("key", "a", false),
        ["new_key_2_down"] = dryRun.Count("key", "2", true),
        ["new_key_2_up"] = dryRun.Count("key", "2", false)
    };
    AddSnapshots(metrics, options, ("after_reload", afterReload), ("running", running), ("stopped", stopped));
    return Passed(name, watch, metrics);
}

static async Task<AcceptanceScenarioResult> RunForegroundGateDuringBurstAsync(AcceptanceOptions options)
{
    const string name = "foreground-gate-during-burst";
    var watch = Stopwatch.StartNew();
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
    for (var index = 0; index < options.BurstCount; index++)
    {
        source.KeyDown("q");
    }

    if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
    {
        return Failed(name, watch, "blocked foreground burst did not drain");
    }

    if (dryRun.Events.Count != 0)
    {
        return Failed(name, watch, $"blocked foreground emitted input: events={dryRun.Events.Count}");
    }

    var blocked = host.Snapshot();
    if (blocked.Runtime.LastForegroundAllowed)
    {
        return Failed(name, watch, "blocked foreground state was not recorded");
    }

    gate.IsAllowed = true;
    gate.ForegroundProcess = "BlueArchive.exe";
    source.KeyDown("q");
    if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
    {
        return Failed(name, watch, "allowed foreground trigger did not drain");
    }

    if (dryRun.Count("key", "1", true) != 1 || dryRun.Count("key", "1", false) != 1)
    {
        return Failed(name, watch, "allowed foreground did not produce one complete tap");
    }

    var running = host.Snapshot();
    await host.StopAsync(CancellationToken.None);
    var stopped = host.Snapshot();
    if (!IsCleanlyStopped(stopped))
    {
        return Failed(name, watch, "stopped runtime is not clean");
    }

    var metrics = new Dictionary<string, object>
    {
        ["blocked_burst"] = options.BurstCount,
        ["blocked_input_events"] = 0,
        ["allowed_key_1_down"] = dryRun.Count("key", "1", true),
        ["allowed_key_1_up"] = dryRun.Count("key", "1", false),
        ["blocked_pipeline_handled"] = blocked.Pipeline.HandledCount
    };
    AddSnapshots(metrics, options, ("blocked", blocked), ("running", running), ("stopped", stopped));
    return Passed(name, watch, metrics);
}

static async Task<AcceptanceScenarioResult> RunActivePointerWaitForegroundLossInterruptsAsync(AcceptanceOptions options)
{
    const string name = "active-pointer-wait-foreground-loss-interrupts";
    const string mappingId = "active-pointer-wait-foreground-loss-interrupts";
    const string trigger = "q";
    const string triggerKey = "keyboard:q";
    const string mappedDragButton = "mouse_middle";
    const int waitMs = 5000;
    const double cleanupLatencyBoundMs = 250;
    var watch = Stopwatch.StartNew();
    var runtimeInput = new CountingInputBackend(new DryRunInputBackend());
    var source = new ManualTriggerSource();
    var gate = new ManualForegroundGate
    {
        IsAllowed = true,
        ForegroundProcess = "BlueArchive.exe"
    };
    var diagnostics = new CountingDiagnosticsSink();
    await using var host = new RuntimeHost(
        LiveMacroConfig(
            "BlueArchive.exe",
            mappingId,
            trigger,
            InterruptibleDragScript(mappedDragButton, waitMs: waitMs)),
        runtimeInput,
        gate,
        source,
        diagnostics);

    Dictionary<string, object> CreateMetrics()
    {
        var snapshot = host.Snapshot();
        return new Dictionary<string, object>
        {
            ["validation_scope"] = "phase2d-e-active-pointer-wait-foreground-loss-interrupts",
            ["foreground_cleanup_timing_model"] = "foreground_aware_interruptible_wait",
            ["cleanup_latency_bound_scope"] = "deterministic_dry_harness_only",
            ["real_target_cleanup_latency_slo_proven"] = false,
            ["wait_requested_ms"] = waitMs,
            ["cleanup_latency_bound_ms"] = cleanupLatencyBoundMs,
            ["trigger_received_count"] = CountRuntimeDispatched(diagnostics, triggerKey, "down"),
            ["pipeline_enqueued_count"] = CountPipelineQueued(diagnostics, triggerKey, "down"),
            ["runtime_dispatched_count"] = CountRuntimeDispatched(diagnostics, triggerKey, "down"),
            ["macro_started_count"] = CountMacroEvent(diagnostics, "started", $"{mappingId}:macro:"),
            ["macro_finished_count"] = CountMacroEvent(diagnostics, "finished", $"{mappingId}:macro:"),
            ["active_pointer_wait_started_count"] = CountDiagnosticEvent(diagnostics, "macro", "active_pointer_wait_started"),
            ["foreground_loss_detected_during_wait_count"] = CountDiagnosticEvent(diagnostics, "macro", "foreground_loss_detected_during_wait"),
            ["active_pointer_wait_interrupted_count"] = CountDiagnosticEvent(diagnostics, "macro", "active_pointer_wait_interrupted"),
            ["foreground_loss_detected_count"] = CountDiagnosticEvent(diagnostics, "macro", "foreground_lost_during_active_pointer_sequence"),
            ["foreground_cleanup_completed_count"] = CountDiagnosticEvent(diagnostics, "macro", "foreground_cleanup_completed"),
            ["wait_completed_normally_count"] = CountDiagnosticEvent(diagnostics, "macro", "wait_completed"),
            ["drag_button_down_count"] = runtimeInput.Count("mouse", mappedDragButton, true),
            ["drag_button_up_count"] = runtimeInput.Count("mouse", mappedDragButton, false),
            ["move_event_count"] = MoveEvents(runtimeInput).Count,
            ["held_owner_count"] = snapshot.Runtime.Presses.OwnerKeys.Count
        };
    }

    try
    {
        await host.StartAsync(CancellationToken.None);
        source.KeyDown(trigger);

        if (!await WaitUntilAsync(
                () => CountDiagnosticEvent(diagnostics, "macro", "active_pointer_wait_started") == 1 &&
                      runtimeInput.Count("mouse", mappedDragButton, true) == 1 &&
                      MoveEvents(runtimeInput).Count >= 1 &&
                      runtimeInput.Count("mouse", mappedDragButton, false) == 0 &&
                      host.Snapshot().Runtime.Presses.OwnerKeys.Count > 0,
                options.DrainTimeout,
                CancellationToken.None))
        {
            var failedMetrics = CreateMetrics();
            AddSnapshots(failedMetrics, options, ("running", host.Snapshot()));
            return Failed(name, watch, "active pointer wait barrier was not reached before foreground-loss probe", failedMetrics);
        }

        var waitStarted = FirstDiagnosticEvent(diagnostics, "macro", "active_pointer_wait_started");
        if (waitStarted is null)
        {
            var failedMetrics = CreateMetrics();
            AddSnapshots(failedMetrics, options, ("running", host.Snapshot()));
            return Failed(name, watch, "active pointer wait start diagnostic was not available", failedMetrics);
        }

        var activeWaitSnapshot = host.Snapshot();
        var activeWaitMoveEventCount = MoveEvents(runtimeInput).Count;
        var activeWaitTotalEventCount = runtimeInput.Events.Count;
        var activeWaitDragButtonUpCount = runtimeInput.Count("mouse", mappedDragButton, false);

        gate.IsAllowed = false;
        gate.ForegroundProcess = "Notepad.exe";
        var foregroundLossTicks = Stopwatch.GetTimestamp();
        var blockedGate = await host.CheckForegroundAsync(CancellationToken.None);
        if (blockedGate.IsAllowed)
        {
            var failedMetrics = CreateMetrics();
            failedMetrics["blocked_foreground_process"] = blockedGate.ForegroundProcess ?? string.Empty;
            AddSnapshots(failedMetrics, options, ("active_pointer_wait", activeWaitSnapshot));
            return Failed(name, watch, "manual foreground gate did not enter blocked state during active pointer wait", failedMetrics);
        }

        if (!await WaitUntilAsync(
                () => CountDiagnosticEvent(diagnostics, "macro", "foreground_loss_detected_during_wait") == 1 &&
                      CountDiagnosticEvent(diagnostics, "macro", "active_pointer_wait_interrupted") == 1 &&
                      CountDiagnosticEvent(diagnostics, "macro", "foreground_lost_during_active_pointer_sequence") == 1 &&
                      CountDiagnosticEvent(diagnostics, "macro", "foreground_cleanup_completed") == 1 &&
                      runtimeInput.Count("mouse", mappedDragButton, false) == activeWaitDragButtonUpCount + 1 &&
                      host.Snapshot().Runtime.Presses.OwnerKeys.Count == 0 &&
                      host.Snapshot().Runtime.PendingActionCount == 0 &&
                      host.Snapshot().Runtime.RunningActionCount == 0,
                options.DrainTimeout,
                CancellationToken.None))
        {
            var failedMetrics = CreateMetrics();
            AddSnapshots(failedMetrics, options, ("active_pointer_wait", activeWaitSnapshot), ("running", host.Snapshot()));
            return Failed(name, watch, "foreground-loss cleanup did not complete from active pointer wait", failedMetrics);
        }

        var cleanupSnapshot = host.Snapshot();
        var cleanupUp = runtimeInput.Events
            .Where(evt =>
                string.Equals(evt.Kind, "mouse", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(evt.Code, mappedDragButton, StringComparison.OrdinalIgnoreCase) &&
                evt.IsDown == false)
            .OrderBy(evt => evt.TimestampTicks)
            .Last();
        var waitStartToCleanupMs = ElapsedMilliseconds(waitStarted.TimestampTicks, cleanupUp.TimestampTicks);
        var foregroundLossToCleanupMs = ElapsedMilliseconds(foregroundLossTicks, cleanupUp.TimestampTicks);
        var foregroundLossWithinExpectedBound = foregroundLossToCleanupMs <= cleanupLatencyBoundMs;
        var dragButtonUpFromForegroundCleanupCount =
            runtimeInput.Count("mouse", mappedDragButton, false) - activeWaitDragButtonUpCount;
        var moveEventAfterForegroundLossCount = Math.Max(0, MoveEvents(runtimeInput).Count - activeWaitMoveEventCount);
        var unexpectedOutputAfterForegroundLossCount = Math.Max(
            0,
            (runtimeInput.Events.Count - activeWaitTotalEventCount) - dragButtonUpFromForegroundCleanupCount);
        var normalReleaseAfterForegroundLossCount = Math.Max(0, runtimeInput.Count("mouse", mappedDragButton, false) - 1);
        var cleanupBeforeFullWaitElapsed = waitStartToCleanupMs < waitMs;

        await host.StopAsync(CancellationToken.None);
        var stopped = host.Snapshot();
        var stoppedClean = IsCleanlyStopped(stopped);
        var duplicateDragButtonUpCount = Math.Max(0, runtimeInput.Count("mouse", mappedDragButton, false) - 1);
        var passed =
            CountDiagnosticEvent(diagnostics, "macro", "active_pointer_wait_started") == 1 &&
            CountDiagnosticEvent(diagnostics, "macro", "foreground_loss_detected_during_wait") == 1 &&
            CountDiagnosticEvent(diagnostics, "macro", "active_pointer_wait_interrupted") == 1 &&
            CountDiagnosticEvent(diagnostics, "macro", "foreground_lost_during_active_pointer_sequence") == 1 &&
            CountDiagnosticEvent(diagnostics, "macro", "foreground_cleanup_completed") == 1 &&
            CountDiagnosticEvent(diagnostics, "macro", "wait_completed") == 0 &&
            runtimeInput.Count("mouse", mappedDragButton, true) == 1 &&
            runtimeInput.Count("mouse", mappedDragButton, false) == 1 &&
            dragButtonUpFromForegroundCleanupCount == 1 &&
            duplicateDragButtonUpCount == 0 &&
            moveEventAfterForegroundLossCount == 0 &&
            unexpectedOutputAfterForegroundLossCount == 0 &&
            normalReleaseAfterForegroundLossCount == 0 &&
            cleanupSnapshot.Runtime.Presses.OwnerKeys.Count == 0 &&
            cleanupBeforeFullWaitElapsed &&
            foregroundLossWithinExpectedBound &&
            stoppedClean;

        var metrics = CreateMetrics();
        metrics["foreground_loss_detected_during_wait"] = true;
        metrics["foreground_loss_to_cleanup_ms"] = foregroundLossToCleanupMs;
        metrics["wait_start_to_cleanup_ms"] = waitStartToCleanupMs;
        metrics["foreground_loss_to_cleanup_within_expected_bound"] = foregroundLossWithinExpectedBound;
        metrics["cleanup_before_full_wait_elapsed"] = cleanupBeforeFullWaitElapsed;
        metrics["drag_button_up_from_foreground_cleanup_count"] = dragButtonUpFromForegroundCleanupCount;
        metrics["duplicate_drag_button_up_count"] = duplicateDragButtonUpCount;
        metrics["normal_release_after_foreground_loss_count"] = normalReleaseAfterForegroundLossCount;
        metrics["move_event_before_foreground_loss_count"] = activeWaitMoveEventCount;
        metrics["move_event_after_foreground_loss_count"] = moveEventAfterForegroundLossCount;
        metrics["unexpected_output_after_foreground_loss_count"] = unexpectedOutputAfterForegroundLossCount;
        metrics["held_owner_count_after_foreground_cleanup"] = cleanupSnapshot.Runtime.Presses.OwnerKeys.Count;
        metrics["foreground_loss_immediate_cleanup_proven"] = false;
        metrics["foreground_loss_cleanup_latency_measured"] = true;
        metrics["foreground_loss_cleanup_latency_note"] = "Measured only in deterministic dry harness; not a Blue Archive real-target latency SLO.";
        metrics["stopped_clean"] = stoppedClean;
        AddSnapshots(
            metrics,
            options,
            ("active_pointer_wait", activeWaitSnapshot),
            ("after_foreground_cleanup", cleanupSnapshot),
            ("stopped", stopped));

        return passed
            ? Passed(name, watch, metrics)
            : Failed(name, watch, "active pointer wait foreground-loss interruption did not satisfy cleanup invariants", metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message, CreateMetrics());
    }
}

static async Task<AcceptanceScenarioResult> RunBuiltInDragForegroundLossContractAsync(AcceptanceOptions options)
{
    const string name = "built-in-drag-foreground-loss-contract";
    const string mappedDragButton = "mouse_middle";
    const int pointerDelayMs = 1000;
    const double cleanupLatencyBoundMs = 250;
    var watch = Stopwatch.StartNew();
    var metrics = new Dictionary<string, object>
    {
        ["validation_scope"] = "phase2d-f-built-in-drag-foreground-loss-contract",
        ["foreground_cleanup_timing_model"] = "built_in_drag_helper_foreground_aware_pointer_delay",
        ["cleanup_latency_bound_scope"] = "deterministic_dry_harness_only",
        ["real_target_cleanup_latency_slo_proven"] = false,
        ["complete_drag_product_scenario_proven"] = false,
        ["mouse_left_right_physical_trigger_proven"] = false,
        ["pre_helper_down_foreground_drift_proven"] = false,
        ["inflight_move_interrupt_proven"] = false,
        ["pointer_delay_ms"] = pointerDelayMs,
        ["cleanup_latency_bound_ms"] = cleanupLatencyBoundMs
    };
    var builtinDragForegroundLossDetectedCount = 0;
    var builtinDragButtonDownCount = 0;
    var builtinDragButtonUpFromCleanupCount = 0;
    var builtinDragMoveEventAfterForegroundLossCount = 0;
    var builtinDragDuplicateUpCount = 0;
    var builtinDragHeldOwnerCountAfterCleanup = 0;
    var builtinDragCancelledByRuntimeCount = 0;
    var builtinDragCompletedNormallyCount = 0;

    RuntimeConfig Config() => new("BlueArchive.exe", 1, []);

    void AddMetricPrefix(string prefix, IReadOnlyDictionary<string, object> caseMetrics)
    {
        foreach (var (key, value) in caseMetrics)
        {
            metrics[$"{prefix}_{key}"] = value;
        }
    }

    async Task<(bool Passed, string? Error)> RunForegroundLossCaseAsync(
        string caseName,
        string helper,
        string script,
        string barrierPhase,
        int expectedMovesAtBarrier,
        int expectedFinalMoveCount)
    {
        var backend = new CountingInputBackend(new DryRunInputBackend());
        var presses = new PressOwnershipTracker(backend);
        var gate = new ManualForegroundGate
        {
            IsAllowed = true,
            ForegroundProcess = "BlueArchive.exe"
        };
        var diagnostics = new CountingDiagnosticsSink();
        var config = Config();
        var executor = new MacroExecutor(
            presses,
            backend,
            gate,
            config,
            diagnostics,
            pointerDelay: TimeSpan.FromMilliseconds(pointerDelayMs));
        var instructions = CompileAcceptanceMacro(script);
        var owner = $"macro:{caseName}";
        var execution = executor.ExecuteAsync(owner, instructions, CancellationToken.None).AsTask();

        if (!await WaitUntilAsync(
                () => CountMacroDiagnostic(diagnostics, "builtin_drag_pointer_delay_started", helper, barrierPhase) == 1 &&
                      CountMacroDiagnostic(diagnostics, "builtin_drag_started", helper) == 1 &&
                      backend.Count("mouse", mappedDragButton, true) == 1 &&
                      backend.Count("mouse", mappedDragButton, false) == 0 &&
                      MoveEvents(backend).Count == expectedMovesAtBarrier &&
                      presses.Snapshot().OwnerKeys.Count > 0,
                options.DrainTimeout,
                CancellationToken.None))
        {
            AddMetricPrefix(caseName, new Dictionary<string, object>
            {
                ["barrier_reached"] = false,
                ["builtin_drag_started_count"] = CountMacroDiagnostic(diagnostics, "builtin_drag_started", helper),
                ["builtin_drag_pointer_delay_started_count"] = CountMacroDiagnostic(diagnostics, "builtin_drag_pointer_delay_started", helper, barrierPhase),
                ["button_down_count"] = backend.Count("mouse", mappedDragButton, true),
                ["button_up_count"] = backend.Count("mouse", mappedDragButton, false),
                ["move_event_count"] = MoveEvents(backend).Count,
                ["held_owner_count"] = presses.Snapshot().OwnerKeys.Count
            });
            return (false, $"{caseName} did not reach deterministic built-in drag active pointer barrier");
        }

        var barrierMoveEventCount = MoveEvents(backend).Count;
        var barrierTotalEventCount = backend.Events.Count;
        var barrierButtonUpCount = backend.Count("mouse", mappedDragButton, false);
        var barrierSnapshot = presses.Snapshot();
        var delayStarted = FirstDiagnosticEvent(diagnostics, "macro", "builtin_drag_pointer_delay_started");

        gate.IsAllowed = false;
        gate.ForegroundProcess = "Notepad.exe";
        var foregroundLossTicks = Stopwatch.GetTimestamp();
        var blockedGate = await gate.CheckAsync(config, CancellationToken.None);
        if (blockedGate.IsAllowed)
        {
            AddMetricPrefix(caseName, new Dictionary<string, object>
            {
                ["barrier_reached"] = true,
                ["manual_gate_blocked"] = false,
                ["foreground_process"] = blockedGate.ForegroundProcess ?? string.Empty
            });
            return (false, $"{caseName} manual foreground gate did not enter blocked state");
        }

        if (!await WaitUntilAsync(
                () => CountMacroDiagnostic(diagnostics, "builtin_drag_foreground_loss_detected", helper, barrierPhase) == 1 &&
                      CountMacroDiagnostic(diagnostics, "builtin_drag_interrupted", helper, barrierPhase, "foreground_loss") == 1 &&
                      CountMacroDiagnostic(diagnostics, "builtin_drag_cleanup_completed", helper, reason: "foreground_loss") == 1 &&
                      backend.Count("mouse", mappedDragButton, false) == barrierButtonUpCount + 1 &&
                      presses.Snapshot().OwnerKeys.Count == 0,
                options.DrainTimeout,
                CancellationToken.None))
        {
            AddMetricPrefix(caseName, new Dictionary<string, object>
            {
                ["barrier_reached"] = true,
                ["manual_gate_blocked"] = true,
                ["foreground_loss_detected_count"] = CountMacroDiagnostic(diagnostics, "builtin_drag_foreground_loss_detected", helper, barrierPhase),
                ["builtin_drag_interrupted_count"] = CountMacroDiagnostic(diagnostics, "builtin_drag_interrupted", helper, barrierPhase, "foreground_loss"),
                ["helper_cleanup_completed_count"] = CountMacroDiagnostic(diagnostics, "builtin_drag_cleanup_completed", helper, reason: "foreground_loss"),
                ["button_up_count"] = backend.Count("mouse", mappedDragButton, false),
                ["held_owner_count"] = presses.Snapshot().OwnerKeys.Count
            });
            return (false, $"{caseName} foreground-loss cleanup did not complete from built-in helper");
        }

        await execution;

        var cleanupUp = backend.Events
            .Where(evt =>
                string.Equals(evt.Kind, "mouse", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(evt.Code, mappedDragButton, StringComparison.OrdinalIgnoreCase) &&
                evt.IsDown == false)
            .OrderBy(evt => evt.TimestampTicks)
            .Last();
        var cleanupLatencyMs = ElapsedMilliseconds(foregroundLossTicks, cleanupUp.TimestampTicks);
        var cleanupWithinBound = cleanupLatencyMs <= cleanupLatencyBoundMs;
        var moveEventAfterForegroundLossCount = Math.Max(0, MoveEvents(backend).Count - barrierMoveEventCount);
        var buttonUpFromCleanupCount = backend.Count("mouse", mappedDragButton, false) - barrierButtonUpCount;
        var duplicateUpCount = Math.Max(0, backend.Count("mouse", mappedDragButton, false) - 1);
        var unexpectedOutputAfterForegroundLossCount = Math.Max(
            0,
            (backend.Events.Count - barrierTotalEventCount) - buttonUpFromCleanupCount);
        var outerInstructionContinuedCount = Math.Max(0, MoveEvents(backend).Count - expectedFinalMoveCount);
        var cleanupSource = FirstMacroDiagnosticField(
            diagnostics,
            "builtin_drag_cleanup_completed",
            "cleanup_source",
            helper);
        var dragOwner = FirstMacroDiagnosticField(
            diagnostics,
            "builtin_drag_cleanup_completed",
            "drag_owner",
            helper);
        var helperCleanupCompletedCount = CountMacroDiagnostic(
            diagnostics,
            "builtin_drag_cleanup_completed",
            helper,
            reason: "foreground_loss");
        var foregroundLossDetectedCount = CountMacroDiagnostic(
            diagnostics,
            "builtin_drag_foreground_loss_detected",
            helper,
            barrierPhase);
        var interruptedCount = CountMacroDiagnostic(
            diagnostics,
            "builtin_drag_interrupted",
            helper,
            barrierPhase,
            "foreground_loss");
        var completedNormallyCount = CountMacroDiagnostic(diagnostics, "builtin_drag_completed", helper);
        var heldOwnerCountAfterCleanup = presses.Snapshot().OwnerKeys.Count;
        var passed =
            CountMacroDiagnostic(diagnostics, "builtin_drag_started", helper) == 1 &&
            CountMacroDiagnostic(diagnostics, "builtin_drag_pointer_delay_started", helper, barrierPhase) == 1 &&
            foregroundLossDetectedCount == 1 &&
            interruptedCount == 1 &&
            helperCleanupCompletedCount == 1 &&
            completedNormallyCount == 0 &&
            backend.Count("mouse", mappedDragButton, true) == 1 &&
            backend.Count("mouse", mappedDragButton, false) == 1 &&
            buttonUpFromCleanupCount == 1 &&
            duplicateUpCount == 0 &&
            MoveEvents(backend).Count == expectedFinalMoveCount &&
            moveEventAfterForegroundLossCount == 0 &&
            unexpectedOutputAfterForegroundLossCount == 0 &&
            outerInstructionContinuedCount == 0 &&
            string.Equals(cleanupSource, "helper_drag_owner", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(dragOwner, $"{owner}:drag:{mappedDragButton}", StringComparison.OrdinalIgnoreCase) &&
            barrierSnapshot.OwnerKeys.Count > 0 &&
            heldOwnerCountAfterCleanup == 0 &&
            cleanupWithinBound &&
            delayStarted is not null;

        AddMetricPrefix(caseName, new Dictionary<string, object>
        {
            ["barrier_reached"] = true,
            ["manual_gate_blocked"] = true,
            ["helper"] = helper,
            ["barrier_phase"] = barrierPhase,
            ["builtin_drag_started_count"] = CountMacroDiagnostic(diagnostics, "builtin_drag_started", helper),
            ["builtin_drag_pointer_delay_started_count"] = CountMacroDiagnostic(diagnostics, "builtin_drag_pointer_delay_started", helper, barrierPhase),
            ["builtin_drag_foreground_loss_detected_count"] = foregroundLossDetectedCount,
            ["builtin_drag_interrupted_foreground_loss_count"] = interruptedCount,
            ["helper_cleanup_completed_count"] = helperCleanupCompletedCount,
            ["helper_cleanup_source"] = cleanupSource ?? string.Empty,
            ["helper_drag_owner"] = dragOwner ?? string.Empty,
            ["button_down_count"] = backend.Count("mouse", mappedDragButton, true),
            ["button_up_count"] = backend.Count("mouse", mappedDragButton, false),
            ["button_up_from_helper_cleanup_count"] = buttonUpFromCleanupCount,
            ["duplicate_up_count"] = duplicateUpCount,
            ["move_event_before_foreground_loss_count"] = barrierMoveEventCount,
            ["move_event_after_foreground_loss_count"] = moveEventAfterForegroundLossCount,
            ["expected_final_move_count"] = expectedFinalMoveCount,
            ["outer_instruction_continued_after_foreground_loss_count"] = outerInstructionContinuedCount,
            ["unexpected_output_after_foreground_loss_count"] = unexpectedOutputAfterForegroundLossCount,
            ["held_owner_count_at_barrier"] = barrierSnapshot.OwnerKeys.Count,
            ["held_owner_count_after_cleanup"] = heldOwnerCountAfterCleanup,
            ["completed_normally_count"] = completedNormallyCount,
            ["foreground_loss_to_cleanup_ms"] = cleanupLatencyMs,
            ["foreground_loss_to_cleanup_within_expected_bound"] = cleanupWithinBound
        });

        builtinDragForegroundLossDetectedCount += foregroundLossDetectedCount;
        builtinDragButtonDownCount += backend.Count("mouse", mappedDragButton, true);
        builtinDragButtonUpFromCleanupCount += buttonUpFromCleanupCount;
        builtinDragMoveEventAfterForegroundLossCount += moveEventAfterForegroundLossCount;
        builtinDragDuplicateUpCount += duplicateUpCount;
        builtinDragHeldOwnerCountAfterCleanup = Math.Max(
            builtinDragHeldOwnerCountAfterCleanup,
            heldOwnerCountAfterCleanup);

        return passed
            ? (true, null)
            : (false, $"{caseName} did not satisfy built-in helper foreground-loss invariants");
    }

    async Task<(bool Passed, string? Error)> RunCancellationCaseAsync()
    {
        const string caseName = "drag_rel_cancellation";
        const string helper = "drag_rel";
        const string owner = "macro:drag-rel-cancellation";
        var backend = new CountingInputBackend(new DryRunInputBackend());
        var presses = new PressOwnershipTracker(backend);
        var gate = new ManualForegroundGate
        {
            IsAllowed = true,
            ForegroundProcess = "BlueArchive.exe"
        };
        var diagnostics = new CountingDiagnosticsSink();
        var config = Config();
        var executor = new MacroExecutor(
            presses,
            backend,
            gate,
            config,
            diagnostics,
            pointerDelay: TimeSpan.FromMilliseconds(pointerDelayMs));
        var instructions = CompileAcceptanceMacro("""
            drag_rel 10 5 middle
            setpos_rel 7 7
            """);
        using var cts = new CancellationTokenSource();
        var execution = executor.ExecuteAsync(owner, instructions, cts.Token).AsTask();

        if (!await WaitUntilAsync(
                () => CountMacroDiagnostic(diagnostics, "builtin_drag_pointer_delay_started", helper, "pre_move_delay") == 1 &&
                      backend.Count("mouse", mappedDragButton, true) == 1 &&
                      backend.Count("mouse", mappedDragButton, false) == 0 &&
                      presses.Snapshot().OwnerKeys.Count > 0,
                options.DrainTimeout,
                CancellationToken.None))
        {
            AddMetricPrefix(caseName, new Dictionary<string, object>
            {
                ["barrier_reached"] = false,
                ["button_down_count"] = backend.Count("mouse", mappedDragButton, true),
                ["button_up_count"] = backend.Count("mouse", mappedDragButton, false),
                ["held_owner_count"] = presses.Snapshot().OwnerKeys.Count
            });
            return (false, "drag_rel cancellation barrier was not reached");
        }

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

        var cleanupReasonCount = CountMacroDiagnostic(
            diagnostics,
            "builtin_drag_cleanup_completed",
            helper,
            reason: "cancellation");
        var foregroundLossCount = CountMacroDiagnostic(diagnostics, "builtin_drag_foreground_loss_detected", helper);
        var foregroundInterruptedCount = CountMacroDiagnostic(
            diagnostics,
            "builtin_drag_interrupted",
            helper,
            reason: "foreground_loss");
        var duplicateUpCount = Math.Max(0, backend.Count("mouse", mappedDragButton, false) - 1);
        var heldOwnerCountAfterCleanup = presses.Snapshot().OwnerKeys.Count;
        var passed =
            cancelled &&
            cleanupReasonCount == 1 &&
            foregroundLossCount == 0 &&
            foregroundInterruptedCount == 0 &&
            backend.Count("mouse", mappedDragButton, true) == 1 &&
            backend.Count("mouse", mappedDragButton, false) == 1 &&
            MoveEvents(backend).Count == 0 &&
            duplicateUpCount == 0 &&
            heldOwnerCountAfterCleanup == 0;

        AddMetricPrefix(caseName, new Dictionary<string, object>
        {
            ["barrier_reached"] = true,
            ["cancelled_by_runtime_count"] = cancelled ? 1 : 0,
            ["helper_cleanup_completed_cancellation_count"] = cleanupReasonCount,
            ["builtin_drag_foreground_loss_detected_count"] = foregroundLossCount,
            ["builtin_drag_interrupted_foreground_loss_count"] = foregroundInterruptedCount,
            ["button_down_count"] = backend.Count("mouse", mappedDragButton, true),
            ["button_up_count"] = backend.Count("mouse", mappedDragButton, false),
            ["move_event_count"] = MoveEvents(backend).Count,
            ["duplicate_up_count"] = duplicateUpCount,
            ["held_owner_count_after_cleanup"] = heldOwnerCountAfterCleanup,
            ["helper_cleanup_source"] = FirstMacroDiagnosticField(
                diagnostics,
                "builtin_drag_cleanup_completed",
                "cleanup_source",
                helper) ?? string.Empty
        });

        builtinDragButtonDownCount += backend.Count("mouse", mappedDragButton, true);
        builtinDragDuplicateUpCount += duplicateUpCount;
        builtinDragHeldOwnerCountAfterCleanup = Math.Max(
            builtinDragHeldOwnerCountAfterCleanup,
            heldOwnerCountAfterCleanup);
        builtinDragCancelledByRuntimeCount += cancelled ? 1 : 0;

        return passed ? (true, null) : (false, "drag_rel cancellation was confused with foreground loss");
    }

    async Task<(bool Passed, string? Error)> RunNormalCompletionCaseAsync()
    {
        const string caseName = "normal_completion";
        var backend = new CountingInputBackend(new DryRunInputBackend());
        var presses = new PressOwnershipTracker(backend);
        var gate = new ManualForegroundGate
        {
            IsAllowed = true,
            ForegroundProcess = "BlueArchive.exe"
        };
        var diagnostics = new CountingDiagnosticsSink();
        var executor = new MacroExecutor(
            presses,
            backend,
            gate,
            Config(),
            diagnostics,
            pointerDelay: TimeSpan.Zero);
        var instructions = CompileAcceptanceMacro("""
            drag 100 200 middle
            drag_rel 10 5 right
            """);

        await executor.ExecuteAsync("macro:normal-built-in-drag", instructions, CancellationToken.None);

        var completedCount =
            CountMacroDiagnostic(diagnostics, "builtin_drag_completed", "drag") +
            CountMacroDiagnostic(diagnostics, "builtin_drag_completed", "drag_rel");
        var foregroundLossCount = CountMacroDiagnostic(diagnostics, "builtin_drag_foreground_loss_detected");
        var duplicateMiddleUpCount = Math.Max(0, backend.Count("mouse", "mouse_middle", false) - 1);
        var duplicateRightUpCount = Math.Max(0, backend.Count("mouse", "mouse_right", false) - 1);
        var duplicateUpCount = duplicateMiddleUpCount + duplicateRightUpCount;
        var heldOwnerCountAfterCleanup = presses.Snapshot().OwnerKeys.Count;
        var passed =
            completedCount == 2 &&
            foregroundLossCount == 0 &&
            backend.Count("mouse", "mouse_middle", true) == 1 &&
            backend.Count("mouse", "mouse_middle", false) == 1 &&
            backend.Count("mouse", "mouse_right", true) == 1 &&
            backend.Count("mouse", "mouse_right", false) == 1 &&
            MoveEvents(backend).Count == 2 &&
            duplicateUpCount == 0 &&
            heldOwnerCountAfterCleanup == 0;

        AddMetricPrefix(caseName, new Dictionary<string, object>
        {
            ["builtin_drag_completed_normally_count"] = completedCount,
            ["builtin_drag_foreground_loss_detected_count"] = foregroundLossCount,
            ["mouse_middle_button_down_count"] = backend.Count("mouse", "mouse_middle", true),
            ["mouse_middle_button_up_count"] = backend.Count("mouse", "mouse_middle", false),
            ["mouse_right_button_down_count"] = backend.Count("mouse", "mouse_right", true),
            ["mouse_right_button_up_count"] = backend.Count("mouse", "mouse_right", false),
            ["move_event_count"] = MoveEvents(backend).Count,
            ["duplicate_up_count"] = duplicateUpCount,
            ["held_owner_count_after_cleanup"] = heldOwnerCountAfterCleanup
        });

        builtinDragButtonDownCount +=
            backend.Count("mouse", "mouse_middle", true) +
            backend.Count("mouse", "mouse_right", true);
        builtinDragDuplicateUpCount += duplicateUpCount;
        builtinDragHeldOwnerCountAfterCleanup = Math.Max(
            builtinDragHeldOwnerCountAfterCleanup,
            heldOwnerCountAfterCleanup);
        builtinDragCompletedNormallyCount += completedCount;

        return passed ? (true, null) : (false, "normal built-in drag helpers did not complete cleanly");
    }

    try
    {
        var cases = new[]
        {
            await RunForegroundLossCaseAsync(
                "drag_pre_move",
                "drag",
                """
                drag 100 200 middle
                setpos_rel 7 7
                """,
                "pre_move_delay",
                expectedMovesAtBarrier: 0,
                expectedFinalMoveCount: 0),
            await RunForegroundLossCaseAsync(
                "drag_rel_pre_move",
                "drag_rel",
                """
                drag_rel 10 5 middle
                setpos_rel 7 7
                """,
                "pre_move_delay",
                expectedMovesAtBarrier: 0,
                expectedFinalMoveCount: 0),
            await RunForegroundLossCaseAsync(
                "drag_rel_post_move",
                "drag_rel",
                """
                drag_rel 10 5 middle
                setpos_rel 7 7
                """,
                "post_move_delay",
                expectedMovesAtBarrier: 1,
                expectedFinalMoveCount: 1),
            await RunCancellationCaseAsync(),
            await RunNormalCompletionCaseAsync()
        };

        metrics["builtin_drag_foreground_loss_detected_count"] = builtinDragForegroundLossDetectedCount;
        metrics["builtin_drag_button_down_count"] = builtinDragButtonDownCount;
        metrics["builtin_drag_button_up_from_cleanup_count"] = builtinDragButtonUpFromCleanupCount;
        metrics["builtin_drag_move_event_after_foreground_loss_count"] = builtinDragMoveEventAfterForegroundLossCount;
        metrics["builtin_drag_duplicate_up_count"] = builtinDragDuplicateUpCount;
        metrics["builtin_drag_held_owner_count_after_cleanup"] = builtinDragHeldOwnerCountAfterCleanup;
        metrics["builtin_drag_cancelled_by_runtime_count"] = builtinDragCancelledByRuntimeCount;
        metrics["builtin_drag_completed_normally_count"] = builtinDragCompletedNormallyCount;
        metrics["foreground_loss_cases_covered"] = 3;
        metrics["pre_move_loss_cases_covered"] = 2;
        metrics["post_move_loss_cases_covered"] = 1;
        metrics["outer_macro_post_loss_instruction_suppressed"] = true;
        metrics["helper_cleanup_distinguished_from_outer_owner_cleanup"] = true;

        var allPassed = cases.All(result => result.Passed);
        var failed = cases.FirstOrDefault(result => !result.Passed);
        return allPassed
            ? Passed(name, watch, metrics)
            : Failed(name, watch, failed.Error ?? "built-in drag foreground-loss contract failed", metrics);
    }
    catch (Exception ex)
    {
        metrics["exception_type"] = ex.GetType().Name;
        return Failed(name, watch, ex.Message, metrics);
    }
}

static Task<AcceptanceScenarioResult> RunXButton2TriggeredCompleteDragNormalCompletionAsync(
    AcceptanceOptions options)
{
    return RunXButton2TriggeredExplicitDragNormalCompletionAsync(
        options,
        "xbutton2-triggered-complete-drag-normal-completion",
        "phase2e-first-batch-complete-drag-normal-completion",
        [(24, 12)]);
}

static Task<AcceptanceScenarioResult> RunXButton2TriggeredMultisegmentDragNormalCompletionAsync(
    AcceptanceOptions options)
{
    return RunXButton2TriggeredExplicitDragNormalCompletionAsync(
        options,
        "xbutton2-triggered-multisegment-drag-normal-completion",
        "phase2e-first-batch-multisegment-drag-normal-completion",
        [(12, 6), (8, 4), (4, 2)]);
}

static async Task<AcceptanceScenarioResult> RunXButton2TriggeredExplicitDragNormalCompletionAsync(
    AcceptanceOptions options,
    string name,
    string validationScope,
    (int Dx, int Dy)[] segments)
{
    const string mappingId = "phase2e-xbutton2-complete-drag-normal";
    const string trigger = "mouse_x2";
    const string triggerKey = "mouse:mouse_x2";
    const string mappedDragButton = "mouse_middle";
    const int postCompletionProbeMs = 100;
    var watch = Stopwatch.StartNew();
    var expectedMoveDeltaX = segments.Sum(segment => segment.Dx);
    var expectedMoveDeltaY = segments.Sum(segment => segment.Dy);
    var dryRun = new DryRunInputBackend();
    var runtimeInput = new CountingInputBackend(dryRun);
    var source = new ManualTriggerSource();
    var diagnostics = new CountingDiagnosticsSink();
    await using var host = new RuntimeHost(
        LiveMacroConfig(
            "BlueArchive.exe",
            mappingId,
            trigger,
            ExplicitCompleteDragScript(segments)),
        runtimeInput,
        AlwaysForegroundGate.Instance,
        source,
        diagnostics);
    (int X, int Y) _cursorStart = default;
    (int X, int Y) _cursorEnd = default;

    Dictionary<string, object> CreateMetrics(
        RuntimeHostSnapshot? completionSnapshot = null,
        RuntimeHostSnapshot? stoppedSnapshot = null,
        int unexpectedOutputAfterCompletionCount = 0,
        int postStopOutputCount = 0,
        bool cursorRestored = false)
    {
        var snapshot = completionSnapshot ?? host.Snapshot();
        var moveEvents = MoveEvents(runtimeInput);
        var cursorStart = _cursorStart;
        var cursorEnd = _cursorEnd;
        var actualMoveDelta = TotalMoveDelta(moveEvents, cursorStart.X, cursorStart.Y);
        var triggerDownReceivedCount = CountRuntimeDispatched(diagnostics, triggerKey, "down");
        var triggerUpReceivedCount = CountRuntimeDispatched(diagnostics, triggerKey, "up");
        var pipelineDownEnqueuedCount = CountPipelineQueued(diagnostics, triggerKey, "down");
        var pipelineUpEnqueuedCount = CountPipelineQueued(diagnostics, triggerKey, "up");
        var macroStartedCount = CountMacroEvent(diagnostics, "started", $"{mappingId}:macro:");
        var macroFinishedCount = CountMacroEvent(diagnostics, "finished", $"{mappingId}:macro:");
        var dragButtonDownCount = runtimeInput.Count("mouse", mappedDragButton, true);
        var dragButtonUpCount = runtimeInput.Count("mouse", mappedDragButton, false);
        var duplicateDragButtonUpCount = Math.Max(0, dragButtonUpCount - 1);
        var macroCompletedNormally =
            macroStartedCount == 1 &&
            macroFinishedCount == 1 &&
            dragButtonDownCount == 1 &&
            dragButtonUpCount == 1 &&
            moveEvents.Count == segments.Length &&
            actualMoveDelta.Dx == expectedMoveDeltaX &&
            actualMoveDelta.Dy == expectedMoveDeltaY &&
            duplicateDragButtonUpCount == 0 &&
            snapshot.Runtime.Presses.OwnerKeys.Count == 0;
        var stoppedClean = stoppedSnapshot is not null && IsCleanlyStopped(stoppedSnapshot);

        return new Dictionary<string, object>
        {
            ["validation_scope"] = validationScope,
            ["scenario_scope"] = "phase2e_first_batch_dry_harness_normal_completion_only",
            ["real_target_complete_drag_slo_proven"] = false,
            ["mouse_left_right_physical_trigger_proven"] = false,
            ["interruption_reload_foreground_loss_product_reproof"] = false,
            ["trigger"] = trigger,
            ["mapped_drag_button"] = mappedDragButton,
            ["trigger_received_count"] = triggerDownReceivedCount,
            ["trigger_up_received_count"] = triggerUpReceivedCount,
            ["trigger_received_total_count"] = triggerDownReceivedCount + triggerUpReceivedCount,
            ["pipeline_enqueued_count"] = pipelineDownEnqueuedCount,
            ["pipeline_up_enqueued_count"] = pipelineUpEnqueuedCount,
            ["pipeline_enqueued_total_count"] = pipelineDownEnqueuedCount + pipelineUpEnqueuedCount,
            ["runtime_dispatched_count"] = triggerDownReceivedCount,
            ["runtime_up_dispatched_count"] = triggerUpReceivedCount,
            ["runtime_dispatched_total_count"] = triggerDownReceivedCount + triggerUpReceivedCount,
            ["macro_started_count"] = macroStartedCount,
            ["macro_finished_count"] = macroFinishedCount,
            ["drag_button_down_count"] = dragButtonDownCount,
            ["drag_button_up_count"] = dragButtonUpCount,
            ["move_segment_count"] = moveEvents.Count,
            ["expected_move_segment_count"] = segments.Length,
            ["expected_move_delta_x"] = expectedMoveDeltaX,
            ["expected_move_delta_y"] = expectedMoveDeltaY,
            ["actual_move_delta_x"] = actualMoveDelta.Dx,
            ["actual_move_delta_y"] = actualMoveDelta.Dy,
            ["duplicate_drag_button_up_count"] = duplicateDragButtonUpCount,
            ["unexpected_output_after_completion_count"] = unexpectedOutputAfterCompletionCount,
            ["post_stop_output_count"] = postStopOutputCount,
            ["held_owner_count_after_completion"] = snapshot.Runtime.Presses.OwnerKeys.Count,
            ["macro_completed_normally"] = macroCompletedNormally,
            ["stopped_clean"] = stoppedClean,
            ["post_completion_probe_ms"] = postCompletionProbeMs,
            ["cursor_start_x"] = cursorStart.X,
            ["cursor_start_y"] = cursorStart.Y,
            ["cursor_end_x"] = cursorEnd.X,
            ["cursor_end_y"] = cursorEnd.Y,
            ["cursor_restored"] = cursorRestored,
            ["cursor_restore_scope"] = "dry_backend_only",
            ["move_segments"] = string.Join(";", segments.Select(segment => $"{segment.Dx},{segment.Dy}")),
            ["stable_supplement_latest_json_generated"] = false
        };
    }

    try
    {
        await host.StartAsync(CancellationToken.None);
        _cursorStart = await runtimeInput.GetMousePositionAsync(CancellationToken.None);
        source.MouseDown(trigger);

        if (!await WaitUntilAsync(
                () => runtimeInput.Count("mouse", mappedDragButton, true) == 1 &&
                      runtimeInput.Count("mouse", mappedDragButton, false) == 1 &&
                      MoveEvents(runtimeInput).Count == segments.Length &&
                      CountMacroEvent(diagnostics, "finished", $"{mappingId}:macro:") == 1 &&
                      host.Snapshot().Runtime.PendingActionCount == 0 &&
                      host.Snapshot().Runtime.RunningActionCount == 0,
                options.DrainTimeout,
                CancellationToken.None))
        {
            var failedMetrics = CreateMetrics();
            AddSnapshots(failedMetrics, options, ("running", host.Snapshot()));
            return Failed(name, watch, "complete drag normal-completion output did not reach expected dry harness barrier", failedMetrics);
        }

        source.MouseUp(trigger);
        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            var failedMetrics = CreateMetrics();
            AddSnapshots(failedMetrics, options, ("running", host.Snapshot()));
            return Failed(name, watch, "queues did not drain after complete drag trigger release", failedMetrics);
        }

        var completionSnapshot = host.Snapshot();
        _cursorEnd = await runtimeInput.GetMousePositionAsync(CancellationToken.None);
        var eventsAtCompletion = runtimeInput.Events.Count;
        await Task.Delay(postCompletionProbeMs, CancellationToken.None);
        var unexpectedOutputAfterCompletionCount = Math.Max(0, runtimeInput.Events.Count - eventsAtCompletion);

        var eventsBeforeStop = runtimeInput.Events.Count;
        await host.StopAsync(CancellationToken.None);
        var stopped = host.Snapshot();
        await Task.Delay(20, CancellationToken.None);
        var postStopOutputCount = Math.Max(0, runtimeInput.Events.Count - eventsBeforeStop);
        var cursorRestored = await TryRestoreCursorAsync(dryRun, _cursorStart.X, _cursorStart.Y);

        var metrics = CreateMetrics(
            completionSnapshot,
            stopped,
            unexpectedOutputAfterCompletionCount,
            postStopOutputCount,
            cursorRestored);
        AddSnapshots(metrics, options, ("after_completion", completionSnapshot), ("stopped", stopped));

        var passed =
            (int)metrics["trigger_received_count"] == 1 &&
            (int)metrics["pipeline_enqueued_count"] == 1 &&
            (int)metrics["runtime_dispatched_count"] == 1 &&
            (int)metrics["macro_started_count"] == 1 &&
            (int)metrics["macro_finished_count"] == 1 &&
            (int)metrics["drag_button_down_count"] == 1 &&
            (int)metrics["drag_button_up_count"] == 1 &&
            (int)metrics["move_segment_count"] == segments.Length &&
            (int)metrics["expected_move_delta_x"] == (int)metrics["actual_move_delta_x"] &&
            (int)metrics["expected_move_delta_y"] == (int)metrics["actual_move_delta_y"] &&
            (int)metrics["duplicate_drag_button_up_count"] == 0 &&
            (int)metrics["unexpected_output_after_completion_count"] == 0 &&
            (int)metrics["post_stop_output_count"] == 0 &&
            (int)metrics["held_owner_count_after_completion"] == 0 &&
            (bool)metrics["macro_completed_normally"] &&
            (bool)metrics["stopped_clean"];

        return passed
            ? Passed(name, watch, metrics)
            : Failed(name, watch, "complete drag normal-completion invariants were not satisfied", metrics);
    }
    catch (Exception ex)
    {
        var metrics = CreateMetrics();
        metrics["exception_type"] = ex.GetType().Name;
        return Failed(name, watch, ex.Message, metrics);
    }

    static string ExplicitCompleteDragScript((int Dx, int Dy)[] segments)
    {
        var lines = new List<string> { "press mouse_middle" };
        foreach (var (dx, dy) in segments)
        {
            lines.Add($"setpos_rel {dx.ToString(CultureInfo.InvariantCulture)} {dy.ToString(CultureInfo.InvariantCulture)}");
            lines.Add("wait 5");
        }

        lines.Add("release mouse_middle");
        return string.Join(Environment.NewLine, lines);
    }
}

static async Task<AcceptanceScenarioResult> RunDryRunSoakAsync(AcceptanceOptions options)
{
    const string name = "dry-run-soak";
    var watch = Stopwatch.StartNew();
    var process = Process.GetCurrentProcess();
    var dryRun = new DryRunInputBackend();
    var source = new ManualTriggerSource();
    var diagnostics = new CountingDiagnosticsSink();
    var peaks = new Dictionary<string, long>
    {
        ["pipeline_pending_max"] = 0,
        ["runtime_pending_actions_max"] = 0,
        ["runtime_running_actions_max"] = 0,
        ["active_workers_max"] = 0,
        ["held_key_count_max"] = 0,
        ["press_owner_count_max"] = 0
    };

    await using var host = new RuntimeHost(
        TapConfig("q", "1", tapHoldMs: 5),
        dryRun,
        AlwaysForegroundGate.Instance,
        source,
        diagnostics);

    await host.StartAsync(CancellationToken.None);
    var startResources = CaptureResourceSample(process);
    var emitWatch = Stopwatch.StartNew();
    var emitted = 0;
    var intervalTicks = Stopwatch.Frequency / options.SoakRateHz;
    var nextDueTicks = (double)Stopwatch.GetTimestamp();
    while (emitWatch.Elapsed < options.SoakDuration)
    {
        source.KeyDown("q");
        emitted++;
        TrackPeak(host.Snapshot(), peaks);
        await Task.Yield();
        TrackPeak(host.Snapshot(), peaks);
        nextDueTicks += intervalTicks;
        var remainingTicks = nextDueTicks - Stopwatch.GetTimestamp();
        if (remainingTicks > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(remainingTicks / Stopwatch.Frequency), CancellationToken.None);
        }
        else
        {
            await Task.Yield();
        }
    }

    emitWatch.Stop();
    TrackPeak(host.Snapshot(), peaks);
    if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
    {
        return Failed(name, watch, "queues did not drain after dry-run soak");
    }

    var expectedEvents = emitted * 2;
    if (dryRun.Events.Count != expectedEvents)
    {
        return Failed(
            name,
            watch,
            $"input event count mismatch after soak: expected={expectedEvents} actual={dryRun.Events.Count}");
    }

    var running = host.Snapshot();
    TrackPeak(running, peaks);
    var eventsBeforeStop = dryRun.Events.Count;
    var stopWatch = Stopwatch.StartNew();
    await host.StopAsync(CancellationToken.None);
    stopWatch.Stop();
    var stopped = host.Snapshot();
    await Task.Delay(100);

    if (dryRun.Events.Count != eventsBeforeStop)
    {
        return Failed(name, watch, "input was emitted after stop");
    }

    if (!IsCleanlyStopped(stopped))
    {
        return Failed(name, watch, "stopped runtime is not clean after dry-run soak");
    }

    var endResources = CaptureResourceSample(process);
    var metrics = new Dictionary<string, object>
    {
        ["soak_seconds"] = Math.Round(options.SoakDuration.TotalSeconds, 3),
        ["soak_rate_hz"] = Math.Round(options.SoakRateHz, 3),
        ["observed_trigger_rate_hz"] = Math.Round(emitted / Math.Max(emitWatch.Elapsed.TotalSeconds, 0.001), 3),
        ["triggers_emitted"] = emitted,
        ["expected_input_events"] = expectedEvents,
        ["input_events"] = dryRun.Events.Count,
        ["diagnostic_events"] = diagnostics.Count,
        ["pipeline_queued"] = running.Pipeline.QueuedCount,
        ["pipeline_handled"] = running.Pipeline.HandledCount,
        ["pipeline_pending_after_drain"] = running.Pipeline.PendingCount,
        ["runtime_pending_actions_after_drain"] = running.Runtime.PendingActionCount,
        ["runtime_running_actions_after_drain"] = running.Runtime.RunningActionCount,
        ["held_key_count_after_drain"] = running.Runtime.Presses.KeyOwners.Count,
        ["press_owner_count_after_drain"] = running.Runtime.Presses.OwnerKeys.Count,
        ["stop_ms"] = stopWatch.Elapsed.TotalMilliseconds,
        ["post_stop_events"] = dryRun.Events.Count - eventsBeforeStop,
        ["stopped_clean"] = IsCleanlyStopped(stopped)
    };

    foreach (var (key, value) in peaks)
    {
        metrics[key] = value;
    }

    AddResourceMetrics(metrics, startResources, endResources, watch.Elapsed);
    AddSnapshots(metrics, options, ("running", running), ("stopped", stopped));
    return Passed(name, watch, metrics);
}

static async Task<AcceptanceScenarioResult> RunLifecycleStartStopLoopAsync(AcceptanceOptions options)
{
    const string name = "lifecycle-start-stop-loop";
    var watch = Stopwatch.StartNew();
    var process = Process.GetCurrentProcess();
    var startResources = CaptureResourceSample(process);
    var dryRun = new DryRunInputBackend();
    var source = new ManualTriggerSource();
    var diagnostics = new CountingDiagnosticsSink();
    await using var host = new RuntimeHost(
        TapConfig("q", "1", tapHoldMs: 1),
        dryRun,
        AlwaysForegroundGate.Instance,
        source,
        diagnostics);

    var stopMsTotal = 0.0;
    RuntimeHostSnapshot? lastStopped = null;
    for (var index = 0; index < options.BurstCount; index++)
    {
        await host.StartAsync(CancellationToken.None);
        source.KeyDown("q");
        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            return Failed(name, watch, $"queues did not drain before stop at loop {index}");
        }

        var stopWatch = Stopwatch.StartNew();
        await host.StopAsync(CancellationToken.None);
        stopWatch.Stop();
        stopMsTotal += stopWatch.Elapsed.TotalMilliseconds;
        lastStopped = host.Snapshot();
        if (!IsCleanlyStopped(lastStopped))
        {
            return Failed(name, watch, $"runtime did not stop cleanly at loop {index}");
        }

        var eventsBeforeStoppedProbe = dryRun.Events.Count;
        source.KeyDown("q");
        await Task.Delay(1);
        if (dryRun.Events.Count != eventsBeforeStoppedProbe)
        {
            return Failed(name, watch, $"stopped source emitted input at loop {index}");
        }
    }

    var expectedEvents = options.BurstCount * 2;
    if (dryRun.Events.Count != expectedEvents)
    {
        return Failed(
            name,
            watch,
            $"input event count mismatch: expected={expectedEvents} actual={dryRun.Events.Count}");
    }

    var endResources = CaptureResourceSample(process);
    var metrics = new Dictionary<string, object>
    {
        ["loops"] = options.BurstCount,
        ["input_events"] = dryRun.Events.Count,
        ["expected_input_events"] = expectedEvents,
        ["pipeline_queued"] = host.Snapshot().Pipeline.QueuedCount,
        ["pipeline_handled"] = host.Snapshot().Pipeline.HandledCount,
        ["pipeline_pending_after_stop"] = host.Snapshot().Pipeline.PendingCount,
        ["held_key_count_after_stop"] = lastStopped?.Runtime.Presses.KeyOwners.Count ?? -1,
        ["press_owner_count_after_stop"] = lastStopped?.Runtime.Presses.OwnerKeys.Count ?? -1,
        ["stop_ms_total"] = stopMsTotal,
        ["stop_ms_avg"] = stopMsTotal / Math.Max(options.BurstCount, 1),
        ["post_stop_events"] = 0,
        ["diagnostic_events"] = diagnostics.Count,
        ["stopped_clean"] = lastStopped is not null && IsCleanlyStopped(lastStopped)
    };
    AddResourceMetrics(metrics, startResources, endResources, watch.Elapsed);
    if (lastStopped is not null)
    {
        AddSnapshots(metrics, options, ("stopped", lastStopped));
    }

    return Passed(name, watch, metrics);
}

static async Task<AcceptanceScenarioResult> RunLifecycleReloadLoopAsync(AcceptanceOptions options)
{
    const string name = "lifecycle-reload-loop";
    var watch = Stopwatch.StartNew();
    var process = Process.GetCurrentProcess();
    var startResources = CaptureResourceSample(process);
    var dryRun = new DryRunInputBackend();
    var source = new ManualTriggerSource();
    var diagnostics = new CountingDiagnosticsSink();
    await using var host = new RuntimeHost(
        TapConfig("q", "1", tapHoldMs: 1),
        dryRun,
        AlwaysForegroundGate.Instance,
        source,
        diagnostics);

    await host.StartAsync(CancellationToken.None);
    for (var index = 0; index < options.BurstCount; index++)
    {
        var trigger = index % 2 == 0 ? "q" : "w";
        var staleTrigger = trigger == "q" ? "w" : "q";
        var target = index % 2 == 0 ? "1" : "2";
        await host.ReloadAsync(TapConfig(trigger, target, tapHoldMs: 1), CancellationToken.None);

        var beforeStaleProbe = dryRun.Events.Count;
        source.KeyDown(staleTrigger);
        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            return Failed(name, watch, $"queues did not drain after stale trigger probe at loop {index}");
        }

        if (dryRun.Events.Count != beforeStaleProbe)
        {
            return Failed(name, watch, $"stale trigger produced input at loop {index}");
        }

        var beforeActiveProbe = dryRun.Count("key", target);
        source.KeyDown(trigger);
        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            return Failed(name, watch, $"queues did not drain after active trigger at loop {index}");
        }

        if (dryRun.Count("key", target) - beforeActiveProbe != 2)
        {
            return Failed(name, watch, $"active trigger did not produce one complete tap at loop {index}");
        }
    }

    var running = host.Snapshot();
    var eventsBeforeStop = dryRun.Events.Count;
    await host.StopAsync(CancellationToken.None);
    var stopped = host.Snapshot();
    await Task.Delay(20);
    if (dryRun.Events.Count != eventsBeforeStop)
    {
        return Failed(name, watch, "input was emitted after reload loop stop");
    }

    if (!IsCleanlyStopped(stopped))
    {
        return Failed(name, watch, "runtime did not stop cleanly after reload loop");
    }

    var expectedEvents = options.BurstCount * 2;
    if (dryRun.Events.Count != expectedEvents)
    {
        return Failed(
            name,
            watch,
            $"input event count mismatch: expected={expectedEvents} actual={dryRun.Events.Count}");
    }

    var endResources = CaptureResourceSample(process);
    var metrics = new Dictionary<string, object>
    {
        ["loops"] = options.BurstCount,
        ["input_events"] = dryRun.Events.Count,
        ["expected_input_events"] = expectedEvents,
        ["key_1_events"] = dryRun.Count("key", "1"),
        ["key_2_events"] = dryRun.Count("key", "2"),
        ["generation"] = running.Runtime.Generation,
        ["pipeline_queued"] = running.Pipeline.QueuedCount,
        ["pipeline_handled"] = running.Pipeline.HandledCount,
        ["pipeline_pending_after_drain"] = running.Pipeline.PendingCount,
        ["runtime_pending_actions_after_drain"] = running.Runtime.PendingActionCount,
        ["runtime_running_actions_after_drain"] = running.Runtime.RunningActionCount,
        ["held_key_count_after_drain"] = running.Runtime.Presses.KeyOwners.Count,
        ["press_owner_count_after_drain"] = running.Runtime.Presses.OwnerKeys.Count,
        ["post_stop_events"] = dryRun.Events.Count - eventsBeforeStop,
        ["diagnostic_events"] = diagnostics.Count,
        ["stopped_clean"] = IsCleanlyStopped(stopped)
    };
    AddResourceMetrics(metrics, startResources, endResources, watch.Elapsed);
    AddSnapshots(metrics, options, ("running", running), ("stopped", stopped));
    return Passed(name, watch, metrics);
}

static async Task<AcceptanceScenarioResult> RunLifecycleEnableDisableLoopAsync(AcceptanceOptions options)
{
    const string name = "lifecycle-enable-disable-loop";
    var watch = Stopwatch.StartNew();
    var process = Process.GetCurrentProcess();
    var startResources = CaptureResourceSample(process);
    var dryRun = new DryRunInputBackend();
    var source = new ManualTriggerSource();
    var diagnostics = new CountingDiagnosticsSink();
    await using var host = new RuntimeHost(
        TapConfig("q", "1", tapHoldMs: 1),
        dryRun,
        AlwaysForegroundGate.Instance,
        source,
        diagnostics);

    await host.StartAsync(CancellationToken.None);
    for (var index = 0; index < options.BurstCount; index++)
    {
        await host.DisableAsync(CancellationToken.None);
        var beforeDisabledProbe = dryRun.Events.Count;
        source.KeyDown("q");
        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            return Failed(name, watch, $"queues did not drain after disabled trigger at loop {index}");
        }

        if (dryRun.Events.Count != beforeDisabledProbe)
        {
            return Failed(name, watch, $"disabled runtime emitted input at loop {index}");
        }

        await host.EnableRuntimeAsync(CancellationToken.None);
        source.KeyDown("q");
        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            return Failed(name, watch, $"queues did not drain after re-enabled trigger at loop {index}");
        }

        if (dryRun.Events.Count - beforeDisabledProbe != 2)
        {
            return Failed(name, watch, $"re-enabled runtime did not emit one complete tap at loop {index}");
        }
    }

    var running = host.Snapshot();
    var eventsBeforeStop = dryRun.Events.Count;
    await host.StopAsync(CancellationToken.None);
    var stopped = host.Snapshot();
    await Task.Delay(20);
    if (dryRun.Events.Count != eventsBeforeStop)
    {
        return Failed(name, watch, "input was emitted after enable/disable loop stop");
    }

    if (!IsCleanlyStopped(stopped))
    {
        return Failed(name, watch, "runtime did not stop cleanly after enable/disable loop");
    }

    var expectedEvents = options.BurstCount * 2;
    var endResources = CaptureResourceSample(process);
    var metrics = new Dictionary<string, object>
    {
        ["loops"] = options.BurstCount,
        ["disabled_trigger_probes"] = options.BurstCount,
        ["enabled_trigger_probes"] = options.BurstCount,
        ["input_events"] = dryRun.Events.Count,
        ["expected_input_events"] = expectedEvents,
        ["pipeline_queued"] = running.Pipeline.QueuedCount,
        ["pipeline_handled"] = running.Pipeline.HandledCount,
        ["pipeline_pending_after_drain"] = running.Pipeline.PendingCount,
        ["runtime_pending_actions_after_drain"] = running.Runtime.PendingActionCount,
        ["runtime_running_actions_after_drain"] = running.Runtime.RunningActionCount,
        ["held_key_count_after_drain"] = running.Runtime.Presses.KeyOwners.Count,
        ["press_owner_count_after_drain"] = running.Runtime.Presses.OwnerKeys.Count,
        ["post_stop_events"] = dryRun.Events.Count - eventsBeforeStop,
        ["diagnostic_events"] = diagnostics.Count,
        ["stopped_clean"] = IsCleanlyStopped(stopped)
    };
    AddResourceMetrics(metrics, startResources, endResources, watch.Elapsed);
    AddSnapshots(metrics, options, ("running", running), ("stopped", stopped));
    return Passed(name, watch, metrics);
}

static async Task<AcceptanceScenarioResult> RunLifecycleBurstReloadStopInterleaveAsync(AcceptanceOptions options)
{
    const string name = "lifecycle-burst-reload-stop-interleave";
    var watch = Stopwatch.StartNew();
    var process = Process.GetCurrentProcess();
    var startResources = CaptureResourceSample(process);
    var dryRun = new DryRunInputBackend();
    var source = new ManualTriggerSource();
    var diagnostics = new CountingDiagnosticsSink();
    await using var host = new RuntimeHost(
        LongMacroConfig(),
        dryRun,
        AlwaysForegroundGate.Instance,
        source,
        diagnostics);

    var expectedKey2Taps = 0;
    var stopCleanupEvents = 0;
    for (var index = 0; index < options.BurstCount; index++)
    {
        await host.StartAsync(CancellationToken.None);
        await host.ReloadAsync(LongMacroConfig(), CancellationToken.None);
        source.KeyDown("q");
        if (!await WaitUntilAsync(
                () => dryRun.Count("key", "a", true) > dryRun.Count("key", "a", false),
                options.DrainTimeout,
                CancellationToken.None))
        {
            return Failed(name, watch, $"long macro did not press before reload at loop {index}");
        }

        await host.ReloadAsync(TapConfig("w", "2", tapHoldMs: 1), CancellationToken.None);
        var afterReload = host.Snapshot();
        if (afterReload.Runtime.Presses.KeyOwners.Count != 0 ||
            afterReload.Runtime.Presses.OwnerKeys.Count != 0 ||
            afterReload.Runtime.PendingActionCount != 0 ||
            afterReload.Runtime.RunningActionCount != 0)
        {
            return Failed(name, watch, $"reload left dirty runtime state at loop {index}");
        }

        for (var burst = 0; burst < Math.Min(options.BurstCount, 25); burst++)
        {
            source.KeyDown("w");
            expectedKey2Taps++;
            if (burst % 5 == 0)
            {
                await Task.Yield();
            }
        }

        var eventsBeforeStop = dryRun.Events.Count;
        await host.StopAsync(CancellationToken.None);
        var eventsAfterStop = dryRun.Events.Count;
        stopCleanupEvents += eventsAfterStop - eventsBeforeStop;
        var stopped = host.Snapshot();
        if (!IsCleanlyStopped(stopped))
        {
            return Failed(name, watch, $"runtime did not stop cleanly at loop {index}");
        }

        await Task.Delay(20);
        if (dryRun.Events.Count != eventsAfterStop)
        {
            return Failed(name, watch, $"input was emitted after interleaved stop at loop {index}");
        }
    }

    var finalSnapshot = host.Snapshot();
    if (!IsCleanlyStopped(finalSnapshot))
    {
        return Failed(name, watch, "final runtime state is not clean");
    }

    if (dryRun.Count("key", "a", true) != options.BurstCount ||
        dryRun.Count("key", "a", false) != options.BurstCount)
    {
        return Failed(name, watch, "long macro held key was not released exactly once per loop");
    }

    var expectedKey2Events = expectedKey2Taps * 2;
    if (dryRun.Count("key", "2") > expectedKey2Events)
    {
        return Failed(
            name,
            watch,
            $"interleaved burst emitted too many key 2 events: expected_max={expectedKey2Events} actual={dryRun.Count("key", "2")}");
    }

    var endResources = CaptureResourceSample(process);
    var metrics = new Dictionary<string, object>
    {
        ["loops"] = options.BurstCount,
        ["burst_per_loop"] = Math.Min(options.BurstCount, 25),
        ["expected_key_2_events_max"] = expectedKey2Events,
        ["key_a_down"] = dryRun.Count("key", "a", true),
        ["key_a_up"] = dryRun.Count("key", "a", false),
        ["key_2_events"] = dryRun.Count("key", "2"),
        ["input_events"] = dryRun.Events.Count,
        ["stop_cleanup_events_allowed"] = stopCleanupEvents,
        ["pipeline_queued"] = finalSnapshot.Pipeline.QueuedCount,
        ["pipeline_handled"] = finalSnapshot.Pipeline.HandledCount,
        ["pipeline_dropped"] = finalSnapshot.Pipeline.DroppedCount,
        ["pipeline_pending_after_stop"] = finalSnapshot.Pipeline.PendingCount,
        ["runtime_pending_actions_after_stop"] = finalSnapshot.Runtime.PendingActionCount,
        ["runtime_running_actions_after_stop"] = finalSnapshot.Runtime.RunningActionCount,
        ["held_key_count_after_stop"] = finalSnapshot.Runtime.Presses.KeyOwners.Count,
        ["press_owner_count_after_stop"] = finalSnapshot.Runtime.Presses.OwnerKeys.Count,
        ["post_stop_events"] = 0,
        ["diagnostic_events"] = diagnostics.Count,
        ["stopped_clean"] = IsCleanlyStopped(finalSnapshot)
    };
    AddResourceMetrics(metrics, startResources, endResources, watch.Elapsed);
    AddSnapshots(metrics, options, ("stopped", finalSnapshot));
    return Passed(name, watch, metrics);
}

static async Task<AcceptanceScenarioResult> RunLiveSafeAsync(AcceptanceOptions options)
{
    const string name = "live-safe";
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, "live-safe requires --allow-live-input");
    }

    var process = Process.GetCurrentProcess();
    var startResources = CaptureResourceSample(process);
    const string targetProcess = "powershell.exe";
    using var blockerWindow = new LiveHarnessWindow("BA KeySmith Live Blocker - foreground gate should block here");
    Process? targetWindow = null;
    RuntimeHost? host = null;

    try
    {
        blockerWindow.Start(TimeSpan.FromSeconds(3));
        targetWindow = await StartPowerShellWindowAsync(
            "BA KeySmith Live Target - safe F13/F14 input",
            TimeSpan.FromSeconds(5));
        if (targetWindow is null)
        {
            return Failed(name, watch, "failed to start temporary PowerShell target window");
        }

        var runtimeInput = new CountingInputBackend(new WindowsInputBackend());
        var triggerSender = new WindowsInputBackend(markInjected: false);
        var diagnostics = new CountingDiagnosticsSink();
        host = new RuntimeHost(
            LiveTapConfig(targetProcess, "f13", "f14"),
            runtimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(),
            diagnostics);

        await host.StartAsync(CancellationToken.None);
        targetWindow.Refresh();
        if (!await LiveWindowTools.FocusAsync(targetWindow.MainWindowHandle, TimeSpan.FromSeconds(3)))
        {
            return Failed(name, watch, "failed to focus live target window");
        }

        var allowedGate = await host.CheckForegroundAsync(CancellationToken.None);
        if (!allowedGate.IsAllowed)
        {
            return Failed(
                name,
                watch,
                $"target foreground gate was not allowed: foreground={allowedGate.ForegroundProcess}");
        }

        await SendTriggerTapAsync(triggerSender, "f13");
        if (!await WaitForInputCountAsync(runtimeInput, "key", "f14", 2, options.DrainTimeout))
        {
            return Failed(name, watch, "allowed foreground did not emit f14 tap through SendInput");
        }

        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            return Failed(name, watch, "queues did not drain after allowed live trigger");
        }

        var allowedOutputEvents = runtimeInput.Count("key", "f14");
        if (!await blockerWindow.FocusAsync(TimeSpan.FromSeconds(3)))
        {
            return Failed(name, watch, "failed to focus temporary blocker window");
        }

        var blockedGate = await host.CheckForegroundAsync(CancellationToken.None);
        if (blockedGate.IsAllowed)
        {
            return Failed(
                name,
                watch,
                $"blocked foreground was unexpectedly allowed: foreground={blockedGate.ForegroundProcess}");
        }

        var beforeBlockedProbe = runtimeInput.Events.Count;
        await SendTriggerTapAsync(triggerSender, "f13");
        await Task.Delay(150);
        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            return Failed(name, watch, "queues did not drain after blocked live trigger");
        }

        var blockedProbeOutputDelta = runtimeInput.Events.Count - beforeBlockedProbe;
        if (blockedProbeOutputDelta != 0)
        {
            return Failed(name, watch, "blocked foreground emitted runtime input");
        }

        targetWindow.Refresh();
        if (!await LiveWindowTools.FocusAsync(targetWindow.MainWindowHandle, TimeSpan.FromSeconds(3)))
        {
            return Failed(name, watch, "failed to refocus live target window before reload");
        }

        await host.ReloadAsync(LiveTapConfig(targetProcess, "f15", "f16"), CancellationToken.None);
        var afterReload = host.Snapshot();
        var f14BeforeOldProbe = runtimeInput.Count("key", "f14");
        await SendTriggerTapAsync(triggerSender, "f13");
        await Task.Delay(150);
        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            return Failed(name, watch, "queues did not drain after old live trigger reload probe");
        }

        if (runtimeInput.Count("key", "f14") != f14BeforeOldProbe)
        {
            return Failed(name, watch, "old live trigger emitted input after reload");
        }

        await SendTriggerTapAsync(triggerSender, "f15");
        if (!await WaitForInputCountAsync(runtimeInput, "key", "f16", 2, options.DrainTimeout))
        {
            return Failed(name, watch, "new live trigger did not emit f16 tap after reload");
        }

        await host.DisableAsync(CancellationToken.None);
        var disabledSnapshot = host.Snapshot();
        var beforeDisabledProbe = runtimeInput.Events.Count;
        await SendTriggerTapAsync(triggerSender, "f15");
        await Task.Delay(150);
        var disabledProbeOutputDelta = runtimeInput.Events.Count - beforeDisabledProbe;
        if (disabledProbeOutputDelta != 0)
        {
            return Failed(name, watch, "disabled runtime emitted input");
        }

        await host.EnableRuntimeAsync(CancellationToken.None);
        await host.ReloadAsync(LiveLongMacroConfig(targetProcess, "f17", "f18"), CancellationToken.None);
        await SendTriggerTapAsync(triggerSender, "f17");
        if (!await WaitForInputCountAsync(runtimeInput, "key", "f18", 1, options.DrainTimeout))
        {
            return Failed(name, watch, "long live macro did not press f18 before stop");
        }

        var beforeStop = host.Snapshot();
        var stopWatch = Stopwatch.StartNew();
        await host.StopAsync(CancellationToken.None);
        stopWatch.Stop();
        var stopped = host.Snapshot();
        var eventsAfterStop = runtimeInput.Events.Count;
        await Task.Delay(150);

        if (runtimeInput.Count("key", "f18", true) != 1 ||
            runtimeInput.Count("key", "f18", false) != 1)
        {
            return Failed(name, watch, "long live macro held key was not released exactly once during stop");
        }

        if (runtimeInput.Events.Count != eventsAfterStop)
        {
            return Failed(name, watch, "live runtime emitted input after stop");
        }

        if (!IsCleanlyStopped(stopped))
        {
            return Failed(name, watch, "live runtime did not stop cleanly");
        }

        var endResources = CaptureResourceSample(process);
        var metrics = new Dictionary<string, object>
        {
            ["target_process"] = targetProcess,
            ["target_foreground_process"] = allowedGate.ForegroundProcess ?? string.Empty,
            ["blocked_foreground_process"] = blockedGate.ForegroundProcess ?? string.Empty,
            ["allowed_f14_events"] = allowedOutputEvents,
            ["blocked_probe_output_delta"] = blockedProbeOutputDelta,
            ["reload_generation"] = afterReload.Runtime.Generation,
            ["new_f16_events"] = runtimeInput.Count("key", "f16"),
            ["disabled_probe_output_delta"] = disabledProbeOutputDelta,
            ["long_macro_f18_down"] = runtimeInput.Count("key", "f18", true),
            ["long_macro_f18_up"] = runtimeInput.Count("key", "f18", false),
            ["stop_ms"] = stopWatch.Elapsed.TotalMilliseconds,
            ["post_stop_events"] = runtimeInput.Events.Count - eventsAfterStop,
            ["diagnostic_events"] = diagnostics.Count,
            ["stopped_clean"] = IsCleanlyStopped(stopped)
        };

        AddResourceMetrics(metrics, startResources, endResources, watch.Elapsed);
        AddSnapshots(
            metrics,
            options,
            ("after_reload", afterReload),
            ("disabled", disabledSnapshot),
            ("before_stop", beforeStop),
            ("stopped", stopped));
        return Passed(name, watch, metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message);
    }
    finally
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }

        CloseProcessWindow(targetWindow);
    }
}

static async Task<AcceptanceScenarioResult> RunLiveSoakAsync(AcceptanceOptions options)
{
    const string name = "live-soak";
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, "live-soak requires --allow-live-input");
    }

    var process = Process.GetCurrentProcess();
    const string targetProcess = "powershell.exe";
    Process? targetWindow = null;
    RuntimeHost? host = null;

    try
    {
        targetWindow = await StartPowerShellWindowAsync(
            "BA KeySmith Live Soak Target - safe F13/F14 input",
            TimeSpan.FromSeconds(5));
        if (targetWindow is null)
        {
            return Failed(name, watch, "failed to start temporary PowerShell live soak target window");
        }

        var runtimeInput = new CountingInputBackend(new WindowsInputBackend());
        var triggerSender = new WindowsInputBackend(markInjected: false);
        var diagnostics = new CountingDiagnosticsSink();
        var peaks = new Dictionary<string, long>
        {
            ["pipeline_pending_max"] = 0,
            ["runtime_pending_actions_max"] = 0,
            ["runtime_running_actions_max"] = 0,
            ["active_workers_max"] = 0,
            ["held_key_count_max"] = 0,
            ["press_owner_count_max"] = 0
        };

        host = new RuntimeHost(
            LiveTapConfig(targetProcess, "f13", "f14"),
            runtimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(),
            diagnostics);

        await host.StartAsync(CancellationToken.None);
        targetWindow.Refresh();
        if (!await LiveWindowTools.FocusAsync(targetWindow.MainWindowHandle, TimeSpan.FromSeconds(3)))
        {
            return Failed(name, watch, "failed to focus live soak target window");
        }

        var gate = await host.CheckForegroundAsync(CancellationToken.None);
        if (!gate.IsAllowed)
        {
            return Failed(name, watch, $"live soak target foreground was not allowed: {gate.ForegroundProcess}");
        }

        var startResources = CaptureResourceSample(process);
        var emitWatch = Stopwatch.StartNew();
        var emitted = 0;
        var foregroundLosses = 0;
        var foregroundRefreshes = 0;
        var intervalTicks = Stopwatch.Frequency / options.SoakRateHz;
        var nextDueTicks = (double)Stopwatch.GetTimestamp();
        while (emitWatch.Elapsed < options.SoakDuration)
        {
            targetWindow.Refresh();
            if (!LiveWindowTools.IsForeground(targetWindow.MainWindowHandle))
            {
                foregroundLosses++;
                if (!await LiveWindowTools.FocusAsync(targetWindow.MainWindowHandle, TimeSpan.FromMilliseconds(500)))
                {
                    return Failed(name, watch, "live soak target lost foreground and could not be refocused");
                }

                foregroundRefreshes++;
            }

            await SendTriggerTapAsync(triggerSender, "f13");
            emitted++;
            TrackPeak(host.Snapshot(), peaks);
            await Task.Yield();
            TrackPeak(host.Snapshot(), peaks);
            nextDueTicks += intervalTicks;
            var remainingTicks = nextDueTicks - Stopwatch.GetTimestamp();
            if (remainingTicks > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(remainingTicks / Stopwatch.Frequency));
            }
            else
            {
                await Task.Yield();
            }
        }

        emitWatch.Stop();
        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            return Failed(name, watch, "live soak queues did not drain");
        }

        var expectedOutputEvents = emitted * 2;
        var running = host.Snapshot();
        TrackPeak(running, peaks);
        var eventsBeforeStop = runtimeInput.Events.Count;
        var stopWatch = Stopwatch.StartNew();
        await host.StopAsync(CancellationToken.None);
        stopWatch.Stop();
        var stopped = host.Snapshot();
        await Task.Delay(150);

        if (runtimeInput.Events.Count != eventsBeforeStop)
        {
            return Failed(name, watch, "live soak emitted input after stop");
        }

        if (!IsCleanlyStopped(stopped))
        {
            return Failed(name, watch, "live soak runtime did not stop cleanly");
        }

        var endResources = CaptureResourceSample(process);
        var metrics = new Dictionary<string, object>
        {
            ["target_process"] = targetProcess,
            ["foreground_process"] = gate.ForegroundProcess ?? string.Empty,
            ["soak_seconds"] = Math.Round(options.SoakDuration.TotalSeconds, 3),
            ["soak_rate_hz"] = Math.Round(options.SoakRateHz, 3),
            ["observed_trigger_rate_hz"] = Math.Round(emitted / Math.Max(emitWatch.Elapsed.TotalSeconds, 0.001), 3),
            ["triggers_emitted"] = emitted,
            ["expected_output_events"] = expectedOutputEvents,
            ["output_events"] = runtimeInput.Events.Count,
            ["f14_output_events"] = runtimeInput.Count("key", "f14"),
            ["foreground_loss_count"] = foregroundLosses,
            ["foreground_refresh_count"] = foregroundRefreshes,
            ["diagnostic_events"] = diagnostics.Count,
            ["pipeline_queued"] = running.Pipeline.QueuedCount,
            ["pipeline_handled"] = running.Pipeline.HandledCount,
            ["pipeline_pending_after_drain"] = running.Pipeline.PendingCount,
            ["runtime_pending_actions_after_drain"] = running.Runtime.PendingActionCount,
            ["runtime_running_actions_after_drain"] = running.Runtime.RunningActionCount,
            ["held_key_count_after_drain"] = running.Runtime.Presses.KeyOwners.Count,
            ["press_owner_count_after_drain"] = running.Runtime.Presses.OwnerKeys.Count,
            ["stop_ms"] = stopWatch.Elapsed.TotalMilliseconds,
            ["post_stop_events"] = runtimeInput.Events.Count - eventsBeforeStop,
            ["stopped_clean"] = IsCleanlyStopped(stopped)
        };

        foreach (var (key, value) in peaks)
        {
            metrics[key] = value;
        }

        AddResourceMetrics(metrics, startResources, endResources, watch.Elapsed);
        AddSnapshots(metrics, options, ("running", running), ("stopped", stopped));
        if (runtimeInput.Count("key", "f14") != expectedOutputEvents)
        {
            return Failed(
                name,
                watch,
                $"live soak output mismatch: expected={expectedOutputEvents} actual={runtimeInput.Count("key", "f14")}",
                metrics);
        }

        return Passed(name, watch, metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message);
    }
    finally
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }

        CloseProcessWindow(targetWindow);
    }
}

static async Task<AcceptanceScenarioResult> RunMouseTriggerSuppressedAsync(AcceptanceOptions options)
{
    const string name = "mouse-trigger-suppressed";
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, $"{name} requires --allow-live-input");
    }

    using var target = new LiveHarnessWindow("BA KeySmith Mouse Suppress Target");
    RuntimeHost? host = null;
    try
    {
        target.Start(TimeSpan.FromSeconds(3));
        var runtimeInput = new CountingInputBackend(new WindowsInputBackend());
        var triggerSender = new WindowsInputBackend(markInjected: false);
        var diagnostics = new CountingDiagnosticsSink();
        host = new RuntimeHost(
            LiveTapConfig(CurrentProcessTargetName(), "mouse_right", "f14"),
            runtimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(),
            diagnostics);

        await host.StartAsync(CancellationToken.None);
        if (!await target.FocusAsync(TimeSpan.FromSeconds(3)) ||
            !LiveWindowTools.MoveCursorToCenter(target.Handle))
        {
            return Failed(name, watch, "failed to focus target and move cursor");
        }

        var mouseDownBefore = target.MouseDownCount;
        var mouseUpBefore = target.MouseUpCount;
        await SendMouseClickAsync(triggerSender, "mouse_right");
        if (!await WaitForInputCountAsync(runtimeInput, "key", "f14", 2, options.DrainTimeout))
        {
            return Failed(name, watch, "mouse trigger did not produce mapped f14 tap");
        }

        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            return Failed(name, watch, "queues did not drain after mouse suppress trigger");
        }

        var running = host.Snapshot();
        await host.StopAsync(CancellationToken.None);
        var stopped = host.Snapshot();
        if (!IsCleanlyStopped(stopped))
        {
            return Failed(name, watch, "stopped runtime is not clean");
        }

        var originalDownDelta = target.MouseDownCount - mouseDownBefore;
        var originalUpDelta = target.MouseUpCount - mouseUpBefore;
        if (originalDownDelta != 0 || originalUpDelta != 0)
        {
            return Failed(name, watch, "suppressed mouse trigger reached target window");
        }

        var metrics = new Dictionary<string, object>
        {
            ["original_mouse_down_delta"] = originalDownDelta,
            ["original_mouse_up_delta"] = originalUpDelta,
            ["mapped_f14_events"] = runtimeInput.Count("key", "f14"),
            ["diagnostic_events"] = diagnostics.Count,
            ["stopped_clean"] = IsCleanlyStopped(stopped)
        };
        AddSnapshots(metrics, options, ("running", running), ("stopped", stopped));
        return Passed(name, watch, metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message);
    }
    finally
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}

static async Task<AcceptanceScenarioResult> RunMouseTriggerForegroundBlockedAsync(AcceptanceOptions options)
{
    const string name = "mouse-trigger-foreground-blocked";
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, $"{name} requires --allow-live-input");
    }

    using var blocker = new LiveHarnessWindow("BA KeySmith Mouse Blocked Pass-Through");
    RuntimeHost? host = null;
    try
    {
        blocker.Start(TimeSpan.FromSeconds(3));
        var runtimeInput = new CountingInputBackend(new WindowsInputBackend());
        var triggerSender = new WindowsInputBackend(markInjected: false);
        var diagnostics = new CountingDiagnosticsSink();
        host = new RuntimeHost(
            LiveTapConfig("powershell.exe", "mouse_right", "f14"),
            runtimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(),
            diagnostics);

        await host.StartAsync(CancellationToken.None);
        if (!await blocker.FocusAsync(TimeSpan.FromSeconds(3)) ||
            !LiveWindowTools.MoveCursorToCenter(blocker.Handle))
        {
            return Failed(name, watch, "failed to focus blocker and move cursor");
        }

        var mouseDownBefore = blocker.MouseDownCount;
        var mouseUpBefore = blocker.MouseUpCount;
        await SendMouseClickAsync(triggerSender, "mouse_right");
        await Task.Delay(150);
        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            return Failed(name, watch, "queues did not drain after blocked mouse trigger");
        }

        var running = host.Snapshot();
        await host.StopAsync(CancellationToken.None);
        var stopped = host.Snapshot();
        if (!IsCleanlyStopped(stopped))
        {
            return Failed(name, watch, "stopped runtime is not clean");
        }

        var originalDownDelta = blocker.MouseDownCount - mouseDownBefore;
        var originalUpDelta = blocker.MouseUpCount - mouseUpBefore;
        if (runtimeInput.Events.Count != 0)
        {
            return Failed(name, watch, "foreground-blocked mouse trigger produced mapped output");
        }

        if (originalDownDelta == 0 || originalUpDelta == 0)
        {
            return Failed(name, watch, "foreground-blocked original mouse input did not pass through to blocker");
        }

        var metrics = new Dictionary<string, object>
        {
            ["original_mouse_down_to_blocker"] = originalDownDelta,
            ["original_mouse_up_to_blocker"] = originalUpDelta,
            ["mapped_output_events"] = runtimeInput.Events.Count,
            ["diagnostic_events"] = diagnostics.Count,
            ["stopped_clean"] = IsCleanlyStopped(stopped)
        };
        AddSnapshots(metrics, options, ("running", running), ("stopped", stopped));
        return Passed(name, watch, metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message);
    }
    finally
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}

static async Task<AcceptanceScenarioResult> RunMouseTriggerReloadDisableStopCleanAsync(AcceptanceOptions options)
{
    const string name = "mouse-trigger-reload-disable-stop-clean";
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, $"{name} requires --allow-live-input");
    }

    using var target = new LiveHarnessWindow("BA KeySmith Mouse Reload Disable Target");
    RuntimeHost? host = null;
    try
    {
        target.Start(TimeSpan.FromSeconds(3));
        var runtimeInput = new CountingInputBackend(new WindowsInputBackend());
        var triggerSender = new WindowsInputBackend(markInjected: false);
        var diagnostics = new CountingDiagnosticsSink();
        var targetProcess = CurrentProcessTargetName();
        host = new RuntimeHost(
            LiveTapConfig(targetProcess, "mouse_right", "f14"),
            runtimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(),
            diagnostics);

        await host.StartAsync(CancellationToken.None);
        if (!await target.FocusAsync(TimeSpan.FromSeconds(3)) ||
            !LiveWindowTools.MoveCursorToCenter(target.Handle))
        {
            return Failed(name, watch, "failed to focus target and move cursor");
        }

        await host.ReloadAsync(LiveTapConfig(targetProcess, "mouse_middle", "f16"), CancellationToken.None);
        var afterReload = host.Snapshot();

        var targetDownBeforeOldProbe = target.MouseDownCount;
        await SendMouseClickAsync(triggerSender, "mouse_right");
        await Task.Delay(150);
        if (runtimeInput.Count("key", "f14") != 0)
        {
            return Failed(name, watch, "old mouse trigger produced mapped output after reload");
        }

        var oldTriggerPassThroughDelta = target.MouseDownCount - targetDownBeforeOldProbe;
        if (oldTriggerPassThroughDelta == 0)
        {
            return Failed(name, watch, "old mouse trigger did not pass through after reload");
        }

        await SendMouseClickAsync(triggerSender, "mouse_middle");
        if (!await WaitForInputCountAsync(runtimeInput, "key", "f16", 2, options.DrainTimeout))
        {
            return Failed(name, watch, "new mouse trigger did not produce mapped output after reload");
        }

        await host.DisableAsync(CancellationToken.None);
        var disabled = host.Snapshot();
        var beforeDisabledProbe = runtimeInput.Events.Count;
        var targetDownBeforeDisabledProbe = target.MouseDownCount;
        await SendMouseClickAsync(triggerSender, "mouse_middle");
        await Task.Delay(150);
        var disabledOutputDelta = runtimeInput.Events.Count - beforeDisabledProbe;
        var disabledPassThroughDelta = target.MouseDownCount - targetDownBeforeDisabledProbe;
        if (disabledOutputDelta != 0)
        {
            return Failed(name, watch, "disabled mouse trigger produced mapped output");
        }

        if (disabledPassThroughDelta == 0)
        {
            return Failed(name, watch, "disabled mouse trigger did not pass through");
        }

        await host.StopAsync(CancellationToken.None);
        var stopped = host.Snapshot();
        if (!IsCleanlyStopped(stopped))
        {
            return Failed(name, watch, "stopped runtime is not clean");
        }

        var metrics = new Dictionary<string, object>
        {
            ["old_trigger_pass_through_delta"] = oldTriggerPassThroughDelta,
            ["new_f16_events"] = runtimeInput.Count("key", "f16"),
            ["disabled_output_delta"] = disabledOutputDelta,
            ["disabled_pass_through_delta"] = disabledPassThroughDelta,
            ["diagnostic_events"] = diagnostics.Count,
            ["stopped_clean"] = IsCleanlyStopped(stopped)
        };
        AddSnapshots(metrics, options, ("after_reload", afterReload), ("disabled", disabled), ("stopped", stopped));
        return Passed(name, watch, metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message);
    }
    finally
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}

static async Task<AcceptanceScenarioResult> RunWheelTriggerBoundariesAsync(AcceptanceOptions options)
{
    const string name = "wheel-trigger-boundaries";
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, $"{name} requires --allow-live-input");
    }

    using var allowedTarget = new LiveHarnessWindow("BA KeySmith Wheel Allowed Target");
    using var blockedTarget = new LiveHarnessWindow("BA KeySmith Wheel Blocked Target");
    RuntimeHost? allowedHost = null;
    RuntimeHost? blockedHost = null;
    try
    {
        allowedTarget.Start(TimeSpan.FromSeconds(3));
        blockedTarget.Start(TimeSpan.FromSeconds(3));

        var allowedRuntimeInput = new CountingInputBackend(new WindowsInputBackend());
        var blockedRuntimeInput = new CountingInputBackend(new WindowsInputBackend());
        var triggerSender = new WindowsInputBackend(markInjected: false);
        var allowedDiagnostics = new CountingDiagnosticsSink();
        var blockedDiagnostics = new CountingDiagnosticsSink();

        allowedHost = new RuntimeHost(
            LiveMappingsConfig(
                CurrentProcessTargetName(),
                new MappingConfigV1 { Trigger = "mouse_wheel_up", Type = "simple", Target = "f17", Mode = "tap" },
                new MappingConfigV1 { Trigger = "mouse_wheel_down", Type = "simple", Target = "f18", Mode = "tap" }),
            allowedRuntimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(allowedDiagnostics),
            allowedDiagnostics);

        await allowedHost.StartAsync(CancellationToken.None);
        if (!await allowedTarget.FocusAsync(TimeSpan.FromSeconds(3)) ||
            !LiveWindowTools.MoveCursorToCenter(allowedTarget.Handle))
        {
            return Failed(name, watch, "failed to focus allowed wheel target and move cursor");
        }

        allowedTarget.Reset();
        allowedRuntimeInput.Reset();
        allowedDiagnostics.Reset();

        await SendMouseWheelAsync(triggerSender, 120);
        if (!await WaitForInputCountAsync(allowedRuntimeInput, "key", "f17", 2, options.DrainTimeout))
        {
            return Failed(name, watch, "wheel-up trigger did not produce mapped f17 tap");
        }

        await SendMouseWheelAsync(triggerSender, -120);
        if (!await WaitForInputCountAsync(allowedRuntimeInput, "key", "f18", 2, options.DrainTimeout))
        {
            return Failed(name, watch, "wheel-down trigger did not produce mapped f18 tap");
        }

        if (!await WaitForDrainedAsync(allowedHost, options.DrainTimeout, CancellationToken.None))
        {
            return Failed(name, watch, "queues did not drain after wheel allowed probes");
        }

        var allowedRunning = allowedHost.Snapshot();
        await allowedHost.StopAsync(CancellationToken.None);
        var allowedStopped = allowedHost.Snapshot();
        if (!IsCleanlyStopped(allowedStopped))
        {
            return Failed(name, watch, "allowed wheel runtime is not clean after stop");
        }

        var allowedWheelUpOriginalDelta = allowedTarget.WheelUpCount;
        var allowedWheelDownOriginalDelta = allowedTarget.WheelDownCount;
        if (allowedWheelUpOriginalDelta != 0 || allowedWheelDownOriginalDelta != 0)
        {
            return Failed(name, watch, "allowed wheel trigger reached target window instead of being suppressed");
        }

        blockedHost = new RuntimeHost(
            LiveMappingsConfig(
                "powershell.exe",
                new MappingConfigV1 { Trigger = "mouse_wheel_up", Type = "simple", Target = "f17", Mode = "tap" },
                new MappingConfigV1 { Trigger = "mouse_wheel_down", Type = "simple", Target = "f18", Mode = "tap" }),
            blockedRuntimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(blockedDiagnostics),
            blockedDiagnostics);

        await blockedHost.StartAsync(CancellationToken.None);
        if (!await blockedTarget.FocusAsync(TimeSpan.FromSeconds(3)) ||
            !LiveWindowTools.MoveCursorToCenter(blockedTarget.Handle))
        {
            return Failed(name, watch, "failed to focus blocked wheel target and move cursor");
        }

        blockedTarget.Reset();
        blockedRuntimeInput.Reset();
        blockedDiagnostics.Reset();

        await SendMouseWheelAsync(triggerSender, 120);
        if (!await WaitUntilAsync(
                () => blockedTarget.WheelUpCount > 0,
                options.ManualTimeout,
                CancellationToken.None))
        {
            return Failed(name, watch, "blocked wheel-up did not pass through to blocker window");
        }

        await SendMouseWheelAsync(triggerSender, -120);
        if (!await WaitUntilAsync(
                () => blockedTarget.WheelDownCount > 0,
                options.ManualTimeout,
                CancellationToken.None))
        {
            return Failed(name, watch, "blocked wheel-down did not pass through to blocker window");
        }

        await Task.Delay(150, CancellationToken.None);
        if (!await WaitForDrainedAsync(blockedHost, options.DrainTimeout, CancellationToken.None))
        {
            return Failed(name, watch, "queues did not drain after blocked wheel probes");
        }

        var blockedRunning = blockedHost.Snapshot();
        await blockedHost.StopAsync(CancellationToken.None);
        var blockedStopped = blockedHost.Snapshot();
        if (!IsCleanlyStopped(blockedStopped))
        {
            return Failed(name, watch, "blocked wheel runtime is not clean after stop");
        }

        var blockedWheelUpPassThrough = blockedTarget.WheelUpCount;
        var blockedWheelDownPassThrough = blockedTarget.WheelDownCount;
        var blockedWheelOutputDelta =
            blockedRuntimeInput.Count("key", "f17") +
            blockedRuntimeInput.Count("key", "f18");
        var blockedWheelUpIgnored = CollectRuntimeIgnored(blockedDiagnostics, "mouse:mouse_wheel_up");
        var blockedWheelDownIgnored = CollectRuntimeIgnored(blockedDiagnostics, "mouse:mouse_wheel_down");
        if (blockedWheelOutputDelta != 0)
        {
            return Failed(name, watch, "blocked wheel trigger produced mapped output");
        }

        var metrics = new Dictionary<string, object>
        {
            ["wheel_semantics"] = "allowed_suppress_blocked_pass_through",
            ["wheel_participates_captured_session"] = false,
            ["wheel_allowed_up_original_delta"] = allowedWheelUpOriginalDelta,
            ["wheel_allowed_down_original_delta"] = allowedWheelDownOriginalDelta,
            ["wheel_allowed_up_mapped_f17_events"] = allowedRuntimeInput.Count("key", "f17"),
            ["wheel_allowed_down_mapped_f18_events"] = allowedRuntimeInput.Count("key", "f18"),
            ["wheel_allowed_up_trigger_received_count"] = CountHookTriggerReceived(allowedDiagnostics, "mouse:mouse_wheel_up"),
            ["wheel_allowed_down_trigger_received_count"] = CountHookTriggerReceived(allowedDiagnostics, "mouse:mouse_wheel_down"),
            ["wheel_allowed_up_pipeline_enqueued_count"] = CountPipelineQueued(allowedDiagnostics, "mouse:mouse_wheel_up"),
            ["wheel_allowed_down_pipeline_enqueued_count"] = CountPipelineQueued(allowedDiagnostics, "mouse:mouse_wheel_down"),
            ["wheel_allowed_up_runtime_dispatched_count"] = CountRuntimeDispatched(allowedDiagnostics, "mouse:mouse_wheel_up"),
            ["wheel_allowed_down_runtime_dispatched_count"] = CountRuntimeDispatched(allowedDiagnostics, "mouse:mouse_wheel_down"),
            ["wheel_allowed_runtime_foreground_allowed_count"] = CountRuntimeForegroundAllowed(allowedDiagnostics),
            ["wheel_blocked_up_pass_through"] = blockedWheelUpPassThrough,
            ["wheel_blocked_down_pass_through"] = blockedWheelDownPassThrough,
            ["wheel_blocked_output_delta"] = blockedWheelOutputDelta,
            ["wheel_blocked_up_trigger_received_count"] = CountHookTriggerReceived(blockedDiagnostics, "mouse:mouse_wheel_up"),
            ["wheel_blocked_down_trigger_received_count"] = CountHookTriggerReceived(blockedDiagnostics, "mouse:mouse_wheel_down"),
            ["wheel_blocked_up_pipeline_enqueued_count"] = CountPipelineQueued(blockedDiagnostics, "mouse:mouse_wheel_up"),
            ["wheel_blocked_down_pipeline_enqueued_count"] = CountPipelineQueued(blockedDiagnostics, "mouse:mouse_wheel_down"),
            ["wheel_blocked_up_runtime_dispatched_count"] = CountRuntimeDispatched(blockedDiagnostics, "mouse:mouse_wheel_up"),
            ["wheel_blocked_down_runtime_dispatched_count"] = CountRuntimeDispatched(blockedDiagnostics, "mouse:mouse_wheel_down"),
            ["wheel_blocked_up_runtime_ignored_count"] = blockedWheelUpIgnored.Length,
            ["wheel_blocked_down_runtime_ignored_count"] = blockedWheelDownIgnored.Length,
            ["wheel_blocked_up_runtime_ignored_reasons"] = JoinIgnoredReasons(blockedWheelUpIgnored),
            ["wheel_blocked_down_runtime_ignored_reasons"] = JoinIgnoredReasons(blockedWheelDownIgnored),
            ["diagnostic_events_allowed"] = allowedDiagnostics.Count,
            ["diagnostic_events_blocked"] = blockedDiagnostics.Count,
            ["allowed_stopped_clean"] = IsCleanlyStopped(allowedStopped),
            ["blocked_stopped_clean"] = IsCleanlyStopped(blockedStopped)
        };
        AddSnapshots(
            metrics,
            options,
            ("allowed_running", allowedRunning),
            ("allowed_stopped", allowedStopped),
            ("blocked_running", blockedRunning),
            ("blocked_stopped", blockedStopped));
        return Passed(name, watch, metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message);
    }
    finally
    {
        if (allowedHost is not null)
        {
            await allowedHost.DisposeAsync();
        }

        if (blockedHost is not null)
        {
            await blockedHost.DisposeAsync();
        }
    }
}

static async Task<AcceptanceScenarioResult> RunXButtonTriggerBoundariesAsync(AcceptanceOptions options)
{
    const string name = "xbutton-trigger-boundaries";
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, $"{name} requires --allow-live-input");
    }

    using var allowedTarget = new LiveHarnessWindow("BA KeySmith XButton Allowed Target");
    using var blockedTarget = new LiveHarnessWindow("BA KeySmith XButton Blocked Target");
    RuntimeHost? allowedHost = null;
    RuntimeHost? blockedHost = null;
    try
    {
        allowedTarget.Start(TimeSpan.FromSeconds(3));
        blockedTarget.Start(TimeSpan.FromSeconds(3));

        var allowedRuntimeInput = new CountingInputBackend(new WindowsInputBackend());
        var blockedRuntimeInput = new CountingInputBackend(new WindowsInputBackend());
        var triggerSender = new WindowsInputBackend(markInjected: false);
        var allowedDiagnostics = new CountingDiagnosticsSink();
        var blockedDiagnostics = new CountingDiagnosticsSink();

        allowedHost = new RuntimeHost(
            LiveMappingsConfig(
                CurrentProcessTargetName(),
                new MappingConfigV1 { Trigger = "mouse_x1", Type = "simple", Target = "f17", Mode = "tap" },
                new MappingConfigV1 { Trigger = "mouse_x2", Type = "simple", Target = "f18", Mode = "tap" }),
            allowedRuntimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(allowedDiagnostics),
            allowedDiagnostics);

        await allowedHost.StartAsync(CancellationToken.None);
        if (!await allowedTarget.FocusAsync(TimeSpan.FromSeconds(3)) ||
            !LiveWindowTools.MoveCursorToCenter(allowedTarget.Handle))
        {
            return Failed(name, watch, "failed to focus allowed xbutton target and move cursor");
        }

        allowedTarget.Reset();
        allowedRuntimeInput.Reset();
        allowedDiagnostics.Reset();

        await SendMouseClickAsync(triggerSender, "x1");
        if (!await WaitForInputCountAsync(allowedRuntimeInput, "key", "f17", 2, options.DrainTimeout))
        {
            return Failed(name, watch, "xbutton1 trigger did not produce mapped f17 tap");
        }

        await SendMouseClickAsync(triggerSender, "x2");
        if (!await WaitForInputCountAsync(allowedRuntimeInput, "key", "f18", 2, options.DrainTimeout))
        {
            return Failed(name, watch, "xbutton2 trigger did not produce mapped f18 tap");
        }

        if (!await WaitForDrainedAsync(allowedHost, options.DrainTimeout, CancellationToken.None))
        {
            return Failed(name, watch, "queues did not drain after xbutton allowed probes");
        }

        var allowedRunning = allowedHost.Snapshot();
        await allowedHost.StopAsync(CancellationToken.None);
        var allowedStopped = allowedHost.Snapshot();
        if (!IsCleanlyStopped(allowedStopped))
        {
            return Failed(name, watch, "allowed xbutton runtime is not clean after stop");
        }

        if (allowedTarget.XButton1DownCount != 0 ||
            allowedTarget.XButton1UpCount != 0 ||
            allowedTarget.XButton2DownCount != 0 ||
            allowedTarget.XButton2UpCount != 0)
        {
            return Failed(name, watch, "allowed xbutton trigger reached target window instead of being suppressed");
        }

        blockedHost = new RuntimeHost(
            LiveMappingsConfig(
                "powershell.exe",
                new MappingConfigV1 { Trigger = "mouse_x1", Type = "simple", Target = "f17", Mode = "tap" },
                new MappingConfigV1 { Trigger = "mouse_x2", Type = "simple", Target = "f18", Mode = "tap" }),
            blockedRuntimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(blockedDiagnostics),
            blockedDiagnostics);

        await blockedHost.StartAsync(CancellationToken.None);
        if (!await blockedTarget.FocusAsync(TimeSpan.FromSeconds(3)) ||
            !LiveWindowTools.MoveCursorToCenter(blockedTarget.Handle))
        {
            return Failed(name, watch, "failed to focus blocked xbutton target and move cursor");
        }

        blockedTarget.Reset();
        blockedRuntimeInput.Reset();
        blockedDiagnostics.Reset();

        await SendMouseClickAsync(triggerSender, "x1");
        if (!await WaitUntilAsync(
                () => blockedTarget.XButton1DownCount > 0 && blockedTarget.XButton1UpCount > 0,
                options.ManualTimeout,
                CancellationToken.None))
        {
            return Failed(name, watch, "blocked xbutton1 did not pass through to blocker window");
        }

        await SendMouseClickAsync(triggerSender, "x2");
        if (!await WaitUntilAsync(
                () => blockedTarget.XButton2DownCount > 0 && blockedTarget.XButton2UpCount > 0,
                options.ManualTimeout,
                CancellationToken.None))
        {
            return Failed(name, watch, "blocked xbutton2 did not pass through to blocker window");
        }

        await Task.Delay(150, CancellationToken.None);
        if (!await WaitForDrainedAsync(blockedHost, options.DrainTimeout, CancellationToken.None))
        {
            return Failed(name, watch, "queues did not drain after blocked xbutton probes");
        }

        var blockedRunning = blockedHost.Snapshot();
        await blockedHost.StopAsync(CancellationToken.None);
        var blockedStopped = blockedHost.Snapshot();
        if (!IsCleanlyStopped(blockedStopped))
        {
            return Failed(name, watch, "blocked xbutton runtime is not clean after stop");
        }

        var blockedOutputDelta =
            blockedRuntimeInput.Count("key", "f17") +
            blockedRuntimeInput.Count("key", "f18");
        var blockedXButton1Ignored = CollectRuntimeIgnored(blockedDiagnostics, "mouse:mouse_x1");
        var blockedXButton2Ignored = CollectRuntimeIgnored(blockedDiagnostics, "mouse:mouse_x2");
        if (blockedOutputDelta != 0)
        {
            return Failed(name, watch, "blocked xbutton trigger produced mapped output");
        }

        var metrics = new Dictionary<string, object>
        {
            ["xbutton_semantics"] = "allowed_suppress_blocked_pass_through",
            ["xbutton1_allowed_original_down_delta"] = allowedTarget.XButton1DownCount,
            ["xbutton1_allowed_original_up_delta"] = allowedTarget.XButton1UpCount,
            ["xbutton2_allowed_original_down_delta"] = allowedTarget.XButton2DownCount,
            ["xbutton2_allowed_original_up_delta"] = allowedTarget.XButton2UpCount,
            ["xbutton1_allowed_mapped_f17_events"] = allowedRuntimeInput.Count("key", "f17"),
            ["xbutton2_allowed_mapped_f18_events"] = allowedRuntimeInput.Count("key", "f18"),
            ["xbutton1_allowed_trigger_received_count"] = CountHookTriggerReceived(allowedDiagnostics, "mouse:mouse_x1"),
            ["xbutton2_allowed_trigger_received_count"] = CountHookTriggerReceived(allowedDiagnostics, "mouse:mouse_x2"),
            ["xbutton1_allowed_pipeline_enqueued_count"] = CountPipelineQueued(allowedDiagnostics, "mouse:mouse_x1"),
            ["xbutton2_allowed_pipeline_enqueued_count"] = CountPipelineQueued(allowedDiagnostics, "mouse:mouse_x2"),
            ["xbutton1_allowed_runtime_dispatched_count"] = CountRuntimeDispatched(allowedDiagnostics, "mouse:mouse_x1"),
            ["xbutton2_allowed_runtime_dispatched_count"] = CountRuntimeDispatched(allowedDiagnostics, "mouse:mouse_x2"),
            ["xbutton_allowed_runtime_foreground_allowed_count"] = CountRuntimeForegroundAllowed(allowedDiagnostics),
            ["xbutton1_blocked_pass_through_down"] = blockedTarget.XButton1DownCount,
            ["xbutton1_blocked_pass_through_up"] = blockedTarget.XButton1UpCount,
            ["xbutton2_blocked_pass_through_down"] = blockedTarget.XButton2DownCount,
            ["xbutton2_blocked_pass_through_up"] = blockedTarget.XButton2UpCount,
            ["xbutton_blocked_output_delta"] = blockedOutputDelta,
            ["xbutton1_blocked_trigger_received_count"] = CountHookTriggerReceived(blockedDiagnostics, "mouse:mouse_x1"),
            ["xbutton2_blocked_trigger_received_count"] = CountHookTriggerReceived(blockedDiagnostics, "mouse:mouse_x2"),
            ["xbutton1_blocked_pipeline_enqueued_count"] = CountPipelineQueued(blockedDiagnostics, "mouse:mouse_x1"),
            ["xbutton2_blocked_pipeline_enqueued_count"] = CountPipelineQueued(blockedDiagnostics, "mouse:mouse_x2"),
            ["xbutton1_blocked_runtime_dispatched_count"] = CountRuntimeDispatched(blockedDiagnostics, "mouse:mouse_x1"),
            ["xbutton2_blocked_runtime_dispatched_count"] = CountRuntimeDispatched(blockedDiagnostics, "mouse:mouse_x2"),
            ["xbutton1_blocked_runtime_ignored_count"] = blockedXButton1Ignored.Length,
            ["xbutton2_blocked_runtime_ignored_count"] = blockedXButton2Ignored.Length,
            ["xbutton1_blocked_runtime_ignored_reasons"] = JoinIgnoredReasons(blockedXButton1Ignored),
            ["xbutton2_blocked_runtime_ignored_reasons"] = JoinIgnoredReasons(blockedXButton2Ignored),
            ["diagnostic_events_allowed"] = allowedDiagnostics.Count,
            ["diagnostic_events_blocked"] = blockedDiagnostics.Count,
            ["allowed_stopped_clean"] = IsCleanlyStopped(allowedStopped),
            ["blocked_stopped_clean"] = IsCleanlyStopped(blockedStopped)
        };
        AddSnapshots(
            metrics,
            options,
            ("allowed_running", allowedRunning),
            ("allowed_stopped", allowedStopped),
            ("blocked_running", blockedRunning),
            ("blocked_stopped", blockedStopped));
        return Passed(name, watch, metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message);
    }
    finally
    {
        if (allowedHost is not null)
        {
            await allowedHost.DisposeAsync();
        }

        if (blockedHost is not null)
        {
            await blockedHost.DisposeAsync();
        }
    }
}

static async Task<AcceptanceScenarioResult> RunXButtonTriggerReloadDisableAsync(AcceptanceOptions options)
{
    const string name = "xbutton-trigger-reload-disable";
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, $"{name} requires --allow-live-input");
    }

    using var target = new LiveHarnessWindow("BA KeySmith XButton Reload Disable Target");
    RuntimeHost? host = null;
    try
    {
        target.Start(TimeSpan.FromSeconds(3));
        var runtimeInput = new CountingInputBackend(new WindowsInputBackend());
        var triggerSender = new WindowsInputBackend(markInjected: false);
        var diagnostics = new CountingDiagnosticsSink();
        var targetProcess = CurrentProcessTargetName();

        host = new RuntimeHost(
            LiveTapConfig(targetProcess, "mouse_x1", "f17"),
            runtimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(diagnostics),
            diagnostics);

        await host.StartAsync(CancellationToken.None);
        if (!await target.FocusAsync(TimeSpan.FromSeconds(3)) ||
            !LiveWindowTools.MoveCursorToCenter(target.Handle))
        {
            return Failed(name, watch, "failed to focus xbutton reload target and move cursor");
        }

        await host.ReloadAsync(LiveTapConfig(targetProcess, "mouse_x2", "f18"), CancellationToken.None);
        var afterReload = host.Snapshot();

        target.Reset();
        runtimeInput.Reset();
        diagnostics.Reset();
        await SendMouseClickAsync(triggerSender, "x1");
        await Task.Delay(150, CancellationToken.None);
        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            return Failed(name, watch, "queues did not drain after xbutton old-trigger probe");
        }

        var oldTriggerOutputDelta = runtimeInput.Count("key", "f17") + runtimeInput.Count("key", "f18");
        if (oldTriggerOutputDelta != 0)
        {
            return Failed(name, watch, "old xbutton trigger produced mapped output after reload");
        }

        if (target.XButton1DownCount == 0 || target.XButton1UpCount == 0)
        {
            return Failed(name, watch, "old xbutton trigger did not pass through after reload");
        }

        var oldTriggerPassThroughDown = target.XButton1DownCount;
        var oldTriggerPassThroughUp = target.XButton1UpCount;
        var oldTriggerReceivedCount = CountHookTriggerReceived(diagnostics, "mouse:mouse_x1");
        var oldPipelineEnqueuedCount = CountPipelineQueued(diagnostics, "mouse:mouse_x1");
        var oldRuntimeDispatchedCount = CountRuntimeDispatched(diagnostics, "mouse:mouse_x1");

        target.Reset();
        runtimeInput.Reset();
        diagnostics.Reset();
        await SendMouseClickAsync(triggerSender, "x2");
        if (!await WaitForInputCountAsync(runtimeInput, "key", "f18", 2, options.DrainTimeout))
        {
            return Failed(name, watch, "new xbutton trigger did not produce mapped output after reload");
        }

        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            return Failed(name, watch, "queues did not drain after xbutton new-trigger probe");
        }

        var newTriggerReceivedCount = CountHookTriggerReceived(diagnostics, "mouse:mouse_x2");
        var newPipelineEnqueuedCount = CountPipelineQueued(diagnostics, "mouse:mouse_x2");
        var newRuntimeDispatchedCount = CountRuntimeDispatched(diagnostics, "mouse:mouse_x2");
        var newTriggerMappedF18Events = runtimeInput.Count("key", "f18");
        var newXButtonOriginalDownDelta = target.XButton2DownCount;
        var newXButtonOriginalUpDelta = target.XButton2UpCount;
        if (newXButtonOriginalDownDelta != 0 || newXButtonOriginalUpDelta != 0)
        {
            return Failed(name, watch, "new xbutton trigger reached target window instead of being suppressed");
        }

        await host.DisableAsync(CancellationToken.None);
        var disabled = host.Snapshot();

        target.Reset();
        runtimeInput.Reset();
        diagnostics.Reset();
        await SendMouseClickAsync(triggerSender, "x2");
        await Task.Delay(150, CancellationToken.None);
        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            return Failed(name, watch, "queues did not drain after disabled xbutton probe");
        }

        var disabledOutputDelta = runtimeInput.Events.Count;
        if (disabledOutputDelta != 0)
        {
            return Failed(name, watch, "disabled xbutton trigger produced mapped output");
        }

        if (target.XButton2DownCount == 0 || target.XButton2UpCount == 0)
        {
            return Failed(name, watch, "disabled xbutton trigger did not pass through");
        }

        var disabledPassThroughDown = target.XButton2DownCount;
        var disabledPassThroughUp = target.XButton2UpCount;
        var disabledTriggerReceivedCount = CountHookTriggerReceived(diagnostics, "mouse:mouse_x2");
        var disabledPipelineEnqueuedCount = CountPipelineQueued(diagnostics, "mouse:mouse_x2");
        var disabledRuntimeDispatchedCount = CountRuntimeDispatched(diagnostics, "mouse:mouse_x2");
        var disabledIgnored = CollectRuntimeIgnored(diagnostics, "mouse:mouse_x2");

        await host.StopAsync(CancellationToken.None);
        var stopped = host.Snapshot();
        if (!IsCleanlyStopped(stopped))
        {
            return Failed(name, watch, "stopped xbutton reload runtime is not clean");
        }

        var metrics = new Dictionary<string, object>
        {
            ["reload_semantics"] = "old_trigger_pass_through_new_trigger_allowed_disabled_pass_through",
            ["old_trigger_pass_through_down"] = oldTriggerPassThroughDown,
            ["old_trigger_pass_through_up"] = oldTriggerPassThroughUp,
            ["old_trigger_output_delta"] = oldTriggerOutputDelta,
            ["old_trigger_received_count"] = oldTriggerReceivedCount,
            ["old_trigger_pipeline_enqueued_count"] = oldPipelineEnqueuedCount,
            ["old_trigger_runtime_dispatched_count"] = oldRuntimeDispatchedCount,
            ["new_trigger_mapped_f18_events"] = newTriggerMappedF18Events,
            ["new_trigger_original_down_delta"] = newXButtonOriginalDownDelta,
            ["new_trigger_original_up_delta"] = newXButtonOriginalUpDelta,
            ["new_trigger_received_count"] = newTriggerReceivedCount,
            ["new_trigger_pipeline_enqueued_count"] = newPipelineEnqueuedCount,
            ["new_trigger_runtime_dispatched_count"] = newRuntimeDispatchedCount,
            ["new_trigger_runtime_foreground_allowed_count"] = CountRuntimeForegroundAllowed(diagnostics),
            ["disabled_output_delta"] = disabledOutputDelta,
            ["disabled_pass_through_down"] = disabledPassThroughDown,
            ["disabled_pass_through_up"] = disabledPassThroughUp,
            ["disabled_trigger_received_count"] = disabledTriggerReceivedCount,
            ["disabled_pipeline_enqueued_count"] = disabledPipelineEnqueuedCount,
            ["disabled_runtime_dispatched_count"] = disabledRuntimeDispatchedCount,
            ["disabled_runtime_ignored_count"] = disabledIgnored.Length,
            ["disabled_runtime_ignored_reasons"] = JoinIgnoredReasons(disabledIgnored),
            ["diagnostic_events"] = diagnostics.Count,
            ["stopped_clean"] = IsCleanlyStopped(stopped)
        };
        AddSnapshots(metrics, options, ("after_reload", afterReload), ("disabled", disabled), ("stopped", stopped));
        return Passed(name, watch, metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message);
    }
    finally
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}

static async Task<AcceptanceScenarioResult> RunXButtonSelfInjectedPassThroughAsync(AcceptanceOptions options)
{
    const string name = "xbutton-self-injected-pass-through";
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, $"{name} requires --allow-live-input");
    }

    using var target = new LiveHarnessWindow("BA KeySmith XButton Self Injected Target");
    RuntimeHost? host = null;
    try
    {
        target.Start(TimeSpan.FromSeconds(3));
        var runtimeInput = new CountingInputBackend(new WindowsInputBackend());
        var markedSender = new WindowsInputBackend();
        var diagnostics = new CountingDiagnosticsSink();
        host = new RuntimeHost(
            LiveMappingsConfig(
                CurrentProcessTargetName(),
                new MappingConfigV1 { Trigger = "mouse_x1", Type = "simple", Target = "f17", Mode = "tap" },
                new MappingConfigV1 { Trigger = "mouse_x2", Type = "simple", Target = "f18", Mode = "tap" }),
            runtimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(diagnostics),
            diagnostics);

        await host.StartAsync(CancellationToken.None);
        if (!await target.FocusAsync(TimeSpan.FromSeconds(3)) ||
            !LiveWindowTools.MoveCursorToCenter(target.Handle))
        {
            return Failed(name, watch, "failed to focus self-injected xbutton target and move cursor");
        }

        target.Reset();
        runtimeInput.Reset();
        diagnostics.Reset();
        var before = host.Snapshot();
        await SendMouseClickAsync(markedSender, "x1");
        await SendMouseClickAsync(markedSender, "x2");
        await Task.Delay(200, CancellationToken.None);
        var after = host.Snapshot();

        await host.StopAsync(CancellationToken.None);
        var stopped = host.Snapshot();
        if (!IsCleanlyStopped(stopped))
        {
            return Failed(name, watch, "stopped self-injected xbutton runtime is not clean");
        }

        var pipelineDelta = after.Pipeline.QueuedCount - before.Pipeline.QueuedCount;
        var xbutton1HookTriggerCount = CountHookTriggerReceived(diagnostics, "mouse:mouse_x1");
        var xbutton2HookTriggerCount = CountHookTriggerReceived(diagnostics, "mouse:mouse_x2");
        if (pipelineDelta != 0 ||
            runtimeInput.Events.Count != 0 ||
            xbutton1HookTriggerCount != 0 ||
            xbutton2HookTriggerCount != 0)
        {
            return Failed(name, watch, "self-injected xbutton input re-entered trigger pipeline or produced mapped output");
        }

        var metrics = new Dictionary<string, object>
        {
            ["self_injected_pipeline_delta"] = pipelineDelta,
            ["mapped_output_events"] = runtimeInput.Events.Count,
            ["self_injected_xbutton1_hook_trigger_count"] = xbutton1HookTriggerCount,
            ["self_injected_xbutton2_hook_trigger_count"] = xbutton2HookTriggerCount,
            ["target_xbutton1_down_delta"] = target.XButton1DownCount,
            ["target_xbutton1_up_delta"] = target.XButton1UpCount,
            ["target_xbutton2_down_delta"] = target.XButton2DownCount,
            ["target_xbutton2_up_delta"] = target.XButton2UpCount,
            ["diagnostic_events"] = diagnostics.Count,
            ["stopped_clean"] = IsCleanlyStopped(stopped)
        };
        AddSnapshots(metrics, options, ("before", before), ("after", after), ("stopped", stopped));
        return Passed(name, watch, metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message);
    }
    finally
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}

static async Task<AcceptanceScenarioResult> RunCapturedTriggerForegroundChangeAsync(AcceptanceOptions options)
{
    const string name = "trigger-captured-then-foreground-changes-before-release";
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, $"{name} requires --allow-live-input");
    }

    using var target = new LiveHarnessWindow("BA KeySmith Captured Trigger Target");
    Process? blocker = null;
    RuntimeHost? host = null;
    try
    {
        target.Start(TimeSpan.FromSeconds(3));
        blocker = await StartPowerShellWindowAsync(
            "BA KeySmith Captured Trigger Blocker",
            TimeSpan.FromSeconds(5));
        if (blocker is null)
        {
            return Failed(name, watch, "failed to start blocker window");
        }

        var runtimeInput = new CountingInputBackend(new WindowsInputBackend());
        var triggerSender = new WindowsInputBackend(markInjected: false);
        var diagnostics = new CountingDiagnosticsSink();
        host = new RuntimeHost(
            LiveMappingsConfig(
                CurrentProcessTargetName(),
                new MappingConfigV1
                {
                    Trigger = "mouse_right",
                    Type = "simple",
                    Target = "f14",
                    Mode = "hold"
                }),
            runtimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(),
            diagnostics);

        await host.StartAsync(CancellationToken.None);
        if (!await target.FocusAsync(TimeSpan.FromSeconds(3)) ||
            !LiveWindowTools.MoveCursorToCenter(target.Handle))
        {
            return Failed(name, watch, "failed to focus target and move cursor");
        }

        await SendMouseDownAsync(triggerSender, "mouse_right");
        if (!await WaitForInputCountAsync(runtimeInput, "key", "f14", 1, options.DrainTimeout))
        {
            return Failed(name, watch, "captured mouse down did not hold mapped f14");
        }

        blocker.Refresh();
        if (!await LiveWindowTools.FocusAsync(blocker.MainWindowHandle, TimeSpan.FromSeconds(3)))
        {
            return Failed(name, watch, "failed to focus blocker before release");
        }

        await SendMouseUpAsync(triggerSender, "mouse_right");
        if (!await WaitForInputCountAsync(runtimeInput, "key", "f14", 2, options.DrainTimeout))
        {
            return Failed(name, watch, "captured mouse up did not release mapped f14 after foreground changed");
        }

        var running = host.Snapshot();
        await host.StopAsync(CancellationToken.None);
        var stopped = host.Snapshot();
        if (!IsCleanlyStopped(stopped))
        {
            return Failed(name, watch, "stopped runtime is not clean");
        }

        var metrics = new Dictionary<string, object>
        {
            ["mapped_f14_down"] = runtimeInput.Count("key", "f14", true),
            ["mapped_f14_up"] = runtimeInput.Count("key", "f14", false),
            ["target_original_mouse_down"] = target.MouseDownCount,
            ["target_original_mouse_up"] = target.MouseUpCount,
            ["diagnostic_events"] = diagnostics.Count,
            ["stopped_clean"] = IsCleanlyStopped(stopped)
        };
        AddSnapshots(metrics, options, ("running", running), ("stopped", stopped));
        return Passed(name, watch, metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message);
    }
    finally
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }

        CloseProcessWindow(blocker);
    }
}

static async Task<AcceptanceScenarioResult> RunSelfInjectedPassThroughAsync(AcceptanceOptions options)
{
    const string name = "self-injected-pass-through";
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, $"{name} requires --allow-live-input");
    }

    using var target = new LiveHarnessWindow("BA KeySmith Self Injected Target");
    RuntimeHost? host = null;
    try
    {
        target.Start(TimeSpan.FromSeconds(3));
        var runtimeInput = new CountingInputBackend(new WindowsInputBackend());
        var markedSender = new WindowsInputBackend();
        var diagnostics = new CountingDiagnosticsSink();
        host = new RuntimeHost(
            LiveMappingsConfig(
                CurrentProcessTargetName(),
                new MappingConfigV1 { Trigger = "f14", Type = "simple", Target = "f15", Mode = "tap" },
                new MappingConfigV1 { Trigger = "mouse_right", Type = "simple", Target = "f16", Mode = "tap" },
                new MappingConfigV1 { Trigger = "mouse_wheel_up", Type = "simple", Target = "f17", Mode = "tap" }),
            runtimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(),
            diagnostics);

        await host.StartAsync(CancellationToken.None);
        if (!await target.FocusAsync(TimeSpan.FromSeconds(3)) ||
            !LiveWindowTools.MoveCursorToCenter(target.Handle))
        {
            return Failed(name, watch, "failed to focus target and move cursor");
        }

        var before = host.Snapshot();
        await SendTriggerTapAsync(markedSender, "f14");
        await SendMouseClickAsync(markedSender, "mouse_right");
        await SendMouseWheelAsync(markedSender, 120);
        await Task.Delay(200);
        var after = host.Snapshot();

        await host.StopAsync(CancellationToken.None);
        var stopped = host.Snapshot();
        if (!IsCleanlyStopped(stopped))
        {
            return Failed(name, watch, "stopped runtime is not clean");
        }

        var pipelineDelta = after.Pipeline.QueuedCount - before.Pipeline.QueuedCount;
        if (pipelineDelta != 0 || runtimeInput.Events.Count != 0)
        {
            return Failed(name, watch, "self-injected input re-entered trigger pipeline or produced mapped output");
        }

        var metrics = new Dictionary<string, object>
        {
            ["self_injected_pipeline_delta"] = pipelineDelta,
            ["mapped_output_events"] = runtimeInput.Events.Count,
            ["diagnostic_events"] = diagnostics.Count,
            ["stopped_clean"] = IsCleanlyStopped(stopped)
        };
        AddSnapshots(metrics, options, ("before", before), ("after", after), ("stopped", stopped));
        return Passed(name, watch, metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message);
    }
    finally
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}

static async Task<AcceptanceScenarioResult> RunXButtonHoldThenForegroundChangeBeforeReleaseAsync(AcceptanceOptions options)
{
    const string name = "xbutton1-hold-then-foreground-change-before-release";
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, $"{name} requires --allow-live-input");
    }

    using var blocker = new LiveHarnessWindow("BA KeySmith XButton Hold Blocker");
    Process? target = null;
    RuntimeHost? host = null;
    try
    {
        blocker.Start(TimeSpan.FromSeconds(3));
        target = await StartPowerShellWindowAsync(
            "BA KeySmith XButton Hold Target - safe F17 hold",
            TimeSpan.FromSeconds(5));
        if (target is null)
        {
            return Failed(name, watch, "failed to start xbutton hold target window");
        }

        var runtimeInput = new CountingInputBackend(new WindowsInputBackend());
        var triggerSender = new WindowsInputBackend(markInjected: false);
        var diagnostics = new CountingDiagnosticsSink();
        const string triggerKey = "mouse:mouse_x1";

        host = new RuntimeHost(
            LiveHoldConfig("powershell.exe", "mouse_x1", "f17"),
            runtimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(diagnostics),
            diagnostics);

        await host.StartAsync(CancellationToken.None);
        target.Refresh();
        if (!await LiveWindowTools.FocusAsync(target.MainWindowHandle, TimeSpan.FromSeconds(3)) ||
            !LiveWindowTools.MoveCursorToCenter(target.MainWindowHandle))
        {
            return Failed(name, watch, "failed to focus xbutton hold target and move cursor");
        }

        blocker.Reset();
        runtimeInput.Reset();
        diagnostics.Reset();

        await SendMouseDownAsync(triggerSender, "x1");
        if (!await WaitForInputCountAsync(runtimeInput, "key", "f17", 1, options.DrainTimeout))
        {
            return Failed(name, watch, "xbutton hold down did not produce mapped f17 down");
        }

        if (!await blocker.FocusAsync(TimeSpan.FromSeconds(3)) ||
            !LiveWindowTools.MoveCursorToCenter(blocker.Handle))
        {
            return Failed(name, watch, "failed to focus blocker while xbutton hold was active");
        }

        const int foregroundChangedWhileHeldCount = 1;
        await SendMouseUpAsync(triggerSender, "x1");
        if (!await WaitForInputCountAsync(runtimeInput, "key", "f17", 2, options.DrainTimeout))
        {
            return Failed(name, watch, "xbutton hold release did not produce mapped f17 up after foreground changed");
        }

        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            return Failed(name, watch, "queues did not drain after xbutton hold foreground-change scenario");
        }

        var running = host.Snapshot();
        await host.StopAsync(CancellationToken.None);
        var stopped = host.Snapshot();
        if (!IsCleanlyStopped(stopped))
        {
            return Failed(name, watch, "xbutton hold foreground-change runtime did not stop cleanly");
        }

        var capturedSessionEnteredCount = CountHookLifecycleEvent(diagnostics, "captured_session_entered", triggerKey, "down");
        var releaseMatchedCapturedSessionCount = CountHookLifecycleEvent(diagnostics, "release_matched_captured_session", triggerKey, "up");
        var blockedByForegroundCount = CountHookLifecycleEvent(diagnostics, "capture_blocked_by_foreground", triggerKey, "down");
        var uncapturedReleasePassThroughCount = CountHookLifecycleEvent(diagnostics, "uncaptured_release_pass_through", triggerKey, "up");
        var triggerDownReceivedCount = CountHookTriggerReceived(diagnostics, triggerKey, "down");
        var triggerUpReceivedCount = CountHookTriggerReceived(diagnostics, triggerKey, "up");
        var releasePipelineEnqueuedCount = CountPipelineQueued(diagnostics, triggerKey, "up");
        var releaseRuntimeDispatchedCount = CountRuntimeDispatched(diagnostics, triggerKey, "up");
        var mappedOutputDownCount = runtimeInput.Count("key", "f17", true);
        var mappedOutputUpCount = runtimeInput.Count("key", "f17", false);
        var blockedPassThroughDownCount = CountHookTriggerReceived(diagnostics, triggerKey, "down", suppress: false, dispatch: false);
        var blockedPassThroughUpCount = CountHookTriggerReceived(diagnostics, triggerKey, "up", suppress: false, dispatch: false);
        var retroactiveCaptureCount = Math.Max(0, capturedSessionEnteredCount - 1);
        var unexpectedDispatchAfterForegroundReturnCount = Math.Max(0, releaseRuntimeDispatchedCount - 1);
        var unexpectedOutputAfterReleaseCount = Math.Max(0, mappedOutputUpCount - 1);

        if (capturedSessionEnteredCount != 1)
        {
            return Failed(name, watch, $"expected exactly one captured session entry, actual={capturedSessionEnteredCount}");
        }

        if (releaseMatchedCapturedSessionCount != 1)
        {
            return Failed(name, watch, $"expected release to match captured session exactly once, actual={releaseMatchedCapturedSessionCount}");
        }

        if (blockedByForegroundCount != 0 ||
            uncapturedReleasePassThroughCount != 0 ||
            blockedPassThroughDownCount != 0 ||
            blockedPassThroughUpCount != 0)
        {
            return Failed(name, watch, "captured release was treated as pass-through after foreground change");
        }

        if (blocker.XButton1DownCount != 0 || blocker.XButton1UpCount != 0)
        {
            return Failed(name, watch, "blocker received xbutton1 input even though captured session should have suppressed it");
        }

        if (mappedOutputDownCount != 1 || mappedOutputUpCount != 1)
        {
            return Failed(name, watch, $"mapped hold output mismatch: down={mappedOutputDownCount} up={mappedOutputUpCount}");
        }

        var metrics = new Dictionary<string, object>
        {
            ["validation_scope"] = "phase2c-xbutton1-hold-then-foreground-change-before-release",
            ["trigger_down_received_count"] = triggerDownReceivedCount,
            ["trigger_up_received_count"] = triggerUpReceivedCount,
            ["captured_session_entered_count"] = capturedSessionEnteredCount,
            ["foreground_changed_while_held_count"] = foregroundChangedWhileHeldCount,
            ["foreground_restored_while_held_count"] = 0,
            ["release_matched_captured_session_count"] = releaseMatchedCapturedSessionCount,
            ["retroactive_capture_count"] = retroactiveCaptureCount,
            ["release_pipeline_enqueued_count"] = releasePipelineEnqueuedCount,
            ["release_runtime_dispatched_count"] = releaseRuntimeDispatchedCount,
            ["mapped_output_down_count"] = mappedOutputDownCount,
            ["mapped_output_up_count"] = mappedOutputUpCount,
            ["blocked_pass_through_down_count"] = blockedPassThroughDownCount,
            ["blocked_pass_through_up_count"] = blockedPassThroughUpCount,
            ["capture_blocked_by_foreground_count"] = blockedByForegroundCount,
            ["uncaptured_release_pass_through_count"] = uncapturedReleasePassThroughCount,
            ["unexpected_dispatch_after_foreground_return_count"] = unexpectedDispatchAfterForegroundReturnCount,
            ["unexpected_output_after_release_count"] = unexpectedOutputAfterReleaseCount,
            ["cleanup_completed_count"] = 0,
            ["blocker_xbutton1_down_count"] = blocker.XButton1DownCount,
            ["blocker_xbutton1_up_count"] = blocker.XButton1UpCount,
            ["diagnostic_events"] = diagnostics.Count,
            ["stopped_clean"] = IsCleanlyStopped(stopped)
        };
        AddSnapshots(metrics, options, ("running", running), ("stopped", stopped));
        return Passed(name, watch, metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message);
    }
    finally
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }

        CloseProcessWindow(target);
    }
}

static async Task<AcceptanceScenarioResult> RunXButtonBlockedHoldThenForegroundReturnBeforeReleaseAsync(AcceptanceOptions options)
{
    const string name = "xbutton1-blocked-hold-then-foreground-return-before-release";
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, $"{name} requires --allow-live-input");
    }

    using var blocker = new LiveHarnessWindow("BA KeySmith XButton Blocked Hold Blocker");
    Process? target = null;
    RuntimeHost? host = null;
    try
    {
        blocker.Start(TimeSpan.FromSeconds(3));
        target = await StartPowerShellWindowAsync(
            "BA KeySmith XButton Hold Return Target - safe F17 hold",
            TimeSpan.FromSeconds(5));
        if (target is null)
        {
            return Failed(name, watch, "failed to start xbutton blocked-hold target window");
        }

        var runtimeInput = new CountingInputBackend(new WindowsInputBackend());
        var triggerSender = new WindowsInputBackend(markInjected: false);
        var diagnostics = new CountingDiagnosticsSink();
        const string triggerKey = "mouse:mouse_x1";

        host = new RuntimeHost(
            LiveHoldConfig("powershell.exe", "mouse_x1", "f17"),
            runtimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(diagnostics),
            diagnostics);

        await host.StartAsync(CancellationToken.None);
        if (!await blocker.FocusAsync(TimeSpan.FromSeconds(3)) ||
            !LiveWindowTools.MoveCursorToCenter(blocker.Handle))
        {
            return Failed(name, watch, "failed to focus blocker before blocked xbutton hold");
        }

        blocker.Reset();
        runtimeInput.Reset();
        diagnostics.Reset();

        await SendMouseDownAsync(triggerSender, "x1");
        if (!await WaitUntilAsync(
                () => blocker.XButton1DownCount > 0 &&
                    CountHookTriggerReceived(diagnostics, triggerKey, "down") > 0,
                options.ManualTimeout,
                CancellationToken.None))
        {
            return Failed(name, watch, "blocked xbutton hold down was not observed in blocker");
        }

        target.Refresh();
        if (!await LiveWindowTools.FocusAsync(target.MainWindowHandle, TimeSpan.FromSeconds(3)) ||
            !LiveWindowTools.MoveCursorToCenter(target.MainWindowHandle))
        {
            return Failed(name, watch, "failed to restore target foreground while xbutton remained held");
        }

        await SendMouseUpAsync(triggerSender, "x1");
        if (!await WaitUntilAsync(
                () => CountHookTriggerReceived(diagnostics, triggerKey, "up") > 0,
                options.DrainTimeout,
                CancellationToken.None))
        {
            return Failed(name, watch, "blocked hold release was not observed by hook after foreground returned");
        }

        await Task.Delay(150, CancellationToken.None);
        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            return Failed(name, watch, "queues did not drain after blocked hold foreground-return scenario");
        }

        var running = host.Snapshot();
        await host.StopAsync(CancellationToken.None);
        var stopped = host.Snapshot();
        if (!IsCleanlyStopped(stopped))
        {
            return Failed(name, watch, "blocked hold foreground-return runtime did not stop cleanly");
        }

        var capturedSessionEnteredCount = CountHookLifecycleEvent(diagnostics, "captured_session_entered", triggerKey, "down");
        var releaseMatchedCapturedSessionCount = CountHookLifecycleEvent(diagnostics, "release_matched_captured_session", triggerKey, "up");
        var blockedByForegroundCount = CountHookLifecycleEvent(diagnostics, "capture_blocked_by_foreground", triggerKey, "down");
        var uncapturedReleasePassThroughCount = CountHookLifecycleEvent(diagnostics, "uncaptured_release_pass_through", triggerKey, "up");
        var triggerDownReceivedCount = CountHookTriggerReceived(diagnostics, triggerKey, "down");
        var triggerUpReceivedCount = CountHookTriggerReceived(diagnostics, triggerKey, "up");
        var pipelineEnqueuedDownCount = CountPipelineQueued(diagnostics, triggerKey, "down");
        var pipelineEnqueuedUpCount = CountPipelineQueued(diagnostics, triggerKey, "up");
        var runtimeDispatchedDownCount = CountRuntimeDispatched(diagnostics, triggerKey, "down");
        var runtimeDispatchedUpCount = CountRuntimeDispatched(diagnostics, triggerKey, "up");
        var mappedOutputDownCount = runtimeInput.Count("key", "f17", true);
        var mappedOutputUpCount = runtimeInput.Count("key", "f17", false);
        var blockedPassThroughDownCount = CountHookTriggerReceived(diagnostics, triggerKey, "down", suppress: false, dispatch: false);
        var blockedPassThroughUpCount = CountHookTriggerReceived(diagnostics, triggerKey, "up", suppress: false, dispatch: false);
        var retroactiveCaptureCount = capturedSessionEnteredCount;
        var unexpectedDispatchAfterForegroundReturnCount = runtimeDispatchedDownCount + runtimeDispatchedUpCount;
        var unexpectedOutputAfterReleaseCount = mappedOutputDownCount + mappedOutputUpCount;

        if (capturedSessionEnteredCount != 0)
        {
            return Failed(name, watch, $"blocked hold unexpectedly entered captured session, actual={capturedSessionEnteredCount}");
        }

        if (releaseMatchedCapturedSessionCount != 0)
        {
            return Failed(name, watch, $"blocked hold release unexpectedly matched a captured session, actual={releaseMatchedCapturedSessionCount}");
        }

        if (pipelineEnqueuedDownCount != 0 ||
            pipelineEnqueuedUpCount != 0 ||
            runtimeDispatchedDownCount != 0 ||
            runtimeDispatchedUpCount != 0)
        {
            return Failed(name, watch, "blocked hold unexpectedly entered pipeline/runtime after foreground returned");
        }

        if (mappedOutputDownCount != 0 || mappedOutputUpCount != 0)
        {
            return Failed(name, watch, "blocked hold unexpectedly produced mapped output after foreground returned");
        }

        if (blockedPassThroughDownCount == 0 || blockedPassThroughUpCount == 0)
        {
            return Failed(name, watch, "blocked hold did not remain pass-through across down/up");
        }

        var metrics = new Dictionary<string, object>
        {
            ["validation_scope"] = "phase2c-xbutton1-blocked-hold-then-foreground-return-before-release",
            ["trigger_down_received_count"] = triggerDownReceivedCount,
            ["trigger_up_received_count"] = triggerUpReceivedCount,
            ["captured_session_entered_count"] = capturedSessionEnteredCount,
            ["foreground_changed_while_held_count"] = 1,
            ["foreground_restored_while_held_count"] = 1,
            ["release_matched_captured_session_count"] = releaseMatchedCapturedSessionCount,
            ["retroactive_capture_count"] = retroactiveCaptureCount,
            ["pipeline_enqueued_down_count"] = pipelineEnqueuedDownCount,
            ["pipeline_enqueued_up_count"] = pipelineEnqueuedUpCount,
            ["runtime_dispatched_down_count"] = runtimeDispatchedDownCount,
            ["runtime_dispatched_up_count"] = runtimeDispatchedUpCount,
            ["mapped_output_down_count"] = mappedOutputDownCount,
            ["mapped_output_up_count"] = mappedOutputUpCount,
            ["blocked_pass_through_down_count"] = blockedPassThroughDownCount,
            ["blocked_pass_through_up_count"] = blockedPassThroughUpCount,
            ["capture_blocked_by_foreground_count"] = blockedByForegroundCount,
            ["uncaptured_release_pass_through_count"] = uncapturedReleasePassThroughCount,
            ["unexpected_dispatch_after_foreground_return_count"] = unexpectedDispatchAfterForegroundReturnCount,
            ["unexpected_output_after_release_count"] = unexpectedOutputAfterReleaseCount,
            ["cleanup_completed_count"] = 0,
            ["blocker_xbutton1_down_count"] = blocker.XButton1DownCount,
            ["blocker_xbutton1_up_count"] = blocker.XButton1UpCount,
            ["diagnostic_events"] = diagnostics.Count,
            ["stopped_clean"] = IsCleanlyStopped(stopped)
        };
        AddSnapshots(metrics, options, ("running", running), ("stopped", stopped));
        return Passed(name, watch, metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message);
    }
    finally
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }

        CloseProcessWindow(target);
    }
}

static async Task<AcceptanceScenarioResult> RunXButton2HoldThenForegroundChangeBeforeReleaseAsync(AcceptanceOptions options)
{
    const string name = "xbutton2-hold-then-foreground-change-before-release";
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, $"{name} requires --allow-live-input");
    }

    using var blocker = new LiveHarnessWindow("BA KeySmith XButton2 Hold Blocker");
    Process? target = null;
    RuntimeHost? host = null;
    try
    {
        blocker.Start(TimeSpan.FromSeconds(3));
        target = await StartPowerShellWindowAsync(
            "BA KeySmith XButton2 Hold Target - safe F18 hold",
            TimeSpan.FromSeconds(5));
        if (target is null)
        {
            return Failed(name, watch, "failed to start xbutton2 hold target window");
        }

        var runtimeInput = new CountingInputBackend(new WindowsInputBackend());
        var triggerSender = new WindowsInputBackend(markInjected: false);
        var diagnostics = new CountingDiagnosticsSink();
        const string triggerKey = "mouse:mouse_x2";

        host = new RuntimeHost(
            LiveHoldConfig("powershell.exe", "mouse_x2", "f18"),
            runtimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(diagnostics),
            diagnostics);

        await host.StartAsync(CancellationToken.None);
        target.Refresh();
        if (!await LiveWindowTools.FocusAsync(target.MainWindowHandle, TimeSpan.FromSeconds(3)) ||
            !LiveWindowTools.MoveCursorToCenter(target.MainWindowHandle))
        {
            return Failed(name, watch, "failed to focus xbutton2 hold target and move cursor");
        }

        blocker.Reset();
        runtimeInput.Reset();
        diagnostics.Reset();

        await SendMouseDownAsync(triggerSender, "x2");
        if (!await WaitForInputCountAsync(runtimeInput, "key", "f18", 1, options.DrainTimeout))
        {
            return Failed(name, watch, "xbutton2 hold down did not produce mapped f18 down");
        }

        if (!await blocker.FocusAsync(TimeSpan.FromSeconds(3)) ||
            !LiveWindowTools.MoveCursorToCenter(blocker.Handle))
        {
            return Failed(name, watch, "failed to focus blocker while xbutton2 hold was active");
        }

        const int foregroundChangedWhileHeldCount = 1;
        await SendMouseUpAsync(triggerSender, "x2");
        if (!await WaitForInputCountAsync(runtimeInput, "key", "f18", 2, options.DrainTimeout))
        {
            return Failed(name, watch, "xbutton2 hold release did not produce mapped f18 up after foreground changed");
        }

        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            return Failed(name, watch, "queues did not drain after xbutton2 hold foreground-change scenario");
        }

        var running = host.Snapshot();
        await host.StopAsync(CancellationToken.None);
        var stopped = host.Snapshot();
        if (!IsCleanlyStopped(stopped))
        {
            return Failed(name, watch, "xbutton2 hold foreground-change runtime did not stop cleanly");
        }

        var capturedSessionEnteredCount = CountHookLifecycleEvent(diagnostics, "captured_session_entered", triggerKey, "down");
        var releaseMatchedCapturedSessionCount = CountHookLifecycleEvent(diagnostics, "release_matched_captured_session", triggerKey, "up");
        var blockedByForegroundCount = CountHookLifecycleEvent(diagnostics, "capture_blocked_by_foreground", triggerKey, "down");
        var uncapturedReleasePassThroughCount = CountHookLifecycleEvent(diagnostics, "uncaptured_release_pass_through", triggerKey, "up");
        var triggerDownReceivedCount = CountHookTriggerReceived(diagnostics, triggerKey, "down");
        var triggerUpReceivedCount = CountHookTriggerReceived(diagnostics, triggerKey, "up");
        var releasePipelineEnqueuedCount = CountPipelineQueued(diagnostics, triggerKey, "up");
        var releaseRuntimeDispatchedCount = CountRuntimeDispatched(diagnostics, triggerKey, "up");
        var mappedOutputDownCount = runtimeInput.Count("key", "f18", true);
        var mappedOutputUpCount = runtimeInput.Count("key", "f18", false);
        var blockedPassThroughDownCount = CountHookTriggerReceived(diagnostics, triggerKey, "down", suppress: false, dispatch: false);
        var blockedPassThroughUpCount = CountHookTriggerReceived(diagnostics, triggerKey, "up", suppress: false, dispatch: false);
        var retroactiveCaptureCount = Math.Max(0, capturedSessionEnteredCount - 1);
        var unexpectedDispatchAfterForegroundReturnCount = Math.Max(0, releaseRuntimeDispatchedCount - 1);
        var unexpectedOutputAfterReleaseCount = Math.Max(0, mappedOutputUpCount - 1);

        if (capturedSessionEnteredCount != 1)
        {
            return Failed(name, watch, $"expected exactly one captured session entry, actual={capturedSessionEnteredCount}");
        }

        if (releaseMatchedCapturedSessionCount != 1)
        {
            return Failed(name, watch, $"expected release to match captured session exactly once, actual={releaseMatchedCapturedSessionCount}");
        }

        if (blockedByForegroundCount != 0 ||
            uncapturedReleasePassThroughCount != 0 ||
            blockedPassThroughDownCount != 0 ||
            blockedPassThroughUpCount != 0)
        {
            return Failed(name, watch, "captured xbutton2 release was treated as pass-through after foreground change");
        }

        if (blocker.XButton2DownCount != 0 || blocker.XButton2UpCount != 0)
        {
            return Failed(name, watch, "blocker received xbutton2 input even though captured session should have suppressed it");
        }

        if (mappedOutputDownCount != 1 || mappedOutputUpCount != 1)
        {
            return Failed(name, watch, $"mapped xbutton2 hold output mismatch: down={mappedOutputDownCount} up={mappedOutputUpCount}");
        }

        var metrics = new Dictionary<string, object>
        {
            ["validation_scope"] = "phase2c-xbutton2-hold-then-foreground-change-before-release",
            ["trigger_down_received_count"] = triggerDownReceivedCount,
            ["trigger_up_received_count"] = triggerUpReceivedCount,
            ["captured_session_entered_count"] = capturedSessionEnteredCount,
            ["foreground_changed_while_held_count"] = foregroundChangedWhileHeldCount,
            ["foreground_restored_while_held_count"] = 0,
            ["release_matched_captured_session_count"] = releaseMatchedCapturedSessionCount,
            ["retroactive_capture_count"] = retroactiveCaptureCount,
            ["release_pipeline_enqueued_count"] = releasePipelineEnqueuedCount,
            ["release_runtime_dispatched_count"] = releaseRuntimeDispatchedCount,
            ["mapped_output_down_count"] = mappedOutputDownCount,
            ["mapped_output_up_count"] = mappedOutputUpCount,
            ["blocked_pass_through_down_count"] = blockedPassThroughDownCount,
            ["blocked_pass_through_up_count"] = blockedPassThroughUpCount,
            ["capture_blocked_by_foreground_count"] = blockedByForegroundCount,
            ["uncaptured_release_pass_through_count"] = uncapturedReleasePassThroughCount,
            ["unexpected_dispatch_after_foreground_return_count"] = unexpectedDispatchAfterForegroundReturnCount,
            ["unexpected_output_after_release_count"] = unexpectedOutputAfterReleaseCount,
            ["cleanup_completed_count"] = 0,
            ["blocker_xbutton2_down_count"] = blocker.XButton2DownCount,
            ["blocker_xbutton2_up_count"] = blocker.XButton2UpCount,
            ["diagnostic_events"] = diagnostics.Count,
            ["stopped_clean"] = IsCleanlyStopped(stopped)
        };
        AddSnapshots(metrics, options, ("running", running), ("stopped", stopped));
        return Passed(name, watch, metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message);
    }
    finally
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }

        CloseProcessWindow(target);
    }
}

static async Task<AcceptanceScenarioResult> RunXButton2BlockedHoldThenForegroundReturnBeforeReleaseAsync(AcceptanceOptions options)
{
    const string name = "xbutton2-blocked-hold-then-foreground-return-before-release";
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, $"{name} requires --allow-live-input");
    }

    using var blocker = new LiveHarnessWindow("BA KeySmith XButton2 Blocked Hold Blocker");
    Process? target = null;
    RuntimeHost? host = null;
    try
    {
        blocker.Start(TimeSpan.FromSeconds(3));
        target = await StartPowerShellWindowAsync(
            "BA KeySmith XButton2 Hold Return Target - safe F18 hold",
            TimeSpan.FromSeconds(5));
        if (target is null)
        {
            return Failed(name, watch, "failed to start xbutton2 blocked-hold target window");
        }

        var runtimeInput = new CountingInputBackend(new WindowsInputBackend());
        var triggerSender = new WindowsInputBackend(markInjected: false);
        var diagnostics = new CountingDiagnosticsSink();
        const string triggerKey = "mouse:mouse_x2";

        host = new RuntimeHost(
            LiveHoldConfig("powershell.exe", "mouse_x2", "f18"),
            runtimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(diagnostics),
            diagnostics);

        await host.StartAsync(CancellationToken.None);
        if (!await blocker.FocusAsync(TimeSpan.FromSeconds(3)) ||
            !LiveWindowTools.MoveCursorToCenter(blocker.Handle))
        {
            return Failed(name, watch, "failed to focus blocker before blocked xbutton2 hold");
        }

        blocker.Reset();
        runtimeInput.Reset();
        diagnostics.Reset();

        await SendMouseDownAsync(triggerSender, "x2");
        if (!await WaitUntilAsync(
                () => CountHookTriggerReceived(diagnostics, triggerKey, "down") > 0,
                options.ManualTimeout,
                CancellationToken.None))
        {
            return Failed(name, watch, "blocked xbutton2 hold down was not observed by hook in blocker foreground");
        }

        target.Refresh();
        if (!await LiveWindowTools.FocusAsync(target.MainWindowHandle, TimeSpan.FromSeconds(3)) ||
            !LiveWindowTools.MoveCursorToCenter(target.MainWindowHandle))
        {
            return Failed(name, watch, "failed to restore target foreground while xbutton2 remained held");
        }

        await SendMouseUpAsync(triggerSender, "x2");
        if (!await WaitUntilAsync(
                () => CountHookTriggerReceived(diagnostics, triggerKey, "up") > 0,
                options.DrainTimeout,
                CancellationToken.None))
        {
            return Failed(name, watch, "blocked xbutton2 hold release was not observed by hook after foreground returned");
        }

        await Task.Delay(150, CancellationToken.None);
        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            return Failed(name, watch, "queues did not drain after blocked xbutton2 hold foreground-return scenario");
        }

        var running = host.Snapshot();
        await host.StopAsync(CancellationToken.None);
        var stopped = host.Snapshot();
        if (!IsCleanlyStopped(stopped))
        {
            return Failed(name, watch, "blocked xbutton2 hold foreground-return runtime did not stop cleanly");
        }

        var capturedSessionEnteredCount = CountHookLifecycleEvent(diagnostics, "captured_session_entered", triggerKey, "down");
        var releaseMatchedCapturedSessionCount = CountHookLifecycleEvent(diagnostics, "release_matched_captured_session", triggerKey, "up");
        var blockedByForegroundCount = CountHookLifecycleEvent(diagnostics, "capture_blocked_by_foreground", triggerKey, "down");
        var uncapturedReleasePassThroughCount = CountHookLifecycleEvent(diagnostics, "uncaptured_release_pass_through", triggerKey, "up");
        var triggerDownReceivedCount = CountHookTriggerReceived(diagnostics, triggerKey, "down");
        var triggerUpReceivedCount = CountHookTriggerReceived(diagnostics, triggerKey, "up");
        var pipelineEnqueuedDownCount = CountPipelineQueued(diagnostics, triggerKey, "down");
        var pipelineEnqueuedUpCount = CountPipelineQueued(diagnostics, triggerKey, "up");
        var runtimeDispatchedDownCount = CountRuntimeDispatched(diagnostics, triggerKey, "down");
        var runtimeDispatchedUpCount = CountRuntimeDispatched(diagnostics, triggerKey, "up");
        var mappedOutputDownCount = runtimeInput.Count("key", "f18", true);
        var mappedOutputUpCount = runtimeInput.Count("key", "f18", false);
        var blockedPassThroughDownCount = CountHookTriggerReceived(diagnostics, triggerKey, "down", suppress: false, dispatch: false);
        var blockedPassThroughUpCount = CountHookTriggerReceived(diagnostics, triggerKey, "up", suppress: false, dispatch: false);
        var retroactiveCaptureCount = capturedSessionEnteredCount;
        var unexpectedDispatchAfterForegroundReturnCount = runtimeDispatchedDownCount + runtimeDispatchedUpCount;
        var unexpectedOutputAfterReleaseCount = mappedOutputDownCount + mappedOutputUpCount;

        if (capturedSessionEnteredCount != 0)
        {
            return Failed(name, watch, $"blocked xbutton2 hold unexpectedly entered captured session, actual={capturedSessionEnteredCount}");
        }

        if (releaseMatchedCapturedSessionCount != 0)
        {
            return Failed(name, watch, $"blocked xbutton2 hold release unexpectedly matched a captured session, actual={releaseMatchedCapturedSessionCount}");
        }

        if (pipelineEnqueuedDownCount != 0 ||
            pipelineEnqueuedUpCount != 0 ||
            runtimeDispatchedDownCount != 0 ||
            runtimeDispatchedUpCount != 0)
        {
            return Failed(name, watch, "blocked xbutton2 hold unexpectedly entered pipeline/runtime after foreground returned");
        }

        if (mappedOutputDownCount != 0 || mappedOutputUpCount != 0)
        {
            return Failed(name, watch, "blocked xbutton2 hold unexpectedly produced mapped output after foreground returned");
        }

        if (blockedPassThroughDownCount == 0 || blockedPassThroughUpCount == 0)
        {
            return Failed(name, watch, "blocked xbutton2 hold did not remain pass-through across down/up");
        }

        var metrics = new Dictionary<string, object>
        {
            ["validation_scope"] = "phase2c-xbutton2-blocked-hold-then-foreground-return-before-release",
            ["trigger_down_received_count"] = triggerDownReceivedCount,
            ["trigger_up_received_count"] = triggerUpReceivedCount,
            ["captured_session_entered_count"] = capturedSessionEnteredCount,
            ["foreground_changed_while_held_count"] = 1,
            ["foreground_restored_while_held_count"] = 1,
            ["release_matched_captured_session_count"] = releaseMatchedCapturedSessionCount,
            ["retroactive_capture_count"] = retroactiveCaptureCount,
            ["pipeline_enqueued_down_count"] = pipelineEnqueuedDownCount,
            ["pipeline_enqueued_up_count"] = pipelineEnqueuedUpCount,
            ["runtime_dispatched_down_count"] = runtimeDispatchedDownCount,
            ["runtime_dispatched_up_count"] = runtimeDispatchedUpCount,
            ["mapped_output_down_count"] = mappedOutputDownCount,
            ["mapped_output_up_count"] = mappedOutputUpCount,
            ["blocked_pass_through_down_count"] = blockedPassThroughDownCount,
            ["blocked_pass_through_up_count"] = blockedPassThroughUpCount,
            ["capture_blocked_by_foreground_count"] = blockedByForegroundCount,
            ["uncaptured_release_pass_through_count"] = uncapturedReleasePassThroughCount,
            ["unexpected_dispatch_after_foreground_return_count"] = unexpectedDispatchAfterForegroundReturnCount,
            ["unexpected_output_after_release_count"] = unexpectedOutputAfterReleaseCount,
            ["cleanup_completed_count"] = 0,
            ["blocker_xbutton2_down_count"] = blocker.XButton2DownCount,
            ["blocker_xbutton2_up_count"] = blocker.XButton2UpCount,
            ["diagnostic_events"] = diagnostics.Count,
            ["stopped_clean"] = IsCleanlyStopped(stopped)
        };
        AddSnapshots(metrics, options, ("running", running), ("stopped", stopped));
        return Passed(name, watch, metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message);
    }
    finally
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }

        CloseProcessWindow(target);
    }
}

static Task<AcceptanceScenarioResult> RunXButton2TriggeredDragStopDuringActiveDragAsync(AcceptanceOptions options)
{
    return RunXButton2TriggeredDragInterruptScenarioAsync(options, disableInterrupt: false);
}

static Task<AcceptanceScenarioResult> RunXButton2TriggeredDragDisableDuringActiveDragAsync(AcceptanceOptions options)
{
    return RunXButton2TriggeredDragInterruptScenarioAsync(options, disableInterrupt: true);
}

static async Task<AcceptanceScenarioResult> RunXButton2TriggeredDragInterruptScenarioAsync(
    AcceptanceOptions options,
    bool disableInterrupt)
{
    var interruptType = disableInterrupt ? "disable" : "stop";
    var name = disableInterrupt
        ? "xbutton2-triggered-drag-disable-during-active-drag"
        : "xbutton2-triggered-drag-stop-during-active-drag";
    const string triggerKey = "mouse:mouse_x2";
    var mappingId = disableInterrupt
        ? "xbutton2-drag-disable-during-active-drag"
        : "xbutton2-drag-stop-during-active-drag";
    const string mappedDragButton = "mouse_middle";
    var script = InterruptibleDragScript(mappedDragButton);
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, $"{name} requires --allow-live-input");
    }

    Process? target = null;
    RuntimeHost? host = null;
    var restoreInput = new WindowsInputBackend();
    var cursorStart = (X: 0, Y: 0);
    var cursorCaptured = false;
    var cursorRestored = false;
    try
    {
        target = await StartPowerShellWindowAsync(
            $"BA KeySmith XButton2 Drag Interrupt Target - {interruptType}",
            TimeSpan.FromSeconds(5));
        if (target is null)
        {
            return Failed(name, watch, $"failed to start xbutton2 drag {interruptType} target window");
        }

        var runtimeInput = new CountingInputBackend(new WindowsInputBackend());
        var triggerSender = new WindowsInputBackend(markInjected: false);
        var diagnostics = new CountingDiagnosticsSink();
        host = new RuntimeHost(
            LiveMacroConfig("powershell.exe", mappingId, "mouse_x2", script),
            runtimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(diagnostics),
            diagnostics);

        await host.StartAsync(CancellationToken.None);
        target.Refresh();
        if (!await LiveWindowTools.FocusAsync(target.MainWindowHandle, TimeSpan.FromSeconds(3)) ||
            !LiveWindowTools.MoveCursorToCenter(target.MainWindowHandle))
        {
            return Failed(name, watch, $"failed to focus xbutton2 drag {interruptType} target and move cursor");
        }

        runtimeInput.Reset();
        diagnostics.Reset();
        cursorStart = await runtimeInput.GetMousePositionAsync(CancellationToken.None);
        cursorCaptured = true;

        await SendMouseClickAsync(triggerSender, "x2");
        var barrier = await WaitForActiveDragBarrierAsync(
            host,
            runtimeInput,
            diagnostics,
            triggerKey,
            mappedDragButton,
            mappingId,
            options.DrainTimeout,
            CancellationToken.None);
        if (barrier is null)
        {
            var failedMetrics = new Dictionary<string, object>
            {
                ["validation_scope"] = $"phase2d-b-xbutton2-triggered-drag-{interruptType}-during-active-drag",
                ["interrupt_type"] = interruptType,
                ["mapped_drag_button"] = mappedDragButton,
                ["trigger_received_count"] = CountHookTriggerReceived(diagnostics, triggerKey, "down") + CountHookTriggerReceived(diagnostics, triggerKey, "up"),
                ["pipeline_enqueued_count"] = CountPipelineQueued(diagnostics, triggerKey, "down"),
                ["runtime_dispatched_count"] = CountRuntimeDispatched(diagnostics, triggerKey, "down"),
                ["drag_started_count"] = runtimeInput.Count("mouse", mappedDragButton, true),
                ["drag_button_down_count"] = runtimeInput.Count("mouse", mappedDragButton, true),
                ["drag_button_up_count"] = runtimeInput.Count("mouse", mappedDragButton, false),
                ["move_event_before_interrupt_count"] = MoveEvents(runtimeInput).Count,
                ["interrupt_while_drag_active_count"] = 0,
                ["held_owner_count_after_cleanup"] = host.Snapshot().Runtime.Presses.OwnerKeys.Count,
                ["stopped_clean"] = false
            };
            AddSnapshots(failedMetrics, options, ("running", host.Snapshot()));
            return Failed(name, watch, "active drag barrier was not reached before interrupt", failedMetrics);
        }

        var activeBarrier = barrier.Value;
        var interruptWhileDragActiveCount =
            activeBarrier.DragButtonDownCount > 0 &&
            activeBarrier.MoveEventCount > 0 &&
            activeBarrier.DragButtonUpCount == 0 &&
            activeBarrier.Snapshot.Runtime.Presses.OwnerKeys.Count > 0 &&
            activeBarrier.MacroFinishedCount == 0
                ? 1
                : 0;
        if (interruptWhileDragActiveCount != 1)
        {
            var failedMetrics = new Dictionary<string, object>
            {
                ["validation_scope"] = $"phase2d-b-xbutton2-triggered-drag-{interruptType}-during-active-drag",
                ["interrupt_type"] = interruptType,
                ["mapped_drag_button"] = mappedDragButton,
                ["trigger_received_count"] = activeBarrier.TriggerDownReceivedCount + activeBarrier.TriggerUpReceivedCount,
                ["pipeline_enqueued_count"] = activeBarrier.PipelineEnqueuedCount,
                ["runtime_dispatched_count"] = activeBarrier.RuntimeDispatchedCount,
                ["drag_started_count"] = activeBarrier.DragButtonDownCount,
                ["drag_button_down_count"] = activeBarrier.DragButtonDownCount,
                ["drag_button_up_count"] = activeBarrier.DragButtonUpCount,
                ["move_event_before_interrupt_count"] = activeBarrier.MoveEventCount,
                ["interrupt_while_drag_active_count"] = interruptWhileDragActiveCount,
                ["held_owner_count_after_cleanup"] = activeBarrier.Snapshot.Runtime.Presses.OwnerKeys.Count
            };
            AddSnapshots(failedMetrics, options, ("running", activeBarrier.Snapshot));
            return Failed(name, watch, "interrupt precondition did not prove an active drag in progress", failedMetrics);
        }

        var cursorAtInterrupt = (X: activeBarrier.CursorX, Y: activeBarrier.CursorY);
        var interruptWatch = Stopwatch.StartNew();
        if (disableInterrupt)
        {
            await host.DisableAsync(CancellationToken.None);
        }
        else
        {
            await host.StopAsync(CancellationToken.None);
        }
        interruptWatch.Stop();

        if (disableInterrupt &&
            !await WaitUntilAsync(
                () =>
                {
                    var snapshot = host.Snapshot();
                    return snapshot.Runtime.State == BAKeySmith.Core.Contracts.RuntimeState.Disabled &&
                        snapshot.Runtime.Presses.IsEmpty &&
                        snapshot.Runtime.PendingActionCount == 0 &&
                        snapshot.Runtime.RunningActionCount == 0;
                },
                options.DrainTimeout,
                CancellationToken.None))
        {
            var failedMetrics = new Dictionary<string, object>
            {
                ["validation_scope"] = $"phase2d-b-xbutton2-triggered-drag-{interruptType}-during-active-drag",
                ["interrupt_type"] = interruptType,
                ["mapped_drag_button"] = mappedDragButton,
                ["interrupt_while_drag_active_count"] = interruptWhileDragActiveCount,
                ["drag_button_down_count"] = activeBarrier.DragButtonDownCount,
                ["move_event_before_interrupt_count"] = activeBarrier.MoveEventCount,
                ["drag_button_up_from_cleanup_count"] = Math.Max(0, runtimeInput.Count("mouse", mappedDragButton, false) - activeBarrier.DragButtonUpCount),
                ["held_owner_count_after_cleanup"] = host.Snapshot().Runtime.Presses.OwnerKeys.Count
            };
            AddSnapshots(failedMetrics, options, ("running", activeBarrier.Snapshot), ("after_interrupt", host.Snapshot()));
            return Failed(name, watch, "disable during active drag did not settle to a clean disabled runtime state", failedMetrics);
        }

        var afterInterrupt = host.Snapshot();
        var dragButtonUpTotalCount = runtimeInput.Count("mouse", mappedDragButton, false);
        var moveEventTotalCount = MoveEvents(runtimeInput).Count;
        var eventsAfterInterruptCount = runtimeInput.Events.Count;
        var dragCompletedNormallyCount = activeBarrier.DragButtonUpCount;
        var dragButtonUpFromCleanupCount = Math.Max(0, dragButtonUpTotalCount - activeBarrier.DragButtonUpCount);
        var moveEventAfterInterruptCount = Math.Max(0, moveEventTotalCount - activeBarrier.MoveEventCount);
        var unexpectedOutputAfterInterruptCount = Math.Max(
            0,
            (eventsAfterInterruptCount - activeBarrier.TotalEventCount) - dragButtonUpFromCleanupCount);
        var cleanupCompletedCount = CountSessionEvent(diagnostics, "stop_completed");
        var heldOwnerCountAfterCleanup = afterInterrupt.Runtime.Presses.OwnerKeys.Count;
        var interruptRequestedCount = CountSessionEvent(diagnostics, "stop_requested");
        var macroFinishedCount = CountMacroEvent(diagnostics, "finished", $"{mappingId}:macro:");

        var stopped = afterInterrupt;
        if (disableInterrupt)
        {
            await host.StopAsync(CancellationToken.None);
            stopped = host.Snapshot();
        }

        var cursorEnd = await runtimeInput.GetMousePositionAsync(CancellationToken.None);
        cursorRestored = await TryRestoreCursorAsync(restoreInput, cursorStart.X, cursorStart.Y);

        var disableSettled =
            afterInterrupt.Runtime.State == BAKeySmith.Core.Contracts.RuntimeState.Disabled &&
            afterInterrupt.Runtime.Presses.IsEmpty &&
            afterInterrupt.Runtime.PendingActionCount == 0 &&
            afterInterrupt.Runtime.RunningActionCount == 0;
        var passed =
            activeBarrier.TriggerDownReceivedCount > 0 &&
            activeBarrier.PipelineEnqueuedCount == 1 &&
            activeBarrier.RuntimeDispatchedCount == 1 &&
            activeBarrier.DragButtonDownCount == 1 &&
            activeBarrier.MoveEventCount >= 1 &&
            interruptWhileDragActiveCount == 1 &&
            dragCompletedNormallyCount == 0 &&
            dragButtonUpFromCleanupCount == 1 &&
            moveEventAfterInterruptCount == 0 &&
            unexpectedOutputAfterInterruptCount == 0 &&
            cleanupCompletedCount == 1 &&
            heldOwnerCountAfterCleanup == 0 &&
            macroFinishedCount == 1 &&
            (!disableInterrupt ? IsCleanlyStopped(stopped) : disableSettled && IsCleanlyStopped(stopped));

        var metrics = new Dictionary<string, object>
        {
            ["validation_scope"] = $"phase2d-b-xbutton2-triggered-drag-{interruptType}-during-active-drag",
            ["interrupt_type"] = interruptType,
            ["mapped_drag_button"] = mappedDragButton,
            ["manual_interrupt_confirmed"] = false,
            ["trigger_received_count"] = activeBarrier.TriggerDownReceivedCount + activeBarrier.TriggerUpReceivedCount,
            ["trigger_down_received_count"] = activeBarrier.TriggerDownReceivedCount,
            ["trigger_up_received_count"] = activeBarrier.TriggerUpReceivedCount,
            ["pipeline_enqueued_count"] = activeBarrier.PipelineEnqueuedCount,
            ["runtime_dispatched_count"] = activeBarrier.RuntimeDispatchedCount,
            ["drag_started_count"] = activeBarrier.DragButtonDownCount,
            ["drag_button_down_count"] = activeBarrier.DragButtonDownCount,
            ["move_event_before_interrupt_count"] = activeBarrier.MoveEventCount,
            ["interrupt_requested_count"] = interruptRequestedCount,
            ["interrupt_while_drag_active_count"] = interruptWhileDragActiveCount,
            ["drag_completed_normally_count"] = dragCompletedNormallyCount,
            ["drag_button_up_count"] = dragButtonUpTotalCount,
            ["drag_button_up_from_cleanup_count"] = dragButtonUpFromCleanupCount,
            ["move_event_after_interrupt_count"] = moveEventAfterInterruptCount,
            ["unexpected_output_after_interrupt_count"] = unexpectedOutputAfterInterruptCount,
            ["cleanup_completed_count"] = cleanupCompletedCount,
            ["held_owner_count_after_cleanup"] = heldOwnerCountAfterCleanup,
            ["macro_started_count"] = activeBarrier.MacroStartedCount,
            ["macro_finished_count"] = macroFinishedCount,
            ["cursor_start_x"] = cursorStart.X,
            ["cursor_start_y"] = cursorStart.Y,
            ["cursor_interrupt_x"] = cursorAtInterrupt.X,
            ["cursor_interrupt_y"] = cursorAtInterrupt.Y,
            ["cursor_end_x"] = cursorEnd.X,
            ["cursor_end_y"] = cursorEnd.Y,
            ["cursor_restored"] = cursorRestored,
            ["move_total_dx"] = cursorEnd.X - cursorStart.X,
            ["move_total_dy"] = cursorEnd.Y - cursorStart.Y,
            ["interrupt_ms"] = interruptWatch.Elapsed.TotalMilliseconds,
            ["diagnostic_events"] = diagnostics.Count,
            ["stopped_clean"] = IsCleanlyStopped(stopped)
        };
        AddSnapshots(
            metrics,
            options,
            ("running", activeBarrier.Snapshot),
            ("after_interrupt", afterInterrupt),
            ("stopped", stopped));

        return passed
            ? Passed(name, watch, metrics)
            : Failed(name, watch, $"{interruptType} during active drag did not satisfy cleanup invariants", metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message);
    }
    finally
    {
        if (!cursorRestored && cursorCaptured)
        {
            _ = TryRestoreCursorAsync(restoreInput, cursorStart.X, cursorStart.Y);
        }

        if (host is not null)
        {
            await host.DisposeAsync();
        }

        CloseProcessWindow(target);
    }
}

static Task<AcceptanceScenarioResult> RunBlueArchiveManualXButton2DragStopDuringActiveDragAsync(AcceptanceOptions options)
{
    return RunBlueArchiveManualXButton2DragInterruptScenarioAsync(options, disableInterrupt: false);
}

static Task<AcceptanceScenarioResult> RunBlueArchiveManualXButton2DragDisableDuringActiveDragAsync(AcceptanceOptions options)
{
    return RunBlueArchiveManualXButton2DragInterruptScenarioAsync(options, disableInterrupt: true);
}

static Task<AcceptanceScenarioResult> RunXButton2TriggeredDragReloadDuringActiveDragOldTriggerBlockedAsync(AcceptanceOptions options)
{
    return RunXButton2TriggeredDragReloadDuringActiveDragScenarioAsync(options, verifyNewTrigger: false);
}

static Task<AcceptanceScenarioResult> RunXButton2TriggeredDragReloadDuringActiveDragNewTriggerAllowedAsync(AcceptanceOptions options)
{
    return RunXButton2TriggeredDragReloadDuringActiveDragScenarioAsync(options, verifyNewTrigger: true);
}

static async Task<AcceptanceScenarioResult> RunXButton2TriggeredDragReloadDuringActiveDragScenarioAsync(
    AcceptanceOptions options,
    bool verifyNewTrigger)
{
    var name = verifyNewTrigger
        ? "xbutton2-triggered-drag-reload-during-active-drag-new-trigger-allowed"
        : "xbutton2-triggered-drag-reload-during-active-drag-old-trigger-blocked";
    const string oldTrigger = "mouse_x2";
    const string oldTriggerKey = "mouse:mouse_x2";
    const string newTrigger = "mouse_x1";
    const string newTriggerKey = "mouse:mouse_x1";
    const string mappedDragButton = "mouse_middle";
    const string newMappedKey = "f18";
    var oldMappingId = verifyNewTrigger
        ? "xbutton2-drag-reload-active-new-trigger"
        : "xbutton2-drag-reload-active-old-trigger";
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, $"{name} requires --allow-live-input");
    }

    using var target = new LiveHarnessWindow(
        verifyNewTrigger
            ? "BA KeySmith XButton2 Drag Reload Target - new trigger"
            : "BA KeySmith XButton2 Drag Reload Target - old trigger");
    RuntimeHost? host = null;
    var restoreInput = new WindowsInputBackend();
    var cursorStart = (X: 0, Y: 0);
    var cursorCaptured = false;
    var cursorRestored = false;
    var detailMetrics = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

    string StageKey(string stage, string suffix) => $"stage_{stage}_{suffix}";

    void SetMetric(string key, object value) => detailMetrics[key] = value;

    void InitializeStage(string stage)
    {
        SetMetric(StageKey(stage, "entered"), false);
        SetMetric(StageKey(stage, "completed"), false);
        SetMetric(StageKey(stage, "failure_reason"), "not_run");
    }

    foreach (var stage in new[]
    {
        "active_drag_before_reload",
        "old_generation_cleanup",
        "old_trigger_after_reload",
        "new_trigger_after_reload"
    })
    {
        InitializeStage(stage);
    }

    Dictionary<string, object> CreateMetrics()
    {
        var metrics = new Dictionary<string, object>
        {
            ["validation_scope"] = verifyNewTrigger
                ? "phase2d-c-xbutton2-triggered-drag-reload-during-active-drag-new-trigger-allowed"
                : "phase2d-c-xbutton2-triggered-drag-reload-during-active-drag-old-trigger-blocked",
            ["old_trigger"] = oldTrigger,
            ["new_trigger"] = newTrigger,
            ["mapped_drag_button"] = mappedDragButton,
            ["new_mapped_output"] = newMappedKey
        };

        foreach (var (key, value) in detailMetrics)
        {
            metrics[key] = value;
        }

        return metrics;
    }

    void RecordActiveStage(
        int triggerReceivedCount,
        int pipelineEnqueuedCount,
        int runtimeDispatchedCount,
        int dragButtonDownCount,
        int dragButtonUpCount,
        int moveEventCount,
        int macroStartedCount,
        int macroFinishedCount,
        bool completed,
        string failureReason)
    {
        SetMetric(StageKey("active_drag_before_reload", "entered"), true);
        SetMetric(StageKey("active_drag_before_reload", "completed"), completed);
        SetMetric(StageKey("active_drag_before_reload", "trigger_received_count"), triggerReceivedCount);
        SetMetric(StageKey("active_drag_before_reload", "pipeline_enqueued_count"), pipelineEnqueuedCount);
        SetMetric(StageKey("active_drag_before_reload", "runtime_dispatched_count"), runtimeDispatchedCount);
        SetMetric(StageKey("active_drag_before_reload", "drag_button_down_count"), dragButtonDownCount);
        SetMetric(StageKey("active_drag_before_reload", "drag_button_up_count"), dragButtonUpCount);
        SetMetric(StageKey("active_drag_before_reload", "move_event_count"), moveEventCount);
        SetMetric(StageKey("active_drag_before_reload", "macro_started_count"), macroStartedCount);
        SetMetric(StageKey("active_drag_before_reload", "macro_finished_count"), macroFinishedCount);
        SetMetric(StageKey("active_drag_before_reload", "failure_reason"), failureReason);
    }

    void RecordCleanupStage(
        int reloadRequestedCount,
        long generationBefore,
        long generationAfter,
        int interruptWhileDragActiveCount,
        int dragCompletedNormallyCount,
        int dragButtonUpFromReloadCleanupCount,
        int moveEventAfterReloadCount,
        int unexpectedOutputAfterReloadCount,
        int cleanupCompletedCount,
        int heldOwnerCountAfterReloadCleanup,
        bool completed,
        string failureReason)
    {
        SetMetric(StageKey("old_generation_cleanup", "entered"), true);
        SetMetric(StageKey("old_generation_cleanup", "completed"), completed);
        SetMetric(StageKey("old_generation_cleanup", "reload_requested_count"), reloadRequestedCount);
        SetMetric(StageKey("old_generation_cleanup", "reload_generation_before"), generationBefore);
        SetMetric(StageKey("old_generation_cleanup", "reload_generation_after"), generationAfter);
        SetMetric(StageKey("old_generation_cleanup", "interrupt_while_drag_active_count"), interruptWhileDragActiveCount);
        SetMetric(StageKey("old_generation_cleanup", "drag_completed_normally_count"), dragCompletedNormallyCount);
        SetMetric(StageKey("old_generation_cleanup", "drag_button_up_from_reload_cleanup_count"), dragButtonUpFromReloadCleanupCount);
        SetMetric(StageKey("old_generation_cleanup", "move_event_after_reload_count"), moveEventAfterReloadCount);
        SetMetric(StageKey("old_generation_cleanup", "unexpected_output_after_reload_count"), unexpectedOutputAfterReloadCount);
        SetMetric(StageKey("old_generation_cleanup", "cleanup_completed_count"), cleanupCompletedCount);
        SetMetric(StageKey("old_generation_cleanup", "held_owner_count_after_reload_cleanup"), heldOwnerCountAfterReloadCleanup);
        SetMetric(StageKey("old_generation_cleanup", "failure_reason"), failureReason);
    }

    void RecordOldTriggerStage(
        int triggerReceivedCount,
        int pipelineCount,
        int runtimeCount,
        int outputDelta,
        int passThroughCount,
        long passThroughDown,
        long passThroughUp,
        bool completed,
        string failureReason)
    {
        SetMetric(StageKey("old_trigger_after_reload", "entered"), true);
        SetMetric(StageKey("old_trigger_after_reload", "completed"), completed);
        SetMetric(StageKey("old_trigger_after_reload", "received_count"), triggerReceivedCount);
        SetMetric(StageKey("old_trigger_after_reload", "pipeline_count"), pipelineCount);
        SetMetric(StageKey("old_trigger_after_reload", "runtime_count"), runtimeCount);
        SetMetric(StageKey("old_trigger_after_reload", "output_delta"), outputDelta);
        SetMetric(StageKey("old_trigger_after_reload", "pass_through_count"), passThroughCount);
        SetMetric(StageKey("old_trigger_after_reload", "pass_through_down"), passThroughDown);
        SetMetric(StageKey("old_trigger_after_reload", "pass_through_up"), passThroughUp);
        SetMetric(StageKey("old_trigger_after_reload", "failure_reason"), failureReason);
    }

    void RecordNewTriggerStage(
        int triggerReceivedCount,
        int pipelineCount,
        int runtimeCount,
        int outputDelta,
        long originalDownDelta,
        long originalUpDelta,
        bool completed,
        string failureReason)
    {
        SetMetric(StageKey("new_trigger_after_reload", "entered"), true);
        SetMetric(StageKey("new_trigger_after_reload", "completed"), completed);
        SetMetric(StageKey("new_trigger_after_reload", "received_count"), triggerReceivedCount);
        SetMetric(StageKey("new_trigger_after_reload", "pipeline_count"), pipelineCount);
        SetMetric(StageKey("new_trigger_after_reload", "runtime_count"), runtimeCount);
        SetMetric(StageKey("new_trigger_after_reload", "output_delta"), outputDelta);
        SetMetric(StageKey("new_trigger_after_reload", "original_down_delta"), originalDownDelta);
        SetMetric(StageKey("new_trigger_after_reload", "original_up_delta"), originalUpDelta);
        SetMetric(StageKey("new_trigger_after_reload", "failure_reason"), failureReason);
    }

    try
    {
        target.Start(TimeSpan.FromSeconds(3));
        var runtimeInput = new CountingInputBackend(new WindowsInputBackend());
        var triggerSender = new WindowsInputBackend(markInjected: false);
        var diagnostics = new CountingDiagnosticsSink();
        var targetProcess = CurrentProcessTargetName();

        host = new RuntimeHost(
            LiveMacroConfig(targetProcess, oldMappingId, oldTrigger, InterruptibleDragScript(mappedDragButton)),
            runtimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(diagnostics),
            diagnostics);

        await host.StartAsync(CancellationToken.None);
        if (!await target.FocusAsync(TimeSpan.FromSeconds(3)) ||
            !LiveWindowTools.MoveCursorToCenter(target.Handle))
        {
            return Failed(name, watch, "failed to focus reload-during-active-drag target and move cursor");
        }

        target.Reset();
        runtimeInput.Reset();
        diagnostics.Reset();
        cursorStart = await runtimeInput.GetMousePositionAsync(CancellationToken.None);
        cursorCaptured = true;

        await SendMouseClickAsync(triggerSender, "x2");
        var barrier = await WaitForActiveDragBarrierAsync(
            host,
            runtimeInput,
            diagnostics,
            oldTriggerKey,
            mappedDragButton,
            oldMappingId,
            options.DrainTimeout,
            CancellationToken.None);
        if (barrier is null)
        {
            RecordActiveStage(
                CountHookTriggerReceived(diagnostics, oldTriggerKey, "down") + CountHookTriggerReceived(diagnostics, oldTriggerKey, "up"),
                CountPipelineQueued(diagnostics, oldTriggerKey, "down"),
                CountRuntimeDispatched(diagnostics, oldTriggerKey, "down"),
                runtimeInput.Count("mouse", mappedDragButton, true),
                runtimeInput.Count("mouse", mappedDragButton, false),
                MoveEvents(runtimeInput).Count,
                CountMacroEvent(diagnostics, "started", $"{oldMappingId}:macro:"),
                CountMacroEvent(diagnostics, "finished", $"{oldMappingId}:macro:"),
                completed: false,
                failureReason: "active_drag_barrier_not_reached");
            return Failed(name, watch, "reload active-drag barrier was not reached", CreateMetrics());
        }

        var activeBarrier = barrier.Value;
        RecordActiveStage(
            activeBarrier.TriggerDownReceivedCount + activeBarrier.TriggerUpReceivedCount,
            activeBarrier.PipelineEnqueuedCount,
            activeBarrier.RuntimeDispatchedCount,
            activeBarrier.DragButtonDownCount,
            activeBarrier.DragButtonUpCount,
            activeBarrier.MoveEventCount,
            activeBarrier.MacroStartedCount,
            activeBarrier.MacroFinishedCount,
            completed: true,
            failureReason: string.Empty);

        var interruptWhileDragActiveCount =
            activeBarrier.DragButtonDownCount > 0 &&
            activeBarrier.MoveEventCount > 0 &&
            activeBarrier.DragButtonUpCount == 0 &&
            activeBarrier.Snapshot.Runtime.Presses.OwnerKeys.Count > 0 &&
            activeBarrier.MacroFinishedCount == 0
                ? 1
                : 0;
        if (interruptWhileDragActiveCount != 1)
        {
            RecordCleanupStage(
                reloadRequestedCount: 0,
                generationBefore: activeBarrier.Snapshot.Runtime.Generation,
                generationAfter: activeBarrier.Snapshot.Runtime.Generation,
                interruptWhileDragActiveCount,
                dragCompletedNormallyCount: activeBarrier.DragButtonUpCount,
                dragButtonUpFromReloadCleanupCount: 0,
                moveEventAfterReloadCount: 0,
                unexpectedOutputAfterReloadCount: 0,
                cleanupCompletedCount: 0,
                heldOwnerCountAfterReloadCleanup: activeBarrier.Snapshot.Runtime.Presses.OwnerKeys.Count,
                completed: false,
                failureReason: "reload_not_requested_while_drag_active");
            return Failed(name, watch, "reload precondition did not prove an active drag in progress", CreateMetrics());
        }

        var generationBefore = activeBarrier.Snapshot.Runtime.Generation;
        await host.ReloadAsync(LiveTapConfig(targetProcess, newTrigger, newMappedKey), CancellationToken.None);
        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            RecordCleanupStage(
                reloadRequestedCount: 1,
                generationBefore,
                generationAfter: host.Snapshot().Runtime.Generation,
                interruptWhileDragActiveCount,
                dragCompletedNormallyCount: activeBarrier.DragButtonUpCount,
                dragButtonUpFromReloadCleanupCount: Math.Max(0, runtimeInput.Count("mouse", mappedDragButton, false) - activeBarrier.DragButtonUpCount),
                moveEventAfterReloadCount: Math.Max(0, MoveEvents(runtimeInput).Count - activeBarrier.MoveEventCount),
                unexpectedOutputAfterReloadCount: 0,
                cleanupCompletedCount: CountSessionEvent(diagnostics, "stop_completed"),
                heldOwnerCountAfterReloadCleanup: host.Snapshot().Runtime.Presses.OwnerKeys.Count,
                completed: false,
                failureReason: "drain_timeout_after_reload");
            return Failed(name, watch, "queues did not drain after reload during active drag", CreateMetrics());
        }

        var afterReload = host.Snapshot();
        var dragButtonUpTotalCount = runtimeInput.Count("mouse", mappedDragButton, false);
        var moveEventTotalCount = MoveEvents(runtimeInput).Count;
        var eventsAfterReloadCount = runtimeInput.Events.Count;
        var dragCompletedNormallyCount = activeBarrier.DragButtonUpCount;
        var dragButtonUpFromReloadCleanupCount = Math.Max(0, dragButtonUpTotalCount - activeBarrier.DragButtonUpCount);
        var moveEventAfterReloadCount = Math.Max(0, moveEventTotalCount - activeBarrier.MoveEventCount);
        var unexpectedOutputAfterReloadCount = Math.Max(
            0,
            (eventsAfterReloadCount - activeBarrier.TotalEventCount) - dragButtonUpFromReloadCleanupCount);
        var oldGenerationCleanupCompletedCount = CountSessionEvent(diagnostics, "stop_completed");
        var heldOwnerCountAfterReloadCleanup = afterReload.Runtime.Presses.OwnerKeys.Count;
        var reloadRequestedCount = CountDiagnosticEvent(diagnostics, "host", "reloaded");
        var generationAfter = afterReload.Runtime.Generation;
        var oldMacroFinishedCount = CountMacroEvent(diagnostics, "finished", $"{oldMappingId}:macro:");

        RecordCleanupStage(
            reloadRequestedCount,
            generationBefore,
            generationAfter,
            interruptWhileDragActiveCount,
            dragCompletedNormallyCount,
            dragButtonUpFromReloadCleanupCount,
            moveEventAfterReloadCount,
            unexpectedOutputAfterReloadCount,
            oldGenerationCleanupCompletedCount,
            heldOwnerCountAfterReloadCleanup,
            completed: true,
            failureReason: string.Empty);

        if (dragCompletedNormallyCount != 0 ||
            interruptWhileDragActiveCount != 1 ||
            reloadRequestedCount != 1 ||
            generationAfter <= generationBefore ||
            dragButtonUpFromReloadCleanupCount != 1 ||
            moveEventAfterReloadCount != 0 ||
            unexpectedOutputAfterReloadCount != 0 ||
            oldGenerationCleanupCompletedCount != 1 ||
            heldOwnerCountAfterReloadCleanup != 0 ||
            oldMacroFinishedCount != 1)
        {
            return Failed(name, watch, "reload during active drag did not satisfy cleanup invariants", CreateMetrics());
        }

        target.Reset();
        runtimeInput.Reset();
        diagnostics.Reset();
        if (verifyNewTrigger)
        {
            await SendMouseClickAsync(triggerSender, "x1");
            if (!await WaitForInputCountAsync(runtimeInput, "key", newMappedKey, 2, options.DrainTimeout))
            {
                RecordNewTriggerStage(
                    CountHookTriggerReceived(diagnostics, newTriggerKey, "down") + CountHookTriggerReceived(diagnostics, newTriggerKey, "up"),
                    CountPipelineQueued(diagnostics, newTriggerKey, "down"),
                    CountRuntimeDispatched(diagnostics, newTriggerKey, "down"),
                    runtimeInput.Count("key", newMappedKey),
                    target.XButton1DownCount,
                    target.XButton1UpCount,
                    completed: false,
                    failureReason: "awaiting_mapped_output");
                return Failed(name, watch, "new trigger did not produce mapped output after reload during active drag", CreateMetrics());
            }

            if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
            {
                RecordNewTriggerStage(
                    CountHookTriggerReceived(diagnostics, newTriggerKey, "down") + CountHookTriggerReceived(diagnostics, newTriggerKey, "up"),
                    CountPipelineQueued(diagnostics, newTriggerKey, "down"),
                    CountRuntimeDispatched(diagnostics, newTriggerKey, "down"),
                    runtimeInput.Count("key", newMappedKey),
                    target.XButton1DownCount,
                    target.XButton1UpCount,
                    completed: false,
                    failureReason: "drain_timeout");
                return Failed(name, watch, "queues did not drain after new trigger probe", CreateMetrics());
            }

            var newTriggerReceivedCount = CountHookTriggerReceived(diagnostics, newTriggerKey, "down") + CountHookTriggerReceived(diagnostics, newTriggerKey, "up");
            var newTriggerPipelineCount = CountPipelineQueued(diagnostics, newTriggerKey, "down");
            var newTriggerRuntimeCount = CountRuntimeDispatched(diagnostics, newTriggerKey, "down");
            var newTriggerOutputDelta = runtimeInput.Count("key", newMappedKey);
            var newTriggerOriginalDownDelta = target.XButton1DownCount;
            var newTriggerOriginalUpDelta = target.XButton1UpCount;
            RecordNewTriggerStage(
                newTriggerReceivedCount,
                newTriggerPipelineCount,
                newTriggerRuntimeCount,
                newTriggerOutputDelta,
                newTriggerOriginalDownDelta,
                newTriggerOriginalUpDelta,
                completed: true,
                failureReason: string.Empty);

            if (newTriggerReceivedCount == 0 ||
                newTriggerPipelineCount != 1 ||
                newTriggerRuntimeCount != 1 ||
                newTriggerOutputDelta != 2 ||
                newTriggerOriginalDownDelta != 0 ||
                newTriggerOriginalUpDelta != 0)
            {
                return Failed(name, watch, "new trigger did not cleanly take over after reload during active drag", CreateMetrics());
            }
        }
        else
        {
            await SendMouseClickAsync(triggerSender, "x2");
            await Task.Delay(150, CancellationToken.None);
            if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
            {
                RecordOldTriggerStage(
                    CountHookTriggerReceived(diagnostics, oldTriggerKey, "down") + CountHookTriggerReceived(diagnostics, oldTriggerKey, "up"),
                    CountPipelineQueued(diagnostics, oldTriggerKey, "down"),
                    CountRuntimeDispatched(diagnostics, oldTriggerKey, "down"),
                    runtimeInput.Events.Count,
                    target.XButton2DownCount > 0 && target.XButton2UpCount > 0 ? 1 : 0,
                    target.XButton2DownCount,
                    target.XButton2UpCount,
                    completed: false,
                    failureReason: "drain_timeout");
                return Failed(name, watch, "queues did not drain after old trigger probe", CreateMetrics());
            }

            var oldTriggerReceivedCount = CountHookTriggerReceived(diagnostics, oldTriggerKey, "down") + CountHookTriggerReceived(diagnostics, oldTriggerKey, "up");
            var oldTriggerPipelineCount = CountPipelineQueued(diagnostics, oldTriggerKey, "down");
            var oldTriggerRuntimeCount = CountRuntimeDispatched(diagnostics, oldTriggerKey, "down");
            var oldTriggerOutputDelta = runtimeInput.Events.Count;
            var oldTriggerPassThroughDown = target.XButton2DownCount;
            var oldTriggerPassThroughUp = target.XButton2UpCount;
            var oldTriggerPassThroughCount =
                oldTriggerPassThroughDown > 0 && oldTriggerPassThroughUp > 0
                    ? 1
                    : 0;
            RecordOldTriggerStage(
                oldTriggerReceivedCount,
                oldTriggerPipelineCount,
                oldTriggerRuntimeCount,
                oldTriggerOutputDelta,
                oldTriggerPassThroughCount,
                oldTriggerPassThroughDown,
                oldTriggerPassThroughUp,
                completed: true,
                failureReason: string.Empty);

            if (oldTriggerPipelineCount != 0 ||
                oldTriggerRuntimeCount != 0 ||
                oldTriggerOutputDelta != 0 ||
                oldTriggerPassThroughCount != 1)
            {
                return Failed(name, watch, "old trigger did not cleanly retire after reload during active drag", CreateMetrics());
            }
        }

        var cursorEnd = await runtimeInput.GetMousePositionAsync(CancellationToken.None);
        cursorRestored = await TryRestoreCursorAsync(restoreInput, cursorStart.X, cursorStart.Y);

        await host.StopAsync(CancellationToken.None);
        var stopped = host.Snapshot();
        if (!IsCleanlyStopped(stopped))
        {
            return Failed(name, watch, "reload-during-active-drag runtime did not stop cleanly", CreateMetrics());
        }

        var metrics = CreateMetrics();
        metrics["trigger_received_count"] = activeBarrier.TriggerDownReceivedCount + activeBarrier.TriggerUpReceivedCount;
        metrics["pipeline_enqueued_count"] = activeBarrier.PipelineEnqueuedCount;
        metrics["runtime_dispatched_count"] = activeBarrier.RuntimeDispatchedCount;
        metrics["drag_started_count"] = activeBarrier.DragButtonDownCount;
        metrics["drag_button_down_count"] = activeBarrier.DragButtonDownCount;
        metrics["move_event_before_reload_count"] = activeBarrier.MoveEventCount;
        metrics["reload_requested_count"] = reloadRequestedCount;
        metrics["reload_generation_before"] = generationBefore;
        metrics["reload_generation_after"] = generationAfter;
        metrics["interrupt_while_drag_active_count"] = interruptWhileDragActiveCount;
        metrics["drag_completed_normally_count"] = dragCompletedNormallyCount;
        metrics["drag_button_up_count"] = dragButtonUpTotalCount;
        metrics["drag_button_up_from_reload_cleanup_count"] = dragButtonUpFromReloadCleanupCount;
        metrics["move_event_after_reload_count"] = moveEventAfterReloadCount;
        metrics["unexpected_output_after_reload_count"] = unexpectedOutputAfterReloadCount;
        metrics["old_generation_cleanup_completed_count"] = oldGenerationCleanupCompletedCount;
        metrics["held_owner_count_after_reload_cleanup"] = heldOwnerCountAfterReloadCleanup;
        metrics["old_trigger_after_reload_received_count"] = verifyNewTrigger ? 0 : detailMetrics[StageKey("old_trigger_after_reload", "received_count")];
        metrics["old_trigger_after_reload_pipeline_count"] = verifyNewTrigger ? 0 : detailMetrics[StageKey("old_trigger_after_reload", "pipeline_count")];
        metrics["old_trigger_after_reload_runtime_count"] = verifyNewTrigger ? 0 : detailMetrics[StageKey("old_trigger_after_reload", "runtime_count")];
        metrics["old_trigger_after_reload_output_delta"] = verifyNewTrigger ? 0 : detailMetrics[StageKey("old_trigger_after_reload", "output_delta")];
        metrics["old_trigger_after_reload_pass_through_count"] = verifyNewTrigger ? 0 : detailMetrics[StageKey("old_trigger_after_reload", "pass_through_count")];
        metrics["new_trigger_after_reload_received_count"] = verifyNewTrigger ? detailMetrics[StageKey("new_trigger_after_reload", "received_count")] : 0;
        metrics["new_trigger_after_reload_pipeline_count"] = verifyNewTrigger ? detailMetrics[StageKey("new_trigger_after_reload", "pipeline_count")] : 0;
        metrics["new_trigger_after_reload_runtime_count"] = verifyNewTrigger ? detailMetrics[StageKey("new_trigger_after_reload", "runtime_count")] : 0;
        metrics["new_trigger_after_reload_output_delta"] = verifyNewTrigger ? detailMetrics[StageKey("new_trigger_after_reload", "output_delta")] : 0;
        metrics["cursor_start_x"] = cursorStart.X;
        metrics["cursor_start_y"] = cursorStart.Y;
        metrics["cursor_end_x"] = cursorEnd.X;
        metrics["cursor_end_y"] = cursorEnd.Y;
        metrics["cursor_restored"] = cursorRestored;
        metrics["move_total_dx"] = cursorEnd.X - cursorStart.X;
        metrics["move_total_dy"] = cursorEnd.Y - cursorStart.Y;
        metrics["diagnostic_events"] = diagnostics.Count;
        metrics["stopped_clean"] = IsCleanlyStopped(stopped);
        AddSnapshots(metrics, options, ("active_drag", activeBarrier.Snapshot), ("after_reload", afterReload), ("stopped", stopped));
        return Passed(name, watch, metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message, CreateMetrics());
    }
    finally
    {
        if (!cursorRestored && cursorCaptured)
        {
            _ = TryRestoreCursorAsync(restoreInput, cursorStart.X, cursorStart.Y);
        }

        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}

static Task<AcceptanceScenarioResult> RunBlueArchiveManualXButton2DragReloadDuringActiveDragOldTriggerBlockedAsync(AcceptanceOptions options)
{
    return RunBlueArchiveManualXButton2DragReloadDuringActiveDragScenarioAsync(options, verifyNewTrigger: false);
}

static Task<AcceptanceScenarioResult> RunBlueArchiveManualXButton2DragReloadDuringActiveDragNewTriggerAllowedAsync(AcceptanceOptions options)
{
    return RunBlueArchiveManualXButton2DragReloadDuringActiveDragScenarioAsync(options, verifyNewTrigger: true);
}

static async Task<AcceptanceScenarioResult> RunBlueArchiveManualXButton2DragReloadDuringActiveDragScenarioAsync(
    AcceptanceOptions options,
    bool verifyNewTrigger)
{
    var name = verifyNewTrigger
        ? "bluearchive-manual-xbutton2-drag-reload-during-active-drag-new-trigger-allowed"
        : "bluearchive-manual-xbutton2-drag-reload-during-active-drag-old-trigger-blocked";
    const string oldTrigger = "mouse_x2";
    const string oldTriggerKey = "mouse:mouse_x2";
    const string newTrigger = "mouse_x1";
    const string newTriggerKey = "mouse:mouse_x1";
    const string mappedDragButton = "mouse_middle";
    var oldMappingId = verifyNewTrigger
        ? "xbutton2-bluearchive-drag-reload-active-new-trigger"
        : "xbutton2-bluearchive-drag-reload-active-old-trigger";
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, $"{name} requires --allow-live-input");
    }

    var targetProcess = options.TargetProcess;
    var targetWindow = FindTargetProcessWithWindow(targetProcess);
    if (targetWindow is null)
    {
        return Failed(name, watch, $"target process with foreground-capable window was not found: {targetProcess}");
    }

    var manualTrigger = KeyNameResolver.NormalizeMouseButton(options.ManualXButtonTrigger);
    var manualReloadTrigger = KeyNameResolver.NormalizeMouseButton(options.ManualXButtonReloadTrigger);
    if (!string.Equals(manualTrigger, oldTrigger, StringComparison.OrdinalIgnoreCase))
    {
        return Failed(name, watch, $"{name} requires manual xbutton trigger mouse_x2, but was: {manualTrigger}");
    }

    if (!string.Equals(manualReloadTrigger, newTrigger, StringComparison.OrdinalIgnoreCase))
    {
        return Failed(name, watch, $"{name} requires manual reload xbutton trigger mouse_x1, but was: {manualReloadTrigger}");
    }

    var runnerElevation = ProbeCurrentProcessElevation();
    var targetElevation = ProbeProcessElevation(targetWindow.Id, $"{targetWindow.ProcessName}.exe");
    var detailMetrics = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
    {
        ["manual_interrupt_confirmed"] = false,
        ["new_trigger_after_reload_manual_confirmed"] = false,
        ["mapped_drag_button"] = mappedDragButton
    };

    string StageKey(string stage, string suffix) => $"stage_{stage}_{suffix}";
    void SetMetric(string key, object value) => detailMetrics[key] = value;
    void InitializeStage(string stage)
    {
        SetMetric(StageKey(stage, "entered"), false);
        SetMetric(StageKey(stage, "completed"), false);
        SetMetric(StageKey(stage, "failure_reason"), "not_run");
    }

    foreach (var stage in new[]
    {
        "active_drag_before_reload",
        "old_generation_cleanup",
        "old_trigger_after_reload",
        "new_trigger_after_reload"
    })
    {
        InitializeStage(stage);
    }

    Dictionary<string, object> CreateMetrics()
    {
        var metrics = new Dictionary<string, object>
        {
            ["target_process"] = targetProcess,
            ["target_process_id"] = targetWindow.Id,
            ["target_window_title"] = targetWindow.MainWindowTitle,
            ["validation_scope"] = verifyNewTrigger
                ? "phase2d-c-bluearchive-xbutton2-drag-reload-during-active-drag-new-trigger-allowed"
                : "phase2d-c-bluearchive-xbutton2-drag-reload-during-active-drag-old-trigger-blocked",
            ["manual_xbutton_trigger"] = oldTrigger,
            ["manual_xbutton_reload_trigger"] = newTrigger,
            ["mapped_drag_button"] = mappedDragButton,
            ["runner_elevation_known"] = runnerElevation.IsKnown,
            ["runner_elevated"] = runnerElevation.IsKnown && runnerElevation.IsElevated,
            ["runner_elevated_state"] = ElevationStateLabel(runnerElevation),
            ["target_elevation_known"] = targetElevation.IsKnown,
            ["target_elevated"] = targetElevation.IsKnown && targetElevation.IsElevated,
            ["target_elevated_state"] = ElevationStateLabel(targetElevation),
            ["runner_target_same_elevation"] = runnerElevation.IsKnown &&
                targetElevation.IsKnown &&
                runnerElevation.IsElevated == targetElevation.IsElevated
        };

        foreach (var (key, value) in detailMetrics)
        {
            metrics[key] = value;
        }

        return metrics;
    }

    void RecordActiveStage(
        int triggerReceivedCount,
        int pipelineEnqueuedCount,
        int runtimeDispatchedCount,
        int dragButtonDownCount,
        int dragButtonUpCount,
        int moveEventCount,
        int macroStartedCount,
        int macroFinishedCount,
        bool completed,
        string failureReason)
    {
        SetMetric(StageKey("active_drag_before_reload", "entered"), true);
        SetMetric(StageKey("active_drag_before_reload", "completed"), completed);
        SetMetric(StageKey("active_drag_before_reload", "trigger_received_count"), triggerReceivedCount);
        SetMetric(StageKey("active_drag_before_reload", "pipeline_enqueued_count"), pipelineEnqueuedCount);
        SetMetric(StageKey("active_drag_before_reload", "runtime_dispatched_count"), runtimeDispatchedCount);
        SetMetric(StageKey("active_drag_before_reload", "drag_button_down_count"), dragButtonDownCount);
        SetMetric(StageKey("active_drag_before_reload", "drag_button_up_count"), dragButtonUpCount);
        SetMetric(StageKey("active_drag_before_reload", "move_event_count"), moveEventCount);
        SetMetric(StageKey("active_drag_before_reload", "macro_started_count"), macroStartedCount);
        SetMetric(StageKey("active_drag_before_reload", "macro_finished_count"), macroFinishedCount);
        SetMetric(StageKey("active_drag_before_reload", "failure_reason"), failureReason);
    }

    void RecordCleanupStage(
        int reloadRequestedCount,
        long generationBefore,
        long generationAfter,
        int interruptWhileDragActiveCount,
        int dragCompletedNormallyCount,
        int dragButtonUpFromReloadCleanupCount,
        int moveEventAfterReloadCount,
        int unexpectedOutputAfterReloadCount,
        int cleanupCompletedCount,
        int heldOwnerCountAfterReloadCleanup,
        bool completed,
        string failureReason)
    {
        SetMetric(StageKey("old_generation_cleanup", "entered"), true);
        SetMetric(StageKey("old_generation_cleanup", "completed"), completed);
        SetMetric(StageKey("old_generation_cleanup", "reload_requested_count"), reloadRequestedCount);
        SetMetric(StageKey("old_generation_cleanup", "reload_generation_before"), generationBefore);
        SetMetric(StageKey("old_generation_cleanup", "reload_generation_after"), generationAfter);
        SetMetric(StageKey("old_generation_cleanup", "interrupt_while_drag_active_count"), interruptWhileDragActiveCount);
        SetMetric(StageKey("old_generation_cleanup", "drag_completed_normally_count"), dragCompletedNormallyCount);
        SetMetric(StageKey("old_generation_cleanup", "drag_button_up_from_reload_cleanup_count"), dragButtonUpFromReloadCleanupCount);
        SetMetric(StageKey("old_generation_cleanup", "move_event_after_reload_count"), moveEventAfterReloadCount);
        SetMetric(StageKey("old_generation_cleanup", "unexpected_output_after_reload_count"), unexpectedOutputAfterReloadCount);
        SetMetric(StageKey("old_generation_cleanup", "cleanup_completed_count"), cleanupCompletedCount);
        SetMetric(StageKey("old_generation_cleanup", "held_owner_count_after_reload_cleanup"), heldOwnerCountAfterReloadCleanup);
        SetMetric(StageKey("old_generation_cleanup", "failure_reason"), failureReason);
    }

    void RecordOldTriggerStage(
        int triggerReceivedCount,
        int pipelineCount,
        int runtimeCount,
        int outputDelta,
        int passThroughCount,
        long passThroughDown,
        long passThroughUp,
        bool completed,
        string failureReason)
    {
        SetMetric(StageKey("old_trigger_after_reload", "entered"), true);
        SetMetric(StageKey("old_trigger_after_reload", "completed"), completed);
        SetMetric(StageKey("old_trigger_after_reload", "received_count"), triggerReceivedCount);
        SetMetric(StageKey("old_trigger_after_reload", "pipeline_count"), pipelineCount);
        SetMetric(StageKey("old_trigger_after_reload", "runtime_count"), runtimeCount);
        SetMetric(StageKey("old_trigger_after_reload", "output_delta"), outputDelta);
        SetMetric(StageKey("old_trigger_after_reload", "pass_through_count"), passThroughCount);
        SetMetric(StageKey("old_trigger_after_reload", "pass_through_down"), passThroughDown);
        SetMetric(StageKey("old_trigger_after_reload", "pass_through_up"), passThroughUp);
        SetMetric(StageKey("old_trigger_after_reload", "failure_reason"), failureReason);
    }

    void RecordNewTriggerStage(
        int triggerReceivedCount,
        int pipelineCount,
        int runtimeCount,
        int outputDelta,
        bool manualConfirmed,
        bool completed,
        string failureReason)
    {
        SetMetric(StageKey("new_trigger_after_reload", "entered"), true);
        SetMetric(StageKey("new_trigger_after_reload", "completed"), completed);
        SetMetric(StageKey("new_trigger_after_reload", "received_count"), triggerReceivedCount);
        SetMetric(StageKey("new_trigger_after_reload", "pipeline_count"), pipelineCount);
        SetMetric(StageKey("new_trigger_after_reload", "runtime_count"), runtimeCount);
        SetMetric(StageKey("new_trigger_after_reload", "output_delta"), outputDelta);
        SetMetric(StageKey("new_trigger_after_reload", "manual_confirmed"), manualConfirmed);
        SetMetric(StageKey("new_trigger_after_reload", "failure_reason"), failureReason);
    }

    if (!runnerElevation.IsKnown || !runnerElevation.IsElevated)
    {
        return Failed(
            name,
            watch,
            $"{name} requires an elevated acceptance/headless/live runner. Current runner is not elevated, so this real-target validation is invalid.",
            CreateMetrics());
    }

    using var blocker = new LiveHarnessWindow("BA KeySmith Real Target XButton Drag Reload Blocker");
    RuntimeHost? host = null;
    var restoreInput = new WindowsInputBackend();
    var cursorStart = (X: 0, Y: 0);
    var cursorCaptured = false;
    var cursorRestored = false;
    try
    {
        blocker.Start(TimeSpan.FromSeconds(3));
        var runtimeInput = new CountingInputBackend(new WindowsInputBackend());
        var diagnostics = new CountingDiagnosticsSink();

        host = new RuntimeHost(
            LiveMacroConfig(targetProcess, oldMappingId, oldTrigger, InterruptibleDragScript(mappedDragButton)),
            runtimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(diagnostics),
            diagnostics);

        await host.StartAsync(CancellationToken.None);
        targetWindow.Refresh();
        if (!await LiveWindowTools.FocusAsync(targetWindow.MainWindowHandle, TimeSpan.FromSeconds(5)) ||
            !LiveWindowTools.MoveCursorToCenter(targetWindow.MainWindowHandle))
        {
            return Failed(name, watch, $"failed to focus target window and move cursor: {targetWindow.ProcessName}", CreateMetrics());
        }

        runtimeInput.Reset();
        diagnostics.Reset();
        cursorStart = await runtimeInput.GetMousePositionAsync(CancellationToken.None);
        cursorCaptured = true;
        Console.Error.WriteLine(
            $"MANUAL xbutton2-drag-reload-{(verifyNewTrigger ? "new" : "old")}: Blue Archive focused. Move the cursor to a safe non-destructive area, then press '{oldTrigger}' once within {options.ManualTimeout.TotalSeconds:0}s. Expect a short mapped drag to begin, reload to interrupt it, and cleanup to release the held drag button.");

        var barrier = await WaitForActiveDragBarrierAsync(
            host,
            runtimeInput,
            diagnostics,
            oldTriggerKey,
            mappedDragButton,
            oldMappingId,
            options.ManualTimeout,
            CancellationToken.None);
        if (barrier is null)
        {
            RecordActiveStage(
                CountHookTriggerReceived(diagnostics, oldTriggerKey, "down") + CountHookTriggerReceived(diagnostics, oldTriggerKey, "up"),
                CountPipelineQueued(diagnostics, oldTriggerKey, "down"),
                CountRuntimeDispatched(diagnostics, oldTriggerKey, "down"),
                runtimeInput.Count("mouse", mappedDragButton, true),
                runtimeInput.Count("mouse", mappedDragButton, false),
                MoveEvents(runtimeInput).Count,
                CountMacroEvent(diagnostics, "started", $"{oldMappingId}:macro:"),
                CountMacroEvent(diagnostics, "finished", $"{oldMappingId}:macro:"),
                completed: false,
                failureReason: "active_drag_barrier_not_reached");
            return Failed(name, watch, "reload active-drag barrier was not reached in real target", CreateMetrics());
        }

        var activeBarrier = barrier.Value;
        RecordActiveStage(
            activeBarrier.TriggerDownReceivedCount + activeBarrier.TriggerUpReceivedCount,
            activeBarrier.PipelineEnqueuedCount,
            activeBarrier.RuntimeDispatchedCount,
            activeBarrier.DragButtonDownCount,
            activeBarrier.DragButtonUpCount,
            activeBarrier.MoveEventCount,
            activeBarrier.MacroStartedCount,
            activeBarrier.MacroFinishedCount,
            completed: true,
            failureReason: string.Empty);

        var interruptWhileDragActiveCount =
            activeBarrier.DragButtonDownCount > 0 &&
            activeBarrier.MoveEventCount > 0 &&
            activeBarrier.DragButtonUpCount == 0 &&
            activeBarrier.Snapshot.Runtime.Presses.OwnerKeys.Count > 0 &&
            activeBarrier.MacroFinishedCount == 0
                ? 1
                : 0;
        if (interruptWhileDragActiveCount != 1)
        {
            RecordCleanupStage(
                reloadRequestedCount: 0,
                generationBefore: activeBarrier.Snapshot.Runtime.Generation,
                generationAfter: activeBarrier.Snapshot.Runtime.Generation,
                interruptWhileDragActiveCount,
                dragCompletedNormallyCount: activeBarrier.DragButtonUpCount,
                dragButtonUpFromReloadCleanupCount: 0,
                moveEventAfterReloadCount: 0,
                unexpectedOutputAfterReloadCount: 0,
                cleanupCompletedCount: 0,
                heldOwnerCountAfterReloadCleanup: activeBarrier.Snapshot.Runtime.Presses.OwnerKeys.Count,
                completed: false,
                failureReason: "reload_not_requested_while_drag_active");
            return Failed(name, watch, "reload precondition did not prove an active drag in real target", CreateMetrics());
        }

        var generationBefore = activeBarrier.Snapshot.Runtime.Generation;
        await host.ReloadAsync(LiveEscTapConfig(targetProcess, newTrigger), CancellationToken.None);
        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            RecordCleanupStage(
                reloadRequestedCount: 1,
                generationBefore,
                generationAfter: host.Snapshot().Runtime.Generation,
                interruptWhileDragActiveCount,
                dragCompletedNormallyCount: activeBarrier.DragButtonUpCount,
                dragButtonUpFromReloadCleanupCount: Math.Max(0, runtimeInput.Count("mouse", mappedDragButton, false) - activeBarrier.DragButtonUpCount),
                moveEventAfterReloadCount: Math.Max(0, MoveEvents(runtimeInput).Count - activeBarrier.MoveEventCount),
                unexpectedOutputAfterReloadCount: 0,
                cleanupCompletedCount: CountSessionEvent(diagnostics, "stop_completed"),
                heldOwnerCountAfterReloadCleanup: host.Snapshot().Runtime.Presses.OwnerKeys.Count,
                completed: false,
                failureReason: "drain_timeout_after_reload");
            return Failed(name, watch, "queues did not drain after reload during active drag in real target", CreateMetrics());
        }

        var afterReload = host.Snapshot();
        var dragButtonUpTotalCount = runtimeInput.Count("mouse", mappedDragButton, false);
        var moveEventTotalCount = MoveEvents(runtimeInput).Count;
        var eventsAfterReloadCount = runtimeInput.Events.Count;
        var dragCompletedNormallyCount = activeBarrier.DragButtonUpCount;
        var dragButtonUpFromReloadCleanupCount = Math.Max(0, dragButtonUpTotalCount - activeBarrier.DragButtonUpCount);
        var moveEventAfterReloadCount = Math.Max(0, moveEventTotalCount - activeBarrier.MoveEventCount);
        var unexpectedOutputAfterReloadCount = Math.Max(
            0,
            (eventsAfterReloadCount - activeBarrier.TotalEventCount) - dragButtonUpFromReloadCleanupCount);
        var oldGenerationCleanupCompletedCount = CountSessionEvent(diagnostics, "stop_completed");
        var heldOwnerCountAfterReloadCleanup = afterReload.Runtime.Presses.OwnerKeys.Count;
        var reloadRequestedCount = CountDiagnosticEvent(diagnostics, "host", "reloaded");
        var generationAfter = afterReload.Runtime.Generation;
        var oldMacroFinishedCount = CountMacroEvent(diagnostics, "finished", $"{oldMappingId}:macro:");

        RecordCleanupStage(
            reloadRequestedCount,
            generationBefore,
            generationAfter,
            interruptWhileDragActiveCount,
            dragCompletedNormallyCount,
            dragButtonUpFromReloadCleanupCount,
            moveEventAfterReloadCount,
            unexpectedOutputAfterReloadCount,
            oldGenerationCleanupCompletedCount,
            heldOwnerCountAfterReloadCleanup,
            completed: true,
            failureReason: string.Empty);

        if (dragCompletedNormallyCount != 0 ||
            interruptWhileDragActiveCount != 1 ||
            reloadRequestedCount != 1 ||
            generationAfter <= generationBefore ||
            dragButtonUpFromReloadCleanupCount != 1 ||
            moveEventAfterReloadCount != 0 ||
            unexpectedOutputAfterReloadCount != 0 ||
            oldGenerationCleanupCompletedCount != 1 ||
            heldOwnerCountAfterReloadCleanup != 0 ||
            oldMacroFinishedCount != 1)
        {
            return Failed(name, watch, "reload during active drag did not satisfy cleanup invariants in real target", CreateMetrics());
        }

        if (verifyNewTrigger)
        {
            targetWindow.Refresh();
            if (!await LiveWindowTools.FocusAsync(targetWindow.MainWindowHandle, TimeSpan.FromSeconds(5)) ||
                !LiveWindowTools.MoveCursorToCenter(targetWindow.MainWindowHandle))
            {
                return Failed(name, watch, "failed to refocus Blue Archive for new trigger after reload", CreateMetrics());
            }

            runtimeInput.Reset();
            diagnostics.Reset();
            Console.Error.WriteLine(
                $"MANUAL xbutton2-drag-reload-new-trigger: Blue Archive focused. Physically press '{newTrigger}' once within {options.ManualTimeout.TotalSeconds:0}s. Expect the new trigger to produce mapped 'escape' output.");
            if (!await WaitForInputCountAsync(runtimeInput, "key", "escape", 2, options.ManualTimeout))
            {
                RecordNewTriggerStage(
                    CountHookTriggerReceived(diagnostics, newTriggerKey, "down") + CountHookTriggerReceived(diagnostics, newTriggerKey, "up"),
                    CountPipelineQueued(diagnostics, newTriggerKey, "down"),
                    CountRuntimeDispatched(diagnostics, newTriggerKey, "down"),
                    runtimeInput.Count("key", "escape"),
                    manualConfirmed: false,
                    completed: false,
                    failureReason: "awaiting_mapped_output");
                return Failed(name, watch, "new trigger did not produce mapped output after reload in real target", CreateMetrics());
            }

            if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
            {
                RecordNewTriggerStage(
                    CountHookTriggerReceived(diagnostics, newTriggerKey, "down") + CountHookTriggerReceived(diagnostics, newTriggerKey, "up"),
                    CountPipelineQueued(diagnostics, newTriggerKey, "down"),
                    CountRuntimeDispatched(diagnostics, newTriggerKey, "down"),
                    runtimeInput.Count("key", "escape"),
                    manualConfirmed: false,
                    completed: false,
                    failureReason: "drain_timeout");
                return Failed(name, watch, "queues did not drain after new trigger probe in real target", CreateMetrics());
            }

            var manualObserved = ResolveManualConfirmation(
                options,
                "Did Blue Archive react only to the reloaded xbutton1 trigger after the active drag was interrupted and cleaned up?");
            var newTriggerReceivedCount = CountHookTriggerReceived(diagnostics, newTriggerKey, "down") + CountHookTriggerReceived(diagnostics, newTriggerKey, "up");
            var newTriggerPipelineCount = CountPipelineQueued(diagnostics, newTriggerKey, "down");
            var newTriggerRuntimeCount = CountRuntimeDispatched(diagnostics, newTriggerKey, "down");
            var newTriggerOutputDelta = runtimeInput.Count("key", "escape");
            detailMetrics["new_trigger_after_reload_manual_confirmed"] = manualObserved is true;
            RecordNewTriggerStage(
                newTriggerReceivedCount,
                newTriggerPipelineCount,
                newTriggerRuntimeCount,
                newTriggerOutputDelta,
                manualConfirmed: manualObserved is true,
                completed: true,
                failureReason: string.Empty);

            if (newTriggerReceivedCount == 0 ||
                newTriggerPipelineCount != 1 ||
                newTriggerRuntimeCount != 1 ||
                newTriggerOutputDelta != 2)
            {
                return Failed(name, watch, "new trigger did not cleanly take over after reload during active drag in real target", CreateMetrics());
            }
        }
        else
        {
            if (!await blocker.FocusAsync(TimeSpan.FromSeconds(5)) ||
                !LiveWindowTools.MoveCursorToCenter(blocker.Handle))
            {
                return Failed(name, watch, "failed to focus blocker for old trigger after reload", CreateMetrics());
            }

            var blockedGate = await host.CheckForegroundAsync(CancellationToken.None);
            if (blockedGate.IsAllowed)
            {
                var blockedGateMetrics = CreateMetrics();
                blockedGateMetrics["blocked_foreground_process"] = blockedGate.ForegroundProcess ?? string.Empty;
                return Failed(name, watch, "blocker foreground was unexpectedly allowed during old-trigger after reload probe", blockedGateMetrics);
            }

            runtimeInput.Reset();
            diagnostics.Reset();
            blocker.Reset();
            Console.Error.WriteLine(
                $"MANUAL xbutton2-drag-reload-old-trigger: blocker focused. Physically press '{oldTrigger}' once within {options.ManualTimeout.TotalSeconds:0}s. Expect pass-through to blocker and no mapped output.");
            if (!await WaitUntilAsync(
                    () => blocker.XButton2DownCount > 0 && blocker.XButton2UpCount > 0,
                    options.ManualTimeout,
                    CancellationToken.None))
            {
                RecordOldTriggerStage(
                    CountHookTriggerReceived(diagnostics, oldTriggerKey, "down") + CountHookTriggerReceived(diagnostics, oldTriggerKey, "up"),
                    CountPipelineQueued(diagnostics, oldTriggerKey, "down"),
                    CountRuntimeDispatched(diagnostics, oldTriggerKey, "down"),
                    runtimeInput.Count("key", "escape"),
                    passThroughCount: blocker.XButton2DownCount > 0 && blocker.XButton2UpCount > 0 ? 1 : 0,
                    passThroughDown: blocker.XButton2DownCount,
                    passThroughUp: blocker.XButton2UpCount,
                    completed: false,
                    failureReason: "awaiting_blocked_pass_through");
                return Failed(name, watch, "old trigger after reload did not pass through to blocker in real target", CreateMetrics());
            }

            if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
            {
                RecordOldTriggerStage(
                    CountHookTriggerReceived(diagnostics, oldTriggerKey, "down") + CountHookTriggerReceived(diagnostics, oldTriggerKey, "up"),
                    CountPipelineQueued(diagnostics, oldTriggerKey, "down"),
                    CountRuntimeDispatched(diagnostics, oldTriggerKey, "down"),
                    runtimeInput.Count("key", "escape"),
                    passThroughCount: blocker.XButton2DownCount > 0 && blocker.XButton2UpCount > 0 ? 1 : 0,
                    passThroughDown: blocker.XButton2DownCount,
                    passThroughUp: blocker.XButton2UpCount,
                    completed: false,
                    failureReason: "drain_timeout");
                return Failed(name, watch, "queues did not drain after old trigger blocker probe in real target", CreateMetrics());
            }

            var oldTriggerReceivedCount = CountHookTriggerReceived(diagnostics, oldTriggerKey, "down") + CountHookTriggerReceived(diagnostics, oldTriggerKey, "up");
            var oldTriggerPipelineCount = CountPipelineQueued(diagnostics, oldTriggerKey, "down");
            var oldTriggerRuntimeCount = CountRuntimeDispatched(diagnostics, oldTriggerKey, "down");
            var oldTriggerOutputDelta = runtimeInput.Count("key", "escape");
            var oldTriggerPassThroughDown = blocker.XButton2DownCount;
            var oldTriggerPassThroughUp = blocker.XButton2UpCount;
            var oldTriggerPassThroughCount =
                oldTriggerPassThroughDown > 0 && oldTriggerPassThroughUp > 0
                    ? 1
                    : 0;
            RecordOldTriggerStage(
                oldTriggerReceivedCount,
                oldTriggerPipelineCount,
                oldTriggerRuntimeCount,
                oldTriggerOutputDelta,
                oldTriggerPassThroughCount,
                oldTriggerPassThroughDown,
                oldTriggerPassThroughUp,
                completed: true,
                failureReason: string.Empty);

            if (oldTriggerPipelineCount != 0 ||
                oldTriggerRuntimeCount != 0 ||
                oldTriggerOutputDelta != 0 ||
                oldTriggerPassThroughCount != 1)
            {
                return Failed(name, watch, "old trigger did not cleanly retire after reload during active drag in real target", CreateMetrics());
            }
        }

        var cursorEnd = await runtimeInput.GetMousePositionAsync(CancellationToken.None);
        cursorRestored = await TryRestoreCursorAsync(restoreInput, cursorStart.X, cursorStart.Y);

        await host.StopAsync(CancellationToken.None);
        var stopped = host.Snapshot();
        if (!IsCleanlyStopped(stopped))
        {
            return Failed(name, watch, "reload-during-active-drag runtime did not stop cleanly in real target", CreateMetrics());
        }

        var metrics = CreateMetrics();
        metrics["trigger_received_count"] = activeBarrier.TriggerDownReceivedCount + activeBarrier.TriggerUpReceivedCount;
        metrics["pipeline_enqueued_count"] = activeBarrier.PipelineEnqueuedCount;
        metrics["runtime_dispatched_count"] = activeBarrier.RuntimeDispatchedCount;
        metrics["drag_started_count"] = activeBarrier.DragButtonDownCount;
        metrics["drag_button_down_count"] = activeBarrier.DragButtonDownCount;
        metrics["move_event_before_reload_count"] = activeBarrier.MoveEventCount;
        metrics["reload_requested_count"] = reloadRequestedCount;
        metrics["reload_generation_before"] = generationBefore;
        metrics["reload_generation_after"] = generationAfter;
        metrics["interrupt_while_drag_active_count"] = interruptWhileDragActiveCount;
        metrics["drag_completed_normally_count"] = dragCompletedNormallyCount;
        metrics["drag_button_up_count"] = dragButtonUpTotalCount;
        metrics["drag_button_up_from_reload_cleanup_count"] = dragButtonUpFromReloadCleanupCount;
        metrics["move_event_after_reload_count"] = moveEventAfterReloadCount;
        metrics["unexpected_output_after_reload_count"] = unexpectedOutputAfterReloadCount;
        metrics["old_generation_cleanup_completed_count"] = oldGenerationCleanupCompletedCount;
        metrics["held_owner_count_after_reload_cleanup"] = heldOwnerCountAfterReloadCleanup;
        metrics["old_trigger_after_reload_received_count"] = detailMetrics.TryGetValue(StageKey("old_trigger_after_reload", "received_count"), out var oldReceived) ? oldReceived : 0;
        metrics["old_trigger_after_reload_pipeline_count"] = detailMetrics.TryGetValue(StageKey("old_trigger_after_reload", "pipeline_count"), out var oldPipeline) ? oldPipeline : 0;
        metrics["old_trigger_after_reload_runtime_count"] = detailMetrics.TryGetValue(StageKey("old_trigger_after_reload", "runtime_count"), out var oldRuntime) ? oldRuntime : 0;
        metrics["old_trigger_after_reload_output_delta"] = detailMetrics.TryGetValue(StageKey("old_trigger_after_reload", "output_delta"), out var oldOutput) ? oldOutput : 0;
        metrics["old_trigger_after_reload_pass_through_count"] = detailMetrics.TryGetValue(StageKey("old_trigger_after_reload", "pass_through_count"), out var oldPass) ? oldPass : 0;
        metrics["new_trigger_after_reload_received_count"] = detailMetrics.TryGetValue(StageKey("new_trigger_after_reload", "received_count"), out var newReceived) ? newReceived : 0;
        metrics["new_trigger_after_reload_pipeline_count"] = detailMetrics.TryGetValue(StageKey("new_trigger_after_reload", "pipeline_count"), out var newPipeline) ? newPipeline : 0;
        metrics["new_trigger_after_reload_runtime_count"] = detailMetrics.TryGetValue(StageKey("new_trigger_after_reload", "runtime_count"), out var newRuntime) ? newRuntime : 0;
        metrics["new_trigger_after_reload_output_delta"] = detailMetrics.TryGetValue(StageKey("new_trigger_after_reload", "output_delta"), out var newOutput) ? newOutput : 0;
        metrics["new_trigger_after_reload_manual_confirmed"] = detailMetrics["new_trigger_after_reload_manual_confirmed"];
        metrics["cursor_start_x"] = cursorStart.X;
        metrics["cursor_start_y"] = cursorStart.Y;
        metrics["cursor_end_x"] = cursorEnd.X;
        metrics["cursor_end_y"] = cursorEnd.Y;
        metrics["cursor_restored"] = cursorRestored;
        metrics["move_total_dx"] = cursorEnd.X - cursorStart.X;
        metrics["move_total_dy"] = cursorEnd.Y - cursorStart.Y;
        metrics["diagnostic_events"] = diagnostics.Count;
        metrics["stopped_clean"] = IsCleanlyStopped(stopped);
        AddSnapshots(metrics, options, ("active_drag", activeBarrier.Snapshot), ("after_reload", afterReload), ("stopped", stopped));
        return Passed(name, watch, metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message, CreateMetrics());
    }
    finally
    {
        if (!cursorRestored && cursorCaptured)
        {
            _ = TryRestoreCursorAsync(restoreInput, cursorStart.X, cursorStart.Y);
        }

        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}

static Task<AcceptanceScenarioResult> RunXButton2TriggeredDragForegroundLossDuringActiveDragAsync(AcceptanceOptions options)
{
    return RunXButton2TriggeredDragForegroundLossScenarioAsync(options, returnBeforeRelease: false);
}

static Task<AcceptanceScenarioResult> RunXButton2TriggeredDragForegroundLossThenReturnBeforeReleaseAsync(AcceptanceOptions options)
{
    return RunXButton2TriggeredDragForegroundLossScenarioAsync(options, returnBeforeRelease: true);
}

static async Task<AcceptanceScenarioResult> RunXButton2TriggeredDragForegroundLossScenarioAsync(
    AcceptanceOptions options,
    bool returnBeforeRelease)
{
    var name = returnBeforeRelease
        ? "xbutton2-triggered-drag-foreground-loss-then-return-before-release"
        : "xbutton2-triggered-drag-foreground-loss-during-active-drag";
    const string triggerKey = "mouse:mouse_x2";
    const string xbuttonTrigger = "mouse_x2";
    const string mappedDragButton = "mouse_middle";
    var mappingId = returnBeforeRelease
        ? "xbutton2-drag-foreground-loss-return-before-release"
        : "xbutton2-drag-foreground-loss-during-active-drag";
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, $"{name} requires --allow-live-input");
    }

    using var blocker = new LiveHarnessWindow(
        returnBeforeRelease
            ? "BA KeySmith XButton2 Drag Foreground Return Blocker"
            : "BA KeySmith XButton2 Drag Foreground Loss Blocker");
    Process? target = null;
    RuntimeHost? host = null;
    var restoreInput = new WindowsInputBackend();
    var cursorStart = (X: 0, Y: 0);
    var cursorCaptured = false;
    var cursorRestored = false;
    try
    {
        blocker.Start(TimeSpan.FromSeconds(3));
        target = await StartPowerShellWindowAsync(
            returnBeforeRelease
                ? "BA KeySmith XButton2 Drag Foreground Return Target"
                : "BA KeySmith XButton2 Drag Foreground Loss Target",
            TimeSpan.FromSeconds(5));
        if (target is null)
        {
            return Failed(name, watch, $"failed to start {name} target window");
        }

        var runtimeInput = new CountingInputBackend(new WindowsInputBackend());
        var triggerSender = new WindowsInputBackend(markInjected: false);
        var diagnostics = new CountingDiagnosticsSink();
        host = new RuntimeHost(
            LiveMacroConfig("powershell.exe", mappingId, xbuttonTrigger, InterruptibleDragScript(mappedDragButton)),
            runtimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(diagnostics),
            diagnostics);

        await host.StartAsync(CancellationToken.None);
        target.Refresh();
        if (!await LiveWindowTools.FocusAsync(target.MainWindowHandle, TimeSpan.FromSeconds(3)) ||
            !LiveWindowTools.MoveCursorToCenter(target.MainWindowHandle))
        {
            return Failed(name, watch, $"failed to focus {name} target and move cursor");
        }

        blocker.Reset();
        runtimeInput.Reset();
        diagnostics.Reset();
        cursorStart = await runtimeInput.GetMousePositionAsync(CancellationToken.None);
        cursorCaptured = true;

        await SendMouseDownAsync(triggerSender, "x2");
        var barrier = await WaitForActiveDragBarrierAsync(
            host,
            runtimeInput,
            diagnostics,
            triggerKey,
            mappedDragButton,
            mappingId,
            options.DrainTimeout,
            CancellationToken.None);
        if (barrier is null)
        {
            var failedMetrics = new Dictionary<string, object>
            {
                ["validation_scope"] = returnBeforeRelease
                    ? "phase2d-d-xbutton2-triggered-drag-foreground-loss-then-return-before-release"
                    : "phase2d-d-xbutton2-triggered-drag-foreground-loss-during-active-drag",
                ["foreground_loss_observed_layer"] = "macro_executor",
                ["foreground_loss_cancellation_layer"] = "macro_executor",
                ["foreground_cleanup_path"] = "macro_executor.finally.release_owner",
                ["trigger_received_count"] = CountHookTriggerReceived(diagnostics, triggerKey, "down") + CountHookTriggerReceived(diagnostics, triggerKey, "up"),
                ["pipeline_enqueued_count"] = CountPipelineQueued(diagnostics, triggerKey, "down"),
                ["runtime_dispatched_count"] = CountRuntimeDispatched(diagnostics, triggerKey, "down"),
                ["drag_button_down_count"] = runtimeInput.Count("mouse", mappedDragButton, true),
                ["normal_drag_button_up_count"] = runtimeInput.Count("mouse", mappedDragButton, false),
                ["move_event_before_foreground_loss_count"] = MoveEvents(runtimeInput).Count,
                ["foreground_lost_while_drag_active_count"] = 0,
                ["drag_completed_normally_count"] = runtimeInput.Count("mouse", mappedDragButton, false),
                ["stopped_clean"] = false
            };
            AddSnapshots(failedMetrics, options, ("running", host.Snapshot()));
            return Failed(name, watch, "active drag barrier was not reached before foreground loss", failedMetrics);
        }

        var activeBarrier = barrier.Value;
        var interruptWhileDragActiveCount =
            activeBarrier.DragButtonDownCount > 0 &&
            activeBarrier.MoveEventCount > 0 &&
            activeBarrier.DragButtonUpCount == 0 &&
            activeBarrier.Snapshot.Runtime.Presses.OwnerKeys.Count > 0 &&
            activeBarrier.MacroFinishedCount == 0
                ? 1
                : 0;
        if (interruptWhileDragActiveCount != 1)
        {
            var failedMetrics = new Dictionary<string, object>
            {
                ["validation_scope"] = returnBeforeRelease
                    ? "phase2d-d-xbutton2-triggered-drag-foreground-loss-then-return-before-release"
                    : "phase2d-d-xbutton2-triggered-drag-foreground-loss-during-active-drag",
                ["foreground_loss_observed_layer"] = "macro_executor",
                ["foreground_loss_cancellation_layer"] = "macro_executor",
                ["foreground_cleanup_path"] = "macro_executor.finally.release_owner",
                ["drag_button_down_count"] = activeBarrier.DragButtonDownCount,
                ["normal_drag_button_up_count"] = activeBarrier.DragButtonUpCount,
                ["move_event_before_foreground_loss_count"] = activeBarrier.MoveEventCount,
                ["interrupt_while_drag_active_count"] = interruptWhileDragActiveCount,
                ["drag_completed_normally_count"] = activeBarrier.DragButtonUpCount
            };
            AddSnapshots(failedMetrics, options, ("active_drag", activeBarrier.Snapshot));
            return Failed(name, watch, "foreground loss precondition did not prove an active drag in progress", failedMetrics);
        }

        if (!await blocker.FocusAsync(TimeSpan.FromSeconds(3)) ||
            !LiveWindowTools.MoveCursorToCenter(blocker.Handle))
        {
            return Failed(name, watch, "failed to focus blocker during active drag");
        }

        var blockedGate = await host.CheckForegroundAsync(CancellationToken.None);
        if (blockedGate.IsAllowed)
        {
            var blockedGateMetrics = new Dictionary<string, object>
            {
                ["validation_scope"] = returnBeforeRelease
                    ? "phase2d-d-xbutton2-triggered-drag-foreground-loss-then-return-before-release"
                    : "phase2d-d-xbutton2-triggered-drag-foreground-loss-during-active-drag",
                ["blocked_foreground_process"] = blockedGate.ForegroundProcess ?? string.Empty
            };
            AddSnapshots(blockedGateMetrics, options, ("active_drag", activeBarrier.Snapshot));
            return Failed(name, watch, "blocker foreground was unexpectedly allowed during active drag foreground-loss probe", blockedGateMetrics);
        }

        var cleanupBarrier = await WaitForForegroundLossCleanupBarrierAsync(
            host,
            runtimeInput,
            diagnostics,
            mappingId,
            mappedDragButton,
            activeBarrier.DragButtonUpCount,
            options.DrainTimeout,
            CancellationToken.None);
        if (cleanupBarrier is null)
        {
            var failedMetrics = new Dictionary<string, object>
            {
                ["validation_scope"] = returnBeforeRelease
                    ? "phase2d-d-xbutton2-triggered-drag-foreground-loss-then-return-before-release"
                    : "phase2d-d-xbutton2-triggered-drag-foreground-loss-during-active-drag",
                ["foreground_loss_observed_layer"] = "macro_executor",
                ["foreground_loss_cancellation_layer"] = "macro_executor",
                ["foreground_cleanup_path"] = "macro_executor.finally.release_owner",
                ["drag_button_down_count"] = activeBarrier.DragButtonDownCount,
                ["normal_drag_button_up_count"] = activeBarrier.DragButtonUpCount,
                ["move_event_before_foreground_loss_count"] = activeBarrier.MoveEventCount,
                ["foreground_lost_while_drag_active_count"] = CountDiagnosticEvent(diagnostics, "macro", "foreground_lost_during_active_pointer_sequence"),
                ["mapped_owner_cleanup_completed_count"] = CountDiagnosticEvent(diagnostics, "macro", "foreground_cleanup_completed"),
                ["held_owner_count_after_foreground_cleanup"] = host.Snapshot().Runtime.Presses.OwnerKeys.Count
            };
            AddSnapshots(failedMetrics, options, ("active_drag", activeBarrier.Snapshot), ("running", host.Snapshot()));
            return Failed(name, watch, "foreground loss cleanup barrier was not reached while drag remained active", failedMetrics);
        }

        var cleanup = cleanupBarrier.Value;
        if (returnBeforeRelease)
        {
            target.Refresh();
            if (!await LiveWindowTools.FocusAsync(target.MainWindowHandle, TimeSpan.FromSeconds(3)) ||
                !LiveWindowTools.MoveCursorToCenter(target.MainWindowHandle))
            {
                return Failed(name, watch, "failed to restore target foreground before physical release");
            }

            var returnedGate = await host.CheckForegroundAsync(CancellationToken.None);
            if (!returnedGate.IsAllowed)
            {
                var returnedGateMetrics = new Dictionary<string, object>
                {
                    ["validation_scope"] = "phase2d-d-xbutton2-triggered-drag-foreground-loss-then-return-before-release",
                    ["returned_foreground_process"] = returnedGate.ForegroundProcess ?? string.Empty
                };
                AddSnapshots(returnedGateMetrics, options, ("active_drag", activeBarrier.Snapshot), ("after_foreground_cleanup", cleanup.Snapshot));
                return Failed(name, watch, "target foreground did not return before release probe", returnedGateMetrics);
            }
        }

        await SendMouseUpAsync(triggerSender, "x2");
        if (!await WaitUntilAsync(
                () => CountHookTriggerReceived(diagnostics, triggerKey, "up") > 0,
                options.DrainTimeout,
                CancellationToken.None))
        {
            var failedMetrics = new Dictionary<string, object>
            {
                ["validation_scope"] = returnBeforeRelease
                    ? "phase2d-d-xbutton2-triggered-drag-foreground-loss-then-return-before-release"
                    : "phase2d-d-xbutton2-triggered-drag-foreground-loss-during-active-drag",
                ["foreground_loss_observed_layer"] = "macro_executor",
                ["foreground_loss_cancellation_layer"] = "macro_executor",
                ["foreground_cleanup_path"] = "macro_executor.finally.release_owner",
                ["trigger_up_received_count"] = CountHookTriggerReceived(diagnostics, triggerKey, "up"),
                ["release_matched_captured_session_count"] = CountHookLifecycleEvent(diagnostics, "release_matched_captured_session", triggerKey, "up")
            };
            AddSnapshots(failedMetrics, options, ("active_drag", activeBarrier.Snapshot), ("after_foreground_cleanup", cleanup.Snapshot));
            return Failed(name, watch, "physical release after foreground loss was not observed by hook", failedMetrics);
        }

        await Task.Delay(150, CancellationToken.None);
        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            var failedMetrics = new Dictionary<string, object>
            {
                ["validation_scope"] = returnBeforeRelease
                    ? "phase2d-d-xbutton2-triggered-drag-foreground-loss-then-return-before-release"
                    : "phase2d-d-xbutton2-triggered-drag-foreground-loss-during-active-drag",
                ["foreground_loss_observed_layer"] = "macro_executor",
                ["foreground_loss_cancellation_layer"] = "macro_executor",
                ["foreground_cleanup_path"] = "macro_executor.finally.release_owner"
            };
            AddSnapshots(failedMetrics, options, ("active_drag", activeBarrier.Snapshot), ("after_foreground_cleanup", cleanup.Snapshot), ("running", host.Snapshot()));
            return Failed(name, watch, "queues did not drain after physical release in foreground-loss drag scenario", failedMetrics);
        }

        var running = host.Snapshot();
        var moveEvents = MoveEvents(runtimeInput);
        var cursorEnd = await runtimeInput.GetMousePositionAsync(CancellationToken.None);
        var triggerDownReceivedCount = CountHookTriggerReceived(diagnostics, triggerKey, "down");
        var triggerUpReceivedCount = CountHookTriggerReceived(diagnostics, triggerKey, "up");
        var pipelineEnqueuedCount = CountPipelineQueued(diagnostics, triggerKey, "down");
        var runtimeDispatchedCount = CountRuntimeDispatched(diagnostics, triggerKey, "down");
        var capturedSessionEnteredCount = CountHookLifecycleEvent(diagnostics, "captured_session_entered", triggerKey, "down");
        var releaseMatchedCapturedSessionCount = CountHookLifecycleEvent(diagnostics, "release_matched_captured_session", triggerKey, "up");
        var blockedPassThroughDownCount = CountHookTriggerReceived(diagnostics, triggerKey, "down", suppress: false, dispatch: false);
        var blockedPassThroughUpCount = CountHookTriggerReceived(diagnostics, triggerKey, "up", suppress: false, dispatch: false);
        var uncapturedReleasePassThroughCount = CountHookLifecycleEvent(diagnostics, "uncaptured_release_pass_through", triggerKey, "up");
        var dragCompletedNormallyCount = activeBarrier.DragButtonUpCount;
        var normalDragButtonUpCount = activeBarrier.DragButtonUpCount;
        var dragButtonDownCount = activeBarrier.DragButtonDownCount;
        var dragButtonUpCount = runtimeInput.Count("mouse", mappedDragButton, false);
        var dragButtonUpFromForegroundCleanupCount = Math.Max(0, cleanup.DragButtonUpTotalCount - activeBarrier.DragButtonUpCount);
        var moveEventBeforeForegroundLossCount = activeBarrier.MoveEventCount;
        var moveEventAfterForegroundLossCount = Math.Max(0, cleanup.MoveEventTotalCount - activeBarrier.MoveEventCount);
        var unexpectedOutputAfterForegroundLossCount = Math.Max(
            0,
            (cleanup.TotalEventCount - activeBarrier.TotalEventCount) - dragButtonUpFromForegroundCleanupCount);
        var unexpectedOutputAfterReleaseCount = Math.Max(0, runtimeInput.Events.Count - cleanup.TotalEventCount);
        var foregroundLostWhileDragActiveCount = cleanup.ForegroundLostWhileDragActiveCount;
        var mappedOwnerCleanupCompletedCount = cleanup.MappedOwnerCleanupCompletedCount;
        var heldOwnerCountAfterForegroundCleanup = cleanup.Snapshot.Runtime.Presses.OwnerKeys.Count;
        var foregroundReturnedBeforeReleaseCount = returnBeforeRelease ? 1 : 0;
        var retroactiveResumeAfterForegroundReturnCount = returnBeforeRelease
            ? Math.Max(0, moveEvents.Count - cleanup.MoveEventTotalCount) +
              Math.Max(0, runtimeInput.Count("mouse", mappedDragButton, true) - dragButtonDownCount) +
              Math.Max(0, dragButtonUpCount - cleanup.DragButtonUpTotalCount)
            : 0;
        var physicalCaptureSessionRetainedAfterForegroundLoss =
            mappedOwnerCleanupCompletedCount > 0 &&
            heldOwnerCountAfterForegroundCleanup == 0 &&
            triggerUpReceivedCount > 0 &&
            releaseMatchedCapturedSessionCount > 0 &&
            blockedPassThroughUpCount == 0 &&
            uncapturedReleasePassThroughCount == 0 &&
            blocker.XButton2UpCount == 0
                ? 1
                : 0;
        var cleanupCompletedCount = mappedOwnerCleanupCompletedCount;

        cursorRestored = await TryRestoreCursorAsync(restoreInput, cursorStart.X, cursorStart.Y);

        await host.StopAsync(CancellationToken.None);
        var stopped = host.Snapshot();
        var passed =
            dragButtonDownCount == 1 &&
            moveEventBeforeForegroundLossCount >= 1 &&
            foregroundLostWhileDragActiveCount == 1 &&
            dragCompletedNormallyCount == 0 &&
            normalDragButtonUpCount == 0 &&
            dragButtonUpFromForegroundCleanupCount == 1 &&
            moveEventAfterForegroundLossCount == 0 &&
            unexpectedOutputAfterForegroundLossCount == 0 &&
            mappedOwnerCleanupCompletedCount == 1 &&
            heldOwnerCountAfterForegroundCleanup == 0 &&
            physicalCaptureSessionRetainedAfterForegroundLoss == 1 &&
            triggerUpReceivedCount == 1 &&
            releaseMatchedCapturedSessionCount == 1 &&
            blockedPassThroughUpCount == 0 &&
            uncapturedReleasePassThroughCount == 0 &&
            unexpectedOutputAfterReleaseCount == 0 &&
            retroactiveResumeAfterForegroundReturnCount == 0 &&
            blocker.XButton2UpCount == 0 &&
            IsCleanlyStopped(stopped);

        var metrics = new Dictionary<string, object>
        {
            ["validation_scope"] = returnBeforeRelease
                ? "phase2d-d-xbutton2-triggered-drag-foreground-loss-then-return-before-release"
                : "phase2d-d-xbutton2-triggered-drag-foreground-loss-during-active-drag",
            ["foreground_loss_observed_layer"] = "macro_executor",
            ["foreground_loss_cancellation_layer"] = "macro_executor",
            ["foreground_cleanup_path"] = "macro_executor.finally.release_owner",
            ["trigger_received_count"] = triggerDownReceivedCount + triggerUpReceivedCount,
            ["trigger_down_received_count"] = triggerDownReceivedCount,
            ["trigger_up_received_count"] = triggerUpReceivedCount,
            ["pipeline_enqueued_count"] = pipelineEnqueuedCount,
            ["runtime_dispatched_count"] = runtimeDispatchedCount,
            ["drag_started_count"] = dragButtonDownCount,
            ["drag_button_down_count"] = dragButtonDownCount,
            ["normal_drag_button_up_count"] = normalDragButtonUpCount,
            ["drag_button_up_count"] = dragButtonUpCount,
            ["move_event_before_foreground_loss_count"] = moveEventBeforeForegroundLossCount,
            ["foreground_lost_while_drag_active_count"] = foregroundLostWhileDragActiveCount,
            ["drag_completed_normally_count"] = dragCompletedNormallyCount,
            ["drag_button_up_from_foreground_cleanup_count"] = dragButtonUpFromForegroundCleanupCount,
            ["move_event_after_foreground_loss_count"] = moveEventAfterForegroundLossCount,
            ["unexpected_output_after_foreground_loss_count"] = unexpectedOutputAfterForegroundLossCount,
            ["mapped_owner_cleanup_completed_count"] = mappedOwnerCleanupCompletedCount,
            ["cleanup_completed_count"] = cleanupCompletedCount,
            ["held_owner_count_after_foreground_cleanup"] = heldOwnerCountAfterForegroundCleanup,
            ["physical_capture_session_retained_after_foreground_loss"] = physicalCaptureSessionRetainedAfterForegroundLoss,
            ["captured_session_entered_count"] = capturedSessionEnteredCount,
            ["release_matched_captured_session_count"] = releaseMatchedCapturedSessionCount,
            ["blocked_pass_through_down_count"] = blockedPassThroughDownCount,
            ["blocked_pass_through_up_count"] = blockedPassThroughUpCount,
            ["unexpected_output_after_release_count"] = unexpectedOutputAfterReleaseCount,
            ["foreground_returned_before_release_count"] = foregroundReturnedBeforeReleaseCount,
            ["retroactive_resume_after_foreground_return_count"] = retroactiveResumeAfterForegroundReturnCount,
            ["blocker_xbutton2_down_count"] = blocker.XButton2DownCount,
            ["blocker_xbutton2_up_count"] = blocker.XButton2UpCount,
            ["macro_started_count"] = activeBarrier.MacroStartedCount,
            ["macro_finished_count"] = cleanup.MacroFinishedCount,
            ["cursor_start_x"] = cursorStart.X,
            ["cursor_start_y"] = cursorStart.Y,
            ["cursor_end_x"] = cursorEnd.X,
            ["cursor_end_y"] = cursorEnd.Y,
            ["cursor_restored"] = cursorRestored,
            ["move_total_dx"] = cursorEnd.X - cursorStart.X,
            ["move_total_dy"] = cursorEnd.Y - cursorStart.Y,
            ["diagnostic_events"] = diagnostics.Count,
            ["stopped_clean"] = IsCleanlyStopped(stopped)
        };
        AddSnapshots(
            metrics,
            options,
            ("active_drag", activeBarrier.Snapshot),
            ("after_foreground_cleanup", cleanup.Snapshot),
            ("running", running),
            ("stopped", stopped));

        return passed
            ? Passed(name, watch, metrics)
            : Failed(name, watch, "foreground-loss during active drag did not satisfy cleanup and captured-session invariants", metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message);
    }
    finally
    {
        if (!cursorRestored && cursorCaptured)
        {
            _ = TryRestoreCursorAsync(restoreInput, cursorStart.X, cursorStart.Y);
        }

        if (host is not null)
        {
            await host.DisposeAsync();
        }

        CloseProcessWindow(target);
    }
}

static async Task<AcceptanceScenarioResult> RunBlueArchiveManualXButton2DragInterruptScenarioAsync(
    AcceptanceOptions options,
    bool disableInterrupt)
{
    var interruptType = disableInterrupt ? "disable" : "stop";
    var name = disableInterrupt
        ? "bluearchive-manual-xbutton2-drag-disable-during-active-drag"
        : "bluearchive-manual-xbutton2-drag-stop-during-active-drag";
    const string triggerKey = "mouse:mouse_x2";
    var mappingId = disableInterrupt
        ? "xbutton2-bluearchive-drag-disable-during-active-drag"
        : "xbutton2-bluearchive-drag-stop-during-active-drag";
    const string mappedDragButton = "mouse_middle";
    var script = InterruptibleDragScript(mappedDragButton);
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, $"{name} requires --allow-live-input");
    }

    var targetProcess = options.TargetProcess;
    var targetWindow = FindTargetProcessWithWindow(targetProcess);
    if (targetWindow is null)
    {
        return Failed(name, watch, $"target process with foreground-capable window was not found: {targetProcess}");
    }

    var xbuttonTrigger = KeyNameResolver.NormalizeMouseButton(options.ManualXButtonTrigger);
    if (!string.Equals(xbuttonTrigger, "mouse_x2", StringComparison.OrdinalIgnoreCase))
    {
        return Failed(name, watch, $"manual xbutton trigger for {name} must be mouse_x2, but was: {xbuttonTrigger}");
    }

    var runnerElevation = ProbeCurrentProcessElevation();
    var targetElevation = ProbeProcessElevation(targetWindow.Id, $"{targetWindow.ProcessName}.exe");
    var detailMetrics = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
    {
        ["manual_interrupt_confirmed"] = false,
        ["mapped_drag_button"] = mappedDragButton
    };

    Dictionary<string, object> CreateMetrics()
    {
        var metrics = new Dictionary<string, object>
        {
            ["target_process"] = targetProcess,
            ["target_process_id"] = targetWindow.Id,
            ["target_window_title"] = targetWindow.MainWindowTitle,
            ["validation_scope"] = $"phase2d-b-bluearchive-xbutton2-drag-{interruptType}-during-active-drag",
            ["manual_xbutton_trigger"] = xbuttonTrigger,
            ["interrupt_type"] = interruptType,
            ["runner_elevation_known"] = runnerElevation.IsKnown,
            ["runner_elevated"] = runnerElevation.IsKnown && runnerElevation.IsElevated,
            ["runner_elevated_state"] = ElevationStateLabel(runnerElevation),
            ["target_elevation_known"] = targetElevation.IsKnown,
            ["target_elevated"] = targetElevation.IsKnown && targetElevation.IsElevated,
            ["target_elevated_state"] = ElevationStateLabel(targetElevation),
            ["runner_target_same_elevation"] = runnerElevation.IsKnown &&
                targetElevation.IsKnown &&
                runnerElevation.IsElevated == targetElevation.IsElevated,
            ["real_target_observed_scope"] = $"{interruptType}_during_active_drag"
        };

        foreach (var (key, value) in detailMetrics)
        {
            metrics[key] = value;
        }

        return metrics;
    }

    if (!runnerElevation.IsKnown || !runnerElevation.IsElevated)
    {
        return Failed(
            name,
            watch,
            $"{name} requires an elevated acceptance/headless/live runner. Current runner is not elevated, so this real-target validation is invalid.",
            CreateMetrics());
    }

    RuntimeHost? host = null;
    var restoreInput = new WindowsInputBackend();
    var cursorStart = (X: 0, Y: 0);
    var cursorCaptured = false;
    var cursorRestored = false;
    try
    {
        var runtimeInput = new CountingInputBackend(new WindowsInputBackend());
        var diagnostics = new CountingDiagnosticsSink();
        host = new RuntimeHost(
            LiveMacroConfig(targetProcess, mappingId, xbuttonTrigger, script),
            runtimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(diagnostics),
            diagnostics);

        await host.StartAsync(CancellationToken.None);
        targetWindow.Refresh();
        if (!await LiveWindowTools.FocusAsync(targetWindow.MainWindowHandle, TimeSpan.FromSeconds(5)) ||
            !LiveWindowTools.MoveCursorToCenter(targetWindow.MainWindowHandle))
        {
            return Failed(name, watch, $"failed to focus target window and move cursor: {targetWindow.ProcessName}", CreateMetrics());
        }

        runtimeInput.Reset();
        diagnostics.Reset();
        cursorStart = await runtimeInput.GetMousePositionAsync(CancellationToken.None);
        cursorCaptured = true;
        Console.Error.WriteLine(
            $"MANUAL xbutton2-drag-{interruptType}-active: Blue Archive focused. Move the cursor to a safe non-destructive area, then press '{xbuttonTrigger}' once within {options.ManualTimeout.TotalSeconds:0}s. Expect the mapped drag to begin and then be interrupted by runtime {interruptType}; structure metrics remain the primary verdict.");

        var barrier = await WaitForActiveDragBarrierAsync(
            host,
            runtimeInput,
            diagnostics,
            triggerKey,
            mappedDragButton,
            mappingId,
            options.ManualTimeout,
            CancellationToken.None);
        if (barrier is null)
        {
            detailMetrics["trigger_received_count"] = CountHookTriggerReceived(diagnostics, triggerKey, "down") + CountHookTriggerReceived(diagnostics, triggerKey, "up");
            detailMetrics["pipeline_enqueued_count"] = CountPipelineQueued(diagnostics, triggerKey, "down");
            detailMetrics["runtime_dispatched_count"] = CountRuntimeDispatched(diagnostics, triggerKey, "down");
            detailMetrics["drag_button_down_count"] = runtimeInput.Count("mouse", mappedDragButton, true);
            detailMetrics["drag_button_up_count"] = runtimeInput.Count("mouse", mappedDragButton, false);
            detailMetrics["move_event_before_interrupt_count"] = MoveEvents(runtimeInput).Count;
            detailMetrics["interrupt_while_drag_active_count"] = 0;
            return Failed(name, watch, $"{interruptType} validation did not reach an active drag barrier in real target", CreateMetrics());
        }

        var activeBarrier = barrier.Value;
        var interruptWhileDragActiveCount =
            activeBarrier.DragButtonDownCount > 0 &&
            activeBarrier.MoveEventCount > 0 &&
            activeBarrier.DragButtonUpCount == 0 &&
            activeBarrier.Snapshot.Runtime.Presses.OwnerKeys.Count > 0 &&
            activeBarrier.MacroFinishedCount == 0
                ? 1
                : 0;
        if (interruptWhileDragActiveCount != 1)
        {
            detailMetrics["trigger_received_count"] = activeBarrier.TriggerDownReceivedCount + activeBarrier.TriggerUpReceivedCount;
            detailMetrics["pipeline_enqueued_count"] = activeBarrier.PipelineEnqueuedCount;
            detailMetrics["runtime_dispatched_count"] = activeBarrier.RuntimeDispatchedCount;
            detailMetrics["drag_button_down_count"] = activeBarrier.DragButtonDownCount;
            detailMetrics["drag_button_up_count"] = activeBarrier.DragButtonUpCount;
            detailMetrics["move_event_before_interrupt_count"] = activeBarrier.MoveEventCount;
            detailMetrics["interrupt_while_drag_active_count"] = interruptWhileDragActiveCount;
            return Failed(name, watch, $"{interruptType} precondition did not prove an active drag in real target", CreateMetrics());
        }

        var cursorAtInterrupt = (X: activeBarrier.CursorX, Y: activeBarrier.CursorY);
        var interruptWatch = Stopwatch.StartNew();
        if (disableInterrupt)
        {
            await host.DisableAsync(CancellationToken.None);
        }
        else
        {
            await host.StopAsync(CancellationToken.None);
        }
        interruptWatch.Stop();

        if (disableInterrupt &&
            !await WaitUntilAsync(
                () =>
                {
                    var snapshot = host.Snapshot();
                    return snapshot.Runtime.State == BAKeySmith.Core.Contracts.RuntimeState.Disabled &&
                        snapshot.Runtime.Presses.IsEmpty &&
                        snapshot.Runtime.PendingActionCount == 0 &&
                        snapshot.Runtime.RunningActionCount == 0;
                },
                options.DrainTimeout,
                CancellationToken.None))
        {
            detailMetrics["interrupt_while_drag_active_count"] = interruptWhileDragActiveCount;
            detailMetrics["drag_button_up_from_cleanup_count"] = Math.Max(0, runtimeInput.Count("mouse", mappedDragButton, false) - activeBarrier.DragButtonUpCount);
            detailMetrics["held_owner_count_after_cleanup"] = host.Snapshot().Runtime.Presses.OwnerKeys.Count;
            return Failed(name, watch, "disable during active drag did not settle to a clean disabled state in real target", CreateMetrics());
        }

        var afterInterrupt = host.Snapshot();
        var dragButtonUpTotalCount = runtimeInput.Count("mouse", mappedDragButton, false);
        var moveEventTotalCount = MoveEvents(runtimeInput).Count;
        var eventsAfterInterruptCount = runtimeInput.Events.Count;
        var dragCompletedNormallyCount = activeBarrier.DragButtonUpCount;
        var dragButtonUpFromCleanupCount = Math.Max(0, dragButtonUpTotalCount - activeBarrier.DragButtonUpCount);
        var moveEventAfterInterruptCount = Math.Max(0, moveEventTotalCount - activeBarrier.MoveEventCount);
        var unexpectedOutputAfterInterruptCount = Math.Max(
            0,
            (eventsAfterInterruptCount - activeBarrier.TotalEventCount) - dragButtonUpFromCleanupCount);
        var cleanupCompletedCount = CountSessionEvent(diagnostics, "stop_completed");
        var heldOwnerCountAfterCleanup = afterInterrupt.Runtime.Presses.OwnerKeys.Count;
        var interruptRequestedCount = CountSessionEvent(diagnostics, "stop_requested");
        var macroFinishedCount = CountMacroEvent(diagnostics, "finished", $"{mappingId}:macro:");

        var stopped = afterInterrupt;
        if (disableInterrupt)
        {
            await host.StopAsync(CancellationToken.None);
            stopped = host.Snapshot();
        }

        var manualObserved = ResolveManualConfirmation(
            options,
            $"Did Blue Archive show a short drag begin and then get interrupted by runtime {interruptType} without a lingering held drag?");
        detailMetrics["manual_interrupt_confirmed"] = manualObserved is true;

        var cursorEnd = await runtimeInput.GetMousePositionAsync(CancellationToken.None);
        cursorRestored = await TryRestoreCursorAsync(restoreInput, cursorStart.X, cursorStart.Y);

        var disableSettled =
            afterInterrupt.Runtime.State == BAKeySmith.Core.Contracts.RuntimeState.Disabled &&
            afterInterrupt.Runtime.Presses.IsEmpty &&
            afterInterrupt.Runtime.PendingActionCount == 0 &&
            afterInterrupt.Runtime.RunningActionCount == 0;
        var passed =
            activeBarrier.TriggerDownReceivedCount > 0 &&
            activeBarrier.PipelineEnqueuedCount == 1 &&
            activeBarrier.RuntimeDispatchedCount == 1 &&
            activeBarrier.DragButtonDownCount == 1 &&
            activeBarrier.MoveEventCount >= 1 &&
            interruptWhileDragActiveCount == 1 &&
            dragCompletedNormallyCount == 0 &&
            dragButtonUpFromCleanupCount == 1 &&
            moveEventAfterInterruptCount == 0 &&
            unexpectedOutputAfterInterruptCount == 0 &&
            cleanupCompletedCount == 1 &&
            heldOwnerCountAfterCleanup == 0 &&
            macroFinishedCount == 1 &&
            (!disableInterrupt ? IsCleanlyStopped(stopped) : disableSettled && IsCleanlyStopped(stopped));

        var metrics = CreateMetrics();
        metrics["trigger_received_count"] = activeBarrier.TriggerDownReceivedCount + activeBarrier.TriggerUpReceivedCount;
        metrics["trigger_down_received_count"] = activeBarrier.TriggerDownReceivedCount;
        metrics["trigger_up_received_count"] = activeBarrier.TriggerUpReceivedCount;
        metrics["pipeline_enqueued_count"] = activeBarrier.PipelineEnqueuedCount;
        metrics["runtime_dispatched_count"] = activeBarrier.RuntimeDispatchedCount;
        metrics["drag_started_count"] = activeBarrier.DragButtonDownCount;
        metrics["drag_button_down_count"] = activeBarrier.DragButtonDownCount;
        metrics["move_event_before_interrupt_count"] = activeBarrier.MoveEventCount;
        metrics["interrupt_requested_count"] = interruptRequestedCount;
        metrics["interrupt_while_drag_active_count"] = interruptWhileDragActiveCount;
        metrics["drag_completed_normally_count"] = dragCompletedNormallyCount;
        metrics["drag_button_up_count"] = dragButtonUpTotalCount;
        metrics["drag_button_up_from_cleanup_count"] = dragButtonUpFromCleanupCount;
        metrics["move_event_after_interrupt_count"] = moveEventAfterInterruptCount;
        metrics["unexpected_output_after_interrupt_count"] = unexpectedOutputAfterInterruptCount;
        metrics["cleanup_completed_count"] = cleanupCompletedCount;
        metrics["held_owner_count_after_cleanup"] = heldOwnerCountAfterCleanup;
        metrics["macro_started_count"] = activeBarrier.MacroStartedCount;
        metrics["macro_finished_count"] = macroFinishedCount;
        metrics["cursor_start_x"] = cursorStart.X;
        metrics["cursor_start_y"] = cursorStart.Y;
        metrics["cursor_interrupt_x"] = cursorAtInterrupt.X;
        metrics["cursor_interrupt_y"] = cursorAtInterrupt.Y;
        metrics["cursor_end_x"] = cursorEnd.X;
        metrics["cursor_end_y"] = cursorEnd.Y;
        metrics["cursor_restored"] = cursorRestored;
        metrics["move_total_dx"] = cursorEnd.X - cursorStart.X;
        metrics["move_total_dy"] = cursorEnd.Y - cursorStart.Y;
        metrics["interrupt_ms"] = interruptWatch.Elapsed.TotalMilliseconds;
        metrics["diagnostic_events"] = diagnostics.Count;
        metrics["stopped_clean"] = IsCleanlyStopped(stopped);
        AddSnapshots(
            metrics,
            options,
            ("running", activeBarrier.Snapshot),
            ("after_interrupt", afterInterrupt),
            ("stopped", stopped));

        return passed
            ? Passed(name, watch, metrics)
            : Failed(name, watch, $"{interruptType} during active drag did not satisfy cleanup invariants in real target", metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message, CreateMetrics());
    }
    finally
    {
        if (!cursorRestored && cursorCaptured)
        {
            _ = TryRestoreCursorAsync(restoreInput, cursorStart.X, cursorStart.Y);
        }

        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}

static async Task<AcceptanceScenarioResult> RunXButton2TriggeredDragMinimalAsync(AcceptanceOptions options)
{
    const string name = "xbutton2-triggered-drag-minimal";
    const string triggerKey = "mouse:mouse_x2";
    const string mappingId = "xbutton2-drag-minimal";
    const string mappedDragButton = "mouse_middle";
    const string script = "drag_rel 24 12 mouse_middle";
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, $"{name} requires --allow-live-input");
    }

    Process? target = null;
    RuntimeHost? host = null;
    var restoreInput = new WindowsInputBackend();
    var cursorStart = (X: 0, Y: 0);
    var cursorCaptured = false;
    var cursorRestored = false;
    try
    {
        target = await StartPowerShellWindowAsync(
            "BA KeySmith XButton2 Drag Target - safe middle drag",
            TimeSpan.FromSeconds(5));
        if (target is null)
        {
            return Failed(name, watch, "failed to start xbutton2 drag target window");
        }

        var runtimeInput = new CountingInputBackend(new WindowsInputBackend());
        var triggerSender = new WindowsInputBackend(markInjected: false);
        var diagnostics = new CountingDiagnosticsSink();
        host = new RuntimeHost(
            LiveMacroConfig("powershell.exe", mappingId, "mouse_x2", script),
            runtimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(diagnostics),
            diagnostics);

        await host.StartAsync(CancellationToken.None);
        target.Refresh();
        if (!await LiveWindowTools.FocusAsync(target.MainWindowHandle, TimeSpan.FromSeconds(3)) ||
            !LiveWindowTools.MoveCursorToCenter(target.MainWindowHandle))
        {
            return Failed(name, watch, "failed to focus xbutton2 drag target and move cursor");
        }

        runtimeInput.Reset();
        diagnostics.Reset();
        cursorStart = await runtimeInput.GetMousePositionAsync(CancellationToken.None);
        cursorCaptured = true;

        await SendMouseClickAsync(triggerSender, "x2");
        if (!await WaitUntilAsync(
                () => runtimeInput.Count("mouse", mappedDragButton, true) > 0 &&
                      runtimeInput.Count("mouse", mappedDragButton, false) > 0 &&
                      MoveEvents(runtimeInput).Count > 0,
                options.DrainTimeout,
                CancellationToken.None))
        {
            return Failed(name, watch, "xbutton2 drag minimal did not emit complete drag output");
        }

        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            return Failed(name, watch, "queues did not drain after xbutton2 drag minimal scenario");
        }

        var running = host.Snapshot();
        var moveEvents = MoveEvents(runtimeInput);
        var cursorEnd = await runtimeInput.GetMousePositionAsync(CancellationToken.None);
        var triggerDownReceivedCount = CountHookTriggerReceived(diagnostics, triggerKey, "down");
        var triggerUpReceivedCount = CountHookTriggerReceived(diagnostics, triggerKey, "up");
        var pipelineEnqueuedCount = CountPipelineQueued(diagnostics, triggerKey, "down");
        var runtimeDispatchedCount = CountRuntimeDispatched(diagnostics, triggerKey, "down");
        var dragButtonDownCount = runtimeInput.Count("mouse", mappedDragButton, true);
        var dragButtonUpCount = runtimeInput.Count("mouse", mappedDragButton, false);
        var dragStartedCount = dragButtonDownCount;
        var dragCompletedCount = Math.Min(dragButtonDownCount, dragButtonUpCount);
        var moveEventCount = moveEvents.Count;
        var moveSegmentCount = moveEvents.Count;
        var (moveTotalDx, moveTotalDy) = TotalMoveDelta(moveEvents, cursorStart.X, cursorStart.Y);
        var macroStartedCount = CountMacroEvent(diagnostics, "started", $"{mappingId}:macro:");
        var macroFinishedCount = CountMacroEvent(diagnostics, "finished", $"{mappingId}:macro:");

        if (triggerDownReceivedCount == 0 ||
            pipelineEnqueuedCount != 1 ||
            runtimeDispatchedCount != 1 ||
            dragButtonDownCount != 1 ||
            dragButtonUpCount != 1 ||
            moveEventCount != 1 ||
            macroStartedCount != 1 ||
            macroFinishedCount != 1)
        {
            var failedMetrics = new Dictionary<string, object>
            {
                ["validation_scope"] = "phase2d-xbutton2-triggered-drag-minimal",
                ["mapped_drag_button"] = mappedDragButton,
                ["trigger_received_count"] = triggerDownReceivedCount + triggerUpReceivedCount,
                ["pipeline_enqueued_count"] = pipelineEnqueuedCount,
                ["runtime_dispatched_count"] = runtimeDispatchedCount,
                ["drag_started_count"] = dragStartedCount,
                ["drag_button_down_count"] = dragButtonDownCount,
                ["move_event_count"] = moveEventCount,
                ["move_segment_count"] = moveSegmentCount,
                ["drag_button_up_count"] = dragButtonUpCount,
                ["drag_completed_count"] = dragCompletedCount,
                ["cursor_start_x"] = cursorStart.X,
                ["cursor_start_y"] = cursorStart.Y,
                ["cursor_end_x"] = cursorEnd.X,
                ["cursor_end_y"] = cursorEnd.Y,
                ["move_total_dx"] = moveTotalDx,
                ["move_total_dy"] = moveTotalDy,
                ["macro_started_count"] = macroStartedCount,
                ["macro_finished_count"] = macroFinishedCount
            };
            return Failed(name, watch, "drag minimal metrics did not align with expected single-segment drag output", failedMetrics);
        }

        var eventsBeforeStop = runtimeInput.Events.Count;
        var stopWatch = Stopwatch.StartNew();
        await host.StopAsync(CancellationToken.None);
        stopWatch.Stop();
        var stopped = host.Snapshot();
        var eventsAfterStop = runtimeInput.Events.Count;
        if (!IsCleanlyStopped(stopped))
        {
            return Failed(name, watch, "xbutton2 drag minimal runtime did not stop cleanly");
        }

        cursorRestored = await TryRestoreCursorAsync(restoreInput, cursorStart.X, cursorStart.Y);

        var metrics = new Dictionary<string, object>
        {
            ["validation_scope"] = "phase2d-xbutton2-triggered-drag-minimal",
            ["mapped_drag_button"] = mappedDragButton,
            ["trigger_received_count"] = triggerDownReceivedCount + triggerUpReceivedCount,
            ["trigger_down_received_count"] = triggerDownReceivedCount,
            ["trigger_up_received_count"] = triggerUpReceivedCount,
            ["pipeline_enqueued_count"] = pipelineEnqueuedCount,
            ["runtime_dispatched_count"] = runtimeDispatchedCount,
            ["macro_started_count"] = macroStartedCount,
            ["macro_finished_count"] = macroFinishedCount,
            ["drag_started_count"] = dragStartedCount,
            ["drag_button_down_count"] = dragButtonDownCount,
            ["move_event_count"] = moveEventCount,
            ["move_segment_count"] = moveSegmentCount,
            ["drag_button_up_count"] = dragButtonUpCount,
            ["drag_completed_count"] = dragCompletedCount,
            ["cursor_start_x"] = cursorStart.X,
            ["cursor_start_y"] = cursorStart.Y,
            ["cursor_end_x"] = cursorEnd.X,
            ["cursor_end_y"] = cursorEnd.Y,
            ["cursor_restored"] = cursorRestored,
            ["move_total_dx"] = moveTotalDx,
            ["move_total_dy"] = moveTotalDy,
            ["stop_requested_count"] = CountSessionEvent(diagnostics, "stop_requested"),
            ["cleanup_completed_count"] = CountSessionEvent(diagnostics, "stop_completed"),
            ["unexpected_output_after_stop_count"] = eventsAfterStop - eventsBeforeStop,
            ["stop_ms"] = stopWatch.Elapsed.TotalMilliseconds,
            ["diagnostic_events"] = diagnostics.Count,
            ["stopped_clean"] = IsCleanlyStopped(stopped)
        };
        AddSnapshots(metrics, options, ("running", running), ("stopped", stopped));
        return Passed(name, watch, metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message);
    }
    finally
    {
        if (!cursorRestored && cursorCaptured)
        {
            _ = TryRestoreCursorAsync(restoreInput, cursorStart.X, cursorStart.Y);
        }

        if (host is not null)
        {
            await host.DisposeAsync();
        }

        CloseProcessWindow(target);
    }
}

static async Task<AcceptanceScenarioResult> RunXButton2TriggeredMultiSegmentMoveMinimalAsync(AcceptanceOptions options)
{
    const string name = "xbutton2-triggered-multisegment-move-minimal";
    const string triggerKey = "mouse:mouse_x2";
    const string mappingId = "xbutton2-multisegment-move-minimal";
    const string script = "setpos_rel 16 0\nwait 40\nsetpos_rel 0 12\nwait 40\nsetpos_rel -8 6";
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, $"{name} requires --allow-live-input");
    }

    Process? target = null;
    RuntimeHost? host = null;
    var restoreInput = new WindowsInputBackend();
    var cursorStart = (X: 0, Y: 0);
    var cursorCaptured = false;
    var cursorRestored = false;
    try
    {
        target = await StartPowerShellWindowAsync(
            "BA KeySmith XButton2 Multi-Segment Move Target",
            TimeSpan.FromSeconds(5));
        if (target is null)
        {
            return Failed(name, watch, "failed to start xbutton2 multi-segment move target window");
        }

        var runtimeInput = new CountingInputBackend(new WindowsInputBackend());
        var triggerSender = new WindowsInputBackend(markInjected: false);
        var diagnostics = new CountingDiagnosticsSink();
        host = new RuntimeHost(
            LiveMacroConfig("powershell.exe", mappingId, "mouse_x2", script),
            runtimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(diagnostics),
            diagnostics);

        await host.StartAsync(CancellationToken.None);
        target.Refresh();
        if (!await LiveWindowTools.FocusAsync(target.MainWindowHandle, TimeSpan.FromSeconds(3)) ||
            !LiveWindowTools.MoveCursorToCenter(target.MainWindowHandle))
        {
            return Failed(name, watch, "failed to focus xbutton2 multi-segment move target and move cursor");
        }

        runtimeInput.Reset();
        diagnostics.Reset();
        cursorStart = await runtimeInput.GetMousePositionAsync(CancellationToken.None);
        cursorCaptured = true;

        await SendMouseClickAsync(triggerSender, "x2");
        if (!await WaitUntilAsync(
                () => MoveEvents(runtimeInput).Count >= 3,
                options.DrainTimeout,
                CancellationToken.None))
        {
            return Failed(name, watch, "xbutton2 multi-segment move did not emit all move segments");
        }

        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            return Failed(name, watch, "queues did not drain after xbutton2 multi-segment move scenario");
        }

        var running = host.Snapshot();
        var moveEvents = MoveEvents(runtimeInput);
        var cursorEnd = await runtimeInput.GetMousePositionAsync(CancellationToken.None);
        var triggerDownReceivedCount = CountHookTriggerReceived(diagnostics, triggerKey, "down");
        var triggerUpReceivedCount = CountHookTriggerReceived(diagnostics, triggerKey, "up");
        var pipelineEnqueuedCount = CountPipelineQueued(diagnostics, triggerKey, "down");
        var runtimeDispatchedCount = CountRuntimeDispatched(diagnostics, triggerKey, "down");
        var moveEventCount = moveEvents.Count;
        var moveSegmentCount = moveEvents.Count;
        var (moveTotalDx, moveTotalDy) = TotalMoveDelta(moveEvents, cursorStart.X, cursorStart.Y);
        var macroStartedCount = CountMacroEvent(diagnostics, "started", $"{mappingId}:macro:");
        var macroFinishedCount = CountMacroEvent(diagnostics, "finished", $"{mappingId}:macro:");

        if (triggerDownReceivedCount == 0 ||
            pipelineEnqueuedCount != 1 ||
            runtimeDispatchedCount != 1 ||
            moveEventCount != 3 ||
            moveSegmentCount != 3 ||
            macroStartedCount != 1 ||
            macroFinishedCount != 1)
        {
            var failedMetrics = new Dictionary<string, object>
            {
                ["validation_scope"] = "phase2d-xbutton2-triggered-multisegment-move-minimal",
                ["trigger_received_count"] = triggerDownReceivedCount + triggerUpReceivedCount,
                ["pipeline_enqueued_count"] = pipelineEnqueuedCount,
                ["runtime_dispatched_count"] = runtimeDispatchedCount,
                ["drag_started_count"] = 0,
                ["drag_button_down_count"] = 0,
                ["move_event_count"] = moveEventCount,
                ["move_segment_count"] = moveSegmentCount,
                ["drag_button_up_count"] = 0,
                ["drag_completed_count"] = 0,
                ["cursor_start_x"] = cursorStart.X,
                ["cursor_start_y"] = cursorStart.Y,
                ["cursor_end_x"] = cursorEnd.X,
                ["cursor_end_y"] = cursorEnd.Y,
                ["move_total_dx"] = moveTotalDx,
                ["move_total_dy"] = moveTotalDy,
                ["macro_started_count"] = macroStartedCount,
                ["macro_finished_count"] = macroFinishedCount
            };
            return Failed(name, watch, "multi-segment move metrics did not align with expected three-segment move output", failedMetrics);
        }

        var eventsBeforeStop = runtimeInput.Events.Count;
        var stopWatch = Stopwatch.StartNew();
        await host.StopAsync(CancellationToken.None);
        stopWatch.Stop();
        var stopped = host.Snapshot();
        var eventsAfterStop = runtimeInput.Events.Count;
        if (!IsCleanlyStopped(stopped))
        {
            return Failed(name, watch, "xbutton2 multi-segment move runtime did not stop cleanly");
        }

        cursorRestored = await TryRestoreCursorAsync(restoreInput, cursorStart.X, cursorStart.Y);

        var metrics = new Dictionary<string, object>
        {
            ["validation_scope"] = "phase2d-xbutton2-triggered-multisegment-move-minimal",
            ["mapped_drag_button"] = "none",
            ["trigger_received_count"] = triggerDownReceivedCount + triggerUpReceivedCount,
            ["trigger_down_received_count"] = triggerDownReceivedCount,
            ["trigger_up_received_count"] = triggerUpReceivedCount,
            ["pipeline_enqueued_count"] = pipelineEnqueuedCount,
            ["runtime_dispatched_count"] = runtimeDispatchedCount,
            ["macro_started_count"] = macroStartedCount,
            ["macro_finished_count"] = macroFinishedCount,
            ["drag_started_count"] = 0,
            ["drag_button_down_count"] = 0,
            ["move_event_count"] = moveEventCount,
            ["move_segment_count"] = moveSegmentCount,
            ["drag_button_up_count"] = 0,
            ["drag_completed_count"] = 0,
            ["cursor_start_x"] = cursorStart.X,
            ["cursor_start_y"] = cursorStart.Y,
            ["cursor_end_x"] = cursorEnd.X,
            ["cursor_end_y"] = cursorEnd.Y,
            ["cursor_restored"] = cursorRestored,
            ["move_total_dx"] = moveTotalDx,
            ["move_total_dy"] = moveTotalDy,
            ["stop_requested_count"] = CountSessionEvent(diagnostics, "stop_requested"),
            ["cleanup_completed_count"] = CountSessionEvent(diagnostics, "stop_completed"),
            ["unexpected_output_after_stop_count"] = eventsAfterStop - eventsBeforeStop,
            ["stop_ms"] = stopWatch.Elapsed.TotalMilliseconds,
            ["diagnostic_events"] = diagnostics.Count,
            ["stopped_clean"] = IsCleanlyStopped(stopped)
        };
        AddSnapshots(metrics, options, ("running", running), ("stopped", stopped));
        return Passed(name, watch, metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message);
    }
    finally
    {
        if (!cursorRestored && cursorCaptured)
        {
            _ = TryRestoreCursorAsync(restoreInput, cursorStart.X, cursorStart.Y);
        }

        if (host is not null)
        {
            await host.DisposeAsync();
        }

        CloseProcessWindow(target);
    }
}

static async Task<AcceptanceScenarioResult> RunCapturedRepeatDownDoesNotRedispatchAsync(AcceptanceOptions options)
{
    const string name = "captured-repeat-down-does-not-redispatch";
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, $"{name} requires --allow-live-input");
    }

    using var target = new LiveHarnessWindow("BA KeySmith Captured Repeat-Down Target");
    RuntimeHost? host = null;
    try
    {
        target.Start(TimeSpan.FromSeconds(3));
        var runtimeInput = new CountingInputBackend(new WindowsInputBackend());
        var triggerSender = new WindowsInputBackend(markInjected: false);
        var diagnostics = new CountingDiagnosticsSink();
        host = new RuntimeHost(
            LiveTapConfig(CurrentProcessTargetName(), "f13", "f14"),
            runtimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(),
            diagnostics);

        await host.StartAsync(CancellationToken.None);
        if (!await target.FocusAsync(TimeSpan.FromSeconds(3)))
        {
            return Failed(name, watch, "failed to focus repeat-down target");
        }

        var originalKeyDownBefore = target.KeyDownCountFor(VirtualKeyF13);
        var originalKeyUpBefore = target.KeyUpCountFor(VirtualKeyF13);
        var before = host.Snapshot();

        await triggerSender.KeyDownAsync("f13", CancellationToken.None);
        await Task.Delay(40, CancellationToken.None);
        await triggerSender.KeyDownAsync("f13", CancellationToken.None);
        await Task.Delay(40, CancellationToken.None);
        await triggerSender.KeyUpAsync("f13", CancellationToken.None);

        if (!await WaitForInputCountAsync(runtimeInput, "key", "f14", 2, options.DrainTimeout))
        {
            return Failed(name, watch, "captured repeat-down sequence did not produce the expected single mapped tap");
        }

        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            return Failed(name, watch, "queues did not drain after captured repeat-down sequence");
        }

        var after = host.Snapshot();
        await host.StopAsync(CancellationToken.None);
        var stopped = host.Snapshot();
        if (!IsCleanlyStopped(stopped))
        {
            return Failed(name, watch, "repeat-down host did not stop cleanly");
        }

        var originalKeyDownDelta = target.KeyDownCountFor(VirtualKeyF13) - originalKeyDownBefore;
        var originalKeyUpDelta = target.KeyUpCountFor(VirtualKeyF13) - originalKeyUpBefore;
        var mappedF14Events = runtimeInput.Count("key", "f14");
        var queuedDelta = after.Pipeline.QueuedCount - before.Pipeline.QueuedCount;
        var handledDelta = after.Pipeline.HandledCount - before.Pipeline.HandledCount;
        if (mappedF14Events != 2)
        {
            return Failed(name, watch, "captured repeat-down redispatched mapped output");
        }

        if (queuedDelta != 2 || handledDelta != 2)
        {
            return Failed(name, watch, "captured repeat-down should queue and handle only the first down plus matching up");
        }

        if (originalKeyDownDelta != 0 || originalKeyUpDelta != 0)
        {
            return Failed(name, watch, "captured repeat-down should remain suppressed at the focused target");
        }

        var metrics = new Dictionary<string, object>
        {
            ["original_key_down_delta"] = originalKeyDownDelta,
            ["original_key_up_delta"] = originalKeyUpDelta,
            ["mapped_f14_events"] = mappedF14Events,
            ["pipeline_queued_delta"] = queuedDelta,
            ["pipeline_handled_delta"] = handledDelta,
            ["diagnostic_events"] = diagnostics.Count,
            ["stopped_clean"] = IsCleanlyStopped(stopped)
        };
        AddSnapshots(metrics, options, ("before", before), ("after", after), ("stopped", stopped));
        return Passed(name, watch, metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message);
    }
    finally
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}

static async Task<AcceptanceScenarioResult> RunKeyboardTriggerGatedSuppressAsync(AcceptanceOptions options)
{
    const string name = "keyboard-trigger-gated-suppress";
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, $"{name} requires --allow-live-input");
    }

    using var allowedTarget = new LiveHarnessWindow("BA KeySmith Keyboard Suppress Target");
    using var blockedTarget = new LiveHarnessWindow("BA KeySmith Keyboard Blocked Pass-Through");
    RuntimeHost? allowedHost = null;
    RuntimeHost? blockedHost = null;
    try
    {
        allowedTarget.Start(TimeSpan.FromSeconds(3));
        var allowedRuntimeInput = new CountingInputBackend(new WindowsInputBackend());
        var triggerSender = new WindowsInputBackend(markInjected: false);
        allowedHost = new RuntimeHost(
            LiveTapConfig(CurrentProcessTargetName(), "f13", "f14"),
            allowedRuntimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(),
            new CountingDiagnosticsSink());

        await allowedHost.StartAsync(CancellationToken.None);
        if (!await allowedTarget.FocusAsync(TimeSpan.FromSeconds(3)))
        {
            return Failed(name, watch, "failed to focus allowed keyboard target");
        }

        var allowedKeyDownBefore = allowedTarget.KeyDownCountFor(VirtualKeyF13);
        var allowedKeyUpBefore = allowedTarget.KeyUpCountFor(VirtualKeyF13);
        await SendTriggerTapAsync(triggerSender, "f13");
        if (!await WaitForInputCountAsync(allowedRuntimeInput, "key", "f14", 2, options.DrainTimeout))
        {
            return Failed(name, watch, "allowed keyboard trigger did not produce mapped f14 output");
        }

        await allowedHost.StopAsync(CancellationToken.None);
        var allowedStopped = allowedHost.Snapshot();
        if (!IsCleanlyStopped(allowedStopped))
        {
            return Failed(name, watch, "allowed keyboard host did not stop cleanly");
        }

        var allowedOriginalDownDelta = allowedTarget.KeyDownCountFor(VirtualKeyF13) - allowedKeyDownBefore;
        var allowedOriginalUpDelta = allowedTarget.KeyUpCountFor(VirtualKeyF13) - allowedKeyUpBefore;
        if (allowedOriginalDownDelta != 0 || allowedOriginalUpDelta != 0)
        {
            return Failed(name, watch, "allowed keyboard trigger was not suppressed");
        }

        blockedTarget.Start(TimeSpan.FromSeconds(3));
        var blockedRuntimeInput = new CountingInputBackend(new WindowsInputBackend());
        blockedHost = new RuntimeHost(
            LiveTapConfig("powershell.exe", "f13", "f14"),
            blockedRuntimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(),
            new CountingDiagnosticsSink());

        await blockedHost.StartAsync(CancellationToken.None);
        if (!await blockedTarget.FocusAsync(TimeSpan.FromSeconds(3)))
        {
            return Failed(name, watch, "failed to focus blocked keyboard target");
        }

        var blockedKeyDownBefore = blockedTarget.KeyDownCountFor(VirtualKeyF13);
        var blockedKeyUpBefore = blockedTarget.KeyUpCountFor(VirtualKeyF13);
        await SendTriggerTapAsync(triggerSender, "f13");
        await Task.Delay(150);
        await blockedHost.StopAsync(CancellationToken.None);
        var blockedStopped = blockedHost.Snapshot();
        if (!IsCleanlyStopped(blockedStopped))
        {
            return Failed(name, watch, "blocked keyboard host did not stop cleanly");
        }

        var blockedOriginalDownDelta = blockedTarget.KeyDownCountFor(VirtualKeyF13) - blockedKeyDownBefore;
        var blockedOriginalUpDelta = blockedTarget.KeyUpCountFor(VirtualKeyF13) - blockedKeyUpBefore;
        if (blockedRuntimeInput.Events.Count != 0)
        {
            return Failed(name, watch, "blocked keyboard trigger produced mapped output");
        }

        if (blockedOriginalDownDelta == 0 || blockedOriginalUpDelta == 0)
        {
            return Failed(name, watch, "blocked keyboard trigger did not pass through");
        }

        var metrics = new Dictionary<string, object>
        {
            ["allowed_original_key_down_delta"] = allowedOriginalDownDelta,
            ["allowed_original_key_up_delta"] = allowedOriginalUpDelta,
            ["allowed_mapped_f14_events"] = allowedRuntimeInput.Count("key", "f14"),
            ["blocked_original_key_down_delta"] = blockedOriginalDownDelta,
            ["blocked_original_key_up_delta"] = blockedOriginalUpDelta,
            ["blocked_mapped_output_events"] = blockedRuntimeInput.Events.Count,
            ["allowed_stopped_clean"] = IsCleanlyStopped(allowedStopped),
            ["blocked_stopped_clean"] = IsCleanlyStopped(blockedStopped)
        };
        AddSnapshots(metrics, options, ("allowed_stopped", allowedStopped), ("blocked_stopped", blockedStopped));
        return Passed(name, watch, metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message);
    }
    finally
    {
        if (allowedHost is not null)
        {
            await allowedHost.DisposeAsync();
        }

        if (blockedHost is not null)
        {
            await blockedHost.DisposeAsync();
        }
    }
}

static async Task<AcceptanceScenarioResult> RunBlueArchiveManualAsync(AcceptanceOptions options)
{
    const string name = "bluearchive-manual";
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, $"{name} requires --allow-live-input");
    }

    var targetProcess = options.TargetProcess;
    var targetWindow = FindTargetProcessWithWindow(targetProcess);
    if (targetWindow is null)
    {
        return Failed(name, watch, $"target process with foreground-capable window was not found: {targetProcess}");
    }

    var manualTrigger = KeyNameResolver.Normalize(options.ManualKeyboardTrigger);
    var manualTriggerInfo = KeyNameResolver.ResolveKeyboardKey(manualTrigger);
    var runnerElevation = ProbeCurrentProcessElevation();
    var targetElevation = ProbeProcessElevation(targetWindow.Id, $"{targetWindow.ProcessName}.exe");
    Dictionary<string, object> CreateMetrics()
    {
        return new Dictionary<string, object>
        {
            ["target_process"] = targetProcess,
            ["target_process_id"] = targetWindow.Id,
            ["target_window_title"] = targetWindow.MainWindowTitle,
            ["manual_trigger"] = manualTrigger,
            ["manual_trigger_mode"] = "physical_keyboard_only",
            ["validation_scope"] = "keyboard-minimal",
            ["runner_elevation_known"] = runnerElevation.IsKnown,
            ["runner_elevated"] = runnerElevation.IsKnown && runnerElevation.IsElevated,
            ["runner_elevated_state"] = ElevationStateLabel(runnerElevation),
            ["target_elevation_known"] = targetElevation.IsKnown,
            ["target_elevated"] = targetElevation.IsKnown && targetElevation.IsElevated,
            ["target_elevated_state"] = ElevationStateLabel(targetElevation),
            ["runner_target_same_elevation"] = runnerElevation.IsKnown &&
                targetElevation.IsKnown &&
                runnerElevation.IsElevated == targetElevation.IsElevated
        };
    }

    if (!runnerElevation.IsKnown || !runnerElevation.IsElevated)
    {
        return Failed(
            name,
            watch,
            "bluearchive-manual requires an elevated acceptance/headless/live runner. Current runner is not elevated, so this real-target validation is invalid.",
            CreateMetrics());
    }

    using var blocker = new LiveHarnessWindow("BA KeySmith Real Target Blocker");
    RuntimeHost? host = null;
    try
    {
        blocker.Start(TimeSpan.FromSeconds(3));
        var runtimeInput = new CountingInputBackend(new WindowsInputBackend());
        var diagnostics = new CountingDiagnosticsSink();

        host = new RuntimeHost(
            LiveEscTapConfig(targetProcess, manualTrigger),
            runtimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(),
            diagnostics);

        await host.StartAsync(CancellationToken.None);
        targetWindow.Refresh();
        if (!await LiveWindowTools.FocusAsync(targetWindow.MainWindowHandle, TimeSpan.FromSeconds(5)))
        {
            return Failed(name, watch, $"failed to focus target window: {targetWindow.ProcessName}");
        }

        var allowedGate = await host.CheckForegroundAsync(CancellationToken.None);
        if (!allowedGate.IsAllowed)
        {
            return Failed(
                name,
                watch,
                $"target foreground gate was not allowed: foreground={allowedGate.ForegroundProcess}");
        }

        Console.Error.WriteLine(
            $"MANUAL keyboard-allowed: target focused. Physically press '{manualTrigger}' once within {options.ManualTimeout.TotalSeconds:0}s. Expect raw trigger suppressed and mapped 'escape' output only.");
        if (!await WaitForInputCountAsync(runtimeInput, "key", "escape", 2, options.ManualTimeout))
        {
            var failed = host.Snapshot();
            var keyboardFailureMetrics = CreateMetrics();
            keyboardFailureMetrics["foreground_process"] = allowedGate.ForegroundProcess ?? string.Empty;
            keyboardFailureMetrics["foreground_allowed"] = allowedGate.IsAllowed;
            keyboardFailureMetrics["keyboard_escape_events"] = runtimeInput.Count("key", "escape");
            keyboardFailureMetrics["pipeline_queued"] = failed.Pipeline.QueuedCount;
            keyboardFailureMetrics["pipeline_handled"] = failed.Pipeline.HandledCount;
            keyboardFailureMetrics["pipeline_dropped"] = failed.Pipeline.DroppedCount;
            keyboardFailureMetrics["pipeline_pending"] = failed.Pipeline.PendingCount;
            keyboardFailureMetrics["runtime_pending_actions"] = failed.Runtime.PendingActionCount;
            keyboardFailureMetrics["runtime_running_actions"] = failed.Runtime.RunningActionCount;
            keyboardFailureMetrics["held_key_count"] = failed.Runtime.Presses.KeyOwners.Count;
            keyboardFailureMetrics["press_owner_count"] = failed.Runtime.Presses.OwnerKeys.Count;
            return Failed(
                name,
                watch,
                "manual keyboard trigger did not produce escape output in target foreground",
                keyboardFailureMetrics);
        }

        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            return Failed(name, watch, "queues did not drain after keyboard target trigger");
        }

        var keyboardOutput = runtimeInput.Count("key", "escape");
        var manualSuppressConfirmed = ResolveManualConfirmation(
            options,
            $"Did Blue Archive react only to mapped 'escape' and not the raw '{manualTrigger}' trigger?");
        if (manualSuppressConfirmed is null)
        {
            var confirmationMetrics = CreateMetrics();
            confirmationMetrics["keyboard_escape_events"] = keyboardOutput;
            return Failed(name, watch, "bluearchive-manual requires interactive console confirmation for manual observation.", confirmationMetrics);
        }

        if (manualSuppressConfirmed is not true)
        {
            var manualObservationMetrics = CreateMetrics();
            manualObservationMetrics["keyboard_escape_events"] = keyboardOutput;
            return Failed(name, watch, "manual observation did not confirm keyboard trigger suppress in target foreground", manualObservationMetrics);
        }

        if (!await blocker.FocusAsync(TimeSpan.FromSeconds(5)))
        {
            return Failed(name, watch, "failed to focus blocker for foreground-blocked keyboard pass-through");
        }

        var blockedGate = await host.CheckForegroundAsync(CancellationToken.None);
        if (blockedGate.IsAllowed)
        {
            var blockedGateMetrics = CreateMetrics();
            blockedGateMetrics["blocked_foreground_process"] = blockedGate.ForegroundProcess ?? string.Empty;
            return Failed(name, watch, "blocker foreground was unexpectedly allowed for target process", blockedGateMetrics);
        }

        var blockerKeyDownBefore = blocker.KeyDownCountFor(manualTriggerInfo.VirtualKey);
        var outputBeforeBlocked = runtimeInput.Count("key", "escape");
        Console.Error.WriteLine(
            $"MANUAL keyboard-blocked: blocker focused. Physically press '{manualTrigger}' once within {options.ManualTimeout.TotalSeconds:0}s. Expect raw key to pass through to blocker and no mapped output.");
        if (!await WaitUntilAsync(
                () => blocker.KeyDownCountFor(manualTriggerInfo.VirtualKey) > blockerKeyDownBefore,
                options.ManualTimeout,
                CancellationToken.None))
        {
            var blockedPassThroughMetrics = CreateMetrics();
            blockedPassThroughMetrics["blocked_foreground_process"] = blockedGate.ForegroundProcess ?? string.Empty;
            return Failed(name, watch, "foreground-blocked keyboard trigger did not pass through to blocker", blockedPassThroughMetrics);
        }

        await Task.Delay(150);
        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            return Failed(name, watch, "queues did not drain after foreground-blocked keyboard trigger");
        }

        var blockedOutputDelta = runtimeInput.Count("key", "escape") - outputBeforeBlocked;
        var blockedKeyboardPassThrough = blocker.KeyDownCountFor(manualTriggerInfo.VirtualKey) - blockerKeyDownBefore;
        if (blockedOutputDelta != 0)
        {
            var blockedOutputMetrics = CreateMetrics();
            blockedOutputMetrics["foreground_blocked_output_delta"] = blockedOutputDelta;
            return Failed(name, watch, "foreground-blocked keyboard trigger produced mapped output", blockedOutputMetrics);
        }

        if (blockedKeyboardPassThrough == 0)
        {
            var blockedKeyboardMetrics = CreateMetrics();
            blockedKeyboardMetrics["foreground_blocked_output_delta"] = blockedOutputDelta;
            blockedKeyboardMetrics["foreground_blocked_keyboard_pass_through"] = blockedKeyboardPassThrough;
            return Failed(name, watch, "foreground-blocked keyboard trigger did not pass through to blocker", blockedKeyboardMetrics);
        }

        var stopWatch = Stopwatch.StartNew();
        await host.StopAsync(CancellationToken.None);
        stopWatch.Stop();
        var stopped = host.Snapshot();
        if (!IsCleanlyStopped(stopped))
        {
            return Failed(name, watch, "runtime did not stop cleanly after real target validation");
        }

        await host.DisposeAsync();
        host = null;

        var metrics = CreateMetrics();
        metrics["foreground_process"] = allowedGate.ForegroundProcess ?? string.Empty;
        metrics["blocked_foreground_process"] = blockedGate.ForegroundProcess ?? string.Empty;
        metrics["keyboard_escape_events"] = keyboardOutput;
        metrics["foreground_blocked_output_delta"] = blockedOutputDelta;
        metrics["foreground_blocked_keyboard_pass_through"] = blockedKeyboardPassThrough;
        metrics["manual_suppress_confirmed"] = true;
        metrics["stop_ms"] = stopWatch.Elapsed.TotalMilliseconds;
        metrics["diagnostic_events"] = diagnostics.Count;
        metrics["stopped_clean"] = IsCleanlyStopped(stopped);
        AddSnapshots(metrics, options, ("stopped", stopped));
        return Passed(name, watch, metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message, CreateMetrics());
    }
    finally
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}

static async Task<AcceptanceScenarioResult> RunBlueArchiveManualWheelAsync(AcceptanceOptions options)
{
    const string name = "bluearchive-manual-wheel";
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, $"{name} requires --allow-live-input");
    }

    var targetProcess = options.TargetProcess;
    var targetWindow = FindTargetProcessWithWindow(targetProcess);
    if (targetWindow is null)
    {
        return Failed(name, watch, $"target process with foreground-capable window was not found: {targetProcess}");
    }

    var wheelTrigger = KeyNameResolver.NormalizeMouseTrigger(options.ManualWheelTrigger);
    if (wheelTrigger is not "mouse_wheel_up" and not "mouse_wheel_down")
    {
        return Failed(name, watch, $"manual wheel trigger must be mouse_wheel_up or mouse_wheel_down, but was: {wheelTrigger}");
    }

    static string WheelDirectionPrompt(string trigger) =>
        string.Equals(trigger, "mouse_wheel_up", StringComparison.OrdinalIgnoreCase)
            ? "scroll upward"
            : "scroll downward";

    static long WheelPassThroughCount(LiveHarnessWindow blocker, string trigger) =>
        string.Equals(trigger, "mouse_wheel_up", StringComparison.OrdinalIgnoreCase)
            ? blocker.WheelUpCount
            : blocker.WheelDownCount;

    var wheelPrompt = WheelDirectionPrompt(wheelTrigger);
    var wheelTriggerKey = $"mouse:{wheelTrigger}";
    var runnerElevation = ProbeCurrentProcessElevation();
    var targetElevation = ProbeProcessElevation(targetWindow.Id, $"{targetWindow.ProcessName}.exe");
    var realTargetObserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var realTargetPending = new HashSet<string>(["wheel_allowed"], StringComparer.OrdinalIgnoreCase);
    var realTargetFailed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var blockerObserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var blockerPending = new HashSet<string>(["wheel_blocked_pass_through"], StringComparer.OrdinalIgnoreCase);
    var blockerFailed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var detailMetrics = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
    {
        ["manual_wheel_confirmed"] = false
    };

    static string ScopeString(IEnumerable<string> orderedSteps, IReadOnlySet<string> activeSteps)
    {
        return string.Join(",", orderedSteps.Where(activeSteps.Contains));
    }

    static string StageMetricKey(string stage, string suffix) => $"stage_{stage}_{suffix}";

    void SetMetric(string key, object value)
    {
        detailMetrics[key] = value;
    }

    void MarkObserved(string step, bool realTarget)
    {
        if (realTarget)
        {
            realTargetPending.Remove(step);
            realTargetFailed.Remove(step);
            realTargetObserved.Add(step);
            return;
        }

        blockerPending.Remove(step);
        blockerFailed.Remove(step);
        blockerObserved.Add(step);
    }

    void MarkFailed(string step, bool realTarget)
    {
        if (realTarget)
        {
            realTargetObserved.Remove(step);
            realTargetPending.Remove(step);
            realTargetFailed.Add(step);
            return;
        }

        blockerObserved.Remove(step);
        blockerPending.Remove(step);
        blockerFailed.Add(step);
    }

    foreach (var stage in new[] { "wheel_allowed", "wheel_blocked" })
    {
        SetMetric(StageMetricKey(stage, "entered"), false);
        SetMetric(StageMetricKey(stage, "completed"), false);
        SetMetric(StageMetricKey(stage, "manual_required"), stage == "wheel_allowed");
        SetMetric(StageMetricKey(stage, "manual_confirmed"), false);
        SetMetric(StageMetricKey(stage, "trigger_received_count"), 0);
        SetMetric(StageMetricKey(stage, "pipeline_enqueued_count"), 0);
        SetMetric(StageMetricKey(stage, "runtime_dispatched_count"), 0);
        SetMetric(StageMetricKey(stage, "runtime_foreground_allowed_count"), 0);
        SetMetric(StageMetricKey(stage, "runtime_ignored_count"), 0);
        SetMetric(StageMetricKey(stage, "runtime_ignored_reasons"), string.Empty);
        SetMetric(StageMetricKey(stage, "escape_down_count"), 0);
        SetMetric(StageMetricKey(stage, "escape_up_count"), 0);
        SetMetric(StageMetricKey(stage, "blocked_pass_through_count"), 0);
        SetMetric(StageMetricKey(stage, "output_delta"), 0);
        SetMetric(StageMetricKey(stage, "failure_reason"), string.Empty);
    }

    Dictionary<string, object> CreateMetrics()
    {
        var metrics = new Dictionary<string, object>
        {
            ["target_process"] = targetProcess,
            ["target_process_id"] = targetWindow.Id,
            ["target_window_title"] = targetWindow.MainWindowTitle,
            ["validation_scope"] = "phase2b-wheel-only",
            ["manual_wheel_trigger"] = wheelTrigger,
            ["manual_wheel_prompt"] = wheelPrompt,
            ["runner_elevation_known"] = runnerElevation.IsKnown,
            ["runner_elevated"] = runnerElevation.IsKnown && runnerElevation.IsElevated,
            ["runner_elevated_state"] = ElevationStateLabel(runnerElevation),
            ["target_elevation_known"] = targetElevation.IsKnown,
            ["target_elevated"] = targetElevation.IsKnown && targetElevation.IsElevated,
            ["target_elevated_state"] = ElevationStateLabel(targetElevation),
            ["runner_target_same_elevation"] = runnerElevation.IsKnown &&
                targetElevation.IsKnown &&
                runnerElevation.IsElevated == targetElevation.IsElevated,
            ["real_target_observed_scope"] = ScopeString(["wheel_allowed"], realTargetObserved),
            ["real_target_pending_scope"] = ScopeString(["wheel_allowed"], realTargetPending),
            ["real_target_failed_scope"] = ScopeString(["wheel_allowed"], realTargetFailed),
            ["blocker_observed_scope"] = ScopeString(["wheel_blocked_pass_through"], blockerObserved),
            ["blocker_pending_scope"] = ScopeString(["wheel_blocked_pass_through"], blockerPending),
            ["blocker_failed_scope"] = ScopeString(["wheel_blocked_pass_through"], blockerFailed)
        };

        foreach (var (key, value) in detailMetrics)
        {
            metrics[key] = value;
        }

        return metrics;
    }

    void RecordWheelStage(
        string stage,
        CountingDiagnosticsSink diagnostics,
        CountingInputBackend runtimeInput,
        int escapeDownCount,
        int escapeUpCount,
        long blockedPassThroughCount,
        int outputDelta,
        bool completed,
        bool manualConfirmed,
        string failureReason)
    {
        var ignored = CollectRuntimeIgnored(diagnostics, wheelTriggerKey);
        SetMetric(StageMetricKey(stage, "entered"), true);
        SetMetric(StageMetricKey(stage, "completed"), completed);
        SetMetric(StageMetricKey(stage, "manual_confirmed"), manualConfirmed);
        SetMetric(StageMetricKey(stage, "trigger_received_count"), CountHookTriggerReceived(diagnostics, wheelTriggerKey));
        SetMetric(StageMetricKey(stage, "pipeline_enqueued_count"), CountPipelineQueued(diagnostics, wheelTriggerKey));
        SetMetric(StageMetricKey(stage, "runtime_dispatched_count"), CountRuntimeDispatched(diagnostics, wheelTriggerKey));
        SetMetric(StageMetricKey(stage, "runtime_foreground_allowed_count"), CountRuntimeForegroundAllowed(diagnostics));
        SetMetric(StageMetricKey(stage, "runtime_ignored_count"), ignored.Length);
        SetMetric(StageMetricKey(stage, "runtime_ignored_reasons"), JoinIgnoredReasons(ignored));
        SetMetric(StageMetricKey(stage, "escape_down_count"), escapeDownCount);
        SetMetric(StageMetricKey(stage, "escape_up_count"), escapeUpCount);
        SetMetric(StageMetricKey(stage, "blocked_pass_through_count"), blockedPassThroughCount);
        SetMetric(StageMetricKey(stage, "output_delta"), outputDelta);
        SetMetric(StageMetricKey(stage, "failure_reason"), failureReason);
    }

    if (!runnerElevation.IsKnown || !runnerElevation.IsElevated)
    {
        return Failed(
            name,
            watch,
            $"{name} requires an elevated acceptance/headless/live runner. Current runner is not elevated, so this real-target validation is invalid.",
            CreateMetrics());
    }

    using var blocker = new LiveHarnessWindow("BA KeySmith Real Target Wheel Blocker");
    RuntimeHost? host = null;
    try
    {
        blocker.Start(TimeSpan.FromSeconds(3));
        var runtimeInput = new CountingInputBackend(new WindowsInputBackend());
        var diagnostics = new CountingDiagnosticsSink();

        host = new RuntimeHost(
            LiveEscTapConfig(targetProcess, wheelTrigger),
            runtimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(diagnostics),
            diagnostics);

        await host.StartAsync(CancellationToken.None);
        targetWindow.Refresh();
        if (!await LiveWindowTools.FocusAsync(targetWindow.MainWindowHandle, TimeSpan.FromSeconds(5)) ||
            !LiveWindowTools.MoveCursorToCenter(targetWindow.MainWindowHandle))
        {
            return Failed(name, watch, $"failed to focus target window and move cursor: {targetWindow.ProcessName}", CreateMetrics());
        }

        var allowedGate = await host.CheckForegroundAsync(CancellationToken.None);
        if (!allowedGate.IsAllowed)
        {
            var gateMetrics = CreateMetrics();
            gateMetrics["foreground_process"] = allowedGate.ForegroundProcess ?? string.Empty;
            return Failed(
                name,
                watch,
                $"target foreground gate was not allowed: foreground={allowedGate.ForegroundProcess}",
                gateMetrics);
        }

        runtimeInput.Reset();
        diagnostics.Reset();
        Console.Error.WriteLine(
            $"MANUAL wheel-allowed: Blue Archive focused. Physically {wheelPrompt} once within {options.ManualTimeout.TotalSeconds:0}s. Expect mapped 'escape' effect.");
        if (!await WaitForInputCountAsync(runtimeInput, "key", "escape", 2, options.ManualTimeout))
        {
            RecordWheelStage(
                "wheel_allowed",
                diagnostics,
                runtimeInput,
                runtimeInput.Count("key", "escape", true),
                runtimeInput.Count("key", "escape", false),
                blockedPassThroughCount: 0,
                outputDelta: runtimeInput.Count("key", "escape"),
                completed: false,
                manualConfirmed: false,
                failureReason: "awaiting_escape_output");
            MarkFailed("wheel_allowed", realTarget: true);
            return Failed(name, watch, "manual wheel trigger did not produce escape output in target foreground", CreateMetrics());
        }

        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            RecordWheelStage(
                "wheel_allowed",
                diagnostics,
                runtimeInput,
                runtimeInput.Count("key", "escape", true),
                runtimeInput.Count("key", "escape", false),
                blockedPassThroughCount: 0,
                outputDelta: runtimeInput.Count("key", "escape"),
                completed: false,
                manualConfirmed: false,
                failureReason: "drain_timeout");
            MarkFailed("wheel_allowed", realTarget: true);
            return Failed(name, watch, "queues did not drain after wheel target trigger", CreateMetrics());
        }

        var wheelAllowedEscapeEvents = runtimeInput.Count("key", "escape");
        var manualWheelConfirmed = ResolveManualConfirmation(
            options,
            $"Did Blue Archive react to '{wheelTrigger}' with the mapped 'escape' effect?");
        if (manualWheelConfirmed is null)
        {
            RecordWheelStage(
                "wheel_allowed",
                diagnostics,
                runtimeInput,
                runtimeInput.Count("key", "escape", true),
                runtimeInput.Count("key", "escape", false),
                blockedPassThroughCount: 0,
                outputDelta: wheelAllowedEscapeEvents,
                completed: false,
                manualConfirmed: false,
                failureReason: "awaiting_manual_confirmation");
            MarkFailed("wheel_allowed", realTarget: true);
            return Failed(name, watch, "bluearchive-manual-wheel requires manual confirmation for the allowed wheel observation.", CreateMetrics());
        }

        if (manualWheelConfirmed is not true)
        {
            RecordWheelStage(
                "wheel_allowed",
                diagnostics,
                runtimeInput,
                runtimeInput.Count("key", "escape", true),
                runtimeInput.Count("key", "escape", false),
                blockedPassThroughCount: 0,
                outputDelta: wheelAllowedEscapeEvents,
                completed: false,
                manualConfirmed: false,
                failureReason: "manual_confirmation_negative");
            MarkFailed("wheel_allowed", realTarget: true);
            return Failed(name, watch, "manual observation did not confirm wheel trigger behavior in real target", CreateMetrics());
        }

        RecordWheelStage(
            "wheel_allowed",
            diagnostics,
            runtimeInput,
            runtimeInput.Count("key", "escape", true),
            runtimeInput.Count("key", "escape", false),
            blockedPassThroughCount: 0,
            outputDelta: wheelAllowedEscapeEvents,
            completed: true,
            manualConfirmed: true,
            failureReason: string.Empty);
        SetMetric("manual_wheel_confirmed", true);
        SetMetric("wheel_allowed_mapped_escape_events", wheelAllowedEscapeEvents);
        MarkObserved("wheel_allowed", realTarget: true);

        if (!await blocker.FocusAsync(TimeSpan.FromSeconds(5)) ||
            !LiveWindowTools.MoveCursorToCenter(blocker.Handle))
        {
            return Failed(name, watch, "failed to focus blocker for foreground-blocked wheel pass-through", CreateMetrics());
        }

        var blockedGate = await host.CheckForegroundAsync(CancellationToken.None);
        if (blockedGate.IsAllowed)
        {
            var blockedGateMetrics = CreateMetrics();
            blockedGateMetrics["blocked_foreground_process"] = blockedGate.ForegroundProcess ?? string.Empty;
            return Failed(name, watch, "blocker foreground was unexpectedly allowed for target process", blockedGateMetrics);
        }

        runtimeInput.Reset();
        diagnostics.Reset();
        blocker.Reset();
        Console.Error.WriteLine(
            $"MANUAL wheel-blocked: blocker focused. Physically {wheelPrompt} once within {options.ManualTimeout.TotalSeconds:0}s. Expect raw wheel input to pass through to blocker and no mapped output.");
        if (!await WaitUntilAsync(
                () => WheelPassThroughCount(blocker, wheelTrigger) > 0,
                options.ManualTimeout,
                CancellationToken.None))
        {
            RecordWheelStage(
                "wheel_blocked",
                diagnostics,
                runtimeInput,
                runtimeInput.Count("key", "escape", true),
                runtimeInput.Count("key", "escape", false),
                blockedPassThroughCount: WheelPassThroughCount(blocker, wheelTrigger),
                outputDelta: runtimeInput.Count("key", "escape"),
                completed: false,
                manualConfirmed: false,
                failureReason: "awaiting_blocked_pass_through");
            MarkFailed("wheel_blocked_pass_through", realTarget: false);
            return Failed(name, watch, "foreground-blocked wheel trigger did not pass through to blocker", CreateMetrics());
        }

        await Task.Delay(150, CancellationToken.None);
        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            RecordWheelStage(
                "wheel_blocked",
                diagnostics,
                runtimeInput,
                runtimeInput.Count("key", "escape", true),
                runtimeInput.Count("key", "escape", false),
                blockedPassThroughCount: WheelPassThroughCount(blocker, wheelTrigger),
                outputDelta: runtimeInput.Count("key", "escape"),
                completed: false,
                manualConfirmed: false,
                failureReason: "drain_timeout");
            MarkFailed("wheel_blocked_pass_through", realTarget: false);
            return Failed(name, watch, "queues did not drain after blocked wheel trigger", CreateMetrics());
        }

        var blockedOutputDelta = runtimeInput.Count("key", "escape");
        var blockedWheelPassThrough = WheelPassThroughCount(blocker, wheelTrigger);
        if (blockedOutputDelta != 0)
        {
            RecordWheelStage(
                "wheel_blocked",
                diagnostics,
                runtimeInput,
                runtimeInput.Count("key", "escape", true),
                runtimeInput.Count("key", "escape", false),
                blockedPassThroughCount: blockedWheelPassThrough,
                outputDelta: blockedOutputDelta,
                completed: false,
                manualConfirmed: false,
                failureReason: "unexpected_mapped_output");
            MarkFailed("wheel_blocked_pass_through", realTarget: false);
            return Failed(name, watch, "foreground-blocked wheel trigger produced mapped output", CreateMetrics());
        }

        RecordWheelStage(
            "wheel_blocked",
            diagnostics,
            runtimeInput,
            runtimeInput.Count("key", "escape", true),
            runtimeInput.Count("key", "escape", false),
            blockedPassThroughCount: blockedWheelPassThrough,
            outputDelta: blockedOutputDelta,
            completed: true,
            manualConfirmed: false,
            failureReason: string.Empty);
        SetMetric("wheel_blocked_pass_through_count", blockedWheelPassThrough);
        SetMetric("wheel_blocked_output_delta", blockedOutputDelta);
        MarkObserved("wheel_blocked_pass_through", realTarget: false);

        var stopWatch = Stopwatch.StartNew();
        await host.StopAsync(CancellationToken.None);
        stopWatch.Stop();
        var stopped = host.Snapshot();
        if (!IsCleanlyStopped(stopped))
        {
            return Failed(name, watch, "runtime did not stop cleanly after wheel real target validation", CreateMetrics());
        }

        await host.DisposeAsync();
        host = null;

        var metrics = CreateMetrics();
        metrics["foreground_process"] = allowedGate.ForegroundProcess ?? string.Empty;
        metrics["blocked_foreground_process"] = blockedGate.ForegroundProcess ?? string.Empty;
        metrics["wheel_allowed_mapped_escape_events"] = wheelAllowedEscapeEvents;
        metrics["wheel_blocked_pass_through_count"] = blockedWheelPassThrough;
        metrics["wheel_blocked_output_delta"] = blockedOutputDelta;
        metrics["stop_ms"] = stopWatch.Elapsed.TotalMilliseconds;
        metrics["diagnostic_events"] = diagnostics.Count;
        metrics["stopped_clean"] = IsCleanlyStopped(stopped);
        AddSnapshots(metrics, options, ("stopped", stopped));
        return Passed(name, watch, metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message, CreateMetrics());
    }
    finally
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}

static async Task<AcceptanceScenarioResult> RunBlueArchiveManualXButtonAsync(AcceptanceOptions options)
{
    const string name = "bluearchive-manual-xbutton";
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, $"{name} requires --allow-live-input");
    }

    var targetProcess = options.TargetProcess;
    var targetWindow = FindTargetProcessWithWindow(targetProcess);
    if (targetWindow is null)
    {
        return Failed(name, watch, $"target process with foreground-capable window was not found: {targetProcess}");
    }

    var xbuttonTrigger = KeyNameResolver.NormalizeMouseButton(options.ManualXButtonTrigger);
    if (xbuttonTrigger is not "mouse_x1" and not "mouse_x2")
    {
        return Failed(name, watch, $"manual xbutton trigger must be mouse_x1 or mouse_x2, but was: {xbuttonTrigger}");
    }

    static string XButtonPrompt(string trigger) =>
        string.Equals(trigger, "mouse_x1", StringComparison.OrdinalIgnoreCase)
            ? "press xbutton1 once"
            : "press xbutton2 once";

    static long XButtonDownCount(LiveHarnessWindow blocker, string trigger) =>
        string.Equals(trigger, "mouse_x1", StringComparison.OrdinalIgnoreCase)
            ? blocker.XButton1DownCount
            : blocker.XButton2DownCount;

    static long XButtonUpCount(LiveHarnessWindow blocker, string trigger) =>
        string.Equals(trigger, "mouse_x1", StringComparison.OrdinalIgnoreCase)
            ? blocker.XButton1UpCount
            : blocker.XButton2UpCount;

    var xbuttonPrompt = XButtonPrompt(xbuttonTrigger);
    var xbuttonTriggerKey = $"mouse:{xbuttonTrigger}";
    var runnerElevation = ProbeCurrentProcessElevation();
    var targetElevation = ProbeProcessElevation(targetWindow.Id, $"{targetWindow.ProcessName}.exe");
    var realTargetObserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var realTargetPending = new HashSet<string>(["xbutton_allowed"], StringComparer.OrdinalIgnoreCase);
    var realTargetFailed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var blockerObserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var blockerPending = new HashSet<string>(["xbutton_blocked_pass_through"], StringComparer.OrdinalIgnoreCase);
    var blockerFailed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var detailMetrics = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
    {
        ["manual_xbutton_confirmed"] = false
    };

    static string ScopeString(IEnumerable<string> orderedSteps, IReadOnlySet<string> activeSteps)
    {
        return string.Join(",", orderedSteps.Where(activeSteps.Contains));
    }

    static string StageMetricKey(string stage, string suffix) => $"stage_{stage}_{suffix}";

    void SetMetric(string key, object value)
    {
        detailMetrics[key] = value;
    }

    void MarkObserved(string step, bool realTarget)
    {
        if (realTarget)
        {
            realTargetPending.Remove(step);
            realTargetFailed.Remove(step);
            realTargetObserved.Add(step);
            return;
        }

        blockerPending.Remove(step);
        blockerFailed.Remove(step);
        blockerObserved.Add(step);
    }

    void MarkFailed(string step, bool realTarget)
    {
        if (realTarget)
        {
            realTargetObserved.Remove(step);
            realTargetPending.Remove(step);
            realTargetFailed.Add(step);
            return;
        }

        blockerObserved.Remove(step);
        blockerPending.Remove(step);
        blockerFailed.Add(step);
    }

    foreach (var stage in new[] { "xbutton_allowed", "xbutton_blocked" })
    {
        SetMetric(StageMetricKey(stage, "entered"), false);
        SetMetric(StageMetricKey(stage, "completed"), false);
        SetMetric(StageMetricKey(stage, "manual_required"), stage == "xbutton_allowed");
        SetMetric(StageMetricKey(stage, "manual_confirmed"), false);
        SetMetric(StageMetricKey(stage, "trigger_received_count"), 0);
        SetMetric(StageMetricKey(stage, "pipeline_enqueued_count"), 0);
        SetMetric(StageMetricKey(stage, "runtime_dispatched_count"), 0);
        SetMetric(StageMetricKey(stage, "runtime_foreground_allowed_count"), 0);
        SetMetric(StageMetricKey(stage, "runtime_ignored_count"), 0);
        SetMetric(StageMetricKey(stage, "runtime_ignored_reasons"), string.Empty);
        SetMetric(StageMetricKey(stage, "escape_down_count"), 0);
        SetMetric(StageMetricKey(stage, "escape_up_count"), 0);
        SetMetric(StageMetricKey(stage, "blocked_pass_through_down"), 0);
        SetMetric(StageMetricKey(stage, "blocked_pass_through_up"), 0);
        SetMetric(StageMetricKey(stage, "output_delta"), 0);
        SetMetric(StageMetricKey(stage, "failure_reason"), string.Empty);
    }

    Dictionary<string, object> CreateMetrics()
    {
        var metrics = new Dictionary<string, object>
        {
            ["target_process"] = targetProcess,
            ["target_process_id"] = targetWindow.Id,
            ["target_window_title"] = targetWindow.MainWindowTitle,
            ["validation_scope"] = "phase2b-xbutton-only",
            ["manual_xbutton_trigger"] = xbuttonTrigger,
            ["manual_xbutton_prompt"] = xbuttonPrompt,
            ["runner_elevation_known"] = runnerElevation.IsKnown,
            ["runner_elevated"] = runnerElevation.IsKnown && runnerElevation.IsElevated,
            ["runner_elevated_state"] = ElevationStateLabel(runnerElevation),
            ["target_elevation_known"] = targetElevation.IsKnown,
            ["target_elevated"] = targetElevation.IsKnown && targetElevation.IsElevated,
            ["target_elevated_state"] = ElevationStateLabel(targetElevation),
            ["runner_target_same_elevation"] = runnerElevation.IsKnown &&
                targetElevation.IsKnown &&
                runnerElevation.IsElevated == targetElevation.IsElevated,
            ["real_target_observed_scope"] = ScopeString(["xbutton_allowed"], realTargetObserved),
            ["real_target_pending_scope"] = ScopeString(["xbutton_allowed"], realTargetPending),
            ["real_target_failed_scope"] = ScopeString(["xbutton_allowed"], realTargetFailed),
            ["blocker_observed_scope"] = ScopeString(["xbutton_blocked_pass_through"], blockerObserved),
            ["blocker_pending_scope"] = ScopeString(["xbutton_blocked_pass_through"], blockerPending),
            ["blocker_failed_scope"] = ScopeString(["xbutton_blocked_pass_through"], blockerFailed)
        };

        foreach (var (key, value) in detailMetrics)
        {
            metrics[key] = value;
        }

        return metrics;
    }

    void RecordXButtonStage(
        string stage,
        CountingDiagnosticsSink diagnostics,
        CountingInputBackend runtimeInput,
        int escapeDownCount,
        int escapeUpCount,
        long blockedPassThroughDown,
        long blockedPassThroughUp,
        int outputDelta,
        bool completed,
        bool manualConfirmed,
        string failureReason)
    {
        var ignored = CollectRuntimeIgnored(diagnostics, xbuttonTriggerKey);
        SetMetric(StageMetricKey(stage, "entered"), true);
        SetMetric(StageMetricKey(stage, "completed"), completed);
        SetMetric(StageMetricKey(stage, "manual_confirmed"), manualConfirmed);
        SetMetric(StageMetricKey(stage, "trigger_received_count"), CountHookTriggerReceived(diagnostics, xbuttonTriggerKey));
        SetMetric(StageMetricKey(stage, "pipeline_enqueued_count"), CountPipelineQueued(diagnostics, xbuttonTriggerKey));
        SetMetric(StageMetricKey(stage, "runtime_dispatched_count"), CountRuntimeDispatched(diagnostics, xbuttonTriggerKey));
        SetMetric(StageMetricKey(stage, "runtime_foreground_allowed_count"), CountRuntimeForegroundAllowed(diagnostics));
        SetMetric(StageMetricKey(stage, "runtime_ignored_count"), ignored.Length);
        SetMetric(StageMetricKey(stage, "runtime_ignored_reasons"), JoinIgnoredReasons(ignored));
        SetMetric(StageMetricKey(stage, "escape_down_count"), escapeDownCount);
        SetMetric(StageMetricKey(stage, "escape_up_count"), escapeUpCount);
        SetMetric(StageMetricKey(stage, "blocked_pass_through_down"), blockedPassThroughDown);
        SetMetric(StageMetricKey(stage, "blocked_pass_through_up"), blockedPassThroughUp);
        SetMetric(StageMetricKey(stage, "output_delta"), outputDelta);
        SetMetric(StageMetricKey(stage, "failure_reason"), failureReason);
    }

    if (!runnerElevation.IsKnown || !runnerElevation.IsElevated)
    {
        return Failed(
            name,
            watch,
            $"{name} requires an elevated acceptance/headless/live runner. Current runner is not elevated, so this real-target validation is invalid.",
            CreateMetrics());
    }

    using var blocker = new LiveHarnessWindow("BA KeySmith Real Target XButton Blocker");
    RuntimeHost? host = null;
    try
    {
        blocker.Start(TimeSpan.FromSeconds(3));
        var runtimeInput = new CountingInputBackend(new WindowsInputBackend());
        var diagnostics = new CountingDiagnosticsSink();

        host = new RuntimeHost(
            LiveEscTapConfig(targetProcess, xbuttonTrigger),
            runtimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(diagnostics),
            diagnostics);

        await host.StartAsync(CancellationToken.None);
        targetWindow.Refresh();
        if (!await LiveWindowTools.FocusAsync(targetWindow.MainWindowHandle, TimeSpan.FromSeconds(5)) ||
            !LiveWindowTools.MoveCursorToCenter(targetWindow.MainWindowHandle))
        {
            return Failed(name, watch, $"failed to focus target window and move cursor: {targetWindow.ProcessName}", CreateMetrics());
        }

        var allowedGate = await host.CheckForegroundAsync(CancellationToken.None);
        if (!allowedGate.IsAllowed)
        {
            var gateMetrics = CreateMetrics();
            gateMetrics["foreground_process"] = allowedGate.ForegroundProcess ?? string.Empty;
            return Failed(
                name,
                watch,
                $"target foreground gate was not allowed: foreground={allowedGate.ForegroundProcess}",
                gateMetrics);
        }

        runtimeInput.Reset();
        diagnostics.Reset();
        Console.Error.WriteLine(
            $"MANUAL xbutton-allowed: Blue Archive focused. Physically {xbuttonPrompt} within {options.ManualTimeout.TotalSeconds:0}s. Expect mapped 'escape' effect.");
        if (!await WaitForInputCountAsync(runtimeInput, "key", "escape", 2, options.ManualTimeout))
        {
            RecordXButtonStage(
                "xbutton_allowed",
                diagnostics,
                runtimeInput,
                runtimeInput.Count("key", "escape", true),
                runtimeInput.Count("key", "escape", false),
                blockedPassThroughDown: 0,
                blockedPassThroughUp: 0,
                outputDelta: runtimeInput.Count("key", "escape"),
                completed: false,
                manualConfirmed: false,
                failureReason: "awaiting_escape_output");
            MarkFailed("xbutton_allowed", realTarget: true);
            return Failed(name, watch, "manual xbutton trigger did not produce escape output in target foreground", CreateMetrics());
        }

        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            RecordXButtonStage(
                "xbutton_allowed",
                diagnostics,
                runtimeInput,
                runtimeInput.Count("key", "escape", true),
                runtimeInput.Count("key", "escape", false),
                blockedPassThroughDown: 0,
                blockedPassThroughUp: 0,
                outputDelta: runtimeInput.Count("key", "escape"),
                completed: false,
                manualConfirmed: false,
                failureReason: "drain_timeout");
            MarkFailed("xbutton_allowed", realTarget: true);
            return Failed(name, watch, "queues did not drain after xbutton target trigger", CreateMetrics());
        }

        var xbuttonAllowedEscapeEvents = runtimeInput.Count("key", "escape");
        var manualXButtonConfirmed = ResolveManualConfirmation(
            options,
            $"Did Blue Archive react to '{xbuttonTrigger}' with the mapped 'escape' effect?");
        if (manualXButtonConfirmed is null)
        {
            RecordXButtonStage(
                "xbutton_allowed",
                diagnostics,
                runtimeInput,
                runtimeInput.Count("key", "escape", true),
                runtimeInput.Count("key", "escape", false),
                blockedPassThroughDown: 0,
                blockedPassThroughUp: 0,
                outputDelta: xbuttonAllowedEscapeEvents,
                completed: false,
                manualConfirmed: false,
                failureReason: "awaiting_manual_confirmation");
            MarkFailed("xbutton_allowed", realTarget: true);
            return Failed(name, watch, "bluearchive-manual-xbutton requires manual confirmation for the allowed xbutton observation.", CreateMetrics());
        }

        if (manualXButtonConfirmed is not true)
        {
            RecordXButtonStage(
                "xbutton_allowed",
                diagnostics,
                runtimeInput,
                runtimeInput.Count("key", "escape", true),
                runtimeInput.Count("key", "escape", false),
                blockedPassThroughDown: 0,
                blockedPassThroughUp: 0,
                outputDelta: xbuttonAllowedEscapeEvents,
                completed: false,
                manualConfirmed: false,
                failureReason: "manual_confirmation_negative");
            MarkFailed("xbutton_allowed", realTarget: true);
            return Failed(name, watch, "manual observation did not confirm xbutton trigger behavior in real target", CreateMetrics());
        }

        RecordXButtonStage(
            "xbutton_allowed",
            diagnostics,
            runtimeInput,
            runtimeInput.Count("key", "escape", true),
            runtimeInput.Count("key", "escape", false),
            blockedPassThroughDown: 0,
            blockedPassThroughUp: 0,
            outputDelta: xbuttonAllowedEscapeEvents,
            completed: true,
            manualConfirmed: true,
            failureReason: string.Empty);
        SetMetric("manual_xbutton_confirmed", true);
        SetMetric("xbutton_allowed_mapped_escape_events", xbuttonAllowedEscapeEvents);
        MarkObserved("xbutton_allowed", realTarget: true);

        if (!await blocker.FocusAsync(TimeSpan.FromSeconds(5)) ||
            !LiveWindowTools.MoveCursorToCenter(blocker.Handle))
        {
            return Failed(name, watch, "failed to focus blocker for foreground-blocked xbutton pass-through", CreateMetrics());
        }

        var blockedGate = await host.CheckForegroundAsync(CancellationToken.None);
        if (blockedGate.IsAllowed)
        {
            var blockedGateMetrics = CreateMetrics();
            blockedGateMetrics["blocked_foreground_process"] = blockedGate.ForegroundProcess ?? string.Empty;
            return Failed(name, watch, "blocker foreground was unexpectedly allowed for target process", blockedGateMetrics);
        }

        runtimeInput.Reset();
        diagnostics.Reset();
        blocker.Reset();
        Console.Error.WriteLine(
            $"MANUAL xbutton-blocked: blocker focused. Physically {xbuttonPrompt} within {options.ManualTimeout.TotalSeconds:0}s. Expect raw xbutton input to pass through to blocker and no mapped output.");
        if (!await WaitUntilAsync(
                () => XButtonDownCount(blocker, xbuttonTrigger) > 0 && XButtonUpCount(blocker, xbuttonTrigger) > 0,
                options.ManualTimeout,
                CancellationToken.None))
        {
            RecordXButtonStage(
                "xbutton_blocked",
                diagnostics,
                runtimeInput,
                runtimeInput.Count("key", "escape", true),
                runtimeInput.Count("key", "escape", false),
                blockedPassThroughDown: XButtonDownCount(blocker, xbuttonTrigger),
                blockedPassThroughUp: XButtonUpCount(blocker, xbuttonTrigger),
                outputDelta: runtimeInput.Count("key", "escape"),
                completed: false,
                manualConfirmed: false,
                failureReason: "awaiting_blocked_pass_through");
            MarkFailed("xbutton_blocked_pass_through", realTarget: false);
            return Failed(name, watch, "foreground-blocked xbutton trigger did not pass through to blocker", CreateMetrics());
        }

        await Task.Delay(150, CancellationToken.None);
        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            RecordXButtonStage(
                "xbutton_blocked",
                diagnostics,
                runtimeInput,
                runtimeInput.Count("key", "escape", true),
                runtimeInput.Count("key", "escape", false),
                blockedPassThroughDown: XButtonDownCount(blocker, xbuttonTrigger),
                blockedPassThroughUp: XButtonUpCount(blocker, xbuttonTrigger),
                outputDelta: runtimeInput.Count("key", "escape"),
                completed: false,
                manualConfirmed: false,
                failureReason: "drain_timeout");
            MarkFailed("xbutton_blocked_pass_through", realTarget: false);
            return Failed(name, watch, "queues did not drain after blocked xbutton trigger", CreateMetrics());
        }

        var blockedOutputDelta = runtimeInput.Count("key", "escape");
        var blockedXButtonPassThroughDown = XButtonDownCount(blocker, xbuttonTrigger);
        var blockedXButtonPassThroughUp = XButtonUpCount(blocker, xbuttonTrigger);
        if (blockedOutputDelta != 0)
        {
            RecordXButtonStage(
                "xbutton_blocked",
                diagnostics,
                runtimeInput,
                runtimeInput.Count("key", "escape", true),
                runtimeInput.Count("key", "escape", false),
                blockedPassThroughDown: blockedXButtonPassThroughDown,
                blockedPassThroughUp: blockedXButtonPassThroughUp,
                outputDelta: blockedOutputDelta,
                completed: false,
                manualConfirmed: false,
                failureReason: "unexpected_mapped_output");
            MarkFailed("xbutton_blocked_pass_through", realTarget: false);
            return Failed(name, watch, "foreground-blocked xbutton trigger produced mapped output", CreateMetrics());
        }

        RecordXButtonStage(
            "xbutton_blocked",
            diagnostics,
            runtimeInput,
            runtimeInput.Count("key", "escape", true),
            runtimeInput.Count("key", "escape", false),
            blockedPassThroughDown: blockedXButtonPassThroughDown,
            blockedPassThroughUp: blockedXButtonPassThroughUp,
            outputDelta: blockedOutputDelta,
            completed: true,
            manualConfirmed: false,
            failureReason: string.Empty);
        SetMetric("xbutton_blocked_pass_through_down", blockedXButtonPassThroughDown);
        SetMetric("xbutton_blocked_pass_through_up", blockedXButtonPassThroughUp);
        SetMetric("xbutton_blocked_output_delta", blockedOutputDelta);
        MarkObserved("xbutton_blocked_pass_through", realTarget: false);

        var stopWatch = Stopwatch.StartNew();
        await host.StopAsync(CancellationToken.None);
        stopWatch.Stop();
        var stopped = host.Snapshot();
        if (!IsCleanlyStopped(stopped))
        {
            return Failed(name, watch, "runtime did not stop cleanly after xbutton real target validation", CreateMetrics());
        }

        await host.DisposeAsync();
        host = null;

        var metrics = CreateMetrics();
        metrics["foreground_process"] = allowedGate.ForegroundProcess ?? string.Empty;
        metrics["blocked_foreground_process"] = blockedGate.ForegroundProcess ?? string.Empty;
        metrics["xbutton_allowed_mapped_escape_events"] = xbuttonAllowedEscapeEvents;
        metrics["xbutton_blocked_pass_through_down"] = blockedXButtonPassThroughDown;
        metrics["xbutton_blocked_pass_through_up"] = blockedXButtonPassThroughUp;
        metrics["xbutton_blocked_output_delta"] = blockedOutputDelta;
        metrics["stop_ms"] = stopWatch.Elapsed.TotalMilliseconds;
        metrics["diagnostic_events"] = diagnostics.Count;
        metrics["stopped_clean"] = IsCleanlyStopped(stopped);
        AddSnapshots(metrics, options, ("stopped", stopped));
        return Passed(name, watch, metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message, CreateMetrics());
    }
    finally
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}

static async Task<AcceptanceScenarioResult> RunBlueArchiveManualXButtonReloadAsync(AcceptanceOptions options)
{
    const string name = "bluearchive-manual-xbutton-reload";
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, $"{name} requires --allow-live-input");
    }

    var targetProcess = options.TargetProcess;
    var targetWindow = FindTargetProcessWithWindow(targetProcess);
    if (targetWindow is null)
    {
        return Failed(name, watch, $"target process with foreground-capable window was not found: {targetProcess}");
    }

    var oldTrigger = KeyNameResolver.NormalizeMouseButton(options.ManualXButtonTrigger);
    var reloadTrigger = KeyNameResolver.NormalizeMouseButton(options.ManualXButtonReloadTrigger);
    if (oldTrigger is not "mouse_x1" and not "mouse_x2")
    {
        return Failed(name, watch, $"manual xbutton trigger must be mouse_x1 or mouse_x2, but was: {oldTrigger}");
    }

    if (reloadTrigger is not "mouse_x1" and not "mouse_x2")
    {
        return Failed(name, watch, $"manual xbutton reload trigger must be mouse_x1 or mouse_x2, but was: {reloadTrigger}");
    }

    if (string.Equals(oldTrigger, reloadTrigger, StringComparison.OrdinalIgnoreCase))
    {
        return Failed(name, watch, "manual xbutton trigger and reload trigger must be different for xbutton reload validation");
    }

    static string XButtonPrompt(string trigger) =>
        string.Equals(trigger, "mouse_x1", StringComparison.OrdinalIgnoreCase)
            ? "press xbutton1 once"
            : "press xbutton2 once";

    static long XButtonDownCount(LiveHarnessWindow blocker, string trigger) =>
        string.Equals(trigger, "mouse_x1", StringComparison.OrdinalIgnoreCase)
            ? blocker.XButton1DownCount
            : blocker.XButton2DownCount;

    static long XButtonUpCount(LiveHarnessWindow blocker, string trigger) =>
        string.Equals(trigger, "mouse_x1", StringComparison.OrdinalIgnoreCase)
            ? blocker.XButton1UpCount
            : blocker.XButton2UpCount;

    var oldPrompt = XButtonPrompt(oldTrigger);
    var reloadPrompt = XButtonPrompt(reloadTrigger);
    var oldTriggerKey = $"mouse:{oldTrigger}";
    var reloadTriggerKey = $"mouse:{reloadTrigger}";
    var runnerElevation = ProbeCurrentProcessElevation();
    var targetElevation = ProbeProcessElevation(targetWindow.Id, $"{targetWindow.ProcessName}.exe");
    var realTargetObserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var realTargetPending = new HashSet<string>(["reload_new_trigger_allowed"], StringComparer.OrdinalIgnoreCase);
    var realTargetFailed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var blockerObserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var blockerPending = new HashSet<string>(["reload_old_trigger_pass_through"], StringComparer.OrdinalIgnoreCase);
    var blockerFailed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var detailMetrics = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
    {
        ["manual_reload_confirmed"] = false
    };

    static string ScopeString(IEnumerable<string> orderedSteps, IReadOnlySet<string> activeSteps)
    {
        return string.Join(",", orderedSteps.Where(activeSteps.Contains));
    }

    static string StageMetricKey(string stage, string suffix) => $"stage_{stage}_{suffix}";

    void SetMetric(string key, object value)
    {
        detailMetrics[key] = value;
    }

    void MarkObserved(string step, bool realTarget)
    {
        if (realTarget)
        {
            realTargetPending.Remove(step);
            realTargetFailed.Remove(step);
            realTargetObserved.Add(step);
            return;
        }

        blockerPending.Remove(step);
        blockerFailed.Remove(step);
        blockerObserved.Add(step);
    }

    void MarkFailed(string step, bool realTarget)
    {
        if (realTarget)
        {
            realTargetObserved.Remove(step);
            realTargetPending.Remove(step);
            realTargetFailed.Add(step);
            return;
        }

        blockerObserved.Remove(step);
        blockerPending.Remove(step);
        blockerFailed.Add(step);
    }

    foreach (var stage in new[] { "reload_old_trigger_blocked", "reload_new_trigger_allowed" })
    {
        SetMetric(StageMetricKey(stage, "entered"), false);
        SetMetric(StageMetricKey(stage, "completed"), false);
        SetMetric(StageMetricKey(stage, "manual_required"), stage == "reload_new_trigger_allowed");
        SetMetric(StageMetricKey(stage, "manual_confirmed"), false);
        SetMetric(StageMetricKey(stage, "trigger_received_count"), 0);
        SetMetric(StageMetricKey(stage, "pipeline_enqueued_count"), 0);
        SetMetric(StageMetricKey(stage, "runtime_dispatched_count"), 0);
        SetMetric(StageMetricKey(stage, "runtime_foreground_allowed_count"), 0);
        SetMetric(StageMetricKey(stage, "runtime_ignored_count"), 0);
        SetMetric(StageMetricKey(stage, "runtime_ignored_reasons"), string.Empty);
        SetMetric(StageMetricKey(stage, "escape_down_count"), 0);
        SetMetric(StageMetricKey(stage, "escape_up_count"), 0);
        SetMetric(StageMetricKey(stage, "blocked_pass_through_down"), 0);
        SetMetric(StageMetricKey(stage, "blocked_pass_through_up"), 0);
        SetMetric(StageMetricKey(stage, "output_delta"), 0);
        SetMetric(StageMetricKey(stage, "failure_reason"), string.Empty);
    }

    Dictionary<string, object> CreateMetrics()
    {
        var metrics = new Dictionary<string, object>
        {
            ["target_process"] = targetProcess,
            ["target_process_id"] = targetWindow.Id,
            ["target_window_title"] = targetWindow.MainWindowTitle,
            ["validation_scope"] = "phase2b-xbutton-reload-only",
            ["manual_xbutton_trigger"] = oldTrigger,
            ["manual_xbutton_reload_trigger"] = reloadTrigger,
            ["manual_xbutton_trigger_prompt"] = oldPrompt,
            ["manual_xbutton_reload_prompt"] = reloadPrompt,
            ["runner_elevation_known"] = runnerElevation.IsKnown,
            ["runner_elevated"] = runnerElevation.IsKnown && runnerElevation.IsElevated,
            ["runner_elevated_state"] = ElevationStateLabel(runnerElevation),
            ["target_elevation_known"] = targetElevation.IsKnown,
            ["target_elevated"] = targetElevation.IsKnown && targetElevation.IsElevated,
            ["target_elevated_state"] = ElevationStateLabel(targetElevation),
            ["runner_target_same_elevation"] = runnerElevation.IsKnown &&
                targetElevation.IsKnown &&
                runnerElevation.IsElevated == targetElevation.IsElevated,
            ["real_target_observed_scope"] = ScopeString(["reload_new_trigger_allowed"], realTargetObserved),
            ["real_target_pending_scope"] = ScopeString(["reload_new_trigger_allowed"], realTargetPending),
            ["real_target_failed_scope"] = ScopeString(["reload_new_trigger_allowed"], realTargetFailed),
            ["blocker_observed_scope"] = ScopeString(["reload_old_trigger_pass_through"], blockerObserved),
            ["blocker_pending_scope"] = ScopeString(["reload_old_trigger_pass_through"], blockerPending),
            ["blocker_failed_scope"] = ScopeString(["reload_old_trigger_pass_through"], blockerFailed)
        };

        foreach (var (key, value) in detailMetrics)
        {
            metrics[key] = value;
        }

        return metrics;
    }

    void RecordReloadStage(
        string stage,
        string triggerKey,
        CountingDiagnosticsSink diagnostics,
        CountingInputBackend runtimeInput,
        int escapeDownCount,
        int escapeUpCount,
        long blockedPassThroughDown,
        long blockedPassThroughUp,
        int outputDelta,
        bool completed,
        bool manualConfirmed,
        string failureReason)
    {
        var ignored = CollectRuntimeIgnored(diagnostics, triggerKey);
        SetMetric(StageMetricKey(stage, "entered"), true);
        SetMetric(StageMetricKey(stage, "completed"), completed);
        SetMetric(StageMetricKey(stage, "manual_confirmed"), manualConfirmed);
        SetMetric(StageMetricKey(stage, "trigger_received_count"), CountHookTriggerReceived(diagnostics, triggerKey));
        SetMetric(StageMetricKey(stage, "pipeline_enqueued_count"), CountPipelineQueued(diagnostics, triggerKey));
        SetMetric(StageMetricKey(stage, "runtime_dispatched_count"), CountRuntimeDispatched(diagnostics, triggerKey));
        SetMetric(StageMetricKey(stage, "runtime_foreground_allowed_count"), CountRuntimeForegroundAllowed(diagnostics));
        SetMetric(StageMetricKey(stage, "runtime_ignored_count"), ignored.Length);
        SetMetric(StageMetricKey(stage, "runtime_ignored_reasons"), JoinIgnoredReasons(ignored));
        SetMetric(StageMetricKey(stage, "escape_down_count"), escapeDownCount);
        SetMetric(StageMetricKey(stage, "escape_up_count"), escapeUpCount);
        SetMetric(StageMetricKey(stage, "blocked_pass_through_down"), blockedPassThroughDown);
        SetMetric(StageMetricKey(stage, "blocked_pass_through_up"), blockedPassThroughUp);
        SetMetric(StageMetricKey(stage, "output_delta"), outputDelta);
        SetMetric(StageMetricKey(stage, "failure_reason"), failureReason);
    }

    if (!runnerElevation.IsKnown || !runnerElevation.IsElevated)
    {
        return Failed(
            name,
            watch,
            $"{name} requires an elevated acceptance/headless/live runner. Current runner is not elevated, so this real-target validation is invalid.",
            CreateMetrics());
    }

    using var blocker = new LiveHarnessWindow("BA KeySmith Real Target XButton Reload Blocker");
    RuntimeHost? host = null;
    try
    {
        blocker.Start(TimeSpan.FromSeconds(3));
        var runtimeInput = new CountingInputBackend(new WindowsInputBackend());
        var diagnostics = new CountingDiagnosticsSink();

        host = new RuntimeHost(
            LiveEscTapConfig(targetProcess, oldTrigger),
            runtimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(diagnostics),
            diagnostics);

        await host.StartAsync(CancellationToken.None);
        await host.ReloadAsync(LiveEscTapConfig(targetProcess, reloadTrigger), CancellationToken.None);
        var afterReload = host.Snapshot();

        if (!await blocker.FocusAsync(TimeSpan.FromSeconds(5)) ||
            !LiveWindowTools.MoveCursorToCenter(blocker.Handle))
        {
            return Failed(name, watch, "failed to focus blocker for old xbutton reload probe", CreateMetrics());
        }

        var blockedGate = await host.CheckForegroundAsync(CancellationToken.None);
        if (blockedGate.IsAllowed)
        {
            var blockedGateMetrics = CreateMetrics();
            blockedGateMetrics["blocked_foreground_process"] = blockedGate.ForegroundProcess ?? string.Empty;
            return Failed(name, watch, "blocker foreground was unexpectedly allowed for target process", blockedGateMetrics);
        }

        runtimeInput.Reset();
        diagnostics.Reset();
        blocker.Reset();
        Console.Error.WriteLine(
            $"MANUAL xbutton-reload-old: blocker focused. Physically {oldPrompt} within {options.ManualTimeout.TotalSeconds:0}s. Expect raw xbutton input to pass through and no mapped output.");
        if (!await WaitUntilAsync(
                () => XButtonDownCount(blocker, oldTrigger) > 0 && XButtonUpCount(blocker, oldTrigger) > 0,
                options.ManualTimeout,
                CancellationToken.None))
        {
            RecordReloadStage(
                "reload_old_trigger_blocked",
                oldTriggerKey,
                diagnostics,
                runtimeInput,
                runtimeInput.Count("key", "escape", true),
                runtimeInput.Count("key", "escape", false),
                blockedPassThroughDown: XButtonDownCount(blocker, oldTrigger),
                blockedPassThroughUp: XButtonUpCount(blocker, oldTrigger),
                outputDelta: runtimeInput.Count("key", "escape"),
                completed: false,
                manualConfirmed: false,
                failureReason: "awaiting_blocked_pass_through");
            MarkFailed("reload_old_trigger_pass_through", realTarget: false);
            return Failed(name, watch, "reloaded old xbutton trigger did not pass through to blocker", CreateMetrics());
        }

        await Task.Delay(150, CancellationToken.None);
        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            RecordReloadStage(
                "reload_old_trigger_blocked",
                oldTriggerKey,
                diagnostics,
                runtimeInput,
                runtimeInput.Count("key", "escape", true),
                runtimeInput.Count("key", "escape", false),
                blockedPassThroughDown: XButtonDownCount(blocker, oldTrigger),
                blockedPassThroughUp: XButtonUpCount(blocker, oldTrigger),
                outputDelta: runtimeInput.Count("key", "escape"),
                completed: false,
                manualConfirmed: false,
                failureReason: "drain_timeout");
            MarkFailed("reload_old_trigger_pass_through", realTarget: false);
            return Failed(name, watch, "queues did not drain after old xbutton reload probe", CreateMetrics());
        }

        var oldPassThroughDown = XButtonDownCount(blocker, oldTrigger);
        var oldPassThroughUp = XButtonUpCount(blocker, oldTrigger);
        var oldOutputDelta = runtimeInput.Count("key", "escape");
        if (oldOutputDelta != 0)
        {
            RecordReloadStage(
                "reload_old_trigger_blocked",
                oldTriggerKey,
                diagnostics,
                runtimeInput,
                runtimeInput.Count("key", "escape", true),
                runtimeInput.Count("key", "escape", false),
                blockedPassThroughDown: oldPassThroughDown,
                blockedPassThroughUp: oldPassThroughUp,
                outputDelta: oldOutputDelta,
                completed: false,
                manualConfirmed: false,
                failureReason: "unexpected_mapped_output");
            MarkFailed("reload_old_trigger_pass_through", realTarget: false);
            return Failed(name, watch, "old xbutton trigger still produced mapped output after reload", CreateMetrics());
        }

        RecordReloadStage(
            "reload_old_trigger_blocked",
            oldTriggerKey,
            diagnostics,
            runtimeInput,
            runtimeInput.Count("key", "escape", true),
            runtimeInput.Count("key", "escape", false),
            blockedPassThroughDown: oldPassThroughDown,
            blockedPassThroughUp: oldPassThroughUp,
            outputDelta: oldOutputDelta,
            completed: true,
            manualConfirmed: false,
            failureReason: string.Empty);
        SetMetric("reload_old_trigger_pass_through_down", oldPassThroughDown);
        SetMetric("reload_old_trigger_pass_through_up", oldPassThroughUp);
        SetMetric("reload_old_trigger_output_delta", oldOutputDelta);
        MarkObserved("reload_old_trigger_pass_through", realTarget: false);

        targetWindow.Refresh();
        if (!await LiveWindowTools.FocusAsync(targetWindow.MainWindowHandle, TimeSpan.FromSeconds(5)) ||
            !LiveWindowTools.MoveCursorToCenter(targetWindow.MainWindowHandle))
        {
            return Failed(name, watch, $"failed to refocus target window and move cursor: {targetWindow.ProcessName}", CreateMetrics());
        }

        var allowedGate = await host.CheckForegroundAsync(CancellationToken.None);
        if (!allowedGate.IsAllowed)
        {
            var gateMetrics = CreateMetrics();
            gateMetrics["foreground_process"] = allowedGate.ForegroundProcess ?? string.Empty;
            return Failed(
                name,
                watch,
                $"target foreground gate was not allowed after reload: foreground={allowedGate.ForegroundProcess}",
                gateMetrics);
        }

        runtimeInput.Reset();
        diagnostics.Reset();
        Console.Error.WriteLine(
            $"MANUAL xbutton-reload-new: Blue Archive focused. Physically {reloadPrompt} within {options.ManualTimeout.TotalSeconds:0}s. Expect mapped 'escape' effect from the reloaded trigger.");
        if (!await WaitForInputCountAsync(runtimeInput, "key", "escape", 2, options.ManualTimeout))
        {
            RecordReloadStage(
                "reload_new_trigger_allowed",
                reloadTriggerKey,
                diagnostics,
                runtimeInput,
                runtimeInput.Count("key", "escape", true),
                runtimeInput.Count("key", "escape", false),
                blockedPassThroughDown: 0,
                blockedPassThroughUp: 0,
                outputDelta: runtimeInput.Count("key", "escape"),
                completed: false,
                manualConfirmed: false,
                failureReason: "awaiting_escape_output");
            MarkFailed("reload_new_trigger_allowed", realTarget: true);
            return Failed(name, watch, "reloaded new xbutton trigger did not produce escape output in target foreground", CreateMetrics());
        }

        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            RecordReloadStage(
                "reload_new_trigger_allowed",
                reloadTriggerKey,
                diagnostics,
                runtimeInput,
                runtimeInput.Count("key", "escape", true),
                runtimeInput.Count("key", "escape", false),
                blockedPassThroughDown: 0,
                blockedPassThroughUp: 0,
                outputDelta: runtimeInput.Count("key", "escape"),
                completed: false,
                manualConfirmed: false,
                failureReason: "drain_timeout");
            MarkFailed("reload_new_trigger_allowed", realTarget: true);
            return Failed(name, watch, "queues did not drain after new xbutton reload trigger", CreateMetrics());
        }

        var newEscapeEvents = runtimeInput.Count("key", "escape");
        var manualReloadConfirmed = ResolveManualConfirmation(
            options,
            $"Did Blue Archive react to reloaded '{reloadTrigger}' with the mapped 'escape' effect?");
        if (manualReloadConfirmed is null)
        {
            RecordReloadStage(
                "reload_new_trigger_allowed",
                reloadTriggerKey,
                diagnostics,
                runtimeInput,
                runtimeInput.Count("key", "escape", true),
                runtimeInput.Count("key", "escape", false),
                blockedPassThroughDown: 0,
                blockedPassThroughUp: 0,
                outputDelta: newEscapeEvents,
                completed: false,
                manualConfirmed: false,
                failureReason: "awaiting_manual_confirmation");
            MarkFailed("reload_new_trigger_allowed", realTarget: true);
            return Failed(name, watch, "bluearchive-manual-xbutton-reload requires manual confirmation for the reloaded xbutton observation.", CreateMetrics());
        }

        if (manualReloadConfirmed is not true)
        {
            RecordReloadStage(
                "reload_new_trigger_allowed",
                reloadTriggerKey,
                diagnostics,
                runtimeInput,
                runtimeInput.Count("key", "escape", true),
                runtimeInput.Count("key", "escape", false),
                blockedPassThroughDown: 0,
                blockedPassThroughUp: 0,
                outputDelta: newEscapeEvents,
                completed: false,
                manualConfirmed: false,
                failureReason: "manual_confirmation_negative");
            MarkFailed("reload_new_trigger_allowed", realTarget: true);
            return Failed(name, watch, "manual observation did not confirm reloaded xbutton trigger behavior in real target", CreateMetrics());
        }

        RecordReloadStage(
            "reload_new_trigger_allowed",
            reloadTriggerKey,
            diagnostics,
            runtimeInput,
            runtimeInput.Count("key", "escape", true),
            runtimeInput.Count("key", "escape", false),
            blockedPassThroughDown: 0,
            blockedPassThroughUp: 0,
            outputDelta: newEscapeEvents,
            completed: true,
            manualConfirmed: true,
            failureReason: string.Empty);
        SetMetric("manual_reload_confirmed", true);
        SetMetric("reload_new_trigger_escape_events", newEscapeEvents);
        SetMetric("reload_generation", afterReload.Runtime.Generation);
        MarkObserved("reload_new_trigger_allowed", realTarget: true);

        var stopWatch = Stopwatch.StartNew();
        await host.StopAsync(CancellationToken.None);
        stopWatch.Stop();
        var stopped = host.Snapshot();
        if (!IsCleanlyStopped(stopped))
        {
            return Failed(name, watch, "runtime did not stop cleanly after xbutton reload real target validation", CreateMetrics());
        }

        await host.DisposeAsync();
        host = null;

        var metrics = CreateMetrics();
        metrics["foreground_process"] = allowedGate.ForegroundProcess ?? string.Empty;
        metrics["blocked_foreground_process"] = blockedGate.ForegroundProcess ?? string.Empty;
        metrics["reload_generation"] = afterReload.Runtime.Generation;
        metrics["reload_old_trigger_pass_through_down"] = oldPassThroughDown;
        metrics["reload_old_trigger_pass_through_up"] = oldPassThroughUp;
        metrics["reload_old_trigger_output_delta"] = oldOutputDelta;
        metrics["reload_new_trigger_escape_events"] = newEscapeEvents;
        metrics["stop_ms"] = stopWatch.Elapsed.TotalMilliseconds;
        metrics["diagnostic_events"] = diagnostics.Count;
        metrics["stopped_clean"] = IsCleanlyStopped(stopped);
        AddSnapshots(metrics, options, ("after_reload", afterReload), ("stopped", stopped));
        return Passed(name, watch, metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message, CreateMetrics());
    }
    finally
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}

static async Task<AcceptanceScenarioResult> RunBlueArchiveManualXButtonDisableAsync(AcceptanceOptions options)
{
    const string name = "bluearchive-manual-xbutton-disable";
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, $"{name} requires --allow-live-input");
    }

    var targetProcess = options.TargetProcess;
    var targetWindow = FindTargetProcessWithWindow(targetProcess);
    if (targetWindow is null)
    {
        return Failed(name, watch, $"target process with foreground-capable window was not found: {targetProcess}");
    }

    var xbuttonTrigger = KeyNameResolver.NormalizeMouseButton(options.ManualXButtonTrigger);
    if (xbuttonTrigger is not "mouse_x1" and not "mouse_x2")
    {
        return Failed(name, watch, $"manual xbutton trigger must be mouse_x1 or mouse_x2, but was: {xbuttonTrigger}");
    }

    static string XButtonPrompt(string trigger) =>
        string.Equals(trigger, "mouse_x1", StringComparison.OrdinalIgnoreCase)
            ? "press xbutton1 once"
            : "press xbutton2 once";

    var xbuttonPrompt = XButtonPrompt(xbuttonTrigger);
    var xbuttonTriggerKey = $"mouse:{xbuttonTrigger}";
    var runnerElevation = ProbeCurrentProcessElevation();
    var targetElevation = ProbeProcessElevation(targetWindow.Id, $"{targetWindow.ProcessName}.exe");
    var realTargetObserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var realTargetPending = new HashSet<string>(["disable_baseline_allowed", "disable_no_output"], StringComparer.OrdinalIgnoreCase);
    var realTargetFailed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var detailMetrics = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
    {
        ["manual_disable_confirmed"] = false
    };

    static string ScopeString(IEnumerable<string> orderedSteps, IReadOnlySet<string> activeSteps)
    {
        return string.Join(",", orderedSteps.Where(activeSteps.Contains));
    }

    static string StageMetricKey(string stage, string suffix) => $"stage_{stage}_{suffix}";

    void SetMetric(string key, object value)
    {
        detailMetrics[key] = value;
    }

    void MarkObserved(string step)
    {
        realTargetPending.Remove(step);
        realTargetFailed.Remove(step);
        realTargetObserved.Add(step);
    }

    void MarkFailed(string step)
    {
        realTargetObserved.Remove(step);
        realTargetPending.Remove(step);
        realTargetFailed.Add(step);
    }

    foreach (var stage in new[] { "disable_baseline_allowed", "disable_no_output" })
    {
        SetMetric(StageMetricKey(stage, "entered"), false);
        SetMetric(StageMetricKey(stage, "completed"), false);
        SetMetric(StageMetricKey(stage, "manual_required"), stage == "disable_no_output");
        SetMetric(StageMetricKey(stage, "manual_confirmed"), false);
        SetMetric(StageMetricKey(stage, "trigger_received_count"), 0);
        SetMetric(StageMetricKey(stage, "pipeline_enqueued_count"), 0);
        SetMetric(StageMetricKey(stage, "runtime_dispatched_count"), 0);
        SetMetric(StageMetricKey(stage, "runtime_foreground_allowed_count"), 0);
        SetMetric(StageMetricKey(stage, "runtime_ignored_count"), 0);
        SetMetric(StageMetricKey(stage, "runtime_ignored_reasons"), string.Empty);
        SetMetric(StageMetricKey(stage, "escape_down_count"), 0);
        SetMetric(StageMetricKey(stage, "escape_up_count"), 0);
        SetMetric(StageMetricKey(stage, "output_delta"), 0);
        SetMetric(StageMetricKey(stage, "failure_reason"), string.Empty);
    }

    Dictionary<string, object> CreateMetrics()
    {
        var metrics = new Dictionary<string, object>
        {
            ["target_process"] = targetProcess,
            ["target_process_id"] = targetWindow.Id,
            ["target_window_title"] = targetWindow.MainWindowTitle,
            ["validation_scope"] = "phase2b-xbutton-disable-only",
            ["manual_xbutton_trigger"] = xbuttonTrigger,
            ["manual_xbutton_prompt"] = xbuttonPrompt,
            ["runner_elevation_known"] = runnerElevation.IsKnown,
            ["runner_elevated"] = runnerElevation.IsKnown && runnerElevation.IsElevated,
            ["runner_elevated_state"] = ElevationStateLabel(runnerElevation),
            ["target_elevation_known"] = targetElevation.IsKnown,
            ["target_elevated"] = targetElevation.IsKnown && targetElevation.IsElevated,
            ["target_elevated_state"] = ElevationStateLabel(targetElevation),
            ["runner_target_same_elevation"] = runnerElevation.IsKnown &&
                targetElevation.IsKnown &&
                runnerElevation.IsElevated == targetElevation.IsElevated,
            ["real_target_observed_scope"] = ScopeString(["disable_baseline_allowed", "disable_no_output"], realTargetObserved),
            ["real_target_pending_scope"] = ScopeString(["disable_baseline_allowed", "disable_no_output"], realTargetPending),
            ["real_target_failed_scope"] = ScopeString(["disable_baseline_allowed", "disable_no_output"], realTargetFailed)
        };

        foreach (var (key, value) in detailMetrics)
        {
            metrics[key] = value;
        }

        return metrics;
    }

    void RecordDisableStage(
        string stage,
        CountingDiagnosticsSink diagnostics,
        CountingInputBackend runtimeInput,
        int escapeDownCount,
        int escapeUpCount,
        int outputDelta,
        bool completed,
        bool manualConfirmed,
        string failureReason)
    {
        var ignored = CollectRuntimeIgnored(diagnostics, xbuttonTriggerKey);
        SetMetric(StageMetricKey(stage, "entered"), true);
        SetMetric(StageMetricKey(stage, "completed"), completed);
        SetMetric(StageMetricKey(stage, "manual_confirmed"), manualConfirmed);
        SetMetric(StageMetricKey(stage, "trigger_received_count"), CountHookTriggerReceived(diagnostics, xbuttonTriggerKey));
        SetMetric(StageMetricKey(stage, "pipeline_enqueued_count"), CountPipelineQueued(diagnostics, xbuttonTriggerKey));
        SetMetric(StageMetricKey(stage, "runtime_dispatched_count"), CountRuntimeDispatched(diagnostics, xbuttonTriggerKey));
        SetMetric(StageMetricKey(stage, "runtime_foreground_allowed_count"), CountRuntimeForegroundAllowed(diagnostics));
        SetMetric(StageMetricKey(stage, "runtime_ignored_count"), ignored.Length);
        SetMetric(StageMetricKey(stage, "runtime_ignored_reasons"), JoinIgnoredReasons(ignored));
        SetMetric(StageMetricKey(stage, "escape_down_count"), escapeDownCount);
        SetMetric(StageMetricKey(stage, "escape_up_count"), escapeUpCount);
        SetMetric(StageMetricKey(stage, "output_delta"), outputDelta);
        SetMetric(StageMetricKey(stage, "failure_reason"), failureReason);
    }

    if (!runnerElevation.IsKnown || !runnerElevation.IsElevated)
    {
        return Failed(
            name,
            watch,
            $"{name} requires an elevated acceptance/headless/live runner. Current runner is not elevated, so this real-target validation is invalid.",
            CreateMetrics());
    }

    RuntimeHost? host = null;
    try
    {
        var runtimeInput = new CountingInputBackend(new WindowsInputBackend());
        var diagnostics = new CountingDiagnosticsSink();

        host = new RuntimeHost(
            LiveEscTapConfig(targetProcess, xbuttonTrigger),
            runtimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(diagnostics),
            diagnostics);

        await host.StartAsync(CancellationToken.None);
        targetWindow.Refresh();
        if (!await LiveWindowTools.FocusAsync(targetWindow.MainWindowHandle, TimeSpan.FromSeconds(5)) ||
            !LiveWindowTools.MoveCursorToCenter(targetWindow.MainWindowHandle))
        {
            return Failed(name, watch, $"failed to focus target window and move cursor: {targetWindow.ProcessName}", CreateMetrics());
        }

        var allowedGate = await host.CheckForegroundAsync(CancellationToken.None);
        if (!allowedGate.IsAllowed)
        {
            var gateMetrics = CreateMetrics();
            gateMetrics["foreground_process"] = allowedGate.ForegroundProcess ?? string.Empty;
            return Failed(
                name,
                watch,
                $"target foreground gate was not allowed: foreground={allowedGate.ForegroundProcess}",
                gateMetrics);
        }

        runtimeInput.Reset();
        diagnostics.Reset();
        Console.Error.WriteLine(
            $"MANUAL xbutton-disable-baseline: Blue Archive focused. Physically {xbuttonPrompt} within {options.ManualTimeout.TotalSeconds:0}s. Expect mapped 'escape' effect before disable.");
        if (!await WaitForInputCountAsync(runtimeInput, "key", "escape", 2, options.ManualTimeout))
        {
            RecordDisableStage(
                "disable_baseline_allowed",
                diagnostics,
                runtimeInput,
                runtimeInput.Count("key", "escape", true),
                runtimeInput.Count("key", "escape", false),
                outputDelta: runtimeInput.Count("key", "escape"),
                completed: false,
                manualConfirmed: false,
                failureReason: "awaiting_escape_output");
            MarkFailed("disable_baseline_allowed");
            return Failed(name, watch, "baseline xbutton trigger did not produce escape output before disable", CreateMetrics());
        }

        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            RecordDisableStage(
                "disable_baseline_allowed",
                diagnostics,
                runtimeInput,
                runtimeInput.Count("key", "escape", true),
                runtimeInput.Count("key", "escape", false),
                outputDelta: runtimeInput.Count("key", "escape"),
                completed: false,
                manualConfirmed: false,
                failureReason: "drain_timeout");
            MarkFailed("disable_baseline_allowed");
            return Failed(name, watch, "queues did not drain after xbutton disable baseline trigger", CreateMetrics());
        }

        var baselineEscapeEvents = runtimeInput.Count("key", "escape");
        RecordDisableStage(
            "disable_baseline_allowed",
            diagnostics,
            runtimeInput,
            runtimeInput.Count("key", "escape", true),
            runtimeInput.Count("key", "escape", false),
            outputDelta: baselineEscapeEvents,
            completed: true,
            manualConfirmed: false,
            failureReason: string.Empty);
        SetMetric("disable_baseline_escape_events", baselineEscapeEvents);
        MarkObserved("disable_baseline_allowed");

        await host.DisableAsync(CancellationToken.None);
        var disabled = host.Snapshot();

        runtimeInput.Reset();
        diagnostics.Reset();
        Console.Error.WriteLine(
            $"MANUAL xbutton-disable: Blue Archive focused. Physically {xbuttonPrompt} within {options.ManualTimeout.TotalSeconds:0}s. Expect no mapped 'escape' effect while runtime is disabled.");
        if (!await WaitUntilAsync(
                () => CountHookTriggerReceived(diagnostics, xbuttonTriggerKey) > 0,
                options.ManualTimeout,
                CancellationToken.None))
        {
            RecordDisableStage(
                "disable_no_output",
                diagnostics,
                runtimeInput,
                runtimeInput.Count("key", "escape", true),
                runtimeInput.Count("key", "escape", false),
                outputDelta: runtimeInput.Count("key", "escape"),
                completed: false,
                manualConfirmed: false,
                failureReason: "awaiting_trigger_reception");
            MarkFailed("disable_no_output");
            return Failed(name, watch, "disabled xbutton trigger was not observed by hook in target foreground", CreateMetrics());
        }

        await Task.Delay(150, CancellationToken.None);
        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            RecordDisableStage(
                "disable_no_output",
                diagnostics,
                runtimeInput,
                runtimeInput.Count("key", "escape", true),
                runtimeInput.Count("key", "escape", false),
                outputDelta: runtimeInput.Count("key", "escape"),
                completed: false,
                manualConfirmed: false,
                failureReason: "drain_timeout");
            MarkFailed("disable_no_output");
            return Failed(name, watch, "queues did not drain after disabled xbutton trigger", CreateMetrics());
        }

        var disabledOutputDelta = runtimeInput.Count("key", "escape");
        if (disabledOutputDelta != 0 ||
            CountPipelineQueued(diagnostics, xbuttonTriggerKey) != 0 ||
            CountRuntimeDispatched(diagnostics, xbuttonTriggerKey) != 0)
        {
            RecordDisableStage(
                "disable_no_output",
                diagnostics,
                runtimeInput,
                runtimeInput.Count("key", "escape", true),
                runtimeInput.Count("key", "escape", false),
                outputDelta: disabledOutputDelta,
                completed: false,
                manualConfirmed: false,
                failureReason: "unexpected_output_or_dispatch");
            MarkFailed("disable_no_output");
            return Failed(name, watch, "disabled xbutton trigger produced mapped output or entered pipeline/runtime", CreateMetrics());
        }

        var manualDisableConfirmed = ResolveManualConfirmation(
            options,
            $"Did Blue Archive remain unchanged with no mapped 'escape' effect after disabled '{xbuttonTrigger}'?");
        if (manualDisableConfirmed is null)
        {
            RecordDisableStage(
                "disable_no_output",
                diagnostics,
                runtimeInput,
                runtimeInput.Count("key", "escape", true),
                runtimeInput.Count("key", "escape", false),
                outputDelta: disabledOutputDelta,
                completed: false,
                manualConfirmed: false,
                failureReason: "awaiting_manual_confirmation");
            MarkFailed("disable_no_output");
            return Failed(name, watch, "bluearchive-manual-xbutton-disable requires manual confirmation for disabled no-output observation.", CreateMetrics());
        }

        if (manualDisableConfirmed is not true)
        {
            RecordDisableStage(
                "disable_no_output",
                diagnostics,
                runtimeInput,
                runtimeInput.Count("key", "escape", true),
                runtimeInput.Count("key", "escape", false),
                outputDelta: disabledOutputDelta,
                completed: false,
                manualConfirmed: false,
                failureReason: "manual_confirmation_negative");
            MarkFailed("disable_no_output");
            return Failed(name, watch, "manual observation did not confirm disabled xbutton no-output behavior in real target", CreateMetrics());
        }

        RecordDisableStage(
            "disable_no_output",
            diagnostics,
            runtimeInput,
            runtimeInput.Count("key", "escape", true),
            runtimeInput.Count("key", "escape", false),
            outputDelta: disabledOutputDelta,
            completed: true,
            manualConfirmed: true,
            failureReason: string.Empty);
        SetMetric("manual_disable_confirmed", true);
        SetMetric("disable_output_delta", disabledOutputDelta);
        MarkObserved("disable_no_output");

        var stopWatch = Stopwatch.StartNew();
        await host.StopAsync(CancellationToken.None);
        stopWatch.Stop();
        var stopped = host.Snapshot();
        if (!IsCleanlyStopped(stopped))
        {
            return Failed(name, watch, "runtime did not stop cleanly after xbutton disable real target validation", CreateMetrics());
        }

        await host.DisposeAsync();
        host = null;

        var metrics = CreateMetrics();
        metrics["foreground_process"] = allowedGate.ForegroundProcess ?? string.Empty;
        metrics["disable_baseline_escape_events"] = baselineEscapeEvents;
        metrics["disable_trigger_received_count"] = CountHookTriggerReceived(diagnostics, xbuttonTriggerKey);
        metrics["disable_pipeline_enqueued_count"] = CountPipelineQueued(diagnostics, xbuttonTriggerKey);
        metrics["disable_runtime_dispatched_count"] = CountRuntimeDispatched(diagnostics, xbuttonTriggerKey);
        metrics["disable_output_delta"] = disabledOutputDelta;
        metrics["stop_ms"] = stopWatch.Elapsed.TotalMilliseconds;
        metrics["diagnostic_events"] = diagnostics.Count;
        metrics["stopped_clean"] = IsCleanlyStopped(stopped);
        AddSnapshots(metrics, options, ("disabled", disabled), ("stopped", stopped));
        return Passed(name, watch, metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message, CreateMetrics());
    }
    finally
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}

static async Task<AcceptanceScenarioResult> RunBlueArchiveManualXButtonHoldForegroundChangeAsync(AcceptanceOptions options)
{
    const string name = "bluearchive-manual-xbutton-hold-foreground-change";
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, $"{name} requires --allow-live-input");
    }

    var targetProcess = options.TargetProcess;
    var targetWindow = FindTargetProcessWithWindow(targetProcess);
    if (targetWindow is null)
    {
        return Failed(name, watch, $"target process with foreground-capable window was not found: {targetProcess}");
    }

    var xbuttonTrigger = KeyNameResolver.NormalizeMouseButton(options.ManualXButtonTrigger);
    if (!string.Equals(xbuttonTrigger, "mouse_x1", StringComparison.OrdinalIgnoreCase))
    {
        return Failed(name, watch, $"manual xbutton trigger for {name} must be mouse_x1, but was: {xbuttonTrigger}");
    }

    var runnerElevation = ProbeCurrentProcessElevation();
    var targetElevation = ProbeProcessElevation(targetWindow.Id, $"{targetWindow.ProcessName}.exe");
    var realTargetObserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var realTargetPending = new HashSet<string>(["hold_capture_allowed", "release_after_foreground_change"], StringComparer.OrdinalIgnoreCase);
    var realTargetFailed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var detailMetrics = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
    {
        ["manual_allowed_hold_confirmed"] = false
    };

    static string ScopeString(IEnumerable<string> orderedSteps, IReadOnlySet<string> activeSteps) =>
        string.Join(",", orderedSteps.Where(activeSteps.Contains));

    void MarkObserved(string step)
    {
        realTargetPending.Remove(step);
        realTargetFailed.Remove(step);
        realTargetObserved.Add(step);
    }

    void MarkFailed(string step)
    {
        realTargetObserved.Remove(step);
        realTargetPending.Remove(step);
        realTargetFailed.Add(step);
    }

    Dictionary<string, object> CreateMetrics()
    {
        var metrics = new Dictionary<string, object>
        {
            ["target_process"] = targetProcess,
            ["target_process_id"] = targetWindow.Id,
            ["target_window_title"] = targetWindow.MainWindowTitle,
            ["validation_scope"] = "phase2c-bluearchive-xbutton1-hold-foreground-change",
            ["manual_xbutton_trigger"] = xbuttonTrigger,
            ["runner_elevation_known"] = runnerElevation.IsKnown,
            ["runner_elevated"] = runnerElevation.IsKnown && runnerElevation.IsElevated,
            ["runner_elevated_state"] = ElevationStateLabel(runnerElevation),
            ["target_elevation_known"] = targetElevation.IsKnown,
            ["target_elevated"] = targetElevation.IsKnown && targetElevation.IsElevated,
            ["target_elevated_state"] = ElevationStateLabel(targetElevation),
            ["runner_target_same_elevation"] = runnerElevation.IsKnown &&
                targetElevation.IsKnown &&
                runnerElevation.IsElevated == targetElevation.IsElevated,
            ["real_target_observed_scope"] = ScopeString(["hold_capture_allowed", "release_after_foreground_change"], realTargetObserved),
            ["real_target_pending_scope"] = ScopeString(["hold_capture_allowed", "release_after_foreground_change"], realTargetPending),
            ["real_target_failed_scope"] = ScopeString(["hold_capture_allowed", "release_after_foreground_change"], realTargetFailed)
        };

        foreach (var (key, value) in detailMetrics)
        {
            metrics[key] = value;
        }

        return metrics;
    }

    if (!runnerElevation.IsKnown || !runnerElevation.IsElevated)
    {
        return Failed(
            name,
            watch,
            $"{name} requires an elevated acceptance/headless/live runner. Current runner is not elevated, so this real-target validation is invalid.",
            CreateMetrics());
    }

    using var blocker = new LiveHarnessWindow("BA KeySmith Real Target XButton Hold Blocker");
    RuntimeHost? host = null;
    try
    {
        blocker.Start(TimeSpan.FromSeconds(3));
        var runtimeInput = new CountingInputBackend(new WindowsInputBackend());
        var diagnostics = new CountingDiagnosticsSink();
        const string triggerKey = "mouse:mouse_x1";

        host = new RuntimeHost(
            LiveEscHoldConfig(targetProcess, xbuttonTrigger),
            runtimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(diagnostics),
            diagnostics);

        await host.StartAsync(CancellationToken.None);
        targetWindow.Refresh();
        if (!await LiveWindowTools.FocusAsync(targetWindow.MainWindowHandle, TimeSpan.FromSeconds(5)) ||
            !LiveWindowTools.MoveCursorToCenter(targetWindow.MainWindowHandle))
        {
            return Failed(name, watch, $"failed to focus target window and move cursor: {targetWindow.ProcessName}", CreateMetrics());
        }

        runtimeInput.Reset();
        diagnostics.Reset();
        blocker.Reset();
        Console.Error.WriteLine(
            $"MANUAL xbutton-hold-foreground-change: Blue Archive focused. Press and hold xbutton1 within {options.ManualTimeout.TotalSeconds:0}s. Keep holding until the blocker window is focused, then release xbutton1 while blocker stays focused. Expect mapped 'escape' down/up to stay aligned.");
        if (!await WaitUntilAsync(
                () => runtimeInput.Count("key", "escape", true) > 0,
                options.ManualTimeout,
                CancellationToken.None))
        {
            MarkFailed("hold_capture_allowed");
            return Failed(name, watch, "xbutton hold did not produce escape down in target foreground", CreateMetrics());
        }

        var manualHoldConfirmed = ResolveManualConfirmation(
            options,
            "Did Blue Archive show one clear 'escape/back/menu' effect when xbutton1 hold began?");
        if (manualHoldConfirmed is null)
        {
            detailMetrics["trigger_down_received_count"] = CountHookTriggerReceived(diagnostics, triggerKey, "down");
            detailMetrics["captured_session_entered_count"] = CountHookLifecycleEvent(diagnostics, "captured_session_entered", triggerKey, "down");
            MarkFailed("hold_capture_allowed");
            return Failed(name, watch, $"{name} requires manual confirmation for the allowed hold observation.", CreateMetrics());
        }

        if (manualHoldConfirmed is not true)
        {
            MarkFailed("hold_capture_allowed");
            return Failed(name, watch, "manual observation did not confirm xbutton hold capture behavior in real target", CreateMetrics());
        }

        detailMetrics["manual_allowed_hold_confirmed"] = true;
        MarkObserved("hold_capture_allowed");

        if (!await blocker.FocusAsync(TimeSpan.FromSeconds(5)) ||
            !LiveWindowTools.MoveCursorToCenter(blocker.Handle))
        {
            MarkFailed("release_after_foreground_change");
            return Failed(name, watch, "failed to focus blocker during real-target xbutton hold", CreateMetrics());
        }

        if (!await WaitUntilAsync(
                () => runtimeInput.Count("key", "escape", false) > 0,
                options.ManualTimeout,
                CancellationToken.None))
        {
            MarkFailed("release_after_foreground_change");
            return Failed(name, watch, "xbutton hold release did not produce escape up after blocker foreground change", CreateMetrics());
        }

        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            MarkFailed("release_after_foreground_change");
            return Failed(name, watch, "queues did not drain after real-target xbutton hold foreground-change scenario", CreateMetrics());
        }

        var running = host.Snapshot();
        var capturedSessionEnteredCount = CountHookLifecycleEvent(diagnostics, "captured_session_entered", triggerKey, "down");
        var releaseMatchedCapturedSessionCount = CountHookLifecycleEvent(diagnostics, "release_matched_captured_session", triggerKey, "up");
        var blockedPassThroughDownCount = CountHookTriggerReceived(diagnostics, triggerKey, "down", suppress: false, dispatch: false);
        var blockedPassThroughUpCount = CountHookTriggerReceived(diagnostics, triggerKey, "up", suppress: false, dispatch: false);
        var releaseRuntimeDispatchedCount = CountRuntimeDispatched(diagnostics, triggerKey, "up");
        var mappedOutputDownCount = runtimeInput.Count("key", "escape", true);
        var mappedOutputUpCount = runtimeInput.Count("key", "escape", false);
        var unexpectedDispatchAfterForegroundReturnCount = Math.Max(0, releaseRuntimeDispatchedCount - 1);
        var unexpectedOutputAfterReleaseCount = Math.Max(0, mappedOutputUpCount - 1);

        if (capturedSessionEnteredCount != 1 ||
            releaseMatchedCapturedSessionCount != 1 ||
            blockedPassThroughDownCount != 0 ||
            blockedPassThroughUpCount != 0 ||
            mappedOutputDownCount != 1 ||
            mappedOutputUpCount != 1)
        {
            MarkFailed("release_after_foreground_change");
            detailMetrics["trigger_down_received_count"] = CountHookTriggerReceived(diagnostics, triggerKey, "down");
            detailMetrics["trigger_up_received_count"] = CountHookTriggerReceived(diagnostics, triggerKey, "up");
            detailMetrics["captured_session_entered_count"] = capturedSessionEnteredCount;
            detailMetrics["release_matched_captured_session_count"] = releaseMatchedCapturedSessionCount;
            detailMetrics["blocked_pass_through_down_count"] = blockedPassThroughDownCount;
            detailMetrics["blocked_pass_through_up_count"] = blockedPassThroughUpCount;
            detailMetrics["mapped_output_down_count"] = mappedOutputDownCount;
            detailMetrics["mapped_output_up_count"] = mappedOutputUpCount;
            return Failed(name, watch, "real-target xbutton hold foreground-change metrics did not align with captured session expectations", CreateMetrics());
        }

        MarkObserved("release_after_foreground_change");

        var stopWatch = Stopwatch.StartNew();
        await host.StopAsync(CancellationToken.None);
        stopWatch.Stop();
        var stopped = host.Snapshot();
        if (!IsCleanlyStopped(stopped))
        {
            return Failed(name, watch, "runtime did not stop cleanly after real-target xbutton hold foreground-change validation", CreateMetrics());
        }

        await host.DisposeAsync();
        host = null;

        var metrics = CreateMetrics();
        metrics["foreground_changed_while_held_count"] = 1;
        metrics["foreground_restored_while_held_count"] = 0;
        metrics["trigger_down_received_count"] = CountHookTriggerReceived(diagnostics, triggerKey, "down");
        metrics["trigger_up_received_count"] = CountHookTriggerReceived(diagnostics, triggerKey, "up");
        metrics["captured_session_entered_count"] = capturedSessionEnteredCount;
        metrics["release_matched_captured_session_count"] = releaseMatchedCapturedSessionCount;
        metrics["retroactive_capture_count"] = Math.Max(0, capturedSessionEnteredCount - 1);
        metrics["pipeline_enqueued_down_count"] = CountPipelineQueued(diagnostics, triggerKey, "down");
        metrics["pipeline_enqueued_up_count"] = CountPipelineQueued(diagnostics, triggerKey, "up");
        metrics["runtime_dispatched_down_count"] = CountRuntimeDispatched(diagnostics, triggerKey, "down");
        metrics["runtime_dispatched_up_count"] = releaseRuntimeDispatchedCount;
        metrics["mapped_output_down_count"] = mappedOutputDownCount;
        metrics["mapped_output_up_count"] = mappedOutputUpCount;
        metrics["blocked_pass_through_down_count"] = blockedPassThroughDownCount;
        metrics["blocked_pass_through_up_count"] = blockedPassThroughUpCount;
        metrics["capture_blocked_by_foreground_count"] = CountHookLifecycleEvent(diagnostics, "capture_blocked_by_foreground", triggerKey, "down");
        metrics["uncaptured_release_pass_through_count"] = CountHookLifecycleEvent(diagnostics, "uncaptured_release_pass_through", triggerKey, "up");
        metrics["unexpected_dispatch_after_foreground_return_count"] = unexpectedDispatchAfterForegroundReturnCount;
        metrics["unexpected_output_after_release_count"] = unexpectedOutputAfterReleaseCount;
        metrics["cleanup_completed_count"] = 0;
        metrics["blocker_xbutton1_down_count"] = blocker.XButton1DownCount;
        metrics["blocker_xbutton1_up_count"] = blocker.XButton1UpCount;
        metrics["stop_ms"] = stopWatch.Elapsed.TotalMilliseconds;
        metrics["diagnostic_events"] = diagnostics.Count;
        metrics["stopped_clean"] = IsCleanlyStopped(stopped);
        AddSnapshots(metrics, options, ("running", running), ("stopped", stopped));
        return Passed(name, watch, metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message, CreateMetrics());
    }
    finally
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}

static async Task<AcceptanceScenarioResult> RunBlueArchiveManualXButtonBlockedHoldReturnAsync(AcceptanceOptions options)
{
    const string name = "bluearchive-manual-xbutton-blocked-hold-return";
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, $"{name} requires --allow-live-input");
    }

    var targetProcess = options.TargetProcess;
    var targetWindow = FindTargetProcessWithWindow(targetProcess);
    if (targetWindow is null)
    {
        return Failed(name, watch, $"target process with foreground-capable window was not found: {targetProcess}");
    }

    var xbuttonTrigger = KeyNameResolver.NormalizeMouseButton(options.ManualXButtonTrigger);
    if (!string.Equals(xbuttonTrigger, "mouse_x1", StringComparison.OrdinalIgnoreCase))
    {
        return Failed(name, watch, $"manual xbutton trigger for {name} must be mouse_x1, but was: {xbuttonTrigger}");
    }

    var runnerElevation = ProbeCurrentProcessElevation();
    var targetElevation = ProbeProcessElevation(targetWindow.Id, $"{targetWindow.ProcessName}.exe");
    var realTargetObserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var realTargetPending = new HashSet<string>(["blocked_hold", "release_after_foreground_return"], StringComparer.OrdinalIgnoreCase);
    var realTargetFailed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var detailMetrics = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
    {
        ["manual_blocked_hold_confirmed"] = false
    };

    static string ScopeString(IEnumerable<string> orderedSteps, IReadOnlySet<string> activeSteps) =>
        string.Join(",", orderedSteps.Where(activeSteps.Contains));

    void MarkObserved(string step)
    {
        realTargetPending.Remove(step);
        realTargetFailed.Remove(step);
        realTargetObserved.Add(step);
    }

    void MarkFailed(string step)
    {
        realTargetObserved.Remove(step);
        realTargetPending.Remove(step);
        realTargetFailed.Add(step);
    }

    Dictionary<string, object> CreateMetrics()
    {
        var metrics = new Dictionary<string, object>
        {
            ["target_process"] = targetProcess,
            ["target_process_id"] = targetWindow.Id,
            ["target_window_title"] = targetWindow.MainWindowTitle,
            ["validation_scope"] = "phase2c-bluearchive-xbutton1-blocked-hold-return",
            ["manual_xbutton_trigger"] = xbuttonTrigger,
            ["runner_elevation_known"] = runnerElevation.IsKnown,
            ["runner_elevated"] = runnerElevation.IsKnown && runnerElevation.IsElevated,
            ["runner_elevated_state"] = ElevationStateLabel(runnerElevation),
            ["target_elevation_known"] = targetElevation.IsKnown,
            ["target_elevated"] = targetElevation.IsKnown && targetElevation.IsElevated,
            ["target_elevated_state"] = ElevationStateLabel(targetElevation),
            ["runner_target_same_elevation"] = runnerElevation.IsKnown &&
                targetElevation.IsKnown &&
                runnerElevation.IsElevated == targetElevation.IsElevated,
            ["real_target_observed_scope"] = ScopeString(["blocked_hold", "release_after_foreground_return"], realTargetObserved),
            ["real_target_pending_scope"] = ScopeString(["blocked_hold", "release_after_foreground_return"], realTargetPending),
            ["real_target_failed_scope"] = ScopeString(["blocked_hold", "release_after_foreground_return"], realTargetFailed)
        };

        foreach (var (key, value) in detailMetrics)
        {
            metrics[key] = value;
        }

        return metrics;
    }

    if (!runnerElevation.IsKnown || !runnerElevation.IsElevated)
    {
        return Failed(
            name,
            watch,
            $"{name} requires an elevated acceptance/headless/live runner. Current runner is not elevated, so this real-target validation is invalid.",
            CreateMetrics());
    }

    using var blocker = new LiveHarnessWindow("BA KeySmith Real Target XButton Blocked Hold Blocker");
    RuntimeHost? host = null;
    try
    {
        blocker.Start(TimeSpan.FromSeconds(3));
        var runtimeInput = new CountingInputBackend(new WindowsInputBackend());
        var diagnostics = new CountingDiagnosticsSink();
        const string triggerKey = "mouse:mouse_x1";

        host = new RuntimeHost(
            LiveEscHoldConfig(targetProcess, xbuttonTrigger),
            runtimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(diagnostics),
            diagnostics);

        await host.StartAsync(CancellationToken.None);
        if (!await blocker.FocusAsync(TimeSpan.FromSeconds(5)) ||
            !LiveWindowTools.MoveCursorToCenter(blocker.Handle))
        {
            return Failed(name, watch, "failed to focus blocker before blocked-hold real-target validation", CreateMetrics());
        }

        runtimeInput.Reset();
        diagnostics.Reset();
        blocker.Reset();
        Console.Error.WriteLine(
            $"MANUAL xbutton-blocked-hold-return: blocker focused. Press and hold xbutton1 within {options.ManualTimeout.TotalSeconds:0}s. Keep holding while Blue Archive is restored to foreground, then release xbutton1 in Blue Archive. Expect no mapped 'escape' effect.");
        if (!await WaitUntilAsync(
                () => blocker.XButton1DownCount > 0 &&
                    CountHookTriggerReceived(diagnostics, triggerKey, "down") > 0,
                options.ManualTimeout,
                CancellationToken.None))
        {
            MarkFailed("blocked_hold");
            return Failed(name, watch, "blocked xbutton hold down was not observed in blocker foreground", CreateMetrics());
        }

        if (CountHookLifecycleEvent(diagnostics, "captured_session_entered", triggerKey, "down") != 0 ||
            CountPipelineQueued(diagnostics, triggerKey, "down") != 0 ||
            CountRuntimeDispatched(diagnostics, triggerKey, "down") != 0 ||
            runtimeInput.Count("key", "escape") != 0)
        {
            MarkFailed("blocked_hold");
            return Failed(name, watch, "blocked xbutton hold incorrectly entered captured session or produced output", CreateMetrics());
        }

        MarkObserved("blocked_hold");

        targetWindow.Refresh();
        if (!await LiveWindowTools.FocusAsync(targetWindow.MainWindowHandle, TimeSpan.FromSeconds(5)) ||
            !LiveWindowTools.MoveCursorToCenter(targetWindow.MainWindowHandle))
        {
            MarkFailed("release_after_foreground_return");
            return Failed(name, watch, $"failed to restore Blue Archive foreground while xbutton remained held: {targetWindow.ProcessName}", CreateMetrics());
        }

        if (!await WaitUntilAsync(
                () => CountHookTriggerReceived(diagnostics, triggerKey, "up") > 0,
                options.ManualTimeout,
                CancellationToken.None))
        {
            MarkFailed("release_after_foreground_return");
            return Failed(name, watch, "blocked hold release was not observed after Blue Archive foreground returned", CreateMetrics());
        }

        await Task.Delay(150, CancellationToken.None);
        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            MarkFailed("release_after_foreground_return");
            return Failed(name, watch, "queues did not drain after blocked-hold real-target return scenario", CreateMetrics());
        }

        var manualBlockedHoldConfirmed = ResolveManualConfirmation(
            options,
            "Did Blue Archive remain unchanged with no mapped 'escape/back/menu' effect when you released xbutton1 after returning to target?");
        if (manualBlockedHoldConfirmed is null)
        {
            detailMetrics["trigger_up_received_count"] = CountHookTriggerReceived(diagnostics, triggerKey, "up");
            detailMetrics["unexpected_dispatch_after_foreground_return_count"] = CountRuntimeDispatched(diagnostics, triggerKey, "up");
            detailMetrics["unexpected_output_after_release_count"] = runtimeInput.Count("key", "escape");
            MarkFailed("release_after_foreground_return");
            return Failed(name, watch, $"{name} requires manual confirmation for the blocked-hold return observation.", CreateMetrics());
        }

        if (manualBlockedHoldConfirmed is not true)
        {
            MarkFailed("release_after_foreground_return");
            return Failed(name, watch, "manual observation did not confirm blocked-hold no-output behavior in real target", CreateMetrics());
        }

        detailMetrics["manual_blocked_hold_confirmed"] = true;
        var running = host.Snapshot();
        var capturedSessionEnteredCount = CountHookLifecycleEvent(diagnostics, "captured_session_entered", triggerKey, "down");
        var releaseMatchedCapturedSessionCount = CountHookLifecycleEvent(diagnostics, "release_matched_captured_session", triggerKey, "up");
        var blockedPassThroughDownCount = CountHookTriggerReceived(diagnostics, triggerKey, "down", suppress: false, dispatch: false);
        var blockedPassThroughUpCount = CountHookTriggerReceived(diagnostics, triggerKey, "up", suppress: false, dispatch: false);
        var runtimeDispatchedDownCount = CountRuntimeDispatched(diagnostics, triggerKey, "down");
        var runtimeDispatchedUpCount = CountRuntimeDispatched(diagnostics, triggerKey, "up");
        var mappedOutputDownCount = runtimeInput.Count("key", "escape", true);
        var mappedOutputUpCount = runtimeInput.Count("key", "escape", false);

        if (capturedSessionEnteredCount != 0 ||
            releaseMatchedCapturedSessionCount != 0 ||
            runtimeDispatchedDownCount != 0 ||
            runtimeDispatchedUpCount != 0 ||
            mappedOutputDownCount != 0 ||
            mappedOutputUpCount != 0)
        {
            MarkFailed("release_after_foreground_return");
            detailMetrics["captured_session_entered_count"] = capturedSessionEnteredCount;
            detailMetrics["release_matched_captured_session_count"] = releaseMatchedCapturedSessionCount;
            detailMetrics["runtime_dispatched_down_count"] = runtimeDispatchedDownCount;
            detailMetrics["runtime_dispatched_up_count"] = runtimeDispatchedUpCount;
            detailMetrics["mapped_output_down_count"] = mappedOutputDownCount;
            detailMetrics["mapped_output_up_count"] = mappedOutputUpCount;
            return Failed(name, watch, "blocked-hold return produced captured session activity or mapped output", CreateMetrics());
        }

        MarkObserved("release_after_foreground_return");

        var stopWatch = Stopwatch.StartNew();
        await host.StopAsync(CancellationToken.None);
        stopWatch.Stop();
        var stopped = host.Snapshot();
        if (!IsCleanlyStopped(stopped))
        {
            return Failed(name, watch, "runtime did not stop cleanly after blocked-hold real-target validation", CreateMetrics());
        }

        await host.DisposeAsync();
        host = null;

        var metrics = CreateMetrics();
        metrics["foreground_changed_while_held_count"] = 1;
        metrics["foreground_restored_while_held_count"] = 1;
        metrics["trigger_down_received_count"] = CountHookTriggerReceived(diagnostics, triggerKey, "down");
        metrics["trigger_up_received_count"] = CountHookTriggerReceived(diagnostics, triggerKey, "up");
        metrics["captured_session_entered_count"] = capturedSessionEnteredCount;
        metrics["release_matched_captured_session_count"] = releaseMatchedCapturedSessionCount;
        metrics["retroactive_capture_count"] = capturedSessionEnteredCount;
        metrics["pipeline_enqueued_down_count"] = CountPipelineQueued(diagnostics, triggerKey, "down");
        metrics["pipeline_enqueued_up_count"] = CountPipelineQueued(diagnostics, triggerKey, "up");
        metrics["runtime_dispatched_down_count"] = runtimeDispatchedDownCount;
        metrics["runtime_dispatched_up_count"] = runtimeDispatchedUpCount;
        metrics["mapped_output_down_count"] = mappedOutputDownCount;
        metrics["mapped_output_up_count"] = mappedOutputUpCount;
        metrics["blocked_pass_through_down_count"] = blockedPassThroughDownCount;
        metrics["blocked_pass_through_up_count"] = blockedPassThroughUpCount;
        metrics["capture_blocked_by_foreground_count"] = CountHookLifecycleEvent(diagnostics, "capture_blocked_by_foreground", triggerKey, "down");
        metrics["uncaptured_release_pass_through_count"] = CountHookLifecycleEvent(diagnostics, "uncaptured_release_pass_through", triggerKey, "up");
        metrics["unexpected_dispatch_after_foreground_return_count"] = runtimeDispatchedDownCount + runtimeDispatchedUpCount;
        metrics["unexpected_output_after_release_count"] = mappedOutputDownCount + mappedOutputUpCount;
        metrics["cleanup_completed_count"] = 0;
        metrics["blocker_xbutton1_down_count"] = blocker.XButton1DownCount;
        metrics["blocker_xbutton1_up_count"] = blocker.XButton1UpCount;
        metrics["stop_ms"] = stopWatch.Elapsed.TotalMilliseconds;
        metrics["diagnostic_events"] = diagnostics.Count;
        metrics["stopped_clean"] = IsCleanlyStopped(stopped);
        AddSnapshots(metrics, options, ("running", running), ("stopped", stopped));
        return Passed(name, watch, metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message, CreateMetrics());
    }
    finally
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}

static async Task<AcceptanceScenarioResult> RunBlueArchiveManualXButton2HoldForegroundChangeAsync(AcceptanceOptions options)
{
    const string name = "bluearchive-manual-xbutton2-hold-foreground-change";
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, $"{name} requires --allow-live-input");
    }

    var targetProcess = options.TargetProcess;
    var targetWindow = FindTargetProcessWithWindow(targetProcess);
    if (targetWindow is null)
    {
        return Failed(name, watch, $"target process with foreground-capable window was not found: {targetProcess}");
    }

    var xbuttonTrigger = KeyNameResolver.NormalizeMouseButton(options.ManualXButtonTrigger);
    if (!string.Equals(xbuttonTrigger, "mouse_x2", StringComparison.OrdinalIgnoreCase))
    {
        return Failed(name, watch, $"manual xbutton trigger for {name} must be mouse_x2, but was: {xbuttonTrigger}");
    }

    var runnerElevation = ProbeCurrentProcessElevation();
    var targetElevation = ProbeProcessElevation(targetWindow.Id, $"{targetWindow.ProcessName}.exe");
    var realTargetObserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var realTargetPending = new HashSet<string>(["hold_capture_allowed", "release_after_foreground_change"], StringComparer.OrdinalIgnoreCase);
    var realTargetFailed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var detailMetrics = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
    {
        ["manual_allowed_hold_confirmed"] = false
    };

    static string ScopeString(IEnumerable<string> orderedSteps, IReadOnlySet<string> activeSteps) =>
        string.Join(",", orderedSteps.Where(activeSteps.Contains));

    void MarkObserved(string step)
    {
        realTargetPending.Remove(step);
        realTargetFailed.Remove(step);
        realTargetObserved.Add(step);
    }

    void MarkFailed(string step)
    {
        realTargetObserved.Remove(step);
        realTargetPending.Remove(step);
        realTargetFailed.Add(step);
    }

    Dictionary<string, object> CreateMetrics()
    {
        var metrics = new Dictionary<string, object>
        {
            ["target_process"] = targetProcess,
            ["target_process_id"] = targetWindow.Id,
            ["target_window_title"] = targetWindow.MainWindowTitle,
            ["validation_scope"] = "phase2c-bluearchive-xbutton2-hold-foreground-change",
            ["manual_xbutton_trigger"] = xbuttonTrigger,
            ["runner_elevation_known"] = runnerElevation.IsKnown,
            ["runner_elevated"] = runnerElevation.IsKnown && runnerElevation.IsElevated,
            ["runner_elevated_state"] = ElevationStateLabel(runnerElevation),
            ["target_elevation_known"] = targetElevation.IsKnown,
            ["target_elevated"] = targetElevation.IsKnown && targetElevation.IsElevated,
            ["target_elevated_state"] = ElevationStateLabel(targetElevation),
            ["runner_target_same_elevation"] = runnerElevation.IsKnown &&
                targetElevation.IsKnown &&
                runnerElevation.IsElevated == targetElevation.IsElevated,
            ["real_target_observed_scope"] = ScopeString(["hold_capture_allowed", "release_after_foreground_change"], realTargetObserved),
            ["real_target_pending_scope"] = ScopeString(["hold_capture_allowed", "release_after_foreground_change"], realTargetPending),
            ["real_target_failed_scope"] = ScopeString(["hold_capture_allowed", "release_after_foreground_change"], realTargetFailed)
        };

        foreach (var (key, value) in detailMetrics)
        {
            metrics[key] = value;
        }

        return metrics;
    }

    if (!runnerElevation.IsKnown || !runnerElevation.IsElevated)
    {
        return Failed(
            name,
            watch,
            $"{name} requires an elevated acceptance/headless/live runner. Current runner is not elevated, so this real-target validation is invalid.",
            CreateMetrics());
    }

    using var blocker = new LiveHarnessWindow("BA KeySmith Real Target XButton2 Hold Blocker");
    RuntimeHost? host = null;
    try
    {
        blocker.Start(TimeSpan.FromSeconds(3));
        var runtimeInput = new CountingInputBackend(new WindowsInputBackend());
        var diagnostics = new CountingDiagnosticsSink();
        const string triggerKey = "mouse:mouse_x2";

        host = new RuntimeHost(
            LiveEscHoldConfig(targetProcess, xbuttonTrigger),
            runtimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(diagnostics),
            diagnostics);

        await host.StartAsync(CancellationToken.None);
        targetWindow.Refresh();
        if (!await LiveWindowTools.FocusAsync(targetWindow.MainWindowHandle, TimeSpan.FromSeconds(5)) ||
            !LiveWindowTools.MoveCursorToCenter(targetWindow.MainWindowHandle))
        {
            return Failed(name, watch, $"failed to focus target window and move cursor: {targetWindow.ProcessName}", CreateMetrics());
        }

        runtimeInput.Reset();
        diagnostics.Reset();
        blocker.Reset();
        Console.Error.WriteLine(
            $"MANUAL xbutton2-hold-foreground-change: Blue Archive focused. Press and hold xbutton2 within {options.ManualTimeout.TotalSeconds:0}s. Keep holding until the blocker window is focused, then release xbutton2 while blocker stays focused. Expect mapped 'escape' down/up to stay aligned.");
        if (!await WaitUntilAsync(
                () => runtimeInput.Count("key", "escape", true) > 0,
                options.ManualTimeout,
                CancellationToken.None))
        {
            MarkFailed("hold_capture_allowed");
            return Failed(name, watch, "xbutton2 hold did not produce escape down in target foreground", CreateMetrics());
        }

        var manualHoldConfirmed = ResolveManualConfirmation(
            options,
            "Did Blue Archive show one clear 'escape/back/menu' effect when xbutton2 hold began?");
        if (manualHoldConfirmed is null)
        {
            detailMetrics["trigger_down_received_count"] = CountHookTriggerReceived(diagnostics, triggerKey, "down");
            detailMetrics["captured_session_entered_count"] = CountHookLifecycleEvent(diagnostics, "captured_session_entered", triggerKey, "down");
            MarkFailed("hold_capture_allowed");
            return Failed(name, watch, $"{name} requires manual confirmation for the allowed hold observation.", CreateMetrics());
        }

        if (manualHoldConfirmed is not true)
        {
            MarkFailed("hold_capture_allowed");
            return Failed(name, watch, "manual observation did not confirm xbutton2 hold capture behavior in real target", CreateMetrics());
        }

        detailMetrics["manual_allowed_hold_confirmed"] = true;
        MarkObserved("hold_capture_allowed");

        if (!await blocker.FocusAsync(TimeSpan.FromSeconds(5)) ||
            !LiveWindowTools.MoveCursorToCenter(blocker.Handle))
        {
            MarkFailed("release_after_foreground_change");
            return Failed(name, watch, "failed to focus blocker during real-target xbutton2 hold", CreateMetrics());
        }

        if (!await WaitUntilAsync(
                () => runtimeInput.Count("key", "escape", false) > 0,
                options.ManualTimeout,
                CancellationToken.None))
        {
            MarkFailed("release_after_foreground_change");
            return Failed(name, watch, "xbutton2 hold release did not produce escape up after blocker foreground change", CreateMetrics());
        }

        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            MarkFailed("release_after_foreground_change");
            return Failed(name, watch, "queues did not drain after real-target xbutton2 hold foreground-change scenario", CreateMetrics());
        }

        var running = host.Snapshot();
        var capturedSessionEnteredCount = CountHookLifecycleEvent(diagnostics, "captured_session_entered", triggerKey, "down");
        var releaseMatchedCapturedSessionCount = CountHookLifecycleEvent(diagnostics, "release_matched_captured_session", triggerKey, "up");
        var blockedPassThroughDownCount = CountHookTriggerReceived(diagnostics, triggerKey, "down", suppress: false, dispatch: false);
        var blockedPassThroughUpCount = CountHookTriggerReceived(diagnostics, triggerKey, "up", suppress: false, dispatch: false);
        var releaseRuntimeDispatchedCount = CountRuntimeDispatched(diagnostics, triggerKey, "up");
        var mappedOutputDownCount = runtimeInput.Count("key", "escape", true);
        var mappedOutputUpCount = runtimeInput.Count("key", "escape", false);
        var unexpectedDispatchAfterForegroundReturnCount = Math.Max(0, releaseRuntimeDispatchedCount - 1);
        var unexpectedOutputAfterReleaseCount = Math.Max(0, mappedOutputUpCount - 1);

        if (capturedSessionEnteredCount != 1 ||
            releaseMatchedCapturedSessionCount != 1 ||
            blockedPassThroughDownCount != 0 ||
            blockedPassThroughUpCount != 0 ||
            mappedOutputDownCount != 1 ||
            mappedOutputUpCount != 1)
        {
            MarkFailed("release_after_foreground_change");
            detailMetrics["trigger_down_received_count"] = CountHookTriggerReceived(diagnostics, triggerKey, "down");
            detailMetrics["trigger_up_received_count"] = CountHookTriggerReceived(diagnostics, triggerKey, "up");
            detailMetrics["captured_session_entered_count"] = capturedSessionEnteredCount;
            detailMetrics["release_matched_captured_session_count"] = releaseMatchedCapturedSessionCount;
            detailMetrics["blocked_pass_through_down_count"] = blockedPassThroughDownCount;
            detailMetrics["blocked_pass_through_up_count"] = blockedPassThroughUpCount;
            detailMetrics["mapped_output_down_count"] = mappedOutputDownCount;
            detailMetrics["mapped_output_up_count"] = mappedOutputUpCount;
            return Failed(name, watch, "real-target xbutton2 hold foreground-change metrics did not align with captured session expectations", CreateMetrics());
        }

        MarkObserved("release_after_foreground_change");

        var stopWatch = Stopwatch.StartNew();
        await host.StopAsync(CancellationToken.None);
        stopWatch.Stop();
        var stopped = host.Snapshot();
        if (!IsCleanlyStopped(stopped))
        {
            return Failed(name, watch, "runtime did not stop cleanly after real-target xbutton2 hold foreground-change validation", CreateMetrics());
        }

        await host.DisposeAsync();
        host = null;

        var metrics = CreateMetrics();
        metrics["foreground_changed_while_held_count"] = 1;
        metrics["foreground_restored_while_held_count"] = 0;
        metrics["trigger_down_received_count"] = CountHookTriggerReceived(diagnostics, triggerKey, "down");
        metrics["trigger_up_received_count"] = CountHookTriggerReceived(diagnostics, triggerKey, "up");
        metrics["captured_session_entered_count"] = capturedSessionEnteredCount;
        metrics["release_matched_captured_session_count"] = releaseMatchedCapturedSessionCount;
        metrics["retroactive_capture_count"] = Math.Max(0, capturedSessionEnteredCount - 1);
        metrics["pipeline_enqueued_down_count"] = CountPipelineQueued(diagnostics, triggerKey, "down");
        metrics["pipeline_enqueued_up_count"] = CountPipelineQueued(diagnostics, triggerKey, "up");
        metrics["runtime_dispatched_down_count"] = CountRuntimeDispatched(diagnostics, triggerKey, "down");
        metrics["runtime_dispatched_up_count"] = releaseRuntimeDispatchedCount;
        metrics["mapped_output_down_count"] = mappedOutputDownCount;
        metrics["mapped_output_up_count"] = mappedOutputUpCount;
        metrics["blocked_pass_through_down_count"] = blockedPassThroughDownCount;
        metrics["blocked_pass_through_up_count"] = blockedPassThroughUpCount;
        metrics["capture_blocked_by_foreground_count"] = CountHookLifecycleEvent(diagnostics, "capture_blocked_by_foreground", triggerKey, "down");
        metrics["uncaptured_release_pass_through_count"] = CountHookLifecycleEvent(diagnostics, "uncaptured_release_pass_through", triggerKey, "up");
        metrics["unexpected_dispatch_after_foreground_return_count"] = unexpectedDispatchAfterForegroundReturnCount;
        metrics["unexpected_output_after_release_count"] = unexpectedOutputAfterReleaseCount;
        metrics["cleanup_completed_count"] = 0;
        metrics["blocker_xbutton2_down_count"] = blocker.XButton2DownCount;
        metrics["blocker_xbutton2_up_count"] = blocker.XButton2UpCount;
        metrics["stop_ms"] = stopWatch.Elapsed.TotalMilliseconds;
        metrics["diagnostic_events"] = diagnostics.Count;
        metrics["stopped_clean"] = IsCleanlyStopped(stopped);
        AddSnapshots(metrics, options, ("running", running), ("stopped", stopped));
        return Passed(name, watch, metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message, CreateMetrics());
    }
    finally
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}

static async Task<AcceptanceScenarioResult> RunBlueArchiveManualXButton2BlockedHoldReturnAsync(AcceptanceOptions options)
{
    const string name = "bluearchive-manual-xbutton2-blocked-hold-return";
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, $"{name} requires --allow-live-input");
    }

    var targetProcess = options.TargetProcess;
    var targetWindow = FindTargetProcessWithWindow(targetProcess);
    if (targetWindow is null)
    {
        return Failed(name, watch, $"target process with foreground-capable window was not found: {targetProcess}");
    }

    var xbuttonTrigger = KeyNameResolver.NormalizeMouseButton(options.ManualXButtonTrigger);
    if (!string.Equals(xbuttonTrigger, "mouse_x2", StringComparison.OrdinalIgnoreCase))
    {
        return Failed(name, watch, $"manual xbutton trigger for {name} must be mouse_x2, but was: {xbuttonTrigger}");
    }

    var runnerElevation = ProbeCurrentProcessElevation();
    var targetElevation = ProbeProcessElevation(targetWindow.Id, $"{targetWindow.ProcessName}.exe");
    var realTargetObserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var realTargetPending = new HashSet<string>(["blocked_hold", "release_after_foreground_return"], StringComparer.OrdinalIgnoreCase);
    var realTargetFailed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var detailMetrics = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
    {
        ["manual_blocked_hold_confirmed"] = false
    };

    static string ScopeString(IEnumerable<string> orderedSteps, IReadOnlySet<string> activeSteps) =>
        string.Join(",", orderedSteps.Where(activeSteps.Contains));

    void MarkObserved(string step)
    {
        realTargetPending.Remove(step);
        realTargetFailed.Remove(step);
        realTargetObserved.Add(step);
    }

    void MarkFailed(string step)
    {
        realTargetObserved.Remove(step);
        realTargetPending.Remove(step);
        realTargetFailed.Add(step);
    }

    Dictionary<string, object> CreateMetrics()
    {
        var metrics = new Dictionary<string, object>
        {
            ["target_process"] = targetProcess,
            ["target_process_id"] = targetWindow.Id,
            ["target_window_title"] = targetWindow.MainWindowTitle,
            ["validation_scope"] = "phase2c-bluearchive-xbutton2-blocked-hold-return",
            ["manual_xbutton_trigger"] = xbuttonTrigger,
            ["runner_elevation_known"] = runnerElevation.IsKnown,
            ["runner_elevated"] = runnerElevation.IsKnown && runnerElevation.IsElevated,
            ["runner_elevated_state"] = ElevationStateLabel(runnerElevation),
            ["target_elevation_known"] = targetElevation.IsKnown,
            ["target_elevated"] = targetElevation.IsKnown && targetElevation.IsElevated,
            ["target_elevated_state"] = ElevationStateLabel(targetElevation),
            ["runner_target_same_elevation"] = runnerElevation.IsKnown &&
                targetElevation.IsKnown &&
                runnerElevation.IsElevated == targetElevation.IsElevated,
            ["real_target_observed_scope"] = ScopeString(["blocked_hold", "release_after_foreground_return"], realTargetObserved),
            ["real_target_pending_scope"] = ScopeString(["blocked_hold", "release_after_foreground_return"], realTargetPending),
            ["real_target_failed_scope"] = ScopeString(["blocked_hold", "release_after_foreground_return"], realTargetFailed)
        };

        foreach (var (key, value) in detailMetrics)
        {
            metrics[key] = value;
        }

        return metrics;
    }

    if (!runnerElevation.IsKnown || !runnerElevation.IsElevated)
    {
        return Failed(
            name,
            watch,
            $"{name} requires an elevated acceptance/headless/live runner. Current runner is not elevated, so this real-target validation is invalid.",
            CreateMetrics());
    }

    using var blocker = new LiveHarnessWindow("BA KeySmith Real Target XButton2 Blocked Hold Blocker");
    RuntimeHost? host = null;
    try
    {
        blocker.Start(TimeSpan.FromSeconds(3));
        var runtimeInput = new CountingInputBackend(new WindowsInputBackend());
        var diagnostics = new CountingDiagnosticsSink();
        const string triggerKey = "mouse:mouse_x2";

        host = new RuntimeHost(
            LiveEscHoldConfig(targetProcess, xbuttonTrigger),
            runtimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(diagnostics),
            diagnostics);

        await host.StartAsync(CancellationToken.None);
        if (!await blocker.FocusAsync(TimeSpan.FromSeconds(5)) ||
            !LiveWindowTools.MoveCursorToCenter(blocker.Handle))
        {
            return Failed(name, watch, "failed to focus blocker before blocked-hold real-target validation", CreateMetrics());
        }

        runtimeInput.Reset();
        diagnostics.Reset();
        blocker.Reset();
        Console.Error.WriteLine(
            $"MANUAL xbutton2-blocked-hold-return: blocker focused. Press and hold xbutton2 within {options.ManualTimeout.TotalSeconds:0}s. Keep holding while Blue Archive is restored to foreground, then release xbutton2 in Blue Archive. Expect no mapped 'escape' effect.");
        if (!await WaitUntilAsync(
                () => CountHookTriggerReceived(diagnostics, triggerKey, "down") > 0,
                options.ManualTimeout,
                CancellationToken.None))
        {
            MarkFailed("blocked_hold");
            return Failed(name, watch, "blocked xbutton2 hold down was not observed by hook in blocker foreground", CreateMetrics());
        }

        if (CountHookLifecycleEvent(diagnostics, "captured_session_entered", triggerKey, "down") != 0 ||
            CountPipelineQueued(diagnostics, triggerKey, "down") != 0 ||
            CountRuntimeDispatched(diagnostics, triggerKey, "down") != 0 ||
            runtimeInput.Count("key", "escape") != 0)
        {
            MarkFailed("blocked_hold");
            return Failed(name, watch, "blocked xbutton2 hold incorrectly entered captured session or produced output", CreateMetrics());
        }

        MarkObserved("blocked_hold");

        targetWindow.Refresh();
        if (!await LiveWindowTools.FocusAsync(targetWindow.MainWindowHandle, TimeSpan.FromSeconds(5)) ||
            !LiveWindowTools.MoveCursorToCenter(targetWindow.MainWindowHandle))
        {
            MarkFailed("release_after_foreground_return");
            return Failed(name, watch, $"failed to restore Blue Archive foreground while xbutton2 remained held: {targetWindow.ProcessName}", CreateMetrics());
        }

        if (!await WaitUntilAsync(
                () => CountHookTriggerReceived(diagnostics, triggerKey, "up") > 0,
                options.ManualTimeout,
                CancellationToken.None))
        {
            MarkFailed("release_after_foreground_return");
            return Failed(name, watch, "blocked xbutton2 hold release was not observed after Blue Archive foreground returned", CreateMetrics());
        }

        await Task.Delay(150, CancellationToken.None);
        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            MarkFailed("release_after_foreground_return");
            return Failed(name, watch, "queues did not drain after blocked-hold real-target return scenario", CreateMetrics());
        }

        var manualBlockedHoldConfirmed = ResolveManualConfirmation(
            options,
            "Did Blue Archive remain unchanged with no mapped 'escape/back/menu' effect when you released xbutton2 after returning to target?");
        if (manualBlockedHoldConfirmed is null)
        {
            detailMetrics["trigger_up_received_count"] = CountHookTriggerReceived(diagnostics, triggerKey, "up");
            detailMetrics["unexpected_dispatch_after_foreground_return_count"] = CountRuntimeDispatched(diagnostics, triggerKey, "up");
            detailMetrics["unexpected_output_after_release_count"] = runtimeInput.Count("key", "escape");
            MarkFailed("release_after_foreground_return");
            return Failed(name, watch, $"{name} requires manual confirmation for the blocked-hold return observation.", CreateMetrics());
        }

        if (manualBlockedHoldConfirmed is not true)
        {
            MarkFailed("release_after_foreground_return");
            return Failed(name, watch, "manual observation did not confirm blocked-hold no-output behavior for xbutton2 in real target", CreateMetrics());
        }

        detailMetrics["manual_blocked_hold_confirmed"] = true;
        var running = host.Snapshot();
        var capturedSessionEnteredCount = CountHookLifecycleEvent(diagnostics, "captured_session_entered", triggerKey, "down");
        var releaseMatchedCapturedSessionCount = CountHookLifecycleEvent(diagnostics, "release_matched_captured_session", triggerKey, "up");
        var blockedPassThroughDownCount = CountHookTriggerReceived(diagnostics, triggerKey, "down", suppress: false, dispatch: false);
        var blockedPassThroughUpCount = CountHookTriggerReceived(diagnostics, triggerKey, "up", suppress: false, dispatch: false);
        var runtimeDispatchedDownCount = CountRuntimeDispatched(diagnostics, triggerKey, "down");
        var runtimeDispatchedUpCount = CountRuntimeDispatched(diagnostics, triggerKey, "up");
        var mappedOutputDownCount = runtimeInput.Count("key", "escape", true);
        var mappedOutputUpCount = runtimeInput.Count("key", "escape", false);

        if (capturedSessionEnteredCount != 0 ||
            releaseMatchedCapturedSessionCount != 0 ||
            runtimeDispatchedDownCount != 0 ||
            runtimeDispatchedUpCount != 0 ||
            mappedOutputDownCount != 0 ||
            mappedOutputUpCount != 0)
        {
            MarkFailed("release_after_foreground_return");
            detailMetrics["captured_session_entered_count"] = capturedSessionEnteredCount;
            detailMetrics["release_matched_captured_session_count"] = releaseMatchedCapturedSessionCount;
            detailMetrics["runtime_dispatched_down_count"] = runtimeDispatchedDownCount;
            detailMetrics["runtime_dispatched_up_count"] = runtimeDispatchedUpCount;
            return Failed(name, watch, "blocked-hold return metrics for xbutton2 did not align with non-captured expectations", CreateMetrics());
        }

        if (blockedPassThroughDownCount == 0 || blockedPassThroughUpCount == 0)
        {
            MarkFailed("release_after_foreground_return");
            detailMetrics["blocked_pass_through_down_count"] = blockedPassThroughDownCount;
            detailMetrics["blocked_pass_through_up_count"] = blockedPassThroughUpCount;
            return Failed(name, watch, "blocked xbutton2 hold did not remain pass-through across down/up in real target", CreateMetrics());
        }

        MarkObserved("release_after_foreground_return");

        var stopWatch = Stopwatch.StartNew();
        await host.StopAsync(CancellationToken.None);
        stopWatch.Stop();
        var stopped = host.Snapshot();
        if (!IsCleanlyStopped(stopped))
        {
            return Failed(name, watch, "runtime did not stop cleanly after real-target xbutton2 blocked-hold return validation", CreateMetrics());
        }

        await host.DisposeAsync();
        host = null;

        var metrics = CreateMetrics();
        metrics["foreground_changed_while_held_count"] = 1;
        metrics["foreground_restored_while_held_count"] = 1;
        metrics["trigger_down_received_count"] = CountHookTriggerReceived(diagnostics, triggerKey, "down");
        metrics["trigger_up_received_count"] = CountHookTriggerReceived(diagnostics, triggerKey, "up");
        metrics["captured_session_entered_count"] = capturedSessionEnteredCount;
        metrics["release_matched_captured_session_count"] = releaseMatchedCapturedSessionCount;
        metrics["retroactive_capture_count"] = capturedSessionEnteredCount;
        metrics["pipeline_enqueued_down_count"] = CountPipelineQueued(diagnostics, triggerKey, "down");
        metrics["pipeline_enqueued_up_count"] = CountPipelineQueued(diagnostics, triggerKey, "up");
        metrics["runtime_dispatched_down_count"] = runtimeDispatchedDownCount;
        metrics["runtime_dispatched_up_count"] = runtimeDispatchedUpCount;
        metrics["mapped_output_down_count"] = mappedOutputDownCount;
        metrics["mapped_output_up_count"] = mappedOutputUpCount;
        metrics["blocked_pass_through_down_count"] = blockedPassThroughDownCount;
        metrics["blocked_pass_through_up_count"] = blockedPassThroughUpCount;
        metrics["capture_blocked_by_foreground_count"] = CountHookLifecycleEvent(diagnostics, "capture_blocked_by_foreground", triggerKey, "down");
        metrics["uncaptured_release_pass_through_count"] = CountHookLifecycleEvent(diagnostics, "uncaptured_release_pass_through", triggerKey, "up");
        metrics["unexpected_dispatch_after_foreground_return_count"] = runtimeDispatchedDownCount + runtimeDispatchedUpCount;
        metrics["unexpected_output_after_release_count"] = mappedOutputDownCount + mappedOutputUpCount;
        metrics["cleanup_completed_count"] = 0;
        metrics["blocker_xbutton2_down_count"] = blocker.XButton2DownCount;
        metrics["blocker_xbutton2_up_count"] = blocker.XButton2UpCount;
        metrics["stop_ms"] = stopWatch.Elapsed.TotalMilliseconds;
        metrics["diagnostic_events"] = diagnostics.Count;
        metrics["stopped_clean"] = IsCleanlyStopped(stopped);
        AddSnapshots(metrics, options, ("running", running), ("stopped", stopped));
        return Passed(name, watch, metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message, CreateMetrics());
    }
    finally
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}

static async Task<AcceptanceScenarioResult> RunBlueArchiveManualXButton2DragMinimalAsync(AcceptanceOptions options)
{
    const string name = "bluearchive-manual-xbutton2-drag-minimal";
    const string triggerKey = "mouse:mouse_x2";
    const string mappingId = "xbutton2-drag-minimal";
    const string mappedDragButton = "mouse_middle";
    const string script = "drag_rel 24 12 mouse_middle";
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, $"{name} requires --allow-live-input");
    }

    var targetProcess = options.TargetProcess;
    var targetWindow = FindTargetProcessWithWindow(targetProcess);
    if (targetWindow is null)
    {
        return Failed(name, watch, $"target process with foreground-capable window was not found: {targetProcess}");
    }

    var xbuttonTrigger = KeyNameResolver.NormalizeMouseButton(options.ManualXButtonTrigger);
    if (!string.Equals(xbuttonTrigger, "mouse_x2", StringComparison.OrdinalIgnoreCase))
    {
        return Failed(name, watch, $"manual xbutton trigger for {name} must be mouse_x2, but was: {xbuttonTrigger}");
    }

    var runnerElevation = ProbeCurrentProcessElevation();
    var targetElevation = ProbeProcessElevation(targetWindow.Id, $"{targetWindow.ProcessName}.exe");
    var detailMetrics = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
    {
        ["manual_drag_confirmed"] = false,
        ["mapped_drag_button"] = mappedDragButton
    };

    Dictionary<string, object> CreateMetrics()
    {
        var metrics = new Dictionary<string, object>
        {
            ["target_process"] = targetProcess,
            ["target_process_id"] = targetWindow.Id,
            ["target_window_title"] = targetWindow.MainWindowTitle,
            ["validation_scope"] = "phase2d-bluearchive-xbutton2-drag-minimal",
            ["manual_xbutton_trigger"] = xbuttonTrigger,
            ["runner_elevation_known"] = runnerElevation.IsKnown,
            ["runner_elevated"] = runnerElevation.IsKnown && runnerElevation.IsElevated,
            ["runner_elevated_state"] = ElevationStateLabel(runnerElevation),
            ["target_elevation_known"] = targetElevation.IsKnown,
            ["target_elevated"] = targetElevation.IsKnown && targetElevation.IsElevated,
            ["target_elevated_state"] = ElevationStateLabel(targetElevation),
            ["runner_target_same_elevation"] = runnerElevation.IsKnown &&
                targetElevation.IsKnown &&
                runnerElevation.IsElevated == targetElevation.IsElevated,
            ["real_target_observed_scope"] = "drag_minimal"
        };

        foreach (var (key, value) in detailMetrics)
        {
            metrics[key] = value;
        }

        return metrics;
    }

    if (!runnerElevation.IsKnown || !runnerElevation.IsElevated)
    {
        return Failed(
            name,
            watch,
            $"{name} requires an elevated acceptance/headless/live runner. Current runner is not elevated, so this real-target validation is invalid.",
            CreateMetrics());
    }

    RuntimeHost? host = null;
    var restoreInput = new WindowsInputBackend();
    var cursorStart = (X: 0, Y: 0);
    var cursorCaptured = false;
    var cursorRestored = false;
    try
    {
        var runtimeInput = new CountingInputBackend(new WindowsInputBackend());
        var diagnostics = new CountingDiagnosticsSink();
        host = new RuntimeHost(
            LiveMacroConfig(targetProcess, mappingId, xbuttonTrigger, script),
            runtimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(diagnostics),
            diagnostics);

        await host.StartAsync(CancellationToken.None);
        targetWindow.Refresh();
        if (!await LiveWindowTools.FocusAsync(targetWindow.MainWindowHandle, TimeSpan.FromSeconds(5)) ||
            !LiveWindowTools.MoveCursorToCenter(targetWindow.MainWindowHandle))
        {
            return Failed(name, watch, $"failed to focus target window and move cursor: {targetWindow.ProcessName}", CreateMetrics());
        }

        runtimeInput.Reset();
        diagnostics.Reset();
        cursorStart = await runtimeInput.GetMousePositionAsync(CancellationToken.None);
        cursorCaptured = true;
        Console.Error.WriteLine(
            $"MANUAL xbutton2-drag-minimal: Blue Archive focused. Move the cursor to a safe non-destructive area, then press '{xbuttonTrigger}' once within {options.ManualTimeout.TotalSeconds:0}s. Expect a short mapped drag using '{mappedDragButton}' as output button; structure metrics remain the primary verdict.");
        if (!await WaitUntilAsync(
                () => runtimeInput.Count("mouse", mappedDragButton, true) > 0 &&
                      runtimeInput.Count("mouse", mappedDragButton, false) > 0 &&
                      MoveEvents(runtimeInput).Count > 0,
                options.ManualTimeout,
                CancellationToken.None))
        {
            detailMetrics["trigger_received_count"] = CountHookTriggerReceived(diagnostics, triggerKey, "down") + CountHookTriggerReceived(diagnostics, triggerKey, "up");
            detailMetrics["pipeline_enqueued_count"] = CountPipelineQueued(diagnostics, triggerKey, "down");
            detailMetrics["runtime_dispatched_count"] = CountRuntimeDispatched(diagnostics, triggerKey, "down");
            detailMetrics["drag_button_down_count"] = runtimeInput.Count("mouse", mappedDragButton, true);
            detailMetrics["drag_button_up_count"] = runtimeInput.Count("mouse", mappedDragButton, false);
            detailMetrics["move_event_count"] = MoveEvents(runtimeInput).Count;
            return Failed(name, watch, "xbutton2 drag minimal did not emit the expected mapped drag sequence in real target", CreateMetrics());
        }

        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            return Failed(name, watch, "queues did not drain after real-target xbutton2 drag minimal scenario", CreateMetrics());
        }

        var manualObserved = ResolveManualConfirmation(
            options,
            "Did Blue Archive show one short, explainable cursor drag/move reaction when xbutton2 triggered the mapped drag?");
        detailMetrics["manual_drag_confirmed"] = manualObserved is true;

        var running = host.Snapshot();
        var moveEvents = MoveEvents(runtimeInput);
        var cursorEnd = await runtimeInput.GetMousePositionAsync(CancellationToken.None);
        var triggerDownReceivedCount = CountHookTriggerReceived(diagnostics, triggerKey, "down");
        var triggerUpReceivedCount = CountHookTriggerReceived(diagnostics, triggerKey, "up");
        var pipelineEnqueuedCount = CountPipelineQueued(diagnostics, triggerKey, "down");
        var runtimeDispatchedCount = CountRuntimeDispatched(diagnostics, triggerKey, "down");
        var dragButtonDownCount = runtimeInput.Count("mouse", mappedDragButton, true);
        var dragButtonUpCount = runtimeInput.Count("mouse", mappedDragButton, false);
        var moveEventCount = moveEvents.Count;
        var moveSegmentCount = moveEvents.Count;
        var (moveTotalDx, moveTotalDy) = TotalMoveDelta(moveEvents, cursorStart.X, cursorStart.Y);
        var macroStartedCount = CountMacroEvent(diagnostics, "started", $"{mappingId}:macro:");
        var macroFinishedCount = CountMacroEvent(diagnostics, "finished", $"{mappingId}:macro:");

        if (triggerDownReceivedCount == 0 ||
            pipelineEnqueuedCount != 1 ||
            runtimeDispatchedCount != 1 ||
            dragButtonDownCount != 1 ||
            dragButtonUpCount != 1 ||
            moveEventCount != 1 ||
            macroStartedCount != 1 ||
            macroFinishedCount != 1)
        {
            detailMetrics["trigger_received_count"] = triggerDownReceivedCount + triggerUpReceivedCount;
            detailMetrics["pipeline_enqueued_count"] = pipelineEnqueuedCount;
            detailMetrics["runtime_dispatched_count"] = runtimeDispatchedCount;
            detailMetrics["drag_started_count"] = dragButtonDownCount;
            detailMetrics["drag_button_down_count"] = dragButtonDownCount;
            detailMetrics["move_event_count"] = moveEventCount;
            detailMetrics["move_segment_count"] = moveSegmentCount;
            detailMetrics["drag_button_up_count"] = dragButtonUpCount;
            detailMetrics["drag_completed_count"] = Math.Min(dragButtonDownCount, dragButtonUpCount);
            detailMetrics["cursor_start_x"] = cursorStart.X;
            detailMetrics["cursor_start_y"] = cursorStart.Y;
            detailMetrics["cursor_end_x"] = cursorEnd.X;
            detailMetrics["cursor_end_y"] = cursorEnd.Y;
            detailMetrics["move_total_dx"] = moveTotalDx;
            detailMetrics["move_total_dy"] = moveTotalDy;
            return Failed(name, watch, "real-target xbutton2 drag minimal metrics did not align with expected sequence", CreateMetrics());
        }

        var eventsBeforeStop = runtimeInput.Events.Count;
        var stopWatch = Stopwatch.StartNew();
        await host.StopAsync(CancellationToken.None);
        stopWatch.Stop();
        var stopped = host.Snapshot();
        var eventsAfterStop = runtimeInput.Events.Count;
        if (!IsCleanlyStopped(stopped))
        {
            return Failed(name, watch, "runtime did not stop cleanly after real-target xbutton2 drag minimal validation", CreateMetrics());
        }

        cursorRestored = await TryRestoreCursorAsync(restoreInput, cursorStart.X, cursorStart.Y);

        var metrics = CreateMetrics();
        metrics["trigger_received_count"] = triggerDownReceivedCount + triggerUpReceivedCount;
        metrics["trigger_down_received_count"] = triggerDownReceivedCount;
        metrics["trigger_up_received_count"] = triggerUpReceivedCount;
        metrics["pipeline_enqueued_count"] = pipelineEnqueuedCount;
        metrics["runtime_dispatched_count"] = runtimeDispatchedCount;
        metrics["macro_started_count"] = macroStartedCount;
        metrics["drag_started_count"] = dragButtonDownCount;
        metrics["drag_button_down_count"] = dragButtonDownCount;
        metrics["move_event_count"] = moveEventCount;
        metrics["move_segment_count"] = moveSegmentCount;
        metrics["drag_button_up_count"] = dragButtonUpCount;
        metrics["drag_completed_count"] = Math.Min(dragButtonDownCount, dragButtonUpCount);
        metrics["macro_finished_count"] = macroFinishedCount;
        metrics["cursor_start_x"] = cursorStart.X;
        metrics["cursor_start_y"] = cursorStart.Y;
        metrics["cursor_end_x"] = cursorEnd.X;
        metrics["cursor_end_y"] = cursorEnd.Y;
        metrics["cursor_restored"] = cursorRestored;
        metrics["move_total_dx"] = moveTotalDx;
        metrics["move_total_dy"] = moveTotalDy;
        metrics["stop_requested_count"] = CountSessionEvent(diagnostics, "stop_requested");
        metrics["cleanup_completed_count"] = CountSessionEvent(diagnostics, "stop_completed");
        metrics["unexpected_output_after_stop_count"] = eventsAfterStop - eventsBeforeStop;
        metrics["stop_ms"] = stopWatch.Elapsed.TotalMilliseconds;
        metrics["diagnostic_events"] = diagnostics.Count;
        metrics["stopped_clean"] = IsCleanlyStopped(stopped);
        AddSnapshots(metrics, options, ("running", running), ("stopped", stopped));
        return Passed(name, watch, metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message, CreateMetrics());
    }
    finally
    {
        if (!cursorRestored && cursorCaptured)
        {
            _ = TryRestoreCursorAsync(restoreInput, cursorStart.X, cursorStart.Y);
        }

        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}

static async Task<AcceptanceScenarioResult> RunBlueArchiveManualXButton2MultiSegmentMoveMinimalAsync(AcceptanceOptions options)
{
    const string name = "bluearchive-manual-xbutton2-multisegment-move-minimal";
    const string triggerKey = "mouse:mouse_x2";
    const string mappingId = "xbutton2-multisegment-move-minimal";
    const string script = "setpos_rel 16 0\nwait 40\nsetpos_rel 0 12\nwait 40\nsetpos_rel -8 6";
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, $"{name} requires --allow-live-input");
    }

    var targetProcess = options.TargetProcess;
    var targetWindow = FindTargetProcessWithWindow(targetProcess);
    if (targetWindow is null)
    {
        return Failed(name, watch, $"target process with foreground-capable window was not found: {targetProcess}");
    }

    var xbuttonTrigger = KeyNameResolver.NormalizeMouseButton(options.ManualXButtonTrigger);
    if (!string.Equals(xbuttonTrigger, "mouse_x2", StringComparison.OrdinalIgnoreCase))
    {
        return Failed(name, watch, $"manual xbutton trigger for {name} must be mouse_x2, but was: {xbuttonTrigger}");
    }

    var runnerElevation = ProbeCurrentProcessElevation();
    var targetElevation = ProbeProcessElevation(targetWindow.Id, $"{targetWindow.ProcessName}.exe");
    var detailMetrics = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
    {
        ["manual_move_confirmed"] = false
    };

    Dictionary<string, object> CreateMetrics()
    {
        var metrics = new Dictionary<string, object>
        {
            ["target_process"] = targetProcess,
            ["target_process_id"] = targetWindow.Id,
            ["target_window_title"] = targetWindow.MainWindowTitle,
            ["validation_scope"] = "phase2d-bluearchive-xbutton2-multisegment-move-minimal",
            ["manual_xbutton_trigger"] = xbuttonTrigger,
            ["runner_elevation_known"] = runnerElevation.IsKnown,
            ["runner_elevated"] = runnerElevation.IsKnown && runnerElevation.IsElevated,
            ["runner_elevated_state"] = ElevationStateLabel(runnerElevation),
            ["target_elevation_known"] = targetElevation.IsKnown,
            ["target_elevated"] = targetElevation.IsKnown && targetElevation.IsElevated,
            ["target_elevated_state"] = ElevationStateLabel(targetElevation),
            ["runner_target_same_elevation"] = runnerElevation.IsKnown &&
                targetElevation.IsKnown &&
                runnerElevation.IsElevated == targetElevation.IsElevated,
            ["real_target_observed_scope"] = "multisegment_move_minimal"
        };

        foreach (var (key, value) in detailMetrics)
        {
            metrics[key] = value;
        }

        return metrics;
    }

    if (!runnerElevation.IsKnown || !runnerElevation.IsElevated)
    {
        return Failed(
            name,
            watch,
            $"{name} requires an elevated acceptance/headless/live runner. Current runner is not elevated, so this real-target validation is invalid.",
            CreateMetrics());
    }

    RuntimeHost? host = null;
    var restoreInput = new WindowsInputBackend();
    var cursorStart = (X: 0, Y: 0);
    var cursorCaptured = false;
    var cursorRestored = false;
    try
    {
        var runtimeInput = new CountingInputBackend(new WindowsInputBackend());
        var diagnostics = new CountingDiagnosticsSink();
        host = new RuntimeHost(
            LiveMacroConfig(targetProcess, mappingId, xbuttonTrigger, script),
            runtimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(diagnostics),
            diagnostics);

        await host.StartAsync(CancellationToken.None);
        targetWindow.Refresh();
        if (!await LiveWindowTools.FocusAsync(targetWindow.MainWindowHandle, TimeSpan.FromSeconds(5)) ||
            !LiveWindowTools.MoveCursorToCenter(targetWindow.MainWindowHandle))
        {
            return Failed(name, watch, $"failed to focus target window and move cursor: {targetWindow.ProcessName}", CreateMetrics());
        }

        runtimeInput.Reset();
        diagnostics.Reset();
        cursorStart = await runtimeInput.GetMousePositionAsync(CancellationToken.None);
        cursorCaptured = true;
        Console.Error.WriteLine(
            $"MANUAL xbutton2-multisegment-move-minimal: Blue Archive focused. Move the cursor to a safe non-destructive area, then press '{xbuttonTrigger}' once within {options.ManualTimeout.TotalSeconds:0}s. Expect a short multi-segment cursor move; structure metrics remain the primary verdict.");
        if (!await WaitUntilAsync(
                () => MoveEvents(runtimeInput).Count >= 3,
                options.ManualTimeout,
                CancellationToken.None))
        {
            detailMetrics["trigger_received_count"] = CountHookTriggerReceived(diagnostics, triggerKey, "down") + CountHookTriggerReceived(diagnostics, triggerKey, "up");
            detailMetrics["pipeline_enqueued_count"] = CountPipelineQueued(diagnostics, triggerKey, "down");
            detailMetrics["runtime_dispatched_count"] = CountRuntimeDispatched(diagnostics, triggerKey, "down");
            detailMetrics["move_event_count"] = MoveEvents(runtimeInput).Count;
            detailMetrics["move_segment_count"] = MoveEvents(runtimeInput).Count;
            return Failed(name, watch, "xbutton2 multisegment move did not emit all expected move segments in real target", CreateMetrics());
        }

        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            return Failed(name, watch, "queues did not drain after real-target xbutton2 multisegment move scenario", CreateMetrics());
        }

        var manualObserved = ResolveManualConfirmation(
            options,
            "Did Blue Archive show one short, explainable multi-step cursor move when xbutton2 triggered the mapped move sequence?");
        detailMetrics["manual_move_confirmed"] = manualObserved is true;

        var running = host.Snapshot();
        var moveEvents = MoveEvents(runtimeInput);
        var cursorEnd = await runtimeInput.GetMousePositionAsync(CancellationToken.None);
        var triggerDownReceivedCount = CountHookTriggerReceived(diagnostics, triggerKey, "down");
        var triggerUpReceivedCount = CountHookTriggerReceived(diagnostics, triggerKey, "up");
        var pipelineEnqueuedCount = CountPipelineQueued(diagnostics, triggerKey, "down");
        var runtimeDispatchedCount = CountRuntimeDispatched(diagnostics, triggerKey, "down");
        var moveEventCount = moveEvents.Count;
        var moveSegmentCount = moveEvents.Count;
        var (moveTotalDx, moveTotalDy) = TotalMoveDelta(moveEvents, cursorStart.X, cursorStart.Y);
        var macroStartedCount = CountMacroEvent(diagnostics, "started", $"{mappingId}:macro:");
        var macroFinishedCount = CountMacroEvent(diagnostics, "finished", $"{mappingId}:macro:");

        if (triggerDownReceivedCount == 0 ||
            pipelineEnqueuedCount != 1 ||
            runtimeDispatchedCount != 1 ||
            moveEventCount != 3 ||
            moveSegmentCount != 3 ||
            macroStartedCount != 1 ||
            macroFinishedCount != 1)
        {
            detailMetrics["trigger_received_count"] = triggerDownReceivedCount + triggerUpReceivedCount;
            detailMetrics["pipeline_enqueued_count"] = pipelineEnqueuedCount;
            detailMetrics["runtime_dispatched_count"] = runtimeDispatchedCount;
            detailMetrics["move_event_count"] = moveEventCount;
            detailMetrics["move_segment_count"] = moveSegmentCount;
            detailMetrics["cursor_start_x"] = cursorStart.X;
            detailMetrics["cursor_start_y"] = cursorStart.Y;
            detailMetrics["cursor_end_x"] = cursorEnd.X;
            detailMetrics["cursor_end_y"] = cursorEnd.Y;
            detailMetrics["move_total_dx"] = moveTotalDx;
            detailMetrics["move_total_dy"] = moveTotalDy;
            return Failed(name, watch, "real-target xbutton2 multisegment move metrics did not align with expected sequence", CreateMetrics());
        }

        var eventsBeforeStop = runtimeInput.Events.Count;
        var stopWatch = Stopwatch.StartNew();
        await host.StopAsync(CancellationToken.None);
        stopWatch.Stop();
        var stopped = host.Snapshot();
        var eventsAfterStop = runtimeInput.Events.Count;
        if (!IsCleanlyStopped(stopped))
        {
            return Failed(name, watch, "runtime did not stop cleanly after real-target xbutton2 multisegment move validation", CreateMetrics());
        }

        cursorRestored = await TryRestoreCursorAsync(restoreInput, cursorStart.X, cursorStart.Y);

        var metrics = CreateMetrics();
        metrics["trigger_received_count"] = triggerDownReceivedCount + triggerUpReceivedCount;
        metrics["trigger_down_received_count"] = triggerDownReceivedCount;
        metrics["trigger_up_received_count"] = triggerUpReceivedCount;
        metrics["pipeline_enqueued_count"] = pipelineEnqueuedCount;
        metrics["runtime_dispatched_count"] = runtimeDispatchedCount;
        metrics["macro_started_count"] = macroStartedCount;
        metrics["drag_started_count"] = 0;
        metrics["drag_button_down_count"] = 0;
        metrics["move_event_count"] = moveEventCount;
        metrics["move_segment_count"] = moveSegmentCount;
        metrics["drag_button_up_count"] = 0;
        metrics["drag_completed_count"] = 0;
        metrics["macro_finished_count"] = macroFinishedCount;
        metrics["cursor_start_x"] = cursorStart.X;
        metrics["cursor_start_y"] = cursorStart.Y;
        metrics["cursor_end_x"] = cursorEnd.X;
        metrics["cursor_end_y"] = cursorEnd.Y;
        metrics["cursor_restored"] = cursorRestored;
        metrics["move_total_dx"] = moveTotalDx;
        metrics["move_total_dy"] = moveTotalDy;
        metrics["stop_requested_count"] = CountSessionEvent(diagnostics, "stop_requested");
        metrics["cleanup_completed_count"] = CountSessionEvent(diagnostics, "stop_completed");
        metrics["unexpected_output_after_stop_count"] = eventsAfterStop - eventsBeforeStop;
        metrics["stop_ms"] = stopWatch.Elapsed.TotalMilliseconds;
        metrics["diagnostic_events"] = diagnostics.Count;
        metrics["stopped_clean"] = IsCleanlyStopped(stopped);
        AddSnapshots(metrics, options, ("running", running), ("stopped", stopped));
        return Passed(name, watch, metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message, CreateMetrics());
    }
    finally
    {
        if (!cursorRestored && cursorCaptured)
        {
            _ = TryRestoreCursorAsync(restoreInput, cursorStart.X, cursorStart.Y);
        }

        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}

static Task<AcceptanceScenarioResult> RunBlueArchiveManualXButton2DragForegroundLossDuringActiveDragAsync(AcceptanceOptions options)
{
    return RunBlueArchiveManualXButton2DragForegroundLossScenarioAsync(options, returnBeforeRelease: false);
}

static Task<AcceptanceScenarioResult> RunBlueArchiveManualXButton2DragForegroundLossThenReturnBeforeReleaseAsync(AcceptanceOptions options)
{
    return RunBlueArchiveManualXButton2DragForegroundLossScenarioAsync(options, returnBeforeRelease: true);
}

static async Task<AcceptanceScenarioResult> RunBlueArchiveManualXButton2DragForegroundLossScenarioAsync(
    AcceptanceOptions options,
    bool returnBeforeRelease)
{
    var name = returnBeforeRelease
        ? "bluearchive-manual-xbutton2-drag-foreground-loss-then-return-before-release"
        : "bluearchive-manual-xbutton2-drag-foreground-loss-during-active-drag";
    const string triggerKey = "mouse:mouse_x2";
    const string xbuttonTrigger = "mouse_x2";
    const string mappedDragButton = "mouse_middle";
    var mappingId = returnBeforeRelease
        ? "xbutton2-bluearchive-drag-foreground-loss-return-before-release"
        : "xbutton2-bluearchive-drag-foreground-loss-during-active-drag";
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, $"{name} requires --allow-live-input");
    }

    var targetProcess = options.TargetProcess;
    var targetWindow = FindTargetProcessWithWindow(targetProcess);
    if (targetWindow is null)
    {
        return Failed(name, watch, $"target process with foreground-capable window was not found: {targetProcess}");
    }

    var manualTrigger = KeyNameResolver.NormalizeMouseButton(options.ManualXButtonTrigger);
    if (!string.Equals(manualTrigger, xbuttonTrigger, StringComparison.OrdinalIgnoreCase))
    {
        return Failed(name, watch, $"manual xbutton trigger for {name} must be {xbuttonTrigger}, but was: {manualTrigger}");
    }

    var runnerElevation = ProbeCurrentProcessElevation();
    var targetElevation = ProbeProcessElevation(targetWindow.Id, $"{targetWindow.ProcessName}.exe");
    var orderedScopes = returnBeforeRelease
        ? new[]
        {
            "active_drag_before_foreground_loss",
            "foreground_cleanup",
            "foreground_return_before_release",
            "physical_release_after_foreground_return"
        }
        : new[]
        {
            "active_drag_before_foreground_loss",
            "foreground_cleanup",
            "physical_release_after_foreground_loss"
        };
    var realTargetObserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var realTargetPending = new HashSet<string>(orderedScopes, StringComparer.OrdinalIgnoreCase);
    var realTargetFailed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var detailMetrics = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
    {
        ["manual_interrupt_confirmed"] = false
    };

    static string ScopeString(IEnumerable<string> orderedSteps, IReadOnlySet<string> activeSteps) =>
        string.Join(",", orderedSteps.Where(activeSteps.Contains));

    void MarkObserved(string step)
    {
        realTargetPending.Remove(step);
        realTargetFailed.Remove(step);
        realTargetObserved.Add(step);
    }

    void MarkFailed(string step)
    {
        realTargetObserved.Remove(step);
        realTargetPending.Remove(step);
        realTargetFailed.Add(step);
    }

    Dictionary<string, object> CreateMetrics()
    {
        var metrics = new Dictionary<string, object>
        {
            ["target_process"] = targetProcess,
            ["target_process_id"] = targetWindow.Id,
            ["target_window_title"] = targetWindow.MainWindowTitle,
            ["validation_scope"] = returnBeforeRelease
                ? "phase2d-d-bluearchive-xbutton2-drag-foreground-loss-then-return-before-release"
                : "phase2d-d-bluearchive-xbutton2-drag-foreground-loss-during-active-drag",
            ["manual_xbutton_trigger"] = manualTrigger,
            ["mapped_drag_button"] = mappedDragButton,
            ["foreground_loss_observed_layer"] = "macro_executor",
            ["foreground_loss_cancellation_layer"] = "macro_executor",
            ["foreground_cleanup_path"] = "macro_executor.finally.release_owner",
            ["runner_elevation_known"] = runnerElevation.IsKnown,
            ["runner_elevated"] = runnerElevation.IsKnown && runnerElevation.IsElevated,
            ["runner_elevated_state"] = ElevationStateLabel(runnerElevation),
            ["target_elevation_known"] = targetElevation.IsKnown,
            ["target_elevated"] = targetElevation.IsKnown && targetElevation.IsElevated,
            ["target_elevated_state"] = ElevationStateLabel(targetElevation),
            ["runner_target_same_elevation"] = runnerElevation.IsKnown &&
                targetElevation.IsKnown &&
                runnerElevation.IsElevated == targetElevation.IsElevated,
            ["real_target_observed_scope"] = ScopeString(orderedScopes, realTargetObserved),
            ["real_target_pending_scope"] = ScopeString(orderedScopes, realTargetPending),
            ["real_target_failed_scope"] = ScopeString(orderedScopes, realTargetFailed)
        };

        foreach (var (key, value) in detailMetrics)
        {
            metrics[key] = value;
        }

        return metrics;
    }

    if (!runnerElevation.IsKnown || !runnerElevation.IsElevated)
    {
        return Failed(
            name,
            watch,
            $"{name} requires an elevated acceptance/headless/live runner. Current runner is not elevated, so this real-target validation is invalid.",
            CreateMetrics());
    }

    using var blocker = new LiveHarnessWindow(
        returnBeforeRelease
            ? "BA KeySmith Real Target XButton2 Drag Foreground Return Blocker"
            : "BA KeySmith Real Target XButton2 Drag Foreground Loss Blocker");
    RuntimeHost? host = null;
    var restoreInput = new WindowsInputBackend();
    var cursorStart = (X: 0, Y: 0);
    var cursorCaptured = false;
    var cursorRestored = false;
    try
    {
        blocker.Start(TimeSpan.FromSeconds(3));
        var runtimeInput = new CountingInputBackend(new WindowsInputBackend());
        var diagnostics = new CountingDiagnosticsSink();
        host = new RuntimeHost(
            LiveMacroConfig(targetProcess, mappingId, manualTrigger, InterruptibleDragScript(mappedDragButton)),
            runtimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(diagnostics),
            diagnostics);

        await host.StartAsync(CancellationToken.None);
        targetWindow.Refresh();
        if (!await LiveWindowTools.FocusAsync(targetWindow.MainWindowHandle, TimeSpan.FromSeconds(5)) ||
            !LiveWindowTools.MoveCursorToCenter(targetWindow.MainWindowHandle))
        {
            return Failed(name, watch, $"failed to focus target window and move cursor: {targetWindow.ProcessName}", CreateMetrics());
        }

        runtimeInput.Reset();
        diagnostics.Reset();
        blocker.Reset();
        cursorStart = await runtimeInput.GetMousePositionAsync(CancellationToken.None);
        cursorCaptured = true;

        Console.Error.WriteLine(
            returnBeforeRelease
                ? $"MANUAL xbutton2-drag-foreground-return: Blue Archive focused. Move the cursor to a safe non-destructive area, then press and hold '{manualTrigger}' within {options.ManualTimeout.TotalSeconds:0}s. Keep holding while blocker steals foreground, then keep holding while Blue Archive is restored, and release only after Blue Archive is focused again. Structure metrics remain the primary verdict."
                : $"MANUAL xbutton2-drag-foreground-loss: Blue Archive focused. Move the cursor to a safe non-destructive area, then press and hold '{manualTrigger}' within {options.ManualTimeout.TotalSeconds:0}s. Keep holding while blocker steals foreground, and release only after blocker stays focused. Structure metrics remain the primary verdict.");

        var barrier = await WaitForActiveDragBarrierAsync(
            host,
            runtimeInput,
            diagnostics,
            triggerKey,
            mappedDragButton,
            mappingId,
            options.ManualTimeout,
            CancellationToken.None);
        if (barrier is null)
        {
            detailMetrics["trigger_received_count"] = CountHookTriggerReceived(diagnostics, triggerKey, "down") + CountHookTriggerReceived(diagnostics, triggerKey, "up");
            detailMetrics["pipeline_enqueued_count"] = CountPipelineQueued(diagnostics, triggerKey, "down");
            detailMetrics["runtime_dispatched_count"] = CountRuntimeDispatched(diagnostics, triggerKey, "down");
            detailMetrics["drag_button_down_count"] = runtimeInput.Count("mouse", mappedDragButton, true);
            detailMetrics["move_event_before_foreground_loss_count"] = MoveEvents(runtimeInput).Count;
            MarkFailed("active_drag_before_foreground_loss");
            return Failed(name, watch, "active drag barrier was not reached in real target before foreground-loss validation", CreateMetrics());
        }

        var activeBarrier = barrier.Value;
        var interruptWhileDragActiveCount =
            activeBarrier.DragButtonDownCount > 0 &&
            activeBarrier.MoveEventCount > 0 &&
            activeBarrier.DragButtonUpCount == 0 &&
            activeBarrier.Snapshot.Runtime.Presses.OwnerKeys.Count > 0 &&
            activeBarrier.MacroFinishedCount == 0
                ? 1
                : 0;
        if (interruptWhileDragActiveCount != 1)
        {
            detailMetrics["drag_button_down_count"] = activeBarrier.DragButtonDownCount;
            detailMetrics["normal_drag_button_up_count"] = activeBarrier.DragButtonUpCount;
            detailMetrics["move_event_before_foreground_loss_count"] = activeBarrier.MoveEventCount;
            detailMetrics["interrupt_while_drag_active_count"] = interruptWhileDragActiveCount;
            MarkFailed("active_drag_before_foreground_loss");
            return Failed(name, watch, "foreground-loss precondition did not prove an active drag in real target", CreateMetrics());
        }

        MarkObserved("active_drag_before_foreground_loss");

        if (!await blocker.FocusAsync(TimeSpan.FromSeconds(5)) ||
            !LiveWindowTools.MoveCursorToCenter(blocker.Handle))
        {
            MarkFailed("foreground_cleanup");
            return Failed(name, watch, "failed to focus blocker during real-target foreground-loss drag validation", CreateMetrics());
        }

        var blockedGate = await host.CheckForegroundAsync(CancellationToken.None);
        if (blockedGate.IsAllowed)
        {
            detailMetrics["blocked_foreground_process"] = blockedGate.ForegroundProcess ?? string.Empty;
            MarkFailed("foreground_cleanup");
            return Failed(name, watch, "blocker foreground was unexpectedly allowed during real-target foreground-loss drag validation", CreateMetrics());
        }

        var cleanupBarrier = await WaitForForegroundLossCleanupBarrierAsync(
            host,
            runtimeInput,
            diagnostics,
            mappingId,
            mappedDragButton,
            activeBarrier.DragButtonUpCount,
            options.DrainTimeout,
            CancellationToken.None);
        if (cleanupBarrier is null)
        {
            detailMetrics["foreground_lost_while_drag_active_count"] = CountDiagnosticEvent(diagnostics, "macro", "foreground_lost_during_active_pointer_sequence");
            detailMetrics["mapped_owner_cleanup_completed_count"] = CountDiagnosticEvent(diagnostics, "macro", "foreground_cleanup_completed");
            detailMetrics["held_owner_count_after_foreground_cleanup"] = host.Snapshot().Runtime.Presses.OwnerKeys.Count;
            MarkFailed("foreground_cleanup");
            return Failed(name, watch, "foreground-loss cleanup barrier was not reached in real target while drag remained active", CreateMetrics());
        }

        var cleanup = cleanupBarrier.Value;
        MarkObserved("foreground_cleanup");

        if (returnBeforeRelease)
        {
            targetWindow.Refresh();
            if (!await LiveWindowTools.FocusAsync(targetWindow.MainWindowHandle, TimeSpan.FromSeconds(5)) ||
                !LiveWindowTools.MoveCursorToCenter(targetWindow.MainWindowHandle))
            {
                MarkFailed("foreground_return_before_release");
                return Failed(name, watch, $"failed to restore Blue Archive before physical release: {targetWindow.ProcessName}", CreateMetrics());
            }

            var returnedGate = await host.CheckForegroundAsync(CancellationToken.None);
            if (!returnedGate.IsAllowed)
            {
                detailMetrics["returned_foreground_process"] = returnedGate.ForegroundProcess ?? string.Empty;
                MarkFailed("foreground_return_before_release");
                return Failed(name, watch, "Blue Archive foreground did not return before release probe", CreateMetrics());
            }

            MarkObserved("foreground_return_before_release");
        }

        Console.Error.WriteLine(
            returnBeforeRelease
                ? "Blue Archive restored. Release xbutton2 now. The old drag must not resume, and no new mapped output should appear."
                : "Blocker focused. Release xbutton2 now while blocker stays foreground. The physical release should only close the captured session and must not pass through.");

        if (!await WaitUntilAsync(
                () => CountHookTriggerReceived(diagnostics, triggerKey, "up") > 0,
                options.ManualTimeout,
                CancellationToken.None))
        {
            MarkFailed(returnBeforeRelease ? "physical_release_after_foreground_return" : "physical_release_after_foreground_loss");
            detailMetrics["trigger_up_received_count"] = CountHookTriggerReceived(diagnostics, triggerKey, "up");
            return Failed(name, watch, "physical release after foreground-loss drag cleanup was not observed in real target", CreateMetrics());
        }

        await Task.Delay(150, CancellationToken.None);
        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            MarkFailed(returnBeforeRelease ? "physical_release_after_foreground_return" : "physical_release_after_foreground_loss");
            return Failed(name, watch, "queues did not drain after physical release in real-target foreground-loss drag validation", CreateMetrics());
        }

        var manualObserved = ResolveManualConfirmation(
            options,
            returnBeforeRelease
                ? "Did Blue Archive avoid resuming the old drag or producing any new drag-like output when foreground returned and you released xbutton2?"
                : "Did Blue Archive show the drag start briefly and then stop without a lingering held state after foreground switched away?");
        detailMetrics["manual_interrupt_confirmed"] = manualObserved is true;

        var running = host.Snapshot();
        var moveEvents = MoveEvents(runtimeInput);
        var cursorEnd = await runtimeInput.GetMousePositionAsync(CancellationToken.None);
        var triggerDownReceivedCount = CountHookTriggerReceived(diagnostics, triggerKey, "down");
        var triggerUpReceivedCount = CountHookTriggerReceived(diagnostics, triggerKey, "up");
        var pipelineEnqueuedCount = CountPipelineQueued(diagnostics, triggerKey, "down");
        var runtimeDispatchedCount = CountRuntimeDispatched(diagnostics, triggerKey, "down");
        var capturedSessionEnteredCount = CountHookLifecycleEvent(diagnostics, "captured_session_entered", triggerKey, "down");
        var releaseMatchedCapturedSessionCount = CountHookLifecycleEvent(diagnostics, "release_matched_captured_session", triggerKey, "up");
        var blockedPassThroughDownCount = CountHookTriggerReceived(diagnostics, triggerKey, "down", suppress: false, dispatch: false);
        var blockedPassThroughUpCount = CountHookTriggerReceived(diagnostics, triggerKey, "up", suppress: false, dispatch: false);
        var uncapturedReleasePassThroughCount = CountHookLifecycleEvent(diagnostics, "uncaptured_release_pass_through", triggerKey, "up");
        var dragButtonDownCount = activeBarrier.DragButtonDownCount;
        var normalDragButtonUpCount = activeBarrier.DragButtonUpCount;
        var dragButtonUpCount = runtimeInput.Count("mouse", mappedDragButton, false);
        var moveEventBeforeForegroundLossCount = activeBarrier.MoveEventCount;
        var foregroundLostWhileDragActiveCount = cleanup.ForegroundLostWhileDragActiveCount;
        var dragCompletedNormallyCount = activeBarrier.DragButtonUpCount;
        var dragButtonUpFromForegroundCleanupCount = Math.Max(0, cleanup.DragButtonUpTotalCount - activeBarrier.DragButtonUpCount);
        var moveEventAfterForegroundLossCount = Math.Max(0, cleanup.MoveEventTotalCount - activeBarrier.MoveEventCount);
        var unexpectedOutputAfterForegroundLossCount = Math.Max(
            0,
            (cleanup.TotalEventCount - activeBarrier.TotalEventCount) - dragButtonUpFromForegroundCleanupCount);
        var mappedOwnerCleanupCompletedCount = cleanup.MappedOwnerCleanupCompletedCount;
        var heldOwnerCountAfterForegroundCleanup = cleanup.Snapshot.Runtime.Presses.OwnerKeys.Count;
        var foregroundReturnedBeforeReleaseCount = returnBeforeRelease ? 1 : 0;
        var unexpectedOutputAfterReleaseCount = Math.Max(0, runtimeInput.Events.Count - cleanup.TotalEventCount);
        var retroactiveResumeAfterForegroundReturnCount = returnBeforeRelease
            ? Math.Max(0, moveEvents.Count - cleanup.MoveEventTotalCount) +
              Math.Max(0, runtimeInput.Count("mouse", mappedDragButton, true) - dragButtonDownCount) +
              Math.Max(0, dragButtonUpCount - cleanup.DragButtonUpTotalCount)
            : 0;
        var physicalCaptureSessionRetainedAfterForegroundLoss =
            mappedOwnerCleanupCompletedCount > 0 &&
            heldOwnerCountAfterForegroundCleanup == 0 &&
            triggerUpReceivedCount > 0 &&
            releaseMatchedCapturedSessionCount > 0 &&
            blockedPassThroughUpCount == 0 &&
            uncapturedReleasePassThroughCount == 0 &&
            blocker.XButton2UpCount == 0
                ? 1
                : 0;

        if (returnBeforeRelease)
        {
            if (capturedSessionEnteredCount == 1 &&
                foregroundLostWhileDragActiveCount == 1 &&
                dragCompletedNormallyCount == 0 &&
                dragButtonUpFromForegroundCleanupCount == 1)
            {
                MarkObserved("physical_release_after_foreground_return");
            }
            else
            {
                MarkFailed("physical_release_after_foreground_return");
            }
        }
        else
        {
            if (capturedSessionEnteredCount == 1 &&
                foregroundLostWhileDragActiveCount == 1 &&
                dragCompletedNormallyCount == 0 &&
                dragButtonUpFromForegroundCleanupCount == 1)
            {
                MarkObserved("physical_release_after_foreground_loss");
            }
            else
            {
                MarkFailed("physical_release_after_foreground_loss");
            }
        }

        cursorRestored = await TryRestoreCursorAsync(restoreInput, cursorStart.X, cursorStart.Y);

        await host.StopAsync(CancellationToken.None);
        var stopped = host.Snapshot();

        var passed =
            dragButtonDownCount == 1 &&
            moveEventBeforeForegroundLossCount >= 1 &&
            foregroundLostWhileDragActiveCount == 1 &&
            dragCompletedNormallyCount == 0 &&
            normalDragButtonUpCount == 0 &&
            dragButtonUpFromForegroundCleanupCount == 1 &&
            moveEventAfterForegroundLossCount == 0 &&
            unexpectedOutputAfterForegroundLossCount == 0 &&
            mappedOwnerCleanupCompletedCount == 1 &&
            heldOwnerCountAfterForegroundCleanup == 0 &&
            physicalCaptureSessionRetainedAfterForegroundLoss == 1 &&
            triggerUpReceivedCount == 1 &&
            releaseMatchedCapturedSessionCount == 1 &&
            blockedPassThroughUpCount == 0 &&
            blocker.XButton2UpCount == 0 &&
            uncapturedReleasePassThroughCount == 0 &&
            unexpectedOutputAfterReleaseCount == 0 &&
            retroactiveResumeAfterForegroundReturnCount == 0 &&
            IsCleanlyStopped(stopped);

        var metrics = CreateMetrics();
        metrics["trigger_received_count"] = triggerDownReceivedCount + triggerUpReceivedCount;
        metrics["trigger_down_received_count"] = triggerDownReceivedCount;
        metrics["trigger_up_received_count"] = triggerUpReceivedCount;
        metrics["pipeline_enqueued_count"] = pipelineEnqueuedCount;
        metrics["runtime_dispatched_count"] = runtimeDispatchedCount;
        metrics["drag_started_count"] = dragButtonDownCount;
        metrics["drag_button_down_count"] = dragButtonDownCount;
        metrics["normal_drag_button_up_count"] = normalDragButtonUpCount;
        metrics["drag_button_up_count"] = dragButtonUpCount;
        metrics["move_event_before_foreground_loss_count"] = moveEventBeforeForegroundLossCount;
        metrics["foreground_lost_while_drag_active_count"] = foregroundLostWhileDragActiveCount;
        metrics["drag_completed_normally_count"] = dragCompletedNormallyCount;
        metrics["drag_button_up_from_foreground_cleanup_count"] = dragButtonUpFromForegroundCleanupCount;
        metrics["move_event_after_foreground_loss_count"] = moveEventAfterForegroundLossCount;
        metrics["unexpected_output_after_foreground_loss_count"] = unexpectedOutputAfterForegroundLossCount;
        metrics["mapped_owner_cleanup_completed_count"] = mappedOwnerCleanupCompletedCount;
        metrics["cleanup_completed_count"] = mappedOwnerCleanupCompletedCount;
        metrics["held_owner_count_after_foreground_cleanup"] = heldOwnerCountAfterForegroundCleanup;
        metrics["physical_capture_session_retained_after_foreground_loss"] = physicalCaptureSessionRetainedAfterForegroundLoss;
        metrics["captured_session_entered_count"] = capturedSessionEnteredCount;
        metrics["release_matched_captured_session_count"] = releaseMatchedCapturedSessionCount;
        metrics["blocked_pass_through_down_count"] = blockedPassThroughDownCount;
        metrics["blocked_pass_through_up_count"] = blockedPassThroughUpCount;
        metrics["unexpected_output_after_release_count"] = unexpectedOutputAfterReleaseCount;
        metrics["foreground_returned_before_release_count"] = foregroundReturnedBeforeReleaseCount;
        metrics["retroactive_resume_after_foreground_return_count"] = retroactiveResumeAfterForegroundReturnCount;
        metrics["blocker_xbutton2_down_count"] = blocker.XButton2DownCount;
        metrics["blocker_xbutton2_up_count"] = blocker.XButton2UpCount;
        metrics["macro_started_count"] = activeBarrier.MacroStartedCount;
        metrics["macro_finished_count"] = cleanup.MacroFinishedCount;
        metrics["cursor_start_x"] = cursorStart.X;
        metrics["cursor_start_y"] = cursorStart.Y;
        metrics["cursor_end_x"] = cursorEnd.X;
        metrics["cursor_end_y"] = cursorEnd.Y;
        metrics["cursor_restored"] = cursorRestored;
        metrics["move_total_dx"] = cursorEnd.X - cursorStart.X;
        metrics["move_total_dy"] = cursorEnd.Y - cursorStart.Y;
        metrics["diagnostic_events"] = diagnostics.Count;
        metrics["stopped_clean"] = IsCleanlyStopped(stopped);
        AddSnapshots(
            metrics,
            options,
            ("active_drag", activeBarrier.Snapshot),
            ("after_foreground_cleanup", cleanup.Snapshot),
            ("running", running),
            ("stopped", stopped));

        return passed
            ? Passed(name, watch, metrics)
            : Failed(name, watch, "real-target foreground-loss drag metrics did not satisfy cleanup and captured-session invariants", metrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message, CreateMetrics());
    }
    finally
    {
        if (!cursorRestored && cursorCaptured)
        {
            _ = TryRestoreCursorAsync(restoreInput, cursorStart.X, cursorStart.Y);
        }

        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}

static async Task<AcceptanceScenarioResult> RunBlueArchiveManualPhase2AAsync(AcceptanceOptions options)
{
    const string name = "bluearchive-manual-phase2a";
    var watch = Stopwatch.StartNew();
    if (!options.AllowLiveInput)
    {
        return Failed(name, watch, $"{name} requires --allow-live-input");
    }

    var targetProcess = options.TargetProcess;
    var targetWindow = FindTargetProcessWithWindow(targetProcess);
    if (targetWindow is null)
    {
        return Failed(name, watch, $"target process with foreground-capable window was not found: {targetProcess}");
    }

    var keyboardTrigger = KeyNameResolver.Normalize(options.ManualKeyboardTrigger);
    var keyboardTriggerInfo = KeyNameResolver.ResolveKeyboardKey(keyboardTrigger);
    var mouseTrigger = KeyNameResolver.NormalizeMouseTrigger(options.ManualMouseTrigger);
    var reloadTrigger = KeyNameResolver.Normalize(options.ManualReloadKeyboardTrigger);
    var longMacroTrigger = KeyNameResolver.Normalize(options.ManualLongMacroTrigger);
    var longMacroOnly = options.ManualLongMacroOnly;
    var runnerElevation = ProbeCurrentProcessElevation();
    var targetElevation = ProbeProcessElevation(targetWindow.Id, $"{targetWindow.ProcessName}.exe");
    var phase2aStages = longMacroOnly
        ? ["long_macro_only"]
        : new[]
        {
            "keyboard_allowed",
            "keyboard_blocked",
            "mouse_allowed",
            "mouse_blocked",
            "reload_old_trigger_blocked",
            "reload_new_trigger_allowed",
            "disable_no_output",
            "long_macro_only"
        };
    var realTargetSteps = longMacroOnly
        ? ["long_macro_cleanup"]
        : new[]
        {
            "keyboard_allowed",
            "mouse_allowed",
            "reload_new_trigger",
            "disable_no_output",
            "long_macro_cleanup"
        };
    var harnessSteps = longMacroOnly
        ? Array.Empty<string>()
        : new[]
        {
            "keyboard_blocked_pass_through",
            "mouse_blocked_pass_through",
            "reload_old_trigger_pass_through"
        };
    var realTargetObserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var realTargetPending = new HashSet<string>(realTargetSteps, StringComparer.OrdinalIgnoreCase);
    var realTargetFailed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var harnessObserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var harnessPending = new HashSet<string>(harnessSteps, StringComparer.OrdinalIgnoreCase);
    var harnessFailed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var stepStatuses = realTargetSteps.Concat(harnessSteps)
        .ToDictionary(step => $"{step}_status", _ => (object)"pending", StringComparer.OrdinalIgnoreCase);
    var detailMetrics = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
    {
        ["manual_keyboard_confirmed"] = false,
        ["manual_mouse_confirmed"] = false,
        ["manual_reload_confirmed"] = false,
        ["manual_disable_confirmed"] = false,
        ["manual_long_macro_confirmed"] = false,
        ["phase2a_mode"] = longMacroOnly ? "long_macro_only" : "full_minimal",
        ["long_macro_failure_stage"] = "not_started",
        ["long_macro_manual_observation_reached"] = false,
        ["long_macro_trigger_received"] = false,
        ["long_macro_trigger_received_count"] = 0,
        ["long_macro_pipeline_enqueued"] = false,
        ["long_macro_pipeline_enqueued_count"] = 0,
        ["long_macro_runtime_dispatched"] = false,
        ["long_macro_runtime_dispatched_count"] = 0,
        ["long_macro_runtime_foreground_allowed"] = false,
        ["long_macro_runtime_foreground_allowed_count"] = 0,
        ["long_macro_runtime_ignored"] = false,
        ["long_macro_runtime_ignored_count"] = 0,
        ["long_macro_runtime_ignored_reasons"] = string.Empty,
        ["long_macro_macro_scheduled"] = false,
        ["long_macro_macro_scheduled_count"] = 0,
        ["long_macro_macro_started"] = false,
        ["long_macro_macro_started_count"] = 0,
        ["long_macro_escape_down_emitted"] = false,
        ["long_macro_escape_down_count"] = 0,
        ["long_macro_escape_up_emitted"] = false,
        ["long_macro_escape_up_count"] = 0,
        ["long_macro_stop_requested"] = false,
        ["long_macro_stop_requested_count"] = 0,
        ["long_macro_cleanup_completed"] = false,
        ["long_macro_cleanup_completed_count"] = 0
    };

    static string ScopeString(IEnumerable<string> orderedSteps, IReadOnlySet<string> activeSteps)
    {
        return string.Join(",", orderedSteps.Where(activeSteps.Contains));
    }

    static string StageMetricKey(string stage, string suffix) => $"stage_{stage}_{suffix}";

    void MarkObserved(string step, bool realTarget)
    {
        stepStatuses[$"{step}_status"] = "passed";
        if (realTarget)
        {
            realTargetPending.Remove(step);
            realTargetFailed.Remove(step);
            realTargetObserved.Add(step);
            return;
        }

        harnessPending.Remove(step);
        harnessFailed.Remove(step);
        harnessObserved.Add(step);
    }

    void MarkFailed(string step, bool realTarget)
    {
        stepStatuses[$"{step}_status"] = "failed";
        if (realTarget)
        {
            realTargetObserved.Remove(step);
            realTargetPending.Remove(step);
            realTargetFailed.Add(step);
            return;
        }

        harnessObserved.Remove(step);
        harnessPending.Remove(step);
        harnessFailed.Add(step);
    }

    void SetMetric(string key, object value)
    {
        detailMetrics[key] = value;
    }

    foreach (var stage in phase2aStages)
    {
        var manualRequired = stage is "keyboard_allowed" or "mouse_allowed" or "reload_new_trigger_allowed" or "long_macro_only";
        SetMetric(StageMetricKey(stage, "entered"), false);
        SetMetric(StageMetricKey(stage, "completed"), false);
        SetMetric(StageMetricKey(stage, "manual_required"), manualRequired);
        SetMetric(StageMetricKey(stage, "manual_confirmed"), false);
        SetMetric(StageMetricKey(stage, "trigger_received_count"), 0);
        SetMetric(StageMetricKey(stage, "pipeline_enqueued_count"), 0);
        SetMetric(StageMetricKey(stage, "runtime_dispatched_count"), 0);
        SetMetric(StageMetricKey(stage, "runtime_foreground_allowed_count"), 0);
        SetMetric(StageMetricKey(stage, "runtime_ignored_count"), 0);
        SetMetric(StageMetricKey(stage, "runtime_ignored_reasons"), string.Empty);
        SetMetric(StageMetricKey(stage, "macro_scheduled_count"), 0);
        SetMetric(StageMetricKey(stage, "macro_started_count"), 0);
        SetMetric(StageMetricKey(stage, "escape_down_count"), 0);
        SetMetric(StageMetricKey(stage, "escape_up_count"), 0);
        SetMetric(StageMetricKey(stage, "blocked_pass_through_count"), 0);
        SetMetric(StageMetricKey(stage, "blocked_pass_through_down"), 0);
        SetMetric(StageMetricKey(stage, "blocked_pass_through_up"), 0);
        SetMetric(StageMetricKey(stage, "output_delta"), 0);
        SetMetric(StageMetricKey(stage, "stop_requested_count"), 0);
        SetMetric(StageMetricKey(stage, "cleanup_completed_count"), 0);
        SetMetric(StageMetricKey(stage, "failure_reason"), string.Empty);
    }

    SetMetric("phase2a_stage_sequence", string.Join(",", phase2aStages));

    Dictionary<string, object> CreateMetrics()
    {
        var metrics = new Dictionary<string, object>
        {
            ["target_process"] = targetProcess,
            ["target_process_id"] = targetWindow.Id,
            ["target_window_title"] = targetWindow.MainWindowTitle,
            ["validation_scope"] = longMacroOnly ? "phase2a-long-macro-only" : "phase2a-full-sequence",
            ["manual_keyboard_trigger"] = keyboardTrigger,
            ["manual_mouse_trigger"] = mouseTrigger,
            ["manual_reload_trigger"] = reloadTrigger,
            ["manual_long_macro_trigger"] = longMacroTrigger,
            ["manual_long_macro_held_key"] = "escape",
            ["runner_elevation_known"] = runnerElevation.IsKnown,
            ["runner_elevated"] = runnerElevation.IsKnown && runnerElevation.IsElevated,
            ["runner_elevated_state"] = ElevationStateLabel(runnerElevation),
            ["target_elevation_known"] = targetElevation.IsKnown,
            ["target_elevated"] = targetElevation.IsKnown && targetElevation.IsElevated,
            ["target_elevated_state"] = ElevationStateLabel(targetElevation),
            ["runner_target_same_elevation"] = runnerElevation.IsKnown &&
                targetElevation.IsKnown &&
                runnerElevation.IsElevated == targetElevation.IsElevated,
            ["real_target_observed_scope"] = ScopeString(realTargetSteps, realTargetObserved),
            ["real_target_pending_scope"] = ScopeString(realTargetSteps, realTargetPending),
            ["real_target_failed_scope"] = ScopeString(realTargetSteps, realTargetFailed),
            ["harness_observed_scope"] = ScopeString(harnessSteps, harnessObserved),
            ["harness_pending_scope"] = ScopeString(harnessSteps, harnessPending),
            ["harness_failed_scope"] = ScopeString(harnessSteps, harnessFailed)
        };

        foreach (var (key, value) in stepStatuses)
        {
            metrics[key] = value;
        }

        foreach (var (key, value) in detailMetrics)
        {
            metrics[key] = value;
        }

        return metrics;
    }

    if (!runnerElevation.IsKnown || !runnerElevation.IsElevated)
    {
        return Failed(
            name,
            watch,
            $"{name} requires an elevated acceptance/headless/live runner. Current runner is not elevated, so this real-target validation is invalid.",
            CreateMetrics());
    }

    using var blocker = new LiveHarnessWindow("BA KeySmith Real Target Phase2A Blocker");
    RuntimeHost? host = null;
    try
    {
        var runtimeInput = new CountingInputBackend(new WindowsInputBackend());
        var diagnostics = new CountingDiagnosticsSink();

        int CountTriggerReceived(string triggerKey) =>
            diagnostics.CountWhere(evt =>
                string.Equals(evt.Source, "windows_hook", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(evt.Name, "trigger_received", StringComparison.OrdinalIgnoreCase) &&
                evt.Fields.TryGetValue("trigger", out var trigger) &&
                string.Equals(trigger, triggerKey, StringComparison.OrdinalIgnoreCase) &&
                evt.Fields.TryGetValue("phase", out var phase) &&
                string.Equals(phase, "down", StringComparison.OrdinalIgnoreCase));

        int CountPipelineEnqueued(string triggerKey) =>
            diagnostics.CountWhere(evt =>
                string.Equals(evt.Source, "trigger_pipeline", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(evt.Name, "queued", StringComparison.OrdinalIgnoreCase) &&
                evt.Fields.TryGetValue("trigger", out var trigger) &&
                string.Equals(trigger, triggerKey, StringComparison.OrdinalIgnoreCase) &&
                evt.Fields.TryGetValue("phase", out var phase) &&
                string.Equals(phase, "down", StringComparison.OrdinalIgnoreCase));

        int CountRuntimeDispatched(string triggerKey) =>
            diagnostics.CountWhere(evt =>
                string.Equals(evt.Source, "runtime", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(evt.Name, "trigger_received", StringComparison.OrdinalIgnoreCase) &&
                evt.Fields.TryGetValue("trigger", out var trigger) &&
                string.Equals(trigger, triggerKey, StringComparison.OrdinalIgnoreCase) &&
                evt.Fields.TryGetValue("phase", out var phase) &&
                string.Equals(phase, "down", StringComparison.OrdinalIgnoreCase));

        int CountRuntimeForegroundAllowed() =>
            diagnostics.CountWhere(evt =>
                string.Equals(evt.Source, "runtime", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(evt.Name, "foreground_checked", StringComparison.OrdinalIgnoreCase) &&
                evt.Fields.TryGetValue("allowed", out var allowed) &&
                string.Equals(allowed, bool.TrueString, StringComparison.OrdinalIgnoreCase));

        DiagnosticEvent[] RuntimeIgnoredEvents(string triggerKey) =>
            diagnostics.Events
                .Where(evt =>
                    string.Equals(evt.Source, "runtime", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(evt.Name, "trigger_ignored", StringComparison.OrdinalIgnoreCase) &&
                    evt.Fields.TryGetValue("trigger", out var trigger) &&
                    string.Equals(trigger, triggerKey, StringComparison.OrdinalIgnoreCase))
                .ToArray();

        int CountMacroScheduled(string mappingId) =>
            diagnostics.CountWhere(evt =>
                string.Equals(evt.Source, "queue", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(evt.Name, "action_enqueued", StringComparison.OrdinalIgnoreCase) &&
                evt.Fields.TryGetValue("name", out var queueName) &&
                string.Equals(queueName, mappingId, StringComparison.OrdinalIgnoreCase));

        int CountMacroStarted(string mappingId) =>
            diagnostics.CountWhere(evt =>
                string.Equals(evt.Source, "macro", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(evt.Name, "started", StringComparison.OrdinalIgnoreCase) &&
                evt.Fields.TryGetValue("owner", out var owner) &&
                owner.StartsWith($"{mappingId}:macro:", StringComparison.OrdinalIgnoreCase));

        int CountStopRequested() =>
            diagnostics.CountWhere(evt =>
                string.Equals(evt.Source, "session", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(evt.Name, "stop_requested", StringComparison.OrdinalIgnoreCase));

        int CountStopCompleted() =>
            diagnostics.CountWhere(evt =>
                string.Equals(evt.Source, "session", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(evt.Name, "stop_completed", StringComparison.OrdinalIgnoreCase));

        (string Name, string? TriggerKey, string? MappingId, int? BlockerVirtualKey, bool TrackMousePassThrough) BeginStage(
            string stageName,
            string? triggerKey = null,
            string? mappingId = null,
            int? blockerVirtualKey = null,
            bool trackMousePassThrough = false)
        {
            runtimeInput.Reset();
            diagnostics.Reset();
            blocker.Reset();
            SetMetric(StageMetricKey(stageName, "entered"), true);
            SetMetric(StageMetricKey(stageName, "completed"), false);
            SetMetric(StageMetricKey(stageName, "manual_confirmed"), false);
            SetMetric(StageMetricKey(stageName, "failure_reason"), string.Empty);
            return (stageName, triggerKey, mappingId, blockerVirtualKey, trackMousePassThrough);
        }

        void RecordStage(
            (string Name, string? TriggerKey, string? MappingId, int? BlockerVirtualKey, bool TrackMousePassThrough) stage,
            RuntimeHostSnapshot? snapshot,
            bool completed,
            bool manualConfirmed,
            string? failureReason = null)
        {
            var triggerReceivedCount = string.IsNullOrWhiteSpace(stage.TriggerKey) ? 0 : CountTriggerReceived(stage.TriggerKey);
            var pipelineEnqueuedCount = string.IsNullOrWhiteSpace(stage.TriggerKey) ? 0 : CountPipelineEnqueued(stage.TriggerKey);
            var runtimeDispatchedCount = string.IsNullOrWhiteSpace(stage.TriggerKey) ? 0 : CountRuntimeDispatched(stage.TriggerKey);
            var runtimeForegroundAllowedCount = CountRuntimeForegroundAllowed();
            var runtimeIgnoredEvents = string.IsNullOrWhiteSpace(stage.TriggerKey)
                ? Array.Empty<DiagnosticEvent>()
                : RuntimeIgnoredEvents(stage.TriggerKey);
            var macroScheduledCount = string.IsNullOrWhiteSpace(stage.MappingId) ? 0 : CountMacroScheduled(stage.MappingId);
            var macroStartedCount = string.IsNullOrWhiteSpace(stage.MappingId) ? 0 : CountMacroStarted(stage.MappingId);
            var escapeDownCount = runtimeInput.Count("key", "escape", true);
            var escapeUpCount = runtimeInput.Count("key", "escape", false);
            var outputDelta = runtimeInput.Count("key", "escape");
            var stopRequestedCount = CountStopRequested();
            var stopCompletedCount = CountStopCompleted();
            var blockedPassThroughDown = stage.TrackMousePassThrough ? (int)blocker.MouseDownCount : 0;
            var blockedPassThroughUp = stage.TrackMousePassThrough ? (int)blocker.MouseUpCount : 0;
            var blockedPassThroughCount = stage.TrackMousePassThrough
                ? Math.Min(blockedPassThroughDown, blockedPassThroughUp)
                : stage.BlockerVirtualKey is int virtualKey
                    ? (int)blocker.KeyDownCountFor(virtualKey)
                    : 0;

            SetMetric(StageMetricKey(stage.Name, "completed"), completed);
            SetMetric(StageMetricKey(stage.Name, "manual_confirmed"), manualConfirmed);
            SetMetric(StageMetricKey(stage.Name, "trigger_received_count"), triggerReceivedCount);
            SetMetric(StageMetricKey(stage.Name, "pipeline_enqueued_count"), pipelineEnqueuedCount);
            SetMetric(StageMetricKey(stage.Name, "runtime_dispatched_count"), runtimeDispatchedCount);
            SetMetric(StageMetricKey(stage.Name, "runtime_foreground_allowed_count"), runtimeForegroundAllowedCount);
            SetMetric(StageMetricKey(stage.Name, "runtime_ignored_count"), runtimeIgnoredEvents.Length);
            SetMetric(
                StageMetricKey(stage.Name, "runtime_ignored_reasons"),
                string.Join(",",
                    runtimeIgnoredEvents
                        .Select(evt => evt.Fields.TryGetValue("reason", out var reason) ? reason : string.Empty)
                        .Where(reason => !string.IsNullOrWhiteSpace(reason))
                        .Distinct(StringComparer.OrdinalIgnoreCase)));
            SetMetric(StageMetricKey(stage.Name, "macro_scheduled_count"), macroScheduledCount);
            SetMetric(StageMetricKey(stage.Name, "macro_started_count"), macroStartedCount);
            SetMetric(StageMetricKey(stage.Name, "escape_down_count"), escapeDownCount);
            SetMetric(StageMetricKey(stage.Name, "escape_up_count"), escapeUpCount);
            SetMetric(StageMetricKey(stage.Name, "blocked_pass_through_count"), blockedPassThroughCount);
            SetMetric(StageMetricKey(stage.Name, "blocked_pass_through_down"), blockedPassThroughDown);
            SetMetric(StageMetricKey(stage.Name, "blocked_pass_through_up"), blockedPassThroughUp);
            SetMetric(StageMetricKey(stage.Name, "output_delta"), outputDelta);
            SetMetric(StageMetricKey(stage.Name, "stop_requested_count"), stopRequestedCount);
            SetMetric(
                StageMetricKey(stage.Name, "cleanup_completed_count"),
                stopCompletedCount > 0 && snapshot is not null && IsCleanlyStopped(snapshot) ? stopCompletedCount : 0);
            SetMetric(StageMetricKey(stage.Name, "failure_reason"), failureReason ?? string.Empty);
        }

        void SyncLongMacroStageMetrics(
            string mappingId,
            string failureStage,
            RuntimeHostSnapshot? snapshot,
            bool manualObservationReached,
            bool manualObservationConfirmed)
        {
            var longMacroStage = ("long_macro_only", $"keyboard:{longMacroTrigger}", mappingId, (int?)null, false);
            RecordStage(longMacroStage, snapshot, string.Equals(failureStage, "passed", StringComparison.OrdinalIgnoreCase), manualObservationConfirmed, string.Equals(failureStage, "passed", StringComparison.OrdinalIgnoreCase) ? null : failureStage);
            var triggerReceivedCount = CountTriggerReceived($"keyboard:{longMacroTrigger}");
            var pipelineEnqueuedCount = CountPipelineEnqueued($"keyboard:{longMacroTrigger}");
            var runtimeDispatchedCount = CountRuntimeDispatched($"keyboard:{longMacroTrigger}");
            var runtimeForegroundAllowedCount = CountRuntimeForegroundAllowed();
            var runtimeIgnoredEvents = RuntimeIgnoredEvents($"keyboard:{longMacroTrigger}");
            var macroScheduledCount = CountMacroScheduled(mappingId);
            var macroStartedCount = CountMacroStarted(mappingId);

            SetMetric("long_macro_failure_stage", failureStage);
            SetMetric("long_macro_manual_observation_reached", manualObservationReached);
            SetMetric("manual_long_macro_confirmed", manualObservationConfirmed);
            SetMetric("long_macro_trigger_received", triggerReceivedCount > 0);
            SetMetric("long_macro_trigger_received_count", triggerReceivedCount);
            SetMetric("long_macro_pipeline_enqueued", pipelineEnqueuedCount > 0);
            SetMetric("long_macro_pipeline_enqueued_count", pipelineEnqueuedCount);
            SetMetric("long_macro_runtime_dispatched", runtimeDispatchedCount > 0);
            SetMetric("long_macro_runtime_dispatched_count", runtimeDispatchedCount);
            SetMetric("long_macro_runtime_foreground_allowed", runtimeForegroundAllowedCount > 0);
            SetMetric("long_macro_runtime_foreground_allowed_count", runtimeForegroundAllowedCount);
            SetMetric("long_macro_runtime_ignored", runtimeIgnoredEvents.Length > 0);
            SetMetric("long_macro_runtime_ignored_count", runtimeIgnoredEvents.Length);
            SetMetric(
                "long_macro_runtime_ignored_reasons",
                string.Join(",",
                    runtimeIgnoredEvents
                        .Select(evt => evt.Fields.TryGetValue("reason", out var reason) ? reason : string.Empty)
                        .Where(reason => !string.IsNullOrWhiteSpace(reason))
                        .Distinct(StringComparer.OrdinalIgnoreCase)));
            SetMetric("long_macro_macro_scheduled", macroScheduledCount > 0);
            SetMetric("long_macro_macro_scheduled_count", macroScheduledCount);
            SetMetric("long_macro_macro_started", macroStartedCount > 0);
            SetMetric("long_macro_macro_started_count", macroStartedCount);
            SetMetric("long_macro_escape_down_emitted", runtimeInput.Count("key", "escape", true) > 0);
            SetMetric("long_macro_escape_down_count", runtimeInput.Count("key", "escape", true));
            SetMetric("long_macro_escape_up_emitted", runtimeInput.Count("key", "escape", false) > 0);
            SetMetric("long_macro_escape_up_count", runtimeInput.Count("key", "escape", false));
            SetMetric("long_macro_stop_requested", CountStopRequested() > 0);
            SetMetric("long_macro_stop_requested_count", CountStopRequested());
            SetMetric("long_macro_cleanup_completed", CountStopCompleted() > 0 && snapshot is not null && IsCleanlyStopped(snapshot));
            SetMetric("long_macro_cleanup_completed_count", CountStopCompleted());
            SetMetric("long_macro_pipeline_pending", snapshot?.Pipeline.PendingCount ?? host?.Snapshot().Pipeline.PendingCount ?? 0);
            SetMetric("long_macro_runtime_pending_actions", snapshot?.Runtime.PendingActionCount ?? host?.Snapshot().Runtime.PendingActionCount ?? 0);
            SetMetric("long_macro_runtime_running_actions", snapshot?.Runtime.RunningActionCount ?? host?.Snapshot().Runtime.RunningActionCount ?? 0);
            SetMetric("long_macro_runtime_active_workers", snapshot?.Runtime.ActiveWorkerCount ?? host?.Snapshot().Runtime.ActiveWorkerCount ?? 0);
        }

        host = new RuntimeHost(
            longMacroOnly
                ? LiveLongMacroConfig(targetProcess, longMacroTrigger, "escape")
                : LiveEscTapConfig(targetProcess, keyboardTrigger),
            runtimeInput,
            new WindowsForegroundGate(),
            new WindowsHookTriggerSource(diagnostics),
            diagnostics);

        await host.StartAsync(CancellationToken.None);
        if (!longMacroOnly)
        {
            blocker.Start(TimeSpan.FromSeconds(3));
        }
        targetWindow.Refresh();
        if (!await LiveWindowTools.FocusAsync(targetWindow.MainWindowHandle, TimeSpan.FromSeconds(5)))
        {
            return Failed(name, watch, $"failed to focus target window: {targetWindow.ProcessName}", CreateMetrics());
        }

        var allowedGate = await host.CheckForegroundAsync(CancellationToken.None);
        if (!allowedGate.IsAllowed)
        {
            var gateMetrics = CreateMetrics();
            gateMetrics["foreground_process"] = allowedGate.ForegroundProcess ?? string.Empty;
            return Failed(
                name,
                watch,
                $"target foreground gate was not allowed: foreground={allowedGate.ForegroundProcess}",
                gateMetrics);
        }

        if (longMacroOnly)
        {
            var beforeLongMacroStopOnly = host.Snapshot();
            var longMacroTriggerKeyOnly = $"keyboard:{longMacroTrigger}";
            var longMacroMappingIdOnly = beforeLongMacroStopOnly.RuntimeConfig.Mappings
                .Select(mapping => mapping.NormalizedId)
                .SingleOrDefault() ?? $"mapping:{longMacroTriggerKeyOnly}";

            runtimeInput.Reset();
            diagnostics.Reset();

            var longMacroEscapeDownBeforeOnly = runtimeInput.Count("key", "escape", true);
            var longMacroEscapeUpBeforeOnly = runtimeInput.Count("key", "escape", false);

            Action<string, RuntimeHostSnapshot?, bool, bool> setLongMacroTraceOnly = (
                failureStage,
                snapshot,
                manualObservationReached,
                manualObservationConfirmed) =>
            {
                var currentSnapshot = snapshot ?? host?.Snapshot() ?? beforeLongMacroStopOnly;
                var triggerReceivedCount = diagnostics.CountWhere(evt =>
                    string.Equals(evt.Source, "windows_hook", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(evt.Name, "trigger_received", StringComparison.OrdinalIgnoreCase) &&
                    evt.Fields.TryGetValue("trigger", out var trigger) &&
                    string.Equals(trigger, longMacroTriggerKeyOnly, StringComparison.OrdinalIgnoreCase) &&
                    evt.Fields.TryGetValue("phase", out var phase) &&
                    string.Equals(phase, "down", StringComparison.OrdinalIgnoreCase));
                var pipelineEnqueuedCount = diagnostics.CountWhere(evt =>
                    string.Equals(evt.Source, "trigger_pipeline", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(evt.Name, "queued", StringComparison.OrdinalIgnoreCase) &&
                    evt.Fields.TryGetValue("trigger", out var trigger) &&
                    string.Equals(trigger, longMacroTriggerKeyOnly, StringComparison.OrdinalIgnoreCase) &&
                    evt.Fields.TryGetValue("phase", out var phase) &&
                    string.Equals(phase, "down", StringComparison.OrdinalIgnoreCase));
                var runtimeDispatchedCount = diagnostics.CountWhere(evt =>
                    string.Equals(evt.Source, "runtime", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(evt.Name, "trigger_received", StringComparison.OrdinalIgnoreCase) &&
                    evt.Fields.TryGetValue("trigger", out var trigger) &&
                    string.Equals(trigger, longMacroTriggerKeyOnly, StringComparison.OrdinalIgnoreCase) &&
                    evt.Fields.TryGetValue("phase", out var phase) &&
                    string.Equals(phase, "down", StringComparison.OrdinalIgnoreCase));
                var runtimeForegroundAllowedCount = diagnostics.CountWhere(evt =>
                    string.Equals(evt.Source, "runtime", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(evt.Name, "foreground_checked", StringComparison.OrdinalIgnoreCase) &&
                    evt.Fields.TryGetValue("allowed", out var allowed) &&
                    string.Equals(allowed, bool.TrueString, StringComparison.OrdinalIgnoreCase));
                var runtimeIgnoredEvents = diagnostics.Events
                    .Where(evt =>
                        string.Equals(evt.Source, "runtime", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(evt.Name, "trigger_ignored", StringComparison.OrdinalIgnoreCase) &&
                        evt.Fields.TryGetValue("trigger", out var trigger) &&
                        string.Equals(trigger, longMacroTriggerKeyOnly, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                var macroScheduledCount = diagnostics.CountWhere(evt =>
                    string.Equals(evt.Source, "queue", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(evt.Name, "action_enqueued", StringComparison.OrdinalIgnoreCase) &&
                    evt.Fields.TryGetValue("name", out var queueName) &&
                    string.Equals(queueName, longMacroMappingIdOnly, StringComparison.OrdinalIgnoreCase));
                var macroStartedCount = diagnostics.CountWhere(evt =>
                    string.Equals(evt.Source, "macro", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(evt.Name, "started", StringComparison.OrdinalIgnoreCase) &&
                    evt.Fields.TryGetValue("owner", out var owner) &&
                    owner.StartsWith($"{longMacroMappingIdOnly}:macro:", StringComparison.OrdinalIgnoreCase));
                var stopRequestedCount = diagnostics.CountWhere(evt =>
                    string.Equals(evt.Source, "session", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(evt.Name, "stop_requested", StringComparison.OrdinalIgnoreCase));
                var stopCompletedCount = diagnostics.CountWhere(evt =>
                    string.Equals(evt.Source, "session", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(evt.Name, "stop_completed", StringComparison.OrdinalIgnoreCase));
                var escapeDownCount = runtimeInput.Count("key", "escape", true) - longMacroEscapeDownBeforeOnly;
                var escapeUpCount = runtimeInput.Count("key", "escape", false) - longMacroEscapeUpBeforeOnly;

                SetMetric("long_macro_failure_stage", failureStage);
                SetMetric("long_macro_manual_observation_reached", manualObservationReached);
                SetMetric("manual_long_macro_confirmed", manualObservationConfirmed);
                SetMetric("long_macro_trigger_received", triggerReceivedCount > 0);
                SetMetric("long_macro_trigger_received_count", triggerReceivedCount);
                SetMetric("long_macro_pipeline_enqueued", pipelineEnqueuedCount > 0);
                SetMetric("long_macro_pipeline_enqueued_count", pipelineEnqueuedCount);
                SetMetric("long_macro_runtime_dispatched", runtimeDispatchedCount > 0);
                SetMetric("long_macro_runtime_dispatched_count", runtimeDispatchedCount);
                SetMetric("long_macro_runtime_foreground_allowed", runtimeForegroundAllowedCount > 0);
                SetMetric("long_macro_runtime_foreground_allowed_count", runtimeForegroundAllowedCount);
                SetMetric("long_macro_runtime_ignored", runtimeIgnoredEvents.Length > 0);
                SetMetric("long_macro_runtime_ignored_count", runtimeIgnoredEvents.Length);
                SetMetric(
                    "long_macro_runtime_ignored_reasons",
                    string.Join(",",
                        runtimeIgnoredEvents
                            .Select(evt => evt.Fields.TryGetValue("reason", out var reason) ? reason : string.Empty)
                            .Where(reason => !string.IsNullOrWhiteSpace(reason))
                            .Distinct(StringComparer.OrdinalIgnoreCase)));
                SetMetric("long_macro_macro_scheduled", macroScheduledCount > 0);
                SetMetric("long_macro_macro_scheduled_count", macroScheduledCount);
                SetMetric("long_macro_macro_started", macroStartedCount > 0);
                SetMetric("long_macro_macro_started_count", macroStartedCount);
                SetMetric("long_macro_escape_down_emitted", escapeDownCount > 0);
                SetMetric("long_macro_escape_down_count", escapeDownCount);
                SetMetric("long_macro_escape_up_emitted", escapeUpCount > 0);
                SetMetric("long_macro_escape_up_count", escapeUpCount);
                SetMetric("long_macro_stop_requested", stopRequestedCount > 0);
                SetMetric("long_macro_stop_requested_count", stopRequestedCount);
                SetMetric("long_macro_cleanup_completed", stopCompletedCount > 0 && IsCleanlyStopped(currentSnapshot));
                SetMetric("long_macro_cleanup_completed_count", stopCompletedCount);
                SetMetric("long_macro_pipeline_pending", currentSnapshot.Pipeline.PendingCount);
                SetMetric("long_macro_runtime_pending_actions", currentSnapshot.Runtime.PendingActionCount);
                SetMetric("long_macro_runtime_running_actions", currentSnapshot.Runtime.RunningActionCount);
                SetMetric("long_macro_runtime_active_workers", currentSnapshot.Runtime.ActiveWorkerCount);
            };

            Console.Error.WriteLine(
                $"MANUAL phase2a long-macro-only: Blue Archive focused. Press '{longMacroTrigger}' once within {options.ManualTimeout.TotalSeconds:0}s. Expect the long macro to begin.");
            if (!await WaitUntilAsync(
                    () => runtimeInput.Count("key", "escape", true) > longMacroEscapeDownBeforeOnly,
                    options.ManualTimeout,
                    CancellationToken.None))
            {
                MarkFailed("long_macro_cleanup", realTarget: true);
                setLongMacroTraceOnly("awaiting_escape_down", null, false, false);
                var longMacroOnlyMetrics = CreateMetrics();
                longMacroOnlyMetrics["long_macro_escape_down"] = runtimeInput.Count("key", "escape", true) - longMacroEscapeDownBeforeOnly;
                longMacroOnlyMetrics["long_macro_escape_up"] = runtimeInput.Count("key", "escape", false) - longMacroEscapeUpBeforeOnly;
                return Failed(name, watch, "phase2a long macro did not press escape before stop", longMacroOnlyMetrics);
            }

            var longMacroObservedOnly = ResolveManualConfirmation(
                options,
                $"Did Blue Archive react once when the isolated long macro trigger '{longMacroTrigger}' started?");
            if (longMacroObservedOnly is null)
            {
                MarkFailed("long_macro_cleanup", realTarget: true);
                setLongMacroTraceOnly("awaiting_manual_confirmation", null, true, false);
                return Failed(name, watch, "phase2a requires manual confirmation for the isolated long macro observation.", CreateMetrics());
            }

            if (longMacroObservedOnly is not true)
            {
                MarkFailed("long_macro_cleanup", realTarget: true);
                setLongMacroTraceOnly("manual_confirmation_negative", null, true, false);
                return Failed(name, watch, "manual observation did not confirm isolated long macro start behavior in real target", CreateMetrics());
            }

            var eventsBeforeStopOnly = runtimeInput.Events.Count;
            var stopWatchOnly = Stopwatch.StartNew();
            await host.StopAsync(CancellationToken.None);
            stopWatchOnly.Stop();
            var stoppedOnly = host.Snapshot();
            setLongMacroTraceOnly("after_stop_requested", stoppedOnly, true, true);
            var eventsAfterStopOnly = runtimeInput.Events.Count;
            await Task.Delay(150, CancellationToken.None);
            if (runtimeInput.Events.Count != eventsAfterStopOnly)
            {
                MarkFailed("long_macro_cleanup", realTarget: true);
                setLongMacroTraceOnly("post_stop_input_detected", stoppedOnly, true, true);
                return Failed(name, watch, "phase2a emitted input after stop returned", CreateMetrics());
            }

            var longMacroEscapeDownOnly = runtimeInput.Count("key", "escape", true) - longMacroEscapeDownBeforeOnly;
            var longMacroEscapeUpOnly = runtimeInput.Count("key", "escape", false) - longMacroEscapeUpBeforeOnly;
            if (longMacroEscapeDownOnly != 1 || longMacroEscapeUpOnly != 1)
            {
                MarkFailed("long_macro_cleanup", realTarget: true);
                setLongMacroTraceOnly("cleanup_validation_failed", stoppedOnly, true, true);
                var longMacroCleanupMetrics = CreateMetrics();
                longMacroCleanupMetrics["long_macro_escape_down"] = longMacroEscapeDownOnly;
                longMacroCleanupMetrics["long_macro_escape_up"] = longMacroEscapeUpOnly;
                return Failed(name, watch, "phase2a long macro cleanup did not release escape exactly once", longMacroCleanupMetrics);
            }

            if (!IsCleanlyStopped(stoppedOnly))
            {
                MarkFailed("long_macro_cleanup", realTarget: true);
                setLongMacroTraceOnly("stop_not_clean", stoppedOnly, true, true);
                return Failed(name, watch, "phase2a runtime did not stop cleanly after isolated long macro validation", CreateMetrics());
            }

            MarkObserved("long_macro_cleanup", realTarget: true);
            SetMetric("long_macro_escape_down", longMacroEscapeDownOnly);
            SetMetric("long_macro_escape_up", longMacroEscapeUpOnly);
            setLongMacroTraceOnly("passed", stoppedOnly, true, true);

            await host.DisposeAsync();
            host = null;

            var finalMetricsOnly = CreateMetrics();
            finalMetricsOnly["foreground_process"] = allowedGate.ForegroundProcess ?? string.Empty;
            finalMetricsOnly["post_stop_events"] = runtimeInput.Events.Count - eventsAfterStopOnly;
            finalMetricsOnly["stop_ms"] = stopWatchOnly.Elapsed.TotalMilliseconds;
            finalMetricsOnly["diagnostic_events"] = diagnostics.Count;
            finalMetricsOnly["stopped_clean"] = IsCleanlyStopped(stoppedOnly);
            AddSnapshots(
                finalMetricsOnly,
                options,
                ("before_long_macro_stop", beforeLongMacroStopOnly),
                ("stopped", stoppedOnly));
            return Passed(name, watch, finalMetricsOnly);
        }

        var keyboardAllowedStage = BeginStage(
            "keyboard_allowed",
            triggerKey: $"keyboard:{keyboardTrigger}");

        Console.Error.WriteLine(
            $"MANUAL phase2a keyboard-allowed: Blue Archive focused. Press '{keyboardTrigger}' once within {options.ManualTimeout.TotalSeconds:0}s. Expect mapped 'escape' effect only.");
        if (!await WaitForInputCountAsync(runtimeInput, "key", "escape", 2, options.ManualTimeout))
        {
            var failed = host.Snapshot();
            MarkFailed("keyboard_allowed", realTarget: true);
            RecordStage(keyboardAllowedStage, failed, completed: false, manualConfirmed: false, failureReason: "awaiting_escape_output");
            var keyboardAllowedMetrics = CreateMetrics();
            keyboardAllowedMetrics["foreground_process"] = allowedGate.ForegroundProcess ?? string.Empty;
            keyboardAllowedMetrics["keyboard_escape_events"] = runtimeInput.Count("key", "escape");
            keyboardAllowedMetrics["pipeline_queued"] = failed.Pipeline.QueuedCount;
            keyboardAllowedMetrics["pipeline_handled"] = failed.Pipeline.HandledCount;
            keyboardAllowedMetrics["pipeline_pending"] = failed.Pipeline.PendingCount;
            return Failed(name, watch, "phase2a keyboard trigger did not produce escape output in target foreground", keyboardAllowedMetrics);
        }

        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            RecordStage(keyboardAllowedStage, host.Snapshot(), completed: false, manualConfirmed: false, failureReason: "drain_timeout");
            return Failed(name, watch, "queues did not drain after phase2a keyboard trigger", CreateMetrics());
        }

        var keyboardEscapeEvents = runtimeInput.Count("key", "escape");
        var keyboardObserved = ResolveManualConfirmation(
            options,
            $"Did Blue Archive react to '{keyboardTrigger}' with only the mapped 'escape' effect?");
        if (keyboardObserved is null)
        {
            MarkFailed("keyboard_allowed", realTarget: true);
            RecordStage(keyboardAllowedStage, host.Snapshot(), completed: false, manualConfirmed: false, failureReason: "awaiting_manual_confirmation");
            var keyboardConfirmMetrics = CreateMetrics();
            keyboardConfirmMetrics["keyboard_escape_events"] = keyboardEscapeEvents;
            return Failed(name, watch, "phase2a requires manual confirmation for the keyboard real-target observation.", keyboardConfirmMetrics);
        }

        if (keyboardObserved is not true)
        {
            MarkFailed("keyboard_allowed", realTarget: true);
            RecordStage(keyboardAllowedStage, host.Snapshot(), completed: false, manualConfirmed: false, failureReason: "manual_confirmation_negative");
            var keyboardObservedMetrics = CreateMetrics();
            keyboardObservedMetrics["keyboard_escape_events"] = keyboardEscapeEvents;
            return Failed(name, watch, "manual observation did not confirm keyboard trigger behavior in real target", keyboardObservedMetrics);
        }

        MarkObserved("keyboard_allowed", realTarget: true);
        SetMetric("keyboard_escape_events", keyboardEscapeEvents);
        SetMetric("manual_keyboard_confirmed", true);
        RecordStage(keyboardAllowedStage, host.Snapshot(), completed: true, manualConfirmed: true);

        if (!await blocker.FocusAsync(TimeSpan.FromSeconds(5)))
        {
            return Failed(name, watch, "failed to focus blocker for keyboard blocked pass-through", CreateMetrics());
        }

        var blockedGate = await host.CheckForegroundAsync(CancellationToken.None);
        if (blockedGate.IsAllowed)
        {
            var blockedGateMetrics = CreateMetrics();
            blockedGateMetrics["blocked_foreground_process"] = blockedGate.ForegroundProcess ?? string.Empty;
            return Failed(name, watch, "blocker foreground was unexpectedly allowed during keyboard blocked pass-through", blockedGateMetrics);
        }

        var keyboardBlockedStage = BeginStage(
            "keyboard_blocked",
            triggerKey: $"keyboard:{keyboardTrigger}",
            blockerVirtualKey: keyboardTriggerInfo.VirtualKey);
        Console.Error.WriteLine(
            $"MANUAL phase2a keyboard-blocked: blocker focused. Press '{keyboardTrigger}' once within {options.ManualTimeout.TotalSeconds:0}s. Expect raw key to reach blocker and no mapped output.");
        if (!await WaitUntilAsync(
                () => blocker.KeyDownCountFor(keyboardTriggerInfo.VirtualKey) > 0,
                options.ManualTimeout,
                CancellationToken.None))
        {
            MarkFailed("keyboard_blocked_pass_through", realTarget: false);
            RecordStage(keyboardBlockedStage, host.Snapshot(), completed: false, manualConfirmed: false, failureReason: "awaiting_blocked_pass_through");
            var keyboardBlockedMetrics = CreateMetrics();
            keyboardBlockedMetrics["blocked_foreground_process"] = blockedGate.ForegroundProcess ?? string.Empty;
            return Failed(name, watch, "phase2a keyboard blocked pass-through was not observed in blocker", keyboardBlockedMetrics);
        }

        await Task.Delay(150);
        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            RecordStage(keyboardBlockedStage, host.Snapshot(), completed: false, manualConfirmed: false, failureReason: "drain_timeout");
            return Failed(name, watch, "queues did not drain after phase2a keyboard blocked probe", CreateMetrics());
        }

        var keyboardBlockedOutputDelta = runtimeInput.Count("key", "escape");
        var keyboardBlockedPassThrough = blocker.KeyDownCountFor(keyboardTriggerInfo.VirtualKey);
        if (keyboardBlockedOutputDelta != 0)
        {
            MarkFailed("keyboard_blocked_pass_through", realTarget: false);
            RecordStage(keyboardBlockedStage, host.Snapshot(), completed: false, manualConfirmed: false, failureReason: "unexpected_mapped_output");
            var keyboardBlockedOutputMetrics = CreateMetrics();
            keyboardBlockedOutputMetrics["keyboard_blocked_output_delta"] = keyboardBlockedOutputDelta;
            return Failed(name, watch, "phase2a keyboard blocked probe produced mapped output", keyboardBlockedOutputMetrics);
        }

        if (keyboardBlockedPassThrough == 0)
        {
            MarkFailed("keyboard_blocked_pass_through", realTarget: false);
            RecordStage(keyboardBlockedStage, host.Snapshot(), completed: false, manualConfirmed: false, failureReason: "blocked_pass_through_missing");
            var keyboardBlockedPassThroughMetrics = CreateMetrics();
            keyboardBlockedPassThroughMetrics["keyboard_blocked_pass_through"] = keyboardBlockedPassThrough;
            return Failed(name, watch, "phase2a keyboard blocked probe did not pass through to blocker", keyboardBlockedPassThroughMetrics);
        }

        MarkObserved("keyboard_blocked_pass_through", realTarget: false);
        SetMetric("keyboard_blocked_output_delta", keyboardBlockedOutputDelta);
        SetMetric("keyboard_blocked_pass_through", keyboardBlockedPassThrough);
        RecordStage(keyboardBlockedStage, host.Snapshot(), completed: true, manualConfirmed: false);

        await host.ReloadAsync(LiveEscTapConfig(targetProcess, mouseTrigger), CancellationToken.None);
        var afterMouseReload = host.Snapshot();
        targetWindow.Refresh();
        if (!await LiveWindowTools.FocusAsync(targetWindow.MainWindowHandle, TimeSpan.FromSeconds(5)))
        {
            return Failed(name, watch, "failed to refocus Blue Archive for mouse real-target validation", CreateMetrics());
        }

        var mouseAllowedStage = BeginStage(
            "mouse_allowed",
            triggerKey: $"mouse:{mouseTrigger}");
        Console.Error.WriteLine(
            $"MANUAL phase2a mouse-allowed: Blue Archive focused. Move the cursor over a safe non-destructive area, then click '{mouseTrigger}' once within {options.ManualTimeout.TotalSeconds:0}s. Expect mapped 'escape' effect only.");
        if (!await WaitForInputCountAsync(runtimeInput, "key", "escape", 2, options.ManualTimeout))
        {
            MarkFailed("mouse_allowed", realTarget: true);
            RecordStage(mouseAllowedStage, host.Snapshot(), completed: false, manualConfirmed: false, failureReason: "awaiting_escape_output");
            var mouseAllowedMetrics = CreateMetrics();
            mouseAllowedMetrics["mouse_escape_events"] = runtimeInput.Count("key", "escape");
            return Failed(name, watch, "phase2a mouse trigger did not produce escape output in target foreground", mouseAllowedMetrics);
        }

        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            RecordStage(mouseAllowedStage, host.Snapshot(), completed: false, manualConfirmed: false, failureReason: "drain_timeout");
            return Failed(name, watch, "queues did not drain after phase2a mouse real-target step", CreateMetrics());
        }

        var mouseEscapeEvents = runtimeInput.Count("key", "escape");
        var mouseObserved = ResolveManualConfirmation(
            options,
            $"Did Blue Archive react to '{mouseTrigger}' with only the mapped 'escape' effect?");
        if (mouseObserved is null)
        {
            MarkFailed("mouse_allowed", realTarget: true);
            RecordStage(mouseAllowedStage, host.Snapshot(), completed: false, manualConfirmed: false, failureReason: "awaiting_manual_confirmation");
            var mouseConfirmMetrics = CreateMetrics();
            mouseConfirmMetrics["mouse_escape_events"] = mouseEscapeEvents;
            return Failed(name, watch, "phase2a requires manual confirmation for the mouse real-target observation.", mouseConfirmMetrics);
        }

        if (mouseObserved is not true)
        {
            MarkFailed("mouse_allowed", realTarget: true);
            RecordStage(mouseAllowedStage, host.Snapshot(), completed: false, manualConfirmed: false, failureReason: "manual_confirmation_negative");
            var mouseObservedMetrics = CreateMetrics();
            mouseObservedMetrics["mouse_escape_events"] = mouseEscapeEvents;
            return Failed(name, watch, "manual observation did not confirm mouse trigger behavior in real target", mouseObservedMetrics);
        }

        MarkObserved("mouse_allowed", realTarget: true);
        SetMetric("mouse_reload_generation", afterMouseReload.Runtime.Generation);
        SetMetric("mouse_escape_events", mouseEscapeEvents);
        SetMetric("manual_mouse_confirmed", true);
        RecordStage(mouseAllowedStage, host.Snapshot(), completed: true, manualConfirmed: true);

        if (!await blocker.FocusAsync(TimeSpan.FromSeconds(5)) ||
            !LiveWindowTools.MoveCursorToCenter(blocker.Handle))
        {
            return Failed(name, watch, "failed to focus blocker for mouse blocked pass-through", CreateMetrics());
        }

        var mouseBlockedStage = BeginStage(
            "mouse_blocked",
            triggerKey: $"mouse:{mouseTrigger}",
            trackMousePassThrough: true);
        Console.Error.WriteLine(
            $"MANUAL phase2a mouse-blocked: blocker focused. Click '{mouseTrigger}' once within {options.ManualTimeout.TotalSeconds:0}s. Expect raw mouse input to reach blocker and no mapped output.");
        if (!await WaitUntilAsync(
                () => blocker.MouseDownCount > 0,
                options.ManualTimeout,
                CancellationToken.None))
        {
            MarkFailed("mouse_blocked_pass_through", realTarget: false);
            RecordStage(mouseBlockedStage, host.Snapshot(), completed: false, manualConfirmed: false, failureReason: "awaiting_blocked_pass_through");
            return Failed(name, watch, "phase2a mouse blocked pass-through was not observed in blocker", CreateMetrics());
        }

        await Task.Delay(150);
        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            RecordStage(mouseBlockedStage, host.Snapshot(), completed: false, manualConfirmed: false, failureReason: "drain_timeout");
            return Failed(name, watch, "queues did not drain after phase2a mouse blocked probe", CreateMetrics());
        }

        var mouseBlockedOutputDelta = runtimeInput.Count("key", "escape");
        var mouseBlockedDownPassThrough = blocker.MouseDownCount;
        var mouseBlockedUpPassThrough = blocker.MouseUpCount;
        if (mouseBlockedOutputDelta != 0)
        {
            MarkFailed("mouse_blocked_pass_through", realTarget: false);
            RecordStage(mouseBlockedStage, host.Snapshot(), completed: false, manualConfirmed: false, failureReason: "unexpected_mapped_output");
            var mouseBlockedOutputMetrics = CreateMetrics();
            mouseBlockedOutputMetrics["mouse_blocked_output_delta"] = mouseBlockedOutputDelta;
            return Failed(name, watch, "phase2a mouse blocked probe produced mapped output", mouseBlockedOutputMetrics);
        }

        if (mouseBlockedDownPassThrough == 0 || mouseBlockedUpPassThrough == 0)
        {
            MarkFailed("mouse_blocked_pass_through", realTarget: false);
            RecordStage(mouseBlockedStage, host.Snapshot(), completed: false, manualConfirmed: false, failureReason: "blocked_pass_through_missing");
            var mouseBlockedPassThroughMetrics = CreateMetrics();
            mouseBlockedPassThroughMetrics["mouse_blocked_pass_through_down"] = mouseBlockedDownPassThrough;
            mouseBlockedPassThroughMetrics["mouse_blocked_pass_through_up"] = mouseBlockedUpPassThrough;
            return Failed(name, watch, "phase2a mouse blocked probe did not pass through to blocker", mouseBlockedPassThroughMetrics);
        }

        MarkObserved("mouse_blocked_pass_through", realTarget: false);
        SetMetric("mouse_blocked_output_delta", mouseBlockedOutputDelta);
        SetMetric("mouse_blocked_pass_through_down", mouseBlockedDownPassThrough);
        SetMetric("mouse_blocked_pass_through_up", mouseBlockedUpPassThrough);
        RecordStage(mouseBlockedStage, host.Snapshot(), completed: true, manualConfirmed: false);

        await host.ReloadAsync(LiveEscTapConfig(targetProcess, reloadTrigger), CancellationToken.None);
        var afterReload = host.Snapshot();
        targetWindow.Refresh();
        if (!await LiveWindowTools.FocusAsync(targetWindow.MainWindowHandle, TimeSpan.FromSeconds(5)))
        {
            return Failed(name, watch, "failed to refocus Blue Archive for reload validation", CreateMetrics());
        }

        var reloadNewStage = BeginStage(
            "reload_new_trigger_allowed",
            triggerKey: $"keyboard:{reloadTrigger}");
        Console.Error.WriteLine(
            $"MANUAL phase2a reload-new-trigger: Blue Archive focused. Press '{reloadTrigger}' once within {options.ManualTimeout.TotalSeconds:0}s. Expect mapped 'escape' effect from the reloaded trigger.");
        if (!await WaitForInputCountAsync(runtimeInput, "key", "escape", 2, options.ManualTimeout))
        {
            MarkFailed("reload_new_trigger", realTarget: true);
            RecordStage(reloadNewStage, host.Snapshot(), completed: false, manualConfirmed: false, failureReason: "awaiting_escape_output");
            var reloadNewMetrics = CreateMetrics();
            reloadNewMetrics["reload_new_trigger_escape_events"] = runtimeInput.Count("key", "escape");
            return Failed(name, watch, "phase2a reload new trigger did not produce escape output in target foreground", reloadNewMetrics);
        }

        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            RecordStage(reloadNewStage, host.Snapshot(), completed: false, manualConfirmed: false, failureReason: "drain_timeout");
            return Failed(name, watch, "queues did not drain after phase2a reload new trigger step", CreateMetrics());
        }

        var reloadNewTriggerEscapeEvents = runtimeInput.Count("key", "escape");
        var reloadObserved = ResolveManualConfirmation(
            options,
            $"Did Blue Archive react to the reloaded trigger '{reloadTrigger}' with the mapped 'escape' effect?");
        if (reloadObserved is null)
        {
            MarkFailed("reload_new_trigger", realTarget: true);
            RecordStage(reloadNewStage, host.Snapshot(), completed: false, manualConfirmed: false, failureReason: "awaiting_manual_confirmation");
            var reloadConfirmMetrics = CreateMetrics();
            reloadConfirmMetrics["reload_new_trigger_escape_events"] = reloadNewTriggerEscapeEvents;
            return Failed(name, watch, "phase2a requires manual confirmation for the reloaded real-target trigger.", reloadConfirmMetrics);
        }

        if (reloadObserved is not true)
        {
            MarkFailed("reload_new_trigger", realTarget: true);
            RecordStage(reloadNewStage, host.Snapshot(), completed: false, manualConfirmed: false, failureReason: "manual_confirmation_negative");
            var reloadObservedMetrics = CreateMetrics();
            reloadObservedMetrics["reload_new_trigger_escape_events"] = reloadNewTriggerEscapeEvents;
            return Failed(name, watch, "manual observation did not confirm reload new trigger behavior in real target", reloadObservedMetrics);
        }

        MarkObserved("reload_new_trigger", realTarget: true);
        SetMetric("reload_generation", afterReload.Runtime.Generation);
        SetMetric("reload_new_trigger_escape_events", reloadNewTriggerEscapeEvents);
        SetMetric("manual_reload_confirmed", true);
        RecordStage(reloadNewStage, host.Snapshot(), completed: true, manualConfirmed: true);

        if (!await blocker.FocusAsync(TimeSpan.FromSeconds(5)) ||
            !LiveWindowTools.MoveCursorToCenter(blocker.Handle))
        {
            return Failed(name, watch, "failed to focus blocker for reload old-trigger probe", CreateMetrics());
        }

        var reloadOldStage = BeginStage(
            "reload_old_trigger_blocked",
            triggerKey: $"mouse:{mouseTrigger}",
            trackMousePassThrough: true);
        Console.Error.WriteLine(
            $"MANUAL phase2a reload-old-trigger: blocker focused. Click the old trigger '{mouseTrigger}' once within {options.ManualTimeout.TotalSeconds:0}s. Expect blocker pass-through and no mapped output.");
        if (!await WaitUntilAsync(
                () => blocker.MouseDownCount > 0,
                options.ManualTimeout,
                CancellationToken.None))
        {
            MarkFailed("reload_old_trigger_pass_through", realTarget: false);
            RecordStage(reloadOldStage, host.Snapshot(), completed: false, manualConfirmed: false, failureReason: "awaiting_blocked_pass_through");
            return Failed(name, watch, "phase2a reload old mouse trigger pass-through was not observed in blocker", CreateMetrics());
        }

        await Task.Delay(150);
        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            RecordStage(reloadOldStage, host.Snapshot(), completed: false, manualConfirmed: false, failureReason: "drain_timeout");
            return Failed(name, watch, "queues did not drain after phase2a reload old-trigger probe", CreateMetrics());
        }

        var reloadOldTriggerPassThrough = blocker.MouseDownCount;
        var reloadOldTriggerOutputDelta = runtimeInput.Count("key", "escape");
        if (reloadOldTriggerOutputDelta != 0)
        {
            MarkFailed("reload_old_trigger_pass_through", realTarget: false);
            RecordStage(reloadOldStage, host.Snapshot(), completed: false, manualConfirmed: false, failureReason: "unexpected_mapped_output");
            var reloadOldOutputMetrics = CreateMetrics();
            reloadOldOutputMetrics["reload_old_trigger_output_delta"] = reloadOldTriggerOutputDelta;
            return Failed(name, watch, "phase2a old trigger produced mapped output after reload", reloadOldOutputMetrics);
        }

        if (reloadOldTriggerPassThrough == 0)
        {
            MarkFailed("reload_old_trigger_pass_through", realTarget: false);
            RecordStage(reloadOldStage, host.Snapshot(), completed: false, manualConfirmed: false, failureReason: "blocked_pass_through_missing");
            var reloadOldPassThroughMetrics = CreateMetrics();
            reloadOldPassThroughMetrics["reload_old_trigger_pass_through"] = reloadOldTriggerPassThrough;
            return Failed(name, watch, "phase2a old trigger did not pass through after reload", reloadOldPassThroughMetrics);
        }

        MarkObserved("reload_old_trigger_pass_through", realTarget: false);
        SetMetric("reload_old_trigger_output_delta", reloadOldTriggerOutputDelta);
        SetMetric("reload_old_trigger_pass_through", reloadOldTriggerPassThrough);
        RecordStage(reloadOldStage, host.Snapshot(), completed: true, manualConfirmed: false);

        targetWindow.Refresh();
        if (!await LiveWindowTools.FocusAsync(targetWindow.MainWindowHandle, TimeSpan.FromSeconds(5)))
        {
            return Failed(name, watch, "failed to refocus Blue Archive for disable validation", CreateMetrics());
        }

        await host.DisableAsync(CancellationToken.None);
        var disabled = host.Snapshot();
        var disableStage = BeginStage(
            "disable_no_output",
            triggerKey: $"keyboard:{reloadTrigger}");
        Console.Error.WriteLine(
            $"MANUAL phase2a disable: Blue Archive focused. Press '{reloadTrigger}' once within {options.ManualTimeout.TotalSeconds:0}s. Expect no mapped 'escape' effect while runtime is disabled.");
        if (!await WaitUntilAsync(
                () => CountTriggerReceived($"keyboard:{reloadTrigger}") > 0,
                options.ManualTimeout,
                CancellationToken.None))
        {
            MarkFailed("disable_no_output", realTarget: true);
            RecordStage(disableStage, host.Snapshot(), completed: false, manualConfirmed: false, failureReason: "awaiting_trigger_reception");
            return Failed(name, watch, "phase2a disable step did not observe the disabled trigger in target foreground", CreateMetrics());
        }

        await Task.Delay(150, CancellationToken.None);
        if (!await WaitForDrainedAsync(host, options.DrainTimeout, CancellationToken.None))
        {
            RecordStage(disableStage, host.Snapshot(), completed: false, manualConfirmed: false, failureReason: "drain_timeout");
            return Failed(name, watch, "queues did not drain after phase2a disable step", CreateMetrics());
        }

        var disableOutputDelta = runtimeInput.Count("key", "escape");
        if (disableOutputDelta != 0)
        {
            MarkFailed("disable_no_output", realTarget: true);
            RecordStage(disableStage, host.Snapshot(), completed: false, manualConfirmed: false, failureReason: "unexpected_mapped_output");
            var disableOutputMetrics = CreateMetrics();
            disableOutputMetrics["disable_output_delta"] = disableOutputDelta;
            return Failed(name, watch, "phase2a disabled runtime produced mapped output in real target", disableOutputMetrics);
        }

        MarkObserved("disable_no_output", realTarget: true);
        SetMetric("disable_output_delta", disableOutputDelta);
        RecordStage(disableStage, host.Snapshot(), completed: true, manualConfirmed: false);

        await host.EnableRuntimeAsync(CancellationToken.None);
        await host.ReloadAsync(LiveLongMacroConfig(targetProcess, longMacroTrigger, "escape"), CancellationToken.None);
        var beforeLongMacroStop = host.Snapshot();
        var longMacroTriggerKey = $"keyboard:{longMacroTrigger}";
        var longMacroMappingId = beforeLongMacroStop.RuntimeConfig.Mappings
            .Select(mapping => mapping.NormalizedId)
            .SingleOrDefault() ?? $"mapping:{longMacroTriggerKey}";
        targetWindow.Refresh();
        if (!await LiveWindowTools.FocusAsync(targetWindow.MainWindowHandle, TimeSpan.FromSeconds(5)))
        {
            return Failed(name, watch, "failed to refocus Blue Archive for long macro cleanup validation", CreateMetrics());
        }

        var longMacroStage = BeginStage(
            "long_macro_only",
            triggerKey: longMacroTriggerKey,
            mappingId: longMacroMappingId);

        Console.Error.WriteLine(
            $"MANUAL phase2a long-macro-stop: Blue Archive focused. Press '{longMacroTrigger}' once within {options.ManualTimeout.TotalSeconds:0}s. Expect the long macro to begin, then runtime stop will clean it up.");
        if (!await WaitUntilAsync(
                () => runtimeInput.Count("key", "escape", true) > 0,
                options.ManualTimeout,
                CancellationToken.None))
        {
            MarkFailed("long_macro_cleanup", realTarget: true);
            var longMacroStartMetrics = CreateMetrics();
            SyncLongMacroStageMetrics(longMacroMappingId, "awaiting_escape_down", null, false, false);
            longMacroStartMetrics = CreateMetrics();
            longMacroStartMetrics["long_macro_escape_down"] = runtimeInput.Count("key", "escape", true);
            longMacroStartMetrics["long_macro_escape_up"] = runtimeInput.Count("key", "escape", false);
            return Failed(name, watch, "phase2a long macro did not press escape before stop", longMacroStartMetrics);
        }

        var longMacroObserved = ResolveManualConfirmation(
            options,
            $"Did Blue Archive react once when the long macro trigger '{longMacroTrigger}' started?");
        if (longMacroObserved is null)
        {
            MarkFailed("long_macro_cleanup", realTarget: true);
            var longMacroConfirmMetrics = CreateMetrics();
            SyncLongMacroStageMetrics(longMacroMappingId, "awaiting_manual_confirmation", null, true, false);
            longMacroConfirmMetrics = CreateMetrics();
            longMacroConfirmMetrics["long_macro_escape_down"] = runtimeInput.Count("key", "escape", true);
            longMacroConfirmMetrics["long_macro_escape_up"] = runtimeInput.Count("key", "escape", false);
            return Failed(name, watch, "phase2a requires manual confirmation for the long macro real-target observation.", longMacroConfirmMetrics);
        }

        if (longMacroObserved is not true)
        {
            MarkFailed("long_macro_cleanup", realTarget: true);
            var longMacroObservedMetrics = CreateMetrics();
            SyncLongMacroStageMetrics(longMacroMappingId, "manual_confirmation_negative", null, true, false);
            longMacroObservedMetrics = CreateMetrics();
            longMacroObservedMetrics["long_macro_escape_down"] = runtimeInput.Count("key", "escape", true);
            longMacroObservedMetrics["long_macro_escape_up"] = runtimeInput.Count("key", "escape", false);
            return Failed(name, watch, "manual observation did not confirm long macro start behavior in real target", longMacroObservedMetrics);
        }

        var eventsBeforeStop = runtimeInput.Events.Count;
        var stopWatch = Stopwatch.StartNew();
        await host.StopAsync(CancellationToken.None);
        stopWatch.Stop();
        var stopped = host.Snapshot();
        SyncLongMacroStageMetrics(longMacroMappingId, "after_stop_requested", stopped, true, true);
        var eventsAfterStop = runtimeInput.Events.Count;
        await Task.Delay(150, CancellationToken.None);
        if (runtimeInput.Events.Count != eventsAfterStop)
        {
            MarkFailed("long_macro_cleanup", realTarget: true);
            SyncLongMacroStageMetrics(longMacroMappingId, "post_stop_input_detected", stopped, true, true);
            return Failed(name, watch, "phase2a emitted input after stop returned", CreateMetrics());
        }

        var longMacroEscapeDown = runtimeInput.Count("key", "escape", true);
        var longMacroEscapeUp = runtimeInput.Count("key", "escape", false);
        if (longMacroEscapeDown != 1 || longMacroEscapeUp != 1)
        {
            MarkFailed("long_macro_cleanup", realTarget: true);
            SyncLongMacroStageMetrics(longMacroMappingId, "cleanup_validation_failed", stopped, true, true);
            var longMacroCleanupMetrics = CreateMetrics();
            longMacroCleanupMetrics["long_macro_escape_down"] = longMacroEscapeDown;
            longMacroCleanupMetrics["long_macro_escape_up"] = longMacroEscapeUp;
            return Failed(name, watch, "phase2a long macro cleanup did not release escape exactly once", longMacroCleanupMetrics);
        }

        if (!IsCleanlyStopped(stopped))
        {
            MarkFailed("long_macro_cleanup", realTarget: true);
            SyncLongMacroStageMetrics(longMacroMappingId, "stop_not_clean", stopped, true, true);
            return Failed(name, watch, "phase2a runtime did not stop cleanly after long macro validation", CreateMetrics());
        }

        MarkObserved("long_macro_cleanup", realTarget: true);
        SetMetric("long_macro_escape_down", longMacroEscapeDown);
        SetMetric("long_macro_escape_up", longMacroEscapeUp);
        SyncLongMacroStageMetrics(longMacroMappingId, "passed", stopped, true, true);

        await host.DisposeAsync();
        host = null;

        var finalMetrics = CreateMetrics();
        finalMetrics["foreground_process"] = allowedGate.ForegroundProcess ?? string.Empty;
        finalMetrics["blocked_foreground_process"] = blockedGate.ForegroundProcess ?? string.Empty;
        finalMetrics["post_stop_events"] = runtimeInput.Events.Count - eventsAfterStop;
        finalMetrics["stop_ms"] = stopWatch.Elapsed.TotalMilliseconds;
        finalMetrics["diagnostic_events"] = diagnostics.Count;
        finalMetrics["stopped_clean"] = IsCleanlyStopped(stopped);
        AddSnapshots(
            finalMetrics,
            options,
            ("after_mouse_reload", afterMouseReload),
            ("after_reload", afterReload),
            ("disabled", disabled),
            ("before_long_macro_stop", beforeLongMacroStop),
            ("stopped", stopped));
        return Passed(name, watch, finalMetrics);
    }
    catch (Exception ex)
    {
        return Failed(name, watch, ex.Message, CreateMetrics());
    }
    finally
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}

static IReadOnlyList<string> ResolveScenarioNames(string requested)
{
    var all = new[]
    {
        "burst-drain",
        "reload-during-burst",
        "stop-during-long-macro",
        "reload-during-long-macro",
        "foreground-gate-during-burst",
        "active-pointer-wait-foreground-loss-interrupts",
        "built-in-drag-foreground-loss-contract",
        "xbutton2-triggered-complete-drag-normal-completion",
        "xbutton2-triggered-multisegment-drag-normal-completion",
        "lifecycle-start-stop-loop",
        "lifecycle-reload-loop",
        "lifecycle-enable-disable-loop",
        "lifecycle-burst-reload-stop-interleave"
    };
    if (string.Equals(requested, "all", StringComparison.OrdinalIgnoreCase))
    {
        return all;
    }

    if (string.Equals(requested, "soak", StringComparison.OrdinalIgnoreCase))
    {
        return ["dry-run-soak"];
    }

    if (string.Equals(requested, "live", StringComparison.OrdinalIgnoreCase))
    {
        return ["live-safe", "live-soak"];
    }

    if (string.Equals(requested, "trigger-suppress", StringComparison.OrdinalIgnoreCase))
    {
        return
        [
            "mouse-trigger-suppressed",
            "mouse-trigger-foreground-blocked",
            "mouse-trigger-reload-disable-stop-clean",
            "trigger-captured-then-foreground-changes-before-release",
            "captured-repeat-down-does-not-redispatch",
            "self-injected-pass-through",
            "keyboard-trigger-gated-suppress"
        ];
    }

    if (string.Equals(requested, "lifecycle-stress", StringComparison.OrdinalIgnoreCase))
    {
        return
        [
            "lifecycle-start-stop-loop",
            "lifecycle-reload-loop",
            "lifecycle-enable-disable-loop",
            "lifecycle-burst-reload-stop-interleave"
        ];
    }

    return [requested.Trim().ToLowerInvariant()];
}

static Task<AcceptanceScenarioResult> RunScenarioAsync(string name, AcceptanceOptions options)
{
    return name switch
    {
        "burst-drain" => RunBurstDrainAsync(options),
        "reload-during-burst" => RunReloadDuringBurstAsync(options),
        "stop-during-long-macro" => RunStopDuringLongMacroAsync(options),
        "reload-during-long-macro" => RunReloadDuringLongMacroAsync(options),
        "foreground-gate-during-burst" => RunForegroundGateDuringBurstAsync(options),
        "active-pointer-wait-foreground-loss-interrupts" => RunActivePointerWaitForegroundLossInterruptsAsync(options),
        "built-in-drag-foreground-loss-contract" => RunBuiltInDragForegroundLossContractAsync(options),
        "xbutton2-triggered-complete-drag-normal-completion" => RunXButton2TriggeredCompleteDragNormalCompletionAsync(options),
        "xbutton2-triggered-multisegment-drag-normal-completion" => RunXButton2TriggeredMultisegmentDragNormalCompletionAsync(options),
        "dry-run-soak" => RunDryRunSoakAsync(options),
        "lifecycle-start-stop-loop" => RunLifecycleStartStopLoopAsync(options),
        "lifecycle-reload-loop" => RunLifecycleReloadLoopAsync(options),
        "lifecycle-enable-disable-loop" => RunLifecycleEnableDisableLoopAsync(options),
        "lifecycle-burst-reload-stop-interleave" => RunLifecycleBurstReloadStopInterleaveAsync(options),
        "live-safe" => RunLiveSafeAsync(options),
        "live-soak" => RunLiveSoakAsync(options),
        "mouse-trigger-suppressed" => RunMouseTriggerSuppressedAsync(options),
        "mouse-trigger-foreground-blocked" => RunMouseTriggerForegroundBlockedAsync(options),
        "mouse-trigger-reload-disable-stop-clean" => RunMouseTriggerReloadDisableStopCleanAsync(options),
        "wheel-trigger-boundaries" => RunWheelTriggerBoundariesAsync(options),
        "xbutton-trigger-boundaries" => RunXButtonTriggerBoundariesAsync(options),
        "xbutton-trigger-reload-disable" => RunXButtonTriggerReloadDisableAsync(options),
        "xbutton-self-injected-pass-through" => RunXButtonSelfInjectedPassThroughAsync(options),
        "xbutton1-hold-then-foreground-change-before-release" => RunXButtonHoldThenForegroundChangeBeforeReleaseAsync(options),
        "xbutton1-blocked-hold-then-foreground-return-before-release" => RunXButtonBlockedHoldThenForegroundReturnBeforeReleaseAsync(options),
        "xbutton2-hold-then-foreground-change-before-release" => RunXButton2HoldThenForegroundChangeBeforeReleaseAsync(options),
        "xbutton2-blocked-hold-then-foreground-return-before-release" => RunXButton2BlockedHoldThenForegroundReturnBeforeReleaseAsync(options),
        "xbutton2-triggered-drag-minimal" => RunXButton2TriggeredDragMinimalAsync(options),
        "xbutton2-triggered-multisegment-move-minimal" => RunXButton2TriggeredMultiSegmentMoveMinimalAsync(options),
        "xbutton2-triggered-drag-stop-during-active-drag" => RunXButton2TriggeredDragStopDuringActiveDragAsync(options),
        "xbutton2-triggered-drag-disable-during-active-drag" => RunXButton2TriggeredDragDisableDuringActiveDragAsync(options),
        "xbutton2-triggered-drag-reload-during-active-drag-old-trigger-blocked" => RunXButton2TriggeredDragReloadDuringActiveDragOldTriggerBlockedAsync(options),
        "xbutton2-triggered-drag-reload-during-active-drag-new-trigger-allowed" => RunXButton2TriggeredDragReloadDuringActiveDragNewTriggerAllowedAsync(options),
        "xbutton2-triggered-drag-foreground-loss-during-active-drag" => RunXButton2TriggeredDragForegroundLossDuringActiveDragAsync(options),
        "xbutton2-triggered-drag-foreground-loss-then-return-before-release" => RunXButton2TriggeredDragForegroundLossThenReturnBeforeReleaseAsync(options),
        "trigger-captured-then-foreground-changes-before-release" => RunCapturedTriggerForegroundChangeAsync(options),
        "captured-repeat-down-does-not-redispatch" => RunCapturedRepeatDownDoesNotRedispatchAsync(options),
        "self-injected-pass-through" => RunSelfInjectedPassThroughAsync(options),
        "keyboard-trigger-gated-suppress" => RunKeyboardTriggerGatedSuppressAsync(options),
        "bluearchive-manual" => RunBlueArchiveManualAsync(options),
        "bluearchive-manual-wheel" => RunBlueArchiveManualWheelAsync(options),
        "bluearchive-manual-xbutton" => RunBlueArchiveManualXButtonAsync(options),
        "bluearchive-manual-xbutton-reload" => RunBlueArchiveManualXButtonReloadAsync(options),
        "bluearchive-manual-xbutton-disable" => RunBlueArchiveManualXButtonDisableAsync(options),
        "bluearchive-manual-xbutton-hold-foreground-change" => RunBlueArchiveManualXButtonHoldForegroundChangeAsync(options),
        "bluearchive-manual-xbutton-blocked-hold-return" => RunBlueArchiveManualXButtonBlockedHoldReturnAsync(options),
        "bluearchive-manual-xbutton2-hold-foreground-change" => RunBlueArchiveManualXButton2HoldForegroundChangeAsync(options),
        "bluearchive-manual-xbutton2-blocked-hold-return" => RunBlueArchiveManualXButton2BlockedHoldReturnAsync(options),
        "bluearchive-manual-xbutton2-drag-minimal" => RunBlueArchiveManualXButton2DragMinimalAsync(options),
        "bluearchive-manual-xbutton2-multisegment-move-minimal" => RunBlueArchiveManualXButton2MultiSegmentMoveMinimalAsync(options),
        "bluearchive-manual-xbutton2-drag-stop-during-active-drag" => RunBlueArchiveManualXButton2DragStopDuringActiveDragAsync(options),
        "bluearchive-manual-xbutton2-drag-disable-during-active-drag" => RunBlueArchiveManualXButton2DragDisableDuringActiveDragAsync(options),
        "bluearchive-manual-xbutton2-drag-reload-during-active-drag-old-trigger-blocked" => RunBlueArchiveManualXButton2DragReloadDuringActiveDragOldTriggerBlockedAsync(options),
        "bluearchive-manual-xbutton2-drag-reload-during-active-drag-new-trigger-allowed" => RunBlueArchiveManualXButton2DragReloadDuringActiveDragNewTriggerAllowedAsync(options),
        "bluearchive-manual-xbutton2-drag-foreground-loss-during-active-drag" => RunBlueArchiveManualXButton2DragForegroundLossDuringActiveDragAsync(options),
        "bluearchive-manual-xbutton2-drag-foreground-loss-then-return-before-release" => RunBlueArchiveManualXButton2DragForegroundLossThenReturnBeforeReleaseAsync(options),
        "bluearchive-manual-phase2a" => RunBlueArchiveManualPhase2AAsync(options),
        _ => Task.FromResult(new AcceptanceScenarioResult(
            name,
            false,
            0,
            $"Unknown scenario: {name}",
            new Dictionary<string, object>()))
    };
}

static void WriteHumanSummary(AcceptanceReport report)
{
    var status = report.Passed ? "PASS" : "FAIL";
    Console.Error.WriteLine($"ACCEPTANCE {status} scenarios={report.Scenarios.Count} elapsed_ms={report.DurationMs:0.###}");
    foreach (var scenario in report.Scenarios)
    {
        var scenarioStatus = scenario.Passed ? "PASS" : "FAIL";
        Console.Error.WriteLine($"{scenarioStatus} {scenario.Name} elapsed_ms={scenario.DurationMs:0.###}");
        if (!scenario.Passed && !string.IsNullOrWhiteSpace(scenario.Error))
        {
            Console.Error.WriteLine($"  error={scenario.Error}");
        }
    }
}

if (Has(args, "--help") || Has(args, "-h"))
{
    PrintUsage();
    return 0;
}

var requestedScenario = ValueAfter(args, "--scenario") ?? "all";
var options = new AcceptanceOptions(
    BurstCount: IntOption(args, "--burst", fallback: 50, minimum: 1),
    DrainTimeout: TimeSpan.FromSeconds(DoubleOption(args, "--drain-timeout", fallback: 5, minimum: 0.1)),
    SoakDuration: TimeSpan.FromSeconds(DoubleOption(args, "--soak-seconds", fallback: 300, minimum: 1)),
    SoakRateHz: DoubleOption(args, "--soak-rate", fallback: 20, minimum: 0.1),
    AllowLiveInput: Has(args, "--allow-live-input"),
    IncludeSnapshots: Has(args, "--include-snapshots"),
    TargetProcess: ValueAfter(args, "--target-process") ?? "BlueArchive.exe",
    ManualKeyboardTrigger: ValueAfter(args, "--manual-trigger") ?? "f8",
    ManualWheelTrigger: ValueAfter(args, "--manual-wheel-trigger") ?? "mouse_wheel_up",
    ManualXButtonTrigger: ValueAfter(args, "--manual-xbutton-trigger") ?? "mouse_x1",
    ManualXButtonReloadTrigger: ValueAfter(args, "--manual-xbutton-reload-trigger") ?? "mouse_x2",
    ManualMouseTrigger: ValueAfter(args, "--manual-mouse-trigger") ?? "mouse_middle",
    ManualReloadKeyboardTrigger: ValueAfter(args, "--manual-reload-trigger") ?? "f9",
    ManualLongMacroTrigger: ValueAfter(args, "--manual-long-trigger") ?? "f10",
    ManualLongMacroOnly: Has(args, "--manual-long-only"),
    ManualTimeout: TimeSpan.FromSeconds(DoubleOption(args, "--manual-timeout", fallback: 15, minimum: 1)),
    ManualConfirmationMode: ValueAfter(args, "--manual-confirm") ?? "prompt");

var watch = Stopwatch.StartNew();
var results = new List<AcceptanceScenarioResult>();
foreach (var name in ResolveScenarioNames(requestedScenario))
{
    Console.Error.WriteLine($"RUN {name}");
    results.Add(await RunScenarioAsync(name, options));
}

watch.Stop();
var report = new AcceptanceReport(
    Tool: "BAKeySmith.Acceptance",
    Runtime: ".NET",
    GeneratedAtUtc: DateTimeOffset.UtcNow,
    Scenario: requestedScenario.Trim().ToLowerInvariant(),
    BurstCount: options.BurstCount,
    DrainTimeoutMs: options.DrainTimeout.TotalMilliseconds,
    SoakSeconds: options.SoakDuration.TotalSeconds,
    SoakRateHz: options.SoakRateHz,
    Passed: results.All(result => result.Passed),
    DurationMs: watch.Elapsed.TotalMilliseconds,
    Scenarios: results);

var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true
};
jsonOptions.Converters.Add(new JsonStringEnumConverter());

WriteHumanSummary(report);
var outputPath = ValueAfter(args, "--output");
var json = JsonSerializer.Serialize(report, jsonOptions);
if (!string.IsNullOrWhiteSpace(outputPath))
{
    var directory = Path.GetDirectoryName(outputPath);
    if (!string.IsNullOrWhiteSpace(directory))
    {
        Directory.CreateDirectory(directory);
    }

    await File.WriteAllTextAsync(outputPath, json);
    Console.Error.WriteLine($"REPORT {outputPath}");
}
else
{
    Console.WriteLine(json);
}

return report.Passed ? 0 : 1;

internal sealed record AcceptanceOptions(
    int BurstCount,
    TimeSpan DrainTimeout,
    TimeSpan SoakDuration,
    double SoakRateHz,
    bool AllowLiveInput,
    bool IncludeSnapshots,
    string TargetProcess,
    string ManualKeyboardTrigger,
    string ManualWheelTrigger,
    string ManualXButtonTrigger,
    string ManualXButtonReloadTrigger,
    string ManualMouseTrigger,
    string ManualReloadKeyboardTrigger,
    string ManualLongMacroTrigger,
    bool ManualLongMacroOnly,
    TimeSpan ManualTimeout,
    string ManualConfirmationMode);

internal sealed record ElevationProbeResult(
    int ProcessId,
    string ProcessName,
    bool IsKnown,
    bool IsElevated);

internal sealed record ResourceSample(
    long WorkingSetBytes,
    long PrivateMemoryBytes,
    int ThreadCount,
    TimeSpan TotalProcessorTime);

internal sealed record AcceptanceScenarioResult(
    string Name,
    bool Passed,
    double DurationMs,
    string? Error,
    IReadOnlyDictionary<string, object> Metrics);

internal sealed record AcceptanceReport(
    string Tool,
    string Runtime,
    DateTimeOffset GeneratedAtUtc,
    string Scenario,
    int BurstCount,
    double DrainTimeoutMs,
    double SoakSeconds,
    double SoakRateHz,
    bool Passed,
    double DurationMs,
    IReadOnlyList<AcceptanceScenarioResult> Scenarios);

internal static partial class NativeMethods
{
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("advapi32.dll", SetLastError = true)]
    internal static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    internal static extern bool GetTokenInformation(
        IntPtr TokenHandle,
        int TokenInformationClass,
        IntPtr TokenInformation,
        int TokenInformationLength,
        out int ReturnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool CloseHandle(IntPtr hObject);
}
