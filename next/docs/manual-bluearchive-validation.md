# Blue Archive Manual Validation

本文件用于真实 Blue Archive 目标环境的最小人工验证。它不是自动化游戏侧压力测试，也不是绕过保护/反作弊的实现。

目标只限于验证标准 Windows 输入链路在真实 Blue Archive PC 前台窗口中的行为边界：

- Windows low-level hook 能收到 trigger。
- foreground gate 只在 Blue Archive 前台时允许输出。
- `SendInput` 输出能按预期进入目标窗口。
- stop / reload / disable / long macro cleanup 不残留输入。

当前阶段补充：

- Phase 1：已 exited
- Phase 2A current scope：已收口
- Phase 2B current scope：已收口，`wheel` 与 `XButton` 的 real-target boundary 已形成稳定补充样例
- Phase 2C current minimal scope：已收口
- Phase 2C-B `xbutton2` symmetry scope：已通过并完成基线复核
- Phase 2D current minimal scope：已通过并完成基线复核
- Phase 2D-B active drag interruption cleanup：已收口；harness / acceptance 已通过，real-target latest JSON 当前未归档为可复用样例
- Phase 2D-C reload-during-active-drag：已通过并完成基线复核
- Phase 2D-D foreground change during active drag：已通过并完成基线复核
- Phase 2D-E foreground-aware / interruptible wait hardening：已通过 dry / harness 收口；未新增 real-target latest JSON
- Phase 2D-F built-in `drag` / `drag_rel` foreground-loss contract：已通过 dry / harness 收口；未新增 real-target latest JSON
- Phase 2E first-batch complete drag / multisegment drag normal completion：已通过 dry / harness 收口；未新增 real-target latest JSON

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

历史前提修正记录：

- Blue Archive 很可能以管理员身份运行。
- 早期有过 Codex / acceptance runner 会话不是管理员的无效样本。

当时最合理的结论是：

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
| mouse suppress | `mouse_middle` | `escape` |
| reload | `f15` -> reload -> `f16` | 旧 trigger 不输出，新 trigger 输出 |
| disable / stop | 任一安全 trigger | disabled / stopped 后不输出 |
| long macro cleanup | 安全 trigger | `press <safe key>` / `wait 5000` / `release <safe key>` |

说明：

- 当前 `bluearchive-manual-phase2a` latest sample 使用的 mouse trigger 是 `mouse_middle`，本文件建议测试映射与样例报告保持一致。

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

## Phase 2B Boundary Expansion Scope

这轮只扩下面两个 real-target 边界，不做别的：

1. `mouse wheel`
2. `XButton`

### Wheel Contract

- `mouse_wheel_up` / `mouse_wheel_down` 是无状态 one-shot trigger。
- allowed：`enabled + trigger hit + foreground allowed` 时 suppress 原始 wheel，并各自产生一次 mapped output。
- blocked / disabled / stopped / trigger miss：不输出 mapped result，原始 wheel 透传给当前前台窗口。
- wheel 不参与 captured session，不和 button-held 语义混用。

建议最小 real-target 映射：

| 用途 | Trigger | Output |
| :--- | :--- | :--- |
| wheel allowed | `mouse_wheel_up` | `f17` 或安全 tap 键 |
| wheel allowed | `mouse_wheel_down` | `f18` 或安全 tap 键 |
| wheel blocked pass-through | `mouse_wheel_up/down` | blocker/harness 仅观察 pass-through，不做人工复杂判断 |

当前证据口径：

- safe-window / harness 已通过 `wheel-trigger-boundaries`。
- real-target 当前也已完成最小人工验证，并已固化：
  - `acceptance-bluearchive-manual-wheel-latest.json`
  - `acceptance-bluearchive-manual-wheel-down-latest.json`
- wheel 当前应理解为“real-target + blocker + harness”联合证据链，而不是只看 harness 结论。

### XButton Contract

