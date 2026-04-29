namespace BAKeySmith.Core.Triggers;

internal interface ITriggerCapturePolicySink
{
    void UpdateCapturePolicy(TriggerCapturePolicySnapshot policy);
}
