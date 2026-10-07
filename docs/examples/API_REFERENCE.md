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
- ⚠️ **Parameter validation still runs**: Always validated for safety

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

## Trimming and Native AOT

`Old<T>()` and `EnsureAssignable<T>()` have limited support in trimmed and Native AOT applications. `DBC_POST=off` turns off every postcondition check, these two helpers included. With it, `Old<T>()` returns `default`, so the documented `Old` + `EnsureAssignable` pair throws `ArgumentNullException` ([#47](https://github.com/cwouyang/uContract.NET/issues/47)).

- `Old<T>()` copies field by field with reflection and needs no JSON setup. A field that the trimmer removed from reflection keeps its bitwise value, so the object it refers to is shared with the original. Preserve such types with `[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(X))]` on `Main` or any method that runs. A caller that forwards its own generic parameter to it gets trim warning `IL2091` unless it carries `[RequiresUnreferencedCode]` or the same `[DynamicallyAccessedMembers]` annotation on that parameter.
- `EnsureAssignable<T>()` compares the members of `T` in every kind of build. A caller that forwards its own generic parameter to it gets trim warning `IL2091` until that parameter has the same `[DynamicallyAccessedMembers]` annotation.
- Under Native AOT, when it reaches a nested class or collection element whose members are not preserved, it asks that class's `Equals`: `true` means equal, `false` throws `InvalidOperationException`. List the top-level member as assignable (plain member name; patterns are regular expressions matched against top-level member names) or preserve the type with `DynamicDependency`.
- Blind spot: a nested class whose `Equals` ignores state (for example entity equality by ID) compares equal when it has no visible members, so a change to it passes silently. Preserve the type with `DynamicDependency` to compare it member by member.
- A value type is equal when its `Equals` says so; otherwise its visible fields are compared. Types that `Old<T>()` shares (`Lazy<T>`, `Task`, streams and similar) are compared by reference.
- Assignable patterns are unanchored regular expressions: `Customer` also exempts `CustomerId`. To exempt exactly one member, anchor the pattern. An auto-property declared on the compared type is also compared as its backing field, so the pattern must cover that too: `^(Customer|<Customer>k__BackingField)$`. An inherited auto-property is compared only as the property, so `^Customer$` is enough.
- In a trimmed app without Native AOT, a nested type whose property getter was removed by the trimmer makes it throw `InvalidOperationException` naming the property ("… no get method is visible through the compared type"); the same two ways out apply.
- Not detected: a type with only some members preserved, and, in a trimmed app without Native AOT, a nested type whose members were removed entirely. A `null` member, the same instance on both sides, or an empty collection is not inspected.

See [Trimming and Native AOT](../../README.md#trimming-and-native-aot) in the README for details and an example.

---

## See Also

- [Usage Examples](USAGE_EXAMPLES.md) - Practical examples for common scenarios
- [DDD Integration Guide](../DDD_INTEGRATION_GUIDE.md) - Using uContract with Domain-Driven Design
- [Architecture Decision Records](../adr/) - Design decisions and rationale
- [README.md](../../README.md) - Project overview and quick start
