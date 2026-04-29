# Acceptance JSON Schema

`BAKeySmith.Acceptance` 是当前 C# baseline 与未来可替换 runtime core 的共同验收口径。任何后续实现都必须在同一套 scenario、同一套字段、同一套单位下比较，不能靠更换报告口径来证明自己更好。

当前状态：

- Phase 1：`Exited`
- Phase 2A current scope：`Closed`
- `bluearchive-manual-phase2a` latest sample：full-sequence `PASS`
- Phase 2B current scope：`Closed (wheel + xbutton real-target boundary samples)`
- Phase 2C / 2C-B current scope：`Closed (xbutton1 + xbutton2 hold/foreground-change stable samples)`
- Phase 2D current minimal scope：`Closed (xbutton2 drag-minimal + multisegment-move-minimal stable samples)`
- Phase 2D-B current scope：`Closed (xbutton2 active-drag interruption cleanup harness coverage; real-target latest sample not archived in this repo)`
- Phase 2D-C current scope：`Closed (xbutton2 reload-during-active-drag stable samples)`
- Phase 2D-D current scope：`Closed (xbutton2 foreground-loss-during-active-drag stable samples)`
- Phase 2D-E current scope：`Closed (explicit active pointer wait foreground-aware / interruptible wait dry harness coverage)`
- Phase 2D-F current scope：`Closed (built-in drag / drag_rel helper foreground-loss dry harness coverage)`
- Phase 2E first batch：`Closed (xbutton2-triggered explicit complete drag / multisegment drag normal completion dry harness coverage)`

## Stability Rules

- JSON 输出是机器可读 contract，human summary 只写到 stderr。
- 顶层字段和 `Scenarios[]` 字段视为稳定字段。
- `Metrics` 是可扩展字典；已有 key 不应改名、删名或改变单位，新指标只能追加。
- 时间单位使用毫秒或秒，字段名必须带单位，例如 `DurationMs`、`DrainTimeoutMs`、`soak_seconds`。
- `Passed=false` 时必须填充 `Error`，并返回非零进程退出码。
- `Runtime` 可为 `.NET`、`Rust` 或其他实现名，但行为语义必须一致。

## Top-Level Report

当前固定形状：

```json
{
  "Tool": "BAKeySmith.Acceptance",
  "Runtime": ".NET",
  "GeneratedAtUtc": "2026-04-20T11:25:49.6729962+00:00",
  "Scenario": "bluearchive-manual-phase2a",
  "BurstCount": 50,
  "DrainTimeoutMs": 5000,
  "SoakSeconds": 300,
  "SoakRateHz": 20,
  "Passed": true,
  "DurationMs": 37675.1583,
  "Scenarios": []
}
```

字段语义：

- `Tool`: report producer name.
- `Runtime`: runtime implementation name.
- `GeneratedAtUtc`: UTC timestamp of report generation.
- `Scenario`: requested scenario name.
- `BurstCount`: requested burst size for burst scenarios.
- `DrainTimeoutMs`: queue drain timeout in milliseconds.
- `SoakSeconds`: requested soak duration in seconds.
- `SoakRateHz`: requested soak trigger rate.
- `Passed`: true only when every scenario passes.
- `DurationMs`: total runner duration in milliseconds.
- `Scenarios`: ordered scenario results.

## Scenario Result

每个 scenario 结果固定为：

```json
{
  "Name": "bluearchive-manual-phase2a",
  "Passed": true,
  "DurationMs": 37656.4223,
  "Error": null,
  "Metrics": {}
}
```

字段语义：

- `Name`: stable scenario id.
- `Passed`: scenario pass/fail.
- `DurationMs`: scenario duration in milliseconds.
- `Error`: null when passed, human-readable reason when failed.
- `Metrics`: scenario-specific stable metrics plus additive future metrics.

## Current Scenario Sets

`all` 当前运行的短矩阵：

- `burst-drain`
- `reload-during-burst`
- `stop-during-long-macro`
- `reload-during-long-macro`
- `foreground-gate-during-burst`
- `active-pointer-wait-foreground-loss-interrupts`
- `built-in-drag-foreground-loss-contract`
- `xbutton2-triggered-complete-drag-normal-completion`
- `xbutton2-triggered-multisegment-drag-normal-completion`
- `lifecycle-start-stop-loop`
- `lifecycle-reload-loop`
- `lifecycle-enable-disable-loop`
- `lifecycle-burst-reload-stop-interleave`

`lifecycle-stress` 当前运行：

- `lifecycle-start-stop-loop`
- `lifecycle-reload-loop`
- `lifecycle-enable-disable-loop`
- `lifecycle-burst-reload-stop-interleave`

`trigger-suppress` 当前运行：

- `mouse-trigger-suppressed`
- `mouse-trigger-foreground-blocked`
- `mouse-trigger-reload-disable-stop-clean`
- `trigger-captured-then-foreground-changes-before-release`
- `captured-repeat-down-does-not-redispatch`
- `self-injected-pass-through`
- `keyboard-trigger-gated-suppress`

real-target manual scenarios：

- `bluearchive-manual`
- `bluearchive-manual-phase2a`
- `bluearchive-manual-wheel`
- `bluearchive-manual-xbutton`
- `bluearchive-manual-xbutton-reload`
- `bluearchive-manual-xbutton-disable`
- `bluearchive-manual-xbutton-hold-foreground-change`
- `bluearchive-manual-xbutton-blocked-hold-return`
- `bluearchive-manual-xbutton2-hold-foreground-change`
- `bluearchive-manual-xbutton2-blocked-hold-return`
- `bluearchive-manual-xbutton2-drag-minimal`
- `bluearchive-manual-xbutton2-multisegment-move-minimal`
- `bluearchive-manual-xbutton2-drag-stop-during-active-drag`
- `bluearchive-manual-xbutton2-drag-disable-during-active-drag`
- `bluearchive-manual-xbutton2-drag-reload-during-active-drag-old-trigger-blocked`
- `bluearchive-manual-xbutton2-drag-reload-during-active-drag-new-trigger-allowed`
- `bluearchive-manual-xbutton2-drag-foreground-loss-during-active-drag`
- `bluearchive-manual-xbutton2-drag-foreground-loss-then-return-before-release`

Phase 2B boundary-expansion scenarios：

