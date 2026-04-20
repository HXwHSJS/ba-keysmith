using BAKeySmith.Core.Input;

namespace BAKeySmith.Core.Scripting;

public static class MacroScriptLanguageCatalog
{
    private static readonly HashSet<string> CommandNames = new(StringComparer.OrdinalIgnoreCase);

    static MacroScriptLanguageCatalog()
    {
        foreach (var command in Commands)
        {
            CommandNames.Add(command.Text);
        }
    }

    public static IReadOnlyList<MacroScriptCompletionItem> Commands { get; } =
    [
        new("press", "command", "按下一个键，直到 release/stop"),
        new("release", "command", "释放一个由宏按下的键"),
        new("tap", "command", "统一 tap：按下并短暂保持后释放"),
        new("wait", "command", "等待毫秒，支持小数"),
        new("loop", "command", "开始循环，0/infinite 表示无限"),
        new("end", "command", "结束最近的 loop"),
        new("combo", "command", "组合键，按顺序按下、逆序释放"),
        new("drag", "command", "拖拽到绝对坐标，可选 mouse 比例"),
        new("drag_rel", "command", "按相对位移拖拽"),
        new("setpos", "command", "移动鼠标到绝对坐标"),
        new("setpos_rel", "command", "移动鼠标相对位移")
    ];

    public static IReadOnlyList<MacroScriptCompletionItem> WaitValues { get; } =
    [
        new("0.5", "time", "0.5 ms"),
        new("1", "time", "1 ms"),
        new("5", "time", "5 ms"),
        new("10", "time", "10 ms"),
        new("16.67", "time", "约一帧 @ 60 FPS"),
        new("50", "time", "50 ms"),
        new("100", "time", "100 ms")
    ];

    public static IReadOnlyList<MacroScriptCompletionItem> LoopValues { get; } =
    [
        new("0", "count", "无限循环"),
        new("infinite", "count", "无限循环"),
        new("2", "count", "循环 2 次"),
        new("5", "count", "循环 5 次"),
        new("10", "count", "循环 10 次")
    ];

    public static IReadOnlyList<MacroScriptCompletionItem> PositionValues { get; } =
    [
        new("0", "number", "坐标/位移 0"),
        new("10", "number", "坐标/位移 10"),
        new("50", "number", "坐标/位移 50"),
        new("100", "number", "坐标/位移 100"),
        new("-10", "number", "相对位移 -10"),
        new("0.5", "number", "比例坐标 0.5")
    ];

    public static IReadOnlyList<MacroScriptCompletionItem> MouseScaleValues { get; } =
    [
        new("mouse", "flag", "setpos/drag 使用屏幕比例坐标")
    ];

    public static IReadOnlyList<MacroScriptCompletionItem> KeyValues { get; } =
        KeyNameResolver.ScriptKeyboardKeyNames
            .Select(key => new MacroScriptCompletionItem(key, "key", "键盘键"))
            .Concat(KeyNameResolver.MouseButtonNames.Select(button =>
                new MacroScriptCompletionItem(button, "mouse", "鼠标键")))
            .ToArray();

    public static IReadOnlyList<MacroScriptCompletionItem> MouseButtons { get; } =
        KeyNameResolver.MouseButtonNames
            .Select(button => new MacroScriptCompletionItem(button, "mouse", "鼠标键"))
            .Concat([
                new MacroScriptCompletionItem("left", "mouse", "鼠标左键别名"),
                new MacroScriptCompletionItem("right", "mouse", "鼠标右键别名"),
                new MacroScriptCompletionItem("middle", "mouse", "鼠标中键别名")
            ])
            .ToArray();

    public static bool IsCommand(string text)
    {
        return CommandNames.Contains(text);
    }
}
