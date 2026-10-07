namespace uContract.Tests;

/// <summary>
///     A fact that runs only on Windows and is reported as skipped elsewhere. COM interop exists only
///     on Windows: on other platforms <see cref="Type.IsCOMObject" /> is false even for a
///     <c>[ComImport]</c> class.
/// </summary>
internal sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "COM interop exists only on Windows.";
        }
    }
}
