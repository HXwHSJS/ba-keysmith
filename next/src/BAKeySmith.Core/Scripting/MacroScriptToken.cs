namespace BAKeySmith.Core.Scripting;

public sealed record MacroScriptToken(
    int Line,
    int Column,
    string Text,
    MacroScriptTokenKind Kind);
