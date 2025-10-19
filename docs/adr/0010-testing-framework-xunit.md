# ADR-0010: Testing Framework - xUnit

## Status

**Accepted**

- **Date**: 2025-10-18
- **Deciders**: Project maintainers
- **Status Date**: 2025-10-18

---

## Context

### Problem Statement

We need to select a testing framework and related tools for unit testing, integration testing, and potentially performance testing. Key decisions:
- Which test framework: xUnit, NUnit, or MSTest?
- Which assertion library: built-in or third-party (FluentAssertions)?
- Which mocking framework (if needed): Moq, NSubstitute?
- Code coverage tool: Coverlet, others?
- Performance benchmarking: BenchmarkDotNet?

This decision affects:
- **Development workflow**: How developers write and run tests
- **CI/CD integration**: How tests are executed in pipelines
- **Test readability**: How clear and maintainable tests are
- **Community familiarity**: How easy it is for contributors to understand tests

### Relevant Context

- The Java version uses **JUnit 5** (Jupiter) for testing
- .NET has three major testing frameworks:
  - **xUnit**: Modern, community-driven, used by .NET Core team
  - **NUnit**: Mature, closer to JUnit syntax
  - **MSTest**: Microsoft's official framework, less feature-rich
- uContract is a library with no external dependencies, making mocking unnecessary for most tests
- Contract validation logic is deterministic, making unit tests straightforward

### Constraints

- Must integrate with .NET CLI (`dotnet test`)
- Should work in CI/CD (GitHub Actions, Azure Pipelines)
- Must support parallel test execution for performance
- Should have good Visual Studio / Rider integration

---

## Decision

**We will use xUnit as the primary testing framework, with no assertion library beyond xUnit's built-in assertions, and BenchmarkDotNet for performance testing.**

### Details

**Testing Stack**:
```xml
<ItemGroup>
  <!-- Test Framework -->
  <PackageReference Include="xunit" Version="2.6.0" />
  <PackageReference Include="xunit.runner.visualstudio" Version="2.5.0" />

  <!-- Test Platform -->
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />

  <!-- Code Coverage (optional, dev-only) -->
  <PackageReference Include="coverlet.collector" Version="6.0.0" />
</ItemGroup>
```

**Performance Testing** (separate project):
```xml
<PackageReference Include="BenchmarkDotNet" Version="0.13.10" />
```

**Test Structure**:
```
tests/
├── uContract.Tests/                    # Unit tests
│   ├── PreconditionTests.cs
│   ├── PostconditionTests.cs
│   ├── InvariantTests.cs
│   ├── CheckTests.cs
│   ├── HelperMethodsTests.cs
│   ├── ConfigurationTests.cs
│   ├── ExceptionTests.cs
│   ├── OldMethodTests.cs
│   ├── EnsureAssignableTests.cs
│   └── AsyncSupportTests.cs
│
└── uContract.Benchmarks/               # Performance benchmarks
    ├── ContractOverheadBenchmarks.cs
    ├── ReflectionCachingBenchmarks.cs
    └── SerializationBenchmarks.cs
```

**Example Test**:
```csharp
using Xunit;
using uContract;

public class PreconditionTests
{
    [Fact]
    public void Require_ShouldThrowException_WhenConditionIsFalse()
    {
        // Arrange
        var x = -1;

        // Act & Assert
        var ex = Assert.Throws<PreconditionViolationException>(
            () => Contract.Require("x must be positive", () => x > 0)
        );

        Assert.Contains("x must be positive", ex.Message);
        Assert.Equal(ContractType.Precondition, ex.ViolationType);
    }

    [Fact]
    public void Require_ShouldNotThrow_WhenConditionIsTrue()
    {
        // Arrange
        var x = 5;

        // Act & Assert (no exception = success)
        Contract.Require("x must be positive", () => x > 0);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void Require_ShouldNotThrow_ForPositiveValues(int x)
    {
        Contract.Require("x must be positive", () => x > 0);
    }
}
```

**Test Coverage Target**: >90% for core contract methods

---

## Consequences

### Positive Consequences

- ✅ **Modern .NET standard**: xUnit is used by .NET Core team and modern libraries
- ✅ **Clean syntax**: `[Fact]` and `[Theory]` are intuitive
- ✅ **Parallel execution**: Tests run in parallel by default (better performance)
- ✅ **Excellent tooling**: Great Visual Studio and Rider support
- ✅ **Community adoption**: Large ecosystem and community support
- ✅ **Simple setup**: No complex configuration needed
- ✅ **Built-in assertions**: Sufficient for DBC testing, no FluentAssertions needed

### Negative Consequences

- ❌ **Different from Java (JUnit)**: Syntax differs from Java version
- ❌ **Less familiar to some developers**: NUnit is closer to JUnit syntax
- ❌ **No built-in mocking**: But we don't need mocking for DBC tests

