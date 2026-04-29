# Release Gate

本文件定义当前 C# baseline 的发布门禁、报告目录约定，以及 real-target manual validation 的适用条件。

当前阶段：

- 当前主线：`next/` C# baseline
- 当前阶段：Core RC0 candidate / GUI RC0 candidate checkpoint; GUI RC0 gate dry run 已通过
- 当前重点：保持当前 release gate、stable supplement samples、Core RC0 candidate、GUI RC0 candidate 与 GUI entry contract 自洽，不重开已收口基线
- 当前不做：GUI 新功能、runtime 新子线、Rust runtime prototype

当前项目状态总入口见 [project-state.md](project-state.md)。GUI 边界见 [gui-entry-contract.md](gui-entry-contract.md)，GUI RC0 manual smoke checklist 见 [gui-manual-smoke-checklist.md](gui-manual-smoke-checklist.md)。Packaging contract 见 [packaging-contract.md](packaging-contract.md)。Professional keymapper contract 见 [professional-keymapper-contract.md](professional-keymapper-contract.md)、[key-name-contract.md](key-name-contract.md)、[macro-language-contract.md](macro-language-contract.md)。

V2 architecture / first usable version planning lives in [first-usable-version-contract.md](first-usable-version-contract.md), [architecture-v2-direction.md](architecture-v2-direction.md), [product-roadmap.md](product-roadmap.md), and [known-issues.md](known-issues.md). These docs do not make release-ready RC true and do not authorize implementation by themselves.

当前 `next/` C# runtime baseline 可以认为是 Core RC0 candidate，`BAKeySmith.App` 可以认为是 GUI RC0 candidate，且 GUI RC0 gate dry run 已通过，但二者都不是 release-ready RC。Release-ready RC 仍必须满足下方 Required Gate、packaging sign-off 和适用 real-target manual validation。

## GUI RC0 Candidate Gate

以下项目是 GUI RC0 candidate 的推荐门禁。它们用于判断当前 WPF shell 是否可以作为 GUI RC0 candidate 固定，不等同于 release-ready RC gate：

1. `dotnet build .\next\BAKeySmith.Next.slnx`
2. Core smoke tests
3. App tests
4. `BAKeySmith.App --smoke`
5. `BAKeySmith.Acceptance --scenario all --burst 50 --drain-timeout 5`
6. `BAKeySmith.Acceptance --scenario lifecycle-stress --burst 50 --drain-timeout 5`
7. [gui-manual-smoke-checklist.md](gui-manual-smoke-checklist.md)

GUI RC0 gate 当前不默认要求：

- `BAKeySmith.Acceptance --scenario trigger-suppress --allow-live-input`
- `BAKeySmith.Acceptance --scenario live-safe --allow-live-input`
- WPF UI automation
- Blue Archive real-target manual validation

如果当前 GUI 变更触及 live mode、Windows hook、input backend、foreground gate、runtime public path、packaging elevation, or real input behavior, then the relevant live / real-target checks must be promoted for that change.

Release-ready RC still requires the full Required Gate below, including soak and applicable Blue Archive manual validation.

## Release-Ready RC Required Gate

以下项目对发布候选版是必过项：

1. `dotnet build .\next\BAKeySmith.Next.slnx`
2. Core smoke tests
3. App tests
4. `BAKeySmith.App --smoke`
5. `BAKeySmith.Acceptance --scenario all --burst 50 --drain-timeout 5`
6. `BAKeySmith.Acceptance --scenario lifecycle-stress --burst 50 --drain-timeout 5`
7. `BAKeySmith.Acceptance --scenario trigger-suppress --allow-live-input --drain-timeout 20`
8. `BAKeySmith.Acceptance --scenario live-safe --allow-live-input --drain-timeout 20`
9. `BAKeySmith.Acceptance --scenario dry-run-soak --soak-seconds 600 --soak-rate 20 --drain-timeout 10`
10. `BAKeySmith.Acceptance --scenario live-soak --allow-live-input --soak-seconds 600 --soak-rate 20 --drain-timeout 10`
11. `bluearchive-manual`
12. `bluearchive-manual-phase2a`
13. GUI manual smoke checklist

