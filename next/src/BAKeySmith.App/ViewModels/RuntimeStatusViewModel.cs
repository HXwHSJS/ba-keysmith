using BAKeySmith.Core.Hosting;
using BAKeySmith.App.Models;

namespace BAKeySmith.App.ViewModels;

public sealed class RuntimeStatusViewModel : ViewModelBase
{
    private string _statusText = "Stopped";
    private string _targetText = "BlueArchive.exe";
    private string _inputEventCountText = "0";
    private string _hostStartedText = "stopped";
    private string _runtimeStateText = "Stopped";
    private string _pipelineStateText = "stopped";
    private string _foregroundStateText = "unknown";
    private string _generationText = "0";
    private string _workerCountText = "0";
    private string _pendingActionCountText = "0";
    private string _runningActionCountText = "0";
    private string _heldKeyCountText = "0";
    private string _ownerCountText = "0";
    private string _snapshotDetailsText = string.Empty;

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string TargetText
    {
        get => _targetText;
        private set => SetProperty(ref _targetText, value);
    }

    public string InputEventCountText
    {
        get => _inputEventCountText;
        private set => SetProperty(ref _inputEventCountText, value);
    }

    public string HostStartedText
    {
        get => _hostStartedText;
        private set => SetProperty(ref _hostStartedText, value);
    }

    public string RuntimeStateText
    {
        get => _runtimeStateText;
        private set => SetProperty(ref _runtimeStateText, value);
    }

    public string PipelineStateText
    {
        get => _pipelineStateText;
        private set => SetProperty(ref _pipelineStateText, value);
    }

    public string ForegroundStateText
    {
        get => _foregroundStateText;
        private set => SetProperty(ref _foregroundStateText, value);
    }

    public string GenerationText
    {
        get => _generationText;
        private set => SetProperty(ref _generationText, value);
    }

    public string WorkerCountText
    {
        get => _workerCountText;
        private set => SetProperty(ref _workerCountText, value);
    }

    public string PendingActionCountText
    {
        get => _pendingActionCountText;
        private set => SetProperty(ref _pendingActionCountText, value);
    }

    public string RunningActionCountText
    {
        get => _runningActionCountText;
        private set => SetProperty(ref _runningActionCountText, value);
    }

    public string HeldKeyCountText
    {
        get => _heldKeyCountText;
        private set => SetProperty(ref _heldKeyCountText, value);
    }

    public string OwnerCountText
    {
        get => _ownerCountText;
        private set => SetProperty(ref _ownerCountText, value);
    }

    public string SnapshotDetailsText
    {
        get => _snapshotDetailsText;
        private set => SetProperty(ref _snapshotDetailsText, value);
    }

    public void ApplySnapshot(
        RuntimeHostSnapshot? snapshot,
        int inputEventCount,
        string fallbackTarget,
        int editorMappingCount)
    {
        InputEventCountText = inputEventCount.ToString();

        if (snapshot is null)
        {
            StatusText = "Stopped";
            TargetText = string.IsNullOrWhiteSpace(fallbackTarget) ? "-" : fallbackTarget.Trim();
            HostStartedText = "stopped";
            RuntimeStateText = "Stopped";
            PipelineStateText = "stopped";
            ForegroundStateText = "unknown";
            GenerationText = "0";
            WorkerCountText = "0";
            PendingActionCountText = "0";
            RunningActionCountText = "0";
            HeldKeyCountText = "0";
            OwnerCountText = "0";
            SnapshotDetailsText =
                $"not running{Environment.NewLine}" +
                $"editor_mappings={editorMappingCount}{Environment.NewLine}" +
                $"target={TargetText}";
            return;
        }

        var runtime = snapshot.Runtime;
        var presses = runtime.Presses;
        StatusText = snapshot.IsStarted ? "Running" : "Stopped";
        TargetText = snapshot.RuntimeConfig.TargetProcess;
        HostStartedText = snapshot.IsStarted ? "started" : "stopped";
        RuntimeStateText = runtime.State.ToString();
        PipelineStateText = snapshot.IsPipelineRunning ? "running" : "stopped";
        ForegroundStateText = runtime.LastForegroundAllowed ? "last allowed" : "last blocked";
        GenerationText = runtime.Generation.ToString();
        WorkerCountText = runtime.ActiveWorkerCount.ToString();
        PendingActionCountText = runtime.PendingActionCount.ToString();
        RunningActionCountText = runtime.RunningActionCount.ToString();
        HeldKeyCountText = presses.KeyOwners.Count.ToString();
        OwnerCountText = presses.OwnerKeys.Count.ToString();
        SnapshotDetailsText = FormatSnapshotDetails(snapshot);
    }

    public void ApplyForegroundProbe(ForegroundProbeDisplay probe)
    {
        ForegroundStateText = probe.StateText;
    }

    private static string FormatSnapshotDetails(RuntimeHostSnapshot snapshot)
    {
        var runtime = snapshot.Runtime;
        var presses = runtime.Presses;
        var heldKeys = presses.KeyOwners.Count == 0
            ? "held_keys=none"
            : "held_keys=" + string.Join("; ", presses.KeyOwners.Select(pair =>
                $"{pair.Key}<-{string.Join(",", pair.Value)}"));
        var owners = presses.OwnerKeys.Count == 0
            ? "owners=none"
            : "owners=" + string.Join("; ", presses.OwnerKeys.Select(pair =>
                $"{pair.Key}->{string.Join(",", pair.Value)}"));

        return string.Join(
            Environment.NewLine,
            $"state={runtime.State} host_started={snapshot.IsStarted} pipeline={snapshot.IsPipelineRunning}",
            $"target={snapshot.RuntimeConfig.TargetProcess} mappings={runtime.MappingCount} generation={runtime.Generation}",
            $"trigger_pipeline queued={snapshot.Pipeline.QueuedCount} handled={snapshot.Pipeline.HandledCount} pending={snapshot.Pipeline.PendingCount}",
            $"workers={runtime.ActiveWorkerCount} pending_actions={runtime.PendingActionCount} running_actions={runtime.RunningActionCount} last_foreground_allowed={runtime.LastForegroundAllowed}",
            heldKeys,
            owners);
    }
}
