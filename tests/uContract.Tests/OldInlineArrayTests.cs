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

        Assert.IsType<InvalidOperationException>(exception);
    }

    // The .NET 9+ runtime reads a struct element without dynamic code.
    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenOldCopiedUnchangedStructElementsWithoutDynamicCode_DoesNotReportThem()
    {
        Bagged bagged = new() { Bags = WithBags() };
        Bagged old = TestRuntime.WithoutDynamicCode(() => Contract.Old(() => bagged));

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(bagged, old));

        Assert.Null(exception);
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
}