- `mouse_x1` / `mouse_x2` 沿用现有 mouse trigger 口径。
- allowed：suppress 原始 XButton，仅产生 mapped output。
- blocked：原始 XButton 透传给当前前台窗口，不输出 mapped result。
- reload 后旧 trigger 透传，新 trigger 输出。
- disable 后原始 XButton 透传，不输出 mapped result。
- self-injected XButton 事件不回灌 trigger pipeline。

建议最小 real-target 映射：

| 用途 | Trigger | Output |
| :--- | :--- | :--- |
| xbutton allowed | `mouse_x1` | `f17` 或安全 tap 键 |
| xbutton allowed | `mouse_x2` | `f18` 或安全 tap 键 |
| xbutton reload | `mouse_x1 -> reload -> mouse_x2` | 旧 trigger 透传，新 trigger 输出 |
| xbutton disable | `mouse_x2` | disabled 后仅透传 |

当前证据口径：

- safe-window / harness 已通过：
  - `xbutton-trigger-boundaries`
  - `xbutton-trigger-reload-disable`
  - `xbutton-self-injected-pass-through`
- real-target 当前也已完成最小人工验证，并已固化：
  - `acceptance-bluearchive-manual-xbutton-latest.json`
  - `acceptance-bluearchive-manual-xbutton2-latest.json`
  - `acceptance-bluearchive-manual-xbutton-reload-latest.json`
  - `acceptance-bluearchive-manual-xbutton-disable-latest.json`
  - `acceptance-bluearchive-manual-xbutton2-disable-latest.json`
- XButton 当前应理解为“real-target + blocker + harness”联合证据链，而不是只看 harness 结论。

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

## Latest Recorded Results

### Phase 1 Real-Target Minimal

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

### Phase 2D-B Active Drag Interruption Cleanup

日期：2026-04-27

当前状态：

- safe-window / harness:
  - `xbutton2-triggered-drag-stop-during-active-drag`: Passed
  - `xbutton2-triggered-drag-disable-during-active-drag`: Passed
- real-target Blue Archive:
  - 当前仓库未找到 `bluearchive-manual-xbutton2-drag-stop-during-active-drag` / `bluearchive-manual-xbutton2-drag-disable-during-active-drag` 对应 latest JSON。
  - 因此本文件不再把这两个 real-target reports 列为当前可复用 stable supplement samples。

当前解释口径：

- 当前 Phase 2D-B 只验证 active drag 已经开始后的中断 cleanup，不扩到 `reload-during-drag`，也不扩到完整 drag 子线。
- `xbutton2-triggered-drag-stop-during-active-drag` 当前已证明：
  - interrupt 确定发生在 active drag 期间，而不是 normal completion 之后；
  - `drag_completed_normally_count=0`，`interrupt_while_drag_active_count=1`；
  - cleanup `up` 由中断路径补发，`drag_button_up_from_cleanup_count=1`；
  - interrupt 后没有继续 `move/output`；
  - cleanup 后 `held_owner_count_after_cleanup=0`，`stopped_clean=true`。
- `xbutton2-triggered-drag-disable-during-active-drag` 当前已证明：
  - disable 同样发生在 active drag 期间；
  - cleanup `up` 正确发出；
  - disable 后没有继续 `move/output`；
  - cleanup 后 `held_owner_count_after_cleanup=0`，`stopped_clean=true`。
- real-target 口径当前按“结构化为主、人工为辅”管理：
  - `stop-during-active-drag` 即使人工没有稳定看到明显视觉反应，也不单独判成 runtime failure；
  - 核心结论依赖 active-drag barrier、cleanup `up`、interrupt 后无后续 `move/output`、held owner 清零这些结构化指标。
- 当前 remaining gap：
  - stop / disable active-drag real-target latest JSON 未归档 / 不可复用；
  - 如果后续相关公共路径变更需要 real-target sign-off，必须重新执行对应 manual scenario 并归档 latest JSON。
- 当前还没有证明的下一层边界仍包括：
  - `reload-during-active-drag`
  - drag 中途 foreground change
  - 完整 drag / multi-segment drag
  - `mouse_left` / `mouse_right` 作为物理 trigger 的 drag / move
  - repeated click / double-click
  - 更长时的 real-target soak

