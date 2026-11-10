namespace uContract.Tests;

/// <summary>
///     A fact that runs only where the runtime does or does not offer <c>RuntimeHelpers.Box(ref byte,
///     RuntimeTypeHandle)</c> (and <c>RuntimeHelpers.SizeOf</c>), and is reported as skipped elsewhere: with
///     <c>exists</c> true on .NET 9 and later, with <c>exists</c> false on .NET 8. The runtime is asked as the library
///     asks it (see <c>StructElements.ReadsWithoutDynamicCode</c>).
/// </summary>
internal sealed class RuntimeHelpersBoxFactAttribute : FactAttribute
{
    public RuntimeHelpersBoxFactAttribute(bool exists)
    {
        if (exists != StructElements.ReadsWithoutDynamicCode)
        {
            Skip = exists
                ? "RuntimeHelpers.Box does not exist on this runtime."
                : "RuntimeHelpers.Box exists on this runtime.";
        }
    }
}
