using BAKeySmith.Core.Actions.V2;
using BAKeySmith.Core.Input.V2;

namespace BAKeySmith.Core.Runtime.V2;

public enum OwnershipResourceKindV2
{
    Key,
    MouseButton,
    CoordinateContact
}

public sealed record OwnershipResourceV2(
    OwnershipResourceKindV2 Kind,
    InputSpec? Input = null,
    CoordinatePointV2? Coordinate = null,
    InputSpec? CoordinateButton = null)
{
    public string DisplayName =>
        Kind switch
        {
            OwnershipResourceKindV2.Key => Input?.CanonicalName ?? "key:<missing>",
            OwnershipResourceKindV2.MouseButton => Input?.CanonicalName ?? "mouse:<missing>",
            OwnershipResourceKindV2.CoordinateContact =>
                $"{Coordinate?.ProfileId}:{Coordinate?.LogicalX}:{Coordinate?.LogicalY}:{CoordinateButton?.CanonicalName ?? "mouse_left"}",
            _ => Kind.ToString()
        };

    public static OwnershipResourceV2 Key(InputSpec key) =>
        new(OwnershipResourceKindV2.Key, Input: key);

    public static OwnershipResourceV2 MouseButton(InputSpec button) =>
        new(OwnershipResourceKindV2.MouseButton, Input: button);

    public static OwnershipResourceV2 CoordinateContact(
        CoordinatePointV2 point,
        InputSpec button) =>
        new(
            OwnershipResourceKindV2.CoordinateContact,
            Coordinate: point,
            CoordinateButton: button);
}
