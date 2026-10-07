# ADR-0016: No TypeReference Overload for Old<T>()

## Status

**Accepted**

- **Date**: 2026-04-19
- **Deciders**: Project maintainers
- **Status Date**: 2026-04-19
- **Amended**: 2026-10-04 — the implication that diagnostics alone make `Old<T>()` usable under Native AOT is superseded by [ADR-0021](0021-postconditions-under-trimming-and-aot.md); the decision stands; see the Amendment under Implementation Notes
- **Amended**: 2026-10-07 — `Old<T>()` no longer uses JSON ([ADR-0022](0022-faithful-old-copies-and-robust-comparison.md)), so the JSON rationale and Alternative 3 no longer apply; the decision stands; see the second Amendment under Implementation Notes

---

## Context

### Problem Statement

The Java uContract 2.0.1 source (`cb1e03f:Contract.java:495`) provides an overload `old(Supplier<T> supplier, TypeReference<T> typeReference)` that accepts a Jackson `TypeReference<T>` to preserve generic type information across Java's type erasure boundary. The .NET port (initial commit 2025-10-19) did not implement this overload. A pre-publish review on 2026-04-19 flagged the absence and raised the question: should the overload be added for API parity, or is its omission justified and worth documenting?

### Relevant Context

- **Java uses type erasure**: at runtime only the raw type is retained (`List<String>` becomes `List`), so Jackson requires an anonymous `TypeReference<T>` subclass to carry full generic information into the deserializer.
- **.NET uses reified generics**: the closed generic type is preserved at runtime (`typeof(List<string>)` yields the full closed type at runtime).
- `System.Text.Json.JsonSerializer.Deserialize<T>(json, options)` receives `T` as a reified closed type; no external type hint is needed to round-trip generic collections such as `List<string>` or `Dictionary<int, User>`.
- ADR-0006 established `System.Text.Json` with `JsonSerializer.Serialize(obj, options)` / `Deserialize<T>(json, options)` as the deep-copy mechanism for `Old<T>()`.
- ADR-0012 established the porting principle of *semantic equivalence with the Java version, not byte-for-byte API parity*.

### Constraints

- Must remain zero-dependency (ADR-0011: Zero-Dependency Principle).
- Must preserve DBC semantic equivalence with the Java version (ADR-0012: .NET Improvements Over Java Implementation), but API-surface divergence is permitted where .NET idioms differ.
- The existing `Old<T>(Func<T>)` is already verified against class, record, struct, record struct, `List<T>`, and `Dictionary<TKey, TValue>` in the test suite.

---

## Decision

**No additional overload of `Old<T>()` accepting a type hint will be provided. The sole public signature is `Old<T>(Func<T> supplier)`.**

### Details

The `TypeReference<T>` overload in Java exists solely to work around type erasure. .NET's reified generics make this workaround unnecessary: the single existing overload already receives a fully-closed `T` at runtime and deserializes generic collections correctly without any additional hint. Introducing a parity overload would add public API surface with no new semantic capability.

---

## Consequences

### Positive Consequences

- ✅ Smaller public API surface — fewer methods to learn, document, test, and maintain.
- ✅ .NET-idiomatic: the platform's reified generic system is exploited rather than masked by a Java-shaped API.
- ✅ Avoids inventing a .NET `TypeReference<T>` analogue that would carry no semantic value of its own.
- ✅ Reinforces ADR-0012's stance that semantic equivalence, not API-signature parity, is the porting goal.

### Negative Consequences

- ❌ Java users migrating to the .NET port who search for the two-argument overload will not find it and may briefly assume the port is incomplete.
- ❌ Mechanical one-to-one Java-to-C# code translation loses a direct signature mapping for `old()`.
- ❌ If a future scenario (e.g., specific AOT, trimming, or polymorphic serialization case) proves to require an explicit type hint, a new overload would have to be added later. This would be a non-breaking addition but is nonetheless future work that could have been front-loaded.

### Neutral Consequences

