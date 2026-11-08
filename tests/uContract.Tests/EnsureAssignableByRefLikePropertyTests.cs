using System.Text;

namespace uContract.Tests;

// A public property of a byref-like type (Span<T>, ReadOnlySpan<T>, a ref struct of the user) cannot be read
// through reflection. It is not compared; the fields behind it are.
public class EnsureAssignableByRefLikePropertyTests
{
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
