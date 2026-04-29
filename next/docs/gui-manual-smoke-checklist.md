# GUI Manual Smoke Checklist

This checklist is for the current `BAKeySmith.App` GUI RC0 candidate. It is a manual confidence checklist for the GUI shell only.

It is not a release-ready RC gate, not Blue Archive real-target validation, and not a substitute for the full runtime release gate. Do not run live input or Blue Archive manual validation as part of this checklist unless a separate scope explicitly authorizes it.

## Scope

Use this checklist to confirm the WPF shell can be opened, inspected, and exercised in dry-run mode without drifting outside the GUI entry contract.

Expected boundaries:

- Python beta remains the public usable version, behavior baseline, reference implementation, and regression-spec source.
- `next/` C# remains the local runtime / engineering baseline.
- AppConfigV1 schema freeze remains in force.
- Macro DSL v1 freeze remains in force.
- Dry-run is the default.
- Live mode has a warning and a start-time confirmation.
- This checklist does not prove real-target behavior, live input behavior, long soak stability, or release readiness.

## Startup

- Start `BAKeySmith.App` normally.
- Confirm the app opens in ordinary dry-run mode.
- Confirm `Dry-run 安全模式` is checked by default.
- Confirm the live warning is hidden by default.
- Confirm the config path field is visible and defaults to
  `%APPDATA%\BAKeySmith\config.json` unless a user-selected path is already
  being tested.
- If `%APPDATA%\BAKeySmith\config.json` does not exist, confirm the GUI loads
  an in-memory default config with a visible warning and does not auto-load the
  legacy root `config.example.json`.
