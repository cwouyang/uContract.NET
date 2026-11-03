using System;
using System.Runtime.CompilerServices;

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

    // Tells, in place of reflection, whether a type shows no declared member (see
    // MemberComparison.ShowsNoDeclaredMember): under the JIT every type that declares a member shows it.
    internal static Func<Type, bool>? ShowsNoDeclaredMemberOverride;

    internal static bool IsDynamicCodeSupported =>
        DynamicCodeSupportedOverride ?? RuntimeFeature.IsDynamicCodeSupported;
}