- `wheel-trigger-boundaries`
- `xbutton-trigger-boundaries`
- `xbutton-trigger-reload-disable`
- `xbutton-self-injected-pass-through`

Phase 2C boundary-expansion scenarios：

- `xbutton1-hold-then-foreground-change-before-release`
- `xbutton1-blocked-hold-then-foreground-return-before-release`
- `xbutton2-hold-then-foreground-change-before-release`
- `xbutton2-blocked-hold-then-foreground-return-before-release`
- `xbutton2-triggered-drag-minimal`
- `xbutton2-triggered-multisegment-move-minimal`
- `xbutton2-triggered-drag-stop-during-active-drag`
- `xbutton2-triggered-drag-disable-during-active-drag`
- `xbutton2-triggered-drag-reload-during-active-drag-old-trigger-blocked`
- `xbutton2-triggered-drag-reload-during-active-drag-new-trigger-allowed`
- `xbutton2-triggered-drag-foreground-loss-during-active-drag`
- `xbutton2-triggered-drag-foreground-loss-then-return-before-release`

这些场景当前不自动并入 `all`、`lifecycle-stress` 或 `trigger-suppress` 聚合；它们用于在不破坏 Phase 1 / Phase 2A 稳定门禁的前提下，扩展 mouse wheel 与 XButton 的 boundary coverage。

当前解释口径：

- Phase 2B 的 harness scenarios 继续作为 boundary contract 的自动化样例。
- Phase 2B 的 Blue Archive manual latest reports 继续作为 real-target 稳定补充样例。
- 这些样例当前不默认提升为 RC 必过 gate，但在 wheel / XButton 相关公共路径被修改时，应作为 release sign-off 的必跑补充项。
- Phase 2C 的 harness scenarios 继续作为 `captured session + foreground change + mouse hold/release` 的最小 contract 样例。
- Phase 2C 的 Blue Archive manual latest reports 继续作为这一条高风险语义链的 real-target 稳定补充样例。
- 这些样例当前也不默认提升为 RC 必过 gate，但在 hold/release、captured session、foreground change、mouse suppress 或相关公共路径被修改时，应作为 release sign-off 的必跑补充项。
- Phase 2D 的 harness scenarios 继续作为最小 `drag / multi-segment move` 输出链 contract 样例。
- Phase 2D 的 Blue Archive manual latest reports 继续作为这一条新边界的 real-target 稳定补充样例。
- 这些样例当前同样不默认提升为 RC 必过 gate，但在 `WindowsInputBackend`、mouse move / drag backend、`InputSequencer`、macro executor、stop / cleanup 或相关公共路径被修改时，应作为 release sign-off 的必跑补充项。
- Phase 2D-B 的 harness scenarios 继续作为 `active drag interruption cleanup` contract 样例。
- Phase 2D-B 当前仓库没有可复用的 Blue Archive manual latest JSON；不能把不存在的 stop / disable active-drag real-target reports 列为稳定补充样例。
- 如果后续改动触及 stop / disable、drag backend、`InputSequencer`、ownership tracker、runtime session lifecycle 或相关公共路径，并需要 real-target sign-off，必须重新跑对应 manual 场景并归档 latest JSON。
- Phase 2D-C 的 harness scenarios 继续作为 `reload-during-active-drag` contract 样例。
- Phase 2D-C 的 Blue Archive manual latest reports 继续作为这一条 generation handoff 边界的 real-target 稳定补充样例。
- 这些样例当前同样不默认提升为 RC 必过 gate，但在 reload、runtime generation、drag backend、`InputSequencer`、ownership tracker、stop / cleanup 或相关公共路径被修改时，应作为 release sign-off 的必跑补充项。
- Phase 2D-D 的 harness scenarios 继续作为 `foreground change during active drag` contract 样例。
- Phase 2D-D 的 Blue Archive manual latest reports 继续作为这一条 runtime foreground-loss cancellation 边界的 real-target 稳定补充样例。
- 这些样例当前同样不默认提升为 RC 必过 gate，但在 foreground gate、captured session、drag backend、`InputSequencer`、ownership tracker、runtime session lifecycle 或相关公共路径被修改时，应作为 release sign-off 的必跑补充项。
- Phase 2D-E 的 dry harness scenario 继续作为显式 active pointer wait 的 foreground-aware / interruptible wait contract 样例，并已并入 `all` 短矩阵。
- Phase 2D-E 当前没有 Blue Archive real-target latency SLO；`cleanup_latency_bound_ms` 只用于 deterministic dry / harness 判断，不证明真实目标环境下 cleanup latency 永远小于该值。
- Phase 2D-F 的 dry harness scenario 继续作为内建 `drag` / `drag_rel` helper 的 foreground-loss contract 样例，并已并入 `all` 短矩阵。
- Phase 2D-F 当前没有 Blue Archive real-target latency SLO；它不证明 complete drag / multi-segment drag full product scenario，也不证明 `mouse_left/right` physical trigger。
- Phase 2E 第一批 dry harness scenarios 继续作为 `mouse_x2` 触发、mapped `mouse_middle` 的 explicit complete drag / multisegment drag 正常完成路径样例，并已并入 `all` 短矩阵。
- Phase 2E 第一批只证明 normal completion：button down、move segment(s)、button up、macro finished、owner 清零、completion 后无额外 output。它不证明 real-target complete drag SLO，也不重证 interruption / reload / foreground-loss product scenario，不证明 `mouse_left/right` physical trigger。

## Common Metrics

短矩阵、lifecycle、soak 等 scenario 继续使用现有稳定指标，例如：

- `pipeline_queued`
- `pipeline_handled`
- `pipeline_dropped`
- `post_stop_events`
- `stopped_clean`
- `working_set_delta_mb`
- `private_memory_delta_mb`
- `thread_count_delta`
- `cpu_percent_estimate`

`bluearchive-manual` 继续记录 preflight 和最小 real-target keyboard path 指标，例如：

- `validation_scope`
- `runner_elevation_known`
- `runner_elevated`
- `target_elevation_known`
- `target_elevated`
- `runner_target_same_elevation`
- `manual_suppress_confirmed`
- `foreground_blocked_keyboard_pass_through`

## `bluearchive-manual-phase2a` Contract

