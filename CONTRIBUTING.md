# Contributing to uContract.NET

## Setup

```bash
git clone https://github.com/cwouyang/uContract.NET.git
cd uContract.NET
dotnet test
```

## Development Setup

Rider configuration (one-time, per-developer):

1. Install the CSharpier plugin from the Rider marketplace; restart Rider.
2. Settings -> Tools -> CSharpier: enable "Run on save" and "Reformat code on save". Pin the plugin version to match the CLI version in `.config/dotnet-tools.json` (currently `1.2.6`).
3. Settings -> Keymap: rebind `Ctrl+Alt+L` (Reformat Code) to the CSharpier Format action so the IDE shortcut matches CI.
4. Settings -> Editor -> Code Cleanup: create a `uContract-safe` profile containing only "CSharpier Format". Do not run Full Cleanup on this codebase.
5. Settings -> Editor -> Code Style -> C#: enable "Load from EditorConfig" so `.editorconfig` governs per-project style.
6. Run once per clone:

   ```bash
   git config blame.ignoreRevsFile .git-blame-ignore-revs
   ```

To verify, introduce a whitespace error in any `.cs` file and trigger Rider Reformat. The result must be byte-identical to `dotnet csharpier format` output; if it differs, re-check the plugin version pin and the CSharpier-only cleanup profile.

## Code Quality Tools

### Daily Verification

Run before every commit:

```bash
dotnet tool restore
dotnet csharpier check .
dotnet build -p:ContinuousIntegrationBuild=true
dotnet test --no-build --verbosity normal
```

### Reproducing CI Locally

The GitHub Actions workflow runs the same steps in this order:

```bash
dotnet tool restore
dotnet csharpier check .
dotnet build -p:ContinuousIntegrationBuild=true --no-restore
dotnet test --no-build --verbosity normal
```

If all four succeed locally, CI will succeed.

### Upgrading CSharpier

Land each step as its own commit so the formatting-only commit can be added to `.git-blame-ignore-revs` cleanly:

1. Bump the version in `.config/dotnet-tools.json`.
   Commit: `chore(build): bump CSharpier to <new-version>`
2. `dotnet tool restore && dotnet csharpier format .`
   Commit: `style: apply CSharpier <new-version> reformat`
3. Append the SHA of the reformat commit to `.git-blame-ignore-revs`.
   Commit: `chore: add CSharpier <new-version> reformat to blame-ignore list`

### Upgrading Roslynator.Analyzers

1. Bump the `Version` attribute on the `Roslynator.Analyzers` `PackageReference` in `Directory.Build.props`.
   Commit: `chore(build): bump Roslynator.Analyzers to <new-version>`
2. Address any new diagnostics, one logical fix group per commit.
   Commit: `refactor: fix RCSxxxx <description>`

## Pull Requests

- One concern per PR -- do not mix refactoring with behavioral changes
- Tests are required for all changes
- For API changes, open an issue first

## Releasing

1. Update `<Version>` in `src/uContract/uContract.csproj`
2. Promote the public API baseline (per [ADR-0019](docs/adr/0019-public-api-baseline-tracking.md)):
   - Move every entry from `src/uContract/PublicAPI.Unshipped.txt` to `src/uContract/PublicAPI.Shipped.txt`
   - Keep the `#nullable enable` header in both files; Unshipped retains only the header after promotion
   - This makes `PublicAPI.Shipped.txt` the canonical record of the APIs committed at `v{version}`
3. Update `CHANGELOG.md` -- move Unreleased items under the new version heading
4. Commit: `release: prepare v{version}` (version bump, baseline promotion, and CHANGELOG in a single commit)
5. Push to master
6. On GitHub, create a Release with tag `v{version}` targeting master
7. The publish workflow runs automatically:
   - Validates tag matches csproj version
   - Runs tests and packs the NuGet package
   - Waits for manual approval (check the Actions tab)
8. Approve the deployment in the Actions tab
9. Package is published to NuGet.org and attached to the GitHub Release

### One-Time Setup

Before the first release, configure the GitHub Environment:

1. GitHub repo > Settings > Environments > create `nuget`
2. Enable "Required reviewers" > add yourself
3. Add `NUGET_API_KEY` as an environment secret (generate at https://www.nuget.org/account/apikeys)
