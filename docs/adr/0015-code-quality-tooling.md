# ADR-0015: Code Quality Tooling

## Status

**Accepted**

- **Date**: 2026-04-18
- **Deciders**: Project maintainer
- **Status Date**: 2026-04-18

---

## Context

### Problem Statement

uContract.NET targets NuGet publication and already enables strict build behavior
(`TreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true`,
`AnalysisLevel=latest-recommended`, Meziantou.Analyzer 3.0.44). It also ships a
378-line `.editorconfig`. Three gaps made style and quality enforcement weaker
than what a published library should offer downstream consumers reading the
source via source link:

1. Most `.editorconfig` naming and coding-convention rules used
   `severity = suggestion`, so they surfaced as IDE hints only and were not
   enforced at build time.
2. There was no CI guard against formatting drift; pull requests could merge
   whitespace and wrapping inconsistencies undetected.
3. The `.editorconfig` formatting section appeared to be the unmodified output
   of a project template, not a curated artifact — no sunk cost attached to it,
   which made it a good candidate for replacement by an opinionated formatter.

### Relevant Context

- The repo has a single maintainer; CI runs on every push to every branch.
- ADR-0011 forbids runtime NuGet dependencies. Any tooling must use
  `PrivateAssets=all` or live outside the package graph entirely.
- Rider is the primary IDE; JetBrains ships a maintained CSharpier plugin.
- 226 xUnit tests form the behavior-preservation safety net for any reformat or
  refactor.

### Constraints

- Must preserve ADR-0011 (zero runtime dependencies).
- Must not regress the 226 existing tests at any commit.
- Must not modify public API signatures (breaking changes are out of scope for
  this rollout).
- Must work on both Windows (CRLF) and Linux CI (LF) without tooling divergence.

---

## Decision

**We adopt CSharpier as the canonical formatter, Roslynator.Analyzers as a
complement to Meziantou, and promote every `.editorconfig` `:suggestion`
severity to `:warning` so that style and quality violations become
build-blocking.**

### Details

**CSharpier 1.2.6** as the canonical formatter, installed via the
`.config/dotnet-tools.json` manifest (not a runtime `PackageReference`),
configured with `printWidth: 120`, `useTabs: false`, `endOfLine: lf`. CSharpier
owns whitespace, line breaks, and wrapping; the `.editorconfig` `C# Formatting
Rules` section was removed to prevent conflicts.

**Roslynator.Analyzers 4.15.0** added to `Directory.Build.props` with
`PrivateAssets=all` and `IncludeAssets="runtime; build; native; contentfiles; analyzers"`
to complement Meziantou without entering the runtime dependency graph.

**Level Z severity promotion**: every `.editorconfig` rule previously at
`:suggestion` is now `:warning`. Combined with `TreatWarningsAsErrors=true`,
this makes 43 coding-convention rules and 19 naming rules build-blocking. Rules
at `:silent` remain unchanged — they are subjective preferences.

**CI formatting gate**: `.github/workflows/build-and-test.yml` gained a
`dotnet csharpier check .` step before build and test, fast-failing PRs that
drift from the CSharpier baseline. The workflow also runs on every branch so
that feature work gets the same gate as `master`.

---

## Consequences

### Positive Consequences

- ✅ Style and naming violations are build-blocking before PR merge.
- ✅ NuGet consumers see a uniformly-formatted library via source link.
- ✅ Level Z surfaces modern C# idioms (primary constructors, collection
  expressions, pattern matching); enforcement doubles as a learning mechanism.
- ✅ CSharpier eliminates formatting bikeshedding — the formatter is
  opinionated and non-configurable beyond `printWidth` and line endings.
- ✅ Zero new runtime dependencies: CSharpier is a dotnet tool, analyzers use
  `PrivateAssets=all`. ADR-0011 preserved.

### Negative Consequences

- ❌ One-off reformat of the entire codebase pollutes `git blame` on the
  baseline commit. Mitigated by `.git-blame-ignore-revs`.
- ❌ Future SDK upgrades may surface new `IDExxxx` rules that break the build
  mid-sprint. Treated as dedicated follow-up PRs, not fixed in-line.
- ❌ Every tool upgrade now requires two commits (bump + reformat) instead of
  one — small review overhead for large clarity gain.

### Neutral Consequences

- ⚖️ Rider users must install the CSharpier plugin and pin its version to the
  CLI version in `.config/dotnet-tools.json`.
- ⚖️ The project gains an additional dotnet tool to restore in CI
  (`dotnet tool restore`), a one-line step.

---

## Alternatives Considered

### Alternative 1: SonarAnalyzer.CSharp

**Description**: Adopt SonarAnalyzer.CSharp alongside or instead of Roslynator.

**Pros**:
- Broad security and reliability rule coverage.
- Backed by SonarSource; well-maintained.

**Cons**:
- Most rules target network, database, and UI code; uContract is pure-logic
  Design-by-Contract with none of those surfaces.
- Meziantou + Roslynator already cover the code-smell angle for this domain.

**Why rejected**: Rule catalogue does not match the project's surface area;
noise outweighs signal.

---

### Alternative 2: Husky.Net pre-commit hook

**Description**: Add a Git pre-commit hook via Husky.Net that runs
`dotnet csharpier check` locally before every commit.

**Pros**:
- Catches violations before they reach CI.
- Shorter feedback loop for contributors.

