# ADR-0003: API Design - Static Class Pattern

## Status

**Accepted**

- **Date**: 2025-10-18
- **Deciders**: Project maintainers
- **Status Date**: 2025-10-18

---

## Context

### Problem Statement

We need to decide the API design pattern for uContract.NET. The primary question is how users will invoke contract methods:
- Static class methods (e.g., `Contract.Require()`)
- Extension methods (e.g., `value.RequireNotNull()`)
- Instance-based with dependency injection
- Fluent interface / method chaining

This decision affects:
- Developer experience (how intuitive the API is)
- Consistency with the Java version
- Testability (ability to mock or substitute implementations)
- Extensibility (how easy it is to add custom contracts)

### Relevant Context

- The Java version uses a static class `Contract` with static methods
- Design by Contract is conceptually a static assertion mechanism
- .NET Code Contracts (deprecated) used static methods: `Contract.Requires()`
- Microsoft.CodeAnalysis uses static methods: `Debug.Assert()`
- Most guard clause libraries (Ardalis.GuardClauses) use extension methods

### Constraints

- Must maintain consistency with Java version's API design
- Should feel natural to .NET developers
- Must support the lazy evaluation pattern (`Func<bool>` lambdas)
- Should not require complex setup or configuration

---

## Decision

**We will use a static class `Contract` with static methods for all contract operations.**

### Details

**API Pattern**:
```csharp
public static class Contract
{
    // Preconditions
    public static void Require(string description, Func<bool> condition);
    public static void RequireNotNull<T>(string description, T? value) where T : class;

    // Postconditions
    public static void Ensure(string description, Func<bool> condition);
    public static void EnsureNotNull<T>(string description, T? value) where T : class;

    // Invariants
    public static void Invariant(string description, Func<bool> condition);

    // Helpers
    public static bool Ignore(string reason, Func<bool> condition);
    public static T Old<T>(Func<T> supplier);  // No constraint - supports both reference and value types
}
```

**Usage Example**:
```csharp
using uContract;

public void ChangeEmail(string newEmail)
{
    Contract.RequireNotNull("Email", newEmail);
    Contract.Require("Valid email", () => newEmail.Contains("@"));

    if (Contract.Ignore("Email unchanged", () => _email == newEmail))
        return;

    var oldState = Contract.Old(() => _state);

    // ... method body ...

    Contract.Ensure("Email updated", () => _email == newEmail);
}
```

---

## Lazy Evaluation Design Principle

### Why `Func<bool>` Instead of `bool`?

A critical design decision in uContract is using **lazy evaluation** (`Func<bool>` delegates) instead of **eager evaluation** (`bool` expressions) for all contract conditions. This ensures **zero performance overhead** when contracts are disabled.

### The Problem with Eager Evaluation

If contract methods accepted `bool` parameters directly:

```csharp
// ❌ EAGER EVALUATION (not used in uContract)
public static void Require(string description, bool condition)
{
    if (!_config.PreconditionsEnabled) return;  // Too late!

    if (!condition)
        throw new PreconditionViolationException(description);
}

// Usage
Contract.Require("x positive", x > 0);  // x > 0 ALWAYS evaluated, even if DBC=off
Contract.Require("Valid state", ExpensiveValidation());  // ALWAYS called!
```

**Problem**: The condition `x > 0` and `ExpensiveValidation()` are **evaluated before** the method is called. Even if `DBC=off` disables all contracts, the CPU still performs these checks. For complex conditions or hot-path code, this creates measurable performance overhead.

### The Solution: Lazy Evaluation

By using `Func<bool>` lambdas, conditions are only evaluated **if and when needed**:

```csharp
// ✅ LAZY EVALUATION (used in uContract)
public static void Require(string description, Func<bool> condition)
{
    if (!_config.PreconditionsEnabled) return;  // Early return, lambda never invoked

    if (!condition())  // Only evaluated if enabled
        throw new PreconditionViolationException(description);
}

// Usage
Contract.Require("x positive", () => x > 0);  // Lambda: condition NOT evaluated yet
Contract.Require("Valid state", () => ExpensiveValidation());  // Function NOT called yet
```

