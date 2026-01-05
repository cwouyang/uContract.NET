# uContract.NET

Port of [Java uContract 2.0.1](https://gitlab.com/TeddyChen/ucontract/) (commit `cb1e03f`).

## Commands

- Build: `dotnet build`
- Test all: `dotnet test`
- Single test: `dotnet test --filter "FullyQualifiedName~TestName"`

## Development Standards

### Workflow

Always follow: **Plan → Confirm → Execute**

1. Present approach before writing code
2. Wait for explicit user approval
3. Implement the approved plan

### Testing

**Never write implementation code before a failing test.** Follow TDD (Red → Green → Refactor).

### Code Changes

Never mix structural and behavioral changes in the same commit.

### Architecture Decisions

- Location: `docs/adr/`
- ADRs are the source of truth
- Check existing ADRs before making architectural suggestions

## Gotchas

- `Old<T>()` and `EnsureAssignable<T>()` have **no** generic constraint
- `RequireNotNull<T>`, `EnsureNotNull<T>`, `InvariantNotNull<T>` require `where T : class`
