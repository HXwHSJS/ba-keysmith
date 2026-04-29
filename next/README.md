# BA KeySmith Next

This directory contains the C#/.NET rewrite track for BA KeySmith.

The Python beta is now the reference implementation, beta behavior baseline, and
regression-spec source. The C# baseline in `next/` is the current engineering
mainline. This is not a line-by-line Python port: the runtime is rebuilt around a
stable contract that a future Rust core can implement and compete against under the
same tests and benchmarks.

Phase 1 focus:

> 先证明 C# baseline 在真实 Windows live/runtime 条件下足够硬，再决定是否值得让 Rust core 接管。

Current status: Phase 1 has exited on the C# baseline. Phase 2A current scope has closed. Phase 2B (`wheel + XButton`), Phase 2C / 2C-B (`hold + foreground change`), Phase 2D / 2D-B / 2D-C / 2D-D / 2D-E / 2D-F (`drag / interruption / reload / foreground-loss / interruptible wait / built-in drag helper`), and Phase 2E first-batch (`complete drag / multisegment drag normal completion` dry harness coverage) have all closed at their currently defined minimal scopes without reopening the stable baseline. The `next/` C# runtime baseline can now be treated as a Core RC0 candidate, but not as a release-ready RC.

GUI shell planning / productization prep has completed under the GUI entry contract. `BAKeySmith.App` has entered first-stage implementation, passed the GUI RC0 gate dry run, and can be treated as a GUI RC0 candidate. It is not release-ready GUI and does not relax the release gate. The mainline remains the accepted C# runtime baseline plus a scoped WPF shell that consumes existing runtime/config/compiler boundaries.

Packaging / release-ready planning is documented separately. The first C# GUI preview should use a portable zip with a self-contained folder publish. It must not replace Python beta, and it must not be described as release-ready RC until the release-ready gate, packaging sign-off, and applicable real-target/manual validation are complete.

Current runtime-core track:

- explicit `IRuntimeCore` boundary
- owner-based key hold tracking
- unified tap sequencing
- per-mapping serial action queue for deterministic repeat triggers
- foreground gate abstraction
- diagnostics event stream
- Win32 `SendInput` backend using scan-code keyboard input
- foreground-window process gate
- DSL v1 compiler and macro executor
- config schema v1 with legacy Python config compatibility
- trigger source abstraction and async trigger pipeline
- Windows low-level hook source on a dedicated message-loop thread
- foreground-gated trigger suppress semantics for keyboard and mouse triggers
- captured trigger sessions for suppressing matching release events safely
- self-injected `SendInput` pass-through marking to prevent trigger feedback loops
- `RuntimeHost` app composition boundary
- `RuntimeHostSnapshot` lifecycle/ownership state boundary for GUI diagnostics
- trigger pipeline queued/handled/pending counters
- trigger pipeline dropped counters for stop-time discarded triggers
- runtime action queue pending/running counters
- `BAKeySmith.Acceptance` runner with stable JSON reports for the Phase 1 dry-run scenario matrix
- `lifecycle-stress` acceptance scenarios for deterministic start/stop/reload/enable-disable/interleaved stop checks
- `dry-run-soak` acceptance scenario with memory/thread/queue/worker/held-input metrics
- `live-safe` acceptance scenario for real Windows foreground gate, SendInput, reload, disable, and stop checks
- `live-soak` acceptance scenario for real Windows hook + foreground gate + SendInput long-run checks
- `BAKeySmith.Headless` runner for dry-run and live-chain validation
- WPF GUI shell consuming the same `RuntimeHost`
- WPF config editor for mappings, macro scripts, target process, hotkey, and tap timing
- WPF diagnostics page with runtime snapshot, held-key owners, and foreground probe
- WPF GUI RC0 candidate gate and manual smoke checklist
- dry-run-by-default GUI start flow with live-mode second confirmation
- GUI admin/elevation status display and non-elevated live-start guard
- packaging contract for portable zip preview and release-ready gate classification
- professional key naming, Macro DSL semantics, hot reload, edit safety, and conflict rule contracts
- reusable WPF macro editor control with line numbers, cursor status, validation, and Tab/double-click completion
- shared DSL completion provider and structured compiler diagnostics
- shared macro language catalog/service for script-safe key names, token classification, and editor hints
- hard runtime session stop cleanup
- dry-run input backend for deterministic tests and benchmarks

