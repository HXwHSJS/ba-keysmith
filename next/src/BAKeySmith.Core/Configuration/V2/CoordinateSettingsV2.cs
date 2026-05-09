namespace BAKeySmith.Core.Configuration.V2;

public sealed record CoordinateSettingsV2(
    string? ProfileId = null,
    CoordinateExecutionPolicyV2 ExecutionPolicy = CoordinateExecutionPolicyV2.StrictCursorPreserving,
    CoordinateTransformPolicyV2 TransformPolicy = CoordinateTransformPolicyV2.SnapshotPerGesture)
{
    public bool ClaimsLiveReadiness => false;
}
