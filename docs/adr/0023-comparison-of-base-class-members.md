# ADR-0023: Comparison of Members Declared on Base Classes in EnsureAssignable<T>()

## Status

**Accepted**

- **Date**: 2026-10-26
- **Deciders**: Project maintainer (cwouyang)
- **Status Date**: 2026-10-26

---

## Context

### Problem Statement

[Issue #40](https://github.com/cwouyang/uContract.NET/issues/40), items 1 and 4. `Contract.EnsureAssignable<T>()` listed the members it compares from one type, with `GetProperties(Public | Instance)` and `GetFields(Public | NonPublic | Instance)`. That left two gaps in every build:

- **Item 1.** Through a derived type, reflection returns the inherited fields that are not private, but not the private fields declared on a base class. A change that touches only such a field passed in silence. An auto-property declared on a base class that is not public is the same case, because its backing field is a private field of that class. The limit was measured for [issue #62](https://github.com/cwouyang/uContract.NET/issues/62) and recorded in the #69 amendment of [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md): with `T` = a derived class, a changed private field, private auto-property or protected auto-property of the base class gave no exception, under the JIT and under Native AOT.
- **Item 4.** A public property declared on a base class with a private getter (`{ private get; set; }`), and a virtual property whose derived class overrides only the setter, have `PropertyInfo.GetMethod == null` through the derived type. The call threw `InvalidOperationException` "… no get method is visible through the compared type", although a getter exists and can be invoked. [ADR-0021](0021-postconditions-under-trimming-and-aot.md) recorded both shapes as "reported, not compared".

Since ADR-0022, `Old<T>()` copies the private fields of base classes (`DeepCopier` walks `BaseType`), so the copy was complete and the comparison lagged behind it. A real case: ezDDD.NET's `AggregateRoot<TId, TEvent>` keeps its domain events in `private readonly List<TEvent> _domainEvents`, so raising an event changes only a base-class private field.

### Relevant Context

- Through a derived type, `GetFields(NonPublic | Instance)` lists the inherited fields that are not private. Several fields of framework classes are of that kind: `Exception._message` and `List<T>._items`, `_size` and `_version` are `internal`, so they were listed and compared before this change.
- A class of the .NET runtime and libraries declares private implementation state (for example the private fields of `Exception`). A class derived from a framework class must not start to report it.
- Under Native AOT the members that reflection returns are those the trimmer preserved. The annotation on `T` (`MemberComparison.ComparedMembers`, ADR-0021 decision 1) does not preserve the private fields of the base classes of `T`.

### Constraints

- No public API signature change; the `PublicAPI.*` baseline files are unchanged.
- No new dependency ([ADR-0011](0011-zero-dependency-principle.md)).
- The change is breaking. It targets 3.0.0 and lands under `[Unreleased]` in `CHANGELOG.md`.
- Under Native AOT and in trimmed applications, no call gets worse: the same calls pass or fail as before, except where this ADR says otherwise.
- Not in scope: the rest of #40 (the top-level comparison by the declared `T` rather than the runtime type, and the other items), and fields-only comparison ([#46](https://github.com/cwouyang/uContract.NET/issues/46)).

---

## Decision

**`EnsureAssignable<T>()` compares the private instance fields declared on the base classes of each type that it compares by its members, up to the first class of .NET; it reads the two property shapes of item 4 through the getter that exists; and it shows members whose names collide with their declaring type, while assignable patterns are still matched against the raw member name. The trimming annotations do not change.**

### Details

1. **Base-class private fields.** For each type that the comparison compares by its members — `T`, the runtime type of a nested object, and an element below a `T` that is compared as a whole (an array, a frozen collection) — the member list is the list of 2.0.0 plus the private instance fields that each base class declares (`FieldInfo.IsPrivate`, `DeclaredOnly`).
   - **Framework stop.** The walk up the base classes stops at the first class whose assembly is signed with a public key token of the .NET 8 shared frameworks (Microsoft.NETCore.App, Microsoft.AspNetCore.App and Microsoft.WindowsDesktop.App). That class and the classes above it add nothing. The six tokens are `7cec85d7bea7798e`, `b03f5f7f11d50a3a`, `cc7b13ffcd2ddd51`, `adb9793829ddae60`, `31bf3856ad364e35` and `b77a5c561934e089`. The token is read, not the namespace, because a namespace can be chosen freely. A library that is signed otherwise or not signed (ezDDD.NET, for example) is walked.
   - **Unchanged.** The fields of a framework base class that are not private are listed by `GetFields` and are compared as before. Static fields are not compared. A nested collection class of yours is compared by its elements, as before.
   - **Backing fields.** The backing field `<X>k__BackingField` of a base class is left out when one of the compared properties is the property `X` that this base class declares, or overrides it, directly or through further overrides. So a public auto-property of a base class is still compared once, through the property. An auto-property of a base class that is not public is not a compared property, so it is compared as its backing field. A property hidden with `new` does not override, so the base backing field it hides is compared.
   - **"No members visible".** The rule of ADR-0021 (R8 of ADR-0022) is decided on the members of the 2.0.0 list, before the base-class private fields are added.
   - Reporting base-class private state is correct also where calls that passed before now fail, such as `_domainEvents` after an event is raised. Callers list such a member as assignable where the change is intended.
2. **Item 4.** A public property whose getter is not visible through the compared type is read with the getter that exists: the getter of its declaring type (private or otherwise), or the getter of the nearest declaration of the same virtual property that has one. The property is listed once in every build: where the listing holds more than one declaration of one virtual property, the declarations are merged. The `InvalidOperationException` "no get method is visible through the compared type" remains for a property with no getter at all (write-only) or with a getter that trimming removed.
3. **Colliding names.** Two members of a compared type collide when their raw names (`MemberInfo.Name`, ordinal) are equal, counted after the merge of decision 2. A property and its own backing field do not collide. Each member of a colliding group is shown with its declaring type:
   - in a violation entry as `<name> (declared on <type>)`, for example `_state (declared on StateBase)`, so the text before the first space is the raw name;
   - in the path of a "cannot compare" message as `<type>.<name>`, at any level, so the path reads `D.B.X` (compared type, declaring type, member); the advice still names the plain name (the raw name, a backing field shown as the property it backs), and when the top-level member is one of a colliding group it adds to "so use the plain member name" the words "; it also excuses the other members of that name";
   - `<type>` is `Type.Name`, or `Type.FullName` for the whole group when two of its declaring types have the same `Name`.

   Assignable patterns are matched against the raw name only, as in 2.0.0. One pattern therefore excuses every member of that name.
4. **Native AOT: the trimming annotations do not change.** The annotations of `T` on `Old<T>()` and `EnsureAssignable<T>()` stay as they are. Under Native AOT the private fields of base classes are not preserved, so they are not compared and `Old<T>()` shares the objects they refer to, unless the application preserves the base class, for example with `[DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicFields, typeof(Base))]`.

### Compatibility

The `[Unreleased]` section of `CHANGELOG.md` holds three **BREAKING** bullets that end `(#40; ADR-0023)`, and one sentence in "Migrating from 2.x". In short:

- A change of a private field declared on a base class that is not part of .NET, or of an auto-property declared there that is not public (compared as its backing field), now fails the call, under the JIT and under Native AOT where the base class is preserved. The private fields of a base class from another library become possible violation entries and pattern targets, and their names can change with that library's versions.
- The two item 4 shapes are compared instead of throwing `InvalidOperationException`. Their getters now run, and a getter that throws ends the call in `TargetInvocationException`, with the getter's exception as `InnerException`.
- Members whose names collide are shown with their declaring type. This also renames entries that collide in 2.0.0 already (a field hidden with `new`). Members whose names do not collide are shown as before.
- Under Native AOT the comparison changes only for a property whose getter is not visible through the compared type, in the display of colliding names, and for a base class whose private fields the application preserves.

---

## Consequences

### Positive Consequences

- ✅ Under the JIT, a change of state held in a private field of a base class is reported for `T`, for nested objects and for elements. The comparison now agrees with the copy of `Old<T>()`.
- ✅ The two item 4 shapes are compared, under the JIT and under Native AOT (smoke checks 93 to 95).
- ✅ The private state of .NET's own base classes stays out of the comparison, so a class derived from `Exception`, `ObservableCollection<T>` or `DeflateStream` (unit tests) or from `Collection<int>` (smoke check 100) reports no private field of that class.
- ✅ The entry text up to its first space is a pattern that excuses the member, also for a member whose name collides.

### Negative Consequences

- ❌ **Breaking**: calls that passed in 2.0.0 now fail, for example after an aggregate raises a domain event into a private list of its base class.
- ❌ **Breaking**: a getter that never ran in such a call before now runs, and can throw.
- ❌ **Breaking**: violation entries of colliding members change their text.
- ❌ The private field names of third-party base classes become part of what callers see and match.

### Neutral Consequences

- ⚖️ No public API signature changed.
- ⚖️ The "no members visible" decision for a type is unchanged: it is made on the members of the 2.0.0 list. Under Native AOT the comparison can now reach that rule through more paths: through the objects that preserved base-class private fields hold, and through the values that the getters of item 4 return.

---

## Known Limitations

- **Native AOT and trimmed applications.** The private fields of base classes are not compared unless the base class is preserved, for example with `[DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicFields, typeof(Base))]` (or `All`). Smoke check 96 shows a change of such a field passing under Native AOT; check 97 shows the way out. The objects that those fields hold are compared by their members, so their types must be preserved too; otherwise two unequal instances of such a type end in "cannot compare" (read from the code, not measured). In a trimmed application without Native AOT a base class whose fields were removed cannot be told from one that declares none, so the gap is not detected (read from the code, not measured).
- **A type whose only state is base-class private fields** still meets the "no members visible" rule under Native AOT, also when the base class is preserved, because the rule is decided before those fields are added.
- **One member of a colliding pair cannot be excused alone.** Patterns are matched against the raw name, so a pattern that matches one member of the pair matches the other.
- **The framework stop is a list of six tokens** read from the .NET 8.0.16 shared frameworks. The stop reads the token, not where the assembly comes from, so a package outside the shared frameworks whose assembly is signed with one of the six tokens is treated as a framework class too: the walk stops at its classes, and their private fields are not compared. Microsoft packages such as EF Core can fall under this (`adb9793829ddae60` is the token of ASP.NET Core and `Microsoft.Extensions.*`). Which packages it covers was not measured.

---

## Alternatives Considered

### Alternative 1: Annotate `T` with `DynamicallyAccessedMemberTypes.All` (option A)

**Description**: Annotate `T` of `Old<T>()` and `EnsureAssignable<T>()` with `All`, so that the private fields of base classes are preserved under Native AOT and the comparison is the same in every build.

**Why rejected**: Measured before the decision. A caller that propagates `[RequiresUnreferencedCode]` gets no warning, but a caller that suppresses `IL2026` gets new warnings for common type arguments: `IL2111` for `T` = `Action` (five warnings), and `IL3050` for `T` = `int[]` (five) and for a class derived from `Exception` (one). And fields that became visible would hold objects whose types are not preserved, so unchanged values would end in "cannot compare".

---

### Alternative 2: Four public key tokens (the runtime libraries and ASP.NET Core only)

**Description**: Stop the walk at classes signed with `7cec85d7bea7798e`, `b03f5f7f11d50a3a`, `cc7b13ffcd2ddd51` and `adb9793829ddae60`, the tokens first measured.

**Why rejected**: A risk review of the first implementation found classes signed with `31bf3856ad364e35` and `b77a5c561934e089` that the walk would enter: WPF and Windows Forms classes in Microsoft.WindowsDesktop.App, such as `DependencyObject`, and assemblies of Microsoft.NETCore.App such as `System.IO.Compression` and the `WindowsBase` facade. A WPF view model would then report the private state of `DependencyObject`. A scan of the three 8.0.16 shared frameworks found exactly six tokens, and the list holds all six.

---

### Alternative 3: Match assignable patterns against the qualified name

**Description**: Match a pattern against the name shown with the declaring type, so that one member of a colliding pair can be excused alone.

**Why rejected**: A pattern then matched through the declaring-type part: `"Base"` excused `StateBase._state`. A narrower rule, which matched the qualified name but not the qualifier alone, still widened for a pattern that spans both parts: `"Base.*Id"` excused the `Id` declared on `BaseEntity`. Matching against the raw name only cannot widen a pattern beyond 2.0.0, so both were dropped. The advice of the "cannot compare" message, which first named an escaped qualified name, now names the plain name.

---

### Alternative 4: Write a colliding entry as `Type.member`

**Description**: Show a colliding member in a violation entry as `StateBase._state`.

**Why rejected**: A caller who copied the entry text as a pattern got the violation back, because patterns are matched against the raw name. The entry now starts with the raw name: `_state (declared on StateBase)`.

---

### Alternative 5: Merge two declarations of a property only when they share an accessor

**Description**: Treat two listed declarations of one name as one virtual property when one of them is, or overrides, an accessor of the other.

**Why rejected**: In a three-level chain `A { virtual P get; set; }` ← `B { override P get; }` ← `C { override P set; }`, `C.P` and `B.P` declare no accessor in common. Under Native AOT, `GetProperties` on `C` returned both declarations (measured with a throwaway program before the implementation), so a risk review of the first merge found that `P` would be listed twice. The unit test `Members_WhenTheListingHoldsASetterOnlyOverrideAndTheGetterOnlyOverrideBelowIt_ListTheTitleOnce` reproduced it under the JIT: it hands that listing to `CreateMetadata`, and before the fix the list held two members. The merge now compares the types that declare the first declarations (`GetBaseDefinition`) of the accessors, whichever accessors each declaration has. Smoke check 95 shows `P` listed once under Native AOT.

---

## Related Decisions

- **Amends**: [ADR-0021 - Postcondition Helpers under Trimming and Native AOT](0021-postconditions-under-trimming-and-aot.md) — its limits "two property shapes with a getter are reported, not compared" and "private fields declared on a base class are not compared" no longer hold; its decisions stand. Carries an amendment.
- **Amends**: [ADR-0022 - Faithful Old<T>() Copies and Robust EnsureAssignable<T>() Comparison](0022-faithful-old-copies-and-robust-comparison.md) — the scope statements on #40 items 1 and 4 and the measured limit of base-class private members (#69 amendment) no longer hold; R0–R8 and `Old<T>()` stand, with a longer member list. Carries an amendment.
- **Related to**: [ADR-0007 - Reflection and Field Comparison for EnsureAssignable<T>()](0007-reflection-field-comparison.md) — the reflection comparison that this ADR widens.
- **Related to**: [ADR-0019 - Public API Baseline Tracking](0019-public-api-baseline-tracking.md) — the baseline is unchanged.

---

## Implementation Notes

This ADR was written after the implementation so that it could record what the work turned up.

- **Where it lives.** `MemberComparison.CreateMetadata` builds the list: the public properties (indexers left out, merged declarations dropped by `IsOverriddenByAnother`), then the fields of `GetFields`, then the base-class private fields of `BaseClassPrivateFields`; `QualifyCollidingNames` names colliding members last. `ExistingGetter` finds the getter of item 4; its walk up the base classes is not stopped by the framework stop, because the property is listed already. `MemberAccessor` keeps the raw name (`RawName`, for patterns), the entry text (`Name`) and the path segment (`PathName`).
- **The "no members visible" count is kept apart.** `TypeMetadata.OwnMemberCount` is the number of members before the base-class private fields are added; `MemberComparison.MembersAreHidden` decides on it, at the top level and for nested objects.
- **A seam for Native AOT listings.** `MemberComparison.CreateMetadata(Type, PropertyInfo[])` takes the property listing as a parameter. Unit tests pass the listing that Native AOT returns (a setter-only override together with its base declarations), so the merge is tested under the JIT; smoke checks 94 and 95 measure it under Native AOT.
- **Trimming suppressions.** The library suppresses `IL2070` on `CreateMetadata` and on `BacksAComparedProperty`, and `IL2075` on `ExistingGetter` and on `BaseClassPrivateFields`, each in the existing `UnconditionalSuppressMessage` style with a justification that states the Native AOT consequence. The annotations did not change.
- **Framework fields that are internal.** `Exception._message` and `List<T>._items`, `_size` and `_version` are `internal`, not private. A first unit test for the framework stop asserted that `_message` was absent, and failed for that reason rather than for a missing stop; the test was rewritten against the private fields that `Exception` declares. The smoke check for the stop (check 100) uses `Collection<int>`, whose only instance field is private.
- **A review finding settled by the compiler.** One review finding needed a get-only auto-property override of a `get; set;` property. C# rejects that (CS8080: an auto-property override must override all accessors), so the shape cannot exist and the finding was withdrawn.
- **Native AOT.** Smoke checks 93 to 100 pin the Native AOT side: the two item 4 shapes and the three-level chain (93–95), a hidden base private field passing (96), the way out with `DynamicDependency(NonPublicFields, typeof(Base))` (97), a preserved base with an overridden auto-property, with and without the pattern `^(P|<P>k__BackingField)$` (98, 99), and the framework stop (100). Measured on `win-x64` for the pull request that closed this work: 100 `PASS` with postconditions on and with `DBC_POST=off`. The variant with the hidden dictionary entry was not published for this change, and CI had not run for it when this ADR was written.
- **Unit tests.** 1,167 unit tests passed in the pull request that closed this work. That figure is a snapshot.

---

## References

- [Issue #40: `EnsureAssignable<T>()` comparison gaps](https://github.com/cwouyang/uContract.NET/issues/40)
- [Issue #62: Confirm three limits](https://github.com/cwouyang/uContract.NET/issues/62)
- [Issue #69: Document the measured limits](https://github.com/cwouyang/uContract.NET/issues/69)
- [`MemberComparison.cs`](../../src/uContract/MemberComparison.cs) — the member list, the framework stop, the merge and the names
- [`MemberAccessor.cs`](../../src/uContract/MemberAccessor.cs) — the getter, the raw name, the entry text and the path segment
- [`TypeMetadata.cs`](../../src/uContract/TypeMetadata.cs) — `OwnMemberCount`
- [`Contract.cs`](../../src/uContract/Contract.cs) — `EnsureAssignable<T>()` and its XML documentation
- [`EnsureAssignableBaseClassFieldTests.cs`](../../tests/uContract.Tests/EnsureAssignableBaseClassFieldTests.cs), [`EnsureAssignableHiddenGetterTests.cs`](../../tests/uContract.Tests/EnsureAssignableHiddenGetterTests.cs), [`EnsureAssignableNameCollisionTests.cs`](../../tests/uContract.Tests/EnsureAssignableNameCollisionTests.cs) — the unit tests
- [`tests/uContract.AotSmoke/Program.cs`](../../tests/uContract.AotSmoke/Program.cs) — smoke checks 93 to 100
- [`CHANGELOG.md`](../../CHANGELOG.md) — the `[Unreleased]` bullets
- [ADR-0007](0007-reflection-field-comparison.md), [ADR-0021](0021-postconditions-under-trimming-and-aot.md), [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md)

---

## Revision History

| Date       | Status      | Notes                          |
|------------|-------------|--------------------------------|
| 2026-10-26 | Accepted    | Decision recorded after implementation (#40 items 1 and 4). Amends ADR-0021 and ADR-0022. |
