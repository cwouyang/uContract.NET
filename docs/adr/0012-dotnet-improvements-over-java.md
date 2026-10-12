# ADR-0012: .NET Improvements Over Java Implementation

## Status

**Accepted**

- **Date**: 2025-10-19
- **Deciders**: Project maintainers
- **Status Date**: 2025-10-19
- **Amended**: 2026-10-09 — `EnsureAssignable<T>()` checks `actual` and `expected` for `null` only when it compares them, an exception to "All public contract methods will validate their parameters before any other logic"; see the Amendment under Implementation Notes
- **Amended**: 2026-10-09 — `EnsureAssignable<T>()` compares a `null` `actual` or `expected` as a value and never rejects it, and `EnsureImmutableCollection<T>()` checks `collection` for `null` only when it checks the collection (#52); this supersedes parts of the #47 amendment; see the second Amendment under Implementation Notes
- **Amended**: 2026-10-10 — in a call that compares, `EnsureAssignable<T>()` does not compare one instance of a shared type whose state can change, passed as `actual` and as `expected`: it throws `InvalidOperationException` (#54, rule S of [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md)); see the third Amendment under Implementation Notes
- **Amended**: 2026-10-10 — when `T` is a `FrozenSet<T>` or a `FrozenDictionary<TKey, TValue>`, `EnsureAssignable<T>()` compares two values that are not `null` as a whole: element by element, in enumeration order, without the patterns (#49, [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md)); see the fourth Amendment under Implementation Notes
- **Amended**: 2026-10-11 — when `T` is an array type or `System.Array`, `EnsureAssignable<T>()` compares two values that are not `null` as a whole: element by element, without the patterns; the release notes gain one entry marked BREAKING, for the rank, lengths and lower bounds of two arrays, and one entry under Fixed, for arrays of pointers and of function pointers (#57, [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md)); see the fifth Amendment under Implementation Notes
- **Amended**: 2026-10-12 — `EnsureImmutableCollection<T>()` decides by a new rule: an array is a violation, whatever its element type; a type that is not an array is accepted when it implements one of five immutable-collection interfaces, or is or derives from a `FrozenSet<T>`, a `FrozenDictionary<TKey, TValue>` or a read-only wrapper, each compared by the full name of the generic type definition; the namespace and the start of the name of the type no longer decide (#61, [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md)); see the sixth Amendment under Implementation Notes

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

### Amendment (2026-10-09): EnsureAssignable checks its compared values only when it compares (#47)

This amendment qualifies six statements that say every public contract method validates its parameters before any other logic. Each is quoted by its opening words:

- Decision, Details: `All public contract methods will validate their parameters before any other logic.`
- Related Decisions: `- **Relates to**: ADR-0003 (Static class design - parameter validation pattern applies to all methods)`
- Parameter Validation Pattern: `Apply to **all public contract methods**:`
- The same pattern, in the code comment: `// ALWAYS validate parameters first (even if DBC is disabled)`
- The same pattern, in the note below the code: `**Important**: Validation happens **before** checking`
- Documentation Requirements, item 1 of the README text: `1. **Parameter Validation**: All contract methods validate their parameters`

For `EnsureAssignable<T>()`, they no longer hold as written. The rule is now:

1. `actual` and `expected` are checked for `null` only when the method compares them. It compares them with postconditions on, and not in a call made while another contract check is running. In every other call it returns without comparing and without checking them. `null` includes a nullable value type without a value.
2. `assignableFieldPatterns` is checked for `null` in every call. When it is `null` and no comparison runs, the `ArgumentNullException` names `assignableFieldPatterns`, even if `actual` or `expected` is `null` too.
3. A call that does not compare does not change the recursion guard.

The reason is that `Old<T>()` returns `default` without running its supplier in two cases: with postconditions off, and in a call made while another contract check is running. Those are the same two cases in which `EnsureAssignable<T>()` does not compare. Before this amendment the method checked `actual` and `expected` before it tested either case. So the documented pair `Old(() => this)` and `EnsureAssignable(this, oldState, …)` threw `ArgumentNullException` for `expected` in those two cases, when `T` was a reference type or a nullable value type. `actual` and `expected` are the data that the method compares, and the pattern array describes the call. `EnsureNotNull` and `EnsureResult` already leave `value` and `result` unchecked, and `Ensure` takes its data only through its condition.

| Call | `actual` | `expected` | `assignableFieldPatterns` | Result |
|---|---|---|---|---|
| does not compare | any | any | not `null` | Returns. No exception. |
| does not compare | any | any | `null` | `ArgumentNullException`, `ParamName` = `assignableFieldPatterns` |
| compares | `null` | any | any | `ArgumentNullException`, `ParamName` = `actual` (unchanged) |
| compares | not `null` | `null` | any | `ArgumentNullException`, `ParamName` = `expected` (unchanged) |
| compares | not `null` | not `null` | `null` | `ArgumentNullException`, `ParamName` = `assignableFieldPatterns` (unchanged) |
| compares | not `null` | not `null` | not `null` | Compares as before (unchanged) |

A call that compares keeps the order of checks `actual`, `expected`, `assignableFieldPatterns`. When more than one argument is `null`, the exception names the first of them.

Consequences for a call that does not compare:

- The calling method continues past the pair. Before, it stopped there with the exception.
- A comparison whose property getter, `Equals` or enumerator itself uses the pair now completes. That inner call is made while another contract check is running, so it returns. Before, the outer call failed with the inner `ArgumentNullException` (wrapped in a `TargetInvocationException` when a property getter raised it).
- Work started from inside the condition of a contract (a task, an asynchronous continuation) inherits the recursion guard, which is an `AsyncLocal`. The pair returns without comparing there too, even after that check has ended. Before this change the `ArgumentNullException` was the only visible sign of that state.
- No violation that was reported before is lost. Every result that changes was an `ArgumentNullException`, or a `TargetInvocationException` that wrapped one. `PostconditionViolationException` is raised only by the comparison.

Three alternatives were rejected:

- **Check nothing in a call that does not compare.** A `null` pattern array would then be reported only by a call that compares. It should fail in every call, as a `null` `description` does in the sibling methods.
- **Keep checking `actual`.** Issue #47 lists this option. `actual` is data, like `value` in `EnsureNotNull`. A rule that checks one compared value and not the other is harder to state. And `EnsureAssignable(_member, Contract.Old(() => _member))` would still throw when `_member` is `null`.
- **Keep the checks and tell callers to test the `Old` result for `null`.** The caller would have to test the `Old` result for `null` before each `EnsureAssignable` call. The pair exists so that the caller does not have to. The caller cannot observe the recursion guard at all.

This is one exception, for one method. It is not a general rule about data arguments. `EnsureImmutableCollection` and `RequireNotEmpty` check a data argument in every configuration and are unchanged. So `EnsureImmutableCollection(Contract.Old(() => _items))` still throws `ArgumentNullException` with postconditions off; that method is meant for a return value and the pattern is not documented (noted in #52). No other `Contract` method changes, and the public signature does not change.

Not covered, and left open in [issue #52](https://github.com/cwouyang/uContract.NET/issues/52): a top-level value that is `null` when the method compares. It still throws. Two shapes lead to it. One is a member that is legitimately `null`, as in `EnsureAssignable(_address, Contract.Old(() => _address))`. The other is an `Old` result taken while another contract check was running (so `null`) and passed later to a call that compares. So the pair on a member that can be `null` returns in a call that does not compare and throws in a call that compares.

Release notes: the CHANGELOG entry is under Fixed and is not marked BREAKING. The `Old<T>()` entry that is marked BREAKING differs. Those exceptions reported that a value could not be copied (an unserializable type, disabled reflection-based serialization, a graph deeper than 64 levels), which a caller could need to handle. This exception was raised for a value that `Old` returns by design, and it made the documented pair unusable.

Related records: [ADR-0009](0009-thread-safety-async-support.md) (the recursion guard) was checked and needs no amendment. [ADR-0020](0020-contracts-enabled-by-default.md) restates the rule qualified here ("parameter validation always runs", line 60); it is not edited, because the amendment sits at the source. The measurements are in the amendment of [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md).

Unchanged: the main decision of this ADR, its other improvements (consistent exception messages, `AsyncLocal` recursion guards), and parameter validation of every other method.

### Amendment (2026-10-09): EnsureAssignable compares null as a value; EnsureImmutableCollection checks null only when it checks (#52)

This amendment follows the #47 amendment above and carries the same date. It records the decisions of [issue #52](https://github.com/cwouyang/uContract.NET/issues/52) on `null` and on argument validation. `EnsureAssignable<T>()` never rejects a `null` `actual` or `expected`: when it compares them, `null` is a value. `EnsureImmutableCollection<T>()` checks `collection` for `null` only when it checks the collection. How two values that are not `null` are compared is recorded in the amendment of [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md) for #52.

**Superseded in the #47 amendment.** Each statement is quoted by its opening words:

- The Status line and the Revision History row: "`EnsureAssignable<T>()` checks `actual` and `expected` for `null` only when it compares them". The method checks them for `null` in no call.
- The title: "EnsureAssignable checks its compared values only when it compares (#47)".
- Rule 1: "`actual` and `expected` are checked for `null` only when the method compares them". They are not checked in any call. The rest of rule 1 still holds: when the method compares, and that `null` includes a nullable value type without a value.
- The two table rows for a call that compares that end in "`ArgumentNullException`, `ParamName` = `actual` (unchanged)" and "`ArgumentNullException`, `ParamName` = `expected` (unchanged)". The table below replaces them.
- The order of checks: "A call that compares keeps the order of checks `actual`, `expected`, `assignableFieldPatterns`." Only the pattern array is checked, and it is checked first. The sentence after it, on the first of several `null` arguments, has no case left.
- "This is one exception, for one method. It is not a general rule about data arguments." There are now two exceptions, for two methods. The two sentences after it say that `EnsureImmutableCollection` is unchanged and still throws `ArgumentNullException` with postconditions off. That no longer holds. `RequireNotEmpty` is still unchanged.
- "Not covered, and left open in" issue #52. Both shapes that the paragraph names are decided here.
- "Release notes: the CHANGELOG entry is under Fixed and is not marked BREAKING." See "Release notes" below.
- In the last sentence, "parameter validation of every other method". The validation of `EnsureImmutableCollection<T>()` changes too.

**Extended.** Rule 2, "When it is `null` and no comparison runs": the `ArgumentNullException` names `assignableFieldPatterns` in every call. That includes a call that compares in which `actual` or `expected` is `null` too.

**Qualified.**

- The table row "Compares as before (unchanged)". When `T` is a `string`, a delegate type or a nullable value type, two values that are not `null` are now compared as a whole. The amendment of ADR-0022 for #52 records that decision.
- The six statements of the accepted text that the #47 amendment lists. They no longer hold as written for `EnsureImmutableCollection<T>()` either.
- [ADR-0020](0020-contracts-enabled-by-default.md) restates the rule ("parameter validation always runs", line 60). It is not edited. The rule now has two exceptions, and both are recorded here.

**Still true.** "`PostconditionViolationException` is raised only by the comparison". The `null` rule is part of the comparison: it runs only in a call that compares.

**The rule for `EnsureAssignable<T>()` after both amendments.**

1. `assignableFieldPatterns` is checked for `null` first, in every call.
2. A call that does not compare returns. It does not look at `actual` or `expected`, and it does not change the recursion guard. The method compares with postconditions on, and not in a call made while another contract check is running.
3. A call that compares applies the `null` rule next. Two `null`s are equal, and the method returns. Exactly one `null` is a postcondition violation. `null` includes a nullable value type without a value.
4. The `null` rule runs before the recursion guard is set. It reads no metadata of `T`, it runs no user code, and it does not examine the patterns.
5. Two values that are not `null` are compared with the recursion guard set. The amendment of ADR-0022 for #52 records how.

| Call | `assignableFieldPatterns` | `actual` | `expected` | Result |
|---|---|---|---|---|
| any | `null` | any | any | `ArgumentNullException`, `ParamName` = `assignableFieldPatterns` |
| does not compare | not `null` | any | any | Returns. No exception. (unchanged) |
| compares | not `null` | `null` | `null` | Returns. No exception. |
| compares | not `null` | not `null` | `null` | `PostconditionViolationException`, message A |
| compares | not `null` | `null` | not `null` | `PostconditionViolationException`, message B |
| compares | not `null` | not `null` | not `null` | Compared; see the amendment of ADR-0022 for #52 |

The messages are the `Description` of the exception. Its `Message` is `Postcondition violated: ` followed by the description, as for every postcondition. Each message is one line.

- Message A: `expected is null and actual is not. If expected came from Contract.Old, null can also mean that no snapshot was taken: Old returns default while another contract check is running.`
- Message B: `actual is null and expected is not.`

What changes in a call that compares:

- Two `null`s give no exception. Before, the method threw `ArgumentNullException` naming `actual`.
- One `null` gives `PostconditionViolationException`. Before, the method threw `ArgumentNullException` naming the `null` argument. The new exception is not an `ArgumentException`.
- A `null` pattern array together with a `null` `actual` or `expected` gives an exception that names `assignableFieldPatterns`. Before, it named `actual` or `expected`.
- With a `null` on either side, the elements of the pattern array are not examined. A `null` element or an invalid regular expression in it is not reported then.

**The rule for `EnsureImmutableCollection<T>()`.** The method checks the collection with postconditions on, and not in a call made while another contract check is running.

| Call | `collection` | Result |
|---|---|---|
| does not check | `null` | Returns `null`. No exception. Before: `ArgumentNullException`. |
| does not check | not `null` | Returns `collection`. (unchanged) |
| checks | `null` | `ArgumentNullException`, `ParamName` = `collection` (unchanged) |
| checks | not `null` | Checks as before (unchanged) |

A call that does not check does not change the recursion guard. The `null` in the first row can be an `Old` result or any other value that the caller passes. The method is documented for a return value, as in `return Contract.EnsureImmutableCollection(names);`. With postconditions off, or in a call made while another contract check is running, a `null` result now passes through to the caller. 2.x threw in every configuration. Code that relied on the method as a `null` guard must add its own. A test suite that runs with postconditions on does not see the difference.

**Decisions and reasons.** The maintainer decided them on 2026-10-09. Two lines below were derived from the code and are not maintainer decisions: the position of the `null` rule before the recursion guard is set, and the account of an `Old` result taken while another contract check was running.

- **`null` is a value (option 1 of the issue).** The pair `Old` + `EnsureAssignable` could not check a member that can be `null`. `Old<T>()` returns `default` when its supplier returns `null`, and the method rejected that `null`. For `EnsureAssignable(_address, Contract.Old(() => _address))`:

  | `_address` before | `_address` after | Before this amendment | Now |
  |---|---|---|---|
  | `null` | `null` | `ArgumentNullException` (`actual`) | no exception |
  | `null` | set | `ArgumentNullException` (`expected`) | postcondition violation |
  | set | `null` | `ArgumentNullException` (`actual`) | postcondition violation |

  A member that the comparison reaches already behaved so: two `null`s are equal, and one `null` is unequal. `actual` and `expected` now follow the same rule.
- **One `null` is always a violation.** The assignable patterns do not excuse it. A pattern allows a member of the compared object to change. A value that appears or disappears is a different change from a member that changes.
- **The messages name the parameters and make no statement about the past.** The method sees two arguments. It cannot know where a `null` came from. Message A carries a hint about `Old`, because of the second shape below.
- **The pattern check comes first, and the `null` rule comes before the recursion guard is set.** The pattern array describes the call, so a `null` array fails in every call, as the #47 amendment decided. The `null` rule needs no guard, because it runs no user code. It reads no metadata of `T`, so under Native AOT it also decides for a `T` that has no members visible to reflection. A smoke check pins that under Native AOT (check 71 in the amendment of ADR-0022 for #52).
- **`EnsureImmutableCollection<T>()` is in scope.** It checked `collection` for `null` before it read the configuration or the recursion guard. So a call in which nothing is checked threw `ArgumentNullException` for a `null` argument. That is the order of checks that the #47 amendment removed from `EnsureAssignable<T>()`. The `null` check now comes after the two tests. When the method checks the collection, it keeps the check and the exception.

**The second shape of the issue is not detected.** The #47 amendment names it: an `Old` result taken while another contract check was running, and passed later to a call that compares. Such a result is `null` for a reference type or a nullable value type. Compared later, it has two outcomes:

- With an `actual` that is not `null`, the method reports message A. That is a violation that did not happen. Before, the method threw `ArgumentNullException`.
- With an `actual` that is `null`, the method returns. A change from a value to `null` is missed. Before, the method threw `ArgumentNullException`.

The library cannot tell such a `null` from an old value that really was `null`. A value type already behaves so: `Old(() => _count)` returns `0` while another contract check is running. The hint in message A names the cause, and the documentation says that the snapshot must be taken outside another contract check. Two unit tests pin the two outcomes.

**Rejected alternatives.**

- **Keep rejecting `null` (option 2 of the issue).** The caller would have to wrap the member in its owner (`Old(() => this)`), which copies and compares the whole object, or test for `null` before the call. The comparison already treats a `null` member as a value.
- **Let the patterns excuse a `null` on one side.** One `null` would count as "every member changed". The message would list every member of `T`, and under Native AOT that list can be empty.
- **Report a `null` `collection` as a violation when the method checks the collection.** `EnsureImmutableCollection<T>()` returns its argument, so that it can wrap a return value. `EnsureNotNull` exists for the `null` case. The method keeps `ArgumentNullException`.
- **Declare the parameters as `T? actual, T? expected`.** It would remove compiler warning CS8604 for a caller that writes the type argument explicitly, as in `EnsureAssignable<Address>(_address, old)` with a nullable `old`. A caller that lets the compiler infer `T` gets no warning. The change would alter a line of `PublicAPI.Shipped.txt` for a small gain. The public signature does not change.

**Release notes.** The CHANGELOG has three entries under Changed, each marked BREAKING: one for `null` in `EnsureAssignable<T>()`, one for a `T` that is compared as a whole (ADR-0022), and one for `EnsureImmutableCollection<T>()`. The #47 entry under Fixed was merged into the first. It described a state that users of 2.x never saw, and the two together give one change from 2.x. The entries are marked BREAKING, and the #47 entry was not, for this reason: a call that compares had a documented exception, and it now gives another result for a `null` that the caller passed. Code that caught `ArgumentNullException` there gets `PostconditionViolationException` for one `null` and nothing for two. The reason that the #47 amendment gave ("raised for a value that `Old` returns by design") still explains the half of the first entry that is about a call that does not compare. The third entry is marked BREAKING because the method no longer stops a `null` with postconditions off.

**Why an amendment and not a new ADR.** The main decision of this ADR stands. The amendment of ADR-0022 for #52 gives the reasoning for all four amended ADRs.

Unchanged: the main decision of this ADR, `RequireNotEmpty` (it checks its data argument in every configuration, is a precondition helper and takes no `Old` result), and every other `Contract` method. The public signatures do not change.

### Amendment (2026-10-10): One shared instance passed as actual and as expected is not compared (#54)

The decision is unchanged. [Issue #54](https://github.com/cwouyang/uContract.NET/issues/54) adds one case to `EnsureAssignable<T>()`. A shared type is a type that `Old<T>()` shares with the original instead of copying it. In a call that compares, one instance of a shared type whose state can change, passed as `actual` and as `expected`, is not compared: the method throws `InvalidOperationException`. A `Stream`, a `Task`, a `Component` and the classes derived from them are examples of such types. The rule is rule S of ADR-0022. The amendment of [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md) for #54 records it, with its reasons, the list of the types, the message, the measurements and the limits.

**Qualified in the #52 amendment.** Each statement is quoted by its opening words:

- Step 5 of the rule: "Two values that are not `null` are compared with the recursion guard set." One instance of a shared type whose state can change, passed as `actual` and as `expected`, is not compared. The test for it comes after the `null` rule and before the recursion guard is set. It reads no member of `T` and no member of the value, it runs no user code, and it does not examine the patterns.
- The table row that ends "Compared; see the amendment of ADR-0022 for #52". For such an instance on both sides the result is `InvalidOperationException`; see the amendment of ADR-0022 for #54. The exception is not a `PostconditionViolationException`, because nothing was compared.
- The pair on a member that can be `null`: "For `EnsureAssignable(_address, Contract.Old(() => _address))`:". The table there has no row for a member that keeps its value. When the value of the member is an instance of a shared type whose state can change, for example for a `CancellationTokenSource? _cts`, the pair throws `InvalidOperationException` when the member was not replaced: `Old<T>()` returns the instance itself, so `actual` and `expected` are one instance. The three rows of that table still hold for such a member. To check that a method does not replace such a member, write `Contract.Ensure(() => ReferenceEquals(_cts, old))`.

**Why an amendment and not a new ADR.** The main decision of this ADR stands. The amendment of ADR-0022 for #54 gives the reasoning for all four amended ADRs.

Unchanged: the main decision of this ADR, the check of the pattern array, the `null` rule and its two messages, `EnsureImmutableCollection<T>()`, and every other `Contract` method. The public signatures do not change.

### Amendment (2026-10-10): A FrozenSet or a FrozenDictionary as T is compared as a whole (#49)

The decision is unchanged. [Issue #49](https://github.com/cwouyang/uContract.NET/issues/49) adds one kind of `T` to those that `EnsureAssignable<T>()` compares as a whole. When the declared `T` is a `FrozenSet<T>` or a `FrozenDictionary<TKey, TValue>` ("the two types" below), two values that are not `null` are compared as one pair: element by element, in enumeration order. No member of `T` is compared, and the assignable patterns do not apply and are not examined. The same change makes `EnsureAssignable<T>()` compare two values of the two types that a member or element holds element by element, where it compared them by reference when their runtime types were equal. `Old<T>()` is unchanged: it still shares such a value with the original instead of copying it. The amendment of [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md) for #49 records the change, with its reasons, the measurements and the limits.

**Qualified in the #52 amendment.** Each statement is quoted by its opening words:

- Under "Qualified.": "When `T` is a `string`, a delegate type or a nullable value type, two values that are not `null` are now compared as a whole." The same now holds when `T` is one of the two types. The amendment of ADR-0022 for #49 records that decision.
- The table row that ends "Compared; see the amendment of ADR-0022 for #52". For the two types, see the amendment of ADR-0022 for #49.

**Still true in the #52 amendment.** Each statement is quoted by its opening words:

- Step 5 of the rule: "Two values that are not `null` are compared with the recursion guard set." That stands, with the exception that the #54 amendment above made. The sentence after it names the amendment of ADR-0022 for #52 as the record of how they are compared. For the two types the record is the amendment of ADR-0022 for #49.
- Release notes: "The CHANGELOG has three entries under Changed, each marked BREAKING:". No entry is added for #49. The entry for a `T` that is compared as a whole now also names the two types. The entry of #45 on what `EnsureAssignable<T>()` reports more now says how two values of the two types that a member or element holds are compared. The state between #45 and this change was never released, so no entry describes it.

**Why an amendment and not a new ADR.** The main decision of this ADR stands. The amendment of ADR-0022 for #49 gives the reasoning for all four amended ADRs.

Unchanged: the main decision of this ADR, the check of the pattern array, the `null` rule and its two messages, the rule for one shared instance on both sides (#54), `EnsureImmutableCollection<T>()`, and every other `Contract` method. The public signatures do not change.

### Amendment (2026-10-11): An array type or System.Array as T is compared as a whole; the release notes for #57

The decision is unchanged. [Issue #57](https://github.com/cwouyang/uContract.NET/issues/57) adds one kind of `T` to those that `EnsureAssignable<T>()` compares as a whole. When the declared `T` is an array type or `System.Array`, two values that are not `null` are compared as one pair: element by element, each element as a member or an element is. No member of `T` is compared, and the assignable patterns do not apply and are not examined. The same change makes `EnsureAssignable<T>()` find two arrays equal only when they have the same rank, and the same length and lower bound in every dimension, and compare two arrays of pointers or of function pointers by address. It also makes `Old<T>()` copy an array whose element type is declared in `System.Collections.Frozen`, and a value of a value type that is declared there; `Old<T>()` still shares a `FrozenSet<T>`, a `FrozenDictionary<TKey, TValue>` and every other class that is declared in that namespace. The amendment of [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md) for #57 records the changes, with their reasons, the behaviour before and after, the measurements and the limits.

**Qualified in the #52 amendment.** Each statement is quoted by its opening words:

- Under "Qualified.": "When `T` is a `string`, a delegate type or a nullable value type, two values that are not `null` are now compared as a whole." The #49 amendment above added a `FrozenSet<T>` and a `FrozenDictionary<TKey, TValue>`. The same now holds when `T` is an array type or `System.Array`. The amendment of ADR-0022 for #57 records that decision.
- The table row that ends "Compared; see the amendment of ADR-0022 for #52". For an array type or `System.Array` as `T`, see the amendment of ADR-0022 for #57.

**Qualified in the #49 amendment.** Each statement is quoted by its opening words:

- The Status line of 2026-10-10 for #49: "when `T` is a `FrozenSet<T>` or a `FrozenDictionary<TKey, TValue>`, `EnsureAssignable<T>()` compares two values that are not `null` as a whole". It now also does so when `T` is an array type or `System.Array`.
- "`Old<T>()` is unchanged: it still shares such a value with the original instead of copying it." That stands for a `FrozenSet<T>` and a `FrozenDictionary<TKey, TValue>`. `Old<T>()` no longer shares an array of them: it copies the array, and each element of the copy is the original's set or dictionary.
- Release notes, under "Still true in the #52 amendment": "No entry is added for #49." That is true of #49. For #57 two entries are added; "Release notes" below says which.

**Release notes.** This is what `CHANGELOG.md` holds under `[Unreleased]` for #57:

- **One entry is added under Changed, marked BREAKING.** It begins "`EnsureAssignable<T>()` finds two arrays equal only when they have the same rank, and the same length and lower bound in every dimension." It names the pairs that 2.0.0 found equal and that are a violation now. It says that the rule is for two arrays, and not for an array and another sequence, and not for two arrays that are passed as a `T` that is neither an array type nor `System.Array`. It says that the violation does not name the rank, the length or the lower bound that differs.
- **One entry is added under Fixed.** It is not marked BREAKING. It begins "`Old<T>()` no longer throws `ArgumentException` for an object that holds an array of pointers or of function pointers, and `EnsureAssignable<T>()` no longer throws `NotSupportedException` for such an object or for such an array as `T`."
- **Existing entries are extended.** The entry for a `T` that is compared as a whole, which the #52 amendment names, now also names an array type and `System.Array`, says what 2.0.0 reported for an array as `T`, and carries "(#52, #57; ADR-0022)". The entry that begins "`Old<T>()` copies by reflection instead of JSON" now says that a `FrozenSet<T>`, a `FrozenDictionary<TKey, TValue>` and every other class that is declared in `System.Collections.Frozen` are shared, that a value type declared there and an array whose element type is declared there are copied, and that an element of a copied array or list keeps its own rule. The entry that begins "`EnsureAssignable<T>()` reports more" now says "class" where it said "type" in its sentence on two instances that are compared by reference, no longer gives two arrays of `FrozenSet<T>` as an example there, and points to the new entry for two arrays. The paragraph "Migrating from 2.x" gains sentences on an array as `T` and on the rank, lengths and lower bounds of two arrays.

The reasons. The entry for the rank, lengths and lower bounds is new and is marked BREAKING because calls that passed in 2.0.0 now throw, and no existing entry said so: the pairs that the entry names were measured with the 2.0.0 package and gave no exception there. 2.0.0 did report two one-dimensional arrays of different lengths, so the entry does not say that 2.0.0 compared none of the three; it names the pairs that were measured. The entry for the arrays of pointers and of function pointers is under Fixed: in the eight calls that were measured with the 2.0.0 package, `Old<T>()` threw `ArgumentException` and the comparison threw `NotSupportedException`. No entry is added for the arrays and the value types of `System.Collections.Frozen`: the design gives as the reason that, against 2.0.0, they bring nothing that the existing entries do not already state (A10 in the amendment of ADR-0022 for #57). The #49 amendment above handled #49 in that way.

**Why an amendment and not a new ADR.** The main decision of this ADR stands. The amendment of ADR-0022 for #57 gives the reasoning for all four amended ADRs.

Unchanged: the main decision of this ADR, the check of the pattern array, the `null` rule and its two messages, the rule for one shared instance on both sides (#54), `EnsureImmutableCollection<T>()`, and every other `Contract` method. The public signatures do not change.

### Amendment (2026-10-12): EnsureImmutableCollection decides by interfaces and base types, compared by full name (#61)

The decision is unchanged. [Issue #61](https://github.com/cwouyang/uContract.NET/issues/61) was filed for one defect: `EnsureImmutableCollection<T>()` ended in `BadImageFormatException` for an array of function pointers. The change grew beyond that defect: the maintainer decided the rule by which the method decides. This amendment records the rule, the comparison with the method of the Java library, the decisions and their reasons, the rejected alternatives, the limits, and the statements of this ADR that no longer hold as written. The amendment of [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md) of the same date records the measured results, and that of [ADR-0021](0021-postconditions-under-trimming-and-aot.md) the trimming suppression and the smoke checks.

"Measured" below means measured on .NET 8 under the JIT, unless the line names another runtime: with unit tests of the repository, or with throwaway tests and programs that are not in the repository. The amendment of ADR-0022 says which. "2.0.0" is the 2.0.0 package of nuget.org, and "before this change" is the library as it was before the work for #61.

**The rule.** The method checks the collection with postconditions on, and not in a call made while another contract check is running. That, the `null` rule of the #52 amendment above, the returned value and the message of the violation (`Ensure resultingCollection is an immutable collection`) are unchanged. When the method checks, it reads the runtime type of the argument with `collection.GetType()` and decides from that type in five steps:

1. **An array type is a violation, whatever its element type.** The method tests `Type.IsArray` first and reads nothing else from an array type.
2. A type that is not an array is accepted when it **implements a generic interface whose definition has the full name of `IImmutableList<T>`, `IImmutableSet<T>`, `IImmutableDictionary<TKey, TValue>`, `IImmutableStack<T>` or `IImmutableQueue<T>`** ("the five interfaces" below). The method takes the interfaces from `Type.GetInterfaces()`. A class whose base class declares the interface is accepted, and so is a class that implements it explicitly (unit tests hold both).
3. A type that is not an array is accepted when it **is or derives from a generic type whose definition has the full name of `FrozenSet<T>` or `FrozenDictionary<TKey, TValue>`** ("the two frozen types").
4. A type that is not an array is accepted when it **is or derives from a generic type whose definition has the full name of `ReadOnlyCollection<T>` or `ReadOnlyDictionary<TKey, TValue>`, or the full name ``System.Collections.ObjectModel.ReadOnlySet`1``** ("a read-only wrapper"; "the three wrappers").
5. A type that is not an array and meets none of the three conditions is a violation.

"The full name" is `Type.FullName` of the generic type definition: the namespace, the name and the number of type parameters, as in ``System.Collections.Immutable.IImmutableList`1``. The method compares the whole name, ordinally. It does not compare the assembly that declares the definition. A full name counts only in the place of its condition: a class that has the full name of one of the five interfaces, and a class that implements an interface with the full name of one of the two frozen types, are violations (unit tests hold both). A definition whose `FullName` is `null` matches no name. The library is built for .NET 8 and cannot refer to `System.Collections.ObjectModel.ReadOnlySet<T>`, a type of a later framework (measured on .NET 10, where `System.Private.CoreLib` declares it), so it holds that one full name as text; it reads the other nine names from the types of the framework.

The declared `T` plays no part: an `ImmutableList<int>` passed as `IEnumerable<int>` or as `object`, and a `FrozenSet<int>` passed as `IReadOnlySet<int>` or as `object`, are accepted (measured). The method calls `GetType()` on the argument and no other member of it. It reads no element, and it keeps no state per type: there is no cache.

Step 1 comes first, and one input shows it: on .NET 8 under the JIT, `Type.GetInterfaces()` of the type `delegate*<void>[]` throws `ArgumentException` (measured with a throwaway program), and with the array test taken out of the method, the unit test for an array of function pointers fails with that exception. The answers do not show the order of steps 2 to 4. In the code the two walks of the base classes come before the read of the interfaces.

Before this change the method accepted a runtime type whose `Type.Namespace` starts with `System.Collections.Immutable`, or whose `Type.Name` starts with `Immutable` or `ReadOnly`. It read the name and the namespace of an array type too.

**In the code.** `Contract.EnsureImmutableCollection<T>()` tests `IsArray` and then calls the private method `_MeetsImmutableCollectionCondition` twice. The first call compares each generic type definition with the nine types that the conditions name and that the library can refer to, and reads no name. Only when that gives no match, the second call compares full names with ten names that the private nested class `ImmutableCollectionFullNames` holds: nine are read from `typeof(…).FullName` in its static fields, and the tenth is the constant `ReadOnlySetFullName`. The amendment of ADR-0022 gives the measurement that led to the two calls. `_IsFrozenSetOrDictionary` and `_IsReadOnlyWrapper` walk the base classes, and `_ImplementsImmutableCollectionInterface` reads the interfaces; the last carries the suppression of trimming warning `IL2070` that the amendment of ADR-0021 records. The method does not use `SharedTypes`.

**Compared with the upstream method.** The design of this change records the upstream rule so: `ensureImmutableCollection` of the Java library accepts a value whose class has a simple name that starts with `Unmodifiable` or `ListN` (`Contract.java`, line 331, at upstream commit `7786f3f`), and its violation has the same message. The rule above differs from it in both directions:

- **Stricter.** It compares the whole full name of a generic type definition, in the place that a condition gives (an implemented interface, or the type or one of its base classes), and not the start of a simple name. An array is a violation, whatever its element type.
- **Wider.** The immutable and the frozen collections of the framework that were measured are accepted: an `ImmutableArray<int>`, an `ImmutableList<int>`, an `ImmutableHashSet<int>`, an `ImmutableSortedSet<int>`, an `ImmutableDictionary<int, int>`, an `ImmutableSortedDictionary<int, int>`, an `ImmutableStack<int>`, an `ImmutableQueue<int>`, and nine `FrozenSet<T>` and `FrozenDictionary<TKey, TValue>` values of different element types and sizes. A type of the caller that implements one of the five interfaces is accepted without a further test.

The rule of the method before this change was not the upstream rule either: it named other prefixes and a namespace.

**Decisions and reasons.** The maintainer made them. They are numbered I1 to I9 so that they are not taken for D1 to D13, S1 to S10, F1 to F10 or A1 to A12 of ADR-0022. I1 to I5, I7, the first sentence of I8 and I9 were decided on 2026-10-11, before the code changed. I6 and the second sentence of I8 were decided on 2026-10-12, after the measurements of the first implementation.

| # | Question | Decision |
|---|----------|----------|
| I1 | An array | A violation, whatever its element type and its rank. Nothing but "is an array" is read from an array type. |
| I2 | The frozen collections | A `FrozenSet<T>`, a `FrozenDictionary<TKey, TValue>` and a type that derives from one of them are accepted. The namespace `System.Collections.Frozen` is not a criterion. |
| I3 | What replaces the name and the namespace | The five interfaces, I2 and I4. Another type is a violation. No member of the instance is read. |
| I4 | The read-only wrappers | A `ReadOnlyCollection<T>`, a `ReadOnlyDictionary<TKey, TValue>` and a class derived from one of them are accepted; a `ReadOnlyObservableCollection<T>` is accepted by this. So is `System.Collections.ObjectModel.ReadOnlySet<T>` of a later framework, which the library recognises by the full name of its generic definition. The key and value collections of a `ReadOnlyDictionary<TKey, TValue>` are not such types and stay a violation. |
| I5 | What `ArrayList.ReadOnly(...)` returns | A violation. These non-generic wrappers are not named by the rule. |
| I6 | How the types of I2 to I4 are compared | By the full name of the generic type definition, not by the type, as I4 already did for `ReadOnlySet<T>`. |
| I7 | The message, the signature and the attributes of the public method | Unchanged. The public method gets no trimming attribute. The trimming warning of the interface read is suppressed on a private method (ADR-0021). |
| I8 | State | The method keeps no state per type: no cache is added. The library keeps a reference to each framework type that it names and can refer to; names that are read once from those types and kept are not state per type. |
| I9 | Release notes | Under Changed, marked BREAKING, one entry for arrays and one for the rule for a type that is not an array. Under Fixed, one entry for arrays of function pointers and one for the frozen collections. "Release notes" below says what the CHANGELOG holds. |

The reasons, as far as the design and the measurements give them:

- **I1.** Issue #61 asks for it: an array is not an immutable collection, and the method should answer for an array of function pointers as it does for an `int[]`. In 2.0.0 an `ImmutableList<int>[]` passed and a `delegate*<void>[]` ended in `BadImageFormatException`. The #57 amendment of ADR-0022 records that the runtime reports the namespace of an array type as that of its innermost element type, and that reading it threw for an array of function pointers (measured there under the JIT). The method read that namespace.
- **I2.** In 2.0.0 a `FrozenSet<int>` and a `FrozenDictionary<int, int>` were violations. The two types are recognised as the #49 amendment of ADR-0022 recognises them, by the type and its base classes. "The namespace of the frozen collections" under "Rejected alternatives" says why the namespace is not used.
- **I4.** The design gives three reasons: the upstream method accepts the unmodifiable wrappers, the method accepted a `ReadOnlyCollection<T>` before, and a class derived from a wrapper deserves the same trust as whoever holds the wrapped collection.
- **I5.** Before this change the result of `ArrayList.ReadOnly(...)` passed by its name alone (measured: the name of its type is `ReadOnlyArrayList`). The design adds that these wrappers are not public types that the rule could name.
- **I6.** The first implementation compared types, and it was measured: an `ImmutableList<int>.Empty` of a second copy of the assembly `System.Collections.Immutable`, loaded into another `AssemblyLoadContext`, passed in 2.0.0 and was a violation with the comparison of types. The maintainer decided that it must not become a violation. With the comparison of full names it is accepted.
- **I7.** An annotation on the public method would put a trimming diagnostic on its callers; the amendment of ADR-0021 gives the measurement.
- **I8.** The second sentence keeps true what the justification of the suppression says: the library itself refers to the five interface types.

**Rejected alternatives.**

- **The read-only flag of `ICollection<T>`.** Measured before this change: `ICollection<T>.IsReadOnly` is true for an `int[]`, for the `Keys` of a `Dictionary<int, int>`, for an `ArraySegment<int>` and for `Enumerable.Empty<int>()`, each of which is a violation before and after this change; an `ImmutableStack<int>` and an `ImmutableQueue<int>` do not implement `ICollection<T>` and have no such flag. Reading the flag would also call a member of the instance, which the rule does not do (I3).
- **Exact types only for the wrappers.** A class derived from a wrapper would then be a violation. Measured: a `ReadOnlyObservableCollection<int>`, which derives from `ReadOnlyCollection<int>`, passed before this change, and a class named `ReadOnlyOrders` that derives from `ReadOnlyCollection<int>` passed in 2.0.0. I4 gives the reason for accepting a derived class.
- **The namespace of the frozen collections.** Measured before this change: `Type.Namespace` is `System.Collections.Frozen` also for a `FrozenSet<int>.Enumerator`, for a `FrozenDictionary<int, int>.Enumerator`, for a class and for a struct that the test assembly declares in that namespace, and for a `FrozenSet<int>[]`. Under the rule each of these five is a violation (measured).
- **Comparing types instead of full names.** Built first and measured (I6). Of 19 inputs from a second copy of the assembly and from a dynamically built assembly, 12 gave another answer with the comparison of types than with the comparison of full names; among them the seven collections of the second copy that are named under limit 8.

**Limits.** A pass is a statement about the runtime type, and not a proof that the content cannot change. The API reference states these limits for users, with the ways out, under "What `EnsureImmutableCollection<T>()` accepts".

1. **The runtime type decides, not the content.** An `ImmutableArray<int[]>` is accepted (measured), although each of its elements is an array.
2. **A read-only wrapper is a view.** It is accepted whatever it wraps: the method does not look at the wrapped collection. A class of the test assembly that derives from `ReadOnlyCollection<int>` is accepted (measured).
3. **An interface is believed.** A class of the smoke program that implements `IImmutableStack<int>`, and whose members throw, is accepted (smoke check 89, in the amendment of ADR-0021): the method calls none of them.
4. **Read-only is not enough.** These are violations (measured): a class of the test assembly that implements `ICollection<int>` with `IsReadOnly` true, what `ArrayList.ReadOnly(...)` returns, a collection expression assigned to an `IReadOnlyList<int>` or to an `IEnumerable<int>`, `Enumerable.Empty<int>()`, the result of `Enumerable.Select`, a `ReadOnlyMemory<int>` and a `ReadOnlySequence<int>`.
5. **Keys and values differ by collection.** The `Keys` and the `Values` of a `FrozenDictionary<int, int>` and the `Items` of a `FrozenSet<int>` are accepted: they are `ImmutableArray<T>` values. The `Keys` and the `Values` of an `ImmutableDictionary<int, int>`, of an `ImmutableSortedDictionary<int, int>` and of a `ReadOnlyDictionary<int, int>` are violations (measured).
6. **An immutable type that the rule does not name is a violation.** This is read from the rule: a collection type that is not an array and meets none of the three conditions is reported as mutable, whatever it lets a caller do.
7. **The instance is not examined.** `default(ImmutableArray<int>)` is accepted (measured).
8. **A type that only has the name is accepted.** The case that I6 is for is a second copy of the assembly `System.Collections.Immutable`, loaded into another `AssemblyLoadContext`. Measured: the `Empty` values of `ImmutableList<int>`, `ImmutableStack<int>`, `ImmutableDictionary<int, int>`, `ImmutableHashSet<int>`, `ImmutableQueue<int>`, `FrozenSet<int>` and `FrozenDictionary<int, int>` of such a copy are accepted, and its `ImmutableList<int>.Builder` and an array of its `ImmutableList<int>` are violations. A type that merely carries a name is accepted with it. Measured with a dynamically built assembly: a class whose generic definition has the full name of `ReadOnlyCollection<T>`, of `ReadOnlyDictionary<TKey, TValue>`, of `FrozenSet<T>` or of `ReadOnlySet<T>` is accepted, and so is a class that implements an interface with the full name of `IImmutableStack<T>` or of `IImmutableList<T>`. Violations there: a class named ``ReadOnlyCollection`1`` in another namespace, a class that implements an interface named ``IImmutableStack`1`` in another namespace, a class that is not generic and has the full name `System.Collections.ObjectModel.ReadOnlyCollection`, and a class that has the full name of `ImmutableList<T>` and implements none of the five interfaces. The text of the name decides, and not the number of type parameters: a class of such an assembly whose name is ``System.Collections.ObjectModel.ReadOnlyCollection`1`` and that has two type parameters is accepted (measured).
9. **The version of an assembly is not compared.** This is read from the rule and was not measured.
10. **A call can allocate more than in 2.0.0.** The amendment of ADR-0022 holds the numbers. The maintainer accepted the cost on 2026-10-12.
11. **`Old<T>()` and `EnsureAssignable<T>()` use other criteria for a frozen collection.** This method accepts a `FrozenSet<int>` of a second copy of the assembly by a full name. `Old<T>()` and the comparison of `EnsureAssignable<T>()` do not compare full names. The amendment of ADR-0022 holds what was measured for such a set. The maintainer decided on 2026-10-12 to state this as a limit and to open a follow-up issue; the issue was not filed when this amendment was written.

**Superseded in the #52 amendment.** The statement is quoted by its opening words:

- The last row of the table under "The rule for `EnsureImmutableCollection<T>()`": "checks", "not `null`", "Checks as before (unchanged)". That was true of #52. The check is now the rule above. The other three rows of that table still hold.

**Qualified in the #52 amendment.** The statement is quoted by its opening words:

- "**Release notes.** The CHANGELOG has three entries under Changed, each marked BREAKING:", which ends "and one for `EnsureImmutableCollection<T>()`." That entry is still there. It is no longer the one entry on this method: "Release notes" below gives the entries of #61.

**Qualified in the #54, #49 and #57 amendments.** Each of the three ends with a sentence that begins "Unchanged: the main decision of this ADR" and names `EnsureImmutableCollection<T>()` among what is unchanged. Each is true of its own change. The method changes with #61.

**Still true in the #52 amendment.** Each statement is quoted by its opening words:

- "**The rule for `EnsureImmutableCollection<T>()`.** The method checks the collection with postconditions on, and not in a call made while another contract check is running."
- "A call that does not check does not change the recursion guard."
- Under "Rejected alternatives": "**Report a `null` `collection` as a violation when the method checks the collection.**" The method keeps `ArgumentNullException` for a `null` `collection` when it checks.

**Qualified in the accepted text.** The statement is quoted by its opening words:

- The decision: "We will improve upon the Java implementation where .NET conventions differ, while maintaining full semantic equivalence", and under Constraints "Must maintain **semantic equivalence** with Java version". The accepted text does not name this method. What this method accepts differs from what the upstream method accepts, in both directions, and it did so before this change; "Compared with the upstream method" above gives the difference.

**Release notes.** This is what `CHANGELOG.md` holds under `[Unreleased]` for #61:

- **Two entries are added under Changed, each marked BREAKING.** The first begins "`EnsureImmutableCollection<T>()` reports an array as mutable, whatever its element type." The second begins "`EnsureImmutableCollection<T>()` decides for a type that is not an array by the interfaces that the runtime type of the argument implements and by the generic types that it is or derives from". The second names what is no longer accepted, what is accepted now and what did not change, each with inputs that were measured with the 2.0.0 package.
- **One entry is added under Changed and is not marked BREAKING.** It begins "A call of `EnsureImmutableCollection<T>()` that passes can allocate more." It was added after the decision of 2026-10-12 on the cost.
- **Two entries are added under Fixed.** One begins "`EnsureImmutableCollection<T>()` throws `PostconditionViolationException` for an array of function pointers", and one "`EnsureImmutableCollection<T>()` accepts a `FrozenSet<T>` or a `FrozenDictionary<TKey, TValue>`".
- **The paragraph "Migrating from 2.x"** gains sentences on arrays, on the rule for a type that is not an array, and on the ways out.

The two entries under Changed are marked BREAKING because calls that passed in 2.0.0 now throw `PostconditionViolationException`: measured with the 2.0.0 package for an `ImmutableList<int>[]`, an `ImmutableList<int>.Builder`, a `ReadOnlyMemory<int>` and the other inputs that the entries name. The two entries under Fixed are for calls that did not give the contract's answer in 2.0.0: a `delegate*<void>[]` ended in `BadImageFormatException`, and a `FrozenSet<int>` and a `FrozenDictionary<int, int>` were violations.

**Why an amendment and not a new ADR.** The main decision of this ADR stands. The change gives one method another rule, as the amendments for #52, #49 and #57 did for `EnsureAssignable<T>()`. `docs/adr/README.md` lists breaking changes under "Requires ADR". The amended ADRs (ADR-0012, ADR-0022 and ADR-0021) are that record. ADR-0007 does not name the method and is not amended.

Unchanged: the main decision of this ADR, the `null` rule of `EnsureImmutableCollection<T>()` and when the method checks, its signature, its message, and the other `Contract` methods. `Old<T>()` and `EnsureAssignable<T>()` are not changed: the code of `SharedTypes` is as it was.

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
| 2026-10-09 | Amended     | `EnsureAssignable<T>()` checks `actual` and `expected` for `null` only when it compares them (#47). Decision unchanged. See Implementation Notes > Amendment. |
| 2026-10-09 | Amended     | `EnsureAssignable<T>()` compares `null` as a value; `EnsureImmutableCollection<T>()` checks `null` only when it checks (#52). Supersedes parts of the #47 amendment. Decision unchanged. See Implementation Notes > second Amendment. |
| 2026-10-10 | Amended     | One instance of a shared type whose state can change, passed as `actual` and as `expected`, is not compared: `InvalidOperationException` (#54, rule S of ADR-0022). Qualifies parts of the #52 amendment. Decision unchanged. See Implementation Notes > third Amendment. |
| 2026-10-10 | Amended     | A `FrozenSet<T>` or a `FrozenDictionary<TKey, TValue>` as `T` is compared as a whole (#49, ADR-0022). Qualifies parts of the #52 amendment. Decision unchanged. See Implementation Notes > fourth Amendment. |
| 2026-10-11 | Amended     | An array type or `System.Array` as `T` is compared as a whole; the release notes gain one BREAKING entry (rank, lengths and lower bounds of two arrays) and one entry under Fixed (arrays of pointers and of function pointers) (#57, ADR-0022). Qualifies parts of the #52 and #49 amendments. Decision unchanged. See Implementation Notes > fifth Amendment. |
| 2026-10-12 | Amended     | `EnsureImmutableCollection<T>()` decides by a new rule: an array is a violation; a type that is not an array is accepted by five interfaces, by the two frozen types or by a read-only wrapper, compared by the full name of the generic type definition (#61, ADR-0022). Supersedes one table row of the #52 amendment and qualifies parts of the #52, #54, #49 and #57 amendments. Decision unchanged. See Implementation Notes > sixth Amendment. |
