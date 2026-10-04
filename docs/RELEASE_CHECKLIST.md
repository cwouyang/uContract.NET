# uContract.NET Release Checklist

Releases are published by the automated pipeline in `.github/workflows/publish.yml`:
creating a GitHub Release triggers validation, tests, `dotnet pack`, the NuGet push,
and attaching the `.nupkg` to the Release. This checklist covers what must happen
before and around that trigger.

---

## First-Time Setup

Already done for this repository; kept for reference:

- [x] NuGet account created at <https://www.nuget.org> (with 2FA enabled)
- [x] API key generated with "Push new packages" permission, scoped to `uContract`
- [x] Key stored as the `NUGET_API_KEY` secret in the `nuget` GitHub environment

---

## Per-Release Checklist

### 1. CI Green

- [ ] `Build and Test` passes on master (both Ubuntu and Windows jobs)

### 2. Version, Changelog, and API Baseline

- [ ] Update `<Version>`, `<AssemblyVersion>` and `<FileVersion>` in `src/uContract/uContract.csproj` (the last two take the four-part form, e.g. `2.0.0.0`)
- [ ] Retitle the `[Unreleased]` section in `CHANGELOG.md` to the new version with the
      release date, and update the link reference at the bottom
- [ ] Promote the public API baseline: move all entries from
      `src/uContract/PublicAPI.Unshipped.txt` into `PublicAPI.Shipped.txt`
      (leave `#nullable enable` in both) — see `CONTRIBUTING.md`

### 3. Local Package Verification

```bash
dotnet clean && dotnet test && dotnet test -c Release
dotnet pack src/uContract/uContract.csproj -c Release -o ./artifacts
```

- [ ] All tests pass
- [ ] Package contains `lib/net8.0/uContract.dll` + `.xml`, `README.md`, `icon.png`,
      `THIRD-PARTY-NOTICES.txt`; no test assemblies
- [ ] `obj/Release/net8.0/uContract.sourcelink.json` exists (Source Link intact)
- [ ] Smoke test: install the package into a fresh console project from a local feed, restoring
      into a throwaway folder (`dotnet restore --packages <temp dir>`) so the local build does not
      stay in the global NuGet cache and mask the published package in step 6. With **no `DBC*`
      variable set**, in a Debug and in a Release build of that project, a passing contract passes
      and `Contract.Require(() => false)` throws `PreconditionViolationException`; with `DBC=off`
      it does not throw. Confirm IntelliSense works
- [ ] In the same project, `typeof(uContract.Contract).Assembly.GetName().Version` prints
      `{VERSION}.0` (confirms `<AssemblyVersion>` was updated with `<Version>`)

### 4. Tag and Push

```bash
git commit -m "chore: prepare release {VERSION}"
git tag -a v{VERSION} -m "Release {VERSION}"
git push origin master && git push origin v{VERSION}
```

The tag version must exactly match `<Version>` in the csproj — the publish workflow
validates this and fails the release otherwise.

### 5. Create the GitHub Release (this triggers publishing)

- [ ] Create a GitHub Release for tag `v{VERSION}`, pasting the changelog entry as notes
- [ ] Publishing the Release starts `publish.yml`: it re-runs tests, packs, pushes to
      NuGet, and attaches the `.nupkg` to the Release

> **Warning**: Once pushed to NuGet, a version cannot be deleted — only unlisted.

### 6. Post-Release Verification

- [ ] `publish.yml` run is green
- [ ] Package visible at <https://www.nuget.org/packages/uContract/>
      (allow 5–10 minutes for indexing)
- [ ] Test install from NuGet.org in a fresh project and repeat the no-`DBC` smoke test
- [ ] Add a fresh `[Unreleased]` section to `CHANGELOG.md`
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
