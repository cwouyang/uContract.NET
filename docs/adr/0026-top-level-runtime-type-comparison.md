# ADR-0026: EnsureAssignable<T>() Compares the Runtime Type at the Top Level

## Status

**Accepted**

- **Date**: 2026-11-02
- **Deciders**: Project maintainer (cwouyang)
- **Status Date**: 2026-11-02
- **Amended**: 2026-11-08 — on SDK 8.0.427 a one-element collection expression is built as another class; see the Amendment under Implementation Notes
- **Amended**: 2026-11-10 — an inline array at the top level is compared as a whole, element by element, whatever `T` is, and the patterns do not apply to it; it is no longer "decided by `Equals` alone" (#84); see the second Amendment under Implementation Notes

---

## Context

### Problem Statement

`Contract.EnsureAssignable<T>()` is a postcondition: "nothing changed except the assignable members". On master at `70d7083`, and in 2.0.0, the top level compared only what the declared `T` shows (`Contract._FindDifferences` read the metadata of `typeof(T)`), while every nested member and element was compared by its runtime type (R0 of [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md)). So the same object was checked fully when it was a member, and not at all, or only in part, when it was passed as `T`:

- `T` = `object`: nothing was compared beyond `null`, also for two different strings or two arrays with a different element.
- `T` = an interface: only the properties that the interface itself declares were compared (`IReadOnlySet<T>` declares none).
- `T` = a base class: what the runtime type adds was not compared. This covers the documented pair `EnsureAssignable(this, Contract.Old(() => this))` written in a base class and run on a subclass instance.

Each such call passed silently. [Issue #40](https://github.com/cwouyang/uContract.NET/issues/40) lists this as item 2. Item 3 (the own members of a class that is also enumerable; dictionary values; members typed as a non-enumerable interface) is the nested side of the same question.

### Relevant Context

- Earlier changes took parts of item 2: a `null` side, a `string`, a delegate type and a nullable value type as `T` (#52); one changing shared instance on both sides (#54); a frozen collection as `T` (#49); an array type or `System.Array` as `T` (#57). Each applied only when `T` itself was such a type. The rest of item 2 is this ADR.
- Two throwaway spikes measured the options on master at `70d7083`, on one machine (.NET SDK 8.0.204, `win-x64`), under the JIT and under Native AOT. Nothing of them was committed. Their reports, source diffs, probe programs and result files are archived outside the repository at `C:\Users\cwouyang\Desktop\Dev\personal\sdd-archive\uContract.NET-40-item2-runtime-type\spike\` (`spike-40-report.md`, `spike-40b-report.md`). Row ids below (O1, BA1, F3, …) refer to those reports. The spikes measured variants close to the final design, not the design itself.
- Spike 1 measured three variants: R (compare the runtime type; under Native AOT throw when its members are hidden, "R-throw"), R-fallback (the same, but fall back to the metadata of `typeof(T)` when the runtime type's members are hidden), and R-seq (R, and compare a top-level sequence by its elements only).
- Spike 2 measured five rules (H1 to H5) for a runtime type that Native AOT does not preserve but whose inherited members still show.
- Under Native AOT, an instantiated type that nothing preserves keeps no reflection metadata at all, not even a constructor (spike 2, section 1). Its inherited members can still show, through a base class that something preserves.
- The trimming analyzer gives no signal that the top level reflects on a runtime type that the annotation on `T` does not preserve (spike 1, "Trim / AOT warnings").

### Constraints

- No public API signature change; the `PublicAPI.*` baseline files are unchanged. `[DynamicallyAccessedMembers]` on `T` stays as it is.
- No new dependency ([ADR-0011](0011-zero-dependency-principle.md)).
- The Native AOT smoke program builds with `-warnaserror`; no new trimming suppression beyond the existing `IL2070` pattern without a written reason.
- The work targets 3.0.0 and lands under `[Unreleased]` in `CHANGELOG.md`.

---

## Decision

**The top level of `EnsureAssignable<T>()` compares the two values by their runtime type, with the rules a member pair follows. Under Native AOT, a runtime type other than `T` that shows no declared member makes the call throw, also when the two values are equal. A sequence is compared by the class chain of its runtime type, at the top level and below it.**

The maintainer's rule for this decision is: "以為有檢查其實沒檢查比較糟" (a check that silently compares nothing is worse than a check that starts to fail after an upgrade). When the library cannot compare, it says so.

The maintainer chose these options; each label is quoted verbatim:

1. Direction: "照實際型別，AOT 大聲失敗" — compare the runtime type with the rules of a member; under Native AOT a runtime type whose members cannot be seen throws, also for equal values; the fix is `DynamicDependency`.
2. A base class as `T` under Native AOT, which stayed silent in spike 1: measured in spike 2, then "採用 H3".
3. Collections: first "和成員一樣逐元素". Spike 2 showed that this drops a collection subclass's own field (smoke check 100, `CodedCollection.Code`), so it was replaced by "元素＋自己成員，巢狀也一起改".
4. After a check of the design by its builder:
   - a framework collection held by an `object` or interface `T` under Native AOT: "套 H3，大聲失敗";
   - nested values under Native AOT: "使用者型別套 H3";
   - two sequences of different runtime types, one with state of its own: "算違反";
   - a class of yours that implements `IEnumerable<T>` itself, with no framework collection as a base: "只比成員".
5. During the implementation, each answer changed the design:
   - an interface `T`: "合併：也比 interface 的屬性" — the properties that the interface declares are compared too, read through it, because an explicitly implemented property is not a public property of the runtime type and a value type's `Equals` can ignore a property;
   - a framework class as `T`: "走訪到 T 為止" — the private fields of the base classes are walked up to and including `T`, because `EnsureAssignable<Random>(new MyRandom(1), new MyRandom(2))` reported `_impl` on master and would otherwise pass;
   - the public state of a framework sequence at the top level: "公開屬性照比，排除 Capacity";
   - a `yield` iterator: "當成序列逐元素比";
   - a sequence that is not a collection at the top level: "頂層非集合序列拋錯", refined by "只在實際型別≠T 時拋錯";
   - a framework property without a getter at the top level: "跳過沒有 getter 的框架屬性";
   - a top-level `yield` iterator from any assembly that is not part of .NET: "寫進文件，維持列舉";
   - a runtime type that something partly preserves under Native AOT: "接受為已知限制，寫進 ADR";
   - a `yield` method that returns only an enumerator: "接受延後，spec 用詞收窄並寫進文件".
6. The final whole-branch review found that the class a C# 12 collection expression is built as when it is held as an interface (`IReadOnlyList<int> items = [1, 2, 3]`) was compared by its fields (`_items`), not by its elements, and so was unequal to a `List<int>` of the same elements, and threw "cannot compare" under Native AOT. On 2026-11-03 the maintainer answered "擴大成 IEnumerable，逐元素比": the rule for a `yield` iterator is widened to every compiler-generated class that implements `IEnumerable` or `IEnumerator`, and messages that would ask for a `typeof` that C# cannot write name none.
7. That widening rested on a premise that was measured false: the collection-expression classes carry no `[CompilerGenerated]` on SDK 8.0.204, so the first implementation also accepted any class with a `<` in its name, and the reviews of 2026-11-03 found two stateful classes of yours that then passed silently where `019baa9` reported them: a `[CompilerGenerated]` class that implements only `IEnumerable` (a source generator's; a changed `Tag` with equal elements) and a C# 11 `file` class (metadata name `<File>F…__FileBag`; a changed `Extra`, and its enumerator was run). On 2026-11-03 the maintainer answered "收窄，連同 Minor 一起修" (spec amendment 7): a compiler-generated sequence is a class marked `[CompilerGenerated]` that implements `IEnumerator` (a `yield` iterator), or a class whose metadata name starts with `<>` that implements `IEnumerable` (`<>z__ReadOnlyArray<T>`, `<>z__ReadOnlyList<T>`); every other class keeps rule 2. The messages follow the same line: a type C# can name in `typeof` (a source-generated class, a `file` class inside its file) keeps the `typeof` fix, and the ways out match why a type cannot be named.
8. The risk review of round 2 found that the `IEnumerator` clause still let a source-generated class through: a `[CompilerGenerated]` class with a name C# can write that is its own enumerator (a cursor with a `Tag`) was compared by its elements and a changed `Tag` passed silently. On 2026-11-03 the maintainer answered "開第 3 輪，全部修" (spec amendment 8): the `IEnumerator` clause also requires a name C# cannot write, a `<` (C# `yield`: `<M>d__N`) or a `$` (VB iterators: `VB$StateMachine_…`). "Compiler-generated" now means the same in the rule and in the messages (`CSharpTypeName.HasCompilerPrefix` and `IsMarkedWithUnwritableName`). The `typeof` fix for a `file` class says that the method must be inside the file that declares the class; a type with a compiler-generated type deeper inside its generic arguments (`Dictionary<int, List<anonymous type>>`) is worded as such.
9. The risk review of round 3 found that the iterator of a local function that captures a variable was still compared by its fields. The compiler nests it in the display class that holds the captured variables (`<>c__DisplayClass0_0+<<Upto>g__Impl|0>d`), and the attribute sits on the display class, not on the iterator (measured, SDK 8.0.204). `EnsureAssignable(Holder{Items=Upto(3)}, Holder{Items=List{1,2,3}})` reported "Fields were modified", and `EnsureAssignable<IEnumerable<int>>(Upto(3), List{1,2,3})` reported "different runtime types", where a plain `yield` iterator passes. On 2026-11-03 the maintainer answered "開第 4 輪，連同剩下的 Minor" (spec amendment 9): the `IEnumerator` clause looks for the attribute and the unwritable name on the type or on a type in its declaring chain, the definition the messages use (`CSharpTypeName.IsCompilerGenerated`). A type whose generic arguments hold `file` classes of more than one file cannot be written in any one file, so its message names no `typeof`.

### Details

"Runtime type" is `actual.GetType()`, after `null` has been decided. A "framework class" is one that `MemberComparison.IsFrameworkClass` accepts: its assembly is signed with a public key token of the .NET 8 shared frameworks ([ADR-0023](0023-comparison-of-base-class-members.md)). Steps 1 to 4 of `EnsureAssignable<T>()` stay as they are and in that order: the pattern-array check, the recursion guard and the postconditions switch, `null` as a value, and one changing shared instance on both sides. A `T` that is compared as a whole (a `string`, a delegate type, a nullable value type, a frozen collection, an array type or `System.Array`) keeps its rule. A nullable value type as `T` keeps its rule and messages, because a boxed `int?` has the runtime type `int`.

1. **The runtime type decides** (`Contract._CompareByRuntimeType`, `_CompareByMembers`).
   - A runtime type that is a `string`, a delegate type, a `FrozenSet<T>` or a `FrozenDictionary<TKey, TValue>`, or an array type is compared as a whole, also when `T` is `object`, an interface or a base class. The patterns do not apply and are not examined. Two frozen collections of one public type are compared as a whole also when the framework built them as different internal classes; a message names the public type.
   - Two different instances of a type that `Old<T>()` shares, other than those above and a boxed pointer, are a violation by reference, whatever `T` is and whatever their state. This also changes `T` = such a type itself (two `MemoryStream`s as `T` = `Stream`, two `Regex`es). One instance of a shared, fixed type on both sides (a `Regex`) keeps master's path: the members of `T` are compared. `T` = `System.Reflection.Pointer` keeps its comparison by members; under another `T` two boxed pointers are compared by address.
   - Two values of different runtime types are a violation that names both runtime types and `T`, except two sequences (rule 3 below) and the value-type sequences of the next bullet.
   - **A value-type sequence keeps the declared `T`** (`Contract._TopLevelType`, `_KeepsTheDeclaredType`). When the runtime type of either value is a value type that implements `IEnumerable` (`ImmutableArray<T>`, `ArraySegment<T>`, a struct of yours), and the two values are not two sequences of different runtime types, the top level keeps master's rule: it compares the members visible through `T`. `T` = `object` compares nothing beyond `null` (`EnsureAssignable<object>(ImmutableArray.Create(1, 2), ImmutableArray.Create(1, 3))` passes), an interface its own properties, a base class its members, and the struct as `T` its members. This holds for two values of one such runtime type, and for such a value against a value that is neither a sequence nor compared as a whole or by reference: that pair of different runtime types is not reported as such. Against a `string`, a delegate or a shared type that is not a sequence, the pair is a different-runtime-types violation (`_CompareByRuntimeType`). The Native AOT rule of item 4 does not apply on this path. Two value-type sequences of different runtime types, or one against a reference-type sequence (an array, a `List<T>`, a frozen collection), take rule 3. There a value-type sequence that is not a collection (a struct of yours that implements only `IEnumerable`) throws `InvalidOperationException` as a sequence that is not a collection: `System.ValueType` is a framework class in its chain (`MemberComparison.IsNonCollectionSequence`, checked by `_ThrowIfNotACollection` before the elements). The spec kept this rule on purpose ("value-type sequences … keep today's rule"); see Known Limitations.
   - A boxed value type other than `T`, other than such a sequence, is compared as a member is: its `Equals` first, then its fields. A primitive, an enum and an inline array are decided by `Equals` alone.
   - Any other value is compared by the members of its runtime type: public instance properties, instance fields, and the private fields of its base classes up to the first framework class. When `T` is a framework class other than `object` that the runtime type derives from, the walk goes up to and including `T`.
   - When `T` is an interface, the properties that it declares are compared too, read through it, after the members of the runtime type. One of them is not reported when a member of its name was reported already.
   - When the runtime type is not `T`, a violation message adds the line "Members were listed from the runtime type X, not from T (Y).", except for a compiler-generated sequence (rule 3), which is compared by its elements and of which no member is listed.
2. **Patterns** apply to the top-level members that the comparison walks, listed from the runtime type, with the rules of [ADR-0025](0025-fields-only-comparison-not-adopted.md). A pattern written against `T` still excuses that member. A member that only the runtime type has is excused only by a pattern that names it. Elements are never excused.
3. **Sequences** (an `IEnumerable` reference type other than a `string`, an array, a frozen collection and a shared type) are compared by the class chain of the runtime type, at the top level and below it (`MemberComparison.GetSequenceRule`):
   - **The chain holds a framework class below `object`** (`List<T>`, `Collection<T>`, `Dictionary<TKey, TValue>`): compared by its elements (a dictionary entry by entry, in enumeration order) and by the members declared on its classes that are not framework classes. The framework's non-public fields are not compared, so two lists of equal content and different capacity or history are equal. At the top level the public properties declared on its framework classes are compared too, except one named `Capacity` and one without a getter that `T` does not declare or inherit. An element difference at the top level is reported as the entry `(the elements; assignable patterns do not apply to them)`.
   - **No framework class in the chain** (a class of yours that implements `IEnumerable` itself): compared by its members only, as any other class, and not enumerated, at every level.
   - **A compiler-generated sequence** of an assembly that is not part of .NET (`MemberComparison.IsCompilerGeneratedSequence`, narrowed by amendments 7 and 8 and corrected by amendment 9: a class that implements `IEnumerator` and is compiler-generated: it, or a type in its declaring chain, is marked `[CompilerGenerated]` with a `<` or a `$` in its name or has a name that starts with `<>` (`CSharpTypeName.IsCompilerGenerated`; the iterator of a local function that captures a variable, `<<M>g__Impl|0>d`, is not marked itself and is nested in its marked display class `<>c__DisplayClass…`, measured on SDK 8.0.204), or a class whose metadata name starts with `<>` that implements `IEnumerable`; reached as an `IEnumerable`): compared by its elements alone, at every level, also against a sequence of another runtime type that is compared by its elements alone. It is a `yield` iterator, whose fields are the state of the iteration, or the class of a collection expression held as `IReadOnlyList<T>`, `IReadOnlyCollection<T>` or `IEnumerable<T>`, whose fields store the elements. The .NET 8 SDK builds that class as `<>z__ReadOnlyArray<T>`, also for one element, and as `<>z__ReadOnlyList<T>` for a spread of a `yield` iterator, without `[CompilerGenerated]` (measured: reflection under the JIT, the Native AOT publish of this branch for `<>z__ReadOnlyArray<T>`, and a unit test that pins the name `<>z__ReadOnlyList`1`), so the name decides; an empty collection expression held as `IReadOnlyList<T>` is an empty array (measured). A `[CompilerGenerated]` class with a name C# can write (a source generator's, one that implements only `IEnumerable` or one that is its own enumerator) and a C# 11 `file` class (`<File>F…__Name`) are classes of yours and take the rule above (measured: unit tests at `T`, `object` and nested, and smoke checks 145 to 147). A capturing local iterator is compared by its elements nested, at `T` = `IEnumerable<int>` and at `T` = `object`, also when its captured state is a delegate (unit tests; smoke checks 148 to 152, where under Native AOT reflection lists no instance field for it).
   - **Not a collection, at the top level.** A sequence whose chain holds a framework class and that implements none of `ICollection`, `ICollection<T>` and `IReadOnlyCollection<T>` (a LINQ query, `GetConsumingEnumerable()`, an `XmlDocument`) is never enumerated at the top level. When its runtime type is not `T`, the call throws `InvalidOperationException` and asks for a materialised collection (`ToList()`) or for the value's own type as `T`. Passed as its own type, it is compared by all its members, as on master. Below the top level it is enumerated, as on master.
   - **Two sequences of different runtime types** are compared by their elements only when neither runtime type declares members on a class that is not a framework class. Otherwise they are unequal; at the top level that is a violation that names both runtime types. With equal elements, the properties of an interface `T` are then compared.
4. **Native AOT: a runtime type that shows no declared member** (`MemberComparison.ShowsNoDeclaredMember`, rule H3). "Shows no declared member" means that reflection lists no field, property, method or constructor that the type declares (`DeclaredOnly`, public and non-public, instance and static). A nested type and an event do not count. The rule runs only when dynamic code is not supported.
   - **Top level, runtime type ≠ `T`**, not compared as a whole: the call throws `InvalidOperationException`, also when the two values are equal. A framework collection is covered too (a `Queue<int>` or a `List<int>` held by `object`, when nothing preserves it). A value type other than `T` asks `Equals` first and throws only when `Equals` reports the values unequal; a primitive, an enum and an inline array need no metadata and are not covered, nor is a value-type sequence that keeps the declared `T` (item 1). Two sequences of different runtime types ask no `Equals`, so there a value type throws as a class does. A compiler-generated sequence is not covered: enumerating it needs no metadata. Measured: under Native AOT `<>z__ReadOnlyArray<int>` shows no declared member.
   - **Nested values**: a class that is not a framework class and shows no declared member is handled as a class with no visible members (R8 of ADR-0022): equal when its `Equals` returns `true`, "cannot compare" otherwise. For a sequence whose chain holds a framework class, the same test applies to each class of yours in the chain. A framework class reached as a nested value keeps master's behaviour.
   - **Runtime type = `T`**: the top level is unchanged; the annotation on `T` preserves `T`.
   - The message names the runtime type and `T`, and gives the fix with the runtime type in C# syntax: `[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(<type>))]` on a method that runs (measured to work: spike 1 A1–A3o, spike 2 F5 and F6aP), or, when both values are of that type, passing them as that type. It does not suggest `[DynamicallyAccessedMembers]` on a base type of yours, which does not help (spike 1 A4–A6). The nested "cannot compare" messages now name the type in C# syntax too (`CSharpTypeName`). For a type that C# cannot write in `typeof` (`CSharpTypeName.CanBeWritten`, as amended 2026-11-03: a compiler-generated type, `CSharpTypeName.IsCompilerGenerated`, whose metadata name starts with `<>` (an anonymous type) or which is marked `[CompilerGenerated]` and has a `<` or, since amendment 8, a `$` in its name (the enumerator of a `yield` method that returns only `IEnumerator<T>`; a VB iterator, tested with an emitted type), or is nested in one; or a type with such a type among or inside its generic arguments, or as its element type; or, since amendment 9, a type whose generic arguments hold `file` classes of more than one file, told apart by the `<File>F{checksum}` prefix of their metadata names), these messages, "no properties or fields are visible" for a runtime type other than `T`, and the message for a property without a getter name no `typeof`. They say why: "The type is compiler-generated and cannot be named in typeof.", "The type has a compiler-generated type among its generic arguments and cannot be named in typeof." or, when it is deeper (`Dictionary<int, List<anonymous type>>`), "… inside its generic arguments …"; for `file` classes of several files (and no compiler-generated type), "The type has file classes of more than one file in its generic arguments, and C# names a file class only inside the file that declares it, so no one file can name the type in typeof.", with the way out "use types that are not file classes, or file classes of one file, as its generic arguments". They then offer what can be written: the top-level member as assignable where one leads to the value; for a compiler-generated type a value of a type of yours (for an enumerator, returning `IEnumerable<T>` from the `yield` method); for a type with a compiler-generated argument, an element type of yours when it is a sequence with one generic argument (`List<anonymous type>`) and a type of yours as its generic argument otherwise; for one deeper inside, a type of yours in place of the compiler-generated one; and `DBC_POST=off`. No `ToList()` is offered: no value that reaches these messages is a sequence that materialising would help. A type that can be written keeps its message: a `[CompilerGenerated]` class with a name C# can write (a source generator's), and a `file` class, which `CSharpTypeName.Of` names by its own name, with the words "on a method that runs inside the file that declares FileTicket" (`CSharpTypeName.WhereItCanBeWritten`; all five messages, unit-tested). A type with `file` classes of one file in its generic arguments keeps that text too (unit-tested with `Tuple<FileTicket, FileMarker>`).

### Compatibility

The `[Unreleased]` section of `CHANGELOG.md` holds one **BREAKING** bullet that ends `(#40; ADR-0026)`. Compared with master at `70d7083` and with 2.0.0:

- Calls with `T` = `object`, an interface or a base class compare what they skipped, and can start to fail. A documented example such as `EnsureAssignable(this, Contract.Old(() => this))` keeps working under the JIT, and on a subclass instance it compares the subclass's state too.
- Under Native AOT such calls throw "cannot compare" until the runtime type is preserved, also when nothing changed. This includes `EnsureAssignable(this, Contract.Old(() => this))` written in a base class and run on a subclass instance, and a framework collection passed as `object` that nothing preserves.
- Under Native AOT, a nested object of a type of yours that nothing preserves and that only inherits visible members ends in "cannot compare" when its `Equals` reports the values unequal, instead of a partial comparison.
- A framework collection as `T` (`List<int>`) is compared by its elements and its public properties: a pattern that excused its private fields (`_items`) no longer applies, and a change of capacity alone is no longer a violation.
- A nested class of yours that implements `IEnumerable<T>` itself is compared by its members, not its elements.
- Two different instances of a shared type are a violation also when `T` is that type.
- Downstream: ezDDD.NET has no `EnsureAssignable` or `Old` call (spike 1, M7). The documentation examples pass the concrete type.

---

## Consequences

### Positive Consequences

- ✅ Under the JIT, the cases that master passed silently with `T` = `object`, an interface or a base class are compared (spike 1, M1 rows).
- ✅ Under Native AOT, each such case is either compared or ends in "cannot compare" with a fix that was measured to work. No case that master reports passes silently, apart from the known limitations below.
- ✅ Two collections of equal content and different capacity or history are equal at the top level too (spike 1, O5d, O5e, I2c, L2).
- ✅ #40 item 3 is closed: the own members of a collection class are compared at every level, and its other two parts (dictionary values, members typed as a non-enumerable interface) were already compared by the full rules on master (`DictionaryWalk`, and `MemberComparison.Start` by runtime type).

### Negative Consequences

- ❌ **Breaking** (see Compatibility).
- ❌ Under Native AOT, a call that passed for equal values can throw until the user adds `DynamicDependency`. A stateless subclass that nothing preserves throws too (false positive, below).
- ❌ More getters run: those of the runtime type's public properties, and of an interface `T`'s properties. A getter that throws ends the call in `TargetInvocationException`, also where comparing by `T` alone passed.

### Neutral Consequences

- ⚖️ No public API signature and no trimming annotation changed. The library adds `IL2070` suppressions in the existing pattern on the new reflection helpers (`MemberComparison.GetTopLevelMetadata`, `IsCollection`, `ListsNoDeclaredMember`), each with a justification.
- ⚖️ When the runtime type equals `T`, the cost per call is unchanged within noise (spike 1, M6: same allocation). With `T` = `object`, a call now costs what a real comparison costs.

---

## Known Limitations

- **A value-type sequence still compares what `T` shows (silent).** When the runtime type of either value is a value type that implements `IEnumerable` (`ImmutableArray<T>`, `ArraySegment<T>`, a struct of yours) and the two values are not two sequences of different runtime types, the top level compares the members visible through `T`, as on master (Details, item 1). With `T` = `object`, `EnsureAssignable<object>(ImmutableArray.Create(1, 2), ImmutableArray.Create(1, 3))` passes, and an `ImmutableArray<int>` against a class that is not a sequence is not reported as two runtime types. Under the maintainer's rule "以為有檢查其實沒檢查比較糟" this is a silent gap; the spec kept it on purpose ("value-type sequences … keep today's rule"). Pass such a value as its own type.
- **H3 false positives.** Under Native AOT a stateless runtime type that nothing preserves (a subclass that declares nothing, an override-only subclass, a stateless derived record) throws "cannot compare", also for equal values (spike 2: F3, F6a, F6aB, F7l, F7v; smoke check 118). The fix in the message removes it (smoke check 119).
- **Partial preservation is not detected (A1).** If anything keeps any member of the runtime type (for example a constructor kept by a dependency-injection registration), H3 treats it as preserved. Its hidden fields are then not compared, and the call compares only what stays visible, which can be the members of `T` alone. This is not detected (spike 2, "Known misses of H3"; inferred, not measured).
- **Preservation is per generic definition.** Under Native AOT, preserving one closed generic type (`GenericTicket<long>`) made another instantiation of the same definition (`GenericTicket<int>`) show its declared members (measured while smoke check 120 was written). An instantiation that nothing preserves, of a definition that something preserves, is not detected.
- **Private fields of a middle base class under Native AOT (F11).** When the runtime type is visible and a base class between it and `T` is not, that base class's private fields are not compared. This is the limit of [ADR-0023](0023-comparison-of-base-class-members.md), which also holds with `T` = the runtime type.
- **`Old<T>()` shares what it cannot see (#39).** Under Native AOT `Old<T>()` shares the hidden fields of a type it cannot see, so a comparison of elements can pass while an element changed (spike 1, OL4). Framework types reached as nested values, with hidden private state, are left to [issue #39](https://github.com/cwouyang/uContract.NET/issues/39).
- **Framework private state of a top-level sequence is not compared.** At the top level the non-public fields of the framework classes of a sequence that is compared by its elements are left to the elements. (A sequence that is not a collection, passed as its own type, is compared by all its members, the private fields of `T` included.) A comparer kept only in a non-public field of a collection is not compared: with `T` = `Hashtable` or `object`, a `Hashtable` whose comparer changed passes (measured in the review of this ADR; master reported the change with `T` = `Hashtable`, read from the code). `Lookup<TKey, TElement>` is not a collection on .NET 8 (it implements neither `ICollection` nor `IReadOnlyCollection<T>`; measured): under another `T` it throws `InvalidOperationException`, and as its own `T` all its members are compared, `_comparer` included (measured). A nested `Lookup` is compared by its elements, so a change of its comparer passes there (measured), as on master. The event-handler fields of a framework class (the subscribers of an `ObservableCollection<T>`'s `CollectionChanged`) are not compared at the top level either.
- **The `Key` of a nested grouping is not compared.** Below the top level, a sequence built on a framework class is compared by its elements (and the members declared on classes of yours), so the public framework properties of a grouping are not read: a nested `Lookup` whose content differs only in the `Key` of a grouping (keys `{a, b}` against `{a, c}` with equal elements) passes (measured). The `Key` is compared only for a grouping at the top level. This is not new: on master at `70d7083` every nested `IEnumerable` was compared by its elements alone (`MemberComparison.StartReferences` → `StartSequences`).
- **A framework property without a getter.** At the top level such a property is not read unless `T` declares or inherits it. When `T` does, the call ends in `InvalidOperationException`, as on master: `T` = `XmlDocument` reads `XmlResolver`, which has no getter, unless a pattern excuses it. With `T` = `object` or `XmlNode`, an `XmlDocument` ends in `InvalidOperationException` too, because it is a sequence that is not a collection.
- **A nested class of yours that implements `IEnumerable` is not enumerated.** It is compared by its fields. Elements that it produces from the captured state of a delegate are missed: a delegate is compared by its methods, not its target.
- **A `yield` iterator of a library is enumerated.** A `[CompilerGenerated]` iterator from any assembly that is not part of .NET, a NuGet package included, is enumerated at the top level. An infinite one does not return. An exception from its body leaves the call unwrapped, with one exception: when the two sequences are of different runtime types, an `InvalidOperationException` while enumerating either is caught and the two count as unequal (`MemberComparison.TryEnumerate`).
- **A `yield` method that returns only an enumerator.** "Compared by its elements" holds for a `yield` method that returns `IEnumerable` or `IEnumerable<T>`. One that returns `IEnumerator` or `IEnumerator<T>` gives an enumerator, not a sequence. It is compared by its fields (the state of the iteration) under the JIT, and under Native AOT, when nothing preserves it, the call throws "no properties or fields are visible". Since 2026-11-03 that message names no `typeof`, which cannot be written for it, and suggests returning `IEnumerable<T>` from the method (measured: smoke check 144). Comparing it by its elements stays deferred: enumerating an enumerator moves it.
- **The `<>` name is a convention of the C# compiler.** A class from another compiler or from IL whose metadata name starts with `<>` and that implements `IEnumerable` is compared by its elements alone, and so is a class that implements `IEnumerator` and is compiler-generated: it, or a type in its declaring chain, is marked `[CompilerGenerated]` with a `<` or a `$` in its name or has a name that starts with `<>` (a `[CompilerGenerated]` class with a name C# can write and no such declaring type is not, since amendment 8); a newer SDK could name its collection-expression classes otherwise (read from the code; only the .NET 8.0.204 SDK was measured).
- **Message wording.** The message for a sequence that is not a collection says that enumerating it "could run a query, consume its source or block"; that is not true for a materialised one (`ImmutableQueue<T>`, `StringDictionary`, `XmlNodeList`). When two sequences of different runtime types have equal elements and a property of an interface `T` differs, the violation does not say that the runtime types differ. (The message for a property without a getter named "typeof(X))] where X is that type" until 2026-11-03; it now names the declaring type of the property in C# syntax.)

---

## Alternatives Considered

### Alternative 1: Fall back to the metadata of `T` (R-fallback)

**Description**: Compare the runtime type; under Native AOT, when its members are hidden, compare by the members of `T`, as master did.

**Pros**:
- Under the JIT it equals R (spike 1: identical outputs, 32 of 1243 unit tests change).
- No call that passed on master throws under Native AOT.

**Cons**:
- Under Native AOT it keeps master's silent result for `T` = `object` or an interface holding a type that nothing preserves (spike 1, M4).

**Why rejected**: A silent fallback to `typeof(T)` is what the maintainer's rule forbids. The maintainer chose "照實際型別，AOT 大聲失敗".

---

### Alternative 2: H1 — no visible instance field

**Description**: Under Native AOT, throw when the runtime type declares no visible instance field.

**Why rejected**: It throws also for a preserved stateless type (spike 2: F5, F6aP), a false positive that the user's fix cannot remove except by adding a field.

---

### Alternative 3: H2 — a class in the chain without a visible field

**Description**: Throw when a class between the runtime type and `T` declares no visible instance field.

**Why rejected**: It has H1's false positives, and one more: a stateless middle base under a preserved leaf (spike 2, F6bP), which `DynamicDependency(All)` on the leaf keeps only as a constructor.

---

### Alternative 4: H4 — instance size larger than the base's

**Description**: Like H2, but blame a class without visible fields only when its instance size (`MethodTable` base size) is larger than its base's.

**Why rejected**: It misses state that fits in the base's padding (spike 2: Z1, Z2, F6b, F7r), and it reads a CoreCLR/ILC layout detail at offset 4, which is not a public API.

---

### Alternative 5: H5 — H3 or the H4 size test

**Description**: H3, or the H4 size test on the chain.

**Why rejected**: It catches F11 but adds the D10 false positive and depends on the size read. Spike 2 recommended H5 only if the F11 limit were to be closed too; it was not.

---

### Alternative 6: Elements only for a sequence (R-seq)

**Description**: Compare a top-level sequence by its elements only, as master compared a nested one. This was the first answer, "和成員一樣逐元素".

**Why rejected**: It drops a collection subclass's own members: smoke check 100 failed (`CodedCollection.Code`), and the patterns on a sequence no longer applied (3 more unit tests; spike 2, section 4). The maintainer replaced it by "元素＋自己成員，巢狀也一起改".

---

### Alternative 7: Literal R for a sequence (by its members)

**Description**: Compare a top-level `List<int>` by the members of its runtime type, as any other class.

**Why rejected**: It walks the framework's private fields, so two lists of equal content and different capacity are a violation (spike 1, O5d, O5e, I2c; master's L2 for `T` = `List<int>`).

---

## Related Decisions

- **Amends**: [ADR-0007 - Reflection and Field Comparison for EnsureAssignable<T>()](0007-reflection-field-comparison.md) — its record of the top level as "the members of `T` one by one" no longer holds. Carries an amendment.
- **Amends**: [ADR-0021 - Postcondition Helpers under Trimming and Native AOT](0021-postconditions-under-trimming-and-aot.md) — the gap list of its Known Limitations and the exemption of `EnsureAssignable<object>`; smoke checks 109 to 139. Carries an amendment.
- **Amends**: [ADR-0022 - Faithful Old<T>() Copies and Robust EnsureAssignable<T>() Comparison](0022-faithful-old-copies-and-robust-comparison.md) — "the top level comparing the members of `typeof(T)`", R6, R7 and R8. Carries an amendment.
- **Amends**: [ADR-0023 - Comparison of Members Declared on Base Classes in EnsureAssignable<T>()](0023-comparison-of-base-class-members.md) — its scope statement, "a nested collection class of yours is compared by its elements", and the framework stop for a framework class as `T`. Carries an amendment.
- **Related to**: [ADR-0025 - Fields-Only Comparison Not Adopted](0025-fields-only-comparison-not-adopted.md) — the pattern rule that applies to the runtime type's members.

---

## Implementation Notes

This ADR was written after the implementation so that it could record what the work turned up.

- **Where it lives.** `Contract.EnsureAssignable<T>()` step 5: `_CompareByRuntimeType` (whole values, shared types, different runtime types), `_CompareSequencesOfDifferentRuntimeTypes`, then `_CompareByMembers` with `_TopLevelType`, `_TopLevelMembers`, `_ThrowIfNotACollection`, `_ThrowIfShowsNoDeclaredMember` and `_RemoveMembersASequenceIsNotComparedBy`. `MemberComparison.GetTopLevelMetadata` lists the private fields up to a class `T`; `GetSequenceRule` holds the sequence rule (cached per type); `ShowsNoDeclaredMember` holds H3 (cached per type); `CSharpTypeName.Of` writes a type in C# syntax for the fixes.
- **The "no members visible" count at the top level.** The private fields declared on a class `T` that the runtime type derives from count as visible, because the annotation on `T` preserves them. Without this, a subclass of `Random` that declares nothing threw "no members visible" under Native AOT where master reported `_impl`.
- **A test seam for H3.** Under the JIT every type shows its members (spike 2, section 3), so unit tests set `RuntimeFacts.ShowsNoDeclaredMemberOverride` under a simulated runtime without dynamic code. Only the smoke program measures H3 itself.
- **Unit tests.** New test classes `EnsureAssignableRuntimeTypeTests`, `EnsureAssignableTopLevelWholeValueTests`, `EnsureAssignableSequenceTests` and `EnsureAssignableUnseenRuntimeTypeTests`; tests that pinned the old gaps were rewritten to the new result, not deleted. On master at `70d7083` 1243 unit tests passed; on this branch 1408 passed before the final fix of 2026-11-03, 1427 after it, 1437 after amendment 7, 1447 after amendment 8, and 1459 after amendment 9. All figures are snapshots. Amendment 9 adds a unit test under the H3 seam that only the elements rule can satisfy: an emitted `[CompilerGenerated]` class named `<Numbers>d__0` that lists no member of its own; removing the exemption for a compiler-generated sequence in `_TopLevelMembers`, or dropping the `<` route of the `IEnumerator` clause, turns it red (measured by a temporary revert; the older test with `CountTo(2)` stayed green under both).
- **Native AOT.** Smoke check 100 now expects `Code|(the elements; assignable patterns do not apply to them)|frameworkPrivateFields=items`. Checks 109 to 139 are added: an `object`-typed class of yours (109–111), a base class `T` with an unpreserved, a preserved and a stateless derived type (112–120), a `Queue<int>` as `object` (121), a type passed as `T` elsewhere (122–124), a nested unpreserved class that only inherits visible members (125), boxed `DateTime` and `decimal` (126–129), a preserved nested type in an unpreserved class (130), `yield` iterators nested and at the top level (131–133, 137–139), an unpreserved nested class as `object` (134), a preserved closed generic (135) and LINQ queries as `object` (136). Measured on `win-x64` with the Native AOT publish of the default build (`-warnaserror`, no IL warnings): 139 `PASS`, no `FAIL`, with postconditions on and with `DBC_POST=off`. The check list "`List<int>` as `object`" is pinned with `Queue<int>`: check 3 of the same program preserves `List<>`. The CI workflow had not run for this change when this ADR was written. The final fix of 2026-11-03 adds checks 140 to 144: a nested collection expression unchanged and changed (140, 141), collection expressions as `IReadOnlyList<int>` of equal and of different elements (142, 143), and the enumerators of a `yield` method that returns only `IEnumerator<int>` as `T` = `IEnumerator<int>` (144, which throws with the fix that names no `typeof`). Checks 137 and 143 compare the whole list of violation entries, `(the elements; assignable patterns do not apply to them)`, so the line "Members were listed from the runtime type" would fail them. Measured the same way: 144 `PASS`, no `FAIL`, with postconditions on and with `DBC_POST=off`. Amendment 7 adds checks 145 (a `[CompilerGenerated]` class that implements only `IEnumerable<int>`, as `T`, `Tag` changed: entries exactly `Tag`) and 146 (a `file` class that implements `IEnumerable<int>` and whose enumerator throws, as `T`, `Extra` changed: entries exactly `Extra`). Measured the same way: 146 `PASS`, no `FAIL`, both ways. Checks 145 and 146 were added after the narrowing and were not run against the wide rule. Amendment 8 adds check 147 (a `[CompilerGenerated]` class with a name C# can write that is its own enumerator, as `T`, `Tag` changed: entries exactly `Tag`). Measured the same way: 147 `PASS`, no `FAIL`, both ways; check 147 was not run against the rule before amendment 8 either. Amendment 9 adds checks 148 to 152, the iterator of a local function that captures a variable: nested against a `List<int>` of equal and of different elements (148, 149), at `T` = `IEnumerable<int>` against a `List<int>` of equal elements, with the number of instance fields reflection lists for it (150: `fields=0|none`), at `T` = `object` against a `List<int>` of different elements (151), and two such iterators that capture different delegates and yield equal elements (152). Measured the same way: 152 `PASS`, no `FAIL`, both ways; checks 148 to 152 were not run against the rule before amendment 9.

### Amendment (2026-11-08): A one-element collection expression on SDK 8.0.427

Item 3 of the Details above (a compiler-generated sequence) says the .NET 8 SDK builds a collection expression "as
`<>z__ReadOnlyArray<T>`, also for one element". That was measured on SDK 8.0.204 and stays true
there. When `global.json` moved to SDK 8.0.427 ([issue #80](https://github.com/cwouyang/uContract.NET/issues/80),
[ADR-0020](0020-contracts-enabled-by-default.md)), a probe measured that SDK 8.0.427 builds a
one-element collection expression held as `IReadOnlyList<T>` or `IEnumerable<T>` as
`<>z__ReadOnlySingleElementList<T>`; more than one element is still `<>z__ReadOnlyArray<T>`.

- The class depends on the compiler. Both names start with `<>`, so the rule (a class whose
  metadata name starts with `<>` that implements `IEnumerable`) compares both by their elements.
  The probe measured this too: equal elements against a `List<T>` pass, different elements throw.
- A unit test pins that a one-element collection expression held as `IReadOnlyList<int>` is
  compared by its elements against a `List<int>`: equal elements pass, and different elements
  report the member. With the `<>` clause disabled by a temporary change, the equal-elements test
  fails; the different-elements test still passes, because a comparison by members reports the
  member too.
- The decision is unchanged.

### Amendment (2026-11-10): An inline array at the top level is compared as a whole, element by element (#84)

[Issue #84](https://github.com/cwouyang/uContract.NET/issues/84) compares an `[InlineArray]` struct element by
element and never asks its built-in `Equals`, which sees only element 0 on the .NET 8 runtime and throws
`NotSupportedException` on the .NET 10 runtime; [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md)
(fourteenth Amendment) records the rules IA1, IA2 and IA3, the measurements and the limits. At the top level:

- **An inline array is compared as a whole.** When `T` is an inline-array type, or `T` is another type
  (`object`, an interface that the inline array implements) and the runtime type of both values is one
  inline-array type, the two values are compared as a whole by IA1, as two arrays are
  (`Contract._CompareByRuntimeType` sends every inline array to `_CompareAsWhole`). The patterns do not
  apply and are not examined: before, `T` = the inline-array type compared its one field, so a pattern that
  named that field could excuse element 0 (spec author's call, approved with the spec). The violation reads
  "actual and expected are not equal. {type} is an inline array, compared as a whole element by element,
  so assignable patterns do not apply." (unit tests
  `EnsureAssignable_WhenTIsAnInlineArrayAndOnlyALaterElementChanged_ReportsTheTwoAsNotEqual`,
  `EnsureAssignable_WhenTIsAnInlineArrayAndAPatternNamesItsElementField_StillReportsTheTwoAsNotEqual`).
  `T` = a `Nullable<>` of an inline array keeps the rule for a nullable value type and its message, "… is
  compared as a whole, so assignable patterns do not apply." (unit test
  `EnsureAssignable_WhenTIsANullableInlineArrayAndOnlyALaterElementChanged_ReportsTheTwoAsNotEqual`); its
  values are compared by IA1.
- **An inline array that implements `IEnumerable`** follows IA1 like any inline array, not the rule for a
  value-type sequence that keeps the declared `T` (Details, item 1): with `T` = `object` two such values of
  distinct lists with equal contents pass, and a changed later list is reported (unit tests
  `EnsureAssignable_WhenTIsObjectAndAnEnumerableInlineArrayHoldsDistinctListsWithEqualContents_Passes`,
  `EnsureAssignable_WhenTIsObjectAndALaterListOfAnEnumerableInlineArrayChanged_ReportsTheTwoAsNotEqual`).
  Against a sequence of another runtime type it is still one of "two sequences of different runtime
  types": as a sequence that is not a collection it throws `InvalidOperationException` (unit test
  `EnsureAssignable_WhenTIsObjectAndAnEnumerableInlineArrayMeetsAListWithTheSameContents_CannotCompareIt`).
  Against a value of another runtime type that is not a sequence (a boxed `int`) it is a
  different-runtime-types violation (unit test
  `EnsureAssignable_WhenTIsObjectAndABoxedInlineArrayMeetsABoxedInt_ReportsDifferentRuntimeTypes`).
- **A holder struct** (IA2) and a struct of IA3, boxed as another `T`, are compared by their fields without
  their built-in `Equals` (`_TopLevelMembers` asks `MemberComparison.IsComparedByFieldsWithoutEquals`
  before `Equals`); one with a user `Equals` is decided by it first, and an exception it throws propagates
  (unit tests `EnsureAssignable_WhenTIsObjectAndOnlyALaterElementOfABoxedHolderStructChanged_ReportsTheField`,
  `EnsureAssignable_WhenTIsObjectAndABoxedHolderStructsHandWrittenEqualsSaysEqual_Passes`,
  `EnsureAssignable_WhenTIsObjectAndTheHandWrittenEqualsOfABoxedHolderStructThrows_LetsItPropagate`).
- **Native AOT.** An inline array is still not covered by rule H3 (Details, item 4), but its elements can be
  read only when reflection lists its element field. When nothing preserves the inline-array type, the
  call throws "cannot compare", also for equal values; that and the other "cannot compare" cases of
  inline arrays are in the [ADR-0021](0021-postconditions-under-trimming-and-aot.md) amendment of this date
  (smoke check 164: two boxes of equal elements with `T` = `object`).

These statements, each quoted by its opening words, no longer hold as written:

- Details, item 1: "A boxed value type other than `T`, other than such a sequence, is compared as a member
  is: its `Equals` first, then its fields. A primitive, an enum and an inline array are decided by `Equals`
  alone." An inline array is compared as a whole by IA1, and a holder struct by its fields without its
  `Equals`. A primitive and an enum are still decided by `Equals` alone.
- Details, item 4: "a primitive, an enum and an inline array need no metadata and are not covered". An
  inline array is not covered by H3, but it needs the metadata of its element field (see above).

The decision is unchanged. The smoke program has 169 checks.

---

## References

- [Issue #40: `EnsureAssignable<T>()` comparison gaps that exist in every build](https://github.com/cwouyang/uContract.NET/issues/40)
- [Issue #39: `Old`/`EnsureAssignable` under Native AOT](https://github.com/cwouyang/uContract.NET/issues/39)
- [`Contract.cs`](../../src/uContract/Contract.cs) — `EnsureAssignable<T>()`, its top-level helpers and its XML documentation
- [`MemberComparison.cs`](../../src/uContract/MemberComparison.cs) — `GetTopLevelMetadata`, `GetSequenceRule`, `ShowsNoDeclaredMember` and the nested walk
- [`CSharpTypeName.cs`](../../src/uContract/CSharpTypeName.cs) — the type name in the fixes
- [`EnsureAssignableRuntimeTypeTests.cs`](../../tests/uContract.Tests/EnsureAssignableRuntimeTypeTests.cs), [`EnsureAssignableTopLevelWholeValueTests.cs`](../../tests/uContract.Tests/EnsureAssignableTopLevelWholeValueTests.cs), [`EnsureAssignableSequenceTests.cs`](../../tests/uContract.Tests/EnsureAssignableSequenceTests.cs), [`EnsureAssignableUnseenRuntimeTypeTests.cs`](../../tests/uContract.Tests/EnsureAssignableUnseenRuntimeTypeTests.cs) — the unit tests
- [`tests/uContract.AotSmoke/Program.cs`](../../tests/uContract.AotSmoke/Program.cs) — smoke checks 100 and 109 to 152
- [`CHANGELOG.md`](../../CHANGELOG.md) — the `[Unreleased]` bullet
- [ADR-0007](0007-reflection-field-comparison.md), [ADR-0021](0021-postconditions-under-trimming-and-aot.md), [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md), [ADR-0023](0023-comparison-of-base-class-members.md), [ADR-0025](0025-fields-only-comparison-not-adopted.md)

---

## Revision History

| Date       | Status      | Notes                          |
|------------|-------------|--------------------------------|
| 2026-11-02 | Accepted    | Decision recorded after implementation (#40 item 2 and the rest of item 3). Amends ADR-0007, ADR-0021, ADR-0022 and ADR-0023. |
| 2026-11-03 | Accepted    | Final fix after the whole-branch review: every compiler-generated sequence (a collection expression too) is compared by its elements; messages name no `typeof` for a type C# cannot write; the not-a-collection message names all three interfaces; no "Members were listed" line for a compiler-generated sequence; smoke checks 140 to 144. |
| 2026-11-03 | Accepted    | Amendment 7, 「收窄，連同 Minor 一起修」: the compiler-generated sequence rule narrowed to `[CompilerGenerated]` + `IEnumerator`, or a `<>` name + `IEnumerable`; a source-generated class or a `file` class keeps the `typeof` fix (a `file` class by its own name); the ways out match why a type cannot be named; smoke checks 145 and 146. |
| 2026-11-03 | Accepted    | Amendment 8, 「開第 3 輪，全部修」: the `IEnumerator` clause also requires a `<` or a `$` in the name; one definition of a compiler-generated name for the rule and the messages; the `typeof` fix for a `file` class says it must be written inside the file that declares the class; wording for a compiler-generated type inside a generic argument; smoke check 147. |
| 2026-11-03 | Accepted    | Amendment 9, 「開第 4 輪，連同剩下的 Minor」: the `IEnumerator` clause checks the declaring chain too, so the iterator of a local function that captures a variable is compared by its elements; a type with `file` classes of more than one file in its generic arguments names no `typeof`; a seam test only the elements rule can satisfy; smoke checks 148 to 152. |
| 2026-11-08 | Amended     | SDK 8.0.427 builds a one-element collection expression as `<>z__ReadOnlySingleElementList<T>` (8.0.204: `<>z__ReadOnlyArray<T>`); the `<>` rule covers both. Decision unchanged. See Implementation Notes > Amendment. |
| 2026-11-10 | Amended     | An inline array at the top level, as `T` or as the runtime type under another `T`, is compared as a whole, element by element, and the patterns do not apply; a holder struct boxed as another `T` is compared by its fields without its `Equals` (#84; rules in ADR-0022). Decision unchanged. See Implementation Notes > second Amendment. |