`bluearchive-manual-phase2a` 当前用于 Phase 2A 的 real-target hardening 与 release gate 收口。它不是游戏侧自动化框架，而是最小必要、可复述、可归档的手动验证场景。

当前固定支持两种 scope：

- `validation_scope = phase2a-full-sequence`
- `validation_scope = phase2a-long-macro-only`

并记录：

- `phase2a_mode = full_minimal | long_macro_only`
- `real_target_observed_scope`
- `real_target_pending_scope`
- `real_target_failed_scope`
- `harness_observed_scope`
- `harness_pending_scope`
- `harness_failed_scope`

当前 full-sequence stage 顺序固定为：

- `keyboard_allowed`
- `keyboard_blocked`
- `mouse_allowed`
- `mouse_blocked`
- `reload_old_trigger_blocked`
- `reload_new_trigger_allowed`
- `disable_no_output`
- `long_macro_only`

## Stage-Level Metrics

从当前 baseline 开始，full `bluearchive-manual-phase2a` 必须输出 stage 级结构化指标，不能只靠整轮 prose summary 归因。

每个 stage 至少保留以下 key：

- `stage_<name>_entered`
- `stage_<name>_completed`
- `stage_<name>_manual_required`
- `stage_<name>_manual_confirmed`
- `stage_<name>_trigger_received_count`
- `stage_<name>_pipeline_enqueued_count`
- `stage_<name>_runtime_dispatched_count`
- `stage_<name>_runtime_foreground_allowed_count`
- `stage_<name>_runtime_ignored_count`
- `stage_<name>_runtime_ignored_reasons`
- `stage_<name>_macro_scheduled_count`
- `stage_<name>_macro_started_count`
- `stage_<name>_escape_down_count`
- `stage_<name>_escape_up_count`
- `stage_<name>_blocked_pass_through_count`
- `stage_<name>_blocked_pass_through_down`
- `stage_<name>_blocked_pass_through_up`
- `stage_<name>_output_delta`
- `stage_<name>_stop_requested_count`
- `stage_<name>_cleanup_completed_count`
- `stage_<name>_failure_reason`

这些字段允许某些 stage 为 `0`，但 key 本身必须存在，方便比较不同 run 是否从同一 stage 开始偏离。

## Long Macro Metrics

`long_macro_only` stage 与 `--manual-long-only` 模式都必须保留以下链路指标：

- `long_macro_failure_stage`
- `long_macro_manual_observation_reached`
- `manual_long_macro_confirmed`
- `long_macro_trigger_received_count`
- `long_macro_pipeline_enqueued_count`
- `long_macro_runtime_dispatched_count`
- `long_macro_runtime_foreground_allowed_count`
- `long_macro_runtime_ignored_count`
- `long_macro_runtime_ignored_reasons`
- `long_macro_macro_scheduled_count`
- `long_macro_macro_started_count`
- `long_macro_escape_down_count`
- `long_macro_escape_up_count`
- `long_macro_stop_requested_count`
- `long_macro_cleanup_completed_count`

当前解释口径已经固定：

- `F10-only` 隔离 run 已证明 long macro path works。
- full-sequence latest run 也已证明 `trigger -> pipeline -> runtime -> macro -> escape down/up -> stop/cleanup` 全链通过。
- 之前 full-sequence 的 long macro 失败，更应归因到旧实现下的观测窗口 / stage 归因问题，而不是 cleanup / macro executor 缺陷。

## Phase 2B Boundary Contracts

Phase 2B 当前只扩两个 real-target boundary：

- `mouse wheel`
- `XButton`

语义约束先于实现：

- wheel 是无状态 one-shot trigger，不参与 captured session。
- wheel allowed：`enabled + trigger hit + foreground allowed` 时 suppress 原始 wheel，并只产生一次 mapped output。
- wheel blocked / disabled / stopped / trigger miss：不输出 mapped result，原始 wheel 透传到当前前台窗口。
- XButton 继续沿用现有 mouse trigger 口径：allowed 时 suppress + mapped output，blocked / disabled / stopped 时 pass-through。
- reload 后旧 XButton trigger 不再 suppress / dispatch，新 trigger 生效。
- self-injected XButton 事件不能回灌 pipeline。

### `wheel-trigger-boundaries`

当前固定输出以下关键指标：

- `wheel_semantics`
- `wheel_participates_captured_session`
- `wheel_allowed_up_original_delta`
- `wheel_allowed_down_original_delta`
- `wheel_allowed_up_mapped_f17_events`
- `wheel_allowed_down_mapped_f18_events`
- `wheel_allowed_up_trigger_received_count`
- `wheel_allowed_down_trigger_received_count`
- `wheel_allowed_up_pipeline_enqueued_count`
- `wheel_allowed_down_pipeline_enqueued_count`
- `wheel_allowed_up_runtime_dispatched_count`
- `wheel_allowed_down_runtime_dispatched_count`
- `wheel_allowed_runtime_foreground_allowed_count`
- `wheel_blocked_up_pass_through`
- `wheel_blocked_down_pass_through`
- `wheel_blocked_output_delta`
- `wheel_blocked_up_trigger_received_count`
- `wheel_blocked_down_trigger_received_count`
- `wheel_blocked_up_pipeline_enqueued_count`
- `wheel_blocked_down_pipeline_enqueued_count`
- `wheel_blocked_up_runtime_dispatched_count`
- `wheel_blocked_down_runtime_dispatched_count`
- `wheel_blocked_up_runtime_ignored_count`
- `wheel_blocked_down_runtime_ignored_count`
- `wheel_blocked_up_runtime_ignored_reasons`
- `wheel_blocked_down_runtime_ignored_reasons`
- `allowed_stopped_clean`
- `blocked_stopped_clean`

### `xbutton-trigger-boundaries`

当前固定输出以下关键指标：

