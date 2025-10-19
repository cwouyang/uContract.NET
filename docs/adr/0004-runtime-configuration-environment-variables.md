# ADR-0004: Runtime Configuration via Environment Variables

## Status

**Accepted**

- **Date**: 2025-10-18
- **Deciders**: Project maintainers
- **Status Date**: 2025-10-18

---

## Context

### Problem Statement

We need to decide how users can enable/disable Design by Contract checks at runtime. Options include:
- Environment variables
- Configuration files (appsettings.json)
- Compile-time symbols ([Conditional] attribute)
- Static configuration API
- Multiple configuration sources with priority

This decision affects:
- **Deployment flexibility**: Can users change settings without recompiling?
- **Performance**: What overhead exists when contracts are disabled?
- **Cross-platform compatibility**: Does it work consistently across Windows, Linux, macOS?
- **Java API parity**: Can we maintain the same configuration mechanism?

### Relevant Context

- The Java version uses environment variables exclusively:
  - `DBC=off` — Disable all contracts
  - `DBC_PRE=off` — Disable preconditions only
  - `DBC_POST=off` — Disable postconditions only
  - `DBC_INV=off` — Disable invariants only
  - `DBC_CHECK=off` — Disable check statements
  - `DBC_DOC=on` — Enable documentation generation
- Environment variables are cross-platform and universally available
- ASP.NET Core uses `appsettings.json`, but uContract is not ASP.NET-specific
- Compile-time configuration ([Conditional]) offers zero overhead but no runtime flexibility

### Constraints

- Must support runtime enable/disable without recompilation
- Must work across all .NET platforms (Windows, Linux, macOS, containers)
- Must be simple and require no additional dependencies
- Should maintain consistency with Java version

---

## Decision

**We will use environment variables as the sole runtime configuration mechanism, with hierarchical priority: specific flags > global flag > build-specific defaults.**

### Details

**Supported Environment Variables**:

| Variable      | Values      | Purpose                          |
|---------------|-------------|----------------------------------|
| `DBC`         | `true`/`false`/`on`/`off`/`1`/`0`/`yes`/`no` | Global switch for all contracts (acts as default) |
| `DBC_PRE`     | `true`/`false`/`on`/`off`/`1`/`0`/`yes`/`no` | Overrides global DBC for preconditions |
| `DBC_POST`    | `true`/`false`/`on`/`off`/`1`/`0`/`yes`/`no` | Overrides global DBC for postconditions |
| `DBC_INV`     | `true`/`false`/`on`/`off`/`1`/`0`/`yes`/`no` | Overrides global DBC for invariants |
| `DBC_CHECK`   | `true`/`false`/`on`/`off`/`1`/`0`/`yes`/`no` | Overrides global DBC for checks |
| `DBC_DOC`     | `true`/`false`/`on`/`off`/`1`/`0`/`yes`/`no` | Documentation generation (independent) |

**Priority Order** (highest to lowest):
1. **Specific type flags** (`DBC_PRE`, `DBC_POST`, `DBC_INV`, `DBC_CHECK`)
2. **Global flag** (`DBC`)
3. **Build-specific defaults** (Debug: enabled, Release: disabled)

**Default Behavior**:
- **Debug builds**: All contracts enabled by default (development-friendly)
- **Release builds**: All contracts disabled by default (production-friendly)
- **Global DBC flag**: Acts as default when specific flags are not set
- **Specific flags**: Allow fine-grained control, can enable/disable individual contract types regardless of global setting

**Evaluation Logic**:

The priority is implemented using null-coalescing (`??`) operator, allowing any level to be overridden:

