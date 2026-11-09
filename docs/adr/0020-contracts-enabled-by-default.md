# ADR-0020: Contracts Enabled by Default

## Status

**Accepted**

- **Date**: 2026-10-04
- **Deciders**: Project maintainer
- **Status Date**: 2026-10-04
- **Amended**: 2026-10-04 — the "Trimming and Native AOT: not verified" consequence below is superseded by [ADR-0021](0021-postconditions-under-trimming-and-aot.md); see the Amendment under Implementation Notes
- **Amended**: 2026-10-04 — the "3 of 40 full-suite runs" figure is one of two measured series; see the second Amendment under Implementation Notes
- **Amended**: 2026-10-07 — `Old<T>()` no longer deep-copies through JSON serialization; see [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md) and the third Amendment under Implementation Notes
- **Amended**: 2026-11-08 — the SDK pin in `global.json` moved to 8.0.427 (the 8.0.4xx band); see the fourth Amendment under Implementation Notes
- **Amended**: 2026-11-09 — the plan for after .NET 8 end of support: a fallback on SDK 8.0.427 and a chosen, not yet done, move to SDK 10 with its prerequisites, gating 3.0.0; see the fifth Amendment under Implementation Notes

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

### Amendment (2026-10-04): Trimming and Native AOT consequence superseded by ADR-0021

