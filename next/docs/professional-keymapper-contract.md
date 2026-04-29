# Professional Keymapper Contract

This document records the professional keymapper product contract for BA
KeySmith. It connects key naming, Macro DSL semantics, hot reload, GUI editing
safety, conflict rules, and future extension boundaries.

This is product contract documentation. Individual sections may describe current
implemented behavior or future boundaries; this document itself does not
authorize runtime, schema, DSL, plugin, profile, layer, or packaging changes.

## Status

- Python beta remains the current public usable version and regression-spec
  source.
- `next/` C# is the local runtime baseline and GUI RC0 candidate track.
- GUI RC0 gate dry run has passed.
- Release-ready RC is not established.
- AppConfigV1 schema freeze remains in force.
- Macro DSL v1 freeze remains in force.
- Dry-run remains default.
- Live mode requires second confirmation.
- Admin / elevation status and live elevated guard are implemented in the GUI
  layer and must be preserved.
- Focused GUI key capture v1 is implemented in the App layer only. It remains
  focused-only, uses no global hook, does not run runtime, and keeps manual
  input fallback.

Companion contracts:

- [key-name-contract.md](key-name-contract.md)
- [macro-language-contract.md](macro-language-contract.md)
- [gui-entry-contract.md](gui-entry-contract.md)
- [runtime-contract.md](runtime-contract.md)
- [release-gate.md](release-gate.md)
- [first-usable-version-contract.md](first-usable-version-contract.md)
- [architecture-v2-direction.md](architecture-v2-direction.md)
- [known-issues.md](known-issues.md)

## Professional Baseline

BA KeySmith should be treated as a professional keymapper, not only as a game
macro demo.

Professional baseline requirements:

- full key naming must be explicit;
- old Python beta capabilities must not be silently weakened;
- modifier-only triggers such as `ctrl`, `alt`, and `shift` remain valid
  mapping triggers;
- mouse button and wheel trigger boundaries must be clear;
- GUI capture must not invent runtime behavior or reject legal manual input;
- runtime reload must be transactional;
- editing the GUI must not accidentally trigger active mappings;
- control hotkeys must not conflict with mapping triggers;
- advanced future features must be planned without weakening current contracts.

BAKS primarily targets Chinese users. UI text, error messages, settings,
search aliases, docs, and public teaching material should be Chinese-friendly.
Canonical config names and Macro DSL commands should remain English ASCII.

## Current Implemented Capability

Implemented with current evidence:

- simple mapping with `tap` and `hold`;
- Macro DSL v1 text scripts;
- key / mouse button output through ownership tracker;
- mouse wheel as trigger-only;
- dry-run backend;
- live-mode confirmation;
- admin / elevation status and non-elevated live guard in GUI;
- foreground gate;
- captured trigger session;
- self-injected input pass-through marking;
- ownership cleanup;
- stop / disable / reload cleanup;
- explicit active pointer wait foreground-loss hardening;
- built-in `drag` / `drag_rel` foreground-loss helper contract;
- dry harness complete drag / multisegment drag normal completion;
- GUI config editor;
- GUI macro diagnostics through `MacroScriptCompiler`;
- GUI runtime diagnostics and manual smoke checklist.

Implemented but not fully proven:

- exhaustive all-key keyboard matrix;
- `f13` through `f24`;
- numpad names;
- OEM punctuation;
- lock-key side effects;
- `pause`;
- `mouse_left/right` as real-target physical product triggers.

Not currently implemented:

- side-specific modifier binding;
- Windows key binding;
- print screen / apps / menu;
- raw virtual-key or scan-code fallback;
- wheel output;
- global GUI key capture in C#;
- profiles / layers / per-app profile;
- chord / sequence trigger;
- double-tap / repeated-click product feature;
- plugin API.

## V2 Architecture Boundary

Current C# `next` remains the engineering baseline. V2 design proceeds in
parallel and should not be squeezed into AppConfigV1 or Macro DSL v1.

V2 design documents:

- [input-model-v2-design.md](input-model-v2-design.md)
- [key-name-v2-contract.md](key-name-v2-contract.md)
- [input-capture-v2-design.md](input-capture-v2-design.md)
- [conflict-model-v2-design.md](conflict-model-v2-design.md)
- [runtime-v2-design-notes.md](runtime-v2-design-notes.md)
- [macro-dsl-v2-design-notes.md](macro-dsl-v2-design-notes.md)
- [macro-editor-assistance-v2.md](macro-editor-assistance-v2.md)
- [bluearchive-coordinate-model.md](bluearchive-coordinate-model.md)
- [appconfig-v2-design-notes.md](appconfig-v2-design-notes.md)

Key v2 commitments:

- InputSpec should be shared by trigger, simple target, macro key action,
  control hotkey, coordinate record hotkey, and emergency stop.
- OEM / punctuation keys and side-specific modifiers are first usable v2
  requirements, not current implementation.
- Conflict Model v2 must distinguish config conflict, live blocker, and risk
  warning.
- Macro DSL v2 is a breaking redesign and should compile to Action Model v2 /
  Macro IR.
- Runtime v2 executes `MappingActivationSession`, with runtime-owned cleanup.
- Blue Archive coordinate mappings use client-area logical profiles.
- AppConfigV2 is the future professional config model.

## Trigger Semantics Boundary

Trigger semantics define what input condition starts a mapping. Macro DSL
semantics define what output actions run after a mapping has already been
triggered. These two layers must stay separate.

Current trigger semantics are intentionally narrow:

- a mapping trigger is one normalized key, mouse button, or mouse wheel name;
- keyboard and mouse button triggers are based on down / up events;
- mapping dispatch currently starts from the trigger down path;
- matching up is still important for captured sessions and hold release;
- wheel triggers are pulse events with no matching up;
- simple mappings choose output mode after trigger:
  - `tap` sends one complete output press: down -> configured duration -> up;
  - `hold` holds mapped output while the captured trigger session is active;
  - macro mappings execute Macro DSL output instructions after trigger.

This means:

- `tap`, `hold`, and `macro` are mapping action modes, not trigger grammar.
- Macro DSL commands such as `tap`, `combo`, `drag`, or `loop` do not define
  how the mapping is triggered.
- A future trigger feature must not be hidden inside Macro DSL implementation.
- A future Macro DSL output command must not be used to imply a new trigger
  condition.

Trigger features that are not implemented now but need explicit future design:

- trigger on key down vs trigger on key up;
- tap-trigger vs hold-trigger vs tap-hold threshold trigger;
- double-tap trigger;
- repeated-click trigger;
- chord trigger, meaning multiple keys/buttons held together as a trigger;
- sequence trigger, meaning ordered input events before dispatch;
- layer-triggered mappings;
- profile switch triggers;
- plugin-driven trigger conditions.

Most of these require schema and runtime design. They cannot be safely added as
small AppConfigV1 edits, GUI-only validation, or Macro DSL aliases.

Recommended milestone split:

- Public beta before release:
  - do not add chord, sequence, layer, or profile triggers;
  - keep the current single-trigger model honest in docs;
  - do not block future trigger routes in naming or GUI capture design.
- Professional milestone:
  - side-specific modifiers;
  - raw input / scan-code fallback;
  - profile and layer model.
- Future major:
  - sequence triggers;
  - plugin-driven triggers;
  - visual macro builder;
  - complex trigger orchestration.

Future trigger design must define:

- config schema representation;
- conflict and overlap rules;
- down / up capture and suppress behavior;
- foreground gate interaction;
- stop / disable / reload cleanup;
- dry-run and acceptance metrics;
- GUI validation and manual editing fallback.

This section is a boundary contract only. It does not implement any new trigger
semantics.

## Hot Reload / Running Config Contract

Professional keymappers must support safe config changes while the app is
running.

Contract:

- editing GUI fields must not immediately mutate the active runtime config;
- save and reload are explicit apply points;
- reload must validate the new config before it replaces the active config;
- if validation fails, the old runtime config continues to run;
- reload should switch runtime generation transactionally;
- old generation must cleanup held keys and mouse buttons;
- new generation must respond only to new config;
- old triggers must not keep producing output after reload;
- reload must not leave held owner state behind;
- diagnostics should make generation handoff observable.

This contract is already partially supported by existing runtime reload tests
and acceptance coverage. Future GUI work must preserve it and should improve
user-facing status around failed reload.

## GUI Editing Safety Contract

When `BAKeySmith.App` is the foreground window, existing mappings should not
interfere with editing.

Required product behavior:

- editing a mapping trigger must not trigger an existing mapping;
- editing macro script text must not trigger an existing mapping;
- focused key capture mode must not dispatch runtime mappings;
- BA KeySmith should not accidentally target itself with mapped output during
  config editing;
- GUI capture must not use global hooks in v1.

Current implementation:

- `BAKeySmith.App` wraps the runtime foreground gate in the App layer.
- When the foreground process is BA KeySmith itself, the wrapper returns
  foreground blocked even if the inner gate would allow dispatch.
- This prevents existing mappings from dispatching while the user edits config,
  mapping trigger, target, hotkey, or macro script fields in the GUI.
- The App live hook wiring also passes BA KeySmith self-process names to
  `WindowsHookTriggerSource` as an explicit blocked-foreground list.
- Core hook defaults do not block the current process; the App supplies this
  list so Headless / Acceptance / harness targets are not broken by default.
- When BA KeySmith itself is foreground, a new uncaptured trigger hit must
  pass through at the hook layer: no suppress, no dispatch, and no new captured
  session.
- Existing captured-session releases still suppress and dispatch so hold
  mappings can release correctly after foreground changes.
- The implementation does not use global hooks for editing suppression and does
  not let GUI code operate on runtime internals.

Future implementation strategies if the UX later needs richer editing behavior:

- suspend dispatch while BAKeySmith GUI is foreground;
- add GUI editing suppression while text fields or capture overlays are active;
- route focused capture through WPF input only, outside runtime.

The current selected behavior is implemented and tested at the App layer.
Focused capture is allowed while runtime is running because App foreground
dispatch blocking and hook self-foreground pass-through hardening are both in
place. Capture remains WPF focused-only; it does not use the live hook and does
not send input.

## Control Hotkey Conflict Contract

Control hotkeys are reserved inputs. Mapping triggers must not conflict with
them.

Rules:

- enable / pause / global control hotkeys are reserved control input;
- a mapping trigger must not equal the control hotkey;
- if the hotkey is a single key such as `f5`, mapping trigger `f5` is invalid;
- if the hotkey is a combination such as `ctrl+shift+f12`, mapping triggers
  `ctrl`, `shift`, and `f12` are invalid because they are part of the reserved
  control input;
- conflict checks must use normalized key names;
- aliases must compare equal, for example `esc` and `escape`, or `page_up` and
  `page up`;
- future side-specific modifiers must also be included in conflict checks.

GUI expectation:

- conflict rows should be highlighted;
- save / start should be blocked until conflict is resolved;
- error text should name the reserved hotkey, for example:
  `f5 is used as the enable/pause hotkey and cannot also be a mapping trigger.`

Current implementation status:

- Core AppConfig validation rejects mapping triggers that conflict with the
  normalized control hotkey or any normalized control hotkey component.
- Runtime start and reload use the same Core validation path, so invalid
  control-hotkey conflicts are rejected outside the GUI as well.
- GUI save / start / reload paths surface the validation error through the
  existing error display path.
- GUI row-level red highlighting remains a UX polish item.

## Duplicate And Overlapping Trigger Contract

Current config validation rejects duplicate normalized trigger names.

Professional target:

- duplicate trigger detection must be based on normalized names;
- GUI should show duplicate errors before save / start;
- aliases must be treated as identical;
- control hotkey conflicts must be based on normalized names;
- future side-specific modifier overlap must be rejected in v1 of that feature;
- do not silently choose first / last mapping for conflicting triggers.

