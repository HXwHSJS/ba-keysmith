# Input Model V2 Design

This document defines the future input model direction. It is planning-only and
does not change `KeyNameResolver`, AppConfigV1, GUI capture, runtime hooks, or
Macro DSL v1.

## Goal

Input Model v2 must provide one normalized model for:

- mapping trigger;
- simple target;
- macro key action;
- control hotkey;
- coordinate record hotkey;
- emergency stop.

The shared representation is `InputSpec`.

## InputSpec Requirements

`InputSpec` should carry enough information to distinguish:

- canonical name;
- physical key identity where available;
- side-specific modifier identity;
- device class: keyboard, mouse button, mouse wheel, coordinate capture hotkey;
- display label;
- aliases;
- layout-dependent risk.

## Required First Usable Keyboard Coverage

First usable v2 must support common OEM / punctuation keys:

- `key_grave`
- `key_minus`
- `key_equal`
- `key_left_bracket`
- `key_right_bracket`
- `key_backslash`
- `key_semicolon`
- `key_quote`
- `key_comma`
- `key_period`
- `key_slash`

These names avoid ambiguous literal punctuation in config and macro text while
still allowing UI display such as `` ` / ~ ``.

## Side-Specific Modifiers

First usable v2 must support:

- `left_ctrl` / `right_ctrl`
- `left_alt` / `right_alt`
- `left_shift` / `right_shift`
- `left_win` / `right_win`

Generic names remain useful:

- `ctrl`
- `alt`
- `shift`
- `win`

But generic and side-specific names overlap. They must not silently coexist in
one active conflict domain.

## Display Names

UI may display Chinese labels and symbols, for example:

- `left_ctrl` -> `左 Ctrl`
- `key_grave` -> `` ` / ~ ``
- `key_minus` -> `- / _`
- `mouse_x1` -> `鼠标侧键 1`

Config and macro source should use canonical names.

## Layout-Dependent Warning

OEM / punctuation keys can be keyboard-layout-sensitive. V2 should warn users
when a binding may depend on keyboard layout or IME state.

Future fallback:

- raw virtual key;
- scan code;
- physical key identity.

Raw fallback is a future design item, not current implementation.

