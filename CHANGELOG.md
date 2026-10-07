# Changelog

All notable changes to uContract.NET will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

**Migrating from 2.x**: `Old<T>()` now copies every field, private ones included, and keeps runtime
types, shared references and cycles. Code that relied on the lossy JSON copy changes behaviour: a
`[JsonIgnore]` or other `System.Text.Json` attribute no longer limits the copy (capture only the
state you need, for example `Old(() => _balance)`), an `object` member holds the boxed value instead
of a `JsonElement`, and a lazily evaluated sequence is cloned as an iterator instead of being
materialized (capture it with `.ToList()`). `EnsureAssignable<T>()` now reports some differences it
missed and no longer reports some it raised wrongly; each case is listed under Changed. Code that
catches the `InvalidOperationException` that `Old<T>()` threw for a type it could not serialize, or
the `JsonException` for a deep graph, can drop that handler: `Old<T>()` throws neither. For trimmed
and Native AOT applications, see [Trimming and Native AOT](README.md#trimming-and-native-aot) in the
README.

### Changed

- **BREAKING**: `Old<T>()` copies by reflection instead of JSON. Each object is a bitwise clone, so
  the copy keeps private state, `readonly` fields, base-class fields and the runtime type of every
  node, the top level included. A shared or cyclic reference is copied once and the cycle is kept.
  No constructor, property accessor, `Equals`, `GetHashCode` or serialization callback runs. The copy
  is a read-only snapshot: delegates are shared, so an event raised on the copy reaches the
  original's subscribers. Instances of resource types are shared with the original, not copied: every
  `Stream`, `Task`, `Lazy<T>`, `CancellationTokenSource`, handle, timer and `HttpClient`, and the
  default comparers of the base class library. State inside them is not snapshotted. Everything
  reachable is copied, so prefer `Old(() => _balance)` to `Old(() => this)`. (ADR-0022)
- **BREAKING**: Callers that forward their own generic parameter to `Old<T>()` get warning `IL2091`.
  This applies to a method's type parameter and to a generic class's type parameter. Only
  `[RequiresUnreferencedCode]` on the caller, or the same
  `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)]`
  on its type parameter, silences it. Suppressing `IL2026` does not. Warning `IL3050` is no longer
  raised for `Old<T>()`. (ADR-0022)
- **BREAKING**: Under Native AOT, a field that is hidden from reflection keeps its bitwise value, so
  the object it refers to is shared with the original. A change to a base-class auto-property (for
  example a `List<Line>`) or to the storage of a collection whose fields are hidden then passes
  silently. Preserve the type with `[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(X))]`.
  (ADR-0022)
- **BREAKING**: `Old<T>()` no longer throws `InvalidOperationException` for a type that "cannot be
  serialized" or because reflection-based JSON serialization is disabled. It no longer throws
  `JsonException` for a graph deeper than 64 levels, and it no longer translates a
  `NotSupportedException` from a getter or converter. It throws nothing of its own while copying;
  an exception from the supplier or from the runtime propagates. (ADR-0022)
- **BREAKING**: `EnsureAssignable<T>()` reports more. Distinct instances of shared types are now
  different. Members whose runtime types differ are unequal; the runtime type decides, not the
  declared type. Base and derived instances are unequal. A `string` and a `char[]` are unequal.
  A user-defined struct that implements `IEnumerable`, held in an interface-typed or `object`
  member, is compared by its fields, so state besides its elements is reported. A change outside the
  window of a `Memory<T>` or `ReadOnlyMemory<T>` is reported. A dictionary value whose `Equals`
  ignores the changed content is reported, because values are compared by their members. Members
  declared on an interface are walked deeply, and their getters run. A getter that throws on a class
  reached through a struct field propagates. When the two sides of a member have different runtime
  types, a violation is reported where 2.0.0 threw `ArgumentException` or `TargetException`. (ADR-0022)
- **BREAKING**: `EnsureAssignable<T>()` reports less. A back-reference to the compared object (for
  example `Order.Lines[i].Order`) is no longer a difference of the member that holds it; 2.0.0
  overflowed the stack or reported it. A cycle whose shape changed but whose values unroll
  identically compares equal. Delegate targets and closure state are not compared. State inside
  shared instances (`Lazy<T>`, `Task`, `CancellationTokenSource`, streams) is not compared. Structs,
  tuples and dictionaries whose content is equal but whose references differ are equal. (ADR-0022)
- **BREAKING**: Under Native AOT, `EnsureAssignable<T>()` asks `Equals` first for a nested class with
  no members visible to reflection. A class whose `Equals` returns `true` is equal. Otherwise it
  throws `InvalidOperationException`, as before; the message now says that `Equals` reports the values
  unequal. Two cases differ from 2.0.0. First, a changed hidden record in an interface-typed member,
  a struct whose `Equals` is false that holds a hidden class with reference `Equals`, and a
  dictionary value of that kind report "cannot compare" where 2.0.0 reported a violation. Second, a
  nested class whose `Equals` ignores state (for example entity equality by ID) is compared by that
  `Equals`, so a change to it passes silently, where a JIT build reports a violation and 2.0.0
  reported "cannot compare". Preserve the type with `DynamicDependency` to compare it member by
  member. (ADR-0022)
- Known limitation, unchanged from 2.0.0: for fixed-size buffers and `[InlineArray]` structs, the
  elements after the first are invisible to reflection and to `ValueType.Equals`. A change there with
  an equal first element passes silently. (ADR-0022)

### Fixed

- `Old<T>()` copied only public fields and lost private state, runtime types, `object` members and
  the shape of the graph. The documented `User.ChangeEmail` example, which pairs `Old(() => this)`
  with `EnsureAssignable<T>()`, now works (#45; ADR-0022).
- `EnsureAssignable<T>()` no longer overflows the stack, which ends the process, on cyclic object
  graphs, on chains of 10 000 nodes or more, on a property that returns `this`, on multicast
  delegates and on pointer fields (ADR-0022).
- `EnsureAssignable<T>()` no longer reports false violations for unchanged dictionaries, structs
  that hold a reference and interface-typed members (ADR-0022).

### Removed

- `[RequiresDynamicCode]` on `Old<T>()` (ADR-0022).
- The need to set `JsonSerializerIsReflectionEnabledByDefault` to `true` in trimmed and Native AOT
  applications. The library no longer uses `System.Text.Json` (ADR-0022).

---

## [2.0.0] - 2026-10-05

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

[Unreleased]: https://github.com/cwouyang/uContract.NET/compare/v2.0.0...HEAD
[2.0.0]: https://github.com/cwouyang/uContract.NET/compare/v1.0.0...v2.0.0
[1.0.0]: https://github.com/cwouyang/uContract.NET/releases/tag/v1.0.0
