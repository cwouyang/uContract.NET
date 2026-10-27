# ADR-0021: Postcondition Helpers under Trimming and Native AOT

## Status

**Accepted**

- **Date**: 2026-10-04
- **Deciders**: Project maintainer
- **Status Date**: 2026-10-04
- **Amended**: 2026-10-04 — an exception thrown by the `Old<T>()` supplier now propagates unchanged, which supersedes one Known Limitation; three statements are qualified; see the Amendment under Implementation Notes
- **Amended**: 2026-10-07 — the `Old<T>()` decisions are superseded by [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md), and the open statements on value types, boxed values and framework types are settled there; see the second Amendment under Implementation Notes
- **Amended**: 2026-10-09 — the "no members visible" check for `T` does not run when `actual` or `expected` is `null`, or when `T` is a `string`, a delegate type or a nullable value type (#52); see the third Amendment under Implementation Notes
- **Amended**: 2026-10-10 — the "no members visible" check for `T` also does not run when one instance of a shared type whose state can change is passed as `actual` and as `expected` (#54, rule S of [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md)); see the fourth Amendment under Implementation Notes
- **Amended**: 2026-10-10 — the "no members visible" check for `T` also does not run when `T` is a `FrozenSet<T>` or a `FrozenDictionary<TKey, TValue>`, and the rule can fire for an element of two such values that a member or element holds (#49, [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md)); see the fifth Amendment under Implementation Notes
- **Amended**: 2026-10-11 — the "no members visible" check for `T` also does not run when `T` is an array type or `System.Array`; below such a `T` the path in the "cannot compare" message starts with the parameter name `actual`; and the rule can fire below an array whose element type is declared in `System.Collections.Frozen` (#57, [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md)); see the sixth Amendment under Implementation Notes
- **Amended**: 2026-10-12 — the library has one more `IL2070` suppression, on the private method that reads the interfaces of the runtime type for `EnsureImmutableCollection<T>()`; the smoke program gains checks 85 to 91 for that method, measured under Native AOT and in a trimmed publish without Native AOT; CI repeats the Native AOT publish only (#61, [ADR-0012](0012-dotnet-improvements-over-java.md)); see the seventh Amendment under Implementation Notes
- **Amended**: 2026-10-26 — `EnsureAssignable<T>()` reads the two property shapes with a getter that is not visible through the compared type through the getter that exists, and compares the private fields declared on base classes that are not part of .NET, so the Known Limitation on the two property shapes no longer holds, and the one on base-class private fields no longer holds under the JIT (under Native AOT and trimming it still holds unless the base class is preserved, and for framework base classes it still holds); the "no members visible" rule is decided before those fields are added; four more trimming suppressions; smoke checks 93 to 100 (#40, [ADR-0023](0023-comparison-of-base-class-members.md)); see the eighth Amendment under Implementation Notes
- **Amended**: 2026-10-26 — smoke checks 101 to 107 for two arrays of one primitive or enum element type, which `EnsureAssignable<T>()` now compares without boxing an element; the annotations and suppressions are unchanged (#64, [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md)); see the ninth Amendment under Implementation Notes

---

## Context

### Problem Statement

[Issue #36](https://github.com/cwouyang/uContract.NET/issues/36): [ADR-0020](0020-contracts-enabled-by-default.md) enabled contracts by default, so `Old<T>()` and `EnsureAssignable<T>()` now run in every application that has not set `DBC_POST=off`. Both rely on reflection. ADR-0020 recorded that nobody had run them in a trimmed or Native AOT published application with contracts enabled. This ADR records what was then measured, what was decided, and what the implementation turned up.

### Relevant Context

Measured before the change: `net8.0` console applications on `win-x64`, SDK 8.0.204, no `DBC*` variable set, against the library as it stood after ADR-0020.

| Call | Ordinary build | `PublishTrimmed` | `PublishAot` |
|---|---|---|---|
| `Old(() => x)` for `int`, `string`, `List<string>`, a user class | deep copy | `InvalidOperationException` (System.Text.Json's own message) | the same |
| `Old(…)` with `JsonSerializerIsReflectionEnabledByDefault=true` | — | deep copy | `int`, `string` work; `List<string>` and a user class fail with `cannot be serialized for Old<T>()` |
| the same, plus `[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(X))]` for the copied type | — | — | deep copy works |
| `EnsureAssignable`, top-level property changed, not assignable | `PostconditionViolationException` | `PostconditionViolationException` | **no exception** |
| the same, for a type whose getters are not referenced elsewhere | `PostconditionViolationException` | `ArgumentException: Property Get method was not found` | **no exception** |

Root causes:

1. **`Old<T>()`.** Reflection-based System.Text.Json serialization is disabled by default in trimmed and Native AOT applications. `Old<T>()` caught only `NotSupportedException`, so System.Text.Json's `InvalidOperationException` reached the caller with no mention of uContract and no way out.
2. **`EnsureAssignable<T>()`.** Under Native AOT, reflection returns no properties and no fields for a type whose members were not kept for reflection. The comparison loop then compares nothing and reports "no differences": a postcondition that silently passes. The private method that reads the metadata suppresses the trim warning (`IL2070`) that pointed at this. Under trimming alone, a removed getter leaves a `PropertyInfo` whose `GetMethod` is `null`, and reading it throws `ArgumentException`.

The second defect does not depend on the default; it was the same in 1.0.0 with `DBC=on`. The first was newly exposed by ADR-0020.

Two facts about the existing comparison matter for the decision:

- Assignable patterns are matched with an unanchored `Regex.IsMatch` against **top-level** member names only. An auto-property is compared twice, as the property and as its backing field `<Name>k__BackingField`; the pattern `Customer` skips both, `^Customer$` skips only the property.
- A member declared as a value type or as `string` is compared with `Equals`, without reflection. Reflection is used for the top-level type `T` and for the runtime type of a class-typed member or of a collection element.

### Constraints

- No public API signature changes (ADR-0019 baseline files are not edited).
- No new dependency (ADR-0011).
- Ordinary builds keep their behaviour unless a change is stated and marked breaking.
- A source-generated serialization path for `Old<T>()` is out of scope; [ADR-0016](0016-no-type-reference-overload-for-old.md) already requires that to be proposed in its own ADR. Tracked in [issue #39](https://github.com/cwouyang/uContract.NET/issues/39).

---

## Decision

**In 2.0.0, `Old<T>()` and `EnsureAssignable<T>()` have limited support under trimming and Native AOT. They work where they can; where they cannot, they throw `InvalidOperationException` with instructions, instead of passing silently or surfacing another library's message. The instructions were measured to work for the cases listed under Measurements; Known Limitations lists the cases in which part of the advice cannot help.**

### Details

"Under Native AOT" below means `RuntimeFeature.IsDynamicCodeSupported` is `false`. "JSON reflection disabled" means `JsonSerializer.IsReflectionEnabledByDefault` is `false`. The library reads both through the internal class `RuntimeFacts`.

Decisions made and implemented:

1. **`EnsureAssignable<T>()` preserves the top-level type.** The type parameter `T` is annotated `[DynamicallyAccessedMembers(PublicProperties | PublicFields | NonPublicFields)]`, so trimming and Native AOT keep the members of `T` that are compared. The flag set is a single private constant, `ComparedMembers`, used by both attributes that carry it.
2. **`EnsureAssignable<T>()` throws when a type shows no members** (maintainer decision). Under Native AOT, when the comparison reaches a type whose member list is empty, it throws `InvalidOperationException`. It is not a contract-violation exception: the contract could not be checked.
3. **`EnsureAssignable<T>()` reports an unreadable property.** In every build, a compared property with no get method fails with `InvalidOperationException` instead of a bare `ArgumentException`. This is the one change in ordinary builds and is **breaking**.
4. **`Old<T>()` explains itself.** When the runtime prevents the copy, the exception says so and names what works, including the combination measured to work under Native AOT (the MSBuild property plus `DynamicDependency`), not only `DBC_POST=off`.
5. **Regression protection** (maintainer decision). A console project, `tests/uContract.AotSmoke`, is published with Native AOT and run by CI with `--assert`, which makes the run fail if `RuntimeFeature.IsDynamicCodeSupported` is `true`. It is part of the solution, so every solution build compiles it. Two things are **not** protected by CI. Trimmed, non-AOT behaviour was measured by hand once. The working `Old<T>()` path under Native AOT (`JsonSerializerIsReflectionEnabledByDefault=true` plus `DynamicDependency`) is also a one-off hand measurement: the smoke project does not set that property, so CI protects the failure message of `Old<T>()` but not the working copy.
6. **Declarations stay.** The package still declares `IsTrimmable` and `IsAotCompatible`. `[RequiresUnreferencedCode]` stays on `EnsureAssignable<T>`; `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]` stay on `Old<T>`. Consumers keep getting publish-time warnings; the documentation states the limits.
7. **A test seam in the shipped assembly.** `RuntimeFacts` exposes the two runtime facts with a nullable override each, and the library declares `InternalsVisibleTo("uContract.Tests")`. Unit tests cannot otherwise reach either behaviour. This is a new convention for this repository.

#### `EnsureAssignable<T>()`

**No members visible.** The check runs where the members of a type would be enumerated: for `T` always; for the runtime type of a class-typed member or of a collection element only after the existing short-circuits (same reference; either side `null`).

- It runs on every call. The cached metadata is read each time, so a cached empty member list throws every time.
- `System.Object` is exempt. It has no members in any build, so `private readonly object _lock = new();` compares as before, and so does `EnsureAssignable<object>(a, b)`.
- A top-level member listed as assignable is skipped before anything beneath it is inspected.
- A top-level difference found earlier does not stop the loop, so a later "cannot compare" wins over it. Inside a nested object the first difference returns first.

Message for a nested member or a collection element:

```text
EnsureAssignable cannot compare {type} (reached through '{path}'): no properties or fields are visible to reflection under Native AOT. Ways out: list '{top-level member}' as assignable (patterns are regular expressions matched against top-level member names, so use the plain member name); if the type has members, preserve them, for example with [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(X))] where X is that type; or set DBC_POST=off (disables all postcondition checks).
```

`{type}` is `Type.ToString()` of the type with no visible members. `{path}` is `typeof(T).Name`, then `.` and the member name per level; a collection element appends `[]` without an index; a compiler-generated backing field is shown as the property it backs. Examples: `OrderHolder.Customer`, `LinesHolder.Items[]`.

Message when the type is `T` itself. It names neither "assignable" nor `DynamicDependency`, because neither applies:

```text
EnsureAssignable cannot compare {type}: no properties or fields are visible to reflection under Native AOT. If the type has members, the generic argument passed to EnsureAssignable is missing its [DynamicallyAccessedMembers] annotation; otherwise set DBC_POST=off (disables all postcondition checks).
```

**Unreadable property.** In any build and at any level — `T`, a nested member, a collection element — when a property that has to be compared (public, instance, non-indexer, inherited ones included) has `PropertyInfo.GetMethod == null`, the read throws before the property is touched:

```text
EnsureAssignable cannot read property {declaring type}.{name}: no get method is visible through the compared type. If trimming removed it, preserve the type's members, for example with [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(X))] where X is that type. Otherwise list the top-level member that leads to it as assignable, or set DBC_POST=off (disables all postcondition checks).
```

- A write-only public property declared on the type, on a nested type or on a base class used to fail with `ArgumentException` and now fails with `InvalidOperationException`.
- A property with a non-public getter (`{ private get; set; }`) is compared as before when the getter is declared on the compared type. A getter that is not visible through the compared type — a private getter declared on a base class, or a virtual property whose derived class overrides only the setter — reflects with `GetMethod == null` and is reported by this rule (see Known Limitations).
- A top-level write-only property listed as assignable is not read.
- Other exceptions keep their type: a getter that throws still surfaces as `TargetInvocationException`.

**Forwarding a generic parameter.** A caller that passes its own generic parameter to `EnsureAssignable<T>` with trim analysis enabled gets warning `IL2091` until it adds the same annotation. That is the intended signal.

#### `Old<T>()`

The order of evaluation is unchanged up to serialization: argument null check, recursion guard, enabled check, supplier, a `null` result returns `default`. Then:

1. If JSON reflection is disabled, `Old<T>()` throws `InvalidOperationException` with no inner exception. This applies in any build in which the application has disabled reflection-based serialization.

   ```text
   Old<T>() cannot copy the value: reflection-based JSON serialization is disabled, which is the default in trimmed and Native AOT applications. Set the MSBuild property JsonSerializerIsReflectionEnabledByDefault to true in the application project; under Native AOT also preserve the copied types, for example with [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(X))]; or set DBC_POST=off (disables all postcondition checks).
   ```

2. If serialization throws `NotSupportedException`, `Old<T>()` throws the existing `InvalidOperationException` with that exception as inner. In an ordinary build the message is unchanged:

   ```text
   Type {typeof(T).Name} cannot be serialized for Old<T>(). Ensure the type is JSON-serializable.
   ```

   Under Native AOT the same message is followed by a space and:

   ```text
   Under Native AOT a JSON-serializable type also needs its members preserved for reflection, for example with [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(X))]; or set DBC_POST=off (disables all postcondition checks).
   ```

With postconditions disabled, `Old<T>()` returns `default` without invoking the supplier and `EnsureAssignable<T>()` returns without comparing, as before.

#### Resulting behaviour

| | Ordinary build | Trimmed, not Native AOT | Native AOT |
|---|---|---|---|
| `Old<T>()`, application defaults | deep copy | throws, first message above | throws, first message above |
| `Old<T>()`, `JsonSerializerIsReflectionEnabledByDefault=true` | deep copy | deep copy in the pre-change measurement; a copied type that lost members to the trimmer was not tested | `int` works; the measured types whose members were not preserved (`List<string>`, a user class) throw with the Native AOT sentence; with `DynamicDependency` the deep copy of those types works |
| `EnsureAssignable<T>()`, members of `T` | compared | compared | compared |
| `EnsureAssignable<T>()`, nested object or collection element | compared | a removed getter is reported by name; a type whose members were removed entirely compares as equal | a member or element declared as a value type or as `string` is compared with `Equals`; a class whose members are not visible to reflection throws "cannot compare" unless the type is preserved or the top-level member is listed as assignable; a class whose members the application happens to keep is compared normally |

---

## Measurements

Environment for everything in this section except "Types measured with a prototype": `win-x64`, .NET SDK 8.0.2xx (from `global.json`), no `DBC*` variable set, each publish from a clean `obj` directory. "Native AOT, by library state" covers the unmodified library and the intermediate states as well as the final one; the other subsections up to the prototype were measured on the final implementation. The prototype subsection is a design-time measurement.

### The smoke program

`tests/uContract.AotSmoke` runs 21 checks, prints one line per check and exits with `0` only when every check matches. When dynamic code is supported, or `--report` is passed, it prints the outcomes without asserting (report mode). With `--assert` it never falls back to report mode: if dynamic code is supported it prints `FAIL 0 Assert mode expected=Native AOT got=dynamic code supported` and exits with `1`, so a publish whose output supports dynamic code cannot pass. Measured: the Native AOT binary with `--assert` gives 21 `PASS` and exit code 0, also with `DBC_POST=off`; a JIT run built with `-p:PublishAot=false` and given `--assert` prints that single `FAIL` line and exits with 1. A plain `dotnet run` of this project is different: it reports no dynamic-code support, so with `--assert` it runs the checks instead. Types used for a "not visible" check are never passed to a reflection API in the program.

| # | Check | Expected under Native AOT, postconditions enabled |
|---|---|---|
| 1 | `Require("…", () => false)` | `PreconditionViolationException` |
| 2 | `Old(() => 42)` | `InvalidOperationException` containing `Old<T>()` and `DBC_POST=off` |
| 3 | `Old(() => new List<string> { "a" })` | the same |
| 4 | flat type, nothing changed | no exception |
| 5 | flat type, property changed | `PostconditionViolationException` |
| 6 | flat type, property changed and listed as assignable | no exception |
| 7 | fields-only type, field changed | `PostconditionViolationException` |
| 8 | type with a user-struct member, struct changed | `PostconditionViolationException` |
| 9 | type with `private readonly object _lock`, each side its own lock, nothing else different | no exception |
| 10 | nested class, nothing changed | `InvalidOperationException` containing the nested type's name, the path, `'Customer' as assignable`, `DynamicDependency`, `DBC_POST=off` |
| 11 | nested class, nested value changed | the same |
| 12 | the same member listed as assignable | no exception |
| 13 | nested record, nothing changed | `InvalidOperationException`, same form |
| 14 | `List<LineElement>` member, one element each | `InvalidOperationException`; path `LinesHolder.Items[]` |
| 15 | nested type preserved with `DynamicDependency`, nested value changed | `PostconditionViolationException` |
| 16 | the same, nothing changed | no exception |
| 17 | `EnsureImmutableCollection` on an immutable collection | no exception |
| 18 | `EnsureImmutableCollection` on a mutable collection | `PostconditionViolationException` |
| 19 | type whose only state is a private field, field changed | `PostconditionViolationException` |
| 20 | property inherited from a base class, changed | `PostconditionViolationException` |
| 21 | protected field inherited from a base class, changed | `PostconditionViolationException` |

With `DBC_POST=off`: check 1 still throws, checks 2 and 3 return the default value, the others raise no exception.

### Native AOT, by library state

| Library state | Checks that fail |
|---|---|
| unmodified, all 21 checks | **13 fail**: 2, 3, 5, 7, 8, 10, 11, 13, 14, 15, 19, 20, 21 (silent passes, and System.Text.Json's own message) |
| "no members visible" rule only (18 checks at the time) | 2, 3; and 4–16 all throw naming the top-level type |
| plus the annotation on `T` (18 checks at the time) | 2, 3 |
| plus the `Old<T>()` messages | none (21 of 21); with `DBC_POST=off`: 21 of 21 |

Checks 19–21 were added after the annotation was in place, so the two intermediate rows were measured when the program had 18 checks; 10 of those 18 failed on the unmodified library. The unmodified library was measured again with all 21 checks: checks 19–21 (private field, inherited property, inherited protected field changed) report "no exception", which are silent passes.

After the flag set was moved into the `ComparedMembers` constant, the Native AOT publish reported no warnings and the run was again 21 of 21.

**Mutation of the annotation.** With `NonPublicFields` removed from both `[DynamicallyAccessedMembers]` attributes, checks 19 and 21 fail with the top-level "cannot compare" message.

### Trimmed, not Native AOT

`dotnet publish tests/uContract.AotSmoke -c Release -r win-x64 --self-contained -p:PublishAot=false -p:PublishTrimmed=true`, run with `--report`. The publish reported 0 warnings.

| Checks | Result |
|---|---|
| 2, 3 `Old(...)` | `InvalidOperationException` with the "reflection-based JSON serialization is disabled" message |
| 4, 6, 9, 12, 16 (nothing changed, or assignable) | no exception |
| 5, 7, 8 top-level property, public field, struct member changed | `PostconditionViolationException` |
| 19, 20, 21 private field, inherited property, inherited protected field changed | `PostconditionViolationException` |
| 10, 11 nested class | `InvalidOperationException` from the unreadable-property rule, naming `uContract.AotSmoke.CustomerInfo.Name`; the trimmer had removed the getter (`GetMethod == null` confirmed) |
| 14 list element | the same message for `LineElement.Sku` |
| 13 nested record, nothing changed | no exception; the "no members visible" rule does not fire without Native AOT. Whether the record's members survived or were removed and compared as equal was not determined |
| 15 preserved nested type, value changed | `PostconditionViolationException` |

No top-level change went undetected.

### `Old<T>()` under Native AOT with JSON reflection enabled

Smoke program published with Native AOT and `JsonSerializerIsReflectionEnabledByDefault=true`:

| Check | Result |
|---|---|
| 2 `Old(() => 42)` | works |
| 3 `Old(() => new List<string> { "a" })` | `InvalidOperationException`: ``Type List`1 cannot be serialized for Old<T>(). Ensure the type is JSON-serializable.`` followed by the Native AOT sentence |

### A consumer application under Native AOT

A separate throwaway console application with no warning suppression and `JsonSerializerIsReflectionEnabledByDefault=true`.

Publish warnings for 7 call sites (3 × `Old`, 4 × `EnsureAssignable`, one of them through a forwarding generic method): **14 × `IL2026`, 6 × `IL3050`, 2 × `IL2091`**. Each warning is reported twice, once by the analyzer and once by trim analysis. The `IL2091` text names `PublicFields`, `NonPublicFields`, `PublicProperties` and the forwarding method.

| Case | Result |
|---|---|
| `Old` of a user class with `[DynamicDependency(All, typeof(Copied))]` | deep copy works: a distinct instance, with a `string` member and a `List<string>` member copied |
| `Old` of `List<string>` with `[DynamicDependency(All, typeof(List<string>))]` | deep copy works |
| `Old` of a user class that is not preserved | `InvalidOperationException` naming the type, with the Native AOT sentence |
| caller forwards its own unannotated generic parameter to `EnsureAssignable<T>` | `IL2091` at publish; at run time `InvalidOperationException` with the top-level "cannot compare" message |
| `object`-typed member holding a `string` (distinct instances, equal) | no exception |
| `object`-typed member holding a boxed `int`, equal or different | `InvalidOperationException`: "cannot compare System.Int32 (reached through 'BoxHolder.Boxed')…" |

### Types measured with a prototype

During design review a throwaway prototype of this design was run under Native AOT in one program, with a distinct, non-null instance on each side and nothing changed.

- **Threw** under the "no members visible" rule: a nested class or record, `Uri`, `Exception`, `Version`, `Lazy<int>`, `SemaphoreSlim`, `CancellationTokenSource`, an `object`-typed member holding a user class instance, an empty marker class, a member-less interface as `T`.
- **No exception**: `DateTime`, `Guid`, `decimal`, enums, `Nullable<T>`, tuples, user structs, arrays, `IReadOnlyList<string>`, `TimeSpan`, a non-null delegate, `new object()`, an `object`-typed member holding a `string`, an interface that declares properties as `T`.

That program also reported no exception for an `object`-typed member holding a boxed `int`. The consumer application above shows that result does not generalize; see Known Limitations. A consumer-side `[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(X))]` made a nested class, a nested record and a `List<>` element type comparable.

`RuntimeFeature.IsDynamicCodeSupported` was `false` under Native AOT and `true` in ordinary and trimmed CoreCLR builds of a project without `PublishAot`.

---

## Consequences

### Positive Consequences

- ✅ Under Native AOT, `EnsureAssignable<T>()` compares the members of `T`, including a private field, an inherited public property and an inherited protected field (smoke checks 19–21), without the consumer doing anything.
- ✅ A nested type or element type whose members are not visible under Native AOT is reported by an exception that names the type, the path and a first path segment that works as an assignable pattern.
- ✅ `Old<T>()` fails with uContract's own message and instructions. Following them under Native AOT was measured to produce a working deep copy. That was a one-off hand measurement in a separate application; CI does not repeat it.
- ✅ Under trimming, a getter removed by the trimmer is reported by declaring type and property name.
- ✅ CI publishes and runs the Native AOT smoke program with `--assert`, with contracts at their default and with `DBC_POST=off`.

### Negative Consequences

- ❌ **Breaking**: a compared type with a property that has no get method now fails with `InvalidOperationException` instead of `ArgumentException`.
- ❌ Under Native AOT, a nested object or collection element whose class has no members visible to reflection throws until the consumer acts. In the measured program this included records and classes used as value objects, and framework classes such as `Uri`, `Version`, `Lazy<int>` and `SemaphoreSlim`. Whether a framework type's members are visible depends on the application: one that happens to keep a type's members compares it normally. Members and elements declared as a value type or as `string` are compared with `Equals` and are not affected. For types the consumer does not own, listing the member as assignable is the practical way out.
- ❌ Under application defaults, `Old<T>()` does not work in a trimmed or Native AOT application; the consumer must change a build property or set `DBC_POST=off`.
- ❌ Callers that forward a generic parameter to `EnsureAssignable<T>` get `IL2091` under trim analysis.
- ❌ The limitations listed below remain.

### Neutral Consequences

- ⚖️ No public API signature changed. Unit tests went from 257 to 296.
- ⚖️ The library assembly now exposes internals to `uContract.Tests`.
- ⚖️ The smoke project is compiled by every solution build, so a compile break in it blocks a build; the release workflow does not run it.

---

## Known Limitations

Accepted with this decision.

- **Partial metadata is not detected.** If only some members of a type are visible to reflection, the others are skipped silently. The rule detects "no members", not "some missing".
- **Detection depends on the data.** The rule fires when the comparison reaches a type: both values non-null, not the same reference and, for a collection, at least one element. A `null` member, a shared instance or an empty collection passes without the type being inspected.
- **Trimming without Native AOT.** The "no members visible" rule does not fire, because dynamic code is supported. A removed getter is reported by the unreadable-property rule; a type whose members were removed entirely compares as equal.
- **A boxed value in an `object`-typed member is application-dependent under Native AOT.** The comparison looks at the declared member type. `object` is a class, so the comparison recurses into the runtime type, here `System.Int32`, whose private field is visible to reflection only if the application happens to keep it. One measured program kept it and compared normally; a clean consumer application did not, and got "cannot compare System.Int32". The outcome is never silent, and listing the member as assignable works. A `string` in an `object`-typed member compares normally. This is left as it is for 2.0.0. Tracked in [issue #40](https://github.com/cwouyang/uContract.NET/issues/40).
- **The rule also fires for a type that has nothing to list**: an empty marker class, `record Marker;`, a class whose only state is private fields declared on a base class. For those `DynamicDependency` cannot help, which is why the message offers it only "if the type has members"; only listing the member as assignable or `DBC_POST=off` works. The message is hedged; that such a type throws at all remains a limitation. Tracked in [issue #40](https://github.com/cwouyang/uContract.NET/issues/40).
- **The Native AOT signal is a proxy.** `RuntimeFeature.IsDynamicCodeSupported == false` is also reported where reflection metadata is intact. One such case was observed: a project with `<PublishAot>true</PublishAot>` run with a plain `dotnet run`. There the only effect of the rule is that a type with genuinely no members throws.
- **Two property shapes with a getter are reported, not compared.** In an ordinary build, a property declared on a base class with a private getter, and a virtual property whose derived class overrides only the setter, both reflect through the derived type with `GetMethod == null`. They are reported as "no get method is visible through the compared type", which is accurate for both, but they are not compared, and `DynamicDependency` does not help. Two characterization tests pin this: for the private getter declared on a base class the message names the base type; for the derived class that overrides only the setter it names the derived type. Before this change the same inputs threw a bare `ArgumentException`, so this is not a regression. Whether to compare them is tracked in [issue #40](https://github.com/cwouyang/uContract.NET/issues/40).
- **`Old<T>()` attributes a supplier's `NotSupportedException` to serialization.** The supplier runs inside the same `try` as the serialization. This predates the change; under Native AOT such an exception now also gets the Native AOT sentence. Noted in [issue #39](https://github.com/cwouyang/uContract.NET/issues/39).
- **`DBC_POST=off` is broader than the helper that failed.** Every message offers it as a way out and says "(disables all postcondition checks)". There is no switch for one helper alone.
- **Comparison gaps that exist in every build and are not changed here**: private fields declared on a base class are not compared; the top level compares the declared type `T`, not the runtime type; dictionary values, members typed as a non-enumerable interface, and the own members of a class that is also enumerable are compared by `Equals` or by element only. These are tracked in [issue #40](https://github.com/cwouyang/uContract.NET/issues/40).

## Not Tested

- `linux-x64` locally. It is exercised by the CI job only; all local measurements are `win-x64`.
- Platforms other than `win-x64` and `linux-x64`.
- Non-CoreCLR runtimes: Mono, Unity, iOS, WebAssembly.
- Trim settings other than the SDK default.
- A trimmed application with JSON reflection enabled whose copied type lost members to the trimmer.
- The smoke program published with its `IL2026;IL3050` suppression removed. The warning counts above come from the separate consumer application instead.

## Questions Raised in Review

Review of the implementation raised four questions about the messages and the rules. The maintainer decided all four before release. Three led to changes in the message texts, which are quoted above as they now stand; one is deferred.

| # | Question | Decision | State |
|---|---|---|---|
| 1 | Should the nested "cannot compare" message be hedged for types that have nothing to list? | Yes. The `DynamicDependency` way out now reads "if the type has members, preserve them, for example with […]". | Implemented |
| 2 | For the two `GetMethod == null` shapes that do have a getter: correct the wording, or actually compare the property? | Correct the wording: "it has no get method" became "no get method is visible through the compared type". Actually comparing the two shapes stays with [issue #40](https://github.com/cwouyang/uContract.NET/issues/40). | Wording implemented; comparison not done |
| 3 | Should a boxed value type be compared with `Equals` when its members are hidden? | Left as it is for 2.0.0. Whether to compare it with `Equals` is to be decided in [issue #40](https://github.com/cwouyang/uContract.NET/issues/40). | Deferred; no change |
| 4 | Should the messages say that `DBC_POST=off` disables all postconditions? | Yes. Every message that offers it now says "DBC_POST=off (disables all postcondition checks)". | Implemented |

After these changes the unit tests passed 296 of 296 in both configurations, and the Native AOT smoke program passed 21 of 21 with `--assert`, also with `DBC_POST=off`.

---

## Alternatives Considered

### Alternative 1: Treat an uncomparable object as modified

**Description**: Throw `PostconditionViolationException` when a type's members are not visible.

**Why rejected**: It reports a violation that may not exist.

---

### Alternative 2: Ask `Equals` first, throw only when unequal

**Description**: Compare an uncomparable object with `Equals` and raise "cannot compare" only when that returns `false`.

**Why rejected**: A changed record then reports "cannot compare" rather than a violation. Harder to explain and to test.

---

### Alternative 3: Declare both helpers unsupported under Native AOT and throw on entry

**Why rejected**: It gives up flat types that work, and would block runtimes that report no dynamic code but keep reflection metadata.

---

### Alternative 4: Skip a property that has no get method

**Description**: Leave the property out of the comparison instead of throwing.

**Why rejected**: In an ordinary build the state of such a property usually lives in a field that is compared anyway, so skipping would lose little there. But a getter removed by trimming looks identical, and its backing field may be gone too, so skipping would be silent in exactly the case this change exists for.

---

### Alternative 5: Documentation only

**Why rejected**: It leaves a silent pass in a library whose purpose is to make failures loud.

---

### Out of scope: source-generated serialization for `Old<T>()`

Full support needs a path that does not depend on reflection, such as a `JsonSerializerContext` or the `JsonTypeInfo<T>` overload described as Alternative 3 of ADR-0016. It is not part of this decision and needs its own ADR. Tracked in [issue #39](https://github.com/cwouyang/uContract.NET/issues/39).

---

## Related Decisions

- **Supersedes (one consequence only)**: [ADR-0020 - Contracts Enabled by Default](0020-contracts-enabled-by-default.md) — its "Trimming and Native AOT: not verified" consequence is replaced by the measurements and limits recorded here. Its decision stands. ADR-0020 stays `Accepted` and carries an amendment.
- **Qualifies**: [ADR-0006 - Serialization and Deep Copy Mechanism for `Old<T>()`](0006-serialization-deep-copy.md) — the mechanism stands; "works on all .NET platforms" does not hold under application defaults in trimmed and Native AOT applications. Carries an amendment.
- **Qualifies**: [ADR-0007 - Reflection and Field Comparison for `EnsureAssignable<T>()`](0007-reflection-field-comparison.md) — the comparison strategy stands; the annotation and the two `InvalidOperationException` rules are added. Carries an amendment.
- **Qualifies**: [ADR-0016 - No TypeReference Overload for `Old<T>()`](0016-no-type-reference-overload-for-old.md) — its decision stands; the implication that diagnostics alone make `Old<T>()` usable under Native AOT is superseded. Carries an amendment.
- **Related to**: [ADR-0019 - Public API Baseline Tracking](0019-public-api-baseline-tracking.md) — the baseline is unchanged.

---

## Implementation Notes

This ADR was written after the implementation so that it could record what the work turned up.

- **Order of the work.** The smoke program came first and was run against the unmodified library. Then: the `RuntimeFacts` seam (structural); seven characterization tests pinning ordinary-build behaviour (two more, for the `GetMethod == null` shapes, were added after review); the "no members visible" rule; the unreadable-property rule (breaking); the annotation; the `Old<T>()` messages; the CI steps. The message wording was revised last, after the questions raised in review were decided.
- **Why `RuntimeFacts` is a separate class.** Putting the overrides on `Contract` runs `Contract`'s static initialiser when a test sets them, which freezes `Contract.Config` early.
- **How "cannot compare" travels.** It leaves the private comparison methods through a `ref` parameter: a small object created only at the point of failure, with path segments prepended while the recursion unwinds. A comparison that is not blocked allocates nothing for the path.
- **Where the unreadable-property check lives.** In `MemberAccessor.GetValue`, the single read point for top-level and nested members.
- **Annotating only the two type parameters is sufficient.** The method that reads metadata from a `Type` is not annotated and keeps its `IL2070` suppression, because nested and element types are known only at run time; its justification says that under Native AOT a type that exposes no members at all is reported by the "no members visible" rule. The annotation on the private generic method is not what preserves members (the public one does); it makes narrowing the public annotation alone show up as `IL2091` in `dotnet build`.
- **The member set must agree in two places.** It was first written out three times: on each of the two attributes and as the `BindingFlags` used to read the metadata. A later structural change made the two attributes share one constant, `ComparedMembers`. The `BindingFlags` are still tied to that constant only by a comment at the reflection query and by the smoke checks.
- **A smoke test that passes before the fix proves nothing.** The smoke program needed three corrections. One check preserved the outer type itself, so it passed on the unmodified library. There was no check for non-public fields or inherited members: removing `NonPublicFields` from the annotation passed every unit test and all 18 original smoke checks; checks 19–21 were added for that. And the program could not tell assert mode from report mode: it chose by itself, so a binary that was not Native AOT would have reported instead of failing. `--assert` was added and CI passes it.
- **`PublishAot` in a project file changes `dotnet run`.** A plain JIT `dotnet run` of such a project reports `RuntimeFeature.IsDynamicCodeSupported == false`, so the smoke program asserts instead of reporting, while reflection still returns everything. Use `--report` or `-p:PublishAot=false` for an ordinary-build run; `CONTRIBUTING.md` says so.
- **Clean `obj` for each publish.** The measurements were taken that way because an incremental publish can keep a stale feature switch.
- **CI.** The Linux job publishes the smoke program for `linux-x64` and runs it twice with `--assert`. `linux-x64` was not measured locally, so that job is the only place where the Linux native toolchain and the checks on Linux are exercised.

### Amendment (2026-10-04): Supplier exceptions in `Old<T>()`, and three corrections

The decision is unchanged. This amendment records one later decision and qualifies three statements of this ADR.

**Supplier exceptions propagate unchanged.** This supersedes the Known Limitation "`Old<T>()` attributes a supplier's `NotSupportedException` to serialization", which was noted in [issue #39](https://github.com/cwouyang/uContract.NET/issues/39).

- In `Old<T>()` the `catch (NotSupportedException)` that produces the "cannot be serialized" message now wraps only the two serializer calls. An exception raised while the supplier delegate executes reaches the caller as thrown, as the same instance. That includes a `NotSupportedException` and a derived type such as `PlatformNotSupportedException`. Under Native AOT such an exception therefore no longer gets the Native AOT sentence either.
- Boundary: user code that the serializer itself runs is still inside the serializer calls. A property getter, a custom converter or a lazily evaluated sequence that throws `NotSupportedException` during the copy is re-wrapped by System.Text.Json and is still reported as "cannot be serialized". For example, `Old(() => stream.Length)` on a non-seekable stream now propagates the stream's exception; `Old(() => stream)` does not propagate it unchanged, and what it reports depends on which property getter fails first.
- Exceptions of other types from the serializer propagate untranslated, as before: for example a `JsonException` when the object graph is deeper than the serializer's maximum depth, or an `InvalidOperationException` thrown by a getter.
- Provenance of these statements: the property-getter case is pinned by a unit test. The converter, lazily evaluated sequence, stream and maximum-depth cases were measured once in a throwaway console project during review and are not covered by tests in the repository.
- This is a **breaking change** for a caller that caught `InvalidOperationException` to handle its own supplier's `NotSupportedException`. In 1.0.0 the supplier ran inside the same `try`.

**Unit-test figures are a snapshot.** "Unit tests went from 257 to 296" under Consequences and "296 of 296" under Questions Raised in Review describe the suite on the day this ADR was accepted. The suite has grown since; neither figure is a statement about the current suite.

**An inherited auto-property is compared once.** Context says an auto-property is compared twice, as the property and as its backing field. That holds for an auto-property declared on the compared type. An inherited one is compared once, as the property: its backing field is a private field declared on a base class, and reflection through the derived type does not return such fields. For an inherited auto-property the pattern `^Customer$` therefore skips the whole comparison of that member.

**The second `Accepted` row in Revision History** records a revision of the message wording after review. It is not a second acceptance; the ADR was accepted once.

### Amendment (2026-10-07): `Old<T>()` parts superseded by ADR-0022

[ADR-0022](0022-faithful-old-copies-and-robust-comparison.md) supersedes the `Old<T>()` parts of this ADR. The `EnsureAssignable<T>()` decisions stand: the annotation on `T`, the "no members visible" rule (now asking `Equals` first, R8), the unreadable-property rule, and the smoke program in CI.

- **Superseded**: decision 4 and the `Old<T>()` messages. `Old<T>()` no longer uses JSON, so the "reflection-based JSON serialization is disabled" and "cannot be serialized" exceptions are gone. Under Native AOT a field hidden from reflection is copied bitwise instead.
- **Superseded**: in decision 6, "`[RequiresDynamicCode]` stays on `Old<T>`". It is removed. `[RequiresUnreferencedCode]` stays, with a new message, and `T` gains `[DynamicallyAccessedMembers(PublicFields | NonPublicFields)]`.
- **Settled**: the statement that a member declared as a value type is compared with `Equals`, the Known Limitation on a boxed value in an `object`-typed member, and the open question 3 on boxed values. Dispatch now uses runtime types (R0), and value types are compared by R3. Framework types such as `Lazy<int>`, `SemaphoreSlim` and `CancellationTokenSource` are compared by reference (R5), so they no longer reach the "no members visible" rule.
- **Settled in part**: ADR-0021 Alternative 2 ("ask `Equals` first") is re-adopted for nested classes (R8). The nested "cannot compare" message gained a sentence; ADR-0022 quotes it.
- The measurements above stay as the record of 2.0.0. ADR-0022 holds the measurements of the new behaviour.

### Amendment (2026-10-09): When the "no members visible" check for T does not run (#52)

The decision is unchanged. [Issue #52](https://github.com/cwouyang/uContract.NET/issues/52) changes how `EnsureAssignable<T>()` treats `actual` and `expected` themselves. A `null` on either side is compared as a value, before any member of `T` is enumerated. When `T` is a `string`, a delegate type or a nullable value type, the two values are compared as a whole, and no member of `T` is enumerated. The decisions and their reasons are in the amendments for #52 of [ADR-0012](0012-dotnet-improvements-over-java.md) (`null`) and of [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md) (the comparison, the measurements and the limits). This amendment names the statements of this ADR that they qualify. Each is quoted by its opening words:

- "The check runs where the members of a type would be enumerated: for `T` always". The check for `T` does not run when `actual` or `expected` is `null`, or when `T` is a `string`, a delegate type or a nullable value type. The members of `T` are not enumerated in those calls.
- "It runs on every call." It runs on every call that enumerates the members of `T`. A `T` with no visible members and a `null` on one side gives a postcondition violation, and two `null`s give no exception.
- "`{path}` is `typeof(T).Name`, then". Below a nullable value type as `T`, the path starts with the name of the underlying type.
- Decision 1: "keep the members of `T` that are compared". For a nullable value type as `T`, the compared fields are those of the underlying type. The annotation names the members of the nullable type and does not name those fields, so they may not be preserved.
- Positive Consequences: "Under Native AOT, `EnsureAssignable<T>()` compares the members of `T`", which ends "without the consumer doing anything". That does not hold for the three kinds of `T`: no member of `T` is compared. For a nullable struct that holds a reference, the consumer may have to preserve the underlying type.
- The unreadable-property message: "Otherwise list the top-level member that leads to it as assignable". Below a nullable value type as `T` there is no member to list. The rest of the message still applies (preserve the type if trimming removed the getter, or set `DBC_POST=off`), and passing the unwrapped values makes the member listable.

Unchanged: the "no members visible" rule itself, its message when the type is `T`, the statement that `System.Object` is exempt, and every other decision of this ADR.

### Amendment (2026-10-10): The "no members visible" check for T does not run for one shared instance on both sides (#54)

The decision is unchanged. [Issue #54](https://github.com/cwouyang/uContract.NET/issues/54) adds one case in which `EnsureAssignable<T>()` enumerates no member of `T`. A shared type is a type that `Old<T>()` shares with the original instead of copying it. In a call that compares, when one instance of a shared type whose state can change is passed as `actual` and as `expected`, the method throws `InvalidOperationException` before any member of `T` is enumerated. The runtime type of the instance decides that, not `T`, and not which members are visible to reflection. So in such a call a `T` with no visible members gets the message of that rule, not the "no members visible" message. The rule is rule S of ADR-0022. The amendment of [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md) for #54 records it, with its reasons, the list of the types, the message, the measurements and the limits. This amendment adds the case to three statements of the #52 amendment (its Status line and two sentences of its section). Each is quoted by its opening words:

- The Status line of 2026-10-09: "the "no members visible" check for `T` does not run when `actual` or `expected` is `null`, or when `T` is a `string`, a delegate type or a nullable value type". It also does not run when one instance of a shared type whose state can change is passed as `actual` and as `expected`. The Revision History row of 2026-10-09 states the same in other words, and the same case is added to it.
- "The check for `T` does not run when `actual` or `expected` is `null`, or when `T` is a `string`, a delegate type or a nullable value type." It also does not run when one instance of a shared type whose state can change is passed as `actual` and as `expected`. The members of `T` are not enumerated in that call.
- "That does not hold for the three kinds of `T`: no member of `T` is compared." It does not hold either when one instance of a shared type whose state can change is passed as `actual` and as `expected`: no member of `T` is compared, whatever `T` is.

Still true: when both sides are one instance of a type that is not shared, a `T` with no visible members still throws the "no members visible" message. A unit test pins that under a simulated Native AOT runtime.

Unchanged: the "no members visible" rule itself, its message when the type is `T`, the statement that `System.Object` is exempt, and every other decision of this ADR.

### Amendment (2026-10-10): The "no members visible" rule and FrozenSet or FrozenDictionary values (#49)

The decision is unchanged. [Issue #49](https://github.com/cwouyang/uContract.NET/issues/49) changes how `EnsureAssignable<T>()` compares a `FrozenSet<T>` and a `FrozenDictionary<TKey, TValue>` ("the two types" below; a value is of the two types when its runtime type is a construction of one of them or derives from one). `Old<T>()` shares such a value with the original instead of copying it, and it still does. Two values of the two types that a member or element holds are now compared element by element, in enumeration order, where they were compared by reference when their runtime types were equal. When the declared `T` is one of the two types, `actual` and `expected` are compared as a whole, and no member of `T` is enumerated. The amendment of [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md) for #49 records the change, with its reasons, the measurements and the limits. This amendment names the statements of this ADR that it qualifies. Each is quoted by its opening words.

**In the accepted text.**

- Known Limitations: "**Detection depends on the data.** The rule fires when the comparison reaches a type", which ends "A `null` member, a shared instance or an empty collection passes without the type being inspected." One instance on both sides still passes without being inspected. That holds for one value of the two types too: it is not enumerated. Since ADR-0022, two different instances of one runtime type of a type that `Old<T>()` shares are decided by reference, also without their members being inspected (R5; a `string` and a delegate have rules of their own). That no longer holds for two instances of the two types of one runtime type; two of different runtime types were already compared element by element. The elements, keys and values of two different instances of the two types are now reached whatever their runtime types, so the rule can fire for the class of an element. Smoke check 78 shows it under Native AOT: two holders, each with a `FrozenSet<HiddenLeaf>` of one element, give "EnsureAssignable cannot compare uContract.AotSmoke.HiddenLeaf (reached through 'FrozenLeavesHolder.Leaves[]')". The rest of the statement stands.
- "`{path}` is `typeof(T).Name`, then" a `.` and the member name per level. When `T` is one of the two types, no member name follows `typeof(T).Name`: the path goes on with `[]` for the element. For `T` = `FrozenSet<HiddenLeaf>` the path is ``FrozenSet`1[]``. This was measured by a unit test under the simulated Native AOT runtime and by smoke check 79 under Native AOT. The message is the one that the #52 amendment of ADR-0022 gives for a class below a nullable value type. It has no way out "list … as assignable", because there is no member to list.

**In the third Amendment (#52).**

- "When `T` is a `string`, a delegate type or a nullable value type, the two values are compared as a whole, and no member of `T` is enumerated." The same now holds when `T` is one of the two types.
- The Status line of 2026-10-09: "the "no members visible" check for `T` does not run when `actual` or `expected` is `null`, or when `T` is a `string`, a delegate type or a nullable value type". It also does not run when `T` is one of the two types. The Revision History row of 2026-10-09 says "for a `T` that is compared as a whole", and that now includes the two types.
- "The check for `T` does not run when `actual` or `expected` is `null`, or when `T` is a `string`, a delegate type or a nullable value type." It also does not run when `T` is one of the two types. The members of `T` are not enumerated in that call.
- "That does not hold for the three kinds of `T`: no member of `T` is compared." It does not hold either when `T` is one of the two types: the elements, keys and values are compared, and no member of `T`. The annotation on `T` does not name the members of an element type, as for every collection element.
- "Below a nullable value type as `T`, the path starts with the name of the underlying type." Below one of the two types as `T` it starts with `typeof(T).Name`, as said above.
- "Below a nullable value type as `T` there is no member to list." Below one of the two types as `T` there is no member to list either. The rest of the unreadable-property message still applies (preserve the type if trimming removed the getter, or set `DBC_POST=off`). This was read from the code.

The fourth Amendment (#54) added one case to the second, third and fourth of these statements. That case stands.

**How this was confirmed.** That the check for `T` does not run when `T` is one of the two types was read from the code. The check is in the method that walks the members of `T`, and that method is not called for a `T` that is compared as a whole. No member metadata is read for `T` or for the runtime types of the two collections. The class of an element, key or value is still checked where the comparison reaches it. Smoke checks 77 and 79 call `EnsureAssignable<T>()` with `T` = `FrozenSet<string>` and with `T` = `FrozenSet<HiddenLeaf>` under Native AOT and give the expected results. They do not show that the check does not run: whether a `FrozenSet<T>` as `T` can have no visible members under Native AOT was not determined.

Unchanged: the "no members visible" rule itself, its message when the type is `T`, the statement that `System.Object` is exempt, and every other decision of this ADR.

### Amendment (2026-10-11): The "no members visible" rule and arrays (#57)

The decision is unchanged. [Issue #57](https://github.com/cwouyang/uContract.NET/issues/57) changes how `EnsureAssignable<T>()` compares arrays. When the declared `T` is an array type or `System.Array`, `actual` and `expected` are compared as a whole: element by element, and no member of `T` is enumerated. The same change makes `Old<T>()` copy an array whose element type is declared in `System.Collections.Frozen`, and a value of a value type that is declared there, where it shared them with the original; so the comparison now goes into two such arrays or values, where it decided them by reference. The amendment of [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md) for #57 records the changes, with their reasons, the behaviour before and after, the measurements and the limits. This amendment names the statements of this ADR that it qualifies. Each is quoted by its opening words.

**In the accepted text.**

- "`{path}` is `typeof(T).Name`, then" a `.` and the member name per level. When `T` is an array type or `System.Array`, the path does not start with `typeof(T).Name`. It starts with the parameter name `actual` and goes on with `[]` for an element: `actual[]` for an element, `actual[][]` for an element of an element, and `actual[].Leaf` for a member `Leaf` of an element. The name of an array type holds brackets of its own, so with `typeof(T).Name` the path to an element of a `HiddenLeaf[]` would read `HiddenLeaf[][]`. The message is the one that the #52 amendment of ADR-0022 gives for a class below a nullable value type. It has no way out "list … as assignable", because there is no member to list. Unit tests under the simulated Native AOT runtime hold the whole message for five paths (`T` = `X[]`, `X[][]`, `X[,]`, `System.Array` and an array of a class with a member of type `X`, where `X` is a class with no members), and smoke check 82 measured the path `actual[]` under Native AOT.
- Known Limitations: "**Detection depends on the data.** The rule fires when the comparison reaches a type". That holds below an array as `T` too. With dynamic code off, two arrays that hold one instance of a class with no members, and two empty arrays of such a class, give no exception: the class is not inspected. Two arrays that each hold their own instance throw, also when one array is the `Old<T>()` copy of the other and nothing changed, unless the `Equals` of the class says "equal". Unit tests hold each of these results.

**In the fifth Amendment (#49).**

- "Since ADR-0022, two different instances of one runtime type of a type that `Old<T>()` shares are decided by reference, also without their members being inspected". An array whose element type is declared in `System.Collections.Frozen` and a value of a value type that is declared there are no longer types that `Old<T>()` shares. Two such arrays are compared element by element, and two such values by `Equals` and then by their fields, so the rule can fire for a class that an element or a field of them leads to. That was read from the code. No unit test and no smoke check has a class with no visible members below such an array or value.
- "For `T` = `FrozenSet<HiddenLeaf>` the path is ``FrozenSet`1[]``." That stands: the unit test of #49 and smoke check 79 still give that path. The path that starts with `actual` is for an array type or `System.Array` as `T` only.
- "Below one of the two types as `T` it starts with `typeof(T).Name`, as said above." Below an array type or `System.Array` as `T` it starts with `actual`.
- "Below one of the two types as `T` there is no member to list either." Below an array type or `System.Array` as `T` there is no member to list either. The rest of the unreadable-property message still applies. This was read from the code.

**In the third Amendment (#52).**

- "When `T` is a `string`, a delegate type or a nullable value type, the two values are compared as a whole, and no member of `T` is enumerated." The same now holds when `T` is an array type or `System.Array`.
- The Status line of 2026-10-09: "the "no members visible" check for `T` does not run when `actual` or `expected` is `null`, or when `T` is a `string`, a delegate type or a nullable value type". It also does not run when `T` is an array type or `System.Array`. The Revision History row of 2026-10-09 says "for a `T` that is compared as a whole", and that now includes an array type and `System.Array`.
- "The check for `T` does not run when `actual` or `expected` is `null`, or when `T` is a `string`, a delegate type or a nullable value type." It also does not run when `T` is an array type or `System.Array`. The members of `T` are not enumerated in that call.
- "That does not hold for the three kinds of `T`: no member of `T` is compared." It does not hold either when `T` is an array type or `System.Array`: the elements are compared, and no member of `T`. The annotation on `T` does not name the members of the element type, as for every collection element, so the consumer may have to preserve the element type. Smoke check 82 shows what happens when it is not preserved.

The fourth Amendment (#54) and the fifth Amendment (#49) each added one case to these statements. Those cases stand.

**How this was confirmed.** That the check for `T` does not run when `T` is an array type or `System.Array` was read from the code. The check is in the method that walks the members of `T`, and that method is not called for a `T` that is compared as a whole. The class of an element is still checked where the comparison reaches it. Smoke checks 81 and 82 call `EnsureAssignable<T>()` with `T` = `int[]` and with `T` = `HiddenLeaf[]`. In the Native AOT publishes for `win-x64`, the default one and the variant with the hidden dictionary entry, both gave the expected results. These are the two lines of check 82 in the default publish, with postconditions on and with `DBC_POST=off`; the variant gave the same two lines. The prefix names the run and is not output of the program, and the first line is given up to the closing parenthesis of "(reached through …)":

```text
on:  PASS 82 EnsureAssignable<HiddenLeaf[]> elements with hidden members expected=InvalidOperationException got=InvalidOperationException EnsureAssignable cannot compare uContract.AotSmoke.HiddenLeaf (reached through 'actual[]')…
off: PASS 82 EnsureAssignable<HiddenLeaf[]> elements with hidden members expected=no exception got=no exception
```

In check 82 each of the two arrays holds its own `HiddenLeaf`, and the two have an equal `Value`. No attribute preserves the members of `HiddenLeaf`, and the class has no `Equals` of its own, so the two instances are unequal by `Equals`, and the call throws although the content is equal. Under the JIT the members of `HiddenLeaf` are visible, and the check gives no exception. The checks do not show that the check for `T` does not run: whether an array type as `T` can have no visible members under Native AOT was not determined. `System.Array` as `T` and an array of pointers or of function pointers were not measured under Native AOT. The `linux-x64` publish runs in CI and was not measured for this amendment.

Unchanged: the "no members visible" rule itself, its message when the type is `T`, the statement that `System.Object` is exempt, and every other decision of this ADR.

### Amendment (2026-10-12): The trimming suppression of EnsureImmutableCollection and its smoke checks (#61)

The decision is unchanged. [Issue #61](https://github.com/cwouyang/uContract.NET/issues/61) gives `EnsureImmutableCollection<T>()` a new rule; the amendment of [ADR-0012](0012-dotnet-improvements-over-java.md) of the same date records it. Under that rule the method reads the interfaces of the runtime type of its argument with `Type.GetInterfaces()`, and the build reports trimming warning `IL2070` for that read. This amendment records the suppression of that warning, the seven smoke checks that were added for the method, what was measured under Native AOT and in a trimmed publish without Native AOT, and what the continuous integration repeats. The amendment of [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md) of the same date records the other measured results of the change and points here for these.

**The suppression.** `[UnconditionalSuppressMessage]` with the category `Trimming` and the id `IL2070` is on the private method `Contract._ImplementsImmutableCollectionInterface`, which holds the read. The public method has no trimming attribute, and its signature is unchanged. The justification of the suppression makes five statements. Each is given here with what holds it:

| # | Statement of the justification | What holds it |
|---|---|---|
| 1 | No caller is annotated: the signature and the attributes of `EnsureImmutableCollection` are not to change, and an annotation on that method, for example `[RequiresUnreferencedCode]`, would put a trimming diagnostic on callers of a method that only reads the runtime type of its argument. | A decision of the maintainer (I7 in the amendment of ADR-0012). Measured with a throwaway project, with the trim analyzer on: `[RequiresUnreferencedCode]` on a method gave `IL2026` at the calls of it that the project holds, with a concrete argument and with a forwarded type parameter; `[DynamicallyAccessedMembers]` for the interfaces on its type parameter gave no diagnostic for a call with a concrete type argument, and `IL2091` where a generic caller forwarded its own type parameter without the annotation. |
| 2 | The library itself refers to the five interface types, in the `typeof` expressions of this method. | Read from the code: the method names `IImmutableList<>`, `IImmutableSet<>`, `IImmutableDictionary<,>`, `IImmutableStack<>` and `IImmutableQueue<>` in `typeof` expressions. |
| 3 | The Native AOT smoke program of the repository holds a check that calls the method with an instance of a class of that program that implements one of the five interfaces, and expects it to be accepted. | Check 89 below. A comment at the check points back to the justification. |
| 4 | The measured results are recorded in ADR-0021. | "Measured" below. |
| 5 | If one of the five interfaces were missing from what the runtime returns here, a type that implements only that interface among the five and passes by no other condition would be reported as a violation, not accepted. | Read from the code: without a match the method answers "violation". No publish was measured in which an interface was missing. |

**The checks.** Checks 85 to 91 of the smoke program call `EnsureImmutableCollection<T>()`:

| # | Check | Expected under Native AOT, postconditions enabled |
|---|---|---|
| 85 | `EnsureImmutableCollection` on an array of immutable lists (`ImmutableList<int>[]`) | `PostconditionViolationException` |
| 86 | `EnsureImmutableCollection` on an array of function pointers (`delegate*<void>[]`) | `PostconditionViolationException` |
| 87 | `EnsureImmutableCollection` on a frozen set of strings | no exception |
| 88 | `EnsureImmutableCollection` on the builder of an immutable list | `PostconditionViolationException` |
| 89 | `EnsureImmutableCollection` on a class of the program that implements `IImmutableStack<int>`; its members throw | no exception |
| 90 | `EnsureImmutableCollection` on a `ReadOnlyCollection<int>` over a list | no exception |
| 91 | `EnsureImmutableCollection` on a class of the program with the full name ``System.Collections.ObjectModel.ReadOnlySet`1`` | no exception |

With `DBC_POST=off`, each of the seven expects no exception. The default build of the program and its variant with the hidden dictionary entry (`-p:AotSmokeHiddenDictionaryEntry=true`) both hold the seven checks.

Check 89 is the check that the justification names. Its class is passed as its own type, so the method reads the interfaces of that class.

Check 91 adds the comparison by full name. The program is built for `net8.0` and declares a class `ReadOnlySet<T>` in the namespace `System.Collections.ObjectModel`; the library has no reference to a type of that name and holds the name as text. Checks 87, 89 and 90 pass a type that is, derives from or implements a type that the library refers to, and the library accepts such a type by comparing types, before it compares a name (see the amendment of ADR-0012). So check 91 is the one check of the program whose result comes from the comparison by full name. Measured under the JIT: with the comparison by full name taken out of the library, check 91 reported a violation, and checks 87, 89 and 90 still reported no exception.

**Measured.** At the final code of the change, on `win-x64`, with the .NET 8 SDK.

Native AOT. Each publish ran `dotnet publish tests\uContract.AotSmoke\uContract.AotSmoke.csproj -c Release -r win-x64 -warnaserror`, with switches that make the build sequential and name the output folder, and with `-p:AotSmokeHiddenDictionaryEntry=true` for the variant. Neither publish log names a diagnostic:

| Publish | With postconditions on | With `DBC_POST=off` |
|---|---|---|
| Default | 91 `PASS`, no `FAIL` | 91 `PASS`, no `FAIL` |
| Variant with the hidden dictionary entry | 90 `PASS`, no `FAIL` | 90 `PASS`, no `FAIL` |

These are the seven lines of the default publish with postconditions on; the variant gave the same seven lines. With `DBC_POST=off`, each of the seven lines ends `expected=no exception got=no exception`, in both publishes:

```text
PASS 85 EnsureImmutableCollection array of immutable lists expected=PostconditionViolationException got=PostconditionViolationException Postcondition violated: Ensure resultingCollection is an immutable collection
PASS 86 EnsureImmutableCollection array of function pointers expected=PostconditionViolationException got=PostconditionViolationException Postcondition violated: Ensure resultingCollection is an immutable collection
PASS 87 EnsureImmutableCollection frozen set of strings expected=no exception got=no exception
PASS 88 EnsureImmutableCollection builder of an immutable list expected=PostconditionViolationException got=PostconditionViolationException Postcondition violated: Ensure resultingCollection is an immutable collection
PASS 89 EnsureImmutableCollection class that implements IImmutableStack expected=no exception got=no exception
PASS 90 EnsureImmutableCollection read-only collection over a list expected=no exception got=no exception
PASS 91 EnsureImmutableCollection class with the full name of ReadOnlySet expected=no exception got=no exception
```

Trimmed, not Native AOT. The publish was made by hand: the smoke project, default build, published self-contained for `win-x64` with `-p:PublishAot=false -p:PublishTrimmed=true -p:TrimMode=full`. The publish log names no diagnostic. The program was run with postconditions on, and it printed its results in report mode (`INFO` lines). Ten checks of the program call the method: 17, 18, 64 and 85 to 91. Each of the ten printed the same result as in the JIT run of the same code:

| Checks | Result in the trimmed publish, postconditions on |
|---|---|
| 17, 87, 89, 90, 91 | no exception |
| 18, 85, 86, 88 | `PostconditionViolationException` |
| 64 (a `null` `collection`) | `ArgumentNullException` naming `collection` |

The trimmed publish was not run with `DBC_POST=off`, and the variant was not published trimmed.

JIT. In its `--report` mode the program prints 91 lines in the default build and 90 in the variant. Checks 85 to 91 gave the same results as under Native AOT, with postconditions on and with `DBC_POST=off`.

So the class of check 89 was accepted in the two Native AOT publishes, in the trimmed publish and under the JIT.

**What the continuous integration repeats.** `build-and-test.yml` has three steps for the smoke program, and they run on the Linux runner only. One publishes the default build with Native AOT for `linux-x64`, with `-warnaserror`. Two run the published program with `--assert`: one with the defaults and one with `DBC_POST=off`. So CI repeats checks 85 to 91 of the default build under Native AOT. It does not publish the variant, and it has no step that publishes the program trimmed without Native AOT: the trimmed result above is a hand measurement. The workflow had not run for this change when this amendment was written. A trimmed publish in CI is to be proposed in a follow-up issue, which was not filed when this amendment was written.

**Not measured.** The `linux-x64` publish locally. A publish in which the trimmer removes one of the five interfaces from a type that is passed to the method.

**Qualified in the accepted text.** Each statement is quoted by its opening words:

- Decision 5: "Two things are **not** protected by CI. Trimmed, non-AOT behaviour was measured by hand once." CI still does not protect it. It was measured by hand again for this change, for the ten checks that call `EnsureImmutableCollection<T>()`; "Measured" above gives the publish at the final code.
- Measurements, "Trimmed, not Native AOT": the table there has no row for checks 17 and 18. In the trimmed publish of this change, check 17 gave no exception and check 18 gave `PostconditionViolationException`.
- Implementation Notes: "**Annotating only the two type parameters is sufficient.** The method that reads metadata from a `Type` is not annotated and keeps its `IL2070` suppression". That is said of the comparison of `EnsureAssignable<T>()`, and it stands. The library now has one more `IL2070` suppression, in `Contract`, with a justification of its own.

**Still true in the accepted text.** The rows of checks 17 and 18 in the table of the smoke program: in the two Native AOT publishes of this change, check 17 gave no exception and check 18 gave `PostconditionViolationException`.

**Qualified in the sixth Amendment (#57).** The statement is quoted by its opening words:

- "`System.Array` as `T` and an array of pointers or of function pointers were not measured under Native AOT." That stands for `EnsureAssignable<T>()` and for `Old<T>()`. Check 86 passes a `delegate*<void>[]` to `EnsureImmutableCollection<T>()` under Native AOT, and the method reports a violation.

Unchanged: the "no members visible" rule, the unreadable-property rule, the annotations and the attributes of `Old<T>()` and of `EnsureAssignable<T>()`, and the other decisions of this ADR.

### Amendment (2026-10-26): Inherited getters and base-class private fields are compared (#40)

The decision is unchanged. [Issue #40](https://github.com/cwouyang/uContract.NET/issues/40), items 1 and 4, changes which members `EnsureAssignable<T>()` lists for a compared type and how it reads them. [ADR-0023](0023-comparison-of-base-class-members.md) records the decision, the rejected alternatives and the limits. In short: a public property whose getter is not visible through the compared type is read with the getter of its declaring type, or of the nearest declaration of the same virtual property that has one, and is listed once in every build; the private instance fields declared on the base classes of a compared type are compared, up to the first class whose assembly is signed with a public key token of the .NET 8 shared frameworks; members whose raw names collide are shown with their declaring type. The trimming annotations do not change. This amendment names the statements of this ADR that it qualifies. Each is quoted by its opening words.

**In the accepted text.**

- Unreadable property: "A property with a non-public getter (`{ private get; set; }`) is compared as before when the getter is declared on the compared type. A getter that is not visible through the compared type". Such a property is no longer reported by this rule: it is read with the getter that exists and compared. The rule now fires only for a property with no getter at all (write-only) or with a getter that trimming removed. The first bullet ("A write-only public property …"), the third and the fourth stand; a getter found this way that throws also surfaces as `TargetInvocationException`.
- Known Limitations: "**Two property shapes with a getter are reported, not compared.**" That no longer holds, in an ordinary build and under Native AOT (smoke checks 93 and 94). The two characterization tests it names were inverted: `EnsureAssignable_WhenInheritedGetterIsPrivate_ThrowsInvalidOperationException` and `EnsureAssignable_WhenDerivedTypeOverridesOnlyTheSetter_ThrowsInvalidOperationException` are now `…AndValuesMatch_DoesNotThrow`.
- Questions Raised in Review, question 2: "Actually comparing the two shapes stays with [issue #40]", state "Wording implemented; comparison not done". The comparison is now done. The wording "no get method is visible through the compared type" stays in the message, which is now raised only for the two cases above.
- Known Limitations: "**Comparison gaps that exist in every build and are not changed here**: private fields declared on a base class are not compared". That part no longer holds for a base class that is not part of .NET: under the JIT its private fields are compared, for `T`, for nested objects and for elements below a `T` that is compared as a whole. The private fields of a framework base class are still not compared. Under Native AOT they are compared only where the application preserves the base class (see "Still true" below; smoke checks 96 and 97). In a trimmed application without Native AOT the same holds, and a base class whose fields the trimmer removed is not detected (read from the code, not measured). This amendment does not change the rest of that item.
- "`{path}` is `typeof(T).Name`, then `.` and the member name per level". A member whose raw name collides with that of another member of the same compared type is shown in the path as `<declaring type>.<name>`, at any level, for example `ShelfDerived.ShelfBase.X`; when the top-level member is one of them, the nested message adds to "so use the plain member name" the words "; it also excuses the other members of that name". A unit test under the simulated Native AOT runtime holds the whole message.
- Implementation Notes: "**Annotating only the two type parameters is sufficient.** The method that reads metadata from a `Type` is not annotated and keeps its `IL2070` suppression". That stands, and the annotations did not change. Reading the member list now takes four more suppressions, each with a justification that states the Native AOT consequence: `IL2070` on `MemberComparison.CreateMetadata` and on `MemberComparison.BacksAComparedProperty`, and `IL2075` on `MemberComparison.ExistingGetter` and on `MemberComparison.BaseClassPrivateFields`.

**In the first Amendment (2026-10-04).**

- "**An inherited auto-property is compared once.**" That stays true for a public auto-property declared on a base class, and the sentence on `^Customer$` stands. The reason given there, "its backing field is a private field declared on a base class, and reflection through the derived type does not return such fields", no longer holds: the comparison now lists the private fields of base classes itself. It leaves out the backing field of a base auto-property when a compared property is that property or overrides it. An auto-property declared on a base class that is not public is not a compared property, so it is now compared once, as its backing field (for example `<Note>k__BackingField`). Unit tests hold both.

**Still true.**

- Known Limitations: "**The rule also fires for a type that has nothing to list**", including "a class whose only state is private fields declared on a base class". The "no members visible" rule is decided on the members listed before the private fields of base classes are added (`TypeMetadata.OwnMemberCount`), so such a class still throws under Native AOT, also when its base class is preserved. Two unit tests under the simulated Native AOT runtime hold it, for `T` and for a nested object. Under the JIT such a class is now compared by those fields.
- Known Limitations: "**Partial metadata is not detected.**" It now also covers the private fields of a base class that the trimmer did not preserve. Smoke check 96 shows a change of such a field passing under Native AOT; check 97 shows that `[DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicFields, typeof(Base))]` makes it a violation.
- Smoke checks 20 and 21 (an inherited property and an inherited protected field) keep their expectations.

**The checks.** Smoke checks 93 to 100 are added. Each value check returns `none` or the violation entries joined by `|`:

| # | Check | Expected under Native AOT, postconditions enabled |
|---|---|---|
| 93 | Inherited property with a private getter changed | `Secret` |
| 94 | Setter-only override changed | `S` (listed once) |
| 95 | Setter-only override over a getter-only override changed | `P` (listed once) |
| 96 | Hidden base private field changed | no exception (the documented limit) |
| 97 | Preserved base private field changed | `_count` |
| 98 | Preserved base, overridden auto-property changed, assignable | no exception |
| 99 | Preserved base, overridden auto-property changed | `P\|<P>k__BackingField` |
| 100 | Base-class walk stops at a framework class | `Code\|frameworkPrivateFields=items` |

With `DBC_POST=off`, checks 96 and 98 expect no exception, check 100 expects `none|frameworkPrivateFields=items`, and the others `none`. Measured on `win-x64` for the pull request that closed this work, with the Native AOT publish of the default build: 100 `PASS`, no `FAIL`, with postconditions on and with `DBC_POST=off`. The variant with the hidden dictionary entry was not published, no trimmed publish was made, and the CI workflow had not run for this change when this amendment was written.

Unchanged: the "no members visible" rule and its messages apart from the path segment above, the annotations and the attributes of `Old<T>()` and of `EnsureAssignable<T>()`, and the other decisions of this ADR.

### Amendment (2026-10-26): Smoke checks for two arrays of a primitive or enum element type (#64)

The decision is unchanged. [Issue #64](https://github.com/cwouyang/uContract.NET/issues/64) makes `EnsureAssignable<T>()` compare two arrays of one runtime type whose element type is a primitive type that [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md) lists in its #64 amendment, or an enum, without boxing an element. It reads the memory of the two arrays with `MemoryMarshal.GetArrayDataReference`, as the comparison of two arrays of pointers already did. No annotation and no trimming suppression is added or changed.

**The checks.** Smoke checks 101 to 107 are added. Each passes the two arrays as `T`:

| # | Check | Expected under Native AOT, postconditions enabled |
|---|---|---|
| 101 | Two `byte[]` of equal elements | no exception |
| 102 | Two `byte[]` with one element differing | `PostconditionViolationException` ("is compared as a whole") |
| 103 | Two `float[]`: `NaN` against `NaN`, and `0.0` against `-0.0` | no exception |
| 104 | Two `DayOfWeek[]` of equal elements | no exception |
| 105 | Two `DayOfWeek[]` with one element differing | `PostconditionViolationException` ("is compared as a whole") |
| 106 | Two `int[,]` of equal elements | no exception |
| 107 | Two `int[,]` with one element differing | `PostconditionViolationException` ("is compared as a whole") |

With `DBC_POST=off`, every one of them expects no exception. Measured on `win-x64` with the Native AOT publish of the default build: 107 `PASS`, no `FAIL`, with postconditions on and with `DBC_POST=off`. The variant with the hidden dictionary entry was not published, no trimmed publish was made, and the CI workflow had not run for this change when this amendment was written.

Unchanged: the "no members visible" rule, the unreadable-property rule, the annotations and the attributes of `Old<T>()` and of `EnsureAssignable<T>()`, and the other decisions of this ADR.

---

## References

- [Issue #36: Verify `Old<T>()` and `EnsureAssignable<T>()` under trimming and Native AOT with contracts enabled](https://github.com/cwouyang/uContract.NET/issues/36)
- [Issue #39: Offer a source-generated path for `Old<T>()` under trimming and Native AOT](https://github.com/cwouyang/uContract.NET/issues/39)
- [Issue #40: `EnsureAssignable<T>()` comparison gaps that exist in every build](https://github.com/cwouyang/uContract.NET/issues/40)
- [`Contract.cs`](../../src/uContract/Contract.cs) — `Old<T>`, `EnsureAssignable<T>`, the comparison helpers and the messages
- [`MemberAccessor.cs`](../../src/uContract/MemberAccessor.cs) — the unreadable-property check
- [`RuntimeFacts.cs`](../../src/uContract/RuntimeFacts.cs) — the two runtime facts and their test overrides
- [`tests/uContract.AotSmoke/Program.cs`](../../tests/uContract.AotSmoke/Program.cs) — the smoke program
- [`build-and-test.yml`](../../.github/workflows/build-and-test.yml) — the CI steps
- [README: Trimming and Native AOT](../../README.md#trimming-and-native-aot) — guidance for consumers
- [ADR-0006: Serialization and Deep Copy Mechanism for `Old<T>()`](0006-serialization-deep-copy.md)
- [ADR-0007: Reflection and Field Comparison for `EnsureAssignable<T>()`](0007-reflection-field-comparison.md)
- [ADR-0016: No TypeReference Overload for `Old<T>()`](0016-no-type-reference-overload-for-old.md)
- [ADR-0020: Contracts Enabled by Default](0020-contracts-enabled-by-default.md)

---

## Revision History

| Date       | Status      | Notes                          |
|------------|-------------|--------------------------------|
| 2026-10-04 | Accepted    | Decision recorded after implementation. Supersedes the "not verified" consequence of ADR-0020; qualifies ADR-0006, ADR-0007 and ADR-0016. |
| 2026-10-04 | Accepted    | Message wording decided after review of the implementation: three of the four questions raised are implemented, one is deferred to issue #40. See Questions Raised in Review. |
| 2026-10-04 | Amended     | Supplier exceptions in `Old<T>()` propagate unchanged (supersedes one Known Limitation). Corrections: unit-test figures are a snapshot; an inherited auto-property is compared once; the second `Accepted` row was a wording revision. See Implementation Notes > Amendment. |
| 2026-10-07 | Amended     | `Old<T>()` decisions and the `[RequiresDynamicCode]` statement superseded by ADR-0022; value-type, boxed-value and framework-type statements settled there. `EnsureAssignable<T>()` decisions and measurements unchanged. See Implementation Notes > second Amendment. |
| 2026-10-09 | Amended     | The check for `T` does not run for a `null` argument or for a `T` that is compared as a whole (#52); decisions in ADR-0012 and ADR-0022. Decision unchanged. See Implementation Notes > third Amendment. |
| 2026-10-10 | Amended     | The check for `T` also does not run for one instance of a shared type whose state can change, passed as `actual` and as `expected` (#54); decision in ADR-0022. Decision unchanged. See Implementation Notes > fourth Amendment. |
| 2026-10-10 | Amended     | The check for `T` also does not run for a `FrozenSet<T>` or a `FrozenDictionary<TKey, TValue>` as `T`; the elements of two such values that a member or element holds are reached (#49); decision in ADR-0022. Decision unchanged. See Implementation Notes > fifth Amendment. |
| 2026-10-11 | Amended     | The check for `T` also does not run for an array type or `System.Array` as `T`; below such a `T` the path in the message starts with `actual`; an array whose element type is declared in `System.Collections.Frozen` is no longer decided by reference (#57); decision in ADR-0022. Decision unchanged. See Implementation Notes > sixth Amendment. |
| 2026-10-12 | Amended     | One more `IL2070` suppression, for the interface read of `EnsureImmutableCollection<T>()`; smoke checks 85 to 91, measured under Native AOT and in a trimmed publish without Native AOT; CI repeats the Native AOT publish only (#61); decision in ADR-0012. Decision unchanged. See Implementation Notes > seventh Amendment. |
| 2026-10-26 | Amended     | The two property shapes with a hidden getter are compared through the getter that exists, and the private fields of base classes that are not part of .NET are compared, so the Known Limitation on the two shapes no longer holds and the one on base-class private fields no longer holds under the JIT; the "no members visible" rule is decided before those fields are added; four more trimming suppressions; smoke checks 93 to 100 (#40); decision in ADR-0023. Decision unchanged. See Implementation Notes > eighth Amendment. |
| 2026-10-26 | Amended     | Smoke checks 101 to 107 for two arrays of one primitive or enum element type, compared without boxing an element (#64); no annotation or suppression changed; decision in ADR-0022. Decision unchanged. See Implementation Notes > ninth Amendment. |
