# First Usable Version Contract

This document defines the intended first usable C# GUI version after the current
GUI RC0 candidate. It is a planning contract only. It does not implement or
authorize code, runtime, schema, Macro DSL, coordinate, plugin, packaging, or
release changes.

## Current Status

- Core RC0 candidate exists.
- GUI RC0 candidate exists and GUI RC0 gate dry run has passed.
- Release-ready RC does not exist.
- Public C# GUI release should not happen now.
- Python beta remains the current public usable version, behavior baseline,
  reference implementation, and regression-spec source.
- AppConfigV1 remains frozen.
- Macro DSL v1 remains frozen for the current `next/` engineering baseline.
- Current C# `next/` remains the engineering baseline while v2 design proceeds
  in parallel.
- Macro editor UX polish can be fixed as `PASS with P2 known issue`: completion
  popup, contextual completion, Esc / Tab / Enter behavior, popup lifecycle, and
  diagnostics duplicate reduction are accepted; line-number gutter clipping is
  a P2 known issue.

## Primary Users

BAKS primarily targets Chinese users.

Product expectations:

- UI labels, errors, settings text, search aliases, examples, and public docs
  should be Chinese-friendly first.
- Canonical config and Macro DSL command names should remain English ASCII for
  script safety and portability.
- The macro command area should avoid Chinese IME interference for command
  typing.
- Comment lines, command palette search, and fuzzy search may support Chinese
  input.
- Documentation and launch materials should teach the system in Chinese-friendly
  terms, while the app itself should stay concise.

## First Usable Scope

The first usable C# GUI version must be genuinely usable, not only an
architecture preview.

Required user-visible capabilities:

- create, edit, and save mappings;
- dry-run preview;
- live run with admin / elevation guard;
- stop / reload cleanup;
- simple `tap`;
- simple `hold`;
- macro lifecycle;
- safe editing and focused capture while runtime is running;
- coordinate capture and a usable coordinate `tap_at` fallback path;
- visible config validation and conflict diagnostics;
- live safety warnings and cancellation path.

Required runtime qualities:

- save / reload remains transactional;
- invalid reload keeps the old runtime config active;
- stop, reload, foreground loss, and emergency stop cleanup held ownership;
- dry-run remains default;
- Blue Archive live mode requires BAKeySmith administrator elevation.

## Explicit Non-Goals For First Usable Version

The first usable version should not implement:

- plugin API;
- profiles / layers;
- sequence triggers;
- complex visual macro builder;
- raw plugin scripting;
- automatic public replacement of Python beta;
- installer / winget / Store package;
- arbitrary DLL loading;
- hidden AppConfigV1 expansion for v2 features.

## Required Companion Designs

The first usable version must be planned against:

- [architecture-v2-direction.md](architecture-v2-direction.md)
- [input-model-v2-design.md](input-model-v2-design.md)
- [key-name-v2-contract.md](key-name-v2-contract.md)
- [input-capture-v2-design.md](input-capture-v2-design.md)
- [conflict-model-v2-design.md](conflict-model-v2-design.md)
- [runtime-v2-design-notes.md](runtime-v2-design-notes.md)
- [macro-dsl-v2-design-notes.md](macro-dsl-v2-design-notes.md)
- [macro-editor-assistance-v2.md](macro-editor-assistance-v2.md)
- [bluearchive-coordinate-model.md](bluearchive-coordinate-model.md)
- [appconfig-v2-design-notes.md](appconfig-v2-design-notes.md)
- [known-issues.md](known-issues.md)

## Release Boundary

First usable version is not the same as release-ready RC.

Before public C# GUI beta / preview:

- portable zip packaging dry run must pass;
- self-contained folder publish must be verified;
- first launch from extracted folder must pass;
- config path smoke must pass;
- admin / live safety docs must be present;
- release notes must clearly state Python beta is not replaced;
- known issues must be visible.

