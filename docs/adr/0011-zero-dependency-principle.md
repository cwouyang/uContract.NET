# ADR-0011: Zero-Dependency Principle

## Status

**Accepted**

- **Date**: 2025-10-18
- **Deciders**: Project maintainers
- **Status Date**: 2025-10-18

---

## Context

### Problem Statement

We need to establish a dependency management policy for uContract.NET. Key questions:
- Should we allow external NuGet dependencies?
- How do we handle version conflicts with user projects?
- What about transitive dependencies?
- How do we ensure long-term maintainability?

This decision affects:
- **Package size**: More dependencies = larger package
- **Version conflicts**: Dependencies can conflict with user's dependencies
- **Security**: Each dependency is a potential vulnerability
- **Maintenance burden**: Dependencies require updates and compatibility management
- **Installation simplicity**: Fewer dependencies = easier installation

### Relevant Context

- The Java version has minimal dependencies:
  - **Jackson** (JSON serialization) for `old()` method
  - **AssertJ** (assertions) for `ensureAssignable()` method
- .NET has rich built-in libraries:
  - `System.Text.Json` for JSON serialization (built-in since .NET Core 3.0)
  - `System.Reflection` for runtime type inspection
  - `System.Threading` for thread-local storage
- Many popular .NET libraries have heavy dependency trees
- Dependency conflicts are a common pain point in .NET projects

### Constraints

- Must provide all core DBC functionality
- Must work across all .NET 8+ platforms
- Should minimize potential for version conflicts
- Must maintain long-term without dependency update churn

---

## Decision

**We will maintain a strict zero-dependency policy: uContract.NET will have NO external NuGet package dependencies. All functionality will use only built-in .NET APIs.**

### Details

**Allowed**:
- ✅ .NET BCL (Base Class Library) APIs:
  - `System.*` namespaces
  - `System.Text.Json` for serialization
  - `System.Reflection` for type inspection
  - `System.Threading` for concurrency
  - `System.Text.RegularExpressions` for pattern matching
  - `System.Collections.Concurrent` for thread-safe collections

**Not Allowed**:
- ❌ External NuGet packages (Newtonsoft.Json, FluentAssertions, etc.)
- ❌ Microsoft.Extensions.* packages (Logging, DependencyInjection, etc.)
- ❌ Third-party libraries

**Test Dependencies** (allowed, not distributed):
- ✅ xUnit (testing only, not part of runtime package)
- ✅ BenchmarkDotNet (performance testing, dev-only)
- ✅ Coverlet (code coverage, dev-only)

**Package Metadata**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <GeneratePackageOnBuild>true</GeneratePackageOnBuild>

    <!-- Zero dependencies -->
    <NoWarn>$(NoWarn);NU5128</NoWarn> <!-- Suppress warning for no dependencies -->
  </PropertyGroup>

  <!-- NO <PackageReference> elements for runtime -->
  <!-- Only built-in .NET APIs -->
</Project>
```

**Verification**:
```bash
# Verify no dependencies in NuGet package
dotnet pack
nuget list -Source ./bin/Release uContract -AllVersions -Detailed
# Should show: "Dependencies: None"
```

---

## Consequences

### Positive Consequences

- ✅ **Zero version conflicts**: Cannot conflict with user's dependencies
- ✅ **Minimal package size**: Only contains uContract code (~50-100KB estimated)
- ✅ **Simple installation**: `dotnet add package uContract` — done
- ✅ **No transitive dependencies**: No hidden dependencies pulled in
- ✅ **Long-term stability**: No dependency updates required
- ✅ **Security**: Smaller attack surface, no dependency vulnerabilities
- ✅ **Offline friendly**: Works without accessing external package sources
- ✅ **Corporate-friendly**: Easier to approve (no dependency chain to audit)

### Negative Consequences

- ❌ **Limited to .NET built-in features**: Cannot use advanced third-party libraries
- ❌ **Must implement everything**: No shortcuts via external packages
- ❌ **May miss optimizations**: Third-party libs might be more optimized
- ❌ **Comparison limitations**: `EnsureAssignable()` won't use AssertJ-equivalent

### Neutral Consequences

- ⚖️ **Different from Java version**: Java version uses Jackson and AssertJ
- ⚖️ **More code to maintain**: We implement features instead of using libraries

---

## Alternatives Considered

### Alternative 1: Minimal Dependencies (System.Text.Json, etc.)

**Description**: Allow .NET-provided NuGet packages like `System.Text.Json` for .NET Standard 2.0

**Pros**:
- Could target .NET Standard 2.0 (wider compatibility)
- Still uses Microsoft-provided packages (trusted)

**Cons**:
- **Not truly zero-dependency**: Still has NuGet dependencies
- **Version conflict potential**: Different apps might use different System.Text.Json versions
- **Not needed**: We target .NET 8+, which has System.Text.Json built-in

**Why rejected**: We target .NET 8+ (ADR-0001), so `System.Text.Json` is already included in the framework. No need for extra dependencies.

---

### Alternative 2: Allow Critical Dependencies (Jackson.NET / Newtonsoft.Json)

**Description**: Allow Newtonsoft.Json for better JSON serialization

**Pros**:
- More mature JSON library
- Better handling of edge cases
- Richer features (polymorphism, custom converters)

**Cons**:
- **Adds dependency**: Goes against simplicity principle
- **Version conflicts**: Newtonsoft.Json is common in user projects (conflict risk)
- **Not necessary**: `System.Text.Json` is sufficient for DBC use cases

**Why rejected**: `System.Text.Json` handles 99% of DDD value object serialization needs. The extra 1% doesn't justify a dependency.

---

### Alternative 3: Pluggable Dependencies (Let Users Choose)

**Description**: Make serialization/comparison pluggable, let users provide implementations

```csharp
public interface IDeepCopyProvider
{
    T DeepCopy<T>(T obj);
}

