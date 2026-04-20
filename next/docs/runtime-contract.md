# Runtime Contract

BA KeySmith Next treats the runtime as a replaceable engine behind a stable contract.

The current implementation is `InProcessRuntimeCore` in C#. A future Rust core must implement equivalent behavior and pass the same tests and benchmarks before it can replace the C# runtime.

## Phase 1 Focus

先证明 C# baseline 在真实 Windows live/runtime 条件下足够硬，再决定是否值得让 Rust core 接管。

This phase freezes new GUI and macro-editor experience expansion unless it directly improves live/runtime observability. The active priority is live validation, soak testing, and Acceptance JSON contract hardening.

See [phase-1-live-runtime-plan.md](phase-1-live-runtime-plan.md) and [acceptance-schema.md](acceptance-schema.md).

## Boundary

GUI and app code should depend on `IRuntimeCore`, not a concrete runtime class.

The contract is intentionally split into small ports:

- `IRuntimeCore`: lifecycle, config load, reload, trigger handling, snapshots.
- `IInputBackend`: keyboard/mouse input injection, including batch input commands.
- `IForegroundGate`: target foreground decision.
- `IDiagnosticsSink`: runtime event stream.
- `MacroScriptCompiler`: DSL text to runtime instruction plan.
- `AppConfigV1`: stable app config schema, including legacy config migration defaults.
- `ITriggerSource`: keyboard/mouse trigger source abstraction.
- `TriggerPipeline`: async queue between hook callbacks and runtime dispatch.
- `RuntimeHost`: app composition boundary for config, runtime, trigger source, and pipeline.
- `BAKeySmith.App`: WPF shell consuming `RuntimeHost`.
- `RuntimeConfig`: target process, tap timing, and mapping definitions.
- `TriggerEvent`: normalized trigger input from keyboard or mouse hooks.
- `RuntimeSnapshot`: runtime state, worker count, foreground state, and pressed-key ownership.
- `TriggerPipelineSnapshot`: queued/handled/dropped/pending trigger counts for drain assertions.
- `RuntimeHostSnapshot`: app composition status, pipeline running state, runtime config, and runtime snapshot for GUI diagnostics.

## Required Invariants

- `DisableAsync`, `ReloadAsync`, `StopAsync`, and `DisposeAsync` must release all held keys.
- No delayed action may continue sending input after disable/stop/reload.
- Foreground gating must happen before dispatching a mapping.
- Foreground-gated trigger suppression must happen in the Windows hook callback before the original input reaches the foreground window.
- Tap uses the same ownership model as hold and macro press/release.
- Repeated trigger behavior must be deterministic. The current tap policy is a single serial queue per mapping.
- Diagnostics must expose lifecycle, trigger, queue, worker, and ownership events.
- Drained runtime means trigger pipeline pending count is zero, action pending count is zero, and action running count is zero.
- Stop-time trigger bursts must not be drained into new mapping output. Any trigger still pending when stop cancels the pipeline is counted as dropped, and stopped snapshots must report pending count zero.

## Trigger Suppression Contract

Default trigger behavior is gated suppression, not global swallowing.

An original physical input event is suppressed only when all conditions are true:

- the event was not injected by BA KeySmith itself;
- the runtime and trigger pipeline are enabled/running;
- the event hits a configured trigger;
- the foreground gate currently allows the target process/window.

If foreground is blocked, stopped, disabled, or the event does not hit a trigger, the original input must pass through to the current foreground application and no mapping/macro output may be emitted.

Keyboard and mouse triggers use the same default rule. If an explicit pass-through mode is added later, it must be an opt-in config field, never the default.

### Trigger Edges

- Keyboard key triggers are captured on `down`; the corresponding `up` closes the captured trigger session.
- Mouse button triggers, including `mouse_left`, `mouse_right`, `mouse_middle`, `mouse_x1`, and `mouse_x2`, are captured on button `down`; the corresponding button `up` closes the captured trigger session.
- Mouse wheel triggers are pulse events with no matching `up`. `mouse_wheel_up` and `mouse_wheel_down` may dispatch one trigger event when foreground is allowed and the wheel event hits a configured trigger; they do not create a long-lived captured session.
- Runtime actions dispatch from the trigger event. Tap/macro actions currently run on `down`; hold actions rely on the captured `up` reaching runtime so the mapped held key can be released.

### Captured Trigger Session

Once a keyboard or mouse button trigger `down` is captured and suppressed, the physical trigger enters a captured session until the matching `up`.

Captured session rules:

- The matching `up` must also be suppressed, even if foreground changes before release.
- The matching `up` must still be dispatched to runtime so hold mappings can release their mapped output.
- `reload`, `disable`, `stop`, and `close` must clear captured sessions and release any mapped held outputs.
- A foreground change during a captured session must not leak the trigger `up` into another foreground app.
- A foreground-blocked `down` must not create a captured session; its original input passes through and no runtime output is emitted.

### Self-Injected Input

All input injected by `WindowsInputBackend` must be marked with a BA KeySmith `dwExtraInfo` value.

Hook callbacks must treat marked events as self-injected output:

- do not suppress them;
- do not convert them into triggers;
- do not enqueue them into the trigger pipeline.

This applies to injected keyboard, mouse button, mouse wheel, and mouse movement output.

## Current Implementations

- `InProcessRuntimeCore`: C# in-process runtime implementation.
- `WindowsInputBackend`: Win32 `SendInput` keyboard/mouse backend. Keyboard events use scan codes for better game compatibility.
- `WindowsForegroundGate`: foreground window handle -> PID -> process name gate.
- `DryRunInputBackend`: deterministic backend for tests and benchmarks.
- `MacroExecutor`: executes DSL instructions through the same ownership and input backend path.
- `AppConfigSerializer`: parses legacy and v1 JSON config into `RuntimeConfig`.
- `WindowsHookTriggerSource`: low-level keyboard/mouse hook source running on a dedicated message-loop thread.
- `ManualTriggerSource`: deterministic trigger source for tests.
- `BAKeySmith.Acceptance`: stable JSON acceptance runner for the dry-run P0 scenario matrix.
- `dry-run-soak`: long-running acceptance scenario for resource, queue, worker, held-input, and stop-clean observation.
- `BAKeySmith.Headless`: minimal runner for validating composition without a GUI.
- `BAKeySmith.App`: GUI shell with dry-run start/stop, diagnostics, config loading, and simulated triggers.

Current DSL v1 compiler accepts:

- `press`
- `release`
- `tap`
- `wait` with decimal milliseconds
- `loop` / `end`
- `combo`
- `drag`
- `drag_rel`
- `setpos`
- `setpos_rel`

## Rust Decision Gate

Rust is not selected by taste. It becomes eligible only if a Rust core:

- implements this contract boundary;
- passes the same behavior contract tests;
- runs the same benchmark workloads;
- demonstrates measurable gains in latency, stop response, memory use, CPU use, or long-run stability;
- does not weaken single-app packaging or GUI maintainability.

If Rust only adds FFI and packaging complexity without measured runtime benefit, the C# runtime remains the mainline.

## Config V1

The app config schema is intentionally separate from `RuntimeConfig`. GUI and config files keep user-facing fields, while runtime receives a compiled plan.

Top-level fields:

- `version`: config schema version, currently `1`.
- `target_process`: foreground process target, default `BlueArchive.exe`.
- `hotkey`: GUI/runtime toggle hotkey, default `ctrl+shift+f12`.
- `tap_hold_ms`: unified tap hold duration, default `20`.
- `mappings`: user mappings.

Legacy Python beta configs without `version`, `target_process`, or `tap_hold_ms` are accepted with defaults.

## Host Composition

`RuntimeHost` owns the app-level lifecycle:

- validate and compile app config;
- load and enable runtime;
- start the trigger pipeline;
- stop the trigger pipeline before stopping runtime;
- reject invalid config instead of starting a partially broken runtime.
- expose `RuntimeHostSnapshot` for diagnostics without letting GUI depend on runtime internals;
- expose foreground probe through the same `IForegroundGate` used by runtime dispatch.

The headless runner is intentionally thin. It exists to validate the same composition that a future WPF GUI will use.

The acceptance runner is the preferred P0 regression gate for automated comparisons:

```powershell
dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario all --burst 50 --drain-timeout 5
dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario all --burst 50 --drain-timeout 5 --output acceptance-report.json
dotnet run --project .\tools\BAKeySmith.Acceptance\BAKeySmith.Acceptance.csproj -- --scenario dry-run-soak --soak-seconds 300 --soak-rate 20 --drain-timeout 10 --output acceptance-dry-run-soak.json
```

It writes a stable JSON report containing scenario name, pass/fail state, duration, error message, and scenario metrics. Human-readable progress is written separately.

Dry-run example:

```powershell
dotnet run --project .\tools\BAKeySmith.Headless\BAKeySmith.Headless.csproj -- --config ..\config.example.json --dry-run --simulate q:down --duration 1
```

Stop-cleanup assertion example:

