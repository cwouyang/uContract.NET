using System.Runtime.CompilerServices;
using uContract.Exceptions;

namespace uContract.Tests;

// Old with inline arrays. An Old copy of a value that holds an inline array copies every element the way an array
// element is copied, so EnsureAssignable(x, old) passes when nothing changed and reports an in-place change to any
// element. Some tests turn dynamic code off, process-wide state, so this class shares the collection of the other
// tests that mutate process-wide state.
[Collection("EnvironmentVariables")]
public class OldInlineArrayTests
{
    private const string Violation = "Fields were modified that are not marked as assignable:\n";

    private const int Added = 7;

    [Fact]
    public void EnsureAssignable_WhenALaterListElementOfAnInlineArrayFieldChangedInPlaceAfterOld_ReportsTheField()
    {
        Listed listed = new() { Lists = WithLists() };
        Listed old = Contract.Old(() => listed);
        listed.Lists[2]!.Add(Added);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(listed, old)
        );

        Assert.Equal(Violation + "  - Lists", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTheListOfALaterStructElementChangedInPlaceAfterOld_ReportsTheField()
    {
        Bagged bagged = new() { Bags = WithBags() };
        Bagged old = Contract.Old(() => bagged);
        bagged.Bags[2].Items!.Add(Added);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(bagged, old)
        );

        Assert.Equal(Violation + "  - Bags", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenAnInlineArrayOfListsIsUnchangedAfterOld_DoesNotReportIt()
    {
        Listed listed = new() { Lists = WithLists() };
        Listed old = Contract.Old(() => listed);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(listed, old));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTheFirstListElementChangedInPlaceAfterOld_ReportsTheField()
    {
        Listed listed = new() { Lists = WithLists() };
        Listed old = Contract.Old(() => listed);
        listed.Lists[0]!.Add(Added);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(listed, old)
        );

        Assert.Equal(Violation + "  - Lists", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenALaterInnerListOfAnInlineArrayOfInlineArraysChangedInPlaceAfterOld_ReportsTheField()
    {
        Layered layered = new();
        layered.Layers[0] = WithLists();
        layered.Layers[1] = WithLists();
        Layered old = Contract.Old(() => layered);
        layered.Layers[1][2]!.Add(Added);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(layered, old)
        );

        Assert.Equal(Violation + "  - Layers", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenALaterIntElementWasReassignedAfterOld_ReportsTheField()
    {
        Counted counted = new();
        Counted old = Contract.Old(() => counted);
        counted.Counts[2] = Added;

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(counted, old)
        );

        Assert.Equal(Violation + "  - Counts", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenALaterListOfAnInlineArrayInAnArrayChangedInPlaceAfterOld_ReportsTheField()
    {
        ListedRows rows = new() { Rows = [WithLists()] };
        ListedRows old = Contract.Old(() => rows);
        rows.Rows[0][2]!.Add(Added);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(rows, old)
        );

        Assert.Equal(Violation + "  - Rows", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenALaterListOfAnInlineArrayInAListChangedInPlaceAfterOld_ReportsTheField()
    {
        ListedEntries entries = new() { Entries = [WithLists()] };
        ListedEntries old = Contract.Old(() => entries);
        ListBuf3 entry = entries.Entries[0];
        entry[2]!.Add(Added);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(entries, old)
        );

        Assert.Equal(Violation + "  - Entries", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenALaterListOfAnInlineArrayHeldAsAnObjectChangedInPlaceAfterOld_ReportsTheField()
    {
        Boxed boxed = new() { Value = WithLists() };
        Boxed old = Contract.Old(() => boxed);
        ListBuf3 lists = (ListBuf3)boxed.Value!;
        lists[2]!.Add(Added);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(boxed, old)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnInlineArrayAndALaterListChangedInPlaceAfterOld_ReportsTheTwoAsNotEqual()
    {
        ListBuf3 lists = WithLists();
        ListBuf3 old = Contract.Old(() => lists);
        lists[2]!.Add(Added);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(lists, old)
        );

        Assert.Equal(
            $"actual and expected are not equal. {typeof(ListBuf3)} is an inline array, compared as a whole element "
                + "by element, so assignable patterns do not apply.",
            exception.Description
        );
    }

    // The .NET 8 runtime reads and writes a struct element only with dynamic code, which Native AOT does not support:
    // Old leaves the struct elements as it copies a field, and the comparison cannot compare them.
    [RuntimeHelpersBoxFact(exists: false)]
    public void EnsureAssignable_WhenOldCopiedStructElementsWithoutDynamicCode_CannotCompareThem()
    {
        Bagged bagged = new() { Bags = WithBags() };
        Bagged old = TestRuntime.WithoutDynamicCode(() => Contract.Old(() => bagged));

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(bagged, old));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(
            $"EnsureAssignable cannot compare {typeof(BagBuf3)} (reached through 'Bagged.Bags'): it is an inline array "
                + $"of {typeof(Bag)}, a struct, whose elements this runtime reads only with dynamic code, and dynamic "
                + "code is not supported here (Native AOT). Ways out: list 'Bags' as assignable (patterns are regular "
                + "expressions matched against top-level member names, so use the plain member name); run on the "
                + ".NET 9 runtime or later, which reads struct elements without dynamic code; or set DBC_POST=off "
                + "(disables all postcondition checks).",
            cannotCompare.Message
        );
    }

    // The .NET 9+ runtime reads a struct element without dynamic code but cannot write one back, so Old leaves the
    // references inside later struct elements shared with the original, and the comparison of that copy refuses.
    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenOldCouldNotCopyUnchangedStructElementsWithoutDynamicCode_CannotCompareThem()
    {
        Bagged bagged = new() { Bags = WithBags() };
        Bagged old = TestRuntime.WithoutDynamicCode(() => Contract.Old(() => bagged));

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(bagged, old));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(OldCouldNotCopy<BagBuf3, Bag>("Bagged.Bags", "Bags"), cannotCompare.Message);
    }

    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenOldCouldNotCopyStructElementsWithoutDynamicCodeAndALaterListChanged_CannotCompareThem()
    {
        Bagged bagged = new() { Bags = WithBags() };
        Bagged old = TestRuntime.WithoutDynamicCode(() => Contract.Old(() => bagged));
        bagged.Bags[2].Items!.Add(Added);

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(bagged, old));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(OldCouldNotCopy<BagBuf3, Bag>("Bagged.Bags", "Bags"), cannotCompare.Message);
    }

    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenOldCouldNotCopyStructElementsOfAnInlineArrayInAListWithoutDynamicCode_CannotCompareThem()
    {
        BaggedEntries entries = new() { Entries = [WithBags()] };
        BaggedEntries old = TestRuntime.WithoutDynamicCode(() => Contract.Old(() => entries));

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(entries, old));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(OldCouldNotCopy<BagBuf3, Bag>("BaggedEntries.Entries[]", "Entries"), cannotCompare.Message);
    }

    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenOldCouldNotCopyStructElementsOfAnInlineArrayHeldAsAnObjectWithoutDynamicCode_CannotCompareThem()
    {
        Boxed boxed = new() { Value = WithBags() };
        Boxed old = TestRuntime.WithoutDynamicCode(() => Contract.Old(() => boxed));

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(boxed, old));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(OldCouldNotCopy<BagBuf3, Bag>("Boxed.Value", "Value"), cannotCompare.Message);
    }

    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenTIsObjectAndOldCouldNotCopyTheStructElementsOfABoxedInlineArray_CannotCompareThem()
    {
        object bags = WithBags();
        object old = TestRuntime.WithoutDynamicCode(() => Contract.Old(() => bags));

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(bags, old));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(
            $"EnsureAssignable cannot compare {typeof(BagBuf3)} (reached through 'BagBuf3'): it is an inline array of "
                + $"{typeof(Bag)}, a struct, whose elements Contract.Old could not copy: this runtime writes struct "
                + "elements only with dynamic code, and dynamic code is not supported here (Native AOT), so the copy "
                + "shares what they refer to with the original. Ways out: set DBC_POST=off (disables all "
                + "postcondition checks).",
            cannotCompare.Message
        );
    }

    // Struct elements that hold no reference are copied whole with the inline array, so nothing is left uncopied.
    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenOldCopiedUnchangedStructElementsWithoutReferencesWithoutDynamicCode_DoesNotReportThem()
    {
        Plotted plotted = new() { Points = WithPoints() };
        Plotted old = TestRuntime.WithoutDynamicCode(() => Contract.Old(() => plotted));

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(plotted, old));

        Assert.Null(exception);
    }

    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenALaterStructElementWithoutReferencesChangedAfterOldWithoutDynamicCode_ReportsTheField()
    {
        Plotted plotted = new() { Points = WithPoints() };
        Plotted old = TestRuntime.WithoutDynamicCode(() => Contract.Old(() => plotted));
        plotted.Points[2] = new Point { X = Added };

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(plotted, old));