**When `DBC=off`**:
1. Method enters
2. `_config.PreconditionsEnabled` is `false`
3. Early return **without invoking the lambda**
4. **Zero CPU cycles spent** on condition evaluation

### Performance Impact

From the Root Contracting Paper (Section 4.4), performance experiments show:

- **Precondition-only mode**: ~4.5% overhead (minimal)
- **Full DBC mode**: ~33% overhead (invariant checking dominates)
- **DBC disabled (`DBC=off`)**: **0% overhead** (JIT compiler optimizes away the lambda)

The JIT compiler can inline the guard check (`if (!_config.PreconditionsEnabled)`) and eliminate dead code when the configuration is constant. Combined with lazy evaluation, this achieves true zero-overhead abstraction.

### Trade-off: Syntax vs Performance

**Syntax cost**:
```csharp
// Eager (more concise, but wrong)
Contract.Require("x positive", x > 0);

// Lazy (slightly verbose, but correct)
Contract.Require("x positive", () => x > 0);
```

**Performance benefit**: In hot-path code with contracts disabled, the 3-character addition `() =>` eliminates 100% of the evaluation overhead.

**Verdict**: The minimal syntax cost is acceptable for a library designed for production DDD systems where performance matters.

### Why Extension Methods Cannot Support This

Extension methods fundamentally conflict with lazy evaluation:

```csharp
// Hypothetical extension method (CANNOT WORK)
public static T Require<T>(this T value, string description, Func<bool> condition)
{
    // ...
}

// Usage attempt
x.Require("x positive", () => x > 0);
//^
// Problem: x is evaluated BEFORE the method is called
// Cannot prevent eager evaluation of the receiver
```

This is why **static methods** are the only viable pattern for lazy-evaluated contracts.

---

## Consequences

### Positive Consequences

- ✅ **100% Java API parity**: Matches the Java version's design exactly (only casing differs)
- ✅ **Simplicity**: No setup, no configuration, no DI container needed
- ✅ **Clarity**: `Contract.Require()` is explicit and self-documenting
- ✅ **Conventional**: Aligns with established .NET assertion patterns (`Debug.Assert`, `Trace.Assert`)
- ✅ **IDE support**: Static methods have excellent IntelliSense support
- ✅ **Zero overhead**: Static methods can be optimized aggressively by the JIT compiler

### Negative Consequences

- ❌ **Harder to test**: Static methods cannot be mocked (but this is rarely needed for contracts)
- ❌ **Cannot override behavior**: Users cannot substitute alternative implementations
- ❌ **No fluent chaining**: Cannot chain methods like `value.RequireNotNull().RequirePositive()`

### Neutral Consequences

- ⚖️ **Different from guard libraries**: .NET guard clause libraries often use extension methods, but DBC is conceptually different
- ⚖️ **No "this" context**: Extension methods would allow `this.RequireNotNull()`, but this adds no semantic value for DBC

---

## Alternatives Considered

### Alternative 1: Extension Methods

**Description**: Provide extension methods on all types for contract checking

```csharp
public static class ContractExtensions
{
    public static T RequireNotNull<T>(this T value, string description) where T : class;
    public static string RequireNotEmpty(this string value, string description);
}

// Usage
newEmail.RequireNotNull("Email").RequireNotEmpty("Email");
```

**Pros**:
- Fluent, chainable API
- Common pattern in .NET guard clause libraries
- Can feel more natural for null checking

**Cons**:
- **Breaks Java API compatibility**: Completely different usage pattern
- **Pollutes IntelliSense**: Extension methods appear on *every* type
- **Cannot support lazy evaluation**: Extension methods work on values, not lambdas
- **Inconsistent semantics**: `Require("x > 0", () => x > 0)` cannot be an extension method

