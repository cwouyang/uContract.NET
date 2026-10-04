using System.Runtime.CompilerServices;
using System.Text.Json;

namespace uContract.Tests;

/// <summary>
///     Tests that the <see cref="RuntimeFacts" /> seam is reachable: real value by default,
///     the override when set, and the real value again after reset.
/// </summary>
[Collection("EnvironmentVariables")]
public sealed class RuntimeFactsTests
{
    [Fact]
    public void IsDynamicCodeSupported_ByDefault_ReturnsRuntimeValue()
    {
        Assert.Equal(RuntimeFeature.IsDynamicCodeSupported, RuntimeFacts.IsDynamicCodeSupported);
    }

    [Fact]
    public void IsDynamicCodeSupported_WhenOverridden_ReturnsOverrideThenRuntimeValueAfterReset()
    {
        var real = RuntimeFeature.IsDynamicCodeSupported;
        try
        {
            RuntimeFacts.DynamicCodeSupportedOverride = !real;
            Assert.Equal(!real, RuntimeFacts.IsDynamicCodeSupported);
        }
        finally
        {
            RuntimeFacts.DynamicCodeSupportedOverride = null;
        }

        Assert.Equal(real, RuntimeFacts.IsDynamicCodeSupported);
    }

    [Fact]
    public void IsJsonReflectionEnabled_ByDefault_ReturnsRuntimeValue()
    {
        Assert.Equal(JsonSerializer.IsReflectionEnabledByDefault, RuntimeFacts.IsJsonReflectionEnabled);
    }

    [Fact]
    public void IsJsonReflectionEnabled_WhenOverridden_ReturnsOverrideThenRuntimeValueAfterReset()
    {
        var real = JsonSerializer.IsReflectionEnabledByDefault;
        try
        {
            RuntimeFacts.JsonReflectionEnabledOverride = !real;
            Assert.Equal(!real, RuntimeFacts.IsJsonReflectionEnabled);
        }
        finally
        {
            RuntimeFacts.JsonReflectionEnabledOverride = null;
        }

        Assert.Equal(real, RuntimeFacts.IsJsonReflectionEnabled);
    }
}
