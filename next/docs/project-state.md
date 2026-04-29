# Project State

This file is the current status entry point for the local `next/` C# mainline. It summarizes the accepted runtime baseline, the current GUI RC0 candidate decision, and the proof boundaries that must not be overstated.

## Mainline Positioning

- Python beta is the current public, downloadable, usable version. Treat it as the behavior baseline, reference implementation, and regression-spec source.
- `next/` C# is the local next-generation mainline and current runtime / engineering baseline.
- Rust is a future replaceable runtime-core reserve path. Do not start Rust work unless explicitly authorized.
- The public GitHub repository should continue to keep the Python beta. Do not overwrite it, delete it, or publish a new GitHub release as a side effect of local `next/` work.

## Closed Phases

The following scopes are accepted and closed at their currently defined boundaries:

- Phase 1
- Phase 2A
- Phase 2B
- Phase 2C / 2C-B
- Phase 2D
- Phase 2D-B
- Phase 2D-C
- Phase 2D-D
- Phase 2D-E
- Phase 2D-F
- Phase 2E first batch

Closed means the stated scope is accepted. It does not mean adjacent product scenarios or real-target guarantees are automatically proven.

## Core RC0 Candidate Status

Current judgment:

- `next/` C# runtime baseline can be treated as a Core RC0 candidate.
- It is not yet a release-ready RC.
- GUI shell planning / productization prep has completed.
- `BAKeySmith.App` has entered first-stage GUI implementation and can be treated as a GUI RC0 candidate.
- GUI RC0 gate dry run has passed.
- GUI RC0 candidate does not mean release-ready GUI.
- No new runtime subline is open at this checkpoint.

The Core RC0 candidate judgment is based on accepted dry, live, acceptance, and manual evidence across the closed phases, including trigger suppression, lifecycle cleanup, ownership cleanup, foreground-loss handling, interruptible active pointer waits, built-in `drag` / `drag_rel` foreground-loss handling, and Phase 2E first-batch dry complete-drag normal completion.

## GUI RC0 Candidate Status

Current judgment:

- `BAKeySmith.App` can be treated as a GUI RC0 candidate.
- GUI RC0 gate dry run has passed: automatic gate passed and GUI manual smoke passed with P2 polish notes.
- It is not a release-ready GUI.
- It is not a release-ready RC.
- The GUI v1 shell consumes existing `next/` runtime boundaries and does not define new runtime behavior.
- Dry-run remains the default GUI mode.
- Live mode requires an explicit warning and a second confirmation before runtime start.
- AppConfigV1 schema freeze remains in force.
- Macro DSL v1 freeze remains in force.

Accepted GUI shell coverage includes:

- app shell;
- config load / save;
- AppConfigV1 field editing;
- mapping list editor;
- root and mapping-level unknown field preservation;
- macro text editor v1;
- macro compiler diagnostics display;
- runtime start / stop / reload wiring through `RuntimeHostController`;
- dry-run simulate down / up;
- runtime status / snapshot display;
- diagnostics log with bounded entries;
- foreground probe display;
- live-mode warning and confirmation gate.

GUI RC0 manual smoke checklist:

- [gui-manual-smoke-checklist.md](gui-manual-smoke-checklist.md)

Packaging / release-readiness contract:

- [packaging-contract.md](packaging-contract.md)

Professional keymapper contracts:

- [key-name-contract.md](key-name-contract.md)
- [macro-language-contract.md](macro-language-contract.md)
- [professional-keymapper-contract.md](professional-keymapper-contract.md)

V2 design and first usable version contracts:

- [first-usable-version-contract.md](first-usable-version-contract.md)
- [architecture-v2-direction.md](architecture-v2-direction.md)
- [product-roadmap.md](product-roadmap.md)
- [known-issues.md](known-issues.md)

## Why This Is Not Release-Ready RC

Release-ready RC still requires the current release gate and applicable real-target validation to be satisfied for the exact candidate being shipped.

Current gaps include:

- full current RC gate must be rerun and archived for the candidate;
- packaging contract must be converted into an actual publish / zip / extraction / first-launch sign-off;
- admin / elevation status and live elevated guard must be included in release-ready GUI sign-off;
- 10-minute dry-run soak must be current for the candidate;
- 10-minute live soak must be current for the candidate;
- `bluearchive-manual` must be current for the candidate;
- `bluearchive-manual-phase2a` must be current for the candidate;
- real-target complete drag SLO is not proven;
- `mouse_left/right` physical trigger is not proven;
- double-click / repeated click is not proven;
- long real-target product soak for newer drag scenarios is not proven.

Release-ready GUI additionally still needs packaging / elevation / distribution sign-off and any required manual GUI sign-off for the candidate being shipped.