```csharp
public class ContractConfiguration
{
    public bool PreconditionsEnabled { get; }
    public bool PostconditionsEnabled { get; }
    public bool InvariantsEnabled { get; }
    public bool CheckEnabled { get; }
    public bool DocumentationEnabled { get; }

    public ContractConfiguration()
    {
        // Determine build-specific default
        bool defaultEnabled = IsDebugBuild();

        // Read environment variables
        bool? globalDbc = ParseEnvironmentVariable("DBC");
        bool? preconditionsDbc = ParseEnvironmentVariable("DBC_PRE");
        bool? postconditionsDbc = ParseEnvironmentVariable("DBC_POST");
        bool? invariantsDbc = ParseEnvironmentVariable("DBC_INV");
        bool? checkDbc = ParseEnvironmentVariable("DBC_CHECK");

        // Apply priority: specific > global > default
        PreconditionsEnabled = preconditionsDbc ?? globalDbc ?? defaultEnabled;
        PostconditionsEnabled = postconditionsDbc ?? globalDbc ?? defaultEnabled;
        InvariantsEnabled = invariantsDbc ?? globalDbc ?? defaultEnabled;
        CheckEnabled = checkDbc ?? globalDbc ?? defaultEnabled;

        // Documentation is independent (default: false)
        DocumentationEnabled = ParseEnvironmentVariable("DBC_DOC") ?? false;
    }

    private static bool? ParseEnvironmentVariable(string variableName)
    {
        string? value = Environment.GetEnvironmentVariable(variableName);
        if (string.IsNullOrWhiteSpace(value)) return null;

        return value.ToLowerInvariant() switch
        {
            "true" or "1" or "yes" or "on" => true,
            "false" or "0" or "no" or "off" => false,
            _ => null  // Invalid values treated as unset, allowing fallback
        };
    }

    private static bool IsDebugBuild()
    {
        #if DEBUG
            return true;
        #else
            return false;
        #endif
    }
}
```

**How it works**:
- If `DBC_PRE` is set → Use that value
- Else if `DBC` is set → Use that value
- Else → Use build-specific default (Debug: true, Release: false)

This allows maximum flexibility:
- `DBC=false, DBC_PRE=true` → Preconditions enabled, other types disabled
- `DBC=true, DBC_POST=false` → Postconditions disabled, other types enabled
- Only `DBC=true` → All enabled (or all disabled for Release builds)
- No variables set → Use build defaults

**Configuration is read once at startup** (when `Contract` static constructor runs). Changes to environment variables during execution do not take effect.

**Usage Examples**:

```bash
# Example 1: Debug build - use defaults (all enabled)
dotnet run --configuration Debug
# Result: All contracts enabled

# Example 2: Release build - use defaults (all disabled)
dotnet run --configuration Release
# Result: All contracts disabled

# Example 3: Release build - Enable all contracts for production debugging
export DBC=true
dotnet run --configuration Release
# Result: All contracts enabled

# Example 4: Debug build - Disable specific contract type for performance test
export DBC_POST=false
dotnet run --configuration Debug
# Result: Pre/Inv/Check enabled, Post disabled

# Example 5: Release build - Enable only preconditions for debugging
export DBC=false
export DBC_PRE=true
dotnet run --configuration Release
# Result: Only preconditions enabled (other types disabled)
# This is the key advantage: fine-grained control even in Release builds

# Example 6: Mixed configuration - Global disabled, selectively enable specific types
export DBC=false
export DBC_PRE=true
export DBC_POST=true
export DBC_INV=false
dotnet run --configuration Release
# Result: Pre and Post enabled, Inv and Check disabled

# Example 7: Enable documentation generation (for test runs)
export DBC_DOC=true
dotnet test
# Result: Documentation is generated during test runs
```

---

## Consequences

### Positive Consequences

- ✅ **Flexible defaults**: Debug builds validate contracts automatically, Release builds optimize performance
- ✅ **Production debugging capability**: Can enable contracts in Release builds via `DBC=on` without recompilation
- ✅ **Cross-platform**: Environment variables work universally (Windows, Linux, macOS, containers)
- ✅ **Simple**: No configuration files, no setup code required
- ✅ **Container-friendly**: Easily configured via Dockerfile `ENV` or Kubernetes ConfigMaps
- ✅ **CI/CD friendly**: Easy to enable/disable in build pipelines
- ✅ **No dependencies**: Uses built-in `Environment.GetEnvironmentVariable()`
- ✅ **Clear semantics**: Variable names are self-documenting
- ✅ **Runtime flexibility**: Can override defaults without recompilation

