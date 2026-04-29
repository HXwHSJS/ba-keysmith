# Key Name Contract

This document defines the professional key naming contract for BA KeySmith.
It records the current `next/` C# behavior, the Python beta compatibility
baseline, and future naming targets. It is a contract document only. It does
not change runtime, schema, GUI, or Macro DSL behavior.

## Status

- Python beta remains the public usable version and behavior baseline.
- `next/` C# is the local runtime baseline and GUI RC0 candidate track.
- AppConfigV1 schema freeze remains in force.
- Macro DSL v1 freeze remains in force.
- GUI focused key capture v1 is implemented in `BAKeySmith.App` only.
- Advanced capture, side-specific modifiers, raw input, scan-code fallback,
  and new aliases remain future work.
- Key Name v2 is tracked separately in [key-name-v2-contract.md](key-name-v2-contract.md)
  and [input-model-v2-design.md](input-model-v2-design.md).

## Naming Layers

BA KeySmith has several naming layers:

- user config strings in AppConfigV1;
- runtime trigger names compiled by `AppConfigSerializer`;
- macro input names compiled by `MacroScriptCompiler`;
- trigger source names emitted by keyboard / mouse hooks;
- GUI manual editing and focused key capture v1 names;
- public docs / examples.

The professional contract is:

- manual input should accept compatible aliases where the runtime/compiler
  already accepts them;
- GUI-generated names should prefer public canonical names;
- macro editor completion should prefer script-safe canonical names;
- serializer and compiler validation remain the source of truth until a future
  explicit naming migration is authorized.

## Current C# Canonical Names

Current C# canonical names are based on `KeyNameResolver`.

### Letters And Digits

Supported:

- `a` through `z`
- `0` through `9`

These names are valid keyboard names for triggers and macro output.

### Function Keys

Supported:

- `f1` through `f24`

Python beta public README only documents `f1` through `f12`. C# currently
supports `f13` through `f24`, but public docs should mark them as C# supported
with limited product evidence until explicit coverage is added.

### Modifiers

Current supported generic modifiers:

- `ctrl`
- `alt`
- `shift`

Current unsupported canonical modifiers:

- `win`
- `left_ctrl`
- `right_ctrl`
- `left_alt`
- `right_alt`
- `left_shift`
- `right_shift`
- `left_win`
- `right_win`

Current C# aliases such as `left ctrl`, `right ctrl`, `ctrl_l`, and `ctrl_r`
fold into generic `ctrl`. Similar left/right aliases fold into generic `alt`
and `shift`.

Side-specific modifiers are a future professional keymapper milestone, not
current implemented behavior.

### Lock Keys

Current canonical names:

- `caps lock`
- `num lock`
- `scroll lock`

Recommended public / script-safe names:

- `caps_lock`
- `num_lock`
- `scroll_lock`

These keys can have system state side effects when pressed or tapped. GUI
capture and docs must warn that capturing or outputting lock keys may toggle
the OS lock state.

### Arrows, Navigation, And Control Keys

Current canonical names:

- `arrow up`
- `arrow down`
- `arrow left`
- `arrow right`
- `home`
- `end`
- `page up`
- `page down`
- `insert`
- `delete`
- `tab`
- `enter`
- `escape`
- `space`
- `backspace`
- `pause`

Recommended public / script-safe names:

- `arrow_up`
- `arrow_down`
- `arrow_left`
- `arrow_right`
- `page_up`
- `page_down`
- `esc` may remain an alias, but `escape` should be the canonical public
  spelling if only one name is displayed.

### Numpad

Current C# canonical names:

- `numpad0` through `numpad9`
- `multiply`
- `add`
- `subtract`
- `decimal`
- `divide`

Current C# does not define `num0` through `num9` aliases. Do not document
`num0` style names as supported unless a future explicit alias change adds
them.

### OEM Punctuation

Current C# supports several OEM punctuation names directly:

- `;`
- `=`
- `,`
- `-`
- `.`
- `/`
- `` ` ``
- `[`
- `\`
- `]`
- `'`