Two sentences of the Negative Consequence that begins "**Trimming and Native AOT: not
verified.**" are superseded by [ADR-0021](0021-postconditions-under-trimming-and-aot.md), which
records what was measured for [issue #36](https://github.com/cwouyang/uContract.NET/issues/36)
and what was changed.

- Superseded: the sentence "Nobody has run them in a trimmed or Native AOT published application
  with contracts enabled". Both helpers were run in trimmed and in Native AOT published
  applications; ADR-0021 holds the results.
- Superseded: the advice "Such consumers should set `DBC_POST=off` until issue #36 is resolved".
  `Old<T>()` and `EnsureAssignable<T>()` have limited support there, described in ADR-0021 and in
  the README section "Trimming and Native AOT". `DBC_POST=off` remains one way out.
- The other sentences of that consequence stand, with one addition. The
  `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]` attributes and the `IsAotCompatible`
  declaration remain, and both helpers run by default. `EnsureAssignable<T>` gained
  `[DynamicallyAccessedMembers]` on its type parameter, and the text of its
  `[RequiresUnreferencedCode]` message changed.
- The decision of this ADR (contracts enabled by default) is unchanged.

### Amendment (2026-10-04): The test-suite ordering figure

The "Test-suite ordering" note above says "3 of 40 full-suite runs then failed". That figure
is one of two independent series measured before the fix:

| Series | Runs | Failing runs |
|---|---|---|
| First | 40 | 3 |
| Second | 80 | 5 |
| Together | 120 | 8 |

These figures come from the maintainer's measurement record of that change. They cannot be
reproduced from the repository. The note above says the failing runs of the first series showed
"the same 78 failures"; nothing is recorded about which tests failed in the second series. The
decision and the rest of the note are unchanged.

### Amendment (2026-10-07): `Old<T>()` no longer uses JSON

The Negative Consequence that begins "`Old<T>()` deep-copies through JSON serialization" no
longer holds as written. [ADR-0022](0022-faithful-old-copies-and-robust-comparison.md) replaces
the JSON copy with a reflection copy.

- `Old<T>()` still runs by default and can still cost time on hot paths: it copies everything
  reachable.
- It no longer throws `InvalidOperationException` for types that cannot be JSON-serialized.
- `[RequiresDynamicCode]` is removed from `Old<T>`; `[RequiresUnreferencedCode]` stays.
- The decision of this ADR (contracts enabled by default) is unchanged.

### Amendment (2026-11-08): The SDK pin moved to 8.0.427

The "SDK pinning" note above was written when `global.json` named `8.0.200`. For
[issue #80](https://github.com/cwouyang/uContract.NET/issues/80) it names `8.0.427`
(`latestPatch`): the 8.0.4xx band from its first patched SDK, whose ILCompiler and runtime packs
are 8.0.33.

- The 8.0.2xx band is out of service. Its ILCompiler and runtime packs (below 8.0.33) fall under
  advisory GHSA-p3jp-9v83-76w9, which fails the restore of the Native AOT smoke project under
  `TreatWarningsAsErrors` (NU1903).
- On SDK 8.0.427 the build raised one new diagnostic, CS9216 on the `System.Threading.Lock`
  stand-in in a test. It is suppressed at that line.
- .NET 8 reaches end of support on 2026-11-10, and 8.0.427 is its last SDK. What happens after
  that is tracked in [issue #82](https://github.com/cwouyang/uContract.NET/issues/82).
- The note about packing from outside the repository directory still applies. The decision of
  this ADR is unchanged.

### Amendment (2026-11-09): After .NET 8 end of support

For [issue #82](https://github.com/cwouyang/uContract.NET/issues/82). .NET 8 and .NET 9 reach end
of support on 2026-11-10; .NET 10 (LTS) on 2028-11-14. Source: Microsoft's
`releases-index.json`, fetched 2026-11-09.

**Measured** on 2026-11-09, on win-x64 only. CI runs the Native AOT smoke on linux-x64, which was
not measured.

- SDK 10.0.403 maps `net8.0` Native AOT to ILCompiler 8.0.33, the same pack as SDK 8.0.427. From
  this it is inferred that a newer SDK alone does not avoid an advisory on the 8.0 packs.
- The fallback works on SDK 8.0.427 (NuGet 6.11.2.1) with the implicit ILCompiler reference that
  `PublishAot` adds. The smoke project was temporarily given
  `<KnownILCompilerPack Update="Microsoft.DotNet.ILCompiler" ILCompilerPackVersion="8.0.4" />`, so
  the SDK itself referenced the vulnerable ILCompiler 8.0.4 (`autoReferenced` in
  `project.assets.json`). The restore failed (exit 1) with NU1903 for
  `https://github.com/advisories/GHSA-p3jp-9v83-76w9`, so the implicit reference is audited.
  With `<NuGetAuditSuppress Include="https://github.com/advisories/GHSA-p3jp-9v83-76w9" />`
  added in an `<ItemGroup>`, the restore succeeded (exit 0, no diagnostic), still resolved 8.0.4,
  and `project.assets.json` listed the advisory under `suppressedAdvisories`. An earlier run with
  an explicit `PackageReference` to 8.0.4 gave the same two results. Advisories on other packs of
  the smoke project, such as the runtime packs, were not measured.
- On SDK 10.0.403, with every project still `net8.0`, the build fails with 124 IDE code-style
  errors: 30 in `src` (IDE0018, IDE0046, IDE0059, IDE0301, IDE0305) and 94 in the tests. The
  cause is inferred, not confirmed: SDK 10 applies the `:warning` suffix of `.editorconfig`
  options in the build, and SDK 8 does not.
- Built for .NET 10, the stand-ins `ReadOnlySetStandIn.cs` and `LockStandIn.cs` clash with .NET 10
  types (CS0436).
- [Issue #84](https://github.com/cwouyang/uContract.NET/issues/84): a `net10.0` build of the
  tests fails 6 of 1491: `EnsureAssignable` throws `NotSupportedException` for inline arrays. A
  `net8.0` build of the tests, run on the newest installed runtime with
  `DOTNET_ROLL_FORWARD=LatestMajor`, fails the same 6; that run did not record the runtime
  version. It is inferred that the shipped `net8.0` package fails the same way on a newer
  runtime.
- [Issue #85](https://github.com/cwouyang/uContract.NET/issues/85): with the smoke built for
  `net10.0` (ILCompiler 10.0.14), check 89 fails: `EnsureImmutableCollection` reports a violation
  for a class that implements `IImmutableStack<int>`. This is the case that statement 5 of the
  `IL2070` justification in the 2026-10-12 amendment of
  [ADR-0021](0021-postconditions-under-trimming-and-aot.md) describes as possible but not
  measured; it is now observed under ILCompiler 10.0.14.
- A `net8.0` test assembly does not roll forward to a newer major runtime by default. Measured
  with a runtimeconfig that asks for an 8.0 runtime that is not installed (8.0.99): the host
  failed (exit 150) although 9.0 and 10.0 runtimes were installed. The test project's
  runtimeconfig sets no `rollForward`. So with SDK 10, CI needs an 8.0 runtime next to it to run
  the `net8.0` tests.
- `Microsoft.CodeAnalysis.PublicApiAnalyzers` 3.3.4 loads and reports on SDK 10 (Roslyn 5.9).

**Plans** (not yet done). The maintainer chose "加上 D（函式庫多目標）"
from "先 C，再遷移到 .NET 10 (Recommended)", "只做 C" and "加上 D（函式庫多目標）".

- Until the switch, `global.json` stays on SDK 8.0.427.
- Fallback, if a new advisory fails the restore: in its own PR, add to
  `tests/uContract.AotSmoke/uContract.AotSmoke.csproj` only an `<ItemGroup>` with one item per
  advisory. Copy the URL exactly as the NU1901–NU1904 message prints it, and name the advisory
  in a comment:

  ```xml
  <ItemGroup>
    <!-- GHSA-xxxx-xxxx-xxxx: suppressed until the move to SDK 10 (issue #82). -->
    <NuGetAuditSuppress Include="https://github.com/advisories/GHSA-xxxx-xxxx-xxxx" />
  </ItemGroup>
  ```

- Consequence of the fallback: the item only lets the restore pass. The toolchain stays
  unpatched, and the audit no longer reports that advisory for any package of the smoke project
  that the advisory names, not only ILCompiler: the item is keyed by the advisory URL (read from
  the NuGet documentation; only ILCompiler was measured). Remove the item when the move to SDK 10 and the `net10.0` smoke lands, or when a patched 8.0
  pack exists.
- Alternative not used: `WarningsNotAsErrors` for the NU190x code (option 2 of issue #82; the
  2026-11-08 amendment of ADR-0021 records a local publish made with
  `-p:WarningsNotAsErrors=NU1903`). Read from the MSBuild and NuGet documentation, not measured
  here: `WarningsNotAsErrors=NU1903` keeps the warning visible but stops the restore failing on
  every advisory of that severity code, for every package; `NuGetAuditSuppress` names one
  advisory and hides it. The procedure uses `NuGetAuditSuppress` because it is the narrower of
  the two.
- Direction: build with SDK 10; run the unit tests on `net8.0` and `net10.0`; run the Native AOT
  smoke on `net10.0`; multi-target the library to `net8.0` and `net10.0`, released with 3.0.0.
  [ADR-0001](0001-target-framework.md) says "No multi-targeting for the initial release
  (v1.0.0)"; once implemented, this direction changes that for 3.0.0.
- Prerequisites before the switch: #84, #85, the IDE severities (`.editorconfig` options with the
  `:warning` suffix), the stand-in clashes and the 8.0 runtime in CI. #82 stays open until the
  switch.
- Release gate: because the multi-targeted library is to be released with 3.0.0, #82 (the
  migration), #84 and #85 must be done before 3.0.0 ships. 3.0.0 does not ship without
  `net10.0`, and does not ship with #84 or #85 open.
- The decision of this ADR is unchanged.

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
| 2026-10-04 | Amended     | "Trimming and Native AOT: not verified" consequence superseded by ADR-0021. Decision unchanged. See Implementation Notes > Amendment. |
| 2026-10-04 | Amended     | "3 of 40 full-suite runs" is one of two measured series (3 of 40 and 5 of 80; 8 of 120 together). See Implementation Notes > second Amendment. |
| 2026-10-07 | Amended     | `Old<T>()` no longer deep-copies through JSON (ADR-0022): the JSON copy statement and its `InvalidOperationException` no longer apply. Decision unchanged. See Implementation Notes > third Amendment. |
| 2026-11-08 | Amended     | SDK pin moved from 8.0.200 to 8.0.427 (issue #80; advisory GHSA-p3jp-9v83-76w9); .NET 8 end of support tracked in #82. Decision unchanged. See Implementation Notes > fourth Amendment. |
| 2026-11-09 | Amended     | After .NET 8 end of support (issue #82): `NuGetAuditSuppress` fallback measured on SDK 8.0.427 with the implicit ILCompiler reference (`WarningsNotAsErrors` not used); chosen, not yet done, move to SDK 10 with a `net8.0`/`net10.0` library, after #84, #85, the IDE severities, the stand-in clashes and the CI runtime; #82, #84 and #85 gate 3.0.0. Decision unchanged. See Implementation Notes > fifth Amendment. |
