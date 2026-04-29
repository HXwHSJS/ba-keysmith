# Blue Archive Coordinate Model

This document records future Blue Archive coordinate design. It is planning-only
and does not implement coordinate capture, coordinate macro execution, or cursor
preserving backend support.

## Coordinate Basis

Coordinate basis is the Blue Archive client area, not the window outer rect.

Default logical profiles:

- `ba16_1920x1080`
- `ba43_1440x1080`

Same-aspect-ratio resize should auto-transform coordinates.

Aspect-ratio switch should disable coordinate mappings bound to the old profile
and show a strong warning / red highlight.

## Coordinate Capture UX

Capture states:

- Tracking: show live logical coordinate.
- RecordingArmed: user clicked, but point is not yet selected.
- StableProgress: stable mouse shows short progress bar.
- Copied: copied state is visible.

UX requirements:

- copy history is retained;
- optional coordinate record hotkey may commit coordinate;
- coordinate record hotkey is BAKS-internal conflict checked;
- coordinate record hotkey is active only in coordinate capture mode;
- default coordinate record hotkey should not suppress game input.

Coordinate capture can be designed before live coordinate macro execution.

## Live Execution

Default target is cursor-preserving coordinate execution: do not move the
player's real mouse.

First usable fallback may support `tap_at` via real cursor fallback, but it must
be explicit.

Policies:

- `strict_cursor_preserving`: default;
- `prefer_cursor_preserving`: allow fallback only with strong warning;
- `allow_real_cursor`: explicit opt-in, strong warning.

`move-click-restore` is not cursor-preserving behavior. It moves the user's
real cursor and must not be presented as equivalent.

Cursor-preserving backend feasibility remains a future spike.

## Live Blocker

Coordinate profile mismatch is a live blocker, not a save error. Config may
save, but affected coordinate mappings must be disabled for live run until the
profile mismatch is resolved.