Blue Archive live mode currently requires BAKeySmith to run as administrator because Blue Archive is an administrator target and Python beta has the same requirement. GUI release-ready sign-off must verify administrator status display and the non-elevated live-start guard. The guard is implemented in the GUI layer and must remain a release-ready sign-off item, not optional polish.

Professional keymapper contract docs must also be current for release-ready RC:

- key naming and aliases must not overstate side-specific modifier support;
- modifier-only mapping triggers such as `ctrl`, `alt`, and `shift` must remain valid;
- wheel must remain documented as trigger-only until DSL/runtime output support is explicitly added;
- hot reload and GUI editing safety must be documented as product contracts or release-ready gaps;
- GUI editing safety must include both runtime-output blocking and the App-supplied
  hook self-foreground suppress guard: new self-foreground trigger hits pass
  through, while existing captured-session releases remain suppressed and
  dispatched for cleanup;
- control hotkey conflict enforcement must remain covered by Core/App validation tests.

## Packaging Sign-Off Gate

Packaging sign-off is required before release-ready RC:

1. Release self-contained folder publish.
2. Zip / unzip smoke.
3. First launch from extracted folder.
4. Packaged example config load from `examples/config.example.json`.
5. User config save to `%APPDATA%\BAKeySmith\config.json` or an equivalent user-writable path.
6. Live warning / cancel path smoke without clicking OK.
7. Admin / elevation docs check.
8. Elevated and non-elevated status display checks.

Packaging sign-off must not write to a bundled example as if it were the user's default config. First-launch behavior remains a release-ready gap until it is implemented and verified.

Packaging sign-off artifacts such as `examples/config.example.json`, `QUICKSTART.md`, `LIVE-MODE-SAFETY.md`, and `release-summary.md` are future packaging outputs / targets. They are not required to exist in the repository before packaging work begins.

Before any public C# GUI beta / preview, release notes must also link or
summarize current known issues, including Macro editor line-number gutter
clipping, missing side-specific modifiers, missing OEM / punctuation capture,
missing cursor-preserving coordinate backend, and the fact that AppConfigV2 /
RuntimeV2 / Macro DSL v2 are not implemented.

## Only When Touched / Supplement Samples

The following are not default RC gate items unless related public paths are changed and the current change explicitly promotes them to sign-off:

- Phase 2B / 2C / 2D stable supplement samples;
- complete drag product real-target samples;
- wheel / XButton latest JSON;
- phase-specific latest JSON tied to hook, backend, foreground, ownership, drag, reload, suppress, or captured-session public paths.

Stable supplement samples are evidence archives and regression comparison material. They are not automatically default RC gate requirements.

## Deferrable After Release-Ready Planning

The following can remain outside the first release-ready RC gate unless a future release decision explicitly pulls them in:

- installer;
- signing;
- winget / Store;
- WPF UI automation;
- acceptance `Program.cs` split;
- `mouse_left/right`;
- double-click / repeated click;
- broader long soak beyond the current required gate.

## Required When Applicable

real-target manual validation 不是每次随手改文档都必须重跑，但以下情况进入 release candidate 时必须补跑：

- Windows hook / trigger source 变更
- foreground gate 变更
- input backend / `SendInput` 变更
- suppress / captured session 变更
- stop / reload / disable / close 生命周期变更
- macro executor / owner model / long macro cleanup 变更
- 打包、权限、管理员启动、前台聚焦等影响真实目标行为的变更

当前 real-target manual release gate 使用：

- `bluearchive-manual`
- `bluearchive-manual-phase2a`

Phase 2B boundary-expansion 当前新增的 acceptance / harness 场景：

- `wheel-trigger-boundaries`
- `xbutton-trigger-boundaries`
- `xbutton-trigger-reload-disable`
- `xbutton-self-injected-pass-through`

这些场景当前用于 boundary expansion 与最小归档样例，不自动提升为 release candidate 的必过 gate。

Phase 2B 当前新增的 real-target stable samples：

- `acceptance-bluearchive-manual-wheel-latest.json`
- `acceptance-bluearchive-manual-wheel-down-latest.json`
- `acceptance-bluearchive-manual-xbutton-latest.json`
- `acceptance-bluearchive-manual-xbutton2-latest.json`
- `acceptance-bluearchive-manual-xbutton-reload-latest.json`
- `acceptance-bluearchive-manual-xbutton-disable-latest.json`
- `acceptance-bluearchive-manual-xbutton2-disable-latest.json`

