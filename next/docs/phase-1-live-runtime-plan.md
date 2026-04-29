# Phase 1 Live Runtime Plan

先证明 C# baseline 在真实 Windows live/runtime 条件下足够硬，再决定是否值得让 Rust core 接管。

## Scope Freeze / GUI Entry Decision

路线不改，节奏纠偏。

- C# 继续作为产品级基线。
- Rust 继续作为未来可替换 runtime core 的终局储备。
- Phase 1 / Phase 2 runtime 结论必须优先来自 live/runtime/soak/acceptance，而不是来自 GUI 完成度。
- 当前 Core RC0 readiness audit 已接受：`next/` C# runtime baseline 可以认为是 Core RC0 candidate，但不是 release-ready RC。
- GUI shell planning / productization prep 已完成；`BAKeySmith.App` 当前可以认为是 GUI RC0 candidate。
- GUI RC0 candidate 不等于 release-ready GUI，也不等于 release-ready RC。
- 后续 GUI 工作仍需要单独授权，且不得新增 runtime 行为、不得引入 AppConfigV2、不得新增 Macro DSL 指令。
- GUI RC0 manual smoke checklist 见 `gui-manual-smoke-checklist.md`。

## Priority Order

1. live/runtime 验证优先：真实 Windows hook、foreground gate、SendInput、Alt-Tab、stop、reload、disable、close。
2. long-run soak tests：5 到 10 分钟 dry-run soak 和 live soak，持续观察资源、队列、worker、held inputs。
3. Acceptance 固化：JSON schema 成为 C# baseline 与未来 Rust core 的共同口径。
4. 观测优先：补 live host 诊断能力，不继续抛光编辑器体验。
5. Rust 不前置：等 C# baseline 在同一套 live、soak、acceptance 下跑实，再用同一 contract 做 Rust core 原型对比。

## Current Validation State

已完成：

- `BAKeySmith.Acceptance --scenario all` dry-run P0 短矩阵通过。
- `BAKeySmith.Acceptance --scenario dry-run-soak` 支持持续 dry-run soak 报告。
- `BAKeySmith.Headless --live` 能启动真实 Windows hook 与 `SendInput` backend，并在无触发情况下 clean stop。
- 安全目标窗口中的真实 trigger -> foreground gate -> SendInput 验证通过。
- foreground blocked 时默认不抢键，原始 trigger 透传给当前前台窗口。
- 5 分钟与 10 分钟 dry-run/live soak 已通过。
- live-safe 已覆盖 allowed foreground、blocked foreground、reload、disable、长宏 stop 清理。
- gated suppress 已覆盖 keyboard、mouse、captured session、self-injected pass-through。
- 真实 Blue Archive 最小人工验证已通过，Phase 1 已 exited。
- Phase 2A current scope 已收口，`bluearchive-manual-phase2a` full-sequence latest run 已通过。
- Phase 2B current scope 已收口：wheel / XButton 的 real-target stable samples 已固化，不重开 Phase 1 / Phase 2A 稳定基线。
- Phase 2C 当前最小 scope 已收口：`xbutton1` 的 hold + foreground change / blocked hold + foreground return 已具备 acceptance、harness 与 real-target stable samples。
- Phase 2C-B `xbutton2` symmetry scope 已收口：`xbutton2` 的 hold + foreground change / blocked hold + foreground return 也已具备 acceptance、harness 与 real-target stable samples。
- Phase 2D 当前最小 scope 已收口：`xbutton2` 的 `drag-minimal` / `multisegment-move-minimal` 已具备 acceptance、harness 与 real-target stable samples。
- Phase 2D-B 当前 scope 已收口：`xbutton2` 的 `stop-during-active-drag` / `disable-during-active-drag` 已具备 acceptance / harness coverage；对应 real-target latest JSON 当前未归档为可复用 stable samples。
- Phase 2D-C 当前 scope 已收口：`xbutton2` 的 `reload-during-active-drag` 已具备 acceptance、harness 与 real-target stable samples，并已证明 old generation cleanup 与 new generation handoff。
- Phase 2D-D 当前 scope 已收口：`xbutton2` 的 `foreground-loss-during-active-drag` / `foreground-loss-then-return-before-release` 已具备 acceptance、harness 与 real-target stable samples，并已证明 runtime foreground-loss cancellation、mapped owner cleanup 与 physical captured-session continuity。该结论来自显式脚本结构 `press mouse_middle` -> `setpos_rel` / move -> `wait` -> `release mouse_middle`，不等于证明内建 `drag` / `drag_rel` helper 已经 foreground-aware。
- Phase 2D-E 当前 scope 已收口：显式 active pointer wait 已具备 foreground-aware / interruptible wait dry harness coverage；已证明 foreground loss 发生在 wait 期间时 cleanup 不必等完整 wait 结束，且 keyboard-only wait 与 runtime/session cancellation 语义不被混淆。本轮未新增 real-target latest JSON，dry harness latency bound 不是 Blue Archive real-target SLO。
- Phase 2D-F 当前 scope 已收口：内建 `drag` / `drag_rel` helper 已具备 foreground-loss dry harness coverage；已证明 mapped drag button down 后发生 foreground loss 时会停止后续 non-cleanup output、通过 helper drag owner cleanup held button、避免 duplicate up，并且不混淆 runtime/session cancellation。本轮未新增 real-target latest JSON，dry harness latency bound 不是 Blue Archive real-target SLO，也不等于 complete drag / multi-segment drag full product scenario。
- Phase 2E 第一批当前 scope 已收口：`mouse_x2` 触发 mapped `mouse_middle` 的 explicit complete drag / multisegment drag normal-completion 已具备 dry harness coverage；已证明 down/up 各一次、duplicate up 为 0、expected / actual move delta 匹配、completion 后无额外 output、held owner 清零、stop clean。本轮未新增 real-target latest JSON，也不重证 stop / disable / reload / foreground-loss during complete drag。
- GUI RC0 audit 已收口：`BAKeySmith.App` 当前可认为是 GUI RC0 candidate；App tests 已覆盖 config、mapping、unknown fields、macro diagnostics、runtime dry wiring、live confirmation gate 等 GUI-side contract；当前仍不是 release-ready GUI / release-ready RC。

