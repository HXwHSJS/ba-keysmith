namespace BAKeySmith.Core.Input.V2.Conflicts;

public sealed record InputBindingV2(
    string Id,
    string Name,
    InputBindingRole Role,
    InputSpec? SingleInput,
    HotkeySpecV2? Hotkey)
{
    public bool IsHotkey => Hotkey is not null;

    public IReadOnlyList<InputSpec> Components => Hotkey?.Components ??
        (SingleInput is null ? [] : [SingleInput]);

    public static InputBindingV2 Single(
        string id,
        InputBindingRole role,
        string inputName,
        string? name = null)
    {
        return new InputBindingV2(
            NormalizeId(id),
            string.IsNullOrWhiteSpace(name) ? NormalizeId(id) : name.Trim(),
            role,
            InputNameResolverV2.Resolve(inputName),
            Hotkey: null);
    }

    public static InputBindingV2 HotkeyCombo(
        string id,
        InputBindingRole role,
        string hotkeyText,
        string? name = null)
    {
        return new InputBindingV2(
            NormalizeId(id),
            string.IsNullOrWhiteSpace(name) ? NormalizeId(id) : name.Trim(),
            role,
            SingleInput: null,
            HotkeySpecV2.Parse(hotkeyText));
    }

    private static string NormalizeId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Binding id must not be empty.", nameof(id));
        }

        return id.Trim();
    }
}