- ⚖️ The README "Differences from Java Version" section must explicitly list this divergence; otherwise readers may mistake it for an oversight.
- ⚖️ Documentation must briefly explain the underlying reason (type erasure vs. reified generics) for Java readers unfamiliar with .NET's generic system.

---

## Alternatives Considered

### Alternative 1: Add a parity overload with an invented .NET `TypeReference<T>` class

**Description**: Create a `TypeReference<T>` analogue in this library so callers can write `Contract.Old(() => list, new TypeReference<List<string>>() {})`, matching the Java signature exactly.

**Pros**:
- Mechanical API mapping from Java is preserved; Java users find the overload they expect.
- One-to-one code translation from Java is trivial.

**Cons**:
- A new type is invented for the sole purpose of mimicking Java; it carries no runtime value because `typeof(T)` already provides identical information.
- Adds public API surface, documentation burden, and a new concept the user must learn.
- Violates the .NET idiom of exploiting the platform's runtime type system.

**Why rejected**: Creating a type solely for Java-parity mimicry violates YAGNI and ADR-0012's preference for .NET idioms over byte-for-byte parity.

---

### Alternative 2: Add an overload accepting an explicit `Type` parameter

**Description**: Provide `Old<T>(Func<T> supplier, Type typeHint)` so callers can pass `typeof(List<string>)` when deep-copying complex generic state.

**Pros**:
- Uses an existing BCL type (`System.Type`); no new concept invented.
- Signature superficially resembles the Java two-argument form.

**Cons**:
- `JsonSerializer.Deserialize<T>(json, options)` already receives `T` as a reified closed type at runtime. Any `Type` argument the caller could pass is exactly `typeof(T)` — 100% redundant with information the runtime already has.
- Adds public API surface for zero new capability.
- Creates an implicit contract-validation burden (what happens if the caller passes `typeof(int)` for `Old<string>()`?).

**Why rejected**: Strictly redundant API with no capability beyond what `typeof(T)` already provides internally.

---

### Alternative 3: Add an overload accepting `JsonTypeInfo<T>` for AOT-safe serialization

**Description**: Provide `Old<T>(Func<T> supplier, JsonTypeInfo<T> typeInfo)` so source-generated serialization metadata can be supplied explicitly for AOT / trimming scenarios.

**Pros**:
- Genuinely useful in Native AOT scenarios where reflection-based serialization is unsafe or forbidden.
- Leverages `System.Text.Json` source-generator tooling.
- Future-proofs the API for the .NET AOT trajectory.

**Cons**:
- Solves a *different* problem (AOT / trimming safety) from what Java's `TypeReference<T>` solves (type erasure).
- Bundling the two concerns in a single ADR muddies the decision rationale.
- Current AOT support is already handled by `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]` annotations on `Old<T>()` itself, which correctly surface as diagnostics at the caller site.

**Why rejected**: AOT support is a distinct concern. If future evidence shows AOT callers need an explicit `JsonTypeInfo<T>` path, it should be proposed in its own ADR so the rationale, testing requirements, and API shape can be evaluated on AOT-specific grounds rather than mixed with Java-parity reasoning.

---

## Related Decisions

- **Related to**: ADR-0006 (Serialization and Deep Copy Mechanism for Old<T>()) — this ADR formally confirms the single-method design boundary that ADR-0006 implicitly adopted without addressing the Java overload's existence.
- **Related to**: ADR-0012 (.NET Improvements Over Java Implementation) — this ADR is a concrete instance of ADR-0012's principle that semantic equivalence trumps byte-for-byte API parity.
- **Related to**: ADR-0005 (Generics, Type Constraints, Nullable Reference Types) — the decision that `Old<T>()` carries no class constraint is what allows a single method to cover both reference and value types, eliminating any signature-level pressure for additional overloads.

---

## Implementation Notes

- No code change is required as part of this ADR; it exclusively records the decision not to add API.
- The README "Differences from Java Version" section should list an entry of roughly this form:

  > `Old<T>()` has a single overload taking `Func<T>`. The Java version's `TypeReference<T>` overload addresses type erasure, which does not apply to .NET's reified generics.

