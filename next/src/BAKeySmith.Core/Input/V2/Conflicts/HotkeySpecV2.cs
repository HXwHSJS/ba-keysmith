namespace BAKeySmith.Core.Input.V2.Conflicts;

public sealed record HotkeySpecV2
{
    public HotkeySpecV2(IReadOnlyList<InputSpec> modifiers, InputSpec mainKey)
    {
        Modifiers = ValidateModifiers(modifiers);
        MainKey = ValidateMainKey(mainKey);
    }

    public IReadOnlyList<InputSpec> Modifiers { get; }
    public InputSpec MainKey { get; }

    public IReadOnlyList<InputSpec> Components => Modifiers.Concat([MainKey]).ToArray();

    public string CanonicalText => string.Join(
        "+",
        Modifiers.Select(modifier => modifier.CanonicalName).Append(MainKey.CanonicalName));

    public static HotkeySpecV2 Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Hotkey must not be empty.", nameof(text));
        }

        return FromNames(text.Split(
            '+',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    public static HotkeySpecV2 FromNames(IEnumerable<string> names)
    {
        var modifiers = new List<InputSpec>();
        InputSpec? mainKey = null;

        foreach (var name in names)
        {
            var spec = InputNameResolverV2.Resolve(name);
            if (spec.CanBeHotkeyModifier)
            {
                modifiers.Add(spec);
                continue;
            }

            if (!spec.CanBeHotkeyMainKey)
            {
                throw new ArgumentException($"{spec.CanonicalName} cannot be used as a hotkey main key.");
            }

            if (mainKey is not null)
            {
                throw new ArgumentException("Hotkey must contain only one main key.");
            }

            mainKey = spec;
        }

        if (mainKey is null)
        {
            throw new ArgumentException("Hotkey must include a non-modifier main key.");
        }

        return new HotkeySpecV2(NormalizeModifierOrder(modifiers), mainKey);
    }

    public static IReadOnlyList<InputSpec> NormalizeAndValidateModifiers(IEnumerable<InputSpec> modifiers)
    {
        var modifierList = modifiers.ToArray();
        foreach (var modifier in modifierList)
        {
            if (!modifier.CanBeHotkeyModifier)
            {
                throw new ArgumentException($"{modifier.CanonicalName} cannot be used as a hotkey modifier.");
            }
        }

        for (var leftIndex = 0; leftIndex < modifierList.Length; leftIndex++)
        {
            for (var rightIndex = leftIndex + 1; rightIndex < modifierList.Length; rightIndex++)
            {
                var left = modifierList[leftIndex];
                var right = modifierList[rightIndex];
                if (InputOverlap.Overlaps(left, right))
                {
                    throw new ArgumentException(
                        $"{left.CanonicalName} overlaps {right.CanonicalName} inside the same hotkey.");
                }
            }
        }

        return modifierList
            .OrderBy(modifier => ModifierSortKey(modifier.CanonicalName))
            .ThenBy(modifier => modifier.CanonicalName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<InputSpec> ValidateModifiers(IReadOnlyList<InputSpec> modifiers)
    {
        return NormalizeAndValidateModifiers(modifiers);
    }

    private static InputSpec ValidateMainKey(InputSpec mainKey)
    {
        if (!mainKey.CanBeHotkeyMainKey)
        {
            throw new ArgumentException($"{mainKey.CanonicalName} cannot be used as a hotkey main key.");
        }

        return mainKey;
    }

    private static IReadOnlyList<InputSpec> NormalizeModifierOrder(IEnumerable<InputSpec> modifiers)
    {
        return NormalizeAndValidateModifiers(modifiers);
    }

    private static int ModifierSortKey(string canonicalName)
    {
        return canonicalName switch
        {
            "ctrl" or "left_ctrl" or "right_ctrl" => 0,
            "shift" or "left_shift" or "right_shift" => 1,
            "alt" or "left_alt" or "right_alt" => 2,
            "win" or "left_win" or "right_win" => 3,
            _ => 10
        };
    }
}
