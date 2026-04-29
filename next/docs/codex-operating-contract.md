# Codex Operating Contract

This document defines how Codex sessions should work in this repository. It is a process contract, not a runtime feature spec.

## 1. Project Positioning

This repository has three deliberately separate tracks:

- Python beta: the current public, downloadable, usable version. It is the behavior baseline, reference implementation, and regression-spec source.
- `next/` C#: the local next-generation mainline. It is the current runtime baseline and engineering baseline.
- Rust: a future replaceable runtime-core reserve path. Do not start Rust work unless explicitly authorized.

Do not overwrite the Python beta, publish a new GitHub release, or move the public repository mainline as a side effect of local `next/` work.

Current project status must be checked through [project-state.md](project-state.md). `BAKeySmith.App` is now a GUI RC0 candidate with GUI RC0 gate dry run passed, but not release-ready GUI. Further GUI work must be explicitly authorized and must follow [gui-entry-contract.md](gui-entry-contract.md). Packaging and release-ready planning must follow [packaging-contract.md](packaging-contract.md). Professional keymapper naming, Macro DSL, GUI capture, hot reload, editing safety, and conflict planning must follow [key-name-contract.md](key-name-contract.md), [macro-language-contract.md](macro-language-contract.md), and [professional-keymapper-contract.md](professional-keymapper-contract.md).

## 2. Behavior Conclusion Rules

Every report must separate these categories:

- Proven: behavior directly supported by passing verification or a named static observation.
- Not proven: behavior that was not covered, did not have enough evidence, or depends on a missing real-target/manual condition.
- Intentionally not being proven now: behavior outside the current authorized scope.
- Failed / pending / invalid sample: any scenario that failed, was not run, or produced a sample that cannot support the claimed conclusion.

Evidence must also be labeled accurately:

- Direct observation: the command, test, manual step, log, or source line directly shows the claim.
- Joint evidence: multiple observations together support the claim.
- Indirect evidence: the claim is inferred from surrounding behavior and must not be stated as a hard guarantee.

Do not overstate sample conclusions. A latest JSON sample proves only the scenario, environment, options, and metrics it actually records.

Phase 2D-D wording is intentionally narrow:

- Proven: after foreground loss, later non-cleanup movement/output does not continue at instruction boundaries or wait boundaries covered by the samples; mapped held ownership is eventually cleaned up; physical release still matches the captured session; foreground return does not resume the old drag.
- Not proven: immediate cleanup at the exact foreground-loss instant.
- Not proven: a foreground-loss cleanup latency bound.
- Not proven: built-in `drag` / `drag_rel` helper foreground awareness. Current Phase 2D-D samples use an explicit script shape: `press mouse_middle` -> `setpos_rel` / move -> `wait` -> `release mouse_middle`.

Therefore Phase 2D-D must be described as instruction-boundary / wait-boundary cleanup, not immediate cleanup.

Phase 2D-E wording is also narrow:

- Proven: for the explicit active pointer script shape `press mouse_middle` -> `setpos_rel` / move -> `wait` -> `release mouse_middle`, foreground loss during the active pointer wait is detected before the full wait elapses; mapped owner cleanup sends one cleanup `up`; later move / release instructions do not run; held owner state is cleared; keyboard-only waits are not cancelled by this hardening; runtime/session cancellation is not reported as foreground loss.
- Not proven: Blue Archive real-target cleanup latency SLO.
- Not proven by Phase 2D-E itself: built-in `drag` / `drag_rel` helper foreground awareness.
- Not proven: complete drag / multi-segment drag full contract.

Phase 2D-E dry harness latency fields, including `cleanup_latency_bound_ms`, must be described as deterministic dry / harness evidence only, not as a real-target latency guarantee.

Phase 2D-F wording is narrow:

- Proven: for built-in `drag` / `drag_rel` after the mapped drag button is down and the active pointer helper sequence has started, foreground loss during pointer delay or after helper move before release stops later non-cleanup output, cleans up the helper drag owner with one `up`, leaves no held owner, and does not report runtime/session cancellation as foreground loss.
- Not proven: complete drag / multi-segment drag full product scenario.
- Not proven: `mouse_left/right` physical trigger.
- Not proven: foreground drift before helper button down.
- Not proven: interruption of a single in-flight `MoveMouseToAsync` call.
- Not proven: Blue Archive real-target cleanup latency SLO.

Phase 2D-F dry harness latency fields, including `cleanup_latency_bound_ms`, must be described as deterministic dry / harness evidence only, not as a real-target latency guarantee.

Phase 2E first-batch wording is narrow:

- Proven: for `mouse_x2`-triggered explicit `mouse_middle` complete drag / multisegment drag scripts composed from `press`, one or more `setpos_rel` moves with short `wait`, and `release`, dry harness normal completion sends one mapped down and one mapped up, emits the expected move segment count and delta, leaves no held owner, emits no post-completion output, and stops cleanly.
- Not proven: Blue Archive real-target complete drag SLO.
- Not proven: stop / disable / reload during complete drag.
- Not proven: foreground-loss during complete drag product-scenario reproof.
- Not proven: `mouse_left/right` physical trigger.
- Not proven: double-click / repeated click or long real-target soak.

