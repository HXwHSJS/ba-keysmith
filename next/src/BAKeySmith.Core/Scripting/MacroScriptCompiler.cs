using System.Globalization;
using BAKeySmith.Core.Contracts;
using BAKeySmith.Core.Input;

namespace BAKeySmith.Core.Scripting;

public sealed class MacroScriptCompiler
{
    public MacroScriptCompileResult Compile(string scriptText)
    {
        var instructions = new List<MacroInstruction>();
        var errors = new List<string>();
        var diagnostics = new List<MacroScriptDiagnostic>();
        var loopStack = new Stack<int>();
        var lines = (scriptText ?? string.Empty).Split(
            ["\r\n", "\n"],
            StringSplitOptions.None);

        for (var lineNumber = 1; lineNumber <= lines.Length; lineNumber++)
        {
            var line = StripComment(lines[lineNumber - 1]).Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            var command = parts[0].Trim().ToLowerInvariant();
            var args = parts.Skip(1).Select(arg => arg.Trim().ToLowerInvariant()).ToArray();

            try
            {
                switch (command)
                {
                    case "press":
                    case "release":
                    case "tap":
                        RequireArgCount(command, args, 1);
                        instructions.Add(new MacroInstruction(
                            command,
                            [NormalizeInputName(args[0])],
                            lineNumber));
                        break;
                    case "wait":
                        RequireArgCount(command, args, 1);
                        var wait = ParseNonNegativeDouble(args[0], "wait");
                        instructions.Add(new MacroInstruction(
                            "wait",
                            [wait.ToString("0.###", CultureInfo.InvariantCulture)],
                            lineNumber));
                        break;
                    case "loop":
                        RequireArgCount(command, args, 1);
                        var count = ParseLoopCount(args[0]);
                        loopStack.Push(instructions.Count);
                        instructions.Add(new MacroInstruction(
                            "loop_start",
                            [count.ToString(CultureInfo.InvariantCulture), "-1"],
                            lineNumber));
                        break;
                    case "end":
                        RequireArgCount(command, args, 0);
                        if (!loopStack.TryPop(out var startIndex))
                        {
                            throw new ArgumentException("多余的 end，没有对应的 loop");
                        }

                        var endIndex = instructions.Count;
                        var start = instructions[startIndex];
                        instructions[startIndex] = start with
                        {
                            Args = [start.Args[0], endIndex.ToString(CultureInfo.InvariantCulture)]
                        };
                        instructions.Add(new MacroInstruction(
                            "loop_end",
                            [startIndex.ToString(CultureInfo.InvariantCulture)],
                            lineNumber));
                        break;
                    case "combo":
                        if (args.Length < 1)
                        {
                            throw new ArgumentException("combo 至少需要一个键名");
                        }

                        instructions.Add(new MacroInstruction(
                            "combo",
                            args.Select(NormalizeInputName).ToArray(),
                            lineNumber));
                        break;
                    case "setpos":
                        RequireArgCount(command, args, 2, 3);
                        var setX = ParseDouble(args[0], "setpos x");
                        var setY = ParseDouble(args[1], "setpos y");
                        var useMouseScale = args.Length == 3 && args[2] == "mouse";
                        instructions.Add(new MacroInstruction(
                            "setpos",
                            [
                                setX.ToString("0.###", CultureInfo.InvariantCulture),
                                setY.ToString("0.###", CultureInfo.InvariantCulture),
                                useMouseScale ? "true" : "false"
                            ],
                            lineNumber));
                        break;
                    case "setpos_rel":
                        RequireArgCount(command, args, 2);
                        instructions.Add(new MacroInstruction(
                            "setpos_rel",
                            [
                                ParseInteger(args[0], "setpos_rel dx").ToString(CultureInfo.InvariantCulture),
                                ParseInteger(args[1], "setpos_rel dy").ToString(CultureInfo.InvariantCulture)
                            ],
                            lineNumber));
                        break;
                    case "drag":
                        CompileDrag(args, instructions, lineNumber);
                        break;
                    case "drag_rel":
                        CompileDragRel(args, instructions, lineNumber);
                        break;
                    default:
                        throw new ArgumentException($"未知指令: {command}");
                }
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
            {
                errors.Add($"第 {lineNumber} 行: {ex.Message}");
                diagnostics.Add(new MacroScriptDiagnostic(lineNumber, ex.Message));
            }
        }

        if (loopStack.Count > 0)
        {
            const string message = "存在未闭合的 loop（缺少 end）";
            errors.Add(message);
            diagnostics.Add(new MacroScriptDiagnostic(0, message));
        }

        return new MacroScriptCompileResult(instructions, errors)
        {
            Diagnostics = diagnostics
        };
    }

    private static void CompileDrag(string[] args, List<MacroInstruction> instructions, int lineNumber)
    {
        var working = args.ToList();
        var useMouseScale = working.Remove("mouse");
        var button = "mouse_left";
        if (working.Count >= 3 && TryNormalizeMouseButton(working[^1], out var parsedButton))
        {
            button = parsedButton;
            working.RemoveAt(working.Count - 1);
        }
        else if (working.Count >= 3 && LooksLikeMouseToken(working[^1]))
        {
            throw new ArgumentException($"Unsupported mouse button: {working[^1]}");
        }

        if (working.Count != 2)
        {
            throw new ArgumentException("drag 需要 x 和 y 参数");
        }

        instructions.Add(new MacroInstruction(
            "drag",
            [
                ParseDouble(working[0], "drag x").ToString("0.###", CultureInfo.InvariantCulture),
                ParseDouble(working[1], "drag y").ToString("0.###", CultureInfo.InvariantCulture),
                useMouseScale ? "true" : "false",
                button
            ],
            lineNumber));
    }

    private static void CompileDragRel(string[] args, List<MacroInstruction> instructions, int lineNumber)
    {
        var working = args.ToList();
        var button = "mouse_left";
        if (working.Count >= 3 && TryNormalizeMouseButton(working[^1], out var parsedButton))
        {
            button = parsedButton;
            working.RemoveAt(working.Count - 1);
        }
        else if (working.Count >= 3 && LooksLikeMouseToken(working[^1]))
        {
            throw new ArgumentException($"Unsupported mouse button: {working[^1]}");
        }

        if (working.Count != 2)
        {
            throw new ArgumentException("drag_rel 需要 dx 和 dy 参数");
        }

        instructions.Add(new MacroInstruction(
            "drag_rel",
            [
                ParseInteger(working[0], "drag_rel dx").ToString(CultureInfo.InvariantCulture),
                ParseInteger(working[1], "drag_rel dy").ToString(CultureInfo.InvariantCulture),
                button
            ],
            lineNumber));
    }

    private static string NormalizeInputName(string name)
    {
        if (TryNormalizeMouseButton(name, out var mouseButton))
        {
            return mouseButton;
        }

        if (KeyNameResolver.IsMouseTrigger(name))
        {
            throw new ArgumentException($"Unsupported mouse button: {name}");
        }

        return KeyNameResolver.ResolveKeyboardKey(name).Name;
    }

    private static bool TryNormalizeMouseButton(string name, out string normalized)
    {
        try
        {
            normalized = KeyNameResolver.NormalizeMouseButton(name);
            return true;
        }
        catch
        {
            normalized = string.Empty;
            return false;
        }
    }

    private static string StripComment(string line)
    {
        var index = line.IndexOf('#');
        return index < 0 ? line : line[..index];
    }

    private static bool LooksLikeMouseToken(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized.StartsWith("mouse_", StringComparison.OrdinalIgnoreCase) ||
            normalized is "wheel_up" or "wheel_down" or "wheelup" or "wheeldown";
    }

    private static void RequireArgCount(string command, string[] args, int expected)
    {
        if (args.Length != expected)
        {
            throw new ArgumentException($"{command} 需要 {expected} 个参数");
        }
    }

    private static void RequireArgCount(string command, string[] args, int min, int max)
    {
        if (args.Length < min || args.Length > max)
        {
            throw new ArgumentException($"{command} 需要 {min} 到 {max} 个参数");
        }
    }

    private static int ParseLoopCount(string value)
    {
        if (value is "0" or "infinite")
        {
            return 0;
        }

        var count = ParseInteger(value, "loop");
        if (count <= 0)
        {
            throw new ArgumentException("loop 次数必须大于 0，或使用 0/infinite 表示无限");
        }

        return count;
    }

    private static int ParseInteger(string value, string field)
    {
        return int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    private static double ParseDouble(string value, string field)
    {
        return double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    private static double ParseNonNegativeDouble(string value, string field)
    {
        var parsed = ParseDouble(value, field);
        if (parsed < 0)
        {
            throw new ArgumentException($"{field} 时间不能为负数");
        }

        return parsed;
    }
}
