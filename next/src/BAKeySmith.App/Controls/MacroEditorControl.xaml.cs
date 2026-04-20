using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using BAKeySmith.Core.Scripting;

namespace BAKeySmith.App.Controls;

public partial class MacroEditorControl : UserControl
{
    public static readonly DependencyProperty ScriptTextProperty = DependencyProperty.Register(
        nameof(ScriptText),
        typeof(string),
        typeof(MacroEditorControl),
        new FrameworkPropertyMetadata(
            "loop 2\ntap esc\nend",
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnScriptTextChanged));

    private readonly MacroScriptCompiler _compiler = new();
    private readonly MacroScriptCompletionProvider _completionProvider = new();
    private readonly MacroScriptLanguageService _languageService = new();
    private MacroScriptCompletionResult? _lastCompletion;
    private bool _updatingText;

    public MacroEditorControl()
    {
        InitializeComponent();
        ScriptBox.Text = ScriptText;
        RefreshEditorState();
    }

    public string ScriptText
    {
        get => (string)GetValue(ScriptTextProperty);
        set => SetValue(ScriptTextProperty, value);
    }

    public bool IsScriptValid { get; private set; }
    public MacroScriptCompileResult LastCompileResult { get; private set; } =
        new MacroScriptCompileResult([], []);

    public MacroScriptCompileResult ValidateScript()
    {
        RefreshValidation();
        return LastCompileResult;
    }

    public void FocusEditor()
    {
        ScriptBox.Focus();
    }

    private static void OnScriptTextChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        var editor = (MacroEditorControl)dependencyObject;
        if (editor._updatingText)
        {
            return;
        }

