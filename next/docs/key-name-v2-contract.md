# Key Name V2 Contract

This document records the future key naming contract. It complements
[key-name-contract.md](key-name-contract.md), which describes current C# `next`
behavior.

## Status

- Current C# `next` supports GUI focused key capture v1 but not full v2 input.
- Current side-specific modifiers still fold into generic modifiers.
- Current focused capture does not satisfy the full professional v2 key target.
- This document is not implemented.

## Canonical Naming Rules

V2 canonical names should be:

- English ASCII;
- snake_case;
- script-safe;
- unambiguous across keyboard, mouse, and coordinate record hotkeys.

Examples:

- `arrow_left`, not bare `left`;
- `mouse_left`, not bare `left`;
- `left_ctrl`, not silently `ctrl`;
- `key_slash`, not `/` as the public canonical name.

## Required First Usable Canonical Names

V2 should include:

- letters and digits;
- `f1` through `f24`;
- `left_ctrl`, `right_ctrl`, `left_alt`, `right_alt`, `left_shift`,
  `right_shift`, `left_win`, `right_win`;
- generic `ctrl`, `alt`, `shift`, `win`;
- `caps_lock`, `num_lock`, `scroll_lock`;
- `arrow_up`, `arrow_down`, `arrow_left`, `arrow_right`;
- `home`, `end`, `page_up`, `page_down`, `insert`, `delete`;
- `tab`, `enter`, `escape`, `space`, `backspace`;
- numpad names;
- OEM punctuation names from [input-model-v2-design.md](input-model-v2-design.md);
- `mouse_left`, `mouse_right`, `mouse_middle`, `mouse_x1`, `mouse_x2`;
- `mouse_wheel_up`, `mouse_wheel_down` where wheel is trigger-only unless a
  future output contract adds wheel output.

## Aliases And Chinese Search

Manual input and command palette search may accept aliases, including Chinese
friendly labels.

Examples:

- `左ctrl` may search to `left_ctrl`;
- `逗号` may search to `key_comma`;
- `鼠标右键` may search to `mouse_right`;
- `esc` may search to `escape`.

Saved config and macro source should use canonical names.

## Conflict Implication

Key-name normalization feeds the conflict model. If `ctrl` and `left_ctrl`
overlap, the conflict model must report it rather than letting both silently
run.

See [conflict-model-v2-design.md](conflict-model-v2-design.md).

