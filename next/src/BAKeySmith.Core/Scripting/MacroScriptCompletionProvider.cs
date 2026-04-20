namespace BAKeySmith.Core.Scripting;

public sealed class MacroScriptCompletionProvider
{
    public MacroScriptCompletionResult Complete(
        string scriptText,
        int caretIndex,
        int maxItems = 16)
    {
        scriptText ??= string.Empty;
        caretIndex = Math.Clamp(caretIndex, 0, scriptText.Length);
        var lineStart = scriptText.LastIndexOf('\n', Math.Max(0, caretIndex - 1));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;
        var currentLine = scriptText[lineStart..caretIndex];
        var commentIndex = currentLine.IndexOf('#');
        if (commentIndex >= 0)
        {
            return new MacroScriptCompletionResult(caretIndex, 0, []);
        }

        var token = GetTokenContext(currentLine, caretIndex, lineStart);
        var candidates = ChooseCandidates(token.Command, token.ArgIndex, token.IsCommandPosition);
        var items = candidates
            .Where(item => item.Text.StartsWith(token.Prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.Text.Length)
            .ThenBy(item => item.Text, StringComparer.OrdinalIgnoreCase)
            .Take(maxItems)
            .ToArray();

        return new MacroScriptCompletionResult(
            token.ReplacementStart,
            token.Prefix.Length,
            items);
    }

    private static IReadOnlyList<MacroScriptCompletionItem> ChooseCandidates(
        string command,
        int argIndex,
        bool isCommandPosition)
    {
        if (isCommandPosition)
        {
            return MacroScriptLanguageCatalog.Commands;
        }

        return command switch
        {
            "press" or "release" or "tap" => argIndex == 0 ? MacroScriptLanguageCatalog.KeyValues : [],
            "combo" => MacroScriptLanguageCatalog.KeyValues,
            "wait" => argIndex == 0 ? MacroScriptLanguageCatalog.WaitValues : [],
            "loop" => argIndex == 0 ? MacroScriptLanguageCatalog.LoopValues : [],
            "drag" => argIndex <= 1
                ? MacroScriptLanguageCatalog.PositionValues
                : argIndex == 2
                    ? MacroScriptLanguageCatalog.MouseScaleValues.Concat(MacroScriptLanguageCatalog.MouseButtons).ToArray()
                    : argIndex == 3
                        ? MacroScriptLanguageCatalog.MouseButtons
                        : [],
            "drag_rel" => argIndex <= 1
                ? MacroScriptLanguageCatalog.PositionValues
                : argIndex == 2
                    ? MacroScriptLanguageCatalog.MouseButtons
                    : [],
            "setpos" => argIndex <= 1
                ? MacroScriptLanguageCatalog.PositionValues
                : argIndex == 2
                    ? MacroScriptLanguageCatalog.MouseScaleValues
                    : [],
            "setpos_rel" => argIndex <= 1 ? MacroScriptLanguageCatalog.PositionValues : [],
            _ => []
        };
    }

    private static TokenContext GetTokenContext(
        string currentLine,
        int caretIndex,
        int lineStart)
    {
        var afterWhitespace = currentLine.Length > 0 && char.IsWhiteSpace(currentLine[^1]);
        var tokens = currentLine
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim().ToLowerInvariant())
            .ToArray();

        if (tokens.Length == 0)
        {
            return new TokenContext(
                string.Empty,
                string.Empty,
                0,
                caretIndex,
                true);
        }

        if (tokens.Length == 1 && !afterWhitespace)
        {
            var prefixStart = FindPrefixStart(currentLine, caretIndex, lineStart);
            return new TokenContext(
                string.Empty,
                tokens[0],
                0,
                prefixStart,
                true);
        }

        var command = tokens[0];
        var prefix = afterWhitespace ? string.Empty : tokens[^1];
        var argIndex = afterWhitespace ? tokens.Length - 1 : tokens.Length - 2;
        return new TokenContext(
            command,
            prefix,
            Math.Max(0, argIndex),
            afterWhitespace ? caretIndex : FindPrefixStart(currentLine, caretIndex, lineStart),
            false);
    }

    private static int FindPrefixStart(string currentLine, int caretIndex, int lineStart)
    {
        var local = currentLine.Length - 1;
        while (local >= 0 && !char.IsWhiteSpace(currentLine[local]))
        {
            local--;
        }

        return lineStart + local + 1;
    }

    private sealed record TokenContext(
        string Command,
        string Prefix,
        int ArgIndex,
        int ReplacementStart,
        bool IsCommandPosition);
}
