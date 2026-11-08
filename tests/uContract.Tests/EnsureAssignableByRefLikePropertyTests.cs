using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace uContract.Tests;

// A public property of a byref-like type (Span<T>, ReadOnlySpan<T>, a ref struct of the user) cannot be read
// through reflection. It is not compared; the fields behind it are. Some tests simulate Native AOT through the
// RuntimeFacts seam, which is process-wide state.
[Collection("EnvironmentVariables")]
public class EnsureAssignableByRefLikePropertyTests
{
    // ---- The rule "no members visible" under Native AOT ----

    // The property is not compared, but it is visible, so the type does not count as showing no members.
    [Fact]
    public void EnsureAssignable_WithoutDynamicCode_WhenTheOnlyMemberIsASpanProperty_DoesNotThrow()
    {
        EmptySpanSource actual = new();
        EmptySpanSource expected = new();

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    public sealed class EmptySpanSource
    {
        [SuppressMessage(
            "Performance",
            "CA1822:Mark members as static",
            Justification = "An instance property is what EnsureAssignable lists."
        )]
        public ReadOnlySpan<byte> Bytes => ReadOnlySpan<byte>.Empty;
    }

    // ---- A framework type with a byref-like property ----

    // Encoding.Preamble is a ReadOnlySpan<byte>.
    [Fact]
    public void EnsureAssignable_WhenAnEncodingMemberIsUnchanged_DoesNotThrow()
    {
        EncodingHolder original = new() { Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true) };
        EncodingHolder old = Contract.Old(() => original);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(original, old));

        Assert.Null(exception);
    }

    // ---- A span property of the compared type ----

    [Fact]
    public void EnsureAssignable_WhenASpanPropertyAndTheFieldBehindItAreUnchanged_DoesNotThrow()
    {
        SpanBuffer original = new(1, 2);
        SpanBuffer old = Contract.Old(() => original);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(original, old));

        Assert.Null(exception);
    }

    public sealed class SpanBuffer(params byte[] bytes)
    {
        private readonly byte[] _bytes = bytes;

        public ReadOnlySpan<byte> Bytes => _bytes;

        public void Overwrite(int index, byte value)
        {
            _bytes[index] = value;
        }
    }

    public sealed class EncodingHolder
    {
        public Encoding Encoding { get; set; } = Encoding.UTF8;
    }
}
