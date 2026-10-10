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
- The frozen collections: the types declared in `System.Collections.Frozen`
- The comparers: those that the base class library itself declares; a class of yours derived from `StringComparer` or `Comparer<T>` is copied

The target of a delegate is not compared. A `FrozenSet<T>` or a `FrozenDictionary<TKey, TValue>` in the result of `Old<T>()` is the original's own set or dictionary, so its elements, keys and values are the original objects, as they are when the comparison runs and not as they were when `Old<T>()` ran; what follows from that is in the last paragraph of this section.

**One instance passed as `actual` and as `expected`.** For the first group there is no earlier state to compare with, so `EnsureAssignable<T>()` compares no member and throws `InvalidOperationException`. The message names the shared base type, or says "a COM object". This holds in every kind of build, whatever `T` is, `object` included, and whatever the assignable patterns are. Two different instances of such a type as `T` are still compared member by member. With postconditions off, or in a call made while another contract check is running, the method returns without comparing, as in every other case.

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

Inside a compared object, a member or element that holds a shared instance is compared by reference; a `string` is compared with `Equals` and a delegate by its methods, and two instances of a `FrozenSet<T>` or a `FrozenDictionary<TKey, TValue>` are compared element by element, in enumeration order (a dictionary entry by entry, key with key and value with value). Any other type declared in `System.Collections.Frozen` (an array of `FrozenSet<T>`, a class of yours declared there) is still compared by reference when a member or element holds it.

**Two instances of a `FrozenSet<T>` or a `FrozenDictionary<TKey, TValue>`** are compared element by element, in enumeration order, whenever a member or element holds them (the runtime types decide, not the declared type of the member), and when `T` itself is one of the two types. Know three limits. Order: two sets or two dictionaries of equal content can enumerate in different orders, and are then reported as different. The order can depend on the order in which the collection was built (observed for strings) and on the hash codes of the elements (observed: two sets of equal content, each of five or more objects of a class without a `GetHashCode` of its own, enumerate in different orders). These are observations, not guarantees of the runtime: do not rely on the order for any element type. Comparer: the comparer of the collection is not compared and not used; elements and keys are compared by the rules of `EnsureAssignable<T>()`. Two sets that their own comparer calls equal (a set of `"a"` and a set of `"A"`, both with `StringComparer.OrdinalIgnoreCase`) are reported as different, and two collections with different comparers and the same elements in the same order are equal. Elements are shared: `Old<T>()` does not copy the set or dictionary, so the elements, keys and values of the old value are the original objects, as they are when the comparison runs and not as they were when `Old<T>()` ran. A change inside an element is therefore not seen while both sides hold that object at the same place in the enumeration order: when the member was not replaced, and also when it was replaced by a new collection that holds the same object there. After the member is replaced by a collection that holds other objects, each of them is compared with the original object at the same place in the enumeration order, as that object is now. A change can then still be missed (the original object was changed, and the new object has the changed content), and a violation can be reported although the content is as it was when `Old<T>()` ran (the original object was changed, and the new object has the earlier content). The same content in a `List<T>` or a `Dictionary<TKey, TValue>` is copied by `Old<T>()`, and there the change is seen. The way out for order and comparer: list the member as assignable and state the condition with `Contract.Ensure` and a value taken with `Contract.Old`, as below; `Old<T>()` returns the earlier set itself, which is what the condition needs. (When `T` itself is one of the two types there is no member to list: use `Contract.Ensure` in place of `EnsureAssignable<T>()`.) The way out for the state of the elements: before the change, take each value that must stay with `Old<T>()` (a value, not the element object), for example `var oldQuantity = Contract.Old(() => line.Quantity);`, and check it afterwards with `Contract.Ensure("Quantity unchanged", () => line.Quantity == oldQuantity);`.

```csharp
var oldState = Contract.Old(() => this);
var oldTags = Contract.Old(() => _tags);

// ...

Contract.EnsureAssignable(this, oldState, nameof(_tags));
Contract.Ensure("Tags unchanged", () => _tags.SetEquals(oldTags));
```

---

## Trimming and Native AOT

`Old<T>()` and `EnsureAssignable<T>()` have limited support in trimmed and Native AOT applications. `DBC_POST=off` turns off every postcondition check, these two helpers included. With it, `Old<T>()` returns `default` and `EnsureAssignable<T>()` returns without comparing, so the documented `Old` + `EnsureAssignable` pair does nothing.

