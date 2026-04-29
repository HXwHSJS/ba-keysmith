# Input Capture V2 Design

This document defines future input capture direction. It is planning-only and
does not change current focused capture v1, hooks, runtime, AppConfigV1, or
Macro DSL.

## Goal

V2 capture must not rely only on WPF focused capture. It should combine:

- focused GUI capture for safe text-box workflows;
- hook-backed capture when precise physical identity is required;
- InputSpec normalization;
- explicit conflict validation.

## Required Behaviors

- Capturing `LeftCtrl` and `RightCtrl` must not silently fold to `ctrl`.
- `Escape` is a legal bindable key.
- Capture cancel must use an explicit Cancel button, not hard-coded Esc.
- Manual input remains available for every field.
- Unsupported keys should show a clear unsupported / manual-input message.
- Capture output must normalize to canonical InputSpec names.

## Capture Modes

Capture modes should be explicit:

- mapping trigger;
- simple target;
- macro key action;
- control hotkey;
- coordinate record hotkey;
- emergency stop.

Each mode may have different allowed input classes and conflict rules.

## Coordinate Record Hotkey

Coordinate record hotkey is active only in coordinate capture mode.

Default behavior:

- do not suppress game input;
- do not behave as a global runtime trigger;
- validate against BAKS internal conflicts;
- clearly show when recording is armed.

## First Usable Boundary

First usable v2 needs enough capture support to bind professional keyboard and
mouse inputs reliably, including side-specific modifiers and OEM punctuation.

Raw input / scan-code fallback can be staged if the first v2 slice clearly
states what remains unsupported.

