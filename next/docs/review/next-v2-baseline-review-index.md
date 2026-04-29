# Next V2 Baseline Review Index

This document indexes the `next/` review snapshot for external audit. It is a
review snapshot, not a release branch, not a public C# GUI release, and not a
replacement for the Python beta.

## Snapshot Identity

- Review branch: `review/next-v2-design-consolidation-baseline`
- Source base commit before this review snapshot: `6350221ad0d818829d4485c4ac8cebc7df405ad6`
- Review snapshot commit: see the commit containing this file and the final
  handoff report for the exact branch HEAD.

No functional code was changed for the purpose of creating this index. The
snapshot branch includes the current accepted `next/` baseline for review.

## Solution Projects

Registered in `next/BAKeySmith.Next.slnx`:

- `src/BAKeySmith.App/BAKeySmith.App.csproj`
- `src/BAKeySmith.Core/BAKeySmith.Core.csproj`
- `tests/BAKeySmith.App.Tests/BAKeySmith.App.Tests.csproj`
- `tests/BAKeySmith.Core.SmokeTests/BAKeySmith.Core.SmokeTests.csproj`
- `tools/BAKeySmith.Acceptance/BAKeySmith.Acceptance.csproj`
- `tools/BAKeySmith.Core.Benchmarks/BAKeySmith.Core.Benchmarks.csproj`
- `tools/BAKeySmith.Headless/BAKeySmith.Headless.csproj`

## Included Review Scope

The intended review scope is:

- `next/BAKeySmith.Next.slnx`
- `next/README.md`
- `next/src/`
- `next/tests/`
- `next/tools/`
- `next/docs/`
- `next/docs/examples/`
- root `AGENTS.md`

This scope intentionally excludes legacy Python beta source files in the root
unless they are separately requested by reviewers.

## `next/` Tree Summary

- `next/src/BAKeySmith.Core/`: C# runtime, configuration, input, foreground,
  scripting, trigger, hosting, and backend code.
- `next/src/BAKeySmith.App/`: WPF GUI RC0 candidate, ViewModels, services,
  focused key capture, runtime controller, macro editor control, diagnostics
  display, and live safety / elevation UI.
- `next/tests/BAKeySmith.Core.SmokeTests/`: core smoke coverage.
- `next/tests/BAKeySmith.App.Tests/`: App / ViewModel / GUI-side service tests.
- `next/tools/BAKeySmith.Acceptance/`: deterministic acceptance runner and
  harness scenarios.
- `next/tools/BAKeySmith.Headless/`: headless composition runner.
- `next/tools/BAKeySmith.Core.Benchmarks/`: benchmark harness.
- `next/docs/`: current contracts, release gates, v2 design notes, and review
  docs.
- `next/docs/examples/`: archived stable sample reports and latest evidence
  where explicitly documented.

Generated `bin/` and `obj/` outputs are not review artifacts and should not be
committed.

## Newly Added V2 Design Docs

- `next/docs/first-usable-version-contract.md`
- `next/docs/architecture-v2-direction.md`
- `next/docs/input-model-v2-design.md`
- `next/docs/key-name-v2-contract.md`
- `next/docs/input-capture-v2-design.md`
- `next/docs/conflict-model-v2-design.md`
- `next/docs/runtime-v2-design-notes.md`
- `next/docs/macro-dsl-v2-design-notes.md`
- `next/docs/macro-editor-assistance-v2.md`
- `next/docs/bluearchive-coordinate-model.md`
- `next/docs/appconfig-v2-design-notes.md`
- `next/docs/product-roadmap.md`
- `next/docs/known-issues.md`

These documents are planning contracts only. They do not implement v2.

## Key Implementation Files For Review

Input / key names:

- `next/src/BAKeySmith.Core/Input/KeyNameResolver.cs`
- `next/src/BAKeySmith.App/Services/KeyCaptureFormatter.cs`
- `next/src/BAKeySmith.App/Models/KeyCaptureResult.cs`

Hook / trigger / capture policy:

- `next/src/BAKeySmith.Core/Triggers/WindowsHookTriggerSource.cs`
- `next/src/BAKeySmith.Core/Triggers/TriggerPipeline.cs`
- `next/src/BAKeySmith.Core/Triggers/ITriggerCapturePolicySink.cs`

Runtime / hosting:

- `next/src/BAKeySmith.Core/Hosting/RuntimeHost.cs`
- `next/src/BAKeySmith.Core/Runtime/InProcessRuntimeCore.cs`
- `next/src/BAKeySmith.App/Services/RuntimeHostController.cs`
- `next/src/BAKeySmith.App/Services/GuiSelfForegroundGate.cs`

Configuration:

- `next/src/BAKeySmith.Core/Configuration/AppConfigSerializer.cs`
- `next/src/BAKeySmith.Core/Configuration/AppConfigV1.cs`
- `next/src/BAKeySmith.Core/Configuration/MappingConfigV1.cs`
- `next/src/BAKeySmith.App/Services/ConfigDocumentService.cs`

Macro / editor:

- `next/src/BAKeySmith.Core/Scripting/MacroScriptCompiler.cs`
- `next/src/BAKeySmith.Core/Scripting/MacroScriptCompletionProvider.cs`
- `next/src/BAKeySmith.App/Controls/MacroEditorControl.xaml`
- `next/src/BAKeySmith.App/Controls/MacroEditorControl.xaml.cs`
- `next/src/BAKeySmith.App/Controls/MacroCompletionPopupPolicy.cs`

GUI ViewModels:

- `next/src/BAKeySmith.App/ViewModels/MainWindowViewModel.cs`
- `next/src/BAKeySmith.App/ViewModels/MappingEditorViewModel.cs`
- `next/src/BAKeySmith.App/ViewModels/MacroDiagnosticsViewModel.cs`
- `next/src/BAKeySmith.App/ViewModels/RuntimeStatusViewModel.cs`
- `next/src/BAKeySmith.App/ViewModels/DiagnosticsLogViewModel.cs`

## Current Known P2 Items

- Macro editor line-number gutter clipping remains imperfect.
- Current `MacroEditorControl` is not the final professional editor route.
- Duplicate / conflict row-level red highlight is missing.
- Dry-run `输入事件` wording should be improved.
- Current focused capture does not satisfy the v2 full-key target.
- OEM / punctuation focused capture is missing from current v1 capture.
- Side-specific modifiers are missing from current implementation.
- Raw input / scan-code fallback is missing.
- WPF UI automation is missing.
- `packaging planning ` trailing-space artifact warning remains repository
  hygiene P2.
- Cursor-preserving coordinate backend is not verified.
- Real cursor fallback has player-mouse interference risk.
- AppConfigV2 / RuntimeV2 / Macro DSL v2 are not implemented.

See `next/docs/known-issues.md`.

## Not Run In This Snapshot Step

This branch creation / index step did not run:

- live input;
- real-target validation;
- Blue Archive manual validation;
- packaging;
- release publication.

The latest standard Macro editor follow-up validation before this snapshot was:

- `dotnet build next\BAKeySmith.Next.slnx`: PASS
- App tests: PASS
- `BAKeySmith.App --smoke`: PASS

No additional build or test gate is required for this index-only snapshot step.