Overall：**Closed with real-target archive gap**

### Phase 2D-C Reload During Active Drag

日期：2026-04-27

当前状态：

- safe-window / harness:
  - `xbutton2-triggered-drag-reload-during-active-drag-old-trigger-blocked`: Passed
  - `xbutton2-triggered-drag-reload-during-active-drag-new-trigger-allowed`: Passed
- real-target Blue Archive:
  - `bluearchive-manual-xbutton2-drag-reload-during-active-drag-old-trigger-blocked`: Passed
  - `bluearchive-manual-xbutton2-drag-reload-during-active-drag-new-trigger-allowed`: Passed

当前解释口径：

- 当前 Phase 2D-C 只验证 active drag 期间发生 reload 时的 cleanup 与 generation handoff，不扩到 foreground change during drag，也不扩到完整 drag 子线。
- old generation 当前已证明：
  - reload 确定发生在 active drag 期间，而不是 normal completion 之后；
  - `drag_completed_normally_count=0`，`interrupt_while_drag_active_count=1`；
  - cleanup `up` 由 reload 路径补发，`drag_button_up_from_reload_cleanup_count=1`；
  - reload 后 old generation 不再继续 `move/output`；
  - cleanup 后 `held_owner_count_after_reload_cleanup=0`；
  - old trigger 在 reload 后不再进入 pipeline/runtime，并在 blocker 中按 pass-through 处理。
- new generation 当前已证明：
  - `reload_generation_after > reload_generation_before`；
  - new trigger 被 hook 收到、进入 pipeline/runtime，并产生预期 mapped output；
  - old trigger 与 new trigger 的 counts 已分开记录，没有混淆 old generation cleanup 与 new generation takeover。
- real-target 口径当前仍按“结构化为主、人工为辅”管理：
  - old generation cleanup、old trigger 退场、new trigger 接管都以结构化指标为核心；
  - `bluearchive-manual-xbutton2-drag-reload-during-active-drag-new-trigger-allowed` 中，new trigger 的人工 `Esc/返回/菜单` 观察只作为辅助增强证据。
- 以下 latest JSON 当前可以作为可复用稳定补充样例：
  - `acceptance-bluearchive-manual-xbutton2-drag-reload-during-active-drag-old-trigger-blocked-latest.json`
  - `acceptance-bluearchive-manual-xbutton2-drag-reload-during-active-drag-new-trigger-allowed-latest.json`
- 当前还没有证明的下一层边界仍包括：
  - drag 中途 foreground change
  - 完整 drag / multi-segment drag
  - `mouse_left` / `mouse_right` 作为物理 trigger 的 drag / move
  - repeated click / double-click
  - 更长时的 real-target soak

Overall：**Passed**

### Phase 2D-D Foreground Change During Active Drag

日期：2026-04-27

当前状态：

- safe-window / harness:
  - `xbutton2-triggered-drag-foreground-loss-during-active-drag`: Passed
  - `xbutton2-triggered-drag-foreground-loss-then-return-before-release`: Passed
- real-target Blue Archive:
  - `bluearchive-manual-xbutton2-drag-foreground-loss-during-active-drag`: Passed
  - `bluearchive-manual-xbutton2-drag-foreground-loss-then-return-before-release`: Passed

当前解释口径：

- 当前 Phase 2D-D 只验证 active drag 期间发生 foreground loss 时的 runtime cancellation + cleanup，不扩到完整 drag 子线，也不扩到 `mouse_left` / `mouse_right` 物理 trigger。
- 当前 Phase 2D-D 样例使用的是显式脚本结构：`press mouse_middle` -> `setpos_rel` / move -> `wait` -> `release mouse_middle`。
- 当前 Phase 2D-D 不等于证明内建 `drag` / `drag_rel` helper 已经 foreground-aware；该 helper contract 已由 Phase 2D-F 的 dry / harness 场景单独处理。
- Phase 2D-E 已单独处理显式 active pointer wait 的 foreground-aware / interruptible wait hardening；Phase 2D-F 已单独处理 built-in helper，但二者都不等于完整 drag / multi-segment drag full product scenario。
- foreground loss 后停止后续 `move/output` 的行为来自 runtime 层，而不是 acceptance 脚本简单跳过后续 move：
  - foreground loss 是 `MacroExecutor` 在 active pointer sequence 期间通过 foreground gate 检测到的；
  - 后续 non-cleanup `move/output` 是由 `MacroExecutor` 中断执行循环而取消的；
  - cleanup `up` 由 `MacroExecutor` 的 `finally -> ReleaseOwnerAsync` 路径发出；
  - 这条 cleanup 路径与现有 stop / disable / reload 的 owner cleanup 语义兼容，没有改变它们的 contract。
