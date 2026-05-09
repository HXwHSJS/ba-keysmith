namespace BAKeySmith.Core.Input.V2.Conflicts;

public sealed record CoordinateRecordHotkeySpecV2
{
    public CoordinateRecordHotkeySpecV2(IReadOnlyList<InputSpec> modifiers, InputSpec mainInput)
    {
        Modifiers = HotkeySpecV2.NormalizeAndValidateModifiers(modifiers);
        MainInput = ValidateMainInput(mainInput);
    }

    public IReadOnlyList<InputSpec> Modifiers { get; }
    public InputSpec MainInput { get; }

    public IReadOnlyList<InputSpec> Components => Modifiers.Concat([MainInput]).ToArray();

    public string CanonicalText => string.Join(
        "+",
        Modifiers.Select(modifier => modifier.CanonicalName).Append(MainInput.CanonicalName));

    public static CoordinateRecordHotkeySpecV2 Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Coordinate record hotkey must not be empty.", nameof(text));
        }

        return FromNames(text.Split(
            '+',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    public static CoordinateRecordHotkeySpecV2 FromNames(IEnumerable<string> names)
    {
        var modifiers = new List<InputSpec>();
        InputSpec? mainInput = null;

        foreach (var name in names)
        {
            var spec = InputNameResolverV2.Resolve(name);
            if (spec.CanBeHotkeyModifier)
            {
                modifiers.Add(spec);
                continue;
            }

            if (!CanBeMainInput(spec))
            {
                throw new ArgumentException($"{spec.CanonicalName} cannot be used as a coordinate record hotkey main input.");
            }

            if (mainInput is not null)
            {
                throw new ArgumentException("Coordinate record hotkey must contain only one main input.");
            }

            mainInput = spec;
        }

        if (mainInput is null)
        {
            throw new ArgumentException("Coordinate record hotkey must include a non-modifier main input.");
        }

        return new CoordinateRecordHotkeySpecV2(
            HotkeySpecV2.NormalizeAndValidateModifiers(modifiers),
            mainInput);
    }

    public static bool CanBeMainInput(InputSpec input)
    {
        return input.CanBeCoordinateRecordHotkey &&
            (input.CanBeHotkeyMainKey || input.Kind == InputKind.MouseButton);
    }

    private static InputSpec ValidateMainInput(InputSpec mainInput)
    {
        if (!CanBeMainInput(mainInput))
        {
            throw new ArgumentException($"{mainInput.CanonicalName} cannot be used as a coordinate record hotkey main input.");
        }

        return mainInput;
    }
}
