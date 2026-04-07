# ADR-0014: Diagnostic Logging via EventSource

## Status

**Accepted**

- **Date**: 2026-04-07
- **Deciders**: uContract.NET Contributors
- **Status Date**: 2026-04-07

---

## Context

### Problem Statement

ADR-0004 introduced the `DBC_DOC` environment variable and `DocumentationEnabled` configuration property for diagnostic output, envisioning a startup log that shows the resolved contract configuration. The configuration parsing was implemented in `ContractConfiguration`, but the consuming logic (actual diagnostic output) was never built. We need to decide how to implement this diagnostic logging.

### Relevant Context

- ADR-0004 defined `DBC_DOC` as an environment variable controlling "documentation generation" (diagnostic output)
- ADR-0004's Future Considerations (Section 8) envisioned startup logging:
  ```
  [uContract] Configuration loaded:
  [uContract]   DBC=on (from environment)
  [uContract]   DBC_PRE=on (default)
  ```
- The Java uContract version uses `DBC_DOC` for similar diagnostic purposes
- `ContractConfiguration.DocumentationEnabled` is parsed but never referenced in `Contract.cs`
- .NET provides several diagnostic APIs: `ILogger`, `EventSource`, `Trace`/`Debug`, `Console`
- Microsoft's official guidance for library authors distinguishes between these APIs by use case

### Constraints

- Must maintain zero external dependencies (ADR-0011)
- Must be thread-safe (ADR-0009)
- Must work cross-platform (Windows, Linux, macOS, containers)
- Must not interfere with normal application output
- Should integrate with standard .NET diagnostic tooling
- Should be testable without external infrastructure

---

## Decision

**We will use `System.Diagnostics.Tracing.EventSource` as the primary diagnostic mechanism, with `DBC_DOC=on` enabling additional human-readable output to stderr.**

### Details

**EventSource** is the .NET runtime's built-in structured logging solution, used by the runtime itself (`System.Net.Http`, `System.Text.Json`) and the Azure SDK. It is part of the BCL (`System.Diagnostics.Tracing` namespace) — zero external dependencies required.

**Two-layer approach**:

1. **EventSource event (always available)**: A `ConfigurationLoaded` event is fired once during `ContractConfiguration` construction. Any subscriber (via `EventListener`, `dotnet-trace`, ETW, EventPipe) can capture it. This happens regardless of `DBC_DOC`.

2. **Stderr output (opt-in via `DBC_DOC=on`)**: When `DocumentationEnabled` is true, a formatted human-readable summary is written to `Console.Error`. This provides immediate visibility without requiring external tooling.

**EventSource definition**:

```csharp
[EventSource(Name = "uContract")]
internal sealed class ContractEventSource : EventSource
{
    public static readonly ContractEventSource Log = new();

    [Event(1, Level = EventLevel.Informational, Message = "Configuration loaded")]
    public void ConfigurationLoaded(
        bool preconditionsEnabled, string preconditionsSource,
        bool postconditionsEnabled, string postconditionsSource,
        bool invariantsEnabled, string invariantsSource,
        bool checkEnabled, string checkSource)
    {
        if (IsEnabled())
        {
            WriteEvent(1,
                preconditionsEnabled, preconditionsSource,
                postconditionsEnabled, postconditionsSource,
                invariantsEnabled, invariantsSource,
                checkEnabled, checkSource);
        }
    }
}
```

**Source tracking**: `ContractConfiguration` tracks where each resolved value came from:

| Source | Label Example |
|--------|--------------|
| Specific environment variable | `"DBC_PRE"` |
| Global DBC variable | `"DBC"` |
| Build-specific default | `"default: Debug"` or `"default: Release"` |

**Stderr output format** (when `DBC_DOC=on`):

```
[uContract] Configuration loaded:
[uContract]   Preconditions: on (from DBC_PRE)
[uContract]   Postconditions: on (from DBC)
[uContract]   Invariants: on (default: Debug)
[uContract]   Check: off (from DBC_CHECK)
```

**Capture via dotnet-trace** (no code changes required by consumer):

```bash
dotnet-trace collect --providers uContract -- dotnet run
```

---

## Consequences

### Positive Consequences

- ✅ **Zero dependencies**: `System.Diagnostics.Tracing.EventSource` is BCL-native
- ✅ **Standard .NET practice**: Same approach used by the .NET runtime and Azure SDK
- ✅ **Cross-platform**: EventPipe works on Windows, Linux, macOS
- ✅ **Non-intrusive**: EventSource events are no-ops when no listener is attached
- ✅ **Tool integration**: Works with `dotnet-trace`, PerfView, ETW, custom `EventListener`
- ✅ **Testable**: `EventListener` can subscribe in-process for unit testing
- ✅ **Fulfills ADR-0004**: Implements the planned `DBC_DOC` diagnostic logging

### Negative Consequences

