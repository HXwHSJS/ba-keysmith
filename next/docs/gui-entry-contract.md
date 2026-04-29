# GUI Entry Contract

This document defines the allowed first GUI phase after the Core RC0 candidate readiness decision. It began as a planning contract and now governs the implemented `BAKeySmith.App` GUI RC0 candidate. It does not authorize GUI behavior outside the boundaries below.

## Status

- `next/` C# runtime baseline can be treated as a Core RC0 candidate.
- Release-ready RC is not yet established.
- GUI shell planning / productization prep has completed.
- First-stage GUI implementation has produced a `BAKeySmith.App` GUI RC0 candidate.
- GUI RC0 gate dry run has passed with P2 polish notes.
- GUI RC0 candidate is not release-ready GUI.
- Release-ready GUI still needs packaging sign-off plus admin / elevation status and live elevated guard verification.
- This contract does not reopen runtime work.

See [project-state.md](project-state.md) for the current project state entry point.
See [packaging-contract.md](packaging-contract.md) for the packaging, admin / elevation, and release-ready GUI contract.
See [key-name-contract.md](key-name-contract.md), [macro-language-contract.md](macro-language-contract.md), and [professional-keymapper-contract.md](professional-keymapper-contract.md) for professional keymapper naming, DSL, capture, hot reload, and conflict contracts.
See [first-usable-version-contract.md](first-usable-version-contract.md) and [architecture-v2-direction.md](architecture-v2-direction.md) for v2 design boundaries.

## First-Phase GUI Goals

GUI v1 first phase may implement a thin shell over existing runtime contracts.

Allowed first-phase surface:

- app shell;
- config load / save;
- mapping list editor;
- existing AppConfigV1 field editor;
- macro text editor v1;
- validation / diagnostics display;
- runtime start / stop / reload wiring;
- dry-run controls;
- runtime status panel;
- diagnostics panel.
- foreground probe display;
- live-mode warning and start confirmation.

The GUI must treat runtime behavior as already owned by the `next/` C# runtime baseline. GUI work should make existing behavior visible and editable, not define new runtime semantics.

## AppConfigV1 Schema Freeze

GUI v1 may edit only the current AppConfigV1 schema.

Allowed top-level fields:

- `target_process`
- `hotkey`
- `tap_hold_ms`
- `mappings`

Allowed mapping fields:

- `id`
- `trigger`
- `type`
- `target`
- `mode`
- `script`

Requirements:

- preserve extension data / unknown fields;
- do not introduce AppConfigV2;
- do not add, remove, rename, or change schema semantics without explicit authorization;
- do not change the existing config loader / serializer behavior;
- do not change Python beta's role as behavior baseline and reference implementation;
- save config through `AppConfigSerializer.Save`;
- load / validate config through the existing serializer and runtime config conversion path;
- surface serializer / validation diagnostics instead of inventing separate GUI-only config rules.

Current source support:

- `AppConfigV1` has top-level `[JsonExtensionData] ExtraFields`;
- `MappingConfigV1` has mapping-level `[JsonExtensionData] ExtraFields`;
- `AppConfigSerializer.Save` serializes AppConfigV1 with the existing JSON options.

If a future GUI design discovers that unknown-field preservation is insufficient in practice, stop and report the gap before changing the config layer.

## Macro DSL V1 Freeze

GUI macro editor v1 is a text editor over the existing macro DSL.

Allowed editor behavior:

- edit macro script text;
- validate with `MacroScriptCompiler`;
- show compiler diagnostics;
- optionally show command / key-name hints based on the existing DSL;
- save script text back to the existing `script` mapping field.

Frozen DSL v1 commands:

- `press`
- `release`
- `tap`
- `wait`
- `loop`
- `end`
- `combo`
- `setpos`
- `setpos_rel`
- `drag`
- `drag_rel`

Not allowed in GUI v1:

- new DSL instructions;
- changed DSL semantics;
- GUI-only macro execution rules;
- visual macro builder;
- treating GUI validation as a runtime behavior change;
- accepting inputs that `MacroScriptCompiler` rejects.

Macro DSL v1 is not the final professional macro language. Future Macro DSL v2,
Action Model v2, coordinate actions, and AppConfigV2 must be designed through
the v2 documents rather than added ad hoc to this GUI v1 contract.

## Live Mode Safety

Dry-run must remain the default GUI mode.

Live mode is allowed only as an explicit GUI state:

