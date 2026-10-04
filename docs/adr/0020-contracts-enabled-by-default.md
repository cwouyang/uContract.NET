# ADR-0020: Contracts Enabled by Default

## Status

**Accepted**

- **Date**: 2026-10-04
- **Deciders**: Project maintainer
- **Status Date**: 2026-10-04

---

## Context

### Problem Statement

[Issue #32](https://github.com/cwouyang/uContract.NET/issues/32): `ContractConfiguration` chose its default with `#if DEBUG`, which is resolved when the uContract assembly itself is compiled. The package is packed with `-c Release`, so the shipped assembly always answered "Release". Every package consumer got contracts **disabled** by default, however they built their own code. The README, the XML docs and ADR-0004 all promised that Debug builds enable all contracts by default; for a package consumer that was never true.

### Relevant Context

Evidence gathered on 2026-10-04, with no `DBC*` variable set unless stated:

| Check | Result |
|---|---|
| Published 1.0.0 package, `net10.0` consumer built in Debug | `PreconditionsEnabled=False`, source `default: Release`, `Contract.Require("x", () => false)` does not throw |
| `dotnet test` (Debug) at `origin/master` `0168f24` | 256 passed |
| `dotnet test -c Release`, same commit | 78 of 256 failed |
| `DBC=on dotnet test -c Release`, same commit | 256 passed |

- The 78 Release failures are the same defect seen from inside the repository. CI did not see them: `build-and-test.yml` and `publish.yml` both ran `dotnet test` in the default (Debug) configuration, while `publish.yml` packs with `-c Release`.
- The existing tests could not catch it. They chose their expected value with the same `#if DEBUG` as the library and reach the library through a project reference, so test and library always agreed with each other and never with what a package consumer receives.
- The Release-off default was deliberate. ADR-0004 recorded it as an intentional deviation from Java (quoted below), so this decision reverses a recorded one.

### Constraints

- ADR-0004's mechanism (environment variables), precedence (per-type flag, then `DBC`, then the default) and variable names stay as they are.
- No public API signature changes.

---

## Decision

**Contracts are enabled by default in every build configuration. The library no longer inspects its own build configuration, and the source label for a defaulted setting is `default`.**

### Details

With no `DBC*` variable set:

| | 1.0.0 (as shipped) | After this decision |
|---|---|---|
| Preconditions, postconditions, invariants, check | disabled | **enabled** |
| `DBC_DOC` | disabled | disabled (unchanged) |
| Source label when the default decided | `default: Debug` or `default: Release` | `default` |

The label appears in `ContractConfiguration.PreconditionsSource` and its three siblings, in the `ConfigurationLoaded` EventSource event, and in the `DBC_DOC` stderr summary, for example `[uContract]   Preconditions: on (from default)`.

Unchanged: the resolution order; the accepted values, with invalid values ignored; configuration read once and frozen; parameter validation always runs; conditions are not evaluated when their contract type is disabled.

This is a breaking change and ships as 2.0.0. `DBC=off` restores the 1.0.0 enforcement behaviour (no contract is evaluated); the reported source then becomes `DBC`.

### Answer to ADR-0004's production-safety rationale

ADR-0004, Negative Consequences:

> Different from Java default behavior: Java always defaults to `on`; .NET defaults to `off` in Release builds (intentional improvement for production safety)

That argument assumed the *consumer's* build configuration would choose the default. For a package it never does: the library's own configuration chooses, and it is always Release. The design therefore delivered "off everywhere". It protected production only by also switching contracts off, silently, in every consumer's development and test runs. A library whose purpose is to make violations loud should not be quiet until someone finds an environment variable. Consumers who want contracts off in production keep a one-line opt-out, `DBC=off`.

### Alignment with Java

Java uContract defaults to enabled: `Contract.java` sets `DBC = true` and turns it off only when the variable equals exactly `off`; the per-type flags are likewise read only for `off`. This was read from a local Java checkout (pom version 2.0.2, HEAD `7786f3f`); the port's stated baseline is 2.0.1 at `cb1e03f`.

Only the default is aligned. The .NET port stays more permissive than Java in three ways this decision does not change:

1. It accepts `false`/`0`/`no`/`off`, case-insensitively.
2. A per-type flag can re-enable a type that `DBC` disabled.
3. Invalid values are ignored.

### Package-level acceptance

The library was packed from the branch (`dotnet pack -c Release -p:Version=2.0.0-local.1`) into a local feed outside the repository and referenced from a fresh `net8.0` console project:

| Consumer build | `DBC` | `PreconditionsSource` | `Contract.Require("x", () => false)` |
|---|---|---|---|
| Debug | unset | `default` | throws `PreconditionViolationException` |
| Release | unset | `default` | throws `PreconditionViolationException` |
| Debug | `off` | `DBC` | does not throw |
| Release | `off` | `DBC` | does not throw |

---

## Consequences

### Positive Consequences

- ✅ A package consumer gets contract enforcement without configuring anything, in Debug and in Release.
- ✅ The default no longer depends on how the library was compiled. No `#if DEBUG` remains, so the Debug and Release builds of the library behave the same.
- ✅ The default matches Java uContract.
- ✅ The documentation and the shipped behaviour now agree.

### Negative Consequences

- ❌ **Breaking.** A consumer upgrading from 1.0.0 without setting `DBC*` starts evaluating contracts in production. Violations that were silent now throw.
- ❌ A consumer who enabled one type with only a per-type flag (for example only `DBC_PRE=on`) got that type alone under 1.0.0. The other three are now on as well; they must add `DBC=off`.
- ❌ `Old<T>()` deep-copies through JSON serialization and now runs by default. It can cost time on hot paths and throws `InvalidOperationException` for types that cannot be JSON-serialized. `EnsureAssignable<T>()` uses reflection and likewise now runs by default.
- ❌ **Trimming and Native AOT: not verified.** `Old<T>` is annotated `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]`, `EnsureAssignable<T>` is annotated `[RequiresUnreferencedCode]`, and the package declares `IsAotCompatible`. Those paths were off by default in 1.0.0 and are on now. Nobody has run them in a trimmed or Native AOT published application with contracts enabled. Such consumers should set `DBC_POST=off` until [issue #36](https://github.com/cwouyang/uContract.NET/issues/36) is resolved.
- ❌ Anything that matches on the strings `default: Debug` or `default: Release` breaks.

### Neutral Consequences

- ⚖️ A consumer who worked around issue #32 by setting `DBC=on` needs no change; the setting is redundant but harmless.
- ⚖️ ezDDD.NET pins `uContract` 1.0.0 and is unaffected until it upgrades ([ezDDD.NET #30](https://github.com/cwouyang/ezDDD.NET/issues/30)). Its five test projects passed with `DBC=on` against 1.0.0, which approximates the new default; they have not yet been run against a 2.0.0 package.

---

## Alternatives Considered

### Alternative 1: Follow the consumer's build configuration at runtime

**Description**: Detect the consumer's configuration when the library loads, for example from `DebuggableAttribute` on `Assembly.GetEntryAssembly()`.

**Pros**:
- Keeps the "Debug on, Release off" behaviour that ADR-0004 intended.

**Cons**:
- The entry assembly is not reliably the consumer's code: under a test host it can be the host, and it can be `null` under native hosts.

**Why rejected**: A default that depends on a heuristic is another way to be silently off. Both statements about the entry assembly were reasoned from documented behaviour; neither was run.

---

### Alternative 2: Keep the behaviour, correct the documentation

**Description**: Document the default as "disabled unless `DBC` is set".

**Pros**:
- No behavioural risk; not a breaking change.

**Cons**:
- Contracts stay silently off for every consumer who has not read the note.

**Why rejected**: It fixes the promise, not the problem.

---

### Deferred: Programmatic configuration switch

Issue #32 also proposed a way to configure contracts from code. It is not part of this decision, because it touches ADR-0004's choice of environment variables as the sole mechanism. Tracked in [issue #35](https://github.com/cwouyang/uContract.NET/issues/35).

---

## Related Decisions

- **Supersedes (default only)**: [ADR-0004 - Runtime Configuration via Environment Variables](0004-runtime-configuration-environment-variables.md) — the build-specific default is replaced. Its mechanism, precedence and variable names stand. ADR-0004 stays `Accepted` and carries an amendment.
- **Supersedes (default source label only)**: [ADR-0014 - Diagnostic Logging via EventSource](0014-diagnostic-logging-eventsource.md) — the labels `default: Debug` and `default: Release` become `default`. The event and the output format stand. ADR-0014 stays `Accepted` and carries an amendment.

---

## Implementation Notes

This ADR was written after the implementation so that it could record what the work turned up.

- **Red/green evidence.** With the default-dependent tests rewritten to expect "enabled" and before the production change, the Release run failed 93 of 256: the 78 above plus 15 newly failing cases. After the change, 256 of 256 passed in both configurations; after the label change and its new stderr test, 257 of 257 in both.
- **CI tested only Debug.** When this decision was made the workflows ran the tests only in the default (Debug) configuration; a Release test run is being added separately. That run tests the Release build through a project reference, not the packed `.nupkg`, so the package-level check above stays a manual step in the [release checklist](../RELEASE_CHECKLIST.md).
- **Assembly version.** The acceptance run reported assembly version `1.0.0.0` for a package versioned `2.0.0-local.1`: the csproj pins `<AssemblyVersion>` and `<FileVersion>` separately from `<Version>`. The release checklist and CONTRIBUTING now tell the releaser to update all three together.
- **Tests that could no longer fail.** With the default enabled, three tests asserted `true` for a setting whose default is also `true`, so they would have passed even if the flag under test were ignored. They now assert the disabling direction (`DBC_PRE=false`, `DBC_CHECK=false`, and `DBC=false` with an invalid `DBC_PRE`), which only the flag under test can produce.
- **Documentation errors found on the way.** Item 2 of the four `*Enabled` property remarks stated the precedence backwards and was corrected. ADR-0014's example stderr output shows `(default: Debug)` without the word "from" that the code emits; its amendment shows the line as emitted.
- **Test-suite ordering.** `Contract.Config` freezes at first use, and `ContractConfigurationTests` changed process-wide environment variables without clearing them. The suite passed only because the last test to run in that class happened to leave contracts enabled; renaming a test changed which test ran last, and 3 of 40 full-suite runs then failed with the same 78 failures. `ContractConfigurationTests` now clears the variables after every test, as `DiagnosticLoggingTests` already did. The suite still relies on `DisableTestParallelization`.
- **SDK pinning.** `dotnet pack` run from outside the repository directory does not see `global.json`, picks a newer SDK, and fails under `TreatWarningsAsErrors` on analyzer rule IDE0305. Packing from the repository root, as the release checklist does, works.

---

## References

- [Issue #32: Contracts are disabled by default for all NuGet consumers, including their Debug builds](https://github.com/cwouyang/uContract.NET/issues/32)
- [Issue #35: Add a programmatic switch for contract configuration](https://github.com/cwouyang/uContract.NET/issues/35)
- [Issue #36: Verify `Old<T>()` and `EnsureAssignable<T>()` under trimming and Native AOT with contracts enabled](https://github.com/cwouyang/uContract.NET/issues/36)
- [ezDDD.NET #30: Move to uContract 2.0.0 (contracts enabled by default)](https://github.com/cwouyang/ezDDD.NET/issues/30)
- [`ContractConfiguration.cs`](../../src/uContract/ContractConfiguration.cs)
- [CHANGELOG](../../CHANGELOG.md) — migration note for consumers
- [ADR-0004: Runtime Configuration via Environment Variables](0004-runtime-configuration-environment-variables.md)
- [ADR-0014: Diagnostic Logging via EventSource](0014-diagnostic-logging-eventsource.md)

---

## Revision History

| Date       | Status      | Notes                          |
|------------|-------------|--------------------------------|
| 2026-10-04 | Accepted    | Decision recorded after implementation. Supersedes the build-specific default of ADR-0004 and the default source labels of ADR-0014. |
