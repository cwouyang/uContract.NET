# ADR-0022: Faithful Old<T>() Copies and Robust EnsureAssignable<T>() Comparison

## Status

**Accepted**

- **Date**: 2026-10-07
- **Deciders**: Project maintainer
- **Status Date**: 2026-10-07
- **Amended**: 2026-10-09 — the `DBC_POST=off` pairing limitation under "What the measurements add or correct" is superseded (#47), and the smoke program gains checks 58 to 62; see the Amendment under Implementation Notes
- **Amended**: 2026-10-09 — at the top level a `string`, a delegate type and a nullable value type are compared as one pair by R0–R8, and a `null` side is decided first for every `T` (#52); the smoke program has 72 checks; see the second Amendment under Implementation Notes
- **Amended**: 2026-10-10 — at the top level, one instance of a shared type whose state can change, passed as `actual` and as `expected`, is not compared: the call throws `InvalidOperationException` (rule S, #54); the smoke program has 74 checks; see the third Amendment under Implementation Notes

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
the first one that the type matches in this order.

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
  "cannot compare" messages format their type. For example `uContract.Tests.OldPairingTests+Worker`
  and `System.ComponentModel.Component`. An open generic entry reads ``System.Lazy`1[T]``.
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
- `RuntimeHelpers.GetUninitializedObject(typeof(ComObject))` gives an instance of `ComObject` on
  every platform.
- No unit test and no smoke check passed the same instance of a state-holding shared type as both
  top-level values. Two calls passed one variable twice, neither with a shared type.

**Measured results, unit tests.** The suite had 629 tests on `71b2e4e` and has 696 after the change:
67 were added. With the library changed, 0 of the 696 tests fail, in the Debug and in the Release
configuration.

The new tests were run before the library changed. 47 of them failed. Each failed for the result
that the table above lists for its case, so those results were observed again. Two examples that are
not in the table also failed as predicted. Both gave no exception before the change: the pair in a
class two levels below `Component`, and a call that forwards its own generic parameter.

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

---

## References

- [Issue #45: Faithful `Old<T>()` copies](https://github.com/cwouyang/uContract.NET/issues/45)
- [Issue #40: `EnsureAssignable<T>()` comparison gaps](https://github.com/cwouyang/uContract.NET/issues/40)
- [Issue #39: `Old`/`EnsureAssignable` under Native AOT](https://github.com/cwouyang/uContract.NET/issues/39)
- [Issue #46: Fields-only comparison](https://github.com/cwouyang/uContract.NET/issues/46)
- [Issue #47: `DBC_POST=off` and the `Old` + `EnsureAssignable` pairing](https://github.com/cwouyang/uContract.NET/issues/47)
- [Issue #52: A `null` `actual` or `expected` in `EnsureAssignable<T>()`](https://github.com/cwouyang/uContract.NET/issues/52)
- [Issue #54: `Old` + `EnsureAssignable` in a class derived from a shared type](https://github.com/cwouyang/uContract.NET/issues/54)
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
