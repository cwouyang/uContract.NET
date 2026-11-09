# Inline Arrays Compared Element by Element — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `Contract.EnsureAssignable<T>()` compares `[InlineArray]` values element by element, gives the same result
on the .NET 8 and .NET 10 runtimes, and never lets the .NET 10 `NotSupportedException` of the built-in
`Equals` escape (issue #84).

**Architecture:** The comparison (`MemberComparison`, `Contract`'s top-level path) stops calling the built-in
`ValueType.Equals` on inline arrays and holder structs and walks inline-array elements by the element rules;
`DeepCopier` copies every element for `Contract.Old`. Element access works without dynamic code where the
runtime allows it, uses guarded `MakeGenericMethod` otherwise, and throws "cannot compare" where neither works.

**Tech Stack:** C# / `net8.0` library, xUnit tests, Native AOT smoke program, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-11-09-inline-array-elements-design.md` (approved: commit 52b049d, sha256
prefix 05e45fa812fc26b1). Section names below (B1–B8, Terms) refer to it. Executors read both.
Spike evidence: `C:\Users\cwouyang\Desktop\Dev\personal\sdd-work\uContract.NET-84\spike-report.md`,
`holder-8.txt`, `holder-10.txt`.

## Global Constraints

- Library target stays `net8.0`; `TreatWarningsAsErrors` stays on; `global.json` stays SDK `8.0.427` / `latestPatch`.
- Same result on the .NET 8 and .NET 10 runtimes for the same input, except B6 (".NET 8 Native AOT, struct
  element").
- No `NotSupportedException` is caught; an exception thrown by a user `Equals` propagates (spec B2a, revision 7).
- The built-in `ValueType.Equals`/`GetHashCode` is never called on an inline array or a holder struct (Terms).
- No new IL2xxx/IL3xxx diagnostic in the library build. The only new suppression is B8's IL3050, at one place,
  with a written justification, and the dynamic code runs only when `RuntimeFacts.IsDynamicCodeSupported` is true.
- C# fixed-size buffers keep today's behaviour (B7).
- Formatting: `dotnet csharpier check .` passes. Analyzers as configured (Roslynator, Meziantou) pass.
- Commits: `<type>(scope): Sentence-case description`, body `Refs #84`, GPG-signed, no session trailers;
  behavioural changes marked `!` where they change documented behaviour; structural and behavioural changes never in
  one commit.
- Machine: run `dotnet` one command at a time, foreground, with `-m:1 -nodeReuse:false -p:UseSharedCompilation=false`.
  Output is zh-TW: read TRX counters and exit codes, not English words.

## Global Interfaces

Existing, binding (public or already in the code):

- `Contract.EnsureAssignable<T>(T actual, T expected, params string[] assignableFieldPatterns)` — throws
  `PostconditionViolationException` (read `.Description`) on a violation, `InvalidOperationException` for "cannot
  compare".
- `Contract.Old<T>(Func<T> supplier)` — returns the deep copy the comparison is later run against.
- `RuntimeFacts.IsDynamicCodeSupported` (internal, `src/uContract/RuntimeFacts.cs`) — the guard for B8; tests
  override it through `TestRuntime.WithoutDynamicCode(...)` (`tests/uContract.Tests/TestHelpers.cs`).
- The existing "cannot compare" messages (`Contract._ShowsNoDeclaredMember`, `Contract._NoMembersVisible`,
  `MemberComparison.WaysOutForAnUnnamedType`) are the style model for B6's message.

## Verification Commands (used by every code task)

From the worktree root, `T` = a TRX directory under
`C:\Users\cwouyang\Desktop\Dev\personal\sdd-work\uContract.NET-84\trx\<task>`:

- .NET 8 runtime: `dotnet test tests/uContract.Tests/uContract.Tests.csproj -m:1 -nodeReuse:false
  -p:UseSharedCompilation=false --logger trx --results-directory "$T/net8"`
- .NET 10 runtime: `DOTNET_ROLL_FORWARD=LatestMajor dotnet test tests/uContract.Tests/uContract.Tests.csproj
  --no-build --logger trx --results-directory "$T/net10"` (record the runtime the test host used).
- Completion condition for a code task: both commands exit 0 and both TRX files report `failed="0"`;
  `dotnet csharpier check .` exits 0; `dotnet build -c Release -p:ContinuousIntegrationBuild=true` has no diagnostics.

## Review Focus

1. Lengths at the edges: an inline array of length 1, and a long one (for example 64), with a change only in the
   last element → reported. (Task 1)
2. `null` elements: a reference element `null` on one side and non-null on the other → reported; `null` on both
   sides → equal. (Task 1)
3. A generic inline-array type (`[InlineArray(n)] struct Buf<TElem>`) closed over a reference type and over a
   struct → compared element by element like a non-generic one. (Tasks 1, 4)
4. An inline array reached through a property getter (a copy each read) and as a dictionary value → compared
   element by element, no exception. (Task 1)
5. An inline array whose element type is itself an inline array → every inner element compared. (Task 4)

---

### Task 1: Inline array as a member or element, primitive/enum/reference elements (B1)  — behavioral

**Observable behavior:** wherever the comparison meets two values of one inline-array type below the top level, a
user `Equals` (Terms) that returns `true` makes them equal; otherwise every element is compared by the element rules,
and a difference is reported under the member that holds the inline array (as for an array member). The built-in
`Equals` of the inline array is not called, so .NET 10 no longer throws for these shapes. This slice covers element
types that are primitives, enums and references (including `string` and collections).

**Files:** `src/uContract/MemberComparison.cs` (and a new focused file under `src/uContract/` if the element access
earns one); tests in `tests/uContract.Tests/` (a new `EnsureAssignableInlineArrayTests.cs` is suggested); the existing
inline-array tests in `EnsureAssignableRobustnessTests.cs` that pin the element-0 behaviour are updated to the new
behaviour in this task.

**Coverage examples** (unordered):

| Input or situation | Observable outcome |
|---|---|
| class field of an `int` inline array, only a later element differs | violation naming that field |
| same, all elements equal | passes |
| `string` elements, element 0 differs | violation |
| `List<int>` elements, distinct list instances with equal contents | passes |
| `List<int>` elements, one later list has a different element | violation |
| enum elements, a later element differs | violation |
| inline array as a property of a class | same as a field |
| inline array as an element of an array, of a `List<>`, as a dictionary value, as the value of an `object` field | element-by-element result as above, no exception |
| inline-array type with a hand-written `Equals` that returns `true` for differing elements | passes |
| same type, `Equals` returns `false` | elements compared; equal elements → passes |
| length 1 inline array; length 64 with only the last element changed (Review Focus 1) | correct result for each |
| `null` vs non-null reference element; `null` on both sides (Review Focus 2) | violation; passes |
| generic inline array closed over a reference type (Review Focus 3) | as non-generic |
| C# fixed-size buffer, only a later element differs | still passes (B7, unchanged) |
| every example above on the .NET 10 runtime | same outcome as on .NET 8 |

**Exclusions:** no change to structs that hold an inline array (holder structs), the top level (`T` itself or a
boxed value compared as `T` = `object`), struct element types, `Contract.Old`, Native AOT behaviour, docs, CI.
Do not call the built-in `Equals` on an inline array.

**Interfaces:** `Contract.EnsureAssignable<T>`.

**Verification:** Verification Commands; completion condition as stated there.

**Commit:** `fix(assignable)!: Compare inline arrays element by element` (body `Refs #84`), plus any separate
structural commit before it.

### Task 2: Holder structs and inline arrays found only at run time (B2, B2a)  — behavioral

**Observable behavior:** a holder struct (Terms) is compared by its fields without calling its `Equals`; an
inline-array field among them is compared element by element. A struct without a user `Equals` whose
`object`/interface/`ValueType` field (directly or in a nested struct) holds an inline array or a holder struct at run
time is compared by its fields without calling its built-in `Equals`. A user `Equals` still decides first and its
exceptions propagate; no `NotSupportedException` is caught.

**Files:** `src/uContract/MemberComparison.cs`; tests as in Task 1. Constraint (spec Non-goals): no per-call
reflection that the existing per-type metadata caches avoid.

**Coverage examples** (unordered):

| Input or situation | Observable outcome |
|---|---|
| class member of type `struct Outer { Buf B; int X; }`, only a later element of `B` differs | violation naming the member, both runtimes |
| same, equal | passes, no exception on .NET 10 |
| holder nested two struct levels deep | as above |
| `record struct Rec(Buf B, int X)` member, later element differs / equal | violation / passes, both runtimes |
| `(Buf, int)` tuple member; a struct holding such a tuple | violation / passes, both runtimes |
| struct with a hand-written `Equals` holding a `Buf`, `Equals` returns `true` | passes (its `Equals` decides) |
| struct with a hand-written `Equals` that throws `NotSupportedException` | the exception propagates |
| struct whose `object` field holds a `Buf`, later element differs | violation, both runtimes |
| same struct, `object` field holds an `int` | unchanged from today |
| struct field of type `Holder?` / `Buf?`, later element differs / equal | violation / passes, both runtimes |
| struct holding a class whose hand-written `Equals` throws `NotSupportedException` | the exception propagates |
| struct field of a record class / `System.Tuple<Buf, int>` holding a `Buf`; struct `object` field holding such a record class; later element differs / equal | violation / passes, both runtimes |
| a boxed struct whose `object` field refers back to itself (one side, both sides) | no stack overflow; result as without the inline-array rules |

**Exclusions:** top level (Task 3), struct elements (Task 4), `Old` (Task 5), AOT (Task 6). Do not catch
`NotSupportedException` thrown by a user `Equals`.

**Interfaces:** `Contract.EnsureAssignable<T>`.

**Verification:** Verification Commands.

**Commit:** `fix(assignable)!: Compare structs holding inline arrays by their fields` (body `Refs #84`).

### Task 3: Inline arrays and holder structs at the top level (B3)  — behavioral

**Observable behavior:** when `T` is an inline-array type, or the runtime type of both values is one inline-array type
under another `T` (for example `T` = `object`), the two values are compared as a whole, element by element, as an
array is; assignable patterns do not apply; the violation says they are not equal and are compared as that
inline-array type element by element. An inline array that implements `IEnumerable` is treated the same way. A
top-level holder struct under another `T` follows B2/B2a instead of calling `Equals` first.

**Files:** `src/uContract/Contract.cs` (`_IsComparedAsWhole`, `_IsDecidedByEqualsAlone`, `_TopLevelMembers`, the
different-runtime-types path), `src/uContract/MemberComparison.cs` if shared; tests; the existing top-level inline-array
tests in `EnsureAssignableRuntimeTypeTests.cs` and the `EnumerableInlineArray…` test in
`EnsureAssignableRobustnessTests.cs` are updated to the new behaviour.

**Coverage examples** (unordered):

| Input or situation | Observable outcome |
|---|---|
| `T` = an inline-array type, only a later element differs | violation (compared as a whole) |
| same, with a pattern naming the element field | still a violation (patterns do not apply) |
| `T` = `object`, both boxed `Buf`, element 0 / a later element differs / equal | violation / violation / passes |
| `T` = `object`, boxed `Buf` vs a boxed value of another type | violation as for other different runtime types today |
| `T` = `object`, inline array implementing `IEnumerable<List<int>>`, distinct equal lists | passes |
| `T` = `object`, boxed holder struct, later element differs | violation |
| all of the above on .NET 10 | same outcome |

**Data:** the message must state that the values are not equal and are compared as the inline-array type element by
element (wording is the implementer's, following the existing "compared as a whole" messages).

**Exclusions:** struct elements (Task 4), `Old` (Task 5), AOT (Task 6), docs.

**Interfaces:** `Contract.EnsureAssignable<T>`.

**Verification:** Verification Commands.

**Commit:** `fix(assignable)!: Compare top-level inline arrays element by element` (body `Refs #84`).

### Task 4: Struct elements and nested inline arrays, with guarded dynamic code (B5, B8)  — behavioral

**Observable behavior:** inline arrays whose element type is a struct (including a struct holding references, and an
inline array of inline arrays) are compared element by element under the JIT on both runtimes. Where no dynamic code
is needed (the .NET 9+ `RuntimeHelpers.SizeOf`/`Box` reached at run time, measured in the spike) that way is used;
on the .NET 8 runtime `MakeGenericMethod` is used only when `RuntimeFacts.IsDynamicCodeSupported` is true, behind one
IL3050 suppression with a justification. When neither is available the call throws the B6 "cannot compare"
`InvalidOperationException` naming the type, the member path and the ways out.

**Files:** the element-access code from Task 1; tests (use `TestRuntime.WithoutDynamicCode` for the no-dynamic-code
path).

**Coverage examples** (unordered):

| Input or situation | Observable outcome |
|---|---|
| element type `struct Named { byte Tag; string Name; }`, a later element's `Name` differs | violation, both runtimes |
| same, equal | passes |
| element struct holding a `List<int>`, distinct equal lists | passes |
| inline array of inline arrays, an inner later element differs (Review Focus 5) | violation |
| generic inline array closed over a struct (Review Focus 3) | as non-generic |
| struct elements with dynamic code unavailable on the .NET 8 runtime | `InvalidOperationException` naming the type and ways out |
| struct elements with dynamic code unavailable on the .NET 10 runtime | compared normally (no dynamic code needed) |
| library Release build | no new IL2xxx/IL3xxx diagnostic |

**Data (B6 message content, from the spec):** names the inline-array type and the member path; ways out: preserve with
`[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(X))]`, list the member that holds it as assignable, or
set `DBC_POST=off`.

**Exclusions:** `Old` (Task 5), Native AOT smoke (Task 6), docs. No dynamic code outside the guarded place.

**Interfaces:** `Contract.EnsureAssignable<T>`, `RuntimeFacts.IsDynamicCodeSupported`, `TestRuntime.WithoutDynamicCode`.

**Verification:** Verification Commands, plus `dotnet build src/uContract -c Release -p:ContinuousIntegrationBuild=true`
without diagnostics.

**Commit:** `fix(assignable)!: Compare inline arrays of structs element by element` (body `Refs #84`).

### Task 5: `Contract.Old` copies every element (B4)  — behavioral

**Observable behavior:** an `Old` copy of a value holding an inline array deep-copies every element (not only element
0), for reference and struct elements. An unchanged pair then passes, and an in-place change to any element after
`Old` is reported. Where the elements cannot be copied (the B6 cases), `Old` does not throw; the comparison that
follows throws "cannot compare".

**Files:** `src/uContract/DeepCopier.cs`, the element-access code; tests.

**Coverage examples** (unordered):

| Input or situation | Observable outcome |
|---|---|
| `Old` of a class with a `List<int>` inline array, unchanged | passes |
| same, `buf[2].Add(1)` after `Old` | violation |
| same, element 0 mutated in place | violation |
| struct elements holding a list, later element's list mutated in place | violation |
| `Old` with dynamic code unavailable on the .NET 8 runtime, struct elements | `Old` returns; the comparison throws "cannot compare" |
| all of the above on .NET 10 | same outcome (the last one: compared normally) |

**Exclusions:** AOT smoke (Task 6), docs.

**Interfaces:** `Contract.Old<T>`, `Contract.EnsureAssignable<T>`, `TestRuntime.WithoutDynamicCode`.

**Verification:** Verification Commands.

**Commit:** `fix(old)!: Copy every element of an inline array` (body `Refs #84`).

### Task 6: Native AOT — hidden element field and smoke checks (B6)  — behavioral

**Observable behavior:** under Native AOT, an inline array whose element field is hidden (nothing preserves the type)
makes the call throw "cannot compare" instead of passing or crashing; a preserved inline array is compared as under
the JIT for primitive and reference elements; struct elements on the .NET 8 runtime throw "cannot compare".

**Files:** the element-access code (hidden-field detection, unit-testable through the existing seams where they reach
it); `tests/uContract.AotSmoke/Program.cs` (new checks, numbered after the last existing one).

**Coverage examples** (unordered):

| Input or situation (Native AOT, net8.0, `--assert`) | Observable outcome |
|---|---|
| preserved `int` inline array field, later element differs | violation reported |
| preserved `List<int>` inline array, distinct equal lists | passes |
| unpreserved inline array field | `InvalidOperationException` "cannot compare" with the ways out |
| preserved inline array of struct elements | `InvalidOperationException` "cannot compare" |
| `Old` with a preserved `List<int>` inline array, element 2 changed in place | violation |
| all existing smoke checks | unchanged, with contracts on and `DBC_POST=off` |

**Exclusions:** net10.0 smoke and linux-x64 local runs (CI covers linux-x64); docs.

**Interfaces:** `Contract.EnsureAssignable<T>`, `Contract.Old<T>`.

**Verification:** Verification Commands; Native AOT publish win-x64 with `-warnaserror` (wrapper:
`sdd-work\uContract.NET-84\publish-smoke.cmd`, delete `tests/uContract.AotSmoke/obj` first) exits 0 with no
diagnostics; the executable with `--assert` exits 0 with contracts on and with `DBC_POST=off`.

**Commit:** `fix(assignable)!: Fail loudly on inline arrays Native AOT cannot read` (body `Refs #84`).

### Task 7: CI runs the unit tests on the .NET 10 runtime  — non-code

**Files:** Modify `.github/workflows/build-and-test.yml`.

- [ ] **Step 1:** In the "Setup .NET" step add `dotnet-version: 10.0.x` next to `global-json-file: global.json`, with
  a comment that it is installed only so the net8.0 tests can also run on the .NET 10 runtime (#84) and that
  `global.json` still selects the SDK.
- [ ] **Step 2:** After "Test", add a step "Test on the .NET 10 runtime" with `env: DOTNET_ROLL_FORWARD: LatestMajor`
  running `dotnet test --no-build --verbosity normal`, and make the runtime visible in the log (a
  `dotnet --list-runtimes` step before it).
- [ ] **Step 3:** Verify locally: `dotnet --version` in the worktree still prints `8.0.427`; the step's command
  exits 0 on the .NET 10 runtime (TRX `failed="0"`).
- [ ] **Step 4:** Commit `ci: Run the unit tests on the .NET 10 runtime too` (body `Refs #84`). CI itself is
  checked on the PR.

### Task 8: Documentation and ADR amendments  — non-code, last

**Files:** `docs/adr/0022-faithful-old-copies-and-robust-comparison.md`, `docs/adr/0026-top-level-runtime-type-comparison.md`,
`docs/adr/0021-postconditions-under-trimming-and-aot.md`, `README.md`, `docs/examples/API_REFERENCE.md`, XML docs in
`src/uContract/Contract.cs` (`EnsureAssignable`, `Old`), `CHANGELOG.md`.

- [ ] **Step 1:** Add a dated three-part amendment (the ADRs' existing amendment convention) to ADR-0022 (R3 step 3
  without the `[InlineArray]` exclusion; B1, B2, B2a, B4; limitations narrowed to fixed-size buffers), ADR-0026 (B3), ADR-0021 (B6 cases; B8 guarded dynamic code and the IL3050 justification).
  Accepted ADR text is never edited; only amendments are added. State measured facts with their source.
- [ ] **Step 2:** Update README, API_REFERENCE and the XML docs wherever they say an inline array is "decided by
  Equals alone" or that only element 0 is seen.
- [ ] **Step 3:** CHANGELOG `[Unreleased]`: Fixed (#84: the .NET 10 `NotSupportedException`, including holder
  structs, record structs and tuples); Changed **BREAKING** (element by element; top level compared as a whole;
  `Old` copies every element; B6 "cannot compare" cases); the known-limitation line keeps only fixed-size buffers.
- [ ] **Step 4:** Check every number and claim against the code, the tests or the spike measurements; mark what was
  not measured. `rg -n -i "inline"` over `docs/`, `README.md`, `CHANGELOG.md` and `src/` to find stale sentences.
- [ ] **Step 5:** Commit `docs(adr): Record element-by-element comparison of inline arrays` and
  `docs: Describe inline-array comparison in the guides and changelog` (body `Refs #84`).

---

Scaffolding: `docs/superpowers/` (this plan and the spec) is archived to `sdd-archive` and dropped from the branch
before the final review.
