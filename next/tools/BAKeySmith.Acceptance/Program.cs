using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
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
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario dry-run-soak --soak-seconds 300 --soak-rate 20
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario live-safe --allow-live-input
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario live-soak --allow-live-input --soak-seconds 60 --soak-rate 20
          dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario bluearchive-manual --allow-live-input --target-process BlueArchive.exe

        Options:
          --scenario <name>         all, burst-drain, reload-during-burst, stop-during-long-macro,
                                    reload-during-long-macro, foreground-gate-during-burst,
                                    dry-run-soak, live-safe, live-soak,
                                    lifecycle-start-stop-loop, lifecycle-reload-loop,
                                    lifecycle-enable-disable-loop,
                                    lifecycle-burst-reload-stop-interleave,
                                    lifecycle-stress,
                                    mouse-trigger-suppressed, mouse-trigger-foreground-blocked,
                                    mouse-trigger-reload-disable-stop-clean,
                                    trigger-captured-then-foreground-changes-before-release,
                                    self-injected-pass-through, keyboard-trigger-gated-suppress,
                                    bluearchive-manual,
                                    trigger-suppress.
          --burst <count>           Burst size for burst scenarios. Default 50.
          --drain-timeout <seconds> Max wait for trigger/action queues to drain. Default 5.
          --soak-seconds <seconds>  Duration for dry-run-soak. Default 300.
          --soak-rate <hz>          Trigger rate for dry-run-soak. Default 20.
          --allow-live-input        Required for live-safe and live-soak. Sends real F13/F14-style input.
          --target-process <name>   Real target process for bluearchive-manual. Default BlueArchive.exe.
          --manual-trigger <key>    Physical keyboard trigger for bluearchive-manual. Default f8.
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

static IReadOnlyList<string> ResolveScenarioNames(string requested)
{
    var all = new[]
    {
        "burst-drain",
        "reload-during-burst",
        "stop-during-long-macro",
        "reload-during-long-macro",
        "foreground-gate-during-burst",
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
        "trigger-captured-then-foreground-changes-before-release" => RunCapturedTriggerForegroundChangeAsync(options),
        "self-injected-pass-through" => RunSelfInjectedPassThroughAsync(options),
        "keyboard-trigger-gated-suppress" => RunKeyboardTriggerGatedSuppressAsync(options),
        "bluearchive-manual" => RunBlueArchiveManualAsync(options),
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
