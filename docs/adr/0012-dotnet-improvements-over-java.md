# ADR-0012: .NET Improvements Over Java Implementation

## Status

**Accepted**

- **Date**: 2025-10-19
- **Deciders**: Project maintainers
- **Status Date**: 2025-10-19

---

## Context

### Problem Statement

The .NET port of uContract needs to decide whether to:
1. **Exactly replicate Java implementation** (including its limitations)
2. **Improve upon Java where .NET conventions differ** (while maintaining semantic equivalence)
3. **Hybridize** (match Java externally, improve internally)

This decision affects:
- **API safety**: Parameter validation, null handling
- **Error clarity**: Exception message consistency
- **Code quality**: Modern .NET idioms vs Java patterns
- **Migration ease**: How easily Java users can adopt .NET version

### Relevant Context

After analyzing the [Java uContract implementation](https://gitlab.com/TeddyChen/ucontract/), we identified several areas where .NET conventions differ from Java patterns:

**Java Implementation Characteristics**:
- No parameter validation in contract methods (null parameters cause `NullPointerException`)
- Inconsistent exception message formats:
  - `require()`: `"Require {description}"`
  - `requireNotNull()`: `"Require [{description}] cannot be null"`
  - `invariant()`: `"Ensure Invariant [{description}]"` (note: "Ensure Invariant", not "Invariant")
- Heavy use of `StringBuilder` for simple string concatenation
- `ThreadLocal<Boolean>` for recursion guards (Java doesn't have async/await)
- Public static fields for configuration (`DBC`, `CHECK_PRE`, etc.)

**.NET Conventions**:
- Parameter validation is standard practice (`ArgumentNullException`)
- String interpolation for simple cases (`$"text {value}"`)
- `AsyncLocal<T>` for async-aware context storage
- Encapsulated configuration classes (not public fields)
- Consistent exception messages

### Constraints

- Must maintain **semantic equivalence** with Java version (same DBC behavior)
- Must not break **zero-dependency principle** (ADR-0011)
- Should follow **.NET best practices** where they don't conflict with DBC semantics
- Must be clearly documented for Java users migrating to .NET

---

## Decision

**We will improve upon the Java implementation where .NET conventions differ, while maintaining full semantic equivalence for Design by Contract behavior.**

### Details

#### 1. **Parameter Validation (NEW - Not in Java)**

All public contract methods will validate their parameters before any other logic.

```csharp
public static void Require(string description, Func<bool> condition)
{
    // 1. VALIDATE PARAMETERS (NEW: Java doesn't do this)
    if (description is null)
        throw new ArgumentNullException(nameof(description));
    if (condition is null)
        throw new ArgumentNullException(nameof(condition));

    // 2. CHECK RECURSION GUARD
    if (_entered.Value) return;

    // 3. CHECK IF ENABLED
    if (!_config.PreconditionsEnabled) return;

    // 4. EXECUTE WITH GUARD
    try
    {
        _entered.Value = true;
        if (!condition())
            throw new PreconditionViolationException($"Precondition violated: {description}");
    }
    finally
    {
        _entered.Value = false;
    }
}
```

**Rationale**:
- **Safety**: Prevents confusing `NullReferenceException` when users pass null
- **Clarity**: `ArgumentNullException` clearly indicates programming error
- **.NET standard**: All .NET APIs validate parameters
- **No semantic change**: DBC behavior remains identical

**Java vs .NET Behavior**:
```java
// Java: NullPointerException at unexpected location
Contract.require(null, () -> true);  // NPE when building message string

// .NET: Clear ArgumentNullException immediately
Contract.Require(null, () => true);  // ArgumentNullException("description")
```

---

#### 2. **Unified Exception Message Format (IMPROVED)**

Standardize all exception messages to `"{Type} violated: {description}"` format.

```csharp
// Preconditions
throw new PreconditionViolationException($"Precondition violated: {description}");

// Postconditions
throw new PostconditionViolationException($"Postcondition violated: {description}");

// Invariants
throw new InvariantViolationException($"Invariant violated: {description}");

// Check statements
throw new CheckViolationException($"Check failed: {description}");
```

**Java's Inconsistent Formats**:
```java
// Java: Inconsistent prefixes
"Require " + annotation                          // require()
"Require [" + annotation + "] cannot be null"    // requireNotNull()
"Ensure " + annotation                           // ensure()
"Ensure [" + annotation + "] cannot be null"     // ensureNotNull()
"Ensure Invariant [" + annotation + "]"          // invariant() - note "Ensure"!
"Invariant [" + annotation + "] cannot be null"  // invariantNotNull()
"Check " + annotation                            // check()
```

**Rationale**:
- **Consistency**: Single predictable format across all contract types
- **Clarity**: Exception type name matches message prefix
- **Parsability**: Structured format easier to parse in logs
- **Professional**: More polished than Java's ad-hoc formatting

---

#### 3. **Modern .NET Syntax (String Interpolation)**

Use string interpolation instead of `StringBuilder` for simple concatenations.

```csharp
// .NET: String interpolation (readable, idiomatic)
var message = $"Precondition violated: {description}";

// Java: StringBuilder (verbose, necessary in older Java)
var msg = new StringBuilder().append("Require ").append(annotation).toString();
```

**Rationale**:
- **Readability**: String interpolation is clearer
- **Performance**: Compiler optimizes string interpolation to `String.Concat`
- **Modern**: Standard practice in C# 6+
- **Maintainability**: Easier to understand and modify

---

#### 4. **AsyncLocal Instead of ThreadLocal (Already in ADR-0009)**

Use `AsyncLocal<bool>` for recursion guards (see ADR-0009).

**Not a change from Java philosophy**, but an **adaptation to .NET's async/await model**.

---

#### 5. **Encapsulated Configuration (Already in ADR-0004)**

Use `ContractConfiguration` class instead of public static fields (see ADR-0004).

**Not a change from Java philosophy**, but **better encapsulation for .NET**.

---

## Consequences

### Positive Consequences

- ✅ **Safer API**: Parameter validation prevents confusing errors
- ✅ **Clearer errors**: Consistent exception messages easier to understand
- ✅ **Modern .NET**: Follows current C# best practices
- ✅ **Better maintainability**: Cleaner, more readable code
- ✅ **Same DBC semantics**: Core Design by Contract behavior unchanged
- ✅ **Professional quality**: Exception messages and API design feel polished
- ✅ **Better debugging experience**: Clear errors guide users to fix issues faster

### Negative Consequences

- ❌ **Not byte-for-byte Java port**: Error messages differ from Java version
- ❌ **Migration consideration**: Java users see different error messages
- ❌ **Documentation burden**: Must document differences from Java

### Neutral Consequences

- ⚖️ **Slight performance overhead**: Parameter validation adds ~2-5ns per call (negligible in context of DBC)
- ⚖️ **Different error messages in logs**: Teams using both Java and .NET versions will see different formats
- ⚖️ **Trade-off**: Java parity vs .NET conventions (we chose .NET conventions)

---

## Alternatives Considered

### Alternative 1: Exact Java Replication

**Description**: Replicate Java implementation exactly, including no parameter validation and inconsistent messages.

```csharp
// Exact Java replication (NOT CHOSEN)
public static void Require(string description, Func<bool> condition)
{
    if (!_config.PreconditionsEnabled) return;
    if (_entered.Value) return;

    var msg = new StringBuilder().Append("Require ").Append(description).ToString();
    // ... no parameter validation, just like Java
}
```

**Pros**:
- Perfect Java parity
- Identical error messages across languages
- No documentation of differences needed

**Cons**:
- **Poor .NET practice**: No parameter validation violates .NET guidelines
- **Confusing errors**: `NullReferenceException` instead of `ArgumentNullException`
- **Inconsistent messages**: Inherits Java's ad-hoc formatting
- **Not idiomatic**: Doesn't feel like a .NET library

**Why rejected**: .NET developers expect parameter validation. Following Java's quirks would make the library feel foreign in .NET ecosystems.

---

### Alternative 2: Hybrid Approach (Java-Compatible Messages Externally)

**Description**: Keep Java's exact error messages but add parameter validation.

```csharp
// Hybrid: Validate but use Java messages (NOT CHOSEN)
public static void Require(string description, Func<bool> condition)
{
    if (description is null) throw new ArgumentNullException(nameof(description));
    if (condition is null) throw new ArgumentNullException(nameof(condition));

    // ... but still throw Java-style messages
    throw new PreconditionViolationException($"Require {description}");  // Java format
}
```

**Pros**:
- Parameter safety
- Identical error messages to Java (for logging/monitoring)

**Cons**:
- **Inconsistent**: Validates parameters but keeps inconsistent Java message formats
- **Weird mix**: Neither fully Java nor fully .NET
- **Missed opportunity**: Doesn't fix Java's message inconsistency

**Why rejected**: If we're diverging from Java for parameter validation, we should also fix the message inconsistency. Half-measures satisfy neither goal.

---

### Alternative 3: Configurable Behavior (Java vs .NET Mode)

**Description**: Let users choose Java-compatible or .NET-native behavior.

```csharp
// Configuration option (NOT CHOSEN)
Contract.ConfigureCompatibilityMode(CompatibilityMode.DotNet);  // or CompatibilityMode.Java
```

**Pros**:
- Maximum flexibility
- Users can choose their preference

**Cons**:
- **Over-engineering**: Adds significant complexity
- **Testing burden**: Must test both modes
- **Configuration burden**: Requires setup (breaks zero-setup principle)
- **Confusing**: Most users won't know which to choose
- **Maintenance**: Doubles the code paths to maintain

**Why rejected**: YAGNI. The improvements (parameter validation, consistent messages) are universally beneficial. No realistic scenario requires Java-exact behavior.

---

## Related Decisions

- **Builds upon**: ADR-0004 (Encapsulated configuration instead of public fields)
- **Builds upon**: ADR-0009 (AsyncLocal instead of ThreadLocal)
- **Affects**: ADR-0008 (Exception hierarchy - standardizes message format)
- **Relates to**: ADR-0003 (Static class design - parameter validation pattern applies to all methods)

---

## Implementation Notes

### Parameter Validation Pattern

Apply to **all public contract methods**:

```csharp
public static void Require(string description, Func<bool> condition)
{
    // ALWAYS validate parameters first (even if DBC is disabled)
    if (description is null) throw new ArgumentNullException(nameof(description));
    if (condition is null) throw new ArgumentNullException(nameof(condition));

    // Then proceed with normal contract logic...
}
```

**Important**: Validation happens **before** checking `_config.PreconditionsEnabled`. This ensures programming errors are caught even when contracts are disabled.

---

### Exception Message Format Standard

| Contract Type | Format | Example |
|---------------|--------|---------|
| Precondition | `Precondition violated: {desc}` | `Precondition violated: x must be positive` |
| Postcondition | `Postcondition violated: {desc}` | `Postcondition violated: result is not null` |
| Invariant | `Invariant violated: {desc}` | `Invariant violated: balance is non-negative` |
| Check | `Check failed: {desc}` | `Check failed: state is valid` |

**For `*NotNull` methods**, embed the null check in the description:

```csharp
// RequireNotNull
throw new PreconditionViolationException($"Precondition violated: {description} cannot be null");

// EnsureNotNull
throw new PostconditionViolationException($"Postcondition violated: {description} cannot be null");

// InvariantNotNull
throw new InvariantViolationException($"Invariant violated: {description} cannot be null");
```

---

### String Interpolation Guidelines

**Use string interpolation** for simple cases:
```csharp
var msg = $"Precondition violated: {description}";
```

**Use StringBuilder** for loops or many concatenations:
```csharp
var sb = new StringBuilder();
foreach (var item in items)
    sb.Append(item).Append(", ");
```

**Rationale**: String interpolation is optimized by the compiler and more readable.

---

### Documentation Requirements

**README.md** must include:

> **Differences from Java Version**
>
> While the .NET port maintains semantic equivalence with the Java version, the following improvements have been made:
>
> 1. **Parameter Validation**: All contract methods validate their parameters (`ArgumentNullException` for null)
> 2. **Consistent Exception Messages**: All exceptions use the format `"{Type} violated: {description}"`
> 3. **Async Support**: Uses `AsyncLocal` for recursion guards (works correctly with `async`/`await`)
>
> These changes improve safety and clarity while preserving Design by Contract semantics.

---

## References

- [Java uContract Repository](https://gitlab.com/TeddyChen/ucontract/)
- [.NET Design Guidelines - Parameter Validation](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/parameter-design)
- [String Interpolation in C#](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/tokens/interpolated)
- ADR-0004 (Runtime configuration)
- ADR-0008 (Exception hierarchy)
- ADR-0009 (Thread safety and async support)

---

## Revision History

| Date       | Status      | Notes                          |
|------------|-------------|--------------------------------|
| 2025-10-19 | Accepted    | Decision finalized after analyzing Java implementation |
