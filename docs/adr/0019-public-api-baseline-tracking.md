# ADR-0019: Public API Baseline Tracking

## Status

**Accepted**

- **Date**: 2026-04-19
- **Deciders**: Project maintainers
- **Status Date**: 2026-04-19

---

## Context

### Problem Statement

The project is about to publish version 1.0 of uContract.NET to NuGet, at which point the public API surface becomes a commitment: consumers will take binary dependencies, mechanically translate from the library's method names into their code, and reasonably expect that upgrading to 1.0.x or 1.y.z will not break them. Without automated tracking of the public API surface, four distinct failure modes remain uncaught until consumers report them: accidental visibility widening (a `public` modifier slipped onto an internal helper), silent signature drift (a refactor reorders parameters for aesthetic reasons), silent deletion (a method is removed because it seemed unused), and the structural gap of having no canonical record of what was committed at v1.0 to use as the reference point for later breaking-change decisions.

The 2026-04-19 pre-publish review (Angle 5) recommended establishing a `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` baseline before the first stable release. Adopting the tool post-1.0 requires reconstructing the baseline from git history, which is costly and lossy; adopting pre-1.0 lets the baseline be accurate by construction.

### Relevant Context

- The project already enforces build-time quality via Level Z (ADR-0015: CSharpier + Roslynator + 62 Level-Z rules with `TreatWarningsAsErrors=true`). Adding one more analyzer fits this pattern without introducing a new failure paradigm.
- The project is a solo-maintainer effort — there is no peer reviewer to catch surface-area drift manually.
- The API surface at this point is 21 public method signatures (4 condition-based CAE overloads from ADR-0018 plus 17 description-first methods) across one primary static class, one configuration class, and four exception types. Small enough that the initial baseline inventory is tractable to hand-verify.
- `Microsoft.CodeAnalysis.PublicApiAnalyzers` is maintained by the .NET team (part of `dotnet/roslyn-analyzers`) and is the de-facto standard for .NET library public-API tracking; Microsoft's own libraries use it.

### Constraints

- Must not add runtime dependencies propagated to consumers (ADR-0011: Zero-Dependency Principle).
- Must align with the existing analyzer-infrastructure pattern (ADR-0015): build-time enforcement, `PrivateAssets=all` + `IncludeAssets` restricted to build-relevant assets.
- Release workflow must remain documentable and practical for a solo maintainer.

---

## Decision

**Adopt `Microsoft.CodeAnalysis.PublicApiAnalyzers` as a build-time `PrivateAssets=all` package reference on the primary library project. Establish two tracking files — `PublicAPI.Shipped.txt` (empty at adoption) and `PublicAPI.Unshipped.txt` (populated with the current pre-1.0 API surface) — under `src/uContract/`. At each stable-release tag, move Unshipped entries to Shipped as part of the release ritual documented in CONTRIBUTING.md.**

### Details

Add the package reference in the existing "Build-time packages" `ItemGroup` in `src/uContract/uContract.csproj`, matching the `PrivateAssets=all` + `IncludeAssets="runtime; build; native; contentfiles; analyzers"` pattern used for `Meziantou.Analyzer`, `Roslynator.Analyzers`, and `Microsoft.SourceLink.GitHub`:

```xml
<PackageReference Include="Microsoft.CodeAnalysis.PublicApiAnalyzers" Version="X.Y.Z">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
</PackageReference>
```

Version `X.Y.Z` is resolved to the latest stable release at the time of the implementation commit. The ADR does not pin a specific version because the implementation commit is the authoritative record; future Dependabot bumps (ADR covered separately) handle forward motion.

Tracking files at `src/uContract/`:

- **`PublicAPI.Shipped.txt`** — committed APIs shipped in stable releases. Begins empty with only the `#nullable enable` header. At the v1.0 release tag, every Unshipped entry moves here.
- **`PublicAPI.Unshipped.txt`** — APIs added in the current pre-release development stream. Populated at adoption with the current public surface. During pre-1.0 this holds the entire surface (pre-release ≠ shipped). Starting at v1.0, new additions land here between releases and are promoted to Shipped at the next release tag.

The analyzer enforces these files at build time. Specifically:

- **RS0016** fires when a public member is missing from both files.
- **RS0017** fires when a `*REMOVED*` annotation is used without the member actually being removed from source.
- **RS0036** fires on nullable-annotation differences between declaration and baseline entry.
- Several other rules govern formatting of the `.txt` files.

The IDE quick-fix (Rider, Visual Studio) adds missing members to Unshipped.txt with one click. CLI alternative: `dotnet format analyzers --fix-all` applies all analyzer fixes project-wide.

