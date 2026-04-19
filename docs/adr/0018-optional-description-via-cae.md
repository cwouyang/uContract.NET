# ADR-0018: Optional Description via CallerArgumentExpression

## Status

**Accepted**

- **Date**: 2026-04-19
- **Deciders**: Project maintainers
- **Status Date**: 2026-04-19

---

## Context

### Problem Statement

Every public DBC method in this library takes a `description` string as its first parameter. ADR-0012 requires this parameter to be non-null at runtime and validated via `ArgumentNullException.ThrowIfNull`. The mandatory description places a writing burden on callers even when the condition or value being checked is already self-documenting: `Contract.Require("x > 0", () => x > 0)` duplicates the expression in prose, and `Contract.RequireNotNull("user", user)` merely restates the variable name.

The friction is visible in the Java uContract source: the test corpus repeatedly uses trivial annotations like `"true"`, `"object"`, and `"a+b"` — placeholders that contribute zero semantic value. The Java authors pay this tax because Java lacks compile-time expression capture; `String annotation` is required and there is no language feature to auto-populate it from the source text of another argument.

C# 10 (.NET 6+) introduced `[CallerArgumentExpression]`, a compile-time attribute that lets an optional parameter's default value be automatically populated with the verbatim source text of another argument. This enables description-less callers in .NET without changing any runtime invariant — the compiler fills in the description before the call is issued, so the runtime still receives a non-null string.

### Relevant Context

- ADR-0012 enumerates five ".NET over Java" improvements, establishing the pattern of exploiting .NET features for better API ergonomics where Java cannot.
- The project targets .NET 8 (ADR-0001), so `[CallerArgumentExpression]` is available.
- The 2026-04-19 pre-publish review (Angle 5) flagged "CallerArgumentExpression 全缺" as a meaningful DBC UX gap.
- Java's friction is not a design choice — its language offers no equivalent (reflection cannot recover argument source text; bytecode manipulation via AspectJ / Javassist is too heavy for this library's zero-dependency ethos). Java uContract simply cannot offer this feature.

### Constraints

- ADR-0012's runtime invariant (`description` non-null, validated with `ArgumentNullException.ThrowIfNull`) must continue to hold.
- ADR-0011 zero-dependency principle must hold. `[CallerArgumentExpression]` is a compiler feature, not a runtime dependency; the attribute type lives in `System.Runtime.CompilerServices` in the BCL.
- Existing public API call sites must continue to compile and behave unchanged (backward compatibility).

---

## Decision

**Add a sixth improvement to the ".NET over Java" set established by ADR-0012: eight new public overloads that use `[CallerArgumentExpression]` so callers can omit the `description` parameter and let the compiler auto-populate it from the source text of the condition or value argument. The existing description-first overloads remain unchanged.**

### Details

For each eligible method, add a new overload that reorders parameters so the condition or value comes first and the description becomes an optional, CAE-populated string:

```csharp
// Existing (unchanged)
public static void Require(string description, Func<bool> condition) { ... }

// New
public static void Require(
    Func<bool> condition,
    [CallerArgumentExpression(nameof(condition))] string? description = null)
{
    ArgumentNullException.ThrowIfNull(condition);
    // ... identical guard + config check flow as the existing overload ...
    if (!condition())
        throw new PreconditionViolationException($"Precondition violated: {description}");
}
```

The eight new overloads are:

**Condition-based** — capturing a `Func<bool>` expression:
- `Require(Func<bool> condition, [CAE] string? description = null)`
- `Ensure(Func<bool> condition, [CAE] string? description = null)`
- `Invariant(Func<bool> condition, [CAE] string? description = null)`
- `Check(Func<bool> condition, [CAE] string? description = null)`

**Value-based** — capturing a variable or expression yielding a value:
- `RequireNotNull<T>(T? value, [CAE] string? description = null) where T : class`
- `RequireNotEmpty(string? value, [CAE] string? description = null)`
- `EnsureNotNull<T>(T? value, [CAE] string? description = null) where T : class`
- `InvariantNotNull<T>(T? value, [CAE] string? description = null) where T : class`

