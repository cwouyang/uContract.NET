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
missed and no longer reports some it raised wrongly; each case is listed under Changed. In a class
that derives from a type that `Old<T>()` shares, replace the pair on `this` by `Contract.Ensure`
checks of the state members. `EnsureAssignable<T>()` now compares the private fields declared on a
base class that is not part of .NET (an aggregate base class of yours or of a library), so a
change of such state, for example a domain event raised into a private list, is a violation: list
that member as assignable where the change is intended. Code that catches the
`InvalidOperationException` that `Old<T>()` threw for a type it could not serialize, or the
`JsonException` for a deep graph, can drop that handler: `Old<T>()` throws neither. A `null` test
or an `ArgumentNullException` handler around the `Old` + `EnsureAssignable` pair is no longer needed for the case that postconditions are off. Removing a
`null` test changes the result for a member that can be `null`: going from `null` to a value, or
back, is now a violation that the assignable patterns cannot excuse, so keep the test where that
change is allowed. Code that caught `ArgumentNullException` or `ArgumentException` from
`EnsureAssignable<T>()` for a `null` value gets `PostconditionViolationException` for one `null` and
nothing for two. For a `string`, a delegate, a nullable value type, or a `FrozenSet<T>` or a
`FrozenDictionary<TKey, TValue>` as `T`, a pattern no longer allows a change, so do not call the
method for a value that may change; a test that matched "Fields were modified" for such a `T` sees
another message; a `string` is compared by ordinal `Equals`, so a change of letter case is a
violation; a delegate's target is no longer compared. An array type or `System.Array` as `T` is
compared as a whole too, element by element, and the patterns do not apply to it: for an `int[]` as
`T`, 2.0.0 let the pattern `SyncRoot` excuse a changed element and the pattern `.*` a changed
length, reported a pattern that is not a valid regular expression, and said "Fields were modified"
in the violation; none of these holds now. A generic method that forwards its own type parameter
and its patterns to `EnsureAssignable<T>()` gets this comparison when it is called with an array
type: its patterns then do not apply, and a different element is a violation, while a call with a
type that is compared by its members (a `List<int>`) honours them. Do not call such a method for an
array that may change; state what may change with `Contract.Ensure`. Two arrays are equal only when
they have the same rank, and the same length and lower bound in every dimension, wherever a member
or element holds them and when `T` is an array type or `System.Array`: an `int[,]` of 1 by 2 no
longer equals one of 2 by 1 with the same elements, and an array whose lower bound is 1 no longer
equals an `int[]` of the same elements. An `Old` result taken while another contract check is
running gave `ArgumentNullException` in 2.x; it now gives a violation that did not happen, or none.
`EnsureImmutableCollection<T>()` is no longer a `null` guard with postconditions off.
`EnsureImmutableCollection<T>()` now reports an array as mutable whatever its element type (an
`int[]` was a violation in 2.0.0 already; an `ImmutableList<int>[]` passed), and decides for a type
that is not an array by the interfaces that the runtime type of its argument implements and by the
generic types that it is or derives from, not by the start of its name or by its namespace; the
cases are listed under Changed and Fixed. Where a call became a violation: return an immutable or a
frozen collection (`ToImmutableArray()`, `ToImmutableList()` or `ToFrozenSet()`, for example; from a
builder, `ToImmutable()`) or a read-only wrapper (`AsReadOnly()` of a `List<T>` or of a
`Dictionary<TKey, TValue>`, or `Array.AsReadOnly(...)`); for a collection type of yours that is
immutable, implement `IImmutableList<T>`, `IImmutableSet<T>`, `IImmutableDictionary<TKey, TValue>`,
`IImmutableStack<T>` or `IImmutableQueue<T>`, or derive from `ReadOnlyCollection<T>` or from
`ReadOnlyDictionary<TKey, TValue>`; for a value that is not a collection (a `ReadOnlyMemory<T>`),
remove the call, or state the property that is meant with `Contract.Ensure` or
`Contract.EnsureResult`. A read-only wrapper is a view of the collection that it wraps, and the
method does not look at the wrapped collection: a wrapper passes also when its owner can still
change that collection. For trimmed and Native AOT applications, see [Trimming and Native
AOT](README.md#trimming-and-native-aot) in the README.

### Changed

- **BREAKING**: `Old<T>()` copies by reflection instead of JSON. Each object is a bitwise clone, so
  the copy keeps private state, `readonly` fields, base-class fields and the runtime type of every
  node, the top level included. A shared or cyclic reference is copied once and the cycle is kept.
  No constructor, property accessor, `Equals`, `GetHashCode` or serialization callback runs. The copy
  is a read-only snapshot: delegates are shared, so an event raised on the copy reaches the
  original's subscribers. Instances of resource types are shared with the original, not copied: every
  `Stream`, `Task`, `Lazy<T>`, `CancellationTokenSource`, handle, timer and `HttpClient`, every
  frozen collection (a `FrozenSet<T>`, a `FrozenDictionary<TKey, TValue>` and every other class
  declared in `System.Collections.Frozen`; a value type declared in `System.Collections.Frozen` and
  an array whose element type is declared in `System.Collections.Frozen` are copied), every `Regex`,
  `AsyncLocal<T>` and `System.Threading.Lock`, and the comparers of the base class library. The full
  list is in [Types that `Old<T>()` shares](docs/examples/API_REFERENCE.md#types-that-oldt-shares).
  An element of a copied array or list keeps its own rule: a `FrozenSet<T>` in a copied
  `FrozenSet<T>[]` or `List<FrozenSet<T>>` is the original's set. A class
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
  silently. `EnsureAssignable<T>()` does not compare a private field of a base class that is hidden
  either (#40). Preserve the type with `[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(X))]`.
  (ADR-0022)
- **BREAKING**: `Old<T>()` no longer throws `InvalidOperationException` for a type that "cannot be
  serialized" or because reflection-based JSON serialization is disabled. It no longer throws
  `JsonException` for a graph deeper than 64 levels, and it no longer translates a
  `NotSupportedException` from a getter or converter. It throws nothing of its own while copying;
  an exception from the supplier or from the runtime propagates. (ADR-0022)
- **BREAKING**: `EnsureAssignable<T>()` reports more. For members and elements that the comparison
  reaches: distinct instances of shared types are now different, because shared instances (see
  `Old<T>()` above) are compared by reference; a `string` is compared with `Equals` and a delegate
  by its methods. A `FrozenSet<T>` or a `FrozenDictionary<TKey, TValue>` is the exception: two of
  them are compared element by element, in enumeration order (a dictionary entry by entry, key with
  key and value with value), as 2.0.0 compared two of them in members of objects passed to it
  directly. Their elements, keys and values follow the rules of this version: two of the
  dictionaries whose values are different objects of equal content were a violation in 2.0.0 and
  are equal now (#49; see the entry that begins "`EnsureAssignable<T>()` reports less" below). The
  limits (order, comparer, shared elements) and the ways out are in [Types that `Old<T>()`
  shares](docs/examples/API_REFERENCE.md#types-that-oldt-shares). Two instances of one other class
  declared in `System.Collections.Frozen` (two instances of a class of yours declared there) are
  compared by reference. Members whose runtime types differ are unequal, except two sequences, which
  are compared element by element; the runtime type decides, not the declared type. Base and derived
  instances are unequal, with the same exception. Two arrays, of one runtime type or of two, are
  equal only when they also have the same rank, and the same length and lower bound in every
  dimension (see the entry that begins "`EnsureAssignable<T>()` finds two arrays equal" below). A
  `string` and a `char[]` are unequal (a `string` is not a sequence here). A user-defined struct
  that implements `IEnumerable`, held in an interface-typed or `object` member, is compared by its
  fields, so state besides its elements is reported. A change outside the window of a `Memory<T>` or
  `ReadOnlyMemory<T>` is reported. A dictionary value whose `Equals` ignores the changed content is
  reported, because values are compared by their members. A member whose declared type is an
  interface is walked into the members of its runtime type, and their getters run. A getter that
  throws on a class reached through a struct field propagates. When the two sides of a member have
  different runtime types, a violation is reported where 2.0.0 threw `ArgumentException` or
  `TargetException`. (ADR-0022)
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
  member. Two `IDictionary` values that a member or element holds, and two
  `FrozenDictionary<TKey, TValue>` values as `T`, are different before any entry is read when their
  counts differ, in every build: such a dictionary that grew or shrank is a violation, not "cannot
  compare", even when an entry holds a hidden class whose `Equals` reports the values unequal, and
  no getter of a key or value runs for it (#50). This does not cover a `T` such as
  `Dictionary<TKey, TValue>`, which is compared by its members, or two values of one value type
  that implements `IDictionary`. (ADR-0022)
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
  delegate type, a nullable value type, a `FrozenSet<T>` or a `FrozenDictionary<TKey, TValue>`, or
  an array type or `System.Array`: a `string` with an ordinal `Equals`, a delegate by its methods, a
  nullable value type with `Equals` and then by its fields, a `FrozenSet<T>` or a
  `FrozenDictionary<TKey, TValue>` element by element, in enumeration order (a dictionary entry by
  entry, key with key and value with value), two arrays element by element, each element as a member
  or element is, when they have the same rank, and the same length and lower bound in every
  dimension (otherwise they are not equal). Before, the members of `T` were compared. For a `string`
  those were its length and first character, so two different strings could pass. For a delegate
  they included its target, compared by its members: now targets and closure state are not compared,
  so the same method on another object passes, and every entry of a multicast delegate counts. A
  nullable value type with a value on both sides could not be compared. For a `FrozenSet<T>` or a
  `FrozenDictionary<TKey, TValue>` as `T`, 2.0.0 reported `Items`, or `Keys` and `Values`, for two
  different instances, also of equal content, and a call that listed those names as patterns passed,
  also for different content: now the elements are compared, and a difference is a violation
  whatever the patterns (#49). For an array as `T`, 2.0.0 reported `SyncRoot` for a different
  element (of an `int[]`, a `string[]`, an `int[,]` and an `object[]`, and of an `int[]` passed as
  `System.Array`), and `Length`, `LongLength` and `SyncRoot` for two `int[]` of different lengths; a
  call for two `int[]` that listed the reported names as patterns passed, also for a different
  element or length: now the elements are compared, and a difference is a violation whatever the
  patterns (#57). A generic method that forwards its own type parameter to `EnsureAssignable<T>()`
  gets this comparison when it is called with an array type, and its patterns then do not apply. The
  assignable patterns do not apply to these types and are not examined, so an invalid pattern is not
  reported. A nullable value type can be compared less strictly than the same type without `?`
  (`DateTime.Equals` ignores `Kind`). Under Native AOT the fields of its underlying type may not be
  preserved: for a nullable struct that holds a reference, the comparison can then report a
  difference for an unchanged value or, with an `Old` snapshot, miss a change inside the object it
  refers to. For an array type or `System.Array` as `T`, the members of the type of the elements may
  not be preserved either: preserve that type with `DynamicDependency`. All of this holds only when
  `T` itself is such a type: `T` = `object` still compares nothing beyond `null`, a base class that
  is not such a type compares the members it declares or inherits (`System.Array`, `Delegate`,
  `MulticastDelegate`, a `FrozenSet<T>` and a `FrozenDictionary<TKey, TValue>` are base classes that
  are compared as a whole), an interface only its own properties, also when the two values are a
  `FrozenSet<T>` or a `FrozenDictionary<TKey, TValue>`, or two arrays (only the members visible
  through `T` are compared), and two different instances of a shared type other than `string`, a
  delegate, a `FrozenSet<T>` or a `FrozenDictionary<TKey, TValue>` are still compared member by
  member (#40), as is a type that is shared, and fixed (a `Regex`), also for one instance on both
  sides. The getters of the public properties of `T` then run, and a getter that throws ends the
  call in `TargetInvocationException`: two `MemoryStream`s as `T` = `Stream` end so, from the
  `ReadTimeout` getter of `Stream`, and so can one instance of a class derived from `Type` or
  `MemberInfo` on both sides. For every `T` that is compared by its members, the private fields
  declared on its base classes are compared too (see the entry that begins "`EnsureAssignable<T>()`
  compares the private fields" below). One instance of a shared type whose state can change, passed
  as `actual` and as `expected`, is not compared, whatever `T` is, `object` included: the method
  throws `InvalidOperationException`; the entry for #54 below gives the ways out. (#52, #57;
  ADR-0022)
- **BREAKING**: `EnsureAssignable<T>()` finds two arrays equal only when they have the same rank,
  and the same length and lower bound in every dimension. This holds for two arrays that a member
  holds, for two arrays that are elements of a collection or of another array, keys or values of a
  dictionary, or fields of a struct, and for the two values themselves when `T` is an array type or
  `System.Array`. In 2.0.0 these pairs in a member were equal: an `int[,]` of 1 by 2 and one of 2 by
  1 with the same elements in the same order; an `int[]` and an `int[,]`, or an `int[,]` and an
  `int[,,]`, of the same elements in a member declared `System.Array`; and two arrays of the same
  elements whose lower bounds differ. As `T`, 2.0.0 found an `int[,]` of 1 by 2 equal to one of 2 by
  1, and an `int[]` equal to an array of the same elements whose lower bound is 1 when both were
  passed as `System.Array`. Each of these is a violation now. The rule is for two arrays: an array
  and a sequence that is not an array (a `List<T>`) are compared by what they enumerate, whatever
  the rank of the array (an array of pointers or of function pointers that has elements is the
  exception: see Fixed). It does not apply either to two arrays passed as a `T` that is neither an
  array type nor `System.Array` (`object`, an interface): only the members visible through `T` are
  compared there. Two arrays of different element types that agree in rank, lengths and lower bounds
  are compared element by element, so an `int[]` and an `object[]` of the same numbers are equal,
  and an `int[]` and a `long[]` are not. `Old<T>()` keeps the rank, the lengths and the lower bounds
  of an array that it copies, so this rule adds no violation to the `Old` + `EnsureAssignable` pair
  for an array that did not change. The violation does not say that a rank, a length or a lower
  bound differs. Where two such arrays must count as equal, list the member as assignable and state
  the condition with `Contract.Ensure`. (#57)
- **BREAKING**: `EnsureAssignable<T>()` throws `InvalidOperationException` when one instance of a
  shared type whose state can change is passed as `actual` and as `expected`. `Old<T>()` shares such
  an instance (a `Stream`, a `Task`, a `CancellationTokenSource`, a class derived from `Component`,
  …) instead of copying it, so the pair `EnsureAssignable(this, Contract.Old(() => this))` in a
  class derived from one has no earlier state to compare with: without this rule it would compare
  the object with itself. The same holds for a member of such a type passed as
  `EnsureAssignable(_member, Contract.Old(() => _member))`. Check each state member with
  `Contract.Ensure` and a value taken with `Old`; to check that a reference was not replaced, use
  `ReferenceEquals`. (#54; ADR-0022)
- **BREAKING**: `EnsureAssignable<T>()` compares the private fields declared on the base classes of
  each type that it compares by its members: `T`, the runtime type of a nested object, and an
  element below a `T` that is compared as a whole. The walk up the base classes stops at the first
  class of the .NET runtime and libraries, a class whose assembly is signed with a public key token
  of the .NET 8 shared frameworks; a library that is signed otherwise or not signed is walked. A
  change of a private field declared on such a base class, or of an auto-property declared there
  that is not public (compared as its backing field, `<Note>k__BackingField`), now fails the call,
  under the JIT and under Native AOT where the base class is preserved. For example, raising a
  domain event that a base aggregate class keeps in a private `_domainEvents` list is now a
  violation. List the member as assignable when the change is intended. A public auto-property of
  a base class is still compared once, through the property. The fields of a framework base class
  that are not private (`List<T>._size`, `Exception._message`) were compared before and still are;
  its private fields are not compared. Static fields are not compared, and a nested collection
  class of yours is still compared by its elements. The private fields of a base class from another
  library become possible violation entries and pattern targets, and their names can change with
  that library's versions. Under Native AOT they are not preserved, so they are not compared unless
  the base class is preserved, for example with
  `[DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicFields, typeof(Base))]`; the rule for
  a type with no visible members is decided before they are added. (#40; ADR-0023)
- **BREAKING**: `EnsureAssignable<T>()` compares a public property whose getter is not visible
  through the compared type (a private getter declared on a base class, or a virtual property whose
  derived class overrides only the setter) instead of throwing `InvalidOperationException` ("… no
  get method is visible through the compared type"). It reads the property with the getter that
  exists, of its declaring type or of the nearest declaration of the same virtual property that has
  one, and lists it once in every build. That getter now runs, and a getter that throws ends the
  call in `TargetInvocationException`, with the getter's exception as `InnerException`. The
  `InvalidOperationException` remains for a property with no getter at all (write-only) or whose
  getter trimming removed. (#40; ADR-0023)
- **BREAKING**: `EnsureAssignable<T>()` shows a member whose name collides with another member of
  the same compared type (a private field declared on two levels, a field hidden with `new`, two
  backing fields of one name) with its declaring type: as `<name> (declared on <type>)` in a
  violation entry, for example `_state (declared on StateBase)`, and as `<type>.<name>` in the path
  of a "cannot compare" message, whose advice still names the plain name and says that it also
  excuses the other members of that name. The type is shown by its name, or by its full name when
  two of the declaring types have the same name. Assignable patterns are unchanged: they are
  matched against the plain name, so one pattern excuses every member of that name, and one of them
  cannot be excused alone. This also renames entries that collide in 2.0.0 already (a field hidden
  with `new`). Members whose names do not collide are shown as before. Under Native AOT the
  comparison changes only for a property whose getter is not visible through the compared type, in
  the display of colliding names, and for a base class whose private fields the application
  preserves. (#40; ADR-0023)
- **BREAKING**: `EnsureImmutableCollection<T>()` returns a `null` `collection` instead of throwing
  `ArgumentNullException` when it does not check it: with postconditions off, or in a call made
  while another contract check is running. This is a second exception to "parameter validation
  always runs". Code that relied on the method as a `null` guard must add its own. (#52; ADR-0012)
- **BREAKING**: `EnsureImmutableCollection<T>()` reports an array as mutable, whatever its element
  type. In 2.0.0 an `ImmutableList<int>[]`, an `ImmutableArray<int>[]`, a
  `ReadOnlyCollection<int>[]`, a `ReadOnlyMemory<int>[]`, an `ImmutableList<int>[][]` and an
  `ImmutableList<int>[,]` passed; each is a violation now. An `int[]` and a `FrozenSet<int>[]` were
  violations in 2.0.0 and still are. Return an `ImmutableArray<T>` (`ToImmutableArray()`), or the
  result of `Array.AsReadOnly(...)`, which is a view of the array that it wraps: the method does not
  look at the wrapped array, so the wrapper passes also when its owner can still change the array.
  (#61; ADR-0012, ADR-0022)
- **BREAKING**: `EnsureImmutableCollection<T>()` decides for a type that is not an array by the
  interfaces that the runtime type of the argument implements and by the generic types that it is or
  derives from; the start of its name and its namespace no longer decide. A type that is not an
  array is accepted when it implements `IImmutableList<T>`, `IImmutableSet<T>`,
  `IImmutableDictionary<TKey, TValue>`, `IImmutableStack<T>` or `IImmutableQueue<T>`, when it is or
  derives from a `FrozenSet<T>` or a `FrozenDictionary<TKey, TValue>` (see Fixed), or when it is a
  read-only wrapper: a `ReadOnlyCollection<T>`, a `ReadOnlyDictionary<TKey, TValue>`, the
  `ReadOnlySet<T>` that the framework declares from .NET 9, or a class derived from one of them.
  Another type that is not an array is a violation. The method compares the full name of each
  generic type definition, whole and ordinally, and not the assembly that declares it. No longer
  accepted: in 2.0.0 these passed, and each is a violation now: the builders of an
  `ImmutableList<int>`, an `ImmutableArray<int>`, an `ImmutableDictionary<int, int>`, an
  `ImmutableHashSet<int>`, an `ImmutableSortedSet<int>` and an `ImmutableSortedDictionary<int, int>`;
  the `Keys` and the `Values` of an `ImmutableDictionary<int, int>`; the enumerators of an
  `ImmutableList<int>` and of an `ImmutableArray<int>`; the result of
  `ImmutableSortedSet<int>.Reverse()`; a `ReadOnlyMemory<int>`, a `ReadOnlySequence<int>` and a
  `ReadOnlyCollectionBuilder<int>`; the result of `ArrayList.ReadOnly(...)` for an `ArrayList`; a
  class named `ImmutableBag` that holds a `List<int>`; and a class whose name starts with `ReadOnly`
  and that meets none of the three conditions. Accepted now: in 2.0.0 these were violations, and
  each passes now: a class that implements `IImmutableStack<int>`; a struct that implements
  `IImmutableQueue<int>`; and a class derived from `ReadOnlyCollection<int>`, from
  `ReadOnlyDictionary<int, int>` or from `ReadOnlyObservableCollection<int>` whose name does not
  start with `ReadOnly`. Not changed: an `ImmutableList<int>`, a `ReadOnlyCollection<int>` and a
  class named `ReadOnlyOrders` derived from `ReadOnlyCollection<int>` passed in 2.0.0 and pass now;
  a `List<int>` was a violation in 2.0.0 and is one now; an `ImmutableList<int>.Empty` of a second
  copy of the assembly `System.Collections.Immutable`, loaded into another `AssemblyLoadContext`,
  passed in 2.0.0 and passes now. The limits of the rule and the ways out are in [What
  `EnsureImmutableCollection<T>()`
  accepts](docs/examples/API_REFERENCE.md#what-ensureimmutablecollectiont-accepts). (#61; ADR-0012,
  ADR-0022)
- A call of `EnsureImmutableCollection<T>()` that passes can allocate more. Measured on .NET 8 under
  the JIT, in bytes allocated per call: 2.0.0 allocated 192 for an `ImmutableList<int>`, for a
  `ReadOnlyCollection<int>` and for an `ImmutableDictionary<int, int>`; this version allocated 312,
  192 and 304. Measured on .NET 8 under Native AOT: this version allocated 1544, 192 and 1528 for
  the same three, 192 for a `FrozenSet<int>` and 1560 for an `ImmutableArray<int>`, and 3552 with
  its exception for a `List<int>`, a violation; there is no earlier number under Native AOT to
  compare these with. (#61; ADR-0022)
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
- `Old<T>()` no longer throws `ArgumentException` for an object that holds an array of pointers or
  of function pointers, and `EnsureAssignable<T>()` no longer throws `NotSupportedException` for
  such an object or for such an array as `T`. `Old<T>()` copies the array with its addresses. Two
  such arrays are compared by address, and are equal only when they also have the same rank, and the
  same length and lower bound in every dimension. The type that a pointer points to and the memory
  behind an address are not compared. Such an array that has elements is different from anything
  that is not an array of pointers or of function pointers (#57).
- `EnsureImmutableCollection<T>()` throws `PostconditionViolationException` for an array of function
  pointers, as it does for other arrays. Version 2.0.0 threw `BadImageFormatException` for a
  `delegate*<void>[]`, a `delegate*<void>[][]` and a `delegate*<void>[,]`; that was measured on
  .NET 8 (runtime 8.0.16), under the JIT (#61; ADR-0012, ADR-0022).
- `EnsureImmutableCollection<T>()` accepts a `FrozenSet<T>` or a `FrozenDictionary<TKey, TValue>`,
  and a type that derives from one of them. In 2.0.0 a `FrozenSet<int>` and a
  `FrozenDictionary<int, int>` were violations (#61; ADR-0012, ADR-0022).
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