- 当前 cleanup timing model 需要收紧表述：
  - 当前已证明 foreground loss 之后不会继续后续 non-cleanup `move/output`；
  - 当前已证明 mapped held owner 最终会 cleanup；
  - 当前已证明 physical release 仍匹配 captured session；
  - 当前**没有**证明 foreground loss 会“瞬间立即 cleanup”；
  - 当前更准确的实现语义是 `instruction-boundary / wait-boundary foreground cleanup`：
    - foreground loss 在 `MacroExecutor` 的指令边界被观察；
    - 如果 foreground loss 发生在长 `wait` 指令执行期间，cleanup 可能要等到该 `wait` 返回后才发生。
- mapped output held owner 生命周期当前已证明：
  - `drag_button_down_count=1`，`move_event_before_foreground_loss_count>=1`，foreground loss 发生时 drag 仍处于 active 状态；
  - `drag_completed_normally_count=0`，foreground loss 不是发生在 normal completion 之后；
  - cleanup `up` 单独记录为 `drag_button_up_from_foreground_cleanup_count=1`；
  - cleanup 后 `move_event_after_foreground_loss_count=0`，`unexpected_output_after_foreground_loss_count=0`；
  - `mapped_owner_cleanup_completed_count=1`，`held_owner_count_after_foreground_cleanup=0`。
- physical trigger captured session 生命周期当前已证明：
  - foreground loss cleanup mapped `mouse_middle` held owner 后，physical `mouse_x2` captured session 仍保留到 matching `up`；
  - `physical_capture_session_retained_after_foreground_loss=1`；
  - `trigger_up_received_count=1`，`release_matched_captured_session_count=1`；
  - `blocked_pass_through_up_count=0`，physical release 不会误透传到 blocker；
  - `unexpected_output_after_release_count=0`，physical release 不会触发新的 mapped output。
- foreground return before release 当前已证明：
  - `foreground_returned_before_release_count=1` 时，`retroactive_resume_after_foreground_return_count=0`；
  - foreground 回到目标窗口后不会 retroactively resume old drag。
- real-target 口径当前仍按“结构化为主、人工为辅”管理：
  - foreground loss 是否发生在 active drag 期间、cleanup `up` 是否发出、cleanup 后是否无 move/output、held owner 是否清零，都以结构化指标为核心；
  - 如果 Blue Archive 中视觉反应不明显，不单独记成 runtime failure。
- 以下 latest JSON 当前可以作为可复用稳定补充样例：
  - `acceptance-bluearchive-manual-xbutton2-drag-foreground-loss-during-active-drag-latest.json`
  - `acceptance-bluearchive-manual-xbutton2-drag-foreground-loss-then-return-before-release-latest.json`
- 当前还没有证明的下一层边界仍包括：
  - 完整 drag / multi-segment drag
  - `mouse_left` / `mouse_right` 作为物理 trigger 的 drag / move
  - repeated click / double-click
  - 更长时的 real-target soak

Overall：**Passed**

### Phase 2D-E Foreground-Aware / Interruptible Wait Hardening

日期：2026-04-27

当前状态：

- dry / harness:
  - `active-pointer-wait-foreground-loss-interrupts`: Passed
- smoke:
  - active pointer wait foreground loss interrupts before full wait completion
  - keyboard-only wait is not cancelled by active pointer hardening
  - runtime/session cancellation is not reported as foreground loss
