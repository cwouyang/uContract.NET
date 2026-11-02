# ADR-0025: Fields-Only Comparison Not Adopted; Assignable Patterns Also Match the Property Name of a Backing Field

## Status

**Accepted**

- **Date**: 2026-11-02
- **Deciders**: Project maintainer (cwouyang)
- **Status Date**: 2026-11-02

---

## Context

### Problem Statement

[Issue #46](https://github.com/cwouyang/uContract.NET/issues/46) asked whether `Contract.EnsureAssignable<T>()` should compare the instance fields of a type only, instead of its public properties and its instance fields. Reading the public properties runs user and framework getters on `actual` and on `expected`. The issue lists getters that throw (`MemoryStream.ReadTimeout`, `Encoding`), getters that create a new handle on each side (`SemaphoreSlim.AvailableWaitHandle`), a getter that blocks (`Task<T>.Result` of a pending task), and getters with side effects. The issue also asked which rule assignable patterns would need, because a pattern anchored on a property name (`^Email$`) does not match the name of its backing field (`<Email>k__BackingField`).

### Relevant Context

- Members compared before this ADR ([ADR-0007](0007-reflection-field-comparison.md), [ADR-0023](0023-comparison-of-base-class-members.md)): the public instance properties and the instance fields of each type that is compared by its members, and the private fields declared on its base classes up to the first class of .NET. An auto-property declared on `T` is compared twice, as the property and as its backing field.
- Assignable patterns are regular expressions matched against the names of the top-level members. In 2.0.0 and until this ADR, a pattern was matched against the raw name (`MemberInfo.Name`) only. So an anchored pattern for an auto-property declared on `T` had to cover both names, `^(Email|<Email>k__BackingField)$`, and an auto-property that is not public, declared on `T` (and, since #40, on a base class), was excused only by a pattern that matches `<Email>k__BackingField`.
- [Issue #40](https://github.com/cwouyang/uContract.NET/issues/40) item 7: "an auto-property is compared twice (the property and its backing field), so a hidden type behind both a field `_customer` and a property `Customer` needs two patterns."

### Constraints

- No public API signature change; the `PublicAPI.*` baseline files are unchanged.
- No new dependency ([ADR-0011](0011-zero-dependency-principle.md)).
- The work targets 3.0.0 and lands under `[Unreleased]` in `CHANGELOG.md`.

---

## The measurements

A throwaway spike measured fields-only comparison on 2026-11-01, on one machine: Intel i5-13500, Windows 11, .NET SDK 8.0.204, `win-x64`, under the JIT and under Native AOT, on .NET 8. The spike put temporary switches into the library (fields-only member lists, three pattern rules, and source names in violation entries) and ran the unit tests, the smoke program `tests/uContract.AotSmoke` and a probe program in each mode. With every switch unset, the build behaved as `master` at `c114a8b`: 1239 of 1239 unit tests passed, and the smoke program with `--assert` exited 0. Nothing of the spike was committed. The numbers below are quoted from its report.

**Fields-only compares a subset of what was compared.** The member list was the properties plus all instance fields, so fields-only can only lose detections, with one exception (new false positives from lazy caches, below).

**Changes that fields-only misses.** "Caught" is a violation, "missed" is a pass, "cannot compare" is the `InvalidOperationException` for a type with no members visible:

| Shape | JIT, today | JIT, fields-only | Native AOT, today | Native AOT, fields-only |
|---|---|---|---|---|
| `T` is an abstract base class; the runtime type computes the changed property from its own field | caught | missed | caught | cannot compare |
| `T` is an interface | caught | missed | caught | cannot compare |
| `T` is a base class whose virtual property the runtime type overrides with a field of its own, which changed | caught | missed | caught | missed |
| A public auto-property declared on a base class of `T` changed (also one level down, in a nested object) | caught | caught, as `<Id>k__BackingField` | caught | missed |
| A derived class whose only state is inherited auto-properties (smoke fixtures) | caught | caught | caught | cannot compare |
| A class derived from `Exception`, held by a member; its `Data` changed | caught | missed (the state is in private fields of a framework base class) | cannot compare | cannot compare |
| A property whose value is kept outside the object, keyed by identity | caught, but the unchanged case is a violation too | missed | as under the JIT | as under the JIT |
| A replaced `CancellationTokenSource` as `T` (unit test) | caught | missed | not run | not run |

Under Native AOT the private backing field of a base auto-property is not preserved, so with fields-only no inherited auto-property is compared there: a change is missed, or the call ends in "cannot compare". The JIT catches it. In the smoke program, fields-only failed 10 of the 107 checks (`--assert` exited 1; 97 `PASS`): checks 20, 28, 29, 30, 47, 48, 93, 94 and 95 end in "no members visible" for a derived class whose state is only inherited auto-properties, and check 99 changes its message. With today's comparison the JIT and Native AOT agree on these shapes, because the public property is preserved and read.

**New false positives.** Getters that fill a lazy cache ran on both sides before the fields of the same object were compared, so both caches were filled. Fields-only compares the caches as they were left. After `Old`, reading such a getter on `actual` only, with no state change, gave a violation under fields-only (JIT and Native AOT) for a user class with `_cache ??= …` and for one with `_items ??= new()`, and under the JIT for a `Uri` after reading `Host` and for a `CultureInfo` after reading `NumberFormat`. Today's comparison passed in each case.

**What fields-only removes.** Under fields-only, under the JIT, each of these cases passed: two equal `MemoryStream`s as `T` (today: `TargetInvocationException`, "Timeouts are not supported"); an `Encoding` held by a member, not shared (today, under the JIT: `NotSupportedException`); two equal `SemaphoreSlim`s as `T` (today, under the JIT: a violation `AvailableWaitHandle, m_waitHandle`, a handle created on each side); two pending `Task<int>`s as `T` (today: blocks for more than 3 s); a user class with a throwing getter (today: `TargetInvocationException`, also when nothing changed). Today's comparison also changes the user's objects: after one call, a `++_reads` getter had run once on `actual` (`_reads = 1`), and a lazy getter had created its list on `actual`. Fields-only changed neither.

**Cost.** For a `Person` with 5 auto-properties and a `List` of 10 items with 2 auto-properties each, compared with its `Old` copy, under the JIT with tiered compilation off: 5296–5555 ns and 5296 bytes per call today, 4108–4309 ns and 4464 bytes per call with fields-only, about 21 % faster and 16 % less allocation. Native AOT was not benchmarked.

**The unit tests.** With fields-only and today's pattern rule, 1077 of the 1239 tests passed and 162 failed. 135 of the failures were the message change "two entries become one" (`Items` and `<Items>k__BackingField` become `<Items>k__BackingField`), which no pattern or name rule fixes. 2 were anchored patterns on an inherited public auto-property that no longer excused it (`^Name$`, `^Version$`). Others were a property reported as its backing field (5), member-listing tests (13), an exception no longer thrown (6, of which 5 by design and 1 a missed change) and the replaced `CancellationTokenSource` (1).

**Three pattern rules.** The spike compared matching each pattern against the raw name only (today), against the raw name or the source name (rule a), and against the source name only (rule b). The source name of a compiler-generated backing field `<Email>k__BackingField` is `Email`; any other name is its own source name.

- With today's comparison, rule (a) and rule (b) each passed 1239 of 1239 unit tests.
- Measured in fields-only mode, on an auto-property `Email`: `^Email$` and `^E` were excused under (a) and (b), not under the raw rule; `^<Email>k__BackingField$`, `k__BackingField` and `Field$` were excused under the raw rule and (a), not under (b); `^(Email|<Email>k__BackingField)$` was excused under all three. Rule (a) matches everything that rule (b) matches, and keeps every pattern that the raw rule excuses.
- A hand-written field `_email` behind a property `Email` is its own source name, so `^Email$` does not excuse it under any rule.

**Source names in violation entries.** In fields-only mode, showing each member by its source name made 5 more unit tests fail, each expecting a raw name. The spike also found a display collision: a public field `X` declared on a derived class and a private auto-property `X` declared on its base class have different raw names (`X`, `<X>k__BackingField`), so they are not qualified, and with source names both entries read `X`. The message would then not say which of the two changed.

---

## Decision

**`EnsureAssignable<T>()` keeps comparing the public properties and the instance fields; fields-only comparison is not adopted. A pattern is now also matched against the property name of a compiler-generated backing field. Violation entries keep the raw names.**

The maintainer stated the goal for this decision as: "應該不想要讓使用者對於行爲感到驚訝吧" (in English: users should not be surprised by the behaviour). The maintainer was shown the measurements above in summary (fields-only misses every inherited auto-property under Native AOT, with 10 smoke checks failing; it also misses changes under the JIT when `T` is a base class or an interface; it removes the hazard getters; the pattern rule passes all unit tests on today's comparison), and chose the option "不改；ADR + pattern 規則 (a)", described as: "維持屬性加欄位；寫 ADR 記下量測與不改的理由（含 getter 會修改使用者物件的已知限制）；這張順便實作 pattern 同時比對原始名與屬性名，解掉 #40 item 7 的 auto-property 部分". The two other options were "不改；只寫 ADR 關卡" and "改成只比欄位".

Keeping raw names in violation entries was part of the plan the maintainer approved, not a reason the maintainer gave.

### Details

1. **Members compared: unchanged.** The member list, the trimming annotations, and the order in which members are compared stay as [ADR-0023](0023-comparison-of-base-class-members.md) left them.
2. **Pattern rule (a).** `MemberComparison.IsAssignable` matches each pattern against the raw name of the member. When the member is a compiler-generated backing field, that is, when `MemberComparison.SourceName` turns its raw name `<X>k__BackingField` into `X`, the pattern is also matched against `X`. A member is excused when any pattern matches either name. For every other member, `SourceName` returns the raw name, and the rule is the same as in 2.0.0. The declaring type of a qualified member still takes no part in matching. Patterns still apply to top-level members only.
   - An auto-property declared on `T` is excused, as the property and as its backing field, by one anchored pattern on its name: `^Email$`.
   - An auto-property that is not public, declared on `T` or on a base class, is compared as its backing field only and is now excused by `^Email$` too. `^<Email>k__BackingField$` still excuses it.
   - Every pattern that excused a member in 2.0.0 still excuses it.
   - For an explicit interface auto-property, the compiler names the backing field `<Ns.IFoo.Id>k__BackingField`, so its source name is `Ns.IFoo.Id`. `Id$` and `^Ns\.IFoo\.Id$` excuse it; `^Id$` does not (measured with a probe program on this branch).
3. **Violation entries keep raw names.** An entry still names a backing field as `<Email>k__BackingField`, and the text before its first space is still a pattern that excuses the member. Entries of the display collision above stay distinguishable: `X` and `<X>k__BackingField`.

### Compatibility

The `[Unreleased]` section of `CHANGELOG.md` holds one **BREAKING** bullet that ends `(#46; ADR-0025)`. The rule widens what a pattern excuses: compared with 2.0.0, a pattern that matches the property name but not the raw name of a backing field (`^Email$`, `^E`, `Email$`, `Id$`) now also excuses the backing field of a public auto-property declared on `T` (2.0.0 excused the property and reported the backing field), of an auto-property that is not public, declared on `T`, and of an explicit interface auto-property declared on `T`. 2.0.0 compared those backing fields and reported a change. Of an auto-property that is not public (or is explicitly implemented), declared on a base class, 2.0.0 compared nothing: the backing field is a private field of the base class, which 2.0.0 did not list (`GetFields` of the compared type only). [ADR-0023](0023-comparison-of-base-class-members.md) (#40, also unreleased) compares it; such a pattern excuses it relative to that behaviour. A call that failed in 2.0.0 can now pass. No call that passed in 2.0.0 fails because of this rule.

---

## Consequences

### Positive Consequences

- ✅ Under the JIT and under Native AOT, the same members are compared as before this ADR, and the JIT and Native AOT still agree on inherited public auto-properties, because the public property is preserved and read. An inherited auto-property that is not public is still compared only under the JIT, unless the base class is preserved (smoke check 96; see [ADR-0023](0023-comparison-of-base-class-members.md), Known Limitations).
- ✅ One anchored pattern on the property name (`^Email$`) excuses an auto-property, declared on `T` or on a base class, public or not. An explicit interface auto-property is the exception: its source name includes the interface (`Ns.IFoo.Id`). This settles the auto-property half of #40 item 7.
- ✅ No pattern that worked in 2.0.0 stops working.
- ✅ Violation entries are unchanged.

### Negative Consequences

- ❌ **Breaking**: some patterns excuse more members than in 2.0.0 (see Compatibility).
- ❌ The getter hazards of #46 remain (see Known Limitations).

### Neutral Consequences

- ⚖️ No public API signature changed, and the trimming annotations did not change.
- ⚖️ The cost per call stays as it was; the spike measured fields-only as about 21 % faster under the JIT.

---

## Known Limitations

- **Getters still run on both sides, and can change the user's objects.** Each compared public property is read on `actual` and on `expected`. A getter that fills a lazy cache, counts reads or logs does so in every call: the spike measured `_reads = 1` on `actual` after one call, and a lazy list created on `actual`. A top-level member that a pattern excuses is not read: `Contract._FindDifferences` skips it before it calls `GetValue`.
- **Hazard getters of types that are not shared still run.** `Old<T>()` shares `MemoryStream`, `SemaphoreSlim`, `Task` and the other types that [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md) lists, and a member that holds one is compared by reference, so their getters are not reached below the top level. They run when such a type is `T` and two different instances are passed (two equal `MemoryStream`s as `T` end in `TargetInvocationException`, two pending `Task<int>`s block), and for every type that is not shared: an `Encoding` held by a member ends in `NotSupportedException` under the JIT, and a user getter that throws ends in `TargetInvocationException`, also when nothing changed. Ways out: list the top-level member as assignable, or check what counts with `Contract.Ensure`.
- **A hand-written backing field needs its own pattern.** A field `_email` behind a property `Email` is not compiler-generated, so `^Email$` does not excuse it: the call reports `_email` (unit test `EnsureAssignable_WhenAHandWrittenFieldBehindAPropertyChangesAndThePropertyNameIsAssignable_ReportsTheField`). This half of #40 item 7 stays open.
- **A pattern excuses every member of the names it matches, and there are more such names.** A pattern that matches `X` excuses a field `X`, a property `X` and the backing field `<X>k__BackingField` of every level. Of a public field `X` declared on a derived class and a private auto-property `X` declared on its base class, `^X$` excuses both, and no pattern excuses the field alone (measured with a probe program on this branch); `^<X>k__BackingField$` excuses the auto-property alone.
- **The limits that fields-only would have removed stay.** A property that returns a new instance of its own type on every read (`DirectoryInfo.Root`) still makes the comparison grow without bound, and the statements in ADR-0022 that defer work to #46 have no follow-up there.

---

## Alternatives Considered

### Alternative 1: Compare instance fields only

**Description**: List the instance fields of each compared type and no properties; show a backing field by the property name.

**Pros**:
- Removes every getter hazard that the spike measured, and no longer changes the user's objects.
- About 21 % faster and 16 % less allocation per call in the spike's benchmark (JIT).
- The annotation on `T` could drop `PublicProperties`: the smoke program built with `-warnaserror` without trim warnings, and failed the same 10 checks as with it.

**Cons**:
- Misses changes when `T` is a base class or an interface (#40 item 2), in private fields of a framework base class (`Exception.Data`), and in state kept outside the object, under the JIT.
- Under Native AOT misses every inherited auto-property, because the private backing field of a base class is not preserved; 10 smoke checks fail. Recovering them would need the base classes preserved and the "no members visible" rule to count base private fields; smoke check 29 shows that a preserved base still fails that rule.
- Reports lazy caches that the getters used to fill on both sides.
- Changes the text of most violation entries (135 unit tests).

**Why rejected**: The maintainer chose not to change what is compared (see Decision). The measured consequences of this alternative: changes that 2.0.0 reported would pass in silence, and the JIT and Native AOT would disagree on every inherited auto-property.

---

### Alternative 2: Keep the comparison and record the decision only

**Description**: Write this ADR and leave the pattern rule as it was.

**Why rejected**: The maintainer chose the option that also implements rule (a). Without it, an anchored pattern for an auto-property declared on `T` still has to name both forms; with it, no unit test failed on today's comparison.

---

### Alternative 3: Match patterns against the source name only (rule b)

**Description**: Match each pattern against `SourceName(raw name)` alone.

**Why rejected**: It breaks patterns that excuse a backing field by its raw form only, which 2.0.0 matched and so could be written against it: `^<Email>k__BackingField$` for an auto-property that is not public, declared on `T` (the documentation has also recommended this pattern since #40), and `k__BackingField` for every backing field. The pattern that the 2.0.0 documentation recommended, `^(Email|<Email>k__BackingField)$`, would still work under rule (b), because the property name `Email` matches it. Rule (a) matches everything rule (b) matches and keeps all of these patterns.

---

### Alternative 4: Show source names in violation entries

**Description**: Name a backing field in an entry by its property name, `Email` instead of `<Email>k__BackingField`.

**Why rejected**: Of a derived field `X` and a base private auto-property `X`, both entries would read `X`, and the message would not say which one changed. Under rule (a) the raw name in the entry is still a pattern that excuses the member, so nothing is lost by keeping it.

---

## Related Decisions

- **Amends**: [ADR-0023 - Comparison of Members Declared on Base Classes in EnsureAssignable<T>()](0023-comparison-of-base-class-members.md) — its statements that patterns are matched against the raw name only no longer hold; its decisions stand. Carries an amendment.
- **Amends**: [ADR-0021 - Postcondition Helpers under Trimming and Native AOT](0021-postconditions-under-trimming-and-aot.md) — its statement that `^Customer$` skips only the property no longer holds; smoke check 108 is added. Carries an amendment.
- **Amends**: [ADR-0022 - Faithful Old<T>() Copies and Robust EnsureAssignable<T>() Comparison](0022-faithful-old-copies-and-robust-comparison.md) — D9 "Members compared" stays unchanged by this decision, the statement of its #40 amendment on patterns no longer holds, and the work it defers to #46 is not done there. Carries an amendment.
- **Related to**: [ADR-0007 - Reflection and Field Comparison for EnsureAssignable<T>()](0007-reflection-field-comparison.md) — the comparison of properties and fields that this ADR keeps.

---

## Implementation Notes

This ADR was written after the implementation so that it could record what the work turned up.

- **Where it lives.** `MemberComparison.IsAssignable` holds the rule; `MemberComparison.SourceName` gives the source name, and only a name that starts with `<` and ends with `>k__BackingField` is turned into another name. The comment on `MemberComparison.CannotCompareNested` was updated with it: the advice of a "cannot compare" message names `SourceName` of the top-level member, which excuses it.
- **Unit tests.** `EnsureAssignableBaseClassFieldTests` holds four: an auto-property declared on `T` excused by `^P$`, property and backing field; a private auto-property of a base class excused by `^PrivateNote$`; the same excused by its raw name `^<PrivateNote>k__BackingField$`; a hand-written field `_email` still reported with `^Email$`. 1243 unit tests passed on this branch; that figure is a snapshot.
- **Native AOT.** Smoke check 108, "Auto-property changed, assignable by its property name", changes the auto-property `P` that `KeptCounterDerived` overrides and passes `^P$`; it expects no exception, with postconditions on and with `DBC_POST=off`. Measured on `win-x64` with the Native AOT publish of the default build (`-warnaserror`): 108 `PASS`, no `FAIL`, with postconditions on and with `DBC_POST=off`. The CI workflow had not run for this change when this ADR was written.
- **A probe program** on this branch, under the JIT, confirmed the explicit interface case (`<Ns.IFoo.Id>k__BackingField`: excused by `Id$`, reported with `^Id$`), an auto-property that is not public declared on `T` (excused by `^Email$`, `^E` and `Email$`), a nested member (`^Email$` does not excuse `Holder.C.Email`; `^C$` does), and the display collision of Known Limitations.

---

## References

- [Issue #46: Evaluate comparing fields only in `EnsureAssignable<T>()`](https://github.com/cwouyang/uContract.NET/issues/46)
- [Issue #40: `EnsureAssignable<T>()` comparison gaps that exist in every build](https://github.com/cwouyang/uContract.NET/issues/40)
- [`MemberComparison.cs`](../../src/uContract/MemberComparison.cs) — `IsAssignable` and `SourceName`
- [`Contract.cs`](../../src/uContract/Contract.cs) — `EnsureAssignable<T>()` and its XML documentation
- [`EnsureAssignableBaseClassFieldTests.cs`](../../tests/uContract.Tests/EnsureAssignableBaseClassFieldTests.cs) — the unit tests
- [`tests/uContract.AotSmoke/Program.cs`](../../tests/uContract.AotSmoke/Program.cs) — smoke check 108
- [`CHANGELOG.md`](../../CHANGELOG.md) — the `[Unreleased]` bullet
- [ADR-0007](0007-reflection-field-comparison.md), [ADR-0021](0021-postconditions-under-trimming-and-aot.md), [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md), [ADR-0023](0023-comparison-of-base-class-members.md)

---

## Revision History

| Date       | Status      | Notes                          |
|------------|-------------|--------------------------------|
| 2026-11-02 | Accepted    | Decision recorded after implementation (#46). Amends ADR-0021, ADR-0022 and ADR-0023. |