下一步：

- 当前先停在 GUI RC0 candidate 文档化后的稳定工程检查点。
- 推荐下一条单一子线是 GUI RC0 gate dry run。
- GUI RC0 gate dry run 应包含 build、Core smoke、App tests、App smoke、`all`、`lifecycle-stress` 与 GUI manual smoke checklist。
- 不切 Rust、不做 Rust FFI / 跨语言集成。
- 后续如要继续 runtime，应单独定义 complete drag interruption / reload / foreground-loss product scenario 或 `mouse_left/right` physical trigger 边界，而不是混入 GUI planning。

## Acceptance Commands

短 P0 矩阵，不包含 soak，适合每次改 runtime 后快速跑：

当前 `all` 已包含 Phase 2D-E 的 `active-pointer-wait-foreground-loss-interrupts`、Phase 2D-F 的 `built-in-drag-foreground-loss-contract`，以及 Phase 2E 第一批的 `xbutton2-triggered-complete-drag-normal-completion` / `xbutton2-triggered-multisegment-drag-normal-completion` dry harness 场景。

```powershell
dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario all --burst 50 --drain-timeout 5
```

dry-run lifecycle stress，当前 Phase 1 主线的一部分：

```powershell
dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario lifecycle-stress --burst 50 --drain-timeout 5 --output .\reports\acceptance-lifecycle-stress.json
```

开发用短 soak，适合快速确认报告字段和 stop-clean：

```powershell
dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario dry-run-soak --soak-seconds 3 --soak-rate 20 --drain-timeout 5
```

阶段验收用 dry-run soak，建议输出报告文件：

```powershell
dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario dry-run-soak --soak-seconds 300 --soak-rate 20 --drain-timeout 10 --output .\reports\acceptance-dry-run-soak.json
```

`.\reports\*.json` here denotes local generated report output. Stable archived
samples remain in `next/docs/examples/`.

## Live Verification Plan

