# ADR-0006: Serialization and Deep Copy Mechanism for Old<T>()

## Status

**Accepted**

- **Date**: 2025-10-18
- **Deciders**: Project maintainers
- **Status Date**: 2025-10-18

---

## Context

### Problem Statement

The `Old<T>()` method needs to create a deep copy of an object's state for use in postconditions. We must decide which serialization technology to use:
- System.Text.Json (built-in .NET)
- Newtonsoft.Json (third-party library)
- Custom reflection-based deep copy
- Pluggable serialization interface

This decision affects:
- **Dependencies**: Whether we maintain zero-dependency principle
- **Performance**: Serialization speed and memory overhead
- **Compatibility**: What types of objects can be captured
- **Java parity**: Consistency with the Java version's approach

### Relevant Context

- The Java version uses Jackson (com.fasterxml.jackson) for JSON serialization
- .NET has built-in `System.Text.Json` since .NET Core 3.0 (included in .NET 8)
- `Old<T>()` is used in postconditions to capture state before method execution
- The captured state is compared against the new state after execution
- Deep copy is necessary to prevent reference aliasing issues

### Constraints

- Must adhere to zero-dependency principle (no external NuGet packages)
- Must support common DDD value objects and aggregate state objects
- Must handle circular references gracefully
- Should maintain consistency with Java version's approach

---

## Decision

**We will use `System.Text.Json` for deep copy via serialization/deserialization.**

### Details

**Implementation Approach**:
```csharp
// No generic constraint - supports both reference and value types
public static T Old<T>(Func<T> supplier)
{
    if (!_config.PostconditionsEnabled)
        return default(T)!;

    if (_entered.Value)
        return default(T)!;

    try
    {
        _entered.Value = true;

        var obj = supplier();

        var options = new JsonSerializerOptions
        {
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            WriteIndentation = false,
            IncludeFields = true  // Serialize fields, not just properties
        };

        var json = JsonSerializer.Serialize(obj, options);
        return JsonSerializer.Deserialize<T>(json, options)!;
    }
    finally
    {
        _entered.Value = false;
    }
}
```

**Supported Types**:
- **Reference types**: Classes, record classes, interfaces
- **Value types**: Structs, record structs, primitives (int, decimal, etc.)
- Plain Old CLR Objects (POCOs) with public properties/fields
- Value objects (both class and struct variants)
- Collections (List, Dictionary, etc.)
- Nested objects (recursively serialized)

**Limitations**:
- Objects must be JSON-serializable (no delegates, IntPtr, etc.)
- Circular references are ignored (broken), not preserved
- Object reference identity is not preserved
- Private fields require `[JsonInclude]` or `IncludeFields = true`

---

## Consequences

### Positive Consequences

- ✅ **Zero dependencies**: Uses built-in .NET library
- ✅ **Cross-platform**: System.Text.Json works on all .NET platforms
- ✅ **Good performance**: Faster than Newtonsoft.Json in most scenarios
- ✅ **Modern .NET alignment**: Uses Microsoft's recommended JSON library
- ✅ **Circular reference handling**: `ReferenceHandler.IgnoreCycles` prevents infinite loops
- ✅ **Consistent with Java approach**: Both use JSON serialization for deep copy
- ✅ **Simple implementation**: Minimal code required
- ✅ **Supports both reference and value types**: Works with class, record, struct, record struct

### Negative Consequences

- ❌ **Serialization requirement**: Objects must be JSON-compatible
- ❌ **No reference preservation**: Circular references are broken, not maintained
- ❌ **Type-specific limitations**: Some types (delegates, DbContext, etc.) cannot be serialized
- ❌ **Performance overhead**: Serialization/deserialization has cost (acceptable for postconditions)
- ❌ **May require annotations**: Private fields need `[JsonInclude]` without `IncludeFields`
- ❌ **Boxing overhead for value types**: Serializing structs may cause boxing, though mitigated by modern JSON serialization

### Neutral Consequences

- ⚖️ **Same limitations as Java version**: Jackson has similar constraints
- ⚖️ **Explicit opt-in for postconditions**: Users choose when to use `Old<T>()`

---

## Alternatives Considered

### Alternative 1: Newtonsoft.Json

**Description**: Use the mature Newtonsoft.Json library

**Pros**:
- More mature and feature-rich than System.Text.Json
- Better support for complex scenarios (polymorphism, type handling)
- More forgiving with edge cases

**Cons**:
- **Breaks zero-dependency principle**: Requires external NuGet package
- **Slower than System.Text.Json**: Performance benchmarks show System.Text.Json is faster
- **Not modern .NET standard**: Microsoft recommends System.Text.Json for new projects

**Why rejected**: Adding a dependency contradicts our core principle. System.Text.Json is sufficient for the vast majority of DDD value objects and aggregate states.

---

