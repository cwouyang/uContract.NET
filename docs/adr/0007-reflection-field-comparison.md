# ADR-0007: Reflection and Field Comparison for EnsureAssignable<T>()

## Status

**Accepted**

- **Date**: 2025-10-18
- **Deciders**: Project maintainers
- **Status Date**: 2025-10-18
- **Amended**: 2026-10-04 — behaviour under trimming and Native AOT, and for a property with no get method, is added by [ADR-0021](0021-postconditions-under-trimming-and-aot.md); see the Amendment under Implementation Notes
- **Amended**: 2026-10-07 — the comparison rules are replaced by rules R0–R8 of [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md), and the walk is iterative; see the second Amendment under Implementation Notes
- **Amended**: 2026-10-09 — at the top level a `null` is compared as a value, and a `string`, a delegate type and a nullable value type are compared as a whole, without the patterns (#52, [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md)); see the third Amendment under Implementation Notes
- **Amended**: 2026-10-10 — at the top level, one instance of a shared type whose state can change, passed as `actual` and as `expected`, is not compared member by member: the call throws `InvalidOperationException`, and the patterns do not apply (#54, rule S of [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md)); see the fourth Amendment under Implementation Notes
- **Amended**: 2026-10-10 — two `FrozenSet<T>` or `FrozenDictionary<TKey, TValue>` values of one runtime type that a member or element holds are compared element by element, not by reference, and one of the two types as `T` is compared as a whole, without the patterns (#49, [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md)); see the fifth Amendment under Implementation Notes

---

## Context

### Problem Statement

The `EnsureAssignable<T>()` method verifies that only specified fields have changed between two object states. We must decide:
- Which members to compare (public properties, private fields, or both)
- Whether to support deep (recursive) comparison
- How to handle performance (caching reflection metadata)
- How to match field names (literal vs regex patterns)

This decision affects:
- **Accuracy**: How thoroughly we validate state changes
- **Performance**: Reflection can be slow without caching
- **Flexibility**: What types of objects can be validated
- **Java parity**: Consistency with the Java version's behavior

### Relevant Context

- The Java version uses **AssertJ's `usingRecursiveComparison()`**
- AssertJ compares **all fields recursively** (including private fields)
- AssertJ supports **regex patterns** via `ignoringFieldsMatchingRegexes()`
- .NET has reflection via `System.Reflection` namespace
- Reflection is slower than direct property access but provides flexibility

### Constraints

- Must maintain consistency with Java version's behavior
- Must support regex patterns for field matching
- Should provide good performance for typical DDD aggregates
- Must work with both properties and fields

---

## Decision

**We will use reflection to compare all public properties and private fields, with recursive comparison support and reflection metadata caching.**

### Details

**Comparison Strategy**:
1. **Compare public properties** (primary members in C#)
2. **Compare private fields** (for completeness, matches Java)
3. **Support recursive comparison** for nested objects
4. **Cache reflection metadata** to improve performance
5. **Support regex patterns** for assignable field matching

**Implementation Approach**:
```csharp
// No generic constraint - supports both reference types and value types (struct, record struct)
public static void EnsureAssignable<T>(T actual, T expected, params string[] assignablePatterns)
{
    if (!_config.PostconditionsEnabled) return;
    if (_entered.Value) return;

    try
    {
        _entered.Value = true;

        var differences = FindDifferences(actual, expected, assignablePatterns);

        if (differences.Count > 0)
        {
            var message = $"Fields were modified that are not marked as assignable:\n" +
                         string.Join("\n", differences.Select(d => $"  - {d}"));
            throw new PostconditionViolationException(message);
        }
    }
    finally
    {
        _entered.Value = false;
    }
}

private static List<string> FindDifferences<T>(T actual, T expected, string[] assignablePatterns)
{
    var type = typeof(T);
    var metadata = GetOrCacheMetadata(type);  // Cached reflection info
    var differences = new List<string>();

    foreach (var member in metadata.Members)
    {
        if (IsAssignable(member.Name, assignablePatterns))
            continue;

        var actualValue = member.GetValue(actual);
        var expectedValue = member.GetValue(expected);

        if (!AreEqual(actualValue, expectedValue, member.MemberType))
        {
            differences.Add(member.Name);
        }
    }

    return differences;
}

private static bool IsAssignable(string fieldName, string[] patterns)
{
    foreach (var pattern in patterns)
    {
        if (Regex.IsMatch(fieldName, pattern))
            return true;
    }
    return false;
}
```

**Metadata Caching**:
```csharp
private static readonly ConcurrentDictionary<Type, TypeMetadata> _metadataCache = new();

private static TypeMetadata GetOrCacheMetadata(Type type)
{
    return _metadataCache.GetOrAdd(type, t =>
    {
        var properties = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var fields = t.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

        return new TypeMetadata
        {
            Members = properties.Cast<MemberInfo>()
                                .Concat(fields.Cast<MemberInfo>())
                                .Select(m => new MemberAccessor(m))
                                .ToList()
        };
    });
}
```

**Recursive Comparison**:
```csharp
private static bool AreEqual(object? actual, object? expected, Type memberType)
{
    if (ReferenceEquals(actual, expected)) return true;
    if (actual == null || expected == null) return false;

    // Value types and strings: direct comparison
    if (memberType.IsValueType || memberType == typeof(string))
        return Equals(actual, expected);

    // Collections: compare elements
    if (actual is IEnumerable actualEnum && expected is IEnumerable expectedEnum)
        return CompareCollections(actualEnum, expectedEnum);

    // Complex objects: recursive comparison
    if (memberType.IsClass)
        return CompareRecursively(actual, expected);

    return Equals(actual, expected);
}

private static bool CompareRecursively(object actual, object expected)
{
    var type = actual.GetType();
    var metadata = GetOrCacheMetadata(type);

    foreach (var member in metadata.Members)
    {
        var actualValue = member.GetValue(actual);
        var expectedValue = member.GetValue(expected);

        if (!AreEqual(actualValue, expectedValue, member.MemberType))
            return false;
    }

    return true;
}
```

---

## Consequences

### Positive Consequences

- ✅ **Java parity**: Matches AssertJ's recursive comparison behavior
- ✅ **Comprehensive validation**: Catches all state changes, not just public properties
- ✅ **Regex support**: Flexible field matching (e.g., `".*Timestamp"`, `"email"`)
- ✅ **Performance optimization**: Reflection metadata caching reduces overhead
- ✅ **Deep comparison**: Validates nested object changes
- ✅ **Encapsulation-friendly**: Can validate private state (useful for DDD aggregates)
- ✅ **Supports both reference and value types**: Works with class, record, struct, record struct

### Negative Consequences

- ❌ **Reflection overhead**: Slower than direct property access (but cached)
- ❌ **Complexity**: Recursive comparison logic is non-trivial
- ❌ **May expose private details**: Comparing private fields might violate encapsulation in some views
- ❌ **Collection comparison edge cases**: Handling different collection types requires care

### Neutral Consequences

- ⚖️ **More thorough than typical .NET comparison**: Most .NET libraries only compare public properties
- ⚖️ **Matches DDD aggregate validation needs**: Aggregates often have private fields that should be validated

---

## Alternatives Considered

### Alternative 1: Compare Only Public Properties

**Description**: Reflect only on public properties, ignore fields

**Pros**:
- Simpler implementation
- Respects encapsulation (public-only API)
- Faster (fewer members to compare)

**Cons**:
- **Breaks Java parity**: AssertJ compares all fields
- **Incomplete validation**: Private fields might change silently
- **Not suitable for DDD aggregates**: Aggregates often use backing fields

**Why rejected**: Would not match Java version behavior. DDD aggregates often have important private state that must be validated in postconditions.

---

### Alternative 2: No Recursive Comparison

**Description**: Compare only top-level members, don't recurse into nested objects

**Pros**:
- Simpler and faster
- Avoids infinite recursion issues
- Easier to reason about

**Cons**:
- **Breaks Java parity**: AssertJ uses recursive comparison
- **Incomplete validation**: Nested object changes would not be detected
- **Limited usefulness**: Many aggregates have nested value objects

**Why rejected**: Recursive comparison is essential for validating complex aggregate states with nested value objects.

---

### Alternative 3: No Reflection Caching

**Description**: Perform reflection on every `EnsureAssignable()` call

**Pros**:
- Simpler implementation (no caching logic)
- No memory overhead for cache

**Cons**:
- **Poor performance**: Reflection is expensive; repeated calls would be slow
- **Not production-ready**: Unacceptable for frequently-called postconditions

**Why rejected**: Caching is essential for acceptable performance. The memory overhead is minimal compared to the performance gain.

---

### Alternative 4: Use Expression Trees for Type-Safe Access

**Description**: Use compiled expression trees for faster member access

```csharp
var accessor = BuildAccessor<T>();  // Compiled expression tree
var value = accessor(obj);  // Faster than reflection
```

**Pros**:
- Faster than reflection (compiled IL)
- Still dynamic (no hard-coded property access)

**Cons**:
- **More complex**: Expression tree building is non-trivial
- **Marginal benefit**: Caching reflection already provides good performance
- **Overkill**: Postconditions are not performance-critical paths

**Why rejected**: Added complexity for marginal benefit. Reflection with caching is sufficient for DBC postconditions.

---

## Related Decisions

- **Related to**: ADR-0006 (Deep copy via serialization pairs with deep comparison)
- **Related to**: ADR-0011 (Zero-dependency principle — use built-in reflection)
- **Related to**: ADR-0005 (`EnsureAssignable<T>()` has **no** generic constraint — supports both reference and value types)
- **Related to**: ADR-0009 (Thread safety via AsyncLocal for recursion guard)
- **Affects**: What types of aggregates can use `EnsureAssignable<T>()`

---

## Implementation Notes

### Type Metadata Structure

```csharp
internal class TypeMetadata
{
    public List<MemberAccessor> Members { get; set; } = new();
}

internal class MemberAccessor
{
    public string Name { get; }
    public Type MemberType { get; }
    private readonly PropertyInfo? _property;
    private readonly FieldInfo? _field;

    public MemberAccessor(MemberInfo member)
    {
        Name = member.Name;
        if (member is PropertyInfo prop)
        {
            _property = prop;
            MemberType = prop.PropertyType;
        }
        else if (member is FieldInfo field)
        {
            _field = field;
            MemberType = field.FieldType;
        }
        else
        {
            throw new ArgumentException("Must be PropertyInfo or FieldInfo");
        }
    }

    public object? GetValue(object obj)
    {
        return _property?.GetValue(obj) ?? _field?.GetValue(obj);
    }
}
```

### Regex Pattern Matching

```csharp
// Examples of valid patterns
EnsureAssignable(state, oldState, "Email");           // Literal match
EnsureAssignable(state, oldState, ".*Timestamp");     // Regex: any field ending with "Timestamp"
EnsureAssignable(state, oldState, "^_.*");            // Regex: any field starting with "_"
EnsureAssignable(state, oldState, "email|phone");     // Regex: "email" OR "phone"
```

### Performance Considerations

- **First call**: ~100-500μs (reflection + caching)
- **Subsequent calls**: ~10-50μs (cached metadata)
- **Acceptable for postconditions**: DBC checks are not in hot paths

### Testing Requirements

- Test with reference types (class, record class)
- Test with value types (struct, record struct)
- Test with objects containing public properties only
- Test with objects containing private fields
- Test with nested objects (recursive comparison)
- Test with collections
- Test regex pattern matching
- Test performance with caching

**Example test for record struct**:
```csharp
[Fact]
public void EnsureAssignable_ShouldWorkWithRecordStruct()
{
    record struct Point(int X, int Y);

    var p1 = new Point(0, 0);
    var p2 = new Point(5, 0);  // Only X changed

    // Should pass: only X changed
    Contract.EnsureAssignable(p2, p1, nameof(Point.X));

    var p3 = new Point(5, 10);  // Both X and Y changed

    // Should fail: Y also changed but not marked as assignable
    Assert.Throws<PostconditionViolationException>(
        () => Contract.EnsureAssignable(p3, p1, nameof(Point.X))
    );
}
```

### Amendment (2026-10-04): `EnsureAssignable<T>()` under trimming and Native AOT

The decision (reflection to compare all public properties and private fields, with recursive
comparison and reflection metadata caching, and regex patterns for assignable members) is
unchanged. [ADR-0021](0021-postconditions-under-trimming-and-aot.md) adds three things and
qualifies two statements above.

- **Annotation.** The type parameter `T` of `EnsureAssignable<T>` carries
  `[DynamicallyAccessedMembers(PublicProperties | PublicFields | NonPublicFields)]`, so trimming
  and Native AOT keep the members of the top-level type that are compared. The types of nested
  objects and collection elements are known only at run time and are not preserved by it.
- **No members visible.** Under Native AOT, when the comparison reaches a type other than
  `System.Object` whose member list is empty, it throws `InvalidOperationException` instead of
  treating the two values as equal. With the `CompareRecursively` listing above, such a type
  compared as equal, so a changed object passed.
- **Unreadable property.** The `GetValue` listing above reads every property. In every build, a
  property with no get method (a write-only property, or one whose getter was removed by trimming)
  now throws `InvalidOperationException` before it is read; it used to surface as
  `ArgumentException`.
- **"Comprehensive validation: Catches all state changes"** and **"Deep comparison: Validates
  nested object changes"** (Positive Consequences) hold where the members are visible to
  reflection. Under Native AOT a nested or element type must be preserved by the consumer, or its
  top-level member listed as assignable; in a trimmed application without Native AOT a nested type
  whose members were removed entirely compares as equal. ADR-0021 lists these and the other
  limits, including comparison gaps that exist in every build.
- Assignable patterns are matched against top-level member names only, as the `FindDifferences`
  listing above shows; ADR-0021 relies on this for the advice its messages give.

### Amendment (2026-10-07): Comparison rules R0–R8 and an iterative walk

The decision (reflection over public properties and instance fields, metadata caching, regex
patterns for top-level members) is unchanged. How two values are compared is replaced by rules
R0–R8 of [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md).

- **The walk is iterative.** The `CompareRecursively` listing above recurses once per level. The
  comparison now uses an explicit stack, so cycles and deep graphs no longer overflow the stack.
- **Runtime types, not declared types.** The `AreEqual` listing above dispatches on the declared
  member type. Every rule now decides on the values' runtime types (R0). Values of different runtime
  types are unequal (R6).
- **Value types** are compared by `Equals`, then by their fields when `Equals` says "unequal" (R3).
  **Dictionaries** are compared by key and value (R7). **Cycles and shared sub-graphs** are tracked
  by reference (R1). **Delegates** are compared by method (R4). **Shared types** are compared by
  reference (R5). A class with no members visible under Native AOT is equal when its `Equals` says
  so (R8).
- The listings above stay as the historical record. ADR-0022 holds the rules and their limits.

### Amendment (2026-10-09): The top level for null and for three kinds of T (#52)

The decision is unchanged. This ADR is the record of the shape of the top level: the members of `T`
are compared one by one, and the assignable patterns are matched against their names.
[Issue #52](https://github.com/cwouyang/uContract.NET/issues/52) changes that shape in two cases.
A `null` `actual` or `expected` is compared as a value: two `null`s are equal, and exactly one
`null` is a postcondition violation. When `T` is a `string`, a delegate type or a nullable value
type, two values that are not `null` are compared as a whole, by rules R0–R8. In both cases no
member of `T` is compared, and the patterns do not apply and are not examined.

The amendment of [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md) for #52 records the
comparison, its reasons, the measurements and the limits. The amendment of
[ADR-0012](0012-dotnet-improvements-over-java.md) for #52 records the decisions on `null`. They
qualify these statements of this ADR, each quoted by its opening words:

- The Decision: "compare all public properties and private fields".
- The amendment of 2026-10-04: "regex patterns for assignable members) is unchanged".
- The same amendment: "Assignable patterns are matched against top-level member names only".
- The amendment of 2026-10-07: "regex patterns for top-level members) is unchanged".
- The same amendment: "Every rule now decides on the values' runtime types (R0)". At the top level
  a test on the declared `T` comes first.
- The Revision History row of 2026-10-07: "Members compared and patterns unchanged".

Each still holds for every other `T` with two values that are not `null`. This amendment adds no
decision. It is the record that this ADR was considered for #52.

### Amendment (2026-10-10): The top level for one shared instance on both sides (#54)

The decision is unchanged. The third Amendment above records two cases in which the top level does
not compare the members of `T` one by one.
[Issue #54](https://github.com/cwouyang/uContract.NET/issues/54) adds a third case. A shared type is
a type that `Old<T>()` shares with the original instead of copying it (B3 of ADR-0022). In a call
that compares, when one instance of a shared type whose state can change is passed as `actual` and
as `expected`, the method throws `InvalidOperationException`. No member of `T` is compared, and the
patterns do not apply and are not examined.

The rule is rule S of ADR-0022. The amendment of
[ADR-0022](0022-faithful-old-copies-and-robust-comparison.md) for #54 records it, with its reasons,
the list of the types, the message, the measurements and the limits. It qualifies these statements
of the third Amendment above, each quoted by its opening words:

- "changes that shape in two cases." There are now three cases.
- "In both cases no member of `T` is compared, and the patterns do not apply and are not examined."
  The same holds in the third case.
- "Each still holds for every other `T` with two values that are not `null`." There is one more
  exception, and it is for every `T`: one instance of a shared type whose state can change, passed
  as `actual` and as `expected`.

This amendment adds no decision. It is the record that this ADR was considered for #54.

### Amendment (2026-10-10): FrozenSet and FrozenDictionary values are compared by element (#49)

The decision is unchanged. [Issue #49](https://github.com/cwouyang/uContract.NET/issues/49) changes
how two rules treat a `FrozenSet<T>` and a `FrozenDictionary<TKey, TValue>` ("the two types" below).
`Old<T>()` shares a value of the two types with the original instead of copying it (B3 of ADR-0022),
and it still does. Two values of the two types that a member or element holds are now compared
element by element, in enumeration order (R7), where R5 compared them by reference when their
runtime types were equal. When the declared `T` is one of the two types, two values that are not
`null` are compared as a whole, by the same rule. No member of `T` is compared, and the patterns do
not apply and are not examined.

The amendment of [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md) for #49 records the
change, with its reasons, the measurements and the limits. It qualifies these statements of this
ADR, each quoted by its opening words:

- The amendment of 2026-10-07: "**Shared types** are compared by" reference (R5). Two values of the
  two types that a member or element holds are the exception: they are compared by element (R7). Two
  instances of one runtime type that is another type declared in the namespace
  `System.Collections.Frozen` (two arrays of `FrozenSet<T>`, two instances of a class of the caller
  declared there) are still compared by reference.
- The third Amendment above: "When `T` is a `string`, a delegate type or a nullable value" type, two
  values that are not `null` are compared as a whole. The same now holds when `T` is one of the two
  types.
- The Status line of 2026-10-09: "a `string`, a delegate type and a nullable value type are compared
  as a whole, without the patterns". So is one of the two types as `T`. The Revision History row of
  2026-10-09 ("three kinds of `T` as a whole") and the title of the third Amendment count three
  kinds. The two types are added to them.
- The third Amendment: "Each still holds for every other `T` with two values that are not `null`."
  "Every other `T`" no longer includes the two types. The statements that the third Amendment lists
  are qualified for them in the same way.

Still true:

- The fourth Amendment above: "There are now three cases." There are still three. The second case, a
  `T` that is compared as a whole, now also covers the two types.

This amendment adds no decision. It is the record that this ADR was considered for #49.

---

## References

- [AssertJ Recursive Comparison](https://assertj.github.io/doc/#assertj-core-recursive-comparison) (Java uContract uses this)
- [System.Reflection Namespace](https://learn.microsoft.com/en-us/dotnet/api/system.reflection)
- [BindingFlags Enumeration](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.bindingflags)
- [ConcurrentDictionary](https://learn.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2)

---

## Revision History

| Date       | Status      | Notes                          |
|------------|-------------|--------------------------------|
| 2025-10-18 | Accepted    | Decision finalized             |
| 2026-10-04 | Amended     | Annotation on `T`, the "no members visible" rule and the unreadable-property rule added by ADR-0021. Comparison strategy unchanged. See Implementation Notes > Amendment. |
| 2026-10-07 | Amended     | Comparison rules replaced by R0–R8 of ADR-0022; the walk is iterative (explicit stack). Members compared and patterns unchanged. See Implementation Notes > second Amendment. |
| 2026-10-09 | Amended     | The top level compares `null` as a value and three kinds of `T` as a whole (#52, ADR-0022). Decision unchanged. See Implementation Notes > third Amendment. |
| 2026-10-10 | Amended     | At the top level, one instance of a shared type whose state can change, passed as `actual` and as `expected`, is not compared: `InvalidOperationException` (#54, ADR-0022). Decision unchanged. See Implementation Notes > fourth Amendment. |
| 2026-10-10 | Amended     | Two `FrozenSet<T>` or `FrozenDictionary<TKey, TValue>` values of one runtime type that a member or element holds are compared by element, not by reference; one of the two types as `T` is compared as a whole (#49, ADR-0022). Decision unchanged. See Implementation Notes > fifth Amendment. |
