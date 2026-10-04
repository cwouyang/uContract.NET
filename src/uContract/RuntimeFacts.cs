using System.Runtime.CompilerServices;
using System.Text.Json;

namespace uContract;

/// <summary>
///     Runtime capability facts, with test overrides so Native AOT can be simulated in unit tests.
/// </summary>
/// <remarks>
///     Kept apart from <see cref="Contract" /> on purpose: setting an override from a test must not
///     run <c>Contract</c>'s static initialiser, which would freeze <c>Contract.Config</c> early.
/// </remarks>
internal static class RuntimeFacts
{
    internal static bool? DynamicCodeSupportedOverride;
    internal static bool? JsonReflectionEnabledOverride;

    internal static bool IsDynamicCodeSupported =>
        DynamicCodeSupportedOverride ?? RuntimeFeature.IsDynamicCodeSupported;

    internal static bool IsJsonReflectionEnabled =>
        JsonReflectionEnabledOverride ?? JsonSerializer.IsReflectionEnabledByDefault;
}
