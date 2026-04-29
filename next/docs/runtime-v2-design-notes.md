# Runtime V2 Design Notes

This document records Runtime v2 direction. It is planning-only and does not
change current C# runtime behavior.

## MappingActivationSession

Runtime v2 executes a `MappingActivationSession`.

Lifecycle:

1. trigger down creates a session;
2. `on_down` runs once;
3. `while_held` runs until trigger release or cancellation;
4. trigger up stops `while_held`;
5. `on_up` runs only on normal trigger up;
6. stop / reload / foreground lost / emergency stop runs cleanup, not `on_up`.

## Cleanup Model

Runtime v2 must maintain an ownership ledger:

- held keyboard keys;
- held mouse buttons;
- coordinate contact / pointer actions where applicable;
- active repeating loops;
- runtime-owned timers.

Cleanup must be automatic and must not rely on user-authored `on_cancel`.

## Repeat Semantics

`while_held interval 0ms` must be allowed for turbo / no-wait needs.

Safety requirements:

- cancellable;
- emergency-stop capable;
- diagnostics-visible;
- bounded by runtime scheduling fairness;
- no unobservable busy loop.

## Same Mapping Reentry

Default same-mapping behavior should be `ignore_when_running` unless a future
explicit policy says otherwise.

## Foreground Lost

Default foreground-lost behavior:

- cancel active session;
- cleanup ownership;
- do not pause and resume by default.

Pause/resume requires explicit future design.

## Runtime V2 And Current Runtime

Current C# `next` remains the engineering baseline. Runtime v2 should be
designed against existing proven cleanup and foreground contracts, but it is not
implemented by editing current dispatch paths opportunistically.