These are implemented but not yet a polished public key naming surface. Public
beta docs should either document them carefully or mark them as advanced /
keyboard-layout-sensitive.

### Unsupported Or Not Yet Contracted Special Keys

Current C# does not expose these as public canonical names:

- `print_screen`
- `apps`
- `menu`
- `win`
- `left_win`
- `right_win`
- international / Japanese keyboard special keys
- raw `vk_XX`
- raw `scancode_XXX`

Professional keymapper coverage eventually needs a raw virtual-key or scan-code
fallback, but that is a future runtime/config contract and is not implemented
now.

### Mouse Buttons

Current mouse button names:

- `mouse_left`
- `mouse_right`
- `mouse_middle`
- `mouse_x1`
- `mouse_x2`

These are valid mouse triggers, simple mapping targets, hold buttons, and Macro
DSL button names. Mouse button triggers use captured sessions for matching
release behavior when foreground allows the trigger.

`mouse_x1` and `mouse_x2` have stronger current evidence than
`mouse_left/right` product usage. `mouse_left/right` are supported by the code
path but remain unproven as physical Blue Archive product triggers.

### Mouse Wheel

Current mouse wheel trigger names:

- `mouse_wheel_up`
- `mouse_wheel_down`

Wheel is trigger-only in the current contract:

- valid as a mapping trigger;
- not valid as a mapped hold button;
- not valid as a drag button;
- not valid as `press`, `release`, or `tap` macro output;
- no captured trigger session because wheel is a pulse event.

## Current Alias Contract

Current accepted aliases include:

- `key.esc` -> `escape`
- `esc` -> `escape`
- `return` -> `enter`
- `caps_lock`, `capslock`, `capital` -> `caps lock`
- `ins` -> `insert`
- `del` -> `delete`
- `pageup`, `page_up`, `pgup` -> `page up`
- `pagedown`, `page_down`, `pgdn` -> `page down`
- `num_lock`, `numlock` -> `num lock`
- `scroll_lock`, `scrolllock` -> `scroll lock`
- `control`, `ctl`, `ctrl_l`, `ctrl_r`, `control_l`, `control_r`,
  `left ctrl`, `right ctrl`, `left control`, `right control` -> `ctrl`
- `alt_l`, `alt_r`, `alt_gr`, `left alt`, `right alt`, `option` -> `alt`
- `shift_l`, `shift_r`, `left shift`, `right shift` -> `shift`
- `up`, `down`, `left`, `right` -> `arrow up`, `arrow down`,
  `arrow left`, `arrow right`
- mouse button aliases `left`, `right`, `middle`, `x1`, `x2`
- wheel aliases `wheel_up`, `wheelup`, `wheel_down`, `wheeldown`

The old Python beta README contains `crtl` as a spelling mistake. Do not make
`crtl` a formal public alias unless a future compatibility decision explicitly
adds it.

## Python Beta Compatibility

Python beta publicly documents:

- `a-z`, `0-9`, `f1-f12`;
- `up`, `down`, `left`, `right`;
- `enter`, `space`, `tab`, `escape`, `backspace`, `caps lock`;
- generic `shift`, `ctrl`, `alt`;
- `insert`, `delete`, `home`, `end`, `page up`, `page down`;
- `num lock`, `scroll lock`;
- `mouse_left`, `mouse_right`, `mouse_middle`, `mouse_x1`, `mouse_x2`.

Python beta aliases also accept underscore variants for several space names and
fold left/right modifiers into generic modifiers. Therefore modifier-only
triggers such as `ctrl`, `alt`, and `shift` are existing product capability and
must not be rejected by current or future capture UX.

## Recommended Public Canonical Names

For public docs and future GUI-generated capture values, prefer these
script-safe names:

- `a` through `z`
- `0` through `9`
- `f1` through `f24`
- `ctrl`, `alt`, `shift`
- `caps_lock`, `num_lock`, `scroll_lock`
- `arrow_up`, `arrow_down`, `arrow_left`, `arrow_right`
- `home`, `end`, `page_up`, `page_down`
- `insert`, `delete`
- `tab`, `enter`, `escape`, `space`, `backspace`, `pause`
- `numpad0` through `numpad9`
- `multiply`, `add`, `subtract`, `decimal`, `divide`
- `mouse_left`, `mouse_right`, `mouse_middle`, `mouse_x1`, `mouse_x2`
- `mouse_wheel_up`, `mouse_wheel_down` as trigger-only