- `Old<T>()` copies field by field with reflection and needs no JSON setup. A field that the trimmer removed from reflection keeps its bitwise value, so the object it refers to is shared with the original. Preserve such types with `[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(X))]` on `Main` or any method that runs. In every kind of build, an instance of a type that `Old<T>()` shares is returned itself, not copied; see [Types that `Old<T>()` shares](#types-that-oldt-shares). A caller that forwards its own generic parameter to it gets trim warning `IL2091` unless it carries `[RequiresUnreferencedCode]` or the same `[DynamicallyAccessedMembers]` annotation on that parameter.
- `EnsureAssignable<T>()` compares the members of `T` in every kind of build, except when `T` is a `string`, a delegate type, a nullable value type, or a `FrozenSet<T>` or a `FrozenDictionary<TKey, TValue>`: those are compared as a whole (a `string` with `Equals`, a delegate by its methods, a nullable value type with `Equals` and then by its fields, a `FrozenSet<T>` or a `FrozenDictionary<TKey, TValue>` element by element, in enumeration order), and nothing needs to be preserved for `T` = `string`, `int?` or `DateTime?`. `T` = `object` compares nothing beyond `null`; a base class compares the members it declares or inherits, an interface only its own properties. One instance of a type that `Old<T>()` shares, and its state can change (a `Stream`, a `Task`, a class derived from `Component`), passed as `actual` and as `expected` is not compared: the method throws `InvalidOperationException` in every kind of build, whatever `T` is, `object` included. Check each state member with `Contract.Ensure` and a value taken with `Old`; to check only that a reference was not replaced, use `Contract.Ensure` with `ReferenceEquals`. See [Types that `Old<T>()` shares](#types-that-oldt-shares). A caller that forwards its own generic parameter to it gets trim warning `IL2091` until that parameter has the same `[DynamicallyAccessedMembers]` annotation.
- Under Native AOT, when it reaches a nested class or collection element whose members are not preserved, it asks that class's `Equals`: `true` means equal, `false` throws `InvalidOperationException`. List the top-level member as assignable (plain member name; patterns are regular expressions matched against top-level member names) or preserve the type with `DynamicDependency` (below a nullable value type as `T` there is no member to list: preserve the type, or test both values for `null` and pass the unwrapped values; below a `FrozenSet<T>` or a `FrozenDictionary<TKey, TValue>` as `T` there is no member to list either: preserve the type).
- Blind spot: a nested class whose `Equals` ignores state (for example entity equality by ID) compares equal when it has no visible members, so a change to it passes silently. Preserve the type with `DynamicDependency` to compare it member by member.
- A member or element of a value type, and a nullable value type as `T`, is equal when its `Equals` says so; otherwise its visible fields are compared. Types that `Old<T>()` shares (`Lazy<T>`, `Task`, streams and similar) are compared by reference when a member or element holds them, except that two instances of a `FrozenSet<T>` or a `FrozenDictionary<TKey, TValue>` are compared element by element, in enumeration order (any other type declared in `System.Collections.Frozen` is still compared by reference); as `T`, two different instances are still compared member by member (a `string`, a delegate type, and a `FrozenSet<T>` or a `FrozenDictionary<TKey, TValue>` excepted: those are compared as a whole), and so is a type that is shared, and fixed (a `Regex`), also for one instance on both sides. One instance of a type that is shared, and its state can change, on both sides is not compared: the method throws, as said above. Under Native AOT, for a nullable value type as `T`, the fields of the underlying type may not be preserved: for a nullable struct that holds a reference, the comparison can then report a difference for an unchanged value or, with an `Old` snapshot, miss a change inside the object it refers to. Preserve the struct with `DynamicDependency`, or test both values for `null` and pass the unwrapped values.
- Assignable patterns are unanchored regular expressions: `Customer` also exempts `CustomerId`. To exempt exactly one member, anchor the pattern. An auto-property declared on the compared type is also compared as its backing field, so the pattern must cover that too: `^(Customer|<Customer>k__BackingField)$`. An inherited auto-property is compared only as the property, so `^Customer$` is enough. The patterns do not apply, and are not examined, when exactly one of the two values is `null`, when `T` is a `string`, a delegate type, a nullable value type, or a `FrozenSet<T>` or a `FrozenDictionary<TKey, TValue>`, or when one instance of a type that `Old<T>()` shares, and its state can change, is passed as `actual` and as `expected`.
- In a trimmed app without Native AOT, a nested type whose property getter was removed by the trimmer makes it throw `InvalidOperationException` naming the property ("… no get method is visible through the compared type"); the same two ways out apply (below a nullable value type as `T` there is no member to list: preserve the type, or pass the unwrapped values; below a `FrozenSet<T>` or a `FrozenDictionary<TKey, TValue>` as `T` there is no member to list either: preserve the type).
- Not detected: a type with only some members preserved, and, in a trimmed app without Native AOT, a nested type whose members were removed entirely. A `null` member, the same instance on both sides, or an empty collection is not inspected.

See [Trimming and Native AOT](../../README.md#trimming-and-native-aot) in the README for details and an example.

---

## See Also

- [Usage Examples](USAGE_EXAMPLES.md) - Practical examples for common scenarios
- [DDD Integration Guide](../DDD_INTEGRATION_GUIDE.md) - Using uContract with Domain-Driven Design
- [Architecture Decision Records](../adr/) - Design decisions and rationale
- [README.md](../../README.md) - Project overview and quick start
