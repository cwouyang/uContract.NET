# ADR-0008: Exception Hierarchy Design

## Status

**Accepted**

- **Date**: 2025-10-18
- **Deciders**: Project maintainers
- **Status Date**: 2025-10-18

---

## Context

### Problem Statement

We need to design the exception hierarchy for Design by Contract violations. Key decisions:
- Should we provide a common base exception for all DBC violations?
- Should DBC exceptions inherit from standard .NET exception types (e.g., `ArgumentException`)?
- What diagnostic information should exceptions contain?
- Should we support custom message formatting?

This decision affects:
- **Error handling**: How users catch and handle contract violations
- **Debugging**: What information is available when contracts fail
- **.NET integration**: How well exceptions fit into the .NET ecosystem
- **Java parity**: Consistency with Java version's exception design

### Relevant Context

- The Java version has a simple hierarchy:
  - All DBC exceptions extend `RuntimeException`
  - Each violation type has its own exception class
  - Exceptions contain description and message
- .NET has standard exception types:
  - `ArgumentException`, `ArgumentNullException` for parameter validation
  - `InvalidOperationException` for state-related errors
- DBC violations are conceptually different from standard .NET exceptions:
  - They represent design flaws, not runtime errors
  - They should never be caught in production (fail-fast principle)

### Constraints

- Must provide clear distinction between DBC violations and standard exceptions
- Should support unified catching (e.g., `catch (ContractViolationException)`)
- Must include enough diagnostic information for debugging
- Should maintain simplicity (avoid over-engineering)

---

## Decision

**We will create a common base exception `ContractViolationException` that inherits from `Exception`, with specific derived exceptions for each violation type. All exceptions will include detailed diagnostic information.**

### Details

**Exception Hierarchy** (Method A):
```
System.Exception
  └─ ContractViolationException (base for all DBC violations)
       ├─ PreconditionViolationException
       ├─ PostconditionViolationException
       ├─ InvariantViolationException
       └─ CheckViolationException
```

**Base Exception**:
```csharp
/// <summary>
/// Base exception for all Design by Contract violations.
/// </summary>
public class ContractViolationException : Exception
{
    /// <summary>
    /// Gets the description of the contract that was violated.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the type of contract that was violated.
    /// </summary>
    public ContractType ViolationType { get; }

    public ContractViolationException(
        string description,
        ContractType violationType,
        string message)
        : base(message)
    {
        Description = description;
        ViolationType = violationType;
    }

    public ContractViolationException(
        string description,
        ContractType violationType,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        Description = description;
        ViolationType = violationType;
    }
}

public enum ContractType
{
    Precondition,
    Postcondition,
    Invariant,
    Check
}
```

**Specific Exceptions**:
```csharp
/// <summary>
/// Thrown when a precondition is violated.
/// </summary>
public sealed class PreconditionViolationException : ContractViolationException
{
    public PreconditionViolationException(string description)
        : base(
            description,
            ContractType.Precondition,
            $"Precondition violated: {description}")
    {
    }
}

/// <summary>
/// Thrown when a postcondition is violated.
/// </summary>
public sealed class PostconditionViolationException : ContractViolationException
{
    public PostconditionViolationException(string description)
        : base(
            description,
            ContractType.Postcondition,
            $"Postcondition violated: {description}")
    {
    }
}

/// <summary>
/// Thrown when a class invariant is violated.
/// </summary>
public sealed class InvariantViolationException : ContractViolationException
{
    public InvariantViolationException(string description)
        : base(
            description,
            ContractType.Invariant,
            $"Invariant violated: {description}")
    {
    }
}

/// <summary>
/// Thrown when a check statement fails.
/// </summary>
public sealed class CheckViolationException : ContractViolationException
{
    public CheckViolationException(string description)
        : base(
            description,
            ContractType.Check,
            $"Check failed: {description}")
    {
    }
}
```

**Usage Examples**:
```csharp
// Catch specific violation type
try
{
    Contract.Require("x > 0", () => x > 0);
}
catch (PreconditionViolationException ex)
{
    Console.WriteLine($"Precondition failed: {ex.Description}");
}

// Catch all DBC violations
try
{
    user.ChangeEmail(newEmail);
}
catch (ContractViolationException ex)
{
    Console.WriteLine($"Contract violation ({ex.ViolationType}): {ex.Description}");
}
```

---

## Consequences

### Positive Consequences

- ✅ **Unified catching**: `catch (ContractViolationException)` catches all DBC violations
- ✅ **Specific handling**: Can catch individual types when needed
- ✅ **Rich diagnostics**: `Description` and `ViolationType` properties aid debugging
- ✅ **Java parity**: Similar hierarchy to Java version
- ✅ **Clear semantics**: Exception name clearly indicates DBC violation
- ✅ **Standard .NET inheritance**: Inherits from `Exception`, works with all .NET error handling
- ✅ **Sealed leaf classes**: Prevents further inheritance (design clarity)