这些 real-target 样例当前定义为“稳定补充样例”，而不是默认必过 RC gate。只有当后续改动直接触及 wheel / XButton 的 hook、normalization、suppress、reload / disable 或相关 mouse trigger 语义时，才建议把这组样例提升为当前变更的 release sign-off 必跑项。

Phase 2C 当前新增的 acceptance / harness 场景：

- `xbutton1-hold-then-foreground-change-before-release`
- `xbutton1-blocked-hold-then-foreground-return-before-release`
- `xbutton2-hold-then-foreground-change-before-release`
- `xbutton2-blocked-hold-then-foreground-return-before-release`

这些场景当前用于验证 `captured session + foreground change + mouse hold/release` 的第一批最小边界，不自动提升为 release candidate 的必过 gate。

Phase 2C 当前新增的 real-target stable samples：

- `acceptance-bluearchive-manual-xbutton-hold-foreground-change-latest.json`
- `acceptance-bluearchive-manual-xbutton-blocked-hold-return-latest.json`
- `acceptance-bluearchive-manual-xbutton2-hold-foreground-change-latest.json`
- `acceptance-bluearchive-manual-xbutton2-blocked-hold-return-latest.json`

这两份 real-target 样例当前同样定义为“稳定补充样例”，而不是默认必过 RC gate。只有当后续改动直接触及 hold/release、captured session、foreground change、mouse trigger suppress 或相关公共路径时，才建议把这组样例提升为当前变更的 release sign-off 必跑项。

Phase 2D 当前新增的 acceptance / harness 场景：

- `xbutton2-triggered-drag-minimal`
- `xbutton2-triggered-multisegment-move-minimal`

这些场景当前用于验证最小 `drag / multi-segment move` 输出链，不自动提升为 release candidate 的必过 gate。

Phase 2D 当前新增的 real-target stable samples：

- `acceptance-bluearchive-manual-xbutton2-drag-minimal-latest.json`
- `acceptance-bluearchive-manual-xbutton2-multisegment-move-minimal-latest.json`

这两份 real-target 样例当前同样定义为“稳定补充样例”，而不是默认必过 RC gate。只有当后续改动直接触及 `WindowsInputBackend`、mouse move / drag backend、`InputSequencer`、macro executor、ownership tracker、stop / cleanup 或相关公共路径时，才建议把这组样例提升为当前变更的 release sign-off 必跑项。

Phase 2D-B 当前新增的 acceptance / harness 场景：

- `xbutton2-triggered-drag-stop-during-active-drag`
- `xbutton2-triggered-drag-disable-during-active-drag`

这些场景当前用于验证 active drag 期间的 stop / disable cleanup，不自动提升为 release candidate 的必过 gate。

Phase 2D-B 当前 real-target archive 状态：

- 当前仓库没有 stop / disable active-drag 对应的可复用 Blue Archive manual latest JSON。

因此 Phase 2D-B 当前不能把 stop / disable active-drag real-target latest reports 列为稳定补充样例。只有当后续改动直接触及 stop / disable、drag backend、`InputSequencer`、ownership tracker、runtime session lifecycle 或相关公共路径，并且需要 real-target sign-off 时，才应重新执行对应 manual scenario 并归档 latest JSON。

Phase 2D-C 当前新增的 acceptance / harness 场景：

- `xbutton2-triggered-drag-reload-during-active-drag-old-trigger-blocked`
- `xbutton2-triggered-drag-reload-during-active-drag-new-trigger-allowed`

这些场景当前用于验证 active drag 期间的 reload cleanup 与 generation handoff，不自动提升为 release candidate 的必过 gate。

Phase 2D-C 当前新增的 real-target stable samples：

- `acceptance-bluearchive-manual-xbutton2-drag-reload-during-active-drag-old-trigger-blocked-latest.json`
- `acceptance-bluearchive-manual-xbutton2-drag-reload-during-active-drag-new-trigger-allowed-latest.json`

这两份 real-target 样例当前同样定义为“稳定补充样例”，而不是默认必过 RC gate。只有当后续改动直接触及 reload、runtime generation、drag backend、`InputSequencer`、ownership tracker、stop / cleanup 或相关公共路径时，才建议把这组样例提升为当前变更的 release sign-off 必跑项。

