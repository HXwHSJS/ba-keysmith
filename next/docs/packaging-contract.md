# Packaging Contract

This document defines the packaging and release-readiness contract for the first C# GUI preview. It is planning documentation only. It does not authorize packaging, publishing, runtime changes, schema changes, or GitHub release work.

## Status

- Python beta remains the current public, downloadable, usable version, behavior baseline, reference implementation, and regression-spec source.
- `next/` C# is the local next-generation runtime / engineering baseline and current GUI RC0 candidate track.
- `BAKeySmith.App` has passed the GUI RC0 gate dry run, but it is not release-ready GUI.
- The project is not release-ready RC.
- Rust remains a future runtime-core reserve path and is not active.
- First usable version and v2 architecture planning are documented, but not
  implemented. See [first-usable-version-contract.md](first-usable-version-contract.md)
  and [architecture-v2-direction.md](architecture-v2-direction.md).

## First Packaging Shape

The first C# GUI preview should use a portable zip package.

Recommended shape:

- portable zip;
- self-contained folder publish preferred;
- no installer;
- no MSIX, Microsoft Store, winget, or store-like distribution;
- no auto-update;
- single-file publish is not the first choice.

Rationale:

- Portable zip keeps the first preview easy to inspect, archive, and remove.
- Self-contained folder publish avoids requiring users to install a matching .NET Desktop Runtime before trying the app.
- A folder publish makes examples, safety notes, and docs visible instead of hiding them inside a single-file bundle.
- Installer, signing, store, winget, and auto-update add release-operation risk before the runtime / GUI preview is ready for public replacement of Python beta.

## Zip Contents

The zip should contain, at minimum:

- published app folder;
- the app executable, currently expected to be `BAKeySmith.exe` from the `BAKeySmith.App` project output;
- `examples/config.example.json`;
- `README.md`;
- `QUICKSTART.md`;
- `LIVE-MODE-SAFETY.md`;
- release notes;
- license;
- bundled docs summary or links to the relevant docs.

These entries are packaging targets, not current repository files.
`examples/config.example.json`, `QUICKSTART.md`, `LIVE-MODE-SAFETY.md`,
release notes, and bundled docs summaries are created or selected during
packaging implementation / sign-off. This contract does not create them.

The zip should make these facts obvious:

- the C# GUI preview does not replace Python beta;
- GUI RC0 candidate does not mean release-ready RC;
- dry-run is the default;
- Blue Archive live mode requires administrator privileges;
- dry / harness conclusions are not Blue Archive real-target SLOs.
- AppConfigV2, RuntimeV2, Macro DSL v2, cursor-preserving coordinate backend,
  plugin API, side-specific modifiers, and full OEM / punctuation capture are
  not implemented unless a future release note explicitly says otherwise.

## Do Not Mislead

The package and release notes must not:

- treat the bundled example config as the user's default writable config;
- present the C# GUI preview as a drop-in replacement for Python beta;
- present GUI RC0 candidate as release-ready GUI or release-ready RC;
- present deterministic dry / harness evidence as a Blue Archive real-target SLO;
- imply that unproven boundaries are complete, including `mouse_left/right`, double-click / repeated click, real-target complete drag SLO, or long real-target product soak.

## Admin / Elevation Contract

Known project fact:

- Blue Archive currently runs as an administrator target.
- The existing Python beta also needs to run as administrator to affect Blue Archive.
- Therefore C# GUI Blue Archive live mode must be treated as requiring BAKeySmith to run as administrator.

Required release-ready position:

- dry-run and config editing may run without administrator privileges;
- Blue Archive live mode requires running BAKeySmith as administrator;
- public beta / preview quickstart must instruct users to use Run as administrator when controlling administrator-run Blue Archive;
- GUI displays whether BAKeySmith is elevated;
- non-elevated Blue Archive live start is blocked before live confirmation / runtime start;
- release-ready sign-off must verify both elevated and non-elevated display / guard behavior.
- automatic relaunch / UAC automation is not required for the first preview.

`admin/elevation status + live elevated guard` is implemented in the GUI layer and remains a release-ready sign-off item. It is not optional P2 polish.

## Config / Examples / First Launch

Packaged examples:

- packaged examples should live under `examples/config.example.json`;
- bundled examples are read-only examples from the user's point of view;
- the GUI must not silently write to the bundled example file.