Methods **not** receiving a CAE overload:
- `Ignore` — its `reason` parameter documents *why* the exception is tolerated, not *what* condition is checked; captured expression adds no value.
- `EnsureResult<T>` — three-parameter shape (description, result, assertion) with no clear single capture target.
- `EnsureImmutableCollection<T>`, `CheckUnsupportedOperation`, `EnsureAssignable<T>` — no condition or value from which a meaningful description could be derived.

Error-message format for the new overloads: `"Precondition violated: {captured-or-user-description}"` — identical to the existing format. The captured string contains whatever source text the caller wrote, including `() =>` when the argument is a lambda (e.g., `"() => balance >= 0"`); no runtime string manipulation to strip the prefix, which would be brittle and misleading when the caller passes a method group or variable reference.

---

## Consequences

### Positive Consequences

- ✅ `Contract.RequireNotNull(user)` — variable name captured automatically; zero writing burden for the most common "non-null guard" case.
- ✅ `Contract.Require(() => balance >= 0)` — condition expression captured; one-liner for self-documenting checks.
- ✅ Exception messages contain the exact source text written at the call site, improving debuggability.
- ✅ Zero runtime cost — the compiler embeds the captured string as a literal at compile time.
- ✅ Purely additive; no existing call site changes.
- ✅ Extends ADR-0012's documented improvement pattern without introducing a new design philosophy.
- ✅ Represents a meaningful ergonomic advantage that Java uContract cannot offer.

### Negative Consequences

- ❌ Public API surface nearly doubles (13 → 21 methods). More to document, test, and maintain.
- ❌ Two ways to write the same intent at every call site — projects that want a single consistent style must pick one and enforce it.
- ❌ Overload-resolution edge case: `Contract.Require(myCondition, "description")` (where `myCondition` is `Func<bool>`) compiles and uses the new overload, with `"description"` silently overriding the CAE default. The call succeeds but the reader's likely expectation (description-first) is inverted. Detection belongs to tests and code review, not runtime.
- ❌ Captured lambda strings carry the `() =>` prefix, which is slightly verbose in error messages.

### Neutral Consequences

- ⚖️ Adds `using System.Runtime.CompilerServices;` to `Contract.cs`.
- ⚖️ README "Differences from Java Version" table should gain a row documenting this improvement, with a blockquote pointing to this ADR (same pattern as the entry added for ADR-0016).
- ⚖️ `[CallerArgumentExpression]` is already available in the BCL for .NET 6+; no new package reference.

---

## Alternatives Considered

### Alternative 1: Retrofit existing methods with an additional CAE parameter (no new overloads)

**Description**: Extend each existing method to take a third parameter `[CallerArgumentExpression(nameof(condition))] string? conditionExpression = null`, then include both values in the error message: `"Precondition violated: {description} ({conditionExpression})"`.

**Pros**:
- No growth in method count.
- Combines the user-supplied description and the auto-captured expression for maximum information in the failure message.

**Cons**:
- Messages become duplicative noise when the description and captured expression convey the same idea (`"balance non-negative (() => balance >= 0)"`).
- Changes the format of existing exception messages — a breaking change for any consumer that parses `Exception.Message`.
- Forces callers into a single verbose style rather than letting them choose a terse form when appropriate.

**Why rejected**: The dual-info message is worse than either the description-only or the expression-only form for the common case. Adding ambient verbosity to satisfy every possible information need contradicts the concise-message ethos of the existing library.

---

### Alternative 2: Replace existing overloads with CAE-first ordering (breaking change)

**Description**: Change the single overload of each method from `(string description, Func<bool> condition)` to `(Func<bool> condition, [CallerArgumentExpression(nameof(condition))] string? description = null)`. No new overloads; existing callers migrate.

**Pros**:
- Cleanest single-method API surface.
- No API doubling.

**Cons**:
- Hard-breaks every existing caller and every test that passes description first.
- Contradicts ADR-0012's pattern of layering .NET improvements additively over preserved Java-shaped APIs.
- Incompatible with the project's intent to release a stable v1.0 soon — a breaking change at this juncture has a high cost with no offsetting benefit beyond pure aesthetic.

