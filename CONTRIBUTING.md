# Contributing to uContract.NET

## Setup

```bash
git clone https://github.com/cwouyang/uContract.NET.git
cd uContract.NET
dotnet test
```

## Pull Requests

- One concern per PR -- do not mix refactoring with behavioral changes
- Tests are required for all changes
- For API changes, open an issue first

## Releasing

1. Update `<Version>` in `src/uContract/uContract.csproj`
2. Update `CHANGELOG.md` -- move Unreleased items under the new version heading
3. Commit: `release: prepare v{version}`
4. Push to master
5. On GitHub, create a Release with tag `v{version}` targeting master
6. The publish workflow runs automatically:
   - Validates tag matches csproj version
   - Runs tests and packs the NuGet package
   - Waits for manual approval (check the Actions tab)
7. Approve the deployment in the Actions tab
8. Package is published to NuGet.org and attached to the GitHub Release

### One-Time Setup

Before the first release, configure the GitHub Environment:

1. GitHub repo > Settings > Environments > create `nuget`
2. Enable "Required reviewers" > add yourself
3. Add `NUGET_API_KEY` as an environment secret (generate at https://www.nuget.org/account/apikeys)