Manual input should keep accepting implemented aliases. GUI display may use
friendly labels, but saved captured values should be normalized to canonical
names once a future save-normalization policy is authorized.

## Save Normalization Policy

Current implementation does not globally rewrite user-entered config strings
into a new public canonical spelling. It validates and compiles through
`AppConfigSerializer` and `MacroScriptCompiler`.

Current / future policy:

- GUI focused capture v1 fills script-safe canonical names.
- Manual text input should preserve user edits until save behavior is
  explicitly changed.
- If save-time normalization is introduced, it must be a separate config
  contract change with tests proving unknown fields and AppConfigV1 semantics
  are preserved.

## Side-Specific Modifier Future Contract

Future professional keymapper targets:

- `left_ctrl`
- `right_ctrl`
- `left_alt`
- `right_alt`
- `left_shift`
- `right_shift`
- `left_win`
- `right_win`

Conflict rule for the first side-specific implementation:

- `ctrl` conflicts with `left_ctrl` and `right_ctrl`.
- `alt` conflicts with `left_alt` and `right_alt`.
- `shift` conflicts with `left_shift` and `right_shift`.
- `win` conflicts with `left_win` and `right_win`.
- Do not trigger both generic and side-specific mappings for one physical key.
- Do not silently prefer a more specific mapping in the first implementation.
- Safer professional behavior is to reject overlapping config and highlight
  conflicts in GUI before save / start.

This section is a future contract target only. It is not current behavior.

V2 decision:

- side-specific modifiers are required for the first usable v2 target;
- `left_ctrl`, `right_ctrl`, `left_alt`, `right_alt`, `left_shift`,
  `right_shift`, `left_win`, and `right_win` must not fold silently into
  generic modifiers in v2;
- generic modifier and side-specific modifier overlap is a conflict.

See [conflict-model-v2-design.md](conflict-model-v2-design.md).

## Focused GUI Capture Requirements

Focused capture v1 is implemented in `BAKeySmith.App` only and must follow this
contract:

- capture only while `BAKeySmith.App` has focus;
- do not use global hooks;
- do not start runtime;
- do not send input;
- do not change Core runtime, schema, or Macro DSL;
- keep manual input as a permanent fallback;
- allow focused capture while runtime is running; BAKeySmith.App foreground input
  is protected by App foreground dispatch blocking and live hook self-foreground
  pass-through hardening;
- fill script-safe, config-friendly names such as `arrow_up`, `page_up`,
  `caps_lock`, `num_lock`, `scroll_lock`, `mouse_left`, and `mouse_right`;
- never output ambiguous bare `left` or `right` from capture;
- allow mapping trigger capture for `ctrl`, `alt`, and `shift`;
- support simple target capture for the current simple output key / mouse button
  set, including `escape`, `ctrl`, `arrow_left`, and `mouse_right`;
- keep mapping trigger capture and hotkey capture as different semantics;
- do not restrict mapping trigger capture just because a hotkey capture mode
  chooses to reject pure modifiers;
- treat `escape` as a bindable key;
- use an explicit cancel button for capture cancellation, not hard-coded Esc;
- avoid capturing the mouse click that arms capture mode;
- defer wheel capture v1 unless explicitly authorized, while keeping manual
  `mouse_wheel_up/down` input;
- do not support wheel target capture or wheel output in the current contract;
- warn that capturing `caps_lock`, `num_lock`, or `scroll_lock` can toggle OS
  lock state;
- report unsupported focused WPF keys such as Windows, PrintScreen, Apps/Menu,
  IME, OEM, or international keys as manual-input fallback rather than adding
  new key names.

Current focused capture v1 does not satisfy the full v2 key target. OEM /
punctuation capture, hook-backed capture, side-specific modifiers, raw input,
and scan-code fallback remain future v2 work.