Phase 2D-D 当前新增的 acceptance / harness 场景：

- `xbutton2-triggered-drag-foreground-loss-during-active-drag`
- `xbutton2-triggered-drag-foreground-loss-then-return-before-release`

这些场景当前用于验证 active drag 期间 foreground loss 时的 runtime cancellation、mapped owner cleanup 与 captured release continuity，不自动提升为 release candidate 的必过 gate。

Phase 2D-D 当前新增的 real-target stable samples：

- `acceptance-bluearchive-manual-xbutton2-drag-foreground-loss-during-active-drag-latest.json`
- `acceptance-bluearchive-manual-xbutton2-drag-foreground-loss-then-return-before-release-latest.json`

这两份 real-target 样例当前同样定义为“稳定补充样例”，而不是默认必过 RC gate。只有当后续改动直接触及 foreground gate、captured session、drag backend、`InputSequencer`、ownership tracker、runtime session lifecycle 或相关公共路径时，才建议把这组样例提升为当前变更的 release sign-off 必跑项。

当前对这组 Phase 2D-D 样例的 release 口径需要收紧：

- 它们当前已证明 foreground loss 后不会继续后续 non-cleanup `move/output`；
- 已证明 mapped held owner 最终 cleanup，并且 physical release 仍匹配 captured session；
- 但它们当前**不证明** foreground loss 会“瞬间立即 cleanup”；
- 当前实现语义更准确应解释为 `instruction-boundary / wait-boundary foreground cleanup`，因此这组样例仍按稳定补充样例管理，而不是新增即时 cleanup 的 RC gate 断言。

Phase 2D-E 当前新增的 acceptance / harness 场景：

- `active-pointer-wait-foreground-loss-interrupts`

该场景已并入 `all` 短矩阵，用于验证显式 active pointer script 在长 `wait` 期间发生 foreground loss 时的 interruptible cleanup。

Phase 2D-E 当前 release 口径：

- 已证明显式脚本结构 `press mouse_middle` -> `setpos_rel` / move -> `wait` -> `release mouse_middle` 在 active pointer wait 期间可检测 foreground loss；
- 已证明 cleanup 在完整 wait 结束前发生，cleanup `up` 只发一次，后续 move / release 不继续执行，held owner 清零；
- 已证明 keyboard-only wait 不会因为本轮 hardening 被错误取消；
- 已证明 stop / disable / reload 等 runtime/session cancellation 不会被记成 foreground loss；
- `cleanup_latency_bound_ms=250` 当前只作为 deterministic dry / harness bound，不是 Blue Archive real-target latency SLO；
- 本轮未新增 Blue Archive real-target latest JSON，Phase 2D-E 当前不是新的 real-target stable supplement sample；
- 内建 `drag` / `drag_rel` helper foreground-loss contract 不由 Phase 2D-E 证明；该项由 Phase 2D-F 单独证明。

Phase 2D-F 当前新增的 acceptance / harness 场景：

- `built-in-drag-foreground-loss-contract`

该场景已并入 `all` 短矩阵，用于验证内建 `drag` / `drag_rel` helper 在 mapped drag button down 后发生 foreground loss 时的 cleanup contract。

Phase 2D-F 当前 release 口径：

- 已证明 built-in `drag` / `drag_rel` helper 进入 active pointer helper sequence 后，foreground loss 可在 pointer delay / move 后 release 前被检测；
- 已证明 pre-move foreground loss 不执行 helper move，post-move foreground loss 不把 loss 前已经发生的 helper move 记成错误 output；
- 已证明 foreground loss 后 outer macro 后续 non-cleanup instruction 不继续执行；
- 已证明 helper cleanup `up` 来自 helper drag owner（形如 `owner:drag:<button>`），cleanup `up` 只发一次，held owner 清零；
- 已证明 runtime/session cancellation 不会被记成 built-in drag foreground loss；
- 已证明 normal built-in `drag` / `drag_rel` 没有 foreground loss 时仍正常完成；
- 不承诺打断已经进入单个同步 `MoveMouseToAsync` 的中途调用；
- 不证明 helper button down 之前已经丢失 foreground 时的更广 macro-output gating；
- `cleanup_latency_bound_ms=250` 当前只作为 deterministic dry / harness bound，不是 Blue Archive real-target latency SLO；
- 本轮未新增 Blue Archive real-target latest JSON，Phase 2D-F 当前不是新的 real-target stable supplement sample；
- complete drag / multi-segment drag full product scenario 与 `mouse_left/right` physical trigger 仍是未来缺口。

