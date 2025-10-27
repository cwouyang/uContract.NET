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

| Method(s) | Environment Variable | Default (Debug) | Default (Release) |
|-----------|---------------------|-----------------|-------------------|
| `Require`, `RequireNotNull`, `RequireNotEmpty`, `Ignore` | `DBC_PRE` | `on` | `off` |
| `Ensure`, `EnsureNotNull`, `EnsureResult`, `EnsureImmutableCollection`, `EnsureAssignable`, `Old` | `DBC_POST` | `on` | `off` |
| `Invariant`, `InvariantNotNull` | `DBC_INV` | `on` | `off` |
| `Check` | `DBC_CHECK` | `on` | `off` |
| **Master switch** | `DBC` | `on` | `off` |

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
1. **Disable in production**: Set `DBC=off` in Release builds (default)
2. **Minimize Old<T>() usage**: Capture only what you need
3. **Use specific assertions**: `RequireNotNull` is faster than `Require(() => x != null)`
4. **Cache invariant results**: If invariant checks are expensive, cache the result

---

## See Also

- [Usage Examples](USAGE_EXAMPLES.md) - Practical examples for common scenarios
- [DDD Integration Guide](../DDD_INTEGRATION_GUIDE.md) - Using uContract with Domain-Driven Design
- [Architecture Decision Records](../adr/) - Design decisions and rationale
- [README.md](../../README.md) - Project overview and quick start