Known admin / elevation fact:

- Blue Archive currently runs as an administrator target.
- The Python beta also needs to run as administrator to affect Blue Archive.
- Therefore C# GUI Blue Archive live mode must require BAKeySmith to run as administrator.
- Dry-run and config editing can remain usable without administrator privileges.
- `admin/elevation status + live elevated guard` has been implemented in the GUI layer and must be preserved and verified in release-ready sign-off.
- Non-elevated Blue Archive live start is blocked before live confirmation / `RuntimeHostController.StartAsync`.

## Current RC Gate

The default release candidate gate remains:

1. `dotnet build .\next\BAKeySmith.Next.slnx`
2. Core smoke tests
3. `BAKeySmith.Acceptance --scenario all --burst 50 --drain-timeout 5`
4. `BAKeySmith.Acceptance --scenario lifecycle-stress --burst 50 --drain-timeout 5`
5. `BAKeySmith.Acceptance --scenario live-safe --allow-live-input --drain-timeout 20`
6. `BAKeySmith.Acceptance --scenario trigger-suppress --allow-live-input --drain-timeout 20`
7. `BAKeySmith.Acceptance --scenario dry-run-soak --soak-seconds 600 --soak-rate 20 --drain-timeout 10`
8. `BAKeySmith.Acceptance --scenario live-soak --allow-live-input --soak-seconds 600 --soak-rate 20 --drain-timeout 10`
9. `bluearchive-manual`
10. `bluearchive-manual-phase2a`

Stable supplement samples are not default RC gate items unless the current change touches their public path and explicitly promotes them to release sign-off for that change.

## GUI RC0 Candidate Gate

The GUI RC0 candidate gate is separate from the release-ready RC gate:

1. `dotnet build .\next\BAKeySmith.Next.slnx`
2. Core smoke tests
3. App tests
4. `BAKeySmith.App --smoke`
5. `BAKeySmith.Acceptance --scenario all --burst 50 --drain-timeout 5`
6. `BAKeySmith.Acceptance --scenario lifecycle-stress --burst 50 --drain-timeout 5`
7. [gui-manual-smoke-checklist.md](gui-manual-smoke-checklist.md)

`trigger-suppress --allow-live-input` and `live-safe --allow-live-input` are not default GUI RC0 requirements unless the GUI change touches live mode, hook behavior, input backend behavior, or runtime public paths. Release-ready RC still requires the full release gate, including soak and applicable Blue Archive manual validation.

WPF UI automation is deferrable and is not a current GUI RC0 blocker.

Latest GUI RC0 gate dry run result:

- automatic gate: PASS;
- GUI manual smoke: PASS with P2 polish notes;
- no live input, real-target validation, Blue Archive manual validation, long soak, GitHub release, or Python beta overwrite was performed.

Current P2 GUI polish notes:

- layout still has visual / experience polish room;
- duplicate trigger error feedback visibility needs improvement;
- dry-run `输入事件` wording should be made more accurate, such as output events / 输出事件.
- Macro editor UX polish can be fixed as `PASS with P2 known issue`:
  completion popup, contextual completion, popup lifecycle, Esc / Tab / Enter
  behavior, and diagnostics duplicate reduction are accepted; line-number
  gutter count improved; line-number gutter clipping remains P2 and is
  deferred.

## Packaging Contract

The current packaging plan is documented in [packaging-contract.md](packaging-contract.md).

Current packaging position:

- first C# GUI preview should use a portable zip;
- self-contained folder publish is preferred;
- no installer, MSIX, Store, winget, or auto-update for the first package;
- packaged examples should live under `examples/config.example.json`;
- recommended user config location is `%APPDATA%\BAKeySmith\config.json`;
- GUI first-launch path hygiene is implemented: missing user config loads an
  in-memory default and no longer auto-selects legacy root `config.example.json`;
- first-launch config creation / copy behavior is still a release-ready gap;
- the C# GUI preview must not replace Python beta or imply release-ready RC.

Release-ready packaging sign-off must include publish, zip / unzip smoke, first launch from extracted folder, packaged example load, user config save to a user-writable path, live warning / cancel smoke, and admin / elevation docs/status checks.

## Professional Keymapper Contract

Professional key naming, Macro DSL semantics, hot reload / edit safety, conflict rules, focused capture v1, and future advanced capture boundaries are documented in:

- [key-name-contract.md](key-name-contract.md)
- [macro-language-contract.md](macro-language-contract.md)
- [professional-keymapper-contract.md](professional-keymapper-contract.md)

Current accepted keymapper boundary:

- modifier-only triggers such as `ctrl`, `alt`, and `shift` are existing product capability from the Python beta baseline and are supported by GUI focused key capture v1.
- C# next currently supports generic `ctrl` / `alt` / `shift`; side-specific modifiers such as `left_ctrl` and `right_ctrl` currently fold into generic modifiers and are future professional milestone work.
- `mouse_wheel_up/down` are trigger-only. They are not mapped hold buttons, drag buttons, or Macro DSL `press` / `release` / `tap` outputs.
- GUI focused key capture v1 stays focused-window only, keeps manual input fallback, and does not use global hooks or runtime dispatch.
- advanced capture, raw input / scan-code fallback, side-specific modifiers, and focused WPF-unstable keys such as Windows, PrintScreen, Apps/Menu, OEM, IME, or international keys remain future work or manual-input fallback.

Current v2 design boundary:

- BAKS primarily targets Chinese users. UI labels, errors, search aliases, and
  documentation should be Chinese-friendly first, while canonical config and
  Macro DSL names remain English ASCII.
- AppConfigV1 remains frozen. Do not keep adding professional v2 concepts into
  V1 string fields.
- Macro DSL v1 remains frozen for current `next`, but it is not the final
  professional macro language.
- Current `MacroEditorControl` is not the final professional editor route.
- Future design should proceed through Input Model v2, Key Name v2, Input
  Capture v2, Conflict Model v2, Runtime v2, Macro DSL v2, Blue Archive
  coordinate model, and AppConfigV2.
- The first usable C# GUI version must be genuinely usable: mapping edit/save,
  dry-run preview, live run, stop/reload cleanup, simple tap/hold, macro
  lifecycle, safe running edit/capture, and coordinate capture plus a usable
  `tap_at` fallback path.

See:

- [input-model-v2-design.md](input-model-v2-design.md)
- [key-name-v2-contract.md](key-name-v2-contract.md)
- [input-capture-v2-design.md](input-capture-v2-design.md)
- [conflict-model-v2-design.md](conflict-model-v2-design.md)
- [runtime-v2-design-notes.md](runtime-v2-design-notes.md)
- [macro-dsl-v2-design-notes.md](macro-dsl-v2-design-notes.md)
- [macro-editor-assistance-v2.md](macro-editor-assistance-v2.md)
- [bluearchive-coordinate-model.md](bluearchive-coordinate-model.md)
- [appconfig-v2-design-notes.md](appconfig-v2-design-notes.md)

## Stable Supplement Samples

Stable supplement samples are archived reports that preserve phase-specific evidence. They are useful for regression comparison and sign-off when related public paths change.

They are not automatically default RC gate requirements.

Examples:

- Phase 2B wheel / XButton samples support wheel and XButton trigger boundary evidence.
- Phase 2C / 2C-B samples support captured session plus foreground-change hold evidence.
- Phase 2D samples support minimal drag / multisegment move evidence.
- Phase 2D-C samples support reload during active drag evidence.
- Phase 2D-D samples support foreground-loss during active drag evidence within their timing model.

Known stable supplement boundary:

- Phase 2D-B stop / disable active-drag real-target latest JSON is not archived in this repository right now. Treat it as a real-target archive gap, not as a stable supplement sample.
- Phase 2D-E and Phase 2D-F did not add Blue Archive real-target latest JSON. Their latency bounds are deterministic dry / harness metrics only.
- Phase 2E first batch did not add Blue Archive real-target latest JSON. It is dry / harness normal-completion evidence only.

## Proven

Current accepted proof includes:

- Python beta remains the public baseline and reference implementation.
- `next/` C# is the runtime / engineering baseline.
- trigger suppression, captured trigger sessions, self-injected pass-through, lifecycle cleanup, reload / disable / stop cleanup, foreground gate behavior, and ownership cleanup have accepted coverage at the scoped levels documented in the release gate.
- Phase 2D-E proves explicit active pointer `wait` can be interrupted by foreground loss before the full wait elapses in deterministic dry / harness coverage.
- Phase 2D-F proves built-in `drag` / `drag_rel` helper foreground-loss contract after the mapped drag button is down and the active helper sequence has started.
- Phase 2E first batch proves dry / harness normal completion for `mouse_x2` triggered mapped `mouse_middle` explicit complete drag and multisegment drag.
- Phase 2E first batch proves down/up symmetry, expected / actual move delta match, duplicate up = 0, no post-completion output, held owner cleared, and clean stop for those dry / harness normal-completion scenarios.
- `BAKeySmith.App` proves the current GUI RC0 candidate shell at the App-test / smoke-test level for config editing, mapping editing, unknown-field preservation, macro diagnostics, dry runtime control wiring, diagnostics display models, and live-start confirmation gate.
- `BAKeySmith.App` GUI RC0 gate dry run has passed with manual smoke P2 polish notes.