### Alternative 2: Custom Reflection-Based Deep Copy

**Description**: Implement deep copy using reflection to traverse object graph

```csharp
public static T DeepCopy<T>(T obj)
{
    // Recursively copy all fields/properties via reflection
    var copy = Activator.CreateInstance<T>();
    foreach (var field in typeof(T).GetFields(...))
    {
        var value = field.GetValue(obj);
        var copiedValue = DeepCopy(value);  // Recursive
        field.SetValue(copy, copiedValue);
    }
    return copy;
}
```

**Pros**:
- No serialization dependency
- Can handle any type (including non-serializable types)
- Full control over copy logic

**Cons**:
- **Complex implementation**: Must handle collections, dictionaries, circular references manually
- **Performance**: Reflection is slower than optimized serialization
- **Maintenance burden**: Edge cases (generic types, nullable types) are tricky
- **No built-in cycle detection**: Must implement manually

**Why rejected**: Overly complex for marginal benefit. System.Text.Json handles the common cases well, and the edge cases (non-serializable types) are rare in DDD contexts.

---

### Alternative 3: Pluggable Serialization Interface

**Description**: Allow users to provide their own serializer

```csharp
public interface IDeepCopySerializer
{
    T DeepCopy<T>(T obj);
}

public static void ConfigureSerializer(IDeepCopySerializer serializer)
{
    _serializer = serializer;
}
```

**Pros**:
- Maximum flexibility
- Users can choose their preferred serialization approach
- Can handle any edge case via custom implementation

**Cons**:
- **Over-engineering**: YAGNI — most users don't need this flexibility
- **Configuration complexity**: Requires setup code (breaks zero-setup principle)
- **Testing burden**: Must test multiple serializer implementations
- **API surface growth**: More public API to maintain

**Why rejected**: Adds complexity without clear benefit. If users have exotic serialization needs, they can avoid using `Old<T>()` and implement postcondition checks manually.

---

### Alternative 4: Binary Serialization

**Description**: Use binary serialization (deprecated in .NET)

**Pros**:
- Can preserve object references
- Works with non-JSON types

**Cons**:
- **Deprecated**: BinaryFormatter is obsolete and insecure in .NET 5+
- **Security risk**: Deserialization vulnerabilities
- **Not cross-platform**: Limited support
- **Not recommended by Microsoft**

**Why rejected**: Deprecated and insecure. Not an option for modern .NET.

---

## Related Decisions

- **Related to**: ADR-0011 (Zero-dependency principle)
- **Related to**: ADR-0005 (`Old<T>()` has **no** generic constraint — supports both reference and value types)
- **Related to**: ADR-0009 (Thread safety via AsyncLocal for recursion guard)
- **Affects**: What types of objects can be used in postconditions

---

## Implementation Notes

### Configuration

```csharp
private static readonly JsonSerializerOptions _jsonOptions = new()
{
    ReferenceHandler = ReferenceHandler.IgnoreCycles,
    WriteIndentation = false,
    IncludeFields = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never
};
```

### Usage Guidelines

**Recommended for**:
- **Value objects (class)**: `record Email(string Value)`
- **Value objects (struct)**: `record struct Money(decimal Amount, string Currency)`
- **Aggregate state DTOs**: Both class and struct variants
- **Simple POCOs**: Classes with properties/fields
- **Collections**: `List<T>`, `Dictionary<TKey, TValue>`, etc.
- **Primitives and structs**: `int`, `decimal`, `DateTime`, custom structs

**Not recommended for**:
- Objects with delegates/events
- DbContext or Entity Framework entities (lazy loading issues)
- Objects with complex circular references that must be preserved
- Objects with unmanaged resources (IntPtr, handles)

### Error Handling

```csharp
public static T Old<T>(Func<T> supplier)
{
    try
    {
        var obj = supplier();
        var json = JsonSerializer.Serialize(obj, _jsonOptions);
        return JsonSerializer.Deserialize<T>(json, _jsonOptions)!;
    }
    catch (NotSupportedException ex)
    {
        throw new InvalidOperationException(
            $"Type {typeof(T).Name} cannot be serialized for Old<T>(). " +
            "Ensure the type is JSON-serializable or avoid using Old<T>() with this type.",
            ex);
    }
}
```

### Documentation Requirements

- Clearly document that `Old<T>()` requires JSON-serializable types
- Provide examples of compatible and incompatible types
- Suggest workarounds for complex scenarios (manual state capture)

---

## References

- [System.Text.Json Documentation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/overview)
- [Java Jackson Library](https://github.com/FasterXML/jackson) (used in Java uContract)
- [ReferenceHandler.IgnoreCycles](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/preserve-references#ignore-circular-references)

---

## Revision History

| Date       | Status      | Notes                          |
|------------|-------------|--------------------------------|
| 2025-10-18 | Accepted    | Decision finalized             |
