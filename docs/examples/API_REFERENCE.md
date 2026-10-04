# uContract.NET API Reference

Complete reference for all 16 public methods in uContract.NET.

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
| **Helpers** (6) | `Check`, `Ignore`, `Old`, `Imply`, `IfAndOnlyIf`, `CheckUnsupportedOperation` | Runtime assertions and utilities |

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

**Pure functions (not controlled by environment variables):**
- `Imply`, `IfAndOnlyIf`, `CheckUnsupportedOperation`

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
- `Message` (string): Human-readable error message
- `ContractType` (ContractType enum): Type of contract violated

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
- ✅ **Zero overhead**: Conditions never evaluated
- ✅ **No allocations**: Lambda expressions not invoked
- ✅ **No exceptions**: No contract violations thrown
- ⚠️ **Parameter validation still runs**: Always validated for safety

### When Contracts Are Enabled
- ⚠️ **Condition evaluation cost**: Lambda expressions are invoked
- ⚠️ **Recursion guard overhead**: AsyncLocal access (minimal)
- ⚠️ **Old<T>() serialization cost**: JSON serialization for deep copy
- ⚠️ **EnsureAssignable<T>() reflection cost**: Metadata cached, but comparison is expensive

### Optimization Tips
1. **Disable in production if needed**: Contracts are on by default; set `DBC=off` (or a per-type flag such as `DBC_POST=off`) to opt out
2. **Minimize Old<T>() usage**: Capture only what you need
3. **Use specific assertions**: `RequireNotNull` is faster than `Require(() => x != null)`
4. **Cache invariant results**: If invariant checks are expensive, cache the result

---

## Trimming and Native AOT

`Old<T>()` and `EnsureAssignable<T>()` have limited support in trimmed and Native AOT applications in 2.0.0. `DBC_POST=off` turns off every postcondition check, these two helpers included.

- `Old<T>()` needs reflection-based JSON serialization: set `JsonSerializerIsReflectionEnabledByDefault` to `true`, and under Native AOT also preserve the copied types with `[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(X))]`.
- `EnsureAssignable<T>()` compares the members of `T` in every kind of build. A caller that forwards its own generic parameter to it gets trim warning `IL2091` until that parameter has the same `[DynamicallyAccessedMembers]` annotation.
- Under Native AOT it throws `InvalidOperationException` when it reaches a nested object or collection element whose type's members are not preserved. List the top-level member as assignable (plain member name; patterns are regular expressions matched against top-level member names) or preserve the type with `DynamicDependency`.
- Framework types such as `Uri`, `Exception`, `Version` and `Lazy<T>` are affected too; listing the member is the practical choice.
- Assignable patterns are unanchored regular expressions: `Customer` also exempts `CustomerId`. To exempt exactly one member, anchor the pattern and cover the auto-property's backing field too: `^(Customer|<Customer>k__BackingField)$`.
- In a trimmed app without Native AOT, a nested type whose property getter was removed by the trimmer makes it throw `InvalidOperationException` naming the property ("… it has no get method"); the same two ways out apply.
- Not detected: a type with only some members preserved, and, in a trimmed app without Native AOT, a nested type whose members were removed entirely. A `null` member, the same instance on both sides, or an empty collection is not inspected.

See [Trimming and Native AOT](../../README.md#trimming-and-native-aot) in the README for details and an example.

---

## See Also

- [Usage Examples](USAGE_EXAMPLES.md) - Practical examples for common scenarios
- [DDD Integration Guide](../DDD_INTEGRATION_GUIDE.md) - Using uContract with Domain-Driven Design
- [Architecture Decision Records](../adr/) - Design decisions and rationale
- [README.md](../../README.md) - Project overview and quick start