### Neutral Consequences

- ⚖️ **Opinionated design**: xUnit's design (e.g., no [SetUp]) may be unfamiliar to NUnit users
- ⚖️ **Different assertion style**: `Assert.Equal(expected, actual)` vs FluentAssertions' `actual.Should().Be(expected)`

---

## Alternatives Considered

### Alternative 1: NUnit

**Description**: Use NUnit 3.x as the test framework

**Pros**:
- Closer to JUnit syntax (familiar for Java developers)
- Mature and stable
- Rich assertion library
- `[SetUp]` and `[TearDown]` attributes

**Cons**:
- Less modern than xUnit
- Not used by .NET Core team
- No parallel execution by default
- More verbose test setup

**Why rejected**: xUnit is the modern .NET standard and aligns better with current .NET ecosystem practices. The syntax difference from JUnit is minimal and worth the benefits.

---

### Alternative 2: MSTest

**Description**: Use Microsoft's official test framework

**Pros**:
- Official Microsoft framework
- Good Visual Studio integration
- Simple and straightforward

**Cons**:
- Less feature-rich than xUnit/NUnit
- Smaller community
- Less active development
- Limited extensibility

**Why rejected**: MSTest lacks features and community support compared to xUnit. No compelling reason to use it over xUnit.

---

### Alternative 3: xUnit + FluentAssertions

**Description**: Use xUnit with FluentAssertions for more expressive assertions

```csharp
result.Should().BePositive();
exception.Should().BeOfType<PreconditionViolationException>()
    .Which.Description.Should().Contain("positive");
```

**Pros**:
- More readable assertions
- Better error messages
- Fluent, chainable API

**Cons**:
- **Additional dependency**: Goes against zero-dependency principle for tests
- **Learning curve**: Developers must learn FluentAssertions API
- **Overkill**: DBC tests are simple boolean checks, don't need elaborate assertions

**Why rejected**: xUnit's built-in assertions are sufficient for contract testing. Adding FluentAssertions would be over-engineering for our use case.

---

### Alternative 4: Multiple Frameworks for Compatibility Testing

**Description**: Test against multiple frameworks (xUnit, NUnit, MSTest) to ensure compatibility

**Pros**:
- Verifies library works with all test frameworks
- Broadest compatibility testing

**Cons**:
- **Unnecessary complexity**: Library doesn't interact with test frameworks
- **Maintenance burden**: Must maintain multiple test projects
- **No real benefit**: Contract methods don't depend on test framework choice

**Why rejected**: The library is framework-agnostic. Users can use any test framework they want. We only need one for our own tests.

---

## Related Decisions

- **Related to**: ADR-0011 (Zero-dependency principle applies to runtime, not test dependencies)
- **Affects**: Test project structure, CI/CD test execution
- **Future**: Documentation generation (Item 19) may integrate with xUnit's `ITestOutputHelper`

---

## Implementation Notes

### Test Project Configuration

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit" Version="2.6.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="coverlet.collector" Version="6.0.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\uContract\uContract.csproj" />
  </ItemGroup>
</Project>
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run with code coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Run specific test class
dotnet test --filter "FullyQualifiedName~PreconditionTests"

# Run in parallel (default in xUnit)
dotnet test --parallel

# Generate coverage report
dotnet test /p:CollectCoverage=true /p:CoverletOutput=./coverage/ /p:CoverletOutputFormat=lcov
```

### Test Categories

Use `[Trait]` for test categorization:
```csharp
[Fact]
[Trait("Category", "Unit")]
public void Require_BasicTest() { }

[Fact]
[Trait("Category", "Integration")]
public void ComplexAggregateScenario() { }
```

Filter by category:
```bash
dotnet test --filter "Category=Unit"
```

### Performance Benchmarks

```csharp
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

[MemoryDiagnoser]
public class ContractOverheadBenchmarks
{
    private int _value = 5;

    [Benchmark(Baseline = true)]
    public void DirectCheck()
    {
        if (_value <= 0)
            throw new Exception("Invalid");
    }

    [Benchmark]
    public void ContractRequire()
    {
        Contract.Require("value positive", () => _value > 0);
    }
}
```

Run benchmarks:
```bash
dotnet run -c Release --project tests/uContract.Benchmarks
```

---

## References

- [xUnit Documentation](https://xunit.net/)
- [xUnit GitHub](https://github.com/xunit/xunit)
- [BenchmarkDotNet](https://benchmarkdotnet.org/)
- [Coverlet](https://github.com/coverlet-coverage/coverlet)
- [JUnit 5](https://junit.org/junit5/) (Java uContract uses this)

---

## Revision History

| Date       | Status      | Notes                          |
|------------|-------------|--------------------------------|
| 2025-10-18 | Accepted    | Decision finalized             |
