namespace BAKeySmith.Core.Input;

public static class InjectedInputMarker
{
    private const ulong MarkerValue = 0x42414B53; // BAKS

    public static UIntPtr ExtraInfo { get; } = (UIntPtr)MarkerValue;

    public static bool IsMarked(UIntPtr extraInfo)
    {
        return extraInfo == ExtraInfo;
    }
}