Release ritual to be added to `CONTRIBUTING.md`:

1. Before tagging a release, confirm `PublicAPI.Unshipped.txt` reflects the intended surface for the release (review with `git diff` against the previous release tag).
2. Move all Unshipped entries to `PublicAPI.Shipped.txt`, preserving the `#nullable enable` header in both.
3. Commit the move with message `release: promote PublicAPI Unshipped → Shipped for vX.Y.Z`.
4. Tag the release and proceed with `publish.yml`.

---

## Consequences

### Positive Consequences

- ✅ Build fails on accidental public API additions — catches overlooked `public` modifiers at compile time rather than post-release in consumer bug reports.
- ✅ Build fails on public signature changes not acknowledged in `.txt` baselines — catches refactors that would ship as silent breaking changes.
- ✅ Removing a public member requires an explicit `*REMOVED*` annotation — breaking-change intent becomes loud and reviewable rather than quiet and forgotten.
- ✅ `.PublicApi.*.txt` files appear in git diff and PR review, making surface-area changes visible at a glance; reviewers (including future-self) can gate on them.
- ✅ `PublicAPI.Shipped.txt` at v1.0 becomes the canonical record of the API contract, answering "did this change ship in 1.0?" with a grep instead of a git-log forensic reconstruction.
- ✅ Extends ADR-0015's analyzer pattern without introducing a new failure paradigm; fits the established mental model.
- ✅ Complementary to Level Z: Level Z governs how code is written; this governs what is exposed. Orthogonal, non-overlapping concerns.

### Negative Consequences

- ❌ Every PR that changes public API requires an accompanying `PublicAPI.Unshipped.txt` update. IDE quick-fix mitigates most of this; CI will fail on PRs that forget.
- ❌ The release ritual gains one manual step (Unshipped → Shipped). Forgettable, though CI will catch it at the next push if Unshipped still contains entries that were implicitly released.
- ❌ Two additional tracking files to maintain at project root. The baseline inventory is ~30-50 lines; growth rate mirrors API surface growth.
- ❌ Solo-maintainer diff-visibility benefit is partial — there is no peer reviewer. The value lands on future-self reviewing past commits.

### Neutral Consequences

- ⚖️ Adds one build-time package reference; runtime surface unchanged (consumers see nothing via `PrivateAssets=all`).
- ⚖️ `PublicAPI.Shipped.txt` intentionally starts empty — semantically accurate (no stable release has shipped) rather than stylistically awkward.
- ⚖️ `CONTRIBUTING.md` gains a release-ritual section documenting Unshipped → Shipped movement.

---

## Alternatives Considered

### Alternative 1: `Verify.PublicApi` (xUnit snapshot-testing approach)

**Description**: Use the `Verify` framework plus `Verify.PublicApi` to dump the public API as a string and snapshot-compare against a `.verified.txt` file. Differences surface as xUnit test failures.

**Pros**:
- Test-based workflow is familiar.
- Diff on failure is detailed and often readable.
- Does not require adding analyzer-level configuration files (`.PublicApi.*.txt`) to project root.

**Cons**:
- Failure signal surfaces at test run time, not build time — one indirection later in the feedback loop than PublicApiAnalyzers.
- Introduces a test-framework dependency (Verify) with its own conventions and update cycle.
- Requires test project to know about production API structure, slightly coupling layers that are otherwise separate.
- Less common in .NET library ecosystems — potential contributors would need to learn an additional tool.

**Why rejected**: The existing analyzer infrastructure (Meziantou, Roslynator, Level Z, Source Link) is uniformly build-time. Adding one more analyzer in the same paradigm has zero marginal cognitive cost; adding a test-based tool introduces a second paradigm for the same class of problem. Build-time feedback is also strictly earlier than test-time feedback.

---

### Alternative 2: Rely on CHANGELOG.md discipline and human PR review

**Description**: Do not install any automated API surface tracker. Maintain CHANGELOG.md (already present, Keep a Changelog format) as the record of API changes; enforce via manual review on PRs.

**Pros**:
- Zero setup; zero per-PR friction from tooling.
- Uses existing CHANGELOG infrastructure.
- No new tracking files.

**Cons**:
- Relies entirely on human vigilance; no build-time enforcement.
- Solo maintainer has no peer reviewer — self-review is prone to blind spots.
- No machine-readable record of the v1.0 API commitment; future breaking-change decisions must grep source and git log, not consult a canonical file.
- Accidental API widening (`public` where `internal` was meant) is essentially undetectable without tooling.

**Why rejected**: ADR-0015 established the project's preference for automated guardrails over discipline when both are available. The same logic applies here: willpower is not a sustainable enforcement strategy, and the tooling cost is low.

