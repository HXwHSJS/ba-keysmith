using System.Diagnostics;

namespace BAKeySmith.Core.Input;

public sealed record InputEvent(
    long TimestampTicks,
    string Kind,
    string Code,
    bool? IsDown = null,
    int? X = null,
    int? Y = null)
{
    public double TimestampMs => TimestampTicks * 1000.0 / Stopwatch.Frequency;
}
