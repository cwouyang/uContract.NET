# ADR-0024: Per-Type Caches That Do Not Keep Collectible Types Alive

## Status

**Accepted**

- **Date**: 2026-10-31
- **Deciders**: Project maintainer (cwouyang)
- **Status Date**: 2026-10-31

---

## Context

### Problem Statement

[Issue #48](https://github.com/cwouyang/uContract.NET/issues/48). uContract kept three static caches with one value per type, each a `ConcurrentDictionary` keyed strongly by `Type`:

- `MemberComparison.MetadataCache` (`TypeMetadata`, the members that `EnsureAssignable<T>()` compares), introduced with the reflection comparison of [ADR-0007](0007-reflection-field-comparison.md);
- `SharedTypes.Cache` (`bool`, whether a type is shared: `Old<T>()` does not copy it, and `EnsureAssignable<T>()` compares it by reference) and `DeepCopier.HoldsReferencesCache` (`bool`, whether a value type holds a reference at any depth), both introduced with the copier of [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md) (#45).

A static field is a root, so each key `Type` stayed reachable, and with it its assembly. A type from a collectible assembly (a collectible `AssemblyLoadContext`, or an assembly emitted with `AssemblyBuilderAccess.RunAndCollect`) that reached `Old<T>()` or `EnsureAssignable<T>()` could never be collected. Measured before the fix: with types emitted with `RunAndCollect`, the `WeakReference` to each type was still alive after 10 rounds of `GC.Collect` and `WaitForPendingFinalizers`.

### Relevant Context

- `Type.IsCollectible` (`MemberInfo.IsCollectible`) is true also for a constructed generic type or an array type that is collectible only through a generic argument or an element type. Measured with a throwaway program on .NET 8 under the JIT, for a type `Account` emitted with `RunAndCollect`: `List<Account>` has `IsCollectible == true`, while its `Assembly` is `System.Private.CoreLib`, whose `IsCollectible` is false. For `Account[]`, `Assembly` is the assembly of `Account`.
- A `TypeMetadata` value refers back to its key type: its `MemberAccessor`s hold the `FieldInfo`, `PropertyInfo` and getter `MethodInfo` of that type.
- The issue rated the problem low priority: typical design-by-contract use is in application code that is not collectible.

### Constraints

- No public API change; the `PublicAPI.*` baseline files are unchanged.
- No new dependency ([ADR-0011](0011-zero-dependency-principle.md)).
- Results do not change: a collectible type is copied and compared as a type that is not collectible is.
- The path for a type that is not collectible, which is nearly every call, must not allocate.

---

## Decision

**Each of the three caches is a `TypeCache<TValue>` with two tiers: a type whose `Type.IsCollectible` is false is cached in a `ConcurrentDictionary<Type, TValue>`, as before, and a collectible type in a `ConditionalWeakTable<Type, StrongBox<TValue>>`, which does not keep its key alive.**

### Details

1. **One internal class.** `TypeCache<TValue>` (`src/uContract/TypeCache.cs`) holds both tiers. `GetOrAdd(type, create)` reads `type.IsCollectible` and picks the tier. The three call sites are unchanged.
2. **`MemberInfo.IsCollectible`, not `Assembly.IsCollectible`.** The issue named `type.Assembly.IsCollectible`. That misses a constructed generic type over a collectible argument: the `Assembly` of `List<Account>` is `System.Private.CoreLib` (measured, see Relevant Context). Such types become keys: `DeepCopier.CopyOf` passes the runtime type of the object it copies, for example a `List<Account>`, to `SharedTypes.IsShared` (read from the code).
3. **`StrongBox<TValue>`.** `ConditionalWeakTable<TKey, TValue>` requires a reference type as `TValue` (`where TValue : class?`), and two of the caches hold `bool`. The collectible tier wraps every value in a `StrongBox<TValue>`, so `TypeCache<TValue>` needs no constraint on `TValue`.
4. **No closure on the common path.** The lambda that wraps the value in a `StrongBox` captures `create`. It lives in a separate method, `GetOrAddCollectible`, so the closure is allocated only for a collectible type. When the lambda sat in `GetOrAdd`, every call allocated 24 bytes, also for a type that is not collectible (measured by `TypeCacheTests` before the fix).
5. **Races.** In both tiers `create` can run more than once for one type when threads race, and one value wins, as with `ConcurrentDictionary.GetOrAdd`. The three factories compute their value from the type alone, so a losing value has the same content as the winning one (read from the code, not measured).

---

## Consequences

### Positive Consequences

- ✅ A collectible type that went through `Old<T>()` or `EnsureAssignable<T>()` can be collected once nothing else refers to it, also when a cached `TypeMetadata` refers back to it. `CollectibleTypeCacheTests` measures this for each cache, for a constructed generic type (`List<Account>`) and for an array type (`Tag[]`).
- ✅ Results are unchanged: three tests copy or compare the same scenario with types emitted with `RunAndCollect` and with `Run`, twice each so that the second run reads the cache, and assert equal results.
- ✅ The path for a type that is not collectible uses the same `ConcurrentDictionary` as before and allocates nothing on a cache hit (measured: `TypeCacheTests.GetOrAdd_WhenNonCollectibleTypeIsCached_AllocatesNothing`, 0 bytes).

### Negative Consequences

- ❌ Every lookup now reads `Type.IsCollectible`. Under the JIT on .NET 8 this property is one native call (`RuntimeTypeHandle.IsCollectible`, a QCall; read from the .NET 8 source of `RuntimeType.CoreCLR.cs`). Its cost per lookup was not measured.
- ❌ A lookup for a collectible type costs more than one in the dictionary: it allocates the closure on every call and goes through the `ConditionalWeakTable` (read from the code; the time was not measured).
- ❌ The collection tests depend on the garbage collector. They try up to 10 rounds of `GC.Collect` and `WaitForPendingFinalizers`. They passed in every run during the work, but a test of this kind can be sensitive to timing on another runner.

### Neutral Consequences

- ⚖️ **Native AOT.** In .NET 8, Native AOT's `RuntimeTypeInfo.IsCollectible` is sealed to return false, and its assemblies cannot be unloaded (read from the .NET 8 source, not measured). Every type therefore takes the `ConcurrentDictionary` tier, as before this change.
- ⚖️ No public API, message or exception changed.

---

## Known Limitations

- The tests emit types with `AssemblyBuilderAccess.RunAndCollect`. Unloading a collectible `AssemblyLoadContext` after its types went through uContract was not measured.
- Only the three caches of this ADR were changed. A search of `src/uContract` for other static fields during the work found none that holds a user type (read from the code).

---

## Alternatives Considered

### Alternative 1: A `ConditionalWeakTable` for every cache

**Description**: Replace each `ConcurrentDictionary` with a `ConditionalWeakTable<Type, …>` for all types (the first option of the issue).

**Why rejected**: When the options were presented to the maintainer on 2026-10-31, this one was described as complete, but with every lookup slower and the cost to be measured again. The maintainer chose the two tiers instead (see "Choice of the two tiers" below).

---

### Alternative 2: Do not cache a collectible type

**Description**: Skip the cache when the assembly is collectible (the second option of the issue, `type.Assembly.IsCollectible`).

**Why rejected**: This was the option the agent recommended to the maintainer: the path for a type that is not collectible stays unchanged, and it is the simplest. The maintainer did not choose it, and chose the two tiers so that collectible types are still cached (see "Choice of the two tiers" below). Found later, during the work: the check that the issue named, `type.Assembly.IsCollectible`, misses `List<Account>` (Decision, item 2).

---

### Choice of the two tiers

On 2026-10-31 the maintainer was offered three options: skip caching a collectible type (the agent's recommendation), a `ConditionalWeakTable` for every cache, or two tiers. The two-tier option was presented as "both at once: ordinary types keep the `ConcurrentDictionary`, collectible types still get a cache, at the cost of more code", and the maintainer chose it. The recorded reason: keep the existing cost of the path for types that are not collectible, and still cache collectible types, accepting a second code path.

---

## Related Decisions

- **Related to**: [ADR-0007 - Reflection and Field Comparison for EnsureAssignable<T>()](0007-reflection-field-comparison.md) — introduced the metadata cache.
- **Related to**: [ADR-0022 - Faithful Old<T>() Copies and Robust EnsureAssignable<T>() Comparison](0022-faithful-old-copies-and-robust-comparison.md) — introduced the shared-type and holds-references caches.
- **Related to**: [ADR-0019 - Public API Baseline Tracking](0019-public-api-baseline-tracking.md) — the baseline is unchanged.

---

## Implementation Notes

This ADR was written after the implementation so that it could record what the work turned up.

- **Order of the work.** Each cache got its collectible tier in its own step, driven by a test that failed because the type stayed alive. The two tiers were then extracted into `TypeCache<TValue>` as a structural change.
- **The closure finding.** The first `TypeCache.GetOrAdd` allocated a closure on every call, found after the extraction. `TypeCacheTests` reproduced it (24 bytes); moving the lambda into `GetOrAddCollectible` brought it to 0.
- **Non-vacuous result tests.** The three tests that compare collectible and non-collectible results would also pass without the change. They were checked against temporary mutants of the collectible tiers, which made them fail; the mutants were not kept.
- **Unit tests.** 1,239 unit tests passed at the end of the implementation. That figure is a snapshot.

---

## References

- [Issue #48: Per-type caches keep collectible types alive](https://github.com/cwouyang/uContract.NET/issues/48)
- [`TypeCache.cs`](../../src/uContract/TypeCache.cs) — the two tiers
- [`MemberComparison.cs`](../../src/uContract/MemberComparison.cs), [`SharedTypes.cs`](../../src/uContract/SharedTypes.cs), [`DeepCopier.cs`](../../src/uContract/DeepCopier.cs) — the three caches
- [`CollectibleTypeCacheTests.cs`](../../tests/uContract.Tests/CollectibleTypeCacheTests.cs), [`TypeCacheTests.cs`](../../tests/uContract.Tests/TypeCacheTests.cs) — the unit tests
- [`CHANGELOG.md`](../../CHANGELOG.md) — the `[Unreleased]` entry
- [ADR-0007](0007-reflection-field-comparison.md), [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md)

---

## Revision History

| Date       | Status      | Notes                          |
|------------|-------------|--------------------------------|
| 2026-10-31 | Accepted    | Decision recorded after implementation (#48). |