### Negative Consequences

- ❌ **No runtime changes**: Cannot enable/disable contracts after application starts
- ❌ **Not discoverable via code**: Users must read documentation to find variable names
- ❌ **No IDE autocomplete**: Variable names are strings, not strongly-typed properties
- ❌ **Limited to boolean flags**: Cannot support complex configuration (e.g., custom output formats)
- ❌ **Slight performance overhead in Release**: Even when disabled, `if` checks remain (but minimal)
- ⚠️ **Different from Java default behavior**: Java always defaults to `on`; .NET defaults to `off` in Release builds (intentional improvement for production safety)

### Neutral Consequences

- ⚖️ **Different from ASP.NET Core conventions**: ASP.NET apps typically use `appsettings.json`, but uContract is not ASP.NET-specific
- ⚖️ **Environment variable namespace**: Uses `DBC_*` prefix for cross-language consistency (see [Environment Variable Naming](#environment-variable-naming) section below)
- ⚖️ **Build-specific behavior**: Developers must be aware of Debug vs Release defaults

---

## Comparison with Java Implementation

### How .NET Implementation Differs

**Java Version** (null-coalescing not possible, uses AND logic):
```java
// Java always defaults to true
DBC = true;
if ("off".equals(dbc_env)) DBC = false;

CHECK_PRE = DBC;  // Initialized to global value
if ("off".equals(pre_env)) CHECK_PRE = false;  // Can only disable, not re-enable
```

**Behavior**: `DBC_PRE` can only disable preconditions, cannot override a disabled `DBC` flag.

**Example**:
- `DBC=false, DBC_PRE=on` → Preconditions remain disabled ❌
- `DBC=true, DBC_PRE=off` → Preconditions are disabled ✓

**Why .NET Uses Different Logic**:

1. **Null-coalescing semantics**: C# supports null-coalescing (`??`), enabling cleaner priority logic
2. **Production safety**: Release build default of `off` requires explicit opt-in to enable contracts
3. **Fine-grained control**: Production debugging scenario often requires `DBC=false` (all off) but `DBC_PRE=true` (enable specific type)
4. **Forward compatibility**: This design doesn't preclude stricter enforcement in future versions

### Full Flexibility Examples

These scenarios work in .NET but NOT in Java:

```bash
# Release build: Enable only preconditions for production debugging
export DBC=false
export DBC_PRE=true
# Java: Would disable preconditions anyway
# .NET: Preconditions enabled, others disabled ✓

# Release build: Enable all except invariants for testing
export DBC=true
export DBC_INV=false
# Java: Would disable invariants only
# .NET: Pre/Post/Check enabled, Inv disabled ✓
```

### Backward Compatibility with Java

If users don't set specific `DBC_XXX` variables:
- `.NET` behavior is identical to Java (uses `DBC` as default)
- Example: Only setting `DBC=on` gives same behavior as Java

This maintains compatibility for users migrating from Java to .NET without requiring code changes.

---

## Alternatives Considered

### Alternative 1: appsettings.json (ASP.NET Core Configuration)

**Description**: Use Microsoft.Extensions.Configuration to read from appsettings.json

```json
{
  "uContract": {
    "Enabled": true,
    "Preconditions": true,
    "Postconditions": true
  }
}
```

**Pros**:
- Familiar to ASP.NET Core developers
- Supports complex configuration structures
- Can be changed without setting environment variables

**Cons**:
- **Requires dependency**: `Microsoft.Extensions.Configuration` (breaks zero-dependency principle)
- **ASP.NET-specific**: Console apps, class libraries would need additional setup
- **Breaks Java parity**: Java version uses environment variables
- **More complex**: Requires configuration builder setup

**Why rejected**: Adding a dependency contradicts our zero-dependency principle. Environment variables are simpler and work everywhere without framework assumptions.

---

### Alternative 2: Compile-Time Configuration ([Conditional] Attribute)

**Description**: Use conditional compilation to remove contracts in Release builds

```csharp
[Conditional("DBC_ENABLED")]
public static void Require(string description, Func<bool> condition) { ... }
```

**Pros**:
- **True zero overhead**: Contract methods completely removed from Release builds
- **Maximum performance**: No runtime checks at all

**Cons**:
- **No runtime flexibility**: Cannot enable contracts in production for debugging
- **Breaks Java parity**: Java version supports runtime configuration
- **Requires recompilation**: To change settings, must rebuild the application
- **All-or-nothing**: Cannot selectively disable preconditions vs postconditions

**Why rejected**: Runtime flexibility is valuable for debugging production issues. The ability to enable contracts in a live environment (e.g., `export DBC=on && dotnet run`) without redeploying is worth the minimal overhead. While we use `#if DEBUG` for default values, we explicitly avoid `[Conditional]` to preserve runtime control.

---

### Alternative 3: Static Configuration API

**Description**: Provide a static configuration API

```csharp
Contract.Configure(options =>
{
    options.PreconditionsEnabled = true;
    options.PostconditionsEnabled = false;
});
```

**Pros**:
- Strongly-typed configuration
- IDE autocomplete support
- Programmatic control

**Cons**:
- **Breaks zero-setup principle**: Requires explicit configuration code
- **Initialization order issues**: When should `Configure()` be called?
- **Breaks Java parity**: Java version doesn't have programmatic configuration
- **Thread safety complexity**: What if `Configure()` is called concurrently?

**Why rejected**: Adds unnecessary complexity. Environment variables provide sufficient control without requiring setup code.

---

### Alternative 4: Multiple Configuration Sources (Priority Chain)

**Description**: Support environment variables, appsettings.json, and programmatic configuration with priority

**Pros**:
- Maximum flexibility
- Users can choose their preferred method

**Cons**:
- **Overly complex**: Adds significant implementation and testing burden
- **Confusing**: Users must understand priority rules
- **Breaks simplicity**: uContract is meant to be a simple, zero-setup library
- **Maintenance burden**: More code to maintain and test

**Why rejected**: YAGNI (You Aren't Gonna Need It). Environment variables alone cover all realistic use cases without added complexity.

---

## Related Decisions

- **Related to**: ADR-0003 (Static class design means configuration must be static/global)
- **Affects**: Performance characteristics, deployment workflows, documentation
- **Design choice**: Uses runtime checks instead of `[Conditional]` attribute for maximum flexibility

---

## Environment Variable Naming

### Why `DBC_*` Instead of More Specific Prefixes?

We considered three naming strategies:

**Option A: `UCONTRACT_*`** (more specific)
- Example: `UCONTRACT_ENABLED`, `UCONTRACT_PRE`
- Pros: Lower collision risk, clearer ownership
- Cons: Breaks Java parity, longer variable names

**Option B: `DBC_UCONTRACT_*`** (hybrid)
- Example: `DBC_UCONTRACT_PRE`
- Pros: Both namespaced and specific
- Cons: Verbose, awkward, still breaks Java parity

**Option C: `DBC_*`** (current choice)
- Example: `DBC`, `DBC_PRE`, `DBC_POST`
- Pros: Cross-language consistency, concise, semantically clear
- Cons: Potential collision with other DBC tools

**Decision**: We chose **Option C (`DBC_*`)** for the following reasons:

1. **Cross-language consistency**: Java uContract uses `DBC_*`, maintaining this naming allows:
   - Shared documentation across Java and .NET versions
   - Consistent mental model for polyglot teams
   - Same Docker/Kubernetes configuration files for both runtimes

2. **Low collision risk in practice**:
   - Very few production tools use `DBC` namespace
   - Generic "Design by Contract" namespace is appropriate for a DBC library
   - Users can verify: `env | grep DBC` before deployment

3. **Semantic clarity**: `DBC` clearly indicates "Design by Contract" functionality

### Potential Naming Conflicts

**Known Tools Using Similar Prefixes**:
- `DB_*` — Database connection strings (common)
- `DEBUG_*` — Various debugging tools
- Other DBC libraries (rare in .NET ecosystem)

**Collision Risk Assessment**:
- **Low risk with `DB_*`**: Different prefix (`DBC` vs `DB`)
- **Low risk with `DEBUG_*`**: Different prefix and different purpose
- **Medium risk with other DBC tools**: Possible but unlikely (few .NET DBC libraries exist)

**Mitigation Strategies**:

1. **Documentation**: Clearly document all environment variables in README
2. **Verification**: Provide diagnostic command to list all `DBC_*` variables:
   ```bash
   # Windows PowerShell
   Get-ChildItem Env: | Where-Object { $_.Name -like "DBC*" }

   # Linux/macOS
   env | grep ^DBC
   ```

3. **Startup logging** (when `DBC_DOC=on`):
   ```
   [uContract] Configuration loaded:
   [uContract]   DBC=on (from environment)
   [uContract]   DBC_PRE=on (default)
   [uContract]   DBC_POST=on (default)
   ```

4. **Future extensibility**: If conflicts arise, we can add:
   - `UCONTRACT_*` as alternative aliases (backward compatible)
   - Configuration file support to supplement environment variables

**Recommendation for Users**:
- In shared environments (containers, CI/CD), prefix with app name:
  ```dockerfile
  ENV DBC=off  # Simple case
  ENV MYAPP_DBC=off  # If multiple apps share environment
  ```

### Alternative: Future Support for `UCONTRACT_*`

If naming collisions become problematic, we can add aliasing in a future version:

```csharp
// Fallback logic (future enhancement)
var dbcEnv = Environment.GetEnvironmentVariable("DBC")
          ?? Environment.GetEnvironmentVariable("UCONTRACT_ENABLED");
```

This would be a **non-breaking change** and can be added if user feedback indicates conflicts.

---

## Implementation Notes

- Configuration is read **once** during static initialization:
  ```csharp
  public static class Contract
  {
      private static readonly ContractConfiguration _config = new();

      static Contract()
      {
          // _config is initialized here, reads environment variables
          // Defaults depend on DEBUG vs RELEASE build
      }
  }
  ```
- Each contract method checks the relevant flag before executing:
  ```csharp
  public static void Require(string description, Func<bool> condition)
  {
      if (!_config.PreconditionsEnabled) return;  // Runtime check, not [Conditional]

      // ... lazy evaluation and exception throwing ...
  }
  ```
- **Performance consideration**: The `if (!_config.PreconditionsEnabled)` check is extremely fast (~1ns) and considered acceptable overhead even in Release builds for the benefit of runtime flexibility
- Documentation should include:
  - Table of all environment variables with build-specific defaults
  - Examples for different platforms (bash, PowerShell, Dockerfile)
  - Examples showing Debug vs Release behavior
  - Note that configuration is read at startup only
  - Clarify that Release builds can still enable contracts via `DBC=on`

---

## References

- [Java uContract Configuration](https://gitlab.com/TeddyChen/ucontract/)
- [Environment.GetEnvironmentVariable Documentation](https://learn.microsoft.com/en-us/dotnet/api/system.environment.getenvironmentvariable)
- [12-Factor App: Config](https://12factor.net/config) (Environment variables as configuration)

---

## Revision History

| Date       | Status      | Notes                          |
|------------|-------------|--------------------------------|
| 2025-10-18 | Accepted    | Decision finalized             |