- real-target Blue Archive:
  - 本轮默认不跑 real-target，未新增 Blue Archive latest JSON

当前解释口径：

- Phase 2D-E 只处理显式脚本结构：`press mouse_middle` -> `setpos_rel` / move -> `wait` -> `release mouse_middle`。
- Phase 2D-E 已证明 foreground loss 发生在 active pointer wait 期间时，`MacroExecutor` 可在完整 wait 结束前检测并 cleanup。
- Phase 2D-E 已证明：
  - active pointer macro 已进入 wait；
  - mapped pointer button 已 down；
  - 至少一个 move 已发生；
  - normal release 尚未发生；
  - foreground gate 此时变为 blocked；
  - cleanup `up` 发出且只发一次；
  - wait 后续 move / release 不继续执行；
  - held owner 清零。
- Phase 2D-E 未证明：
  - Blue Archive real-target cleanup latency SLO；
  - 真实目标环境下 cleanup latency 永远小于 dry harness bound；
  - 内建 `drag` / `drag_rel` helper foreground awareness（该项已由 Phase 2D-F 单独证明，不由 Phase 2D-E 证明）；
  - complete drag / multi-segment drag full contract。
- `foreground_loss_to_cleanup_ms`、`cleanup_latency_bound_ms`、`foreground_loss_to_cleanup_within_expected_bound` 当前只用于 deterministic dry / harness 口径。
- 当前 `cleanup_latency_bound_ms=250` 不是 Blue Archive real-target latency SLO。
- 本轮不改变 Phase 2D-D real-target latest JSON 的稳定补充样例口径；Phase 2D-E dry harness 是新增自动化 contract，不是新的 real-target stable supplement sample。

Overall：**Passed (dry / harness only)**

### Phase 2D-F Built-In `drag` / `drag_rel` Foreground-Loss Contract

日期：2026-04-27

当前状态：

- dry / harness:
  - `built-in-drag-foreground-loss-contract`: Passed
- smoke:
  - built-in `drag` foreground loss during pre-move delay skips move and cleans up
  - built-in `drag_rel` foreground loss during pre-move delay skips move and cleans up
  - built-in `drag_rel` foreground loss during post-move delay stops outer instructions
  - built-in `drag_rel` cancellation is not foreground loss
  - built-in `drag` and `drag_rel` complete normally without foreground loss
- real-target Blue Archive:
  - 本轮默认不跑 real-target，未新增 Blue Archive latest JSON

当前解释口径：

- Phase 2D-F 只处理内建 `drag` / `drag_rel` helper 在 mapped drag button down 之后的 active pointer helper sequence。
- Phase 2D-F 已证明：
  - helper 已经进入 active pointer helper sequence；
  - mapped drag button 已 down；
  - normal helper release 尚未发生；
  - foreground gate 在该阶段变为 blocked；
  - helper cleanup `up` 来自 helper drag owner（形如 `owner:drag:<button>`），不是 outer macro owner cleanup；
  - cleanup `up` 只发一次，没有 duplicate up；
  - foreground loss 后不继续 outer macro 后续 non-cleanup instruction；
  - held owner 清零；
  - cancellation 不会被记成 foreground loss；
  - normal built-in `drag` / `drag_rel` 没有 foreground loss 时仍正常完成。
- Phase 2D-F 未证明：
  - complete drag / multi-segment drag full product scenario；
  - `mouse_left` / `mouse_right` 作为物理 trigger；
  - helper button down 之前已经丢失 foreground 时的更广 macro-output gating；
  - 打断已经进入单个同步 `MoveMouseToAsync` 的中途调用；
  - Blue Archive real-target cleanup latency SLO。
- `foreground_loss_to_cleanup_ms`、`cleanup_latency_bound_ms`、`foreground_loss_to_cleanup_within_expected_bound` 当前只用于 deterministic dry / harness 口径。
- 当前 `cleanup_latency_bound_ms=250` 不是 Blue Archive real-target latency SLO。
- 本轮不新增 Blue Archive real-target stable supplement sample。

Overall：**Passed (dry / harness only)**

