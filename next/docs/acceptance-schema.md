# Acceptance JSON Schema

`BAKeySmith.Acceptance` 是 C# baseline 与未来 Rust runtime core 的共同验收口径。任何 runtime 想替换当前 C# core，都必须能在同一套场景下输出兼容报告，不能更换指标口径来证明自己更好。

## Stability Rules

- JSON 输出是机器可读 contract，human summary 只写到 stderr。
- 顶层字段和 `Scenarios[]` 字段视为稳定字段。
- `Metrics` 是可扩展字典，已有 key 不应改名或改变单位，新指标只能追加。
- 时间单位使用毫秒或秒，字段名必须带单位，例如 `DurationMs`、`DrainTimeoutMs`、`soak_seconds`。
- `Passed=false` 时必须填充 `Error`，并返回非零进程退出码。
- `Runtime` 可为 `.NET`、`Rust` 或其他实现名，但行为语义必须一致。

## Commands

短 Phase 1 矩阵：

```powershell
dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario all --burst 50 --drain-timeout 5
```

lifecycle stress：

```powershell
dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario lifecycle-stress --burst 50 --drain-timeout 5 --output .\reports\acceptance-lifecycle-stress.json
```

dry-run soak：

```powershell
dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario dry-run-soak --soak-seconds 300 --soak-rate 20 --drain-timeout 10 --output .\reports\acceptance-dry-run-soak.json
```

live soak：

```powershell
dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario live-soak --allow-live-input --soak-seconds 300 --soak-rate 20 --drain-timeout 10 --output .\reports\acceptance-live-soak.json
```

real target manual：

```powershell
dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario bluearchive-manual --allow-live-input --target-process BlueArchive.exe --manual-trigger f8 --manual-timeout 15 --manual-confirm yes
```

## Top-Level Report

Current shape:

```json
{
  "Tool": "BAKeySmith.Acceptance",
  "Runtime": ".NET",
  "GeneratedAtUtc": "2026-04-19T12:25:30.4716781+00:00",
  "Scenario": "all",
  "BurstCount": 50,
  "DrainTimeoutMs": 5000,
  "SoakSeconds": 300,
  "SoakRateHz": 20,
  "Passed": true,
  "DurationMs": 1915.5491,
  "Scenarios": []
}
```

Field contract:

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

Each scenario result has:

```json
{
  "Name": "burst-drain",
  "Passed": true,
  "DurationMs": 1608.7741,
  "Error": null,
  "Metrics": {}
}
```

Field contract:

- `Name`: stable scenario id.
- `Passed`: scenario pass/fail.
- `DurationMs`: scenario duration in milliseconds.
- `Error`: null when passed, human-readable reason when failed.
- `Metrics`: scenario-specific stable metrics plus additive future metrics.

## Current Scenario Set

`all` currently runs the short Phase 1 matrix:

- `burst-drain`
- `reload-during-burst`
- `stop-during-long-macro`
- `reload-during-long-macro`
- `foreground-gate-during-burst`
- `lifecycle-start-stop-loop`
- `lifecycle-reload-loop`
- `lifecycle-enable-disable-loop`
- `lifecycle-burst-reload-stop-interleave`

`all` deliberately does not include soak because it must remain fast enough for frequent runtime regression checks.

Soak scenarios:

- `dry-run-soak`
- `live-soak`
- `soak` is an alias for `dry-run-soak`

Lifecycle stress scenarios:

- `lifecycle-start-stop-loop`
- `lifecycle-reload-loop`
- `lifecycle-enable-disable-loop`
- `lifecycle-burst-reload-stop-interleave`
- `lifecycle-stress` runs the full lifecycle stress set.

Live trigger suppression scenarios:

- `mouse-trigger-suppressed`
- `mouse-trigger-foreground-blocked`
- `mouse-trigger-reload-disable-stop-clean`
- `trigger-captured-then-foreground-changes-before-release`
- `self-injected-pass-through`
- `keyboard-trigger-gated-suppress`
- `trigger-suppress` runs the full trigger suppression scenario set.

These scenarios must validate gated suppress semantics: original input is suppressed only when runtime is enabled, the event hits a configured trigger, and foreground is allowed. Foreground-blocked trigger input must pass through to the current foreground window and must not produce mapped/macro output.

Real target manual scenario:

- `bluearchive-manual`

`bluearchive-manual` is a guided real-target validation scenario. It is intentionally narrower than live-safe and should not become a game-side automation framework. Its current Phase 1 contract is:

- it must run from an elevated acceptance/headless/live runner;
- it must record whether the target process is elevated, whether the runner is elevated, and whether both are at the same elevation level;
- a non-elevated runner invalidates the real-target verdict and must be reported as a bad validation premise, not as proof that runtime correctness failed;
- the first pass uses a physical user keyboard trigger instead of generating the trigger via `SendInput`;
- it should allow the human observation result to be supplied explicitly via `--manual-confirm yes|no` when the runner itself is non-interactive;
- the first pass validates the minimal keyboard path before mouse-trigger manual checks continue.