        PostconditionViolationException violation = Assert.IsType<PostconditionViolationException>(exception);
        Assert.Equal(Violation + "  - Points", violation.Description);
    }

    // Only an Old copy records what it could not copy: two values built apart are compared as usual.
    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenExpectedIsNotAnOldCopyAndALaterStructElementDiffersWithoutDynamicCode_ReportsTheField()
    {
        Bagged actual = new() { Bags = WithBags() };
        Bagged expected = new() { Bags = WithBags() };
        expected.Bags[2].Items!.Add(Added);

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        PostconditionViolationException violation = Assert.IsType<PostconditionViolationException>(exception);
        Assert.Equal(Violation + "  - Bags", violation.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenALaterListOfAnInlineArrayInAHolderStructChangedInPlaceAfterOld_ReportsTheField()
    {
        HeldLists held = new() { Value = new ListHolder { Lists = WithLists() } };
        HeldLists old = Contract.Old(() => held);
        held.Value.Lists[2]!.Add(Added);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(held, old)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenALaterListOfAnInlineArrayAutoPropertyChangedInPlaceAfterOld_ReportsTheProperty()
    {
        ListedProperty listed = new() { Lists = WithLists() };
        ListedProperty old = Contract.Old(() => listed);
        ListBuf3 lists = listed.Lists;
        lists[2]!.Add(Added);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(listed, old)
        );

        Assert.Equal(Violation + "  - Lists\n  - <Lists>k__BackingField", exception.Description);
    }

    [Fact]
    public void Old_WhenTwoElementsOfAnInlineArrayShareAList_CopiesItOnceForBoth()
    {
        List<int> shared = [1];
        Listed listed = new() { Lists = WithLists() };
        listed.Lists[0] = shared;
        listed.Lists[2] = shared;

        Listed old = Contract.Old(() => listed);

        Assert.NotSame(shared, old.Lists[0]);
        Assert.Same(old.Lists[0], old.Lists[2]);
    }

    // When T is a struct, an Old copy is an unboxed value with no record, so an inline array that T holds by value and
    // whose struct elements Old could not copy is refused whether or not the values came from Old.
    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenTIsAnInlineArrayOfStructElementsHoldingReferencesWithoutDynamicCode_CannotCompareIt()
    {
        BagBuf3 actual = WithBags();
        BagBuf3 expected = WithBags();

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(OldCouldNotCopyAtTheTopLevel<BagBuf3, Bag>(), cannotCompare.Message);
    }

    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenTIsAnInlineArrayAndExpectedIsItsOldCopyWithoutDynamicCode_CannotCompareIt()
    {
        BagBuf3 bags = WithBags();
        BagBuf3 old = TestRuntime.WithoutDynamicCode(() => Contract.Old(() => bags));

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(bags, old));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(OldCouldNotCopyAtTheTopLevel<BagBuf3, Bag>(), cannotCompare.Message);
    }

    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenTIsAStructHoldingSuchAnInlineArrayByValueWithoutDynamicCode_CannotCompareTheMember()
    {
        BagHolder actual = new() { Bags = WithBags() };
        BagHolder expected = new() { Bags = WithBags() };

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(OldCouldNotCopy<BagBuf3, Bag>("BagHolder.Bags", "Bags"), cannotCompare.Message);
    }

    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenTIsAnInlineArrayOfStructElementsWithoutReferencesWithoutDynamicCode_ComparesIt()
    {
        PointBuf3 actual = WithPoints();
        PointBuf3 expected = WithPoints();
        expected[2] = new Point { X = Added };

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        PostconditionViolationException violation = Assert.IsType<PostconditionViolationException>(exception);
        Assert.Equal(
            $"actual and expected are not equal. {typeof(PointBuf3)} is an inline array, compared as a whole element "
                + "by element, so assignable patterns do not apply.",
            violation.Description
        );
    }

    // An inline array that a struct T reaches through a reference keeps the rule for Old copies: refused in a copy
    // that recorded it, compared otherwise.
    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenTIsAStructHoldingAClassWhoseInlineArrayOldCouldNotCopy_CannotCompareIt()
    {
        BaggedHolder held = new() { Inner = new Bagged { Bags = WithBags() } };
        BaggedHolder old = TestRuntime.WithoutDynamicCode(() => Contract.Old(() => held));

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(held, old));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(OldCouldNotCopy<BagBuf3, Bag>("BaggedHolder.Inner.Bags", "Inner"), cannotCompare.Message);
    }

    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenTIsAStructHoldingAClassWithSuchAnInlineArrayNotFromOldWithoutDynamicCode_ReportsTheMember()
    {
        BaggedHolder actual = new() { Inner = new Bagged { Bags = WithBags() } };
        BaggedHolder expected = new() { Inner = new Bagged { Bags = WithBags() } };
        expected.Inner.Bags[2].Items!.Add(Added);

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        PostconditionViolationException violation = Assert.IsType<PostconditionViolationException>(exception);
        Assert.Equal(Violation + "  - Inner", violation.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnInlineArrayOfStructElementsHoldingListsAndUnchangedAfterOld_DoesNotReportIt()
    {
        BagBuf3 bags = WithBags();
        BagBuf3 old = Contract.Old(() => bags);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(bags, old));

        Assert.Null(exception);
    }

    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenOldCouldNotCopyAnInlineArrayTwoStructsDeepInAClassWithoutDynamicCode_CannotCompareIt()
    {
        Deep deep = new() { Value = new Outer { Inner = new BagHolder { Bags = WithBags() } } };
        Deep old = TestRuntime.WithoutDynamicCode(() => Contract.Old(() => deep));

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(deep, old));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(OldCouldNotCopy<BagBuf3, Bag>("Deep.Value.Inner.Bags", "Value"), cannotCompare.Message);
    }

    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenOldCouldNotCopyTheInlineArraysInAnArrayWithoutDynamicCode_CannotCompareThem()
    {
        BagRows rows = new() { Rows = [WithBags(), WithBags()] };
        BagRows old = TestRuntime.WithoutDynamicCode(() => Contract.Old(() => rows));

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(rows, old));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(OldCouldNotCopy<BagBuf3, Bag>("BagRows.Rows[]", "Rows"), cannotCompare.Message);
    }

    private static string OldCouldNotCopyAtTheTopLevel<TInlineArray, TElement>()
    {
        return $"EnsureAssignable cannot compare {typeof(TInlineArray)} (reached through '{typeof(TInlineArray).Name}'): "
            + $"it is an inline array of {typeof(TElement)}, a struct, whose elements Contract.Old could not copy: this "
            + "runtime writes struct elements only with dynamic code, and dynamic code is not supported here (Native "
            + "AOT), so the copy shares what they refer to with the original. Ways out: set DBC_POST=off (disables all "
            + "postcondition checks).";
    }

    private static string OldCouldNotCopy<TInlineArray, TElement>(string path, string member)
    {
        return $"EnsureAssignable cannot compare {typeof(TInlineArray)} (reached through '{path}'): it is an inline "
            + $"array of {typeof(TElement)}, a struct, whose elements Contract.Old could not copy: this runtime writes "
            + "struct elements only with dynamic code, and dynamic code is not supported here (Native AOT), so the "
            + "copy shares what they refer to with the original. Ways out: "
            + $"list '{member}' as assignable (patterns are regular expressions matched against top-level member "
            + "names, so use the plain member name); or set DBC_POST=off (disables all postcondition checks).";
    }

    private static PointBuf3 WithPoints()
    {
        PointBuf3 buffer = new();
        buffer[0] = new Point { X = 1, Y = 1 };
        buffer[1] = new Point { X = 2, Y = 2 };
        buffer[2] = new Point { X = 3, Y = 3 };
        return buffer;
    }

    private static BagBuf3 WithBags()
    {
        BagBuf3 buffer = new();
        buffer[0] = new Bag { Items = [1] };
        buffer[1] = new Bag { Items = [2] };
        buffer[2] = new Bag { Items = [3] };
        return buffer;
    }

    private static ListBuf3 WithLists()
    {
        ListBuf3 buffer = new();
        buffer[0] = [1];
        buffer[1] = [2];
        buffer[2] = [3];
        return buffer;
    }

    [InlineArray(3)]
    private struct ListBuf3
    {
        private List<int>? _element;
    }

    private sealed class Listed
    {
        public ListBuf3 Lists;
    }

    private struct Bag
    {
        public List<int>? Items;
    }

    [InlineArray(3)]
    private struct BagBuf3
    {
        private Bag _element;
    }

    private sealed class Bagged
    {
        public BagBuf3 Bags;
    }

    [InlineArray(2)]
    private struct ListBuf3Buf2
    {
        private ListBuf3 _element;
    }

    private sealed class Layered
    {
        public ListBuf3Buf2 Layers;
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

    private sealed class ListedRows
    {
        public ListBuf3[] Rows = [];
    }

    private sealed class ListedEntries
    {
        public List<ListBuf3> Entries = [];
    }

    private sealed class Boxed
    {
        public object? Value;
    }

    private struct ListHolder
    {
        public ListBuf3 Lists;
    }

    private sealed class HeldLists
    {
        public ListHolder Value;
    }

    private sealed class ListedProperty
    {
        public ListBuf3 Lists { get; set; }
    }

    private struct BagHolder
    {
        public BagBuf3 Bags;
    }

    private struct BaggedHolder
    {
        public Bagged Inner;
    }

    private struct Outer
    {
        public BagHolder Inner;
    }

    private sealed class Deep
    {
        public Outer Value;
    }

    private sealed class BagRows
    {
        public BagBuf3[] Rows = [];
    }

    private sealed class BaggedEntries
    {
        public List<BagBuf3> Entries = [];
    }

    private struct Point
    {
        public int X;
        public int Y;
    }

    [InlineArray(3)]
    private struct PointBuf3
    {
        private Point _element;
    }

    private sealed class Plotted
    {
        public PointBuf3 Points;
    }
}
