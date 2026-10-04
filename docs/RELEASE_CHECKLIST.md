# uContract.NET Release Checklist

Releases are published by the automated pipeline in `.github/workflows/publish.yml`:
creating a GitHub Release triggers validation, tests, `dotnet pack`, the NuGet push,
and attaching the `.nupkg` to the Release. This checklist covers what must happen
before and around that trigger.

`master` is protected: the release changes reach it through a pull request, like any other
change. The tag is created only after that pull request is merged.

---

## First-Time Setup

Already done for this repository; kept for reference:

- [x] NuGet account created at <https://www.nuget.org> (with 2FA enabled)
- [x] API key generated with "Push new packages" permission, scoped to `uContract`
- [x] Key stored as the `NUGET_API_KEY` secret in the `nuget` GitHub environment

---

## Per-Release Checklist

### 1. CI Green

- [ ] `Build and Test` passes on master (both Ubuntu and Windows jobs; the Ubuntu job also publishes and runs the Native AOT smoke test)

### 2. Version, Changelog, and API Baseline

- [ ] Create the branch `chore/release-{VERSION}` from the current `master`
- [ ] Update `<Version>`, `<AssemblyVersion>` and `<FileVersion>` in `src/uContract/uContract.csproj` (the last two take the four-part form, e.g. `2.0.0.0`)
- [ ] Retitle the `[Unreleased]` section in `CHANGELOG.md` to the new version with the
      release date, and add a new, empty `## [Unreleased]` section above it
- [ ] Update the link references at the bottom of `CHANGELOG.md`, so that `master` needs no
      further change after the tag ({PREVIOUS} is the version released before this one):
      - `[Unreleased]: https://github.com/cwouyang/uContract.NET/compare/v{VERSION}...HEAD`
      - `[{VERSION}]: https://github.com/cwouyang/uContract.NET/compare/v{PREVIOUS}...v{VERSION}`
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

### 4. Pull Request, Merge, and Tag

All changes from step 2 go into a single commit on the release branch:

```bash
git commit -m "chore: Prepare release {VERSION}"
git push -u origin chore/release-{VERSION}
```

- [ ] Open a pull request from `chore/release-{VERSION}` to `master`
- [ ] Merge it when all checks are green: `build (ubuntu-latest)` and `build (windows-latest)`,
      including the Native AOT smoke test steps of the Ubuntu job
- [ ] Wait for the `Build and Test` run that the merge starts on `master` to pass
- [ ] Tag the commit that this run tested (the tip of `master` right after the merge) and push
      the tag:

```bash
git switch master && git pull
git tag -a v{VERSION} -m "Release {VERSION}"
git push origin v{VERSION}
```

The tag version must exactly match `<Version>` in the csproj — the publish workflow
validates this and fails the release otherwise.

### 5. Create the GitHub Release (this triggers publishing)

- [ ] Create a GitHub Release from the existing tag `v{VERSION}`, pasting the changelog entry
      as notes. Rewrite the relative links in the entry as absolute links (for example
      `README.md#…` becomes `https://github.com/cwouyang/uContract.NET/blob/v{VERSION}/README.md#…`):
      a Release body does not resolve repository-relative paths
- [ ] Publishing the Release starts `publish.yml` (shown as `Publish to NuGet` in the Actions
      tab): it validates the tag against the csproj version, re-runs tests and packs
- [ ] Approve the `nuget` deployment in that workflow run. The `nuget` environment requires
      the maintainer's approval and accepts deployments from tags matching `v*`. After
      approval the run pushes to NuGet and attaches the `.nupkg` to the Release

> **Warning**: Once pushed to NuGet, a version cannot be deleted — only unlisted.

### 6. Post-Release Verification

- [ ] `publish.yml` run is green
- [ ] Package visible at <https://www.nuget.org/packages/uContract/>
      (allow 5–10 minutes for indexing)
- [ ] Test install from NuGet.org in a fresh project and repeat the no-`DBC` smoke test
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
