using BAKeySmith.Core.Scripting;

namespace BAKeySmith.App.Controls;

public static class MacroCompletionPopupPolicy
{
    public static MacroCompletionPopupDecision Evaluate(
        string scriptText,
        int caretIndex,
        MacroScriptCompletionResult? completion,
        MacroCompletionPopupContext? dismissedContext = null,
        bool suppressOpen = false)
    {
        var context = CreateContext(scriptText, caretIndex, completion);
        if (suppressOpen ||
            completion is null ||
            completion.Items.Count == 0 ||
            context.IsInComment ||
            dismissedContext == context ||
            IsCompleteUniqueMatch(context, completion))
        {
            return new MacroCompletionPopupDecision(false, context);
        }

        if (context.IsCommandPosition)
        {
            return new MacroCompletionPopupDecision(true, context);
        }

        return new MacroCompletionPopupDecision(
            context.AfterWhitespace || !string.IsNullOrWhiteSpace(context.Prefix),
            context);
    }

    public static MacroCompletionPopupContext CreateContext(
        string scriptText,
        int caretIndex,
        MacroScriptCompletionResult? completion)
    {
        scriptText ??= string.Empty;
        caretIndex = Math.Clamp(caretIndex, 0, scriptText.Length);
        var lineStart = 0;
        if (caretIndex > 0)
        {
            var previousNewline = scriptText.LastIndexOf('\n', caretIndex - 1);
            lineStart = previousNewline < 0 ? 0 : previousNewline + 1;
        }
        var currentLine = scriptText[lineStart..caretIndex];
        var commentIndex = currentLine.IndexOf('#');
        if (commentIndex >= 0)
        {
            return new MacroCompletionPopupContext(
                scriptText,
                caretIndex,
                completion?.ReplacementStart ?? caretIndex,
                completion?.ReplacementLength ?? 0,
                string.Empty,
                IsCommandPosition: false,
                AfterWhitespace: false,
                IsInComment: true);
        }

        var afterWhitespace = currentLine.Length > 0 && char.IsWhiteSpace(currentLine[^1]);
        var tokens = currentLine
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim().ToLowerInvariant())
            .ToArray();
        var isCommandPosition = tokens.Length == 0 || (tokens.Length == 1 && !afterWhitespace);
        var prefix = afterWhitespace || tokens.Length == 0
            ? string.Empty
            : tokens[^1];

        return new MacroCompletionPopupContext(
            scriptText,
            caretIndex,
            completion?.ReplacementStart ?? caretIndex,
            completion?.ReplacementLength ?? prefix.Length,
            prefix,
            isCommandPosition,
            afterWhitespace,
            IsInComment: false);
    }

    private static bool IsCompleteUniqueMatch(
        MacroCompletionPopupContext context,
        MacroScriptCompletionResult completion)
    {
        return !string.IsNullOrWhiteSpace(context.Prefix) &&
            completion.Items.Count == 1 &&
            string.Equals(completion.Items[0].Text, context.Prefix, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record MacroCompletionPopupDecision(
    bool ShouldOpen,
    MacroCompletionPopupContext Context);

public sealed record MacroCompletionPopupContext(
    string ScriptText,
    int CaretIndex,
    int ReplacementStart,
    int ReplacementLength,
    string Prefix,
    bool IsCommandPosition,
    bool AfterWhitespace,
    bool IsInComment);
