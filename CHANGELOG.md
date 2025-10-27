# Changelog

All notable changes to uContract.NET will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

---

## [1.0.0-alpha.1] - 2025-10-27

Initial alpha release. Direct port of [Java uContract 2.0.1](https://gitlab.com/TeddyChen/ucontract/)
(commit `cb1e03f`) to .NET 8+ with idiomatic improvements.

### Added
- 16 public API methods: preconditions, postconditions, invariants, and helpers
- Runtime configuration via environment variables (`DBC`, `DBC_PRE`, `DBC_POST`, `DBC_INV`, `DBC_CHECK`)
- Exception hierarchy rooted at `ContractViolationException`
- 216 tests with 100% pass rate
- Documentation: API reference, usage examples, DDD integration guide, 12 ADRs

### Changed
- Method naming from camelCase to PascalCase (`require()` → `Require()`)
- `reject()` renamed to `Ignore()` for semantic clarity
- Exception messages unified to `"{Type} violated: {description}"` format
- Configuration encapsulated in `ContractConfiguration` class (Java used public static fields)
- Thread safety via `AsyncLocal` instead of Java's `ThreadLocal`
- Parameter validation always executed, even when DBC is disabled
- Full nullable reference type annotations (compiler-enforced, not annotation-only)

---

[Unreleased]: https://github.com/cwouyang/uContract.NET/compare/v1.0.0-alpha.1...HEAD
[1.0.0-alpha.1]: https://github.com/cwouyang/uContract.NET/releases/tag/v1.0.0-alpha.1
