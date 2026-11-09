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

    // An Equals(object) declared with "new" hides ValueType.Equals instead of overriding it, so it is not the
    // Equals the comparison calls: the elements decide.
    [Fact]
    public void EnsureAssignable_WhenAnEqualsDeclaredWithNewCallsInlineArraysEqual_ReportsALaterElementChange()
    {
        Hidden actual = new();
        actual.Readings[1] = Changed;
        Hidden expected = new();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Readings", exception.Description);
    }

    // An Equals override marked [CompilerGenerated] is not written by hand: the elements decide.
    [Fact]
    public void EnsureAssignable_WhenACompilerGeneratedEqualsCallsInlineArraysEqual_ReportsALaterElementChange()
    {
        Generated actual = new();
        actual.Readings[1] = Changed;
        Generated expected = new();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Readings", exception.Description);
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

    // ---- A struct that holds an inline array ----

    // A struct without a hand-written Equals that holds an inline array is compared by its fields; its built-in
    // Equals, which reaches the inline array's (and throws on .NET 10), is not asked.
    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAnInlineArrayInAStructMemberChanged_ReportsTheMember()
    {
        Holding actual = new()
        {
            Value = new Holder { Counts = WithLaterElementChanged(), Total = 1 },
        };
        Holding expected = new()
        {
            Value = new Holder { Counts = new IntBuf3(), Total = 1 },
        };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenAStructMemberHoldsInlineArraysWithEqualElements_DoesNotReportIt()
    {
        Holding actual = new()
        {
            Value = new Holder { Counts = WithLaterElementChanged(), Total = 1 },
        };
        Holding expected = new()
        {
            Value = new Holder { Counts = WithLaterElementChanged(), Total = 1 },
        };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenAStructMemberHoldsDistinctListsWithEqualContents_DoesNotReportIt()
    {
        ListHolding actual = new() { Value = new ListHolder { Lists = WithLists([1], [2, 3]) } };
        ListHolding expected = new() { Value = new ListHolder { Lists = WithLists([1], [2, 3]) } };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAnInlineArrayInARecordStructMemberChanged_ReportsTheMember()
    {
        RecordHolding actual = new() { Value = new HolderRecord(WithLaterElementChanged(), 1) };
        RecordHolding expected = new() { Value = new HolderRecord(new IntBuf3(), 1) };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenARecordStructMemberHoldsInlineArraysWithEqualElements_DoesNotReportIt()
    {
        RecordHolding actual = new() { Value = new HolderRecord(WithLaterElementChanged(), 1) };
        RecordHolding expected = new() { Value = new HolderRecord(WithLaterElementChanged(), 1) };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAnInlineArrayInATupleMemberChanged_ReportsTheMember()
    {
        TupleHolding actual = new() { Value = (WithLaterElementChanged(), 1) };
        TupleHolding expected = new() { Value = (new IntBuf3(), 1) };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenATupleMemberHoldsInlineArraysWithEqualElements_DoesNotReportIt()
    {
        TupleHolding actual = new() { Value = (WithLaterElementChanged(), 1) };
        TupleHolding expected = new() { Value = (WithLaterElementChanged(), 1) };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    // A struct with a hand-written Equals keeps today's rule: its Equals decides first.
    [Fact]
    public void EnsureAssignable_WhenAHandWrittenEqualsCallsStructsHoldingDifferentInlineArraysEqual_DoesNotReportIt()
    {
        TolerantHolding actual = new() { Value = new TolerantHolder { Counts = WithLaterElementChanged() } };
        TolerantHolding expected = new() { Value = new TolerantHolder { Counts = new IntBuf3() } };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenAHandWrittenEqualsOfAStructHoldingAnInlineArrayThrowsNotSupported_LetsItPropagate()
    {
        ThrowingHolding actual = new() { Value = new ThrowingHolder { Counts = WithLaterElementChanged() } };
        ThrowingHolding expected = new() { Value = new ThrowingHolder { Counts = new IntBuf3() } };

        Assert.Throws<NotSupportedException>(() => Contract.EnsureAssignable(actual, expected));
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAnInlineArrayTwoStructsDeepChanged_ReportsTheMember()
    {
        DeepHolding actual = new()
        {
            Value = new OuterHolder { Inner = new Holder { Counts = WithLaterElementChanged() } },
        };
        DeepHolding expected = new() { Value = new OuterHolder { Inner = new Holder { Counts = new IntBuf3() } } };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenInlineArraysTwoStructsDeepHaveEqualElements_DoesNotReportThem()
    {
        DeepHolding actual = new()
        {
            Value = new OuterHolder { Inner = new Holder { Counts = WithLaterElementChanged() } },
        };
        DeepHolding expected = new()
        {
            Value = new OuterHolder { Inner = new Holder { Counts = WithLaterElementChanged() } },
        };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAnInlineArrayInATupleInAStructChanged_ReportsTheMember()
    {
        TupleHolderHolding actual = new() { Value = new TupleHolder { Pair = (WithLaterElementChanged(), 1) } };
        TupleHolderHolding expected = new() { Value = new TupleHolder { Pair = (new IntBuf3(), 1) } };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenATupleInAStructHoldsInlineArraysWithEqualElements_DoesNotReportIt()
    {
        TupleHolderHolding actual = new() { Value = new TupleHolder { Pair = (WithLaterElementChanged(), 1) } };
        TupleHolderHolding expected = new() { Value = new TupleHolder { Pair = (WithLaterElementChanged(), 1) } };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAnInlineArrayInAStructInAnArrayChanged_ReportsTheField()
    {
        HolderArray actual = new() { Values = [new Holder(), new Holder { Counts = WithLaterElementChanged() }] };
        HolderArray expected = new() { Values = [new Holder(), new Holder()] };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Values", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAnInlineArrayInAStructInAListChanged_ReportsTheField()
    {
        HolderList actual = new() { Values = [new Holder { Counts = WithLaterElementChanged() }] };
        HolderList expected = new() { Values = [new Holder()] };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Values", exception.Description);
    }

    // ---- An inline array found only at run time ----

    // A struct whose object field holds an inline array: on .NET 10 its built-in Equals throws, and the pair is
    // compared by its fields; on .NET 8 the built-in Equals sees only element 0 and decides "equal" (a documented
    // difference between the runtimes).
    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAnInlineArrayInAStructsObjectFieldChanged_ReportsItFromNet10On()
    {
        BoxHolding actual = new() { Value = new BoxHolder { Value = WithLaterElementChanged() } };
        BoxHolding expected = new() { Value = new BoxHolder { Value = new IntBuf3() } };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        if (Environment.Version.Major >= 10)
        {
            PostconditionViolationException violation = Assert.IsType<PostconditionViolationException>(exception);
            Assert.Equal(Violation + "  - Value", violation.Description);
        }
        else
        {
            Assert.Null(exception);
        }
    }

    [Fact]
    public void EnsureAssignable_WhenAStructsObjectFieldHoldsAChangedInt_ReportsTheMember()
    {
        BoxHolding actual = new() { Value = new BoxHolder { Value = Changed } };
        BoxHolding expected = new() { Value = new BoxHolder { Value = 0 } };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenAStructsObjectFieldHoldsEqualInts_DoesNotReportIt()
    {
        BoxHolding actual = new() { Value = new BoxHolder { Value = Changed } };
        BoxHolding expected = new() { Value = new BoxHolder { Value = Changed } };

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

    private static ListBuf2 WithLists(List<int> first, List<int> second)
    {
        ListBuf2 buffer = new();
        buffer[0] = first;
        buffer[1] = second;
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

    // Declares an Equals(object) that compares only element 0, but hides ValueType.Equals instead of
    // overriding it.
    [InlineArray(2)]
    private struct HidingEqualsBuf2
    {
        private int _element;

        public new readonly bool Equals(object? obj) => obj is HidingEqualsBuf2 other && other[0] == this[0];
    }

    private sealed class Hidden
    {
        public HidingEqualsBuf2 Readings;
    }

    // Equal by an Equals override marked [CompilerGenerated] whatever its elements hold.
    [InlineArray(2)]
    private struct GeneratedEqualsBuf2
    {
        private int _element;

        [CompilerGenerated]
        public override readonly bool Equals(object? obj) => obj is GeneratedEqualsBuf2;

        public override readonly int GetHashCode() => 0;
    }

    private sealed class Generated
    {
        public GeneratedEqualsBuf2 Readings;
    }

    private struct Holder
    {
        public IntBuf3 Counts;
        public int Total;
    }

    private sealed class Holding
    {
        public Holder Value;
    }

    // Equal by its hand-written Equals whatever its inline array holds.
    private struct TolerantHolder
    {
        public IntBuf3 Counts;

        public override readonly bool Equals(object? obj) => obj is TolerantHolder;

        public override readonly int GetHashCode() => 0;
    }

    private sealed class TolerantHolding
    {
        public TolerantHolder Value;
    }

    private struct ThrowingHolder
    {
        public IntBuf3 Counts;

        public override readonly bool Equals(object? obj) => throw new NotSupportedException();

        public override readonly int GetHashCode() => 0;
    }

    private sealed class ThrowingHolding
    {
        public ThrowingHolder Value;
    }

    private struct OuterHolder
    {
        public Holder Inner;
    }

    private sealed class DeepHolding
    {
        public OuterHolder Value;
    }

    private struct TupleHolder
    {
        public (IntBuf3 Counts, int Total) Pair;
    }

    private sealed class TupleHolderHolding
    {
        public TupleHolder Value;
    }

    private sealed class HolderArray
    {
        public Holder[] Values = [];
    }

    private sealed class HolderList
    {
        public List<Holder> Values = [];
    }

    private struct BoxHolder
    {
        public object? Value;
    }

    private sealed class BoxHolding
    {
        public BoxHolder Value;
    }

    [InlineArray(2)]
    private struct ListBuf2
    {
        private List<int>? _element;
    }

    private struct ListHolder
    {
        public ListBuf2 Lists;
    }

    private sealed class ListHolding
    {
        public ListHolder Value;
    }

    private record struct HolderRecord(IntBuf3 Counts, int Total);

    private sealed class RecordHolding
    {
        public HolderRecord Value;
    }

    private sealed class TupleHolding
    {
        public (IntBuf3 Counts, int Total) Value;
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
