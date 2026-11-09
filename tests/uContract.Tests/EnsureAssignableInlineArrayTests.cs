using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using uContract.Exceptions;

namespace uContract.Tests;

// EnsureAssignable with inline arrays. Wherever two values of one inline-array type meet (a member, an element, a
// field of a holder struct, an object-typed field, and at the top level as T, as object or as an interface they
// implement), they are compared element by element, by the rules for two elements of an array, whatever their
// element type. The comparison never asks the built-in ValueType.Equals of an inline array, which sees only element 0
// (and throws on .NET 10); a hand-written Equals of yours that reaches it still does. A struct that holds an inline
// array is compared by its fields, and a C# fixed-size buffer is not an inline array.
public class EnsureAssignableInlineArrayTests
{
    private const string Violation = "Fields were modified that are not marked as assignable:\n";

    private const int Changed = 7;

    private static readonly Guid SecondGuid = new("6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6f7");

    // Long enough that a look using the stack for each node overflows it (measured at about 2400 nodes).
    private const int LongChainLength = 3000;

    // The fields behind a Visual Basic anonymous type's Key property and non-Key property, named as the compiler
    // names them.
    private const string VisualBasicKeyField = "$Key";

    private const string VisualBasicNonKeyField = "$Extra";

    private static readonly Lazy<Type> VisualBasicAnonymousType = new(DefineVisualBasicAnonymousType);

    // The last ways out of a "cannot compare" message for struct elements that need dynamic code: preserving the type
    // would not help, so it is not offered.
    private const string RunOnNet9OrSetPostOff =
        "run on the .NET 9 runtime or later, which reads struct elements without dynamic code; or set DBC_POST=off "
        + "(disables all postcondition checks).";

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

    // ---- Struct elements ----

