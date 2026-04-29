# Codex Project Rules

This repository has three distinct tracks:

- Python beta is the current public, downloadable, usable version. Treat it as the behavior baseline, reference implementation, and regression-spec source.
- `next/` C# is the local next-generation mainline and current runtime/engineering baseline.
- Rust is a future replaceable runtime-core reserve path. Do not start Rust work unless explicitly authorized.

Default scope boundaries:

- Do not enter GUI/editor work, Rust, complete drag, `mouse_left/right`, double-click/repeated-click, or long real-target soak unless explicitly authorized.
- Do not overwrite, delete, or republish the Python beta by default.
- Runtime public-path changes must state the impact surface and be backed by smoke, acceptance, and docs updates appropriate to the change.
- Every report must distinguish what is proven, not proven, and intentionally not being proven, and must label failed/pending/invalid samples clearly.

Current project state lives in [`next/docs/project-state.md`](next/docs/project-state.md). `BAKeySmith.App` is a GUI RC0 candidate with gate dry run passed, not release-ready GUI. Further GUI work requires explicit authorization and must stay within [`next/docs/gui-entry-contract.md`](next/docs/gui-entry-contract.md). Packaging and release-ready planning lives in [`next/docs/packaging-contract.md`](next/docs/packaging-contract.md); Blue Archive live mode requires BAKeySmith administrator elevation.

Professional keymapper contracts live in [`next/docs/key-name-contract.md`](next/docs/key-name-contract.md), [`next/docs/macro-language-contract.md`](next/docs/macro-language-contract.md), and [`next/docs/professional-keymapper-contract.md`](next/docs/professional-keymapper-contract.md). GUI focused key capture v1 is implemented in the App layer only; advanced capture, raw input, scan-code fallback, and side-specific modifiers remain future work. Capture UX must not reject existing modifier-only triggers such as `ctrl`, `alt`, and `shift`, and must not claim side-specific modifier support until it is implemented and proven.

Root `*-code-review-bundle.md` files are historical external-review bundles. Do not treat them as current contract sources; current contracts live under `next/docs/`.

Detailed operating rules live in [`next/docs/codex-operating-contract.md`](next/docs/codex-operating-contract.md).