Contract.Configure(config =>
{
    config.DeepCopyProvider = new NewtonsoftDeepCopyProvider();
});
```

**Pros**:
- Ultimate flexibility
- Users can optimize for their scenarios
- Can use their preferred libraries

**Cons**:
- **Complexity**: Requires configuration, interfaces, abstractions
- **Breaks zero-setup**: Users must configure before using
- **Over-engineering**: YAGNI — 99% of users don't need this
- **Testing burden**: Must test multiple provider implementations

**Why rejected**: Adds significant complexity for marginal benefit. Simple, opinionated defaults (System.Text.Json) work for the vast majority of use cases.

---

### Alternative 4: Separate Packages for Extensions

**Description**: Core package has zero dependencies, extension packages add features with dependencies

```
uContract (zero dependencies)
uContract.Newtonsoft (uses Newtonsoft.Json)
uContract.FluentValidation (integrates FluentValidation)
```

**Pros**:
- Core package stays dependency-free
- Power users can opt into extensions
- Modular architecture

**Cons**:
- **Maintenance burden**: Multiple packages to maintain and version
- **Confusing**: Users must understand which package to use
- **Version sync complexity**: Must keep packages in sync
- **Not needed**: Core package provides all necessary functionality

**Why rejected**: Core uContract provides complete DBC functionality using built-in APIs. Extensions would be unnecessary complexity.

---

## Related Decisions

- **Related to**: ADR-0001 (Targeting .NET 8 means built-in APIs are sufficient)
- **Related to**: ADR-0006 (System.Text.Json for serialization — built-in)
- **Related to**: ADR-0007 (System.Reflection for field comparison — built-in)
- **Related to**: ADR-0009 (System.Threading.AsyncLocal — built-in)
- **Related to**: ADR-0010 (xUnit chosen as test framework - allowed as dev dependency, not distributed)
- **Related to**: ADR-0013 (No subcontracting support — custom tooling for subcontracting would violate this principle)
- **Affects**: All implementation decisions (must use built-in APIs only)

---

## Implementation Notes

### Dependency Verification in CI/CD

Add CI/CD check to ensure no dependencies sneak in:

```yaml
# GitHub Actions example
- name: Verify Zero Dependencies
  run: |
    dotnet pack -c Release
    # Check that package has no dependencies
    unzip -p bin/Release/uContract.*.nupkg uContract.nuspec | grep -q "<dependencies />" || exit 1
```

### Code Review Checklist

When reviewing PRs, ensure:
- ✅ No `<PackageReference>` added to main project
- ✅ Only `System.*` namespaces imported
- ✅ No use of external libraries

### Documentation

Clearly advertise zero-dependency as a feature:

```markdown
# uContract.NET

✨ **Features**:
- 🎯 Zero dependencies — uses only built-in .NET APIs
- 📦 Minimal package size (~50KB)
- ⚡ No version conflicts
- 🔒 Small security surface
```

### Exception to Test Projects

Test projects (`uContract.Tests`, `uContract.Benchmarks`) ARE allowed dependencies:
```xml
<ItemGroup>
  <!-- OK: Test dependencies, not distributed -->
  <PackageReference Include="xunit" Version="2.6.0" />
  <PackageReference Include="BenchmarkDotNet" Version="0.13.10" />
</ItemGroup>
```

---

## Long-Term Considerations

### If .NET Framework Support is Needed

If we later decide to support .NET Framework or .NET Standard 2.0:
- `System.Text.Json` would become a NuGet dependency
- **Decision**: Re-evaluate zero-dependency principle vs. compatibility
- **Recommendation**: Maintain .NET 8+ focus to preserve zero dependencies

### If Advanced Features are Requested

If users request features requiring dependencies (e.g., advanced comparison):
- **Option 1**: Implement using reflection (slower but zero-dependency)
- **Option 2**: Document how users can implement themselves
- **Option 3**: Create optional extension package (separate from core)

**Recommended**: Option 1 or 2. Keep core dependency-free.

---

## References

- [NuGet Package Dependencies](https://learn.microsoft.com/en-us/nuget/consume-packages/dependency-resolution)
- [.NET API Browser](https://learn.microsoft.com/en-us/dotnet/api/)
- [System.Text.Json](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/overview) (built-in)
- [Dependency Hell](https://en.wikipedia.org/wiki/Dependency_hell)

---

## Revision History

| Date       | Status      | Notes                          |
|------------|-------------|--------------------------------|
| 2025-10-18 | Accepted    | Decision finalized             |
