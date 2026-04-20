using BAKeySmith.Core.Contracts;

namespace BAKeySmith.Core.Configuration;

public sealed record ConfigLoadResult(
    AppConfigV1 Config,
    RuntimeConfig RuntimeConfig,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    public bool Success => Errors.Count == 0;
}