- `xbutton_semantics`
- `xbutton1_allowed_original_down_delta`
- `xbutton1_allowed_original_up_delta`
- `xbutton2_allowed_original_down_delta`
- `xbutton2_allowed_original_up_delta`
- `xbutton1_allowed_mapped_f17_events`
- `xbutton2_allowed_mapped_f18_events`
- `xbutton1_allowed_trigger_received_count`
- `xbutton2_allowed_trigger_received_count`
- `xbutton1_allowed_pipeline_enqueued_count`
- `xbutton2_allowed_pipeline_enqueued_count`
- `xbutton1_allowed_runtime_dispatched_count`
- `xbutton2_allowed_runtime_dispatched_count`
- `xbutton_allowed_runtime_foreground_allowed_count`
- `xbutton1_blocked_pass_through_down`
- `xbutton1_blocked_pass_through_up`
- `xbutton2_blocked_pass_through_down`
- `xbutton2_blocked_pass_through_up`
- `xbutton_blocked_output_delta`
- `xbutton1_blocked_trigger_received_count`
- `xbutton2_blocked_trigger_received_count`
- `xbutton1_blocked_pipeline_enqueued_count`
- `xbutton2_blocked_pipeline_enqueued_count`
- `xbutton1_blocked_runtime_dispatched_count`
- `xbutton2_blocked_runtime_dispatched_count`
- `xbutton1_blocked_runtime_ignored_count`
- `xbutton2_blocked_runtime_ignored_count`
- `xbutton1_blocked_runtime_ignored_reasons`
- `xbutton2_blocked_runtime_ignored_reasons`
- `allowed_stopped_clean`
- `blocked_stopped_clean`

### `xbutton-trigger-reload-disable`

当前固定输出以下关键指标：

- `reload_semantics`
- `old_trigger_pass_through_down`
- `old_trigger_pass_through_up`
- `old_trigger_output_delta`
- `old_trigger_received_count`
- `old_trigger_pipeline_enqueued_count`
- `old_trigger_runtime_dispatched_count`
- `new_trigger_mapped_f18_events`
- `new_trigger_original_down_delta`
- `new_trigger_original_up_delta`
- `new_trigger_received_count`
- `new_trigger_pipeline_enqueued_count`
- `new_trigger_runtime_dispatched_count`
- `new_trigger_runtime_foreground_allowed_count`
- `disabled_output_delta`
- `disabled_pass_through_down`
- `disabled_pass_through_up`
- `disabled_trigger_received_count`
- `disabled_pipeline_enqueued_count`
- `disabled_runtime_dispatched_count`
- `disabled_runtime_ignored_count`
- `disabled_runtime_ignored_reasons`
- `stopped_clean`

### `xbutton-self-injected-pass-through`

当前固定输出以下关键指标：

- `self_injected_pipeline_delta`
- `mapped_output_events`
- `self_injected_xbutton1_hook_trigger_count`
- `self_injected_xbutton2_hook_trigger_count`
- `target_xbutton1_down_delta`
- `target_xbutton1_up_delta`
- `target_xbutton2_down_delta`
- `target_xbutton2_up_delta`
- `stopped_clean`

## Phase 2B Real-Target Stable Samples

Phase 2B 当前已固定以下 Blue Archive manual latest reports：

- `acceptance-bluearchive-manual-wheel-latest.json`
- `acceptance-bluearchive-manual-wheel-down-latest.json`
- `acceptance-bluearchive-manual-xbutton-latest.json`
- `acceptance-bluearchive-manual-xbutton2-latest.json`
- `acceptance-bluearchive-manual-xbutton-reload-latest.json`
- `acceptance-bluearchive-manual-xbutton-disable-latest.json`
- `acceptance-bluearchive-manual-xbutton2-disable-latest.json`

这些样例共同复用了以下稳定字段族：

- `real_target_observed_scope`
- `blocker_observed_scope`
- `manual_*_confirmed`
- `stage_*_trigger_received_count`
- `stage_*_pipeline_enqueued_count`
- `stage_*_runtime_dispatched_count`
- `stage_*_escape_down_count`
- `stage_*_escape_up_count`
- `stage_*_blocked_pass_through_count`
- `stage_*_blocked_pass_through_down`
- `stage_*_blocked_pass_through_up`
- `stage_*_output_delta`
- `stopped_clean`

当前解释口径：

- wheel 已形成 `wheel_up + wheel_down` 的 real-target allowed / blocked 对称样例。
- XButton 已形成 `xbutton1 + xbutton2` 的 allowed / blocked、reload-only、disable-only 对称样例。
- 这些样例当前证明的是最小 boundary coverage，不等价于更复杂鼠标边界已经全部闭环。

## Phase 2C Real-Target Stable Samples

Phase 2C 当前已固定以下 Blue Archive manual latest reports：

- `acceptance-bluearchive-manual-xbutton-hold-foreground-change-latest.json`
- `acceptance-bluearchive-manual-xbutton-blocked-hold-return-latest.json`
- `acceptance-bluearchive-manual-xbutton2-hold-foreground-change-latest.json`
- `acceptance-bluearchive-manual-xbutton2-blocked-hold-return-latest.json`

这些样例共同复用了以下稳定字段族：

- `real_target_observed_scope`
- `real_target_pending_scope`
- `real_target_failed_scope`
- `manual_allowed_hold_confirmed`
- `manual_blocked_hold_confirmed`
- `trigger_down_received_count`
- `trigger_up_received_count`
- `captured_session_entered_count`
- `release_matched_captured_session_count`
- `blocked_pass_through_down_count`
- `blocked_pass_through_up_count`
- `runtime_dispatched_down_count`
- `runtime_dispatched_up_count`
- `mapped_output_down_count`
- `mapped_output_up_count`
- `retroactive_capture_count`
- `unexpected_dispatch_after_foreground_return_count`
- `unexpected_output_after_release_count`
- `stopped_clean`

当前解释口径：

- 这四份样例当前证明的是第一批最小 `button-hold + foreground change` 边界，而不是完整 drag 子线。
- `xbutton1` 与 `xbutton2` 都已经具备：
  - `hold-foreground-change`：allowed 前台开始的 captured session 在前台切走后仍能正确以 release 收尾；
  - `blocked-hold-return`：blocked 前台开始的 hold 在 foreground 回到目标窗口后不会 retroactively capture / dispatch，也不会补发 output。
- 这些样例当前仍是稳定补充样例，不默认并入 RC 必过 gate。

## Phase 2D Real-Target Stable Samples

Phase 2D 当前已固定以下 Blue Archive manual latest reports：

- `acceptance-bluearchive-manual-xbutton2-drag-minimal-latest.json`
- `acceptance-bluearchive-manual-xbutton2-multisegment-move-minimal-latest.json`

