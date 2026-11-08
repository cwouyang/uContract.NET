using System.Diagnostics.CodeAnalysis;
using System.Text;
using uContract.Exceptions;

namespace uContract.Tests;

// A public property of a byref-like type (Span<T>, ReadOnlySpan<T>, a ref struct of the user) cannot be read
// through reflection. It is not compared; the fields behind it are. Some tests simulate Native AOT through the
// RuntimeFacts seam, which is process-wide state.
[Collection("EnvironmentVariables")]
public class EnsureAssignableByRefLikePropertyTests
{
    private const string Violation = "Fields were modified that are not marked as assignable:\n";

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

    [Fact]
    public void EnsureAssignable_WhenTheFieldBehindASpanPropertyChanges_ReportsTheField()
    {
        SpanBuffer original = new(1, 2);
        SpanBuffer old = Contract.Old(() => original);
        original.Overwrite(0, 9);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(original, old)
        );

        Assert.Equal(Violation + "  - _bytes", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenAPropertyOfAUserRefStructIsUnchanged_DoesNotThrow()
    {
        SlidingWindow original = new(start: 3);
        SlidingWindow old = Contract.Old(() => original);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(original, old));

        Assert.Null(exception);
    }

    // ---- A span property below the top level ----

    [Fact]
    public void EnsureAssignable_WhenASpanPropertyIsReachedThroughAMemberDeclaredAsObject_DoesNotThrow()
    {
        Holder original = new() { Anything = new SpanBuffer(1, 2) };
        Holder old = Contract.Old(() => original);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(original, old));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenASpanPropertyIsReachedThroughAMemberDeclaredAsAnInterface_DoesNotThrow()
    {
        Holder original = new() { Source = new SpanBuffer(1, 2) };
        Holder old = Contract.Old(() => original);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(original, old));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenASpanPropertyIsReachedThroughACollectionElement_DoesNotThrow()
    {
        Holder original = new() { Buffers = [new SpanBuffer(1, 2), new SpanBuffer(3)] };
        Holder old = Contract.Old(() => original);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(original, old));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTheFieldBehindANestedSpanPropertyChanges_ReportsTheTopLevelMember()
    {
        Holder original = new() { Source = new SpanBuffer(1, 2) };
        Holder old = Contract.Old(() => original);
        ((SpanBuffer)original.Source).Overwrite(1, 9);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(original, old)
        );

        Assert.Equal(Violation + "  - Source\n  - <Source>k__BackingField", exception.Description);
    }

    // ---- An interface T that declares a span property ----

    [Fact]
    public void EnsureAssignable_WhenTIsAnInterfaceThatDeclaresASpanProperty_DoesNotThrow()
    {
        ISpanSource original = new SpanBuffer(1, 2);
        ISpanSource old = Contract.Old(() => original);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(original, old));

        Assert.Null(exception);
    }

    // ---- A property that returns by reference is not byref-like: it is still compared ----

    [Fact]
    public void EnsureAssignable_WhenTheValueBehindRefReturningPropertiesIsUnchanged_DoesNotThrow()
    {
        RefCounter original = new(count: 1, limit: 5);
        RefCounter old = Contract.Old(() => original);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(original, old));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTheValueBehindARefReturningPropertyChanges_ReportsThePropertyAndTheField()
    {
        RefCounter original = new(count: 1, limit: 5);
        RefCounter old = Contract.Old(() => original);
        original.Count = 2;

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(original, old)
        );

        Assert.Equal(Violation + "  - Count\n  - _count", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTheValueBehindARefReadonlyPropertyChanges_ReportsThePropertyAndTheField()
    {
        RefCounter original = new(count: 1, limit: 5);
        RefCounter old = Contract.Old(() => original);
        original.RaiseLimit();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(original, old)
        );

        Assert.Equal(Violation + "  - Limit\n  - _limit", exception.Description);
    }

    public sealed class RefCounter(int count, int limit)
    {
        private int _count = count;
        private int _limit = limit;

        public ref int Count => ref _count;

        public ref readonly int Limit => ref _limit;

        public void RaiseLimit()
        {
            _limit++;
        }
    }

    public interface ISpanSource
    {
        ReadOnlySpan<byte> Bytes { get; }
    }

    public sealed class SpanBuffer(params byte[] bytes) : ISpanSource
    {
        private readonly byte[] _bytes = bytes;

        public ReadOnlySpan<byte> Bytes => _bytes;

        public void Overwrite(int index, byte value)
        {
            _bytes[index] = value;
        }
    }

    public ref struct Window
    {
        public int Start { get; init; }
    }

    public sealed class SlidingWindow(int start)
    {
        private readonly int _start = start;

        public Window Current => new() { Start = _start };
    }

    public sealed class Holder
    {
        public object? Anything { get; set; }

        public ISpanSource? Source { get; set; }

        public IList<SpanBuffer> Buffers { get; set; } = [];
    }

    public sealed class EncodingHolder
    {
        public Encoding Encoding { get; set; } = Encoding.UTF8;
    }
}
