using System.Runtime.CompilerServices;

namespace uContract.Tests;

/// <summary>
///     A fact that runs only where the built-in <see cref="ValueType.Equals(object)" /> of an inline array behaves as
///     stated, and is reported as skipped elsewhere: with <c>throws</c> true where it throws
///     <see cref="NotSupportedException" /> (.NET 10), with <c>throws</c> false where it compares element 0 only
///     (.NET 8). The runtime is asked once, by calling that Equals.
/// </summary>
internal sealed class BuiltInInlineArrayEqualsFactAttribute : FactAttribute
{
    public BuiltInInlineArrayEqualsFactAttribute(bool throws)
    {
        if (throws != Throws)
        {
            Skip = throws
                ? "The built-in Equals of an inline array does not throw on this runtime."
                : "The built-in Equals of an inline array throws on this runtime.";
        }
    }

    private static bool Throws { get; } = BuiltInEqualsThrows();

    private static bool BuiltInEqualsThrows()
    {
        try
        {
            object pair = default(Pair);
            _ = pair.Equals(default(Pair));
            return false;
        }
        catch (NotSupportedException)
        {
            return true;
        }
    }

    [InlineArray(2)]
    private struct Pair
    {
        private int _element;
    }
}
