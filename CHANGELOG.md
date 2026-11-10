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
missed and no longer reports some it raised wrongly; each case is listed under Changed. An
assignable pattern is now also matched against the property name of an auto-property's backing
field, so a pattern such as `^Email$` can excuse a change that 2.0.0 reported. In a class
that derives from a type that `Old<T>()` shares, replace the pair on `this` by `Contract.Ensure`
checks of the state members. `EnsureAssignable<T>()` now compares the private fields declared on a
base class that is not part of .NET (an aggregate base class of yours or of a library), so a
change of such state, for example a domain event raised into a private list, is a violation: list
that member as assignable where the change is intended. `EnsureAssignable<T>()` now compares the
runtime type of the two values, so a call with `T` = `object`, an interface or a base class compares
what it skipped and can start to fail; under Native AOT such a call throws "cannot compare" until the
runtime type is preserved: add the `[DynamicDependency(...)]` that the message names.
`EnsureAssignable<T>()` now compares an `[InlineArray]` struct element by element on the .NET 8 and
the .NET 10 runtimes, where earlier versions saw only element 0 on .NET 8 and could throw
`NotSupportedException` on .NET 10: a change of a later element is now a violation, so list as
assignable an inline-array member that may change; a pattern no longer excuses an inline array passed
as `T`; where the call throws "cannot compare" for an inline array (Native AOT, or no dynamic code),
follow the ways out that the message gives. Code that catches the
`InvalidOperationException` that `Old<T>()` threw for a type it could not serialize, or the
`JsonException` for a deep graph, can drop that handler: `Old<T>()` throws neither. A `null` test
or an `ArgumentNullException` handler around the `Old` + `EnsureAssignable` pair is no longer needed
for the case that postconditions are off. Removing a
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
  either (#40). Preserve the type with
  `[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(X))]`. (ADR-0022)
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
  are compared element by element when neither has members on a class of yours (see the entry for
  #40 item 2 below); the runtime type decides, not the declared type. Base and derived
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
  not be preserved either: preserve that type with `DynamicDependency`. The same values are now
  compared as a whole also when `T` is `object`, an interface or a base class and the runtime type of
  the two values is such a type, and two different instances of a shared type are a violation by
  reference whatever `T` is (see the entry for #40 item 2 below). One instance of a type that is
  shared, and fixed (a `Regex`), on both sides is compared by the members of `T`: the getters of the
  public properties of `T` then run, and a getter that throws ends the call in
  `TargetInvocationException`, as can one instance of a class derived from `Type` or `MemberInfo` on
  both sides. For every value that is compared by its members, the private fields
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
  exception: see Fixed). It applies also to two arrays passed as a `T` that is neither an array type
  nor `System.Array` (`object`, an interface), which are now compared by their runtime type (see the
  entry for #40 item 2 below). Two arrays of different element types that agree in rank, lengths and lower bounds
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
  change of a private field declared on a base class that the walk enters, or of an auto-property
  declared there that is not public (compared as its backing field, `<Note>k__BackingField`), now
  fails the call,
  under the JIT and under Native AOT where the base class is preserved. For example, raising a
  domain event that a base aggregate class keeps in a private `_domainEvents` list is now a
  violation. List the member as assignable when the change is intended. A public auto-property of
  a base class is still compared once, through the property. The fields of a framework base class
  that are not private (`Exception._message`) were compared before and still are, except in a
  sequence built on a framework collection (`List<T>._size`; see the entry for #40 item 2 below);
  its private fields are not compared. Static fields are not compared. The private fields of a base class from another
  library become possible violation entries and pattern targets, and their names can change with
  that library's versions. The entries most likely to surprise are lazily cached values (for example
  a cached hash code in a value-object base class, set the first time the object is hashed inside
  the checked method) and event subscriptions (the backing field of an event declared on a base
  class that is not part of .NET); inside a nested object, such a change is reported under the
  top-level member. List the member as assignable, or check with `Contract.Ensure`.
  Under Native AOT they are not preserved, so they are not compared unless
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
  of a "cannot compare" message, whose advice still names the member without its declaring type
  (for a backing field, the property name it backs) and says that it also
  excuses the other members of that name. The type is shown by its name, or by its full name when
  two of the declaring types have the same name. The declaring type takes no part in matching
  assignable patterns: they are matched against the plain name (and, for a backing field, its
  property name; see the next entry), so one pattern excuses every member of that name, and one of
  them cannot be excused alone. This also renames entries that collide in 2.0.0 already (a field
  hidden with `new`). Members whose names do not collide are shown as before. Under Native AOT the
  comparison changes only for a property whose getter is not visible through the compared type, in
  the display of colliding names, and for a base class whose private fields the application
  preserves. (#40; ADR-0023)
- **BREAKING**: `EnsureAssignable<T>()` matches an assignable pattern against the plain name of a
  member and, for the backing field that the compiler generates for an auto-property
  (`<Email>k__BackingField`), also against the property name (`Email`). One anchored pattern
  `^Email$` now excuses an auto-property declared on `T`, as the property and as its backing field,
  where 2.0.0 needed `^(Email|<Email>k__BackingField)$`. Every pattern that excused a member in
  2.0.0 still excuses it, and violation entries still show the plain name. A pattern can now excuse
  a change that 2.0.0 reported: a pattern that matches the property name but not the plain name of
  the backing field (`^Email$`, `^E`, `Email$`) now also excuses the backing field of a public
  auto-property declared on `T` (2.0.0 excused the property and reported the backing field), and of
  an auto-property that is not public, declared on `T` (2.0.0 reported it). The same holds for an
  explicitly implemented interface auto-property declared on `T`, whose backing field
  `<Ns.IFoo.Id>k__BackingField` is matched as `Ns.IFoo.Id`, so `Id$` excuses it. Of an
  auto-property that is not public (or is explicitly implemented), declared on a base class, 2.0.0
  compared nothing (its backing field is a private field of the base class); the entry above that
  begins "`EnsureAssignable<T>()` compares the private fields declared on the base classes" makes it
  compared, and such a pattern excuses it. A field
  written by hand behind a property (`_email`) is still matched by its own name only. Patterns
  still apply to top-level members only. Which members are compared does not change: fields-only
  comparison was evaluated and not adopted. (#46; ADR-0025)
- **BREAKING**: `EnsureAssignable<T>()` compares the two values at the top level by their runtime
  type, with the rules that a member pair follows (#40 item 2). In 2.0.0 the top level compared only
  what the declared `T` shows: `T` = `object` compared nothing beyond `null`, an interface only the
  properties that it declares, and a base class not what the runtime type adds. Now, for a `T` that is
  not compared as a whole: a runtime type that is a `string`, a delegate type, a `FrozenSet<T>` or a
  `FrozenDictionary<TKey, TValue>`, or an array type is compared as a whole, also when `T` is
  `object`, an interface or a base class; two different instances of a type that `Old<T>()` shares
  are a violation by reference, whatever their state, also when `T` is that type (two
  `MemoryStream`s as `T` = `Stream`; in 2.0.0 their getters ran, and `ReadTimeout` threw); two values
  of different runtime types are a violation that names both runtime types and `T`, except two
  sequences that are compared by their elements; a boxed value type is compared with `Equals` first,
  then by its fields; any other value is compared by the members of its runtime type, and for an
  interface `T` also by the properties that the interface declares, read through it. One exception
  keeps the 2.0.0 rule: when the runtime type of either value is a value type that implements
  `IEnumerable` (`ImmutableArray<T>`, `ArraySegment<T>`, a struct of yours) and the two values are not
  two sequences of different runtime types, the members visible through `T` are compared, so
  `EnsureAssignable<object>` of two `ImmutableArray<int>` values still compares nothing beyond `null`;
  pass such a value as its own type. (Against a `string`, a delegate or a shared type that is not a
  sequence, such a value is a different-runtime-types violation.) The patterns
  apply to those members: a pattern written against `T` still excuses its member, and a member that
  only the runtime type has needs a pattern that names it. When `T` is a framework class that the
  runtime type derives from, the private fields of the base classes are compared up to and including
  `T`. A violation names the runtime type when it is not `T` ("Members were listed from the runtime
  type X, not from T (Y)."), except for a compiler-generated sequence compared by its elements, of
  which no member is listed. So a call with `T` = `object`, an interface or a base class compares
  what it skipped and can start to fail, and `EnsureAssignable(this, Contract.Old(() => this))`
  written in a base class compares the subclass's state too when it runs on a subclass instance; the
  documentation examples pass the concrete type and keep working under the JIT. A sequence (other
  than a `string`, an array, a frozen collection and a shared type) is compared by the class chain of
  its runtime type, at the top level and below it. One built on a framework collection is compared
  by its elements and by the members declared on its classes of yours; at the top level also by its
  public framework properties other than `Capacity` (a `Comparer`, the `Key` of a grouping), and an
  element difference is the entry `(the elements; assignable patterns do not apply to them)`. Its
  framework's non-public fields are not compared: for `T` = `List<int>` a pattern that excused
  `_items` no longer applies, a change of capacity alone is no longer a violation, and a comparer
  kept only in a non-public field (`Hashtable`) and the subscribers of an `ObservableCollection<T>` are
  no longer compared. A `Lookup<TKey, TElement>` is not a collection: passed as its own type it is
  compared by all its members, its comparer included. At the top level a framework property without a
  getter is not read unless `T` declares or inherits it: `T` = `XmlDocument` still ends in
  `InvalidOperationException` at `XmlResolver` unless a pattern excuses it. A class of yours that
  implements `IEnumerable` itself is compared by its members only and is not enumerated, also as a
  nested value, where 2.0.0 compared its elements; elements that it produces from the captured state
  of a delegate are not compared. A compiler-generated sequence of any assembly that is not part of
  .NET (a NuGet package included) is compared by its elements, at every level and also against a
  `List<T>` or an array: a `yield` iterator from a method that returns `IEnumerable` or
  `IEnumerable<T>`, and a collection expression held as `IReadOnlyList<T>`, `IReadOnlyCollection<T>`
  or `IEnumerable<T>` (built by the .NET 8 SDK as `<>z__ReadOnlyArray<T>`, or `<>z__ReadOnlyList<T>`
  for a spread of a `yield` iterator; a single element is `<>z__ReadOnlyArray<T>` with the 8.0.2xx SDK
  and `<>z__ReadOnlySingleElementList<T>` with SDK 8.0.427, so the class depends on the compiler and
  the `<>` at the start of its name decides). It is recognised as a class that implements `IEnumerator` and
  is compiler-generated: it, or a type in its declaring chain, is marked `[CompilerGenerated]` with a
  `<` or a `$` in its name or has a metadata name that starts with `<>` (so the iterator of a local function that
  captures a variable, nested in its display class, counts too), or a class whose metadata name
  starts with `<>` that implements `IEnumerable`; a source-generated `[CompilerGenerated]` class with a name
  C# can write (also one that implements only `IEnumerable` or is its own enumerator) and a `file`
  class are classes of yours, compared by their members. An infinite iterator does
  not return, and an exception from its body leaves the call
  unwrapped, except an `InvalidOperationException` while two sequences of different runtime types
  are enumerated, which makes them unequal. One from a method that returns only `IEnumerator` or `IEnumerator<T>` is compared by its
  fields. At the top level a sequence that is not a collection (a LINQ query,
  `GetConsumingEnumerable()`, an `XmlDocument`) is not enumerated: passed as a `T` other than its own
  type, it throws `InvalidOperationException`; pass `ToList()`, or the value as its own type. The
  message names the three interfaces that make a collection: `ICollection`, `ICollection<T>` and
  `IReadOnlyCollection<T>`. Two
  sequences of different runtime types are compared by their elements only when neither declares
  members on a class of yours; otherwise they are unequal. Under Native AOT, a runtime type other
  than `T` that nothing preserves shows no declared member to reflection, and the call throws
  `InvalidOperationException` ("cannot compare …"), also when the two values are equal. This covers
  `EnsureAssignable(this, Contract.Old(() => this))` written in a base class and run on a subclass
  instance, and a framework collection such as a `List<int>` passed as `object` when nothing in the
  application preserves it. A boxed value type throws only when its `Equals` reports the values
  unequal; a primitive, an enum, a value-type sequence that keeps the declared `T` and a
  compiler-generated sequence are not affected, and neither is an inline array, which has rules of its
  own (see the entry for #84). A subclass
  that adds no state and that nothing preserves throws too. The fix is the one the message names,
  with the type in C# syntax: `[DynamicDependency(DynamicallyAccessedMemberTypes.All,
  typeof(Shop.ExpressOrder))]` on a method that runs, or pass the values as their own type;
  `[DynamicallyAccessedMembers]` on a base class of yours does not help. A `yield` method that
  returns only an enumerator throws under Native AOT, when nothing preserves it; the message names
  no `typeof`, which cannot be written for it, and suggests returning `IEnumerable<T>` from the
  method. For any type that C# cannot name in `typeof` (an anonymous type, a VB iterator, a type with
  one among or inside its generic arguments) the messages name no `typeof`, say why, and offer what
  can be written instead: a value of a type of yours, an element type of yours for a `List<T>` of
  anonymous values, a type of yours in place of one deeper inside, the member as assignable, or
  `DBC_POST=off`. A source-generated class keeps the `typeof` fix, and a `file` class is named there
  by its own name, with the words that the method must be inside the file that declares it. A type
  whose generic arguments hold `file` classes of more than one file cannot be named in any one file:
  its message names no `typeof`, says so, and offers other generic arguments or `DBC_POST=off`.
  Below the
  top level, a class of yours that nothing preserves and that only inherits visible members is
  compared by its `Equals` and ends in "cannot compare" when `Equals` reports the values unequal,
  where it was compared by its inherited members. The nested "cannot compare" messages now write the
  type inside `typeof` in C# syntax, where they said "typeof(X))] where X is that type", and so does
  the message for a property without a getter, which names the declaring type of the property. Not
  detected under Native AOT: a runtime type of
  which something preserves some members (a constructor kept by a dependency-injection
  registration) is compared by what stays visible, which can be the members of `T` alone; and
  preservation is per generic definition, so an instantiation that nothing preserves shows its
  members when another instantiation of its definition is preserved. Known message limits: the
  message for a sequence that is not a collection says that enumerating it "could run a query,
  consume its source or block", also for a materialised one (`ImmutableQueue<T>`,
  `StringDictionary`, `XmlNodeList`); and when two sequences of different runtime types have equal
  elements and a property of an interface `T` differs, the violation does not say that the runtime
  types differ. Migrating: where a call starts to fail because the runtime type adds a member that
  may change, list that member as assignable; under Native AOT add the `DynamicDependency` that the
  message names, on `Main` or on any method that runs; remove a pattern that excused the private
  fields of a framework collection; materialise a query before the call; and check the state of a
  shared type with `Contract.Ensure`. (#40; ADR-0026)
- **BREAKING**: `EnsureAssignable<T>()` compares an `[InlineArray]` struct element by element
  wherever it meets two values of one inline-array type: as `T`, as the runtime type of the values
  under another `T` (`object`, an interface that the inline array implements), and as a field, a
  property, an element of an array or a list, a dictionary value or the value of an `object` member.
  In 2.0.0 it saw only element 0 on the .NET 8 runtime (reflection and `ValueType.Equals` see only the
  one declared field), so a change of a later element with an equal element 0 passed; it is now a
  violation. An `Equals` of the inline array written by hand decides when it returns `true`; otherwise
  the elements are compared in index order, as the elements of an array are (pointer and
  function-pointer elements by address), and the built-in `Equals` is never called. Below the top level
  a difference is reported as the member that holds the inline array, without an element index. At
  the top level an inline array is compared as a whole and the patterns do not apply: before this
  change `T` = the inline-array type compared its one field, so a pattern that named it could excuse
  element 0. The
  violation reads "actual and expected are not equal. {type} is an inline array, compared as a whole
  element by element, so assignable patterns do not apply." An inline array that implements
  `IEnumerable` follows this rule too, not the rule for a value-type sequence. A struct that has no
  `Equals` written by hand and whose built-in `Equals` would reach an inline array's is compared by its
  fields without that `Equals`: one with a field of an inline-array type or a `Nullable<>` of one, a
  `record struct` or a `ValueTuple` with one, one with a nested such struct, and one whose field
  declared as `object`, `ValueType`, an interface or a class holds such a value at run time, also
  inside a record class, a `System.Tuple<…>` (or a class derived from one), or a C# or Visual Basic
  anonymous object (for Visual Basic only through its `Key` properties). A struct or record whose
  `Equals` is written by hand keeps it: it decides first, and an exception it throws propagates; a
  record whose own typed `Equals(R)`, or that of a base record, is written by hand counts as such.
  `Old<T>()` copies every element of an inline array; before this change its reflection copy copied
  element 0 as a field and shared the other elements with the original, so an unchanged inline array
  of mutable reference elements such as `List<int>` (the type measured) was reported on every call
  after `Old` on .NET 8; elements of a type that `Old` shares, such as `string`, are the same instance
  in the copy and were not (read from the code). It now passes, and an in-place change of any element
  is reported. Without dynamic code (Native AOT) the call throws `InvalidOperationException` ("cannot
  compare …"), also for equal values, where it cannot read the elements or `Old<T>()` could not copy
  them; the message names the inline-array type and the path, and only the ways out that work for its
  case. (1) Nothing preserves the inline-array type, so reflection does not list its element field:
  preserve it with `[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(X))]`, list the
  member as assignable, or set `DBC_POST=off`. An `Equals` written by hand that says "unequal" gives
  this exception too, where the JIT compares the elements. (2) On the .NET 8 runtime, struct elements:
  list the member as assignable, run on the .NET 9 runtime or later, or set `DBC_POST=off`; preserving
  the type does not help. (3) On the .NET 9 runtime or later, struct elements that hold references and
  that an `Old` copy could not copy (the runtime offers no way without dynamic code to write them back,
  so the copy shares what they refer to with the original; `Old` does not throw): list the member as
  assignable, or set `DBC_POST=off`. Case 3 is decided before an `Equals` written by hand is asked, for
  the inline array and for the structs that hold it by value at any depth. For a struct `T`, whose
  copy carries no record of what was not copied, it refuses every such inline array that `T` holds by
  value, also for values that did not come from `Old`, and the message still says "Contract.Old could
  not copy"; a box held in a field or an inline-array element declared `object`, an interface or
  `ValueType` is held through a reference, so only the record on an `Old` copy refuses it (an
  auto-property, recognised by its compiler-generated getter, counts as its backing field; a computed
  property does not, as its getter may box a field afresh, and neither does a C# 14 `field` property
  with a getter written by hand). Case 3 was
  measured with unit tests that simulate a runtime without dynamic code on .NET
  10; no Native AOT build on .NET 9 or later was run. The library uses dynamic code
  (`MakeGenericMethod`) in one place, for struct elements, only where it is supported; its `IL3050` is
  suppressed there with a justification, and the smoke program's Native AOT publish reports no warning.
  Migrating: where a later element of an inline array may change, list the member that holds it as
  assignable; under Native AOT add the `DynamicDependency` that the message names, or list the member;
  for a test that matched the element-0 result or the old top-level message, expect the new one.
  (#84; ADR-0021, ADR-0022, ADR-0026)
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
- `EnsureAssignable<T>()` compares two arrays of one runtime type whose element type is `bool`,
  `char`, an integer type, `float`, `double` or an enum without boxing an element; every result stays
  as it was. Measured once, for two `byte[]` of 1,000,000 equal elements as `T` (.NET 8.0.16, JIT,
  Release): 80,780,176 bytes and about 187 ms before, 1,152 bytes and about 0.13 ms now. (#64;
  ADR-0022)
- Under Native AOT, `EnsureAssignable<T>()` throws `InvalidOperationException` ("cannot compare …")
  for a type of which reflection lists no property or field other than properties of a byref-like
  type (`Span<T>`, `ReadOnlySpan<T>`, a `ref struct`): such properties are not compared and do not
  count as visible members. As the type compared at the top level it throws on every call, also for
  one instance on both sides and also when an assignable pattern names the property, which avoided
  the call's exception before (a value of a value type passed as another `T` asks its `Equals`
  first). Below the top level its `Equals` decides, so an `Old` copy of it
  throws unless an `Equals` override reports it equal. The message says that only properties of a
  byref-like type are visible and names the ways out: `Contract.Ensure` on each state member, an
  `Equals` override or the top-level member as assignable (below the top level), preserving the
  fields of the type if it has any, or `DBC_POST=off`. Builds with dynamic code are not affected
  (#40; ADR-0021, ADR-0022).
- Known limitation: a property that returns a new instance of its own type on every read (for
  example `DirectoryInfo.Root`) now makes the comparison grow without bound, where 2.0.0 overflowed
  the stack. Fields-only comparison would remove it; it was evaluated in
  [#46](https://github.com/cwouyang/uContract.NET/issues/46) and not adopted. (ADR-0022, ADR-0025)
- Known limitation, unchanged from 2.0.0: for C# fixed-size buffers (`fixed int X[4]`), which are not
  inline arrays, the elements after the first are invisible to reflection and to `ValueType.Equals`. A
  change there with an equal first element passes silently, on the .NET 8 and the .NET 10 runtimes.
  (ADR-0022)
- Known limitations of the inline-array comparison (#84; ADR-0021, ADR-0022). A record that declares
  its own field holding an inline array, under a base record whose typed `Equals(R)` is written by
  hand, compares that field with the built-in `Equals`: on .NET 10 `NotSupportedException` propagates,
  on .NET 8 only element 0 is seen; write the record's own typed `Equals`, or list the member as
  assignable. Without dynamic code on the .NET 9 runtime or later, an `Equals` written by hand that is
  asked first and reaches the inline array of an `Old` copy only through a reference (a struct's
  `Equals` over a class, or under Native AOT the `Equals` of a class whose members reflection does not
  show) can read the elements that the copy shares with the original and pass a change; list the member
  as assignable. Under Native AOT a struct that has no `Equals` written by hand and that nothing
  preserves cannot be looked into, so it is decided by its built-in `Equals`, also when it holds an
  inline array: on .NET 8 a change in a later element passes (measured), on .NET 10 it throws
  `NotSupportedException` (not measured); `[DynamicallyAccessedMembers]` on `T` does not preserve the
  struct types of its fields, so preserve the struct with
  `[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(S))]`, or list the member as
  assignable (neither way out was measured). Without dynamic code on the .NET 9 runtime or later, for
  a struct `T`, an inline array among the elements of a value-type sequence `T` holds by value
  (`ImmutableArray<object>`, `ArraySegment<object>`, a struct that implements `IEnumerable`) is
  refused also when the values did not come from `Old` ("cannot compare", never a silent pass;
  accepted); list the member as assignable. A computed property whose getter builds an object from a
  field (`AsArray => [Buf]`) carries no `Old` record, so when that field is excused and `Old` could not
  copy its struct elements, an in-place change of them passes through the property (accepted); list
  the computed property as assignable too. A getter written by hand that is marked `[CompilerGenerated]`
  (by you, a source generator or an IL weaver) is trusted as an auto-property's, so with its field
  excused and `Old` unable to copy the struct elements of an inline array, an in-place change of them
  can pass through it (accepted); do not mark hand-written getters `[CompilerGenerated]`, or list the
  property as assignable too. After the first `Old` copy in a process that could not
  copy struct elements, every compared pair costs up to two more weak-table lookups (read from the
  code, not measured). A struct with a field declared as `object`, `ValueType`, an interface or a
  class (other than `string`, an array or a delegate) has the run-time values of those fields looked at each time it is compared, and the look can
  walk a whole graph of records: 2000 such structs that share one 2000-node record chain took 3.3 s on
  .NET 8 and 4.9 s on .NET 10, measured at an intermediate commit of this change, before the look used
  a work list, and not measured again.

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
- `Old<T>()` and `EnsureAssignable<T>()` no longer keep a collectible type alive. Their per-type
  caches held each type they looked up, so a type from a collectible assembly, and the assembly, could
  not be collected; a constructed generic type over such a type, such as a `List<T>` of it, is
  covered too. Results do not change (#48; ADR-0024).
- `EnsureAssignable<T>()` no longer throws `NotSupportedException` when a compared type has a public
  property of a byref-like type (`Span<T>`, `ReadOnlySpan<T>`, a `ref struct`), for example an
  object that holds an `Encoding`, whose `Preamble` is a `ReadOnlySpan<byte>`. Reflection cannot read
  such a property, so it is not compared, at any level, and nothing reports that it was left out; a
  write-only one does not throw "cannot read property" either. The fields of the type are still
  compared, so a change of a field that such a property shows is still a violation, named by the
  field at the top level and, below it, by the top-level member that leads to it. A property that
  returns by reference (`ref int`) is compared as before. For the rule on two sequences of different
  runtime types, such a property declared on a class of yours still counts as a member of yours.
  Under Native AOT such a property that reflection lists does not count as a visible member, because
  the fields behind it may not be preserved: a type of which reflection lists no other property or
  field is a type with no visible members (see the entry under Changed) (#40; ADR-0022).
- `EnsureAssignable<T>()` no longer throws `NotSupportedException` ("Calling built-in Equals() or
  GetHashCode() on type marked as InlineArray is invalid.") on the .NET 10 runtime when it meets an
  `[InlineArray]` struct, also for equal values: as a member, an element, a boxed value, an `Old` copy,
  or inside a struct whose built-in `Equals` reaches it (a `record struct` or a tuple with an inline
  array, a struct that holds such a tuple, a struct whose `object` field holds an inline array). The
  package targets `net8.0`, and an application on .NET 10 runs it on the .NET 10 runtime. An exception
  thrown by an `Equals` written by hand still propagates (see the known limitations for a record under
  a base record with a typed `Equals` written by hand) (#84; ADR-0022).

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