See [runtime-contract.md](docs/runtime-contract.md) for the C# first / Rust reserve strategy.
See [project-state.md](docs/project-state.md) for the current Core RC0 candidate status, proof boundaries, and next recommended subline.
See [gui-entry-contract.md](docs/gui-entry-contract.md) for the GUI boundary, AppConfigV1 freeze, and Macro DSL v1 freeze.
See [gui-manual-smoke-checklist.md](docs/gui-manual-smoke-checklist.md) for the GUI RC0 manual smoke checklist.
See [packaging-contract.md](docs/packaging-contract.md) for portable zip packaging, admin/elevation, config, and release strategy.
See [key-name-contract.md](docs/key-name-contract.md), [macro-language-contract.md](docs/macro-language-contract.md), and [professional-keymapper-contract.md](docs/professional-keymapper-contract.md) for professional keymapper naming, DSL, capture, hot reload, editing safety, and conflict boundaries.
See [phase-1-live-runtime-plan.md](docs/phase-1-live-runtime-plan.md) for the current live/runtime focus.
See [phase-1-exit.md](docs/phase-1-exit.md) for the Phase 1 exit gate and current status.
See [acceptance-schema.md](docs/acceptance-schema.md) for the stable Acceptance JSON contract.
See [manual-bluearchive-validation.md](docs/manual-bluearchive-validation.md) for the required real-target validation checklist.
See [release-gate.md](docs/release-gate.md) for the current release / validation gate and report naming rules.
See [migration-statement.md](docs/migration-statement.md) for the Python / C# mainline decision.

Real-target Blue Archive validation is gated by runner integrity: run the acceptance/headless/live runner as Administrator, record target/runner elevation in the report, and use a physical keyboard trigger for the first manual pass instead of generating the trigger via `SendInput`.

Known live-mode elevation requirement: Blue Archive currently runs as an administrator target, and Python beta also needs administrator privileges to affect it. Dry-run and config editing can run without administrator privileges, but Blue Archive live mode requires BAKeySmith to run as Administrator. GUI elevation status and non-elevated live-start guard are implemented in the App layer and must be verified during release-ready sign-off.

Build:

```powershell
dotnet build .\BAKeySmith.Next.slnx
```

Run smoke tests:

```powershell
dotnet run --project .\tests\BAKeySmith.Core.SmokeTests\BAKeySmith.Core.SmokeTests.csproj
```

Run acceptance matrix:

```powershell
dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario all --burst 50 --drain-timeout 5
dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario all --burst 50 --drain-timeout 5 --output acceptance-report.json
dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario xbutton2-triggered-complete-drag-normal-completion
dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario xbutton2-triggered-multisegment-drag-normal-completion
```

Run dry-run soak:

```powershell
dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario dry-run-soak --soak-seconds 300 --soak-rate 20 --drain-timeout 10 --output acceptance-dry-run-soak.json
```

Run lifecycle stress:

```powershell
dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario lifecycle-stress --burst 50 --drain-timeout 5 --output acceptance-lifecycle-stress.json
```

Run live-safe validation:

```powershell
dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario live-safe --allow-live-input --drain-timeout 5
```

Run live trigger suppression validation:

```powershell
dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario mouse-trigger-suppressed --allow-live-input --drain-timeout 5
dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario mouse-trigger-foreground-blocked --allow-live-input --drain-timeout 5
dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario trigger-captured-then-foreground-changes-before-release --allow-live-input --drain-timeout 5
dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario captured-repeat-down-does-not-redispatch --allow-live-input --drain-timeout 5
dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario trigger-suppress --allow-live-input --drain-timeout 5
```

Run live soak:

```powershell
dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario live-soak --allow-live-input --soak-seconds 60 --soak-rate 20 --drain-timeout 10 --output acceptance-live-soak.json
```

The `--output acceptance-report.json`, `acceptance-dry-run-soak.json`, and
`acceptance-live-soak.json` names above are local generated report outputs.
Archived stable samples live under `docs/examples/` and use `*-latest.json` or
`*-10min.json` names.

Run real-target manual validation from an elevated terminal:

```powershell
dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario bluearchive-manual --allow-live-input --target-process BlueArchive.exe --manual-trigger f8 --manual-timeout 15 --manual-confirm yes
```

Run expanded Phase 2A real-target manual validation from an elevated terminal:

```powershell
dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario bluearchive-manual-phase2a --allow-live-input --target-process BlueArchive.exe --manual-trigger f8 --manual-mouse-trigger mouse_middle --manual-reload-trigger f9 --manual-long-trigger f10 --manual-timeout 20 --manual-confirm yes
```

Stable contract / soak examples:

```text
docs\examples\acceptance-dry-run-soak-10min.json
docs\examples\acceptance-live-soak-10min.json
docs\examples\acceptance-short-matrix-latest.json
docs\examples\acceptance-lifecycle-stress-latest.json
docs\examples\acceptance-live-safe-latest.json
docs\examples\acceptance-trigger-suppress-latest.json
docs\examples\acceptance-bluearchive-manual-latest.json
docs\examples\acceptance-wheel-trigger-boundaries-latest.json
docs\examples\acceptance-xbutton-trigger-boundaries-latest.json
docs\examples\acceptance-xbutton-trigger-reload-disable-latest.json
docs\examples\acceptance-xbutton-self-injected-pass-through-latest.json
```

Latest manual real-target evidence:

```text
docs\examples\acceptance-bluearchive-manual-phase2a-latest.json
docs\examples\acceptance-bluearchive-manual-xbutton2-drag-minimal-latest.json
docs\examples\acceptance-bluearchive-manual-xbutton2-multisegment-move-minimal-latest.json
docs\examples\acceptance-bluearchive-manual-xbutton2-drag-reload-during-active-drag-old-trigger-blocked-latest.json
docs\examples\acceptance-bluearchive-manual-xbutton2-drag-reload-during-active-drag-new-trigger-allowed-latest.json
docs\examples\acceptance-bluearchive-manual-xbutton2-drag-foreground-loss-during-active-drag-latest.json
docs\examples\acceptance-bluearchive-manual-xbutton2-drag-foreground-loss-then-return-before-release-latest.json
```

Phase 2D-B stop/disable active-drag real-target latest JSON is not archived in this repository right now; treat that as a real-target archive gap, not as a stable supplement sample.
Phase 2E first-batch complete drag / multisegment drag normal completion currently has dry harness evidence only; no Blue Archive real-target latest JSON was added for it.

For the full current stable supplement sample list, see [release-gate.md](docs/release-gate.md).

The 10-minute dry/live soak examples are part of the acceptance contract for comparing the current C# baseline with any future Rust runtime core.

Run benchmark:

```powershell
dotnet run --project .\tools\BAKeySmith.Core.Benchmarks\BAKeySmith.Core.Benchmarks.csproj -- 10000
```

Run headless dry-run:

```powershell
dotnet run --project .\tools\BAKeySmith.Headless\BAKeySmith.Headless.csproj -- --config ..\config.example.json --dry-run --simulate q:down --duration 1
```

Run headless with stop-cleanup assertion:

```powershell
dotnet run --project .\tools\BAKeySmith.Headless\BAKeySmith.Headless.csproj -- --config ..\config.example.json --dry-run --simulate q:down --duration 1 --snapshot --assert-clean
```

Run headless high-frequency burst check:

```powershell
dotnet run --project .\tools\BAKeySmith.Headless\BAKeySmith.Headless.csproj -- --config ..\config.example.json --dry-run --burst 50 --burst-trigger q --burst-phase down --drain-timeout 5 --assert-drained --assert-input-events 100 --assert-clean
```

Run built-in lifecycle scenarios:

```powershell
dotnet run --project .\tools\BAKeySmith.Headless\BAKeySmith.Headless.csproj -- --scenario all --burst 50 --drain-timeout 5
dotnet run --project .\tools\BAKeySmith.Headless\BAKeySmith.Headless.csproj -- --scenario burst-drain --burst 50 --drain-timeout 5 --snapshot
dotnet run --project .\tools\BAKeySmith.Headless\BAKeySmith.Headless.csproj -- --scenario reload-during-burst --burst 50 --drain-timeout 5 --snapshot
dotnet run --project .\tools\BAKeySmith.Headless\BAKeySmith.Headless.csproj -- --scenario stop-during-long-macro --drain-timeout 5 --snapshot
dotnet run --project .\tools\BAKeySmith.Headless\BAKeySmith.Headless.csproj -- --scenario reload-during-long-macro --drain-timeout 5 --snapshot
dotnet run --project .\tools\BAKeySmith.Headless\BAKeySmith.Headless.csproj -- --scenario foreground-gate-during-burst --burst 50 --drain-timeout 5 --snapshot
```

Live mode is explicit because it installs global hooks and sends real input:

```powershell
dotnet run --project .\tools\BAKeySmith.Headless\BAKeySmith.Headless.csproj -- --config ..\config.example.json --live --duration 10
```

Run WPF smoke:

```powershell
dotnet run --project .\src\BAKeySmith.App\BAKeySmith.App.csproj -- --smoke
```
