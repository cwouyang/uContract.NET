# ADR-0001: Target Framework - .NET 8

## Status

**Accepted**

- **Date**: 2025-10-18
- **Deciders**: Project maintainers
- **Status Date**: 2025-10-18

---

## Context

### Problem Statement

We need to decide which .NET version(s) to target for uContract.NET. This decision affects:
- Available C# language features and APIs
- User adoption (compatibility with existing projects)
- Maintenance cost (supporting multiple versions requires more testing)
- Performance characteristics and modern optimizations

### Relevant Context

- The Java version uses Java 17+ with modern features
- .NET 8 is the latest Long-Term Support (LTS) release
- .NET 6 is the previous LTS release (supported until November 2024)
- .NET Standard 2.0 offers maximum compatibility but limits available APIs
- System.Text.Json (required for `Old<T>()`) is built-in from .NET Core 3.0+

### Constraints

- Must support `System.Text.Json` for deep copy functionality (`Old<T>()`)
- Must support modern C# features including nullable reference types
- Should align with the modern, type-safe philosophy of the Java version

---

## Decision

**We will target .NET 8 as the minimum supported framework.**

### Details

- **Target Framework Moniker (TFM)**: `net8.0`
- **C# Language Version**: C# 12 (default for .NET 8)
- **No multi-targeting** for the initial release (v1.0.0)
- Future versions may add .NET 9+ support as new LTS versions are released

---

## Consequences

### Positive Consequences

- ✅ **Modern C# features**: Access to latest language features (primary constructors, collection expressions, etc.)
- ✅ **Best performance**: .NET 8 includes significant performance improvements over earlier versions
- ✅ **Built-in APIs**: System.Text.Json with all features available without additional dependencies
- ✅ **Long-term support**: .NET 8 LTS is supported until November 2026
- ✅ **Nullable reference types**: Full support for modern null safety features
- ✅ **Simplified maintenance**: Single target framework reduces testing matrix

### Negative Consequences

- ❌ **Limited adoption**: Projects on .NET 6 or .NET Framework cannot use this library
- ❌ **Migration barrier**: Users must upgrade to .NET 8 to adopt uContract.NET
- ❌ **No .NET Framework support**: Cannot be used in legacy .NET Framework projects

### Neutral Consequences

- ⚖️ **Modern-only approach**: Explicitly targeting modern .NET aligns with the library's philosophy
- ⚖️ **Clear upgrade path**: Users know they need .NET 8+; no confusion about supported versions

---

## Alternatives Considered

### Alternative 1: .NET 6 (Previous LTS)

**Description**: Target .NET 6 as the minimum version

**Pros**:
- Wider adoption (many projects still on .NET 6)
- Still LTS until November 2024
- Has all necessary APIs (System.Text.Json, nullable reference types)

**Cons**:
- Less performance optimizations compared to .NET 8
- LTS support ending soon (November 2024)
- Missing some C# 11/12 features

**Why rejected**: .NET 6 LTS support is ending in 2024. Starting a new library on an expiring LTS version would force an early upgrade. .NET 8 provides better long-term stability.

---

### Alternative 2: .NET Standard 2.0

**Description**: Target .NET Standard 2.0 for maximum compatibility

**Pros**:
- Maximum compatibility (includes .NET Framework 4.6.1+, .NET Core 2.0+, Xamarin)
- Largest potential user base

**Cons**:
- No built-in `System.Text.Json` (requires NuGet dependency, breaks zero-dependency principle)
- Limited C# language features (no nullable reference types, no modern syntax)
- Performance limitations
- Increased complexity to work around missing APIs

**Why rejected**: Would compromise the library's design philosophy. Nullable reference types are essential for type safety, and adding external dependencies contradicts the zero-dependency principle.

---

### Alternative 3: Multi-targeting (net6.0;net8.0)

**Description**: Support both .NET 6 and .NET 8 simultaneously

**Pros**:
- Wider compatibility
- Users can choose based on their constraints

**Cons**:
- Increased testing burden (must test both TFMs)
- Increased maintenance complexity
- Conditional compilation complexity
- .NET 6 LTS ending soon makes this short-lived benefit

**Why rejected**: Added complexity does not justify the benefit. .NET 6 support ends in 2024, so multi-targeting would only be useful for ~6 months. Better to start with .NET 8 from the beginning.

---

## Related Decisions

- **Related to**: ADR-0005 (Generics and Type Constraints) - .NET 8 ensures full support for nullable reference types
- **Related to**: ADR-0006 (Serialization via System.Text.Json) - .NET 8 has System.Text.Json built-in

---

## Implementation Notes

- `.csproj` will specify `<TargetFramework>net8.0</TargetFramework>`
- CI/CD pipeline should test against .NET 8 SDK
- Documentation should clearly state ".NET 8+ required"
- NuGet package metadata should specify minimum framework version

---

## References

- [.NET Release Schedule](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
- [.NET 8 Announcement](https://devblogs.microsoft.com/dotnet/announcing-dotnet-8/)
- [C# 12 Language Features](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-12)

---

## Revision History

| Date       | Status      | Notes                          |
|------------|-------------|--------------------------------|
| 2025-10-18 | Accepted    | Decision finalized             |
