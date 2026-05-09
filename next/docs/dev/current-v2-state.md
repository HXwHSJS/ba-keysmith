# Current V2 State

This handoff records the current BAKS C# `next/` and V2 sandbox state for
future ChatGPT, Codex, and GitHub review work.

## Project Structure

- Root Python beta is the historical public, downloadable, usable version.
- Root Python beta remains the behavior baseline and reference implementation.
- `next/` is the C# rewrite root and current next-generation engineering track.
- Do not move or rename `next/`.
- Do not sweep root Python beta files into a `next/` review snapshot unless a
  later task explicitly scopes them in.
- Historical root `*-code-review-bundle.md` files are external review bundles.
  They are not current contract sources.
- Current contracts and design notes live under `next/docs/`.

See also:

- [Project state](../project-state.md)
- [Architecture v2 direction](../architecture-v2-direction.md)
- [AppConfig v2 design notes](../appconfig-v2-design-notes.md)
- [Runtime v2 design notes](../runtime-v2-design-notes.md)

## V1 And V2 Boundary

- V1 is the current real GUI, runtime, and config path.
- V1 owns the default config path, current `RuntimeHost`, hook integration, GUI
  startup/load/save flow, and real input behavior.
- V2 is parallel draft, skeleton, and sandbox work.
- V2 must not pollute or silently replace V1.
- Do not connect V2 to the default GUI config path.
- Do not connect V2 to the existing `RuntimeHost`.
- Do not connect V2 to hook sources or real input.
- Do not use V2 as a release-ready runtime path.

## Completed V2 Modules

The following V2 modules have been implemented as Core-only or explicit
developer/test-only sandbox pieces:

- Input Model v2 / KeyName v2.
- Input Capture normalization v2.
- Conflict Model v2.
- Action Model v2 / ActivationPlan v2.
- Simple Mapping v2 builder.
- AppConfigV2 draft model / validator.
- AppConfigV2 JSON serializer draft.
- AppConfigV2 draft document sandbox.
- AppConfigV2 draft sandbox path / IO hardening.
- Runtime v2 fake executor.
- Runtime v2 `MappingActivationSessionManager`.
- AppConfigV2 simple mapping to RuntimeV2 sandbox adapter.
- Adapter test-closure.

## Current Core-Only V2 Chain

The current sandbox chain is:

```text
AppConfigV2
-> SimpleMappingPlanBuilderV2
-> ActivationPlanV2
-> RuntimeV2 fake executor
-> MappingActivationSessionManagerV2
-> RuntimeV2 config adapter
```

The runtime-facing adapter also forms the practical bridge from `AppConfigV2`
enabled simple mappings into runtime sandbox entries.

This chain is Core-only sandbox work:

- It is not live runtime.
- It does not send real input.
- It does not call `SendInput`.
- It does not listen to hooks.
- It is not connected to GUI.
- It is not connected to `ConfigDocumentService`.
- It is not connected to the default V1 config path.

## Latest Node Status

- `AppConfigV2 -> RuntimeV2 sandbox adapter skeleton + test-closure` is
  complete.
- Adapter audit completed with no P0 or P1 findings.
- Repository hygiene audit completed.
- Current GitHub review branch exists but is behind the current uncommitted V2
  sandbox baseline.
- Current next task is a selective review snapshot branch/commit, not more
  feature implementation.

## Current P2 And Production Blockers

- AppConfigV2 unknown-field preservation is not implemented.
- Production atomic save / replace / backup strategy is not implemented.
- Production path root policy is not implemented.
- Runtime v2 remains a fake sandbox.
- There is no real backend and no real `SendInput` path.
- There is no hook integration.
- There is no GUI v2 integration.
- Macro DSL v2 compiler is not implemented.
- Coordinate live backend is not implemented.
- Timing default resolution is not implemented.
- Source-scan no-integration guard is useful but brittle.
- Core smoke tests are large and should eventually be split.
- `CanBuildCompleteRuntime` can be misread as production-ready. It currently
  means adapter-level blocker semantics only.
- Repository hygiene items remain:
  - historical root review bundles;
  - `next.7z`;
  - `next/gui-smoke.config.json`;
  - `next/gui-smoke.config.json.bak`;
  - `packaging planning ` trailing-space warning;
  - root dirty Python beta files.

## Review Snapshot Readiness

The current local tree contains valid V2 baseline candidates and unrelated
local artifacts. Do not stage everything.

Include only the explicit V2 baseline scope in a future review snapshot. Keep
root Python beta and historical artifacts out unless a later task explicitly
approves them.

