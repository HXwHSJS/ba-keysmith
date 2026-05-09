using BAKeySmith.Core.Actions.V2;
using BAKeySmith.Core.Input.V2;

namespace BAKeySmith.Core.Mappings.V2;

public enum SimpleMappingTargetKindV2
{
    Input,
    Coordinate
}

public sealed record SimpleMappingTargetV2(
    SimpleMappingTargetKindV2 Kind,
    InputSpec? Input = null,
    CoordinatePointV2? Coordinate = null)
{
    public static SimpleMappingTargetV2 FromInput(InputSpec input) =>
        new(SimpleMappingTargetKindV2.Input, Input: input);

    public static SimpleMappingTargetV2 FromCoordinate(CoordinatePointV2 coordinate) =>
        new(SimpleMappingTargetKindV2.Coordinate, Coordinate: coordinate);
}