**Why rejected**: Breaking-change cost dwarfs the benefit of a smaller API surface.

---

### Alternative 3: Do not adopt CAE

**Description**: Leave the API unchanged; callers continue to supply an explicit description on every call.

**Pros**:
- Zero API churn, zero new code to test or document.
- Forces every call to carry a prose description, which some teams consider a documentation-discipline win.

**Cons**:
- Leaves the documented Angle 5 UX gap unclosed.
- The Java test corpus (e.g., `require("true", () -> true)`) demonstrates the friction is real even for the upstream library's own authors.
- .NET offers a zero-cost language feature that eliminates the friction; not using it is an avoidable opportunity cost.

**Why rejected**: The friction evidence is concrete, and the solution is free. The documentation-discipline argument is valid for teams that want that rigour — and those teams can simply keep using the existing description-first overloads, which remain available.

---

## Related Decisions

- **Extends**: ADR-0012 (.NET Improvements Over Java Implementation) — this ADR adds a sixth entry to ADR-0012's list of improvements, reusing the same design philosophy: exploit .NET features, preserve semantic equivalence, maintain all runtime invariants.
- **Related to**: ADR-0005 (Generics, Type Constraints, and Nullable Reference Types) — the four value-based overloads retain `where T : class` per ADR-0005's rule for null-check methods.
- **Related to**: ADR-0008 (Exception Hierarchy Design) — exception types and message-format scheme (`"{Type} violated: {description}"`) are unchanged.

---

## Implementation Notes

- Add `using System.Runtime.CompilerServices;` at the top of `src/uContract/Contract.cs` to bring `[CallerArgumentExpression]` into scope.
- Use `[CallerArgumentExpression(nameof(condition))]` (or `nameof(value)`) rather than a hard-coded parameter name string, so renaming the parameter updates the attribute reference automatically.
- Follow TDD: add tests for each new overload before the implementation. Each new overload gets its own test class (e.g., `RequireCaeTests`, `RequireNotNullCaeTests`) in `tests/uContract.Tests/` mirroring the existing one-class-per-method convention.
- Per-class minimum test coverage:
  - CAE-capture verification — call without description, assert the exception message contains the expected captured expression.
  - Explicit-override — call with an explicit description argument, assert the explicit value wins over the CAE default.
  - Null-condition guard — confirm `ArgumentNullException.ThrowIfNull` fires as on the existing overload.
  - Exception propagation (condition-based overloads) — the condition's own exceptions propagate unmodified.
- Update README's "Differences from Java Version > .NET Improvements" table with a new row documenting this capability, followed by a blockquote pointing to this ADR — matching the pattern used for ADR-0016.
- This ADR is immutable once accepted. Future changes to CAE usage (additional methods getting overloads, or changes to captured-expression formatting) require a new ADR.

---

## References

- [`CallerArgumentExpressionAttribute` — Microsoft Learn](https://learn.microsoft.com/dotnet/api/system.runtime.compilerservices.callerargumentexpressionattribute)
- [C# 10: `CallerArgumentExpression` attribute diagnostics — Microsoft Learn](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-10#callerargumentexpression-attribute-diagnostics)
- [Java uContract 2.0.1 test corpus (commit `cb1e03f`)](https://gitlab.com/TeddyChen/ucontract/-/tree/cb1e03f/src/test/java/tw/teddysoft/ucontract) — evidence of trivial-annotation friction
- [ADR-0012: .NET Improvements Over Java Implementation](0012-dotnet-improvements-over-java.md)
- [ADR-0005: Generics, Type Constraints, and Nullable Reference Types](0005-generics-type-constraints-nullable.md)
- [ADR-0008: Exception Hierarchy Design](0008-exception-hierarchy.md)

---

## Revision History

| Date       | Status      | Notes                          |
|------------|-------------|--------------------------------|
| 2026-04-19 | Accepted    | Decision recorded during pre-publish review. Extends ADR-0012 with a sixth .NET-over-Java improvement: description-less DBC calls via `[CallerArgumentExpression]` on new additive overloads. |
