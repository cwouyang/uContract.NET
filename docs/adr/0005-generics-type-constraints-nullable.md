# ADR-0005: Generics, Type Constraints, and Nullable Reference Types

## Status

**Accepted**

- **Date**: 2025-10-18
- **Deciders**: Project maintainers
- **Status Date**: 2025-10-18

---

## Context

### Problem Statement

We need to decide how to handle:
- **Nullable Reference Types (NRT)**: Should we enable `<Nullable>enable</Nullable>`?
- **Generic constraints**: When should we use `where T : class`, `where T : struct`, etc.?
- **Nullable parameters**: Should `RequireNotNull<T>` accept `T` or `T?`?
- **Value types vs reference types**: How do we handle both consistently?

This decision affects:
- **Type safety**: How well the API prevents null-related bugs at compile time
- **Developer experience**: How clear type signatures are
- **API complexity**: Whether we need separate overloads for different type categories
- **Modern C# compatibility**: Alignment with C# 8+ best practices

### Relevant Context

- .NET 8 and C# 12 strongly encourage nullable reference types
- The Java version uses non-null types by default (Java 17+ with strict null checks)
- Microsoft's own BCL libraries are fully annotated for nullable reference types
- Nullable reference types provide compile-time safety without runtime overhead
- Generic constraints help clarify API contracts and improve IntelliSense

### Constraints

- Must work with .NET 8's nullable reference type system
- Must provide clear compile-time errors for common mistakes
- Should align with modern C# best practices
- Must not overcomplicate the API with excessive overloads

---

## Decision

**We will fully embrace nullable reference types with minimal generic constraints to support both reference types and value types (including struct, record, and record struct).**

### Details

**1. Enable Nullable Reference Types**
```xml
<PropertyGroup>
  <Nullable>enable</Nullable>
</PropertyGroup>
```

**2. Use Generic Constraints Only Where Necessary**

**Reference-type-only methods** use `where T : class`:
```csharp
// Only for reference types (because null check requires reference type)
public static void RequireNotNull<T>(string description, T? value)
    where T : class
{
    if (value is null)
        throw new PreconditionViolationException(description);
}
```

**Type-agnostic methods** have no constraint (support both reference and value types):
```csharp
// Supports class, record, struct, record struct
public static T Old<T>(Func<T> supplier)
{
    // Uses System.Text.Json which supports both reference and value types
}

// Supports all types for field comparison
public static void EnsureAssignable<T>(T actual, T expected, params string[] assignableFields)
{
    // Uses Reflection which works for both reference and value types
}
```

**3. Distinguish Nullable and Non-Nullable Parameters**

- `RequireNotNull<T>(string, T?)` — Explicitly accepts `T?` (nullable reference types only)
- `Require(string, Func<bool>)` — Explicitly non-null `Func<bool>` (not `Func<bool>?`)
- `Ensure(string, Func<bool>)` — Non-null lambda required

**4. Support Both Reference Types and Value Types**

**Reference types** (class, record class):
```csharp
// ✅ Null check for reference types
string? email = GetEmail();
Contract.RequireNotNull("Email", email);  // where T : class

// ✅ Old() captures reference type state
var oldUser = Contract.Old(() => user);  // user is a class or record
```

**Value types** (struct, record struct):
```csharp
// ❌ Compile error: struct cannot use RequireNotNull
// Contract.RequireNotNull("point", point);  // Error: Point (struct) does not satisfy 'where T : class'

// ✅ For nullable value types, use Require with lambda:
int? maybeAge = GetAge();
Contract.Require("Age provided", () => maybeAge.HasValue);

// ✅ Old() supports value types (struct, record struct)
record struct Email(string Value);
var oldEmail = Contract.Old(() => currentEmail);  // Works! No constraint

// ✅ EnsureAssignable() supports value types
record struct Money(decimal Amount, string Currency);
var oldMoney = Contract.Old(() => money);
// ... modify money.Amount ...
Contract.EnsureAssignable(money, oldMoney, "Amount");  // Works! Checks only Amount changed
```

**Complete API Examples**:

```csharp
// ========== Reference Types ==========

// ✅ Nullable reference type with RequireNotNull
string? email = GetEmail();
Contract.RequireNotNull("Email", email);  // T? = string?, where T : class

// ✅ Non-null lambda
Contract.Require("Valid email", () => email.Contains("@"));

// ❌ Compile warning: Passing null to non-nullable parameter
Contract.Require("Test", null);  // Warning: Converting null literal to non-nullable reference

// ✅ Old() captures reference type state
record User(string Name, string Email);
var oldUser = Contract.Old(() => user);  // user is record class

// ========== Value Types ==========

// ✅ Old() captures struct state
record struct Money(decimal Amount, string Currency);
var oldMoney = Contract.Old(() => money);  // Works! No constraint on Old<T>()

// ✅ EnsureAssignable for record struct
public void ChangeAmount(decimal newAmount)
{
    var oldMoney = Contract.Old(() => _money);
    _money = _money with { Amount = newAmount };  // Record struct 'with' expression
    Contract.EnsureAssignable(_money, oldMoney, nameof(Money.Amount));
}

// ✅ For nullable value types, use Require
int? maybeAge = GetAge();
Contract.Require("Age provided", () => maybeAge.HasValue);
Contract.Require("Age valid", () => maybeAge!.Value >= 0);

// ❌ Compile error: struct cannot use RequireNotNull
// Contract.RequireNotNull("point", point);  // Error: Point (struct) doesn't satisfy 'where T : class'
```

**Type Signatures Summary**:

| Method                   | Parameter Type          | Constraint         | Supports Value Types? |
|--------------------------|-------------------------|--------------------|----------------------|
| `Require`                | `Func<bool>`            | None               | N/A (lambda)         |
| `RequireNotNull<T>`      | `T?`                    | `where T : class`  | ❌ No (by design)    |
| `Ensure`                 | `Func<bool>`            | None               | N/A (lambda)         |
| `EnsureNotNull<T>`       | `T?`                    | `where T : class`  | ❌ No (by design)    |
| `Old<T>`                 | `Func<T>`               | **None**           | ✅ **Yes**           |
| `EnsureAssignable<T>`    | `T`, `T`                | **None**           | ✅ **Yes**           |

---

## Consequences

### Positive Consequences

- ✅ **Compile-time null safety**: Developers get warnings for potential null issues
- ✅ **Self-documenting API**: Type signatures clearly show what accepts null
- ✅ **Modern C# alignment**: Matches best practices for C# 8+
- ✅ **Better IntelliSense**: IDE shows nullable annotations, improving discoverability
- ✅ **Prevents common mistakes**: `RequireNotNull("x", null)` is valid, `Require("x", null)` warns
- ✅ **Clear intent**: `T?` parameter means "I expect this might be null and will check"
- ✅ **Value type support**: `Old<T>()` and `EnsureAssignable<T>()` work with struct, record struct
- ✅ **DDD-friendly**: Supports common DDD value objects implemented as record struct

### Negative Consequences

- ❌ **Nullable annotation complexity**: Developers unfamiliar with NRT may find `T?` confusing
- ❌ **Cannot use RequireNotNull with value types**: `RequireNotNull(42)` won't compile (but that's by design!)
- ❌ **Warnings for legacy code**: Code not using nullable reference types may see warnings
- ❌ **Boxing overhead for value types**: `Old<T>()` with struct may cause boxing during serialization

### Neutral Consequences

- ⚖️ **Opt-in null safety**: Projects not using `<Nullable>enable</Nullable>` won't see full benefits
- ⚖️ **Annotation burden**: We must carefully annotate all public APIs, but this is good practice
- ⚖️ **Different constraints per method**: Some methods have `where T : class`, others don't

---

## Alternatives Considered

### Alternative 1: Disable Nullable Reference Types

**Description**: Set `<Nullable>disable</Nullable>` and avoid nullable annotations

**Pros**:
- Simpler type signatures (no `?` annotations)
- Works identically for projects with or without NRT enabled

**Cons**:
- **Misses major C# 8+ feature**: Nullable reference types are a key language improvement
- **Less type safety**: No compile-time null checking
- **Not modern**: Microsoft recommends enabling NRT for all new projects

**Why rejected**: .NET 8 targets modern C#. Disabling NRT would make the library feel outdated and miss opportunities for compile-time safety.

---

### Alternative 2: Use `where T : class` for All Generic Methods