## Not Proven

Current accepted proof does not include:

- release-ready RC status for the current candidate;
- release-ready GUI status for `BAKeySmith.App`;
- packaging, installer, signing, or distribution readiness;
- WPF UI automation coverage;
- Blue Archive real-target evidence from GUI workflows;
- Blue Archive real-target complete drag SLO;
- real-target cleanup latency SLO for Phase 2D-E / 2D-F;
- `mouse_left/right` physical trigger;
- double-click / repeated click;
- long real-target soak for newer product drag scenarios;
- stop / disable / reload / foreground-loss during complete drag product-scenario reproof;
- pre-helper-down foreground drift safety as a completed contract;
- interruption of a single in-flight `MoveMouseToAsync` call.

Phase 2E first batch is not:

- Blue Archive real-target complete drag SLO;
- proof that `mouse_left/right` physical triggers work;
- proof that stop / disable / reload / foreground-loss during complete drag product scenarios are revalidated;
- proof of a real-target latency SLO.

Dry / harness latency bounds are deterministic harness evidence only. They must not be reported as Blue Archive real-target SLOs.

## Intentionally Not Being Proven Now

The current checkpoint intentionally does not start:

- new runtime work;
- new GUI features beyond the current GUI RC0 candidate shell;
- Rust;
- GitHub release;
- Python beta replacement;
- `mouse_left/right`;
- double-click / repeated click;
- long real-target soak;
- complete drag interruption / reload / foreground-loss product-scenario reproof;
- acceptance `Program.cs` refactor.

## GUI Entry Decision

GUI shell planning / productization prep has completed. First-stage GUI implementation has produced a GUI RC0 candidate in `BAKeySmith.App`.

Further GUI work remains separate explicit scope and must stay inside the GUI entry contract. GUI code must consume existing runtime boundaries instead of creating new runtime behavior.

Required GUI entry companion document:

- [gui-entry-contract.md](gui-entry-contract.md)

## GUI Boundary Still In Force

For the current GUI RC0 candidate and any future GUI work, keep these P2 items closed or explicitly accepted:

- `project-state.md` remains the state entry point.
- `gui-entry-contract.md` remains the GUI scope boundary.
- AppConfigV1 schema freeze is documented and respected.
- Macro DSL v1 freeze is documented and respected.
- GUI save path is required to use existing serializer / validation paths.
- GUI must preserve unknown config fields.
- GUI must not introduce AppConfigV2 without explicit authorization.
- GUI must not add macro DSL commands without explicit authorization.

## Deferrable Until After GUI Shell

These P2 items can wait until after the GUI RC0 candidate:

- acceptance `Program.cs` split;
- real-target complete drag SLO;
- complete drag stop / disable / reload / foreground-loss product-scenario reproof;
- `mouse_left/right` physical trigger;
- double-click / repeated click;
- long real-target soak for newer product drag scenarios;
- Phase 2E real-target latest JSON;
- acceptance report-writer / helper cleanup.
- WPF UI automation spike.
- installer/signing strategy after portable zip planning.

## Current Largest Risks

- State drift: many phases and sample classes make it easy to overstate what is proven.
- GUI release drift: GUI RC0 candidate can be overstated as release-ready GUI unless the GUI gate and manual checklist are recorded.
- GUI write path risk: future GUI config changes can accidentally change config semantics unless AppConfigV1 freeze and unknown-field preservation are respected.
- DSL drift: future macro editor work can accidentally become new language design unless Macro DSL v1 is frozen.
- V2 drift: future coordinate, trigger, capture, or macro features can
  accidentally become AppConfigV1 / DSL v1 patchwork unless the v2 architecture
  docs are followed.
- Professional keymapper drift: future capture UX or validation can accidentally shrink Python beta key capability, especially modifier-only triggers, unless the key-name contract is followed.
- Acceptance maintainability: `BAKeySmith.Acceptance` is large and should eventually be split, but not before the GUI entry state is fixed.
- Real-target gap: complete drag product SLO remains unproven in Blue Archive.
- Release-ready sign-off gap: Blue Archive live mode requires BAKeySmith administrator elevation, and the implemented GUI elevation status / live guard must be rechecked for the exact release candidate.

## Recommended Next Subline

Recommended next single subline:

- repository docs cleanup / stale reference cleanup, then GUI editing safety enforcement or remaining GUI UX polish.

Focused key capture v1 is already accepted and archived. Future capture work must not reopen runtime work, schema, or DSL. It must preserve modifier-only triggers, keep manual input fallback, and stay inside the focused-window capture contract unless a later explicit contract change authorizes a broader capture layer.
