namespace BAKeySmith.Core.Contracts;

public sealed record TriggerSpec(string Device, string Code)
{
    public string NormalizedDevice => Normalize(Device);
    public string NormalizedCode => Normalize(Code);
    public string Key => $"{NormalizedDevice}:{NormalizedCode}";

    public static TriggerSpec Keyboard(string key) => new("keyboard", key);
    public static TriggerSpec Mouse(string button) => new("mouse", button);

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Trigger fields must not be empty.", nameof(value));
        }

        return value.Trim().ToLowerInvariant();
    }
}
