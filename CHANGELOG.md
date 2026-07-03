# Changelog

All notable changes to uContract.NET will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.0.0] - 2026-07-04

Initial release. Direct port of [Java uContract 2.0.1](https://gitlab.com/TeddyChen/ucontract/)
(commit `cb1e03f`) to .NET 8+ with idiomatic improvements.

### Added

- 17 public API methods across 4 categories:
  - Preconditions: `Require`, `RequireNotNull`, `RequireNotEmpty`
  - Postconditions: `Ensure`, `EnsureNotNull`, `EnsureResult`, `EnsureImmutableCollection`, `EnsureAssignable`
  - Invariants: `Invariant`, `InvariantNotNull`
  - Helpers: `Check`, `Ignore`, `Old`, `Imply`, `IfAndOnlyIf`, `FollowsFrom`, `CheckUnsupportedOperation`
- Optional descriptions via `[CallerArgumentExpression]` on the four condition-based methods
  (`Require`, `Ensure`, `Invariant`, `Check`) — the compiler captures the condition's source text
  when the description is omitted (ADR-0018)
- Runtime configuration via environment variables (`DBC`, `DBC_PRE`, `DBC_POST`, `DBC_INV`, `DBC_CHECK`)
- Diagnostic logging via `EventSource` (ADR-0014) — `DBC_DOC=on` prints contract configuration to stderr
- Configuration source tracking — each setting reports where it came from (`DBC_PRE`, `DBC`, `default: Debug`, etc.)
- Exception hierarchy rooted at `ContractViolationException`
- Source Link with embedded PDB — consumers can step into library source while debugging (ADR-0017)
- Trim and Native AOT compatibility annotations
- Public API baseline tracking via `Microsoft.CodeAnalysis.PublicApiAnalyzers` (ADR-0019)
- Documentation: API reference, usage examples, DDD integration guide, 19 ADRs
- Community health files: `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md` (Contributor Covenant 2.1),
  `SECURITY.md`, issue and PR templates
- Tooling and CI: CSharpier 1.2.6 as canonical formatter, Roslynator + Meziantou analyzers,
  GitHub Actions build/test on Ubuntu + Windows with a `dotnet csharpier check` gate,
  Dependabot (NuGet + GitHub Actions), release-triggered NuGet publish workflow

### Changed (compared to Java uContract 2.0.1)

- Method naming from camelCase to PascalCase (`require()` → `Require()`)
- `reject()` renamed to `Ignore()` for semantic clarity
- `ClassInvariantViolationException` renamed to `InvariantViolationException`
- Exception messages unified to `"{Type} violated: {description}"` format
  (Java uses the raw description text with no prefix)
- Configuration encapsulated in `ContractConfiguration` class (Java used public static fields)
- Thread safety via `AsyncLocal` instead of Java's `ThreadLocal`
- Parameter validation always executed, even when DBC is disabled
- Full nullable reference type annotations (compiler-enforced, not annotation-only)
- `Old<T>()` exposes a single overload — .NET's reified generics make Java's
  `TypeReference` overload unnecessary (ADR-0016)

---

[1.0.0]: https://github.com/cwouyang/uContract.NET/releases/tag/v1.0.0
