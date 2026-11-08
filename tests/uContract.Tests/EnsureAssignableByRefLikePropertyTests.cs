using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
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

    // The property is not compared, and it does not count as visible either: under Native AOT the fields behind it
    // may be hidden, and a change to them must not pass silently.
    [Fact]
    public void EnsureAssignable_WithoutDynamicCode_WhenTheOnlyMemberIsASpanProperty_ThrowsCannotCompare()
    {
        EmptySpanSource actual = new();
        EmptySpanSource expected = new();

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(
            $"EnsureAssignable cannot compare {typeof(EmptySpanSource)}: "
                + OnlyByRefLikePropertiesVisible
                + "Ways out: check each state member with Contract.Ensure and a value taken with Contract.Old; "
                + "or set DBC_POST=off (disables all postcondition checks).",
            cannotCompare.Message
        );
    }

    [Fact]
    public void EnsureAssignable_WithoutDynamicCode_WhenTIsObjectAndTheOnlyMemberIsASpanProperty_ThrowsCannotCompare()
    {
        object actual = new EmptySpanSource();
        object expected = new EmptySpanSource();

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(
            $"EnsureAssignable cannot compare {typeof(EmptySpanSource)}, the runtime type of the values (T is "
                + $"{typeof(object)}): "
                + OnlyByRefLikePropertiesVisible
                + "Ways out: if the type has fields, preserve them, for example with "
                + $"[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof({EmptySpanSourceInCSharp}))] on a "
                + "method that runs, or pass the values as that type; check each state member with Contract.Ensure "
                + "and a value taken with Contract.Old; or set DBC_POST=off (disables all postcondition checks).",
            cannotCompare.Message
        );
    }

    // Below the top level, the type's Equals decides; two distinct instances are unequal.
    [Fact]
    public void EnsureAssignable_WithoutDynamicCode_WhenANestedMemberHasOnlyASpanProperty_ThrowsCannotCompare()
    {
        EmptySpanSourceHolder actual = new() { Source = new EmptySpanSource() };
        EmptySpanSourceHolder expected = new() { Source = new EmptySpanSource() };

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(
            $"EnsureAssignable cannot compare {typeof(EmptySpanSource)} (reached through "
                + "'EmptySpanSourceHolder.Source'): "
                + OnlyByRefLikePropertiesVisible
                + "Their Equals reports them unequal (without an Equals override this only means they are different "
                + "instances), and their members cannot be listed. Ways out: override Equals so that it compares the "
                + "state; list 'Source' as assignable (patterns are regular expressions matched against top-level "
                + "member names, so use the plain member name); if the type has fields, preserve them, for example "
                + $"with [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof({EmptySpanSourceInCSharp}))] "
                + "on a method that runs; or set DBC_POST=off (disables all postcondition checks).",
            cannotCompare.Message
        );
    }

    // An array type as T is compared as a whole, element by element.
    [Fact]
    public void EnsureAssignable_WithoutDynamicCode_WhenArrayElementsHaveOnlyASpanProperty_ThrowsCannotCompare()
    {
        EmptySpanSource[] actual = [new()];
        EmptySpanSource[] expected = [new()];

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(
            $"EnsureAssignable cannot compare {typeof(EmptySpanSource)} (reached through 'actual[]'): "
                + OnlyByRefLikePropertiesVisible
                + "Their Equals reports them unequal (without an Equals override this only means they are different "
                + "instances), and their members cannot be listed. Ways out: override Equals so that it compares the "
                + "state; if the type has fields, preserve them, for example with "
                + $"[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof({EmptySpanSourceInCSharp}))] on a "
                + "method that runs; or set DBC_POST=off (disables all postcondition checks).",
            cannotCompare.Message
        );
    }

    // The ways out the nested message names.
    [Fact]
    public void EnsureAssignable_WithoutDynamicCode_WhenANestedSpanOnlyTypeOverridesEquals_DoesNotThrow()
    {
        EqualSpanSourceHolder actual = new() { Source = new EqualSpanSource() };
        EqualSpanSourceHolder expected = new() { Source = new EqualSpanSource() };

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WithoutDynamicCode_WhenTheMemberLeadingToASpanOnlyTypeIsAssignable_DoesNotThrow()
    {
        EmptySpanSourceHolder actual = new() { Source = new EmptySpanSource() };
        EmptySpanSourceHolder expected = new() { Source = new EmptySpanSource() };

        Exception? exception = TestRuntime.WithoutDynamicCode(() =>
            Contract.EnsureAssignable(actual, expected, "^Source$")
        );

        Assert.Null(exception);
    }

    private const string OnlyByRefLikePropertiesVisible =
        "under Native AOT the only properties or fields visible to reflection are properties of a byref-like type "
        + "(Span<T>, ReadOnlySpan<T>, a ref struct), which are not compared and do not count as members. ";

    private const string EmptySpanSourceInCSharp =
        "uContract.Tests.EnsureAssignableByRefLikePropertyTests.EmptySpanSource";

    public sealed class EmptySpanSource
    {
        [SuppressMessage(
            "Performance",
            "CA1822:Mark members as static",
            Justification = "An instance property is what EnsureAssignable lists."
        )]
        public ReadOnlySpan<byte> Bytes => ReadOnlySpan<byte>.Empty;
    }

    public sealed class EmptySpanSourceHolder
    {
        public EmptySpanSource Source { get; set; } = new();
    }

    // Stateless, so any two instances are equal.
    public sealed class EqualSpanSource : IEquatable<EqualSpanSource>
    {
        [SuppressMessage(
            "Performance",
            "CA1822:Mark members as static",
            Justification = "An instance property is what EnsureAssignable lists."
        )]
        public ReadOnlySpan<byte> Bytes => ReadOnlySpan<byte>.Empty;

        public bool Equals(EqualSpanSource? other) => other is not null;

        public override bool Equals(object? obj) => obj is EqualSpanSource;

        public override int GetHashCode() => 0;
    }

    public sealed class EqualSpanSourceHolder
    {
        public EqualSpanSource Source { get; set; } = new();
    }

    // ---- A sequence of yours that declares a span property ----

    // The span property is not read, but it is a member declared on a class of yours: the list is not compared by
    // its elements alone, so it differs from a List<byte> of the same elements.
    [Fact]
    public void EnsureAssignable_WhenAListWithASpanViewAndAListHoldEqualElements_ReportsBothRuntimeTypes()
    {
        SpanViewList withView = [1, 2];
        List<byte> plain = [1, 2];
        IEnumerable<byte> actual = withView;
        IEnumerable<byte> expected = plain;

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "actual and expected are of different runtime types: actual is "
                + $"{typeof(SpanViewList)}, expected is {typeof(List<byte>)}, and T is {typeof(IEnumerable<byte>)}. "
                + "No member was compared, so assignable patterns do not apply.",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenAMemberHoldsAListWithASpanViewAndThenAListOfEqualElements_ReportsTheMember()
    {
        SpanViewList withView = [1, 2];
        List<byte> plain = [1, 2];
        SequenceHolder actual = new() { Items = withView };
        SequenceHolder expected = new() { Items = plain };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Items\n  - <Items>k__BackingField", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTwoListsWithASpanViewHoldEqualElements_DoesNotThrow()
    {
        SpanViewList actual = [1, 2];
        SpanViewList expected = [1, 2];

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTwoListsWithASpanViewHoldDifferentElements_ReportsTheElements()
    {
        SpanViewList actual = [1, 2];
        SpanViewList expected = [1, 3];

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.IsType<PostconditionViolationException>(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTheLabelOfALabelledListWithASpanViewChanges_ReportsTheLabel()
    {
        LabelledSpanViewList actual = new() { Label = "a" };
        LabelledSpanViewList expected = new() { Label = "b" };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Label\n  - <Label>k__BackingField", exception.Description);
    }

    [SuppressMessage(
        "Naming",
        "CA1711:Identifiers should not have incorrect suffix",
        Justification = "Names a list of bytes."
    )]
    public sealed class SpanViewList : List<byte>
    {
        public ReadOnlySpan<byte> View => CollectionsMarshal.AsSpan(this);
    }

    [SuppressMessage(
        "Naming",
        "CA1711:Identifiers should not have incorrect suffix",
        Justification = "Names a list of bytes."
    )]
    public sealed class LabelledSpanViewList : List<byte>
    {
        public string Label { get; set; } = "";

        public ReadOnlySpan<byte> View => CollectionsMarshal.AsSpan(this);
    }

    public sealed class SequenceHolder
    {
        public IEnumerable<byte>? Items { get; set; }
    }

    // ---- A write-only span property ----

    [Fact]
    public void EnsureAssignable_WhenAWriteOnlySpanPropertyAndTheFieldBehindItAreUnchanged_DoesNotThrow()
    {
        SpanSink original = new() { Bytes = [1, 2] };
        SpanSink old = Contract.Old(() => original);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(original, old));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTheFieldBehindAWriteOnlySpanPropertyChanges_ReportsTheField()
    {
        SpanSink original = new() { Bytes = [1, 2] };
        SpanSink old = Contract.Old(() => original);
        original.Bytes = [9];

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(original, old)
        );

        Assert.Equal(Violation + "  - _bytes", exception.Description);
    }

    public sealed class SpanSink
    {
        private byte[] _bytes = [];

        public ReadOnlySpan<byte> Bytes
        {
            set => _bytes = value.ToArray();
        }
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
