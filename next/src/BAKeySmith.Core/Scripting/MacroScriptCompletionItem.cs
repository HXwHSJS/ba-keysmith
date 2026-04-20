namespace BAKeySmith.Core.Scripting;

public sealed record MacroScriptCompletionItem(
    string Text,
    string Kind,
    string Detail)
{
    public string Display => $"{Text}  ·  {Detail}";
}