live 验证会安装全局 hook 并可能发送真实输入，因此必须显式执行，不能混入默认 dry-run。

先跑无触发 live smoke：

```powershell
dotnet run --project .\tools\BAKeySmith.Headless\BAKeySmith.Headless.csproj -- --config ..\config.example.json --live --duration 1 --snapshot --assert-clean
```

随后在安全目标窗口中验证：

- hook install：键盘与鼠标 hook 均能启动，退出后释放。
- foreground gate allowed：目标窗口前台时 trigger 被处理并发送输入。
- foreground gate blocked：Alt-Tab 到其他窗口后 trigger 不发送映射/宏输出，原始输入必须照常透传给当前前台窗口。
- trigger suppress：keyboard 与 mouse trigger 默认只在 enabled + trigger hit + foreground allowed 时 suppress 原始输入。
- captured session：如果 trigger down 已 capture+suppress，对应 up 必须按 session 收尾，即使 release 前 foreground 已切走。
- self-injected pass-through：BA KeySmith 自己注入的 keyboard、mouse、wheel 输出不能被 hook 再当 trigger 吞回 pipeline。
- SendInput tap：simple tap 与 macro tap 走同一 tap hold 策略。
- SendInput hold：按下、释放、stop 时 owner 清理正确。
- macro stop：长 wait 宏执行中 stop，不再发送延迟输入。
- reload：运行中修改映射后旧 trigger 失效，新 trigger 生效，旧 worker 不残留。
- disable：disable 后延迟动作不能继续发输入。
- close：关闭时 held inputs 为空，worker 与 hook thread 退出。
- high-frequency：持续触发下队列可解释，不随机丢事件，不产生 task storm。
- live soak：5 到 10 分钟后线程、内存、queue、worker、held inputs 没有异常增长。
- live soak harness：持续确认目标窗口仍是 foreground，并记录 `foreground_loss_count` / `foreground_refresh_count`，避免把前台丢失误判为 runtime 丢触发。

## Real Target Manual Validation

真实 Blue Archive 目标验证是 Phase 1 exit 的必要条件，但必须保持最小必要、可复述、可归档：

- 只验证标准 Windows hook / foreground gate / SendInput 链路在 Blue Archive 前台窗口中的行为边界。
- 不做自动化游戏侧压力测试。
- 不做绕过保护、注入游戏进程、修改游戏内存或规避反作弊的实现。
- 不让这部分扩张成新的测试框架。
- 真实目标验证首先要求 acceptance / headless / live runner 以管理员身份运行，并记录 target/runner 是否 elevated、是否同级。
- 第一轮真实目标验证不再用 `SendInput` 生成 trigger，而是由测试者手动按真实 keyboard trigger。
- 顺序固定为：先最小 keyboard path，再 mouse trigger；如果 elevated + 手动 keyboard trigger 仍失败，再回头分析 hook / suppress / gate / output backend。

Checklist、模板与当前前提修正见 `manual-bluearchive-validation.md`。最近的样例报告见 `docs/examples/acceptance-bluearchive-manual-latest.json`。

当前记录结果：

- runner / target 同为 elevated；
- real target process `BlueArchive` 前台时，手动 `f8` 触发产生 `escape` tap；
- foreground blocked 时，原始 `f8` 透传至 blocker，mapped output delta 为 `0`；
- stop clean，`stop_ms` 约 `7.71ms`。

## Soak Metrics

每次 soak 必须记录：

- trigger emitted / handled / pending
- action pending / running
- worker count
- held key count / owner count
- diagnostic event count
- input event count
- stop response time
- post-stop emitted input count
- process working set / private memory delta
- thread count delta
- CPU estimate
- final stopped-clean state

## Lifecycle Stress

`lifecycle-stress` 先以 deterministic dry-run 为主，进入当前 Phase 1 主线。live-safe 版可以作为增强项继续补，但不作为无限抬高 Phase 1 的理由。

当前场景：

