using System.Diagnostics.Tracing;

namespace uContract.Tests;

/// <summary>
///     Tests for diagnostic logging via EventSource (ADR-0014).
///     Verifies EventSource event firing, source tracking, and DBC_DOC stderr output.
/// </summary>
[Collection("EnvironmentVariables")]
public sealed class DiagnosticLoggingTests : IDisposable
{
    public DiagnosticLoggingTests()
    {
        _ClearEnvironment();
    }

    public void Dispose()
    {
        _ClearEnvironment();
    }

    private static void _ClearEnvironment()
    {
        Environment.SetEnvironmentVariable("DBC", null);
        Environment.SetEnvironmentVariable("DBC_PRE", null);
        Environment.SetEnvironmentVariable("DBC_POST", null);
        Environment.SetEnvironmentVariable("DBC_INV", null);
        Environment.SetEnvironmentVariable("DBC_CHECK", null);
        Environment.SetEnvironmentVariable("DBC_DOC", null);
    }

    #region Source Tracking Tests

    [Fact]
    public void Constructor_WhenSpecificEnvVarSet_TracksSourceAsSpecificVariable()
    {
        Environment.SetEnvironmentVariable("DBC_PRE", "true");

        ContractConfiguration config = new();

        Assert.Equal("DBC_PRE", config.PreconditionsSource);
    }

    [Fact]
    public void Constructor_WhenGlobalDbcUsed_TracksSourceAsGlobal()
    {
        Environment.SetEnvironmentVariable("DBC", "false");

        ContractConfiguration config = new();

        Assert.Equal("DBC", config.PostconditionsSource);
        Assert.Equal("DBC", config.InvariantsSource);
        Assert.Equal("DBC", config.CheckSource);
    }

    [Fact]
    public void Constructor_WhenNoEnvVarSet_TracksSourceAsBuildDefault()
    {
        ContractConfiguration config = new();

#if DEBUG
        string expectedSource = "default: Debug";
#else
        string expectedSource = "default: Release";
#endif
        Assert.Equal(expectedSource, config.PreconditionsSource);
        Assert.Equal(expectedSource, config.PostconditionsSource);
        Assert.Equal(expectedSource, config.InvariantsSource);
        Assert.Equal(expectedSource, config.CheckSource);
    }

    [Fact]
    public void Constructor_WhenMixedSources_TracksEachIndependently()
    {
        Environment.SetEnvironmentVariable("DBC", "true");
        Environment.SetEnvironmentVariable("DBC_PRE", "false");
        Environment.SetEnvironmentVariable("DBC_CHECK", "false");

        ContractConfiguration config = new();

        Assert.Equal("DBC_PRE", config.PreconditionsSource);
        Assert.Equal("DBC", config.PostconditionsSource);
        Assert.Equal("DBC", config.InvariantsSource);
        Assert.Equal("DBC_CHECK", config.CheckSource);
    }

    #endregion

    #region EventSource Tests

    [Fact]
    public void Constructor_WhenCreated_FiresConfigurationLoadedEvent()
    {
        using TestEventListener listener = new();

        ContractConfiguration config = new();

        EventWrittenEventArgs? configEvent = listener.Events.Find(e =>
            string.Equals(e.EventName, "ConfigurationLoaded", StringComparison.Ordinal)
        );
        Assert.NotNull(configEvent);
    }

    [Fact]
    public void ConfigurationLoadedEvent_ContainsFormattedSummary_WithSettingsAndSources()
    {
        Environment.SetEnvironmentVariable("DBC_PRE", "true");
        Environment.SetEnvironmentVariable("DBC_POST", "false");

        using TestEventListener listener = new();

        ContractConfiguration config = new();

        EventWrittenEventArgs? configEvent = listener.Events.Find(e =>
            string.Equals(e.EventName, "ConfigurationLoaded", StringComparison.Ordinal)
        );
        Assert.NotNull(configEvent);

        string? summary = configEvent.Payload?[0] as string;
        Assert.NotNull(summary);
        Assert.Contains("Preconditions: on (from DBC_PRE)", summary, StringComparison.Ordinal);
        Assert.Contains("Postconditions: off (from DBC_POST)", summary, StringComparison.Ordinal);
    }

    #endregion

    #region Stderr Output Tests

    [Fact]
    public void Constructor_WhenDbcDocOn_WritesFormattedOutputToStderr()
    {
        Environment.SetEnvironmentVariable("DBC_DOC", "on");
        Environment.SetEnvironmentVariable("DBC", "true");

        using StringWriter stderrCapture = new();
        TextWriter originalStderr = Console.Error;
        Console.SetError(stderrCapture);

        try
        {
            ContractConfiguration config = new();

            string output = stderrCapture.ToString();
            Assert.Contains("[uContract] Configuration loaded:", output, StringComparison.Ordinal);
            Assert.Contains("Preconditions: on", output, StringComparison.Ordinal);
        }
        finally
        {
            Console.SetError(originalStderr);
        }
    }

    [Fact]
    public void Constructor_WhenDbcDocOff_DoesNotWriteToStderr()
    {
        Environment.SetEnvironmentVariable("DBC_DOC", "off");

        using StringWriter stderrCapture = new();
        TextWriter originalStderr = Console.Error;
        Console.SetError(stderrCapture);

        try
        {
            ContractConfiguration config = new();

            string output = stderrCapture.ToString();
            Assert.Empty(output);
        }
        finally
        {
            Console.SetError(originalStderr);
        }
    }

    [Fact]
    public void Constructor_WhenDbcDocUnset_DoesNotWriteToStderr()
    {
        using StringWriter stderrCapture = new();
        TextWriter originalStderr = Console.Error;
        Console.SetError(stderrCapture);

        try
        {
            ContractConfiguration config = new();

            string output = stderrCapture.ToString();
            Assert.Empty(output);
        }
        finally
        {
            Console.SetError(originalStderr);
        }
    }

    [Fact]
    public void StderrOutput_ShowsSourceForEachSetting()
    {
        Environment.SetEnvironmentVariable("DBC_DOC", "on");
        Environment.SetEnvironmentVariable("DBC", "true");
        Environment.SetEnvironmentVariable("DBC_PRE", "false");

        using StringWriter stderrCapture = new();
        TextWriter originalStderr = Console.Error;
        Console.SetError(stderrCapture);

        try
        {
            ContractConfiguration config = new();

            string output = stderrCapture.ToString();
            Assert.Contains("Preconditions: off (from DBC_PRE)", output, StringComparison.Ordinal);
            Assert.Contains("Postconditions: on (from DBC)", output, StringComparison.Ordinal);
            Assert.Contains("Invariants: on (from DBC)", output, StringComparison.Ordinal);
            Assert.Contains("Check: on (from DBC)", output, StringComparison.Ordinal);
        }
        finally
        {
            Console.SetError(originalStderr);
        }
    }

    #endregion

    #region Test Helpers

    private sealed class TestEventListener : EventListener
    {
        public List<EventWrittenEventArgs> Events { get; } = new();

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (string.Equals(eventSource.Name, "uContract", StringComparison.Ordinal))
            {
                EnableEvents(eventSource, EventLevel.Informational);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            Events.Add(eventData);
        }
    }

    #endregion
}
