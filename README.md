# uContract.NET

> **Design by Contract library for .NET 8+**
>
> Based on [Java uContract 2.0.1](https://gitlab.com/TeddyChen/ucontract/)

A modern Design by Contract (DBC) library for .NET. This is a direct port of the **Java uContract 2.0.1** library with .NET-specific improvements.

[![.NET](https://img.shields.io/badge/.NET-8.0+-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![Status](https://img.shields.io/badge/status-alpha-orange.svg)](#status)

---

## Status

**Current Version**: `1.0.0-alpha.1`

- ✅ **16 public methods** — Core API, advanced features, convenience methods
- ✅ **216 tests passing (100%)**
- ✅ **Zero external dependencies**

---

## Table of Contents

- [Quick Start](#quick-start)
- [Features](#features)
- [API Reference](#api-reference) - Quick overview ([Complete API Docs →](docs/examples/API_REFERENCE.md))
- [Configuration](#configuration) - Environment variables ([Detailed Config →](docs/examples/USAGE_EXAMPLES.md#configuration-examples))
- [Examples](#examples) - Minimal example ([15+ More Examples →](docs/examples/USAGE_EXAMPLES.md))
- [Requirements](#requirements)
- [Differences from Java Version](#differences-from-java-version)
- [Documentation](#documentation)
- [License](#license)

---

## Quick Start

### Installation

```bash
dotnet add package uContract
```

### Basic Usage

```csharp
using uContract;

public class BankAccount
{
    private decimal _balance;
    private string _owner;

    public BankAccount(string owner, decimal initialBalance)
    {
        // Preconditions: Validate inputs
        Contract.RequireNotNull("Owner", owner);
        Contract.Require("Initial balance non-negative", () => initialBalance >= 0);

        _owner = owner;
        _balance = initialBalance;
    }

    public void Transfer(decimal amount)
    {
        // Precondition: Valid amount
        Contract.Require("Amount positive", () => amount > 0);

        // Capture old state for postcondition
        var oldBalance = Contract.Old(() => _balance);

        // Business logic
        _balance -= amount;

        // Postcondition: Balance decreased correctly
        Contract.Ensure("Balance decreased", () => _balance == oldBalance - amount);
    }

    private void CheckInvariant()
    {
        // Invariant: Balance never negative
        Contract.Invariant("Balance non-negative", () => _balance >= 0);
    }
}
```

---

## Features

### Core Capabilities

- ✅ **Preconditions**: Validate method inputs and caller state
- ✅ **Postconditions**: Verify method results and state changes
- ✅ **Invariants**: Enforce class-level constraints
- ✅ **Check Assertions**: Runtime checks for general conditions
- ✅ **State Capture**: `Old<T>()` for comparing before/after states
- ✅ **Field Comparison**: `EnsureAssignable<T>()` with reflection-based validation

### Design Philosophy

- 🚀 **Zero overhead when disabled**: Lazy evaluation via `Func<bool>` delegates
- ⚙️ **Runtime configuration**: Control contracts via environment variables
- 🎯 **DDD-focused**: Designed for domain models and aggregate roots
- 📦 **Zero external dependencies**: Uses only .NET built-in APIs
- 🔒 **Thread-safe**: AsyncLocal support for async/await
- 💎 **Strongly typed**: Generic constraints ensure type safety

### Why Root Contracting?

Traditional Design by Contract tools face a sustainability challenge - most extensions are **abandoned or dormant** because they require custom compilers/preprocessors that must constantly keep up with language evolution.

**Root contracting** solves this with a **simple utility library** approach:

- ✅ **No custom tooling** - Uses standard C# features only (no IL weaving, no code generation)
- ✅ **Language-independent design** - Same approach can be ported to Java, Go, Python, Rust
- ✅ **Zero dependencies** - Only .NET built-in APIs (`System.Text.Json`, `System.Reflection`)

**Learn more**:
- 📖 [Research paper](https://www.mdpi.com/2079-9292/14/21/4205) - Formal foundation, experiments, and comparisons with other DbC tools
- 📋 [Architecture Decision Records](docs/adr/) - Design rationale and trade-offs for each decision
- 📚 [DDD Integration Guide](docs/DDD_INTEGRATION_GUIDE.md) - How to apply contracts with DDD patterns

---

## API Reference

> 📖 **Complete Documentation**: [API_REFERENCE.md](docs/examples/API_REFERENCE.md) — configuration, exceptions, best practices, and performance

uContract.NET provides **16 public methods** organized into 4 categories:

| Category | Methods | Purpose |
|----------|---------|---------|
| **Preconditions** (3) | `Require`, `RequireNotNull`, `RequireNotEmpty` | Validate inputs at method entry |
| **Postconditions** (5) | `Ensure`, `EnsureNotNull`, `EnsureResult`, `EnsureImmutableCollection`, `EnsureAssignable` | Verify results and state changes |
| **Invariants** (2) | `Invariant`, `InvariantNotNull` | Enforce class-level constraints |
| **Helpers** (6) | `Check`, `Ignore`, `Old`, `Imply`, `IfAndOnlyIf`, `CheckUnsupportedOperation` | Runtime assertions and utilities |

---

## Configuration

> ⚙️ **Detailed Configuration**: [USAGE_EXAMPLES.md - Configuration Examples](docs/examples/USAGE_EXAMPLES.md#configuration-examples)

Control contract evaluation at runtime via environment variables.

### Environment Variables

| Variable      | Values      | Default (Debug) | Default (Release) | Purpose                          |
|---------------|-------------|-----------------|-------------------|----------------------------------|
| `DBC`         | `on`/`off`  | `on`            | `off`             | Master switch for all contracts  |
| `DBC_PRE`     | `on`/`off`  | `on`            | `off`             | Preconditions only               |
| `DBC_POST`    | `on`/`off`  | `on`            | `off`             | Postconditions only              |
| `DBC_INV`     | `on`/`off`  | `on`            | `off`             | Invariants only                  |
| `DBC_CHECK`   | `on`/`off`  | `on`            | `off`             | Check statements only            |
| `DBC_DOC`     | `on`/`off`  | `off`           | `off`             | Diagnostic output                |

### Quick Setup

**Windows (PowerShell):**
```powershell
$env:DBC="on"
```

**Linux/macOS:**
```bash
export DBC=on
```

### Key Behaviors

- **Debug builds**: All contracts **enabled** by default
- **Release builds**: All contracts **disabled** by default
- **Parameter validation**: Always executed (even when DBC is disabled)
- **Zero overhead**: Conditions are never evaluated when disabled (lazy evaluation)

---

## Examples

> 📚 **More Examples**: [USAGE_EXAMPLES.md](docs/examples/USAGE_EXAMPLES.md) (15+ real-world scenarios)

- **DDD Quick Start**: Minimal aggregate root example → [USAGE_EXAMPLES.md](docs/examples/USAGE_EXAMPLES.md#domain-driven-design-patterns)
- **State Capture**: Using `Old<T>()` for complex objects → [USAGE_EXAMPLES.md](docs/examples/USAGE_EXAMPLES.md#state-capture-and-comparison)
- **Field Validation**: Using `EnsureAssignable<T>()` → [USAGE_EXAMPLES.md](docs/examples/USAGE_EXAMPLES.md#field-assignment-validation)
- **Real-World Scenarios**: Banking, e-commerce, inventory systems → [USAGE_EXAMPLES.md](docs/examples/USAGE_EXAMPLES.md#real-world-scenarios)
- **Complete DDD Guide**: Value objects, domain services (10+ patterns) → [DDD_INTEGRATION_GUIDE.md](docs/DDD_INTEGRATION_GUIDE.md)

---

## Requirements

- **.NET 8.0 or later**
- **C# 12** (nullable reference types enabled)
- **No external dependencies** (uses only .NET built-in APIs)

### Supported Types

- ✅ **All reference types** (`class`, `record`)
- ✅ **All value types** (`struct`, `record struct`)
- ✅ **Generic types** with appropriate constraints
- ✅ **Collections** (IEnumerable, List, Array, ImmutableList, etc.)

---

## Differences from Java Version

uContract.NET maintains **semantic parity** with the Java version while leveraging modern .NET features.

### Syntax Differences

| Feature | Java | C# |
|---------|------|-----|
| Method naming | `require()`, `old()`, `imply()` | `Require()`, `Old()`, `Imply()` |
| Lambda syntax | `() -> condition` | `() => condition` |
| Functional types | `BooleanSupplier`, `Supplier<T>` | `Func<bool>`, `Func<T>` |
| Null checks | `@NonNull` annotation | `where T : class` constraint |

### .NET Improvements

| Feature | Java | C# |
|---------|------|-----|
| **Parameter validation** | ❌ Not validated | ✅ Always validated (even when DBC off) |
| **Exception messages** | ⚠️ Inconsistent format | ✅ Unified format |
| **Async support** | ❌ ThreadLocal (no async) | ✅ AsyncLocal (async/await safe) |
| **Configuration** | ⚠️ Public static fields | ✅ Encapsulated ContractConfiguration class |
| **Nullability** | ⚠️ Annotations only | ✅ Compiler-enforced nullable reference types |

### Example Comparison

**Java:**
```java
// Java
require("x positive", () -> x > 0);
var oldBalance = old(() -> balance);
ensure("balance decreased", () -> balance < oldBalance);
```

**C#:**
```csharp
// C#
Contract.Require("x positive", () => x > 0);
var oldBalance = Contract.Old(() => balance);
Contract.Ensure("balance decreased", () => balance < oldBalance);
```

---

## Documentation

### User Documentation

- 📖 **[API_REFERENCE.md](docs/examples/API_REFERENCE.md)** - API reference
  - Configuration, exception hierarchy, best practices, performance
  - Per-method signatures and XML docs available via IDE IntelliSense

- 📚 **[USAGE_EXAMPLES.md](docs/examples/USAGE_EXAMPLES.md)** - Real-world examples
  - 15+ practical scenarios (banking, e-commerce, inventory, user management)
  - Quick DDD examples with links to complete guide
  - Configuration examples and testing patterns

- 🏗️ **[DDD_INTEGRATION_GUIDE.md](docs/DDD_INTEGRATION_GUIDE.md)** - DDD integration guide
  - 10+ DDD patterns with best practices
  - Specifications, repositories

### Developer Documentation

- **[CLAUDE.md](CLAUDE.md)** - Development guidelines and session context
- **[docs/adr/](docs/adr/)** - Architecture Decision Records

---

## Contributing

Contributions are welcome!

Before contributing:
1. Read the [Architecture Decision Records](docs/adr/)

---

## License

**MIT License** - Same as the [Java uContract library](https://gitlab.com/TeddyChen/ucontract/)

Copyright (c) 2025 uContract.NET Contributors

See [LICENSE](LICENSE) file for details.

---

## References

### Original Java Version (2.0.1)

**This .NET port is based on Java uContract 2.0.1** (GitLab commit: `cb1e03f`)

- **Repository**: [Java uContract (GitLab)](https://gitlab.com/TeddyChen/ucontract/)
- **Documentation**: [Javadoc](https://www.javadoc.io/doc/tw.teddysoft.ucontract/uContract)

### Design by Contract Theory
- [Bertrand Meyer - Design by Contract](https://se.ethz.ch/~meyer/publications/computer/contract.pdf)
- [Wikipedia: Design by Contract](https://en.wikipedia.org/wiki/Design_by_contract)
- [Eiffel: The Language](https://www.eiffel.com/values/design-by-contract/introduction/)

---

