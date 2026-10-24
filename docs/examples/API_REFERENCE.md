# uContract.NET API Reference

Complete reference for all 17 public methods in uContract.NET.

> **Per-method signatures, parameters, and XML documentation** are available via IntelliSense in your IDE.
> This document covers what IntelliSense cannot: configuration, exception hierarchy, best practices, and performance.

---

## Table of Contents

- [Method Overview](#method-overview)
- [Environment Variable Configuration](#environment-variable-configuration)
- [Exception Hierarchy](#exception-hierarchy)
- [Best Practices](#best-practices)
- [Performance Considerations](#performance-considerations)
- [Types that `Old<T>()` shares](#types-that-oldt-shares)
- [What `EnsureImmutableCollection<T>()` accepts](#what-ensureimmutablecollectiont-accepts)
- [Trimming and Native AOT](#trimming-and-native-aot)
- [See Also](#see-also)

---

## Method Overview

| Category | Methods | Purpose |
|----------|---------|---------|
| **Preconditions** (3) | `Require`, `RequireNotNull`, `RequireNotEmpty` | Validate inputs at method entry |
| **Postconditions** (5) | `Ensure`, `EnsureNotNull`, `EnsureResult`, `EnsureImmutableCollection`, `EnsureAssignable` | Verify results and state changes |
| **Invariants** (2) | `Invariant`, `InvariantNotNull` | Enforce class-level constraints |
| **Helpers** (7) | `Check`, `Ignore`, `Old`, `Imply`, `IfAndOnlyIf`, `CheckUnsupportedOperation`, `FollowsFrom` | Runtime assertions and utilities |

---

## Environment Variable Configuration

All contract methods (except pure logic functions) are controlled by environment variables:

| Method(s) | Environment Variable | Default |
|-----------|---------------------|---------|
| `Require`, `RequireNotNull`, `RequireNotEmpty`, `Ignore` | `DBC_PRE` | `on` |
| `Ensure`, `EnsureNotNull`, `EnsureResult`, `EnsureImmutableCollection`, `EnsureAssignable`, `Old` | `DBC_POST` | `on` |
| `Invariant`, `InvariantNotNull` | `DBC_INV` | `on` |
| `Check` | `DBC_CHECK` | `on` |
| **Fallback for any of the above that sets no flag of its own** | `DBC` | `on` |

Each per-type flag **overrides** `DBC`; `DBC` is not a master switch that outranks them. So
`DBC=on DBC_INV=off` leaves `Invariant` and `InvariantNotNull` disabled. See
[ADR-0004](../adr/0004-runtime-configuration-environment-variables.md).

`Ignore` is never disabled: `DBC_PRE` does not turn it off. It evaluates its condition and returns the
result whether preconditions are enabled or not; called from inside another contract's condition, it
returns `false` without evaluating.

**Pure functions (not controlled by environment variables):**
- `Imply`, `IfAndOnlyIf`, `CheckUnsupportedOperation`, `FollowsFrom`

---

## Exception Hierarchy

```
System.Exception
  └── ContractViolationException (abstract base)
        ├── PreconditionViolationException
        ├── PostconditionViolationException
        ├── InvariantViolationException
        └── CheckViolationException
```

**ContractViolationException Properties:**
- `Message` (string): The description with its prefix (see the format below)
- `Description` (string): The description without the prefix
- `ViolationType` (`ContractType` enum): Type of contract violated

**Exception Message Format:**
- Precondition: `"Precondition violated: {description}"`
- Postcondition: `"Postcondition violated: {description}"`
- Invariant: `"Invariant violated: {description}"`
- Check: `"Check failed: {description}"`

---

## Best Practices

### 1. Lazy Evaluation
✅ **Always use lambda expressions** for conditions:
```csharp
// ✅ CORRECT - Lazy evaluation
Contract.Require("x positive", () => x > 0);

// ❌ WRONG - Eager evaluation (always evaluated!)
Contract.Require("x positive", x > 0);  // Compilation error
```

### 2. Parameter Validation
✅ **Parameter validation always runs** (even when DBC is disabled):
```csharp
Contract.Require(null, () => true);  // ✅ Throws ArgumentNullException
Contract.Require("test", null);      // ✅ Throws ArgumentNullException
```

Two exceptions. `EnsureAssignable<T>()` never rejects a `null` `actual` or `expected`: it compares `null` as a value, and returns without comparing with postconditions off or while another contract check is running; its pattern array is always checked. `EnsureImmutableCollection<T>()` checks `collection` for `null` only with postconditions on and not while another contract check is running.

### 3. Generic Constraints
✅ **Use correct constraints**:
```csharp
// ✅ RequireNotNull/EnsureNotNull have constraint
Contract.RequireNotNull<string>("Name", name);  // ✅ Works

// ✅ Old<T> and EnsureAssignable<T> have NO constraint
var oldValue = Contract.Old(() => someValueType);  // ✅ Works with value types
```

### 4. Invariant Checking Pattern
✅ **Check invariants at construction and after public methods**:
```csharp
public class BankAccount
{
    public BankAccount(decimal initialBalance)
    {
        _balance = initialBalance;
        CheckInvariant();  // ✅ Check after construction
    }

    public void Withdraw(decimal amount)
    {
        _balance -= amount;
        CheckInvariant();  // ✅ Check after modification
    }

    private void CheckInvariant()
    {
        Contract.Invariant("Balance non-negative", () => _balance >= 0);
    }
}
```

### 5. State Capture
✅ **Capture state BEFORE modification**:
```csharp
// ✅ CORRECT
var oldBalance = Contract.Old(() => _balance);
_balance -= amount;
Contract.Ensure("Balance decreased", () => _balance < oldBalance);

// ❌ WRONG - Captures state AFTER modification
_balance -= amount;
var oldBalance = Contract.Old(() => _balance);  // ❌ Too late!
```

---

## Performance Considerations

### When Contracts Are Disabled
- ✅ **Zero overhead**: Conditions never evaluated (except `Ignore`, which evaluates its condition and returns the result; called from inside another contract's condition, it returns `false` without evaluating)
- ✅ **No allocations**: Lambda expressions not invoked (except by `Ignore`, as above)
- ✅ **No exceptions**: No contract violations thrown
- ⚠️ **Parameter validation still runs**: Always validated for safety, except `actual` and `expected` of `EnsureAssignable<T>()` and `collection` of `EnsureImmutableCollection<T>()`

### When Contracts Are Enabled
- ⚠️ **Condition evaluation cost**: Lambda expressions are invoked
- ⚠️ **Recursion guard overhead**: AsyncLocal access (minimal)
- ⚠️ **Old<T>() copy cost**: reflection deep copy of everything reachable from the value
- ⚠️ **EnsureAssignable<T>() reflection cost**: Metadata cached, but comparison is expensive

### Optimization Tips
1. **Disable in production if needed**: Contracts are on by default; set `DBC=off` (or a per-type flag such as `DBC_POST=off`) to opt out
2. **Minimize Old<T>() usage**: Capture only what you need
3. **Use specific assertions**: `RequireNotNull` is faster than `Require(() => x != null)`
4. **Cache invariant results**: If invariant checks are expensive, cache the result

---

## Types that `Old<T>()` shares

`Old<T>()` copies everything reachable from the value, except an instance of one of the types below. It returns such an instance itself, not a copy, at the top level and behind every field that refers to one. State inside a shared instance is not snapshotted. There are two groups.

**Shared, and its state can change.** `EnsureAssignable<T>()` throws `InvalidOperationException` when one such instance is passed as `actual` and as `expected`.

- `Stream`
- `Task`
- `Lazy<T>`, `ThreadLocal<T>`, `AsyncLocal<T>`
- `CancellationTokenSource`
- `WeakReference` and `WeakReference<T>`
- `ConditionalWeakTable<TKey, TValue>`
- `Component`
- `HttpClient` and `HttpMessageHandler`
- `Socket`
- `Thread`, `Timer` (`System.Threading.Timer`), `SynchronizationContext`
- The locks and signals: `WaitHandle`, `SemaphoreSlim`, `ManualResetEventSlim`, `CountdownEvent`, `ReaderWriterLockSlim`, `Barrier`, `System.Threading.Lock`
- Handles: `SafeHandle`, `CriticalHandle` and every other `CriticalFinalizerObject`
- COM objects and `ComObject`
- Every type derived from the types above

**Shared, and fixed.** `EnsureAssignable<T>()` does not throw that exception for them.

- `string`
- Delegates (`Delegate`)
- `Type`, `MemberInfo`, `Assembly`, `Module`, `Pointer`
- `Regex`
- Every type derived from the types above
- The frozen collections: a `FrozenSet<T>`, a `FrozenDictionary<TKey, TValue>` and every other class declared in `System.Collections.Frozen`. A value type declared in `System.Collections.Frozen` is copied, and so is an array whose element type is declared in `System.Collections.Frozen`
- The comparers: those that the base class library itself declares; a class of yours derived from `StringComparer` or `Comparer<T>` is copied

The target of a delegate is not compared. A `FrozenSet<T>` or a `FrozenDictionary<TKey, TValue>` in the result of `Old<T>()` is the original's own set or dictionary, so its elements, keys and values are the original objects, as they are when the comparison runs and not as they were when `Old<T>()` ran; what follows from that is below, under **Elements are shared** in the list after the paragraph that begins "Two instances of a `FrozenSet<T>` or a `FrozenDictionary<TKey, TValue>`".

**An array, a `List<T>`, a `Stack<T>` or a `Queue<T>` whose elements are shared instances** is copied itself; each element keeps its own rule. The result of `Old<T>()` for a `FrozenSet<T>[]`, a `List<FrozenSet<T>>`, a `Stack<FrozenSet<T>>` or a `Queue<FrozenSet<T>>` is another array or collection, so replacing an element afterwards does not reach it, and each set in it is the original's own set, with the limits under **Elements are shared** below. The same holds for an array of a class of yours declared in `System.Collections.Frozen`: the array is copied, and its elements are shared. A value of a value type declared in `System.Collections.Frozen` is copied like a value of any other value type, also when a member declared as `object` holds it in a box: the result of `Old<T>()` holds another box. A member declared as `IEnumerator<T>` that holds the enumerator of a `FrozenSet<T>` holds such a box: calling `MoveNext()` on the original after `Old<T>()` is a difference of that member, so list the member as assignable where the enumerator may move. A change inside an element of the set can also be a difference of a member that holds an enumerator of that set, although the enumerator did not move. Measured on .NET 8, for an object that holds a `FrozenSet<T>` of a class and an enumerator of that set in a member declared as `FrozenSet<T>.Enumerator` or as `IEnumerator<T>`: after a field of an element changed, `EnsureAssignable<T>()` reported a violation that names the enumerator member, where the same change with no enumerator member gives no exception (**Elements are shared** below) and the enumerator member with no change gives no exception. The result depends on the runtime. Keep no enumerator of a frozen collection in the state that is compared, or, when `EnsureAssignable<T>()` compares the members of the type that holds it, list that member as assignable.

**The copy that `Old<T>()` makes of a dictionary whose keys are arrays, with its default comparer,** cannot be looked up by key; the original still finds its keys. `Old<T>()` copies each key and calls no `GetHashCode`: an entry of the copy keeps the hash code that was computed for the original key, and holds another array as its key. The default comparer hashes and compares an array by its instance. So a lookup with an original array meets the entry's hash code but no equal key, and a lookup with a key taken from the copy's own `Keys` has another hash code: `ContainsKey` is false for both, for a `Dictionary<int[], TValue>` and for a `Dictionary<FrozenSet<T>[], TValue>`. For a `Dictionary<int[], TValue>` built with a comparer of yours that hashes and compares arrays by content, both lookups succeed. A `HashSet<int[]>` with its default comparer misses in the same two ways with `Contains`. `Count` and enumeration still work: read the old dictionary by enumerating it. The same miss is seen for a key of a class without a `GetHashCode` of its own, looked up with the copy's own key. A key of a type that `Old<T>()` shares (a `FrozenSet<T>`) is the original object, and the lookup with it succeeds.

**One instance passed as `actual` and as `expected`.** For the first group there is no earlier state to compare with, so `EnsureAssignable<T>()` compares no member and throws `InvalidOperationException`. The message names the shared base type, or says "a COM object". This holds in every kind of build, whatever `T` is, `object` included, and whatever the assignable patterns are. The rule looks only at the two values passed to the method: an instance of such a type that an array or a member holds on both sides is compared with itself and is equal, whatever changed inside it, so `EnsureAssignable(_streams, Contract.Old(() => _streams))` for a `Stream[]` does not throw and does not see what was written to a stream. Two different instances of such a type as `T` are still compared member by member. With postconditions off, or in a call made while another contract check is running, the method returns without comparing, as in every other case.

**A class that derives from a type of the first group** is shared too, so `Old(() => this)` in such a class returns `this`, and `EnsureAssignable(this, Contract.Old(() => this))` throws. Check each state member with `Contract.Ensure` and a value taken with `Old`:

```csharp
public class Worker : Component
{
    private int _processed;

    public void Process()
    {
        var oldProcessed = Contract.Old(() => _processed);

        _processed++;

        Contract.Ensure("One more processed", () => _processed == oldProcessed + 1);
    }
}
```

**A member of a type of the first group**, for example `CancellationTokenSource? _cts`: `EnsureAssignable(_cts, Contract.Old(() => _cts))` throws when the member was not replaced, because both sides are then the same instance. It passes when the member stays `null`, and it compares the members that the declared type shows when the member was replaced by another instance. To check that the member was not replaced, use `Contract.Ensure` with `ReferenceEquals`:

```csharp
var oldCts = Contract.Old(() => _cts);

// ...

Contract.Ensure("Token source not replaced", () => ReferenceEquals(_cts, oldCts));
```

Inside a compared object, a member or element that holds a shared instance is compared by reference; a `string` is compared with `Equals` and a delegate by its methods; for a `FrozenSet<T>` or a `FrozenDictionary<TKey, TValue>` see the paragraph below. Two instances of one other class declared in `System.Collections.Frozen` (two instances of a class of yours declared there) are still compared by reference when a member or element holds them. A value type declared in `System.Collections.Frozen` and an array whose element type is declared in `System.Collections.Frozen` are not shared: two values of such a value type are compared like two values of any other value type, and two such arrays like any other two arrays (see **Two arrays** below), each element by its own rule.

**Two instances of a `FrozenSet<T>` or a `FrozenDictionary<TKey, TValue>`** are compared element by element, in enumeration order (a dictionary entry by entry, key with key and value with value), whenever a member or element holds them (the runtime types decide, not the declared type of the member), and when `T` itself is one of the two types. This is said of the framework's own two types; for a frozen set of a second copy of the assembly `System.Collections.Immutable`, loaded into another `AssemblyLoadContext`, see the limit **`Old<T>()` and `EnsureAssignable<T>()` use other criteria for a frozen collection** under [What `EnsureImmutableCollection<T>()` accepts](#what-ensureimmutablecollectiont-accepts). Know three limits.

- **Order.** Two sets or two dictionaries of equal content can enumerate in different orders, and are then reported as different. The order can depend on the order in which the collection was built (observed for strings) and on the hash codes of the elements (observed: two sets of equal content, each of five or more objects of a class without a `GetHashCode` of its own, enumerate in different orders). These are observations, not guarantees of the runtime: do not rely on the order for any element type.
- **Comparer.** The comparer of the collection is not compared and not used; elements and keys are compared by the rules of `EnsureAssignable<T>()`. Two sets that their own comparer calls equal (a set of `"a"` and a set of `"A"`, both with `StringComparer.OrdinalIgnoreCase`) are reported as different, and two collections with different comparers and the same elements in the same order are equal.
- **Elements are shared.** `Old<T>()` does not copy the set or dictionary, so the elements, keys and values of the old value are the original objects, as they are when the comparison runs and not as they were when `Old<T>()` ran. A change inside an element is therefore not seen while both sides hold that object at the same place in the enumeration order: when the member was not replaced, and also when it was replaced by a new collection that holds the same object there. After the member is replaced by a collection that holds other objects, each of them is compared with the original object at the same place in the enumeration order, as that object is now. A change can then still be missed (the original object was changed, and the new object has the changed content), and a violation can be reported although the content is as it was when `Old<T>()` ran (the original object was changed, and the new object has the earlier content). The same content in a `List<T>` or a `Dictionary<TKey, TValue>` is copied by `Old<T>()`, and there the change is seen.

The way out for order and comparer: list the member as assignable and state the condition with `Contract.Ensure` and a value taken with `Contract.Old`, as below; `Old<T>()` returns the earlier set itself, which is what the condition needs. (When `T` itself is one of the two types there is no member to list: use `Contract.Ensure` in place of `EnsureAssignable<T>()`.)

The way out for the state of the elements: before the change, take with `Old<T>()` each value that must stay, for example `var oldQuantity = Contract.Old(() => line.Quantity);`, and check it afterwards with `Contract.Ensure("Quantity unchanged", () => line.Quantity == oldQuantity);`; or take the element itself, for example `var oldLine = Contract.Old(() => line);`, and compare it afterwards with `Contract.EnsureAssignable(line, oldLine);`. `Old<T>()` copies an element that is taken on its own, unless the element's own type is one that `Old<T>()` shares (the lists above; a nested `FrozenSet<T>`, for example, is shared again). Taking a value that holds no reference, such as a number (`line.Quantity`), or a `string`, always works: `Old<T>()` copies such a value, and a `string` cannot change.

```csharp
var oldState = Contract.Old(() => this);
var oldTags = Contract.Old(() => _tags);

// ...

Contract.EnsureAssignable(this, oldState, nameof(_tags));
Contract.Ensure("Tags unchanged", () => _tags.SetEquals(oldTags));
```

**Two arrays** are equal only when they have the same rank, and the same length and lower bound in every dimension; then they are compared element by element. This holds whenever a member or element holds them (the runtime types decide, so also in a member declared as `object`, as `System.Array` or as `IEnumerable`), and when `T` itself is an array type or `System.Array`. The copy that `Old<T>()` makes of an array has the same rank, lengths and lower bounds. Know these limits.

- **A lower bound counts.** An array whose lower bound is 1 (one made with `Array.CreateInstance`) is different from an `int[]` of the same elements.
- **The message does not say what differs.** A violation names the member, or says that the two values are not equal; it does not say whether a rank, a length, a lower bound or an element differs.
- **The element type counts only through the elements.** Two arrays of different element types that agree in rank, lengths and lower bounds are compared element by element: an `int[]` `{1, 2}` and an `object[]` `{1, 2}` are equal, and an `int[]` `{1, 2}` and a `long[]` `{1, 2}` are not, because an `int` and a `long` are values of different types.
- **An array and another sequence.** The rule is for two arrays. An array and a sequence that is not an array are compared by what they enumerate, whatever the rank of the array (an array of pointers or of function pointers that has elements is the exception: see **Pointers**). In a member declared as `IEnumerable`, an `int[,]` `{{1, 2}}` equals a `List<int>` `{1, 2}`, and that list equals an `int[]` `{1, 2}`, but the two arrays are different.
- **Pointers.** Two arrays of pointers or of function pointers are compared by address, and also need the same rank, and the same length and lower bound in every dimension. The type that a pointer points to is not compared (an `int*[]` and a `long*[]` that hold the same address are equal), and neither is the memory behind an address. Such an array that has elements is different from anything that is not an array of pointers or of function pointers, also from a `nint[]` that holds the same address. `Old<T>()` copies such an array with its addresses.
- **An array as `T`.** Each element is compared as a member or element is, which is not always how its type is compared as `T`: a `DateTime` element with `Equals`, which ignores `Kind`, where `DateTime` as `T` reports `Kind`; an element by its runtime type, so in an array of a base class a member that only the derived class of an element has counts; two different `Stream`s in a `Stream[]` are different. Two different instances of one class that is neither a shared type nor a sequence are compared by their members, so the getters of their public properties run. This holds wherever the comparison reaches such a pair: as two elements of the arrays, as two elements of a `FrozenSet<T>` that the arrays hold, as two keys or two values of a `FrozenDictionary<TKey, TValue>` that the arrays hold (a dictionary is compared entry by entry, key with key and value with value), or through a field of a value-type element that its `Equals` does not settle. A getter that throws ends the call with `TargetInvocationException`, which holds the getter's exception as `InnerException`. The comparison itself does not read the properties of an object that is one instance on both sides (nothing of it is read), those of a shared instance itself (two different `Stream`s are different without being read; of a `FrozenSet<T>` the elements are compared, and of a `FrozenDictionary<TKey, TValue>` its `Count` and then, when the two counts are equal, the keys and values, not its other properties), those of a sequence itself (a `List<T>` or another collection class as an element is enumerated and its elements are compared by these same rules; a property of it that the enumeration does not use is not read, also one that would throw, except the `Count` of an `IDictionary`, which is read on both sides before any entry: two `IDictionary` values of different counts are different without being enumerated (a collection class that implements only `IDictionary<TKey, TValue>` is enumerated as any other sequence); but enumerating runs the sequence's own code, which for a collection class of yours can include its `Count`, and an exception thrown there, from two sequences of one type, leaves the call as it is, not wrapped in a `TargetInvocationException`) and those of a value type itself (it is compared with `Equals` and then by its fields, and what a field refers to is compared by these same rules). Measured once, in a Debug build: comparing two `byte[]` of 1,000,000 equal elements as `T` allocated 80,781,544 bytes and took about 220 ms; this is one call of one size, not a rate. The assignable patterns do not apply and are not examined: a pattern that is not a valid regular expression, or a `null` in the pattern array, is not reported, while a pattern array that is `null` itself still throws `ArgumentNullException`.
- **Another declared type.** Two arrays passed as `object` or as an interface are not compared as a whole: only the members that `T` shows are compared. `object`, `IList<int>` and `IEnumerable<int>` show nothing of what an array holds: neither a different element nor a different length is reported. `ICollection`, `ICollection<int>` and `IReadOnlyCollection<int>` show `Count`: a different number of elements is reported, as a violation that names `Count`. With `ICollection`, a different element is still not reported, and neither is a different rank or length per dimension when the number of elements is the same (an `int[]` `{1, 2}` against an `int[,]` `{{1, 2}}`, or 1 by 2 against 2 by 1). To have the arrays compared, pass them as their array type or as `System.Array`.

The way out when two arrays that differ in rank, a length or a lower bound must count as equal: list the member as assignable and state the condition with `Contract.Ensure` and a value taken with `Contract.Old`. (When `T` itself is an array type or `System.Array` there is no member to list: use `Contract.Ensure` in place of `EnsureAssignable<T>()`.)

---

## What `EnsureImmutableCollection<T>()` accepts

`EnsureImmutableCollection<T>()` decides from the runtime type of the argument. It reads no element of the collection and no state of the instance, and the declared `T` plays no part: a `FrozenSet<int>` passed as `IReadOnlySet<int>` or as `object` is accepted. The method checks the argument with postconditions on and not while another contract check is running; in a call that does not check, it returns the argument as it is. When it checks, it decides in five steps.

1. **An array is a violation, whatever its element type.** An `ImmutableList<int>[]` and a `FrozenSet<int>[]` are violations.
2. A type that is not an array is accepted when it **implements `IImmutableList<T>`, `IImmutableSet<T>`, `IImmutableDictionary<TKey, TValue>`, `IImmutableStack<T>` or `IImmutableQueue<T>`**. It does not matter whether the type or one of its base classes declares the interface, or whether the implementation is explicit. `ImmutableArray<T>`, `ImmutableList<T>`, `ImmutableHashSet<T>`, `ImmutableSortedSet<T>`, `ImmutableDictionary<TKey, TValue>`, `ImmutableSortedDictionary<TKey, TValue>`, `ImmutableStack<T>` and `ImmutableQueue<T>` are accepted by this condition.
3. A type that is not an array is accepted when it **is or derives from a `FrozenSet<T>` or a `FrozenDictionary<TKey, TValue>`**.
4. A type that is not an array is accepted when it is **a read-only wrapper: a `ReadOnlyCollection<T>`, a `ReadOnlyDictionary<TKey, TValue>`, or a class derived from one of them**. The result of `AsReadOnly()` of a `List<T>` or of a `Dictionary<TKey, TValue>`, the result of `Array.AsReadOnly(...)` and a `ReadOnlyObservableCollection<T>` are accepted by this condition. So are the `ReadOnlySet<T>` of `System.Collections.ObjectModel`, which the framework declares from .NET 9, and a class derived from it (measured on .NET 10).
5. A type that is not an array and meets none of the three conditions is a violation: the method throws `PostconditionViolationException`.

**What is compared.** For the conditions of steps 2 to 4 the method compares full names of generic type definitions, as `Type.FullName` gives them (for example ``System.Collections.Immutable.IImmutableList`1``): that of each generic interface that the runtime type implements with those of the five interfaces, and that of the runtime type and of each of its base classes, where generic, with those of the two frozen types and of the three wrappers. It compares the whole name, ordinally: a name with another namespace does not match, and neither does one that differs only in letter case. It does not compare the assembly that declares the definition. A full name counts only in the place of its condition: a class that has the full name of one of the five interfaces, and a class that implements an interface with the full name of a frozen type or of a wrapper, meet no condition by that. The library is built for .NET 8 and cannot refer to `ReadOnlySet<T>`, so it holds that one full name as text.

**Limits.** A pass is a statement about the runtime type, not a proof that the content cannot change.

- **The runtime type decides, not the content.** An `ImmutableArray<int[]>` is accepted, although each of its elements is an array, whose elements can be replaced.
- **A read-only wrapper is a view.** A `ReadOnlyCollection<T>` or a `ReadOnlyDictionary<TKey, TValue>` is accepted whatever it wraps: the method does not look at the wrapped collection, which may be one that its owner can still change. A class derived from a wrapper is accepted too, also one that adds a method that changes the wrapped list.
- **An interface is believed.** A type that is not an array and implements one of the five interfaces is accepted whatever its members do: the method calls none of them.
- **Read-only is not enough.** A collection type of yours whose `IsReadOnly` is true and that meets none of the three conditions is a violation, and so are a `ReadOnlyMemory<T>` and a `ReadOnlySequence<T>`. Measured on .NET 8, these are violations too: what `ArrayList.ReadOnly(...)` returns, a collection expression assigned to an `IReadOnlyList<int>` or to an `IEnumerable<int>`, `Enumerable.Empty<int>()` and the result of `Enumerable.Select`.
- **Keys and values differ by collection.** The `Keys` and the `Values` of a `FrozenDictionary<TKey, TValue>` and the `Items` of a `FrozenSet<T>` are `ImmutableArray<T>` values, and are accepted. Measured on .NET 8, the `Keys` and the `Values` of an `ImmutableDictionary<int, int>`, of an `ImmutableSortedDictionary<int, int>` and of a `ReadOnlyDictionary<int, int>` are violations.
- **An immutable type that the rule does not name is a violation.** A collection type of another library or language, or of yours, that is not an array and meets none of the three conditions is reported as mutable, whatever it lets a caller do.
- **The instance is not examined.** `default(ImmutableArray<int>)` is accepted.
- **A type that only has the name is accepted.** The method compares full names and not assemblies. So a type of another assembly that is not an array is accepted when it is or derives from a generic type whose definition has the full name of one of the two frozen types or of the three wrappers, or when it implements a generic interface whose definition has the full name of one of the five interfaces. The case this is for is a second copy of the assembly `System.Collections.Immutable`, which declares the immutable and the frozen collections, loaded into another `AssemblyLoadContext`: an `ImmutableList<int>.Empty` and a `FrozenSet<int>.Empty` of such a copy are accepted. A type that merely carries such a name is accepted with it: measured on .NET 8, a class of a dynamically built assembly whose full name is that of `ReadOnlyCollection<T>` or of `ReadOnlyDictionary<TKey, TValue>` is accepted.
- **The version of an assembly is not compared.** The method reads no version, so a type of a copy of another version is judged by the same full names. This is read from the rule; it was not measured.
- **A call that passes can allocate more than in 2.0.0.** Measured on .NET 8 under the JIT, in bytes allocated per call: 312 for an `ImmutableList<int>`, 192 for a `ReadOnlyCollection<int>` and 304 for an `ImmutableDictionary<int, int>`, where version 2.0.0 allocated 192 for each of the three. Measured on .NET 8 under Native AOT, for this version only: 1544 for an `ImmutableList<int>`, 192 for a `ReadOnlyCollection<int>`, 1528 for an `ImmutableDictionary<int, int>`, 192 for a `FrozenSet<int>` and 1560 for an `ImmutableArray<int>`; a call with a `List<int>`, a violation, allocated 3552 with its exception. These are measurements, not bounds that the library keeps.
- **`Old<T>()` and `EnsureAssignable<T>()` use other criteria for a frozen collection.** This method accepts a `FrozenSet<int>` of a second copy of the assembly `System.Collections.Immutable`, loaded into another `AssemblyLoadContext`, by a full name. `Old<T>()` tests for a class declared in the namespace `System.Collections.Frozen`, and the comparison of `EnsureAssignable<T>()` for the two types of the framework. Measured on .NET 8: `Old<T>()` shares such a set that a member holds, as it shares a set of the framework; two objects that hold two such sets of equal elements are a violation in `EnsureAssignable<T>()`, where two sets of the framework of equal elements pass; one such set held on both sides passes. The way out is the one for order and comparer under [Types that `Old<T>()` shares](#types-that-oldt-shares): list the member as assignable and state the condition with `Contract.Ensure`. Measured on .NET 8: with the member listed, two objects that hold two such sets pass, whether the elements of the sets are equal or different.

**Ways out** for a call that is a violation:

- Return an immutable or a frozen collection: `ToImmutableArray()`, `ToImmutableList()` or `ToFrozenSet()`, for example; from a builder, `ToImmutable()`.
- Return a read-only wrapper: `AsReadOnly()` of a `List<T>` or of a `Dictionary<TKey, TValue>`, or `Array.AsReadOnly(...)`. This gives a view (see **A read-only wrapper is a view** above).
- For a collection type of yours that is immutable: implement the one of the five interfaces that fits, or derive from `ReadOnlyCollection<T>` or from `ReadOnlyDictionary<TKey, TValue>`.
- For a value that is not a collection (a `ReadOnlyMemory<T>`, a `ReadOnlySequence<T>`): remove the call, or state the property that is meant with `Contract.Ensure` or `Contract.EnsureResult`.

**Trimmed and Native AOT applications.** Seven checks of the smoke program of the repository, which is built for .NET 8, call the method with the arguments named here. Published with Native AOT, and published trimmed without Native AOT, the program got these answers: an array of immutable lists, an array of function pointers and the builder of an immutable list are violations; a frozen set of strings, a class of the program that implements `IImmutableStack<T>`, a `ReadOnlyCollection<int>` and a class of the program that has the full name of `ReadOnlySet<T>` are accepted.

---

## Trimming and Native AOT

`Old<T>()` and `EnsureAssignable<T>()` have limited support in trimmed and Native AOT applications. `DBC_POST=off` turns off every postcondition check, these two helpers included. With it, `Old<T>()` returns `default` and `EnsureAssignable<T>()` returns without comparing, so the documented `Old` + `EnsureAssignable` pair does nothing.

- `Old<T>()` copies field by field with reflection and needs no JSON setup. A field that the trimmer removed from reflection keeps its bitwise value, so the object it refers to is shared with the original. Preserve such types with `[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(X))]` on `Main` or any method that runs. In every kind of build, an instance of a type that `Old<T>()` shares is returned itself, not copied; see [Types that `Old<T>()` shares](#types-that-oldt-shares). A caller that forwards its own generic parameter to it gets trim warning `IL2091` unless it carries `[RequiresUnreferencedCode]` or the same `[DynamicallyAccessedMembers]` annotation on that parameter.
- `EnsureAssignable<T>()` compares the members of `T` in every kind of build, except when `T` is a `string`, a delegate type, a nullable value type, a `FrozenSet<T>` or a `FrozenDictionary<TKey, TValue>`, or an array type or `System.Array`: those are compared as a whole (a `string` with `Equals`, a delegate by its methods, a nullable value type with `Equals` and then by its fields, a `FrozenSet<T>` or a `FrozenDictionary<TKey, TValue>` element by element, in enumeration order, two arrays element by element when they have the same rank, and the same length and lower bound in every dimension), and nothing needs to be preserved for `T` = `string`, `int?` or `DateTime?`. For an array type or `System.Array` as `T`, the members of the type of the elements may not be preserved: preserve that type with `DynamicDependency`. `T` = `object` compares nothing beyond `null`; a base class that is not compared as a whole (`System.Array`, `Delegate`, `MulticastDelegate`, a `FrozenSet<T>` and a `FrozenDictionary<TKey, TValue>` are) compares the members it declares or inherits, an interface only its own properties. One instance of a type that `Old<T>()` shares, and its state can change (a `Stream`, a `Task`, a class derived from `Component`), passed as `actual` and as `expected` is not compared: the method throws `InvalidOperationException` in every kind of build, whatever `T` is, `object` included. Check each state member with `Contract.Ensure` and a value taken with `Old`; to check only that a reference was not replaced, use `Contract.Ensure` with `ReferenceEquals`. See [Types that `Old<T>()` shares](#types-that-oldt-shares). A caller that forwards its own generic parameter to it gets trim warning `IL2091` until that parameter has the same `[DynamicallyAccessedMembers]` annotation.
- Under Native AOT, when it reaches a nested class or collection element whose members are not preserved, it asks that class's `Equals`: `true` means equal, `false` throws `InvalidOperationException`. List the top-level member as assignable (plain member name; patterns are regular expressions matched against top-level member names) or preserve the type with `DynamicDependency` (below a nullable value type as `T` there is no member to list: preserve the type, or test both values for `null` and pass the unwrapped values; below a `FrozenSet<T>` or a `FrozenDictionary<TKey, TValue>` as `T` there is no member to list either: preserve the type; below an array type or `System.Array` as `T` there is no member to list either: preserve the type, which the message names with a path from the parameter name, `actual[]` for an element of the array).
- Blind spot: a nested class whose `Equals` ignores state (for example entity equality by ID) compares equal when it has no visible members, so a change to it passes silently. Preserve the type with `DynamicDependency` to compare it member by member.
- A member or element of a value type, and a nullable value type as `T`, is equal when its `Equals` says so; otherwise its visible fields are compared. Two arrays that a member or element holds, or that are passed as an array type or `System.Array`, are equal only when they have the same rank, and the same length and lower bound in every dimension; then they are compared element by element, and two arrays of pointers or of function pointers by address (see **Two arrays** under [Types that `Old<T>()` shares](#types-that-oldt-shares)). Types that `Old<T>()` shares (`Lazy<T>`, `Task`, streams and similar) are compared by reference when a member or element holds them, except that two instances of a `FrozenSet<T>` or a `FrozenDictionary<TKey, TValue>` are compared element by element, in enumeration order (two instances of one other class declared in `System.Collections.Frozen` are still compared by reference; a value type declared in `System.Collections.Frozen` and an array whose element type is declared in `System.Collections.Frozen` are not shared); as `T`, two different instances are still compared member by member (a `string`, a delegate type, and a `FrozenSet<T>` or a `FrozenDictionary<TKey, TValue>` excepted: those are compared as a whole), and so is a type that is shared, and fixed (a `Regex`), also for one instance on both sides. One instance of a type that is shared, and its state can change, on both sides is not compared: the method throws, as said above. Under Native AOT, for a nullable value type as `T`, the fields of the underlying type may not be preserved: for a nullable struct that holds a reference, the comparison can then report a difference for an unchanged value or, with an `Old` snapshot, miss a change inside the object it refers to. Preserve the struct with `DynamicDependency`, or test both values for `null` and pass the unwrapped values.
- Assignable patterns are unanchored regular expressions: `Customer` also exempts `CustomerId`. To exempt exactly one member, anchor the pattern. An auto-property declared on the compared type is also compared as its backing field, so the pattern must cover that too: `^(Customer|<Customer>k__BackingField)$`. An inherited auto-property is compared only as the property, so `^Customer$` is enough. The patterns do not apply, and are not examined, when exactly one of the two values is `null`, when `T` is a `string`, a delegate type, a nullable value type, a `FrozenSet<T>` or a `FrozenDictionary<TKey, TValue>`, or an array type or `System.Array`, or when one instance of a type that `Old<T>()` shares, and its state can change, is passed as `actual` and as `expected`.
- In a trimmed app without Native AOT, a nested type whose property getter was removed by the trimmer makes it throw `InvalidOperationException` naming the property ("… no get method is visible through the compared type"); the same two ways out apply (below a nullable value type as `T` there is no member to list: preserve the type, or pass the unwrapped values; below a `FrozenSet<T>` or a `FrozenDictionary<TKey, TValue>` as `T`, or below an array type or `System.Array` as `T`, there is no member to list either: preserve the type).
- Not detected: a type with only some members preserved, and, in a trimmed app without Native AOT, a nested type whose members were removed entirely. A `null` member, the same instance on both sides, or an empty collection is not inspected.

See [Trimming and Native AOT](../../README.md#trimming-and-native-aot) in the README for details and an example.

---

## See Also

- [Usage Examples](USAGE_EXAMPLES.md) - Practical examples for common scenarios
- [DDD Integration Guide](../DDD_INTEGRATION_GUIDE.md) - Using uContract with Domain-Driven Design
- [Architecture Decision Records](../adr/) - Design decisions and rationale
- [README.md](../../README.md) - Project overview and quick start