### Phase 2E First-Batch Complete Drag / Multisegment Drag Normal Completion

日期：2026-04-27

当前状态：

- dry / harness:
  - `xbutton2-triggered-complete-drag-normal-completion`: Passed
  - `xbutton2-triggered-multisegment-drag-normal-completion`: Passed
- smoke:
  - xbutton2 complete drag normal completion leaves no residue
  - xbutton2 multisegment drag normal completion leaves no residue
- real-target Blue Archive:
  - 本轮默认不跑 real-target，未新增 Blue Archive latest JSON

当前解释口径：

- Phase 2E 第一批只处理 dry / harness normal-completion product path。
- 当前 explicit DSL shape 是：`press mouse_middle` -> one or more `setpos_rel` segments with short `wait` -> `release mouse_middle`。
- physical trigger 是 `mouse_x2`，mapped drag button 是 `mouse_middle`。
- 当前已证明：
  - trigger / pipeline / runtime 链路各收到并分发一次 down trigger；
  - macro started / finished 各为 1；
  - `mouse_middle` down = 1、up = 1；
  - duplicate up = 0；
  - complete-drag 1 段 move、multisegment 3 段 move 的 expected delta 与 actual delta 匹配；
  - completion 后 100ms dry harness probe 内没有额外 output；
  - stop 后没有额外 output；
  - completion 后 held owner 清零；
  - runtime stop clean。
- 当前未证明：
  - Blue Archive real-target complete drag SLO；
  - stop / disable / reload during complete drag；
  - foreground-loss during complete drag 的产品场景重证；
  - `mouse_left/right` physical trigger；
  - double-click / repeated click；
  - long real-target soak。
- 本轮不新增 Blue Archive real-target stable supplement sample。

Overall：**Passed (dry / harness only)**

### Phase 2A Current Scope

日期：2026-04-20

目标进程：

- `BlueArchive.exe` / PID `9880`
- window title: `ブルーアーカイブ`

执行前提：

- runner elevated: `true`
- target elevated: `true`
- same elevation level: `true`
- validation scope: `phase2a-full-sequence`
- keyboard trigger: `f8`
- mouse trigger: `mouse_middle`
- reload trigger: `f9`
- long macro trigger: `f10`

已验证结论：

- `F10-only` 隔离 real-target：Passed
  证据：`long_macro_trigger_received_count=1`、`long_macro_pipeline_enqueued_count=1`、`long_macro_runtime_dispatched_count=1`、`long_macro_macro_started_count=1`、`long_macro_escape_down_count=1`、`long_macro_escape_up_count=1`、`long_macro_stop_requested_count=1`、`long_macro_cleanup_completed_count=1`
- full `bluearchive-manual-phase2a` long sequence：Passed
  证据：8 个 stage 全部 `entered=true`、`completed=true`、`failure_reason=""`
- real-target allowed stages：Passed
  证据：`keyboard_allowed`、`mouse_allowed`、`reload_new_trigger_allowed`、`long_macro_only` 人工观察与结构化指标一致
- blocker / pass-through stages：Passed
  证据：`keyboard_blocked`、`mouse_blocked`、`reload_old_trigger_blocked` 的 pass-through 指标均为正，mapped output delta 为 `0`
- disable no-output：Passed
  证据：`stage_disable_no_output_trigger_received_count=1`，且 `pipeline/runtime/output = 0/0/0`

当前解释口径：

- full-sequence 与 `F10-only` 现在都已通过。
- 之前 full-sequence 下的 long macro 失败，更合理地归因于旧实现下的观测窗口 / stage 归因问题，而不是 cleanup / macro executor 缺陷。
- 当前 `acceptance-bluearchive-manual-phase2a-latest.json` 已可作为当前可复用稳定样例，而不再是 partial / in-progress 证据。

Overall：**Passed**

### Phase 2B Current Scope

日期：2026-04-21

当前状态：

- safe-window / harness:
  - `wheel-trigger-boundaries`: Passed
  - `xbutton-trigger-boundaries`: Passed
  - `xbutton-trigger-reload-disable`: Passed
  - `xbutton-self-injected-pass-through`: Passed
