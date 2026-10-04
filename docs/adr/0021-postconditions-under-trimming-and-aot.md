# ADR-0021: Postcondition Helpers under Trimming and Native AOT

## Status

**Accepted**

- **Date**: 2026-10-04
- **Deciders**: Project maintainer
- **Status Date**: 2026-10-04

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