**Description**: Apply `where T : class` constraint to all generic methods including `Old<T>()` and `EnsureAssignable<T>()`

```csharp
public static T Old<T>(Func<T> supplier) where T : class { ... }
public static void EnsureAssignable<T>(T actual, T expected, ...) where T : class { ... }
```

**Pros**:
- Consistent constraint across all generic methods
- Prevents boxing overhead for value types
- Simpler mental model (all generics require reference types)

**Cons**:
- **Blocks DDD value objects**: Cannot use with `record struct` (common in DDD)
- **Loses value type support**: Structs cannot use `Old()` or field comparison
- **Unnecessary limitation**: System.Text.Json and Reflection both support value types
- **Inconsistent with .NET BCL**: `JsonSerializer.Serialize<T>()` has no constraint

**Why rejected**: Modern DDD frequently uses `record struct` for value objects (e.g., `record struct Email(string Value)`, `record struct Money(decimal Amount, string Currency)`). Restricting `Old<T>()` and `EnsureAssignable<T>()` to reference types would make the library less useful for DDD scenarios.

---

### Alternative 3: No Generic Constraints for `RequireNotNull<T>`

**Description**: Allow `RequireNotNull<T>(T value)` without `where T : class`

```csharp
public static void RequireNotNull<T>(string description, T value)
{
    if (value is null) throw ...
}
```

**Pros**:
- Works for both reference types and nullable value types

