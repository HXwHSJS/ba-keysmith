# Conflict Model V2 Design

This document defines the future conflict model.

Implementation status:

- a Core-only Conflict Model v2 skeleton exists under
  `next/src/BAKeySmith.Core/Input/V2/Conflicts/`;
- it uses `InputSpec`, `InputNameResolverV2`, and `InputOverlap` to detect
  internal BAKS binding conflicts;
- it is not wired into AppConfigV1, current runtime validation, GUI capture, or
  GUI row highlighting.

## Categories

V2 should separate:

- config conflict: the config is internally invalid and should not save / start;
- live blocker: the config may save, but live run is blocked under current
  environment;
- risk warning: the config can run, but user should understand the risk.

## Required Conflict Checks

V2 must check:

- duplicate mapping trigger;
- control hotkey vs mapping trigger;
- coordinate record hotkey vs mapping trigger;
- emergency stop vs mapping trigger;
- emergency stop vs control hotkey;
- generic modifier vs side-specific modifier overlap;
- aliases resolving to the same canonical InputSpec.

BAKS only promises internal conflict detection. It cannot guarantee that
external software, OS hotkeys, overlays, or drivers will not steal an input.

## Live Blockers

Coordinate profile mismatch is a live blocker, not a save error.

Example:

- mapping is bound to `ba16_1920x1080`;
- current Blue Archive client is detected as `ba43_1440x1080`;
- config may still save;
- live coordinate mappings using the old profile are disabled and red-highlighted.

## Risk Warnings

Real cursor fallback is a risk warning, not a config conflict.

The user can opt into it, but the UI must explain that it moves the real mouse
and may affect gameplay or user control.

## UI Expectations

- row-level red highlight for config conflicts;
- live blocker banner before start;
- risk warning with explicit opt-in;
- Chinese-friendly errors;
- canonical name plus friendly label where useful.
