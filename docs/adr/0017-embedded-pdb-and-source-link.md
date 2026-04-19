# ADR-0017: Embedded PDB and Source Link

## Status

**Accepted**

- **Date**: 2026-04-19
- **Deciders**: Project maintainers
- **Status Date**: 2026-04-19

---

## Context

### Problem Statement

A .NET NuGet library must decide how to ship debugging symbols to package consumers so they can step into library code in their IDE debuggers. Two prevailing patterns exist: embedded PDBs bundled inside the assembly that ships with the main `.nupkg`, or a separate `.snupkg` published to the NuGet.org symbol server.

The project's `src/uContract/uContract.csproj` already sets `<DebugType>embedded</DebugType>`, `<EmbedUntrackedSources>true</EmbedUntrackedSources>`, and `<PublishRepositoryUrl>true</PublishRepositoryUrl>`; `Directory.Build.props` sets `<ContinuousIntegrationBuild>` conditionally on CI. But the `Microsoft.SourceLink.GitHub` package reference is not wired, so Source Link metadata (repo URL + commit SHA) is NOT currently injected into PDBs. This ADR resolves both questions at once: it confirms embedded PDB as the chosen symbol strategy, and adds Source Link to complete the debugging chain.

### Relevant Context

- The 2026-04-19 pre-publish review flagged "missing `.snupkg`" as a publishing blocker; investigation revealed the project had already chosen the alternative embedded-PDB route without documenting that choice.
- Without Source Link, embedded PDBs carry only build-machine file paths (e.g., `/home/runner/work/...` from CI), so consumer IDEs cannot fetch source code and step-into fails.
- `Microsoft.SourceLink.GitHub` is a build-time tool: with `PrivateAssets=all` + `IncludeAssets` restricted to build/analyzer concerns, no runtime dependency is propagated to consumers.
- ADR-0011 (Zero-Dependency Principle) constrains the project from adding runtime dependencies; build-only tooling is compatible with that principle.

### Constraints

- Must respect ADR-0011: zero external runtime dependencies propagated to consumers.
- Consumers must be able to debug uContract code in their IDE without additional configuration.
- Minimise dependence on external infrastructure whose outage would degrade the consumer debugging experience (e.g., NuGet.org symbol server).

---

## Decision

**Ship debugging symbols by embedding PDBs inside the assembly (already configured via `<DebugType>embedded</DebugType>`) and add `Microsoft.SourceLink.GitHub` as a build-only PackageReference so PDBs carry repository URL and commit SHA metadata. No `.snupkg` is produced; no separate symbol push is required.**

### Details

Add the following to `src/uContract/uContract.csproj` inside a new `<ItemGroup>`:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.SourceLink.GitHub" Version="8.0.0">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
  </PackageReference>