- ❌ **Internal structure change**: `ContractConfiguration` must track setting sources, adding complexity
- ❌ **New file**: Adds `ContractEventSource.cs` to the project
- ❌ **Stderr output**: `DBC_DOC=on` writes to `Console.Error`, which some environments may not expect from a library

### Neutral Consequences

- ⚖️ EventSource events fire even when `DBC_DOC=off`, but with zero overhead when no listener is attached (`IsEnabled()` guard)
- ⚖️ The `DBC_DOC` flag now serves a dual role: EventSource is always available, but `DBC_DOC` specifically controls the stderr convenience output

---

## Alternatives Considered

### Alternative 1: ILogger (Microsoft.Extensions.Logging)

**Description**: Accept `ILoggerFactory` in the API surface, following Microsoft's general guidance for library logging.

**Pros**:
- Microsoft's recommended approach for general-purpose logging
- Rich ecosystem of sinks (Console, Seq, Serilog, Application Insights)
- Source-generated logging for high performance

**Cons**:
- Requires `Microsoft.Extensions.Logging` NuGet package — **violates ADR-0011 (zero-dependency)**
- Changes the static API design — would need DI or factory pattern
- Overkill for a single diagnostic message

**Why rejected**: Violates the zero-dependency principle, which is fundamental to the root contracting design philosophy. Would also require significant API changes.

---

### Alternative 2: System.Diagnostics.Trace

**Description**: Use `Trace.WriteLine()` with `TraceSource` for diagnostic output.

**Pros**:
- Zero dependency (BCL built-in)
- Simple API
- Configurable via `TraceListener`

**Cons**:
- Microsoft explicitly states: "no new functionality will be added" — legacy API
- Less structured than EventSource
- No cross-platform tracing story (no EventPipe integration)
- Harder to filter and consume programmatically

**Why rejected**: Classified as legacy by Microsoft. EventSource is the modern replacement with better tooling support.

---

### Alternative 3: Console.Error only

**Description**: Simply write diagnostic output to stderr when `DBC_DOC=on`.

**Pros**:
- Simplest possible implementation
- No new types needed
- Matches ADR-0004's original vision exactly

**Cons**:
- Not a .NET library best practice — libraries should not write to Console
- Not structured — can't be programmatically consumed
- Not captured by standard .NET diagnostic tooling
- Difficult to test (requires stderr redirection)

**Why rejected**: Violates .NET library conventions. However, stderr output is retained as an opt-in convenience layer on top of EventSource.

---

### Alternative 4: Expose public diagnostic method only

**Description**: Add `Contract.GetDiagnostics()` that returns a formatted string, letting consumers decide how to output it.

**Pros**:
- Maximum flexibility for consumers
- Easy to test (just check return value)
- No side effects

**Cons**:
- Consumers must actively call it — easy to forget
- Doesn't integrate with standard .NET diagnostic tooling
- Doesn't fulfill `DBC_DOC`'s purpose of automatic diagnostic output

**Why rejected**: Doesn't provide automatic diagnostic output. Could be added as a complementary API in the future.

---

## Related Decisions

- **Implements**: [ADR-0004 - Runtime Configuration via Environment Variables](0004-runtime-configuration-environment-variables.md) — Fulfills the `DBC_DOC` diagnostic logging planned in Future Considerations
- **Depends on**: [ADR-0011 - Zero-Dependency Principle](0011-zero-dependency-principle.md) — Constrains choice to BCL-native APIs
- **Depends on**: [ADR-0009 - Thread Safety and Async/Await Support](0009-thread-safety-async-support.md) — EventSource is thread-safe by design

---

## Implementation Notes

- `ContractEventSource` is `internal sealed` — not part of the public API surface
- EventSource name `"uContract"` follows Microsoft naming convention (no "EventSource" suffix)
- Event ID `1` is assigned explicitly per EventSource best practices
- `IsEnabled()` guard prevents any work when no listener is attached
- Source tracking uses simple string labels, avoiding a new enum to keep the change minimal
- Tests use `EventListener` subclass to capture events in-process

---

## References

- [EventSource - Microsoft Learn](https://learn.microsoft.com/dotnet/core/diagnostics/eventsource) — Overview and getting started
- [Instrument Code to Create EventSource Events](https://learn.microsoft.com/dotnet/core/diagnostics/eventsource-instrumentation) — Best practices for EventSource authoring
- [Logging guidance for .NET library authors](https://learn.microsoft.com/dotnet/core/extensions/logging/library-guidance) — Microsoft's official guidance
- [.NET logging and tracing](https://learn.microsoft.com/dotnet/core/diagnostics/logging-tracing) — Comparison of all .NET logging APIs
- [Azure SDK Logging with EventSource](https://learn.microsoft.com/dotnet/azure/sdk/logging) — Real-world EventSource usage in Azure SDK

---

## Revision History

| Date       | Status      | Notes                          |
|------------|-------------|--------------------------------|
| 2026-04-07 | Proposed    | Initial draft                  |
| 2026-04-07 | Accepted    | Decision finalized             |

---