Phase 2E 第一批当前新增的 acceptance / harness 场景：

- `xbutton2-triggered-complete-drag-normal-completion`
- `xbutton2-triggered-multisegment-drag-normal-completion`

这两个场景已并入 `all` 短矩阵，用于验证 `mouse_x2` 触发、mapped `mouse_middle` 的 explicit complete drag / multisegment drag 正常完成路径。

Phase 2E 第一批当前 release 口径：

- 已证明 dry harness 中 trigger / pipeline / runtime 链路各收到并分发一次 down trigger；
- 已证明 macro started / finished 各为 1；
- 已证明 mapped `mouse_middle` down = 1、up = 1，duplicate up = 0；
- 已证明 complete-drag 1 段 move、multisegment 3 段 move 的 expected delta 与 actual delta 匹配；
- 已证明 completion 后 100ms dry harness probe 内没有额外 output，stop 后也没有额外 output；
- 已证明 completion 后 held owner 清零，runtime stop clean；
- 本轮未改 runtime 公共路径，未新增 macro instruction；
- 本轮未新增 Blue Archive real-target latest JSON，Phase 2E 第一批当前不是新的 real-target stable supplement sample；
- 该结论只覆盖 dry / harness normal-completion，不证明 real-target complete drag SLO，也不重证 stop / disable / reload / foreground-loss during complete drag，不证明 `mouse_left/right` physical trigger。

对于当前这条 release candidate 基线，还必须满足：

- `bluearchive-manual`：Passed
- `bluearchive-manual-phase2a`：Passed

原因：

- 这轮改动直接触及 acceptance / harness / real-target 观测口径；
- 当前 release candidate 需要把 Phase 1 与 Phase 2A 的最新人工证据一并固定成可复用、可交接、可回归的基线。

## Recommended Gate

以下项目不是每次都必须重跑，但推荐在以下情况补跑：

- 变更 acceptance schema 或报告结构时：重跑所有 acceptance 并更新样例报告
- 变更 diagnostics / soak 采样逻辑时：重跑 10 分钟 dry/live soak
- 变更仓库入口、README、迁移声明时：复核 `docs/README.md`、root README、`next/README.md`

## Real-Target Validation Conditions

real-target manual validation 的适用条件：

- Blue Archive 已启动
- 已进入安全、非战斗、可观察输入效果的界面
- acceptance / headless / live runner 以管理员身份运行
- target 与 runner 完整性级别可记录
- 测试者可手动按键或点击鼠标

real-target manual validation 的重跑条件：

- 首次 release candidate
- 变更 hook / suppress / foreground gate / reload / disable / stop / long macro cleanup 相关实现
- 更换打包方式或提升权限启动方式
- 切换目标窗口版本、区域服、启动器路径后怀疑行为不同

## Report Layout

固定样例报告：

- 放在 `next/docs/examples/`
- 用于固化 contract、示例字段和稳定基线
- 文件名使用：
  - `acceptance-<scenario>-latest.json`
  - `acceptance-<scenario>-10min.json`
  - `acceptance-bluearchive-manual-latest.json`

最新人工 real-target 证据：

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
- 其中 `acceptance-bluearchive-manual-phase2a-latest.json` 当前已记录 full-sequence `PASS`
- 以上文件当前都已升级为可复用稳定对照样例，而不再是 partial / in-progress latest evidence

本地 release / validation 报告：

- 推荐放在 `next/reports/<YYYYMMDD-HHmm>/`
- 目录内建议使用以下命名：
  - `01-acceptance-all.json`
  - `02-acceptance-lifecycle-stress.json`
  - `03-acceptance-live-safe.json`
  - `04-acceptance-trigger-suppress.json`
  - `05-acceptance-dry-run-soak-10min.json`
  - `06-acceptance-live-soak-10min.json`
  - `07-acceptance-bluearchive-manual.json`
  - `08-acceptance-bluearchive-manual-phase2a.json`
  - `09-acceptance-wheel-trigger-boundaries.json`
  - `10-acceptance-xbutton-trigger-boundaries.json`
  - `11-acceptance-xbutton-trigger-reload-disable.json`
  - `12-acceptance-xbutton-self-injected-pass-through.json`
  - `release-summary.md`