- `lifecycle-start-stop-loop`：多轮 start/stop，验证 stop clean、held inputs 归零、pipeline drained、无 post-stop input。
- `lifecycle-reload-loop`：多轮 reload，验证旧 trigger 失效、新 trigger 生效、generation 推进、队列清理。
- `lifecycle-enable-disable-loop`：多轮 disable/enable，验证 disabled 不输出、enabled 恢复输出。
- `lifecycle-burst-reload-stop-interleave`：trigger burst 与 reload/stop 交错，验证 stop 返回后不再发输入、held inputs 归零、pending 归零。

Stop 与 burst 交错时，未处理 trigger 不应继续 drain 成映射输出，而应计入 `pipeline_dropped`，并保证 `pipeline_pending_after_stop=0`。

## P0 Trigger Suppression Acceptance

Trigger suppression is part of runtime correctness, not UI behavior.

Default suppress semantics:

- keyboard trigger：enabled + trigger hit + foreground allowed 时 suppress；foreground blocked / disabled / stopped / trigger miss 时透传。
- mouse trigger：enabled + trigger hit + foreground allowed 时 suppress；foreground blocked / disabled / stopped / trigger miss 时透传。
- pass-through mode 如果未来支持，必须是显式配置，不是默认行为。

Required P0 scenarios before longer soak:

- `mouse-trigger-suppressed`：目标前台命中时，鼠标 trigger 不进入目标窗口，只产生映射/宏输出。
- `mouse-trigger-foreground-blocked`：前台不命中时，不输出映射/宏，原始鼠标输入继续进入 blocker/当前前台窗口。
- `mouse-trigger-reload-disable-stop-clean`：reload / disable / stop 后旧鼠标 trigger 不残留、不继续 suppress、不继续输出，runtime clean stop。
- `trigger-captured-then-foreground-changes-before-release`：down 已 capture+suppress 后，release 前切换前台，对应 up 仍被 session suppress，并正确通知 runtime 收尾。
- `self-injected-pass-through`：注入的 keyboard、mouse、wheel 输出不会被 hook 当作 trigger，也不会再次进入 pipeline。
- `keyboard-trigger-gated-suppress`：keyboard trigger 与 mouse trigger 使用同一 gated suppress 语义；allowed 时 suppress 原始 trigger，blocked 时原始 trigger 透传。

Current result:

- `trigger-suppress` 聚合场景当前 7/7 通过。
- `mouse-trigger-suppressed`：目标前台命中时 original mouse down/up delta 为 0，映射输出正常。
- `mouse-trigger-foreground-blocked`：foreground blocked 时映射输出为 0，original mouse down/up 进入 blocker。
- `mouse-trigger-reload-disable-stop-clean`：reload 后旧 trigger 透传，新 trigger 输出；disable 后透传且不输出。
- `trigger-captured-then-foreground-changes-before-release`：mouse down captured 后切换前台，mouse up 仍释放 mapped hold。
- `self-injected-pass-through`：self-injected keyboard / mouse / wheel pipeline delta 为 0。
- `keyboard-trigger-gated-suppress`：allowed 时 original F13 delta 为 0，blocked 时 original F13 down/up 进入 blocker。

## Current Soak Results

60 秒回归：

- `dry-run-soak`：1200 triggers，2400 input events，post-stop input 0，held inputs 0，clean stop。
- `live-soak`：1200 triggers，2400 output events，pipeline queued/handled 2400/2400，post-stop input 0，held inputs 0，clean stop。

5 分钟回归：

- `dry-run-soak`：6000 triggers，12000 input events，pipeline queued/handled 6000/6000，post-stop input 0，held inputs 0，stop 约 6.63ms，clean stop。
- `live-soak`：6000 triggers，12000 output events，pipeline queued/handled 12000/12000，post-stop input 0，held inputs 0，stop 约 7.42ms，clean stop。
- `live-soak` foreground：`foreground_loss_count=0`，`foreground_refresh_count=0`。
- `live-soak` resources：working set delta 约 15.95MB，private memory delta 约 12.42MB，thread delta -2，CPU estimate 约 0.028%。

10 分钟回归：