## Common Metrics

Burst and lifecycle scenarios use stable metric names such as:

- `burst_count`
- `input_events`
- `emit_ms`
- `pipeline_queued`
- `pipeline_handled`
- `pipeline_dropped`
- `generation`
- `post_stop_events`

`dry-run-soak` currently records:

- `soak_seconds`
- `soak_rate_hz`
- `observed_trigger_rate_hz`
- `triggers_emitted`
- `expected_input_events`
- `input_events`
- `diagnostic_events`
- `pipeline_queued`
- `pipeline_handled`
- `pipeline_pending_after_drain`
- `runtime_pending_actions_after_drain`
- `runtime_running_actions_after_drain`
- `held_key_count_after_drain`
- `press_owner_count_after_drain`
- `stop_ms`
- `post_stop_events`
- `stopped_clean`

Lifecycle stress scenarios should record additive metrics such as:

- `loops`
- `disabled_trigger_probes`
- `enabled_trigger_probes`
- `pipeline_queued`
- `pipeline_handled`
- `pipeline_dropped`
- `pipeline_pending_after_stop`
- `runtime_pending_actions_after_stop`
- `runtime_running_actions_after_stop`
- `held_key_count_after_stop`
- `press_owner_count_after_stop`
- `stop_cleanup_events_allowed`
- `post_stop_events`
- `stopped_clean`
- `pipeline_pending_max`
- `runtime_pending_actions_max`
- `runtime_running_actions_max`
- `active_workers_max`
- `held_key_count_max`
- `press_owner_count_max`
- `working_set_start_mb`
- `working_set_end_mb`
- `working_set_delta_mb`
- `private_memory_start_mb`
- `private_memory_end_mb`
- `private_memory_delta_mb`
- `thread_count_start`
- `thread_count_end`
- `thread_count_delta`
- `cpu_percent_estimate`

`live-soak` records the same lifecycle/resource metrics and additionally records:

- `target_process`
- `foreground_process`
- `expected_output_events`
- `output_events`
- `f14_output_events`
- `foreground_loss_count`
- `foreground_refresh_count`

Trigger suppression scenarios should record additive metrics such as:

- `original_input_received_by_target`
- `original_input_received_by_blocker`
- `mapped_output_events`
- `blocked_output_delta`
- `captured_down_suppressed`
- `captured_up_suppressed`
- `self_injected_pipeline_delta`
- `allowed_original_key_down_delta`
- `blocked_original_key_down_delta`
- `post_stop_events`
- `stopped_clean`

`bluearchive-manual` should additionally record preflight and manual-step metrics such as:

- `validation_scope`
- `manual_trigger`
- `manual_trigger_mode`
- `runner_elevation_known`
- `runner_elevated`
- `runner_elevated_state`
- `target_elevation_known`
- `target_elevated`
- `target_elevated_state`
- `runner_target_same_elevation`
- `manual_suppress_confirmed`
- `foreground_blocked_keyboard_pass_through`

## Snapshot Metrics

When `--include-snapshots` is passed, scenario metrics may include keys named `snapshot_<name>`.

Snapshots are useful for diagnosis but are not the primary stable comparison unit. Rust parity should first match pass/fail semantics and stable scalar metrics; snapshot DTO parity can follow the runtime contract.

## Example Reports

The repository keeps stable sample reports under `next/docs/examples/` so C# baseline and future Rust runtime candidates can compare against the same shape and metric names.

- `acceptance-dry-run-soak-10min.json`: 10-minute dry-run soak at 20Hz.
- `acceptance-live-soak-10min.json`: 10-minute live soak at 20Hz with real Windows hook, foreground gate, and `SendInput`.
- `acceptance-short-matrix-latest.json`: short Phase 1 dry-run matrix for frequent runtime regression checks.
- `acceptance-lifecycle-stress-latest.json`: deterministic dry-run lifecycle stress for start/stop/reload/enable-disable/interleaved stop.
- `acceptance-live-safe-latest.json`: short live-safe regression with allowed foreground, blocked foreground, reload, disable, long macro stop, and clean stop.
- `acceptance-trigger-suppress-latest.json`: gated suppress regression for keyboard, mouse, captured sessions, and self-injected pass-through.
- `acceptance-bluearchive-manual-latest.json`: latest real-target manual validation sample or blocked preflight report.

Current 10-minute sample baseline:

- dry-run: `12000` triggers, `24000` input events, queue drained, held inputs `0`, post-stop input `0`, stop about `7.10ms`.
- live: `12000` triggers, `24000` output events, queue drained, held inputs `0`, post-stop input `0`, stop about `8.67ms`, foreground loss `0`.
- lifecycle stress: `4/4` scenarios passed at 50 loops, post-stop input `0`, held inputs `0`, queue/pipeline clean after stop.

## Rust Compatibility Requirement

Rust core is eligible only if it can:

- implement the same runtime contract behavior;
- pass this scenario set without weaker semantics;
- emit the same Acceptance JSON shape;
- keep existing stable metric names and units;
- show measurable improvement under the same benchmark and soak commands.
