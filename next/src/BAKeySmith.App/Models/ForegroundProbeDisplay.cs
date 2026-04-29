using BAKeySmith.Core.Foreground;

namespace BAKeySmith.App.Models;

public sealed record ForegroundProbeDisplay(
    bool? IsAllowed,
    string TargetProcess,
    string ForegroundProcess,
    string ForegroundWindowTitle)
{
    public static ForegroundProbeDisplay Unknown { get; } = new(
        null,
        "-",
        "-",
        "-");

    public string StateText => IsAllowed switch
    {
        true => "allowed",
        false => "blocked",
        _ => "unknown"
    };

    public string Display => IsAllowed is null
        ? "前台判定尚未刷新"
        : $"allowed={IsAllowed.Value} target={TargetProcess} foreground={ForegroundProcess} title={ForegroundWindowTitle}";

    public static ForegroundProbeDisplay FromResult(ForegroundGateResult result)
    {
        return new ForegroundProbeDisplay(
            result.IsAllowed,
            string.IsNullOrWhiteSpace(result.TargetProcess) ? "-" : result.TargetProcess,
            string.IsNullOrWhiteSpace(result.ForegroundProcess) ? "-" : result.ForegroundProcess,
            string.IsNullOrWhiteSpace(result.ForegroundWindowTitle) ? "-" : result.ForegroundWindowTitle);
    }
}
