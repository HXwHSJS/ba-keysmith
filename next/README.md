# BA KeySmith Next

This directory contains the C#/.NET rewrite track for BA KeySmith.

The Python beta is now the reference implementation, beta behavior baseline, and
regression-spec source. The C# baseline in `next/` is the current engineering
mainline. This is not a line-by-line Python port: the runtime is rebuilt around a
stable contract that a future Rust core can implement and compete against under the
same tests and benchmarks.

Phase 1 focus:

> 先证明 C# baseline 在真实 Windows live/runtime 条件下足够硬，再决定是否值得让 Rust core 接管。

Current status: Phase 1 exit criteria are now met on the C# baseline, including same-elevation real-target Blue Archive manual validation.

New GUI and macro-editor experience work is frozen for this phase unless it directly
improves live/runtime observability. The mainline is now live validation, soak tests,
and acceptance contract hardening.

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
- reusable WPF macro editor control with line numbers, cursor status, validation, and Tab/double-click completion
- shared DSL completion provider and structured compiler diagnostics
- shared macro language catalog/service for script-safe key names, token classification, and editor hints
- hard runtime session stop cleanup
- dry-run input backend for deterministic tests and benchmarks

See [runtime-contract.md](docs/runtime-contract.md) for the C# first / Rust reserve strategy.
See [phase-1-live-runtime-plan.md](docs/phase-1-live-runtime-plan.md) for the current live/runtime focus.
See [phase-1-exit.md](docs/phase-1-exit.md) for the Phase 1 exit gate and current status.
See [acceptance-schema.md](docs/acceptance-schema.md) for the stable Acceptance JSON contract.
See [manual-bluearchive-validation.md](docs/manual-bluearchive-validation.md) for the required real-target validation checklist.
See [migration-statement.md](docs/migration-statement.md) for the Python / C# mainline decision.

Real-target Blue Archive validation is gated by runner integrity: run the acceptance/headless/live runner as Administrator, record target/runner elevation in the report, and use a physical keyboard trigger for the first manual pass instead of generating the trigger via `SendInput`.

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
dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario trigger-suppress --allow-live-input --drain-timeout 5
```

Run live soak:

```powershell
dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario live-soak --allow-live-input --soak-seconds 60 --soak-rate 20 --drain-timeout 10 --output acceptance-live-soak.json
```

Run real-target manual validation from an elevated terminal:

```powershell
dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario bluearchive-manual --allow-live-input --target-process BlueArchive.exe --manual-trigger f8 --manual-timeout 15 --manual-confirm yes
```

Stable example reports:

```text
docs\examples\acceptance-dry-run-soak-10min.json
docs\examples\acceptance-live-soak-10min.json
docs\examples\acceptance-short-matrix-latest.json
docs\examples\acceptance-lifecycle-stress-latest.json
docs\examples\acceptance-live-safe-latest.json
docs\examples\acceptance-trigger-suppress-latest.json
docs\examples\acceptance-bluearchive-manual-latest.json
```

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
