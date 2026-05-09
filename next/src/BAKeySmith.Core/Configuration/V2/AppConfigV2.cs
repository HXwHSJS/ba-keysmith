using BAKeySmith.Core.Input.V2.Conflicts;

namespace BAKeySmith.Core.Configuration.V2;

public sealed record AppConfigV2(
    int Version,
    IReadOnlyList<MappingConfigV2> Mappings,
    HotkeySpecV2? ControlHotkey = null,
    CoordinateRecordHotkeySpecV2? CoordinateRecordHotkey = null,
    HotkeySpecV2? EmergencyStopHotkey = null,
    TimingSettingsV2? DefaultTiming = null,
    CoordinateSettingsV2? DefaultCoordinateSettings = null)
{
    public const int CurrentVersion = 2;

    public IReadOnlyList<MappingConfigV2> Mappings { get; init; } =
        Mappings.ToArray();

    public static AppConfigV2 Create(
        IEnumerable<MappingConfigV2> mappings,
        HotkeySpecV2? controlHotkey = null,
        CoordinateRecordHotkeySpecV2? coordinateRecordHotkey = null,
        HotkeySpecV2? emergencyStopHotkey = null,
        TimingSettingsV2? defaultTiming = null,
        CoordinateSettingsV2? defaultCoordinateSettings = null)
    {
        return new AppConfigV2(
            CurrentVersion,
            mappings.ToArray(),
            controlHotkey,
            coordinateRecordHotkey,
            emergencyStopHotkey,
            defaultTiming,
            defaultCoordinateSettings);
    }
}