    [Fact]
    public void EnsureAssignable_WhenOnlyTheNameOfALaterStructElementChanged_ReportsTheField()
    {
        Badged actual = new();
        actual.Badges[2] = new Badge { Tag = 1, Name = "before" };
        Badged expected = new();
        expected.Badges[2] = new Badge { Tag = 1, Name = "after" };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Badges", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenEveryStructElementIsEqual_DoesNotReportIt()
    {
        Badged actual = new();
        actual.Badges[2] = new Badge { Tag = 1, Name = "same" };
        Badged expected = new();
        expected.Badges[2] = new Badge { Tag = 1, Name = "same" };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenStructElementsHoldDistinctListsWithEqualContents_DoesNotReportIt()
    {
        Bagged actual = new();
        actual.Bags[1] = new Bag { Items = [1, 2] };
        Bagged expected = new();
        expected.Bags[1] = new Bag { Items = [1, 2] };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenAHandWrittenEqualsOfAStructElementCallsDifferentElementsEqual_DoesNotReportIt()
    {
        Tolerated actual = new();
        actual.Values[1] = new LenientValue { Value = Changed };
        Tolerated expected = new();

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenAHandWrittenEqualsOfAStructElementThrows_LetsItPropagate()
    {
        Thrown actual = new();
        actual.Values[1] = new ThrowingValue { Value = Changed };
        Thrown expected = new();

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        // Thrown by the element's hand-written Equals, not by the runtime for the built-in Equals of an inline array.
        Assert.Equal(typeof(ThrowingValue), exception.TargetSite?.DeclaringType);
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAGuidInlineArrayFieldChanged_ReportsTheField()
    {
        Identified actual = new() { Ids = WithSecondGuid(SecondGuid) };
        Identified expected = new();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Ids", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfADecimalInlineArrayFieldChanged_ReportsTheField()
    {
        Priced actual = new();
        actual.Prices[1] = Changed;
        Priced expected = new();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Prices", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfANullableIntInlineArrayFieldChanged_ReportsTheField()
    {
        Optional actual = new();
        actual.Values[1] = Changed;
        Optional expected = new();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Values", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAnInlineArrayOfInlineArraysChanged_ReportsTheField()
    {
        Layered actual = new();
        actual.Layers[1] = WithLaterElementChanged();
        Layered expected = new();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Layers", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAGenericInlineArrayOfStructsChanged_ReportsTheField()
    {
        GenericBadged actual = new();
        actual.Badges[1] = new Badge { Tag = 1, Name = "before" };
        GenericBadged expected = new();
        expected.Badges[1] = new Badge { Tag = 1, Name = "after" };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Badges", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAStructElementInlineArrayInAStructMemberChanged_ReportsTheMember()
    {
        BadgeHolding actual = new() { Value = new BadgeHolder { Badges = WithLaterBadge("before") } };
        BadgeHolding expected = new() { Value = new BadgeHolder { Badges = WithLaterBadge("after") } };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAStructElementInlineArrayInAStructsObjectFieldChanged_ReportsTheMember()
    {
        BoxHolding actual = new() { Value = new BoxHolder { Value = WithLaterBadge("before") } };
        BoxHolding expected = new() { Value = new BoxHolder { Value = WithLaterBadge("after") } };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAStructElementInlineArrayInAnArrayChanged_ReportsTheField()
    {
        BadgeGrid actual = new() { Rows = [WithLaterBadge("before")] };
        BadgeGrid expected = new() { Rows = [WithLaterBadge("after")] };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Rows", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAStructElementInlineArrayInAListChanged_ReportsTheField()
    {
        BadgeJournal actual = new() { Entries = [WithLaterBadge("before")] };
        BadgeJournal expected = new() { Entries = [WithLaterBadge("after")] };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Entries", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyALaterInnerElementOfAnInlineArrayOfStructElementInlineArraysChanged_ReportsTheField()
    {
        BadgeLayered actual = new();
        actual.Layers[1] = WithLaterBadge("before");
        BadgeLayered expected = new();
        expected.Layers[1] = WithLaterBadge("after");

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Layers", exception.Description);
    }

    [RuntimeHelpersBoxFact(exists: false)]
    public void EnsureAssignable_WhenAnArrayElementsStructElementsCannotBeReadWithoutDynamicCode_NamesThePathThroughTheElement()
    {
        BadgeGrid actual = new() { Rows = [WithLaterBadge("before")] };
        BadgeGrid expected = new() { Rows = [WithLaterBadge("after")] };

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(
            $"EnsureAssignable cannot compare {typeof(BadgeBuf3)} (reached through 'BadgeGrid.Rows[]'): "
                + StructElementsNeedDynamicCode<Badge>()
                + "Ways out: list 'Rows' as assignable (patterns are regular expressions matched against top-level "
                + "member names, so use the plain member name); "
                + RunOnNet9OrSetPostOff,
            cannotCompare.Message
        );
    }

    // The .NET 8 runtime reads a struct element only with dynamic code, which Native AOT does not support.
    [RuntimeHelpersBoxFact(exists: false)]
    public void EnsureAssignable_WhenAStructElementCannotBeReadWithoutDynamicCode_CannotCompareTheMember()
    {
        Badged actual = new() { Badges = WithLaterBadge("before") };
        Badged expected = new() { Badges = WithLaterBadge("after") };

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(
            $"EnsureAssignable cannot compare {typeof(BadgeBuf3)} (reached through 'Badged.Badges'): "
                + StructElementsNeedDynamicCode<Badge>()
                + "Ways out: list 'Badges' as assignable (patterns are regular expressions matched against top-level "
                + "member names, so use the plain member name); "
                + RunOnNet9OrSetPostOff,
            cannotCompare.Message
        );
    }

    [RuntimeHelpersBoxFact(exists: false)]
    public void EnsureAssignable_WhenStructElementsThatCannotBeReadWithoutDynamicCodeAreEqual_StillCannotCompareThem()
    {
        Badged actual = new() { Badges = WithLaterBadge("same") };
        Badged expected = new() { Badges = WithLaterBadge("same") };

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        Assert.IsType<InvalidOperationException>(exception);
    }

    [RuntimeHelpersBoxFact(exists: false)]
    public void EnsureAssignable_WhenTIsAnInlineArrayWhoseStructElementsCannotBeReadWithoutDynamicCode_CannotCompareIt()
    {
        BadgeBuf3 actual = WithLaterBadge("before");
        BadgeBuf3 expected = WithLaterBadge("after");

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(
            $"EnsureAssignable cannot compare {typeof(BadgeBuf3)} (reached through 'BadgeBuf3'): "
                + StructElementsNeedDynamicCode<Badge>()
                + "Ways out: "
                + RunOnNet9OrSetPostOff,
            cannotCompare.Message
        );
    }

    // The .NET 9+ runtime boxes a struct element without dynamic code.
    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenOnlyALaterStructElementChangedWithoutDynamicCode_ReportsTheField()
    {
        Badged actual = new() { Badges = WithLaterBadge("before") };
        Badged expected = new() { Badges = WithLaterBadge("after") };

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        PostconditionViolationException violation = Assert.IsType<PostconditionViolationException>(exception);
        Assert.Equal(Violation + "  - Badges", violation.Description);
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

    // An anonymous type's Equals compares its values, like a record class's.
    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAnInlineArrayInAnAnonymousObjectInAStructsObjectFieldChanged_ReportsTheMember()
    {
        BoxHolding actual = new() { Value = new BoxHolder { Value = new { Counts = WithLaterElementChanged() } } };
        BoxHolding expected = new() { Value = new BoxHolder { Value = new { Counts = new IntBuf3() } } };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenAnAnonymousObjectInAStructsObjectFieldHoldsInlineArraysWithEqualElements_DoesNotReportIt()
    {
        BoxHolding actual = new() { Value = new BoxHolder { Value = new { Counts = WithLaterElementChanged() } } };
        BoxHolding expected = new() { Value = new BoxHolder { Value = new { Counts = WithLaterElementChanged() } } };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    // A Visual Basic anonymous type is recognised by its compiler-given name, as a C# one is; its Equals compares
    // only its Key properties, so only the fields behind them are looked at.
    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAnInlineArrayInAVisualBasicAnonymousObjectsKeyPropertyInAStructsObjectFieldChanged_ReportsTheMember()
    {
        BoxHolding actual = new()
        {
            Value = new BoxHolder { Value = NewVisualBasicAnonymousObject(WithLaterElementChanged(), extra: 1) },
        };
        BoxHolding expected = new()
        {
            Value = new BoxHolder { Value = NewVisualBasicAnonymousObject(new IntBuf3(), extra: 1) },
        };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenAVisualBasicAnonymousObjectsKeyPropertyInAStructsObjectFieldHoldsInlineArraysWithEqualElements_DoesNotReportIt()
    {
        BoxHolding actual = new()
        {
            Value = new BoxHolder { Value = NewVisualBasicAnonymousObject(WithLaterElementChanged(), extra: 1) },
        };
        BoxHolding expected = new()
        {
            Value = new BoxHolder { Value = NewVisualBasicAnonymousObject(WithLaterElementChanged(), extra: 1) },
        };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    // The Equals of the anonymous object ignores its non-Key property, and decides.
    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAnInlineArrayInAVisualBasicAnonymousObjectsNonKeyPropertyInAStructsObjectFieldChanged_DoesNotReportIt()
    {
        BoxHolding actual = new()
        {
            Value = new BoxHolder { Value = NewVisualBasicAnonymousObject(key: 1, WithLaterElementChanged()) },
        };
        BoxHolding expected = new()
        {
            Value = new BoxHolder { Value = NewVisualBasicAnonymousObject(key: 1, new IntBuf3()) },
        };

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

    // ---- A record with a hand-written typed Equals ----

    // A record's [CompilerGenerated] Equals(object) calls its typed Equals; one written by hand is a hand-written
    // Equals, and it decides.
    [Fact]
    public void EnsureAssignable_WhenAHandWrittenTypedEqualsOfARecordStructCallsItsInlineArraysEqual_DoesNotReportIt()
    {
        TolerantRecordHolding actual = new() { Value = new TolerantRecord(WithLaterElementChanged(), 1) };
        TolerantRecordHolding expected = new() { Value = new TolerantRecord(new IntBuf3(), 1) };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenAHandWrittenTypedEqualsOfARecordClassInAStructCallsItsInlineArraysEqual_DoesNotReportIt()
    {
        TolerantRecordClassOwnerHolding actual = new()
        {
            Value = new TolerantRecordClassOwner { Item = new TolerantBufRecord(WithLaterElementChanged(), 1) },
        };
        TolerantRecordClassOwnerHolding expected = new()
        {
            Value = new TolerantRecordClassOwner { Item = new TolerantBufRecord(new IntBuf3(), 1) },
        };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    // A derived record's generated Equals calls the typed Equals of its base record; one written by hand there decides.
    [Fact]
    public void EnsureAssignable_WhenAHandWrittenTypedEqualsOfABaseRecordInAStructCallsItsInlineArraysEqual_DoesNotReportIt()
    {
        TolerantBaseOwnerHolding actual = new()
        {
            Value = new TolerantBaseOwner
            {
                Item = new TolerantDerived { Raw = WithLaterElementChanged(), Total = 1 },
            },
        };
        TolerantBaseOwnerHolding expected = new()
        {
            Value = new TolerantBaseOwner
            {
                Item = new TolerantDerived { Raw = new IntBuf3(), Total = 1 },
            },
        };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenAHandWrittenTypedEqualsOfAGenericBaseRecordInAStructCallsItsInlineArraysEqual_DoesNotReportIt()
    {
        TolerantGenericBaseOwnerHolding actual = new()
        {
            Value = new TolerantGenericBaseOwner
            {
                Item = new TolerantGenericDerived(1) { Raw = WithLaterElementChanged() },
            },
        };
        TolerantGenericBaseOwnerHolding expected = new()
        {
            Value = new TolerantGenericBaseOwner { Item = new TolerantGenericDerived(1) { Raw = new IntBuf3() } },
        };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    // ---- A class marked [CompilerGenerated] by hand ----

    // A user or a source generator may mark a class [CompilerGenerated]; an Equals written by hand in it still decides.
    [Fact]
    public void EnsureAssignable_WhenAHandWrittenEqualsOfAClassMarkedCompilerGeneratedInAStructsObjectFieldCallsItsInlineArraysEqual_DoesNotReportIt()
    {
        BoxHolding actual = new() { Value = new BoxHolder { Value = new Marked(WithLaterElementChanged(), 1) } };
        BoxHolding expected = new() { Value = new BoxHolder { Value = new Marked(new IntBuf3(), 1) } };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenAHandWrittenTypedEqualsOfARecordMarkedCompilerGeneratedInAStructCallsItsInlineArraysEqual_DoesNotReportIt()
    {
        MarkedRecordOwnerHolding actual = new()
        {
            Value = new MarkedRecordOwner { Item = new MarkedRecord(WithLaterElementChanged(), 1) },
        };
        MarkedRecordOwnerHolding expected = new()
        {
            Value = new MarkedRecordOwner { Item = new MarkedRecord(new IntBuf3(), 1) },
        };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    // ---- An inline array held through a base record ----

    // A derived record's Equals calls its base record's, which compares the backing field of a positional property
    // declared on the base: an inline array there is found like one the derived record declares.
    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAnInlineArrayInABaseRecordsPropertyInAStructChanged_ReportsTheMember()
    {
        CountsBaseOwnerHolding actual = new()
        {
            Value = new CountsBaseOwner { Item = new CountsDerived(WithLaterElementChanged()) },
        };
        CountsBaseOwnerHolding expected = new()
        {
            Value = new CountsBaseOwner { Item = new CountsDerived(new IntBuf3()) },
        };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenABaseRecordsPropertyInAStructHoldsInlineArraysWithEqualElements_DoesNotReportIt()
    {
        CountsBaseOwnerHolding actual = new()
        {
            Value = new CountsBaseOwner { Item = new CountsDerived(WithLaterElementChanged()) },
        };
        CountsBaseOwnerHolding expected = new()
        {
            Value = new CountsBaseOwner { Item = new CountsDerived(WithLaterElementChanged()) },
        };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfAnInlineArrayInAGenericBaseRecordsPropertyInAStructChanged_ReportsTheMember()
    {
        GenericCountsBaseOwnerHolding actual = new()
        {
            Value = new GenericCountsBaseOwner { Item = new GenericCountsDerived(1, WithLaterElementChanged()) },
        };
        GenericCountsBaseOwnerHolding expected = new()
        {
            Value = new GenericCountsBaseOwner { Item = new GenericCountsDerived(1, new IntBuf3()) },
        };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    // The derived record also declares an inline array of its own; only the one on the base differs.
    [Fact]
    public void EnsureAssignable_WhenOnlyALaterElementOfTheBaseRecordsInlineArrayOfARecordHoldingTwoInAStructChanged_ReportsTheMember()
    {
        CountsBaseOwnerHolding actual = new()
        {
            Value = new CountsBaseOwner { Item = new ExtendedCounts(WithLaterElementChanged(), new IntBuf3()) },
        };
        CountsBaseOwnerHolding expected = new()
        {
            Value = new CountsBaseOwner { Item = new ExtendedCounts(new IntBuf3(), new IntBuf3()) },
        };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    // ---- A derived record's own inline array under a base record's hand-written typed Equals ----

    // A known limitation: the base's hand-written Equals makes the record's Equals a hand-written one, which decides;
    // its generated part still compares the fields the derived record declares with their built-in Equals. On .NET 10
    // that Equals of an inline array throws, and an exception from a hand-written Equals propagates.
    [BuiltInInlineArrayEqualsFact(throws: true)]
    public void EnsureAssignable_WhenADerivedRecordsOwnInlineArrayUnderAHandWrittenBaseEqualsIsReached_LetsTheNotSupportedExceptionPropagate()
    {
        QuotaOwnerHolding actual = new() { Value = new QuotaOwner { Item = new OwnQuota(WithLaterElementChanged()) } };
        QuotaOwnerHolding expected = new() { Value = new QuotaOwner { Item = new OwnQuota(new IntBuf3()) } };

        Assert.Throws<NotSupportedException>(() => Contract.EnsureAssignable(actual, expected));
    }

    // On .NET 8 that built-in Equals compares element 0 only.
    [BuiltInInlineArrayEqualsFact(throws: false)]
    public void EnsureAssignable_WhenOnlyALaterElementOfADerivedRecordsOwnInlineArrayUnderAHandWrittenBaseEqualsChanged_DoesNotSeeIt()
    {
        QuotaOwnerHolding actual = new() { Value = new QuotaOwner { Item = new OwnQuota(WithLaterElementChanged()) } };
        QuotaOwnerHolding expected = new() { Value = new QuotaOwner { Item = new OwnQuota(new IntBuf3()) } };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [BuiltInInlineArrayEqualsFact(throws: false)]
    public void EnsureAssignable_WhenTheFirstElementOfADerivedRecordsOwnInlineArrayUnderAHandWrittenBaseEqualsChanged_ReportsTheMember()
    {
        IntBuf3 firstChanged = new();
        firstChanged[0] = Changed;
        QuotaOwnerHolding actual = new() { Value = new QuotaOwner { Item = new OwnQuota(firstChanged) } };
        QuotaOwnerHolding expected = new() { Value = new QuotaOwner { Item = new OwnQuota(new IntBuf3()) } };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    [BuiltInInlineArrayEqualsFact(throws: false)]
    public void EnsureAssignable_WhenADerivedRecordsOwnInlineArraysUnderAHandWrittenBaseEqualsHaveEqualElements_DoesNotReportIt()
    {
        QuotaOwnerHolding actual = new() { Value = new QuotaOwner { Item = new OwnQuota(WithLaterElementChanged()) } };
        QuotaOwnerHolding expected = new()
        {
            Value = new QuotaOwner { Item = new OwnQuota(WithLaterElementChanged()) },
        };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    // The record between the derived record and the base record declares the inline array.
    [BuiltInInlineArrayEqualsFact(throws: true)]
    public void EnsureAssignable_WhenAMiddleRecordsInlineArrayUnderAHandWrittenBaseEqualsIsReached_LetsTheNotSupportedExceptionPropagate()
    {
        QuotaOwnerHolding actual = new()
        {
            Value = new QuotaOwner { Item = new LeafQuota(WithLaterElementChanged(), 1) },
        };
        QuotaOwnerHolding expected = new() { Value = new QuotaOwner { Item = new LeafQuota(new IntBuf3(), 1) } };

        Assert.Throws<NotSupportedException>(() => Contract.EnsureAssignable(actual, expected));
    }

    [BuiltInInlineArrayEqualsFact(throws: true)]
    public void EnsureAssignable_WhenAnInlineArrayInADerivedRecordsHolderStructUnderAHandWrittenBaseEqualsIsReached_LetsTheNotSupportedExceptionPropagate()
    {
        QuotaOwnerHolding actual = new()
        {
            Value = new QuotaOwner { Item = new HolderQuota(new Holder { Counts = WithLaterElementChanged() }) },
        };
        QuotaOwnerHolding expected = new()
        {
            Value = new QuotaOwner { Item = new HolderQuota(new Holder { Counts = new IntBuf3() }) },
        };

        Assert.Throws<NotSupportedException>(() => Contract.EnsureAssignable(actual, expected));
    }

    [BuiltInInlineArrayEqualsFact(throws: true)]
    public void EnsureAssignable_WhenAnInlineArrayInADerivedRecordsObjectFieldUnderAHandWrittenBaseEqualsIsReached_LetsTheNotSupportedExceptionPropagate()
    {
        QuotaOwnerHolding actual = new() { Value = new QuotaOwner { Item = new BoxQuota(WithLaterElementChanged()) } };
        QuotaOwnerHolding expected = new() { Value = new QuotaOwner { Item = new BoxQuota(new IntBuf3()) } };

        Assert.Throws<NotSupportedException>(() => Contract.EnsureAssignable(actual, expected));
    }

    [BuiltInInlineArrayEqualsFact(throws: true)]
    public void EnsureAssignable_WhenAnInlineArrayInADerivedRecordsRecordUnderAHandWrittenBaseEqualsIsReached_LetsTheNotSupportedExceptionPropagate()
    {
        QuotaOwnerHolding actual = new()
        {
            Value = new QuotaOwner { Item = new RecordQuota(new BufRecord(WithLaterElementChanged())) },
        };
        QuotaOwnerHolding expected = new()
        {
            Value = new QuotaOwner { Item = new RecordQuota(new BufRecord(new IntBuf3())) },
        };

        Assert.Throws<NotSupportedException>(() => Contract.EnsureAssignable(actual, expected));
    }

    [BuiltInInlineArrayEqualsFact(throws: true)]
    public void EnsureAssignable_WhenAGenericDerivedRecordsOwnInlineArrayUnderAHandWrittenBaseEqualsIsReached_LetsTheNotSupportedExceptionPropagate()
    {
        QuotaOwnerHolding actual = new()
        {
            Value = new QuotaOwner { Item = new GenericQuota<IntBuf3>(WithLaterElementChanged()) },
        };
        QuotaOwnerHolding expected = new()
        {
            Value = new QuotaOwner { Item = new GenericQuota<IntBuf3>(new IntBuf3()) },
        };

        Assert.Throws<NotSupportedException>(() => Contract.EnsureAssignable(actual, expected));
    }

    // Whatever fields the derived record declares, the base's hand-written Equals decides; these hold no inline array.
    [Fact]
    public void EnsureAssignable_WhenADerivedRecordDeclaresOnlyAnIntAndTheBaseRecordsHandWrittenEqualsIgnoresItsInlineArray_DoesNotReportIt()
    {
        TolerantBaseOwnerHolding actual = new()
        {
            Value = new TolerantBaseOwner
            {
                Item = new TolerantExtended(1) { Raw = WithLaterElementChanged(), Total = 1 },
            },
        };
        TolerantBaseOwnerHolding expected = new()
        {
            Value = new TolerantBaseOwner
            {
                Item = new TolerantExtended(1) { Raw = new IntBuf3(), Total = 1 },
            },
        };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenADerivedRecordDeclaresANullListAndTheBaseRecordsHandWrittenEqualsIgnoresItsInlineArray_DoesNotReportIt()
    {
        TolerantBaseOwnerHolding actual = new()
        {
            Value = new TolerantBaseOwner
            {
                Item = new TolerantWithList(null) { Raw = WithLaterElementChanged(), Total = 1 },
            },
        };
        TolerantBaseOwnerHolding expected = new()
        {
            Value = new TolerantBaseOwner
            {
                Item = new TolerantWithList(null) { Raw = new IntBuf3(), Total = 1 },
            },
        };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenADerivedRecordDeclaresANullObjectAndTheBaseRecordsHandWrittenEqualsIgnoresItsInlineArray_DoesNotReportIt()
    {
        TolerantBaseOwnerHolding actual = new()
        {
            Value = new TolerantBaseOwner
            {
                Item = new TolerantWithObject(null) { Raw = WithLaterElementChanged(), Total = 1 },
            },
        };
        TolerantBaseOwnerHolding expected = new()
        {
            Value = new TolerantBaseOwner
            {
                Item = new TolerantWithObject(null) { Raw = new IntBuf3(), Total = 1 },
            },
        };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenADerivedRecordDeclaresAPlainClassAndTheBaseRecordsHandWrittenEqualsIgnoresItsInlineArray_DoesNotReportIt()
    {
        Label label = new() { Name = "north" };
        TolerantBaseOwnerHolding actual = new()
        {
            Value = new TolerantBaseOwner
            {
                Item = new TolerantWithLabel(label) { Raw = WithLaterElementChanged(), Total = 1 },
            },
        };
        TolerantBaseOwnerHolding expected = new()
        {
            Value = new TolerantBaseOwner
            {
                Item = new TolerantWithLabel(label) { Raw = new IntBuf3(), Total = 1 },
            },
        };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenAGenericDerivedRecordHoldsNullAndTheBaseRecordsHandWrittenEqualsIgnoresItsInlineArray_DoesNotReportIt()
    {
        TolerantBaseOwnerHolding actual = new()
        {
            Value = new TolerantBaseOwner
            {
                Item = new TolerantWith<object?>(null) { Raw = WithLaterElementChanged(), Total = 1 },
            },
        };
        TolerantBaseOwnerHolding expected = new()
        {
            Value = new TolerantBaseOwner
            {
                Item = new TolerantWith<object?>(null) { Raw = new IntBuf3(), Total = 1 },
            },
        };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
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

    // ---- A class that declares Equals for two interfaces ----

    // Neither Equals(IFirstKey) nor Equals(ISecondKey) is an Equals of the class's own type; asking for that one does
    // not pick between them.
    [Fact]
    public void EnsureAssignable_WhenAMemberClassDeclaresEqualsForTwoInterfacesAndItsFieldChanged_ReportsTheMember()
    {
        TwoKeysHolding actual = new() { Item = new TwoKeys { Key = 1 } };
        TwoKeysHolding expected = new() { Item = new TwoKeys { Key = 2 } };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Item", exception.Description);
    }

    // ---- At the top level ----

    [Fact]
    public void EnsureAssignable_WhenTIsAnInlineArrayAndOnlyALaterElementChanged_ReportsTheTwoAsNotEqual()
    {
        IntBuf3 actual = WithLaterElementChanged();
        IntBuf3 expected = new();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ComparedAsAnInlineArray<IntBuf3>(), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnInlineArrayAndAPatternNamesItsElementField_StillReportsTheTwoAsNotEqual()
    {
        IntBuf3 actual = WithLaterElementChanged();
        IntBuf3 expected = new();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected, "_element")
        );

        Assert.Equal(ComparedAsAnInlineArray<IntBuf3>(), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnInlineArrayAndEveryElementIsEqual_Passes()
    {
        IntBuf3 actual = WithLaterElementChanged();
        IntBuf3 expected = WithLaterElementChanged();

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTwoBoxedInlineArraysHoldEqualElements_Passes()
    {
        object actual = WithLaterElementChanged();
        object expected = WithLaterElementChanged();

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndABoxedInlineArrayMeetsABoxedInt_ReportsDifferentRuntimeTypes()
    {
        object actual = WithLaterElementChanged();
        object expected = Changed;

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            $"actual and expected are of different runtime types: actual is {typeof(IntBuf3)}, expected is "
                + $"{typeof(int)}, and T is {typeof(object)}. No member was compared, so assignable patterns do not apply.",
            exception.Description
        );
    }

    // An inline array that is also a sequence is compared element by element as any other inline array, not by
    // the rules for a value-type sequence.
    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndAnEnumerableInlineArrayHoldsDistinctListsWithEqualContents_Passes()
    {
        object actual = WithEnumerableLists([1, 2], [3]);
        object expected = WithEnumerableLists([1, 2], [3]);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndALaterListOfAnEnumerableInlineArrayChanged_ReportsTheTwoAsNotEqual()
    {
        object actual = WithEnumerableLists([1, 2], [3]);
        object expected = WithEnumerableLists([1, 2], [Changed]);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ComparedAsAnInlineArray<EnumerableListBuf2>(), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndAStructFieldOfTypeObjectHoldsAnInlineArrayWhoseLaterElementChanged_ReportsTheField()
    {
        object actual = new BoxHolder { Value = WithLaterElementChanged() };
        object expected = new BoxHolder { Value = new IntBuf3() };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value" + ListedFrom<BoxHolder>(), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndOnlyALaterElementOfABoxedHolderStructChanged_ReportsTheField()
    {
        object actual = new Holder { Counts = WithLaterElementChanged() };
        object expected = new Holder();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Counts" + ListedFrom<Holder>(), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTwoBoxedHolderStructsAreEqual_Passes()
    {
        object actual = new Holder { Counts = WithLaterElementChanged(), Total = Changed };
        object expected = new Holder { Counts = WithLaterElementChanged(), Total = Changed };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndABoxedHolderStructsHandWrittenEqualsSaysEqual_Passes()
    {
        object actual = new TolerantHolder { Counts = WithLaterElementChanged() };
        object expected = new TolerantHolder();

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    // Under an interface T the inline array is still compared as a whole, element by element, not by the rules
    // for a sequence.
    [Fact]
    public void EnsureAssignable_WhenTIsAnInterfaceAnInlineArrayImplementsAndALaterListChanged_ReportsTheTwoAsNotEqual()
    {
        IEnumerable<List<int>?> actual = WithEnumerableLists([1, 2], [3]);
        IEnumerable<List<int>?> expected = WithEnumerableLists([1, 2], [Changed]);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ComparedAsAnInlineArray<EnumerableListBuf2>(), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnInterfaceAnInlineArrayImplementsAndItHoldsDistinctListsWithEqualContents_Passes()
    {
        IEnumerable<List<int>?> actual = WithEnumerableLists([1, 2], [3]);
        IEnumerable<List<int>?> expected = WithEnumerableLists([1, 2], [3]);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    // Pins today's outcome: an inline array and a sequence of another runtime type are left to the rules for two
    // sequences of different runtime types, which do not enumerate a sequence that is not a collection.
    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndAnEnumerableInlineArrayMeetsAListWithTheSameContents_CannotCompareIt()
    {
        object actual = WithEnumerableLists([1, 2], [3]);
        object expected = new List<List<int>?>
        {
            new() { 1, 2 },
            new() { 3 },
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            $"EnsureAssignable cannot compare {typeof(EnumerableListBuf2)} as T ({typeof(object)}): it is a sequence "
                + "that is not a collection (it implements none of ICollection, ICollection<T> and "
                + "IReadOnlyCollection<T>), and enumerating it could run a query, consume its source or block. Pass a "
                + "materialised collection (for example ToList()), or pass the value as its own type as T.",
            exception.Message
        );
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTheHandWrittenEqualsOfABoxedHolderStructThrows_LetsItPropagate()
    {
        object actual = new ThrowingHolder { Counts = WithLaterElementChanged() };
        object expected = new ThrowingHolder();

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        // Thrown by the hand-written Equals, not by the runtime for the built-in Equals of an inline array.
        Assert.Equal(typeof(ThrowingHolder), exception.TargetSite?.DeclaringType);
    }

    // An inline array of struct elements is compared as a whole, element by element, as any other inline array.
    [Fact]
    public void EnsureAssignable_WhenTIsAnInlineArrayOfGuidsAndEveryElementIsEqual_Passes()
    {
        GuidBuf2 actual = WithSecondGuid(SecondGuid);
        GuidBuf2 expected = WithSecondGuid(SecondGuid);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnInlineArrayOfGuidsAndOnlyALaterElementChanged_ReportsTheTwoAsNotEqual()
    {
        GuidBuf2 actual = WithSecondGuid(SecondGuid);
        GuidBuf2 expected = new();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ComparedAsAnInlineArray<GuidBuf2>(), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndOnlyALaterStructElementOfABoxedInlineArrayChanged_ReportsTheTwoAsNotEqual()
    {
        object actual = WithLaterBadge("before");
        object expected = WithLaterBadge("after");

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ComparedAsAnInlineArray<BadgeBuf3>(), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnInterfaceAStructElementInlineArrayImplementsAndALaterElementChanged_ReportsTheTwoAsNotEqual()
    {
        IBadgeSet actual = WithLaterBadgeInSet("before");
        IBadgeSet expected = WithLaterBadgeInSet("after");

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ComparedAsAnInlineArray<BadgeSet3>(), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnInterfaceAStructElementInlineArrayImplementsAndEveryElementIsEqual_Passes()
    {
        IBadgeSet actual = WithLaterBadgeInSet("same");
        IBadgeSet expected = WithLaterBadgeInSet("same");

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTwoBoxedInlineArraysHoldEqualStructElements_Passes()
    {
        object actual = WithLaterBadge("same");
        object expected = WithLaterBadge("same");

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsANullableInlineArrayAndOnlyALaterElementChanged_ReportsTheTwoAsNotEqual()
    {
        IntBuf3? actual = WithLaterElementChanged();
        IntBuf3? expected = new IntBuf3();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            $"actual and expected are not equal. {typeof(IntBuf3?)} is compared as a whole, so assignable patterns "
                + "do not apply.",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenTIsANullableInlineArrayAndEveryElementIsEqual_Passes()
    {
        IntBuf3? actual = WithLaterElementChanged();
        IntBuf3? expected = WithLaterElementChanged();

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    private static string ListedFrom<TRuntime>()
    {
        return $"\nMembers were listed from the runtime type {typeof(TRuntime)}, not from T ({typeof(object)}).";
    }

    private static string ComparedAsAnInlineArray<TInlineArray>()
    {
        return $"actual and expected are not equal. {typeof(TInlineArray)} is an inline array, compared as a whole "
            + "element by element, so assignable patterns do not apply.";
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

    private static object NewVisualBasicAnonymousObject(object key, object extra)
    {
        Type type = VisualBasicAnonymousType.Value;
        object anonymous = Activator.CreateInstance(type)!;
        type.GetField(VisualBasicKeyField, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(anonymous, key);
        type.GetField(VisualBasicNonKeyField, BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(anonymous, extra);
        return anonymous;
    }

    // C# cannot declare a Visual Basic anonymous type, so one is emitted in the shape the Visual Basic compiler gives
    // New With {Key .Key = key, .Extra = extra} (measured on a Visual Basic build): a [CompilerGenerated] class named
    // VB$AnonymousType_0 with a private read-only field $Key behind the Key property and a private writable field
    // $Extra behind the non-Key one, whose Equals compares only the $Key of both (see
    // EqualsAsAVisualBasicAnonymousTypeDoes). The properties are left out, and the fields are declared object: an
    // emitted type cannot declare a field of the private IntBuf3.
    private static Type DefineVisualBasicAnonymousType()
    {
        string name = $"Emitted{Guid.NewGuid():N}";
        TypeBuilder builder = AssemblyBuilder
            .DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run)
            .DefineDynamicModule(name)
            .DefineType("VB$AnonymousType_0", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class);
        builder.SetCustomAttribute(
            new CustomAttributeBuilder(typeof(CompilerGeneratedAttribute).GetConstructor(Type.EmptyTypes)!, [])
        );
        builder.DefineField(VisualBasicKeyField, typeof(object), FieldAttributes.Private | FieldAttributes.InitOnly);
        builder.DefineField(VisualBasicNonKeyField, typeof(object), FieldAttributes.Private);
        builder.DefineDefaultConstructor(MethodAttributes.Public);
        ILGenerator equals = builder
            .DefineMethod(
                nameof(Equals),
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                typeof(bool),
                [typeof(object)]
            )
            .GetILGenerator();
        equals.Emit(OpCodes.Ldarg_0);
        equals.Emit(OpCodes.Ldarg_1);
        equals.Emit(
            OpCodes.Call,
            typeof(EnsureAssignableInlineArrayTests).GetMethod(nameof(EqualsAsAVisualBasicAnonymousTypeDoes))!
        );
        equals.Emit(OpCodes.Ret);
        return builder.CreateType();
    }

    // As the Equals of a Visual Basic anonymous type: the Equals of the values of its Key field only.
    public static bool EqualsAsAVisualBasicAnonymousTypeDoes(object self, object? other)
    {
        FieldInfo key = self.GetType().GetField(VisualBasicKeyField, BindingFlags.NonPublic | BindingFlags.Instance)!;
        return other is not null
            && other.GetType() == self.GetType()
            && Equals(key.GetValue(self), key.GetValue(other));
    }

    private static string StructElementsNeedDynamicCode<TElement>()
    {
        return $"it is an inline array of {typeof(TElement)}, a struct, whose elements this runtime reads only with "
            + "dynamic code, and dynamic code is not supported here (Native AOT). ";
    }

    private static BadgeSet3 WithLaterBadgeInSet(string name)
    {
        BadgeSet3 set = new();
        set[2] = new Badge { Tag = 1, Name = name };
        return set;
    }

    private static BadgeBuf3 WithLaterBadge(string name)
    {
        BadgeBuf3 buffer = new();
        buffer[2] = new Badge { Tag = 1, Name = name };
        return buffer;
    }

    private static GuidBuf2 WithSecondGuid(Guid second)
    {
        GuidBuf2 buffer = new();
        buffer[1] = second;
        return buffer;
    }

    private static EnumerableListBuf2 WithEnumerableLists(List<int> first, List<int> second)
    {
        EnumerableListBuf2 buffer = new();
        buffer[0] = first;
        buffer[1] = second;
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

    // A struct of a primitive and a reference, padded between them.
    private struct Badge
    {
        public byte Tag;
        public string? Name;
    }

    [InlineArray(3)]
    private struct BadgeBuf3
    {
        private Badge _element;
    }

    private sealed class Badged
    {
        public BadgeBuf3 Badges;
    }

    private struct BadgeHolder
    {
        public BadgeBuf3 Badges;
    }

    private sealed class BadgeHolding
    {
        public BadgeHolder Value;
    }

    [InlineArray(2)]
    private struct BadgeBuf3Buf2
    {
        private BadgeBuf3 _element;
    }

    private sealed class BadgeLayered
    {
        public BadgeBuf3Buf2 Layers;
    }

    private interface IBadgeSet { }

    [InlineArray(3)]
    private struct BadgeSet3 : IBadgeSet
    {
        private Badge _element;
    }

    private sealed class BadgeGrid
    {
        public BadgeBuf3[] Rows = [];
    }

    private sealed class BadgeJournal
    {
        public List<BadgeBuf3> Entries = [];
    }

    private sealed class GenericBadged
    {
        public Buf2<Badge> Badges;
    }

    private struct Bag
    {
        public List<int>? Items;
    }

    [InlineArray(2)]
    private struct BagBuf2
    {
        private Bag _element;
    }

    private sealed class Bagged
    {
        public BagBuf2 Bags;
    }

    // Equal by its hand-written Equals to any other LenientValue.
    private struct LenientValue
    {
        public int Value;

        public override readonly bool Equals(object? obj) => obj is LenientValue;

        public override readonly int GetHashCode() => 0;
    }

    [InlineArray(2)]
    private struct LenientBuf2
    {
        private LenientValue _element;
    }

    private sealed class Tolerated
    {
        public LenientBuf2 Values;
    }

    private struct ThrowingValue
    {
        public int Value;

        public override readonly bool Equals(object? obj) => throw new NotSupportedException();

        public override readonly int GetHashCode() => 0;
    }

    [InlineArray(2)]
    private struct ThrowingBuf2
    {
        private ThrowingValue _element;
    }

    private sealed class Thrown
    {
        public ThrowingBuf2 Values;
    }

    private sealed class Identified
    {
        public GuidBuf2 Ids;
    }

    [InlineArray(2)]
    private struct DecimalBuf2
    {
        private decimal _element;
    }

    private sealed class Priced
    {
        public DecimalBuf2 Prices;
    }

    [InlineArray(2)]
    private struct NullableIntBuf2
    {
        private int? _element;
    }

    private sealed class Optional
    {
        public NullableIntBuf2 Values;
    }

    [InlineArray(2)]
    private struct IntBuf3Buf2
    {
        private IntBuf3 _element;
    }

    private sealed class Layered
    {
        public IntBuf3Buf2 Layers;
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

    // Equal when the totals are: the inline arrays do not count.
    private record struct TolerantRecord(IntBuf3 Counts, int Total)
    {
        public readonly bool Equals(TolerantRecord other) => Total == other.Total;

        public override readonly int GetHashCode() => Total;
    }

    private sealed class TolerantRecordHolding
    {
        public TolerantRecord Value;
    }

    // Equal when the totals are: the inline arrays do not count.
    private sealed record TolerantBufRecord(IntBuf3 Counts, int Total)
    {
        public bool Equals(TolerantBufRecord? other) => other is not null && Total == other.Total;

        public override int GetHashCode() => Total;
    }

    private struct TolerantRecordClassOwner
    {
        public TolerantBufRecord Item;
    }

    private sealed class TolerantRecordClassOwnerHolding
    {
        public TolerantRecordClassOwner Value;
    }

    // Equal when the totals are: the inline array does not count.
    private record TolerantBase
    {
        public IntBuf3 Raw;

        public int Total { get; init; }

        public virtual bool Equals(TolerantBase? other) => other is not null && Total == other.Total;

        public override int GetHashCode() => Total;
    }

    private sealed record TolerantDerived : TolerantBase;

    private struct TolerantBaseOwner
    {
        public TolerantBase Item;
    }

    private sealed class TolerantBaseOwnerHolding
    {
        public TolerantBaseOwner Value;
    }

    // Equal when the totals are: the inline array does not count.
    private record TolerantGenericBase<TTotal>(TTotal Total)
    {
        public IntBuf3 Raw;

        public virtual bool Equals(TolerantGenericBase<TTotal>? other) =>
            other is not null && EqualityComparer<TTotal>.Default.Equals(Total, other.Total);

        public override int GetHashCode() => EqualityComparer<TTotal>.Default.GetHashCode(Total!);
    }

    private sealed record TolerantGenericDerived(int Total) : TolerantGenericBase<int>(Total);

    private struct TolerantGenericBaseOwner
    {
        public TolerantGenericBase<int> Item;
    }

    private sealed class TolerantGenericBaseOwnerHolding
    {
        public TolerantGenericBaseOwner Value;
    }

    // Marked as a source generator may mark it. Equal when the totals are: the inline array does not count.
    [CompilerGenerated]
    private sealed class Marked(IntBuf3 counts, int total)
    {
        public IntBuf3 Counts = counts;

        public int Total = total;

        public override bool Equals(object? obj) => obj is Marked other && Total == other.Total;

        public override int GetHashCode() => Total;
    }

    // Marked as a source generator may mark it. Equal when the totals are: the inline array does not count.
    [CompilerGenerated]
    private sealed record MarkedRecord(IntBuf3 Counts, int Total)
    {
        public bool Equals(MarkedRecord? other) => other is not null && Total == other.Total;

        public override int GetHashCode() => Total;
    }

    private struct MarkedRecordOwner
    {
        public MarkedRecord Item;
    }

    private sealed class MarkedRecordOwnerHolding
    {
        public MarkedRecordOwner Value;
    }

    private record CountsBase(IntBuf3 Counts);

    // Counts is the base record's property: its backing field is declared on CountsBase.
    private sealed record CountsDerived(IntBuf3 Counts) : CountsBase(Counts);

    private sealed record ExtendedCounts(IntBuf3 Counts, IntBuf3 Extra) : CountsBase(Counts);

    private struct CountsBaseOwner
    {
        public CountsBase Item;
    }

    private sealed class CountsBaseOwnerHolding
    {
        public CountsBaseOwner Value;
    }

    private record GenericCountsBase<TValue>(TValue Value, IntBuf3 Counts);

    private sealed record GenericCountsDerived(int Value, IntBuf3 Counts) : GenericCountsBase<int>(Value, Counts);

    private struct GenericCountsBaseOwner
    {
        public GenericCountsBase<int> Item;
    }

    private sealed class GenericCountsBaseOwnerHolding
    {
        public GenericCountsBaseOwner Value;
    }

    // Equal when the totals are.
    private record QuotaBase
    {
        public int Total { get; init; }

        public virtual bool Equals(QuotaBase? other) => other is not null && Total == other.Total;

        public override int GetHashCode() => Total;
    }

    private sealed record OwnQuota(IntBuf3 Own) : QuotaBase;

    private record MiddleQuota(IntBuf3 Middle) : QuotaBase;

    private sealed record LeafQuota(IntBuf3 Middle, int Extra) : MiddleQuota(Middle);

    private sealed record HolderQuota(Holder Inner) : QuotaBase;

    private sealed record BoxQuota(object Box) : QuotaBase;

    private sealed record RecordQuota(BufRecord Inner) : QuotaBase;

    private sealed record GenericQuota<TOwn>(TOwn Own) : QuotaBase;

    private struct QuotaOwner
    {
        public QuotaBase Item;
    }

    private sealed class QuotaOwnerHolding
    {
        public QuotaOwner Value;
    }

    private sealed record TolerantExtended(int Extra) : TolerantBase;

    private sealed record TolerantWithList(List<int>? Extra) : TolerantBase;

    private sealed record TolerantWithObject(object? Extra) : TolerantBase;

    private sealed record TolerantWithLabel(Label Extra) : TolerantBase;

    private sealed record TolerantWith<TExtra>(TExtra Extra) : TolerantBase;

    private sealed class Label
    {
        public string Name = "";
    }

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

    [InlineArray(2)]
    private struct GuidBuf2
    {
        private Guid _element;
    }

    [InlineArray(2)]
    private struct EnumerableListBuf2 : IEnumerable<List<int>?>
    {
        private List<int>? _element;

        public IEnumerator<List<int>?> GetEnumerator() => new List<List<int>?> { this[0], this[1] }.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
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

    private interface IFirstKey
    {
        int Key { get; }
    }

    private interface ISecondKey
    {
        int Key { get; }
    }

    private sealed class TwoKeys : IFirstKey, ISecondKey
    {
        public int Key { get; init; }

        public bool Equals(IFirstKey? other) => other is not null && Key == other.Key;

        public bool Equals(ISecondKey? other) => other is not null && Key == other.Key;
    }

    private sealed class TwoKeysHolding
    {
        public TwoKeys? Item;
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