- `dry-run-soak`：12000 triggers，24000 input events，pipeline queued/handled 12000/12000，post-stop input 0，held inputs 0，stop 约 7.10ms，clean stop。
- `live-soak`：12000 triggers，24000 output events，pipeline queued/handled 24000/24000，post-stop input 0，held inputs 0，stop 约 8.67ms，clean stop。
- `live-soak` foreground：`foreground_loss_count=0`，`foreground_refresh_count=0`。
- `live-soak` resources：working set delta 约 17.87MB，private memory delta 约 13.97MB，thread delta -2，CPU estimate 约 0.028%。
- 示例 JSON 已固化在 `next/docs/examples/acceptance-dry-run-soak-10min.json` 与 `next/docs/examples/acceptance-live-soak-10min.json`。

Live harness 修正：

- 10 分钟 live soak 首次执行在初始化阶段暴露 `failed to focus live soak target window`。
- 已将 harness 聚焦从单次 `SetForegroundWindow` 加固为重试式 foreground activation：restore、临时 top-most、`BringWindowToTop`、`SetActiveWindow`、`SetFocus`、`SetForegroundWindow`，并在必要时 `AttachThreadInput`。
- 修正后 10 分钟 live soak、`live-safe`、`trigger-suppress` 均通过；该修正只影响 acceptance harness，不改变 runtime 语义。

Lifecycle stress 回归：

- `lifecycle-stress --burst 50 --drain-timeout 5`：4/4 通过，总耗时约 8.21s。
- `lifecycle-start-stop-loop`：50 loops，post-stop input 0，held / owner 归零。
- `lifecycle-reload-loop`：50 loops，generation 到 51，旧 trigger 不输出，新 trigger 输出。
- `lifecycle-enable-disable-loop`：50 disabled probes 无输出，50 enabled probes 输出正常。
- `lifecycle-burst-reload-stop-interleave`：50 loops，pipeline queued/handled/dropped 为 1300/794/506，pending after stop 0，post-stop input 0，held / owner 归零。

## Phase 1 Exit Criteria

Phase 1 不能只看 dry-run 通过。正式 exit 必须满足：

- 10 分钟 dry-run soak 通过。
- 10 分钟 live soak 通过。
- acceptance 全部稳定通过。
- mouse/keyboard gated suppress 场景稳定通过。
- self-injected pass-through 稳定通过。
- stop / reload / disable / long macro 清理稳定通过。
- 示例 JSON 报告与 schema 固定。
- 至少一次真实 Blue Archive 目标环境人工验证通过。
- 文档口径与实现一致。

最好满足：

- lifecycle stress 通过。
- memory/thread 没有明显持续增长。
- Python / C# 主次关系明确。
- 风险 backlog 已记录且不分散主线。

当前状态见 `phase-1-exit.md`：Phase 1 已 exited。当前工作重点不是重开 Phase 1，而是把 Phase 2A current scope 的 real-target manual baseline 与 release gate 固定成稳定检查点。

## Risk Backlog

这些风险进入 backlog，但不改变当前收口主线：

- live-safe 不是充分条件，仍需真实目标环境人工验证。
- 鼠标边界条件要继续补 acceptance：左右键同时按下、双击、完整 drag / 多段 drag、`mouse_left/right` 物理 trigger、capture 后 reload/disable/stop、多鼠标 trigger 并存。
- `drag` / `drag_rel` helper foreground-loss contract 已由 Phase 2D-F dry harness 覆盖；`mouse_x2` 触发 mapped `mouse_middle` 的 explicit complete drag / multisegment drag normal-completion 已由 Phase 2E 第一批 dry harness 覆盖。剩余缺口是 stop / disable / reload / foreground-loss during complete drag 的产品场景重证、`mouse_left/right` physical trigger、pre-helper-down foreground drift，以及真实目标 latency / soak SLO。
- soak 之外必须保留 lifecycle stress。
- `TriggerPipeline.StartAsync` 当前在失败后已经能正确回滚到稳态，但 start 提交期间仍存在短暂 `running` 窗口；该点已记入 backlog，后续如需更强事务性再单独收紧，这轮不继续重构。
- Python 侧与 `next/` C# baseline 的主次关系必须按 `migration-statement.md` 收口，避免双主线。