These numbered JSON files and `release-summary.md` are generated validation outputs for a local release / report directory. They are not archived stable samples unless they are intentionally copied into `next/docs/examples/`.

## Current Interpretation

在当前阶段：

- `all` 与 `lifecycle-stress` 证明 dry-run P0 / lifecycle contract
- `live-safe` 与 `trigger-suppress` 证明 safe-window / harness 下的真实 Windows live 链路
- 10 分钟 dry/live soak 证明长时资源、stop-clean、queue drained
- `bluearchive-manual` 证明 real-target `keyboard-minimal`
- `bluearchive-manual-phase2a` 用于扩 real-target 的最小 mouse / reload / disable / stop / long macro 覆盖
- Phase 2B real-target stable samples 证明 wheel / XButton 的最小 boundary coverage，但当前仍按“稳定补充样例”管理，不默认并入 RC 必过 gate

当前状态说明：

- `bluearchive-manual` 已通过，可作为 Phase 1 real-target 最小键盘路径证据
- `bluearchive-manual-phase2a` 已通过，可作为 Phase 2A current scope 的稳定样例
- `wheel-trigger-boundaries` 已通过，可作为 Phase 2B wheel boundary 的 harness 稳定样例
- `xbutton-trigger-boundaries`、`xbutton-trigger-reload-disable`、`xbutton-self-injected-pass-through` 已通过，可作为 Phase 2B XButton boundary 的 harness 稳定样例
- `acceptance-bluearchive-manual-wheel-latest.json` 与 `acceptance-bluearchive-manual-wheel-down-latest.json` 已通过，可作为 Phase 2B wheel real-target 稳定样例
- `acceptance-bluearchive-manual-xbutton-latest.json`、`acceptance-bluearchive-manual-xbutton2-latest.json`、`acceptance-bluearchive-manual-xbutton-reload-latest.json`、`acceptance-bluearchive-manual-xbutton-disable-latest.json`、`acceptance-bluearchive-manual-xbutton2-disable-latest.json` 已通过，可作为 Phase 2B XButton real-target 稳定样例
- `acceptance-bluearchive-manual-xbutton-hold-foreground-change-latest.json`、`acceptance-bluearchive-manual-xbutton-blocked-hold-return-latest.json`、`acceptance-bluearchive-manual-xbutton2-hold-foreground-change-latest.json`、`acceptance-bluearchive-manual-xbutton2-blocked-hold-return-latest.json` 已通过，可作为 Phase 2C / Phase 2C-B 最小 `button-hold + foreground change` real-target 稳定补充样例
- `acceptance-bluearchive-manual-xbutton2-drag-minimal-latest.json` 与 `acceptance-bluearchive-manual-xbutton2-multisegment-move-minimal-latest.json` 已通过，可作为 Phase 2D 最小 `drag / multi-segment move` real-target 稳定补充样例
- Phase 2D-B 当前只有 stop / disable active-drag harness / acceptance coverage 可作为自动化 contract 样例；对应 real-target latest JSON 当前未归档 / 不可复用
- `acceptance-bluearchive-manual-xbutton2-drag-reload-during-active-drag-old-trigger-blocked-latest.json` 与 `acceptance-bluearchive-manual-xbutton2-drag-reload-during-active-drag-new-trigger-allowed-latest.json` 已通过，可作为 Phase 2D-C `reload-during-active-drag` real-target 稳定补充样例
- 当前 full-sequence stage 稳定通过：keyboard allowed、keyboard blocked、mouse allowed、mouse blocked、reload old trigger blocked、reload new trigger allowed、disable no-output、long macro only
- `F10-only` 与 full-sequence 都已验证 long macro 链路可通
- 之前 full-sequence 下的 long macro 失败，更应归因于旧实现下的观测窗口 / stage 归因问题，而不是 cleanup / macro executor 缺陷
- Phase 2B 当前 wheel + XButton 子线已收口；它扩展了 real-target 边界证据，但不改变现有 RC 必过 gate 主干，也不重开 Phase 1 / Phase 2A 判定
- Phase 2C 当前最小 scope 已证明两条高风险语义链：
  - allowed 前台开始的 captured session，在 foreground change 后仍能正确收尾；
  - blocked 前台开始的 hold，在 foreground return 后不会 retroactively capture / dispatch。
