using System.Reflection;
using System.Runtime.CompilerServices;

namespace uContract.Tests;

/// <summary>
///     A fact that runs only where the runtime does or does not offer <c>RuntimeHelpers.Box(ref byte,
///     RuntimeTypeHandle)</c>, and is reported as skipped elsewhere: with <c>exists</c> true on .NET 9 and later, with
///     <c>exists</c> false on .NET 8. The runtime is asked once, by looking the method up.
/// </summary>
internal sealed class RuntimeHelpersBoxFactAttribute : FactAttribute
{
    public RuntimeHelpersBoxFactAttribute(bool exists)
    {
        if (exists != Exists)
        {
            Skip = exists
                ? "RuntimeHelpers.Box does not exist on this runtime."
                : "RuntimeHelpers.Box exists on this runtime.";
        }
    }

    private static bool Exists { get; } =
        typeof(RuntimeHelpers).GetMethod(
            "Box",
            BindingFlags.Public | BindingFlags.Static,
            [typeof(byte).MakeByRefType(), typeof(RuntimeTypeHandle)]
        )
        is not null;
}
