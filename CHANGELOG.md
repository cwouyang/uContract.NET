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
missed and no longer reports some it raised wrongly; each case is listed under Changed. In a
class that derives from a type that `Old<T>()` shares, replace the pair on `this` by
`Contract.Ensure` checks of the state members. Code that catches the
`InvalidOperationException` that `Old<T>()` threw for a type it could not serialize, or the `JsonException` for a deep graph, can drop that handler: `Old<T>()` throws neither. A `null`
test or an `ArgumentNullException` handler around the `Old` + `EnsureAssignable` pair is no longer
needed for the case that postconditions are off. Removing a `null` test changes the result for a
member that can be `null`: going from `null` to a value, or back, is now a violation that the
assignable patterns cannot excuse, so keep the test where that change is allowed. Code that caught
`ArgumentNullException` or `ArgumentException` from `EnsureAssignable<T>()` for a `null` value gets
`PostconditionViolationException` for one `null` and nothing for two. For a `string`, a delegate or
a nullable value type as `T`, a pattern no longer allows a change, so do not call the method for a
value that may change; a test that matched "Fields were modified" for such a `T` sees another
message; a `string` is compared by ordinal `Equals`, so a change of letter case is a violation; a
delegate's target is no longer compared. An `Old` result taken while another contract check is
running gave `ArgumentNullException` in 2.x; it now gives a violation that did not happen, or none.
`EnsureImmutableCollection<T>()` is no longer a `null` guard with postconditions off. For trimmed
and Native AOT applications, see [Trimming and Native AOT](README.md#trimming-and-native-aot) in the
README.

### Changed

- **BREAKING**: `Old<T>()` copies by reflection instead of JSON. Each object is a bitwise clone, so
  the copy keeps private state, `readonly` fields, base-class fields and the runtime type of every
  node, the top level included. A shared or cyclic reference is copied once and the cycle is kept.
  No constructor, property accessor, `Equals`, `GetHashCode` or serialization callback runs. The copy
  is a read-only snapshot: delegates are shared, so an event raised on the copy reaches the
  original's subscribers. Instances of resource types are shared with the original, not copied: every
  `Stream`, `Task`, `Lazy<T>`, `CancellationTokenSource`, handle, timer and `HttpClient`, every
  frozen collection (`System.Collections.Frozen`), every `Regex`, `AsyncLocal<T>` and
  `System.Threading.Lock`, and the comparers of the base class library. The full list is in
  [Types that `Old<T>()` shares](docs/examples/API_REFERENCE.md#types-that-oldt-shares). A class
  that derives from a shared type whose state can change (a `Stream`, a `Task`, a `Component`, …)
  is shared too, so `Old(() => this)` in such a class returns `this`. A copy of a `Regex`,
  `AsyncLocal<T>` or `Lock` would differ from an unchanged original (a `Regex` caches a runner when it
  is used, an `AsyncLocal<T>` value is keyed by the instance, a `Lock` changes state under
  contention), so `EnsureAssignable<T>()` would report a false violation. State inside shared
  instances is not snapshotted. Everything reachable is copied, so prefer `Old(() => _balance)` to
  `Old(() => this)`. (ADR-0022)
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
- **BREAKING**: `EnsureAssignable<T>()` reports more. For members and elements that the comparison
  reaches: distinct instances of shared types are now different, frozen collections
  (`System.Collections.Frozen`) included: two equal frozen sets that are different instances are a
  violation. Other shared instances (see `Old<T>()` above) are compared by reference; a `string` is
  compared with `Equals` and a delegate by its methods. Members whose runtime types differ are
  unequal; the runtime type decides, not the declared type. Base and derived instances are unequal.
  A `string` and a `char[]` are unequal. A user-defined struct that implements `IEnumerable`, held
  in an interface-typed or `object` member, is compared by its fields, so state besides its elements
  is reported. A change outside the window of a `Memory<T>` or `ReadOnlyMemory<T>` is reported. A
  dictionary value whose `Equals` ignores the changed content is reported, because values are
  compared by their members. A member whose declared type is an interface is walked into the members
  of its runtime type, and their getters run. A getter that throws on a class reached through a
  struct field propagates. When the two sides of a member have different runtime types, a violation
  is reported where 2.0.0 threw `ArgumentException` or `TargetException`. (ADR-0022)
- **BREAKING**: `EnsureAssignable<T>()` reports less. A back-reference to the compared object (for
  example `Order.Lines[i].Order`) is no longer a difference of the member that holds it; 2.0.0
  overflowed the stack or reported it. A cycle whose shape changed but whose values unroll
  identically compares equal. Delegate targets and closure state are not compared. State inside
  shared instances that a member or element holds (`Lazy<T>`, `Task`, `CancellationTokenSource`,
  streams, `Regex`) is not compared; the `Value` of a shared `AsyncLocal<T>` is not compared either,
  so a value first set after `Old` passes. Structs, tuples and dictionaries whose content is equal
  but whose references differ are equal. (ADR-0022)
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
- **BREAKING**: `EnsureAssignable<T>()` no longer throws `ArgumentNullException` for a `null`
  `actual` or `expected`. This is an exception to "parameter validation always runs". With
  postconditions off, or in a call made while another contract check is running, it returns without
  comparing. `Old<T>()` returns `default` in both cases, so the documented `Old` +
  `EnsureAssignable` pair threw there in 2.x for a reference type or a nullable value type; it now
  does nothing. When the method compares, `null` is a value: two `null`s are equal, one `null` is a
  `PostconditionViolationException` that the assignable patterns do not excuse and for which they
  are not examined. A `null` pattern array still throws in every call, and the exception names
  `assignableFieldPatterns` also when `actual` or `expected` is `null`. An `Old` result taken while
  another contract check is running is `default` and cannot be told from a real `null`: compared
  later, it gives a violation that did not happen, or misses one. (#47, #52; ADR-0012)
- **BREAKING**: `EnsureAssignable<T>()` compares the two values as a whole when `T` is a `string`, a
  delegate type or a nullable value type: a `string` with an ordinal `Equals`, a delegate by its
  methods, a nullable value type with `Equals` and then by its fields. Before, the members of `T`
  were compared. For a `string` those were its length and first character, so two different strings
  could pass. For a delegate they included its target, compared by its members: now targets and
  closure state are not compared, so the same method on another object passes, and every entry of a
  multicast delegate counts. A nullable value type with a value on both sides could not be compared.
  The assignable patterns do not apply to these types and are not examined. A nullable value type
  can be compared less strictly than the same type without `?` (`DateTime.Equals` ignores `Kind`).
  Under Native AOT the fields of its underlying type may not be preserved: for a nullable struct
  that holds a reference, the comparison can then report a difference for an unchanged value or,
  with an `Old` snapshot, miss a change inside the object it refers to. This holds only when `T`
  itself is such a type: `T` = `object` still compares nothing beyond `null`, a base class compares
  the members it declares or inherits, an interface only its own properties, and two different
  instances of a shared type other than `string` or a delegate are still compared member by member
  (#40), as is a type that is shared, and fixed (a frozen collection, a `Regex`), also for one
  instance on both sides. One instance of a shared type whose state can change, passed as `actual`
  and as `expected`, is not compared, whatever `T` is, `object` included: the method throws
  `InvalidOperationException`; the next entry gives the ways out. (#52; ADR-0022)
- **BREAKING**: `EnsureAssignable<T>()` throws `InvalidOperationException` when one instance of a
  shared type whose state can change is passed as `actual` and as `expected`. `Old<T>()` shares such
  an instance (a `Stream`, a `Task`, a `CancellationTokenSource`, a class derived from `Component`,
  …) instead of copying it, so the pair `EnsureAssignable(this, Contract.Old(() => this))` in a
  class derived from one has no earlier state to compare with: without this rule it would compare
  the object with itself. The same holds for a member of such a type passed as
  `EnsureAssignable(_member, Contract.Old(() => _member))`. Check each state member with
  `Contract.Ensure` and a value taken with `Old`; to check that a reference was not replaced, use
  `ReferenceEquals`. (#54; ADR-0022)
- **BREAKING**: `EnsureImmutableCollection<T>()` returns a `null` `collection` instead of throwing
  `ArgumentNullException` when it does not check it: with postconditions off, or in a call made
  while another contract check is running. This is a second exception to "parameter validation
  always runs". Code that relied on the method as a `null` guard must add its own. (#52; ADR-0012)
- Known limitation: a property that returns a new instance of its own type on every read (for
  example `DirectoryInfo.Root`) now makes the comparison grow without bound, where 2.0.0 overflowed
  the stack. Fields-only comparison ([#46](https://github.com/cwouyang/uContract.NET/issues/46))
  would remove it. (ADR-0022)
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
  that hold a reference, interface-typed members, and objects that hold a `Regex` (also after it is
  used), an `AsyncLocal<T>` or a `System.Threading.Lock` (ADR-0022).

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