</ItemGroup>
```

The `PrivateAssets=all` + `IncludeAssets` pattern matches the existing `Meziantou.Analyzer` and `Roslynator.Analyzers` references in `Directory.Build.props`, ensuring consumers receive nothing at runtime.

The existing `<DebugType>embedded</DebugType>`, `<EmbedUntrackedSources>true</EmbedUntrackedSources>`, `<PublishRepositoryUrl>true</PublishRepositoryUrl>`, and conditional `<ContinuousIntegrationBuild>` remain unchanged — they are the MSBuild properties Source Link reads from when building the PDB.

No change to `.github/workflows/publish.yml` is required. `dotnet pack` continues to produce a single `.nupkg`; `dotnet nuget push` pushes that single artifact.

---

## Consequences

### Positive Consequences

- ✅ Consumers receive debugging symbols automatically when they install the `.nupkg` — no configuration, no symbol server lookup, no extra download step.
- ✅ Offline debugging works: the PDB is inside the DLL, always present.
- ✅ No dependence on NuGet.org symbol server uptime (which has had historical outages).
- ✅ Publish pipeline stays single-artifact (`dotnet pack` → push one `.nupkg`); fewer moving parts.
- ✅ Runtime dependency surface remains zero for consumers (Source Link is build-only).
- ✅ Extends ADR-0011's zero-dependency principle from runtime to infrastructure: consumers need no external services to debug.

### Negative Consequences

- ❌ Main `.nupkg` is modestly larger (~5-10 KB for uContract's <60 KB DLL; absolute increase is negligible but measurable).
- ❌ Symbols cannot be stripped independently of the main package — consumers always receive them. For an MIT-licensed library intended for inspection, this is not a real drawback.
- ❌ Diverges from the pattern adopted by many high-profile NuGet libraries (which ship `.snupkg`); tooling or documentation that assumes the `.snupkg` path may require adaptation when someone applies it to this project.

### Neutral Consequences

- ⚖️ Adds one build-time PackageReference (`Microsoft.SourceLink.GitHub`), tracked and versioned like the existing analyzer packages.
- ⚖️ Consumers running `dotnet list package` on their project see no new entries (the `PrivateAssets=all` pattern hides this from the consumer dependency graph).

---

## Alternatives Considered

### Alternative 1: Separate `.snupkg` published to NuGet.org symbol server

**Description**: Remove `<DebugType>embedded</DebugType>`, add `<IncludeSymbols>true</IncludeSymbols>` plus `<SymbolPackageFormat>snupkg</SymbolPackageFormat>`, and extend `publish.yml` to push the resulting `.snupkg` alongside the main `.nupkg`.

**Pros**:
- Smaller main `.nupkg`.
- Matches the most common NuGet library distribution pattern; aligns with tooling that expects this shape.
- Symbols are distributed via separate artifact, which is how some teams prefer to manage debug artefact lifecycles.

**Cons**:
- Adds a hard dependency on NuGet.org symbol server uptime; consumer debugging degrades when that service is slow or unavailable.
- Requires consumer IDEs to be configured to query the symbol server; default configurations may not be set up for it.
- Offline debugging may fail if the symbols were not pre-fetched.
- More moving parts in `publish.yml` (two artifacts, two pushes).

**Why rejected**: Contradicts the spirit of ADR-0011 — consumers should have what they need without relying on external infrastructure. For a small DBC library (<60 KB DLL), the size saving from separating symbols is negligible, and reliable always-available symbols matter more than that saving.

---

### Alternative 2: No symbols shipped (`<DebugType>none</DebugType>`)

**Description**: Do not ship debugging symbols at all; consumers can only debug their own code and step over uContract internals.

**Pros**:
- Smallest possible `.nupkg`.
- No build-time dependency on Source Link.

**Cons**:
- Hostile to consumers — when a contract violation surfaces in an unexpected way, consumers cannot inspect library state to understand what happened.
- Undermines the educational value of an MIT-licensed library intended to be read and learned from.
- Stack traces lose file and line information, making bug reports from consumers less actionable.

**Why rejected**: Against the spirit of an open-source library that welcomes inspection. The download-size saving is trivial compared to the debugging friction imposed on every consumer.

---

### Alternative 3: Embedded PDBs without Source Link (the pre-2026-04-19 state)

**Description**: Keep `<DebugType>embedded</DebugType>` and the other Source-Link-adjacent properties but do not reference `Microsoft.SourceLink.GitHub`.

**Pros**:
- Zero build-time dependencies added.
- Symbols do ship inside the DLL, so file and line numbers appear in stack traces.

**Cons**:
- PDBs carry only build-machine file paths (e.g., `/home/runner/work/...` on GitHub-hosted runners); consumer IDEs cannot fetch source because the URL-to-commit mapping is absent.
- Step-into from a consumer's debugger either fails outright or opens an empty file-not-found tab — the symbols are present but unusable for source navigation.
- Partial feature: embedded PDBs without Source Link are strictly inferior to embedded PDBs with Source Link for consumer debugging.

**Why rejected**: The build-time dependency cost is trivial (one analyzer-style PackageReference with `PrivateAssets=all`), and without Source Link the symbols are effectively decorative — they satisfy the property "has symbols" but fail the behaviour "consumers can debug the library".

---

## Related Decisions

- **Related to**: ADR-0011 (Zero-Dependency Principle) — this ADR extends ADR-0011 from the runtime layer to the infrastructure layer: consumers should not need external services (such as the NuGet.org symbol server) to debug. Source Link's `PrivateAssets=all` ensures the runtime surface remains unchanged.
- **Related to**: ADR-0006 (Serialization and Deep Copy Mechanism for Old<T>()) — both ADRs instance the pattern of preferring solutions that keep consumers self-sufficient rather than externalising a capability to another service.

---

## Implementation Notes

- Add the `Microsoft.SourceLink.GitHub` PackageReference to `src/uContract/uContract.csproj` as specified in the Decision > Details section above. Version `8.0.0` aligns with the modern .NET-style versioning adopted by the other analyzer packages in `Directory.Build.props`.
- No change is required in `.github/workflows/publish.yml`. The existing `dotnet pack` step already emits the embedded-PDB `.nupkg`; once Source Link is in the dependency graph, the PDB inside the DLL will carry repo URL + commit SHA metadata.
- Verification: after adding the package reference, run `dotnet pack -c Release` locally and confirm the build succeeds. Optionally, inspect the produced `.nupkg` with NuGet Package Explorer (or `unzip -p`) to confirm the embedded PDB now carries Source Link metadata; the `sourcelink test` CLI (`dotnet tool install -g sourcelink`) can validate the mapping end-to-end.
- This ADR is immutable once accepted. If a future decision reverses the symbol strategy (e.g., switching to `.snupkg`), a new ADR must be written that supersedes this one rather than editing this document.

---

## References

- [Source Link introduction — Microsoft Learn](https://learn.microsoft.com/dotnet/standard/library-guidance/sourcelink)
- [Microsoft.SourceLink.GitHub on NuGet](https://www.nuget.org/packages/Microsoft.SourceLink.GitHub)
- [Creating Symbol Packages (`.snupkg`) — NuGet Docs](https://learn.microsoft.com/nuget/create-packages/symbol-packages-snupkg)
- [`DebugType` MSBuild property reference](https://learn.microsoft.com/dotnet/core/project-sdk/msbuild-props#debugtype)
- [ADR-0011: Zero-Dependency Principle](0011-zero-dependency-principle.md)
- [ADR-0006: Serialization and Deep Copy Mechanism for Old<T>()](0006-serialization-deep-copy.md)

---

## Revision History

| Date       | Status      | Notes                          |
|------------|-------------|--------------------------------|
| 2026-04-19 | Accepted    | Decision recorded during pre-publish review; confirms the pre-existing embedded-PDB configuration as the deliberate symbol-shipping strategy and authorises adding `Microsoft.SourceLink.GitHub` to complete the debugging chain. |