- Phase 2C-B 当前已补齐 `xbutton2` 的对称 hold + foreground change 验证。
- Phase 2D 当前已补齐 `xbutton2` 的最小 `drag / multi-segment move` 输出链验证：
- `drag-minimal` 当前按“结构化为主、人工为辅”管理；
- `multisegment-move-minimal` 当前已具备结构化正证据和人工正证据。
- Phase 2D-B 当前已补齐 `xbutton2` 的 `active drag interruption cleanup` harness / acceptance 验证：
  - `stop-during-active-drag` 与 `disable-during-active-drag` 都已证明 active drag 期间中断、cleanup `up`、无后续 `move/output`、held owner 清零；
  - 对应 Blue Archive real-target latest JSON 当前未归档 / 不可复用，不能列为 stable supplement sample。
- Phase 2D-C 当前已补齐 `xbutton2` 的 `reload-during-active-drag` 验证：
  - old generation 已证明在 active drag 期间被 reload 硬 cleanup；
  - old trigger 已证明退场并按 blocker pass-through 处理；
  - new trigger 已证明在新 generation 下干净接管。
- Phase 2D-D 当前已补齐 `xbutton2` 的 `foreground change during active drag` 验证：
  - foreground loss 已证明发生在 active drag 期间，而不是 normal completion 之后；
  - runtime 已证明会停止后续 non-cleanup `move/output`；
  - mapped held owner 已证明通过 foreground cleanup `up` 释放并清零；
  - physical trigger captured session 已证明保留到 matching `up`，且该 `up` 不 pass-through、不触发新 output；
  - foreground return before release 已证明不会 retroactively resume old drag。
  - 当前样例使用的是显式脚本结构 `press mouse_middle` -> `setpos_rel` / move -> `wait` -> `release mouse_middle`；不证明内建 `drag` / `drag_rel` helper 已经 foreground-aware。
- Phase 2D-E 当前已补齐显式 active pointer wait 的 foreground-aware / interruptible wait hardening：
  - `active-pointer-wait-foreground-loss-interrupts` 已并入 `all`；
  - 已证明 foreground loss 发生在 active pointer wait 期间时，cleanup 不必等完整 wait 结束；
  - dry harness latency bound 不是 Blue Archive real-target SLO；
  - 内建 `drag` / `drag_rel` helper 不由 Phase 2D-E 证明。
- Phase 2D-F 当前已补齐 built-in `drag` / `drag_rel` helper foreground-loss contract：
  - `built-in-drag-foreground-loss-contract` 已并入 `all`；
  - 已证明 helper down 后 active pointer helper sequence 内 foreground loss 会停止后续 non-cleanup output；
  - helper cleanup `up` 来自 helper drag owner，duplicate up 为 0，held owner 清零；
  - runtime/session cancellation 不会被误记成 foreground loss；
  - dry harness latency bound 不是 Blue Archive real-target SLO；
  - complete drag / multi-segment drag full product scenario 与 `mouse_left/right` physical trigger 仍未证明。
- Phase 2E 第一批当前已补齐 `mouse_x2` 触发 mapped `mouse_middle` 的 explicit complete drag / multisegment drag dry harness normal-completion：
  - `xbutton2-triggered-complete-drag-normal-completion` 与 `xbutton2-triggered-multisegment-drag-normal-completion` 已并入 `all`；
  - 已证明 normal completion 下 down/up 各一次、duplicate up 为 0、expected / actual move delta 匹配、completion 后无额外 output、held owner 清零、stop clean；
  - 未新增 real-target latest JSON；
  - stop / disable / reload / foreground-loss during complete drag 的产品场景重证、`mouse_left/right` physical trigger、double-click / repeated click 与 real-target latency / soak 仍未证明。
- Phase 2C / 2C-B 当前还没有证明：
  - stop / disable / reload / foreground-loss during complete drag 的产品场景重证
  - `mouse_left` / `mouse_right` 的 hold + foreground change
  - repeated click / double-click
  - 更长时 real-target soak

只有在 required gate 满足且适用的 real-target manual validation 也满足时，才应标记为可发布候选版。
