# Compare `[InlineArray]` values element by element (issue #84)

Status: draft for approval. Scaffolding: this file is dropped before the merge; the durable record is the ADR
amendments listed under "Documentation".

## Why

On the .NET 10 runtime, `Contract.EnsureAssignable<T>()` throws `NotSupportedException` ("Calling built-in Equals()
or GetHashCode() on type marked as InlineArray is invalid.") whenever the comparison meets an `[InlineArray]` struct,
also when the two values are equal. The published package targets `net8.0`, and an application on .NET 10 runs it
on the .NET 10 runtime, so this is a defect of the released library. It also blocks 3.0.0 and the move to .NET 10
(#82, ADR-0020 amendment of 2026-11-09).

On .NET 8 the same comparison does not throw, but it sees only element 0: `ValueType.Equals` and reflection show only
the one declared field. A change in a later element with an equal element 0 passes silently. ADR-0022 records this
as an accepted limitation, unchanged since 2.0.0.

The maintainer chose, after a measuring spike, to fix both: compare an inline array element by element, on both
runtimes ("A 逐元素，這張就做"). Evidence for every measured statement below is in the spike report
`C:\Users\cwouyang\Desktop\Dev\personal\sdd-work\uContract.NET-84\spike-report.md` (archived to `sdd-archive` when
the work is done).

## Decisions taken by the maintainer (verbatim option labels)

1. Comparison: "A 逐元素，這張就做" (options were A element by element, B emulate .NET 8, C always "cannot compare").
2. Native AOT, inline-array element field hidden because nothing preserves the type: "丟 cannot compare (Recommended)".
3. CI: also run the unit tests on the .NET 10 runtime: "這次一起加 (Recommended)".
4. Endgame: "審查後直接開 PR (Recommended)".
5. Decide path: "Path A：完整 spec + plan". Clarifying questions were skipped ("跳過提問直接看 spec"); the calls the
   spec author made are marked **[author's call]**.
6. After the builder check (measured first: on .NET 10 a `record struct` with an inline-array field, a `(Buf, int)`
   tuple, a struct holding such a tuple, and a struct whose `object` field holds a boxed inline array all throw
   `NotSupportedException`, equal values included; on .NET 8 all pass, also with only element 2 changed; a record
   struct's `Equals` overloads carry `[CompilerGenerated]`; outputs `holder-8.txt`, `holder-10.txt` next to the
   spike report):
   - record struct / ValueTuple: "當成 holder，跳過 Equals 逐欄位比 (Recommended)" (B2).
   - `object`/interface field holding an inline array at run time: "接住例外退回逐欄位，記下 .NET 8 差異 (Recommended)"
     (B2a).
   - dynamic code: "可以，只在支援動態程式碼時用 (Recommended)" (B8).
7. Revision during Act (Task 2 review; measured by the reviewer on both runtimes: the catch of decision 6 also
   swallowed a `NotSupportedException` thrown by a user `Equals` of a nested class, which propagated before; a
   `Nullable<Holder>` field still reached the holder's built-in `Equals` on .NET 8, so .NET 8 and .NET 10 differed):
   - B2a: "改成先看執行期值，不接例外 (Recommended)" — replaces the catch of decision 6.
   - `Nullable<T>`: "把 Nullable 當成像 ValueTuple (Recommended)".
8. Revision during Act (Task 2 re-review; measured by the controller on both runtimes: a struct field of a
   `record class` holding an inline array, a struct field of `System.Tuple<Buf, int>`, and a struct `object` field
   holding such a record class all throw `NotSupportedException` on .NET 10, equal values included, and pass on .NET 8
   with only element 2 changed; a record class as a class member is walked by members and is fine):
   "也沿著這類類別追 (Recommended)" (B2a, routes through classes whose `Equals` is not a user `Equals`).
9. Revision during Act (Task 2 risk review; measured by the reviewer on both runtimes: a `record struct` / `record
   class` with a hand-written typed `Equals(R)` lost its equality, because its `[CompilerGenerated]` `Equals(object)`
   forwards to it): "手寫 Equals(R) 算使用者 Equals (Recommended)" (Terms, User `Equals`).
10. Revision during Act (Task 2 fix round 4 reviews; measured by both reviewers on both runtimes: a derived record
   whose base record declares a hand-written `Equals(Base)` lost that equality): "基底手寫的也算 (Recommended)"
   (Terms, User `Equals`).
11. Return to Decide at Task 2's fix-round cap (round 5 re-review, measured on copies of both commits: a derived record
   that declares its own inline array under a base with a hand-written typed `Equals` throws `NotSupportedException` on
   .NET 10 and misses the change on .NET 8 under revision 10's wording; Visual Basic anonymous types were no longer
   followed): "回 Decide，補兩條規則 (Recommended)" (Terms, User `Equals`: the limit of the base exception, and VB
   anonymous types).
12. Revision after Task 2 fix round 6 (risk review, measured on both runtimes: revision 11's limit made a derived record
   with an unused class-typed field under a tolerant hand-written base report the base's ignored inline array, where it
   passed before; a real Visual Basic build showed non-`Key` fields being compared): "改回簡單規則，記一個限制
   (Recommended)" — revision 11's limit is removed and its case becomes a known limitation; "只追 Key 欄位 (Recommended)".
13. Revision during Act (Task 4 implementer: preserving the type does not let the .NET 8 runtime read a struct element
   without dynamic code): "依情況列解法 (Recommended)" — B6 lists, per case, only the ways out that work.

## Terms

- **Inline array**: a struct type marked `[System.Runtime.CompilerServices.InlineArray(n)]`. It declares exactly one
  instance field; its length is `n` and its element type is that field's type.
- **Holder struct**: a struct that is not an inline array, whose `Equals` is not a user `Equals`, and that has an
  instance field whose declared type is an inline array, directly or through a field of a nested struct that is
  itself a holder struct. A field declared as `Nullable<X>` counts as declared `X`. This includes a `ValueTuple<…>`
  with an inline-array item, a `record struct` with an inline-array field, and a `Nullable<>` of an inline array or of
  a holder struct. Its `Equals` reaches the inline array's built-in `Equals` and throws on .NET 10 (measured).
- **User `Equals`**: an `Equals(object)` override written by hand in the type (of the inline array, or of a struct).
  Not a user `Equals`: the built-in `ValueType.Equals`, an override marked `[CompilerGenerated]` (as a record
  struct's is, measured; a record class's too), and the `Equals` of `System.ValueTuple<…>`, `System.Nullable<T>` and
  `System.Tuple<…>` (they only call the `Equals` of the values they hold). Exception: when a record (struct or class),
  or any record class it derives from, declares a typed `Equals(R)` written by hand, its `[CompilerGenerated]`
  `Equals(object)` reaches that method, so the record has a user `Equals` (it decides first; its exceptions propagate).
  This holds whatever fields the derived records declare. Known limitation (accepted by the user): when a record
  declares its own field that holds an inline array (or reaches one) and a base record's typed `Equals` is written by
  hand, the record's compiler-generated part of `Equals` still compares that field with the built-in `Equals`: on
  .NET 10 it throws `NotSupportedException` (propagated like any exception from a user `Equals`), on .NET 8 it sees
  only element 0. The way out is to write the derived record's own typed `Equals`, or to list the member as assignable.
  The anonymous types the look follows are recognised as such, not by `[CompilerGenerated]` alone: C# anonymous types
  and Visual Basic anonymous types, by their compiler-given names. For a Visual Basic anonymous type, whose `Equals`
  compares only its `Key` properties, the look follows only the fields behind its `Key` properties. A class a user or a source generator marks
  `[CompilerGenerated]` but whose `Equals` is written by hand keeps its user `Equals`.
- **Element rules**: the rules by which two elements of an array are compared today (ADR-0022 R1–R7 as applied to
  array elements: `Equals` for primitives and enums, R3 for value types, references walked by members, sequences by
  elements, and so on).

## Required behaviour

The same input gives the same result on the .NET 8 and .NET 10 runtimes, under the JIT. Under Native AOT the result
is either that same result or the "cannot compare" `InvalidOperationException` described in B6; never a silent pass
that the JIT would have reported, and never `NotSupportedException` or `IndexOutOfRangeException`.

**B1. Inline array as a member or element.** Wherever the comparison meets two values of one inline-array type (a
field or property of a class or struct, an element of an array, a `List<>` or another sequence, the value of an
`object`-typed member, a dictionary value):

1. If the type has a user `Equals` and it returns `true`, the pair is equal.
2. Otherwise all `n` elements are compared pairwise, in index order, by the element rules. The pair is equal when
   every element pair is equal.
3. The built-in `ValueType.Equals`/`GetHashCode` of the inline array is never called.

A difference is reported as a difference of the member that holds the inline array, as for an array member today
(for example `- B`). **[author's call]**: no element index is added to the message, matching arrays.

**B2. Holder struct.** Its `Equals` is never called. It is compared by its fields under the existing rules (as R3
step 3 does after `Equals` said "unequal"), and an inline-array field among them follows B1. A struct with a user
`Equals` keeps today's behaviour: its `Equals` decides first, and an exception it throws propagates (ADR-0022 R3).

**B2a. Inline array found only at run time.** A struct that is not a holder struct and has no user `Equals` may
still hold an inline array or a holder struct in a field whose declared type does not show it (`object`, an
interface, `ValueType`), directly or in such a field of a nested struct whose `Equals` is not a user `Equals`. Before
its built-in `Equals` is asked, the comparison looks at the run-time values of those fields (which fields to look at
is a per-type fact; only their values are read per call). When either side holds an inline array or a holder struct
there, the built-in `Equals` is not called and the pair is compared by its fields, as in B2. The result is the same
on .NET 8 and .NET 10. No `NotSupportedException` is caught: an exception thrown by a user `Equals`, at any depth,
propagates as today (ADR-0022 R3).
The look also follows a field (declared or held at run time) whose type is a **class** whose `Equals` compares its
values but is not a user `Equals` (a record class, `System.Tuple<…>`, an anonymous type), as it follows a nested
struct: when such a class
holds an inline array or a holder struct (directly, or further along such routes), the outer struct is compared by its
fields as above. A class with no `Equals` override (reference equality) or with a user `Equals` is not followed. The
look terminates on cyclic values (a box or an object that refers back to itself): a value already being looked at is
not looked at again, so a self-referencing value never overflows the stack or loops, and a cycle alone does not make a
pair be compared by its fields. A deep acyclic graph (for example a long chain of records) does not overflow the stack
either: a struct that holds no inline array keeps today's result at any depth.

**B3. Top level.** When `T` is an inline-array type, or `T` is another type and the runtime type of the two values is
one inline-array type (for example a boxed inline array with `T` = `object`), the two values are compared as a whole
by B1, as an array is. **[author's call]**: assignable patterns do not apply to it, as for arrays (today `T` = the
inline-array type compares its one field, so a pattern naming that field could excuse element 0). The violation
message says the two values are not equal and are compared as that inline-array type element by element; the exact
wording is the builder's, following the existing "compared as a whole" messages. Top-level holder structs follow B2,
and B2a applies there too (today `_TopLevelMembers` calls `Equals` first on a boxed value type ≠ `T`).
**[author's call]**: an inline array that also implements `IEnumerable` follows B3 and B1 like any inline array,
not the rules for a value-type sequence (ADR-0026 item 1, R6/R7); the existing
`EnumerableInlineArrayHoldsDistinctListsWithEqualElements` test is rewritten accordingly.

**B4. `Contract.Old` copies.** An `Old` copy of a value that contains an inline array deep-copies **every** element
the way an array element is copied, not only element 0. Then an unchanged pair passes, and an in-place change to any
element after `Old` (for example `buf[2].Add(1)` on a `List<int>` element) is reported. (Measured: today elements
1..n-1 are shared with the original; with B1 alone an in-place change of element ≥ 1 would pass silently.)

**B5. Elements of every kind.** B1–B4 hold for primitive, enum, reference and struct element types, including a
struct element that holds references, and an element type that is itself an inline array.

**B6. Cannot compare (loud failure).** When the elements of an inline array cannot be read, the call throws
`InvalidOperationException` and does not pass. Two cases are known (measured):

- Native AOT, the inline-array type's element field is not visible to reflection because nothing preserves the type.
- Native AOT on the .NET 8 runtime, the element type is a struct: there is no AOT-safe way on .NET 8 to get the
  element size (the .NET 9+ `RuntimeHelpers.SizeOf`/`Box` exist from the .NET 9 runtime on).

The message names the inline-array type and the member path, and gives, in the style of the existing "cannot
compare" messages (ADR-0021), only the ways out that work for its case:

- Element field not visible: preserve the type with `[DynamicDependency(DynamicallyAccessedMemberTypes.All,
  typeof(X))]`, list the member that holds it as assignable, or set `DBC_POST=off`.
- Struct element without dynamic code on the .NET 8 runtime: list the member that holds it as assignable, set
  `DBC_POST=off`, or run on the .NET 9 runtime or later. Preserving the type does not help here and is not offered. **[author's call]**: if `Contract.Old`
cannot copy the elements in these cases it does not throw itself; the comparison that follows throws as above.

**B8. Dynamic code.** Where reading or writing elements needs a generic instantiation that is not known at compile
time (struct elements on the .NET 8 runtime, measured), the library may use `MakeGenericMethod`, only when
`RuntimeFeature.IsDynamicCodeSupported` is true. The IL3050 diagnostic this raises is suppressed at that one place
with a written justification, recorded in an ADR-0021 amendment. When dynamic code is not supported, B6 applies.
Where the runtime offers a way that needs no dynamic code (the .NET 9+ `RuntimeHelpers` APIs, primitive, enum and
reference elements), that way is preferred. The library build stays free of other new IL2xxx/IL3xxx diagnostics.

**B7. Not changed.** C# fixed-size buffers (`fixed int X[4]`) are not inline arrays; they keep today's behaviour
(element 0 only, on both runtimes, measured). Other `Ensure*`/`Require*` methods do not compare values and are not
changed (read from code). No `GetHashCode` of a user value is called anywhere (read from code); this stays so.

## Compatibility

Measured with a prototype of A on both runtimes, unit suite of 1491 tests:

- 5 existing tests change result and are rewritten to the new behaviour (named in the spike report, M3): later
  elements are now seen; distinct lists with equal elements are now equal; the top-level message changes.
- On .NET 8 the check becomes stricter (a later-element change is now reported) and in one case more lenient (an
  inline array of distinct but equal references is no longer reported on every call). **[author's call]**: the
  CHANGELOG lists this under Changed as **BREAKING**, since 3.0.0 is a major release.
- ezDDD.NET does not use inline arrays or `EnsureAssignable` (measured); no downstream change.

## Verification

- New unit tests cover B1–B6 for the shapes in the spike's probe (element kinds × member, property, top level with
  `T` = the struct and `T` = `object`, array and `List<>` element, nested in a holder struct, `Old` unchanged and
  changed in place, element 0 changed and only a later element changed, a user `Equals`), plus a `record struct`
  with an inline-array field, a `(Buf, int)` tuple member, a struct holding such a tuple, and a struct whose
  `object` field holds an inline array (B2a), a `Nullable<>` of a holder struct or of an inline array as a struct
  field, and a nested class whose user `Equals` throws `NotSupportedException` (propagates). The B6 cases are covered
  in unit tests where the existing test seams can simulate them (for example `TestRuntime.WithoutDynamicCode`) and in
  the Native AOT smoke.
- The whole unit suite passes on the .NET 8 runtime and on the .NET 10 runtime (`DOTNET_ROLL_FORWARD=LatestMajor`),
  locally and in CI.
- Native AOT smoke (net8.0, win-x64 locally, linux-x64 in CI): publishes with `-warnaserror`, `--assert` passes with
  contracts on and with `DBC_POST=off`. New smoke checks cover an inline array that is preserved (reported / passes
  as under the JIT, primitive and reference elements), one that is not preserved (B6), and a struct element (B6 on
  .NET 8).
- No trim or AOT warning (IL2xxx/IL3xxx) is added to the library build; the one IL3050 suppression of B8 is the
  only new suppression, and it carries its justification.

## CI

`build-and-test.yml` also runs the unit tests on the .NET 10 runtime on both runners: `actions/setup-dotnet`
installs `10.0.x` next to the SDK from `global.json`, and a second test step sets `DOTNET_ROLL_FORWARD: LatestMajor`
(both measured locally: 10.0.14 used, 6 known failures on master, SDK still 8.0.427). The log shows which runtime ran
(a `dotnet --list-runtimes` step or a test output line; builder's choice). `global.json` stays on 8.0.427.

## Documentation

- ADR-0022: amendment. R3 step 3's "and is not `[InlineArray]`" and the limitations "`[InlineArray]` structs and
  fixed-size buffers" (narrowed to fixed-size buffers) and "Inline arrays of references" (replaced by the B6 note).
- ADR-0026: amendment (top level: an inline array is compared as a whole element by element, not by `Equals` alone).
- ADR-0021: amendment (the new "cannot compare" cases of B6, and the guarded dynamic code and IL3050 suppression of
  B8 with its justification).
- ADR-0022 also records B2/B2a: holder structs (including record structs, tuples and `Nullable<>` of them) skip
  `Equals`; a struct whose `object`/interface field holds an inline array at run time skips its built-in `Equals`.
- README, `docs/examples/API_REFERENCE.md`, the XML docs of `EnsureAssignable` and `Old` where they name inline
  arrays or "decided by Equals alone".
- CHANGELOG `[Unreleased]`: Fixed (#84, the .NET 10 exception, including holder structs) and Changed **BREAKING**
  (element by element; B6 cases); the known-limitation line keeps only fixed-size buffers.
- ADRs are written after the code and the measurements, as the last task.

## Non-goals

- Fixed-size buffers (B7).
- Running the Native AOT smoke as `net10.0`, multi-targeting, SDK 10: issue #82.
- The .NET 9 runtime is not tested (it is out of support from 2026-11-10).
- Performance of the element walk beyond not adding per-call reflection that the existing metadata caches avoid.

## Anti-goals

- A silent pass where an element differs and the JIT would report it.
- A different result on .NET 8 and .NET 10 for the same input, other than B6's "cannot compare" on .NET 8 Native AOT
  and the known limitation under Terms (a record's own inline-array field under a hand-written base typed `Equals`).
- A `NotSupportedException` thrown by a user `Equals` being swallowed.
- Any remaining call of the built-in `Equals`/`GetHashCode` on an inline array or a holder struct.
- A `NotSupportedException` from the built-in `Equals` escaping `EnsureAssignable` on .NET 10, other than through a
  user `Equals` (the known limitation under Terms).

## Left to the builder

How elements are read (the spike's M2 lists measured techniques and their limits under JIT and Native AOT; none is
binding), where the new logic lives, the exact message wording within the constraints above, and test placement.
