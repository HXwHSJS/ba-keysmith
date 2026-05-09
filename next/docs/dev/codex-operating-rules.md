# Codex Operating Rules

This document summarizes operating rules for future Codex work on the current
BAKS C# `next/` / V2 baseline.

For the broader contract, see [Codex operating contract](../codex-operating-contract.md).

## Per-Turn Rules

Every task should stay on a single subline.

Each turn must state or preserve:

- allowed scope;
- forbidden scope;
- modified files;
- validation run;
- P0 / P1 / P2 status;
- whether work stopped.

Do not silently expand scope.

## Default Forbidden Work

Unless the user explicitly authorizes it, do not:

- change AppConfigV1 / MappingConfigV1 / AppConfigSerializer V1;
- connect V2 to the default V1 config path;
- connect V2 to `RuntimeHost`;
- connect V2 to hook sources;
- connect V2 to GUI;
- call real `SendInput`;
- implement real keyboard or mouse output;
- modify `WindowsInputBackend`;
- modify MacroScriptCompiler v1, completion, or MacroEditorControl;
- implement Macro DSL v2 compiler;
- implement coordinate live execution;
- run live input;
- run real-target tests;
- start Blue Archive manual testing;
- package;
- publish;
- push.

## Git And File Hygiene

- Do not use `git add -A`.
- Do not automatically clean files.
- Do not delete local artifacts unless the user explicitly asks.
- Do not move directories as a cleanup gesture.
- Do not rename `next/`.
- Do not rename V2 namespaces or folders without an explicit architecture task.
- Do not automatically commit.
- Do not automatically push.

The repository may contain dirty or untracked files from earlier work. Treat
them as user or local state until scoped in.

## V1 / V2 Safety

- V1 is the real GUI / runtime / config path.
- V2 is parallel draft / skeleton / sandbox work.
- V2 must stay isolated until an explicit integration task exists.
- A passing Core smoke test does not imply production runtime readiness.
- A V2 JSON roundtrip does not imply production config migration safety.
- A fake runtime event log does not imply live input support.

## Validation Rules

Run only the validation requested by the user.

When validation is requested, report:

- exact command;
- pass/fail result;
- skipped validation;
- why it was skipped.

Do not run live input, real-target, or Blue Archive manual testing unless the
user explicitly requests that line.

## Reporting Rules

Every completion report should distinguish:

- proven;
- not proven;
- intentionally not proven.

Mark P0 / P1 / P2 clearly.

Do not describe draft sandbox components as release-ready, production-safe, or
live runtime.

## Review Snapshot Staging Guidance

Future review snapshots should use selective staging.

Stage only explicitly scoped baseline files, such as:

- `next/src/` current V2 baseline code;
- `next/tests/` related Core/App tests;
- `next/docs/` current V2 docs;
- these handoff docs under `next/docs/dev/`;
- `next/src/BAKeySmith.App/Services/AppConfigV2DraftDocumentService.cs`.

Do not stage unless explicitly approved:

- root Python beta dirty files;
- root historical `*-code-review-bundle.md`;
- `next.7z`;
- `next/gui-smoke.config.json`;
- `next/gui-smoke.config.json.bak`;
- root probe or benchmark scripts;
- `packaging planning ` trailing-space artifact;
- root `docs/` unless explicitly scoped.

## Current Review Branch Note

The current review branch exists, but local uncommitted V2 baseline work is not
captured by the pushed branch yet. The next snapshot should be selective and
explicit.