        editor.ScriptBox.Text = eventArgs.NewValue?.ToString() ?? string.Empty;
        editor.RefreshEditorState();
    }

    private void ScriptBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _updatingText = true;
        ScriptText = ScriptBox.Text;
        _updatingText = false;
        RefreshEditorState();
    }

    private void ScriptBox_SelectionChanged(object sender, RoutedEventArgs e)
    {
        RefreshCursorAndCompletions();
    }

    private void ScriptBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab && ApplySelectedCompletion())
        {
            e.Handled = true;
        }
    }

    private void SuggestionList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        _ = ApplySelectedCompletion();
    }

    private void DiagnosticList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DiagnosticList.SelectedItem is DiagnosticDisplayItem { Line: > 0 } item)
        {
            GoToLine(item.Line);
        }
    }

    private void InsertCompletion_Click(object sender, RoutedEventArgs e)
    {
        _ = ApplySelectedCompletion();
    }

    private void RefreshEditorState()
    {
        RefreshLineNumbers();
        RefreshValidation();
        RefreshCursorAndCompletions();
    }

    private void RefreshLineNumbers()
    {
        var lineCount = Math.Max(1, ScriptBox.LineCount);
        LineNumbersText.Text = string.Join(
            Environment.NewLine,
            Enumerable.Range(1, lineCount));
    }

    private void RefreshValidation()
    {
        LastCompileResult = _compiler.Compile(ScriptBox.Text);
        IsScriptValid = LastCompileResult.Success;
        if (LastCompileResult.Success)
        {
            StatusText.Text = $"macro ok · {LastCompileResult.Instructions.Count} instructions";
            StatusText.Foreground = Brush("#0f766e");
            DiagnosticList.ItemsSource = null;
            DiagnosticList.Visibility = Visibility.Collapsed;
            ScriptBox.BorderBrush = Brush("#dbe4ef");
            return;
        }

        var first = LastCompileResult.Diagnostics.FirstOrDefault();
        var prefix = first is null || first.Line <= 0 ? "macro error" : $"line {first.Line}";
        StatusText.Text = $"{prefix}: {string.Join(" / ", LastCompileResult.Errors)}";
        StatusText.Foreground = Brush("#b91c1c");
        DiagnosticList.ItemsSource = LastCompileResult.Diagnostics
            .Select(diagnostic => new DiagnosticDisplayItem(
                diagnostic.Line,
                diagnostic.Line <= 0
                    ? $"global: {diagnostic.Message}"
                    : $"line {diagnostic.Line}: {diagnostic.Message}"))
            .ToArray();
        DiagnosticList.Visibility = Visibility.Visible;
        ScriptBox.BorderBrush = Brush("#b91c1c");
    }

    private void RefreshCursorAndCompletions()
    {
        var line = ScriptBox.GetLineIndexFromCharacterIndex(ScriptBox.CaretIndex) + 1;
        var lineStart = ScriptBox.GetCharacterIndexFromLineIndex(Math.Max(0, line - 1));
        var column = ScriptBox.CaretIndex - lineStart + 1;
        CursorText.Text = $"Ln {line}, Col {column}";

        _lastCompletion = _completionProvider.Complete(
            ScriptBox.Text,
            ScriptBox.CaretIndex);
        SuggestionList.ItemsSource = _lastCompletion.Items;
        if (_lastCompletion.Items.Count > 0)
        {
            SuggestionList.SelectedIndex = 0;
        }

        RefreshTokenPreview(line);
    }

    private bool ApplySelectedCompletion()
    {
        if (_lastCompletion is null || _lastCompletion.Items.Count == 0)
        {
            return false;
        }

        var selected = SuggestionList.SelectedItem as MacroScriptCompletionItem
            ?? _lastCompletion.Items[0];
        var text = ScriptBox.Text;
        var start = Math.Clamp(_lastCompletion.ReplacementStart, 0, text.Length);
        var length = Math.Min(_lastCompletion.ReplacementLength, text.Length - start);
        ScriptBox.Text = text.Remove(start, length).Insert(start, selected.Text);
        ScriptBox.CaretIndex = start + selected.Text.Length;
        ScriptBox.Focus();
        RefreshEditorState();
        return true;
    }

    private void RefreshTokenPreview(int line)
    {
        TokenPreviewText.Inlines.Clear();
        TokenPreviewText.Inlines.Add(new Run("当前行语义  ")
        {
            Foreground = Brush("#64748b")
        });

        var lineText = GetLineText(ScriptBox.Text, line);
        var tokens = _languageService.TokenizeLine(lineText, line);
        if (tokens.Count == 0)
        {
            TokenPreviewText.Inlines.Add(new Run("(空行)")
            {
                Foreground = Brush("#94a3b8")
            });
            return;
        }

        foreach (var token in tokens)
        {
            TokenPreviewText.Inlines.Add(new Run(token.Text)
            {
                Foreground = TokenBrush(token.Kind),
                FontWeight = token.Kind is MacroScriptTokenKind.Command or MacroScriptTokenKind.Invalid
                    ? FontWeights.SemiBold
                    : FontWeights.Normal
            });
            TokenPreviewText.Inlines.Add(new Run($"[{token.Kind}] ")
            {
                Foreground = Brush("#94a3b8")
            });
        }
    }

    private void GoToLine(int line)
    {
        if (line <= 0 || line > ScriptBox.LineCount)
        {
            return;
        }

        var index = ScriptBox.GetCharacterIndexFromLineIndex(line - 1);
        if (index < 0)
        {
            return;
        }

        ScriptBox.Focus();
        ScriptBox.CaretIndex = index;
        ScriptBox.ScrollToLine(line - 1);
    }

    private static string GetLineText(string text, int line)
    {
        var lines = (text ?? string.Empty).Split(
            ["\r\n", "\n"],
            StringSplitOptions.None);
        return line <= 0 || line > lines.Length ? string.Empty : lines[line - 1];
    }

    private static SolidColorBrush TokenBrush(MacroScriptTokenKind kind)
    {
        return kind switch
        {
            MacroScriptTokenKind.Command => Brush("#0f766e"),
            MacroScriptTokenKind.Key => Brush("#1d4ed8"),
            MacroScriptTokenKind.MouseButton => Brush("#7c3aed"),
            MacroScriptTokenKind.Number => Brush("#c2410c"),
            MacroScriptTokenKind.LoopValue => Brush("#c2410c"),
            MacroScriptTokenKind.Flag => Brush("#0369a1"),
            MacroScriptTokenKind.Comment => Brush("#64748b"),
            MacroScriptTokenKind.Invalid => Brush("#b91c1c"),
            _ => Brush("#172033")
        };
    }

    private static SolidColorBrush Brush(string hex)
    {
        return (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
    }

    private sealed record DiagnosticDisplayItem(int Line, string Display);
}
