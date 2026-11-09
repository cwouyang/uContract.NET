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

    // Long enough that a look using the stack for each node overflows it (measured at about 2400 nodes).
    private const int LongChainLength = 3000;

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

    // A struct whose object field holds an inline array is compared by its fields: the run-time value of the field
    // is looked at before the struct's built-in Equals, which would reach the inline array's, is asked.
    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAnInlineArrayInAStructsObjectFieldChanged_ReportsTheMember()
    {
        BoxHolding actual = new() { Value = new BoxHolder { Value = WithLaterElementChanged() } };
        BoxHolding expected = new() { Value = new BoxHolder { Value = new IntBuf3() } };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenAStructsObjectFieldHoldsInlineArraysWithEqualElements_DoesNotReportIt()
    {
        BoxHolding actual = new() { Value = new BoxHolder { Value = WithLaterElementChanged() } };
        BoxHolding expected = new() { Value = new BoxHolder { Value = WithLaterElementChanged() } };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAnInlineArrayInANestedStructsObjectFieldChanged_ReportsTheMember()
    {
        NestedBoxHolding actual = new()
        {
            Value = new NestedBoxHolder { Inner = new BoxHolder { Value = WithLaterElementChanged() } },
        };
        NestedBoxHolding expected = new()
        {
            Value = new NestedBoxHolder { Inner = new BoxHolder { Value = new IntBuf3() } },
        };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAStructInAStructsObjectFieldChanged_ReportsTheMember()
    {
        BoxHolding actual = new()
        {
            Value = new BoxHolder { Value = new Holder { Counts = WithLaterElementChanged() } },
        };
        BoxHolding expected = new() { Value = new BoxHolder { Value = new Holder { Counts = new IntBuf3() } } };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    // ---- Nullable fields ----

    // A field declared Nullable<X> counts as declared X: a struct with a Holder? or an IntBuf3? field holds an
    // inline array.
    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAnInlineArrayInANullableStructFieldChanged_ReportsTheMember()
    {
        NullableHolderHolding actual = new()
        {
            Value = new NullableHolder { Inner = new Holder { Counts = WithLaterElementChanged() } },
        };
        NullableHolderHolding expected = new()
        {
            Value = new NullableHolder { Inner = new Holder { Counts = new IntBuf3() } },
        };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenANullableStructFieldHoldsInlineArraysWithEqualElements_DoesNotReportIt()
    {
        NullableHolderHolding actual = new()
        {
            Value = new NullableHolder { Inner = new Holder { Counts = WithLaterElementChanged() } },
        };
        NullableHolderHolding expected = new()
        {
            Value = new NullableHolder { Inner = new Holder { Counts = WithLaterElementChanged() } },
        };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenANullableStructFieldIsNullOnBothSides_DoesNotReportIt()
    {
        NullableHolderHolding actual = new() { Value = new NullableHolder { Inner = null } };
        NullableHolderHolding expected = new() { Value = new NullableHolder { Inner = null } };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfANullableInlineArrayFieldChanged_ReportsTheMember()
    {
        NullableBufHolding actual = new() { Value = new NullableBufHolder { Counts = WithLaterElementChanged() } };
        NullableBufHolding expected = new() { Value = new NullableBufHolder { Counts = new IntBuf3() } };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenANullableInlineArrayFieldHasEqualElements_DoesNotReportIt()
    {
        NullableBufHolding actual = new() { Value = new NullableBufHolder { Counts = WithLaterElementChanged() } };
        NullableBufHolding expected = new() { Value = new NullableBufHolder { Counts = WithLaterElementChanged() } };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    // ---- A nested struct with a hand-written Equals ----

    // A struct whose only route to an inline array is a nested struct with a hand-written Equals is not a holder:
    // its built-in Equals is asked, and it asks the nested Equals.
    [Fact]
    public void EnsureAssignable_WhenTheOnlyRouteToAnInlineArrayIsANestedHandWrittenEqualsThatCallsThemEqual_DoesNotReportIt()
    {
        TolerantOwnerHolding actual = new()
        {
            Value = new TolerantOwner { Inner = new TolerantHolder { Counts = WithLaterElementChanged() } },
        };
        TolerantOwnerHolding expected = new()
        {
            Value = new TolerantOwner { Inner = new TolerantHolder { Counts = new IntBuf3() } },
        };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
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

    // ---- A class whose Equals compares values, held by a struct ----

    // A record class's compiler-generated Equals and System.Tuple's Equals compare values, so a struct's built-in
    // Equals reaches the inline array they hold: the struct is compared by its fields.
    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAnInlineArrayInARecordClassInAStructChanged_ReportsTheMember()
    {
        RecordClassOwnerHolding actual = new()
        {
            Value = new RecordClassOwner { Item = new BufRecord(WithLaterElementChanged()) },
        };
        RecordClassOwnerHolding expected = new()
        {
            Value = new RecordClassOwner { Item = new BufRecord(new IntBuf3()) },
        };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenARecordClassInAStructHoldsInlineArraysWithEqualElements_DoesNotReportIt()
    {
        RecordClassOwnerHolding actual = new()
        {
            Value = new RecordClassOwner { Item = new BufRecord(WithLaterElementChanged()) },
        };
        RecordClassOwnerHolding expected = new()
        {
            Value = new RecordClassOwner { Item = new BufRecord(WithLaterElementChanged()) },
        };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAnInlineArrayInASystemTupleInAStructChanged_ReportsTheMember()
    {
        SystemTupleOwnerHolding actual = new()
        {
            Value = new SystemTupleOwner { Item = Tuple.Create(WithLaterElementChanged(), 1) },
        };
        SystemTupleOwnerHolding expected = new()
        {
            Value = new SystemTupleOwner { Item = Tuple.Create(new IntBuf3(), 1) },
        };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenASystemTupleInAStructHoldsInlineArraysWithEqualElements_DoesNotReportIt()
    {
        SystemTupleOwnerHolding actual = new()
        {
            Value = new SystemTupleOwner { Item = Tuple.Create(WithLaterElementChanged(), 1) },
        };
        SystemTupleOwnerHolding expected = new()
        {
            Value = new SystemTupleOwner { Item = Tuple.Create(WithLaterElementChanged(), 1) },
        };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAnInlineArrayInARecordClassInAStructsObjectFieldChanged_ReportsTheMember()
    {
        BoxHolding actual = new() { Value = new BoxHolder { Value = new BufRecord(WithLaterElementChanged()) } };
        BoxHolding expected = new() { Value = new BoxHolder { Value = new BufRecord(new IntBuf3()) } };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenARecordClassInAStructsObjectFieldHoldsInlineArraysWithEqualElements_DoesNotReportIt()
    {
        BoxHolding actual = new() { Value = new BoxHolder { Value = new BufRecord(WithLaterElementChanged()) } };
        BoxHolding expected = new() { Value = new BoxHolder { Value = new BufRecord(WithLaterElementChanged()) } };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    // A class derived from System.Tuple<…> compares values with the base class's Equals; its items are read through
    // the tuple, not through fields the derived class shows.
    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAnInlineArrayInADerivedTupleInAStructChanged_ReportsTheMember()
    {
        DerivedTupleOwnerHolding actual = new()
        {
            Value = new DerivedTupleOwner { Item = new DerivedTuple(WithLaterElementChanged(), 1) },
        };
        DerivedTupleOwnerHolding expected = new()
        {
            Value = new DerivedTupleOwner { Item = new DerivedTuple(new IntBuf3(), 1) },
        };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenADerivedTupleInAStructHoldsInlineArraysWithEqualElements_DoesNotReportIt()
    {
        DerivedTupleOwnerHolding actual = new()
        {
            Value = new DerivedTupleOwner { Item = new DerivedTuple(WithLaterElementChanged(), 1) },
        };
        DerivedTupleOwnerHolding expected = new()
        {
            Value = new DerivedTupleOwner { Item = new DerivedTuple(WithLaterElementChanged(), 1) },
        };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAnInlineArrayInADerivedTupleInAStructsObjectFieldChanged_ReportsTheMember()
    {
        BoxHolding actual = new() { Value = new BoxHolder { Value = new DerivedTuple(WithLaterElementChanged(), 1) } };
        BoxHolding expected = new() { Value = new BoxHolder { Value = new DerivedTuple(new IntBuf3(), 1) } };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenADerivedTupleInAStructsObjectFieldHoldsInlineArraysWithEqualElements_DoesNotReportIt()
    {
        BoxHolding actual = new() { Value = new BoxHolder { Value = new DerivedTuple(WithLaterElementChanged(), 1) } };
        BoxHolding expected = new()
        {
            Value = new BoxHolder { Value = new DerivedTuple(WithLaterElementChanged(), 1) },
        };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    // ---- A value that refers back to itself ----

    // Looking at run-time values stops at a value already looked at: a box whose object field refers back to it is
    // compared as without the inline-array rules.
    [Fact]
    public void EnsureAssignable_WhenOneSideIsABoxThatRefersToItself_ReportsTheMember()
    {
        Boxed actual = new() { Value = SelfReferring(tag: 1) };
        Boxed expected = new() { Value = new Referring { Tag = 1 } };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    // The tags differ, so the built-in Equals decides "unequal" before it reaches the cycles.
    [Fact]
    public void EnsureAssignable_WhenBothSidesAreBoxesThatReferToThemselves_ReportsTheMember()
    {
        Boxed actual = new() { Value = SelfReferring(tag: 1) };
        Boxed expected = new() { Value = SelfReferring(tag: 2) };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenOneSideIsTwoBoxesThatReferToEachOther_ReportsTheMember()
    {
        Boxed actual = new() { Value = ReferringToEachOther(tag: 1) };
        Boxed expected = new() { Value = new Referring { Tag = 1 } };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    // ---- A deep graph without an inline array ----

    // Looking at run-time values does not use the stack for each level: a struct holding a long chain of records keeps
    // today's result, the chains' Equals decides.
    [Fact]
    public void EnsureAssignable_WhenAStructHoldsDistinctLongRecordChainsWithEqualValues_DoesNotReportIt()
    {
        ChainOwnerHolding actual = new() { Value = new ChainOwner { Head = Chain(LongChainLength) } };
        ChainOwnerHolding expected = new() { Value = new ChainOwner { Head = Chain(LongChainLength) } };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenAStructHoldsOneSharedLongRecordChainOnBothSides_DoesNotReportIt()
    {
        ChainNode head = Chain(LongChainLength);
        ChainOwnerHolding actual = new() { Value = new ChainOwner { Head = head } };
        ChainOwnerHolding expected = new() { Value = new ChainOwner { Head = head } };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    // ---- An exception from a hand-written Equals below a struct ----

    // No NotSupportedException is caught: one thrown by the hand-written Equals of a class a struct holds
    // propagates, whatever the struct.
    [Fact]
    public void EnsureAssignable_WhenAStructHoldsAClassWhoseEqualsThrowsNotSupported_LetsItPropagate()
    {
        ThrowerOwnerHolding actual = new() { Value = new ThrowerOwner { Item = new Thrower() } };
        ThrowerOwnerHolding expected = new() { Value = new ThrowerOwner { Item = new Thrower() } };

        Assert.Throws<NotSupportedException>(() => Contract.EnsureAssignable(actual, expected));
    }

    [Fact]
    public void EnsureAssignable_WhenARecordStructHoldsAClassWhoseEqualsThrowsNotSupported_LetsItPropagate()
    {
        ThrowerRecordHolding actual = new() { Value = new ThrowerRecord(new Thrower()) };
        ThrowerRecordHolding expected = new() { Value = new ThrowerRecord(new Thrower()) };

        Assert.Throws<NotSupportedException>(() => Contract.EnsureAssignable(actual, expected));
    }

    [Fact]
    public void EnsureAssignable_WhenATupleHoldsAClassWhoseEqualsThrowsNotSupported_LetsItPropagate()
    {
        ThrowerTupleHolding actual = new() { Value = (new Thrower(), 1) };
        ThrowerTupleHolding expected = new() { Value = (new Thrower(), 1) };

        Assert.Throws<NotSupportedException>(() => Contract.EnsureAssignable(actual, expected));
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

    private struct NestedBoxHolder
    {
        public BoxHolder Inner;
    }

    private sealed class NestedBoxHolding
    {
        public NestedBoxHolder Value;
    }

    private struct NullableHolder
    {
        public Holder? Inner;
    }

    private sealed class NullableHolderHolding
    {
        public NullableHolder Value;
    }

    private struct NullableBufHolder
    {
        public IntBuf3? Counts;
    }

    private sealed class NullableBufHolding
    {
        public NullableBufHolder Value;
    }

    private struct TolerantOwner
    {
        public TolerantHolder Inner;
    }

    private sealed class TolerantOwnerHolding
    {
        public TolerantOwner Value;
    }

    // Setting the field through the interface sets it on the box itself, not on a copy.
    private interface IReferring
    {
        void Refer(object? target);
    }

    private struct Referring : IReferring
    {
        public int Tag;
        public object? Target;

        public void Refer(object? target) => Target = target;
    }

    private static object SelfReferring(int tag)
    {
        IReferring box = new Referring { Tag = tag };
        box.Refer(box);
        return box;
    }

    private static object ReferringToEachOther(int tag)
    {
        IReferring first = new Referring { Tag = tag };
        IReferring second = new Referring { Tag = tag };
        first.Refer(second);
        second.Refer(first);
        return first;
    }

    private static ChainNode Chain(int length)
    {
        ChainNode head = new(0, null);
        for (int value = 1; value < length; value++)
        {
            head = new ChainNode(value, head);
        }

        return head;
    }

    private sealed record ChainNode(int Value, ChainNode? Next);

    private struct ChainOwner
    {
        public ChainNode? Head;
    }

    private sealed class ChainOwnerHolding
    {
        public ChainOwner Value;
    }

    private sealed record BufRecord(IntBuf3 Counts);

    private struct RecordClassOwner
    {
        public BufRecord Item;
    }

    private sealed class RecordClassOwnerHolding
    {
        public RecordClassOwner Value;
    }

    private struct SystemTupleOwner
    {
        public Tuple<IntBuf3, int> Item;
    }

    private sealed class SystemTupleOwnerHolding
    {
        public SystemTupleOwner Value;
    }

    // Its items are private fields of the framework base class System.Tuple<IntBuf3, int>.
#pragma warning disable MA0097 // Test fixture only: the comparison operators of the base tuple are not under test.
    private sealed class DerivedTuple(IntBuf3 counts, int total) : Tuple<IntBuf3, int>(counts, total);
#pragma warning restore MA0097

    private struct DerivedTupleOwner
    {
        public DerivedTuple Item;
    }

    private sealed class DerivedTupleOwnerHolding
    {
        public DerivedTupleOwner Value;
    }

    private sealed class Thrower
    {
        public override bool Equals(object? obj) => throw new NotSupportedException();

        public override int GetHashCode() => 0;
    }

    private struct ThrowerOwner
    {
        public Thrower Item;
    }

    private sealed class ThrowerOwnerHolding
    {
        public ThrowerOwner Value;
    }

    private record struct ThrowerRecord(Thrower Item);

    private sealed class ThrowerRecordHolding
    {
        public ThrowerRecord Value;
    }

    private sealed class ThrowerTupleHolding
    {
        public (Thrower Item, int Count) Value;
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
