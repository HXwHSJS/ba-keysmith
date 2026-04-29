# Macro Editor Assistance V2

This document records the future macro editor assistance design. It is
planning-only and does not change the current WPF `MacroEditorControl`.

## Current Status

Current `MacroEditorControl` has improved popup completion and diagnostics, but
it is not the final professional editor route.

Accepted current status:

- completion popup / contextual policy: accepted;
- popup lifecycle / Esc / Tab / Enter: accepted;
- diagnostics duplicate reduction: accepted;
- line-number count: improved;
- line-number gutter clipping: P2 known issue.

Future options:

- custom editor control refactor;
- AvalonEdit evaluation;
- hide the line-number gutter before public beta if it remains distracting.

## V2 Assistance Requirements

Macro DSL v2 must ship with editor assistance:

- token completion;
- snippet completion;
- parameter completion;
- context-aware completion;
- progressive completion such as `o` -> `on_` -> `on_down`;
- Chinese alias search in command palette;
- concise inline hints, not long in-app teaching.

## Snippets

Example:

- typing / selecting `on_down` inserts:

```text
on_down
  
end
```

The caret enters the block body.

## Keyboard UX

- Enter = newline.
- Tab = popup completion / placeholder jump / indent.
- Esc = close completion or search.
- Ctrl+Enter = exit current block.
- Double Enter can exit before `end` and remove extra blank line when safe.
- Ctrl+Space = command palette / fuzzy search.

## Chinese-Friendly Search

Command palette may accept Chinese aliases, such as:

- `按下` -> `on_down`;
- `按住` -> `while_held`;
- `松开` -> `on_up`;
- `点击坐标` -> `tap_at`.

Canonical inserted text remains English ASCII.

## Teaching Boundary

The app should not contain long tutorial prose. README, website copy, and docs
should teach the model. The editor should provide local, actionable assistance.