```powershell
dotnet run --project .\tools\BAKeySmith.Headless\BAKeySmith.Headless.csproj -- --config ..\config.example.json --dry-run --simulate q:down --duration 1 --snapshot --assert-clean
```

`--assert-clean` fails if the stopped snapshot still has a running host, running pipeline, active workers, or held inputs.

High-frequency dry-run burst example:

```powershell
dotnet run --project .\tools\BAKeySmith.Headless\BAKeySmith.Headless.csproj -- --config ..\config.example.json --dry-run --burst 50 --burst-trigger q --burst-phase down --drain-timeout 5 --assert-drained --assert-input-events 100 --assert-clean
```

`--assert-drained` waits for both the trigger pipeline and runtime action queues to drain before stop.

Built-in lifecycle scenarios:

```powershell
dotnet run --project .\tools\BAKeySmith.Headless\BAKeySmith.Headless.csproj -- --scenario all --burst 50 --drain-timeout 5
dotnet run --project .\tools\BAKeySmith.Headless\BAKeySmith.Headless.csproj -- --scenario burst-drain --burst 50 --drain-timeout 5 --snapshot
dotnet run --project .\tools\BAKeySmith.Headless\BAKeySmith.Headless.csproj -- --scenario reload-during-burst --burst 50 --drain-timeout 5 --snapshot
dotnet run --project .\tools\BAKeySmith.Headless\BAKeySmith.Headless.csproj -- --scenario stop-during-long-macro --drain-timeout 5 --snapshot
dotnet run --project .\tools\BAKeySmith.Headless\BAKeySmith.Headless.csproj -- --scenario reload-during-long-macro --drain-timeout 5 --snapshot
dotnet run --project .\tools\BAKeySmith.Headless\BAKeySmith.Headless.csproj -- --scenario foreground-gate-during-burst --burst 50 --drain-timeout 5 --snapshot
```

- `all`: runs the full dry-run scenario matrix and fails if any scenario fails.
- `burst-drain`: emits a trigger burst, waits for trigger/action queues to drain, verifies exact input event count, and verifies stop-clean.
- `reload-during-burst`: emits old trigger burst, reloads to a new trigger, verifies old trigger is inactive, new trigger works, and stop is clean.
- `stop-during-long-macro`: starts a macro that presses a key then waits, stops during the wait, verifies the held key is released exactly once and no post-stop input is emitted.
- `reload-during-long-macro`: reloads while a macro owner is holding a key, verifies the owner is released, old trigger is inactive, new trigger works, and stop is clean.
- `foreground-gate-during-burst`: blocks a trigger burst through the foreground gate, verifies no input is emitted, then allows foreground and verifies the trigger works.

Live mode exists, but should only be used deliberately because it installs global hooks and uses `SendInput`:

```powershell
dotnet run --project .\tools\BAKeySmith.Headless\BAKeySmith.Headless.csproj -- --config ..\config.example.json --live --duration 10
```

## WPF Shell

The WPF app is intentionally a thin shell over `RuntimeHost`.

Current GUI and macro-editor experience work is frozen for Phase 1 unless it directly improves live/runtime observability.

Current GUI scope:

- load JSON config;
- start/stop runtime;
- dry-run mode by default;
- optional live mode warning;
- diagnostics stream viewer;
- runtime snapshot panel;
- held key / owner count and details;
- trigger pipeline queued/handled/dropped/pending details;
- action queue pending/running details;
- foreground probe button using the shared foreground gate;
- dry-run simulated trigger input;
- runtime mapping/target/input counters;
- mapping list editor;
- add/update/remove simple mappings;
- add/update/remove macro mappings;
- macro script validation through the shared DSL compiler;
- reusable `MacroEditorControl` instead of editor behavior embedded in `MainWindow`;
- macro line numbers and cursor status;
- structured macro diagnostics with line numbers from `MacroScriptCompiler`;
- shared `MacroScriptLanguageCatalog` and `MacroScriptLanguageService` for script-safe key names, token classification, and editor hints;
- always-visible completion suggestions from the shared DSL catalog;
- Tab/double-click completion insertion;
- context-aware completions for commands, wait durations, loop counts, key names, mouse buttons, coordinates, and flags;
- completion key names are script-safe single tokens such as `caps_lock` and `arrow_left`, not space-containing display names;
- save config through schema v1;
- running reload through `RuntimeHost.ReloadAsync`;
- explicit `target_process`, `hotkey`, and `tap_hold_ms` fields.

Smoke test:

```powershell
dotnet run --project .\src\BAKeySmith.App\BAKeySmith.App.csproj -- --smoke
```

The smoke mode opens the WPF window and closes it automatically.
