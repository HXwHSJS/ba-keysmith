namespace BAKeySmith.Core.Actions.V2;

public abstract record ActionModelV2(ActionKindV2 Kind)
{
    public virtual ActionOwnershipHintV2 OwnershipHint => ActionOwnershipHintV2.None;

    public bool ClaimsLiveReadiness => false;
}
