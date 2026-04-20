# Migration Statement

本项目从当前阶段起明确主次关系，避免 Python 与 C# 双主线继续拖长。

## Decision

- Python 版本：参考实现 / beta 行为基线 / 回归规格来源。
- `next/` C# baseline：当前主线。
- Rust runtime core：未来可替换 runtime core 的终局储备，但 Phase 1 exit 前不进入 FFI / 跨语言集成实现。

## What This Means

Python 侧继续保留的价值：

- 已验证的用户产品价值来源。
- beta 行为对照样本。
- README / 使用说明中的用户语义参考。
- 配置迁移兼容性参考。

Python 侧不再承担：

- 未来 2 年 runtime 主线。
- 新 stop / reload / suppress / owner model 的主实现。
- Phase 1 live/runtime/soak/acceptance 的判定标准。

`next/` C# baseline 承担：

- 当前产品级 runtime 主线。
- runtime contract、acceptance、benchmark、diagnostics、soak 的统一口径。
- 后续 GUI / packaging / release 的工程基线。
- 未来 Rust core 原型的对照目标。

## Repository Scope To Version

Phase 1 收口时应正式纳入版本控制：

- `next/src/BAKeySmith.Core/**`
- `next/src/BAKeySmith.App/**`
- `next/tools/BAKeySmith.Acceptance/**`
- `next/tools/BAKeySmith.Headless/**`
- `next/tools/BAKeySmith.Core.Benchmarks/**`
- `next/tests/BAKeySmith.Core.SmokeTests/**`
- `next/docs/**`
- `next/README.md`
- `next/BAKeySmith.Next.slnx`
- root `docs/rebuild_assessment.md`
- root `README.md` 中关于主线迁移的声明
- root `.gitignore` 中的 .NET build artifact 忽略规则

继续保留但作为参考 / legacy baseline：

- `main.py`
- `gui.py`
- `mapper.py`
- `utils.py`
- `script_compiler.py`
- `diagnostics.py`
- `runtime_probe.py`
- `runtime_benchmark.py`
- `config.example.json`
- `requirements.txt`
- `BAKeySmith.spec`
- `BAKeySmith_使用说明.txt`

不应纳入版本控制：

- `config.json`
- `build/`
- `dist/`
- `venv/`
- `__pycache__/`
- `next/**/bin/`
- `next/**/obj/`
- 本机 IDE / OS 临时文件

## Documentation Authority

面向当前已发布 Python beta 用户：

- root `README.md`
- `BAKeySmith_使用说明.txt`

面向 Phase 1 以后工程主线：

- `next/README.md`
- `next/docs/runtime-contract.md`
- `next/docs/acceptance-schema.md`
- `next/docs/phase-1-live-runtime-plan.md`
- `next/docs/phase-1-exit.md`
- `next/docs/manual-bluearchive-validation.md`
- `next/docs/migration-statement.md`

如果 root 文档和 `next/` 文档在 runtime 设计、acceptance、stop/reload/suppress 语义上冲突，以 `next/` 文档为准。

## Exit-Gated Rust Rule

Rust 不靠直觉切换，也不因语言偏好切换。只有当 C# baseline 已完成 Phase 1 exit，并且 Rust core 在同一 runtime contract、同一 acceptance JSON schema、同一 benchmark / soak 压测下证明有可测收益时，才进入实现与替换讨论。
