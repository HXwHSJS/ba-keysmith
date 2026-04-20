using BAKeySmith.Core.Contracts;

namespace BAKeySmith.Core.Scripting;

public sealed record MacroScriptCompileResult(
    IReadOnlyList<MacroInstruction> Instructions,
    IReadOnlyList<string> Errors)
{
    public bool Success => Errors.Count == 0;
    public IReadOnlyList<MacroScriptDiagnostic> Diagnostics { get; init; } = Array.Empty<MacroScriptDiagnostic>();
}