这些样例共同复用了以下稳定字段族：

- `validation_scope`
- `mapped_drag_button`
- `manual_drag_confirmed`
- `manual_move_confirmed`
- `trigger_received_count`
- `trigger_down_received_count`
- `trigger_up_received_count`
- `pipeline_enqueued_count`
- `runtime_dispatched_count`
- `macro_started_count`
- `macro_finished_count`
- `drag_started_count`
- `drag_button_down_count`
- `move_event_count`
- `move_segment_count`
- `drag_button_up_count`
- `drag_completed_count`
- `cursor_start_x`
- `cursor_start_y`
- `cursor_end_x`
- `cursor_end_y`
- `cursor_restored`
- `move_total_dx`
- `move_total_dy`
- `stop_requested_count`
- `cleanup_completed_count`
- `unexpected_output_after_stop_count`
- `stopped_clean`

当前解释口径：

- 这两份样例当前证明的是第一批最小 `drag / multi-segment move` 边界，而不是完整 drag 子线。
- `xbutton2-triggered-drag-minimal` 当前以结构化指标为主：
  - trigger / pipeline / runtime 链路、mapped drag button down / move / up、cursor restore 与 stop-clean 都已固定；
  - Blue Archive 中最小短 drag 的视觉反应可能不稳定，因此人工观察只作为辅助，不单独构成失败判定。
- `xbutton2-triggered-multisegment-move-minimal` 当前已具备结构化正证据与人工正证据。
- 这两份样例当前仍是稳定补充样例，不默认并入 RC 必过 gate。

## Phase 2D-B Harness Coverage And Real-Target Archive Gap

Phase 2D-B 当前已固定以下 harness / acceptance scenarios：

- `xbutton2-triggered-drag-stop-during-active-drag`
- `xbutton2-triggered-drag-disable-during-active-drag`

如果后续重新生成 Blue Archive manual reports，它们应复用以下稳定字段族：

- `validation_scope`
- `interrupt_type`
- `trigger_received_count`
- `pipeline_enqueued_count`
- `runtime_dispatched_count`
- `drag_started_count`
- `drag_button_down_count`
- `move_event_before_interrupt_count`
- `interrupt_requested_count`
- `interrupt_while_drag_active_count`
- `drag_completed_normally_count`
- `drag_button_up_from_cleanup_count`
- `move_event_after_interrupt_count`
- `unexpected_output_after_interrupt_count`
- `cleanup_completed_count`
- `held_owner_count_after_cleanup`
- `manual_interrupt_confirmed`
- `stopped_clean`

当前解释口径：

- 这两份样例当前证明的是 active drag 已开始后的 `stop / disable` interruption cleanup，而不是完整 drag 子线。
- `stop-during-active-drag` 与 `disable-during-active-drag` 都必须证明：
  - 中断发生在 active drag 期间，而不是 natural completion 之后；
  - cleanup `up` 由中断路径补发；
  - 中断后没有继续 `move/output`；
  - cleanup 后 held owner 清零。
- 当前仓库没有 stop / disable active-drag 对应的可复用 Blue Archive manual latest JSON，因此不得把它们列为 stable supplement samples。
- Phase 2D-B 当前 remaining gap 是 real-target latest JSON 未归档 / 不可复用。它不改变 Phase 2D-B 已收口判断，但会在相关公共路径变更需要 real-target sign-off 时要求重新执行并归档对应 manual sample。

## Phase 2D-C Real-Target Stable Samples

Phase 2D-C 当前已固定以下 Blue Archive manual latest reports：

- `acceptance-bluearchive-manual-xbutton2-drag-reload-during-active-drag-old-trigger-blocked-latest.json`
- `acceptance-bluearchive-manual-xbutton2-drag-reload-during-active-drag-new-trigger-allowed-latest.json`

这些样例共同复用了以下稳定字段族：

- `validation_scope`
- `trigger_received_count`
- `pipeline_enqueued_count`
- `runtime_dispatched_count`
- `drag_started_count`
- `drag_button_down_count`
- `move_event_before_reload_count`
- `reload_requested_count`
- `reload_generation_before`
- `reload_generation_after`
- `interrupt_while_drag_active_count`
- `drag_completed_normally_count`
- `old_generation_cleanup_completed_count`
- `drag_button_up_from_reload_cleanup_count`
- `move_event_after_reload_count`
- `unexpected_output_after_reload_count`
- `held_owner_count_after_reload_cleanup`
- `old_trigger_after_reload_received_count`
- `old_trigger_after_reload_pipeline_count`
- `old_trigger_after_reload_runtime_count`
- `old_trigger_after_reload_output_delta`
- `old_trigger_after_reload_pass_through_count`
- `new_trigger_after_reload_received_count`
- `new_trigger_after_reload_pipeline_count`
- `new_trigger_after_reload_runtime_count`
- `new_trigger_after_reload_output_delta`
- `new_trigger_after_reload_manual_confirmed`
- `stopped_clean`

当前解释口径：

- 这两份样例当前证明的是 active drag 期间发生 reload 时的 old generation cleanup 与 new generation handoff，而不是完整 drag 子线。
- old generation 当前必须证明：
  - reload 发生在 active drag 期间；
  - cleanup `up` 由 reload 路径补发；
  - reload 后 old generation 不再继续 `move/output`；
  - cleanup 后 held owner 清零；
  - old trigger 在 reload 后不再进入 pipeline/runtime。
- new generation 当前必须证明：
  - `reload_generation_after > reload_generation_before`；
  - new trigger 能被 hook 收到并进入 pipeline/runtime；
  - new trigger 产生预期 mapped output。
- 这些样例当前仍是稳定补充样例，不默认并入 RC 必过 gate。

## Phase 2D-D Real-Target Stable Samples

Phase 2D-D 当前已固定以下 Blue Archive manual latest reports：

- `acceptance-bluearchive-manual-xbutton2-drag-foreground-loss-during-active-drag-latest.json`
- `acceptance-bluearchive-manual-xbutton2-drag-foreground-loss-then-return-before-release-latest.json`

这些样例共同复用了以下稳定字段族：

