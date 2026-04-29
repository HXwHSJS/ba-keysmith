# Architecture V2 Direction

This document records the v2 architecture direction for BA KeySmith. It is
planning documentation only. It does not change current C# `next/` runtime,
AppConfigV1, Macro DSL v1, GUI implementation, or packaging.

## Position

Current `next/` remains the engineering baseline:

- Core RC0 candidate: established.
- GUI RC0 candidate: established.
- Release-ready RC: not established.
- Python beta: still the public usable version and behavior baseline.

V2 design should proceed in parallel instead of pushing professional v2
capabilities into AppConfigV1 or Macro DSL v1.

## V2 Layers

BAKS v2 should separate these layers:

- Input Model v2: physical inputs, aliases, side-specific modifiers, OEM keys,
  raw fallback, display names.
- Trigger Model v2: conditions that activate mappings.
- Conflict Model v2: config errors, live blockers, and risk warnings.
- Action Model v2: executable output actions.
- Macro DSL v2: user text that compiles into Action Model / Macro IR.
- Runtime v2: MappingActivationSession execution.
- AppConfigV2: structured persisted model.
- GUI v2: editor, capture, validation, coordinate tools, diagnostics.

## Action Model V2

Action Model v2 is the target runtime-independent representation for user
actions.

Categories:

- key actions;
- mouse actions;
- timing actions;
- coordinate actions;
- repeat / control flow;
- cleanup-owned actions.

Future visual macro builder, Macro DSL v2, templates, and trusted automation
plugins should target Action Model v2 instead of injecting raw Macro DSL text.

## Trigger Model V2

Trigger Model v2 must not be hidden inside Macro DSL.

Future trigger categories:

- down;
- up;
- tap;
- hold;
- tap-hold;
- double-tap;
- repeated click;
- chord;
- sequence;
- layer / profile trigger;
- plugin-driven trigger as future major.

These require schema and runtime design. Do not add them as AppConfigV1
string hacks.

## Runtime V2

Runtime v2 executes a `MappingActivationSession`, not a simple script.

High-level flow:

- trigger down creates a session;
- `on_down` runs once;
- `while_held` runs while the trigger is held;
- trigger up stops `while_held` and runs `on_up`;
- runtime stop / reload / foreground lost / emergency stop does not run
  `on_up`; it runs cleanup only.

See [runtime-v2-design-notes.md](runtime-v2-design-notes.md).

## Macro DSL V2

Macro DSL v1 is not the final product language.

DSL v2 should:

- be a breaking redesign;
- teach users the v2 model explicitly;
- compile to Action Model v2 / Macro IR;
- keep v1-like syntax only as migration hints, not executable hidden
  compatibility;
- avoid adding coordinate semantics casually to DSL v1.

See [macro-dsl-v2-design-notes.md](macro-dsl-v2-design-notes.md).

## Coordinate Model

Blue Archive coordinate work should use a client-area logical coordinate model,
not window outer-rect pixels.

See [bluearchive-coordinate-model.md](bluearchive-coordinate-model.md).

## Plugin Boundary

First release does not implement plugin API.

Future levels:

- external companion;
- read-only plugin;
- trusted automation plugin.

Plugins must not directly access `RuntimeHost` internals, and no plugin metadata
belongs in AppConfigV1.