---

### Alternative 3: Adopt post-1.0 when a breaking change is first contemplated

**Description**: Skip public API tracking for v1.0. Revisit when someone proposes a breaking change and formal tracking is explicitly needed.

**Pros**:
- Defers the decision; zero pre-publish effort.
- Avoids the perception of "over-engineering for a small library".

**Cons**:
- Baseline reconstruction from git history at that later point is costly and error-prone — every ambiguous commit ("was this a public API change?") becomes a judgment call.
- No canonical v1.0 API commitment artifact exists; the implicit baseline is whatever the v1.0 tag happens to produce.
- Signals less rigor to library consumers who inspect the repo before adopting.
- Misses the unique pre-1.0 window where baseline establishment is free of reconstruction cost.

**Why rejected**: Pre-1.0 is the only timing window where adoption cost is minimal and the resulting baseline is accurate by construction. Adopting later is strictly more expensive and less trustworthy.

---

## Related Decisions

- **Extends**: ADR-0015 (Code Quality Tooling) — this ADR adds one more build-time analyzer (`Microsoft.CodeAnalysis.PublicApiAnalyzers`) to the set already established in ADR-0015, using the same `PrivateAssets=all` + `IncludeAssets` pattern as `Meziantou.Analyzer`, `Roslynator.Analyzers`, and `Microsoft.SourceLink.GitHub` (ADR-0017). The decision is explicit about being an extension rather than a peer relationship: it does not replace any existing decision; it layers new surface-area enforcement on top of the existing code-quality enforcement.
- **Related to**: ADR-0011 (Zero-Dependency Principle) — compatible. `PrivateAssets=all` prevents the analyzer from propagating to consumers; runtime dependency graph is unchanged.
- **Related to**: ADR-0012 (.NET Improvements Over Java Implementation) — the parameter-validation, unified-exception-message, async-support, configuration-encapsulation, nullable, `Old<T>` single-overload, and CAE improvements documented in ADR-0012 are all part of the initial Unshipped.txt surface.
- **Related to**: ADR-0018 (Optional Description via CAE) — the four CAE overloads (`Require`, `Ensure`, `Invariant`, `Check`) added in ADR-0018 are part of the initial API inventory that will be recorded in Unshipped.txt at adoption.

---

## Implementation Notes

- Add the PackageReference to `src/uContract/uContract.csproj` in the existing `<ItemGroup>` labelled "Build-time packages (not propagated to consumers)". Version resolved to latest stable at the implementation commit.
- Create `src/uContract/PublicAPI.Shipped.txt` containing only the single line `#nullable enable`. This is semantically accurate: no stable release has shipped.
- Create `src/uContract/PublicAPI.Unshipped.txt` initially with `#nullable enable` only; run `dotnet format analyzers --fix-all` (or accept the IDE quick-fix) to populate it with the current 21-method public surface plus supporting types.
- Verify the build produces zero analyzer warnings after the Unshipped population step.
- Add a "Releasing a new version" section to `CONTRIBUTING.md` documenting the Unshipped → Shipped promotion ritual. The section lives near the existing CSharpier / Roslynator upgrade sections.
- This ADR is immutable once accepted. Future adjustments to the API-tracking workflow (e.g., switching to a different tool, changing the release ritual, adding a second library project to the baseline scope) require a new ADR.

---

## References

- [`Microsoft.CodeAnalysis.PublicApiAnalyzers` on NuGet](https://www.nuget.org/packages/Microsoft.CodeAnalysis.PublicApiAnalyzers)
- [PublicApiAnalyzers rules documentation (`dotnet/roslyn-analyzers`)](https://github.com/dotnet/roslyn-analyzers/blob/main/src/PublicApiAnalyzers/PublicApiAnalyzers.Help.md)
- [`Verify.PublicApi` — rejected alternative](https://github.com/VerifyTests/Verify.PublicApi)
- [ADR-0011: Zero-Dependency Principle](0011-zero-dependency-principle.md)
- [ADR-0015: Code Quality Tooling](0015-code-quality-tooling.md)
- [ADR-0017: Embedded PDB and Source Link](0017-embedded-pdb-and-source-link.md)

---

## Revision History

| Date       | Status      | Notes                          |
|------------|-------------|--------------------------------|
| 2026-04-19 | Accepted    | Decision recorded during pre-publish review. Extends ADR-0015 with a sixth build-time analyzer. `PublicAPI.Shipped.txt` begins empty (no stable release shipped); `PublicAPI.Unshipped.txt` to be populated at the implementation commit. |