- `validation_scope`
- `foreground_loss_observed_layer`
- `foreground_loss_cancellation_layer`
- `foreground_cleanup_path`
- `foreground_cleanup_timing_model`
- `foreground_loss_detected_count`
- `foreground_loss_immediate_cleanup_proven`
- `foreground_loss_cleanup_latency_measured`
- `foreground_loss_to_cleanup_ms`
- `cleanup_latency_bound_ms`
- `foreground_loss_to_cleanup_within_expected_bound`
- `foreground_loss_detected_during_wait`
- `foreground_loss_cleanup_latency_note`
- `trigger_received_count`
- `trigger_down_received_count`
- `trigger_up_received_count`
- `pipeline_enqueued_count`
- `runtime_dispatched_count`
- `drag_started_count`
- `drag_button_down_count`
- `normal_drag_button_up_count`
- `drag_button_up_count`
- `move_event_before_foreground_loss_count`
- `foreground_lost_while_drag_active_count`
- `drag_completed_normally_count`
- `drag_button_up_from_foreground_cleanup_count`
- `move_event_after_foreground_loss_count`
- `unexpected_output_after_foreground_loss_count`
- `mapped_owner_cleanup_completed_count`
- `cleanup_completed_count`
- `held_owner_count_after_foreground_cleanup`
- `physical_capture_session_retained_after_foreground_loss`
- `release_matched_captured_session_count`
- `blocked_pass_through_up_count`
- `unexpected_output_after_release_count`
- `foreground_returned_before_release_count`
- `retroactive_resume_after_foreground_return_count`
- `stopped_clean`

当前解释口径：

- 这两份样例当前证明的是 active drag 期间发生 foreground loss 时的 runtime cancellation 与 cleanup，而不是完整 drag 子线。
- 这两份样例使用的是显式脚本结构：`press mouse_middle` -> `setpos_rel` / move -> `wait` -> `release mouse_middle`。
- 这两份样例本身不证明内建 `drag` / `drag_rel` helper 已经 foreground-aware；Phase 2D-F 另以 dry harness 单独覆盖该 helper contract。
- Phase 2D-E 已单独处理显式 active pointer wait 的 foreground-aware / interruptible wait hardening；Phase 2D-F 已单独处理 built-in helper，但两者都不等于 complete drag / multi-segment drag full product scenario。
- 这两份样例当前的 cleanup timing model 必须解释为 `instruction_boundary`：
  - foreground loss 当前由 `MacroExecutor` 在宏执行循环的指令边界检查并观察；
  - 如果 foreground loss 发生在长 `wait` 指令执行期间，cleanup 可能要等到该 `wait` 返回后才被执行；
  - 因此当前 **未证明** foreground loss 会“瞬间立即 cleanup”。
- foreground loss 当前必须由 runtime 层观测并取消后续 non-cleanup output，而不是 acceptance 脚本简单跳过后续 move：
  - `foreground_loss_observed_layer`
  - `foreground_loss_cancellation_layer`
  - `foreground_cleanup_path`
  共同记录了这条行为的实现路径。
- cleanup latency 相关字段当前用于固定语义边界，而不是伪造精确测量值：
  - `foreground_loss_immediate_cleanup_proven = false`
  - `foreground_loss_cleanup_latency_measured = false`
  - 如果没有精确测量，`foreground_loss_to_cleanup_ms` 与 `cleanup_latency_bound_ms` 应保留为 `null`
  - `foreground_loss_cleanup_latency_note` 用于说明当前是 instruction-boundary / wait-boundary cleanup 语义
- mapped output held owner 当前必须证明：
  - foreground loss 发生时 drag 仍 active，且不是 natural completion 之后；
  - cleanup `up` 由 foreground cleanup 路径发出；
  - cleanup 后没有继续 `move/output`；
  - `held_owner_count_after_foreground_cleanup = 0`。
- physical trigger captured session 当前必须证明：
  - foreground loss 后 session 仍保留到 matching physical `up`；
  - physical `up` 匹配原 captured session；
  - physical `up` 不 pass-through 到 blocker；
  - physical `up` 不触发新的 mapped output；
  - foreground return 后不 retroactively resume old drag。
- 这些样例当前仍是稳定补充样例，不默认并入 RC 必过 gate。

## Phase 2D-E Dry Harness Contract

Phase 2D-E 当前新增 dry / harness scenario：

- `active-pointer-wait-foreground-loss-interrupts`

该场景已并入 `all` 短矩阵，用于证明显式脚本结构：

- `press mouse_middle`
- `setpos_rel` / move
- `wait`
- `release mouse_middle`

当前已证明：

- active pointer macro 已经进入 `wait`；
- mapped pointer button 已经 down；
- 至少一个 move 已经发生；
- normal release 尚未发生；
- foreground gate 在 active pointer wait 期间变为 blocked；
- cleanup 在完整 wait 结束前发生；
- cleanup `up` 只发一次，没有 duplicate up；
- wait 后续 move / release 不再执行；
- cleanup 后 held owner 清零；
- keyboard-only wait 不会因为本轮 hardening 被 foreground loss 错误取消；
- stop / disable / reload 等 runtime/session cancellation 不会被记成 foreground loss。

当前未证明：

- Blue Archive real-target cleanup latency SLO；
- foreground loss 在真实目标环境下永远小于 `cleanup_latency_bound_ms`；
- 内建 `drag` / `drag_rel` helper foreground awareness（该项已由 Phase 2D-F 单独证明，不由 Phase 2D-E 证明）；
- complete drag / multi-segment drag full contract。

该场景固定输出以下关键指标：

- `validation_scope`
- `foreground_cleanup_timing_model`
- `cleanup_latency_bound_scope`
- `real_target_cleanup_latency_slo_proven`
- `wait_requested_ms`
- `active_pointer_wait_started_count`
- `foreground_loss_detected_during_wait`
- `foreground_loss_detected_during_wait_count`
- `active_pointer_wait_interrupted_count`
- `foreground_loss_detected_count`
- `foreground_loss_to_cleanup_ms`
- `wait_start_to_cleanup_ms`
- `cleanup_latency_bound_ms`
- `foreground_loss_to_cleanup_within_expected_bound`
- `cleanup_before_full_wait_elapsed`
- `drag_button_down_count`
- `drag_button_up_count`
- `drag_button_up_from_foreground_cleanup_count`
- `duplicate_drag_button_up_count`
- `normal_release_after_foreground_loss_count`
- `move_event_before_foreground_loss_count`
- `move_event_after_foreground_loss_count`
- `unexpected_output_after_foreground_loss_count`
- `held_owner_count_after_foreground_cleanup`
- `foreground_loss_cleanup_latency_measured`
- `foreground_loss_cleanup_latency_note`
- `stopped_clean`

