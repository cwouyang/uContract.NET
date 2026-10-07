# ADR-0022: Faithful Old<T>() Copies and Robust EnsureAssignable<T>() Comparison

## Status

**Accepted**

- **Date**: 2026-10-07
- **Deciders**: Project maintainer
- **Status Date**: 2026-10-07

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
    object (`Type.IsCOMObject`) or derived from `ComObject`; in the `System.Collections.Frozen`
    namespace; a CoreLib comparer (declared in CoreLib and implementing `IEqualityComparer`,
    `IComparer`, `IEqualityComparer<string>` or `IComparer<string>`);
  - by list, the type or a derived type: `string`; `Delegate`; `Type`, `MemberInfo`, `Assembly`,
    `Module`, `Pointer`; `Stream`; `WaitHandle`, `CancellationTokenSource`, `Thread`, `Timer`,
    `SynchronizationContext`, `SemaphoreSlim`, `ManualResetEventSlim`, `CountdownEvent`,
    `ReaderWriterLockSlim`, `Barrier`; `Task`; `WeakReference`; `Component`; `HttpMessageHandler`,
    `HttpClient`; `Socket`;
  - by open generic definition, matched along `BaseType`: `ThreadLocal<>`, `Lazy<>`,
    `WeakReference<>`, `ConditionalWeakTable<,>`.

  Copying these gives a second object over the same OS handle, timer, callback list or lazy factory.
  Disposing or cancelling the copy then affects the original, and some getters block. The BCL
  compares default comparers by reference, so they must stay the same instance. A plain
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
  Old<T> copies T field by field through reflection. Trimming preserves the fields declared on T; private fields of T's base classes, the fields of the types that T's fields refer to, and of the runtime types other than T, may not be preserved. A field that is not preserved is copied bitwise, so an object it refers to is shared with the original. Preserve such types with [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(X))].
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
- **Shared types** (B3, D7). State inside them is not snapshotted and compares by reference.
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

---

## References

- [Issue #45: Faithful `Old<T>()` copies](https://github.com/cwouyang/uContract.NET/issues/45)
- [Issue #40: `EnsureAssignable<T>()` comparison gaps](https://github.com/cwouyang/uContract.NET/issues/40)
- [Issue #39: `Old`/`EnsureAssignable` under Native AOT](https://github.com/cwouyang/uContract.NET/issues/39)
- [Issue #46: Fields-only comparison](https://github.com/cwouyang/uContract.NET/issues/46)
- [Issue #47: `DBC_POST=off` and the `Old` + `EnsureAssignable` pairing](https://github.com/cwouyang/uContract.NET/issues/47)
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