### Negative Consequences

- ❌ **Not integrated with standard exceptions**: Doesn't inherit from `ArgumentException` or `InvalidOperationException`
- ❌ **Requires DBC knowledge**: Developers must understand what "precondition violation" means
- ❌ **Catch-all risk**: Generic `catch (Exception)` also catches DBC violations

### Neutral Consequences

- ⚖️ **Conceptually separate**: DBC violations are design errors, not runtime errors
- ⚖️ **Fail-fast friendly**: Exceptions are meant to crash early, not be handled

---

## Alternatives Considered

### Alternative 1: Inherit from Standard .NET Exception Types

**Description**: Integrate with .NET exception hierarchy

```csharp
public class PreconditionViolationException : ArgumentException { }
public class PostconditionViolationException : InvalidOperationException { }
```

**Pros**:
- Integrates with standard .NET exception handling patterns
- Familiar to .NET developers
- Can be caught with `catch (ArgumentException)`

**Cons**:
- **Semantic mismatch**: DBC violations are not argument errors or invalid operations
- **Loss of common base**: Cannot catch all DBC violations with one type
- **Confusing**: Mixing DBC violations with standard exceptions obscures their nature

**Why rejected**: DBC violations are conceptually different from standard .NET exceptions. They represent design contract failures, not runtime argument errors or state issues.

---

### Alternative 2: No Common Base Exception

**Description**: Each violation type is independent

```csharp
public sealed class PreconditionViolationException : Exception { }
public sealed class PostconditionViolationException : Exception { }
// No common base
```

**Pros**:
- Simplest possible design
- Forces explicit handling of each type

**Cons**:
- **Cannot catch all DBC violations uniformly**
- **Inconvenient for logging/monitoring**: Must catch each type separately
- **Breaks Java parity**: Java version has shared behavior

**Why rejected**: Unified catching is valuable for logging, monitoring, and cleanup scenarios.

---

### Alternative 3: Include Condition Expression in Exception

**Description**: Store the failed condition lambda as a string

```csharp
public class ContractViolationException : Exception
{
    public string Description { get; }
    public string ConditionExpression { get; }  // "() => x > 0"
}
```

**Pros**:
- More detailed debugging information
- Shows the exact condition that failed

**Cons**:
- **Not possible in C#**: Cannot extract lambda source code at runtime
- **Would require Roslyn analyzers**: Complex implementation
- **Limited value**: Description already explains the requirement

**Why rejected**: Not feasible without source code analysis. Description string is sufficient.

---

### Alternative 4: Configurable Message Formatting

**Description**: Allow users to customize exception messages

```csharp
Contract.ConfigureMessageFormatter(violation =>
    $"[{violation.Type}] {violation.Description} at {violation.Location}"
);
```

**Pros**:
- Flexible for different organizational standards
- Can integrate with logging frameworks

**Cons**:
- **Over-engineering**: YAGNI — message format is rarely a concern
- **Configuration complexity**: Adds setup code
- **Thread safety**: Formatter must be thread-safe

**Why rejected**: Simple, consistent message format is sufficient. Users can catch exceptions and reformat if needed.

---

## Related Decisions

- **Related to**: ADR-0003 (Static API design — exceptions are thrown from static methods)
- **Affects**: Error handling patterns in user code
- **Affects**: Debugging and diagnostic workflows

---

## Implementation Notes

### Exception Properties

All exceptions must implement:
- `Description`: Human-readable description of the violated contract
- `ViolationType`: Enum indicating the type of violation
- `Message`: Formatted message combining type and description
- `StackTrace`: Standard .NET stack trace (inherited from `Exception`)

### Serialization

Exceptions should be serializable for remoting scenarios (though rare):
```csharp
[Serializable]
public class ContractViolationException : Exception
{
    protected ContractViolationException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        Description = info.GetString(nameof(Description))!;
        ViolationType = (ContractType)info.GetValue(nameof(ViolationType), typeof(ContractType))!;
    }

    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(Description), Description);
        info.AddValue(nameof(ViolationType), ViolationType);
    }
}
```

### XML Documentation

All exception classes must have:
- `<summary>`: What the exception represents
- `<remarks>`: When it's thrown
- `<example>`: Code that would trigger this exception

### Testing

Test scenarios:
- Exception message format
- Exception properties (`Description`, `ViolationType`)
- Exception inheritance (catch base vs derived)
- Stack trace correctness
- Serialization round-trip (if supported)

---

## References

- [Exception Class](https://learn.microsoft.com/en-us/dotnet/api/system.exception)
- [Design Guidelines for Exceptions](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/exceptions)
- [Java uContract Exceptions](https://gitlab.com/TeddyChen/ucontract/-/tree/master/src/main/java/tw/teddysoft/ucontract)

---

## Revision History

| Date       | Status      | Notes                          |
|------------|-------------|--------------------------------|
| 2025-10-18 | Accepted    | Decision finalized             |
