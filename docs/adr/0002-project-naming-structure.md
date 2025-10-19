# ADR-0002: Project Naming and Structure

## Status

**Accepted**

- **Date**: 2025-10-18
- **Deciders**: Project maintainers
- **Status Date**: 2025-10-18

---

## Context

### Problem Statement

We need to establish naming conventions for:
- NuGet Package ID
- Root namespace
- Solution and project file names
- Assembly names

This affects user experience (how they reference the library), discoverability (finding it on NuGet), and consistency with the Java version.

### Relevant Context

- The Java version uses:
  - Group ID: `tw.teddysoft.ucontract`
  - Artifact ID: `uContract`
  - Package: `tw.teddysoft.ucontract`
- .NET naming conventions favor simple, unambiguous names
- NuGet.org allows Package IDs without organizational prefixes
- The library should be easy to discover and reference

### Constraints

- Must be clear and unambiguous on NuGet.org
- Should avoid conflicts with existing packages
- Should be easy to type and remember
- Namespace should follow .NET conventions (PascalCase)

---

## Decision

**We will use `uContract` for all naming contexts without organizational prefix.**

### Details

- **NuGet Package ID**: `uContract`
- **Root Namespace**: `uContract`
- **Solution Name**: `uContract.sln`
- **Main Project Name**: `uContract.csproj`
- **Assembly Name**: `uContract.dll`
- **Test Project**: `uContract.Tests.csproj`
- **Sample Project**: `uContract.Samples.DDD.csproj` (if created)

**Usage Example**:
```csharp
using uContract;

Contract.Require("x positive", () => x > 0);
```

**Installation**:
```bash
dotnet add package uContract
```

---

## Consequences

### Positive Consequences

- ✅ **Simple and memorable**: Easy to type and remember
- ✅ **Consistent with Java naming**: Same artifact name (`uContract`)
- ✅ **Clean imports**: `using uContract;` is concise
- ✅ **Searchable**: Easy to search for on NuGet.org and GitHub
- ✅ **No namespace pollution**: Short namespace won't conflict with user code

### Negative Consequences

- ❌ **Potential name conflicts**: No organizational prefix means slightly higher risk of future naming conflicts
- ❌ **Less organizational identity**: Doesn't explicitly identify TeddySoft as maintainer

### Neutral Consequences

- ⚖️ **Simple branding**: Focuses on the library name rather than organization
- ⚖️ **Different from Java convention**: Java uses `tw.teddysoft.ucontract`, .NET uses `uContract`

---

## Alternatives Considered

### Alternative 1: TeddySoft.uContract

**Description**: Use organizational prefix for both Package ID and namespace

**Pros**:
- More explicit organizational identity
- Closer match to Java's `tw.teddysoft.ucontract`
- Lower risk of naming conflicts

**Cons**:
- Longer to type: `using TeddySoft.uContract;`
- Adds unnecessary verbosity for a focused library
- Less common pattern in .NET ecosystem (compared to Java)

**Why rejected**: .NET ecosystem favors shorter, more direct names (e.g., `Newtonsoft.Json` is an exception, not the rule). Most popular libraries use simple names (Dapper, AutoMapper, FluentValidation).

---

### Alternative 2: uContract.NET (Package ID) + uContract (Namespace)

**Description**: Use `.NET` suffix in Package ID to emphasize platform, but keep namespace clean

**Pros**:
- Clearly identifies this as the .NET port
- Namespace remains clean
- Easier to distinguish from Java version in documentation

**Cons**:
- Mismatch between Package ID and namespace is confusing
- `.NET` suffix is redundant (it's on NuGet.org, obviously .NET)
- Inconsistent with package/namespace alignment convention

**Why rejected**: In .NET, Package ID and primary namespace should typically match. Having `uContract.NET` package but `uContract` namespace creates unnecessary confusion.

---

### Alternative 3: UContract (PascalCase everywhere)

**Description**: Use standard PascalCase for both namespace and Package ID

**Pros**:
- Follows strict .NET naming conventions (namespaces should be PascalCase)
- Consistent casing

**Cons**:
- Loses the distinctive lowercase 'u' branding from the original uContract
- "UContract" looks less distinctive than "uContract"
- Breaks visual continuity with Java version

**Why rejected**: The lowercase 'u' is part of the brand identity. While `uContract` is technically non-standard for .NET namespace casing, it maintains brand consistency and is acceptable for library names (similar to IdentityServer, EntityFramework).

---

## Related Decisions

- **Related to**: Overall branding and consistency with Java uContract
- **Affects**: ADR-0003 (API design - defines `Contract` class structure within `uContract` namespace)
- **Affects**: All documentation, import statements, and user-facing references

---

## Implementation Notes

- All `.csproj` files should use consistent naming:
  ```xml
  <PackageId>uContract</PackageId>
  <AssemblyName>uContract</AssemblyName>
  <RootNamespace>uContract</RootNamespace>
  ```
- Documentation should consistently use `uContract` (lowercase 'u')
- README should show: `dotnet add package uContract`
- GitHub repository should be named `ucontract` or `uContract` (lowercase preferred for URLs)

---

## References

- [.NET Naming Guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/naming-guidelines)
- [NuGet Package ID Guidelines](https://learn.microsoft.com/en-us/nuget/create-packages/creating-a-package#choose-a-unique-package-identifier)
- [Java uContract Maven Coordinates](https://gitlab.com/TeddyChen/ucontract/)

---

## Revision History

| Date       | Status      | Notes                          |
|------------|-------------|--------------------------------|
| 2025-10-18 | Accepted    | Decision finalized             |
