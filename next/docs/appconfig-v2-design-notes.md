# AppConfigV2 Design Notes

This document records future AppConfigV2 direction. It is planning-only and
does not change AppConfigV1.

## Position

AppConfigV1 remains frozen for the current C# `next` GUI RC0 candidate.

AppConfigV2 is the future professional configuration model and is not limited
to AppConfigV1's trigger / mode / script strings.

Do not hide v2 features inside AppConfigV1 extension fields.

## Mapping V2

Mapping v2 should be structured.

Likely concepts:

- mapping id;
- enabled state;
- trigger InputSpec / TriggerSpec;
- activation plan;
- conflict status;
- coordinate profile binding where applicable;
- execution policy;
- timing override.

Simple mappings should also compile to `ActivationPlan` rather than bypassing
the action model.

Macro DSL v2 mappings should save user source text and compile to an
ActivationPlan / Macro IR for runtime.

## Settings Precedence

Precedence:

1. explicit action parameter;
2. mapping override;
3. global default.

Examples:

- tap duration;
- repeat interval;
- coordinate execution policy;
- real cursor fallback permission.

## Coordinate Config

AppConfigV2 needs explicit fields for:

- coordinate profile;
- coordinate logical basis;
- profile mismatch policy;
- real cursor fallback policy;
- coordinate record hotkey.

## Plugin Metadata

No plugin metadata belongs in AppConfigV1.

Future plugin metadata requires an explicit plugin config contract and trust
model.