Phase 2E first-batch dry harness evidence must not be described as a real-target stable supplement sample unless a real-target latest JSON is actually produced and archived.

## 3. Code Change Rules

Codex must keep code changes scoped to the requested work:

- Do not perform unrelated refactors.
- Do not expand scope opportunistically.
- Do not change only acceptance scripts to manufacture a false pass.
- Do not hide runtime behavior changes behind report or fixture edits.
- Public runtime-path changes must state the affected surface area.
- Runtime contract changes must be backed by appropriate smoke tests, acceptance coverage, and documentation.

If a P0/P1 issue is found, report before broad changes:

- Where the problem is.
- What evidence supports it.
- Which closed baselines or release gates it affects.
- The smallest recommended fix.

Only proceed to the fix when the user has authorized that scope, unless the user explicitly asked for an immediate fix.

## 4. Documentation Sync Rules

Synchronize documentation whenever work affects:

- Runtime contract.
- Project state.
- GUI entry boundary.
- GUI RC0 gate.
- GUI manual smoke checklist.
- Packaging contract.
- Admin / elevation contract.
- Professional key naming contract.
- Macro language contract.
- GUI focused key capture contract.
- Hot reload / running config contract.
- GUI editing safety or control hotkey conflict rules.
- Acceptance scenario.
- Schema fields.
- Release gate.
- Manual validation.
- Latest JSON examples.
- Stable supplement samples.

Every documentation update must state:

- Whether the RC gate changes.
- Whether the material is only a stable supplement sample.
- Whether any field was added, removed, renamed, or semantically changed.
- Whether any closed-phase conclusion changes.
- Whether admin / elevation sign-off is required when Blue Archive live mode is involved.
- Whether modifier-only triggers remain supported when GUI capture or validation changes are made.
- Whether side-specific modifier behavior is current implementation or future target only.

Do not mix release-gate requirements with stable supplement samples. Phase-specific latest JSON files are stable supplement samples unless the current change touches their public path and explicitly promotes them to sign-off for that change.

Blue Archive live mode must not be described as ordinary non-elevated live usage. Blue Archive is currently an administrator target, and Python beta also needs administrator privileges to affect it. GUI elevation status and live elevated guard are implemented in the App layer and must be preserved and verified for release-ready sign-off.

## 5. Test Regression Rules

Choose the verification layer by change scope.

For pure documentation / JSON changes:

- Validate JSON formatting where JSON changed.
- Check documentation wording for contract consistency.

For acceptance-layer changes:

- Build.
- Core smoke tests.
- Related acceptance scenarios.

For runtime public-path changes:

- Build.
- Core smoke tests.
- `Acceptance --scenario all`.
- `Acceptance --scenario lifecycle-stress`.
- `Acceptance --scenario trigger-suppress --allow-live-input`.
- `Acceptance --scenario live-safe --allow-live-input`.

For hook / input backend / foreground / ownership / cleanup changes:

- Run the full required regression serially.
- Do not run live or real-target validation in parallel.
- If real-target validation is required, first state why it is required and what user cooperation is needed.

Live and real-target scenarios must never be used to imply coverage that was not actually executed. Long real-target soak requires explicit authorization.

## 6. Reporting Rules

Every round report must include:

- What was done in this round.
- What was not done in this round.
- Changed files.
- Whether public runtime paths were touched.
- Verification commands and results.
- Whether docs / JSON were synchronized.
- P0 / P1 / P2 risks.
- Recommended next step.
- Whether Codex is stopping.

Avoid vague reporting such as:

- "optimized"
- "cleaned up"
- "should be fine"
- "probably passed"

Reports must say exactly what passed, what was not proven, whether verification was serial, whether real-target/manual validation was involved, and whether the conclusion is backed by structured metrics.

## 7. Prohibited Work Without Explicit Authorization

Do not enter these areas unless the user explicitly authorizes them:

- GUI / editor implementation.
- Rust.
- Complete drag.
- `mouse_left/right`.
- Double-click / repeated click.
- Long real-target soak.
- GitHub release.
- Python beta overwrite, deletion, or replacement.
- Unrelated runtime refactors.

GUI shell planning / productization prep is not GUI implementation, but it must be clearly labeled as planning-only and must not add runtime behavior, AppConfigV2, or Macro DSL instructions.

The current GUI shell has moved beyond planning into a GUI RC0 candidate, and the GUI RC0 gate dry run has passed. Do not report it as release-ready GUI or release-ready RC unless the release-ready gate, packaging sign-off, admin / elevation guard, and applicable manual validation have actually been run and accepted.

When in doubt, stop at static review, local verification, risk reporting, and minimal next-step recommendations.