Latency 字段解释：

- `foreground_loss_to_cleanup_ms` 是 dry harness 中 foreground gate 被切到 blocked 到 cleanup up 的测量值。
- `cleanup_latency_bound_ms` 当前是 deterministic dry / harness bound，当前值为 `250`。
- `foreground_loss_to_cleanup_within_expected_bound` 只说明本地 dry harness run 满足该 bound。
- 这些字段不构成 Blue Archive real-target latency SLO，也不证明真实目标环境下 cleanup latency 永远小于该 bound。

## Phase 2D-F Built-In Drag Helper Dry Harness Contract

Phase 2D-F 当前新增 dry / harness scenario：

- `built-in-drag-foreground-loss-contract`

该场景已并入 `all` 短矩阵，用于证明内建 helper 本身：

- `drag`
- `drag_rel`

当前已证明：

- mapped drag button down 后，helper 已进入 active pointer helper sequence；
- normal helper release 尚未发生时，foreground gate 可被 deterministic barrier 切到 blocked；
- `drag` / `drag_rel` 在 pre-move pointer delay 期间发生 foreground loss 时，不执行 helper move，也不继续 outer macro 后续 non-cleanup instruction；
- `drag_rel` 在 post-move delay / move 后 release 前发生 foreground loss 时，不把 loss 前已经发生的 helper move 记成错误 output，并且不继续 outer macro 后续 non-cleanup instruction；
- helper cleanup `up` 来自 helper drag owner（形如 `owner:drag:<button>`）的 `ReleaseKeyAsync`，不是 outer macro owner cleanup；
- cleanup `up` 只发一次，没有 duplicate up；
- cleanup 后 held owner 清零；
- cancellation 发生在 pointer delay 期间时继续按 runtime/session cancellation 处理，不发 foreground-loss diagnostics；
- normal built-in `drag` / `drag_rel` 在没有 foreground loss 时仍正常完成。

当前未证明：

- complete drag / multi-segment drag full product scenario；
- `mouse_left/right` physical trigger；
- foreground 在 helper button down 之前已经丢失时的更广 macro-output gating；
- 已经进入单个同步 `MoveMouseToAsync` 调用后的中途打断；
- Blue Archive real-target cleanup latency SLO。

该场景固定输出以下关键指标：

- `validation_scope`
- `foreground_cleanup_timing_model`
- `cleanup_latency_bound_scope`
- `real_target_cleanup_latency_slo_proven`
- `complete_drag_product_scenario_proven`
- `mouse_left_right_physical_trigger_proven`
- `pre_helper_down_foreground_drift_proven`
- `inflight_move_interrupt_proven`
- `pointer_delay_ms`
- `cleanup_latency_bound_ms`
- `builtin_drag_foreground_loss_detected_count`
- `builtin_drag_button_down_count`
- `builtin_drag_button_up_from_cleanup_count`
- `builtin_drag_move_event_after_foreground_loss_count`
- `builtin_drag_duplicate_up_count`
- `builtin_drag_held_owner_count_after_cleanup`
- `builtin_drag_cancelled_by_runtime_count`
- `builtin_drag_completed_normally_count`
- `foreground_loss_cases_covered`
- `pre_move_loss_cases_covered`
- `post_move_loss_cases_covered`
- `outer_macro_post_loss_instruction_suppressed`
- `helper_cleanup_distinguished_from_outer_owner_cleanup`
- Per-case metrics prefixed with `drag_pre_move_`, `drag_rel_pre_move_`, `drag_rel_post_move_`, `drag_rel_cancellation_`, and `normal_completion_`.

Latency 字段解释：

- `foreground_loss_to_cleanup_ms` 是 dry harness 中 foreground gate 被切到 blocked 到 helper cleanup up 的测量值。
- `cleanup_latency_bound_ms` 当前是 deterministic dry / harness bound，当前值为 `250`。
- `foreground_loss_to_cleanup_within_expected_bound` 只说明本地 dry harness run 满足该 bound。
- 这些字段不构成 Blue Archive real-target latency SLO，也不证明真实目标环境下 cleanup latency 永远小于该 bound。

## Phase 2E First-Batch Complete Drag Normal Completion Dry Harness Contract

Phase 2E 第一批新增 dry / harness scenarios：

- `xbutton2-triggered-complete-drag-normal-completion`
- `xbutton2-triggered-multisegment-drag-normal-completion`

这两个场景已并入 `all` 短矩阵，用于证明产品形态的正常完成路径，但范围保持很窄：

- physical trigger：`mouse_x2`
- mapped drag button：`mouse_middle`
- DSL shape：`press mouse_middle` -> one or more `setpos_rel` segments with short `wait` -> `release mouse_middle`
- complete-drag 场景：1 段 move
- multisegment 场景：3 段 move

当前已证明：

- trigger / pipeline / runtime 链路各自收到并分发一次 down trigger；
- macro started / finished 各为 1；
- mapped `mouse_middle` down = 1、up = 1；
- duplicate `mouse_middle` up = 0；
- `move_segment_count` 与 expected segment count 匹配；
- `expected_move_delta_x/y` 与 `actual_move_delta_x/y` 匹配；
- completion 后 100ms dry harness probe 内没有额外 output；
- stop 后没有额外 output；
- completion 后 held owner count 为 0；
- runtime stop clean。

当前未证明：

- Blue Archive real-target complete drag SLO；
- stop / disable / reload during complete drag；
- foreground-loss during complete drag 的产品场景重证；
- `mouse_left/right` physical trigger；
- double-click / repeated click；
- long real-target soak。

这两个场景固定输出以下关键指标：