- Click `保存配置` while the config path is
  `%APPDATA%\BAKeySmith\config.json`, then confirm the
  `%APPDATA%\BAKeySmith\` directory and `config.json` are created only after
  the explicit save action.
- Restart or reload from that user config path and confirm the saved user config
  loads instead of the legacy root `config.example.json`.
- Confirm target process, hotkey, and tap hold fields are visible.
- Confirm the Administrator status is visible.
- If the app is not elevated, confirm the help text says dry-run / config editing are available and Blue Archive live mode requires Run as administrator.
- Confirm the status badge is visible and starts in a stopped state.
- Confirm the default window size does not hide any main workflow permanently.
- Resize the window toward the minimum size and confirm the app stops at a stable minimum size before the two-column layout becomes unusable.
- Confirm the default and minimum window sizes do not require dragging a whole-page horizontal scrollbar to reach the right-side workflow.
- Confirm left-side runtime controls remain reachable by vertical scrolling when height is limited.
- Confirm the mapping / macro editor tab remains reachable by scrolling when vertical space is limited.
- Confirm the Diagnostics status area wraps or scrolls without overlapping Host / Runtime / Pipeline / Foreground / queue metrics.
- Maximize the window and confirm the main workflow remains usable without obvious clipped controls.

## Config Load / Save

- Choose a JSON config file with the config file picker.
- Load the config.
- Confirm mappings appear in the mapping grid.
- Modify `target_process`.
- Modify `hotkey`.
- Modify `tap_hold_ms`.
- Add a mapping.
- Update an existing mapping.
- Delete a mapping.
- Save the config.
- Confirm root-level unknown fields are not lost.
- Confirm existing mapping `id` values are not lost when a mapping is updated.
- Confirm existing mapping-level unknown fields are not lost when a mapping is updated.
- Confirm newly added mappings do not inherit unrelated unknown fields.
- Confirm deleted mappings remove their associated unknown fields.

## Mapping Editor

- Add a simple mapping.
- Update a simple mapping.
- Delete a simple mapping.
- Add a macro mapping.
- Update a macro mapping.
- Delete a macro mapping.
- Try a duplicate trigger and confirm an error is shown.
- Try a mapping trigger that conflicts with the configured control hotkey, such as `f5` when hotkey is `f5`, and confirm save/start validation reports the conflict.
- Try an alias conflict, such as hotkey `escape` with trigger `esc`, and confirm validation treats them as the same key.
- Try an invalid simple target and confirm validation reports an error before save/start.
- Make an edit and confirm the editor feels dirty until save / reload actions clear the state.
- Click mapping trigger `捕获`.
- Press `Ctrl` by itself and confirm the trigger field becomes `ctrl`.
- Click mapping trigger `捕获` again, press `Esc`, and confirm the trigger field becomes `escape`; `Esc` must not cancel capture.
- Click mapping trigger `捕获` again, press an arrow key, and confirm the field uses `arrow_up/down/left/right` rather than bare `left` or `right`.
- Click mapping trigger `捕获` again, click a mouse button after capture is armed, and confirm mouse buttons use `mouse_left/right/middle/x1/x2`.
- Confirm the click on the `捕获` button itself is not captured as `mouse_left`.
- Click simple target `捕获`.
- Press `Esc` and confirm the simple target field becomes `escape`.
- Click simple target `捕获` again, press `Ctrl`, and confirm the simple target field becomes `ctrl`.
- Click simple target `捕获` again, press left arrow, and confirm the simple target field becomes `arrow_left`, not bare `left`.
- Click simple target `捕获` again, click right mouse button after capture is armed, and confirm the simple target field becomes `mouse_right`.
- If testing `caps_lock`, `num_lock`, or `scroll_lock`, note that the OS lock state may toggle; this is a capture side effect to watch, not runtime output evidence.
- Confirm wheel capture / wheel target capture is not part of GUI capture v1; manual `mouse_wheel_up/down` trigger input remains available.
- Start dry-run runtime, then try mapping trigger, hotkey, and simple target capture.
- Confirm capture is allowed while Runtime is running and the status text explains that BAKeySmith.App foreground input will not trigger mappings.
- Click hotkey `捕获`.
- Press only a modifier such as `Ctrl` and confirm hotkey capture asks for a non-modifier key instead of completing.
- Press `Ctrl+Shift+F12` and confirm the hotkey field becomes `ctrl+shift+f12`.
- Confirm unsupported focused keys such as Windows, PrintScreen, Apps/Menu, IME, or international/OEM keys show an unsupported/manual-input fallback instead of filling a new unverified name.

## Macro Editor

- Select or create a macro mapping.
- Enter a valid macro such as:

```text
tap esc
```

- Confirm macro diagnostics show a valid state.
- Confirm instruction count is visible.
- Enter an invalid macro such as:

```text
tap
```

- Confirm macro diagnostics show an invalid state.
- Confirm a diagnostic line and message are visible.
- Switch back to a simple mapping and confirm invalid macro editor text does not block the simple mapping path.
- Type a partial command such as `ta` and confirm the completion popup appears near the editor caret rather than as a fixed bottom panel.
- Create an empty macro line and confirm command candidates are available near the caret.
- Type `loop ` and confirm loop argument candidates such as `0` / `infinite` are available.
- Type `tap ` and confirm key candidates are available.
- Type a complete unique token such as `loop` or `loop 0` and confirm the popup does not keep showing an already-complete single candidate.
- Press Tab or Enter while the popup is open and confirm the selected completion is inserted without corrupting script text.
- Press Esc while the popup is open, then press Enter and confirm Enter returns to normal editor behavior instead of applying a hidden completion.
- Open the popup again and double-click a completion item; confirm insertion still preserves the surrounding script text.
- Move the caret or scroll the editor and confirm the popup follows the caret or closes/reopens in a reasonable caret-near position.
- Switch away from the app while the popup is open, then switch back and confirm the popup is closed and the rest of the GUI remains interactive.
- Edit a multi-line macro and scroll vertically; confirm line numbers remain aligned with visible text lines.
- Double-click a diagnostic row when an invalid macro reports a line number and confirm the editor jumps to that line.
- Confirm detailed macro diagnostics are shown in the main diagnostics panel while the editor itself only shows a short status / border state.
- If testing with a Chinese IME, confirm the macro editor defaults to ASCII-oriented input; Chinese comments may require pasting or changing input settings outside this smoke path.
- Treat token preview and deeper editor polish as manual visual confidence only. They are not GUI RC0 blockers unless they crash the app or corrupt the script text.

## Runtime Dry-Run

- Confirm dry-run is checked.
- Click Start Runtime.
- Confirm no live confirmation dialog appears.
- Confirm status changes to running.
- Click refresh status.
- Confirm runtime snapshot fields update.
- Use dry-run simulate down / up.
- Confirm input event count changes.
- Confirm diagnostics log receives events.
- Modify a config field and reload config.
- Confirm reload does not crash the UI.
- Stop runtime.
- Confirm status returns to stopped.
- Confirm the UI returns to a state where runtime can be started again.

## Foreground Probe

- Click foreground probe.
- Confirm foreground probe text updates.
- With BAKeySmith.App in foreground, confirm the probe reports blocked /
  `gui_self_foreground_blocked`.
- Confirm the result displays allowed / blocked / unknown style information.
- Do not require a real Blue Archive foreground result for GUI RC0.

## Live Safety

- Uncheck dry-run.
- Confirm the live warning appears.
- Click Start Runtime.
- If BAKeySmith is not running as administrator, confirm live start is blocked before the second confirmation dialog.
- If BAKeySmith is not running as administrator, confirm runtime does not start and diagnostics / LastError say Blue Archive live mode requires Run as administrator.
- If BAKeySmith is running as administrator, confirm a second confirmation dialog appears.
- If the confirmation dialog appears, confirm it says live mode installs a global hook and sends real input.
- If the confirmation dialog appears, confirm it says live mode should only be used when ready to test.
- If the confirmation dialog appears, confirm it asks the user to check target process / foreground state.
- If the confirmation dialog appears, click Cancel.
- Confirm runtime does not start after either the non-elevated guard or confirmation Cancel path.
- Confirm diagnostics and / or LastError show live start was blocked or cancelled.
- Confirm no real live input is sent.
- Do not start Blue Archive manual validation.
- Re-check dry-run before continuing.
- Save config and confirm live mode is not written to AppConfigV1.

## Diagnostics

- Confirm the diagnostics list is visible.
- Confirm events appear while using dry-run start / simulate / stop.
- Click clear diagnostics.
- Confirm the diagnostics list is cleared.
- WPF UI automation is not required for GUI RC0; manual confidence is sufficient for this checklist.

## End

- Stop runtime if it is running.
- Close the window.
- Confirm no obvious unexpected error dialog appears during close.

## Result Recording

Record:

- date / time;
- build or commit identifier if available;
- whether the checklist passed;
- any failed step;
- whether the run stayed dry-run only.

Do not report this checklist as release-ready RC evidence. Report it as GUI RC0 manual smoke evidence only.
