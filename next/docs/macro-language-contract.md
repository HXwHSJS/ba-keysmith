# Macro Language Contract

This document defines the BA KeySmith Macro DSL contract. It records current
DSL v1 behavior and quality notes for future DSL design. It does not change the
compiler, runtime, GUI, schema, or language implementation.

## Status

- Macro DSL v1 is frozen for the current GUI RC0 candidate.
- Freeze means no accidental semantic changes. It does not forbid future DSL
  v1.1 or v2 planning.
- GUI macro editor v1 remains a text editor backed by `MacroScriptCompiler`.
- Current compiler diagnostics remain the source of truth.
- Macro DSL v1 is not the final professional macro language for the C# rewrite.
- Macro DSL v2 design is tracked in [macro-dsl-v2-design-notes.md](macro-dsl-v2-design-notes.md)
  and editor assistance is tracked in [macro-editor-assistance-v2.md](macro-editor-assistance-v2.md).
- Do not keep extending DSL v1 with coordinate commands by convenience.

## Current DSL V1 Commands

Current commands accepted by `MacroScriptCompiler`:

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

Comments begin with `#`. Blank lines are ignored. Commands and arguments are
currently lower-cased by the compiler before normalization.

## `press`

Syntax:

```text
press <key-or-mouse-button>
```

Semantics:

- sends a key or mouse button down through `PressOwnershipTracker`;
- creates ownership under the macro owner;
- can hold keyboard keys or mouse buttons;
- mouse buttons held by `press` participate in active pointer foreground checks;
- released by explicit `release`, by owner cleanup, or by stop / disable /
  reload cleanup.

Wheel names are not valid `press` targets.

## `release`

Syntax:

```text
release <key-or-mouse-button>
```

Semantics:

- releases a key or mouse button for the macro owner;
- clears active pointer tracking when releasing a mouse button;
- redundant release is handled by ownership tracking and should not create a
  duplicate physical up for an owner that is no longer holding the key.

Wheel names are not valid `release` targets.

## `tap`

Syntax:

```text
tap <key-or-mouse-button>
```

Semantics:

- performs one complete press action: down -> short duration -> up;
- uses the same ownership / input sequencing model as simple tap mappings;
- `tap_hold_ms` is the down-to-up press duration;
- `tap` is not a hold mapping;
- `tap` must not leave held ownership behind after completion or cleanup;
- simple tap and macro tap must remain semantically aligned.

Recommended GUI wording:

- `Tap press duration (ms)`
- Chinese UI may use `Tap 按压时长(ms)`

Avoid wording that implies `tap_hold_ms` is the same as a user hold mapping.

Wheel names are not valid `tap` targets in DSL v1.

## `wait`

Syntax:

```text
wait <milliseconds>
```

Semantics:

- waits for a non-negative decimal millisecond duration;
- keyboard-only waits use normal runtime/session cancellation behavior;
- active pointer waits can be foreground-aware when a mouse button is held by
  the macro owner;
- stop / disable / reload cancellation is distinct from foreground loss and
  must not be reported as foreground loss.

Current proven boundary:

- explicit active pointer scripts using `press mouse_middle`, movement,
  `wait`, and `release mouse_middle` can detect foreground loss during the wait
  before the full wait elapses;
- cleanup sends one cleanup up, clears ownership, and prevents later
  non-cleanup instructions from running;
- keyboard-only waits are not cancelled by this active pointer hardening.

Not proven:

- Blue Archive real-target cleanup latency SLO;
- immediate cleanup at the exact foreground-loss instant;
- interruption of a single in-flight backend mouse move.

Dry / harness latency bounds are not real-target SLOs.

## `loop` / `end`

Syntax:

```text
loop <count>
  ...
end
```

Current semantics:

- positive count repeats the body that many times;
- `loop 0` means infinite loop in the current compiler/executor contract;
- `loop infinite` is accepted and also means infinite loop;
- missing `end` is a compile diagnostic;
- extra `end` is a compile diagnostic.

Important note:

- `loop 0` is not zero iterations today. It means forever.
- This is powerful but potentially ambiguous for users.

Future quality recommendation:

- consider adding a clearer `loop_forever` alias in a future DSL version;
- keep `loop 0` and `loop infinite` as compatibility aliases if behavior is
  changed or clarified;
- future `break`, `until`, or conditional looping would require a new DSL
  contract and acceptance coverage.

## `combo`

Syntax:

```text
combo <key-or-mouse-button> [key-or-mouse-button...]
```

Current semantics:

- presses each key/button in argument order;
- waits `_comboKeyGap` between presses;
- holds the full set briefly using `_comboHold`;
- releases in reverse argument order;
- release happens in `finally`, so cancellation or exception should still
  cleanup keys held by the combo owner.

Quality note:

- `combo` currently means an output chord, not a trigger chord.
- The name may be confused with future chord triggers.
- Future DSL may add `chord` as a clearer alias for output chord while keeping
  `combo` for compatibility.

## `setpos`

Syntax:

```text
setpos <x> <y> [mouse]
```

Semantics:

- moves the cursor to an absolute position;
- if `mouse` is present, `x` and `y` are interpreted as normalized screen scale
  factors and multiplied by the current screen size;
- otherwise `x` and `y` are absolute pixel coordinates.

Quality note:

- `setpos` is precise but not especially user-friendly.
- Future aliases such as `move_to` may be clearer.

## `setpos_rel`

Syntax:

```text
setpos_rel <dx> <dy>
```

Semantics:

- reads current cursor position;
- moves cursor by integer delta;
- does not press or release any button by itself.

Quality note:

- Future alias `move_rel` would be clearer for most users.
- Existing `setpos_rel` should remain as a compatibility command.

## `drag`

Syntax:

```text
drag <x> <y> [mouse] [button]
```

Current semantics:

- helper command;
- default button is `mouse_left`;
- optional button must be a valid mouse button;
- if `mouse` is present, target position uses normalized screen scale;
- helper presses the drag button using a helper drag owner;
- performs pointer delay / foreground checks;
- moves cursor to the target;
- performs additional foreground checks;
- releases the helper drag owner in `finally`.

Current foreground-loss contract:

- after mapped drag button down and active helper sequence start, foreground
  loss during pointer delay or after move before release stops later
  non-cleanup output and releases the helper drag owner;
- cleanup owner is the helper drag owner, shaped like `owner:drag:<button>`;
- runtime/session cancellation is not reported as foreground loss.

Not proven:

- foreground drift before helper button down;
- interruption of a single in-flight move call;
- Blue Archive real-target latency SLO.

## `drag_rel`

Syntax:

```text
drag_rel <dx> <dy> [button]
```

Current semantics:

- helper command;
- default button is `mouse_left`;
- optional button must be a valid mouse button;
- reads current cursor position;
- drags by integer delta;
- uses the same helper drag owner and foreground-loss contract as `drag`.

## Mouse Wheel Output

Mouse wheel is not a Macro DSL v1 output.

Current wheel names:

- `mouse_wheel_up`
- `mouse_wheel_down`

These are trigger-only. They are not valid `press`, `release`, `tap`, `drag`,
or `drag_rel` button names.

Adding wheel output would require an explicit future DSL contract. It must not
be smuggled into DSL v1 by GUI validation or completion changes.

## Cleanup And Cancellation

Current cleanup principles:

- held macro-owner keys must be released on macro cleanup;
- combo helper owner releases keys in reverse order in `finally`;
- built-in drag helper owner releases its drag button in `finally`;
- stop / disable / reload use runtime/session cancellation and cleanup paths;
- foreground loss is a separate condition for active pointer sequences and
  must not be confused with runtime cancellation.

## Dry-Run Observation

The DSL should be observable in dry-run through backend event counts and
diagnostics:

- key down/up;
- mouse down/up;
- cursor move;
- macro started / finished;
- active pointer wait diagnostics;
- built-in drag helper diagnostics.

Dry-run evidence proves deterministic harness behavior. It does not prove
Blue Archive real-target SLOs.

## Current Evidence

Current smoke / acceptance coverage includes:

- `tap` uses ownership tracker;
- runtime tap uses unified tap semantics;
- macro compiler supports DSL v1;
- macro compiler supports script-safe key names;
- macro executor runs core DSL commands;
- runtime macro executes through queue;
- reload / stop during long macro releases held owner;
- active pointer wait foreground loss interrupts before wait completes;
- keyboard-only wait ignores foreground-loss hardening;
- active pointer wait cancellation is not foreground loss;
- built-in `drag` / `drag_rel` foreground-loss contract;
- complete drag / multisegment drag dry harness normal-completion.

This evidence is scoped. It is not exhaustive proof of every key name or every
real-target product scenario.

## DSL V1 Quality Review

Clear commands:

- `press`
- `release`
- `tap`
- `wait`
- `loop`
- `end`

Potentially misleading or less professional names:

- `combo`: currently output chord, may conflict with future chord trigger.
- `setpos`: technical, less intuitive than `move_to`.
- `setpos_rel`: technical, less intuitive than `move_rel`.
- `drag` / `drag_rel`: convenient helpers, but users must understand they are
  helper sequences rather than magic product-level drag contracts.
- `loop 0`: currently infinite, but many users may read zero as zero
  iterations.

Missing or future DSL candidates:

- `loop_forever` alias;
- `move_to` alias for `setpos`;
- `move_rel` alias for `setpos_rel`;
- `chord` alias for `combo`;
- explicit wheel output command;
- conditional or breakable loops;
- comments / labels / variables beyond current text support;
- profile / layer integration commands are out of scope for DSL v1.

Deprecation guidance:

- Do not remove current DSL v1 commands in public beta.
- Prefer additive aliases with diagnostics / docs before any deprecation.
- Any v1.1 or v2 language change needs new tests, docs, and migration notes.

## DSL V2 Boundary

Macro DSL v2 is expected to be a breaking redesign. It should compile to Action
Model v2 / Macro IR, not directly mutate current DSL v1 execution.

Current v2 decisions:

- v1-like syntax provides migration hints only, not hidden executable
  compatibility.
- v2 skeleton includes `requires`, `on_down`, `while_held`, and `on_up`.
- first v2 does not expose user-writable `on_cancel`.
- runtime-owned cleanup handles stop / reload / foreground lost / emergency
  stop.
- `on_up` may execute real actions but cannot contain an infinite loop.
- `while_held interval 0ms` must be supported for turbo / no-wait needs.
- variables, conditions, functions, goto, toggle infinite, and plugin scripting
  are not first-version goals.
