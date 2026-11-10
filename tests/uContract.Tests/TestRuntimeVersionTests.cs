using System.Globalization;

namespace uContract.Tests;

// CI runs the suite a second time with DOTNET_ROLL_FORWARD=LatestMajor so that the tests gated on the runtime version
// run on the .NET 10 runtime. If that runtime were missing, the run would fall back to .NET 8, the gated tests would be
// skipped, and the step would still pass. That step sets ExpectedRuntimeMajorVariable, and this test fails when the
// runtime it runs on is not that major version. Without the variable it checks nothing.
public class TestRuntimeVersionTests
{
    private const string ExpectedRuntimeMajorVariable = "UCONTRACT_EXPECTED_RUNTIME_MAJOR";

    [Fact]
    public void TheSuite_WhenARuntimeMajorVersionIsExpected_RunsOnIt()
    {
        string? expected = Environment.GetEnvironmentVariable(ExpectedRuntimeMajorVariable);
        if (string.IsNullOrEmpty(expected))
        {
            return;
        }

        Assert.Equal(int.Parse(expected, CultureInfo.InvariantCulture), Environment.Version.Major);
    }
}
