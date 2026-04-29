# Known Issues

This file lists accepted known issues and gaps. It is not a bug tracker
replacement, but it prevents current status from being overstated.

## Current P2 Known Issues

- Macro editor line-number gutter clipping remains imperfect.
- `MacroEditorControl` is not the final professional editor route.
- duplicate / conflict row-level red highlight is missing.
- dry-run `输入事件` wording should be improved, likely to output events /
  输出事件.
- Current focused capture does not satisfy v2 full-key target.
- OEM / punctuation focused capture is missing from current v1 capture.
- side-specific modifiers are missing from current implementation.
- raw input / scan-code fallback is missing.
- WPF UI automation is missing.
- `packaging planning ` trailing-space artifact warning remains repository
  hygiene P2.
- cursor-preserving coordinate backend is not verified.
- real cursor fallback has risk of interfering with the player's mouse.
- AppConfigV2 is not implemented.
- RuntimeV2 is not implemented.
- Macro DSL v2 is not implemented.

## Macro Editor Status

Accepted:

- completion popup / contextual completion policy;
- popup lifecycle / Esc / Tab / Enter behavior;
- diagnostics duplicate reduction;
- line-number count improvement.

Deferred:

- line-number clipping polish.

Future options:

- editor control refactor;
- AvalonEdit evaluation;
- hiding line-number gutter before public beta.

## Release-Ready Gaps

- release-ready RC gate is not complete for a shipping candidate;
- portable zip packaging dry run not complete;
- first launch from extracted package not complete;
- release notes / known issue sign-off not complete;
- coordinate model not implemented;
- public C# GUI release should not happen now.

