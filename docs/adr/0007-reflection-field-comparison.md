# ADR-0007: Reflection and Field Comparison for EnsureAssignable<T>()

## Status

**Accepted**

- **Date**: 2025-10-18
- **Deciders**: Project maintainers
- **Status Date**: 2025-10-18

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
