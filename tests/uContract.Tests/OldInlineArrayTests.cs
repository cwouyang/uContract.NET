using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
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

    // The inline-array field of the type that DefineFieldKeywordHolder emits.
    private const string EmittedBufField = "Buf";

    // The patterns that excuse that field, passed to EnsureAssignable called through reflection.
    private static readonly string[] EmittedBufPatterns = ["^Buf$"];

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
    // Old leaves the struct elements as it copies a field, so the references inside them stay shared with the
    // original, and the comparison refuses that inline array of the copy, as on the .NET 9 runtime or later.
    [RuntimeHelpersBoxFact(exists: false)]
    public void EnsureAssignable_WhenOldLeftStructElementsUncopiedWithoutDynamicCode_CannotCompareThem()
    {
        Bagged bagged = new() { Bags = WithBags() };
        Bagged old = TestRuntime.WithoutDynamicCode(() => Contract.Old(() => bagged));

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(bagged, old));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(OldCouldNotCopy<BagBuf3, Bag>("Bagged.Bags", "Bags"), cannotCompare.Message);
    }

    // A hand-written Equals asked before the elements are read would find the elements Old left shared equal, so on
    // the .NET 8 runtime without dynamic code too the refusal comes first: for an inline array with its own Equals,
    // and for a struct with its own Equals that holds one by value.
    [RuntimeHelpersBoxFact(exists: false)]
    public void EnsureAssignable_WhenOldLeftTheElementsOfAnInlineArrayWithItsOwnEqualsUncopiedOnDotNet8_CannotCompareItFirst()
    {
        TolerantBagged bagged = new() { Bags = WithTolerantBags() };
        TolerantBagged old = TestRuntime.WithoutDynamicCode(() => Contract.Old(() => bagged));
        bagged.Bags[2].Items!.Add(Added);

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(bagged, old));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(OldCouldNotCopy<TolerantBagBuf3, Bag>("TolerantBagged.Bags", "Bags"), cannotCompare.Message);
    }

    [RuntimeHelpersBoxFact(exists: false)]
    public void EnsureAssignable_WhenAnUnchangedInlineArrayWithItsOwnEqualsIsUncopiedOnDotNet8_StillCannotCompareIt()
    {
        TolerantBagged bagged = new() { Bags = WithTolerantBags() };
        TolerantBagged old = TestRuntime.WithoutDynamicCode(() => Contract.Old(() => bagged));

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(bagged, old));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(OldCouldNotCopy<TolerantBagBuf3, Bag>("TolerantBagged.Bags", "Bags"), cannotCompare.Message);
    }

    [RuntimeHelpersBoxFact(exists: false)]
    public void EnsureAssignable_WhenOldLeftAnInlineArrayInAContentComparingStructUncopiedOnDotNet8_CannotCompareItFirst()
    {
        ContentHolding holding = WithContentBags();
        ContentHolding old = TestRuntime.WithoutDynamicCode(() => Contract.Old(() => holding));
        holding.Value.Bags[2].Items!.Add(Added);

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(holding, old));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(OldCouldNotCopy<BagBuf3, Bag>("ContentHolding.Value.Bags", "Value"), cannotCompare.Message);
    }

    [RuntimeHelpersBoxFact(exists: false)]
    public void EnsureAssignable_WhenAnUnchangedInlineArrayInAContentComparingStructIsUncopiedOnDotNet8_StillCannotCompareIt()
    {
        ContentHolding holding = WithContentBags();
        ContentHolding old = TestRuntime.WithoutDynamicCode(() => Contract.Old(() => holding));

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(holding, old));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(OldCouldNotCopy<BagBuf3, Bag>("ContentHolding.Value.Bags", "Value"), cannotCompare.Message);
    }

    // With dynamic code Old copies every element, and the content-comparing Equals sees the change.
    [Fact]
    public void EnsureAssignable_WhenAnInlineArrayInAContentComparingStructChangedAfterOldWithDynamicCode_ReportsTheField()
    {
        ContentHolding holding = WithContentBags();
        ContentHolding old = Contract.Old(() => holding);
        holding.Value.Bags[2].Items!.Add(Added);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(holding, old)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
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

    // A field declared object holds a box, a reference: the struct-T rule does not reach through it, and the record on
    // the box of an Old copy decides.
    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenTIsAStructWhoseObjectFieldHoldsSuchAnInlineArrayNotFromOldWithoutDynamicCode_ReportsTheField()
    {
        BoxHolder actual = new() { Value = WithBags() };
        BagBuf3 changed = WithBags();
        changed[2].Items!.Add(Added);
        BoxHolder expected = new() { Value = changed };

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        PostconditionViolationException violation = Assert.IsType<PostconditionViolationException>(exception);
        Assert.Equal(Violation + "  - Value", violation.Description);
    }

    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenTIsAStructWhoseObjectFieldHoldsSuchAnInlineArrayFromOldWithoutDynamicCode_CannotCompareIt()
    {
        BoxHolder held = new() { Value = WithBags() };
        BoxHolder old = TestRuntime.WithoutDynamicCode(() => Contract.Old(() => held));

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(held, old));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(OldCouldNotCopy<BagBuf3, Bag>("BoxHolder.Value", "Value"), cannotCompare.Message);
    }

    // A property's getter may box a field afresh on every read, and such a box carries no record of the Old copy, so a
    // property, whatever its declared type, is held where its holder is held.
    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenAnObjectPropertyOfAClassBoxesSuchAnInlineArrayFieldOfAnOldCopyWithoutDynamicCode_CannotCompareIt()
    {
        BoxingClass boxing = new() { Buf = WithBags() };
        BoxingClass old = TestRuntime.WithoutDynamicCode(() => Contract.Old(() => boxing));
        boxing.Buf[2].Items!.Add(Added);

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(boxing, old, "^Buf$"));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(OldCouldNotCopy<BagBuf3, Bag>("BoxingClass.Boxed", "Boxed"), cannotCompare.Message);
    }

    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenAnObjectPropertyOfAStructTBoxesSuchAnInlineArrayFieldOfAnOldCopyWithoutDynamicCode_CannotCompareIt()
    {
        BoxingStruct boxing = new() { Buf = WithBags() };
        BoxingStruct old = TestRuntime.WithoutDynamicCode(() => Contract.Old(() => boxing));
        boxing.Buf[2].Items!.Add(Added);

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(boxing, old, "^Buf$"));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(OldCouldNotCopy<BagBuf3, Bag>("BoxingStruct.Boxed", "Boxed"), cannotCompare.Message);
    }

    // Known limitation (revision 18): the elements of a sequence that a struct T holds by value are not tracked by
    // their declared type, so a box among them is refused as if T held it by value, also when not from Old.
    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenTIsAStructWhoseImmutableArrayOfObjectsHoldsSuchAnInlineArrayNotFromOldWithoutDynamicCode_CannotCompareIt()
    {
        ImmutableBoxes actual = new() { Items = [WithBags()] };
        ImmutableBoxes expected = new() { Items = [WithBags()] };

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(OldCouldNotCopy<BagBuf3, Bag>("ImmutableBoxes.Items[]", "Items"), cannotCompare.Message);
    }

    // An auto-property, and a positional member of a record struct, is judged as its backing field: declared object,
    // it holds a box through a reference.
    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenTIsAStructWhoseObjectAutoPropertyHoldsSuchAnInlineArrayNotFromOldWithoutDynamicCode_ComparesIt()
    {
        AutoPropHolder actual = new() { Value = WithBags() };
        AutoPropHolder expected = new() { Value = WithBags() };

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenTIsARecordStructWhosePositionalObjectMemberHoldsSuchAnInlineArrayNotFromOldWithoutDynamicCode_ComparesIt()
    {
        PositionalHolder actual = new(WithBags());
        PositionalHolder expected = new(WithBags());

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenTIsAStructWhoseObjectAutoPropertyHoldsSuchAnInlineArrayChangedInPlaceAfterOldWithoutDynamicCode_CannotCompareIt()
    {
        AutoPropHolder held = new() { Value = WithBags() };
        AutoPropHolder old = TestRuntime.WithoutDynamicCode(() => Contract.Old(() => held));
        ((BagBuf3)held.Value!)[2].Items!.Add(Added);

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(held, old));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(OldCouldNotCopy<BagBuf3, Bag>("AutoPropHolder.Value", "Value"), cannotCompare.Message);
    }

    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenTIsARecordStructWhosePositionalObjectMemberHoldsSuchAnInlineArrayChangedInPlaceAfterOldWithoutDynamicCode_CannotCompareIt()
    {
        PositionalHolder held = new(WithBags());
        PositionalHolder old = TestRuntime.WithoutDynamicCode(() => Contract.Old(() => held));
        ((BagBuf3)held.Value)[2].Items!.Add(Added);

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(held, old));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(OldCouldNotCopy<BagBuf3, Bag>("PositionalHolder.Value", "Value"), cannotCompare.Message);
    }

    // A property with a field named as a backing field but a getter written by hand (a C# 14 "field" property with a
    // custom getter) may box afresh on every read, so it counts as computed. The SDK 8 compiler has no "field"
    // keyword; the type is emitted: its getter boxes Buf on every read.
    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenAPropertyWithABackingFieldNameHasAHandWrittenGetterThatBoxesAnExcusedFieldOfAnOldCopy_CannotCompareIt()
    {
        Type type = DefineFieldKeywordHolder(asStruct: false);
        object held = Activator.CreateInstance(type)!;
        type.GetField(EmittedBufField)!.SetValue(held, WithPublicBags());
        object old = TestRuntime.WithoutDynamicCode(() => Contract.Old(() => held));
        ((PublicBagBuf3)type.GetField(EmittedBufField)!.GetValue(held)!)[2].Items!.Add(Added);

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(held, old, "^Buf$"));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(
            OldCouldNotCopy<PublicBagBuf3, PublicBag>("FieldKeywordHolder.Value", "Value"),
            cannotCompare.Message
        );
    }

    // Pin (passed on its first run): the same shape as a struct T. Its Old copy is unboxed when passed as T, so the rule
    // for a struct T refuses the property, which counts as computed.
    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenTIsAStructWhosePropertyWithABackingFieldNameHasAHandWrittenGetterThatBoxesAnExcusedField_CannotCompareIt()
    {
        Type type = DefineFieldKeywordHolder(asStruct: true);
        object held = Activator.CreateInstance(type)!;
        type.GetField(EmittedBufField)!.SetValue(held, WithPublicBags());
        object old = TestRuntime.WithoutDynamicCode(() => Contract.Old(() => held));
        ((PublicBagBuf3)type.GetField(EmittedBufField)!.GetValue(held)!)[2].Items!.Add(Added);
        MethodInfo ensureAssignable = typeof(Contract)
            .GetMethod(nameof(Contract.EnsureAssignable))!
            .MakeGenericMethod(type);

        Exception? exception = TestRuntime.WithoutDynamicCode(() =>
            Record.Exception(() => ensureAssignable.Invoke(null, [held, old, EmittedBufPatterns]))
        );

        TargetInvocationException invocation = Assert.IsType<TargetInvocationException>(exception);
        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(invocation.InnerException);
        Assert.Equal(
            OldCouldNotCopy<PublicBagBuf3, PublicBag>("FieldKeywordStruct.Value", "Value"),
            cannotCompare.Message
        );
    }

    // public class FieldKeywordHolder (or struct FieldKeywordStruct) { public PublicBagBuf3 Buf;
    //     private object <Value>k__BackingField; public object Value { get => (object)Buf; } }, with a getter that is
    // not marked [CompilerGenerated].
    private static Type DefineFieldKeywordHolder(bool asStruct)
    {
        string name = $"Emitted{Guid.NewGuid():N}";
        ModuleBuilder module = AssemblyBuilder
            .DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run)
            .DefineDynamicModule(name);
        TypeBuilder builder = asStruct
            ? module.DefineType(
                "Emitted.FieldKeywordStruct",
                TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout,
                typeof(ValueType)
            )
            : module.DefineType(
                "Emitted.FieldKeywordHolder",
                TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class
            );
        FieldBuilder buf = builder.DefineField(EmittedBufField, typeof(PublicBagBuf3), FieldAttributes.Public);
        builder.DefineField("<Value>k__BackingField", typeof(object), FieldAttributes.Private);
        MethodBuilder getter = builder.DefineMethod(
            "get_Value",
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            typeof(object),
            Type.EmptyTypes
        );
        ILGenerator il = getter.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, buf);
        il.Emit(OpCodes.Box, typeof(PublicBagBuf3));
        il.Emit(OpCodes.Ret);
        builder.DefineProperty("Value", PropertyAttributes.None, typeof(object), Type.EmptyTypes).SetGetMethod(getter);
        if (!asStruct)
        {
            builder.DefineDefaultConstructor(MethodAttributes.Public);
        }

        return builder.CreateType();
    }

    private static PublicBagBuf3 WithPublicBags()
    {
        PublicBagBuf3 buffer = new();
        buffer[0] = new PublicBag { Items = [1] };
        buffer[1] = new PublicBag { Items = [2] };
        buffer[2] = new PublicBag { Items = [3] };
        return buffer;
    }

    // Known limitation (revision 19): Old records the objects it copies; an object a computed property builds from a
    // field on every read carries no record, so with that field excused, an in-place change of the elements Old left
    // shared passes through the property. Way out: list the computed property as assignable too.
    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenAComputedPropertyWrapsAnExcusedFieldWhoseElementsOldCouldNotCopy_PassesAnInPlaceChangeAsAKnownLimitation()
    {
        WrappingClass wrapping = new() { Buf = WithBags() };
        WrappingClass old = TestRuntime.WithoutDynamicCode(() => Contract.Old(() => wrapping));
        wrapping.Buf[2].Items!.Add(Added);

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(wrapping, old, "^Buf$"));

        Assert.Null(exception);
    }

    // An element declared object of an inline array that T holds by value is a reference too.
    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenTIsAStructWhoseInlineArrayOfObjectsHoldsSuchAnInlineArrayNotFromOldWithoutDynamicCode_ReportsTheField()
    {
        ObjectsHolder actual = new();
        actual.Items[0] = WithBags();
        ObjectsHolder expected = new();
        BagBuf3 changed = WithBags();
        changed[2].Items!.Add(Added);
        expected.Items[0] = changed;

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        PostconditionViolationException violation = Assert.IsType<PostconditionViolationException>(exception);
        Assert.Equal(Violation + "  - Items", violation.Description);
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
        return OldCouldNotCopyAtTheTopLevel<TInlineArray, TElement>(typeof(TInlineArray).Name);
    }

    private static string OldCouldNotCopyAtTheTopLevel<TInlineArray, TElement>(string path)
    {
        return $"EnsureAssignable cannot compare {typeof(TInlineArray)} (reached through '{path}'): "
            + $"it is an inline array of {typeof(TElement)}, a struct, whose elements Contract.Old could not copy: this "
            + "runtime writes struct elements only with dynamic code, and dynamic code is not supported here (Native "
            + "AOT), so the copy shares what they refer to with the original. Ways out: set DBC_POST=off (disables all "
            + "postcondition checks).";
    }

    // A nullable struct T is a struct T: the inline array it holds by value is refused, whether or not from Old.
    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenTIsANullableStructHoldingSuchAnInlineArrayByValueWithoutDynamicCode_CannotCompareIt()
    {
        BagHolder? actual = new BagHolder { Bags = WithBags() };
        BagHolder? expected = new BagHolder { Bags = WithBags() };

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(
            $"EnsureAssignable cannot compare {typeof(BagBuf3)} (reached through 'BagHolder.Bags'): it is an inline "
                + $"array of {typeof(Bag)}, a struct, whose elements Contract.Old could not copy: this runtime writes "
                + "struct elements only with dynamic code, and dynamic code is not supported here (Native AOT), so the "
                + "copy shares what they refer to with the original. Ways out: set DBC_POST=off (disables all "
                + "postcondition checks).",
            cannotCompare.Message
        );
    }

    // Nullable<> struct elements that hold references are left uncopied as other struct elements are.
    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenOldCouldNotCopyNullableStructElementsWithoutDynamicCode_CannotCompareThem()
    {
        NBagged bagged = WithSecondNullableBag();
        NBagged old = TestRuntime.WithoutDynamicCode(() => Contract.Old(() => bagged));

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(bagged, old));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(OldCouldNotCopy<NBagBuf3, Bag?>("NBagged.Bags", "Bags"), cannotCompare.Message);
    }

    // A Nullable<> element without a value reads as null, as an array element does, and stays null in the copy.
    [Fact]
    public void Old_WhenNullableStructElementsHaveNoValue_CopiesThemAsNoValue()
    {
        NBagged bagged = WithSecondNullableBag();

        NBagged old = Contract.Old(() => bagged);

        Assert.Null(old.Bags[0]);
        Assert.Null(old.Bags[2]);
        Assert.NotSame(bagged.Bags[1]!.Value.Items, old.Bags[1]!.Value.Items);
    }

    [Fact]
    public void EnsureAssignable_WhenNullableStructElementsWithoutAValueAreUnchangedAfterOld_DoesNotReportThem()
    {
        NBagged bagged = WithSecondNullableBag();
        NBagged old = Contract.Old(() => bagged);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(bagged, old));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTheListOfTheOnlyNullableStructElementWithAValueChangedInPlaceAfterOld_ReportsTheField()
    {
        NBagged bagged = WithSecondNullableBag();
        NBagged old = Contract.Old(() => bagged);
        bagged.Bags[1]!.Value.Items!.Add(Added);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(bagged, old)
        );

        Assert.Equal(Violation + "  - Bags", exception.Description);
    }

    private static NBagged WithSecondNullableBag()
    {
        NBagged bagged = new();
        bagged.Bags[1] = new Bag { Items = [2] };
        return bagged;
    }

    // A hand-written Equals over elements Old left shared would find them equal, so the refusal comes first.
    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenOldLeftTheElementsOfAnInlineArrayWithItsOwnEqualsUncopied_CannotCompareItFirst()
    {
        TolerantBagged bagged = new() { Bags = WithTolerantBags() };
        TolerantBagged old = TestRuntime.WithoutDynamicCode(() => Contract.Old(() => bagged));
        bagged.Bags[2].Items!.Add(Added);

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(bagged, old));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(OldCouldNotCopy<TolerantBagBuf3, Bag>("TolerantBagged.Bags", "Bags"), cannotCompare.Message);
    }

    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenOldLeftAnInlineArrayInAStructWithItsOwnEqualsUncopied_CannotCompareItFirst()
    {
        TolerantHolding holding = new() { Value = new TolerantHolder { Bags = WithBags() } };
        TolerantHolding old = TestRuntime.WithoutDynamicCode(() => Contract.Old(() => holding));
        holding.Value.Bags[2].Items!.Add(Added);

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(holding, old));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(OldCouldNotCopy<BagBuf3, Bag>("TolerantHolding.Value.Bags", "Value"), cannotCompare.Message);
    }

    // A class is compared by its members whatever its Equals says (its Equals is asked only when reflection shows none
    // of its members, under Native AOT), so the walk meets the inline array and refuses it.
    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenOldLeftAnInlineArrayInAClassWithItsOwnEqualsUncopied_CannotCompareIt()
    {
        TolerantClassHolding holding = new() { Value = new TolerantClass { Bags = WithBags() } };
        TolerantClassHolding old = TestRuntime.WithoutDynamicCode(() => Contract.Old(() => holding));
        holding.Value!.Bags[2].Items!.Add(Added);

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(holding, old));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(OldCouldNotCopy<BagBuf3, Bag>("TolerantClassHolding.Value.Bags", "Value"), cannotCompare.Message);
    }

    [Fact]
    public void EnsureAssignable_WhenAnInlineArrayWithItsOwnEqualsChangedAfterOldWithDynamicCode_LetsItsEqualsDecide()
    {
        TolerantBagged bagged = new() { Bags = WithTolerantBags() };
        TolerantBagged old = Contract.Old(() => bagged);
        bagged.Bags[2].Items!.Add(Added);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(bagged, old));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenAStructWithItsOwnEqualsHoldingAnInlineArrayChangedAfterOldWithDynamicCode_LetsItsEqualsDecide()
    {
        TolerantHolding holding = new() { Value = new TolerantHolder { Bags = WithBags() } };
        TolerantHolding old = Contract.Old(() => holding);
        holding.Value.Bags[2].Items!.Add(Added);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(holding, old));

        Assert.Null(exception);
    }

    // An outer struct whose hand-written Equals delegates to an inner struct's, which compares the lists' contents,
    // would find elements Old left shared equal, so the look for an uncopied inline array goes through every by-value
    // struct, whatever its Equals.
    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenOldLeftAnInlineArrayBelowTwoStructsWithTheirOwnEqualsUncopied_CannotCompareItFirst()
    {
        DelegatingHolding holding = WithDelegatingBags();
        DelegatingHolding old = TestRuntime.WithoutDynamicCode(() => Contract.Old(() => holding));
        holding.Value.Inner.Bags[2].Items!.Add(Added);

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(holding, old));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(
            OldCouldNotCopy<BagBuf3, Bag>("DelegatingHolding.Value.Inner.Bags", "Value"),
            cannotCompare.Message
        );
    }

    [Fact]
    public void EnsureAssignable_WhenAnInlineArrayBelowTwoStructsWithTheirOwnEqualsChangedAfterOldWithDynamicCode_LetsTheirEqualsReportIt()
    {
        DelegatingHolding holding = WithDelegatingBags();
        DelegatingHolding old = Contract.Old(() => holding);
        holding.Value.Inner.Bags[2].Items!.Add(Added);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(holding, old)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenAnInlineArrayBelowATolerantEqualsTwoStructsDeepChangedAfterOldWithDynamicCode_LetsItPass()
    {
        DelegatingTolerantHolding holding = new()
        {
            Value = new DelegatingTolerantHolder { Inner = new TolerantHolder { Bags = WithBags() } },
        };
        DelegatingTolerantHolding old = Contract.Old(() => holding);
        holding.Value.Inner.Bags[2].Items!.Add(Added);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(holding, old));

        Assert.Null(exception);
    }

    // At the top level, a boxed struct passed under T = object or an interface is compared as a member is, its Equals
    // first; the refusal of an inline array it holds by value that Old could not copy comes before that Equals too.
    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndABoxedContentComparingStructHoldsAnInlineArrayOldLeftUncopied_CannotCompareItFirst()
    {
        object holder = new ContentBagHolder { Bags = WithBags() };
        object old = TestRuntime.WithoutDynamicCode(() => Contract.Old(() => holder));
        ((ContentBagHolder)holder).Bags[2].Items!.Add(Added);

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(holder, old));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(OldCouldNotCopyAtTheTopLevel<BagBuf3, Bag>("ContentBagHolder.Bags"), cannotCompare.Message);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnInterfaceAndABoxedContentComparingStructHoldsAnInlineArrayOldLeftUncopied_CannotCompareItFirst()
    {
        IHoldsBags holder = new ContentBagHolder { Bags = WithBags() };
        IHoldsBags old = TestRuntime.WithoutDynamicCode(() => Contract.Old(() => holder));
        ((ContentBagHolder)holder).Bags[2].Items!.Add(Added);

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(holder, old));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(OldCouldNotCopyAtTheTopLevel<BagBuf3, Bag>("ContentBagHolder.Bags"), cannotCompare.Message);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndABoxedTupleHoldsAContentComparingStructWhoseInlineArrayOldLeftUncopied_CannotCompareItFirst()
    {
        ContentBagHolder held = new() { Bags = WithBags() };
        object tuple = (held, Added);
        object old = TestRuntime.WithoutDynamicCode(() => Contract.Old(() => tuple));
        held.Bags[2].Items!.Add(Added);

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(tuple, old));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(
            OldCouldNotCopyAtTheTopLevel<BagBuf3, Bag>($"{tuple.GetType().Name}.Item1.Bags"),
            cannotCompare.Message
        );
    }

    // With dynamic code Old copies every element, and the content-comparing Equals sees the change.
    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndABoxedContentComparingStructChangedAfterOldWithDynamicCode_ReportsItsField()
    {
        object holder = new ContentBagHolder { Bags = WithBags() };
        object old = Contract.Old(() => holder);
        ((ContentBagHolder)holder).Bags[2].Items!.Add(Added);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(holder, old)
        );

        Assert.Equal(ListedFromContentBagHolder("Bags", typeof(object)), exception.Description);
    }

    // Values that did not come from Old carry no record: the Equals decides as under the JIT.
    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenTIsObjectAndBoxedContentComparingStructsNotFromOldDifferWithoutDynamicCode_ReportsTheField()
    {
        object actual = new ContentBagHolder { Bags = WithBags() };
        ContentBagHolder changed = new() { Bags = WithBags() };
        changed.Bags[2].Items!.Add(Added);
        object expected = changed;

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        PostconditionViolationException violation = Assert.IsType<PostconditionViolationException>(exception);
        Assert.Equal(ListedFromContentBagHolder("Bags", typeof(object)), violation.Description);
    }

    [RuntimeHelpersBoxFact(exists: true)]
    public void EnsureAssignable_WhenTIsObjectAndEqualBoxedContentComparingStructsNotFromOldWithoutDynamicCode_Passes()
    {
        object actual = new ContentBagHolder { Bags = WithBags() };
        object expected = new ContentBagHolder { Bags = WithBags() };

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    private static string ListedFromContentBagHolder(string member, Type declaredType)
    {
        return Violation
            + $"  - {member}\nMembers were listed from the runtime type {typeof(ContentBagHolder)}, not from T "
            + $"({declaredType}).";
    }

    private static DelegatingHolding WithDelegatingBags()
    {
        return new DelegatingHolding
        {
            Value = new DelegatingHolder { Inner = new ContentBagHolder { Bags = WithBags() } },
        };
    }

    private static ContentHolding WithContentBags()
    {
        return new ContentHolding { Value = new ContentBagHolder { Bags = WithBags() } };
    }

    private static TolerantBagBuf3 WithTolerantBags()
    {
        TolerantBagBuf3 buffer = new();
        buffer[0] = new Bag { Items = [1] };
        buffer[1] = new Bag { Items = [2] };
        buffer[2] = new Bag { Items = [3] };
        return buffer;
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

    private struct BoxHolder
    {
        public object? Value;
    }

    private struct ImmutableBoxes
    {
        public ImmutableArray<object> Items;
    }

    // Public, so that the type emitted by DefineFieldKeywordHolder can hold them.
    public struct PublicBag
    {
        public IList<int>? Items { get; set; }
    }

    [InlineArray(3)]
    public struct PublicBagBuf3
    {
        private PublicBag _element;
    }

    private struct AutoPropHolder
    {
        public object? Value { get; set; }
    }

    private readonly record struct PositionalHolder(object Value);

    // AsArray builds a new array that holds a box of Buf on every read.
    private sealed class WrappingClass
    {
        public BagBuf3 Buf;

        public object[] AsArray => [Buf];
    }

    // Boxed boxes Buf afresh on every read.
    private sealed class BoxingClass
    {
        public BagBuf3 Buf;

        public object Boxed => Buf;
    }

    private struct BoxingStruct
    {
        public BagBuf3 Buf;

        public readonly object Boxed => Buf;
    }

    [InlineArray(2)]
    private struct ObjectBuf2
    {
        private object? _element;
    }

    private struct ObjectsHolder
    {
        public ObjectBuf2 Items;
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

    [InlineArray(3)]
    private struct NBagBuf3
    {
        private Bag? _element;
    }

    private sealed class NBagged
    {
        public NBagBuf3 Bags;
    }

    // Each Equals below says "equal" to any value of its type.
    [InlineArray(3)]
    private struct TolerantBagBuf3
    {
        private Bag _element;

        public override readonly bool Equals(object? obj) => obj is TolerantBagBuf3;

        public override readonly int GetHashCode() => 0;
    }

    private sealed class TolerantBagged
    {
        public TolerantBagBuf3 Bags;
    }

    private struct TolerantHolder
    {
        public BagBuf3 Bags;

        public override readonly bool Equals(object? obj) => obj is TolerantHolder;

        public override readonly int GetHashCode() => 0;
    }

    private sealed class TolerantHolding
    {
        public TolerantHolder Value;
    }

    private sealed class TolerantClass
    {
        public BagBuf3 Bags;

        public override bool Equals(object? obj) => obj is TolerantClass;

        public override int GetHashCode() => 0;
    }

    private sealed class TolerantClassHolding
    {
        public TolerantClass? Value;
    }

    private interface IHoldsBags;

    // Its Equals compares the contents of the elements' lists, so two values sharing their lists are "equal".
    private struct ContentBagHolder : IHoldsBags
    {
        private const int BagCount = 3;

        public BagBuf3 Bags;

        public override readonly bool Equals(object? obj) =>
            obj is ContentBagHolder other && SameItems(Bags, other.Bags);

        public override readonly int GetHashCode() => 0;

        private static bool SameItems(BagBuf3 left, BagBuf3 right)
        {
            for (int i = 0; i < BagCount; i++)
            {
                if (!(left[i].Items ?? []).SequenceEqual(right[i].Items ?? []))
                {
                    return false;
                }
            }

            return true;
        }
    }

    private sealed class ContentHolding
    {
        public ContentBagHolder Value;
    }

    // Its Equals delegates to Inner's.
    private struct DelegatingHolder
    {
        public ContentBagHolder Inner;

        public override readonly bool Equals(object? obj) => obj is DelegatingHolder other && Inner.Equals(other.Inner);

        public override readonly int GetHashCode() => 0;
    }

    private sealed class DelegatingHolding
    {
        public DelegatingHolder Value;
    }

    // Its Equals delegates to Inner's, which says "equal" to any value of its type.
    private struct DelegatingTolerantHolder
    {
        public TolerantHolder Inner;

        public override readonly bool Equals(object? obj) =>
            obj is DelegatingTolerantHolder other && Inner.Equals(other.Inner);

        public override readonly int GetHashCode() => 0;
    }

    private sealed class DelegatingTolerantHolding
    {
        public DelegatingTolerantHolder Value;
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
