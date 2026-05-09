# Next Task Queue

This queue records recommended next steps after the current Core-only V2
sandbox chain.

## Current Stop Point

Current completed node:

```text
AppConfigV2 -> RuntimeV2 sandbox adapter skeleton + test-closure
```

The adapter audit completed with no P0 or P1 findings.

The repository hygiene audit completed and found that the current GitHub review
branch exists but does not include the latest uncommitted V2 sandbox baseline.

Do not continue feature implementation before producing a selective review
snapshot.

## Recommended Order

1. Create a selective GitHub review snapshot branch or update an explicitly
   named review branch with the current V2 baseline.
2. Provide ChatGPT / reviewer with the branch link and exact commit hash.
3. After review snapshot handoff, run a Runtime v2 host boundary design audit.
4. Do not jump directly into GUI, hook, or real input integration.
5. Coordinate Capture UI can be planned later, but it should not jump ahead of
   Runtime v2 boundary review and review baseline work.

## Snapshot Branch Strategy

Use selective staging. Do not use `git add -A`.

Recommended include scope:

- `next/src/` V2 baseline code;
- `next/tests/` related Core/App tests;
- `next/docs/` current V2 docs;
- `next/docs/dev/current-v2-state.md`;
- `next/docs/dev/codex-operating-rules.md`;
- `next/docs/dev/next-task-queue.md`;
- `next/src/BAKeySmith.App/Services/AppConfigV2DraftDocumentService.cs`.

Recommended exclude scope unless explicitly approved:

- root Python beta dirty files;
- root historical `*-code-review-bundle.md`;
- `next.7z`;
- `next/gui-smoke.config.json`;
- `next/gui-smoke.config.json.bak`;
- root probe / benchmark scripts;
- `packaging planning ` trailing-space artifact;
- root `docs/`.

## Next Implementation Candidates After Snapshot

Preferred next design line:

- Runtime v2 host boundary design audit.

Do not start these until explicitly authorized:

- GUI v2 integration;
- hook integration;
- real input backend;
- Macro DSL v2 compiler;
- coordinate live backend;
- production AppConfigV2 service;
- production migration;
- packaging or release.

## Current P2 Queue

- AppConfigV2 unknown-field preservation.
- Production atomic save / replace / backup.
- Production path root policy.
- Runtime v2 fake sandbox to host boundary.
- Real backend and `SendInput` path.
- Hook integration boundary.
- GUI v2 migration.
- Macro DSL v2 compiler.
- Coordinate live backend.
- Timing default resolution.
- Source-scan no-integration guard brittleness.
- Core smoke test split.
- `CanBuildCompleteRuntime` naming clarity.
- Repository hygiene artifacts and historical bundles.

## Handoff Reminder

The next task should be review-snapshot work, not a new feature. If a future
turn proposes implementation before snapshot, stop and confirm scope first.

