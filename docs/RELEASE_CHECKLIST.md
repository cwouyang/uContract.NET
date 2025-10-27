# uContract.NET Release Checklist

> **Current Version**: 1.0.0-alpha.1

---

## First-Time Setup

These steps only need to be done once:

- [ ] NuGet account created at <https://www.nuget.org>
- [ ] API key generated with "Push new packages" permission
- [ ] Store API key:
  ```bash
  dotnet nuget push --help  # Follow instructions to set up API key
  ```

---

## Per-Release Checklist

### 1. CI Green

Confirm the GitHub Actions pipeline passes (build + test + pack):

- [ ] CI pipeline is green on the release branch
- [ ] No new compiler warnings introduced

### 2. Version & Changelog

- [ ] Update version in `uContract.csproj` (`<Version>`, `<AssemblyVersion>`, `<FileVersion>`)
- [ ] Move `[Unreleased]` items to new version section in `CHANGELOG.md` with release date
- [ ] Update "Current Version" in `README.md`

### 3. Local Package Verification

Build and verify the package locally:

```bash
dotnet clean && dotnet build -c Release
dotnet pack -c Release -o ./artifacts
```

- [ ] Package size is reasonable (< 100 KB for zero-dependency library)
- [ ] Inspect package contents — verify `lib/net8.0/uContract.dll` and `.xml` exist, no test assemblies included
- [ ] Install into a fresh console project and run a smoke test:
  ```csharp
  using uContract;
  Contract.Require("Test", () => true);
  Console.WriteLine("Package works!");
  ```
- [ ] IntelliSense (XML docs) works in the test project

### 4. Package Metadata

- [ ] Verify in `csproj`: Package ID, version, authors, description, license (MIT), project URL, repository URL, tags, release notes

### 5. Git Tag

```bash
git commit -m "chore: prepare release {VERSION}"
git tag -a v{VERSION} -m "Release {VERSION}"
git push origin master && git push origin v{VERSION}
```

### 6. Publish to NuGet

> **Warning**: Once published, a version cannot be deleted — only unlisted.

- [ ] Publish:
  ```bash
  dotnet nuget push ./artifacts/uContract.{VERSION}.nupkg \
    --source https://api.nuget.org/v3/index.json \
    --api-key YOUR_API_KEY
  ```
- [ ] Verify at <https://www.nuget.org/packages/uContract/> (allow 5–10 minutes for indexing)
- [ ] Test install from NuGet.org in a fresh project

### 7. Post-Release

- [ ] Create GitHub Release (tag `v{VERSION}`, attach `.nupkg`, copy changelog entry)
- [ ] Add `[Unreleased]` section to `CHANGELOG.md`
- [ ] Monitor GitHub Issues for bug reports

---

## Rollback

NuGet packages cannot be deleted, only unlisted.

| Option | When to use |
|--------|-------------|
| **Unlist** | Hide from search (still downloadable by version) — use NuGet website |
| **Hotfix release** | Increment version, fix, re-release following this checklist |
| **Deprecate** | Mark as deprecated in package metadata, publish replacement |

After rollback: notify users via GitHub Release notes, document the issue in `CHANGELOG.md`, and plan the fix.
