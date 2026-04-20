namespace BAKeySmith.Core.Scripting;

public sealed record MacroScriptCompletionResult(
    int ReplacementStart,
    int ReplacementLength,
    IReadOnlyList<MacroScriptCompletionItem> Items)
{
    public bool HasItems => Items.Count > 0;
}