- real-target Blue Archive:
  - `bluearchive-manual-wheel` with `mouse_wheel_up`: Passed
  - `bluearchive-manual-wheel` with `mouse_wheel_down`: Passed
  - `bluearchive-manual-xbutton` with `mouse_x1`: Passed
  - `bluearchive-manual-xbutton` with `mouse_x2`: Passed
  - `bluearchive-manual-xbutton-reload`: Passed
  - `bluearchive-manual-xbutton-disable` with `mouse_x1`: Passed
  - `bluearchive-manual-xbutton-disable` with `mouse_x2`: Passed

当前解释口径：

- 当前 Phase 2B 子线只覆盖 wheel + XButton 的最小 real-target boundary expansion，不扩到 double-click / drag / foreground-change 等更复杂边界。
- wheel 当前已形成对称证据链：`mouse_wheel_up` 与 `mouse_wheel_down` 都已具备 harness + blocker + real-target allowed/blocked 样例。
- XButton 当前已形成对称证据链：`mouse_x1` / `mouse_x2` 的 real-target allowed/blocked、reload-only、disable-only 都已具备 harness + real-target 样例。
- 以下 latest JSON 当前可以作为可复用稳定样例：
  - `acceptance-bluearchive-manual-wheel-latest.json`
  - `acceptance-bluearchive-manual-wheel-down-latest.json`
  - `acceptance-bluearchive-manual-xbutton-latest.json`
  - `acceptance-bluearchive-manual-xbutton2-latest.json`
  - `acceptance-bluearchive-manual-xbutton-reload-latest.json`
  - `acceptance-bluearchive-manual-xbutton-disable-latest.json`
  - `acceptance-bluearchive-manual-xbutton2-disable-latest.json`
- 当前未证明的下一层边界仍包括：
  - repeated click / double-click
  - 完整 drag / 多段 hold + foreground change
  - 更长时的 real-target soak

Overall：**Passed**

### Phase 2C Current Minimal Scope

日期：2026-04-21

当前状态：

- safe-window / harness:
  - `xbutton1-hold-then-foreground-change-before-release`: Passed
  - `xbutton1-blocked-hold-then-foreground-return-before-release`: Passed
- real-target Blue Archive:
  - `bluearchive-manual-xbutton-hold-foreground-change`: Passed
  - `bluearchive-manual-xbutton-blocked-hold-return`: Passed

当前解释口径：

- 当前 Phase 2C 只覆盖第一批最小高风险边界，不扩到完整 drag。
- `xbutton1-hold-then-foreground-change-before-release` 当前已证明：
  - allowed 前台按下后会进入 captured session；
  - 切到 blocker 后 release 仍匹配原 captured session；
  - mapped output 的 `down/up` 完整对齐；
  - 没有错误 pass-through、没有重复 dispatch、没有 release 后额外 output。
- `xbutton1-blocked-hold-then-foreground-return-before-release` 当前已证明：
  - blocked 前台按下不会进入 captured session；
  - 即使 foreground 回到目标窗口，也不会 retroactively capture / dispatch；
  - release 按未 capture 语义结束；
  - 没有“回到目标窗口后补发 mapped output”。
- 以下 latest JSON 当前可以作为可复用稳定补充样例：
  - `acceptance-bluearchive-manual-xbutton-hold-foreground-change-latest.json`
  - `acceptance-bluearchive-manual-xbutton-blocked-hold-return-latest.json`

### Phase 2C-B XButton2 Symmetry Scope

日期：2026-04-26

当前状态：

- safe-window / harness:
  - `xbutton2-hold-then-foreground-change-before-release`: Passed
  - `xbutton2-blocked-hold-then-foreground-return-before-release`: Passed
- real-target Blue Archive:
  - `bluearchive-manual-xbutton2-hold-foreground-change`: Passed
  - `bluearchive-manual-xbutton2-blocked-hold-return`: Passed

当前解释口径：

