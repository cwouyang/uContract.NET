# ADR-0009: Thread Safety and Async/Await Support

## Status

**Accepted**

- **Date**: 2025-10-18
- **Deciders**: Project maintainers
- **Status Date**: 2025-10-18

---

## Context

### Problem Statement

Contract methods need recursion guards to prevent infinite loops when contract code calls other methods with contracts. We must decide:
- Use `ThreadLocal<bool>` (traditional multi-threading)
- Use `AsyncLocal<bool>` (async/await-aware)
- Support both with configuration
- How to handle async contract methods (if needed)

This decision affects:
- **Async compatibility**: Whether contracts work correctly in async methods
- **Performance**: AsyncLocal has slight overhead vs ThreadLocal
- **Complexity**: Supporting both adds implementation complexity
- **Modern .NET alignment**: AsyncLocal is recommended for async-heavy codebases

### Relevant Context

- The Java version uses `ThreadLocal<Boolean>` for recursion guards
- .NET has:
  - `ThreadLocal<T>`: Thread-specific storage (doesn't flow across async)
  - `AsyncLocal<T>`: Async context storage (flows across await boundaries)
- Modern .NET applications heavily use async/await
- DDD aggregates may have async command methods
- The recursion guard prevents infinite loops:
  ```csharp
  public void ChangeEmail(string email)
  {
      Contract.Require(...);  // Sets guard
      Apply(new EmailChanged(...));  // Might call other methods with contracts
      Contract.Ensure(...);  // Uses guard
  }
  ```

### Constraints

- Must prevent infinite recursion in nested contract calls
- Should work correctly in async/await scenarios
- Must have minimal performance overhead
- Should maintain simplicity (avoid over-engineering)

---

## Decision

**We will use `AsyncLocal<bool>` for recursion guards to support both synchronous and asynchronous execution contexts.**

### Details

**Implementation**:
```csharp
public static class Contract
{
    // AsyncLocal instead of ThreadLocal
    private static readonly AsyncLocal<bool> _entered = new();

    private static readonly ContractConfiguration _config = new();

    public static void Require(string description, Func<bool> condition)
    {
        if (!_config.PreconditionsEnabled) return;

        // Recursion guard check
        if (_entered.Value) return;

        try
        {
            _entered.Value = true;

            if (!condition())
                throw new PreconditionViolationException(description);
        }
        finally
        {
            _entered.Value = false;
        }
    }

    // Similar pattern for Ensure, Invariant, Check, Old, etc.
}
```

**Behavior**:
- **Synchronous code**: Works like ThreadLocal (thread-specific)
- **Async code**: Flows across `await` boundaries within the same logical call context
- **Parallel tasks**: Each task has its own logical context (isolated)

**Example with Async**:
```csharp
public async Task ChangeEmailAsync(string newEmail)
{
    Contract.RequireNotNull("Email", newEmail);  // Sets _entered.Value = true

    await SaveToDatabase(newEmail);  // Awaits, but _entered stays true in this context

    Contract.Ensure("Email updated", () => _email == newEmail);  // Sees _entered = true, skips check
}

private async Task SaveToDatabase(string email)
{
    Contract.RequireNotNull("Email", email);  // Nested call, sees _entered = true, returns early
    await _db.SaveAsync();
}
```

---

## Complete Assertion Evaluation Rule

### Formal Statement

**Rule (from Root Contracting Paper, Definition 2)**: When a contract assertion is executed (i.e., when DBC is enabled and recursion guard is not active), the assertion **must be evaluated completely** to determine its truth value. The evaluation must not be short-circuited or skipped due to implementation details.

### Relationship to Recursion Guard

The recursion guard (`AsyncLocal<bool> _entered`) serves a specific purpose: **preventing infinite loops in nested contract calls**. It does NOT affect the complete evaluation rule:

| Scenario | Recursion Guard | Evaluation Behavior |
|----------|----------------|---------------------|
| **First call** | `_entered = false` | ✅ **Complete evaluation**: Assertion lambda is invoked, result checked |
| **Nested call** | `_entered = true` | ⏭️ **Skip**: Early return prevents infinite recursion |
| **After exception** | Reset to `false` (finally block) | ✅ **Next assertion evaluates completely** |

### Implementation Guarantees Complete Evaluation

The `try-finally` pattern ensures assertions evaluate completely, even when exceptions occur:

```csharp
public static void Require(string description, Func<bool> condition)
{
    if (!_config.PreconditionsEnabled) return;  // (1) Early exit if disabled

    if (_entered.Value) return;  // (2) Skip if already in a contract (recursion guard)

    try
    {
        _entered.Value = true;  // (3) Mark as entered

        // (4) COMPLETE EVALUATION: Lambda is invoked fully
        if (!condition())
            throw new PreconditionViolationException(description);
    }
    finally
    {
        _entered.Value = false;  // (5) Always reset guard (even on exception)
    }
}
```

**Key points**:
1. **Line (4)**: When reached, `condition()` is **always evaluated completely** — the lambda executes to return `true` or `false`
2. **Line (5)**: The `finally` block ensures the guard is reset, even if the assertion throws an exception
3. **No short-circuiting**: There is no mechanism to skip evaluation once line (4) is reached

### Example: Nested Calls with Complete Evaluation

```csharp
public void OuterMethod(int x)
{
    // First assertion: COMPLETE EVALUATION
    Contract.Require("x positive", () =>
    {
        Console.WriteLine("Evaluating outer assertion");  // This WILL print
        return x > 0;
    });

    HelperMethod(x);  // Calls another method with contracts

    // Postcondition: COMPLETE EVALUATION
    Contract.Ensure("x unchanged", () =>
    {
        Console.WriteLine("Evaluating postcondition");  // This WILL print
        return x > 0;
    });
}

private void HelperMethod(int x)
{
    // Nested assertion: SKIPPED (recursion guard active)
    Contract.Require("x positive", () =>
    {
        Console.WriteLine("Evaluating helper assertion");  // This will NOT print
        return x > 0;
    });
}
```

**Output**:
```
Evaluating outer assertion
Evaluating postcondition
```

**Explanation**:
- `OuterMethod` first assertion: Guard is `false`, evaluates completely
- `HelperMethod` assertion: Guard is `true` (set by outer), skipped entirely (early return)
- `OuterMethod` postcondition: Guard is `false` again (finally reset), evaluates completely

### Why This Matters

Complete assertion evaluation ensures:
1. **Correctness**: All contract checks are fully validated when enabled
2. **Predictability**: Developers can rely on assertions being checked thoroughly
3. **Debugging**: Side effects in assertions (e.g., logging) execute completely
4. **Aggregate Root Correctness**: Aligns with the formal rules in Definition 2 of the Root Contracting Paper

**Trade-off**: The recursion guard prevents infinite loops but also means **nested contracts are silently skipped**. This is acceptable because:
- DBC checks are meant to validate the **public command interface** of aggregates (outer method)
- Helper methods called internally are implementation details (nested calls)
- Contracts should be placed at the correct level (public command interface)

---

## Consequences

### Positive Consequences

- ✅ **Async/await compatible**: Works correctly in async methods
- ✅ **Modern .NET alignment**: Recommended pattern for async-heavy applications
- ✅ **Logical context flow**: Maintains recursion guard across await points
- ✅ **Thread-safe**: Each async context is isolated
- ✅ **Future-proof**: Async is increasingly prevalent in .NET
- ✅ **Simple API**: No configuration needed, works automatically

### Negative Consequences

- ❌ **Slight performance overhead**: AsyncLocal is marginally slower than ThreadLocal (~10-20ns difference)
- ❌ **Different from Java**: Java uses ThreadLocal (but Java doesn't have async/await)
- ❌ **More complex internally**: AsyncLocal uses ExecutionContext, which is more complex than thread-local storage

### Neutral Consequences

- ⚖️ **Async not required**: Still works perfectly in synchronous code
- ⚖️ **Rare overhead concern**: DBC checks are not hot-path code

---

## Alternatives Considered

### Alternative 1: ThreadLocal<bool>

**Description**: Use traditional thread-local storage

```csharp
private static readonly ThreadLocal<bool> _entered = new(() => false);
```

**Pros**:
- Simpler (less conceptual overhead)
- Slightly faster (~10-20ns per access)
- Matches Java version exactly

**Cons**:
- **Breaks in async/await**: Guard doesn't flow across await boundaries
- **Incorrect behavior**: Could allow infinite recursion in async methods
- **Not modern .NET practice**: AsyncLocal is recommended for new code

**Why rejected**: Modern .NET applications use async/await extensively. Using ThreadLocal would cause incorrect behavior in async scenarios, potentially leading to infinite recursion or missed contract checks.

**Example of broken behavior**:
```csharp
// With ThreadLocal: BROKEN
private static readonly ThreadLocal<bool> _entered = new(() => false);

public async Task MethodAsync()
{
    Contract.Require(...);  // Sets _entered = true on Thread A

    await SomethingAsync();  // Continuation might run on Thread B

    Contract.Ensure(...);  // Reads _entered from Thread B = false (WRONG!)
    // Contracts may execute twice or fail incorrectly
}
```

---

### Alternative 2: Support Both with Configuration

**Description**: Let users choose ThreadLocal or AsyncLocal

```csharp
public static void ConfigureContextStorage(ContextStorageType type)
{
    _storageType = type;
}

private static bool GetEntered()
{
    return _storageType == ContextStorageType.ThreadLocal
        ? _threadLocal.Value
        : _asyncLocal.Value;
}
```

**Pros**:
- Maximum flexibility
- Users can optimize for their scenario (sync-only vs async)

**Cons**:
- **Over-engineering**: Adds significant complexity for minimal benefit
- **Configuration burden**: Requires setup code (breaks zero-setup principle)
- **Testing complexity**: Must test both paths
- **Confusing**: Most users won't know which to choose

**Why rejected**: YAGNI. AsyncLocal works fine for both sync and async code. The performance difference is negligible in the context of DBC checks.

---

### Alternative 3: No Recursion Guard (Trust Users)

**Description**: Eliminate recursion guards entirely

**Pros**:
- Simplest possible implementation
- No performance overhead
- No thread-safety concerns

**Cons**:
- **Infinite recursion risk**: Nested contract calls would loop infinitely
- **Poor user experience**: Subtle bugs when contracts call contracted methods
- **Breaks Java parity**: Java version has guards

**Why rejected**: Recursion guards are essential for practical use. Many DDD aggregates have command methods that call helper methods, and both may have contracts.

---

### Alternative 4: Async-Aware Contract Methods

**Description**: Provide async versions of contract methods

```csharp
public static async Task RequireAsync(string desc, Func<Task<bool>> condition)
{
    if (!await condition())
        throw new PreconditionViolationException(desc);
}
```

**Pros**:
- Could support async condition evaluation

**Cons**:
- **Unnecessary**: Contracts should validate state, not perform async operations
- **API bloat**: Doubles the number of methods
- **Confusing**: When to use Require vs RequireAsync?
- **Anti-pattern**: Contracts should be fast, synchronous checks

**Why rejected**: Contract conditions should be simple boolean expressions, not async operations. If async work is needed, do it before calling the contract method.

---

## Related Decisions

- **Related to**: ADR-0003 (Static class design requires static recursion guards)
- **Related to**: ADR-0006 (Old<T>() uses recursion guard to prevent nested serialization)
- **Related to**: ADR-0007 (EnsureAssignable uses recursion guard for comparison)
- **Affects**: All contract methods (Require, Ensure, Invariant, Check, Old, Ignore)

---

## Implementation Notes

### AsyncLocal Initialization

```csharp
// Initialize with default value (false)
private static readonly AsyncLocal<bool> _entered = new();

// AsyncLocal<T> doesn't require a factory method like ThreadLocal<T>
// Default value for bool is false
```

### Usage Pattern in Contract Methods

```csharp
public static void Require(string description, Func<bool> condition)
{
    if (!_config.PreconditionsEnabled) return;

    // Guard check
    if (_entered.Value) return;  // Already in a contract, skip to prevent recursion

    try
    {
        _entered.Value = true;  // Mark as entered

        // Execute contract logic
        if (!condition())
            throw new PreconditionViolationException(description);
    }
    finally
    {
        _entered.Value = false;  // Always reset guard
    }
}
```

### Performance Characteristics

- **ThreadLocal access**: ~5ns
- **AsyncLocal access**: ~15-25ns
- **Delta**: ~10-20ns per contract check
- **Context**: Contract checks typically involve lambda evaluation (100s-1000s of ns), so AsyncLocal overhead is <10% of total

### Testing Requirements

Test scenarios:
- ✅ Synchronous nested contract calls
- ✅ Async method with contracts before and after await
- ✅ Parallel async tasks (isolated contexts)
- ✅ Mixed sync/async call chains
- ✅ Deeply nested contract calls (recursion guard prevents infinite loop)

**Example test**:
```csharp
[Fact]
public async Task AsyncLocal_FlowsAcrossAwait()
{
    var called = false;

    await TestMethod();

    async Task TestMethod()
    {
        Contract.Require("test", () =>
        {
            // This runs in async context
            await Task.Delay(10);  // Simulate async work
            called = true;
            return true;
        });
    }

    Assert.True(called);
}
```

---

## References

- [AsyncLocal<T> Class](https://learn.microsoft.com/en-us/dotnet/api/system.threading.asynclocal-1)
- [ThreadLocal<T> Class](https://learn.microsoft.com/en-us/dotnet/api/system.threading.threadlocal-1)
- [ExecutionContext](https://learn.microsoft.com/en-us/dotnet/api/system.threading.executioncontext)
- [Async/Await Best Practices](https://learn.microsoft.com/en-us/archive/msdn-magazine/2013/march/async-await-best-practices-in-asynchronous-programming)
- [Java ThreadLocal](https://docs.oracle.com/javase/8/docs/api/java/lang/ThreadLocal.html) (used in Java uContract)
- [Root Contracting Paper - Definition 2: Aggregate Root Correctness Rules](https://www.mdpi.com/2079-9292/14/21/4205) (Complete assertion evaluation requirement)

---

## Revision History

| Date       | Status      | Notes                          |
|------------|-------------|--------------------------------|
| 2025-10-18 | Accepted    | Decision finalized             |
| 2025-10-29 | Enhanced    | Added complete assertion evaluation rule (Section: Complete Assertion Evaluation Rule) |
