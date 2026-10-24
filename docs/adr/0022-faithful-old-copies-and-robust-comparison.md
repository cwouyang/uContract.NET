# ADR-0022: Faithful Old<T>() Copies and Robust EnsureAssignable<T>() Comparison

## Status

**Accepted**

- **Date**: 2026-10-07
- **Deciders**: Project maintainer
- **Status Date**: 2026-10-07
- **Amended**: 2026-10-09 — the `DBC_POST=off` pairing limitation under "What the measurements add or correct" is superseded (#47), and the smoke program gains checks 58 to 62; see the Amendment under Implementation Notes
- **Amended**: 2026-10-09 — at the top level a `string`, a delegate type and a nullable value type are compared as one pair by R0–R8, and a `null` side is decided first for every `T` (#52); the smoke program has 72 checks; see the second Amendment under Implementation Notes
- **Amended**: 2026-10-10 — at the top level, one instance of a shared type whose state can change, passed as `actual` and as `expected`, is not compared: the call throws `InvalidOperationException` (rule S, #54); the smoke program has 74 checks; see the third Amendment under Implementation Notes
- **Amended**: 2026-10-10 — two `FrozenSet<T>` or `FrozenDictionary<TKey, TValue>` values of one runtime type that a member or element holds are compared element by element, in enumeration order (R7), not by reference (R5), and one of the two types as `T` is compared as a whole; `Old<T>()` and B3 are unchanged (#49); the smoke program has 79 checks; see the fourth Amendment under Implementation Notes
- **Amended**: 2026-10-11 — `Old<T>()` copies an array whose element type is declared in `System.Collections.Frozen` and a value of a value type declared there (B3 shares the classes of that namespace that are not array types); `EnsureAssignable<T>()` compares an array type or `System.Array` as `T` as a whole, finds two arrays equal only when they have the same rank, and the same length and lower bound in every dimension, and compares two arrays of pointers or of function pointers by address (#57); the smoke program has 84 checks; see the fifth Amendment under Implementation Notes
- **Amended**: 2026-10-12 — limit 18 of the #57 amendment no longer holds: `Contract.EnsureImmutableCollection<T>()` no longer reads the namespace of the runtime type of its argument, and it reports an array of function pointers as a violation (#61, [ADR-0012](0012-dotnet-improvements-over-java.md)); `Old<T>()`, `EnsureAssignable<T>()`, B1 to B8 and R0–R8 are unchanged; this amendment holds the measured results of that change; the smoke program has 91 checks; see the sixth Amendment under Implementation Notes
- **Amended**: 2026-10-24 — R7 compares the counts of two dictionaries first: two of different counts are unequal and no entry of either is read (#50); `Old<T>()`, B1 to B8, R0 to R6 and R8 are unchanged; the smoke program has 92 checks; see the seventh Amendment under Implementation Notes
- **Amended**: 2026-10-24 — records the measurements behind #69: private fields and auto-properties that are not public, declared on a base class of `T`, are not compared; two different `Stream`s as `T` end in `TargetInvocationException` when a getter throws; in the measured cases a class derived from a fixed shared type passes the pair on `this` in silence unless a public getter of `T` throws, which corrects one item of the Limits of the #54 amendment; the library is unchanged; see the eighth Amendment under Implementation Notes

---

## Context

### Problem Statement

[Issue #45](https://github.com/cwouyang/uContract.NET/issues/45): in 2.0.0, `Old<T>()` deep-copies by
serializing the value with System.Text.Json (`IncludeFields = true`) and deserializing it as `T`.
Measured with the 2.0.0 package on `net8.0`, the copy loses:

1. **Private state.** `IncludeFields` covers public fields only. A copy holds what its constructor
   produced. A type whose constructor parameters bind to no public member, such as the documented
   `User.ChangeEmail` example, makes `Old` throw `InvalidOperationException`.
2. **Runtime types.** `Old(() => this)` in a base-class method returns the base class. A property
   declared `Animal` that holds a `Puppy` is copied as an `Animal`. Interface-typed and
   abstract-typed members cannot be copied at all.
3. **`object` members.** They come back as `JsonElement`.
4. **Graph shape.** Cycles are cut, shared references are duplicated, and a graph deeper than 64
   levels throws `JsonException`.

The Java original serializes all fields (`setVisibility(PropertyAccessor.FIELD, Visibility.ANY)`)
and deserializes as `supplier.get().getClass()`. So items 1 and 2 at the top level are porting
defects.

A faithful copy exposes defects of `EnsureAssignable<T>()` that the lossy copy hid. Measured on
2.0.0:

5. **Stack overflow** (process exit, cannot be caught) on cyclic pairs, on 10 000-node chains, on a
   property that returns `this`, and on a property that returns a new instance-method delegate.
6. **False violations** for unchanged values: a `Dictionary<string, Elem>`, a struct that holds a
   `List<int>` (`ValueType.Equals` compares reference fields by reference), and interface-typed
   members (declared-type dispatch compared them by reference).
7. **Unwrapped reflection exceptions** when the two sides of a member have different runtime types.

This ADR records both halves: a faithful copy, and a comparison that can consume it.

### Relevant Context

- [ADR-0006](0006-serialization-deep-copy.md) chose System.Text.Json and rejected a reflection deep
  copy (its Alternative 2) as too complex.
- ADR-0006 says "Private fields require `[JsonInclude]` or `IncludeFields = true`". **That statement
  was wrong.** `IncludeFields = true` includes public fields only, so every 2.0.0 copy lost private
  state.
- [ADR-0021](0021-postconditions-under-trimming-and-aot.md) measured both helpers under trimming and
  Native AOT. Under application defaults, the JSON copy did not work there at all. It also rejected
  "ask `Equals` first" (its Alternative 2) for types with no visible members.
- [Issue #40](https://github.com/cwouyang/uContract.NET/issues/40) lists comparison gaps that exist
  in every build. It was blocked: closing its gaps needs a copy that keeps private state and runtime
  types. This work unblocks it and resolves item 5, the value-type part of item 3 and the nested half
  of item 2.
- [Issue #39](https://github.com/cwouyang/uContract.NET/issues/39) asked for a source-generated
  serialization path. It is re-scoped (D11).

### Constraints

- No new dependency ([ADR-0011](0011-zero-dependency-principle.md)).
- No public API signature change. The `PublicAPI.*` baseline files are unchanged.
- The change is breaking. It targets 3.0.0 and lands under `[Unreleased]`; the release is decided
  separately.
- Not in scope: private fields declared on base classes, and members that a runtime type derived
  from `T` adds at the top level, are still not compared (#40 items 1 and 2-top-level).

---

## Decision

**`Old<T>()` copies by reflection: each object is a bitwise clone, then each reference-typed field
visible to reflection is replaced by its copy. `EnsureAssignable<T>()` compares by the rules R0–R8
below, on the values' runtime types, without recursion and with cycle tracking. This supersedes
ADR-0006.**

### Details

#### Decisions D1–D13

| # | Question | Decision |
|---|----------|----------|
| D1 | Approach | Reflection deep copy (ADR-0006 Alternative 2), superseding ADR-0006. Rejected: Java-parity JSON with a `JsonTypeInfo` modifier; documentation only. |
| D2 | Copy primitive | A bitwise clone of each object (`Object.MemberwiseClone`, reached through `[UnsafeAccessor]` in a non-generic static class). Then each reference-typed field visible to reflection is replaced by its copy. Rejected: an uninitialized object filled with `SetValue`. |
| D3 | Shared types | By category and by list (B3). Rejected: namespace-name heuristics for DI containers or EF. |
| D4 | System.Text.Json attributes | Ignored by `Old`. None are used in this repository or in ezDDD.NET. |
| D5 | Native AOT, fields hidden from reflection | No exception. A hidden field keeps its bitwise value, so what it refers to is shared. Documented, with the `DynamicDependency` remedy, and pinned by AotSmoke checks. |
| D6 | Scope | #45 includes the comparison rules R0–R8 needed to consume a faithful copy. #40 items 1, 2-top-level, 4, 6 and 7 stay open. |
| D7 | Streams | Every `Stream` is shared, `MemoryStream` and its subclasses included. Contents are not snapshotted. |
| D8 | Hash collections keyed by identity | A known limitation. No rebuild, because a rebuild runs comparers and user `GetHashCode`. |
| D9 | Members compared | Unchanged: public properties and instance fields. Fields-only comparison is evaluated in [#46](https://github.com/cwouyang/uContract.NET/issues/46). |
| D10 | Records | This ADR is the single record. ADR-0007, ADR-0021, ADR-0016, ADR-0020, ADR-0001 and ADR-0005 get short pointer amendments. ADR-0006 is superseded. |
| D11 | #39 | Re-scoped to "complete `Old`/`EnsureAssignable` under Native AOT": make hidden fields visible, for example with a source generator or annotations. |
| D12 | Version | 3.0.0 (breaking), together with #40. The release timing is separate. |
| D13 | A class with no members visible to reflection (Native AOT) | Ask `Equals` first. `true` means equal. `false` means "cannot compare", as before (R8). This re-adopts ADR-0021 Alternative 2. |

#### `Old<T>()`: required behaviour B1–B8

Unchanged: argument validation, `DBC_POST` handling, the recursion guard, a `null` supplier result
returning `default`, and supplier exceptions propagating unchanged.

- **B1 — Shape and runtime types.** The copy is a new graph with the same shape as the original.
  Every copied node has the same runtime type as its original, the top level included.
- **B2 — Fields.** Each copied object starts as a bitwise clone. So every instance field has the
  original's value: public or not, `readonly`, declared on any base class, backing fields, and fields
  hidden from reflection. Then each reference-typed field visible to reflection, on the type and on
  each base class, private included, is replaced by the copy of what it refers to.
  - "Reference-typed" means not a value type, not a pointer and not a function pointer. Pointer,
    function-pointer, `IntPtr`/`UIntPtr` fields and `fixed` buffers stay bitwise.
  - A value-typed field stays bitwise, except that the reference-typed fields visible inside it are
    replaced the same way, at any nesting depth.
  - A boxed value type (in an `object`, interface or `ValueType` field, or an `object[]` element) is
    cloned before its fields are replaced. The original's box is never written.
  - Static fields are never read or written. No user code runs: no instance or class constructor,
    property accessor, `Equals`, `GetHashCode` or serialization callback.
- **B3 — Shared instead of copied.** The copy refers to the original instance when its runtime type
  is one of these:
  - by category: derived from `CriticalFinalizerObject` (`SafeHandle`, `CriticalHandle`); a COM
    object (`Type.IsCOMObject`, false under Native AOT and on non-Windows platforms) or derived from
    `ComObject`; in the `System.Collections.Frozen`
    namespace; a CoreLib comparer (declared in CoreLib and implementing `IEqualityComparer`,
    `IComparer`, `IEqualityComparer<string>` or `IComparer<string>`);
  - by list, the type or a derived type: `string`; `Delegate`; `Type`, `MemberInfo`, `Assembly`,
    `Module`, `Pointer`; `Stream`; `WaitHandle`, `CancellationTokenSource`, `Thread`, `Timer`,
    `SynchronizationContext`, `SemaphoreSlim`, `ManualResetEventSlim`, `CountdownEvent`,
    `ReaderWriterLockSlim`, `Barrier`; `Task`; `WeakReference`; `Component`; `HttpMessageHandler`,
    `HttpClient`; `Socket`; `Regex`;
  - by open generic definition, matched along `BaseType`: `ThreadLocal<>`, `Lazy<>`,
    `WeakReference<>`, `ConditionalWeakTable<,>`, `AsyncLocal<>`;
  - by full name, matched along `BaseType`: `System.Threading.Lock` (.NET 9 and later runtimes; net8.0
    cannot reference it).

  Copying these gives a second object over the same OS handle, timer, callback list or lazy factory.
  Disposing or cancelling the copy then affects the original, and some getters block. The BCL
  compares default comparers by reference, so they must stay the same instance. `Regex`,
  `AsyncLocal<T>` and `Lock` were added after the final review (user decision 2026-10-07), because
  a copy of each differs from an unchanged original: a `Regex` caches a runner and its scratch state
  when it is used, an `AsyncLocal<T>` value is keyed by the instance in the `ExecutionContext` (a
  copy reads the default), and a `Lock` changes its spin and waiter state under contention. Without
  them, `EnsureAssignable(x, Old(() => x))` reports a false violation for an unchanged object. A plain
  `System.Object` (a lock) is copied as a new `object`.
- **B4 — Identity and cycles.** An instance reached more than once is copied once, and cycles are
  reproduced. Identity is tracked by reference only. The copier uses an explicit work list: a
  50 000-node chain copies on a thread with a 256 KB stack.
- **B5 — Arrays.** A copied array has the same runtime type, rank and bounds, non-zero lower bounds
  included. Its elements are copied by B2–B4. An array whose element type holds no reference is
  cloned without visiting its elements.
- **B6 — Finalizers.** Each clone is excluded from finalization right after it is made
  (`MemberwiseClone` registers finalizable clones). No copy ever runs a finalizer.
- **B7 — Errors.** Removed: the "cannot be serialized" and "reflection-based JSON serialization is
  disabled" `InvalidOperationException`s, `JsonException` for deep graphs, and the translation of
  `NotSupportedException`. `Old` throws nothing of its own while copying.
- **B8 — Attributes.** `T` gains `[DynamicallyAccessedMembers(PublicFields | NonPublicFields)]`. A
  caller that forwards an unannotated generic parameter to `Old` gets `IL2091`, unless it carries
  `[RequiresUnreferencedCode]`. `[RequiresDynamicCode]` is removed. `[RequiresUnreferencedCode]`
  stays, with this message:

  ```text
  Old<T> copies T field by field through reflection. Trimming preserves the fields declared on T. These may not be preserved: private fields of T's base classes, fields of the types that T's fields refer to, and fields of runtime types other than T. A field that is not preserved is copied bitwise, so an object it refers to is shared with the original. Preserve such types with [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(X))].
  ```

The copy is a read-only snapshot. Delegates are shared, so their targets are the original objects:
raising an event on the copy notifies the original's subscribers.

#### `EnsureAssignable<T>()`: comparison rules R0–R8

Unchanged: which members are compared (D9), the top level comparing the members of `typeof(T)`,
the assignable patterns, and the violation message format. A "pair" is the (actual, expected)
values reached through a member, a collection element or a field.

Order for a pair: same reference or a `null` side → R6 → pointer and `string` → R4 → R5 → R3 →
R1 memo → R7 or R8 → member walk. R1 and R2 govern the walk.

- **R0 — Runtime types.** Every branch decides on the values' runtime types, not on the declared
  member type.
- **R1 — Cycles and shared sub-graphs.** Pairs of references are tracked by reference for the whole
  call. The top-level pair is in progress for the whole call.
  - A pair reached again while it is in progress counts as equal.
  - A pair found different is remembered and counts as different wherever it is reached again. So a
    property and its backing field, or two members that share a changed object, are all reported.
  - A pair found equal without relying on an in-progress pair is remembered as equal.
  - A pair found equal while relying on in-progress pairs is remembered provisionally. It becomes
    equal when every pair it relied on ends equal, and is discarded when one of them ends different.
    This keeps cyclic graphs linear. This matches AssertJ's recursive comparison, which the Java
    original uses.
- **R2 — Depth.** The comparison uses an explicit stack, depth-first in member order. The reported
  member and the "cannot compare" path are the same as a recursive walk gives. A 50 000-node chain
  compares on a thread with a 256 KB stack.
- **R3 — Value types.** Primitives and enums are compared with `Equals`. A pointer field, read
  through reflection as a `System.Reflection.Pointer`, is compared by address with `Pointer.Equals`.
  Any other value type:
  1. if its `Equals` returns `true`, the pair is equal;
  2. otherwise, an `ArraySegment<T>` or `ImmutableArray<T>` is compared by its elements (R7). If one
     side cannot be enumerated (a default `ImmutableArray<T>`), it falls through to step 3;
  3. otherwise, if the type has fields visible to reflection and is not `[InlineArray]`, its fields
     are compared by these rules;
  4. otherwise the pair is unequal.

  An exception thrown by `Equals` propagates. A user-defined struct that implements `IEnumerable` is
  compared by its fields, so state besides its elements counts.
- **R4 — Delegates.** Equal if `Delegate.Equals` is true. Otherwise equal if the invocation lists
  have the same length and pairwise the same non-null `Method`. A `null` `Method`, or a
  `NotSupportedException` when reading it, means unequal. Targets are not compared.
- **R5 — Shared types.** Values of B3 types are compared by reference; their members are never
  read. A `string` is compared with `Equals`.
- **R6 — Runtime types differ.** Two non-null values of different runtime types are unequal, and no
  member of either is read. Exception: when both are `IEnumerable` and neither is a `string`, they are
  compared by R7. If enumerating either side throws `InvalidOperationException`, the pair is unequal.
- **R7 — Collections.** An `IDictionary` is compared entry by entry, in enumeration order, through
  `IDictionaryEnumerator.Key` and `.Value`. Any other `IEnumerable` is compared element by element,
  in order.
- **R8 — No members visible** (Native AOT, D13). For a nested class other than `System.Object`
  with no visible members: equal if `Equals` returns `true`. Otherwise `InvalidOperationException`:

  ```text
  EnsureAssignable cannot compare {type} (reached through '{path}'): no properties or fields are visible to reflection under Native AOT. Their Equals reports them unequal (without an Equals override this only means they are different instances), and their members cannot be listed. Ways out: list '{top-level member}' as assignable (patterns are regular expressions matched against top-level member names, so use the plain member name); if the type has members, preserve them, for example with [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(X))] where X is that type; or set DBC_POST=off (disables all postcondition checks).
  ```

  ADR-0021 quotes the 2.0.0 text, which lacks the second sentence. R8 applies to the nested walk
  only. When `T` itself has no visible members, the top-level message of ADR-0021 is unchanged.

#### Why ADR-0006's Alternative 2 now wins

ADR-0006 rejected a reflection copy as "overly complex for marginal benefit". Both halves of that
judgement changed:

- **The benefit is not marginal.** The JSON copy lost private state, runtime types and graph shape
  (Problem Statement). For a Design-by-Contract library, a copy that is wrong without a warning is
  worse than no copy. Under Native AOT, the JSON copy did not work at all with application defaults
  (ADR-0021).
- **The complexity is small.** A bitwise clone needs no per-collection code: `List<T>`,
  `Dictionary<,>` and user types are all handled as "objects with fields". Cycle detection is a
  reference-keyed map. The copier is about 200 lines.
- **The sketch in ADR-0006 is not what was built.** It created the copy with
  `Activator.CreateInstance` and `SetValue`. That runs constructors and leaves hidden fields at
  their default values. D2 clones instead.

#### Why ADR-0021's Alternative 2 is re-adopted (D13)

ADR-0021 rejected "ask `Equals` first" because a changed record would then report "cannot compare"
rather than a violation. R0 changes the balance. In 2.0.0, an interface-typed member that held an
equal record was compared with `Equals`, because dispatch used the declared type. With runtime-type
dispatch and no `Equals` step, that member would become a new "cannot compare" under Native AOT. R8
keeps it equal. The cost that ADR-0021 named is accepted: a changed hidden record now reports
"cannot compare" where 2.0.0 reported a violation.

---

## Native AOT: measured

Environment: `win-x64`, `-p:PublishAot=true -warnaserror`, clean `obj` and `bin` before each
publish. The smoke program `tests/uContract.AotSmoke` has 57 checks. Each check names its mutation
and its source of visibility.

| Build | ILC and MSBuild warnings | `--assert`, defaults | `--assert`, `DBC_POST=off` |
|---|---|---|---|
| Project reference | 0 | 57 of 57 pass | 57 of 57 pass |
| Project reference, `-p:AotSmokeHiddenDictionaryEntry=true` | 0 | 56 of 56 pass | 56 of 56 pass |

### What held

Every prediction of the design held under Native AOT:

- The core copy works: private state is kept (check 22), a private reference is copied when the
  visibility comes from `T` only (check 23) or through an annotated generic forwarder (check 24), and
  `Old(() => this)` in a base method keeps the derived runtime type and its field (check 25).
- A base class's private field is hidden. Its bitwise value is kept and its reference is shared. A
  base auto-property `List<Line>` changed by an indexer set after `Old` **passes silently** (check
  28). With `DynamicDependency(All, typeof(Derived))`, an `Add` is detected (check 29). An indexer
  set then reaches the hidden element type and reports "cannot compare" (check 30).
- A generic collection definition that no part of the program makes visible (`SortedList<,>`) is
  shared with stale counts. An indexer set on the original passes silently (check 31). An `Add`
  beyond the copy's count is detected (check 32).
- A nested unpreserved class keeps its bitwise value and shares its leaf (check 26). With
  `DynamicDependency` it is copied (check 27).
- `Dictionary` entries are compared through `IDictionaryEnumerator` (R7) (checks 33–35).
- With `Dictionary<,>.Entry` visible (default build), a field write on a dictionary value after
  `Old` is detected (check 36). A `string` in an `object` field is shared, not copied (check 41).
- R3: a changed `DateTime` and a changed `int` are violations (checks 42, 43).
- R4: the same method on a different target is equal (check 44).
- R8: a hidden record equal to its copy is equal, whether reached through its own type (check 13)
  or through an interface-typed member (check 53).
- R8: a hidden class whose `Equals` is reference equality reports "cannot compare", unchanged or
  changed (checks 10, 11), and so does a hidden list element type (check 14).
- Arrays, struct arrays and `Nullable<T>[]` are copied (checks 37–39).
- The `[UnsafeAccessor]` `MemberwiseClone` works, and a dropped copy is not finalized (check 40).
- The namespace rule for frozen collections works (checks 49, 50).
- A pointer field is boxed as `Pointer` and compared by address (checks 51, 52).
- The documented `User.ChangeEmail` pairing passes, and changing `_name` too is reported (checks 54,
  55). A cyclic pairing works (checks 56, 57).

### What the measurements add or correct

- **Check 36h measures a fully hidden `Dictionary<,>`.** The variant build preserves no
  `Dictionary`, so the copy shares the original's `_entries` array, and a change to a value passes
  silently. It does **not** measure an `Entry` hidden behind a visible `Dictionary`, as in
  `Old<Dictionary<K, V>>`. There, the `Entry[]` is cloned without visiting its elements, so keys and
  values are shared too. No build measures that case. CI does not publish the hidden-Entry variant;
  it is checked by hand.
- **The R4 branch for a `null` `Method` or a `NotSupportedException` is pinned by unit tests
  only.** The smoke fixture for it was a CoreLib-internal override (`ComparisonComparer<int>.Compare`).
  Under Native AOT its `Method` stays readable, so the fixture reaches the ordinary R4 path. Checks
  45 and 46 now pin that: `method=readable|ensure=passed`. No fixture was found that makes `Method`
  unreadable from a user program.
- **`DBC_POST=off` and the `Old` + `EnsureAssignable` pairing.** `Old` returns `default`, and
  `EnsureAssignable` validates its arguments before it reads the configuration. So the documented
  pairing throws `ArgumentNullException`. This is pre-existing (2.0.0 has the same order) and is
  tracked in [issue #47](https://github.com/cwouyang/uContract.NET/issues/47). The `DBC_POST=off`
  column of the pairing checks pins it.

### The 2.0.0 package on the same checks

Package mode (`-p:UContractPackageVersion=2.0.0`), defaults, `--assert`. These runs are not
expected to pass; they show what changed.

| Run | Result | Failing checks |
|---|---|---|
| 2.0.0 | 23 pass, 34 fail | 2, 3, 22–32, 35–41, 47–49, 51, 52, 54–57: "reflection-based JSON serialization is disabled". 13: "cannot compare AddressRecord" (no R8). 44, 45, 46: "cannot compare System.Func`…" (no delegate rule). 50: no exception (frozen sets compared by element). |
| 2.0.0, `JsonSerializerIsReflectionEnabledByDefault=true` | 24 pass, 33 fail | 3, 22–32, 35–41, 47–49, 54–57: "Type X cannot be serialized for Old<T>()". 51, 52: "`System.Int32*` … is invalid for serialization". 13, 44, 45, 46, 50: as above. |

For check 30, the exception type matched in both runs, but the message lacked the element type,
because the JSON error came first.

---

## Consequences

### Positive Consequences

- ✅ `Old` keeps private state, runtime types, identity and cycles. The documented `User.ChangeEmail`
  example works.
- ✅ `Old` works under trimming and Native AOT without `JsonSerializerIsReflectionEnabledByDefault`.
  `[RequiresDynamicCode]` and `IL3050` are gone.
- ✅ No user code runs while copying, and no copy runs a finalizer.
- ✅ `EnsureAssignable` no longer overflows the stack on cycles, deep graphs, multicast delegates or
  pointer fields. A test pins that a cyclic diamond ladder of depth 40 compares within one second;
  on 2.0.0 it overflowed the stack or hung.
- ✅ No false violations for unchanged dictionaries, structs holding references, or interface-typed
  members.
- ✅ System.Text.Json is no longer used by the library.

### Negative Consequences

- ❌ **Breaking.** `EnsureAssignable` reports more in some cases (distinct B3 instances, different
  runtime types, value-type and dictionary rules) and less in others (back-references, delegate
  targets, state inside B3 instances). The CHANGELOG lists each case.
- ❌ Under Native AOT, hidden fields are copied shallowly, and some changes pass silently (see Known
  Limitations).
- ❌ Under Native AOT, an interface-typed member that holds a changed hidden record reports "cannot
  compare" where 2.0.0 reported a violation (D13). A member declared as the record type already
  reported "cannot compare" in 2.0.0.
- ❌ Callers that forward a generic parameter to `Old` get `IL2091`.
- ❌ Everything reachable is copied, including service graphs. `Old(() => _balance)` is cheaper than
  `Old(() => this)`.

### Neutral Consequences

- ⚖️ System.Text.Json attributes such as `[JsonIgnore]` no longer limit the copy (D4).
- ⚖️ A lazily evaluated sequence is cloned as an iterator, not materialized.
- ⚖️ `object` members hold the boxed value, not a `JsonElement`.

---

## Known Limitations

Condensed from the design. "Accepted by the user" marks limitations that the maintainer accepted
explicitly on 2026-10-07.

- **Identity-hashed keys** (D8). A hash collection whose keys hash by identity enumerates, but
  lookups miss, also with the copy's own keys. This covers every key whose hash depends on an
  identity hash, including a record or tuple key that holds a class without a value hash
  (`record K(Owner O, string Name)`, `(Owner, int)`).
- **R8 trusts `Equals`** (accepted by the user). Under Native AOT, a nested class with no visible
  members is compared by its own `Equals`. An override that ignores state, such as entity equality by
  ID, makes a changed instance compare equal: the change passes silently. A JIT build reports a
  violation, and 2.0.0 reported "cannot compare". Preserve the type's members with
  `DynamicDependency` to compare it member by member.
- **Static sentinels.** Sentinel instances that are not B3 types (`DBNull.Value`, `EventArgs.Empty`,
  user "smart enum" statics) are copied. So `ReferenceEquals(old.X, DBNull.Value)` is false. Use
  `Equals` or an `is` pattern.
- **`[InlineArray]` structs and fixed-size buffers** (accepted by the user). Reflection and
  `ValueType.Equals` see only the first element. A change in a later element, with an equal first
  element, passes silently, as in 2.0.0. An inline array whose `Equals` is false is always reported.
- **Inline arrays of references.** With an `Old` copy, an `[InlineArray]` of reference-type elements
  is reported on every call, so list it as assignable.
- **Disposing a copy.** Disposing a copy of a user type that wraps a shared or bitwise-copied
  resource (`GCHandle`, `IntPtr`, a held `SpinLock`) affects the original.
- **Native AOT, hidden collection storage.** A collection whose storage is a nested struct or node
  type (`Dictionary<,>.Entry`, `HashSet<T>.Entry`, `ConcurrentDictionary` nodes,
  `LinkedListNode<T>`) needs that type's own fields visible; the annotation on `T` does not reach it.
  If they are hidden, the storage is copied bitwise, keys and values are shared, and a change to a
  value after `Old` passes silently.
- **Native AOT, hidden fields in general.** Base-class auto-properties and hidden collections pass
  silently for some mutations. Partial visibility is not detected; a struct is compared on its
  visible fields only.
- **Shared types** (B3, D7). State inside them is not snapshotted and compares by reference. This
  includes the `Value` of an `AsyncLocal<T>`: a value first set after `Old` passes, because the copy
  shares the instance that keys the value in the `ExecutionContext`.
- **Delegates.** Delegates whose `Method` is a shared trampoline compare equal even when their
  behaviour differs: `new D(d)`, `d.Invoke`, and interpreted or Native AOT `Expression.Compile`
  delegates.
- **Enumeration order.** A collection reordered without a content change, or two dictionaries of
  different types with a different order, is reported as changed (as in 2.0.0).
- **Other changes in what is reported.** `Memory<T>` is compared by its fields, so a change outside
  the window is reported. A getter that throws on a class reached through a struct field now
  propagates. A property that returns a new instance of its own type on every read makes the
  comparison grow without bound (fields-only comparison, #46, would remove it).
- **Concurrent mutation** of a non-concurrent collection during `Old` can give a torn copy.
- **Scope.** #40 items 1, 2-top-level, 4, 6 and 7 remain.

---

## Alternatives Considered

### Alternative 1: Java-parity JSON with a `JsonTypeInfo` modifier

**Description**: Keep System.Text.Json, add a `JsonTypeInfo` modifier that serializes all fields,
and deserialize as the runtime type, as the Java original does with Jackson.

**Pros**:
- Closest to the Java original.
- Fixes private state and the top-level runtime type.

**Cons**:
- Still loses graph shape, `object` members and nested runtime types without more converters.
- Still runs constructors and needs JSON reflection under Native AOT.

**Why rejected**: It fixes two of the four losses and keeps the Native AOT problem.

---

### Alternative 2: Documentation only

**Description**: Document the losses and keep the JSON copy.

**Why rejected**: It leaves a silent wrong copy in a library whose purpose is to make failures loud.

---

### Alternative 3: Uninitialized object filled with `SetValue`

**Description**: Create each copy with `RuntimeHelpers.GetUninitializedObject` and set every field
through reflection.

**Why rejected**: Fields hidden from reflection would become default instead of keeping their
value. Under Native AOT, creating the object runs class constructors. A bitwise clone keeps every
field.

---

### Alternative 4: Namespace heuristics for shared types

**Description**: Share instances from DI-container or Entity Framework namespaces.

**Why rejected**: Fragile, and outside the zero-dependency boundary. B3 uses categories and a list
of BCL types.

---

### Alternative 5: Rebuild hash collections keyed by identity

**Description**: After copying, rebuild hash collections so that lookups work.

**Why rejected**: A rebuild runs comparers and user `GetHashCode`, which breaks "no user code
runs" (D8).

---

### Alternative 6: Trust `Equals` overrides on value types, or ignore them

**Description**: Decide a value type by its `Equals` alone (FluentAssertions), or ignore `Equals`
overrides by default (AssertJ).

**Why rejected**: Trusting `Equals` alone keeps the false violations of `ValueType.Equals` for
structs that hold references. Ignoring overrides reports changes the type itself calls equal. R3
asks `Equals` first and compares fields only when it says "unequal". This is a deliberate departure
from both.

---

### Alternative 7: Runtime-type dispatch without R8

**Description**: Keep ADR-0021's rule: a class with no visible members always throws "cannot
compare".

**Why rejected**: An interface-typed member that holds an equal hidden record would become a new
"cannot compare" under Native AOT, where 2.0.0 compared it with `Equals` (D13).

---

## Prior Art

Surveyed, each at a pinned commit or tag (links under References): DeepCloner (`da61ac6`),
FastCloner (`c3fc2eb`, v1.2.6), FastDeepCloner (`5769dbc`), AnyClone (`d40a1bd`), Burtsev DeepCopy
(`bbe10e6`), Baksteen.DeepCopy (`0471fe3`), CloneExtensions (`4ce9574`), FluentAssertions
`BeEquivalentTo` (tag `8.11.0`, `9ed9dc0`), CompareNETObjects (`4ecf3dd`, v4.84) and AssertJ
`usingRecursiveComparison` (tag `assertj-build-3.27.7`).

- **Same as prior art**: a `MemberwiseClone`-based copy without user code (DeepCloner,
  Baksteen.DeepCopy); identity tracking by reference; shared delegates; shared `Task`,
  `CancellationTokenSource` and `WeakReference`; shared default comparers (DeepCloner
  `DeepClonerSafeTypes.cs`, FastCloner); coinductive cycle handling without a depth limit (AssertJ);
  strict runtime types (CompareNETObjects); identity-hashed keys left as a limitation
  (force-net/DeepCloner#39).
- **Ours alone**: `GC.SuppressFinalize` on copies (B6).
- **Deliberate departures**: R3's lenient value-type rule (Alternative 6); comparing dictionaries in
  enumeration order (R7). The latter is valid while the copy keeps the original's internal order,
  which a bitwise clone does.

---

## Related Decisions

- **Supersedes**: [ADR-0006 - Serialization and Deep Copy Mechanism for Old<T>()](0006-serialization-deep-copy.md) — the reflection copy replaces the JSON copy. ADR-0006's `IncludeFields` statement was wrong (see Relevant Context).
- **Supersedes (`Old` parts only)**: [ADR-0021 - Postcondition Helpers under Trimming and Native AOT](0021-postconditions-under-trimming-and-aot.md) — its `Old<T>()` decisions and `[RequiresDynamicCode]` statement are replaced. Its value-type, boxed-value and framework-type statements are settled by R0, R3 and R5. Its `EnsureAssignable<T>()` rules stand, and its measurements stay as the record. ADR-0021 stays `Accepted` and carries an amendment.
- **Amends**: [ADR-0007 - Reflection and Field Comparison for EnsureAssignable<T>()](0007-reflection-field-comparison.md) — the comparison rules R0–R8 and the iterative walk.
- **Amends**: [ADR-0016](0016-no-type-reference-overload-for-old.md), [ADR-0020](0020-contracts-enabled-by-default.md), [ADR-0001](0001-target-framework.md) and [ADR-0005](0005-generics-type-constraints-nullable.md) — their statements about the JSON copy.
- **Related to**: [ADR-0011 - Zero-Dependency Principle](0011-zero-dependency-principle.md) — checked; no amendment.

---

## Implementation Notes

- **Explicit work lists.** The copier keeps a reference-keyed map of copies and a stack of clones
  whose fields are still to be replaced. Recursion happens only per level of struct nesting, which
  the type bounds, not the graph. The comparison keeps a stack of walks (member, collection and
  dictionary walks). A difference ends every enclosing walk, and each one prepends its path segment
  to a blocked comparison on the way up.
- **The coinductive pair memo.** `ComparisonContext` keeps in-progress pairs with their depth, and
  sets of equal and different pairs. A pair found equal while relying on in-progress pairs joins the
  provisional group of the shallowest pair it relied on. When that pair ends, the group is committed,
  discarded, or handed over whole to the group it relied on. Groups are linked lists, so a hand-over
  takes constant time. A group handed over forwards to the group it joined, with path compression on
  lookup. A pair that could not be compared (R8) is remembered neither way.
- **`GC.SuppressFinalize` on every clone**, immediately after the clone and before it is recorded,
  so a later failure cannot leave a finalizable copy.
- **`[UnsafeAccessor]` `MemberwiseClone`.** `MemberwiseClone` is protected. An `[UnsafeAccessor]`
  `extern` method in the non-generic static class `DeepCopier` reaches it without reflection or
  dynamic code. Native AOT implements `UnsafeAccessor` in .NET 8; check 40 confirms the clone.
- **The `HoldsReferences` cache.** Whether a value type holds a reference at any depth is computed
  once per type and cached. It lets the copier skip struct fields and arrays (for example a large
  `byte[]`) that cannot refer to an object.
- **Pointers.** R3 compares pointers with `Pointer.Equals`. The design first named `Pointer.Unbox`;
  `Pointer.Equals` compares the same addresses and needs no `unsafe` code in the library.
- **Trimming suppressions.** The copier suppresses `IL2075` and `IL2070` in the existing
  `UnconditionalSuppressMessage` style. Each justification states the Native AOT consequence: hidden
  fields stay bitwise.
- **Order of the work.** The comparison rules came before the engine swap, so no commit leaves `Old`
  producing graphs that `EnsureAssignable` cannot compare. The intermediate commits are not release
  candidates.

### Amendment (2026-10-09): The DBC_POST=off pairing limitation is removed (#47)

**Superseded.** The entry "`DBC_POST=off` and the `Old` + `EnsureAssignable` pairing" under "What
the measurements add or correct" no longer holds. It said that the documented pairing throws
`ArgumentNullException` when postconditions are off. The pair now does nothing there: with
postconditions off, and in a call made while another contract check is running, `EnsureAssignable`
checks neither `actual` nor `expected`. The decision, its reason and the rejected alternatives are
recorded in the amendment of [ADR-0012](0012-dotnet-improvements-over-java.md). This amendment
records the measurements.

**The dated record stays.** The statement "57 checks" under "Native AOT: measured" and its result
table are the measurements of 2026-10-07. The smoke program now has 62 checks in the default build
and 61 in the `-p:AotSmokeHiddenDictionaryEntry=true` variant, which has no check 35. The pairing
checks (28 to 32, 35 to 37, 47, 48, 51, 52 and 54 to 57) now expect no exception in the
`DBC_POST=off` column. Their other column is unchanged. Five checks are new:

- Check 58: `EnsureAssignable` with a `null` `expected`. With the defaults, `ArgumentNullException`
  naming `expected`. With `DBC_POST=off`, no exception.
- Check 59: the same with a `null` `actual`, naming `actual`. With `DBC_POST=off`, no exception.
- Check 60: a `null` pattern array. It names `assignableFieldPatterns` in both columns. It passed
  before the change and after it, and pins that a call that does not compare still checks the array.
- Check 61: a `null` `expected` and a `null` pattern array. With the defaults, the exception names
  `expected`. With `DBC_POST=off`, it names `assignableFieldPatterns`.
- Check 62: the documented `User.ChangeEmail` pairing, called from inside the condition of
  `Contract.Require`. In both columns the condition runs to its end, and a contract nested after the
  pairing is still skipped: the pairing neither throws nor clears the recursion guard.

Checks 58 to 61 compare the `ParamName` of the exception, not its message.

**Measured results.** Environment: `win-x64`. The command was
`dotnet publish tests\uContract.AotSmoke\uContract.AotSmoke.csproj -c Release -r win-x64
-warnaserror`; the project file sets `PublishAot`. The `obj` and `bin` folders were cleaned before
each publish. The variant adds `-p:AotSmokeHiddenDictionaryEntry=true`. Each run exited with code 0.

| Build | ILC and MSBuild warnings | `--assert`, defaults | `--assert`, `DBC_POST=off` |
|---|---|---|---|
| Project reference | 0 | 62 of 62 pass | 62 of 62 pass |
| Project reference, `-p:AotSmokeHiddenDictionaryEntry=true` | 0 | 61 of 61 pass | 61 of 61 pass |

The tests were also run before the library changed, with the unit tests, the smoke checks and the
smoke table already changed (the RED run):

- Unit tests (JIT, whole suite, postconditions on): 584 tests, 580 passed, 4 failed. Two failures
  are the pair inside another contract's condition (it threw `ArgumentNullException` for `expected`)
  and a `null` `actual` inside a condition (it threw for `actual`). The other two are the rows of
  the null-pattern-array theory: the exception named `expected` and `actual`, not
  `assignableFieldPatterns`.
- Smoke program (JIT run of the default build, `DBC_POST=off`, `--assert`): 20 `FAIL`, 42 `PASS`,
  exit code 1. The failing checks are the 16 pairing checks and checks 58, 59, 61 and 62.

With the library changed, the unit tests pass: 584 of 584, and 585 of 585 after one further unit
test (a call made inside another contract's condition does not compare two different objects and
keeps the recursion guard). A second test-only pin is in the smoke program: the nested `Require` of
check 62. The JIT smoke run with `DBC_POST=off` gives 62 `PASS` and 0 `FAIL`.

**Not re-run.** The table "The 2.0.0 package on the same checks" is unchanged. Package mode was not
run again for this change.

**Limitation.** The postconditions-off path is tested only by the smoke program: in Linux CI, and by
hand on Windows. `Contract.Config` is read once for each process, and the unit test project cannot
turn postconditions off for a `Contract` call. The unit tests cover the call made while another
contract check is running. An implementation that handled only that call would pass every unit test.
A switch that works in the same process is [issue #35](https://github.com/cwouyang/uContract.NET/issues/35).

**Unchanged.** Every decision of this ADR.

### Amendment (2026-10-09): The top level compares null, and a string, a delegate or a nullable value as a whole (#52)

This amendment follows the #47 amendment above and carries the same date. It records how
`EnsureAssignable<T>()` compares `actual` and `expected` themselves after
[issue #52](https://github.com/cwouyang/uContract.NET/issues/52). A `null` side is decided first,
for every `T`. When `T` is a `string`, a delegate type or a nullable value type, two values that are
not `null` are compared as a whole: as one pair, by R0–R8. The decisions on `null` and on argument
validation are in the amendment of [ADR-0012](0012-dotnet-improvements-over-java.md) for #52. This
amendment records the comparison, the measurements and the limits.

**Qualified in the accepted text.** Each statement is quoted by its opening words:

- The Decision: "`EnsureAssignable<T>()` compares by the rules R0–R8 below, on the values' runtime
  types". At the top level a test on the declared `T` comes first. It selects whether those rules or
  the member walk compare the two arguments.
- "Unchanged: which members are compared (D9), the top level comparing the members of `typeof(T)`,
  the assignable patterns, and the violation message format." For the three kinds of `T` the top
  level is a pair, and the patterns do not apply. There are three more violation messages (A and B
  in the amendment of ADR-0012 for #52, C below).
- R0: "Every branch decides on the values' runtime types". At the top level a test on the declared
  `T` comes first.
- R1: "The top-level pair is in progress for the whole call." That does not hold for the three
  kinds of `T`. Their two values are not tracked as a pair of references.
- "R8 applies to the nested walk only. When `T` itself has no visible members, the top-level message
  of ADR-0021 is unchanged." R8 can now be raised below a nullable value type at the top level, with
  the text given below. The top-level message of ADR-0021 is not reached when `actual` or `expected`
  is `null`, or for the three kinds of `T`.
- The three scope statements on #40. Constraints: "members that a runtime type derived from `T` adds
  at the top level, are still not compared (#40 items 1 and 2-top-level)". D6: "#40 items 1,
  2-top-level, 4, 6 and 7 stay open." Known Limitations: "**Scope.** #40 items 1, 2-top-level, 4, 6
  and 7 remain." Item 2-top-level is now partly taken: see "Relation to #40 and #54" below.

**The rule.** The steps of a call, in order:

1. The pattern array is checked for `null`.
2. A call that does not compare returns: with postconditions off, or a call made while another
   contract check is running.
3. The `null` rule. Two `null`s are equal, and the method returns. Exactly one `null` is a
   violation.
4. The recursion guard is set. Then:
   - when `T` is a `string`, a delegate type or a nullable value type, the two values are compared
     as one pair by R0–R8. If they are unequal, the method throws
     `PostconditionViolationException` with message C;
   - for every other `T`, the members of `typeof(T)` are walked, as before.
5. The recursion guard is cleared, also when step 4 throws.

Steps 1 to 3 are recorded in the amendment of ADR-0012 for #52.

The declared type `T` decides whether the two values are compared as a whole. The test is:

- `typeof(T)` is `string`; or
- `typeof(Delegate).IsAssignableFrom(typeof(T))`, so `Delegate` and `MulticastDelegate` themselves
  count as delegate types; or
- `Nullable.GetUnderlyingType(typeof(T))` is not `null`.

For these three kinds of `T` the top level applies the whole "Order for a pair" of this ADR. For
every other `T` it applies only the "a `null` side" half of the first step of that order. It does
not apply "same reference": two references to one object are still walked member by member.

What R0–R8 give for two values that are not `null`:

| `T` | Compared by | Rule |
|---|---|---|
| `string` | `Equals`: ordinal and case-sensitive | R5 |
| a delegate type | Two delegates of different runtime types are unequal. That is possible when `T` is `Delegate` or `MulticastDelegate`, and for a variant generic delegate (an `Action<object>` held as an `Action<string>`). Otherwise `Delegate.Equals`; if it is false, the methods of the two invocation lists. Targets are not compared. A method that cannot be read makes the pair unequal | R6, R4 |
| a nullable value type | R3, applied to the two boxed values of the underlying type: a primitive or an enum with `Equals`; any other value type by steps 1 to 4 of R3 | R3 |

Message C is the `Description` of the exception. `{T}` is `typeof(T)` formatted with
`Type.ToString()`, as in the existing "cannot compare" messages: for example `System.String`,
`System.Action`, ``System.Nullable`1[System.Int32]``. The message holds no value of either argument.

```text
actual and expected are not equal. {T} is compared as a whole, so assignable patterns do not apply.
```

Below a nullable value type, the comparison can reach a class with no members visible under Native
AOT: in a field of the underlying type, or in an element of a collection in such a field. When that
class's `Equals` reports the values unequal, the method throws `InvalidOperationException`. The
text is the one of R8 without the way out "list … as assignable", because no member can be listed.
`{path}` starts with the name of the underlying type, not with `typeof(T).Name`:

```text
EnsureAssignable cannot compare {type} (reached through '{path}'): no properties or fields are visible to reflection under Native AOT. Their Equals reports them unequal (without an Equals override this only means they are different instances), and their members cannot be listed. Ways out: if the type has members, preserve them, for example with [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(X))] where X is that type; or set DBC_POST=off (disables all postcondition checks).
```

What holds for a comparison as a whole:

- It runs with the recursion guard set, as the member walk does. Below a nullable value type it can
  run the `Equals` of a user struct, and the property getters, enumerators and `Equals` of what the
  fields of the underlying type refer to. An exception from any of them propagates as it does from
  the member walk (one from a getter is wrapped in a `TargetInvocationException`), and the guard is
  cleared.
- It reads no member metadata of `T`. So the rule "no members visible" of ADR-0021 is not reached
  for `T`.
- The patterns are not used. The elements of the pattern array are not examined, so a `null`
  element or an invalid regular expression in it is not reported. The array itself is still checked
  for `null` first.

What changes, all in a call that compares:

- `T` = `string`. The two strings are compared with `Equals`. Before, the members of `string`
  visible to reflection were compared: `Length`, `_stringLength` and `_firstChar`. So two different
  strings of the same length and first character were equal by accident. A difference gives message
  C, not a list of those members.
- `T` = a nullable value type with a value on both sides. The values are compared. Before, the
  method threw `NotSupportedException`.
- `T` = a delegate type. Before, `Method`, `Target` and the fields visible through `T` were
  compared, and `Target` by its members. Now less is reported in one case: the same method on
  another target whose state differs is equal. And more is reported in another: every entry of a
  multicast delegate counts (R4). This was read from the code; no measurement or test compares a
  multicast delegate as `T`. A difference gives message C, not a list of delegate members.
- For these three kinds of `T`, a pattern that matched a member name no longer excuses a
  difference.

Nothing changes when `T` is any other type and both values are not `null`:

- `T` = `object`. `object` has no members, so nothing is compared beyond `null`, even for two
  different strings. This is about a value passed as `actual` and `expected`. A member typed as
  `object` inside the compared object is compared as the type it holds (R0).
- A base class as `T`. The members that it declares or inherits are compared (private fields of its
  own base classes excepted), not those that the runtime type adds.
- An interface as `T`. Only the properties that the interface itself declares are compared.
- A shared type (B3) other than `string` or a delegate as `T`, such as a `Stream`, a `Task` or a
  frozen collection. Its members are walked.
- A value type without `?` as `T`. Its members are walked: `EnsureAssignable(1, 2)` still reports
  the field `m_value` (measured on `669499c`).

**Decisions and reasons.** The maintainer decided them on 2026-10-09, except where a line says
otherwise.

- **A `string`, a delegate type and a nullable value type as `T` are compared as one pair.** These
  are the types that a member that can be `null` usually has. The amendment of ADR-0012 for #52
  makes the pair `EnsureAssignable(_name, Contract.Old(() => _name))` usable for such a member.
  With the member walk, that pair missed a `string` that changed but kept its length and first
  character, and it threw for an `int?` with a value on both sides (see "Measured on `669499c`"
  below). So the two were decided in one change.
- **A nullable value type with a value on both sides is compared by R3**, exactly as a member of
  the underlying type is. Equal values then need no reflection.
- **The declared type `T` decides.** For each of the three kinds, a value that is not `null` has a
  runtime type of the same kind: `string` is sealed, a value of a delegate type is a delegate, and a
  boxed nullable value is a boxed value of the underlying type. So the declared type and the runtime
  type cannot disagree about the kind. `Delegate` and `MulticastDelegate` count as delegate types.
  With them as `T`, the two delegates can have different runtime types, and R6 makes them unequal. A
  unit test pins that with an `Action` and a `ThreadStart` over one method on one target: the result
  is message C with `System.Delegate`.
- **Other shared types are not compared as a whole.** `Stream`, `Task`, `Regex`, frozen
  collections, comparers and the rest of B3 keep the member walk at the top level. They stay with
  #40 and #54. See the first rejected alternative.
- **The assignable patterns do not apply to a comparison as a whole, and message C says so.** A
  pattern names a member of `T`. One pair has no member to name.
- **Message C names the parameters and the type**, and makes no statement about what happened
  before the call, like messages A and B.
- **The comparison runs with the recursion guard set**, after the pattern check and the `null`
  rule. It can run user code, as the member walk can. (Derived from the code, not a maintainer
  decision.)
- **No shortcut for the same reference is added for other types.** When `T` is not one of the three
  kinds and both values are not `null`, nothing changes. A shortcut would stop a call with one
  instance on both sides from reaching the rule "no members visible" (item 6 of #40, at the top
  level), and would hide #54. A unit test pins one case under a simulated Native AOT runtime: a `T`
  with no visible members still throws when both sides are the same instance. (Derived from the
  code, not a maintainer decision.)
- **Under Native AOT, a nullable value type whose underlying type has no visible fields, and whose
  `Equals` says "unequal", is a violation with message C.** That is what step 4 of R3 gives for a
  member. It is not reported as "cannot compare". See the last rejected alternative.

**Known consequences of comparing a nullable value type by R3.** They are accepted and documented.

- **A nullable value type is compared less strictly than the same type without `?`.**
  `EnsureAssignable<DateTime>` walks the fields and reports a changed `Kind`. `DateTime.Equals`
  ignores `Kind`, so `EnsureAssignable<DateTime?>` does not report it. The same holds for a
  `DateTimeOffset?` with a changed offset, for `1.0m` against `1.00m`, and for a user struct whose
  `Equals` ignores state. Members of these types already behave so.
- **The patterns cannot name a field of a nullable struct.** The way out is to test both values for
  `null` and to pass the unwrapped values:

  ```csharp
  if (_price is { } now && oldPrice is { } before)
      Contract.EnsureAssignable(now, before, "Currency");   // T = Money: members and patterns
  else
      Contract.EnsureAssignable(_price, oldPrice);          // the null rule
  ```

  `_price.Value` and `oldPrice.Value` must not be passed. `.Value` throws
  `InvalidOperationException` when there is no value, and `Old` returns a value without one with
  postconditions off and in a call made while another contract check is running. The user
  documentation gives this form and never advises `.Value`.
- **Under Native AOT, the fields of the underlying type may not be preserved.** The annotations on
  `T` name the members of the nullable type, not those of the underlying type, for
  `EnsureAssignable<T>()` and for `Old<T>()` alike. Whether the fields are visible then depends on
  the rest of the application. When they are not, an underlying type that holds a reference (a
  struct with a `List<T>` field) goes wrong in two directions:
  - two values built separately with equal content are a violation: `Equals` says "unequal", and no
    field is visible (step 4 of R3);
  - with an `Old` snapshot, a change inside the object that the field refers to is missed. `Old`
    cannot replace a field that it cannot see (D5), so the snapshot shares the object with the
    original, and `Equals` says "equal".

  An underlying type without reference fields (`int`, `DateTime`, `Guid`, a struct of such values)
  is not affected: `Equals` decides correctly without reflection. The ways out are to preserve the
  underlying type with `DynamicDependency`, or the two-branch form above: the annotation on `T` =
  `Money` preserves the fields of `Money`. Smoke checks 70 and 72 were written to measure the two
  directions. They measured the opposite: see "Checks 70 and 72" below.

**Rejected alternatives.**

- **Every shared type (B3) as a `T` that is compared as a whole.** An earlier form of this change
  took `SharedTypes.IsShared(typeof(T))` as the test. The declared type and the runtime type can
  then disagree:
  - The B3 test is true for the interfaces `IEqualityComparer`, `IComparer`,
    `IEqualityComparer<string>` and `IComparer<string>`, and false for a user class that implements
    them. Such a pair would be walked member by member by R0–R8, with no defined result when its
    class has no visible members.
  - Two frozen sets with equal content compared equal as `T` on `669499c`. By reference they would
    be a violation that no pattern could excuse.
  - A shared value type (an enumerator in `System.Collections.Frozen`) would never be equal: each
    side is boxed separately.
  - With `Old`, the two sides of a shared type are the same instance. The comparison could only
    show that the member was reassigned.
- **Compare a nullable value type as `T` = its underlying type.** The members of the underlying
  type would be walked, and the patterns would apply. But the annotation on `T` preserves the
  members of the nullable type for trimming, not those of the underlying type. Under Native AOT an
  `int?` or a `DateTime?` could then throw "no members visible" even when both sides are equal.
  With R3, equal values need no reflection.
- **Decide on the runtime type at the top level.** `EnsureAssignable<object>("a", "b")` would
  change, and so would every call whose declared type is a base class or an interface. That is item
  2 of #40 as a whole.
- **An `ArgumentException` for patterns passed with one of the three kinds of `T`.** It would show
  the misuse at once. But a call that runs today would start to throw when the values are equal,
  and the method would need a second kind of argument validation. Message C tells the caller
  instead.
- **"Cannot compare" for a nullable value type whose underlying type has no visible fields.** A
  review proposed `InvalidOperationException` instead of message C when `Equals` says "unequal" and
  no field is visible under Native AOT, because message C names no member and the patterns cannot
  excuse it. But the usual underlying type holds only values (a `Money`, a `DateTime`), and for it
  "unequal" is reliable. A real change would be reported as "cannot compare" until the type is
  preserved. The top level and a member would also differ.

**Why an amendment and not a new ADR.** The main decision of this ADR stands: R0–R8, decided on
runtime types. It is applied one level higher, for a closed set of `T`. `docs/adr/README.md` gives
the amendment as the form for "a later decision that leaves the ADR's main decision standing". The
same file lists breaking changes under "Requires ADR". The amended ADRs (ADR-0012, ADR-0022,
ADR-0021 and ADR-0007) are that record. The three scope statements of this ADR on #40 are reversed
in part, and this amendment says so above. Issue #52 itself asks for an amendment of ADR-0012. A
comment on #40 of 2026-10-06 says that "the ADR README asks for a new ADR rather than an amendment
for a new decision". The rest of item 2 of #40 is that case: it would change what every top-level
comparison does.

**Superseded in the #47 amendment.** Each statement is quoted by its opening words:

- "The smoke program now has 62 checks in the default build and 61 in the" variant. It has 72 and
  71.
- The results with the defaults of check 58 ("With the defaults, `ArgumentNullException` naming
  `expected`"), of check 59 ("naming `actual`") and of check 61 ("With the defaults, the exception
  names `expected`"). The list of checks below gives the results now.
- "Checks 58 to 61 compare the `ParamName` of the exception, not its message." Checks 58 to 61 and
  63 to 72 now report one of three outcomes: no exception, a postcondition violation, or the
  `ParamName` of an `ArgumentNullException`.

Its result table, its unit-test figures and its other statements are the dated record of #47 and
stay.

**Measured on `669499c`.** These were measured while the change was designed, on `origin/master`
at `669499c`, with throwaway tests that were not committed:

| Call | Result on `669499c` |
|---|---|
| `EnsureAssignable("abc", "abd")` | no exception (reflection shows `Length`, `_stringLength` and `_firstChar`) |
| `EnsureAssignable("abc", "abcd")` | violation listing `Length` and `_stringLength` |
| `EnsureAssignable("abc", "abd", ".*")` | no exception |
| `EnsureAssignable<int?>(1, 1)`, `<int?>(1, 2)`, `<DateTime?>` with equal values, a user struct `Holder?` | `NotSupportedException` ("Specified method is not supported") |
| `EnsureAssignable(1, 2)` (`T` = `int`) | violation listing the field `m_value` |
| `T` = `Action`, two delegates for different methods | violation listing `Method`, `_target`, `_methodBase`, `_methodPtrAux` |
| `T` = `Action`, one instance method on one target, or on two targets with equal state | no exception |
| `Action a = M; Action b = M;` for a static method `M` | one cached instance (`ReferenceEquals` is true); an instance method group gives two instances |
| two `FrozenSet<string>` with equal content, different instances, as `T` | no exception |
| `EnsureAssignable<DateTime>`, equal ticks, `Kind` `Utc` against `Local` | violation listing `Kind` and `_dateData`; `DateTime.Equals` returns true |
| `class Worker : Component` with `Old(() => this)` | `Old` returns the same instance, so the pair never fails (issue #54) |

The cached static delegate matters for the tests: a test or a smoke check that builds its two
delegates from a static method group passes one instance twice and proves nothing. The delegate
fixtures use an instance method group.

**Measured results, unit tests.** JIT, whole suite, postconditions on. The suite had 585 tests on
`669499c`. One theory row was removed and 43 cases were added, so it has 627. The change was made in
three steps. For each step the tests were run before the library changed, with the tests of that
step already written (the RED run), and after:

| Step | Tests | Before: failed / passed | After |
|---|---|---|---|
| The `null` rule | 599 | 20 / 579 | 599 of 599 pass |
| A `string`, a delegate type or a nullable value type as a whole | 625 | 19 / 606 | 625 of 625 pass |
| `EnsureImmutableCollection<T>()` | 627 | 2 / 625 | 627 of 627 pass |

Two more cases were added after the last review, and both passed on their first run: two strings
that differ only in letter case are a violation, and a nullable value type without a value still
throws `ArgumentNullException` from `EnsureImmutableCollection<T>()` when the method checks the
collection. The suite then has 629 tests.

- 20 failures were predicted for the second step. 19 were observed, in two runs. The test of
  a nullable struct whose `Equals` throws passed before the change. Before the change, the member
  walk over the nullable type read the properties `HasValue` and `Value` from the boxed value. That
  worked. It compared the two `Value` results by R3, and so called the struct's `Equals`, which
  threw the exception of the fixture. `NotSupportedException` came later, from reading the fields
  `hasValue` and `value`. So before the change the `Equals` of the underlying type already ran once
  before the call failed, and the test saw the exception that it expects.
- 8 of the added cases pass before and after, and are pins: the same instance with no visible
  members; equal strings in different instances; two delegates for one method on one target; equal
  frozen sets; `T` = `object` with two strings; the pair on a string member that does not change; a
  `null` pattern array with two strings; and the nullable struct whose `Equals` throws.
- Two predictions that were traced in the code, not measured on `669499c`, held. The same instance
  method on two targets whose state differs was a violation listing `Target` and `_target` before,
  and is equal after. One method through two delegate types (`Action` and `ThreadStart`) with `T` =
  `Delegate` gave no exception before, and is unequal after.

**Measured results, smoke program.** A JIT run of the default build with `DBC_POST=off` and
`--assert` gave 62 `PASS` before the work, 64 after the first step and 71 after the second. In the
third step the RED run gave 1 `FAIL` and 71 `PASS`: check 64 expected no exception and got an
`ArgumentNullException` naming `collection`. With the library changed it gave 72 `PASS`. The results
with the defaults were not observed RED in the smoke program; the unit tests are the RED evidence
for them.

Native AOT. Environment: `win-x64`. The command was
`dotnet publish tests\uContract.AotSmoke\uContract.AotSmoke.csproj -c Release -r win-x64
-warnaserror`; the project file sets `PublishAot`. Clean `obj` and `bin` before each publish. The
variant adds `-p:AotSmokeHiddenDictionaryEntry=true`. Every publish exited with code 0.

The first publish had checks 70 and 72 with the predicted expectations:

| Build | ILC and MSBuild warnings | `--assert`, defaults | `--assert`, `DBC_POST=off` |
|---|---|---|---|
| Project reference | 0 | 70 of 72 pass (checks 70 and 72 fail) | 72 of 72 pass |
| Project reference, `-p:AotSmokeHiddenDictionaryEntry=true` | 0 | 69 of 71 pass (checks 70 and 72 fail) | 71 of 71 pass |

The second publish had the expectations of checks 70 and 72 changed to the measured results:

| Build | ILC and MSBuild warnings | `--assert`, defaults | `--assert`, `DBC_POST=off` |
|---|---|---|---|
| Project reference | 0 | 72 of 72 pass | 72 of 72 pass |
| Project reference, `-p:AotSmokeHiddenDictionaryEntry=true` | 0 | 71 of 71 pass | 71 of 71 pass |

The program has 72 checks in the default build and 71 in the variant, which has no check 35.

**Checks 70 and 72: predicted one way, measured the other.** Both checks use `ListHolder`, a struct
with a `List<int>` field, as a nullable value. No attribute in the smoke program preserves
`ListHolder`, and the annotation on `T` names only the members of `Nullable<ListHolder>`. From that,
it was predicted that the field of `ListHolder` is hidden under Native AOT. Then check 70 (equal
contents in different lists) would be a violation, and check 72 (the pair on a value whose list
changes in place) would report nothing. Those are the two directions of the known consequence
above. The first publish measured the opposite. Check 70 gave no exception, and check 72 gave a
violation. So the field of `ListHolder` is visible to reflection in this program, and both checks
give the results of the JIT. The expectations were changed to the measured results, and the second
publish passed. Why the field is visible in this program was not determined.

The documentation keeps "may not be preserved" for the fields of the underlying type. One program's
result does not show what another application preserves. ADR-0021 says the same of a framework
type's members ("Whether a framework type's members are visible depends on the application"). So
the two directions of the known consequence are a prediction from the annotations. No run has shown
them, and no run has shown the violation with message C for an underlying type with no visible
fields.

**The smoke checks changed or added.** Each reports no exception, a violation, or the `ParamName`
of an `ArgumentNullException`. Checks 60 and 62 are unchanged.

- Check 58: `EnsureAssignable` with a `null` `expected`. With the defaults, a violation; it was an
  `ArgumentNullException` naming `expected`. With `DBC_POST=off`, no exception.
- Check 59: the same with a `null` `actual`. With the defaults, a violation; it was an
  `ArgumentNullException` naming `actual`. With `DBC_POST=off`, no exception.
- Check 61: a `null` `expected` and a `null` pattern array. It names `assignableFieldPatterns` in
  both columns; with the defaults it named `expected`. It pins that the pattern check comes first.
- Check 63: `EnsureAssignable<FlatType>(null, null)`. No exception in both columns. It pins that
  two `null`s are equal.
- Check 64: `EnsureImmutableCollection<ImmutableList<string>>(null)`. With the defaults,
  `ArgumentNullException` naming `collection`. With `DBC_POST=off`, no exception. It is the only
  test of that method with postconditions off.
- Check 65: `EnsureAssignable("abc", "abd")`. With the defaults, a violation. With `DBC_POST=off`,
  no exception. The two strings have the same length and first character.
- Check 66: `EnsureAssignable<int?>(1, 1)`. No exception in both columns.
- Check 67: `EnsureAssignable<DateTime?>` with two different dates. With the defaults, a violation.
  With `DBC_POST=off`, no exception.
- Check 68: `EnsureAssignable<DateTime?>` with two equal dates. No exception in both columns.
- Check 69: `EnsureAssignable<Action>` with two delegates for one instance method on one target. No
  exception in both columns. The helper reports `same-instance` when the two delegates are one
  reference. It did not, so two distinct delegates were compared.
- Check 70: `EnsureAssignable<ListHolder?>` with equal contents in different lists. No exception in
  both columns (measured, see above).
- Check 71: `EnsureAssignable<Marker>(new Marker(), null)`; `Marker` is a class with no members.
  With the defaults, a violation. With `DBC_POST=off`, no exception. It shows under Native AOT that
  the `null` rule comes before the rule "no members visible": with two `Marker` values that are not
  `null`, that rule would throw.
- Check 72: `Old` and `EnsureAssignable` on a `ListHolder?` whose list changes in place. With the
  defaults, a violation (measured, see above). With `DBC_POST=off`, no exception.

Checks 63 to 72 use fixtures that no other check uses, or `FlatType`, which is already the `T` of
other checks. So no fixture that must stay hidden becomes visible to reflection.

**Limits.** Checked by reading the code, and not pinned by a test:

- A call of `EnsureImmutableCollection<T>()` with postconditions off and made while another
  contract check is running.
- That the `null` rule and the comparison as a whole read no metadata of `T`. The cache is private.
  A `string`, a delegate type and a nullable value type always have visible members, so an
  implementation that still ran the rule "no members visible" for them would give the same results.
- That the `null` rule runs before the recursion guard is set. It runs no user code, so the
  position cannot be observed. The unit tests show that the guard is not left set.
- A comparison as a whole that reaches a class with no visible members under real Native AOT. A
  unit test simulates it and compares the whole message. It uses a direct field of the underlying
  type. A class reached through an element of a collection is not tested; the path is built by the
  code that R8 already uses.
- `T` = `MulticastDelegate`. A unit test covers `T` = `Delegate`. A test on the base type that left
  out `MulticastDelegate` itself would pass.
- That `EnsureAssignable<int?>` and `EnsureAssignable<int>` give different messages.
- A multicast delegate as `T`.
- That a nullable value type whose underlying type has no visible fields and whose `Equals` says
  "unequal" gives message C. Read from the code (step 4 of R3); no unit test and no Native AOT run
  shows it.

Known limits of the behaviour:

- **`int` against `int?`, and `DateTime` against `DateTime?`.** A value type without `?` as `T` is
  walked by its members, and its patterns can name fields of a user struct. The same type with `?`
  is compared as a whole. So the two give different messages for the same change, and can give
  different results (the first known consequence above).
- **A nullable value type whose underlying type is a shared value type** (an enumerator in
  `System.Collections.Frozen`) is never equal. Each side is boxed separately, and a shared type is
  compared by reference (R5). It is not handled; such a `T` has no use.
- **The advice of the unreadable-property message cannot be followed below a nullable value type.**
  A property of a class below the underlying type that has no visible get method still throws the
  message of ADR-0021. Its advice to list the top-level member as assignable does not apply: there
  is no member to list. The rest of the message still applies (preserve the type if trimming
  removed the getter, or set `DBC_POST=off`), and passing the unwrapped values makes the member
  listable.

**Relation to #40 and #54.**

- **Taken from #40**: the delegate half of its comment that a `T` that is itself a shared type or a
  delegate walks its members; a `string` as `T`, which is one shared type; and the nullable value
  type with a value on both sides, which a comment on #40 of 2026-10-09 measured. #40 files all of
  these under its item 2 (top level). So that item is partly taken.
- **Left in #40**: the rest of item 2. `EnsureAssignable<object>("abc", "abd")` compares nothing. A
  base class as `T` compares the members it declares or inherits, and an interface only its own
  properties; in neither case what the runtime type adds. A shared type other than `string` or a
  delegate as `T` walks its members. Items 1, 4, 6 (top level) and 7, and the byref-like property,
  are untouched.
- **[Issue #54](https://github.com/cwouyang/uContract.NET/issues/54)** was opened while this change
  was designed. A class derived from a shared type (`Component`, `Stream`, `Task`) is not copied by
  `Old`, so the pair never fails in it. This change does not touch it.

**Not re-run.** The table "The 2.0.0 package on the same checks" is unchanged. Package mode was not
run again for this change. The CHANGELOG states no 2.x result that was not read from the 2.0.0
source. `linux-x64` was not measured locally; CI publishes and runs the smoke program there.

**Unchanged.** Every other decision of this ADR.

### Amendment (2026-10-10): One shared instance on both sides is reported, not compared (#54)

This amendment follows the #52 amendment above. It records what `EnsureAssignable<T>()` does after
[issue #54](https://github.com/cwouyang/uContract.NET/issues/54) when `actual` and `expected` are
one instance of a type that `Old<T>()` shares. `Old<T>()` does not copy an instance of a B3 type,
and B3 matches a listed type and every type derived from it. So in a user class that derives from
`Component`, `Stream` or `Task`, the pair `EnsureAssignable(this, Contract.Old(() => this))`
compared the object with itself. For most such classes the call passed, whatever the method changed.
For the shared types whose state can change, the call now throws `InvalidOperationException`.
`Old<T>()` and B3 do not change. The amendments of 2026-10-10 of
[ADR-0012](0012-dotnet-improvements-over-java.md),
[ADR-0021](0021-postconditions-under-trimming-and-aot.md) and
[ADR-0007](0007-reflection-field-comparison.md) point here.

**Terms.** This amendment uses these terms:

- **A call that compares**: a call of `EnsureAssignable<T>()` with postconditions on that is not
  made while another contract check is running.
- **Same instance**: `ReferenceEquals(actual, expected)` is true, and neither is `null`.
- **Shared type**: a type that B3 names, by category, by list, by open generic definition or by full
  name. `Old<T>()` returns an instance of a shared type itself, not a copy.
- **State-holding entry**: one of the six entries of B3 that are listed under "The state-holding
  entries" below.
- **State-holding shared type**: a type that matches at least one state-holding entry.
- **Matched entry**: the first state-holding entry that a state-holding shared type matches, in the
  order of that list.
- **Fixed shared type**: a shared type that matches no state-holding entry.
- **Rule S**: the rule that this amendment adds. It is given under "The rule" below.
- **Message D**: the message of the exception that rule S throws. It is given under "Message D"
  below. The letter follows messages A and B (the amendment of ADR-0012 for #52) and message C (the
  #52 amendment above). The user documentation gives the message no letter.
- **`71b2e4e`**: the commit of `master` on which the design was measured, before the library
  changed.

**The rule.** One rule is added, rule S:

> In a call that compares, when `actual` and `expected` are the same instance and its runtime type
> is a state-holding shared type, the call throws `InvalidOperationException` with message D.

The steps of a call, in order. Rule S is step 4. Steps 1 to 3 are steps 1 to 3 of the #52 amendment
above. Steps 5 and 6 are the two branches of its step 4. Its step 5 is unchanged and follows: the
recursion guard is cleared.

1. The pattern array is checked for `null`. A `null` array throws `ArgumentNullException`, in every
   call.
2. A call that does not compare returns.
3. The `null` rule. Two `null`s are equal, and the method returns. Exactly one `null` is a
   violation.
4. Rule S.
5. When `T` is a `string`, a delegate type or a nullable value type, the two values are compared as
   a whole.
6. For every other `T`, the members of `typeof(T)` are walked.

What holds for rule S:

- **Position against the guard.** Rule S is evaluated before the recursion guard is set, as the
  `null` rule is. It runs no user code, so the position cannot be observed.
- **Two different instances.** Rule S does not apply. The call behaves as on `71b2e4e`.
- **Patterns.** Rule S applies with any patterns, a pattern that matches every member included. The
  elements of the pattern array are not examined, so a `null` element or an invalid regular
  expression is not reported in such a call. On `71b2e4e` the pattern `"["` gave a
  `RegexParseException` and a `null` element an `ArgumentNullException` for `pattern` (both
  measured, see "Measured" below).
- **Declared type.** `T` does not matter for rule S. `EnsureAssignable<object>(worker, worker)` and
  `EnsureAssignable<IDisposable>(worker, worker)` throw the same way.
- **Types compared as a whole.** A `string` and a delegate are fixed shared types. The two sides of
  a nullable value type are two boxes, and never the same instance. So rule S and the comparison as
  a whole never both apply, and their relative order cannot be observed. The unit tests of #52 for
  the same `string` and the same delegate on both sides stay as they are.
- **Value types.** When `T` is a value type, each side is boxed separately, so the two are never the
  same instance. The implementation skips rule S for such a `T`. When `T` is `object` or an
  interface, one box can be passed on both sides. Its runtime type is a value type, and no
  state-holding shared type is a value type: every state-holding entry is a class, or a test that
  only a class satisfies. The shared value types (enumerators in `System.Collections.Frozen`) are
  fixed. So rule S never applies to a value.
- **User code.** Rule S reads the runtime type and classifies it from data of the `Type` object
  (`IsAssignableFrom`, `BaseType`, `FullName`, `IsCOMObject`). It reads no member of the value and
  runs no user code. A getter that throws and a `Lazy<T>` factory are no longer reached in such a
  call. The classification adds no cache keyed by `Type` and no mutable static state.
- **Native AOT.** Rule S does not depend on which members are visible to reflection. It comes before
  the rule "no members visible" for `T`: with the same instance of a state-holding shared type on
  both sides, a `T` with no visible members gets message D. `Type.IsCOMObject` is false under Native
  AOT and off Windows (B3). A COM object is state-holding wherever one can exist.

What does not change:

- The same instance of a fixed shared type, or of a type that is not shared. The call behaves as on
  `71b2e4e`. For most `T` that is "no exception". For `T` = `Type` it is a
  `TargetInvocationException` from a getter (measured). A `T` with no visible members under Native
  AOT still throws "no members visible" when both sides are the same instance of a type that is not
  shared. A unit test pins that.
- Two different instances of a shared type as `T`. Their members are walked, as on `71b2e4e`.
- A member, an element or a field that holds a shared instance. R5 compares it by reference and
  reads none of its members (a `string` and a delegate excepted: `Equals` and R4). State inside it
  is not compared.
- `Old<T>()`. It shares the same types as before, and it throws nothing of its own while copying.

**Decisions and reasons.** The maintainer decided S1 to S3 on 2026-10-10, before the design was
written, and S9 and S10 on 2026-10-10, after the design review. S4 to S8 follow from them and from
the code. The maintainer approved S4 to S8 with the design. The decisions are numbered S1 to S10 so
that they are not taken for D1 to D13 of this ADR.

| # | Question | Decision |
|---|----------|----------|
| S1 | Approach | Option 3 of the issue together with option 1: `EnsureAssignable<T>()` reports the case, and the documentation gives the full list. Option 2 is rejected for this change (see below). |
| S2 | Which types | Only a state-holding shared type is reported. For a fixed shared type the call behaves as on `71b2e4e`. |
| S3 | Exception | `InvalidOperationException`, as for the other "cannot compare" results. Nothing was compared, so it is not a violation. |
| S4 | Which type decides | The runtime type of the value decides whether rule S applies. The declared `T` still decides how two values are compared. |
| S5 | Where | At the top level only, in a call that compares, after the `null` rule and before any member of `T` is read. |
| S6 | Patterns | The assignable patterns do not excuse it, and their elements are not examined. No member was compared, so no pattern can apply. |
| S7 | `Old<T>()` | Unchanged. It shares the same types as before and throws nothing of its own while copying. |
| S8 | `SharedTypes.IsShared` | Unchanged. B3 and R5 stand. |
| S9 | A member of a state-holding shared type passed at the top level | Accepted and documented: `EnsureAssignable(_cts, Contract.Old(() => _cts))` throws when the member was not replaced. See "The member idiom" below. |
| S10 | Message | It names the matched entry, so that the author of `class OrderForm : Form` reads which base class causes the result. |

"Option 1", "option 2" and "option 3" are the three options that issue #54 lists. Option 2 and the
other rejected alternatives are under "Rejected alternatives" below.

**The state-holding entries.** In this order:

1. the listed types `Stream`, `WaitHandle`, `CancellationTokenSource`, `Thread`, `Timer`
   (`System.Threading`), `SynchronizationContext`, `SemaphoreSlim`, `ManualResetEventSlim`,
   `CountdownEvent`, `ReaderWriterLockSlim`, `Barrier`, `Task`, `WeakReference`, `Component`,
   `HttpMessageHandler`, `HttpClient`, `Socket`;
2. the listed open generic types `ThreadLocal<>`, `Lazy<>`, `WeakReference<>`,
   `ConditionalWeakTable<,>`, `AsyncLocal<>`;
3. `System.Threading.Lock`, matched by full name;
4. `CriticalFinalizerObject`;
5. `ComObject`;
6. a COM object (`Type.IsCOMObject`).

A type matches an entry as B3 matches it. Entries 1, 4 and 5: the type is the entry or derives from
it. Entry 2: the type is a construction of the definition, or derives from one, along `BaseType`.
Entry 3: the full name, along `BaseType`. Entry 6: `Type.IsCOMObject` is true. The matched entry is
the first one that the type matches in this order. For entries 1 and 2, the matched entry is the
listed type or the definition that matched, the first in the order of the list.

**The fixed shared types.** With B3 as it is on `71b2e4e`, these are: `string`; `Delegate`, `Type`,
`MemberInfo`, `Assembly`, `Module`, `Pointer`, `Regex` and the types derived from them; the types in
the namespace `System.Collections.Frozen`; and the CoreLib comparers. A type that matches a
state-holding entry is state-holding, also when it belongs to one of these categories. "Fixed" means
that the instance holds no state that a caller sets: which elements a frozen collection holds, which
methods a delegate calls. A `Regex` caches a runner when it is used (B3). A caller does not set or
read that.

An entry added to B3 later is a fixed shared type until it is added to the state-holding entries. A
unit test fails when a listed type, a listed open generic type or the type matched by full name is
in neither list, so the choice is made when such an entry is added. The category tests of B3 are
code, not data. A category added later is classified by review, and a comment at the method that
holds the category tests says so.

**Message D.** For every matched entry except "a COM object":

```text
EnsureAssignable cannot compare {runtime type}: actual and expected are the same instance. Contract.Old shares an instance of {matched entry} or of a type derived from it with the original instead of copying it, so there is no earlier state to compare with. Assignable patterns do not apply: no member was compared. Ways out: check each state member with Contract.Ensure and a value taken with Contract.Old; to check only that a reference was not replaced, use Contract.Ensure with ReferenceEquals; or set DBC_POST=off (disables all postcondition checks).
```

For the entry "a COM object", the second sentence begins "Contract.Old shares a COM object with the
original instead of copying it, so". The rest is the same.

- `{runtime type}` and `{matched entry}` are formatted with `Type.ToString()`, as the existing
  "cannot compare" messages format their type. For example
  `uContract.Tests.EnsureAssignableSameSharedInstanceTests+Worker` and
  `System.ComponentModel.Component`. An open generic entry reads ``System.Lazy`1[T]``.
  `System.Threading.Lock` is written as that name.
- The message begins "EnsureAssignable cannot compare", as the three existing messages of this kind
  do.
- The message makes no statement about what the caller did before the call. Every sentence holds
  when the caller never used `Old`.
- The first way out names `Contract.Ensure`, not `EnsureAssignable`. For a member typed as `object`,
  as an interface or as a base class, `EnsureAssignable` on the member compares little or nothing.

**The member idiom.** The #52 amendments of ADR-0012 and of this ADR document the pair on a member
that can be `null`: `EnsureAssignable(_address, Contract.Old(() => _address))` in ADR-0012, and the
same with `_name` above. For a member whose value is of a state-holding shared type, for example
`CancellationTokenSource? _cts`, that call now gives:

| `_cts` before | `_cts` after | On `71b2e4e` | After this change |
|---|---|---|---|
| `null` | `null` | passes | passes |
| an instance | the same instance | passes; no state was compared | **message D** |
| an instance | another instance | members walked (#40) | members walked (#40) |
| one of them `null` | | violation | violation |

When the member is declared as `object` or as an interface without properties, the third row is "no
exception" before and after: such a `T` has no members to walk. So for such a member the call throws
when the reference was kept and passes when it was replaced. The call never checked the state of the
instance. After this change it says so in the one case where it can tell.

The top level and a member now differ, and that is decided (S9). Inside a holder, the same shared
instance is "equal" (R5), because the holder has other members that are compared. At the top level
the shared instance is the whole comparison.

Rule S also applies to an instance that cannot change any more, such as `Task.CompletedTask`.
"Cannot compare" is still true for it: nothing was compared.

The documentation tells the user to write `Contract.Ensure(() => ReferenceEquals(_cts, old))` for
"this method does not replace `_cts`".

**Measured on `71b2e4e`.** These were measured on 2026-10-10, while the change was designed, with
throwaway tests in a separate checkout. There were two runs, of 25 and of 14 test methods. None
timed out. "The call" is `EnsureAssignable(x, Contract.Old(() => x))` unless a line says otherwise:

| Case | Result |
|---|---|
| `Worker : Component`, field changed or not | `Old` returns the same instance; no exception |
| a class derived from `Task`, `DelegatingHandler`, `CancellationTokenSource`, `SynchronizationContext`, `WeakReference`, `Lazy<int>` | the same instance; no exception |
| a class derived from `MemoryStream`; `new MemoryStream()` | `TargetInvocationException`, inner `InvalidOperationException` "Timeouts are not supported on this stream." |
| `EnsureAssignable<object>(worker, worker)`, `<IDisposable>`, and with the pattern `".*"` | no exception |
| `new CancellationTokenSource()`, `Task.CompletedTask`, `new Lazy<int>(…)` on both sides | no exception; the `Lazy<int>` factory ran |
| `FrozenSet<int>`, `Regex`, `StringComparer.Ordinal`, a `string` as `object`, on both sides | no exception |
| `typeof(string)` on both sides, `T` = `Type` | `TargetInvocationException`, inner `InvalidOperationException` "Method may only be called on a Type for which Type.IsGenericParameter is true." |
| an ordinary class, the same instance | no exception |
| `Holder { Worker W }`, `W.State` changed | the holder is copied, `W` is shared; no exception |
| two different `Worker`s, `State` differs | violation: "Fields were modified that are not marked as assignable:\n  - State" |
| `Worker` pair under the simulated Native AOT runtime | no exception |
| a class with no members, the same instance, simulated Native AOT | "EnsureAssignable cannot compare …: no properties or fields are visible to reflection under Native AOT. …" |
| `Worker` pair in a process started with `DBC_POST=off` | `Old` returns `null`; no exception |
| `EnsureAssignable(worker, worker)` inside the condition of `Contract.Ensure` | no exception |
| `EnsureAssignable<IDisposable>(worker, worker)` under the simulated Native AOT runtime | `InvalidOperationException` "EnsureAssignable cannot compare System.IDisposable: no properties or fields are visible to reflection under Native AOT. …" |
| `EnsureAssignable<object>(x, x)` for an instance of each state-holding listed and open generic type, a `SafeFileHandle`, the `Lock` stand-in (27 instances) | `IsShared` is true; no exception |
| the same for a `FrozenSet<int>`, a `Regex`, a `Type`, a `MethodInfo`, `StringComparer.Ordinal`, an `Action`, a `string` | no exception |
| `EnsureAssignable(worker, worker, "[")` | `RegexParseException` "Invalid pattern '[' at offset 1. Unterminated [] set." |
| `EnsureAssignable(worker, worker, new string[] { null! })` | `ArgumentNullException` for `pattern` |
| `EnsureAssignable(worker, worker, null!)` | `ArgumentNullException` for `assignableFieldPatterns` |
| a class in the namespace `System.Collections.Frozen` that derives from `Component` | it compiles (one suppression, as `LockStandIn.cs`); `IsShared` is true; no exception |
| a `CancellationTokenSource?` member, the pair: both `null`; the same instance; two instances | no exception; no exception; a violation that names `Token` |
| a class derived from `Regex`, a field changed, the pair | the same instance; no exception |
| two different `Worker`s as `<object>` and as `<IDisposable>` | no exception |
| `object box = 5; EnsureAssignable(box, box)` | no exception |
| the value factory of a class derived from `Lazy<int>`, the pair | the factory ran |

Also measured on `71b2e4e`:

- `Thread` derives from `CriticalFinalizerObject`, and it is the only state-holding listed or open
  generic type that does.
- `Dispose()` on a `Task` that was not started throws `InvalidOperationException`.
- A `[ComImport]` class that is not registered cannot be instantiated (`COMException` 0x80040154).
- `RuntimeHelpers.GetUninitializedObject(typeof(ComObject))` gives an instance of `ComObject`. This
  was measured on Windows only. CI runs the unit-test row that uses it on Linux.
- No unit test and no smoke check passed the same instance of a state-holding shared type as both
  top-level values. Two calls passed one variable twice, neither with a shared type.

**Measured results, unit tests.** The suite had 629 tests on `71b2e4e` and has 696 after the change:
67 were added. With the library changed, 0 of the 696 tests fail, in the Debug and in the Release
configuration.

The new tests were run before the library changed. 47 of them failed. 45 failed for the result that
the table above lists for their case, so those results were observed again. The other two are the
two examples that are not in the table, and they failed as predicted. Both gave no exception before
the change: the pair in a class two levels below `Component`, and a call that forwards its own
generic parameter.

**Measured results, smoke program.** The program has 74 checks in the default build and 73 in the
variant with the hidden dictionary entry, which has no check 35. Two checks are new:

| Check | Call | With postconditions on | With `DBC_POST=off` |
|---|---|---|---|
| 73 | the pair on `this` in a class derived from `CancellationTokenSource` | `InvalidOperationException` | no exception |
| 74 | `EnsureAssignable<IDisposable>` with one such instance on both sides | `InvalidOperationException` | no exception |

Native AOT. Environment: `win-x64`, 2026-10-10. Both publishes ran with `-warnaserror` and gave no
diagnostic:

| Publish | With postconditions on | With `DBC_POST=off` |
|---|---|---|
| Default | 74 `PASS`, no `FAIL` | 74 `PASS`, no `FAIL` |
| Variant with the hidden dictionary entry | 73 `PASS`, no `FAIL` | 73 `PASS`, no `FAIL` |

Checks 73 and 74 gave the results of the table above in both publishes. Under Native AOT the message
begins "EnsureAssignable cannot compare uContract.AotSmoke.CountingTokenSource: actual and expected
are the same instance. Contract.Old shares an instance of System.Threading.CancellationTokenSource".
So `Type.ToString()` gives full names there for these two types.

Under the JIT, the smoke program was run in its `--report` mode. Checks 73 and 74 showed the same
results there.

CI publishes the default build for `linux-x64` and runs the smoke program there. It had not run for
this change when this amendment was written.

**Qualified in the accepted text.** Each statement is quoted by its opening words:

- Known Limitations: "**Shared types** (B3, D7). State inside them is not snapshotted and compares
  by reference." The entry does not name the case of a derived class. B3 matches a listed type and
  every type derived from it, so a user class that derives from a shared type is shared as a whole,
  with the fields that the user class adds. `Old(() => this)` in such a class returns `this`. At the
  top level, the same instance of a state-holding shared type on both sides now throws message D.
  Below the top level, and for a fixed shared type, the limitation stands: see "Limits" below.
- "Unchanged: which members are compared (D9), the top level comparing the members of `typeof(T)`,
  the assignable patterns, and the violation message format." When rule S applies, the top level
  compares no member of `typeof(T)`, and the patterns do not apply. Message D is not a violation
  message: the exception is an `InvalidOperationException`.
- "R8 applies to the nested walk only. When `T` itself has no visible members, the top-level message
  of ADR-0021 is unchanged." The message is unchanged. It is not reached when rule S applies: such a
  call gets message D.

**Qualified in the #52 amendment.** Each statement is quoted by its opening words:

- "At the top level a test on the declared `T` comes first." The #52 amendment says it of the
  Decision and of R0. For two values that are not `null`, a test on the runtime type now comes
  before the test on `T`: rule S.
- "The top-level message of ADR-0021 is not reached when `actual` or `expected` is `null`, or for
  the three kinds of `T`." It is not reached either when rule S applies.
- The steps of a call, at "4. The recursion guard is set. Then:". Rule S is a new step after the
  `null` rule and before that step. "The rule" above gives the steps as they are now.
- "It does not apply "same reference": two references to one object are still walked member by
  member." That holds for a fixed shared type and for a type that is not shared. For the same
  instance of a state-holding shared type, no member is walked: rule S throws. Rule S does not apply
  "same reference" either: it does not report the two values as equal.
- "Nothing changes when `T` is any other type and both values are not `null`:" and the fourth entry
  of its list, "A shared type (B3) other than `string` or a delegate as `T`, such as a `Stream`, a
  `Task` or a frozen collection. Its members are walked." The entries of that list hold for two
  different instances, and for the same instance of a fixed shared type or of a type that is not
  shared. For the same instance of a state-holding shared type the call throws message D, whether
  `T` is `object`, a base class, an interface or a shared type.
- "Other shared types are not compared as a whole." and, in the same entry, "keep the member walk at
  the top level". They are still not compared as a whole. They keep the member walk for two
  different instances, and a fixed shared type keeps it for the same instance too. The same instance
  of a state-holding shared type is not walked.
- "No shortcut for the same reference is added for other types." with "When `T` is not one of the
  three kinds and both values are not `null`, nothing changes." and "A shortcut would stop a call
  with one instance on both sides from reaching the rule "no members visible"". Rule S is not a
  shortcut to "equal", and it does not hide #54: it reports the case. But for the same instance of a
  state-holding shared type, the rule "no members visible" is no longer reached: message D replaces
  it. The unit test that the entry names still passes, because its type is not shared.
- The rejected alternative "Decide on the runtime type at the top level." Rule S decides on the
  runtime type for one question only: whether the call can compare anything. It does not select how
  two values are compared. `T` still selects that, and `EnsureAssignable<object>("a", "b")` does not
  change.
- Relation to #40 and #54, the entry "Left in #40": "the rest of item 2." and, in the same entry, "A
  shared type other than `string` or a delegate as `T` walks its members." It does not walk them for
  the same instance of a state-holding shared type. "What stays open" below gives what is left in
  #40 now.
- The same part, on issue #54: "A class derived from a shared type (`Component`, `Stream`, `Task`)
  is not copied by `Old`, so the pair never fails in it." Such a class is still not copied. The pair
  now throws message D in it.

**Superseded in the #52 amendment.** The statement is quoted by its opening words:

- "The program has 72 checks in the default build and 71 in the" variant. It has 74 and 73. The
  Status line of that amendment ("the smoke program has 72 checks") and its Revision History row
  ("72 smoke checks") state the same count and are superseded with it.

Its result tables, its unit-test figures and its other statements are the dated record of #52 and
stay.

**Rejected alternatives.**

- **Copy the user-derived class (option 2 of the issue).** A class that derives from a listed type
  would be copied field by field, and only the fields that the framework base declares would be
  shared. Two reasons are against it:
  - The base class chain does not tell a user class from a framework class. `Task<T>` derives from
    `Task`, `MemoryStream` from `Stream`, `BackgroundWorker` from `Component`; all of them must stay
    shared. To tell them apart needs a test on the assembly or the namespace of the user's type. D3
    rejected namespace heuristics for third-party types as fragile. B3 uses such tests only for two
    categories of the base class library (frozen collections, CoreLib comparers).
  - The comparison would then read the public properties of the framework base on the copy and on
    the original. `Control.Handle` creates a window handle, and a `Stream` getter throws. To avoid
    that needs fields-only comparison ([#46](https://github.com/cwouyang/uContract.NET/issues/46)),
    and a rule that leaves out the fields of the framework base.

  It is deferred to #46, not closed.
- **Documentation only (option 1 alone).** The pair would still pass in silence. A postcondition
  that cannot fail is the defect.
- **Report every shared type other than `string` and delegates.** The rule would be shorter. But
  `EnsureAssignable(_lookup, Contract.Old(() => _lookup))` for a frozen collection passes today, and
  the collection cannot be given other elements. The call would start to throw.
- **`PostconditionViolationException`.** No change was detected. A caller that catches violations
  would take "cannot compare" for "the state changed".
- **Report it in `Old<T>()`.** `Old(() => _stream)` is a valid way to keep a reference for
  `Contract.Ensure(() => ReferenceEquals(_stream, old))`. B7 says that `Old` throws nothing of its
  own while copying.
- **Tell a user class derived from a fixed shared type from a framework class.** A user class
  derived from `Regex`, `Type` or `MemberInfo` that adds mutable fields still passes in silence. A
  class that `[GeneratedRegex]` emits also derives from `Regex` and also lives in the user assembly,
  so no test on the base class chain or on the assembly separates the two. With `Type` or
  `MemberInfo` among the state-holding entries, a pair of `typeof(X)` would throw. This stays a
  documented limit.
- **Report only when the declared `T` is a state-holding shared type.**
  `EnsureAssignable<object>(worker, worker)` and a pair taken through an interface would keep
  passing in silence. That repairs half of the defect.
- **Also compare two different instances of a state-holding shared type by reference.** The top
  level would then agree with R5 for a member: a replaced reference is a violation. It takes more of
  item 2 of #40 than this issue needs, and with it the call
  `EnsureAssignable(_cts, Contract.Old(() => _cts))` could never pass. It stays with #40.

**Limits.** Each of these passes in silence before and after this change:

- A shared instance below the top level. A holder whose member is a `Worker` passes when the
  `Worker` changed inside (measured, see the table above). This is the Known Limitation "**Shared
  types** (B3, D7)."
- State that a fixed shared instance refers to: a mutable element or value of a frozen collection,
  the target of a delegate, an object that a comparer wraps.
- A user class derived from a fixed shared type, and the framework's own mutable reflection types
  (`System.Reflection.Emit` builders, `DynamicMethod`, `TypeDelegator`), which derive from `Type` or
  `MemberInfo`. A user type declared in the namespace `System.Collections.Frozen` is fixed too.
- The same instance of a type that is not shared: `var old = this;` without `Old`. `Old` never
  returns the same instance for such a type, so this comes from the caller. A held reference to an
  immutable object is a valid use, so it cannot be reported.
- Two different instances at the top level when `T` is `object` or an interface without properties
  (#40).

Also not done by this change: it does not check the state of a class derived from a shared type. The
pair now fails with message D there, and it still cannot compare. And it does not change which types
are shared.

Not tested:

- The throw and the message for a real COM object.
- `System.Threading.Lock` itself. The measurements above used a stand-in.
- The names of other types under Native AOT. Only the two types of checks 73 and 74 were read there.
- That rule S runs before the recursion guard is set. It runs no user code, so the position cannot
  be observed. It was read from the code.

**Why an amendment and not a new ADR.** B3 and R0–R8 stand. Rule S adds one case in which a call
reports that it cannot compare. It does not change how any two values are compared. It takes one
part of the rest of item 2 of #40. It does not do what the #52 amendment reserved for a new ADR,
which is to change what every top-level comparison does. `docs/adr/README.md` lists breaking changes
under "Requires ADR". The amended ADRs (ADR-0022, ADR-0012, ADR-0021 and ADR-0007) are that record,
as for #52.

**What stays open.**

- **#54** is closed by this change, with the limits above.
- **[#40](https://github.com/cwouyang/uContract.NET/issues/40)**: this change takes one part of the
  rest of item 2. Left in #40 of item 2: two different instances of a shared type as `T`; the same
  instance of a fixed shared type; and `T` = `object`, a base class or an interface for two
  different instances. The change also touches item 6 at the top level: for the same instance of a
  state-holding shared type, the rule "no members visible" is not reached. The rest of item 6 stays
  in #40.
- **[#46](https://github.com/cwouyang/uContract.NET/issues/46)** (fields-only comparison) receives
  option 2 of #54 as a deferred alternative.
- **[#49](https://github.com/cwouyang/uContract.NET/issues/49)** (frozen collections by elements)
  proposes to compare them by elements "while still sharing them in the copy". So a frozen
  collection stays a fixed shared type, and the limit above about its elements stays until #49 or a
  later change removes it.

**Unchanged.** Every other decision of this ADR.

### Amendment (2026-10-10): FrozenSet and FrozenDictionary values are compared by element (#49)

This amendment follows the #54 amendment above and carries the same date. It records how
`EnsureAssignable<T>()` compares a `FrozenSet<T>` and a `FrozenDictionary<TKey, TValue>` after
[issue #49](https://github.com/cwouyang/uContract.NET/issues/49). Since #45, `Old<T>()` shares these
collections with the original (B3), and R5 compared two of them by reference when their runtime
types were equal. So a method that replaced a member with a collection of equal content violated its
postcondition (measured: row 25 below). Two of them that a member or element holds are now compared
element by element, in enumeration order (R7). When the declared `T` is one of the two types,
`actual` and `expected` are compared as a whole. `Old<T>()`, B3 and `SharedTypes.IsShared` do not
change. The amendments of 2026-10-10 for #49 of [ADR-0012](0012-dotnet-improvements-over-java.md),
[ADR-0021](0021-postconditions-under-trimming-and-aot.md) and
[ADR-0007](0007-reflection-field-comparison.md) point here.

**Terms.** This amendment uses these terms:

- **The two types**: `FrozenSet<T>` and `FrozenDictionary<TKey, TValue>`. A value is of the two
  types when its runtime type is a construction of one of them or derives from one. An instance is
  always of an internal class of the base class library that derives from one of them
  (`Int32FrozenSet`, `LengthBucketsFrozenSet` and others; "Measured" below lists the classes that
  were seen). Which class the library picks was seen to depend on the element type, on the elements
  and their number, and on the comparer. Code outside the base class library cannot derive from the
  two types. An array of them is not one of them, and neither is any other type declared in the
  namespace `System.Collections.Frozen`. As the declared `T`, "one of the two types" means
  `FrozenSet<…>`, `FrozenDictionary<…, …>` or a class derived from one; a derived class can be `T`
  only through reflection.
- **A pair that a member or element holds**: two values that the comparison meets below `actual` and
  `expected`: the two values of a member of `T` or of a field, or two elements, keys or values of a
  collection. Their runtime types decide what is done (R0), not the declared type of the member.
- **By reference**: equal only when both sides are the same instance.
- **By element**: R7. Two `IDictionary` values are compared entry by entry, in enumeration order,
  key with key and value with value. Any other two `IEnumerable` values are compared element by
  element, in enumeration order. Each key, value and element is compared by R0–R8. A different
  number of entries or elements is a difference.
- **Compared as a whole**: `actual` and `expected` are compared as one pair, as the #52 amendment
  above defines it for three kinds of `T`. No member of `T` is compared, and the assignable patterns
  are not examined.
- **Message C**: the violation message of the #52 amendment for a `T` that is compared as a whole.
- **`6b24bee`**: the commit of `master` on which the design was measured, before the library
  changed. "Before" in this amendment is the result there, under the JIT, on .NET 8.0.16. "2.0.0" is
  the result with the 2.0.0 package.

**What changed.** Two rules change, and both are about the two types only:

1. **A pair that a member or element holds.** Two values of the two types are compared by element
   (R7), whether their runtime types are equal or not. On `6b24bee`, R5 compared two of them by
   reference when their runtime types were equal, and R6 sent two of different runtime types to R7.
   So the verdict depended on which internal class the library had picked. Measured on `6b24bee`:
   with 30 strings, a set built with `StringComparer.Ordinal` and a set of the same strings built
   with `StringComparer.OrdinalIgnoreCase` are of different classes, and the pair passed; with three
   strings the two are of one class (`LengthBucketsFrozenSet`), and the pair was a violation. R5 no
   longer decides a pair of two values of the two types. The same instance on both sides is equal,
   as for every pair, and is not enumerated.
2. **One of the two types as `T`.** `actual` and `expected` are compared as a whole: as one pair, by
   the rule of item 1. The assignable patterns do not apply, and their elements are not examined. A
   difference is a `PostconditionViolationException` with message C. No message is added or changed.
   On `6b24bee` the members of the declared type were walked. These are members of the base class
   library: a violation named `Items`, `Count`, `Keys` or `Values`, and the pattern `"Items"` hid a
   change of the elements (measured: rows 31 to 37 below).

Not changed:

- `Old<T>()`: every result. It still shares a value of the two types, as it shares every type
  declared in the namespace `System.Collections.Frozen`. B3 and `SharedTypes.IsShared` are as they
  were.
- Rule S of the #54 amendment: which types it reports, and message D. A value of the two types is a
  fixed shared type, so rule S does not apply to it.
- Every pair in which no side is of the two types.
- A pair with a value of the two types on one side only (row 16 below). The runtime types differ,
  and R6 decides, as before. The same holds for a `FrozenSet` and a `FrozenDictionary`, and for two
  values of the two types with different element types.
- Any other type that B3 shares because of its namespace: see F5 below.
- A call with postconditions off, or made while another contract check is running. It returns
  without comparing.

**Why the two types and not the namespace.** B3 shares every type whose namespace is
`System.Collections.Frozen`. That rule also matches types that are not immutable collections.
Measured on `6b24bee`: `typeof(FrozenSet<int>[]).Namespace` is `System.Collections.Frozen`, a class
of the caller can be declared in that namespace, and `Old<T>()` shares a `FrozenSet<int>[]` and an
instance of such a class. To compare such an object by element would run the caller's own
enumerator, and the object can be a resource: a class declared there can also derive from
`Component`. R5 compares a shared instance by reference so that no getter or factory of the instance
runs. The two types cannot be derived from outside the base class library, so the enumerator of a
value of them is code of that library. That `Old<T>()` shares an array of the two types is a defect
of its own. It is filed as [#57](https://github.com/cwouyang/uContract.NET/issues/57) and is not
changed here.

**Decisions and reasons.** The maintainer decided F1 to F4 and F9 on 2026-10-10, before the design
was written. F5 to F8 and F10 follow from them, come from the review of the design, or keep the
behaviour of `6b24bee`. The maintainer approved them with the design. The decisions are numbered F1
to F10 so that they are not taken for D1 to D13 of this ADR or for S1 to S10 of the #54 amendment.

| # | Question | Decision |
|---|----------|----------|
| F1 | `Old<T>()` | Unchanged. It still shares a value of the two types. B3 and `SharedTypes.IsShared` do not change. |
| F2 | A pair of two values of the two types that a member or element holds | Compared by element, whether the runtime types of the two values are equal or not. |
| F3 | Order and comparer | The order is the enumeration order. The comparer of the collection is not compared and not used. Keys, values and elements are compared by R0–R8, as for every other collection. |
| F4 | One of the two types as the declared `T` | `actual` and `expected` are compared as a whole, by F2. |
| F5 | Every other type that B3 shares because of its namespace | Unchanged: a class of the caller declared in `System.Collections.Frozen` (also one that implements `IEnumerable`), a value type declared there, and an array whose element type is declared there. Two different instances that a member or element holds are compared by reference. |
| F6 | One instance on both sides | Equal, and not enumerated. This is the rule for every pair. A value of the two types stays a fixed shared type in the sense of the #54 amendment: `EnsureAssignable<T>()` does not throw message D for it. |
| F7 | `T` is `object` or an interface, and the two values are of the two types | Unchanged: the members visible through `T` are compared (#40). |
| F8 | The message for F4 | Message C. No message is added or changed. |
| F9 | CHANGELOG | The entries of #45 and #52 are rewritten to state the final behaviour. No entry is added for the state between #45 and this change, which was never released. |
| F10 | Record | An amendment of ADR-0022, ADR-0012, ADR-0021 and ADR-0007, not a new ADR. See "Why an amendment and not a new ADR" below. |

For F5, "by reference" is R5, which applies to two instances of one runtime type; two sequences of
different runtime types are compared by R7, as before.

The reasons:

- **F1.** To copy a value of the two types would mean to copy internal structures of the base class
  library, and it would reopen B3. See the first rejected alternative.
- **F2 and F3.** This is what R7 does for every other collection. `HashSet<T>` and `Dictionary<,>`
  are compared in enumeration order (measured on `6b24bee`: two `HashSet<string>` filled in reverse
  order are a violation). It is also what 2.0.0 did for two members of the two types in objects
  passed to it directly (measured: the column "2.0.0" of rows 1 to 8 below). One rule then holds for
  every set and every dictionary.
- **F4.** With the member walk, the verdict for `T` depended on members of the base class library,
  and a pattern could name them. The rule for a member and the rule for `T` would also disagree. The
  #52 amendment gives this reason for letting the declared `T` decide: for its three kinds, the
  declared type and the runtime type cannot disagree about the kind. That holds here too: a value
  whose declared type is one of the two types has a runtime type that is that type or derives from
  it.
- **F5.** See "Why the two types and not the namespace" above.
- **F7.** To decide on the runtime type at the top level is the rest of item 2 of #40. The #52
  amendment rejected it for that change, and it stays with #40.
- **F8.** Message C is true of the case as it stands. See the last rejected alternative.

**The rule as it is now.** The steps of a call are those of the #54 amendment. Step 5 gains the two
types:

1. The pattern array is checked for `null`.
2. A call that does not compare returns.
3. The `null` rule.
4. Rule S.
5. When `T` is a `string`, a delegate type, a nullable value type or one of the two types, the two
   values are compared as a whole.
6. For every other `T`, the members of `typeof(T)` are walked.

The test of the #52 amendment on the declared `T` gains a fourth case: `typeof(T)`, or a class that
it derives from, is a construction of `FrozenSet<>` or of `FrozenDictionary<,>`. The table of that
amendment ("What R0–R8 give for two values that are not `null`") gains this row:

| `T` | Compared by | Rule |
|---|---|---|
| one of the two types | The same instance is equal. Otherwise two `FrozenDictionary` values entry by entry and two `FrozenSet` values element by element, in enumeration order; each key, value and element by R0–R8 | R7; R6 leads to it when the runtime types of the two values differ |

What holds for the comparison of one of the two types as a whole:

- It runs with the recursion guard set. It enumerates two different collections, and it can run the
  property getters, enumerators and `Equals` of their elements, keys and values and of what those
  refer to, as the member walk can (read from the code).
- The pair of the two collections is tracked by R1 while it is compared. An element that leads back
  to its collection counts as equal (measured: rows 46 and 47 below).
- The patterns are not used, and the elements of the pattern array are not examined. A `null`
  element or an invalid regular expression in it is not reported (measured: rows 39 and 40 below).
  The array itself is still checked for `null` first.
- No member metadata is read, neither of `T` nor of the runtime types of the two collections: a
  collection is enumerated, not walked member by member. So the rule "no members visible" of
  ADR-0021 is not reached for `T` (read from the code).

**In the code.** `SharedTypes.IsFrozenSetOrDictionary(Type)` is the test for the two types. The
comparison of a member or element and the test on `T` both use it. The helper that builds the
"cannot compare" exception below a `T` that is compared as a whole was renamed from
`CannotCompareBelowNullable` to `CannotCompareBelowWhole`, because it now also serves the two types.

**Behaviour.** "Before" was measured on `6b24bee` and "2.0.0" with the 2.0.0 package, with tests and
a program that are not in the repository. Every cell of those two columns is measured unless it says
otherwise; where the measured input differs from the row, the cell or a note says so. "After" is
what the design stated before the library changed. The unit tests of this change show it for every
row except rows 15, 16, 30, 42, 43 and 45; tests that existed before this change show rows 16 and
30, and rows 15, 42, 43 and 45 have no test. For those four, the two tests that the change added
(`IsFrozenSetOrDictionary` on `T` and on the runtime type of a pair) are false or not reached, so
the path is that of `6b24bee` (read from the code). In the tables, "build order" is the order of the
sequence from which the collection was built. Sets and dictionaries of strings hold the three
strings `"a"`, `"b"`, `"c"` unless the row says otherwise. `H` is a class with one property `Items`
of the type that the row names. `Box` is a small class with content of its own (in the unit tests:
one property, `Value`). "Violation" for a pair that a member holds means a
`PostconditionViolationException` that names `Items` and its backing field. "F8 text" is message C.
"(F5)" and "(F7)" name the decision that the row shows.

A pair that a member holds:

| # | Pair (the `Items` of two `H`) | 2.0.0 | Before | After |
|---|---|---|---|---|
| 1 | two `FrozenSet<string>`, equal content, built from the same sequence | no exception | violation | no exception |
| 2 | two `FrozenDictionary<string, int>`, equal content, built from the same sequence | no exception | violation | no exception |
| 3 | two `FrozenSet<string>`, one element differs | violation (measured with `{"a"}` and `{"b"}`) | violation | violation |
| 4 | two `FrozenDictionary<string, int>`, one value differs | violation | violation | violation |
| 5 | two `FrozenDictionary<string, int>`, one key differs | violation | violation | violation |
| 6 | two `FrozenSet<string>`, different counts | violation | violation | violation |
| 7 | two `FrozenSet<string>`, equal content, built in reverse order | violation | violation | violation |
| 8 | two `FrozenDictionary<string, int>`, equal content, built in reverse order | violation | violation | violation |
| 9a | `FrozenSet<string>` with `Ordinal` and with `OrdinalIgnoreCase`, the three strings, same order (one runtime type) | no exception | violation | no exception |
| 9b | the same with 30 strings `"key1"`…`"key30"` (two runtime types) | not measured | no exception | no exception |
| 10 | two `OrdinalIgnoreCase` sets, `"a"`, `"b"`, `"c"` and `"A"`, `"B"`, `"C"` (one runtime type; `SetEquals` is true) | violation | violation | violation |
| 11 | two `FrozenSet<Box>`, each with one `Box` of its own, equal content | no exception | violation | no exception |
| 12 | two `FrozenSet<Box>`, each with one `Box` of its own, contents differ | violation | violation | violation |
| 13 | two `FrozenDictionary<string, Box>`, each with `Box` values of its own, equal content | violation | violation | no exception |
| 14 | the same frozen set on both sides | no exception | no exception | no exception |
| 15 | two empty frozen sets (measured: one instance) | no exception | no exception | no exception |
| 16 | `Items` declared `IEnumerable<int>`: a `List<int>` and a `FrozenSet<int>`, equal elements | no exception (measured with `string`) | no exception | no exception |
| 17 | `Items` declared `IReadOnlySet<string>`: two frozen sets, equal content | no exception | violation | no exception |
| 18 | `Items` declared `object`: two frozen sets, equal content | not measured | violation | no exception |
| 19 | `Items` is a `List<FrozenSet<string>>`: one frozen set each, equal content | not measured | violation | no exception |
| 20 | `Items` is a `Dictionary<string, FrozenSet<string>>`: one entry each, equal content | not measured | violation | no exception |
| 21 | two `FrozenSet<int>[]`, different arrays, the same frozen set as element (F5) | not measured | violation | violation |
| 22 | two instances of a class of the caller in the namespace that implements `IEnumerable<int>`, equal elements (F5) | not measured | violation | violation |
| 23 | the same, and the class derives from `Component` (F5) | not measured | violation | violation |
| 24 | two instances of a class of the caller in the namespace, not enumerable, equal properties (F5) | not measured | violation | violation |

Among the rows that were measured with 2.0.0, row 13 is the only one of this table in which 2.0.0
and "After" differ. 2.0.0 reported two dictionaries whose values are different objects of equal
content (item 6 of the Problem Statement). The values of a `FrozenDictionary` now follow R0–R8, as
the values of every other dictionary do since #45.

With `Old<T>()`; `h` is an `H`:

| # | Steps | 2.0.0 | Before | After |
|---|---|---|---|---|
| 25 | `old = Old(() => h)`; replace `h.Items` by a frozen set of equal strings; `EnsureAssignable(h, old)` | `Old` throws | violation | no exception |
| 26 | `old = Old(() => h)`; change a `Box` inside the set; `EnsureAssignable(h, old)` | `Old` throws | no exception | no exception |
| 27 | `old = Old(() => h)`; change a `Box`; replace `h.Items` by a new frozen set of the same `Box` | `Old` throws | violation | no exception |
| 28 | `old = Old(() => h)`; replace `h.Items` by a frozen set of a new `Box` with other content | `Old` throws | violation | violation |

Rows 26 and 27 show the limit "Elements are shared" below: the change is not seen. The 2.0.0 cells
of rows 25 to 28 were measured with a `FrozenSet<string>` and a `FrozenDictionary<string, int>`
member, not with `Box`.

Cycles. A value of the two types that leads back to a pair that is being compared is handled as
every other collection is: the pair counts as equal (R1). `Node` has a value and a property that
refers to the frozen set that holds the node; each side has its own set with one node.

| # | Call | 2.0.0 | Before | After |
|---|---|---|---|---|
| 29a | two `H` with such a set, equal values | not measured | violation | no exception |
| 29b | two `H` with such a set, values differ | not measured | violation | violation |
| 46 | `T` = `FrozenSet<Node>`, equal values | not measured | violation (`Items`) | no exception |
| 47 | `T` = `FrozenSet<Node>`, values differ | not measured | violation (`Items`) | violation, F8 text |

`actual` and `expected` themselves:

| # | Call | 2.0.0 | Before | After |
|---|---|---|---|---|
| 30 | `T` = `FrozenSet<string>`, equal content, same build order | violation (`Items`) | no exception | no exception |
| 31 | the same, reverse build order | violation (`Items`) | violation (`Items`) | violation, F8 text |
| 32 | the same, one element differs, equal counts | violation (`Items`) | violation (`Items`) | violation, F8 text |
| 33 | row 32 with the pattern `"Items"` | no exception | no exception | violation, F8 text |
| 34 | the same, different counts | not measured | violation (`Items`, `Count`) | violation, F8 text |
| 35 | `T` = `FrozenDictionary<string, int>`, equal content, same build order | violation (`Keys`, `Values`) | no exception | no exception |
| 36 | the same, a value differs | violation (`Keys`, `Values`) | violation (`Values`) | violation, F8 text |
| 37 | the same, reverse build order | not measured | violation (`Keys`) | violation, F8 text |
| 38 | `T` = `FrozenSet<string>`, the same instance | no exception | no exception | no exception |
| 39 | `T` = `FrozenSet<string>`, equal content, pattern `"["` | not measured | `RegexParseException` | no exception |
| 40 | `T` = `FrozenSet<string>`, equal content, a `null` pattern | not measured | `ArgumentNullException` | no exception |
| 41a | `T` = `IReadOnlySet<string>`, two frozen sets, an element differs (F7) | no exception | no exception | no exception |
| 41b | the same, the counts differ (F7) | no exception | no exception | no exception |
| 42 | `T` = `IReadOnlyCollection<string>`, two frozen sets, counts differ (F7) | not measured | violation (`Count`) | violation (`Count`) |
| 43 | `T` = `IReadOnlyDictionary<string, int>`, two frozen dictionaries, a value differs (F7) | not measured | violation (`Values`) | violation (`Values`) |
| 44 | `T` = `object`, two frozen sets, an element differs (F7) | no exception | no exception | no exception |
| 45 | `T` = `FrozenSet<int>[]`, two arrays with frozen sets of equal content (F5) | not measured | violation (`SyncRoot`) | violation (`SyncRoot`) |

Message C for row 31:

```text
actual and expected are not equal. System.Collections.Frozen.FrozenSet`1[System.String] is compared as a whole, so assignable patterns do not apply.
```

Notes on the last table:

- Rows 39 and 40. A pattern is examined only when the members of `T` are walked. `T` = `string`
  already behaved so (measured on `6b24bee`: the pattern `"["` gives no exception). The array of
  patterns itself must still not be `null`.
- One side `null`. The `null` rule of the #52 amendment applies before all of this, unchanged.
- Rows 41a, 41b and 44 are results under the JIT. Under Native AOT the code reports a `T` other than
  `object` with no visible members as before ("EnsureAssignable cannot compare …: no properties or
  fields are visible"), and `T` = `object` is exempt in every build. Both were read from the code.
  This change has no Native AOT run of these rows.

Measured after the design was written, with the same `H`:

| Example | Before | After |
|---|---|---|
| two `FrozenSet<string?>`, each built from `"a"` and `null` (one internal class, `SmallFrozenSet`; the same enumeration order) | violation | no exception |
| a `FrozenSet<string?>` built from `"a"` and `null`, and one built from `"a"` and `"b"` (two internal classes, `SmallFrozenSet` and `LengthBucketsFrozenSet`) | violation | violation |
| `old = Old(() => h)`; change the `Box` inside the set; replace `h.Items` by a frozen set of a new `Box` with the changed content; `EnsureAssignable(h, old)` | violation (`Items` and its backing field, by reference; measured on `6b24bee`) | no exception |
| the same steps, but the new `Box` has the content as it was when `Old` ran | not measured on `6b24bee` | violation |

The third and the fourth example are the two results of the limit "Elements are shared" after the
collection is replaced. In the third the change is missed. Its "After" is a unit test; with the rule
of `6b24bee` restored by hand, that test fails (see the mutation check under "Measured"). In the
fourth a violation is reported although the content is as it was when `Old<T>()` ran. Its "After" is
a unit test too, which passed on its first run; no change of the library was made for it.

**Native AOT, the members of an element are not visible.** An element, key or value can be of a
class whose members are not visible to reflection. When that class's `Equals` reports the two values
unequal, the comparison cannot go on, as for every other collection (R8). Which exception the call
throws depends on where the two collections are:

- **A member or element holds them.** The call throws the `InvalidOperationException` of R8. Its
  path leads through the member: `typeof(T).Name`, the member, and `[]` for the element.
- **`T` is one of the two types.** The call throws the `InvalidOperationException` that the #52
  amendment gives for a class below a nullable value type: the text of R8 without the way out "list
  … as assignable", because no member can be listed. No text is added. The path starts with
  `typeof(T).Name`; for a nullable value type it starts with the name of the underlying type. For a
  `FrozenSet<T>` the path is ``FrozenSet`1[]``.

Measured, in two ways. Two unit tests simulate Native AOT with the existing test seam and compare
the whole message, with a class that has no members as the element type: for a member the path is
`OrderWithFrozenLines.Lines[]`, and for `T` it is ``FrozenSet`1[]``. Smoke checks 78 and 79 show the
same two cases in a Native AOT publish, with the paths `FrozenLeavesHolder.Leaves[]` and
``FrozenSet`1[]`` (see "Measured"). In both checks the two elements have equal content. The element
class `HiddenLeaf` has no `Equals` of its own, so two instances are unequal by `Equals`, and the
call throws. Under the JIT the members of `HiddenLeaf` are visible, and both checks give no
exception.

The design had read the path for `T` from the code; the unit test and check 79 measured it. Read
from the code and not measured: on `6b24bee`, under Native AOT, a call with `T` =
`FrozenSet<HiddenLeaf>` threw the exception of R8 with a path through `Items`. Also read from the
code and not measured: the path below a `FrozenDictionary<TKey, TValue>`. The code appends `[]` for
a key and for a value alike.

The #52 amendment has two statements under its "Limits", among what was checked by reading the code
only: "A comparison as a whole that reaches a class with no visible members under real Native AOT."
and, in the same entry, "A class reached through an element of a collection is not tested". Check 79
is such a run, and its class is reached through an element of a collection. It shows the case for a
`FrozenSet<T>` as `T` only. For a nullable value type both statements stand.

**Limits that stay.** Limits 1 to 3 are stated in the user documentation, with the ways out.

1. **Order.** Two values of the two types with equal content can enumerate in different orders, and
   are then different (rows 7, 8, 31 and 37). Measured on .NET 8.0.16, for sets and dictionaries:
   - `string` elements and keys, and reference types whose hash codes collide: the order follows the
     order in which the collection was built.
   - A class without a `GetHashCode` of its own, five or more elements: two sets of equal content
     that were built the same way enumerate differently (measured for sets of 5 and 30 elements and
     for a dictionary with 30 such keys; up to 4 elements a set keeps the order in which it was
     built). The likely cause is that the hash code of such an object does not come from its
     content.
   - A record (hash code from content), 5 and 30 elements: the order does not depend on the order in
     which the collection was built; with 2 and 4 elements it does.
   - `int` (sets of 5, 11 and 200; dictionaries of 5 and 200) and `Guid` (a set of 30): the order
     does not depend on the order in which the collection was built.

   These are observations of one runtime, not guarantees. This is the Known Limitation
   "**Enumeration order.**" of this ADR, which now also holds for two values of the two types of one
   runtime type. `HashSet<T>` and `Dictionary<,>` are compared in enumeration order too, and 2.0.0
   compared the two types so.
2. **Comparer.** The comparer of the collection is not compared and not used. Two sets that their
   own comparer calls equal can be different (row 10), and two collections with different comparers
   can be equal (rows 9a and 9b).
3. **Elements are shared.** `Old<T>()` does not copy a value of the two types. So the elements, keys
   and values of the old value are the original objects, as they are when the comparison runs and
   not as they were when `Old<T>()` ran. Two things follow:
   - A change inside an element is not seen while both sides hold that object at the same place in
     the enumeration order. That is so when the member was not replaced (row 26), and when it was
     replaced by a new collection that holds the same object there (row 27).
   - After the member is replaced by a collection that holds other objects, each of them is compared
     with the original object at the same place in the enumeration order, as that object is now. A
     change can then be missed: the original object was changed, and the new object has the changed
     content (the third example under "Measured after the design was written"). And a violation can
     be reported although the content is as it was when `Old<T>()` ran: the original object was
     changed, and the new object has the earlier content (the fourth example there). A unit test
     shows each of the two cases.

   When the original objects are not changed, a new collection with other content is reported (row
   28). The same content in a `List<T>` or a `Dictionary<,>` is copied by `Old<T>()`, and there a
   change inside an element is seen.
4. **Cost.** Two different sets are each enumerated in full (copied into an array) before any
   element is compared; two dictionaries are enumerated entry by entry up to the first difference. A
   pair that R1 has already settled is not enumerated again. The array copy boxes elements of a
   value type. The keys and values of a dictionary are read as `object` through
   `IDictionaryEnumerator`, so those of a value type are boxed too. This was read from the code and
   was not measured. A member that was not replaced is one instance on both sides and is not
   enumerated.
5. **The value types of the namespace.** A value type declared in `System.Collections.Frozen` is not
   one of the two types. B3 shares it, and R5 compares it by reference. Each side of a member of
   such a type is boxed separately, so the member is never equal. The #52 amendment records this for
   the enumerators ("**A nullable value type whose underlying type is a shared value type**"), and
   it stands. The set of such value types can grow with the runtime: the design names the
   `AlternateLookup` structs of .NET 9 as an example. No run on a later runtime supports that: every
   measurement of this amendment is of .NET 8.0.16. That such a member is never equal was read from
   the code and was not tested.

The way out for limits 1 and 2: list the member as assignable, and state the condition with
`Contract.Ensure` and a value taken with `Contract.Old`, for example `Contract.Ensure("tags
unchanged", () => _tags.SetEquals(oldTags))` with `oldTags = Contract.Old(() => _tags)`. `Old<T>()`
returns the earlier instance itself, which is what the condition needs. When `T` itself is one of
the two types there is no member to list: use `Contract.Ensure` in place of `EnsureAssignable<T>()`.
The way out for limit 3: before the change, take each value that must stay with `Old<T>()`, and
check it afterwards with `Contract.Ensure`. An element taken on its own is copied by `Old<T>()`,
unless its own type is one that `Old<T>()` shares (a nested `FrozenSet<T>` is shared again); compare
it afterwards with `EnsureAssignable<T>()`. Taking a value that holds no reference, such as a
number, or a `string`, always works: `Old<T>()` copies such a value, and a `string` cannot change.

**Measured results, unit tests.** The suite had 696 tests on `6b24bee`, with 0 failed in the Debug
and in the Release configuration. After the change it has 746, with 0 failed in both configurations.
50 were added: 31 for two values of the two types that a member or element holds, 15 for `T` itself,
and 4 after review. The 4 are: an element class with no visible members below a `FrozenSet<T>`, as a
member and as `T`, under the test seam that simulates Native AOT (2 tests); an element that is
changed in place and whose collection is then replaced by one that holds a new object with the new
content (no exception: the change is missed); and the same with a new object that has the content as
it was when `Old<T>()` ran (a violation is reported). The last of these,
`EnsureAssignable_WhenAnElementIsChangedAndItsFrozenSetReplacedByOneWithANewElementAsItWas_ReportsTheMember`,
passed on its first run, and no change of the library was made for it. 48 of the 50 are in the new
class `EnsureAssignableFrozenCollectionTests`, and 2 in `EnsureAssignableWithoutDynamicCodeTests`.

These failing tests were seen before each of the two changes of the library, each for the result
that "Before" gives:

- a member that holds two frozen sets of equal content: a violation that names `Items`;
- a member that holds two frozen dictionaries of equal content: the same;
- `T` = `FrozenSet<string>` with different content and the pattern `"Items"`: no exception was
  thrown.

With `T` = `FrozenSet<Node>` and a cycle through an element (row 46), the result changed with the
first of the two changes of the library, not with the second: the member walk over `T` met the pair
of the two sets while it was in progress. The design had read that from the code; the work showed
it.

**Measured results, mutation check.** At the final state of the library and the tests, with 746
tests, the library was changed by hand in two ways. Neither change was committed.

- Every pair of two different instances of the two types is forced equal, whatever the runtime
  types. 21 tests fail: the 19 that expect a violation for such a pair, and the 2 tests for elements
  with no visible members.
- The rule of `6b24bee` is restored: two instances of one runtime type are unequal. 21 tests fail:
  the 17 that expect no exception for such a pair; the test
  `EnsureAssignable_WhenFrozenSetsHaveEqualContent_DoesNotThrow`, which existed before; the 2 tests
  for elements with no visible members; and the test for an element changed in place whose
  collection is replaced by one that holds a new object with the new content. Two tests whose two
  sets have different runtime types still pass: their pairs gave the same results on `6b24bee`. The
  test for a new object with the content as it was still passes too: its pair is a violation either
  way.

**Measured results, smoke program.** The program had 74 checks in the default build and 73 in the
variant with the hidden dictionary entry (`-p:AotSmokeHiddenDictionaryEntry=true`). It now has 79
and 78. Check 50 is renamed and its expected result changed; checks 75 to 79 are new:

| Check | Call | With postconditions on | With `DBC_POST=off` |
|---|---|---|---|
| 50, now "Frozen set compared by elements" | two holders, frozen sets of equal content | no exception | no exception |
| 75 | two holders, frozen sets of different content | `PostconditionViolationException` | no exception |
| 76 | two holders, `FrozenDictionary<string, int>` of equal content | no exception | no exception |
| 77 | `T` = `FrozenSet<string>`, the content differs | `PostconditionViolationException` whose text contains "is compared as a whole" | no exception |
| 78 | two holders, each with a `FrozenSet<HiddenLeaf>` of one element, equal values | Native AOT: `InvalidOperationException` whose text contains "cannot compare" and `uContract.AotSmoke.HiddenLeaf`. JIT: no exception | no exception |
| 79 | `T` = `FrozenSet<HiddenLeaf>`, the same content | Native AOT: `InvalidOperationException` whose text contains "cannot compare", `uContract.AotSmoke.HiddenLeaf` and ``reached through 'FrozenSet`1[]'``. JIT: no exception | no exception |

Before the change, check 50 was named "Frozen set compared by reference" and expected a violation
with postconditions on. "What held" under "Native AOT: measured" says: "The namespace rule for
frozen collections works (checks 49, 50)." Check 49 still shows that `Old<T>()` shares a frozen set.
Check 50 no longer shows a comparison by reference: it shows the comparison by element.

Native AOT. Environment: `win-x64`. Each run used `--assert`, and no publish gave a diagnostic:

| Publish | With postconditions on | With `DBC_POST=off` |
|---|---|---|
| Default | 79 `PASS`, 0 `FAIL` | 79 `PASS`, 0 `FAIL` |
| Variant with the hidden dictionary entry | 78 `PASS`, 0 `FAIL` | 78 `PASS`, 0 `FAIL` |

The four runs were made at the final state of the library and of the tests. They repeat four earlier
runs of this change, with the same results and the same lines for the six checks. These are the
twelve lines of the six checks in the default publish: six with postconditions on, and six with
`DBC_POST=off`. The prefix `on:` or `off:` names the run and is not output of the program. The
program prints the first line of a message. Every line is given in full, except the lines of checks
78 and 79 with postconditions on: they are given up to the closing parenthesis of "(reached through
…)", and each message continues as every "cannot compare" message does:

```text
on:  PASS 50 Frozen set compared by elements expected=no exception got=no exception
on:  PASS 75 Frozen sets of different content expected=PostconditionViolationException got=PostconditionViolationException Postcondition violated: Fields were modified that are not marked as assignable:
on:  PASS 76 Frozen dictionary compared by entries expected=no exception got=no exception
on:  PASS 77 EnsureAssignable<FrozenSet<string>> sets of different content expected=PostconditionViolationException got=PostconditionViolationException Postcondition violated: actual and expected are not equal. System.Collections.Frozen.FrozenSet`1[System.String] is compared as a whole, so assignable patterns do not apply.
on:  PASS 78 Frozen set of elements with hidden members expected=InvalidOperationException got=InvalidOperationException EnsureAssignable cannot compare uContract.AotSmoke.HiddenLeaf (reached through 'FrozenLeavesHolder.Leaves[]')…
on:  PASS 79 EnsureAssignable<FrozenSet<HiddenLeaf>> elements with hidden members expected=InvalidOperationException got=InvalidOperationException EnsureAssignable cannot compare uContract.AotSmoke.HiddenLeaf (reached through 'FrozenSet`1[]')…
off: PASS 50 Frozen set compared by elements expected=no exception got=no exception
off: PASS 75 Frozen sets of different content expected=no exception got=no exception
off: PASS 76 Frozen dictionary compared by entries expected=no exception got=no exception
off: PASS 77 EnsureAssignable<FrozenSet<string>> sets of different content expected=no exception got=no exception
off: PASS 78 Frozen set of elements with hidden members expected=no exception got=no exception
off: PASS 79 EnsureAssignable<FrozenSet<HiddenLeaf>> elements with hidden members expected=no exception got=no exception
```

Under the JIT the program is run in its `--report` mode, where it prints 79 and 78 lines. There
checks 78 and 79 report no exception, because the members of `HiddenLeaf` are visible.

Also measured on `6b24bee` in a Native AOT publish for `win-x64`, with a temporary probe in the
smoke program: a `FrozenSet<string>` and a `FrozenSet<int>` enumerate through the non-generic
`IEnumerable`, and a `FrozenDictionary<string, int>` through `IDictionaryEnumerator`; a `List<int>`
and a `FrozenSet<int>` in a member declared `IEnumerable<int>` are compared by element; and the 74
checks of that commit pass.

CI publishes the default build for `linux-x64` and runs the smoke program there. It had not run for
this change when this amendment was written.

**Measured, other results.**

- The internal classes that were seen for sets: `SmallValueTypeComparableFrozenSet`,
  `Int32FrozenSet`, `LengthBucketsFrozenSet`, `OrdinalStringFrozenSet_RightJustifiedSubstring`,
  `OrdinalStringFrozenSet_RightJustifiedCaseInsensitiveAsciiSubstring`,
  `ValueTypeDefaultComparerFrozenSet`, `SmallFrozenSet`, `DefaultFrozenSet`, `EmptyFrozenSet`. For
  dictionaries: `SmallValueTypeComparableFrozenDictionary`, `Int32FrozenDictionary`,
  `LengthBucketsFrozenDictionary`, `OrdinalStringFrozenDictionary_RightJustifiedSubstring`,
  `DefaultFrozenDictionary`.
- `FrozenDictionary` implements the non-generic `IDictionary`. Its non-generic enumerator is
  `System.Collections.Immutable.DictionaryEnumerator`.
- `Old<T>()` shares an array of the two types because `Type.Namespace` of an array type is the
  namespace of its element type (#57).

**Measured with the 2.0.0 package.** These results come from a program with the 2.0.0 package that
is not in the repository:

- `Old<T>()` threw `InvalidOperationException` for a value of the two types and for a class with a
  member of one of them. This was measured with a `FrozenSet<string>`, a `FrozenDictionary<string,
  int>`, and a class with a member of each. So the pair `EnsureAssignable(this, Contract.Old(() =>
  this))` did not work in such a class. For example, these are three lines of the record: for a
  class `H` with a frozen set member, for a frozen set itself, and for a frozen dictionary itself.

  ```text
  old set member: InvalidOperationException: Type H cannot be serialized for Old<T>(). Ensure the type is JSON-serializable.
  old top set: InvalidOperationException: Type FrozenSet`1 cannot be serialized for Old<T>(). Ensure the type is JSON-serializable.
  old top dict: InvalidOperationException: Type FrozenDictionary`2 cannot be serialized for Old<T>(). Ensure the type is JSON-serializable.
  ```
- Two members of the two types in objects passed directly were compared element by element, in
  enumeration order, by the comparison of 2.0.0. That comparison used reflection; only `Old<T>()`
  used JSON.
- One of the two types as `T` was a violation for two different instances, also of equal content
  (measured with three strings). The violation named `Items`, or `Keys` and `Values`. With those
  names given as patterns the call passed, also for different content.

The table "The 2.0.0 package on the same checks" of the accepted text is unchanged. It already says
of check 50 that 2.0.0 gave no exception ("frozen sets compared by element"). No run of the smoke
program in package mode is recorded for this change.

**Not tested.**

- Which internal class the library picks, and which element types keep the order in which the
  collection was built. The tests assert in their arrangement what they rely on: that two
  collections enumerate in the same order or in different orders, and that two runtime types are
  equal or differ.
- Rows 15, 42, 43 and 45 after the change. Row 15 is one instance on both sides (measured on
  `6b24bee`), which row 14 covers. Rows 42 and 43 are F7 and stay with #40. Row 45 stays with #57.
- Limit 1 for a class without a `GetHashCode` of its own with five or more elements: the verdict
  cannot be reproduced.
- A value type of the namespace that a member holds (F5, limit 5).
- Limit 4, the cost.
- Under Native AOT, an element class with no visible members below a `FrozenDictionary<TKey,
  TValue>`, and an element class with no visible members whose `Equals` reports the two values equal
  below a value of the two types. R8 decides both, and R8 did not change.
- A class derived from one of the two types as the declared `T`. Such a class is internal to the
  base class library, so only reflection can pass it. It was read from the code that it is compared
  as a whole.

**Qualified in the accepted text.** Each statement is quoted by its opening words:

- "Order for a pair: same reference or a `null` side → R6 → pointer and `string` → R4 → R5 → R3 →".
  At the step R5, a pair of two values of the two types is not decided. It goes on to the R1 memo
  and to R7.
- R5: "Values of B3 types are compared by reference". Two values of the two types are the exception:
  they are compared by R7. The rest of the sentence still holds for them: their members are never
  read. They are enumerated.
- Negative Consequences: "`EnsureAssignable` reports more in some cases (distinct B3 instances,
  different". Two distinct instances of the two types with equal elements, keys and values in the
  same enumeration order are not reported. The same entry names "state inside B3 instances" among
  what is reported less. For two different instances of the two types the elements, keys and values
  are compared; inside one shared instance they are still not.
- Known Limitations: "**Shared types** (B3, D7). State inside them is not snapshotted and compares
  by reference." A value of the two types is still not snapshotted. Two different instances of the
  two types do not compare by reference: they compare by element. What remains of the limitation for
  the two types is limit 3 above.
- "What held": "The namespace rule for frozen collections works (checks 49, 50)." See "Measured
  results, smoke program" above.

**Qualified in the #52 amendment.** Each statement is quoted by its opening words:

- The Status line of that amendment: "at the top level a `string`, a delegate type and a nullable
  value type are compared as one pair by R0–R8". One of the two types as `T` is now compared as one
  pair too. Its Revision History row ("compares a `string`, a delegate type and a nullable value
  type as one pair (#52)") states the same and is qualified with it.
- The statements that name the three kinds of `T`. "When `T` is a `string`, a delegate type or a
  nullable value type, two values that are"; in step 4 of the rule, "when `T` is a `string`, a
  delegate type or a nullable value type, the two values are compared" and "for every other `T`, the
  members of `typeof(T)` are walked, as before"; "The declared type `T` decides whether the two
  values are compared as a whole. The test is:"; and "For these three kinds of `T` the top level
  applies the whole "Order for a pair" of this ADR." Each also holds for one of the two types as
  `T`, and "every other `T`" no longer includes the two types. "The rule as it is now" above gives
  the steps, the fourth case of the test and the new row of the table.
- "A shared type (B3) other than `string` or a delegate as `T`, such as a `Stream`, a `Task` or a"
  frozen collection. "Its members are walked." The members of one of the two types as `T` are not
  walked. Every other type declared in the namespace `System.Collections.Frozen` is still walked as
  `T`: for example an array of the two types (row 45, which was not run after the change).
- "**Other shared types are not compared as a whole.**" The two types now are. `Stream`, `Task`,
  `Regex`, the comparers and the rest of B3 keep the member walk at the top level, with the
  exception that the #54 amendment made.
- The first rejected alternative, at "Two frozen sets with equal content compared equal as `T` on
  `669499c`." The alternative stays rejected: the test on `T` is not
  `SharedTypes.IsShared(typeof(T))`. It names the two types. Two frozen sets with equal content in
  the same enumeration order still compare equal as `T` (row 30), now by element and not by the
  members `Items` and `Count`.
- Limits: "A `string`, a delegate type and a nullable value type always have visible members, so
  an". Whether one of the two types as `T` always has visible members under Native AOT was not
  determined. That the check "no members visible" does not run for such a `T` was read from the
  code.
- Relation to #40 and #54, the entry "Left in #40", at "A shared type other than `string` or a"
  delegate as `T` walks its members. One of the two types as `T` does not. "What stays open" below
  gives what is left in #40 now.

The two statements of its Limits about real Native AOT are qualified under "Native AOT, the members
of an element are not visible" above.

**Still true in the #52 amendment.** The statement is quoted by its opening words:

- R1: "Their two values are not tracked as a pair of references." That stands for the three kinds of
  `T`. The two values of one of the two types are tracked as a pair while they are compared.

**Qualified in the #54 amendment.** Each statement is quoted by its opening words:

- The steps of a call: "5. When `T` is a `string`, a delegate type or a nullable value type, the two
  values are compared as" a whole. Step 5 also holds for one of the two types as `T`, and step 6
  ("every other `T`") no longer includes them.
- What does not change: "The same instance of a fixed shared type, or of a type that is not shared."
  The entry says that the call behaves as on `71b2e4e`. For one instance of one of the two types as
  `T`, the result is still "no exception" (row 38). But no member is read any more, and the elements
  of the pattern array are not examined, so an invalid pattern is no longer reported in such a call.
  That was read from the code; rows 39 and 40 measured it for two different instances.
- "Two different instances of a shared type as `T`. Their members are walked, as on `71b2e4e`." Not
  for one of the two types as `T`: the two instances are compared as a whole.
- "A member, an element or a field that holds a shared instance. R5 compares it by reference". The
  entry names two exceptions, a `string` and a delegate. Two different instances of the two types
  are a third: they are compared by R7. Its last sentence, on state inside the instance, holds for
  the two types as limit 3 above says.
- Decision S8: "Unchanged. B3 and R5 stand." `SharedTypes.IsShared` and B3 are unchanged by this
  change too. R5 now has the exception for the two types.
- Qualified in the #52 amendment, the fifth entry: "The entries of that list hold for two" different
  instances, and for the same instance of a fixed shared type. The fourth entry of that list does
  not hold for one of the two types as `T`, for two instances or for one.
- The sixth entry of the same part: "They are still not compared as a whole." and "a fixed shared
  type keeps it for the same instance too". The two types are now compared as a whole, and they do
  not keep the member walk, for two instances or for one. The other fixed shared types keep it.
- Limits: "State that a fixed shared instance refers to: a mutable element or value of a frozen
  collection,". The part names #49 for it under "What stays open". For a value of the two types that
  limit is now limit 3 above. It still passes in silence while both sides hold the same object at
  the same place. When the two sides are two instances, the elements, keys and values are compared.
  The target of a delegate and an object that a comparer wraps are as that entry says.
- What stays open, on #40: "Left in #40 of item 2: two different instances of a shared type as `T`;
  the same". For one of the two types as `T`, the first two parts of that list are taken by this
  change. "What stays open" below gives what is left.
- What stays open, on #49, at "proposes to compare them by elements". This change is #49. A value of
  the two types stays a fixed shared type, and the limit about its elements is now limit 3 above.

**Still true in the #54 amendment.** The statement is quoted by its opening words:

- "**Types compared as a whole.** A `string` and a delegate are fixed shared types." A value of the
  two types is a fixed shared type too. So its conclusion also holds for one of the two types as
  `T`: rule S and the comparison as a whole never both apply (read from the code; measured for one
  frozen set on both sides, row 38).

**Superseded in the #54 amendment.** The statement is quoted by its opening words:

- "The program has 74 checks in the default build and 73 in the" variant. It has 79 and 78. The
  Status line of that amendment ("the smoke program has 74 checks") and its Revision History row
  ("74 smoke checks") state the same count and are superseded with it.

Its result tables, its unit-test figures and its other statements are the dated record of #54 and
stay.

**Rejected alternatives.**

- **Copy a value of the two types in `Old<T>()`.** A change inside an element could then be
  detected. But it means to copy internal structures of the base class library (hash tables,
  comparers), and it reopens B3. It is outside #49.
- **Compare a `FrozenDictionary` by key lookup.** `FrozenDictionary` implements the non-generic
  `IDictionary`, so a lookup needs no reflection. But the rule would differ from the rule for
  `Dictionary<,>` (R7: enumeration order) and from the rule for `FrozenSet`.
- **Compare without regard to order.** `FrozenSet<T>` has no non-generic `Contains`. To call the
  generic one needs reflection over a method that trimming may remove; otherwise the pairs must be
  found by comparing every element with every other one. `HashSet<T>` and `Dictionary<,>` are
  compared in enumeration order, so a frozen set would be the only set that is compared differently.
- **Leave one of the two types as `T` to #40.** The rule for a member and the rule for `T` would
  disagree, and the verdict for `T` would still depend on members of the base class library.
- **Say in message C that the order counts.** It would be the only place where a message explains a
  rule of comparison. The documentation says it.

**What stays open.**

- **#49** is closed by this change, with the limits above.
- **[#40](https://github.com/cwouyang/uContract.NET/issues/40)**: this change takes one more part of
  the rest of item 2: one of the two types as `T`, for two different instances and for one instance
  on both sides. Left in #40 of item 2, for a shared type as `T` other than a `string`, a delegate
  type and the two types: two different instances, and the same instance of a fixed shared type (for
  example a `Regex`, a `Type` or a comparer). Also left: `T` = `object`, a base class or an
  interface for two different instances. Rows 41a to 44 record that last part for two values of the
  two types. With `T` = `object`, nothing of two frozen sets is compared. With `T` =
  `IReadOnlySet<string>`, nothing is compared under the JIT.
- **[#50](https://github.com/cwouyang/uContract.NET/issues/50)** (compare the counts of two
  dictionaries first): a pair of two `FrozenDictionary` values of one runtime type that a member or
  element holds now goes through the dictionary comparison of R7, so #50 will cover it. This change
  does not change where the counts are compared.
- **[#57](https://github.com/cwouyang/uContract.NET/issues/57)** (new, filed on 2026-10-10):
  `Old<T>()` shares an array of the two types. A unit test records the result of today for two such
  arrays that a member holds (row 21: a violation, by reference). Row 45 is recorded here and has no
  test.
- **[#46](https://github.com/cwouyang/uContract.NET/issues/46)** (fields-only comparison) is not
  affected.

**Why an amendment and not a new ADR.** B3 and R0–R8 stand. The change moves one group of types from
one existing rule (R5) to another (R7), and it adds one kind of `T` to the existing rule "compared
as a whole". It does not do what the #52 amendment reserved for a new ADR, which is to change what
every top-level comparison does. `docs/adr/README.md` lists breaking changes under "Requires ADR".
The amended ADRs (ADR-0022, ADR-0012, ADR-0021 and ADR-0007) are that record, as for #52 and #54
(F10).

**Unchanged.** Every other decision of this ADR.

### Amendment (2026-10-11): Arrays and the value types of System.Collections.Frozen are copied and compared like other arrays and values (#57)

This amendment follows the #49 amendment above. It records five changes that were made together for
[issue #57](https://github.com/cwouyang/uContract.NET/issues/57). #57 was filed for one defect, which
the #49 amendment names: `Old<T>()` shared an array of `FrozenSet<T>` with the original instead of
copying it. The measurements for #57 found four more defects on the same paths of the copy and of
the comparison, and the maintainer decided to repair all five in one change. `Old<T>()` now copies
an array whose element type is declared in `System.Collections.Frozen`, and a value of a value type
declared there. `EnsureAssignable<T>()` now compares two arrays that are passed as an array type or
as `System.Array` as a whole; it finds two arrays equal only when they have the same rank, and the
same length and lower bound in every dimension; and it compares two arrays of pointers or of
function pointers by address. `Old<T>()` still shares a `FrozenSet<T>`, a
`FrozenDictionary<TKey, TValue>` and every other class declared in that namespace. The amendments of
2026-10-11 of [ADR-0012](0012-dotnet-improvements-over-java.md),
[ADR-0021](0021-postconditions-under-trimming-and-aot.md) and
[ADR-0007](0007-reflection-field-comparison.md) point here.

**Terms.** This amendment uses these terms:

- **The namespace**: `System.Collections.Frozen`.
- **The two types**: `FrozenSet<T>` and `FrozenDictionary<TKey, TValue>`, as the #49 amendment
  defines them.
- **An array of the namespace**: an array whose element type is declared in the namespace, or whose
  innermost element type is: a `FrozenSet<int>[]`, a `FrozenSet<int>[][]`, a `FrozenSet<int>[,]`.
  `Type.Namespace` of such an array type is the namespace (measured for these three array types and
  for four more).
- **A value type of the namespace**: a struct or an enum declared in the namespace. The base class
  library declares the enumerators `FrozenSet<T>.Enumerator` and
  `FrozenDictionary<TKey, TValue>.Enumerator` there. An assembly of the caller can declare a type in
  that namespace too.
- **A class of the caller in the namespace**: a class that an assembly of the caller declares in the
  namespace. F5 of the #49 amendment names it.
- **An array as `T`**: the declared `T` is an array type, of any rank and with any element type, or
  it is `System.Array`.
- **Rank, lengths and lower bounds**: the rank of an array, and its length and its lower bound in
  every dimension. This amendment does not call them the "shape" of an array, because B1 uses
  "shape" for the object graph.
- **The dimension test**: the rule that this amendment adds for two arrays. They are different when
  their ranks differ, or when a length or a lower bound differs in any dimension.
- **Compared as a whole** and **message C**: as the #52 amendment and the #49 amendment define them.
- **Violation**: a `PostconditionViolationException` whose description begins "Fields were modified
  that are not marked as assignable" and names a member.
- **`82c6255`**: the commit of `master` on which the design was measured, before the library
  changed. "Before" in this amendment is the result there, under the JIT, on .NET 8.0.16. "2.0.0" is
  the result with the 2.0.0 package. "After" is the result on the library as this change leaves it.
  "Measured" below says where each result comes from.
- **Dynamic code off**: a unit test or a measurement that runs under the test seam that simulates
  Native AOT. Under the seam a type counts as having no visible members only when it reflects with
  no member at all.

**What changed.** There are five changes. The first two are one change of one test in the code.

1. **`Old<T>()` copies an array of the namespace.** B3 shares a type "in the
   `System.Collections.Frozen` namespace". On `82c6255` the code tested `Type.Namespace` alone, and
   the runtime reports the namespace of an array type as that of its innermost element type. So an
   array of the namespace was shared: the copy of an object held the original's own array (rows 1,
   4, 16 and 17 below). The copy of a `List<FrozenSet<int>>` kept the original's storage: an element
   that was replaced afterwards was replaced in the copy too (row 8), and after `Clear` the copy
   still counted two elements and held `null` (row 9). The namespace category of B3 now matches a
   type that is neither an array type nor a value type. An array of the namespace is copied by B5,
   as every other array is. Each element keeps its own rule: an element of the two types, or of a
   class of the caller in the namespace, is still the original's instance.
2. **A value of a value type of the namespace is copied and compared like a value of any other
   value type.** The same test no longer matches a value type. On `82c6255` `Old<T>()` returned a
   boxed value of such a type as it was (row 39), with the references inside it (rows 43 to 45), and
   R5 compared two values of such a type by reference. Each side of a member of such a type is boxed
   separately, so the member was never equal, also to its own copy (rows 30, 33, 34, 40 and 47;
   limit 5 of the #49 amendment). Now B2 applies to a boxed value, and R3 compares two values:
   `Equals` first, and then the visible fields.
3. **An array as `T` is compared as a whole.** `actual` and `expected` are one pair. The pair is
   compared element by element, and each element by the rules for a member or an element. No member
   of `T` is compared, the assignable patterns do not apply, and their elements are not examined. A
   difference is a `PostconditionViolationException` with message C. On `82c6255` the members of the
   array type were walked. Two arrays of different lengths were reported through `Length` and
   `LongLength` (row 52). A different element was not reported at all (rows 51 and 53 to 57, 64 and
   65). The design gives the cause, which was read from the code of `82c6255` and was not measured:
   of the members of an array, only `SyncRoot` leads to its elements, and it returns the array
   itself; the two arrays are the top-level pair, which is in progress for the whole call (R1), so
   the pair counted as equal when `SyncRoot` reached it again.
4. **Two arrays are equal only when they have the same rank, lengths and lower bounds** (the
   dimension test). The test applies to two arrays wherever the comparison meets them as a pair: the
   two values of a member or of a field of a struct, two elements, two keys or two values of a
   dictionary, and the two values of an array as `T`. The runtime types of the two arrays need not
   be equal. The test does not apply to an array and a sequence that is not an array. On `82c6255`
   an array of any rank was compared as the one sequence that it enumerates, so an `int[,]` of 1 by
   2 equalled one of 2 by 1 with the same elements in the same order (row 83).
5. **Two arrays of pointers or of function pointers are compared by address.** After the dimension
   test, the addresses that the two arrays hold are compared in the order of the memory of the
   arrays. The types that the pointers point to are not compared, and no address is dereferenced. An
   array of pointers or of function pointers that has at least one element is different from every
   value that is not such an array. One that has no element is compared as any other sequence is.
   Limit 8 below gives the one case in which neither the dimension test nor the comparison by
   address decides: such an array as one of the two values of a call whose `T` is compared by its
   members.
   `Old<T>()` copies such an array with its addresses; for an array of pointers it did so before
   (row 101). In the cases that were measured on `82c6255`, the comparison threw
   `NotSupportedException` when a member held an array of pointers with an element and the other
   side held another array or sequence (rows 102, 103, 107, 109, 110, 112 and 114). `Old<T>()` and
   the comparison threw `BadImageFormatException` for an object that holds an array of function
   pointers, and the comparison threw it for such an array as `T` (rows 108, 116 and 117): the
   classification of B3 read `Type.Namespace` of the array type, and the runtime throws when the
   innermost element type of an array type is a function pointer.

**In the code.** `SharedTypes.IsClassOfTheFrozenNamespace` is the namespace category of B3: the type
is not an array type, it is not a value type, and its `Type.Namespace` is the namespace. The two
tests on the kind of type come first, so `Type.Namespace` of an array type is not read.
`SharedTypes.Classify` answers "not shared" before it asks any category about an array of pointers
or of function pointers, and about a type whose innermost element type is a function pointer.
`MemberComparison.ArraysDifferInRankLengthOrLowerBound` is the dimension test. It reads `Rank`,
`GetLength` and `GetLowerBound`, and no element. `MemberComparison.TryGetAddresses` gives the
addresses of an array of pointers or of function pointers that has an element. It reads the memory
of the array as native integers through `MemoryMarshal.GetArrayDataReference`,
`MemoryMarshal.CreateReadOnlySpan` and `Unsafe.As`, after it has tested the element type.
`MemberComparison.IsArrayTypeOrSystemArray` is the test for an array as `T`: the type is an array
type, or it is `typeof(Array)`. The helper `CannotCompareBelowWhole` now also serves an array as
`T`.

**The rule as it is now.** The steps of a call are those of the #49 amendment. Step 5 gains the
arrays:

1. The pattern array is checked for `null`.
2. A call that does not compare returns.
3. The `null` rule.
4. Rule S.
5. When `T` is a `string`, a delegate type, a nullable value type, one of the two types, or an array
   type or `System.Array`, the two values are compared as a whole.
6. For every other `T`, the members of `typeof(T)` are walked.

The test of the #52 amendment on the declared `T` gains a fifth case: `typeof(T)` is an array type,
or it is `System.Array`. The table of that amendment ("What R0–R8 give for two values that are not
`null`") gains this row:

| `T` | Compared by | Rule |
|---|---|---|
| an array type or `System.Array` | The same instance is equal. Otherwise the dimension test comes first. Then two arrays of pointers or of function pointers that have elements are compared by address, and such an array against an array that is not one is different (change 5 above); any other two arrays are compared element by element, in the order in which they enumerate, each element by R0–R8 | the dimension test, then R7; R6 leads to them when the runtime types of the two arrays differ |

The "Order for a pair" of the accepted text, as the #49 amendment and this amendment leave it:

same reference or a `null` side → R6 → pointer and `string` → R4 → R5 (not for two values of the two
types) → R3 → R1 memo → the dimension test (for two arrays) → R7 or R8 → member walk.

What holds for the comparison of an array as `T` as a whole:

- It runs with the recursion guard set. It can run the property getters, enumerators and `Equals` of
  the elements and of what they refer to, as the member walk can (read from the code; measured for a
  getter of an element that throws: the call ends in a `TargetInvocationException`).
- The pair of the two arrays is tracked by R1 while it is compared. An element that leads back to
  its array counts as equal (measured: rows 70 and 71 below).
- The patterns are not used, and the elements of the pattern array are not examined (measured: rows
  59 and 60). The array itself is still checked for `null` first (row 61).
- No member metadata of `T` is read. So the rule "no members visible" of ADR-0021 is not reached for
  `T` (read from the code). The class of an element is still checked where the comparison reaches it
  (rows 79 to 82).
- The declared type decides. A value that is not `null` and whose declared type is an array type or
  `System.Array` is an array, so the declared type and the runtime type cannot disagree about the
  kind. Two arrays that are passed as `object` or as an interface are not compared as a whole: the
  members that are visible through that `T` are compared (row 77; #40). A collection class as `T`,
  such as a `List<int>`, is compared by its members as before (row 78).

**Decisions and reasons.** The design records that the maintainer made these decisions on
2026-10-10. They are numbered A1 to A12 so that they are not taken for D1 to D13 of this ADR, for S1
to S10 of the #54 amendment or for F1 to F10 of the #49 amendment.

| # | Question | Decision |
|---|----------|----------|
| A1 | Scope | All five changes are made in one change: the arrays of the namespace (the defect that #57 was filed for), the value types of the namespace, an array as `T` (an array type and `System.Array`), the dimension test, and the arrays of pointers and of function pointers. |
| A2 | Where an array type and a value type leave B3 | In the namespace category only. The other categories and the lists of B3 are asked about an array type and about a value type as before. An array of pointers or of function pointers, and a type whose innermost element type is a function pointer, are answered "not shared" before any category is asked. |
| A3 | What stays shared | A value of the two types (F1 of the #49 amendment), and a class of the caller in the namespace, which is still compared by reference (F5). |
| A4 | A lower bound | It counts. A one-dimensional array with lower bound 1 and an `int[]` of the same elements are different. |
| A5 | Element types | Two arrays that pass the dimension test and have different element types are compared element by element. An array of pointers or of function pointers that has elements is the exception: A9 and change 5 above give its rule. |
| A6 | An array against a sequence that is not an array | Compared by what the two enumerate, as before. The dimension test is for two arrays. |
| A7 | The message for an array as `T` | Message C, with the declared `T`. No message is added or changed. Neither message C nor a violation says that a rank, a length or a lower bound differs. |
| A8 | The path of the "cannot compare" message below an array as `T` | It starts with the parameter name `actual`, not with `typeof(T).Name`. Below that the path is built as before. The path below one of the two types as `T` and below a nullable value type as `T` does not change. |
| A9 | A pointer or a function pointer as an element | Compared by its address only, as a pointer field is. An array of pointers or of function pointers that has no element follows the rules for other sequences. |
| A10 | CHANGELOG | For the arrays and the value types of the namespace, existing entries are rewritten and none is added. An array as `T` goes into the existing entry on a `T` that is compared as a whole. The dimension test gets an entry of its own, marked BREAKING. The arrays of pointers and of function pointers get an entry under Fixed. The amendment of ADR-0012 for #57 records what the CHANGELOG holds. |
| A11 | Unit tests of `SharedTypes.IsShared` and `SharedTypes.IsFrozenSetOrDictionary` | Direct tests are added. They pin two internal methods on purpose. |
| A12 | Smoke checks | Five are added, checks 80 to 84. No smoke check covers an array of pointers or of function pointers. |

The reasons, as far as the design gives them:

- **A1.** The four further defects were found on the paths that the first one uses. Six rows of the
  tables below reach their result only with two of the changes together (rows 18, 58, 99, 100, 105
  and 117).
- **A2.** For the array types and the value types that were measured, no test can tell this
  placement from an exclusion ahead of every rule: on `82c6255`, no other rule of B3 matched one of
  them (see "Measured, other results"). An array of pointers or of function pointers is never
  shared, and the code gives that answer before it asks any rule, so that no rule has to be shown
  not to throw and not to match for such a type; the comment at that place says so. On `82c6255`
  the namespace category threw for an array of function pointers. At the final state it reads the
  namespace of no array type, and the early answer is kept (see "Where the work differs from the
  design").
- **A4.** The design records the decision and its known cost, and no reason beyond that. The cost: a
  one-based array against a zero-based array of the same elements is a violation, for example an
  `object[,]` that was read from a spreadsheet against an expected array written in C#. `Old<T>()`
  keeps the lower bounds of an array that it copies (B5; row 98), so the dimension test adds no
  violation to the pair of `Old<T>()` and `EnsureAssignable<T>()` when nothing changed.
- **A5.** This is what R6 and R7 already did for two sequences of different runtime types. An
  `int[] {1, 2}` and an `object[] {1, 2}` are equal. An `int[] {1, 2}` and a `long[] {1, 2}` are
  not, because the boxed elements have different runtime types (rows 74 and 93).
- **A6.** This is R6 and R7 as they were. It has one consequence: across kinds of sequence, equality
  is not transitive (row 94, and the limits below).
- **A7.** Message C is true of the case as it stands, as F8 of the #49 amendment says for the two
  types.
- **A8.** The name of an array type holds brackets of its own. With `typeof(T).Name`, the path to an
  element of a `HiddenLeaf[]` would read `HiddenLeaf[][]`, as if there were one more level of
  elements, and for `System.Array` it would read `Array[]`. A prototype that was measured for the
  design printed a path of that form (``Empty57[][]`` for an element of an `Empty57[]`).
- **A9.** A pointer field is compared by its address (R3; row 115), and an element follows the same
  rule. With no element there is no address to compare.
- **A10.** The design gives this reason for adding no entry for the arrays and the value types of
  the namespace: against 2.0.0 they bring nothing that the existing entries do not already state.
  2.0.0 threw from `Old<T>()` for these types (rows 1, 8, 17, 30 and 37). 2.0.0 found two arrays of
  frozen sets of equal content equal (rows 20 and 21). A struct of the caller in the namespace
  behaved in 2.0.0 as it does now (rows 34 to 36). Two enumerators of two sets of equal content were
  a violation in 2.0.0 and are equal now (row 32); the existing entry on what is reported less
  covers that with its sentence on structs of equal content. #49 was handled in the same way (F9).
- **A12.** The design fixed the expected result of each check before the publish. For checks 82 and
  84 it also fixed that a publish with another result stops the work and goes back to the
  maintainer. Both gave the expected result (see "Measured results, smoke program"). That no check
  covers an array of pointers or of function pointers is a decision; "Not covered by a measurement"
  below lists it.

Two more points of the design are about words and not about behaviour. The documents say "rank,
lengths and lower bounds" and not "shape". And the unit tests state "dynamic code off" only for a
type that reflects with no member at all; every other statement about Native AOT rests on the smoke
program or is marked as not measured.

**Behaviour.** "2.0.0" was measured with the 2.0.0 package and "Before" on `82c6255`, with tests and
programs that are not in the repository. "After" was measured in the same way on the library as this
change leaves it, and unit tests of this change show it (see "Measured"). "Not measured" means that
no measurement exists for the cell. In the tables:

- `h` is a holder whose member `Items` has the type that the row names, and `old` is
  `Contract.Old(() => h)`. "Two holders" are two such objects that are passed to
  `EnsureAssignable<T>()` directly, without `Old<T>()`.
- s1 and s1b are two frozen sets of equal content that enumerate in one order. s2 is a frozen set of
  other content.
- "—" means no exception. "V" means a violation that names `Items` (and the backing field of
  `Items`, when it is an auto-property), or the member that the cell gives. "C" means a
  `PostconditionViolationException` with message C.
- "n/a" means that the step cannot be reached, because `Old<T>()` throws.
- The cells of one row are separated by ";", in the order of the cases of the row.
- The rows keep the numbers that the design gave them; 28 and 29 are not used.

`Old<T>()` and an array of the namespace:

| # | Case | 2.0.0 | Before | After |
|---|---|---|---|---|
| 1 | `FrozenSet<int>[]`: is `old.Items` the original's array | `Old<T>()` throws `InvalidOperationException` | yes | no; its element is the original's set |
| 2 | row 1, then `h.Items[0] = s2`; `EnsureAssignable(h, old)` | n/a | — | V |
| 3 | row 1, nothing changed; `EnsureAssignable(h, old)` | n/a | — | — |
| 4 | `FrozenSet<int>[][]`, an element of an inner array replaced by s2 | not measured | — (the outer and the inner array are the original's) | V (neither is) |
| 5 | `FrozenSet<int>[,]`, an element replaced by s2 | not measured | — | V |
| 6 | an array of a class of the caller in the namespace, an element replaced by an instance of other content | V | — | V |
| 7 | row 6, nothing changed; then a property of the element changed | not measured | —; — | —; — (the element is shared) |
| 8 | `List<FrozenSet<int>>`, an element replaced through the indexer: is `old.Items[0]` still the original set; `EnsureAssignable(h, old)` | `Old<T>()` throws | no; — | yes; V |
| 9 | `List<FrozenSet<int>>` with two elements, then `Clear`: `old.Items[0]` | n/a | `null` | not `null` |
| 10 | `List<FrozenDictionary<string, int>>`, an element replaced by a dictionary of other content | not measured | — | V |
| 11 | `Stack<FrozenSet<int>>`, `Pop` and then `Push` of another set: is `old.Items.Peek()` the original set; `EnsureAssignable(h, old)` | not measured | no; — | yes; V |
| 12 | `Queue<FrozenSet<int>>` of capacity 1, `Dequeue` and then `Enqueue` of another set: is `old.Items.Peek()` the original set; `EnsureAssignable(h, old)` | not measured | no; — | yes; V |
| 13 | `ImmutableArray<FrozenSet<int>>`, an element replaced through `ImmutableCollectionsMarshal.AsArray`: is `old.Items[0]` the original set; `EnsureAssignable(h, old)` | not measured | no; — | yes; V |
| 14 | `ArraySegment<FrozenSet<int>>`, an element of its array replaced by s2 | not measured | — | V |
| 15 | `Collection<FrozenSet<int>>`, an element replaced by s2 | not measured | — | V |
| 16 | `Items` declared `object` and holding a `FrozenSet<int>[]`: is `old.Items` the original's array | not measured | yes | no |
| 17 | `Old(() => a)` for a `FrozenSet<int>[]` `a`: is the result `a` | `Old<T>()` throws | yes | no |
| 18 | row 17, then `a[0] = s2`; `EnsureAssignable(a, old)` | n/a | — | C |
| 19 | for comparison: `List<int>`, an element replaced | not measured | V | V |

Two holders, each with its own array:

| # | Values | 2.0.0 | Before | After |
|---|---|---|---|---|
| 20 | two `FrozenSet<int>[]` that hold one set | — | V | — |
| 21 | two `FrozenSet<int>[]`, with s1 and with s1b | — | V | — |
| 22 | two `FrozenSet<int>[]`, with s1 and with s2 | V | V | V |
| 23 | two `FrozenSet<int>[]` of lengths 1 and 2 | V | V | V |
| 24 | both hold `null`; both are empty (two instances) | —; — | V; V | —; — |
| 25 | two `FrozenSet<int>[][]`; two `FrozenSet<int>[,]`; one set in both | not measured | V; V | —; — |
| 26 | two `FrozenDictionary<string, int>[]` of equal content | — | V | — |
| 27 | two arrays of a class of the caller in the namespace: one element instance in both; two instances of equal content | —; — | V; V | —; V |

The second cell of row 27 differs from 2.0.0. #45 made that difference, and it stays: a class of the
caller in the namespace is still shared and compared by reference (A3).

The value types of the namespace. `E` is a `FrozenSet<int>.Enumerator`. `S` is a struct of the
caller that is declared in the namespace and has one `int` field. `SR` is such a struct with a field
`Numbers` of type `List<int>`. The 2.0.0 cells of rows 34 to 36, 38 and 41 were measured with a
struct whose `int` is an auto-property; such a struct gave the same "Before" and "After" as `S`.

| # | Case | 2.0.0 | Before | After |
|---|---|---|---|---|
| 30 | member `E`, `Old<T>()`, nothing changed | `Old<T>()` throws `InvalidOperationException` | V | — |
| 31 | member `E`, the enumerator advanced by one after `Old<T>()` | n/a; two holders without `Old<T>()`, one enumerator advanced: V | V | V |
| 32 | two holders, enumerators of s1 and of s1b; of s1 and of s2 | V; V | V; V | —; V |
| 33 | member `FrozenDictionary<string, int>.Enumerator`, `Old<T>()`, nothing changed | not measured | V | — |
| 34 | member `S`, `Old<T>()`, nothing changed; the value changed | —; V | V; V | —; V |
| 35 | two holders, `S` of equal content; of different content | —; V | V; V | —; V |
| 36 | member `S[]`, `Old<T>()`, nothing changed; an element changed | —; V | —; — (the array is shared) | —; V |
| 37 | member `E[]`, `Old<T>()`, nothing changed | `Old<T>()` throws | — | — |
| 38 | member `List<S>`, `Old<T>()`, nothing changed; an element replaced by one of other content | —; not measured | V; V | —; V |
| 39 | `Items` declared `object` and holding a boxed `S`: is `old.Items` the original's box; nothing changed; replaced by a value of other content; replaced by another box of equal content | not measured | yes; —; V; V | no; —; V; — |
| 40 | member `S?`, `Old<T>()`, nothing changed; the value changed | not measured | V; V | —; V |
| 41 | `T` = `S?`, equal content; different content | `NotSupportedException`; `NotSupportedException` | C; C | —; C |
| 42 | member `SR`, `Old<T>()`, nothing changed; the list gained an element | not measured | V; V | —; V |
| 43 | `Old(() => sr)` for an `SR` `sr`: is the list of the result the original's list; then the list gained an element, `EnsureAssignable(sr, old)` | not measured | yes; — | no; V that names `Numbers` |
| 44 | `Items` declared `object` and holding a boxed `SR`: is the list of `old.Items` the original's list; then the list gained an element, `EnsureAssignable(h, old)` | not measured | yes; — | no; V |
| 45 | member `object[]` that holds a boxed `SR`, `Old<T>()`, the list gained an element | not measured | — | V |
| 46 | for comparison, two holders with a class of the caller in the namespace: one instance; two of equal content | not measured | —; V | —; V |
| 47 | member of an enum that is declared in the namespace, `Old<T>()`, nothing changed; the value changed | not measured | V; V | —; V |
| 48 | member array of that enum, `Old<T>()`, nothing changed; an element changed | not measured | —; — (the array is shared) | —; V |
| 49 | member `S?[]`, `Old<T>()`, nothing changed; an element changed | not measured | V; V | —; V |

Rows 42 to 45: on `82c6255` `Old<T>()` returned a boxed value of such a type as it was, so a
reference inside the box was shared with the original (rows 43 to 45). A member that is declared as
the struct type is part of the memory of its holder. Read from the code of the copier, which this
change did not touch: the references inside such a member were copied before too, so row 42 changed
through the comparison alone. Rows 30 to 33 and 37 rest on the private fields of the enumerators in
the runtime that ran them, .NET 8.0.16. The first cell of row 32 also needs two sets of equal
content to give enumerators with equal fields; no unit test holds that cell.

An array as `T`. `Box` is a small class with one property. `Base` and `Derived` are a class and a
class derived from it that adds a member:

| # | Case | 2.0.0 | Before | After |
|---|---|---|---|---|
| 50 | `T` = `int[]`, equal content | — | — | — |
| 51 | `T` = `int[]`, one element differs | V that names `SyncRoot` | — | C |
| 52 | `T` = `int[]`, lengths 2 and 1 | V that names `Length`, `LongLength` and `SyncRoot` | V that names `Length` and `LongLength` | C |
| 53 | `T` = `string[]`: one element differs; `null` against a string; `"aB"` against `"ab"` | V that names `SyncRoot`, each | —; —; — | C; C; C |
| 54 | `T` = `Box[]`: two instances of equal content; the content differs | —; V that names `SyncRoot` | —; — | —; C |
| 55 | `T` = `int[][]`, an element of an inner array differs | V that names `SyncRoot` | — | C |
| 56 | `T` = `int[,]`, one element differs | V that names `SyncRoot` | — | C |
| 57 | `T` = `object[]`, one element differs | V that names `SyncRoot` | — | C |
| 58 | `T` = `FrozenSet<int>[]`: with s1 and with s1b; with s1 and with s2 | —; V that names `SyncRoot` | V that names `SyncRoot`, each | —; C |
| 59 | `T` = `int[]`: one element differs, pattern `Length`; one element differs, pattern `SyncRoot`; the lengths differ, pattern `.*` | V that names `SyncRoot`; —; — | —; —; — | C; C; C |
| 60 | `T` = `int[]`, equal content: a pattern that is not a valid regular expression; a pattern array that holds `null` | `RegexParseException`; `ArgumentNullException` | the same | —; — |
| 61 | `T` = `int[]`, one element differs, and the pattern array itself is `null` | not measured | `ArgumentNullException` | `ArgumentNullException` |
| 62 | `actual` is `null` and `expected` is not; both are `null`: with `T` = `int[]`; with `T` = `Array` | `ArgumentNullException` for the first case with `T` = `int[]`; the others not measured | a `PostconditionViolationException` with the message "actual is null and expected is not."; —; for each `T` | the same |
| 63 | `T` = `int[]`: one instance on both sides; both are empty (two instances) | —; not measured | —; — | —; — |
| 64 | `Old(() => a)` for an `int[]` `a`, then `a[0]` changed; nothing changed | V that names `SyncRoot`; — | —; — | C; — |
| 65 | `Old(() => a)` for a `Box[]` `a`, then a property of an element changed | not measured | — | C |
| 66 | `T` = `Stream[]`: two arrays that hold one stream; one array on both sides; two arrays with two streams | not measured; not measured; `TargetInvocationException` | —; —; — | —; —; C |
| 67 | `Old(() => a)` for a `Stream[]` `a`, then the stream was written to | not measured | — | — (the stream is shared and is compared with itself) |
| 68 | `T` = `DateTime[]`, the same ticks and another `Kind`; the same two values with `T` = `DateTime` | —; not measured | —; V that names `Kind` and `_dateData` | the same |
| 69 | `T` = `Base[]`, with two `Derived` whose own member differs; the same two values with `T` = `Base` | not measured | —; — | C; — |
| 70 | `T` = `object[]`, each array holds itself: and nothing else; and a second element that differs; `Old<T>()`, nothing changed | not measured | —; —; — | —; C; — |
| 71 | `T` = `object[]`, two pairs of arrays that hold each other: and nothing else; and one other element that differs | not measured | —; — | —; C |
| 72 | `T` = `Array`, two `int[]`: one element differs; equal content | V that names `SyncRoot`; not measured | —; — | C; — |
| 73 | `T` = `Array`, two `int[]` of different lengths, pattern `Length` | V that names `SyncRoot` | — | C |
| 74 | `T` = `Array`: `int[] {1, 2}` and `object[] {1, 2}`; `int[] {1, 2}` and `long[] {1, 2}` | —; V that names `SyncRoot` | —; — | —; C |
| 75 | `T` = `Array`, `Old<T>()`, then one element changed | not measured | — | C |
| 76 | `T` = `object[]`, a `string[]` and an `object[]` of equal content | — | — | — |
| 77 | for comparison, a declared type that is not an array type and can hold an array: `T` = `object`, `IEnumerable<int>`, `IList<int>`, `IReadOnlyList<int>`, `IList`, `ICloneable`, each with two `int[]` of which one element differs | — for `object`, `IList<int>` and `IList`; the others not measured | —, each | —, each |
| 78 | for comparison, a collection class as `T`: `List<int>`, one element differs; equal content; one element differs, pattern `_items`; `ImmutableArray<int>`, one element differs, pattern `array` | not measured | V that names `_items`; —; —; — | the same |

Message C names the declared `T` as `Type.ToString()` gives it. For row 51 and for row 72:

```text
actual and expected are not equal. System.Int32[] is compared as a whole, so assignable patterns do not apply.
actual and expected are not equal. System.Array is compared as a whole, so assignable patterns do not apply.
```

Rows 77 and 78 stay with #40: the members that the declared type shows are compared.

An array as `T` whose element class reflects with no member, with dynamic code off. `X` is a class
with no member. No 2.0.0 result exists for these rows:

| # | Case | Before | After |
|---|---|---|---|
| 79 | `T` = `X[]`: two arrays, each with its own instance; `Old<T>()`, nothing changed | —; — | `InvalidOperationException` "cannot compare" with the path `actual[]`, each |
| 80 | `T` = `X[]`: two arrays that hold one instance; both are empty | —; — | —; — |
| 81 | `T` = an array of a class with no member whose `Equals` says "equal" for any two instances; two arrays, two instances | — | — |
| 82 | for comparison, a member `X[]`: two arrays, two instances; `Old<T>()`, nothing changed; a member array of the class of row 81 | `InvalidOperationException` of R8 with a path through the member (``H`1.Items[]``); the same; — | the same |

Row 79 gives an array as `T` the result that an array in a member already had (row 82). The
exception is thrown only when `Equals` reports the two elements unequal (row 81), as its message
says. The message is the one that the #52 amendment gives for a class below a nullable value type,
with another path. Unit tests hold the whole message for five paths:

| `T` and values | Path in the message |
|---|---|
| `X[]`, two arrays, each with its own instance | `actual[]` |
| `X[][]`, the inner arrays hold two instances | `actual[][]` |
| `X[,]` | `actual[]` |
| `System.Array`, holding two `X[]` | `actual[]` |
| `Holder[]`, where `Holder` has a member `Leaf` of type `X` | `actual[].Leaf` |

The path has one `[]` for each sequence that the comparison walks, whatever its rank, and `.Name`
for each member.

Rank, lengths and lower bounds:

| # | Case | 2.0.0 | Before | After |
|---|---|---|---|---|
| 83 | member `int[,]`: 1 by 2 and 2 by 1, the same elements in the same order | — | — | V |
| 84 | member `int[,]`: 2 by 3 and 3 by 2 | — | — | V |
| 85 | member `int[,]`: 2 by 0 and 0 by 2; 0 by 3 and 0 by 5 | —; not measured | —; — | V; V |
| 86 | member `int[,]`, 2 by 2: equal content; one element differs | not measured; V | —; V | —; V |
| 87 | member declared `Array`: `int[] {1, 2}` and `int[,] {{1, 2}}` | — | — | V |
| 88 | member declared `Array`: an `int[,]` of 1 by 2 and an `int[,,]` of 1 by 1 by 2 | — | — | V |
| 89 | member declared `Array`: `int[] {1, 2}` and a one-dimensional array with lower bound 1 that holds 1 and 2 | — | — | V |
| 90 | member declared `Array`: two one-dimensional arrays of equal content, with lower bounds 1 and 1; with lower bounds 1 and 2 | not measured; — | —; — | —; V |
| 91 | member declared `Array`: two `int[,]` of 1 by 2, with lower bounds (0, 0) and (1, 1); with lower bounds (0, 0) and (0, 1) | —; not measured | —; — | V; V |
| 92 | member `int[,,]`: 1 by 2 by 3 and 1 by 3 by 2 | — | — | V |
| 93 | member declared `Array`: `int[] {1, 2}` and `object[] {1, 2}`; `int[] {1, 2}` and `long[] {1, 2}` | —; not measured | —; V | —; V |
| 94 | member declared `IEnumerable`: `int[,] {{1, 2}}` and `List<int> {1, 2}`; `List<int> {1, 2}` and `int[] {1, 2}`; `int[,] {{1, 2}}` and `int[] {1, 2}` | not measured | —; —; — | —; —; V |
| 95 | member declared `object`: `int[,]` of 1 by 2 and of 2 by 1 | — | — | V |
| 96 | arrays of 1 by 2 and of 2 by 1 below a member: the elements of a `List<int[,]>`; the values of a `Dictionary<string, int[,]>`; the keys of a `Dictionary<int[,], int>`; a field of a struct; the elements of an `ImmutableArray<int[,]>`; the elements of an `int[][,]` | — for the list; the others not measured | —, each | V, each |
| 97 | member declared `Array`: an `int[0][]` and an `int[0, 0]`; an empty `int[]` and an empty one-dimensional array with lower bound 1 | not measured | —; — | V; V |
| 98 | `Old<T>()`, nothing changed: a member `int[,]` of 2 by 2; one of 2 by 3; a member declared `Array` that holds an array with lower bound 1 | `Old<T>()` throws for the first and for the third; the second not measured | —, each | —, each |
| 99 | `T` = `int[,]`: 1 by 2 and 2 by 1 | — | — | C |
| 100 | `T` = `Array`: `int[] {1, 2}` and `int[,] {{1, 2}}`; an `int[]` and an array with lower bound 1 of the same content | V that names `Rank`; — | V that names `Rank`; — | C; C |

`Old<T>()` keeps rank, lengths and lower bounds (B5; row 98). Row 94 shows that equality is not
transitive across kinds of sequence: an `int[,] {{1, 2}}` equals a `List<int> {1, 2}`, which equals
an `int[] {1, 2}`, and the two arrays differ.

Arrays of pointers and of function pointers. `px` and `py` are two addresses. "NSE" is
`NotSupportedException`, and "BIF" is `BadImageFormatException`. The table has no column for 2.0.0;
"Measured with the 2.0.0 package" below gives the eight results that exist:

| # | Case | Before | After |
|---|---|---|---|
| 101 | for comparison, `Old<T>()` of a holder of an `int*[]`: is the array the original's; is the address the same | no; yes | the same |
| 102 | two holders, `int*[]`: the same address; different addresses; lengths 1 and 2 with an equal first address | NSE, each | —; V; V |
| 103 | holder of an `int*[]`, `Old<T>()`: nothing changed; an element set to another address; the `int` behind the address changed | NSE, each | —; V; — |
| 104 | for comparison: one `int*[]` instance in two holders; two empty `int*[]` in a member declared `Array`; `T` = `int*[]`, one instance on both sides; `T` = `int*[]`, both are empty | —, each | —, each |
| 105 | `T` = `int*[]`: the same address; different addresses; `Old<T>()`, then an element set to another address | —; —; — | —; C; C |
| 106 | for comparison: `T` = `Array`, two `int*[]` of the same address | — | — |
| 107 | two holders, `int*[,]` of 1 by 2: the same addresses; the second address differs; 1 by 2 against 2 by 1 | NSE, each | —; V; V |
| 108 | two holders, `delegate*<void>[]`: the same function; a function and null; two different functions | BIF, each | —; V; V |
| 109 | member declared `Array`: an `int*[]` and a `long*[]` that hold the same address; an `int*[]` and a `delegate*<void>[]` that hold the same address | NSE; NSE | —; — |
| 110 | an array of pointers that has an element against another kind of value: an `int*[]` and a `nint[]` that hold the same address; `int*[] {px}` and an empty `nint[]`; `int*[] {px}` and an empty `List<int>`; `int*[] {px}` and an `object[]` that holds `Pointer.Box(px)` | NSE, each | V, each |
| 111 | for comparison, no element: an empty `int*[]` against an empty `List<int>`; against an empty `int[]`; against an empty `long*[]`; against an empty `nint[]` | —, each | —, each |
| 112 | member declared `Array`: `int*[] {px, py}` and `int*[,] {{px, py}}`; `int*[] {px, py}` and an array with lower bound 1 of the same addresses; two arrays with lower bound 1 of the same addresses; two such arrays of which the second address differs; `int*[,]` of 1 by 2 and of 2 by 1 that hold null pointers | NSE, each | V; V; —; V; V |
| 113 | member declared `Array`: `int*[,]` of 0 by 3 and of 0 by 5 | — | V |
| 114 | arrays of pointers as elements: an `int*[][]` whose inner arrays hold the same address; one whose inner arrays hold different addresses; a `List<int*[]>` whose arrays hold different addresses | NSE, each | —; V; V |
| 115 | for comparison, pointer fields: the same address; different addresses; `Old<T>()`, nothing changed; `Old<T>()`, then the `int` behind the address changed | —; V that names the field; —; — | the same |
| 116 | holder of a `delegate*<void>[]`: `Old<T>()` alone; `Old<T>()`, nothing changed; `Old<T>()`, then an element set to another function | BIF, each | `Old<T>()` throws nothing, and its array is another array with the same address; —; V |
| 117 | `T` = `delegate*<void>[]`: the same function; different functions | BIF; BIF | —; C |

Of the 378 calls that were measured for the design, 203 give another result on the library as this
change leaves it than on `82c6255`.

**Limits.** Each was checked against the code, and the row or the measurement that shows it is
named. "Read from the code" marks a limit that no test or measurement shows.

1. **An element keeps its own rule.** An element of a copied array or list that is of the two types
   is the original's instance (row 1). The limits that the #49 amendment gives for shared elements
   (its limit 3) apply to it unchanged.
2. **One shared instance in both arrays.** A shared element that is one instance on both sides is
   compared with itself and is equal, whatever changed inside it (row 67). Rule S reports one
   instance of a state-holding shared type only when it is passed as `actual` and as `expected`
   themselves.
3. **A class of the caller in the namespace** is still shared and compared by reference (rows 7, 27
   and 46).
4. **An element of an array as `T` is not compared as that type is as `T`.** It is compared by the
   rules for a member or an element: a value type by `Equals` first (row 68), by the runtime type of
   the element (row 69), and a shared element by reference (row 66). R5 has its exceptions here
   too: a `string` element is compared with `Equals` (row 53), a delegate by its methods (R4), and
   two elements of the two types element by element (row 58).
5. **A value type of the namespace as `T`** is compared member by member, as every struct as `T` is
   (row 43). One whose getter throws still fails: `T` = `FrozenSet<int>.Enumerator` with two
   enumerators of one set gives a `TargetInvocationException`, before and after (measured; #40).
6. **Only the declared type selects the comparison as a whole.** Any other declared type that holds
   an array at run time is compared by the members that it shows (rows 77 and 78; #40). A generic
   method that forwards its own type parameter and its patterns gets the comparison as a whole when
   it is called with an array type, and its patterns then do not apply (measured).
7. **Patterns.** For an array as `T`, a pattern that is not a valid regular expression and a `null`
   element of the pattern array are not reported (row 60). A pattern array that is `null` itself is
   still rejected (row 61).
8. **The two values of a call whose `T` is compared by its members.** Neither the dimension test nor
   the comparison by address decides the two values of the call themselves when `T` is neither an
   array type nor `System.Array`: a member that leads back to those two values is answered as equal
   before either. For the comparison by address it was measured for two arrays of pointers: with
   `ICollection` as `T`, an `int*[] {px}` and an `int*[] {py}` give no exception, where the same two
   arrays give message C with `T` = `int*[]` and with `T` = `Array`; an `int*[] {px}` and an
   `int*[] {px, py}` as `ICollection` give a violation that names `Count`. Read from the code and
   not measured: the same holds for two arrays of function pointers, and for the two values of the
   call when they are not both arrays. An `int*[] {px}` and a `List<int>` of one element as
   `ICollection` have equal counts, and when `SyncRoot` of each returns the value itself, that
   member leads back to the top-level pair and counts as equal, so the rule that such an array is
   different from every value that is not such an array does not decide the call. For the dimension
   test it was measured. With
   `ICollection` as `T`, an `int[,]` of 1 by 2 and one of 2 by 1 give no exception: `ICollection`
   shows `Count`, `IsSynchronized` and `SyncRoot`, the counts are equal, and `SyncRoot` of an array
   is the array itself, which leads back to the top-level pair (a unit test shows it; measured also
   for an `int[2]` and an `int[1, 2]`). With different numbers of elements, `Count` is reported
   (measured for `ICollection`, `ICollection<int>` and `IReadOnlyCollection<int>` with an `int[2]`
   and an `int[1]`).
9. **Native AOT, an element class with no visible members.** An array as `T` that holds two
   instances of such a class throws `InvalidOperationException` when `Equals` reports them unequal,
   also when nothing changed (row 79; smoke check 82), as an array in a member does. When `Equals`
   says "equal", they are equal (row 81); the Known Limitation "**R8 trusts `Equals`**" applies. No
   smoke check was added for an `Equals` that ignores state.
10. **Native AOT, `Old<T>()` copies what reflection shows.** A collection whose storage field is not
    visible keeps sharing its storage, as the Known Limitations say of every hidden field. Smoke
    check 83 preserves `List<FrozenSet<string>>` with `DynamicDependency`. What the copy of such a
    list shares without that attribute was not measured.
11. **An array as a key.** After `Old<T>()` has copied a dictionary with its default comparer, a
    lookup with a key that is an array misses, with the original key and with the copy's own key
    (measured for a `Dictionary<int[], int>`; a `HashSet<int[]>` copy contains neither). This is the
    Known Limitation "**Identity-hashed keys** (D8)." This change extends it to a key that is an
    array of the namespace: on `82c6255` such a key was shared and was found; now it is copied and
    is not found (measured for a `Dictionary<FrozenSet<int>[], int>`). A key that is a
    `FrozenSet<int>` is still the original's set and is found. Enumerating the copy works, and the
    comparison of such a dictionary is not affected (measured: no exception when nothing changed,
    and a violation when the value of that key changed). With a comparer that compares the content
    of the arrays, both lookups find the key (measured for a `Dictionary<int[], int>`).
12. **Lower bounds, element types, kinds of sequence.** A lower bound counts (A4). Element types
    count through the boxed elements (A5). Across kinds of sequence, equality is not transitive (row
    94).
13. **The messages.** Neither message C nor a violation says that a rank, a length or a lower bound
    differs.
14. **Pointers.** A pointer or a function pointer as an element is compared by its address only. The
    type that it points to and the memory behind the address are not compared (row 109; row 103,
    third cell), as for a pointer field (row 115).
15. **Cost.** An array as `T` is now compared element by element, which `82c6255` skipped. One call
    was measured: `EnsureAssignable(new byte[1_000_000], new byte[1_000_000])` with equal content
    allocated 80,781,544 bytes and took about 220 ms, in a Debug build. This is one measurement, not
    a rate. `Old<T>()` now copies an array of the namespace that it shared before, and the storage
    of a list of such elements (read from the code; not measured as a cost).
16. **Getters below an array as `T`.** The getters of the public properties run for two different
    instances of one class that is neither a shared type nor a sequence, wherever the comparison
    reaches such a pair: as two elements of the arrays, as two elements of a `FrozenSet<T>` that the
    arrays hold, or through a field of an element of a value type that its `Equals` does not settle.
    A getter that throws there ends the call in a `TargetInvocationException` (measured for these
    three places). No getter runs for one instance on both sides, for two different `Stream`s, or
    for two values of a value type that `Equals` settles (measured). The Known Limitation on a
    property that returns a new instance of its own type on every read applies below an array as `T`
    too (read from the code).
17. **A member that is an enumerator of a frozen set.** Measured on .NET 8.0.16, with a holder of a
    `FrozenSet<T>` of a class and of a `FrozenSet<T>.Enumerator` of that set: after `Old<T>()`, a
    field of an element of the set changes and the enumerator is not moved, and the call reports a
    violation that names the enumerator member. The same holds when the enumerator is boxed in a
    member declared `IEnumerator<T>`. With no enumerator member the same change gives no exception
    (limit 3 of the #49 amendment), and with the enumerator member and no change the call gives no
    exception. Why the enumerator member differs was not measured: the fields of the enumerator are
    private to the runtime and were not read. So the result depends on the runtime. The design
    leaves the private fields of the enumerators without a rule. The maintainer decided to state
    this as a limit, in the XML remarks of `Old<T>()` and in the API reference, and not to treat
    `FrozenSet<T>.Enumerator` and `FrozenDictionary<TKey, TValue>.Enumerator` differently from the
    other value types of the namespace: the code is not changed. Both texts say what to do: keep no
    enumerator of a frozen collection in the state that is compared, or, when `EnsureAssignable<T>()`
    compares the members of the type that holds it, list that member as assignable (read from the
    code: a member whose name a pattern matches is not compared; not measured for this case). The
    limit was measured for a `FrozenSet<T>` only.
18. **Another method still reads the namespace.** `Contract.EnsureImmutableCollection<T>()`
    reads `Type.Namespace` of the runtime type of its argument, and so throws
    `BadImageFormatException` for an array of function pointers (measured). This change does not
    touch that method.

**Measured results, unit tests.** JIT, whole suite, postconditions on. In each state the Debug and
the Release configuration gave the same total, with 0 failed:

| State | Tests |
|---|---|
| `82c6255`, before the work | 746 |
| after the dimension test | 780 |
| after the arrays of pointers and of function pointers | 836 |
| after an array as `T` | 907 |
| after the change of the namespace category | 989 |
| after the corrections that the review of the work asked for | 994 |

248 tests were added: 222 in four new test files (34 in `EnsureAssignableArrayDimensionTests.cs`, 54
in `EnsureAssignablePointerArrayTests.cs`, 74 in `EnsureAssignableArrayAsTTests.cs` and 60 in
`OldFrozenNamespaceArrayTests.cs`), and 26 in `SharedTypesTests.cs`. One test of #49 was rewritten:
`EnsureAssignable_WhenMemberHoldsTwoArraysOfOneFrozenSet_ReportsTheMember` held the "Before" of row
20, and is now `EnsureAssignable_WhenMemberHoldsTwoArraysOfOneFrozenSet_DoesNotThrow`.

**Measured results, the calls of the design.** The design measured 378 calls on `82c6255` and on
four prototypes, one for each state of the work. The same calls were measured again on the library
after each of the four changes and at its final state. In each of the five runs every call gave the
result that was expected for that state. At the final state that is the "After" of the tables above.
The expected value of two calls was corrected before the runs, the two calls of row 79: the path in
their message is `actual[]` (A8), where the prototype printed ``Empty57[][]``. The measurement files
are not in the repository.

**Measured results, mutation check.** With the four changes in place and 989 tests, before the five
tests that the review added, the library was changed by hand in four ways. No change was committed,
and the 989 tests ran each time.

- The dimension test never answers "different". 30 tests fail.
- No addresses are taken from an array of pointers or of function pointers, and the classification
  reads the namespace of such an array type again. 45 tests fail.
- An array type or `System.Array` as `T` is compared by the members of `T`. 38 tests fail.
- The namespace category shares arrays and value types again. 65 tests fail.

Before the runs, the work had listed the measured calls whose unit tests must fail under each
change: 25, 25, 33 and 49 calls. The test of each of them failed.

**Measured results, smoke program.** The program had 79 checks in the default build and 78 in the
variant with the hidden dictionary entry (`-p:AotSmokeHiddenDictionaryEntry=true`). It now has 84
and 83. Checks 80 to 84 are new:

| Check | Call | With postconditions on | With `DBC_POST=off` |
|---|---|---|---|
| 80 | two holders with a member `int[,]`: 1 by 2 and 2 by 1, the same elements in the same order | `PostconditionViolationException` | no exception |
| 81 | `T` = `int[]`, one element differs | `PostconditionViolationException` whose text contains "is compared as a whole" | no exception |
| 82 | `T` = `HiddenLeaf[]`, two arrays, each with its own `HiddenLeaf` of equal `Value` | Native AOT: `InvalidOperationException` whose text contains "cannot compare", `uContract.AotSmoke.HiddenLeaf` and `reached through 'actual[]'`. JIT: no exception | no exception |
| 83 | a holder of a `List<FrozenSet<string>>`, `Old<T>()`, then an element replaced through the indexer by a set of other content | `PostconditionViolationException` | no exception |
| 84 | a holder of a `FrozenSet<string>.Enumerator`, `Old<T>()`, nothing changed | no exception | no exception |

Native AOT. Environment: `win-x64`. Each publish ran `dotnet publish
tests\uContract.AotSmoke\uContract.AotSmoke.csproj -c Release -r win-x64 -warnaserror`, with
switches that make the build sequential and name the output folder, and with
`-p:AotSmokeHiddenDictionaryEntry=true` for the variant. Neither publish gave a diagnostic:

| Publish | With postconditions on | With `DBC_POST=off` |
|---|---|---|
| Default | 84 `PASS`, no `FAIL` | 84 `PASS`, no `FAIL` |
| Variant with the hidden dictionary entry | 83 `PASS`, no `FAIL` | 83 `PASS`, no `FAIL` |

The four runs were made at the final state of the library and of the tests. These are the ten lines
of the five checks in the default publish; the variant gave the same ten lines. The prefix `on:` or
`off:` names the run and is not output of the program. The program prints the first line of a
message. Every line is given in full, except the line of check 82 with postconditions on: it is
given up to the closing parenthesis of "(reached through …)", and the message continues as every
"cannot compare" message of this kind does:

```text
on:  PASS 80 Arrays of the same elements and different lengths per dimension expected=PostconditionViolationException got=PostconditionViolationException Postcondition violated: Fields were modified that are not marked as assignable:
on:  PASS 81 EnsureAssignable<int[]> arrays with one element differing expected=PostconditionViolationException got=PostconditionViolationException Postcondition violated: actual and expected are not equal. System.Int32[] is compared as a whole, so assignable patterns do not apply.
on:  PASS 82 EnsureAssignable<HiddenLeaf[]> elements with hidden members expected=InvalidOperationException got=InvalidOperationException EnsureAssignable cannot compare uContract.AotSmoke.HiddenLeaf (reached through 'actual[]')…
on:  PASS 83 List of frozen sets with one element replaced expected=PostconditionViolationException got=PostconditionViolationException Postcondition violated: Fields were modified that are not marked as assignable:
on:  PASS 84 Enumerator of a frozen set unchanged expected=no exception got=no exception
off: PASS 80 Arrays of the same elements and different lengths per dimension expected=no exception got=no exception
off: PASS 81 EnsureAssignable<int[]> arrays with one element differing expected=no exception got=no exception
off: PASS 82 EnsureAssignable<HiddenLeaf[]> elements with hidden members expected=no exception got=no exception
off: PASS 83 List of frozen sets with one element replaced expected=no exception got=no exception
off: PASS 84 Enumerator of a frozen set unchanged expected=no exception got=no exception
```

Two publishes of the default build were made earlier in the work, each with postconditions on and
off: after the change for an array as `T`, when the program had 82 checks, and after the change of
the namespace category. They gave 82 `PASS` and 84 `PASS`, with no `FAIL`.

Under the JIT the program was run in its `--report` mode at the final state, where it prints 84 and
83 lines. There check 82 reports no exception, because the members of `HiddenLeaf` are visible.

CI publishes the default build for `linux-x64` and runs the smoke program there. It had not run for
this change when this amendment was written.

**Measured, other results.**

- **The namespace of an array of function pointers.** Reading `Type.Namespace` throws
  `BadImageFormatException` for `delegate*<void>[]`, for `delegate*<void>[][]`, for
  `delegate*<void>[,]` and for `delegate* unmanaged<void>[]`. It does not throw for
  `List<delegate*<void>[]>` or for `Dictionary<string, delegate*<void>[]>`. On `82c6255`
  `SharedTypes.IsShared` threw that exception for `delegate*<void>[]`; it now returns `false`.
- **Nested arrays of function pointers.** The design measured a holder of a `delegate*<void>[]`
  only. During the work, a first form of the change classified a one-dimensional array of function
  pointers without reading its namespace. With that form `Old<T>()` and the comparison still threw
  `BadImageFormatException` for a holder of a `delegate*<void>[][]`, and `Old<T>()` threw it for a
  holder of a `List<delegate*<void>[]>`, whose storage is an array of such arrays. The
  classification now looks at the innermost element type. Unit tests hold both holders. For the
  array of arrays: the copy has other arrays and the same address, two holders of one function are
  equal, and two holders of different functions are a violation. For the list: the copy has another
  list, another array and the same address, an unchanged holder is equal to its copy, and a replaced
  function is a violation.
- **Other holders of function pointers.** Measured after the change for the arrays of pointers and
  before the two later changes, each with `Old<T>()` and then `EnsureAssignable<T>()`. Nothing
  changed, and no exception: a holder of a `Dictionary<string, delegate*<void>[]>`, a class with a
  function pointer field, a holder of an array of a struct with a field `delegate*<void>[]`, a
  holder of an array of a struct with a function pointer field, and a holder of a tuple with a
  `delegate*<void>[]`. A function replaced, and a violation: the first four of these. `Old<T>()` of
  a holder of a `HashSet<delegate*<void>[]>` does not throw. One `delegate*<void>[]` passed as
  `actual` and as `expected` gives no exception, with `T` inferred and with `T` = `object`; unit
  tests hold these two calls at the final state.
- **The rules of B3 and the arrays and value types.** On `82c6255`, 28 array types were classified:
  7 were shared, each through the namespace category alone, and 21 were not shared. Among them were
  arrays of types that other rules share (`Stream[]`, `Task[]`, `Delegate[]`, `Type[]`,
  `SafeHandle[]`, `StringComparer[]`, `Regex[]`). At the final state none of the 28 is shared. Of
  the value types of six assemblies of the runtime (`System.Private.CoreLib`,
  `System.Collections.Immutable`, `System.Net.Http`, `System.ComponentModel.Primitives`,
  `System.Net.Sockets`, `System.Text.RegularExpressions`), 14 were shared on `82c6255`, all in
  `System.Collections.Immutable`, the assembly that holds the namespace. At the final state none is.
- **The measurements that the limits cite** were made with throwaway tests at the final state of the
  library, or on a state of this work whose library code is the final one: the array keys (limit
  11), `ICollection` and the other interfaces as `T` (limit 8), the generic method (limit 6), the
  getter that throws (limit 16), the `byte[]` of 1,000,000 elements (limit 15), the enumerator
  member (limit 17), and `EnsureImmutableCollection<T>()` (limit 18, measured after the change for
  the arrays of pointers).

**Measured with the 2.0.0 package.** These results come from programs with the 2.0.0 package, on
.NET 8.0.16, that are not in the repository. The 2.0.0 cells of the tables above come from them.
In short:

- **`Old<T>()` threw** for a holder of a `FrozenSet<int>[]`, of a `List<FrozenSet<int>>`, of a
  `FrozenSet<int>.Enumerator` and of an array of such enumerators, and for a `FrozenSet<int>[]`
  itself (`InvalidOperationException`); for a holder of an `int[,]` of 2 by 2
  (`InvalidOperationException`) and of an array with lower bound 1 (`IndexOutOfRangeException`); and
  for a holder of an `int*[]` or of a `delegate*<void>[]` (`ArgumentException`).
- **An array as `T`.** The members of the array type were compared. A different element was a
  violation that names `SyncRoot` (rows 51, 53 to 58, 64 and 72). The pattern `SyncRoot`
  excused a different element, and the pattern `.*` different lengths (row 59). An invalid pattern
  and a `null` pattern were reported (row 60).
- **Rank, lengths and lower bounds.** In a member, 2.0.0 found these pairs equal: `int[,]` of 1 by 2
  and of 2 by 1, of 2 by 3 and of 3 by 2, and of 2 by 0 and of 0 by 2; `int[,,]` of 1 by 2 by 3 and
  of 1 by 3 by 2; in a member declared `Array`, an `int[]` and an `int[,]`, an `int[,]` and an
  `int[,,]`, and arrays of equal content with lower bounds 0 and 1, 1 and 2, and (0, 0) and (1, 1);
  an `int[,]` of 1 by 2 and of 2 by 1 in a member declared `object` and as elements of a list (rows
  83 to 85, 87 to 92, 95 and 96). As `T`, it found an `int[,]` of 1 by 2 equal to one of 2 by 1, and
  with `T` = `Array` an `int[]` equal to an array with lower bound 1 of the same content (rows 99
  and 100). 2.0.0 did report three differences of this kind: two `int[]` of lengths 2 and 1 as `T`
  (`Length`, `LongLength` and `SyncRoot`; row 52), two `FrozenSet<int>[]` of lengths 1 and 2 in a
  member (row 23), and an `int[]` against an `int[,]` with `T` = `Array` (`Rank`; row 100). So the
  sentence of the design that 2.0.0 "compared neither rank nor lengths per dimension nor lower
  bounds" is too wide; the CHANGELOG states the pairs that were measured.
- **Arrays of pointers and of function pointers.** Eight calls were measured. `Old<T>()` of a holder
  of an `int*[]` and of a holder of a `delegate*<void>[]` threw `ArgumentException`. The comparison
  threw `NotSupportedException` for two holders of an `int*[]` with the same address and with
  different addresses, for `T` = `int*[]` with the same address and with different addresses, for
  two holders of a `delegate*<void>[]` with the same function, and for `T` = `delegate*<void>[]`
  with the same function. None of the eight overflowed the stack. The next call of that program, two
  holders with pointer fields, ended the process with a stack overflow, which the accepted text
  records for pointer fields.

The table "The 2.0.0 package on the same checks" of the accepted text is unchanged. No run of the
smoke program in package mode is recorded for this change.

**Where the work differs from the design.**

- More unit tests were added than the cells of the design's tables need: tests were added while the
  work was done and reviewed. "Measured results, unit tests" gives the counts.
- The design has no row for a nested array of function pointers. "Measured, other results" above
  records the case and its tests.
- The design says of 2.0.0 that it "compared neither rank nor lengths per dimension nor lower
  bounds". "Measured with the 2.0.0 package" above gives what the measurements show.
- `SharedTypes.Classify` keeps its early answer for a type whose innermost element type is a
  function pointer, although the namespace category no longer reads the namespace of an array type.
  A comment at that place says what must be checked before the early answer is removed.

**Qualified in the accepted text.** Each statement is quoted by its opening words:

- B3: "in the `System.Collections.Frozen`" namespace. The category now matches a class that is
  declared in that namespace and is not an array type: the two types and every other such class. An
  array type is not matched, whatever its element type, and a value type that is declared there is
  not matched.
- B2: "A boxed value type (in an `object`, interface or `ValueType` field, or an `object[]` element) is"
  cloned before its fields are replaced. On `82c6255` that did not hold for a boxed value of a value
  type of the namespace: B3 came first, and the copy held the original's box (row 39). It holds now.
- B5: "A copied array has the same runtime type, rank and bounds, non-zero lower bounds" included. On
  `82c6255` an array of the namespace was not copied at all, because B3 came first (rows 1, 4, 16
  and 17). B5 holds for it now.
- "Order for a pair: same reference or a `null` side → R6 → pointer and `string` → R4 → R5 → R3 →".
  At the step R5, a pair of two values of a value type of the namespace and a pair of two arrays of
  the namespace are no longer decided. The first goes on to R3. The second goes on to the R1 memo,
  to the dimension test and to R7. The dimension test is a new step for two arrays, after the R1
  memo and before R7. "The rule as it is now" above gives the order.
- "**R3 — Value types.** Primitives and enums are compared with `Equals`." R3 now also decides a
  value of a value type of the namespace: an enum that is declared there with `Equals`, and a struct
  by its steps 1 to 4 (rows 30 to 49).
- R3: "A pointer field, read through reflection as a `System.Reflection.Pointer`, is compared by
  address with `Pointer.Equals`." That is the rule for a field, and it stands. An element of an
  array of pointers or of function pointers is not read as a `Pointer`: the two arrays are compared
  by the addresses in their memory.
- R5: "Values of B3 types are compared by reference". R5 follows B3. An array of the namespace and a
  value of a value type of the namespace are no longer values of B3 types, and they are not compared
  by reference. Two instances of a class of the caller in the namespace still are (rows 27 and 46).
- R6: "Exception: when both are `IEnumerable` and neither is a `string`, they are" compared by R7.
  For two arrays the dimension test comes before R7. Two arrays of different rank, such as an
  `int[]` and an `int[,]`, are of different runtime types, and they are unequal without an element
  being read (rows 87 and 88). Two arrays that pass the dimension test and have different element
  types are still compared by R7 (rows 74 and 93), and so are an array and a sequence that is not an
  array (row 94). An array of pointers or of function pointers that has an element is the exception
  to R7: it is not enumerated.
- R7: "Any other `IEnumerable` is compared element by element," in order. Two arrays are compared so
  only after the dimension test (rows 83 to 100). Two arrays of pointers or of function pointers
  that have elements are compared by address and are not enumerated (rows 102 to 117).
- "Unchanged: which members are compared (D9), the top level comparing the members of `typeof(T)`,"
  the assignable patterns, and the violation message format. Qualified once more: for an array as
  `T` the top level is a pair, and the patterns do not apply.
- R1: "The top-level pair is in progress for the whole call." That holds for a `T` that is compared
  by its members, and it is what hid a different element of an array as `T` on `82c6255` (change 3
  above). The two values of an array as `T` are not entered as the top-level pair. They are tracked
  as every other pair is while it is compared (rows 70 and 71). For two arrays behind a `T` that is
  compared by its members the statement still holds, with the consequence that limit 8 gives.
- "What held": "The namespace rule for frozen collections works (checks 49, 50)." The rule is
  narrowed, as the first entry of this list says. Check 49 still shows that `Old<T>()` shares a
  frozen set, and the #49 amendment says what check 50 shows. Check 83 shows under Native AOT that a
  list of frozen sets, preserved with `DynamicDependency`, is copied with its storage, and check 84
  that an unchanged enumerator member is equal to its copy.
- The three scope statements on #40. Constraints: "are still not compared (#40 items 1 and
  2-top-level)". D6: "#40 items 1, 2-top-level, 4, 6 and 7 stay open." Known Limitations:
  "**Scope.** #40 items 1, 2-top-level, 4, 6 and 7 remain." Item 2-top-level is taken for one more
  kind of `T`: an array type and `System.Array`. "What stays open" below gives what is left.
- Known Limitations: "**Identity-hashed keys** (D8)." The limitation now also holds for a key that
  is an array of the namespace, which was shared before (limit 11 above).
- Implementation Notes, "**Pointers.**": "`Pointer.Equals` compares the same addresses and needs no
  `unsafe` code in the library." That is said of a pointer field, and it stands. The library still
  has no `unsafe` block. But for two arrays of pointers or of function pointers it now reads the
  memory of the arrays as native integers, through `MemoryMarshal.GetArrayDataReference`,
  `MemoryMarshal.CreateReadOnlySpan` and `Unsafe.As`. The read is in bounds only when each element
  is one native integer, so the code tests the element type before it reads anything.

**Still true in the accepted text.** The statement is quoted by its opening words:

- Implementation Notes: "**The `HoldsReferences` cache.**" What the entry says of the copier
  stands: it does not visit an array whose element type cannot refer to an object. That includes
  an array of pointers or of function pointers (B2 does not count a pointer or a function pointer as
  reference-typed). The comparison of a large array of such a kind as `T` is another matter: see
  limit 15.

**Qualified in the #52 amendment.** Each statement is quoted by its opening words:

- The Status line of that amendment: "at the top level a `string`, a delegate type and a nullable
  value type are compared as one pair by R0–R8". An array as `T` is now compared as one pair too.
  Its Revision History row states the same and is qualified with it.
- The statements that name the three kinds of `T`. "When `T` is a `string`, a delegate type or a
  nullable value type, two values that are"; in step 4 of the rule, "when `T` is a `string`, a
  delegate type or a nullable value type, the two values are compared" and "for every other `T`, the
  members of `typeof(T)` are walked, as before"; "The declared type `T` decides whether the two
  values are compared as a whole. The test is:"; and "For these three kinds of `T` the top level
  applies the whole "Order for a pair" of this ADR." Each also holds for an array as `T`, and "every
  other `T`" no longer includes it. "The rule as it is now" above gives the steps, the fifth case of
  the test and the new row of the table.
- "It does not apply "same reference": two references to one object are still walked member by
  member." Not for an array as `T`: one instance on both sides is equal, and nothing is walked (rows
  63 and 104).
- "A base class as `T`. The members that it declares or inherits are compared" (private fields of
  its own base classes excepted). `System.Array` is a base class, and it is compared as a whole
  (rows 72 to 75 and 100).
- "**The declared type `T` decides.** For each of the three kinds, a value that is not `null` has a"
  runtime type of the same kind. The same holds for the new kind: a value that is not `null` and
  whose declared type is an array type or `System.Array` is an array. With `System.Array` as `T`,
  and with an array type that other array types can be assigned to, the two arrays can have
  different runtime types; then the dimension test, R6 and R7 decide (rows 74, 76 and 100).
- "`{path}` starts with the name of the underlying type, not with `typeof(T).Name`:". Below an array
  as `T` the path starts with `actual` (A8).
- The first rejected alternative, at "A shared value type (an enumerator in
  `System.Collections.Frozen`) would never be equal: each" side is boxed separately. The enumerators
  of the namespace are no longer shared, and none of the value types of the six assemblies that were
  examined is shared (see "Measured, other results"). The alternative stays rejected for its other
  reasons.
- Known limits of the behaviour: "**A nullable value type whose underlying type is a shared value
  type** (an enumerator in" the namespace) is never equal. Superseded. A value type of the namespace
  is no longer shared, and a nullable struct of the caller in the namespace as `T` gives no
  exception for equal content (row 41). A nullable enumerator as `T` was not measured.
- Limits: "A `string`, a delegate type and a nullable value type always have visible members, so
  an". Whether an array type as `T` always has visible members under Native AOT was not determined.
  That the check "no members visible" does not run for such a `T` was read from the code.
- Relation to #40 and #54: "**Left in #40**: the rest of item 2." "What stays open" below gives what
  is left in #40 now.

**Qualified in the #54 amendment.** Each statement is quoted by its opening words:

- The steps of a call: "5. When `T` is a `string`, a delegate type or a nullable value type, the two
  values are compared as" a whole, and "6. For every other `T`, the members of `typeof(T)` are
  walked." Step 5 also holds for an array as `T`, and step 6 no longer includes it.
- "So rule S and the comparison as a whole never both apply". That also holds for an array as `T`.
  Read from the code: an array type derives from `System.Array` alone, so it matches no listed type,
  no listed open generic type, not the type that is matched by full name, and neither
  `CriticalFinalizerObject` nor `ComObject`. `Type.IsCOMObject` was measured as false for
  `delegate*<void>[]`; it was not measured for an array of a COM type. Measured: one `int[]`, one
  `Stream[]`, one `FrozenSet<int>[]` and one `delegate*<void>[]`, each passed as `actual` and as
  `expected`, give no exception.
- "The shared value types (enumerators in `System.Collections.Frozen`) are" fixed. The enumerators
  are no longer shared. This change does not touch the rest of that entry.
- "**The fixed shared types.** With B3 as it is on `71b2e4e`, these are: `string`; `Delegate`,
  `Type`," and, later in that sentence, "the types in the namespace `System.Collections.Frozen`".
  They are now the classes that are declared in that namespace and are not array types.
- Limits: "A user type declared in the namespace `System.Collections.Frozen` is fixed too." A user
  class is. A struct or an enum of the caller that is declared there is not shared (rows 34, 35 and
  47).
- "That holds for a fixed shared type and for a type that is not shared." The sentence is about two
  references to one object, which are walked member by member. It does not hold for an array as
  `T`: one instance on both sides is equal, and it is not walked (row 63).
- What does not change: "The same instance of a fixed shared type, or of a type that is not shared."
  The entry says that the call behaves as on `71b2e4e`. For one array on both sides with an array
  as `T` the result is still "no exception" (row 63). But no member is read any more, and the
  elements of the pattern array are not examined, so an invalid pattern is not reported in such a
  call. That was read from the code; row 60 measured it for two different instances.
- Decision S7: "Unchanged. It shares the same types as before and throws nothing of its own while
  copying." and decision S8: "Unchanged. B3 and R5 stand." Both are true of #54. With this change
  the namespace category of B3 is narrowed, and `SharedTypes.IsShared` with it. `Old<T>()` still
  throws nothing of its own while it copies.
- What stays open, on #40: "Left in #40 of item 2: two different instances of a shared type as `T`;
  the same". "What stays open" below gives what is left.

**Qualified in the #49 amendment.** Each statement is quoted by its opening words:

- Its first paragraph: "`Old<T>()`, B3 and `SharedTypes.IsShared` do not" change. Under "Not
  changed": "`Old<T>()`: every result. It still shares a value of the two types, as it shares every
  type" that is declared in the namespace. Decision F1: "Unchanged. It still shares a value of the
  two types. B3 and `SharedTypes.IsShared` do not change." All three are true of #49. With this
  change B3 and `SharedTypes.IsShared` change: `Old<T>()` no longer shares every type that is
  declared in the namespace. It still shares a value of the two types.
- "**Why the two types and not the namespace.** B3 shares every type whose namespace is" the
  namespace. B3 now shares the classes of that namespace that are not array types. In the same
  part: "That `Old<T>()` shares an array of the two types is a defect" of its own. This change
  repairs it. The reason that the part gives for R5, that no getter or factory of the instance runs,
  still holds for a class of the caller in the namespace.
- Decision F5: "Unchanged: a class of the caller declared in `System.Collections.Frozen` (also one
  that implements `IEnumerable`), a value type declared there, and an array whose element type is
  declared there." Superseded for the value type and for the array. It stands for the class of the
  caller (rows 27 and 46). The entry "Any other type that B3 shares because of its namespace: see F5
  below." under "Not changed" is qualified with it.
- "The rule as it is now": "5. When `T` is a `string`, a delegate type, a nullable value type or one
  of the two types, the two" values are compared as a whole. Step 5 now also names an array type and
  `System.Array`. "The test of the #52 amendment on the declared `T` gains a fourth case:". It now
  has a fifth.
- "In the code": "because it now also serves the two types". The helper also serves an array as `T`.
- The table "A pair that a member holds", row 21: "two `FrozenSet<int>[]`, different arrays, the
  same frozen set as element (F5)". Its "After" is superseded: no exception (row 20 above). Its
  2.0.0 cell, "not measured", can be filled: no exception.
- The table "`actual` and `expected` themselves", row 45: "`T` = `FrozenSet<int>[]`, two arrays with
  frozen sets of equal content (F5)". Its "After" is superseded: no exception for equal content, and
  message C for different content (row 58 above). Its 2.0.0 cell, "not measured", can be filled: no
  exception.
- "Native AOT, the members of an element are not visible": "The path starts with `typeof(T).Name`;
  for a nullable value type it starts with the name of the underlying type." Below an array as `T`
  the path starts with `actual` (A8).
- Limit 5: "**The value types of the namespace.** A value type declared in
  `System.Collections.Frozen` is not" one of the two types. That stays true, and the rest of the
  limit is superseded: such a value type is not shared, and R3 compares it (rows 30 to 49).
- Not tested: "Row 45 stays with #57." and "A value type of the namespace that a member holds (F5,
  limit 5)." Unit tests of this change hold both.
- Qualified in the #52 amendment: "Every other type declared in the namespace
  `System.Collections.Frozen` is still walked as" `T`, with an array of the two types as its
  example. An array of the two types as `T` is not walked any more. A class of the caller in the
  namespace as `T` is still walked (read from the code), and so is a value type of the namespace
  without `?` (row 43).
- Qualified in the #54 amendment: "`SharedTypes.IsShared` and B3 are unchanged by this" change too.
  They change with #57.
- Measured, other results: "`Old<T>()` shares an array of the two types because `Type.Namespace` of
  an array type is the" namespace of its element type. `Old<T>()` no longer shares such an array.
  What the sentence says of `Type.Namespace` stands.
- What stays open, on #40: "Left in #40 of item 2, for a shared type as `T` other than a `string`, a
  delegate" type and the two types. "What stays open" below gives what is left.
- What stays open, on #57: "`Old<T>()` shares an array of the two types. A unit test records the
  result of today for two such" arrays. #57 is closed by this change. That unit test is rewritten to
  "no exception" (see "Measured results, unit tests"), and the case of row 45 now has unit tests.

**Still true in the #49 amendment.** The statement is quoted by its opening words:

- Terms: "An array of them is not one of them, and neither is any other type declared in the"
  namespace. That stands, and `SharedTypes.IsFrozenSetOrDictionary` is still the test for the two
  types. A unit test holds that it is false for an array of frozen sets.

**Superseded in the #49 amendment.** The statement is quoted by its opening words:

- "It now has 79" and 78. The program has 84 and 83 checks. The Status line of that amendment ("the
  smoke program has 79 checks") and its Revision History row ("79 smoke checks") state the same
  count and are superseded with it.

Its result tables, its unit-test figures and its other statements are the dated record of #49 and
stay.

**Not covered by a measurement.**

- The 2.0.0 cells that are marked "not measured".
- Native AOT for an array of pointers or of function pointers: no smoke check covers one (A12).
- Native AOT for `System.Array` as `T`: checks 81 and 82 use `int[]` and `HiddenLeaf[]`.
- Native AOT for a type that has members and loses them to the trimmer, beyond checks 82 and 84. The
  unit tests state "dynamic code off" only for a type that reflects with no member at all.
- Whether an array type as `T` can have no visible members under Native AOT. That the check "no
  members visible" does not run for it was read from the code.
- The `linux-x64` publish, which CI runs.
- An array of a COM type. No rule of B3 but the namespace category matched an array type that was
  measured, and the suite can test a COM import on Windows only.
- A struct of the caller whose full name is `System.Threading.Lock`. The rule that shares that name
  is not changed, and it was not measured for a value type.
- An array of pointers or of function pointers with more than `int.MaxValue` elements in all, which
  a rank above 1 allows. The code reads `Array.Length` for the number of addresses.
- What `Old<T>()` copies through a member that is an enumerator of a frozen set, beyond the one
  case of limit 17.
- What the copy of a `List<FrozenSet<T>>` shares under Native AOT when the list type is not
  preserved (limit 10).

**Rejected alternatives.**

- **A separate issue for each of the four further defects.** The four were found on the paths that
  the first one uses, and six rows of the tables need two of the changes together (A1). The
  prototypes of the design show what a change made alone gives. With an array as `T` compared as a
  whole and the namespace category unchanged, `T` = `FrozenSet<int>[]` with two sets of equal
  content gave message C, where `82c6255` gave a violation that names `SyncRoot` and the final state
  gives no exception. The same state gave message C for one set in both arrays with the pattern
  `SyncRoot`, where `82c6255` and the final state give no exception.
- **Exclude an array type and a value type ahead of every rule of B3.** No test can tell that from
  the exclusion in the namespace category for the types that were measured (A2). It would also
  decide the cases that were not measured: an array of a COM type, and a struct that the rule by
  full name matches (see "Not covered by a measurement").
- **The type name as the start of the path below an array as `T`.** The name of an array type holds
  brackets of its own, so the path would show one level of elements too many (A8).
- **Compare two arrays of pointers by boxing each element.** The runtime does not give an element
  of such an array as an object: on `82c6255` the comparison threw `NotSupportedException` where it
  enumerated one (rows 102, 103, 107, 109, 110, 112 and 114). A comparison of boxes would also make
  an `int*[] {px}` equal to an `object[]` that holds `Pointer.Box(px)`, which A9 keeps different
  (row 110).

**What stays open.**

- **#57** is closed by this change, with the limits above.
- **[#40](https://github.com/cwouyang/uContract.NET/issues/40)**: this change takes one more part of
  the rest of item 2: an array type and `System.Array` as `T`. Left in #40 of item 2: `T` =
  `object`, an interface, or a base class that is not compared as a whole, for two different
  instances, also when the two values are arrays (row 77; limit 8); a collection class as `T` (row
  78); a value type of the namespace as `T` whose getter throws (limit 5); and, for a shared type as
  `T` other than a `string`, a delegate type and the two types, two different instances and the same
  instance of a fixed shared type. The design asks for a comment on #40 after the merge that says
  what this change took and what stays.
- **`Contract.EnsureImmutableCollection<T>()`** throws `BadImageFormatException` for an array of
  function pointers (limit 18). No issue was filed for it when this amendment was written.
- **#46, #48 and #50** are outside this change.

**Why an amendment and not a new ADR.** B1 to B8 and R0–R8 stand. The change narrows one category of
B3, adds one test for two arrays before R7, adds one kind of `T` to the existing rule "compared as a
whole", and gives an element of an array of pointers the rule that a pointer field already has. It
does not do what the #52 amendment reserved for a new ADR, which is to change what every top-level
comparison does. `docs/adr/README.md` lists breaking changes under "Requires ADR". The amended ADRs
(ADR-0022, ADR-0012, ADR-0021 and ADR-0007) are that record, as for #52, #54 and #49.

**Unchanged.** Every other decision of this ADR.

### Amendment (2026-10-12): EnsureImmutableCollection no longer reads the namespace; the measured results of #61

This amendment follows the #57 amendment above.
[Issue #61](https://github.com/cwouyang/uContract.NET/issues/61) gives
`Contract.EnsureImmutableCollection<T>()` ("the method" below) a new rule. The amendment of
2026-10-12 of [ADR-0012](0012-dotnet-improvements-over-java.md) records the rule, its decisions, the
rejected alternatives and the limits. That of
[ADR-0021](0021-postconditions-under-trimming-and-aot.md) records the trimming suppression, the
seven smoke checks, and what was measured under Native AOT and in a trimmed publish without Native
AOT. This amendment records four things: the statements of this ADR that no longer hold as written,
one limit that the rule brings next to `Old<T>()` and `EnsureAssignable<T>()`, the decisions on cost
and on checks, and the other measured results of the change. `Old<T>()`, `EnsureAssignable<T>()`, B1
to B8 and R0–R8 are not changed: in `SharedTypes.cs` the change touches comments only.

**Terms.** This amendment uses these terms:

- **A second copy**: a second copy of the assembly `System.Collections.Immutable`, which declares
  the immutable and the frozen collections, loaded into another `AssemblyLoadContext`. A type of the
  copy has the full name of the framework's type and is not that type (measured for
  `ImmutableList<int>` and for the type of a frozen set).
- **Before the work**: the library on `master` as the work for #61 found it. **The final code**: the
  library and the smoke program as this change leaves them. The runs on the inputs, the mutation
  check and the allocation runs were made on states of this work whose library code is the final
  one: what came after them changed comments, added one smoke check and changed documents. The
  publishes that ADR-0021 records were made with that smoke check in place. **2.0.0**: the 2.0.0
  package of nuget.org; the SHA512 of the restored package was compared with the one that nuget.org
  gives, and they are equal.
- **A throwaway program**: a program or a test that is not in the repository. Its result files are
  not in the repository either.
- **Accepted** and **violation**: the method returns its argument, or throws
  `PostconditionViolationException`.

Where the result file of a run on .NET 8 names the runtime, it is 8.0.16 under the JIT and 8.0.28
under Native AOT (the allocation runs).

**Superseded in the #57 amendment.** Each statement is quoted by its opening words:

- Limit 18: "**Another method still reads the namespace.** `Contract.EnsureImmutableCollection<T>()`
  reads `Type.Namespace` of the runtime type of its argument, and so throws". The method reads
  neither `Type.Namespace` nor `Type.Name` now. It tests `Type.IsArray` first, and an array of
  function pointers is a violation, as other arrays are (unit tests for `delegate*<void>[]`,
  `delegate*<void>[][]` and `delegate*<void>[,]`; smoke check 86).
- Measured, other results: "and `EnsureImmutableCollection<T>()` (limit 18, measured after the change
  for the arrays of pointers)". That is the record of its date. Measured again for #61, with
  throwaway programs: before the work and in 2.0.0 the three array types above ended in
  `BadImageFormatException`.
- What stays open: "**`Contract.EnsureImmutableCollection<T>()`** throws `BadImageFormatException`
  for an array of function pointers (limit 18). No issue was filed for it when this amendment was
  written." Issue #61 was filed for it on 2026-10-11, and this change is the change for that issue.
- "It now has 84" and 83. The smoke program has 91 checks in the default build and 90 in the variant
  with the hidden dictionary entry. The Status line of that amendment ("the smoke program has 84
  checks") and its Revision History row ("84 smoke checks") state the same count and are superseded
  with it, and so is "The program has 84 and 83 checks." under "Superseded in the #49 amendment".
- "Under the JIT the program was run in its `--report` mode at the final state, where it prints 84"
  and 83 lines. It prints 91 and 90 lines at the final code.

Its result tables and its other statements are the dated record of #57 and stay.

**Qualified in the #57 amendment.** Each statement is quoted by its opening words:

- A12: "No smoke check covers an array of pointers or of function pointers." Under "Not covered by a
  measurement": "Native AOT for an array of pointers or of function pointers: no smoke check covers
  one (A12)." Check 86 passes a `delegate*<void>[]` to the method, and ADR-0021 gives its result
  under Native AOT. No smoke check passes such an array to `Old<T>()` or to `EnsureAssignable<T>()`,
  and none uses an array of pointers; for those the two statements stand.
- Change 5: "the classification of B3 read `Type.Namespace` of the array type, and the runtime throws
  when the innermost element type of an array type is a function pointer". Measured, other results:
  "**The namespace of an array of function pointers.** Reading `Type.Namespace` throws
  `BadImageFormatException` for". Both were measured under the JIT. A throwaway program for .NET 8
  gave these answers under the JIT and published with Native AOT:

  | Read | Array type | JIT | Native AOT |
  |---|---|---|---|
  | `Type.Namespace` | `delegate*<void>[]`, `delegate*<void>[][]`, `delegate*<void>[,]` | throws `BadImageFormatException`, each | `null`, each |
  | `Type.FullName` | the same three | `System.Void()[]`, `System.Void()[][]`, `System.Void()[,]` | `null`, each |
  | `Type.GetInterfaces()` | `delegate*<void>[]` | throws `ArgumentException` | 6 interfaces, none generic |
  | `Type.GetInterfaces()` | `delegate*<void>[][]`; `delegate*<void>[,]` | 11 interfaces, 5 of them generic; 6, none generic | the same counts |

  The method reads none of the three from an array type, and smoke check 86 gives the same result
  under the JIT and under Native AOT: a violation.

**Qualified in the #49 amendment.** The statement is quoted by its opening words:

- In the code: "`SharedTypes.IsFrozenSetOrDictionary(Type)` is the test for the two types." It still
  is, for the comparison of `EnsureAssignable<T>()`, and it compares types. The method has a test of
  its own, `Contract._IsFrozenSetOrDictionary`, which compares types and then full names. A comment
  there says that the two are kept apart on purpose. The #57 amendment repeats the statement under
  "Still true in the #49 amendment" ("`SharedTypes.IsFrozenSetOrDictionary` is still the test for
  the two" types), and the same holds for that place.

**Qualified in the #52 amendment.** The statement is quoted by its opening words:

- Check 64: "It is the only test of that method with postconditions off." Check 64 gave its two
  results again at the final code. It is not the one check that calls the method with
  `DBC_POST=off`. Checks 17 and 18 do so, and ADR-0021 records that for 2.0.0 already; checks 85 to
  91 do so now. Each of these nine checks expects no exception there, and each gave none in the
  Native AOT publishes of this change.

**A frozen collection of a second copy: three criteria.** This is a limit. The method, `Old<T>()` and
the comparison of `EnsureAssignable<T>()` each test for a frozen collection in another way. Measured
with a throwaway program, under the JIT on .NET 8, with a `FrozenSet<int>` of a second copy:

| Where | Criterion | Result for a set of a second copy |
|---|---|---|
| The method | The full name of the generic definition of the type or of one of its base classes | Accepted. |
| `Old<T>()` | A class that is declared in the namespace `System.Collections.Frozen` (the namespace category of B3, as the #57 amendment left it) | A holder of such a set is copied, and the copy holds the original's set, as for a set of the framework. |
| The comparison of `EnsureAssignable<T>()` | The two types of the framework, compared as types (`SharedTypes.IsFrozenSetOrDictionary`) | Two holders with two such sets of equal elements: a violation, where two sets of the framework of equal elements give no exception. Two such sets of different elements: a violation. One such set in both holders: no exception. |

The three results of the last row are those of a comparison by reference. The way out is the one for
order and comparer: list the member as assignable. Measured: with the member listed, two holders
with two such sets give no exception, whether the elements are equal or different. Also measured and
not stated in the user documents: `EnsureAssignable<object>` with two such sets of equal elements as
`actual` and `expected` gives no exception. The maintainer decided on 2026-10-12 to state the limit
in the API reference and here, and to open a follow-up issue on the comparison; the issue was not
filed when this amendment was written.

**Decisions.** The maintainer made them on 2026-10-12, on the measurements below:

- **The cost of a call is accepted.** The numbers are recorded below and in the CHANGELOG.
- **One smoke check for the comparison by full name.** It is check 91, the seventh of the checks
  that this change adds for the method; ADR-0021 lists the seven. No check that bounds the allocation is added.
- **Follow-up issues.** These are to be opened, and none was filed when this amendment was written:
  a trimmed publish in CI; a standing check that a call of the method does not run the type
  initialiser of `SharedTypes`; a standing check for what the mutation check below leaves open; and
  the comparison of `EnsureAssignable<T>()` for a frozen collection of a second copy.

**Measured results, unit tests.** JIT, whole suite. The suite had 994 tests before the work and has
1103 at the final code; the Debug and the Release configuration gave the same total each time, with
0 failed. The 109 added tests are in five new test files of `tests/uContract.Tests`. These are the
tests of the repository that hold the rule:

| File | Tests | Inputs that its tests hold, in short |
|---|---|---|
| `EnsureImmutableCollectionArrayTests.cs` | 13 | Arrays, each a violation: of immutable collections, of read-only wrappers, of `ReadOnlyMemory<int>`, of frozen sets, of classes of the test assembly, and three array types of function pointers. |
| `EnsureImmutableCollectionFrozenTests.cs` | 21 | Frozen sets and frozen dictionaries, and the `Keys`, the `Values` and the `Items` of them: accepted. Their enumerators, a class and a struct that the test assembly declares in the namespace `System.Collections.Frozen`, and a class named `FrozenSet<T>` that derives from no frozen type: violations. |
| `EnsureImmutableCollectionInterfaceTests.cs` | 27 | Classes and a struct of the test assembly that implement one of the five interfaces, also through a base class and explicitly: accepted. Builders and enumerators of immutable collections, the `Keys` and the `Values` of an `ImmutableDictionary<int, int>`, a class with `IsReadOnly` true, and a class that implements an interface of its own named `IImmutableStack<T>`: violations. |
| `EnsureImmutableCollectionWrapperTests.cs` | 26 | The read-only wrappers, classes derived from them, and a class of the test assembly with the full name of `ReadOnlySet<T>`: accepted. Types whose name starts with `Immutable` or `ReadOnly` and that meet no condition, what `ArrayList.ReadOnly(...)` returns, and the `Keys` and the `Values` of a `ReadOnlyDictionary<int, int>`: violations. |
| `EnsureImmutableCollectionFullNameTests.cs` | 22 | A second copy: seven collections accepted, a builder and an array violations. A dynamically built assembly: six types accepted by a full name; four types whose full name is none of the ten names, violations; a full name that differs by letter case only, a violation; a class with the full name of an interface and a class that implements an interface with the full name of a frozen type, violations. |

**Measured results, the inputs.** With throwaway tests, under the JIT on .NET 8. The throwaway runs
have more inputs than the 109 tests, and they also recorded the answers before the work; the tests
above hold inputs of each of the three sets in the repository. The tests also hold inputs that are
in no throwaway run, for example the full name that differs by letter case only.

| Inputs | Result |
|---|---|
| 133 runtime types, before the work and at the final code | Before the work: 62 accepted, 68 violations, 3 ended in `BadImageFormatException`. At the final code: 62 accepted, 71 violations. 55 answers changed: 26 from accepted to violation, 26 from violation to accepted, and the 3 exceptions to violation. Each of the 133 answers at the final code is the one that the design had derived from the rule. |
| 19 types of a second copy and of a dynamically built assembly | At the final code: 13 accepted, 6 violations, each as the design expected. While the code compared types, 12 of the 13 were violations. |
| 3 more forms from a dynamically built assembly | A generic class with the full name of `IImmutableStack<T>`: a violation. A class that implements a generic interface with the full name of `FrozenSet<T>`: a violation. A class with the name ``System.Collections.ObjectModel.ReadOnlyCollection`1`` and two type parameters: accepted. |

The amendment of ADR-0012 names the inputs that its rule, its rejected alternatives and its limits
rest on.

**Measured with the 2.0.0 package.** A throwaway program ran 14 inputs and one value of a second
copy with the 2.0.0 package and with the final code, on .NET 8.0.16:

| Input | 2.0.0 | Final code |
|---|---|---|
| `ImmutableList<int>[]` | accepted | violation |
| `delegate*<void>[]` | `BadImageFormatException` | violation |
| `FrozenSet<int>`; `FrozenDictionary<int, int>` | violation, each | accepted, each |
| `ImmutableList<int>.Builder`; a class named `ImmutableBag` that holds a `List<int>`; `ReadOnlyMemory<int>`; the result of `ArrayList.ReadOnly(...)` for an `ArrayList` | accepted, each | violation, each |
| a class that implements `IImmutableStack<int>`; a class derived from `ReadOnlyCollection<int>` whose name does not start with `ReadOnly` | violation, each | accepted, each |
| `ImmutableList<int>`; `ReadOnlyCollection<int>`; a class named `ReadOnlyOrders` derived from `ReadOnlyCollection<int>`; `ImmutableList<int>.Empty` of a second copy | accepted, each | accepted, each |
| `List<int>` | violation | violation |

A second throwaway program ran 25 more inputs with the same restored 2.0.0 package. For each of the
25, 2.0.0 gave the answer that the library gave before the work. What the CHANGELOG says of 2.0.0
for this method comes from these two programs.

**Measured results, allocation.** Bytes allocated per call, measured with throwaway programs. "Types
only" is the first implementation of the rule, which compared types and compared a name for
`ReadOnlySet<T>` only. "Names at each comparison" is the first implementation of the comparison by
full name, which read two names at each comparison. "—" means that no number exists.

| Call | 2.0.0, JIT | Final code, JIT | Types only, Native AOT | Names at each comparison, Native AOT | Final code, Native AOT |
|---|---|---|---|---|---|
| `ImmutableList<int>`, accepted | 192 | 312 | 1544 | 7064 | 1544 |
| `ReadOnlyCollection<int>`, accepted | 192 | 192 | 1024 | 10512 | 192 |
| `ImmutableDictionary<int, int>`, accepted | 192 | 304 | 1528 | 7928 | 1528 |
| `FrozenSet<int>`, accepted | — | 192 | 1008 | 11120 | 192 |
| `ImmutableArray<int>`, accepted | — | 368 | 1560 | 11744 | 1560 |
| a class of the program with the full name of `ReadOnlySet<T>`, accepted by full name | — | 232 | 824 | — | 984 |
| `List<int>`, a violation, with the exception | — | 881 | 1800 | — | 3552 |
| a class of the program that implements one generic interface of the program, a violation, with the exception | — | 768 | 1296 | — | 1792 |

- The first implementation of the comparison by full name allocated 7064 to 11744 bytes for the five
  accepted rows under Native AOT. That is why the final code compares types first and compares full
  names only without a match, with names that it reads once. A first correction of that kind still
  gave 1024 bytes for the `ReadOnlyCollection<int>` and 1608 for the row that is accepted by full
  name; with the two walks of the base classes put before the read of the interfaces they are 192
  and 984.
- At the final code under Native AOT, a call that ends in a violation allocates more than with types
  only: 3552 against 1800 bytes, and 1792 against 1296.
- No number exists for 2.0.0 under Native AOT.
- These are measurements of one program each, and not bounds that the library keeps.

**Measured results, mutation check.** At the final code the library was changed by hand in eight
ways. No change was committed, and the 124 unit tests that the work selected for the method ran each
time:

| Change | Tests that fail |
|---|---|
| The test for an array is taken out | 1: the test for an array of function pointers, which ends in `ArgumentException` |
| The condition on the five interfaces is taken out | 29 |
| The condition on the two frozen types is taken out | 16 |
| The condition on the wrappers is taken out | 16 |
| The second call, which compares full names, is taken out | 15 |
| Simple names are compared in place of full names (the constant for `ReadOnlySet<T>` stays a full name) | 7 |
| The names are compared without regard to letter case | 1 |
| The first call, which compares types, is taken out | 0 |

For the first four, the work had listed before the runs which tests must fail, and those failed. The
last row shows what no test holds: the first call changed no answer of the 124 tests, and it is
there to spare the reads of `Type.FullName`. No test and no smoke check holds it or the order of the
walks; the allocation numbers above are the evidence for both.

**Measured, other results.** Each with a throwaway program:

- **The type initialiser of `SharedTypes`.** Under the JIT on .NET 8, with one kind of call in each
  process: a process that only calls the method loaded none of `System.ComponentModel.Primitives`,
  `System.Net.Http`, `System.Net.Primitives`, `System.Net.Sockets` and
  `System.Text.RegularExpressions`; a process that only calls `Old<T>()`, and one that only calls
  `EnsureAssignable<T>()`, loaded the five. The lists of B3 in `SharedTypes` name types of such
  assemblies, so the result is taken to show that a call of the method does not run that
  initialiser. No standing check holds it.
- **.NET 10.** The library, built for .NET 8, in a program on runtime 10.0.9, under the JIT and
  published with Native AOT, with the same answers in both: the framework's `ReadOnlySet<int>`, the
  result of `HashSet<int>.AsReadOnly()`, a class derived from `ReadOnlySet<int>` and a
  `ReadOnlyCollection<int>` are accepted; a `ReadOnlySet<int>` that is declared in another namespace
  and a `List<int>` are violations.
- **The comparison by full name under Native AOT on .NET 8.** In the program of the allocation runs,
  a class of the program with the full name of `ReadOnlySet<T>` and a class derived from it are
  accepted; a `List<int>`, a class that implements only a generic interface of the program, and an
  `ImmutableList<int>.Builder` are violations.

**Not covered by a measurement.**

- 2.0.0 under Native AOT, and the method on .NET 9.
- A second copy under Native AOT, and a second copy of another version of the assembly.
- The `linux-x64` publish, which CI runs. It had not run for this change when this amendment was
  written.
- What the last row of the mutation check leaves open.

**Why an amendment and not a new ADR.** B1 to B8 and R0–R8 stand, and this change does not touch
`Old<T>()` or `EnsureAssignable<T>()`. It is recorded here because limit 18 stood here, because the
measured results of the changes to the postcondition helpers are kept here, and because the rule
brings a limit next to B3 and R7. The rule itself is in ADR-0012.

**Unchanged.** The decisions of this ADR.

### Amendment (2026-10-24): Two dictionaries of different counts are different before any entry is read (#50)

This amendment follows the #61 amendment above.
[Issue #50](https://github.com/cwouyang/uContract.NET/issues/50) found, in the reviews of #45, that
R7 compared the lengths of two sequences before their elements but walked the entries of two
dictionaries without comparing their counts first.

**The change.** R7 has a first step for two `IDictionary` values: when their `Count` differs, the
pair is unequal, and neither dictionary is enumerated, so no key and no value of either is read.
Two dictionaries of equal `Count` are compared entry by entry, in enumeration order, as before.

**Where it applies.** Wherever R7 compares two dictionaries: a pair that a member or element holds,
of one runtime type or of two (R6 leads two of different runtime types to R7), and two
`FrozenDictionary<TKey, TValue>` values as `T`, which are compared as a whole. These do not change:

- a `T` such as `Dictionary<TKey, TValue>`, which is compared by its members (its `Count` property
  is one of them);
- two values of one value type that implements `IDictionary`, which are compared with `Equals` and
  then by their fields (a boxed value of such a type and an `IDictionary` of another runtime type
  are led to R7 by R6, and the count step applies to them; against a sequence of another runtime
  type that is not an `IDictionary`, such as a `List<T>`, R7 compares elements and the count step
  does not apply);
- every sequence that is not an `IDictionary`.

**Why.** Under Native AOT (D13) a key or a value whose class has no visible members, and whose
`Equals` says unequal, ends the call with the "cannot compare" message of R8 when the walk reaches
it. A dictionary that only grew or shrank was therefore reported as "cannot compare" when such an
entry came before the end of the shorter dictionary, not as a violation of the member that holds it.
Under the JIT such a pair was already unequal, unless reading an entry threw (read from the code);
what changes there is that no entry is read.

**Superseded.** Each statement is quoted by its opening words:

- R7: "An `IDictionary` is compared entry by entry, in enumeration order". That now holds for two
  dictionaries of equal `Count`; two of different counts are unequal without a walk.
- In the #49 amendment: "a pair of two `FrozenDictionary` values of one runtime type that a member
  or element holds now goes through the dictionary comparison of R7, so #50 will cover it." It does:
  the count step applies to that pair.
- In the #61 amendment: "The smoke program has 91 checks in the default build and 90 in the variant"
  with the hidden dictionary entry. It has 92 and 91. The Status line of that amendment ("the smoke
  program has 91 checks") and its Revision History row ("91 smoke checks") state the same count and
  are superseded with it.

**Checks.**

- Unit tests, in `tests/uContract.Tests/EnsureAssignableDictionaryCountTests.cs`: two dictionaries
  of different counts that a member holds, with a test double of `IDictionary` that counts every
  read of its entries (no read); a `Dictionary<,>` and a `SortedDictionary<,>` of different counts,
  and two `FrozenDictionary<,>` values of different counts, both under
  `TestRuntime.WithoutDynamicCode` with keys or values of a type without members, so that reading
  an entry would end in "cannot compare". Each expects `PostconditionViolationException`.
- Smoke check 92, "Dictionaries of different counts with hidden values": a member of type
  `Dictionary<string, HiddenLeaf>`, two separate graphs, two entries on one side and one on the
  other; the common key holds two `HiddenLeaf` instances of equal `Value`. It expects a violation
  with postconditions on and no exception with `DBC_POST=off`. It is outside the
  `HIDDEN_DICTIONARY_ENTRY` condition, so both builds hold it.

**Measured results.** On the final code, the unit tests: 1,106, all passed. The smoke program,
published with Native AOT for `win-x64` with `-warnaserror` (no diagnostic in either publish log):

| Build | Postconditions on | `DBC_POST=off` |
|---|---|---|
| Default | 92 `PASS`, no `FAIL` | 92 `PASS`, no `FAIL` |
| Variant with the hidden dictionary entry | 91 `PASS`, no `FAIL` | 91 `PASS`, no `FAIL` |

With the count step removed from the library and the smoke program unchanged, the default build gave
91 `PASS` and one `FAIL` with postconditions on: check 92 got `InvalidOperationException`
"EnsureAssignable cannot compare uContract.AotSmoke.HiddenLeaf (reached through
'LeafDictionaryHolder.Leaves[]')". The unit tests above failed the same way under the JIT: the test
double was enumerated, and the two others got `InvalidOperationException`.

**Not covered by a measurement.**

- The `linux-x64` publish, which CI runs. It had not run for this change when this amendment was
  written.
- 2.0.0, and the cost of the count step (one `Count` read on each side).

**Unchanged.** The decisions of this ADR, B1 to B8, and R0 to R6 and R8.

### Amendment (2026-10-24): The measured limits of base-class private members, of a Stream as T and of a class derived from a fixed shared type (#69)

This amendment follows the #50 amendment above.
[Issue #62](https://github.com/cwouyang/uContract.NET/issues/62) asked, in items 1 to 3, whether
three limits hold as the issue stated them. They were measured on master `40692cc`, on .NET 8, with
one scenario source run under the JIT (in an xunit test, Debug) and under Native AOT (in a program
published for `win-x64`, Release). The results are in
[a comment on #62](https://github.com/cwouyang/uContract.NET/issues/62#issuecomment-6381014514);
the measuring code was not committed.
[Issue #69](https://github.com/cwouyang/uContract.NET/issues/69) changes the documentation from
them. The library does not change. The results are the same in both runtimes unless a line says
otherwise.

**Private members declared on a base class.** With `T` = a derived class, the pair
`var old = Contract.Old(() => this);`, a change of one member of the base class, and
`Contract.EnsureAssignable(this, old);`:

| Member of the base class that changed | Result |
|---|---|
| a private field | no exception |
| a private auto-property | no exception |
| a protected auto-property | no exception |
| a protected field | violation: "- _prot" |
| an internal field | violation: "- _internal" |
| a public field | violation: "- PubField" |
| a public auto-property with a private setter | violation: "- PubAutoPrivateSet" |

Controls: a change of a private field declared on the derived class gave a violation that names it.
The change of the private field of the base class, with `T` = the base class, gave a violation that
names it ("- _secret"). Read through the base class, that field held 0 in `old` and 1 in the
original: the copy is complete, and the gap is in the comparison. Cause, read from the code:
`MemberComparison.GetOrCacheMetadata` lists `GetProperties(Public | Instance)` and
`GetFields(Public | NonPublic | Instance)` of the compared type only. Reflection does not return
there the private fields of a base class, and the backing field of an auto-property is private.
`DeepCopier` walks `BaseType`. This is item 1 of #40. The XML documentation of
`EnsureAssignable<T>()` already stated it ("Private fields declared on a base class are not
compared"); the user documents did not.

**A `Stream` as `T`.** `EnsureAssignable<Stream>` with two different `MemoryStream`s threw
`TargetInvocationException`, with the inner `InvalidOperationException` "Timeouts are not supported
on this stream." from `Stream.get_ReadTimeout`, for equal content, for different content and for
different lengths. `T` = `MemoryStream` threw the same. As `T` = `Stream`:

| Two streams | Result |
|---|---|
| of a class of the measuring program that keeps the timeout getters of `Stream` | `TargetInvocationException`, inner from `ReadTimeout` |
| of a class of the measuring program whose `Length` throws | `TargetInvocationException`, inner `NotSupportedException` from `Length` |
| two `GZipStream`s | `TargetInvocationException`, inner `NotSupportedException` "This operation is not supported."; from `GZipStream.get_Length` under the JIT, getter not identified under Native AOT |
| two `MemoryStream`s of the same length, different content, pattern `Timeout$` | no exception |
| two `MemoryStream`s of different lengths, pattern `Timeout$` | violation: "- Length" |
| of the class whose `Length` throws, pattern `^(Length\|Position\|ReadTimeout\|WriteTimeout)$` | no exception |

With the pattern `Timeout$` and `T` = `MemoryStream`, two streams of different content gave a
violation that names `_buffer`. A variable typed `Stream` infers `T` = `Stream`. A `Stream` that a
member holds is compared by reference (R5): replacing it by another stream is a violation that
names the member, and writing to the same stream passes. One stream on both sides throws the
`InvalidOperationException` of rule S, with `T` = `Stream` and with `T` = `object`. Cause, read
from the code: as `T`, a `Stream` is compared by its members, and `PropertyInfo.GetValue` wraps the
exception of a getter; R5 applies to a member or an element, not to `T`. This is the part of item 2
of #40 that the #54 amendment left there ("two different instances of a shared type as `T`").

**A class derived from a fixed shared type.** Three classes were measured: one derived from
`Regex`, with a field `Uses`; one derived from `TypeDelegator`, over `typeof(int)`, with a field
`Tag`; one derived from `MemberInfo` that does not override `Module`, with a field `Tag`. In each,
`Old(() => this)` returned `this`. After the field changed, `EnsureAssignable(this, old)`:

| Class | Result |
|---|---|
| derived from `Regex` | no exception |
| derived from `TypeDelegator` | `TargetInvocationException`; inner `InvalidOperationException` from `Type.get_GenericParameterPosition` under the JIT; under Native AOT the inner message was "There is no metadata token available for the given member." and the getter was not identified |
| derived from `MemberInfo` | `TargetInvocationException`; inner `NotImplementedException` from `MemberInfo.get_Module` |

Controls. Two different instances of the `Regex` class, `Uses` 0 and 1: under the JIT a violation,
"- Uses"; under Native AOT `InvalidOperationException` "EnsureAssignable cannot compare
System.Text.RegularExpressions.RegexInterpreterFactory (reached through 'CountingRegex.factory'):
no properties or fields are visible to reflection under Native AOT", also for two instances with
`Uses` 0 (R8). The `MemberInfo` class with the pattern `^(CustomAttributes|MetadataToken|Module)$`:
the same instance passed after `Tag` changed, and two different instances, `Tag` 0 and 1, gave a
violation, "- Tag"; without the pattern, two different instances threw as the same instance did.
Cause, read from the code: B3 matches the types derived from the listed types, and `Regex` and the
reflection types are not among those whose state can change (`SharedTypes.ListedWithChangingState`),
so rule S does not apply. The public properties of `T` are read on the one instance, and each
member is equal to itself. Not measured: classes derived from `Assembly` or `Module`, a class of
yours declared in `System.Collections.Frozen`, `TypeDelegator` itself, the `System.Reflection.Emit`
builders and `DynamicMethod`.

**Corrected.** Each statement is quoted by its opening words. The text of the #54 amendment is left
as it was written.

- In the #54 amendment, under "**Limits.** Each of these passes in silence before and after this
  change", the item "A user class derived from a fixed shared type, and the framework's own mutable
  reflection types". Such a class does not always pass in silence. In the measured cases it passes
  in silence unless a getter of a public property of `T` throws: the classes derived from
  `TypeDelegator` and from `MemberInfo` threw `TargetInvocationException` from a getter that they
  inherit. For the framework's own types of that item nothing was measured.
- In the #54 amendment, under "**Rejected alternatives.**", the item "Tell a user class derived from
  a fixed shared type from a framework class.", the sentence "A user class derived from `Regex`,
  `Type` or `MemberInfo` that adds mutable fields still passes in silence." It holds for the
  measured `Regex` class; the measured `Type` and `MemberInfo` classes threw as above. The decision
  of that item stands: it stays a documented limit.

**The documentation.** #69 adds the three limits, with the ways out, to `README.md`,
`docs/examples/API_REFERENCE.md`, `docs/examples/USAGE_EXAMPLES.md`, the `[Unreleased]` section of
`CHANGELOG.md` and the XML documentation of `Old<T>()` and `EnsureAssignable<T>()`. The ways out:
for base-class private members, a second pair with `T` = the base class (measured for a private
field) or `Contract.Ensure`; for two streams as `T`, the throwing properties listed as assignable
or `Contract.Ensure`; for a class derived from a fixed shared type, `Contract.Ensure` with a value
taken with `Old`, as for a class derived from a type whose state can change. The migration
sentence of the CHANGELOG, "In a class that derives from a type that `Old<T>()` shares, replace the
pair on `this` by `Contract.Ensure` checks of the state members.", names no group and holds for both
as written.

**What stays open.**

- **[#40](https://github.com/cwouyang/uContract.NET/issues/40)**: item 1 (private fields declared
  on a base class) and, of item 2, two different instances of a shared type as `T` and the same
  instance of a fixed shared type. A change there can remove the limits above; the documentation is
  then to be revised with it.
- **[#46](https://github.com/cwouyang/uContract.NET/issues/46)** (fields-only comparison) changes
  which members are compared, and with it whether property getters run.

**Unchanged.** The decisions of this ADR, `Old<T>()`, B1 to B8, R0 to R8 and rule S.

---

## References

- [Issue #45: Faithful `Old<T>()` copies](https://github.com/cwouyang/uContract.NET/issues/45)
- [Issue #40: `EnsureAssignable<T>()` comparison gaps](https://github.com/cwouyang/uContract.NET/issues/40)
- [Issue #39: `Old`/`EnsureAssignable` under Native AOT](https://github.com/cwouyang/uContract.NET/issues/39)
- [Issue #46: Fields-only comparison](https://github.com/cwouyang/uContract.NET/issues/46)
- [Issue #47: `DBC_POST=off` and the `Old` + `EnsureAssignable` pairing](https://github.com/cwouyang/uContract.NET/issues/47)
- [Issue #52: A `null` `actual` or `expected` in `EnsureAssignable<T>()`](https://github.com/cwouyang/uContract.NET/issues/52)
- [Issue #54: `Old` + `EnsureAssignable` in a class derived from a shared type](https://github.com/cwouyang/uContract.NET/issues/54)
- [Issue #49: Compare frozen collections by elements in `EnsureAssignable`](https://github.com/cwouyang/uContract.NET/issues/49)
- [Issue #57: `Old` shares an array of frozen collections instead of copying it](https://github.com/cwouyang/uContract.NET/issues/57)
- [Issue #61: `EnsureImmutableCollection` throws `BadImageFormatException` for an array of function pointers](https://github.com/cwouyang/uContract.NET/issues/61)
- [`Contract.cs`](../../src/uContract/Contract.cs) — `EnsureImmutableCollection<T>()` and the helpers of its rule
- [`DeepCopier.cs`](../../src/uContract/DeepCopier.cs) — the copy
- [`SharedTypes.cs`](../../src/uContract/SharedTypes.cs) — B3
- [`MemberComparison.cs`](../../src/uContract/MemberComparison.cs) — R0–R8 and the walk
- [`ComparisonContext.cs`](../../src/uContract/ComparisonContext.cs) — the pair memo (R1)
- [`ReferencePair.cs`](../../src/uContract/ReferencePair.cs) — pairs compared by reference
- [`tests/uContract.AotSmoke/Program.cs`](../../tests/uContract.AotSmoke/Program.cs) — the smoke checks
- Prior art, pinned:
  - [DeepCloner @ `da61ac6`](https://github.com/force-net/DeepCloner/blob/da61ac691905bd4bea302f548f42520f670ea667/) (force-net/DeepCloner; the survey cites `DeepClonerSafeTypes.cs`; its path is not pinned) and [issue #39 there](https://github.com/force-net/DeepCloner/issues/39) (issue; not pinned)
  - [FastCloner @ `c3fc2eb` (v1.2.6)](https://github.com/lofcz/FastCloner/blob/c3fc2eb89272019dc9e1a3b0896fc7eb9020abd6/)
  - [FastDeepCloner @ `5769dbc`](https://github.com/AlenToma/FastDeepCloner/blob/5769dbc77b0422d2470a185b6676ac8d1d4a91a9/)
  - [AnyClone @ `d40a1bd`](https://github.com/replaysMike/AnyClone/blob/d40a1bd1ce26c212ead6836e5d497a4186bcb9e1/AnyClone/AnyClone/)
  - [Burtsev net-object-deep-copy @ `bbe10e6`](https://github.com/Burtsev-Alexey/net-object-deep-copy/blob/bbe10e6bac16687cb28196f3b45238f1ac7aa072/ObjectExtensions.cs)
  - [Baksteen.Extensions.DeepCopy @ `0471fe3`](https://github.com/jpmikkers/Baksteen.Extensions.DeepCopy/blob/0471fe3cad55706863461303a1873a29d46723e5/deepcopy/DeepCopyObjectExtensions.cs)
  - [CloneExtensions @ `4ce9574`](https://github.com/MarcinJuraszek/CloneExtensions/blob/4ce957461dc7a9a9d1e7a3db4dcb4763b468f449/src/CloneExtensions/)
  - [FluentAssertions tag `8.11.0` (`9ed9dc0`)](https://github.com/fluentassertions/fluentassertions/blob/8.11.0/)
  - [Compare-Net-Objects @ `4ecf3dd` (v4.84)](https://github.com/GregFinzer/Compare-Net-Objects/blob/4ecf3ddcfd252e276b0b9f59e5a57c1dc5ad74b9/Compare-NET-Objects/)
  - [AssertJ tag `assertj-build-3.27.7`](https://github.com/assertj/assertj/blob/assertj-build-3.27.7/assertj-core/src/main/java/org/assertj/core/)
- [AssertJ Recursive Comparison](https://assertj.github.io/doc/#assertj-core-recursive-comparison) (documentation, not pinned)
- [ADR-0006](0006-serialization-deep-copy.md), [ADR-0007](0007-reflection-field-comparison.md), [ADR-0021](0021-postconditions-under-trimming-and-aot.md)

---

## Revision History

| Date       | Status      | Notes                          |
|------------|-------------|--------------------------------|
| 2026-10-07 | Accepted    | Decision recorded after implementation (issue #45). Supersedes ADR-0006 and the `Old` parts of ADR-0021; amends ADR-0007, ADR-0016, ADR-0020, ADR-0001 and ADR-0005. |
| 2026-10-09 | Amended     | The `DBC_POST=off` pairing limitation is superseded (#47); checks 58 to 62 added. Decision unchanged. See Implementation Notes > Amendment. |
| 2026-10-09 | Amended     | The top level decides a `null` side first and compares a `string`, a delegate type and a nullable value type as one pair (#52); 72 smoke checks. Decision unchanged. See Implementation Notes > second Amendment. |
| 2026-10-10 | Amended     | One instance of a shared type whose state can change, passed as `actual` and as `expected`, throws `InvalidOperationException` (rule S, #54); 74 smoke checks. Decision unchanged. See Implementation Notes > third Amendment. |
| 2026-10-10 | Amended     | Two `FrozenSet<T>` or `FrozenDictionary<TKey, TValue>` values of one runtime type that a member or element holds are compared by element (R7), not by reference (R5), and one of the two types as `T` is compared as a whole (#49); 79 smoke checks. Decision unchanged. See Implementation Notes > fourth Amendment. |
| 2026-10-11 | Amended     | `Old<T>()` copies an array whose element type is declared in `System.Collections.Frozen` and a value of a value type declared there; an array type or `System.Array` as `T` is compared as a whole; two arrays are equal only with the same rank, lengths and lower bounds; two arrays of pointers or of function pointers are compared by address (#57); 84 smoke checks. Decision unchanged. See Implementation Notes > fifth Amendment. |
| 2026-10-12 | Amended     | Limit 18 of the #57 amendment is superseded: `Contract.EnsureImmutableCollection<T>()` no longer reads the namespace of the runtime type and reports an array of function pointers as a violation (#61; rule in ADR-0012); the measured results of that change; a limit for a frozen collection of a second copy of its assembly; 91 smoke checks. Decision unchanged. See Implementation Notes > sixth Amendment. |
| 2026-10-24 | Amended     | R7 compares the counts of two dictionaries before their entries; two of different counts are unequal without reading an entry (#50); 92 smoke checks. Decision unchanged. See Implementation Notes > seventh Amendment. |
| 2026-10-24 | Amended     | The measurements behind #69: base-class private members are not compared; two `Stream`s as `T` end in `TargetInvocationException`; in the measured cases a class derived from a fixed shared type passes in silence unless a public getter of `T` throws (corrects one item of the Limits of the #54 amendment). Library unchanged. Decision unchanged. See Implementation Notes > eighth Amendment. |
