using System.Diagnostics;
using System.Text.Json;
using BAKeySmith.Core.Contracts;
using BAKeySmith.Core.Diagnostics;
using BAKeySmith.Core.Foreground;
using BAKeySmith.Core.Input;
using BAKeySmith.Core.Runtime;

static async Task WaitForEventsAsync(DryRunInputBackend backend, int eventCount, TimeSpan timeout)
{
    var sw = Stopwatch.StartNew();
    while (backend.Events.Count < eventCount && sw.Elapsed < timeout)
    {
        await Task.Delay(1);
    }
}

var iterations = args.Length > 0 && int.TryParse(args[0], out var parsedIterations)
    ? parsedIterations
    : 10_000;

static async Task<object> RunCaseAsync(int iterations, bool collectDiagnostics)
{
    var backend = new DryRunInputBackend();
    var memoryDiagnostics = collectDiagnostics ? new InMemoryDiagnosticsSink() : null;
    var diagnostics = (IDiagnosticsSink?)memoryDiagnostics ?? NoOpDiagnosticsSink.Instance;
    await using var runtime = new InProcessRuntimeCore(
        backend,
        AlwaysForegroundGate.Instance,
        diagnostics);

    var config = new RuntimeConfig(
        "BlueArchive.exe",
        TapHoldMilliseconds: 0,
        Mappings:
        [
            new MappingDefinition(
                "bench:q_to_1_tap",
                TriggerSpec.Keyboard("q"),
                RuntimeAction.Tap("1"))
        ]);

    await runtime.LoadAsync(config, CancellationToken.None);
    await runtime.EnableAsync(CancellationToken.None);

    var process = Process.GetCurrentProcess();
    var cpuStart = process.TotalProcessorTime;
    var memoryStart = process.WorkingSet64;
    var stopwatch = Stopwatch.StartNew();

    for (var i = 0; i < iterations; i++)
    {
        await runtime.HandleTriggerAsync(TriggerEvent.Down(TriggerSpec.Keyboard("q")), CancellationToken.None);
    }

    await WaitForEventsAsync(backend, iterations * 2, TimeSpan.FromSeconds(10));
    stopwatch.Stop();
    process.Refresh();
    var cpuEnd = process.TotalProcessorTime;
    var memoryEnd = process.WorkingSet64;

    var disableWatch = Stopwatch.StartNew();
    await runtime.DisableAsync(CancellationToken.None);
    disableWatch.Stop();

    var elapsedSeconds = Math.Max(stopwatch.Elapsed.TotalSeconds, 0.000001);
    var cpuPercent = (cpuEnd - cpuStart).TotalMilliseconds /
        (stopwatch.Elapsed.TotalMilliseconds * Environment.ProcessorCount) * 100.0;

    return new
    {
        collect_diagnostics = collectDiagnostics,
        elapsed_ms = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 3),
        triggers_per_second = Math.Round(iterations / elapsedSeconds, 2),
        input_events = backend.Events.Count,
        diagnostics = memoryDiagnostics?.Events.Count ?? 0,
        cpu_percent_estimate = Math.Round(cpuPercent, 3),
        memory_delta_mb = Math.Round((memoryEnd - memoryStart) / 1024.0 / 1024.0, 3),
        disable_ms = Math.Round(disableWatch.Elapsed.TotalMilliseconds, 3),
        snapshot = runtime.Snapshot()
    };
}

var result = new
{
    iterations,
    cases = new[]
    {
        await RunCaseAsync(iterations, collectDiagnostics: false),
        await RunCaseAsync(iterations, collectDiagnostics: true)
    }
};

Console.WriteLine(JsonSerializer.Serialize(
    result,
    new JsonSerializerOptions { WriteIndented = true }));
