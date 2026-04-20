namespace BAKeySmith.Core.Contracts;

public sealed record RuntimeAction(
    string Kind,
    string? Key = null,
    IReadOnlyList<MacroInstruction>? Macro = null)
{
    public string NormalizedKind => Normalize(Kind);

    public static RuntimeAction Hold(string key) => new("hold", Key: Normalize(key));
    public static RuntimeAction Tap(string key) => new("tap", Key: Normalize(key));
    public static RuntimeAction MacroPlan(IReadOnlyList<MacroInstruction> instructions) =>
        new("macro", Macro: instructions);

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Action fields must not be empty.", nameof(value));
        }

        return value.Trim().ToLowerInvariant();
    }
}

public sealed record MacroInstruction(string Op, IReadOnlyList<string> Args, int Line = 0)
{
    public string NormalizedOp
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Op))
            {
                throw new ArgumentException("Macro instruction op must not be empty.", nameof(Op));
            }

            return Op.Trim().ToLowerInvariant();
        }
    }
}