**Why rejected**: Extension methods fundamentally conflict with lazy evaluation (`Func<bool>` conditions). You cannot write `x.Require(() => x > 0)` — it evaluates `x` before the method is called. This breaks the core design principle of zero overhead when contracts are disabled.

---

### Alternative 2: Instance-Based with Dependency Injection

**Description**: Make `Contract` an injectable service

```csharp
public interface IContractChecker
{
    void Require(string description, Func<bool> condition);
}

public class ContractChecker : IContractChecker { ... }

// Usage
public class MyService
{
    private readonly IContractChecker _contract;

    public MyService(IContractChecker contract)
    {
        _contract = contract;
    }

    public void DoWork(int x)
    {
        _contract.Require("x positive", () => x > 0);
    }
}
```

**Pros**:
- Testable (can inject mock implementation)
- Extensible (users can provide custom implementations)
- Aligns with modern .NET DI patterns

**Cons**:
- **Massive overhead**: Every class needs to inject `IContractChecker`
- **Breaks Java compatibility**: Completely different API
- **Overcomplicated**: DBC is not a service, it's a language-level assertion mechanism
- **Defeats zero-overhead principle**: DI adds runtime overhead

**Why rejected**: Design by Contract is a compile-time/development-time concern, not a runtime service. Treating it as an injectable dependency is architectural overengineering that contradicts the library's philosophy.

---

### Alternative 3: Fluent Builder Pattern

**Description**: Use a fluent interface for building contracts

```csharp
Contract.For(() => x)
    .IsNotNull("x")
    .Satisfies(val => val > 0, "x must be positive");
```

**Pros**:
- Very readable and expressive
- Can provide rich, discoverable API

**Cons**:
- **Too complex**: Requires builder classes, intermediate state
- **Doesn't match Java API**: Completely different style
- **Overhead**: Creates intermediate objects
- **Unclear semantics**: When does the check actually execute?

**Why rejected**: While fluent interfaces are popular in .NET, they add unnecessary complexity for assertion logic. The simple static method pattern is clearer and more performant.

---

## Related Decisions

- **Related to**: ADR-0002 (Namespace `uContract` directly contains the static `Contract` class)
- **Related to**: Decision to maintain Java API compatibility
- **Related to**: ADR-0013 (Static design means no inheritance at Contract class level, aligns with no subcontracting decision)
- **Affects**: All user-facing code examples and documentation

---

## Implementation Notes

- The `Contract` class should be `public static`
- All methods should be `public static`
- No instance state should exist (stateless design)
- Configuration is handled via a separate internal `ContractConfiguration` class (environment variables)
- Thread-local recursion guards are managed via `AsyncLocal<bool>` (internal detail)

**Example .cs file structure**:
```csharp
namespace uContract;

/// <summary>
/// Design by Contract assertions for preconditions, postconditions, and invariants.
/// </summary>
public static class Contract
{
    private static readonly AsyncLocal<bool> _entered = new(() => false);
    private static readonly ContractConfiguration _config = new();

    // Preconditions
    public static void Require(string description, Func<bool> condition) { ... }

    // Postconditions
    public static void Ensure(string description, Func<bool> condition) { ... }

    // Invariants
    public static void Invariant(string description, Func<bool> condition) { ... }
}
```

---

## References

- [Java uContract API](https://gitlab.com/TeddyChen/ucontract/)
- [Microsoft Code Contracts (deprecated)](https://learn.microsoft.com/en-us/dotnet/framework/debug-trace-profile/code-contracts)
- [Design Patterns: Static vs Instance Methods](https://docs.microsoft.com/en-us/dotnet/standard/design-guidelines/static-class)
- [Root Contracting Paper - Section 4.4: Performance](https://www.mdpi.com/2079-9292/14/21/4205) (Lazy evaluation enables zero-overhead abstraction)

---

## Revision History

| Date       | Status      | Notes                          |
|------------|-------------|--------------------------------|
| 2025-10-18 | Accepted    | Decision finalized             |
| 2025-10-29 | Enhanced    | Added explicit lazy evaluation rationale (Section: Lazy Evaluation Design Principle) |