Side-specific modifier future rule:

- `ctrl` conflicts with `left_ctrl` and `right_ctrl`;
- `alt` conflicts with `left_alt` and `right_alt`;
- `shift` conflicts with `left_shift` and `right_shift`;
- `win` conflicts with `left_win` and `right_win`;
- first implementation should reject overlap instead of simultaneous trigger or
  more-specific priority.

## Focused GUI Capture Contract

GUI focused key capture v1:

- only captures while `BAKeySmith.App` has focus;
- does not use global hooks;
- does not start runtime;
- does not send input;
- does not change Core runtime;
- does not change AppConfigV1;
- does not change Macro DSL;
- keeps manual input as permanent fallback;
- allows legal mapping triggers including modifier-only `ctrl`, `alt`, and
  `shift`;
- supports simple target capture for current valid simple output key / mouse
  button names;
- separates mapping trigger capture from hotkey capture;
- does not restrict mapping trigger capture because of hotkey capture policy;
- treats `escape` as bindable;
- uses an explicit cancel button rather than hard-coded Esc cancellation;
- avoids capturing the click that arms mouse capture;
- may run while runtime is running because BAKeySmith.App foreground input is
  blocked from runtime dispatch and App live hook self-foreground trigger
  capture passes through;
- defers wheel capture while preserving manual `mouse_wheel_up/down` input;
- does not support wheel output / wheel target capture;
- treats unsupported or unstable focused WPF keys such as Windows, PrintScreen,
  Apps/Menu, IME, and international/OEM paths as manual-input fallback rather
  than inventing new Core key names.

Focused capture must not become a new runtime feature by accident.

## Macro DSL Product Contract

Macro DSL v1 remains text-first.

Rules:

- GUI validation must come from `MacroScriptCompiler`;
- GUI must not add commands;
- GUI must not accept commands the compiler rejects;
- GUI must not reinterpret compiler diagnostics as runtime behavior;
- script-safe completion names must remain compatible with key-name contract;
- future DSL aliases or v2 changes require explicit contract, tests, docs, and
  migration notes.

See [macro-language-contract.md](macro-language-contract.md).

## Plugin / Extension Future Boundary

Plugins are not implemented now.

Future extension categories:

- read-only advisor / overlay plugins:
  - timeline reminders;
  - raid or battle phase prompts;
  - configuration suggestions;
  - external tool read-only integration;
  - lower risk because they do not affect runtime input.
- runtime-affecting plugins:
  - trigger modification;
  - mapping generation;
  - profile switching;
  - runtime action injection;
  - high risk because they can affect input output and safety.

Any future plugin API needs:

- permission model;
- trusted boundary;
- event bus contract;
- clear separation between read-only advice and runtime-affecting behavior;
- diagnostics and disable controls;
- release gate and safety documentation.

Current architecture should avoid intentionally blocking future extension, but
no plugin API is authorized or planned for implementation in this checkpoint.

## Public Beta Before / After

Public beta before release should have:

- this professional contract set:
  - [key-name-contract.md](key-name-contract.md);
  - [macro-language-contract.md](macro-language-contract.md);
  - [professional-keymapper-contract.md](professional-keymapper-contract.md);
- focused key capture contract fixed before implementation;
- duplicate trigger and hotkey conflict feedback planned or implemented;
- tap wording tightened to press duration / output events vocabulary;
- GUI self-foreground editing safety implemented and covered;
- hot reload contract recorded as release-ready behavior.

Public beta after / deferrable:

- side-specific modifier implementation;
- raw input / scan-code fallback;
- plugin API;
- profiles / layers;
- chord / sequence triggers;
- visual macro builder;
- WPF UI automation;
- installer / winget / Store packaging.

If a future public beta decision claims broad professional key coverage, then
side-specific modifiers and raw key fallback may need to move earlier. Under the
current GUI preview scope they can remain future milestones as long as docs are
honest.
