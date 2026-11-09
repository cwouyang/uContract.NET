using System.Runtime.CompilerServices;
using uContract.Exceptions;

namespace uContract.Tests;

// Below the top level, two values of one inline-array type are compared element by element, by the rules for
// two elements of an array. The built-in ValueType.Equals, which sees only element 0 (and throws on .NET 10),
// is never asked.
public class EnsureAssignableInlineArrayTests
{
    private const string Violation = "Fields were modified that are not marked as assignable:\n";

    private const int Changed = 7;

    // ---- Primitive and enum elements ----

    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAnIntInlineArrayFieldChanged_ReportsTheField()
    {
        Counted actual = new();
        actual.Counts[2] = Changed;
        Counted expected = new();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Counts", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenEveryElementOfAnIntInlineArrayFieldIsEqual_DoesNotReportIt()
    {
        Counted actual = new();
        actual.Counts[2] = Changed;
        Counted expected = new();
        expected.Counts[2] = Changed;

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAnEnumInlineArrayFieldChanged_ReportsTheField()
    {
        Scheduled actual = new();
        actual.Days[1] = DayOfWeek.Friday;
        Scheduled expected = new();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Days", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTheOnlyElementOfALengthOneInlineArrayChanged_ReportsTheField()
    {
        Single actual = new();
        actual.Value[0] = Changed;
        Single expected = new();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyTheLastElementOfALength64InlineArrayChanged_ReportsTheField()
    {
        Sampled actual = new();
        actual.Samples[63] = Changed;
        Sampled expected = new();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Samples", exception.Description);
    }

    // ---- Reference elements ----

    [Fact]
    public void EnsureAssignable_WhenTheFirstStringElementOfAnInlineArrayChanged_ReportsTheField()
    {
        Named actual = new();
        actual.Names[0] = "before";
        Named expected = new();
        expected.Names[0] = "after";

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Names", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenAStringElementIsNullOnOneSideOnly_ReportsTheField()
    {
        Named actual = new();
        actual.Names[1] = "set";
        Named expected = new();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Names", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenEveryStringElementIsNullOnBothSides_DoesNotReportIt()
    {
        Named actual = new();
        Named expected = new();

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAGenericInlineArrayOfStringsChanged_ReportsTheField()
    {
        Tagged actual = new();
        actual.Tags[1] = "before";
        Tagged expected = new();
        expected.Tags[1] = "after";

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Tags", exception.Description);
    }

    // ---- Where the inline array is held ----

    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAnInlineArrayPropertyChanged_ReportsTheProperty()
    {
        CountedProperty actual = new() { Counts = WithLaterElementChanged() };
        CountedProperty expected = new() { Counts = new IntBuf3() };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Counts\n  - <Counts>k__BackingField", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAnInlineArrayInAnArrayChanged_ReportsTheField()
    {
        Grid actual = new() { Rows = [new IntBuf3(), WithLaterElementChanged()] };
        Grid expected = new() { Rows = [new IntBuf3(), new IntBuf3()] };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Rows", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAnInlineArrayInAListChanged_ReportsTheField()
    {
        Journal actual = new() { Entries = [WithLaterElementChanged()] };
        Journal expected = new() { Entries = [new IntBuf3()] };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Entries", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAnInlineArrayDictionaryValueChanged_ReportsTheField()
    {
        Ledger actual = new();
        actual.ByName["a"] = WithLaterElementChanged();
        Ledger expected = new();
        expected.ByName["a"] = new IntBuf3();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - ByName", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAnInlineArrayInAnObjectFieldChanged_ReportsTheField()
    {
        Boxed actual = new() { Value = WithLaterElementChanged() };
        Boxed expected = new() { Value = new IntBuf3() };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenAnObjectFieldHoldsInlineArraysWithEqualElements_DoesNotReportIt()
    {
        Boxed actual = new() { Value = WithLaterElementChanged() };
        Boxed expected = new() { Value = WithLaterElementChanged() };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    // ---- A hand-written Equals ----

    [Fact]
    public void EnsureAssignable_WhenAHandWrittenEqualsCallsInlineArraysWithDifferentElementsEqual_DoesNotReportIt()
    {
        Tolerant actual = new();
        actual.Readings[1] = Changed;
        Tolerant expected = new();

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenAHandWrittenEqualsCallsInlineArraysWithEqualElementsUnequal_DoesNotReportIt()
    {
        Strict actual = new();
        actual.Readings[1] = Changed;
        Strict expected = new();
        expected.Readings[1] = Changed;

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    // ---- A C# fixed-size buffer is not an inline array ----

    // Pins today's behavior: ValueType.Equals and reflection see only the first element of a fixed-size buffer.
    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAFixedSizeBufferChanged_DoesNotSeeIt()
    {
        Fixed actual = new();
        Fixed expected = new();
        unsafe
        {
            actual.Buffer.Values[3] = Changed;
        }

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    private static IntBuf3 WithLaterElementChanged()
    {
        IntBuf3 buffer = new();
        buffer[2] = Changed;
        return buffer;
    }

    [InlineArray(3)]
    private struct IntBuf3
    {
        private int _element;
    }

    private sealed class Counted
    {
        public IntBuf3 Counts;
    }

    private sealed class CountedProperty
    {
        public IntBuf3 Counts { get; set; }
    }

    private sealed class Grid
    {
        public IntBuf3[] Rows = [];
    }

    private sealed class Journal
    {
        public List<IntBuf3> Entries = [];
    }

    private sealed class Ledger
    {
        public Dictionary<string, IntBuf3> ByName = [];
    }

    private sealed class Boxed
    {
        public object? Value;
    }

    [InlineArray(2)]
    private struct DayBuf2
    {
        private DayOfWeek _element;
    }

    private sealed class Scheduled
    {
        public DayBuf2 Days;
    }

    [InlineArray(1)]
    private struct IntBuf1
    {
        private int _element;
    }

    private sealed class Single
    {
        public IntBuf1 Value;
    }

    [InlineArray(64)]
    private struct IntBuf64
    {
        private int _element;
    }

    private sealed class Sampled
    {
        public IntBuf64 Samples;
    }

    [InlineArray(2)]
    private struct StringBuf2
    {
        private string? _element;
    }

    private sealed class Named
    {
        public StringBuf2 Names;
    }

    [InlineArray(2)]
    private struct Buf2<TElem>
    {
        private TElem _element;
    }

    private sealed class Tagged
    {
        public Buf2<string> Tags;
    }

    // Equal by its hand-written Equals whatever its elements hold.
    [InlineArray(2)]
    private struct AlwaysEqualBuf2
    {
        private int _element;

        public override readonly bool Equals(object? obj) => obj is AlwaysEqualBuf2;

        public override readonly int GetHashCode() => 0;
    }

    private sealed class Tolerant
    {
        public AlwaysEqualBuf2 Readings;
    }

    // Unequal by its hand-written Equals whatever its elements hold.
    [InlineArray(2)]
    private struct NeverEqualBuf2
    {
        private int _element;

        public override readonly bool Equals(object? obj) => false;

        public override readonly int GetHashCode() => 0;
    }

    private sealed class Strict
    {
        public NeverEqualBuf2 Readings;
    }

    private unsafe struct FixedInts
    {
        public fixed int Values[4];
    }

    private sealed class Fixed
    {
        public FixedInts Buffer;
    }
}
