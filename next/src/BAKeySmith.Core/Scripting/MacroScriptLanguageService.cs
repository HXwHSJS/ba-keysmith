using System.Globalization;
using BAKeySmith.Core.Input;

namespace BAKeySmith.Core.Scripting;

public sealed class MacroScriptLanguageService
{
    public IReadOnlyList<MacroScriptToken> Tokenize(string scriptText)
    {
        var tokens = new List<MacroScriptToken>();
        var lines = (scriptText ?? string.Empty).Split(
            ["\r\n", "\n"],
            StringSplitOptions.None);

        for (var index = 0; index < lines.Length; index++)
        {
            tokens.AddRange(TokenizeLine(lines[index], index + 1));
        }

        return tokens;
    }

    public IReadOnlyList<MacroScriptToken> TokenizeLine(string line, int lineNumber = 1)
    {
        line ??= string.Empty;
        var tokens = new List<MacroScriptToken>();
        var commentIndex = line.IndexOf('#');
        var code = commentIndex >= 0 ? line[..commentIndex] : line;
        var codeTokens = ReadCodeTokens(code);

        if (codeTokens.Count > 0)
        {
            var command = codeTokens[0].Text.ToLowerInvariant();
            tokens.Add(codeTokens[0] with
            {
                Line = lineNumber,
                Kind = MacroScriptLanguageCatalog.IsCommand(command)
                    ? MacroScriptTokenKind.Command
                    : MacroScriptTokenKind.Invalid
            });

            for (var index = 1; index < codeTokens.Count; index++)
            {
                var token = codeTokens[index];
                tokens.Add(token with
                {
                    Line = lineNumber,
                    Kind = ClassifyArgument(command, index - 1, token.Text)
                });
            }
        }

        if (commentIndex >= 0)
        {
            tokens.Add(new MacroScriptToken(
                lineNumber,
                commentIndex + 1,
                line[commentIndex..],
                MacroScriptTokenKind.Comment));
        }

        return tokens;
    }

    private static MacroScriptTokenKind ClassifyArgument(string command, int argIndex, string text)
    {
        return command switch
        {
            "press" or "release" or "tap" or "combo" => ClassifyInput(text),
            "wait" => IsNonNegativeDouble(text) ? MacroScriptTokenKind.Number : MacroScriptTokenKind.Invalid,
            "loop" => IsLoopValue(text) ? MacroScriptTokenKind.LoopValue : MacroScriptTokenKind.Invalid,
            "setpos" => argIndex <= 1
                ? ClassifyDouble(text)
                : IsMouseScaleFlag(text)
                    ? MacroScriptTokenKind.Flag
                    : MacroScriptTokenKind.Invalid,
            "setpos_rel" => argIndex <= 1
                ? ClassifyInteger(text)
                : MacroScriptTokenKind.Invalid,
            "drag" => ClassifyDragArgument(text),
            "drag_rel" => argIndex <= 1
                ? ClassifyInteger(text)
                : ClassifyMouseButton(text),
            _ => MacroScriptTokenKind.Text
        };
    }

    private static MacroScriptTokenKind ClassifyInput(string text)
    {
        var mouse = ClassifyMouseButton(text);
        if (mouse != MacroScriptTokenKind.Invalid)
        {
            return mouse;
        }

        return KeyNameResolver.TryResolveKeyboardKey(text, out _)
            ? MacroScriptTokenKind.Key
            : MacroScriptTokenKind.Invalid;
    }

    private static MacroScriptTokenKind ClassifyMouseButton(string text)
    {
        try
        {
            _ = KeyNameResolver.NormalizeMouseButton(text);
            return MacroScriptTokenKind.MouseButton;
        }
        catch (ArgumentException)
        {
            return MacroScriptTokenKind.Invalid;
        }
    }

    private static MacroScriptTokenKind ClassifyDouble(string text)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
            ? MacroScriptTokenKind.Number
            : MacroScriptTokenKind.Invalid;
    }

    private static MacroScriptTokenKind ClassifyInteger(string text)
    {
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            ? MacroScriptTokenKind.Number
            : MacroScriptTokenKind.Invalid;
    }

    private static MacroScriptTokenKind ClassifyDragArgument(string text)
    {
        if (IsMouseScaleFlag(text))
        {
            return MacroScriptTokenKind.Flag;
        }

        var mouse = ClassifyMouseButton(text);
        if (mouse != MacroScriptTokenKind.Invalid)
        {
            return mouse;
        }

        return ClassifyDouble(text);
    }

    private static bool IsMouseScaleFlag(string text)
    {
        return string.Equals(text, "mouse", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNonNegativeDouble(string text)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
            value >= 0;
    }

    private static bool IsLoopValue(string text)
    {
        if (string.Equals(text, "infinite", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) &&
            count >= 0;
    }

    private static List<MacroScriptToken> ReadCodeTokens(string code)
    {
        var tokens = new List<MacroScriptToken>();
        var index = 0;
        while (index < code.Length)
        {
            while (index < code.Length && char.IsWhiteSpace(code[index]))
            {
                index++;
            }

            if (index >= code.Length)
            {
                break;
            }

            var start = index;
            while (index < code.Length && !char.IsWhiteSpace(code[index]))
            {
                index++;
            }

            tokens.Add(new MacroScriptToken(
                0,
                start + 1,
                code[start..index],
                MacroScriptTokenKind.Text));
        }

        return tokens;
    }
}