- If a future AOT / trimming / polymorphic serialization scenario genuinely requires an explicit type hint, a new ADR must be written rather than silently amending this one. This ADR is immutable once accepted, per the ADR workflow established in [./README.md](./README.md).

### Amendment (2026-10-04): Native AOT statement qualified by ADR-0021

This amendment adds no overload and changes no decision. It records a correction that
[ADR-0021](0021-postconditions-under-trimming-and-aot.md) makes to one sentence, in the open
rather than silently.

- **Stands**: the decision. No overload of `Old<T>()` accepting a type hint is provided; the sole
  public signature is `Old<T>(Func<T> supplier)`.
- **Stands**: the statement under Alternative 3 that the `[RequiresUnreferencedCode]` and
  `[RequiresDynamicCode]` annotations on `Old<T>()` surface as diagnostics at the caller site. A
  Native AOT publish of a consumer application reported `IL2026` and `IL3050` for its `Old` calls.
- **Superseded**: only the implication, in "Current AOT support is already handled by" those
  annotations, that diagnostics alone make `Old<T>()` usable under Native AOT. Measured: with
  application defaults `Old<T>()` throws in a trimmed or Native AOT application, because
  reflection-based JSON serialization is disabled there. It works under Native AOT once the
  application enables that serialization and preserves the copied types. ADR-0021 holds the
  measurements and the messages `Old<T>()` now gives.
- **Stands**: a `JsonTypeInfo<T>` or other source-generated path still needs its own ADR, as
  Alternative 3 and the note above require. ADR-0021 does not add one.

### Amendment (2026-10-07): `Old<T>()` no longer uses JSON

The decision stands: the sole public signature is `Old<T>(Func<T> supplier)`.
[ADR-0022](0022-faithful-old-copies-and-robust-comparison.md) replaces the JSON copy with a
reflection copy.

- The rationale that `JsonSerializer.Deserialize<T>` receives a reified `T` no longer describes the
  mechanism. The copy keeps each object's runtime type, so a type hint is even less useful than
  before.
- Alternative 3 (a `JsonTypeInfo<T>` overload) no longer applies: there is no serializer to give it
  to. Completing `Old<T>()` under Native AOT is now tracked as making hidden fields visible, in
  [issue #39](https://github.com/cwouyang/uContract.NET/issues/39).
- `[RequiresDynamicCode]` is removed from `Old<T>()`. `[RequiresUnreferencedCode]` stays.

---

## References

- [Java uContract 2.0.1 — `Contract.java:495` (pinned commit `cb1e03f`)](https://gitlab.com/TeddyChen/ucontract/-/blob/cb1e03f/src/main/java/tw/teddysoft/ucontract/Contract.java#L495)
- [Jackson `TypeReference` — jackson-databind wiki](https://github.com/FasterXML/jackson-databind/wiki)
- [.NET Generics (reified vs. erased) — Microsoft Learn](https://learn.microsoft.com/dotnet/standard/generics/)
- [`System.Text.Json.JsonSerializer.Deserialize<TValue>` — Microsoft Learn](https://learn.microsoft.com/dotnet/api/system.text.json.jsonserializer.deserialize)
- [ADR-0006: Serialization and Deep Copy Mechanism for Old<T>()](0006-serialization-deep-copy.md)
- [ADR-0012: .NET Improvements Over Java Implementation](0012-dotnet-improvements-over-java.md)

---

## Revision History

| Date       | Status      | Notes                          |
|------------|-------------|--------------------------------|
| 2026-04-19 | Accepted    | Decision recorded during pre-publish review; clarifies that .NET reified generics make the Java `TypeReference<T>` overload unnecessary and that the single `Old<T>(Func<T>)` signature is complete. |
| 2026-10-04 | Amended     | Implication that diagnostics alone make `Old<T>()` usable under Native AOT superseded by ADR-0021. Decision and diagnostics statement unchanged. See Implementation Notes > Amendment. |
| 2026-10-07 | Amended     | `Old<T>()` copies by reflection (ADR-0022); the JSON rationale and Alternative 3 no longer apply. Decision unchanged. See Implementation Notes > second Amendment. |
