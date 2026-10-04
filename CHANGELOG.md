# Changelog

All notable changes to uContract.NET will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

This is a major release (2.0.0): the default for contract evaluation changes.

**Migrating from 1.0.0**: contract violations that the 1.0.0 package silently ignored now throw
(`PreconditionViolationException` and its siblings), in Release builds as well as Debug. Set
`DBC=off` to keep contracts disabled as the 1.0.0 package had them (the reported source becomes
`DBC`). If you already set `DBC=on`, nothing changes for you. If you enabled a single contract type
with only a per-type flag (for example `DBC_PRE=on`), add `DBC=off` to keep the other types
disabled. Use per-type flags to keep cheap contracts on and expensive ones off, for example
`DBC_POST=off`. For trimmed and Native AOT applications, see
[Trimming and Native AOT](README.md#trimming-and-native-aot) in the README. Three further changes
affect code that matches on exceptions: code that catches `ArgumentException` from
`EnsureAssignable<T>()` for a property with no get method must catch `InvalidOperationException`;
code that catches `InvalidOperationException` around `Old<T>()` to handle its own supplier's
`NotSupportedException` must catch that exception itself; and code that matches the text of an
`EnsureAssignable<T>()` violation must expect one `Postcondition violated:` prefix in `Message`
and none in `Description` (see Changed).

### Changed

- **BREAKING**: Contracts are enabled by default in every build configuration. With no `DBC*`
  variable set, 1.0.0 packages evaluated nothing; 2.0.0 evaluates preconditions, postconditions,
  invariants and checks. `Old<T>()` therefore runs by default: it deep-copies through JSON
  serialization and throws `InvalidOperationException` for types that cannot be serialized.
  `Old<T>()` and `EnsureAssignable<T>()` have limited support in trimmed and Native AOT
  applications; see [Trimming and Native AOT](README.md#trimming-and-native-aot) and
  [ADR-0021](docs/adr/0021-postconditions-under-trimming-and-aot.md). (ADR-0020)
- **BREAKING**: The source label for a defaulted setting is `default` instead of `default: Debug`
  or `default: Release`. It appears in `ContractConfiguration.*Source`, the `DBC_DOC` output and
  the `ConfigurationLoaded` event.
- **BREAKING**: `EnsureAssignable<T>()` fails with `InvalidOperationException` instead of
  `ArgumentException` when a compared type has a property with no get method (a write-only
  property, including one inherited from a base class; also a property whose getter is not visible
  through the compared type — a private getter declared on a base class, or a virtual property
  whose derived class overrides only the setter).
- **BREAKING**: An exception thrown while the supplier passed to `Old<T>()` runs now propagates
  unchanged. In 1.0.0 a `NotSupportedException` (or a derived type such as
  `PlatformNotSupportedException`) thrown by the supplier was wrapped in an
  `InvalidOperationException` saying the type cannot be serialized. One thrown by a property
  getter, a converter or a lazily evaluated sequence during the copy is still reported that way.
  (ADR-0021)
- **BREAKING**: The `EnsureAssignable<T>()` violation message has one `Postcondition violated:`
  prefix instead of two, and the exception's `Description` no longer starts with it. Before:
  `Postcondition violated: Postcondition violated: Fields were modified …`; after:
  `Postcondition violated: Fields were modified …`.
- Callers that forward their own generic parameter to `EnsureAssignable<T>` and have trim analysis
  enabled get warning `IL2091` until they add
  `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)]`
  to their own type parameter.

### Fixed

- Package consumers no longer get contracts silently disabled in their Debug builds. The default
  was decided by `#if DEBUG` inside the library, and the package is built in Release (#32).
- `EnsureAssignable<T>()` no longer passes silently under Native AOT when a compared type exposes
  no members to reflection. The top-level type is now preserved, and a nested type that is not
  throws `InvalidOperationException` with instructions (#36).
- `Old<T>()` explains how to proceed when reflection-based JSON serialization is disabled, instead
  of surfacing System.Text.Json's message (#36).

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

[Unreleased]: https://github.com/cwouyang/uContract.NET/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/cwouyang/uContract.NET/releases/tag/v1.0.0