**Cons**:
- **Confusing for value types**: `RequireNotNull("x", 42)` compiles but is nonsensical
- **Runtime-only check**: `is null` check happens at runtime, not compile time
- **Unclear semantics**: Does it check non-null for value types? (No, because they can't be null)

**Why rejected**: Using `where T : class` makes the API clearer. If you have a nullable value type, use `Require(() => value.HasValue)` instead. `RequireNotNull` is specifically for reference type null checks.

---

### Alternative 4: Separate Methods for Value Types and Reference Types

**Description**: Provide different methods for value types vs reference types

```csharp
// For reference types
public static void RequireNotNull<T>(string description, T? value) where T : class;

// For value types
public static void RequireNotNull<T>(string description, T? value) where T : struct;
```

**Pros**:
- Explicitly supports both value and reference types

**Cons**:
- **Unnecessary**: Value types can't be null (except `T?`), so there's no need to check
- **API bloat**: Doubles the number of methods
- **Confusing**: What does it mean to check if `int` is not null? It never is.

**Why rejected**: YAGNI. For nullable value types (`int?`), users can just use `Require(() => value.HasValue)`. Creating extra methods adds complexity without benefit.

---

### Alternative 5: Accept Both `T` and `T?` Without Distinction

**Description**: Use `T` (non-nullable) for `RequireNotNull`

```csharp
public static void RequireNotNull<T>(string description, T value) where T : class
{
    if (value is null) throw ...
}
```

**Pros**:
- Simpler signature (no `?`)

**Cons**:
- **Misleading intent**: `RequireNotNull` *should* accept potentially null values to check them
- **Defeats NRT purpose**: If the parameter is `T` (non-nullable), why check it?
- **Compiler warnings**: Callers passing `T?` get warnings about nullability mismatch

**Why rejected**: The *purpose* of `RequireNotNull` is to validate potentially null values. The parameter should be `T?` to clearly communicate "I accept null and will validate it".

---

## Related Decisions

- **Related to**: ADR-0001 (.NET 8 fully supports nullable reference types)
- **Related to**: ADR-0006 (System.Text.Json supports both reference and value types for `Old<T>()`)
- **Related to**: ADR-0007 (Reflection supports both reference and value types for `EnsureAssignable<T>()`)
- **Affects**: All public API method signatures
- **Affects**: XML documentation comments must include nullable annotations
- **Affects**: Test coverage requirements (must test both reference and value types)

---

## Implementation Notes

**Project Configuration** (`.csproj`):
```xml
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <Nullable>enable</Nullable>
  <LangVersion>12</LangVersion>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
</PropertyGroup>
```

**Example Implementation**:
```csharp
namespace uContract;

public static class Contract
{
    /// <summary>
    /// Validates that a reference type value is not null.
    /// </summary>
    /// <typeparam name="T">Reference type</typeparam>
    /// <param name="description">Description of the requirement</param>
    /// <param name="value">Value to check (may be null)</param>
    /// <exception cref="PreconditionViolationException">Thrown when value is null</exception>
    public static void RequireNotNull<T>(string description, T? value)
        where T : class
    {
        if (!_config.PreconditionsEnabled) return;

        if (value is null)
            throw new PreconditionViolationException(description);
    }

    /// <summary>
    /// Validates a precondition using a lambda expression.
    /// </summary>
    /// <param name="description">Description of the requirement</param>
    /// <param name="condition">Lazy-evaluated condition (must not be null)</param>
    public static void Require(string description, Func<bool> condition)
    {
        ArgumentNullException.ThrowIfNull(condition);  // Explicit null check for Func

        if (!_config.PreconditionsEnabled) return;

        if (!condition())
            throw new PreconditionViolationException(description);
    }

    /// <summary>
    /// Captures the current state of an object for later comparison in postconditions.
    /// Supports both reference types (class, record) and value types (struct, record struct).
    /// </summary>
    /// <typeparam name="T">Type to capture (no constraint - works for any type)</typeparam>
    /// <param name="supplier">Lambda providing the value to capture</param>
    /// <returns>Deep copy of the captured state</returns>
    public static T Old<T>(Func<T> supplier)
    {
        ArgumentNullException.ThrowIfNull(supplier);

        if (!_config.PostconditionsEnabled) return default(T)!;

        var value = supplier();

        // Deep copy via JSON serialization (works for both reference and value types)
        var options = new JsonSerializerOptions
        {
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            IncludeFields = true
        };

        var json = JsonSerializer.Serialize(value, options);
        return JsonSerializer.Deserialize<T>(json, options)!;
    }

    /// <summary>
    /// Verifies that only specified fields were modified between two object states.
    /// Supports both reference types and value types (including record struct).
    /// </summary>
    /// <typeparam name="T">Type to compare (no constraint)</typeparam>
    /// <param name="actual">Current state</param>
    /// <param name="expected">Expected/old state</param>
    /// <param name="assignableFields">Field name patterns (regex) that are allowed to change</param>
    public static void EnsureAssignable<T>(T actual, T expected, params string[] assignableFields)
    {
        if (!_config.PostconditionsEnabled) return;

        // Reflection-based comparison works for both reference and value types
        // Implementation in ADR-0007
    }
}
```

**Coding Guidelines**:
- All public APIs must have complete nullable annotations
- All reference type parameters should be explicitly `T` or `T?`
- Internal code should also follow nullable best practices
- Unit tests should cover:
  - Null handling for reference types
  - Value type scenarios (struct, record struct)
  - Nullable value types (`int?`, `DateTime?`)
  - Reference type scenarios (class, record class)

**Testing Value Type Support**:
```csharp
// Test case: Old() with record struct
[Fact]
public void Old_ShouldCaptureRecordStruct()
{
    record struct Money(decimal Amount, string Currency);

    var money = new Money(100m, "USD");
    var oldMoney = Contract.Old(() => money);

    money = money with { Amount = 200m };  // Modify

    Assert.Equal(100m, oldMoney.Amount);  // Old value preserved
    Assert.Equal(200m, money.Amount);     // New value updated
}

// Test case: EnsureAssignable() with record struct
[Fact]
public void EnsureAssignable_ShouldWorkWithRecordStruct()
{
    record struct Point(int X, int Y);

    var p1 = new Point(0, 0);
    var p2 = new Point(5, 0);  // Only X changed

    Contract.EnsureAssignable(p2, p1, nameof(Point.X));  // Should pass

    var p3 = new Point(5, 10);  // Both X and Y changed
    Assert.Throws<PostconditionViolationException>(
        () => Contract.EnsureAssignable(p3, p1, nameof(Point.X))
    );  // Should fail (Y also changed)
}
```

---

## References

- [Nullable Reference Types (C# 8.0)](https://learn.microsoft.com/en-us/dotnet/csharp/nullable-references)
- [Generic Constraints](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/generics/constraints-on-type-parameters)
- [.NET API Design Guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/)

---

## Revision History

| Date       | Status      | Notes                          |
|------------|-------------|--------------------------------|
| 2025-10-18 | Accepted    | Decision finalized             |
