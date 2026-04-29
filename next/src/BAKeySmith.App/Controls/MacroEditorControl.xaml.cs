using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
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
    private ScrollViewer? _scriptScrollViewer;
    private Window? _hostWindow;
    private MacroCompletionPopupContext? _currentCompletionContext;
    private MacroCompletionPopupContext? _dismissedCompletionContext;
    private bool _suppressNextCompletionOpen;
    private bool _updatingText;

    public MacroEditorControl()
    {
        InitializeComponent();
        ScriptBox.Text = ScriptText;
        Loaded += MacroEditorControl_Loaded;
        Unloaded += MacroEditorControl_Unloaded;
        ScriptBox.LostKeyboardFocus += ScriptBox_LostKeyboardFocus;
        ScriptBox.SizeChanged += ScriptBox_SizeChanged;
        RefreshEditorState(openCompletionIfEligible: false);
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

    public void GoToLine(int line)
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
        RefreshCursorAndCompletions(openIfEligible: false);
    }

    private void MacroEditorControl_Loaded(object sender, RoutedEventArgs e)
    {
        AttachHostWindow();
        AttachScriptScrollViewer();
        RefreshVisibleLineNumbers();
        Dispatcher.BeginInvoke(RefreshVisibleLineNumbers, DispatcherPriority.Loaded);
        RefreshCursorAndCompletions(openIfEligible: false);
    }

    private void MacroEditorControl_Unloaded(object sender, RoutedEventArgs e)
    {
        DetachHostWindow();
        if (_scriptScrollViewer is not null)
        {
            _scriptScrollViewer.ScrollChanged -= ScriptScrollViewer_ScrollChanged;
            _scriptScrollViewer = null;
        }

        CloseCompletionPopup();
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
        editor.RefreshEditorState(openCompletionIfEligible: false);
    }

    private void ScriptBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _updatingText = true;
        ScriptText = ScriptBox.Text;
        _updatingText = false;
        RefreshEditorState(openCompletionIfEligible: true);
    }

    private void ScriptBox_SelectionChanged(object sender, RoutedEventArgs e)
    {
        RefreshVisibleLineNumbers();
        RefreshCursorAndCompletions(openIfEligible: CompletionPopup.IsOpen);
    }

    private void ScriptBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && CompletionPopup.IsOpen)
        {
            _dismissedCompletionContext = _currentCompletionContext;
            CloseCompletionPopup();
            e.Handled = true;
            return;
        }

        if (CompletionPopup.IsOpen &&
            (e.Key == Key.Tab || e.Key == Key.Enter) &&
            ApplySelectedCompletion())
        {
            e.Handled = true;
            return;
        }

        if (CompletionPopup.IsOpen && e.Key is Key.Down or Key.Up)
        {
            MoveCompletionSelection(e.Key == Key.Down ? 1 : -1);
            e.Handled = true;
        }
    }

    private void CompletionList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        _ = ApplySelectedCompletion();
    }

    private void CompletionList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _dismissedCompletionContext = _currentCompletionContext;
            CloseCompletionPopup();
            ScriptBox.Focus();
            e.Handled = true;
            return;
        }

        if ((e.Key == Key.Tab || e.Key == Key.Enter) && ApplySelectedCompletion())
        {
            e.Handled = true;
        }
    }

    private void ScriptBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.NewFocus is DependencyObject dependencyObject &&
            IsWithin(dependencyObject, CompletionPopup.Child))
        {
            return;
        }

        CloseCompletionPopup();
    }

    private void ScriptBox_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RefreshVisibleLineNumbers();
        RepositionCompletionPopup();
    }

    private void ScriptScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        RefreshVisibleLineNumbers();
        RepositionCompletionPopup();
    }

    private void HostWindow_Deactivated(object? sender, EventArgs e)
    {
        CloseCompletionPopup();
    }

    private void RefreshEditorState(bool openCompletionIfEligible)
    {
        RefreshLineNumbers();
        RefreshValidation();
        RefreshCursorAndCompletions(openCompletionIfEligible);
    }

    private void RefreshLineNumbers()
    {
        RefreshVisibleLineNumbers();
    }

    private void RefreshValidation()
    {
        LastCompileResult = _compiler.Compile(ScriptBox.Text);
        IsScriptValid = LastCompileResult.Success;
        if (LastCompileResult.Success)
        {
            StatusText.Text = $"macro ok · {LastCompileResult.Instructions.Count} instructions";
            StatusText.Foreground = Brush("#0f766e");
            ScriptBox.BorderBrush = Brush("#dbe4ef");
            return;
        }

        var first = LastCompileResult.Diagnostics.FirstOrDefault();
        var prefix = first is null || first.Line <= 0 ? "macro error" : $"line {first.Line}";
        StatusText.Text = $"{prefix}: {string.Join(" / ", LastCompileResult.Errors)}";
        StatusText.Foreground = Brush("#b91c1c");
        ScriptBox.BorderBrush = Brush("#b91c1c");
    }

    private void RefreshCursorAndCompletions(bool openIfEligible)
    {
        var lineCount = ScriptBox.LineCount;
        var line = lineCount <= 0
            ? 1
            : ScriptBox.GetLineIndexFromCharacterIndex(ScriptBox.CaretIndex) + 1;
        var lineStart = lineCount <= 0
            ? 0
            : ScriptBox.GetCharacterIndexFromLineIndex(Math.Max(0, line - 1));
        if (lineStart < 0)
        {
            lineStart = 0;
        }

        var column = ScriptBox.CaretIndex - lineStart + 1;
        CursorText.Text = $"Ln {line}, Col {column}";

        _lastCompletion = _completionProvider.Complete(
            ScriptBox.Text,
            ScriptBox.CaretIndex);
        CompletionList.ItemsSource = _lastCompletion.Items;
        if (_lastCompletion.Items.Count > 0)
        {
            CompletionList.SelectedIndex = 0;
        }

        var suppressOpen = _suppressNextCompletionOpen || !openIfEligible;
        var decision = MacroCompletionPopupPolicy.Evaluate(
            ScriptBox.Text,
            ScriptBox.CaretIndex,
            _lastCompletion,
            _dismissedCompletionContext,
            suppressOpen);
        _suppressNextCompletionOpen = false;
        _currentCompletionContext = decision.Context;
        if (decision.ShouldOpen)
        {
            OpenOrRepositionCompletionPopup();
        }
        else
        {
            CloseCompletionPopup();
        }

        RefreshTokenPreview(line);
    }

    private bool ApplySelectedCompletion()
    {
        if (_lastCompletion is null || _lastCompletion.Items.Count == 0)
        {
            return false;
        }

        var selected = CompletionList.SelectedItem as MacroScriptCompletionItem
            ?? _lastCompletion.Items[0];
        var text = ScriptBox.Text;
        var start = Math.Clamp(_lastCompletion.ReplacementStart, 0, text.Length);
        var length = Math.Min(_lastCompletion.ReplacementLength, text.Length - start);
        _suppressNextCompletionOpen = true;
        _dismissedCompletionContext = null;
        ScriptBox.Text = text.Remove(start, length).Insert(start, selected.Text);
        ScriptBox.CaretIndex = start + selected.Text.Length;
        ScriptBox.Focus();
        CloseCompletionPopup();
        RefreshEditorState(openCompletionIfEligible: false);
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

    private void AttachScriptScrollViewer()
    {
        if (_scriptScrollViewer is not null)
        {
            return;
        }

        ScriptBox.ApplyTemplate();
        _scriptScrollViewer = ScriptBox.Template.FindName("PART_ContentHost", ScriptBox) as ScrollViewer
            ?? FindVisualChild<ScrollViewer>(ScriptBox);
        if (_scriptScrollViewer is null)
        {
            return;
        }

        _scriptScrollViewer.ScrollChanged += ScriptScrollViewer_ScrollChanged;
    }

    private void AttachHostWindow()
    {
        var window = Window.GetWindow(this);
        if (ReferenceEquals(_hostWindow, window))
        {
            return;
        }

        DetachHostWindow();
        _hostWindow = window;
        if (_hostWindow is not null)
        {
            _hostWindow.Deactivated += HostWindow_Deactivated;
        }
    }

    private void DetachHostWindow()
    {
        if (_hostWindow is null)
        {
            return;
        }

        _hostWindow.Deactivated -= HostWindow_Deactivated;
        _hostWindow = null;
    }

    private void RepositionCompletionPopup()
    {
        if (CompletionPopup.IsOpen)
        {
            OpenOrRepositionCompletionPopup();
        }
    }

    private void OpenOrRepositionCompletionPopup()
    {
        if (_lastCompletion is null ||
            _lastCompletion.Items.Count == 0 ||
            !ScriptBox.IsKeyboardFocusWithin)
        {
            CloseCompletionPopup();
            return;
        }

        var caretRect = ScriptBox.GetRectFromCharacterIndex(
            Math.Clamp(ScriptBox.CaretIndex, 0, ScriptBox.Text.Length),
            trailingEdge: true);
        if (caretRect.IsEmpty)
        {
            CloseCompletionPopup();
            return;
        }

        CompletionPopup.HorizontalOffset = Math.Max(0, caretRect.X);
        CompletionPopup.VerticalOffset = Math.Min(
            ScriptBox.ActualHeight,
            Math.Max(0, caretRect.Bottom + 4));
        CompletionPopupBorder.MaxWidth = Math.Max(220, Math.Min(360, ScriptBox.ActualWidth - 12));
        CompletionPopup.IsOpen = true;
    }

    private void CloseCompletionPopup()
    {
        CompletionPopup.IsOpen = false;
    }

    private void MoveCompletionSelection(int delta)
    {
        if (CompletionList.Items.Count == 0)
        {
            return;
        }

        var current = CompletionList.SelectedIndex < 0 ? 0 : CompletionList.SelectedIndex;
        var next = Math.Clamp(current + delta, 0, CompletionList.Items.Count - 1);
        CompletionList.SelectedIndex = next;
        CompletionList.ScrollIntoView(CompletionList.SelectedItem);
    }

    private static T? FindVisualChild<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
            {
                return match;
            }

            var descendant = FindVisualChild<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private static bool IsWithin(DependencyObject child, object? possibleParent)
    {
        if (possibleParent is not DependencyObject parent)
        {
            return false;
        }

        var current = child;
        while (current is not null)
        {
            if (ReferenceEquals(current, parent))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private void RefreshVisibleLineNumbers()
    {
        if (LineNumberCanvas is null || ScriptBox is null)
        {
            return;
        }

        LineNumberCanvas.Children.Clear();
        var viewport = GetTextContentViewport();
        LineNumberViewport.Margin = new Thickness(0, viewport.Top, 0, 0);
        LineNumberViewport.Height = Math.Max(0, viewport.Height);
        LineNumberCanvas.Width = Math.Max(0, LineNumberViewport.ActualWidth);
        LineNumberCanvas.Height = Math.Max(0, viewport.Height);

        var lineCount = ScriptBox.LineCount;
        if (lineCount <= 0)
        {
            return;
        }

        var viewportTop = viewport.Top;
        var viewportBottom = viewport.Bottom;
        for (var lineIndex = 0; lineIndex < lineCount; lineIndex++)
        {
            var characterIndex = ScriptBox.GetCharacterIndexFromLineIndex(lineIndex);
            if (characterIndex < 0)
            {
                continue;
            }

            var rect = ScriptBox.GetRectFromCharacterIndex(characterIndex);
            if (rect.IsEmpty || rect.Bottom < viewportTop || rect.Top > viewportBottom)
            {
                continue;
            }

            var lineHeight = double.IsNaN(rect.Height) || rect.Height <= 0 ? ScriptBox.FontSize * 1.4 : rect.Height;
            var lineNumber = new TextBlock
            {
                Text = (lineIndex + 1).ToString(),
                Width = Math.Max(0, LineNumberViewport.ActualWidth - 10),
                Height = lineHeight,
                LineHeight = lineHeight,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                Foreground = Brush("#64748b"),
                FontFamily = ScriptBox.FontFamily,
                FontSize = ScriptBox.FontSize,
                TextAlignment = TextAlignment.Right
            };
            Canvas.SetLeft(lineNumber, 0);
            Canvas.SetTop(lineNumber, rect.Top - viewport.Top);
            LineNumberCanvas.Children.Add(lineNumber);
        }
    }

    private Rect GetTextContentViewport()
    {
        if (_scriptScrollViewer is null)
        {
            AttachScriptScrollViewer();
        }

        if (_scriptScrollViewer is null ||
            !ScriptBox.IsLoaded ||
            _scriptScrollViewer.ActualHeight <= 0)
        {
            return new Rect(0, 0, Math.Max(0, ScriptBox.ActualWidth), Math.Max(0, ScriptBox.ActualHeight));
        }

        var origin = _scriptScrollViewer.TransformToAncestor(ScriptBox).Transform(new Point(0, 0));
        var height = _scriptScrollViewer.ViewportHeight;
        if (double.IsNaN(height) || double.IsInfinity(height) || height <= 0)
        {
            height = _scriptScrollViewer.ActualHeight;
        }

        return new Rect(
            origin.X,
            origin.Y,
            Math.Max(0, _scriptScrollViewer.ViewportWidth),
            Math.Max(0, height));
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
}
