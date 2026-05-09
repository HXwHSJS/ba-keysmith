namespace BAKeySmith.Core.Actions.V2;

public enum ActionOwnershipEffectV2
{
    None,
    Acquire,
    Release,
    Transient,
    TimedHold
}

public enum ActionOwnershipResourceV2
{
    None,
    Key,
    MouseButton,
    CoordinateContact
}

public sealed record ActionOwnershipHintV2(
    ActionOwnershipEffectV2 Effect,
    ActionOwnershipResourceV2 Resource)
{
    public static readonly ActionOwnershipHintV2 None = new(
        ActionOwnershipEffectV2.None,
        ActionOwnershipResourceV2.None);

    public static readonly ActionOwnershipHintV2 AcquireKey = new(
        ActionOwnershipEffectV2.Acquire,
        ActionOwnershipResourceV2.Key);

    public static readonly ActionOwnershipHintV2 ReleaseKey = new(
        ActionOwnershipEffectV2.Release,
        ActionOwnershipResourceV2.Key);

    public static readonly ActionOwnershipHintV2 TransientKey = new(
        ActionOwnershipEffectV2.Transient,
        ActionOwnershipResourceV2.Key);

    public static readonly ActionOwnershipHintV2 TimedHoldKey = new(
        ActionOwnershipEffectV2.TimedHold,
        ActionOwnershipResourceV2.Key);

    public static readonly ActionOwnershipHintV2 AcquireMouseButton = new(
        ActionOwnershipEffectV2.Acquire,
        ActionOwnershipResourceV2.MouseButton);

    public static readonly ActionOwnershipHintV2 ReleaseMouseButton = new(
        ActionOwnershipEffectV2.Release,
        ActionOwnershipResourceV2.MouseButton);

    public static readonly ActionOwnershipHintV2 TransientMouseButton = new(
        ActionOwnershipEffectV2.Transient,
        ActionOwnershipResourceV2.MouseButton);

    public static readonly ActionOwnershipHintV2 TimedHoldMouseButton = new(
        ActionOwnershipEffectV2.TimedHold,
        ActionOwnershipResourceV2.MouseButton);

    public static readonly ActionOwnershipHintV2 AcquireCoordinateContact = new(
        ActionOwnershipEffectV2.Acquire,
        ActionOwnershipResourceV2.CoordinateContact);

    public static readonly ActionOwnershipHintV2 ReleaseCoordinateContact = new(
        ActionOwnershipEffectV2.Release,
        ActionOwnershipResourceV2.CoordinateContact);

    public static readonly ActionOwnershipHintV2 TransientCoordinateContact = new(
        ActionOwnershipEffectV2.Transient,
        ActionOwnershipResourceV2.CoordinateContact);

    public static readonly ActionOwnershipHintV2 TimedHoldCoordinateContact = new(
        ActionOwnershipEffectV2.TimedHold,
        ActionOwnershipResourceV2.CoordinateContact);
}