- unchecking dry-run must show a visible warning;
- starting runtime while dry-run is off must ask for a second confirmation;
- the confirmation must state that live mode installs a global hook and sends real input;
- cancelling confirmation must not call `RuntimeHostController.StartAsync`;
- live / dry-run UI state must not be written into AppConfigV1.

Known Blue Archive live-mode requirement:

- Blue Archive currently runs as an administrator target.
- Python beta also requires administrator privileges to affect Blue Archive.
- Therefore C# GUI Blue Archive live mode must require BAKeySmith to run as administrator.
- Dry-run and config editing may run without administrator privileges.
- GUI must show whether BAKeySmith is elevated.
- Non-elevated Blue Archive live start must be blocked before live confirmation and before `RuntimeHostController.StartAsync`.
- `admin/elevation status + live elevated guard` is implemented in the GUI layer and must be preserved and verified in release-ready sign-off.

## Runtime Boundary

GUI must depend on public composition and runtime boundaries:

- `RuntimeHost`;
- `IRuntimeCore`;
- `RuntimeHostSnapshot`;
- diagnostics stream;
- `AppConfigSerializer`;
- `MacroScriptCompiler`.

GUI must not:

- directly mutate runtime internals;
- bypass `RuntimeHost` lifecycle;
- bypass `AppConfigSerializer`;
- bypass `MacroScriptCompiler` diagnostics;
- create a second config parser;
- create a second macro language;
- directly manage held-owner state;
- hide runtime errors behind GUI-only success states.

## Focused Key Capture Boundary

Focused key capture v1 is implemented in `BAKeySmith.App` after explicit
authorization and remains inside the GUI boundary.

Required boundary:

- capture only while `BAKeySmith.App` has focus;
- do not use global hooks;
- do not start runtime;
- do not send real input;
- do not change Core runtime, AppConfigV1, or Macro DSL;
- keep manual input fallback;
- allow focused capture while runtime is running; BAKeySmith.App foreground input
  is protected by App foreground dispatch blocking and live hook self-foreground
  pass-through hardening;
- allow modifier-only mapping triggers such as `ctrl`, `alt`, and `shift`;
- support simple target capture for the current simple output key / mouse button
  set;
- keep hotkey capture and mapping trigger capture as different semantics;
- treat `escape` as a bindable key and use an explicit cancel button instead of hard-coded Esc cancellation;
- defer wheel capture / wheel target capture while keeping manual
  `mouse_wheel_up/down` trigger input.

Detailed contract: [professional-keymapper-contract.md](professional-keymapper-contract.md).

## Explicitly Forbidden In First Phase

Do not do these in GUI v1 first phase:

- add runtime behavior;
- add macro DSL instructions;
- introduce AppConfigV2;
- bypass existing serializer / validation paths;
- directly change runtime internal state;
- introduce Rust;
- overwrite, delete, or replace Python beta;
- publish a GitHub release;
- add `mouse_left/right` physical trigger support;
- add double-click / repeated-click product behavior;
- add long real-target soak automation;
- reprove complete drag interruption / reload / foreground-loss product scenarios.

## Validation Expectations

Minimum GUI RC0 candidate validation should include:

- build;
- Core smoke tests;
- App tests;
- GUI smoke;
- config load / save round trip preserving unknown fields;
- invalid config diagnostics are surfaced;
- invalid macro diagnostics are surfaced;
- runtime start / stop / reload wiring works in dry-run mode;
- live-mode confirmation gate is covered at App-test level;
- [gui-manual-smoke-checklist.md](gui-manual-smoke-checklist.md) is executed for manual GUI confidence;
- no runtime public-path change unless separately authorized and covered by the required runtime regression gate.

For docs-only planning changes, full acceptance is not required. Check links and wording instead.

WPF UI automation can be added later. It is not a current GUI RC0 blocker.

## Current Macro Editor Known Issue

Macro editor UX polish is accepted as `PASS with P2 known issue`:

- completion popup and contextual completion policy are accepted;
- popup lifecycle, Esc / Tab / Enter behavior, and diagnostics duplicate
  reduction are accepted;
- line-number gutter count improved;
- line-number gutter clipping remains P2 and is deferred.

Current `MacroEditorControl` is not the final Macro Editor v2 route. Future
options include editor control refactor, AvalonEdit evaluation, or hiding the
line-number gutter before public beta.

## Reporting Rules

Every GUI-related report must state:

- whether work was planning-only or implementation;
- whether runtime public paths were touched;
- whether AppConfigV1 schema changed;
- whether Macro DSL semantics changed;
- whether unknown fields are preserved;
- whether validation used existing serializer / compiler paths;
- what remains unproven.
