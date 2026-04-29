# Product Roadmap

This roadmap is planning-only. It does not authorize implementation by itself.

## Current Checkpoint

- Core RC0 candidate: yes.
- GUI RC0 candidate: yes.
- Release-ready RC: no.
- Public C# GUI release: not now.
- Python beta: remains public usable baseline.

## Near-Term Stabilization

- Fix / document public beta known issues.
- Keep Macro editor UX polish as `PASS with P2 known issue`.
- Decide whether to hide line-number gutter, refactor editor, or evaluate
  AvalonEdit before public beta.
- Improve duplicate / conflict row-level highlighting.
- Improve dry-run `输入事件` wording.
- Keep release-ready gate and packaging sign-off honest.

## First Usable Version

Target:

- create / edit / save mappings;
- dry-run preview;
- live run with admin safety;
- stop / reload cleanup;
- simple tap / hold;
- macro lifecycle;
- running safe edit / capture;
- coordinate capture and `tap_at` fallback path.

See [first-usable-version-contract.md](first-usable-version-contract.md).

## V2 Design Parallel Track

Design in parallel:

- Input Model v2;
- Key Name v2;
- Input Capture v2;
- Conflict Model v2;
- Runtime v2;
- Macro DSL v2;
- Macro Editor assistance v2;
- Blue Archive coordinate model;
- AppConfigV2.

## Future Major

Deferrable major items:

- profiles / layers;
- sequence triggers;
- plugin-driven triggers;
- visual macro builder;
- trusted automation plugin API;
- raw plugin scripting;
- installer / winget / Store.

