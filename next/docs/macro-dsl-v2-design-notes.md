# Macro DSL V2 Design Notes

This document records Macro DSL v2 direction. It is planning-only and does not
change `MacroScriptCompiler`, `MacroScriptCompletionProvider`, Macro DSL v1, or
runtime behavior.

## Position

Macro DSL v1 is not the final professional macro language for the C# rewrite.

V2 should be a breaking redesign. Users should learn v2 explicitly instead of
being hidden behind ambiguous v1 compatibility.

V1-like syntax may provide migration hints, but it should not execute as hidden
compatibility unless a future migration contract explicitly permits it.

## V2 Skeleton

Proposed first skeleton:

```text
requires <capability>

on_down
  ...
end

while_held interval 0ms
  ...
end

on_up
  ...
end
```

First version does not expose user-writable `on_cancel`.

Cancel cleanup is runtime-owned and automatic.

## Semantics

- `on_down`: runs on trigger down.
- `while_held`: repeats while trigger is held.
- `on_up`: runs on normal trigger up only.
- runtime stop / reload / foreground lost / emergency stop: cleanup only, no
  `on_up`.
- `on_up` can execute real actions, but infinite loop in `on_up` is an Error.
- `while_held interval 0ms` is valid.

## Not In First Version

Do not include:

- variables;
- conditions;
- functions;
- goto;
- toggle infinite;
- plugin scripting;
- arbitrary embedded script language.

## Coordinate Commands

Do not casually add coordinate commands to Macro DSL v1.

DSL v2 coordinate actions should compile into Action Model v2 and respect
[bluearchive-coordinate-model.md](bluearchive-coordinate-model.md).

First usable coordinate action may include `tap_at`, but it needs explicit
coordinate profile and cursor execution policy.

## Migration

Migration must be explicit:

- explain old v1 commands;
- provide suggestions;
- do not silently reinterpret dangerous scripts;
- preserve Python beta as public baseline until C# public preview is ready.