- `validation_scope`
- `scenario_scope`
- `real_target_complete_drag_slo_proven`
- `mouse_left_right_physical_trigger_proven`
- `interruption_reload_foreground_loss_product_reproof`
- `trigger_received_count`
- `pipeline_enqueued_count`
- `runtime_dispatched_count`
- `macro_started_count`
- `macro_finished_count`
- `drag_button_down_count`
- `drag_button_up_count`
- `move_segment_count`
- `expected_move_segment_count`
- `expected_move_delta_x`
- `expected_move_delta_y`
- `actual_move_delta_x`
- `actual_move_delta_y`
- `duplicate_drag_button_up_count`
- `unexpected_output_after_completion_count`
- `post_stop_output_count`
- `held_owner_count_after_completion`
- `macro_completed_normally`
- `stopped_clean`
- `post_completion_probe_ms`
- `cursor_start_x`
- `cursor_start_y`
- `cursor_end_x`
- `cursor_end_y`
- `cursor_restored`
- `cursor_restore_scope`
- `move_segments`
- `stable_supplement_latest_json_generated`

当前没有新增 Blue Archive real-target latest JSON。不要把这两个 dry harness normal-completion 场景写成 real-target stable supplement sample。

## Example Reports

当前稳定样例放在 `next/docs/examples/`：

- `acceptance-short-matrix-latest.json`
- `acceptance-lifecycle-stress-latest.json`
- `acceptance-live-safe-latest.json`
- `acceptance-trigger-suppress-latest.json`
- `acceptance-dry-run-soak-10min.json`
- `acceptance-live-soak-10min.json`
- `acceptance-bluearchive-manual-latest.json`
- `acceptance-bluearchive-manual-phase2a-latest.json`
- `acceptance-bluearchive-manual-wheel-latest.json`
- `acceptance-bluearchive-manual-wheel-down-latest.json`
- `acceptance-bluearchive-manual-xbutton-latest.json`
- `acceptance-bluearchive-manual-xbutton2-latest.json`
- `acceptance-bluearchive-manual-xbutton-reload-latest.json`
- `acceptance-bluearchive-manual-xbutton-disable-latest.json`
- `acceptance-bluearchive-manual-xbutton2-disable-latest.json`
- `acceptance-bluearchive-manual-xbutton-hold-foreground-change-latest.json`
- `acceptance-bluearchive-manual-xbutton-blocked-hold-return-latest.json`
- `acceptance-bluearchive-manual-xbutton2-hold-foreground-change-latest.json`
- `acceptance-bluearchive-manual-xbutton2-blocked-hold-return-latest.json`
- `acceptance-bluearchive-manual-xbutton2-drag-minimal-latest.json`
- `acceptance-bluearchive-manual-xbutton2-multisegment-move-minimal-latest.json`
- `acceptance-bluearchive-manual-xbutton2-drag-reload-during-active-drag-old-trigger-blocked-latest.json`
- `acceptance-bluearchive-manual-xbutton2-drag-reload-during-active-drag-new-trigger-allowed-latest.json`
- `acceptance-bluearchive-manual-xbutton2-drag-foreground-loss-during-active-drag-latest.json`
- `acceptance-bluearchive-manual-xbutton2-drag-foreground-loss-then-return-before-release-latest.json`
- `acceptance-wheel-trigger-boundaries-latest.json`
- `acceptance-xbutton-trigger-boundaries-latest.json`
- `acceptance-xbutton-trigger-reload-disable-latest.json`
- `acceptance-xbutton-self-injected-pass-through-latest.json`

其中：

- `acceptance-bluearchive-manual-latest.json` 是 Phase 1 real-target keyboard-minimal 样例。
- `acceptance-bluearchive-manual-phase2a-latest.json` 现在已升级为当前可复用的 full-sequence real-target Phase 2A 稳定样例。
- Phase 2B 的 `acceptance-bluearchive-manual-wheel*` 与 `acceptance-bluearchive-manual-xbutton*` 现在已升级为当前可复用的 wheel / XButton real-target 稳定补充样例。
- Phase 2C / 2C-B 的 `acceptance-bluearchive-manual-xbutton*-hold-foreground-change-latest.json` 与 `acceptance-bluearchive-manual-xbutton*-blocked-hold-return-latest.json` 现在已升级为当前可复用的最小 `button-hold + foreground change` real-target 稳定补充样例。
- Phase 2D 的 `acceptance-bluearchive-manual-xbutton2-drag-minimal-latest.json` 与 `acceptance-bluearchive-manual-xbutton2-multisegment-move-minimal-latest.json` 现在已升级为当前可复用的最小 `drag / multi-segment move` real-target 稳定补充样例。
- Phase 2D-B 当前只有 harness / acceptance coverage 可作为自动化 contract 样例；stop / disable active-drag real-target latest JSON 当前未归档为可复用稳定补充样例。
- Phase 2D-C 的 `acceptance-bluearchive-manual-xbutton2-drag-reload-during-active-drag-old-trigger-blocked-latest.json` 与 `acceptance-bluearchive-manual-xbutton2-drag-reload-during-active-drag-new-trigger-allowed-latest.json` 现在已升级为当前可复用的 `reload-during-active-drag` real-target 稳定补充样例。
- Phase 2D-D 的 `acceptance-bluearchive-manual-xbutton2-drag-foreground-loss-during-active-drag-latest.json` 与 `acceptance-bluearchive-manual-xbutton2-drag-foreground-loss-then-return-before-release-latest.json` 现在已升级为当前可复用的 `foreground change during active drag` real-target 稳定补充样例。
- Phase 2D-E 当前新增的是 dry / harness contract scenario，未新增 Blue Archive real-target latest JSON；不要把 dry harness latency bound 写成 real-target stable supplement sample。
- Phase 2D-F 当前新增的是 dry / harness contract scenario，未新增 Blue Archive real-target latest JSON；不要把 built-in helper dry harness latency bound 写成 real-target stable supplement sample。
- Phase 2E 第一批当前新增的是 dry / harness normal-completion scenarios，未新增 Blue Archive real-target latest JSON；不要把 complete drag normal-completion dry harness 证据写成 real-target stable supplement sample 或 interruption / reload / foreground-loss product reproof。

## Rust Compatibility Requirement

Rust runtime core 只有在满足以下条件时才有资格进入对比：

- 实现相同 runtime contract；
- 在同一 acceptance scenario set 下通过；
- 输出兼容的 Acceptance JSON 形状；
- 保持现有稳定 metric names 与单位；
- 在相同 benchmark / soak / real-target gate 下给出可比结果。