- `xbutton2` 现在已经把 `xbutton1` 的两条最小高风险语义链做成了对称样例。
- `xbutton2-hold-then-foreground-change-before-release` 当前已证明：
  - allowed 前台按下后会进入 captured session；
  - 切到 blocker 后 release 仍匹配原 captured session；
  - mapped output 的 `down/up` 完整对齐；
  - 没有错误 pass-through、没有重复 dispatch、没有 release 后额外 output。
- `xbutton2-blocked-hold-then-foreground-return-before-release` 当前已证明：
  - blocked 前台按下不会进入 captured session；
  - 即使 foreground 回到目标窗口，也不会 retroactively capture / dispatch；
  - release 按未 capture 语义结束；
  - 没有“回到目标窗口后补发 mapped output”。
- 以下 latest JSON 当前可以作为可复用稳定补充样例：
  - `acceptance-bluearchive-manual-xbutton2-hold-foreground-change-latest.json`
  - `acceptance-bluearchive-manual-xbutton2-blocked-hold-return-latest.json`
- 当前还没有证明的下一层边界仍包括：
  - 完整 drag / 多段 mouse move + hold/release
  - `mouse_left` / `mouse_right` 的 hold + foreground change
  - repeated click / double-click
  - 更长时的 real-target soak

Overall：**Passed**

### Phase 2D Current Minimal Drag / Move Scope

日期：2026-04-26

当前状态：

- safe-window / harness:
  - `xbutton2-triggered-drag-minimal`: Passed
  - `xbutton2-triggered-multisegment-move-minimal`: Passed
- real-target Blue Archive:
  - `bluearchive-manual-xbutton2-drag-minimal`: Passed
  - `bluearchive-manual-xbutton2-multisegment-move-minimal`: Passed

当前解释口径：

- 当前 Phase 2D 只覆盖第一批最小 `drag / multi-segment mouse move` 运行时边界，不扩到完整 drag 子线，也不扩到 `mouse_left` / `mouse_right` 物理 trigger。
- `xbutton2-triggered-drag-minimal` 当前已证明：
  - 物理 trigger `mouse_x2` 可稳定进入 trigger / pipeline / runtime 链路；
  - mapped output 固定为 `mouse_middle down -> 1 段短 move_rel -> mouse_middle up`；
  - `drag_started / drag_button_down / move / drag_button_up / drag_completed` 结构化指标完整对齐；
  - 光标起止位置、累计 delta、`cursor_restored` 都可记录；
  - `stop` 后没有额外 output，`stopped_clean=true`。
- `bluearchive-manual-xbutton2-drag-minimal` 当前应按“结构化为主、人工为辅”的 real-target 样例理解：
  - runtime drag 输出链已成立；
  - Blue Archive 中这条最小短 drag 的视觉反应可能不稳定或不明显；
  - 如果人工没有稳定观察到明确拖动反应，不单独判成 runtime 失败。
- `xbutton2-triggered-multisegment-move-minimal` 当前已证明：
  - 物理 trigger `mouse_x2` 可稳定进入 trigger / pipeline / runtime 链路；
  - mapped output 不按住 mapped button，只发固定 3 段 `move_rel`；
  - `move_event_count=3`、`move_segment_count=3`，sequence 可完整结束；
  - 光标起止位置、累计 delta、`cursor_restored` 都可记录；
  - `stop` 后没有额外 output，`stopped_clean=true`。
- `bluearchive-manual-xbutton2-multisegment-move-minimal` 当前具备结构化正证据，且人工观察也已确认存在可解释的分段位移反应。
- 以下 latest JSON 当前可以作为可复用稳定补充样例：
  - `acceptance-bluearchive-manual-xbutton2-drag-minimal-latest.json`
  - `acceptance-bluearchive-manual-xbutton2-multisegment-move-minimal-latest.json`
- 当前还没有证明的下一层边界仍包括：
  - 完整 drag / multi-segment drag
  - drag 中途 foreground change
  - `mouse_left` / `mouse_right` 作为物理 trigger 的 drag / move
  - repeated click / double-click
  - 更长时的 real-target soak

Overall：**Passed**
