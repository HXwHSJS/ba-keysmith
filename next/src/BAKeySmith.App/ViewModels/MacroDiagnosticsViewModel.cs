using BAKeySmith.Core.Scripting;

namespace BAKeySmith.App.ViewModels;

public sealed class MacroDiagnosticsViewModel : ViewModelBase
{
    private readonly MacroScriptCompiler _compiler = new();
    private IReadOnlyList<MacroDiagnosticDisplayItem> _diagnostics = [];
    private IReadOnlyList<string> _errors = [];
    private bool _isActive;
    private bool _isValid = true;
    private int _instructionCount;
    private string _summary = "macro diagnostics inactive";
    private string _firstError = string.Empty;

    public bool IsActive
    {
        get => _isActive;
        private set => SetProperty(ref _isActive, value);
    }

    public bool IsValid
    {
        get => _isValid;
        private set => SetProperty(ref _isValid, value);
    }

    public int InstructionCount
    {
        get => _instructionCount;
        private set => SetProperty(ref _instructionCount, value);
    }

    public IReadOnlyList<string> Errors
    {
        get => _errors;
        private set => SetProperty(ref _errors, value);
    }

    public IReadOnlyList<MacroDiagnosticDisplayItem> Diagnostics
    {
        get => _diagnostics;
        private set => SetProperty(ref _diagnostics, value);
    }

    public string Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    public string FirstError
    {
        get => _firstError;
        private set => SetProperty(ref _firstError, value);
    }

    public int DiagnosticCount => Diagnostics.Count;

    public void UpdateForMapping(bool isMacro, string script)
    {
        if (!isMacro)
        {
            ClearForSimpleMapping();
            return;
        }

        _ = Validate(script);
    }

    public MacroScriptCompileResult Validate(string script)
    {
        var result = _compiler.Compile(script ?? string.Empty);
        IsActive = true;
        IsValid = result.Success;
        InstructionCount = result.Instructions.Count;
        Errors = result.Errors;
        FirstError = result.Errors.FirstOrDefault() ?? string.Empty;
        Diagnostics = result.Diagnostics
            .Select(diagnostic => new MacroDiagnosticDisplayItem(
                diagnostic.Line,
                diagnostic.Message,
                diagnostic.Line <= 0
                    ? $"global: {diagnostic.Message}"
                    : $"line {diagnostic.Line}: {diagnostic.Message}"))
            .ToArray();
        Summary = result.Success
            ? $"macro valid · {InstructionCount} instructions"
            : string.IsNullOrWhiteSpace(FirstError)
                ? "macro invalid"
                : $"macro invalid · {FirstError}";
        OnPropertyChanged(nameof(DiagnosticCount));
        return result;
    }

    public void ClearForSimpleMapping()
    {
        IsActive = false;
        IsValid = true;
        InstructionCount = 0;
        Errors = [];
        FirstError = string.Empty;
        Diagnostics = [];
        Summary = "simple mapping · macro diagnostics inactive";
        OnPropertyChanged(nameof(DiagnosticCount));
    }
}

public sealed record MacroDiagnosticDisplayItem(int Line, string Message, string Display);