**Cons**:
- Adds onboarding friction: every clone must run `dotnet husky install`.
- The CI gate already catches violations at PR time.
- Single-maintainer project; friction cost exceeds the value today.

**Why rejected**: Premature for the current contributor count. Re-evaluate when
the project gains external contributors.

---

### Alternative 3: StyleCop.Analyzers

**Description**: Add StyleCop.Analyzers for additional style enforcement.

**Pros**:
- Mature, widely-used analyzer package.
- Covers documentation and naming conventions.

**Cons**:
- Heavy rule overlap with the existing `.editorconfig` and Meziantou.
- Would require extensive per-rule suppression to match project intent.

**Why rejected**: Overlap forces a large suppression matrix; Meziantou +
Roslynator + built-in `IDExxxx` cover the same ground with less noise.

---

### Alternative 4: ReSharper Command Line Tools

**Description**: Run JetBrains' ReSharper CLT in CI for inspections and cleanup.

**Pros**:
- Same engine as Rider inspections; familiar to the maintainer.

**Cons**:
- Duplicates the Roslyn analyzer pathway.
- Slower in CI than in-build analyzers.
- Not mainstream in modern OSS .NET tooling.

**Why rejected**: Rider users already get equivalent inspections through the
Roslyn integration at edit time; no need to duplicate in CI.

---

### Alternative 5: Plain `dotnet format` without CSharpier

**Description**: Keep the existing `.editorconfig` formatting section and rely
on `dotnet format` (built into the SDK) for whitespace enforcement.

**Pros**:
- No new tool to install.
- One fewer version to track.

**Cons**:
- `dotnet format` is configurable, so the team still has to curate formatting
  rules line by line in `.editorconfig`. Bikeshedding is not eliminated.
- CSharpier is opinionated and non-negotiable on layout — once adopted, there
  is nothing left to argue about.
- The existing `.editorconfig` formatting section was template-generated, so
  discarding it carried no sunk cost.

**Why rejected**: The win from CSharpier is the *absence* of knobs; replacing
hand-curated layout rules with an opinionated formatter eliminates an entire
category of review discussion.

---

### Alternative 6: `dotnet format --verify-no-changes` in CI alongside CSharpier

**Description**: Run both `dotnet csharpier check .` and
`dotnet format --verify-no-changes` in CI.

**Pros**:
- Double coverage for style violations.

**Cons**:
- `EnforceCodeStyleInBuild=true` + `TreatWarningsAsErrors=true` already fail the
  build on the same style violations that `dotnet format --verify-no-changes`
  would catch.
- A second check step slows CI without catching anything new.

**Why rejected**: Redundant. The build itself already enforces style; CSharpier
check covers the whitespace and wrapping that the build cannot see.

---

## Related Decisions

- **Related to**: ADR-0011 (Zero-Dependency Principle) — Roslynator.Analyzers
  uses `PrivateAssets=all`, and CSharpier is installed via dotnet tool manifest
  (not `PackageReference`). Neither contributes to the runtime NuGet package
  dependency graph. Verified by the post-`dotnet pack` check that the emitted
  `.nuspec` contains an empty `<dependencies />`.
- **Superseded by**: none

---

## Implementation Notes

### Maintenance Principles

- **Tool version changes split into two commits**: a `chore(build):` bump
  commit that changes the version number in `.config/dotnet-tools.json` or
  `Directory.Build.props`, and a follow-up `style:` or `refactor:` commit that
  applies the new version's output. This keeps PRs reviewable by separating the
  configuration change from its code-wide effect.
- **Severity changes** (promoting additional rules in the future) follow the
  same split: one `style(editorconfig):` commit for the severity change, then
  one `refactor:` commit per `IDExxxx` rule for the fix-ups. Preserving the
  per-rule granularity keeps the history useful for bisection and for
  retrospectives.
- **Do not squash fix-up commits.** They document which idiom was learned when.

### Cross-Platform Line Endings

CSharpier is configured with `endOfLine: lf`, and `.gitattributes` declares
`*.cs text eol=lf` to match. The baseline reformat commit (`7bdf572`) and the
line-ending switch (`d1e60b6`) converted the repository to LF so that Linux CI
and Windows development produce byte-identical output. A future maintainer who
sees these two settings should understand they move together — changing one
without the other breaks the CI formatting gate.

### Verification

- `dotnet csharpier check .` and `dotnet build -p:ContinuousIntegrationBuild=true`
  run in the `build-and-test.yml` GitHub Actions workflow on every branch.
- `dotnet pack -c Release` must continue to emit a `.nupkg` whose embedded
  `.nuspec` shows `<dependencies />` empty (ADR-0011 invariant).
- The 226 existing tests must remain green at every commit on the feature
  branch and on `master`.

### Documentation

- Operational procedures (Rider setup, version upgrade commands, CI
  reproduction) live in `CONTRIBUTING.md`. This ADR intentionally omits them to
  avoid drift.
- Complete rollout record:
  `docs/superpowers/specs/2026-04-18-code-quality-tooling-design.md` and
  `docs/superpowers/plans/2026-04-18-code-quality-tooling.md`.
- Baseline CSharpier reformat commit is listed in `.git-blame-ignore-revs` so
  that `git blame` attributes lines to their original authoring commits.

---

## Revision History

| Date       | Status   | Notes              |
|------------|----------|--------------------|
| 2026-04-18 | Accepted | Decision finalized |
