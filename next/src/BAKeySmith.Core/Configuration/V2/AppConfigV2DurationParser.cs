using System.Globalization;

namespace BAKeySmith.Core.Configuration.V2;

internal static class AppConfigV2DurationParser
{
    public static bool TryParse(string text, out TimeSpan duration)
    {
        duration = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (trimmed.EndsWith("ms", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseUnit(trimmed[..^2], TimeSpan.FromMilliseconds, out duration);
        }

        if (trimmed.EndsWith("s", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseUnit(trimmed[..^1], TimeSpan.FromSeconds, out duration);
        }

        return false;
    }

    public static string Format(TimeSpan duration)
    {
        if (duration.Ticks % TimeSpan.TicksPerMillisecond == 0)
        {
            return $"{duration.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture)}ms";
        }

        return $"{duration.TotalSeconds.ToString("0.#########", CultureInfo.InvariantCulture)}s";
    }

    private static bool TryParseUnit(
        string value,
        Func<double, TimeSpan> factory,
        out TimeSpan duration)
    {
        duration = default;
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) ||
            !double.TryParse(
                trimmed,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var number))
        {
            return false;
        }

        if (number < 0)
        {
            return false;
        }

        duration = factory(number);
        return true;
    }
}