This is a future packaged layout. The current repository does not need
`examples/config.example.json` to exist until packaging implementation /
sign-off creates or selects it.

Recommended user config location:

- `%APPDATA%\BAKeySmith\config.json`

First-launch behavior:

- current GUI behavior: the default editable config path is
  `%APPDATA%\BAKeySmith\config.json`;
- if that user config does not exist, the GUI loads an in-memory default config
  and reports a warning;
- the GUI does not silently write `%APPDATA%\BAKeySmith\config.json`;
- the GUI does not silently write to or auto-load the bundled example file;
- when the user explicitly saves while the current config path is
  `%APPDATA%\BAKeySmith\config.json`, the GUI creates `%APPDATA%\BAKeySmith\`
  and writes `config.json`;
- later launches can load that saved user config from the default path;
- the GUI should prompt the user to choose or save a config path before relying on persistence;
- first-launch config creation / copy-from-example behavior remains a release-ready gap until implemented and verified.

Config compatibility:

- AppConfigV1 freeze remains in force;
- do not introduce AppConfigV2 for packaging;
- do not change `AppConfigSerializer` behavior as part of packaging;
- Python beta configuration remains the behavior/reference baseline;
- Python beta to C# AppConfigV1 migration needs a guide before public C# GUI preview;
- automatic migration can be delayed. Documentation-based migration should come first.

## GitHub / Release Strategy

Use the existing repository:

- `https://github.com/HXwHSJS/ba-keysmith`

Do not create a new repository for the C# GUI preview unless a future explicit release decision changes this.

Repository strategy:

- keep Python beta in the root because it is the current public usable version;
- keep `next/` as the C# runtime / GUI next-generation mainline until release-ready RC is established;
- do not overwrite the root README's Python beta entry point yet;
- use prerelease / clearly marked preview releases for the first C# GUI package;
- release notes must state that C# GUI preview does not replace Python beta;
- release notes must state Blue Archive live mode requires administrator privileges;
- release notes must list unproven boundaries and avoid implying release-ready RC.

Only after the C# GUI becomes the public mainline with migration docs, release gate evidence, packaging smoke, and accepted release notes should the repository README main entry point be reconsidered.

## Release-Ready RC Gate Classification

Release-ready RC must pass:

- `dotnet build .\next\BAKeySmith.Next.slnx`;
- Core smoke tests;
- App tests;
- `BAKeySmith.App --smoke`;
- `Acceptance --scenario all`;
- `Acceptance --scenario lifecycle-stress`;
- `Acceptance --scenario trigger-suppress --allow-live-input`;
- `Acceptance --scenario live-safe --allow-live-input`;
- 10-minute dry-run soak;
- 10-minute live soak;
- `bluearchive-manual`;
- `bluearchive-manual-phase2a`;
- GUI manual smoke checklist.

Packaging sign-off must pass:

- Release self-contained folder publish;
- zip / unzip smoke;
- first launch from extracted folder;
- packaged example config load;
- user config save to `%APPDATA%\BAKeySmith\config.json` or an equivalent user-writable path;
- live warning / cancel path smoke without clicking OK;
- admin / elevation docs check;
- elevated and non-elevated status display checks.

Only when touched / supplement:

- Phase 2B / 2C / 2D stable supplement samples;
- complete drag product real-target samples;
- wheel / XButton latest JSON;
- related public-path sample promotion when a change touches that path.

Deferrable:

- installer;
- signing;
- winget / Store;
- WPF UI automation;
- acceptance `Program.cs` split;
- `mouse_left/right`;
- double-click / repeated click;
- broader long soak beyond the current required gate.

Stable supplement samples are not default RC gate items unless the current change touches their public path and explicitly promotes them to sign-off for that change.

## GUI UX Polish Scheduling

Public beta / preview before release-ready should preserve and verify:

- admin / elevation status and live elevated guard;

Public beta / preview before release-ready should fix:

- dry-run `输入事件` wording, preferably toward output events / 输出事件;
- duplicate trigger error feedback visibility.

RC-after or nice-to-have:

- layout visual polish beyond the accepted RC0 accessibility contract;
- `MacroEditorControl` local status and ViewModel diagnostics duplication cleanup;
- WPF UI automation spike.
- Macro editor line-number gutter clipping remains P2 unless public beta
  decides to hide the gutter, refactor the editor, or evaluate AvalonEdit.

Admin / elevation is release-ready P1. Do not classify it as ordinary P2 polish.
