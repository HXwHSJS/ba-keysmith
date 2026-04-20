# Blue Archive Manual Validation

本文件用于 Phase 1 的真实目标环境人工验证。它不是自动化游戏侧压力测试，也不是绕过保护/反作弊的实现。

目标只限于验证标准 Windows 输入链路在真实 Blue Archive PC 前台窗口中的行为边界：

- Windows low-level hook 能收到 trigger。
- foreground gate 只在 Blue Archive 前台时允许输出。
- `SendInput` 输出能按预期进入目标窗口。
- stop / reload / disable / long macro cleanup 不残留输入。

这份验证首先要求验证前提成立：

- live runner / headless / acceptance 进程必须以管理员身份运行。
- 需要记录 target 是否 elevated、runner 是否 elevated、两者是否同级。
- 如果 runner 不是管理员，本轮真实目标验证应记为 **Invalid / Not a valid runtime verdict**，不能据此否定 runtime correctness。

## Scope Constraints

- 最小必要验证，不扩张成新的大工程。
- 不做自动化游戏侧压力测试。
- 不做绕过保护、注入游戏进程、修改游戏内存或规避反作弊的实现。
- 不把 live-safe harness 的结论等同于真实目标验证结论。

## Current Validation Premise

真实目标验证当前采用的首要前提：

- 不再把 “live-safe / safe harness 通过” 当成真实目标验证结论。
- 不再在第一轮真实目标验证里用 `SendInput` 生成 trigger。
- 第一轮只做最小必要的 **真实手动键盘 trigger** 路径。
- 键盘路径通过后，再继续鼠标 trigger 路径。

当前会话已确认的风险：

- Blue Archive 很可能以管理员身份运行。
- 当前 Codex / acceptance runner 会话不是管理员。

因此当前最合理的结论是：

> 这次真实目标验证失败，更可能是权限 / 完整性级别不匹配导致的无效验证，而不是 runtime correctness 已被否定。

该前提问题已经被纠正。最近一次有效验证是在 runner / target 同为 elevated 的条件下完成的，结果见 `docs/examples/acceptance-bluearchive-manual-latest.json`。

## Minimal Checklist

执行前准备：

- 启动 Blue Archive PC 客户端并进入安全、可重复观察输入效果的界面。
- 确认 BA KeySmith 使用 C# baseline runtime 或等价 live runtime host。
- 确认 acceptance / headless / live runner 以管理员身份运行。
- target process 设置为真实 Blue Archive 前台窗口进程名。
- 使用不会造成账号、战斗或 UI 风险的测试键位。
- 保持 diagnostics / acceptance log 可观察：foreground result、trigger received、mapped output、pipeline queued/handled/dropped、held inputs、post-stop events。

第一轮真实目标验证请遵循这条顺序：

1. 先做最小键盘路径，不用 `SendInput` 生成 trigger，由测试者手动按真实 trigger。
2. keyboard path 通过后，再继续 mouse trigger。
3. 如果 elevated runner + 真实手动 keyboard trigger 仍失败，再回头分析 hook / suppress / gate / output backend 的哪一段有问题。

如果 acceptance runner 自身不是交互式控制台，可以在完成肉眼观察后，用 `--manual-confirm yes|no` 将人工观察结论写入报告。

建议测试映射：

| 用途 | Trigger | Output / Macro |
| :--- | :--- | :--- |
| keyboard suppress | `f8` 或安全实体键 | `escape` |
| mouse suppress | `mouse_right` | `escape` |
| reload | `f15` -> reload -> `f16` | 旧 trigger 不输出，新 trigger 输出 |
| disable / stop | 任一安全 trigger | disabled / stopped 后不输出 |
| long macro cleanup | 安全 trigger | `press <safe key>` / `wait 5000` / `release <safe key>` |

验证项：

- keyboard trigger suppress：Blue Archive 前台且 trigger 命中时，原始 trigger 不进入游戏，只出现映射/宏输出。第一轮必须由用户手动按真实 trigger 验证。
- mouse trigger suppress：Blue Archive 前台且 trigger 命中时，原始鼠标点击不进入游戏，只出现映射/宏输出。
- foreground blocked pass-through：Alt-Tab 到其他应用后，runtime 不输出映射/宏，原始 keyboard/mouse trigger 正常进入当前前台应用。
- reload：运行中 reload 后旧 trigger 不再输出，新 trigger 生效；diagnostics 中 generation 更新。
- disable：disable 后 trigger 不输出，原始输入按 gated suppress 语义透传。
- stop：stop 返回后不再输出；held inputs / owners / queue / pipeline 均清零或 dropped clean。
- long macro cleanup：长 wait / hold 宏执行中 stop，按下状态被释放，stop 返回后无延迟输入。
- self-injected pass-through：runtime 自己注入的 keyboard / mouse / wheel 不再进入 trigger pipeline。
- foreground gate：Blue Archive 前台时 allowed；切到其他窗口时 blocked，且 blocked 时原始输入透传。

## Result Template

复制以下模板记录一次真实目标验证：

```text
Date:
Tester:
Blue Archive process name:
BA KeySmith build/runtime:
Config target_process:
Runner elevated:
Target elevated:
Same elevation level:
Validation premise valid:
Manual trigger mode:

keyboard trigger suppress:
Result:
Evidence:

mouse trigger suppress:
Result:
Evidence:

foreground blocked pass-through:
Result:
Evidence:

reload:
Result:
Evidence:

disable:
Result:
Evidence:

stop:
Result:
Evidence:

long macro cleanup:
Result:
Evidence:

self-injected pass-through:
Result:
Evidence:

foreground gate:
Result:
Evidence:

Overall:
```

Phase 1 exit 要求至少一次 `Overall: Passed`。

## Latest Recorded Result

日期：2026-04-20

目标进程：

- `BlueArchive` / PID `7820`
- window title: `ブルーアーカイブ`

执行前提：

- runner elevated: `true`
- target elevated: `true`
- same elevation level: `true`
- manual trigger: `f8`
- validation scope: `keyboard-minimal`

结果：

- keyboard trigger path：Passed
  证据：`keyboard_escape_events=2`
- foreground blocked pass-through：Passed
  证据：`foreground_blocked_keyboard_pass_through=1`，`foreground_blocked_output_delta=0`
- stop clean：Passed
  证据：`stop_ms=7.7136`，`stopped_clean=true`
- manual observation：Passed
  证据：测试者确认前台 Blue Archive 中只观察到 `Esc` 效果

说明：

- 本轮 trigger 选用 `f8` 作为低风险实体键。
- 因为 Blue Archive 本身没有可见的 `f8` 原生动作，这一轮对“原始 trigger 被 suppress”的证据属于“行为一致 + blocker pass-through + runtime metrics”联合证据，而不是游戏内原生副作用的直接对照。

Overall：**Passed**
