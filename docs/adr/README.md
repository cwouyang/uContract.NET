# Architecture Decision Records (ADR)

This directory contains Architecture Decision Records (ADRs) for the uContract.NET project.

---

## Table of Contents

1. [What is an ADR?](#what-is-an-adr)
2. [When to Write an ADR](#when-to-write-an-adr)
3. [ADR Format and Template](#adr-format-and-template)
4. [Maintenance Workflow](#maintenance-workflow)
5. [ADR Index](#adr-index)

---

## What is an ADR?

An **Architecture Decision Record (ADR)** is a document that captures an important architectural decision made along with its context and consequences.

**Key principles**:
- **Immutable**: Once accepted, ADRs are not modified (except Status)
- **Contextual**: Records WHY a decision was made, not just WHAT was decided
- **Traceable**: Indexed in this README for easy reference
- **Versioned**: Changes to decisions require new ADRs that supersede old ones

---

## When to Write an ADR

Create an ADR when making decisions about:

### Requires ADR
- **Framework and tooling choices** (e.g., .NET version, test framework)
- **Core API design** (e.g., static class vs instance, naming conventions)
- **Architectural patterns** (e.g., configuration mechanism, serialization approach)
- **Dependencies** (e.g., adding external packages, zero-dependency policy)
- **Breaking changes** (e.g., API redesign, major refactoring)
- **Performance trade-offs** (e.g., compile-time vs runtime checks)

### May Not Need ADR
- Minor bug fixes
- Documentation updates
- Code formatting changes
- Internal refactoring without API impact

---

## ADR Format and Template

### File Naming Convention

```
NNNN-short-title.md
```

- `NNNN`: 4-digit sequence number (e.g., 0001, 0002, 0003)
- `short-title`: Kebab-case descriptive title

### ADR Template

See [ADR.template.md](ADR.template.md) for the standard template.

---

## Maintenance Workflow

### After Decision Confirmation

1. Write ADR (use `ADR.template.md`, set Status to "Accepted")
2. Update this README's ADR Index

---

## ADR Index

### Accepted

**First Priority (Core Architecture)**:
- [ADR-0001: Target Framework - .NET 8](0001-target-framework.md) — 2025-10-18
- [ADR-0002: Project Naming and Structure](0002-project-naming-structure.md) — 2025-10-18
- [ADR-0003: API Design - Static Class Pattern](0003-api-design-static-class.md) — 2025-10-18
- [ADR-0004: Runtime Configuration via Environment Variables](0004-runtime-configuration-environment-variables.md) — 2025-10-18
- [ADR-0005: Generics, Type Constraints, and Nullable Reference Types](0005-generics-type-constraints-nullable.md) — 2025-10-18

**Second Priority (Implementation Details)**:
- [ADR-0006: Serialization and Deep Copy Mechanism for Old<T>()](0006-serialization-deep-copy.md) — 2025-10-18
- [ADR-0007: Reflection and Field Comparison for EnsureAssignable<T>()](0007-reflection-field-comparison.md) — 2025-10-18
- [ADR-0008: Exception Hierarchy Design](0008-exception-hierarchy.md) — 2025-10-18
- [ADR-0009: Thread Safety and Async/Await Support](0009-thread-safety-async-support.md) — 2025-10-18

**Third Priority (Publishing and Maintenance)**:
- [ADR-0010: Testing Framework - xUnit](0010-testing-framework-xunit.md) — 2025-10-18
- [ADR-0011: Zero-Dependency Principle](0011-zero-dependency-principle.md) — 2025-10-18

**Fourth Priority (Cross-Language Considerations)**:
- [ADR-0012: .NET Improvements Over Java Implementation](0012-dotnet-improvements-over-java.md) — 2025-10-19

**Fifth Priority (Design Constraints from Root Contracting Paper)**:
- [ADR-0013: No Subcontracting Support](0013-no-subcontracting-support.md) — 2025-10-29

**Sixth Priority (Diagnostics)**:
- [ADR-0014: Diagnostic Logging via EventSource](0014-diagnostic-logging-eventsource.md) — 2026-04-07

**Seventh Priority (Tooling and Quality)**:
- [ADR-0015: Code Quality Tooling](0015-code-quality-tooling.md) — 2026-04-18

---

## Notes for Maintainers

### Adding a New ADR

1. Copy `ADR.template.md` to `NNNN-your-title.md` (increment number)
2. Fill in all sections
3. Set initial Status to "Proposed" or "Accepted"
4. Update this README's [ADR Index](#adr-index)

### Best Practices

- Write ADRs **during** decision-making, not after implementation
- Keep ADRs **concise** but complete (1-2 pages max)
- Focus on **WHY**, not just WHAT
- Include **alternatives considered** to avoid future repetition
- Link to **related ADRs** to build decision graph
