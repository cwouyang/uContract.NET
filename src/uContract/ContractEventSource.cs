using System;
using System.Diagnostics.Tracing;

namespace uContract;

/// <summary>
///     EventSource for uContract diagnostic events.
///     Provides structured event tracing compatible with dotnet-trace, ETW, and EventPipe.
/// </summary>
/// <remarks>
///     <para>
///         To capture events using dotnet-trace:
///         <code>dotnet-trace collect --providers uContract -- dotnet run</code>
///     </para>
///     <para>
///         To capture events in code, use a custom <see cref="EventListener" />:
///         <code>
///         class MyListener : EventListener {
///             protected override void OnEventSourceCreated(EventSource es) {
///                 if (es.Name == "uContract") EnableEvents(es, EventLevel.Informational);
///             }
///             protected override void OnEventWritten(EventWrittenEventArgs e) {
///                 Console.WriteLine(e.Payload[0]);
///             }
///         }
///         </code>
///     </para>
/// </remarks>
[EventSource(Name = "uContract")]
internal sealed class ContractEventSource : EventSource
{
    /// <summary>
    ///     Singleton instance for firing events.
    /// </summary>
    public static readonly ContractEventSource Log = new();

    /// <summary>
    ///     Fired once when <see cref="ContractConfiguration" /> is initialized.
    ///     Contains a formatted summary of all resolved contract settings and their sources.
    /// </summary>
    /// <param name="configurationSummary">
    ///     Human-readable summary of resolved configuration, including each setting's
    ///     value (on/off) and source (environment variable name or build default).
    /// </param>
    [Event(1, Level = EventLevel.Informational, Message = "Configuration loaded: {0}")]
    public void ConfigurationLoaded(string configurationSummary)
    {
        if (IsEnabled())
        {
            WriteEvent(1, configurationSummary);
        }
    }
}
