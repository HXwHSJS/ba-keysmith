# Phase 1 Exit Criteria

Phase 1 的目标不是继续扩功能，而是收口 C# baseline 的 runtime 可信度：

> 先证明 C# baseline 在真实 Windows live/runtime 条件下足够硬，再决定是否值得让 Rust core 接管。

在本文件标记为通过前：

- 不切 Rust。
- 不恢复 GUI / 宏编辑器体验类投入。
- 不扩张功能面。

## Required Exit Criteria

| 条件 | 当前状态 | 证据 |
| :--- | :--- | :--- |
| 10 分钟 dry-run soak 通过 | Passed | `docs/examples/acceptance-dry-run-soak-10min.json` |
| 10 分钟 live soak 通过 | Passed | `docs/examples/acceptance-live-soak-10min.json` |
| acceptance 全部稳定通过 | Passed | `docs/examples/acceptance-short-matrix-latest.json`，当前 `all` 包含 9 个 dry-run P0 / lifecycle 场景 |
| mouse/keyboard gated suppress 稳定通过 | Passed | `docs/examples/acceptance-trigger-suppress-latest.json` |
| self-injected pass-through 稳定通过 | Passed | `docs/examples/acceptance-trigger-suppress-latest.json` |
| stop / reload / disable / long macro 清理稳定通过 | Passed | `all`、`live-safe`、`lifecycle-stress` |
| 示例 JSON 报告与 schema 固定 | Passed | `docs/acceptance-schema.md` 与 `docs/examples/` |
| 至少一次真实 Blue Archive 目标环境人工验证通过 | Passed | `docs/examples/acceptance-bluearchive-manual-latest.json` |
| 文档口径与实现一致 | Passed | 本文件、`phase-1-live-runtime-plan.md`、`acceptance-schema.md`、`migration-statement.md`、`manual-bluearchive-validation.md` 已收口 |

Phase 1 当前结论：**Exited**。

本阶段的必需条件已经满足：安全窗口 live-safe、trigger-suppress、10 分钟 live soak，以及真实 Blue Archive 前台窗口最小人工验证都已通过；并且真实目标验证是在 runner / target 同为 elevated 的前提下完成的。

## Preferred Exit Criteria

| 条件 | 当前状态 | 证据 |
| :--- | :--- | :--- |
| lifecycle stress 通过 | Passed | `docs/examples/acceptance-lifecycle-stress-latest.json` |
| memory/thread 没有明显持续增长 | Passed for current scope | 10 分钟 soak 与 lifecycle stress 报告未见持续增长迹象 |
| Python / C# 主次关系明确 | Passed | `migration-statement.md` |
| 风险 backlog 已记录且不分散主线 | Passed | `phase-1-live-runtime-plan.md` |

## Current Automated Baseline

本轮收口后的自动化基线：

- `dotnet build next\BAKeySmith.Next.slnx`：通过，0 warnings / 0 errors。
- Core smoke tests：通过。
- `BAKeySmith.Acceptance --scenario all --burst 50 --drain-timeout 5`：9/9 通过。
- `BAKeySmith.Acceptance --scenario lifecycle-stress --burst 50 --drain-timeout 5`：4/4 通过。
- `BAKeySmith.Acceptance --scenario live-safe --allow-live-input --drain-timeout 20`：通过。
- `BAKeySmith.Acceptance --scenario trigger-suppress --allow-live-input --drain-timeout 20`：6/6 通过。
- `dry-run-soak --soak-seconds 600 --soak-rate 20`：通过。
- `live-soak --allow-live-input --soak-seconds 600 --soak-rate 20`：通过。

## Lifecycle Stress Result

`lifecycle-stress` 是 Phase 1 主线的一部分，先以 deterministic dry-run 为准：

- `lifecycle-start-stop-loop`：50 次 start/stop，post-stop input 0，held / owner 归零。
- `lifecycle-reload-loop`：50 次 reload，旧 trigger 不输出，新 trigger 输出，generation 正确推进。
- `lifecycle-enable-disable-loop`：50 次 disable/enable，disabled 不输出，enabled 恢复输出。
- `lifecycle-burst-reload-stop-interleave`：50 轮 long macro -> reload -> burst -> stop，stop 返回后 post-stop input 0，held / owner / queue 均归零。

Stop 期间允许必要的 cleanup release，例如释放 held key；Phase 1 的硬标准是 `StopAsync` 返回后不能再继续发输入。

## Stop / Dropped Trigger Semantics

当 stop 与 trigger burst 交错时，pipeline 中尚未处理的 trigger 不应被 drain 成新的映射输出。它们会被取消并计入 `pipeline_dropped`，同时 `pipeline_pending_after_stop` 必须为 `0`。

这点是 stop 硬语义的一部分：

- stop 后不能继续处理旧 trigger。
- stop 后不能继续发映射/宏输出。
- stop 后 snapshot 必须显示 pipeline drained / dropped clean。
- 必要的 held-input release 必须在 stop 返回前完成。

## Manual Target Validation Gate

真实目标人工验证必须保持最小必要、可复述、可归档：

- 只验证标准 Windows hook / foreground gate / SendInput 链路在 Blue Archive 前台窗口中的行为边界。
- 不做自动化游戏侧压力测试。
- 不做绕过保护或反作弊的实现。
- 不把这部分扩张成新的测试框架。
- 第一轮真实目标验证先用真实手动 keyboard trigger 做最小路径，不再用 `SendInput` 生成 trigger。
- 若 runner 不是管理员，本轮真实目标验证应记为 invalid，而不是 runtime 失败。

只有 `manual-bluearchive-validation.md` 中的 checklist 至少完成一次并记录为 passed，Phase 1 才能正式 exit。

## Real Target Validation Result

最新真实目标验证结果：

- target process: `BlueArchive`
- runner elevated: `true`
- target elevated: `true`
- same elevation level: `true`
- manual trigger: `f8`
- allowed foreground: runtime emitted `escape` tap (`keyboard_escape_events=2`)
- blocked foreground: original `f8` passed through to blocker (`foreground_blocked_keyboard_pass_through=1`), mapped output delta `0`
- stop: `7.71ms`
- clean stop: `true`

结论：真实 Blue Archive 最小 keyboard path 已通过，满足 Phase 1 exit 所需的真实目标环境人工验证条件。
