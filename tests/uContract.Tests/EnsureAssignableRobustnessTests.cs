using System.Collections;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Reflection;
using uContract.Exceptions;

namespace uContract.Tests;

public class EnsureAssignableRobustnessTests
{
    [Fact]
    public void EnsureAssignable_WhenInterfaceMemberHoldsEqualDistinctInstances_DoesNotThrow()
    {
        PetOwner actual = new() { Pet = new Puppy { Tricks = 3 } };
        PetOwner expected = new() { Pet = new Puppy { Tricks = 3 } };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenClassMemberHoldsDifferentRuntimeTypes_ReportsTheMember()
    {
        Shelter actual = new() { Resident = new Puppy { Tricks = 1 } };
        Shelter expected = new() { Resident = new Cat { Lives = 9 } };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Resident\n  - <Resident>k__BackingField",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenSequenceMemberHoldsDifferentCollectionTypesWithEqualElements_DoesNotThrow()
    {
        List<int> list = [1, 2];
        int[] array = [1, 2];
        Scores actual = new() { Values = list };
        Scores expected = new() { Values = array };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenObjectMemberHoldsStringAndCharArrayWithSameCharacters_ReportsTheMember()
    {
        char[] characters = ['a', 'b'];
        Box actual = new() { Content = "ab" };
        Box expected = new() { Content = characters };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Content\n  - <Content>k__BackingField",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenSequenceMemberHoldsImmutableArrayAndArrayWithEqualElements_DoesNotThrow()
    {
        ImmutableArray<int> immutable = [1, 2];
        int[] array = [1, 2];
        Scores actual = new() { Values = immutable };
        Scores expected = new() { Values = array };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenObjectMemberHoldsClassInstanceAndBoxedInt_ReportsTheMember()
    {
        Box actual = new() { Content = new Elem(1) };
        Box expected = new() { Content = 1 };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Content\n  - <Content>k__BackingField",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenObjectMemberHoldsStringAndBoxedInt_ReportsTheMember()
    {
        Box actual = new() { Content = "1" };
        Box expected = new() { Content = 1 };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Content\n  - <Content>k__BackingField",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenStructMemberHoldsDistinctListsWithEqualElements_DoesNotThrow()
    {
        Satchel actual = new() { Bag = new Bag { L = [1, 2] } };
        Satchel expected = new() { Bag = new Bag { L = [1, 2] } };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenStructMemberHoldsListWithChangedElements_ReportsTheMember()
    {
        Satchel actual = new() { Bag = new Bag { L = [1, 2] } };
        Satchel expected = new() { Bag = new Bag { L = [1, 3] } };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Bag\n  - <Bag>k__BackingField",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenStructEqualsIgnoresTheDifferingField_DoesNotThrow()
    {
        Ticket actual = new()
        {
            Stub = new Stub { Id = 7, Note = "printed" },
        };
        Ticket expected = new()
        {
            Stub = new Stub { Id = 7, Note = "scanned" },
        };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenValueTupleMemberHoldsEqualDistinctInstances_DoesNotThrow()
    {
        Pair actual = new() { Items = (new Elem(1), new Elem(2)) };
        Pair expected = new() { Items = (new Elem(1), new Elem(2)) };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenImmutableArrayMemberHoldsEqualElementsInDistinctArrays_DoesNotThrow()
    {
        Shelf actual = new() { Items = [new Elem(1), new Elem(2)] };
        Shelf expected = new() { Items = [new Elem(1), new Elem(2)] };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenActualImmutableArrayIsDefaultAndExpectedIsInitialised_ReportsTheMember()
    {
        Shelf actual = new() { Items = default };
        Shelf expected = new() { Items = ImmutableArray.Create(new Elem(1)) };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Items\n  - <Items>k__BackingField",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenActualImmutableArrayIsInitialisedAndExpectedIsDefault_ReportsTheMember()
    {
        Shelf actual = new() { Items = ImmutableArray.Create(new Elem(1)) };
        Shelf expected = new() { Items = default };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Items\n  - <Items>k__BackingField",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenArraySegmentMemberHoldsEqualElementsInDistinctArrays_DoesNotThrow()
    {
        Window actual = new() { Items = new ArraySegment<Elem>([new Elem(1), new Elem(2)]) };
        Window expected = new() { Items = new ArraySegment<Elem>([new Elem(1), new Elem(2)]) };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenRecordStructMemberHoldsDistinctListsWithEqualElements_DoesNotThrow()
    {
        Ledger actual = new() { Entry = new Entry([1, 2]) };
        Ledger expected = new() { Entry = new Entry([1, 2]) };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenNullableIntAndEnumMembersAreComparedByEquals_ReportsOnlyChangedValues()
    {
        Lamp lamp = new() { Brightness = 5, Shade = Shade.Warm };
        Lamp sameLamp = new() { Brightness = 5, Shade = Shade.Warm };
        Lamp changedLamp = new() { Brightness = 6, Shade = Shade.Cool };

        Exception? unchanged = Record.Exception(() => Contract.EnsureAssignable(lamp, sameLamp));
        PostconditionViolationException changed = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(lamp, changedLamp)
        );

        Assert.Null(unchanged);
        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n"
                + "  - Brightness\n  - Shade\n  - <Brightness>k__BackingField\n  - <Shade>k__BackingField",
            changed.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenStructEqualsThrows_PropagatesTheException()
    {
        Grumpy actual = new() { Mood = new Mood { Level = 1 } };
        Grumpy expected = new() { Mood = new Mood { Level = 2 } };

        Assert.Throws<NotImplementedException>(() => Contract.EnsureAssignable(actual, expected));
    }

    [Fact]
    public void EnsureAssignable_WhenComparableMemberHoldsBoxedStructsWithEqualContent_DoesNotThrow()
    {
        Badge actual = new()
        {
            Tag = new Tag { Id = 1, L = [1, 2] },
        };
        Badge expected = new()
        {
            Tag = new Tag { Id = 1, L = [1, 2] },
        };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenObjectMemberHoldsBoxedStructsWithEqualContent_DoesNotThrow()
    {
        Box actual = new() { Content = new Bag { L = [1, 2] } };
        Box expected = new() { Content = new Bag { L = [1, 2] } };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenValueTypeMemberHoldsBoxedStructsWithEqualContent_DoesNotThrow()
    {
        ValueBox actual = new() { Content = new Bag { L = [1, 2] } };
        ValueBox expected = new() { Content = new Bag { L = [1, 2] } };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenStructWithoutFieldsIsUnequalByEquals_ReportsTheMember()
    {
        Vault actual = new() { Seal = new Seal() };
        Vault expected = new() { Seal = new Seal() };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Seal\n  - <Seal>k__BackingField",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenStructPropertyChangesOnEveryReadButFieldsAreEqual_DoesNotThrow()
    {
        Envelope actual = new() { Stamp = new Stamp { L = [1, 2] } };
        Envelope expected = new() { Stamp = new Stamp { L = [1, 2] } };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public unsafe void EnsureAssignable_WhenPointerFieldsHoldTheSameAddress_DoesNotThrow()
    {
        int target = 1;
        Cursor actual = new() { P = &target };
        Cursor expected = new() { P = &target };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public unsafe void EnsureAssignable_WhenPointerFieldsHoldDifferentAddresses_ReportsTheMember()
    {
        int first = 1;
        int second = 1;
        Cursor actual = new() { P = &first };
        Cursor expected = new() { P = &second };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal("Fields were modified that are not marked as assignable:\n  - P", exception.Description);
    }

    [Fact]
    public unsafe void EnsureAssignable_WhenStructWithPointerAndListHoldsEqualContent_DoesNotThrow()
    {
        int target = 1;
        Bookmark actual = new()
        {
            Mark = new Mark { P = &target, L = [1, 2] },
        };
        Bookmark expected = new()
        {
            Mark = new Mark { P = &target, L = [1, 2] },
        };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenHiddenTypeIsReachedThroughStructField_PathNamesTheField()
    {
        Holder actual = new() { Pouch = new Pouch { Content = new Opaque() } };
        Holder expected = new() { Pouch = new Pouch { Content = new Opaque() } };

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("'Holder.Pouch.Content'", cannotCompare.Message);
    }

    [Fact]
    public void EnsureAssignable_WhenInlineArrayElementAfterTheFirstChanges_ReportsTheMember()
    {
        Buffered actual = new();
        actual.B[0] = [1];
        actual.B[1] = [2];
        Buffered expected = new();
        expected.B[0] = [1];
        expected.B[1] = [3];

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal("Fields were modified that are not marked as assignable:\n  - B", exception.Description);
    }

    // An inline array exposes only its first element to reflection, so its fields are never compared:
    // when its Equals says "unequal", it is reported even if every element has equal content.
    [Fact]
    public void EnsureAssignable_WhenInlineArrayHoldsDistinctListsWithEqualElements_ReportsTheMember()
    {
        Buffered actual = new();
        actual.B[0] = [1];
        actual.B[1] = [2];
        Buffered expected = new();
        expected.B[0] = [1];
        expected.B[1] = [2];

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal("Fields were modified that are not marked as assignable:\n  - B", exception.Description);
    }

    // Pins a documented limitation shared with 2.0.0: ValueType.Equals and reflection see only element 0.
    [Fact]
    public void EnsureAssignable_WhenInlineArrayFirstElementIsEqual_DoesNotSeeLaterElements()
    {
        Labelled actual = new();
        actual.Labels[0] = "x";
        actual.Labels[1] = "before";
        Labelled expected = new();
        expected.Labels[0] = "x";
        expected.Labels[1] = "after";

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    // Only ArraySegment<T> and ImmutableArray<T> are compared by their elements; a user-defined inline
    // array is not, even when it is enumerable, so it follows the inline-array rule above.
    [Fact]
    public void EnsureAssignable_WhenEnumerableInlineArrayHoldsDistinctListsWithEqualElements_ReportsTheMember()
    {
        Sequenced actual = new();
        actual.S[0] = [1];
        actual.S[1] = [2];
        Sequenced expected = new();
        expected.S[0] = [1];
        expected.S[1] = [2];

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal("Fields were modified that are not marked as assignable:\n  - S", exception.Description);
    }

    [Fact]
    public unsafe void EnsureAssignable_WhenStructPointerFieldsHoldDifferentAddresses_ReportsTheMember()
    {
        int first = 1;
        int second = 1;
        Bookmark actual = new()
        {
            Mark = new Mark { P = &first, L = [1, 2] },
        };
        Bookmark expected = new()
        {
            Mark = new Mark { P = &second, L = [1, 2] },
        };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Mark\n  - <Mark>k__BackingField",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenObjectMemberHoldsBoxedStructsWithChangedListElements_ReportsTheMember()
    {
        Box actual = new() { Content = new Bag { L = [1, 2] } };
        Box expected = new() { Content = new Bag { L = [1, 3] } };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Content\n  - <Content>k__BackingField",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenEnumMemberHoldsEqualBoxedValues_DoesNotThrow()
    {
        Dial actual = new() { Setting = Shade.Warm };
        Dial expected = new() { Setting = Shade.Warm };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenEnumMemberHoldsDifferentBoxedValues_ReportsTheMember()
    {
        Dial actual = new() { Setting = Shade.Warm };
        Dial expected = new() { Setting = Shade.Cool };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Setting\n  - <Setting>k__BackingField",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenArraySegmentsDifferOnlyOutsideTheirWindow_DoesNotThrow()
    {
        Frame actual = new() { Bytes = new ArraySegment<byte>([1, 2, 3, 4], 0, 2) };
        Frame expected = new() { Bytes = new ArraySegment<byte>([1, 2, 9, 9], 0, 2) };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenArraySegmentsHoldEqualWindowsAtDifferentOffsets_DoesNotThrow()
    {
        Frame actual = new() { Bytes = new ArraySegment<byte>([1, 2, 0, 0], 0, 2) };
        Frame expected = new() { Bytes = new ArraySegment<byte>([0, 0, 1, 2], 2, 2) };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenArraySegmentElementInsideTheWindowChanges_ReportsTheMember()
    {
        Frame actual = new() { Bytes = new ArraySegment<byte>([1, 2, 3, 4], 0, 2) };
        Frame expected = new() { Bytes = new ArraySegment<byte>([1, 7, 3, 4], 0, 2) };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Bytes\n  - <Bytes>k__BackingField",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenEnumerableStructHoldsDistinctListsWithEqualFields_DoesNotThrow()
    {
        Book actual = new()
        {
            Page = new Page { Items = [1, 2], Total = 10 },
        };
        Book expected = new()
        {
            Page = new Page { Items = [1, 2], Total = 10 },
        };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenEnumerableStructFieldOutsideItsElementsChanges_ReportsTheMember()
    {
        Book actual = new()
        {
            Page = new Page { Items = [1, 2], Total = 10 },
        };
        Book expected = new()
        {
            Page = new Page { Items = [1, 2], Total = 99 },
        };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal("Fields were modified that are not marked as assignable:\n  - Page", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenExpectedSequenceIsDefaultImmutableArrayAndActualIsList_ReportsTheMember()
    {
        List<byte> list = [1];
        Frame actual = new() { Bytes = list };
        Frame expected = new() { Bytes = default(ImmutableArray<byte>) };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Bytes\n  - <Bytes>k__BackingField",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenActualSequenceIsDefaultImmutableArrayAndExpectedIsList_ReportsTheMember()
    {
        List<byte> list = [1];
        Frame actual = new() { Bytes = default(ImmutableArray<byte>) };
        Frame expected = new() { Bytes = list };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Bytes\n  - <Bytes>k__BackingField",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenDictionaryValueChangesContentItsEqualsIgnores_ReportsTheMember()
    {
        Registry actual = new() { Accounts = new(StringComparer.Ordinal) { ["alice"] = new Account(1, "Alice") } };
        Registry expected = new() { Accounts = new(StringComparer.Ordinal) { ["alice"] = new Account(1, "Alicia") } };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Accounts\n  - <Accounts>k__BackingField",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenDictionaryHoldsEqualDistinctValues_DoesNotThrow()
    {
        Catalog actual = new()
        {
            Items = new(StringComparer.Ordinal) { ["apple"] = new Elem(1), ["pear"] = new Elem(2) },
        };
        Catalog expected = new()
        {
            Items = new(StringComparer.Ordinal) { ["apple"] = new Elem(1), ["pear"] = new Elem(2) },
        };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenDictionaryHoldsDistinctListsWithEqualElements_DoesNotThrow()
    {
        Roster actual = new() { Scores = new(StringComparer.Ordinal) { ["alice"] = [90, 85] } };
        Roster expected = new() { Scores = new(StringComparer.Ordinal) { ["alice"] = [90, 85] } };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenKeyValuePairArrayHoldsEqualDistinctValues_DoesNotThrow()
    {
        Listing actual = new() { Entries = [new KeyValuePair<string, Elem>("apple", new Elem(1))] };
        Listing expected = new() { Entries = [new KeyValuePair<string, Elem>("apple", new Elem(1))] };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenDictionaryValueChanges_ReportsTheMember()
    {
        Catalog actual = new()
        {
            Items = new(StringComparer.Ordinal) { ["apple"] = new Elem(1), ["pear"] = new Elem(2) },
        };
        Catalog expected = new()
        {
            Items = new(StringComparer.Ordinal) { ["apple"] = new Elem(1), ["pear"] = new Elem(3) },
        };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Items\n  - <Items>k__BackingField",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenDictionaryKeyChanges_ReportsTheMember()
    {
        Catalog actual = new() { Items = new(StringComparer.Ordinal) { ["a"] = new Elem(1) } };
        Catalog expected = new() { Items = new(StringComparer.Ordinal) { ["b"] = new Elem(1) } };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Items\n  - <Items>k__BackingField",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenDictionaryEntryCountDiffers_ReportsTheMember()
    {
        Catalog actual = new() { Items = new(StringComparer.Ordinal) { ["apple"] = new Elem(1) } };
        Catalog expected = new()
        {
            Items = new(StringComparer.Ordinal) { ["apple"] = new Elem(1), ["pear"] = new Elem(2) },
        };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Items\n  - <Items>k__BackingField",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenHashtableHoldsEqualDistinctValues_DoesNotThrow()
    {
        Archive actual = new()
        {
            Table = new Hashtable { ["apple"] = new Elem(1), ["pear"] = new Elem(2) },
        };
        Archive expected = new()
        {
            Table = new Hashtable { ["apple"] = new Elem(1), ["pear"] = new Elem(2) },
        };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenObjectMemberHoldsDictionaryAndHashtableWithEqualEntries_DoesNotThrow()
    {
        Dictionary<string, Elem> dictionary = new(StringComparer.Ordinal) { ["apple"] = new Elem(1) };
        Hashtable hashtable = new() { ["apple"] = new Elem(1) };
        Box actual = new() { Content = dictionary };
        Box expected = new() { Content = hashtable };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenObjectMemberHoldsDictionaryAndHashtableWithChangedValue_ReportsTheMember()
    {
        Dictionary<string, Elem> dictionary = new(StringComparer.Ordinal) { ["apple"] = new Elem(1) };
        Hashtable hashtable = new() { ["apple"] = new Elem(2) };
        Box actual = new() { Content = dictionary };
        Box expected = new() { Content = hashtable };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Content\n  - <Content>k__BackingField",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenTaskMemberHoldsDistinctPendingTasks_ReportsTheMemberWithoutBlocking()
    {
        Job actual = new() { Work = new TaskCompletionSource<int>().Task };
        Job expected = new() { Work = new TaskCompletionSource<int>().Task };
        PostconditionViolationException? exception = null;

        SmallStack.OnSmallStack(
            () =>
                exception = Assert.Throws<PostconditionViolationException>(() =>
                    Contract.EnsureAssignable(actual, expected)
                ),
            timeoutMilliseconds: PendingTimeoutMilliseconds
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Work\n  - <Work>k__BackingField",
            exception!.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenLazyMemberHoldsDistinctInstances_ReportsTheMemberWithoutRunningTheFactory()
    {
        int factoryRuns = 0;
        Func<int> factory = () => ++factoryRuns;
        Deferred actual = new() { Value = new Lazy<int>(factory) };
        Deferred expected = new() { Value = new Lazy<int>(factory) };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Value\n  - <Value>k__BackingField",
            exception.Description
        );
        Assert.Equal(0, factoryRuns);
    }

    [Fact]
    public void EnsureAssignable_WhenSemaphoreMemberHoldsDistinctInstances_ReportsTheMemberWithoutCreatingAWaitHandle()
    {
        using SemaphoreSlim actualGate = new(1);
        using SemaphoreSlim expectedGate = new(1);
        Turnstile actual = new() { Gate = actualGate };
        Turnstile expected = new() { Gate = expectedGate };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Gate\n  - <Gate>k__BackingField",
            exception.Description
        );
        // Reading AvailableWaitHandle is the only way the wait handle is created; no public member reports it.
        Assert.Null(SemaphoreWaitHandle(actualGate));
        Assert.Null(SemaphoreWaitHandle(expectedGate));
    }

    [Fact]
    public void EnsureAssignable_WhenCancellationTokenMemberComesFromDistinctSources_ReportsTheMember()
    {
        using CancellationTokenSource actualSource = new();
        using CancellationTokenSource expectedSource = new();
        Request actual = new() { Cancellation = actualSource.Token };
        Request expected = new() { Cancellation = expectedSource.Token };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Cancellation\n  - <Cancellation>k__BackingField",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenValueTaskMemberWrapsDistinctPendingTasks_ReportsTheMemberWithoutBlocking()
    {
        Promise actual = new() { Pending = new ValueTask<int>(new TaskCompletionSource<int>().Task) };
        Promise expected = new() { Pending = new ValueTask<int>(new TaskCompletionSource<int>().Task) };
        PostconditionViolationException? exception = null;

        SmallStack.OnSmallStack(
            () =>
                exception = Assert.Throws<PostconditionViolationException>(() =>
                    Contract.EnsureAssignable(actual, expected)
                ),
            timeoutMilliseconds: PendingTimeoutMilliseconds
        );

        Assert.Equal("Fields were modified that are not marked as assignable:\n  - Pending", exception!.Description);
    }

    // Limitation: the state inside a shared instance is not compared, so cancelling the same source is not a change.
    [Fact]
    public void EnsureAssignable_WhenSameCancellationSourceIsCancelledInBetween_DoesNotThrow()
    {
        using CancellationTokenSource source = new();
        Canceller actual = new() { Source = source };
        Canceller expected = new() { Source = source };
        source.Cancel();

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenStringMemberHoldsDistinctEqualStrings_DoesNotThrow()
    {
        Label actual = new() { Text = new string('a', 3) };
        Label expected = new() { Text = new string('a', 3) };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenSequenceMemberHoldsListAndFrozenSetWithEqualElements_DoesNotThrow()
    {
        List<int> list = [1, 2];
        Scores actual = new() { Values = list };
        Scores expected = new() { Values = list.ToFrozenSet() };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenSequenceMemberHoldsFrozenSetAndListWithEqualElements_DoesNotThrow()
    {
        List<int> list = [1, 2];
        Scores actual = new() { Values = list.ToFrozenSet() };
        Scores expected = new() { Values = list };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenSequenceMemberHoldsListAndFrozenSetWithDifferentElements_ReportsTheMember()
    {
        List<int> list = [1, 2];
        List<int> changed = [1, 3];
        Scores actual = new() { Values = list };
        Scores expected = new() { Values = changed.ToFrozenSet() };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Values\n  - <Values>k__BackingField",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenDelegatesCallSameMethodOnTargetsWithDifferentContent_DoesNotThrow()
    {
        NonSerializableType actual = new() { Callback = new Switch { IsOn = true }.Read };
        NonSerializableType expected = new() { Callback = new Switch { IsOn = false }.Read };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMulticastDelegatesCombineSameDelegatesInSameOrder_DoesNotThrow()
    {
        Switch light = new() { IsOn = true };
        Func<bool> read = light.Read;
        Func<bool> flip = light.Flip;
        NonSerializableType actual = new() { Callback = read + flip };
        NonSerializableType expected = new() { Callback = read + flip };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMulticastDelegateIsComparedWithItsLastDelegate_ReportsTheMember()
    {
        Switch light = new() { IsOn = true };
        Func<bool> read = light.Read;
        Func<bool> flip = light.Flip;
        NonSerializableType actual = new() { Callback = read + flip };
        NonSerializableType expected = new() { Callback = flip };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Callback\n  - <Callback>k__BackingField",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenDelegatesAreLambdasWithDifferentBodies_ReportsTheMember()
    {
        NonSerializableType actual = new() { Callback = () => true };
        NonSerializableType expected = new() { Callback = () => false };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Callback\n  - <Callback>k__BackingField",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenDelegateIsComparedWithNull_ReportsTheMember()
    {
        NonSerializableType actual = new() { Callback = () => true };
        NonSerializableType expected = new() { Callback = null! };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Callback\n  - <Callback>k__BackingField",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenMulticastDelegatesCombineSameDelegatesInReverseOrder_ReportsTheMember()
    {
        Switch light = new() { IsOn = true };
        Func<bool> read = light.Read;
        Func<bool> flip = light.Flip;
        NonSerializableType actual = new() { Callback = read + flip };
        NonSerializableType expected = new() { Callback = flip + read };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Callback\n  - <Callback>k__BackingField",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenMulticastDelegateIsComparedWithItsFirstDelegate_ReportsTheMember()
    {
        Switch light = new() { IsOn = true };
        Func<bool> read = light.Read;
        Func<bool> flip = light.Flip;
        NonSerializableType actual = new() { Callback = read + flip };
        NonSerializableType expected = new() { Callback = read };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Callback\n  - <Callback>k__BackingField",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenDistinctSelfCyclesAreEqual_DoesNotThrow()
    {
        Fuse fuse = new();
        Link actual = Link.SelfCycle(1, fuse);
        Link expected = Link.SelfCycle(1, fuse);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenDistinctSelfCyclesDifferInId_ReportsOnlyTheId()
    {
        Fuse fuse = new();
        Link actual = Link.SelfCycle(1, fuse);
        Link expected = Link.SelfCycle(2, fuse);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Id\n  - <Id>k__BackingField",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenPropertyReturnsItsOwnerAndOwnersAreEqual_DoesNotThrow()
    {
        Fuse fuse = new();
        Mirror actual = new(fuse) { Z = 1 };
        Mirror expected = new(fuse) { Z = 1 };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenPropertyReturnsItsOwnerAndSiblingDiffers_ReportsOnlyTheSibling()
    {
        Fuse fuse = new();
        Mirror actual = new(fuse) { Z = 1 };
        Mirror expected = new(fuse) { Z = 2 };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Z\n  - <Z>k__BackingField",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenPropertyReturnsNewInstanceMethodGroupOnEachRead_DoesNotThrow()
    {
        Ticker actual = new();
        Ticker expected = new();

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenInterfaceTypedMembersFormEqualSelfCycles_DoesNotThrow()
    {
        Fuse fuse = new();
        Chain actual = Chain.SelfCycle(fuse);
        Chain expected = Chain.SelfCycle(fuse);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenEqualCyclesPassThroughAStructField_DoesNotThrow()
    {
        Fuse fuse = new();
        Nest actual = Nest.CycleThroughWrap(fuse);
        Nest expected = Nest.CycleThroughWrap(fuse);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenOneChangedObjectIsReachedFromTwoMembers_ReportsBothMembers()
    {
        Leaf before = new() { V = 1 };
        Leaf after = new() { V = 2 };
        Twins actual = new() { A = before, B = before };
        Twins expected = new() { A = after, B = after };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - A\n  - B\n"
                + "  - <A>k__BackingField\n  - <B>k__BackingField",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenReferenceTypedAutoPropertyObjectChanges_ReportsPropertyAndBackingField()
    {
        Carrier actual = new() { X = new Leaf { V = 1 } };
        Carrier expected = new() { X = new Leaf { V = 2 } };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - X\n  - <X>k__BackingField",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenEqualAcyclicDiamondLadderIsDeep_CompletesInTime()
    {
        Rung actual = Rung.AcyclicLadder(LadderDepth);
        Rung expected = Rung.AcyclicLadder(LadderDepth);

        Exception? exception = Record.Exception(() =>
            SmallStack.OnSmallStack(
                () => Contract.EnsureAssignable(actual, expected),
                timeoutMilliseconds: LadderTimeoutMilliseconds
            )
        );

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenObjectLeadsBackToAChangedObjectBeingCompared_ReportsBothMembers()
    {
        Tangle actual = Tangle.Of(changingValue: 1);
        Tangle expected = Tangle.Of(changingValue: 2);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - M1\n  - M2\n"
                + "  - <M1>k__BackingField\n  - <M2>k__BackingField",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenEqualCyclicDiamondLadderIsDeep_CompletesInTime()
    {
        Carrier actual = new() { Ladder = Rung.CyclicLadder(LadderDepth) };
        Carrier expected = new() { Ladder = Rung.CyclicLadder(LadderDepth) };

        Exception? exception = Record.Exception(() =>
            SmallStack.OnSmallStack(
                () => Contract.EnsureAssignable(actual, expected),
                timeoutMilliseconds: LadderTimeoutMilliseconds
            )
        );

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenListMemberHoldsManyEqualElements_CompletesInTime()
    {
        Inventory actual = Inventory.WithEqualElements(ManyElements);
        Inventory expected = Inventory.WithEqualElements(ManyElements);

        Exception? exception = Record.Exception(() =>
            SmallStack.OnSmallStack(
                () => Contract.EnsureAssignable(actual, expected),
                timeoutMilliseconds: ManyElementsTimeoutMilliseconds
            )
        );

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenOneChangedObjectIsReachedFromTwoMembers_ComparesItOnce()
    {
        ProbeSource source = new();
        Probe before = source.Create(1);
        Probe after = source.Create(2);
        Probes actual = new() { A = before, B = before };
        Probes expected = new() { A = after, B = after };

        Assert.Throws<PostconditionViolationException>(() => Contract.EnsureAssignable(actual, expected));

        // One read of the changed value on each side, although four members (A, B and their backing fields) reach it.
        Assert.Equal(2, source.Reads);
    }

    [Fact]
    public void EnsureAssignable_WhenEqualLongRingIsCompared_CompletesInTime()
    {
        Strands actual = new() { Head = Strand.Ring(LongChainLength) };
        Strands expected = new() { Head = Strand.Ring(LongChainLength) };

        Exception? exception = Record.Exception(() =>
            SmallStack.OnSmallStack(
                () => Contract.EnsureAssignable(actual, expected),
                SmallStackSize,
                LongChainTimeoutMilliseconds
            )
        );

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenEqualLongDoublyLinkedListIsCompared_CompletesInTime()
    {
        Strands actual = new() { Head = Strand.DoublyLinkedList(LongChainLength) };
        Strands expected = new() { Head = Strand.DoublyLinkedList(LongChainLength) };

        Exception? exception = Record.Exception(() =>
            SmallStack.OnSmallStack(
                () => Contract.EnsureAssignable(actual, expected),
                SmallStackSize,
                LongChainTimeoutMilliseconds
            )
        );

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenEqualChainIsVeryDeep_CompletesOnASmallStack()
    {
        NodeChain actual = new() { Head = Node.Chain(VeryDeepChainLength) };
        NodeChain expected = new() { Head = Node.Chain(VeryDeepChainLength) };

        Exception? exception = Record.Exception(() =>
            SmallStack.OnSmallStack(() => Contract.EnsureAssignable(actual, expected), SmallStackSize)
        );

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenVeryDeepChainDiffersAtItsLastNode_ReportsTheTopLevelMember()
    {
        NodeChain actual = new() { Head = Node.Chain(VeryDeepChainLength) };
        NodeChain expected = new() { Head = Node.Chain(VeryDeepChainLength, lastId: -1) };
        PostconditionViolationException? exception = null;

        SmallStack.OnSmallStack(
            () =>
                exception = Assert.Throws<PostconditionViolationException>(() =>
                    Contract.EnsureAssignable(actual, expected)
                ),
            SmallStackSize
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Head\n  - <Head>k__BackingField",
            exception!.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenEqualLongChainLeadsEveryNodeToItsLast_CompletesInTime()
    {
        FarChain actual = new() { Head = FarNode.Chain(FarChainLength) };
        FarChain expected = new() { Head = FarNode.Chain(FarChainLength) };

        Exception? exception = Record.Exception(() =>
            SmallStack.OnSmallStack(
                () => Contract.EnsureAssignable(actual, expected),
                SmallStackSize,
                FarChainTimeoutMilliseconds
            )
        );

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenObjectReliesOnAChangedObjectOnlyThroughADeeperOne_ReportsBothMembers()
    {
        Duo<Cell> actual = Cell.ThreeCycle(changingValue: 1);
        Duo<Cell> expected = Cell.ThreeCycle(changingValue: 2);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(BothDuoMembersChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenObjectReachesOneFoundEqualWhileRelyingOnAChangedObject_ReportsBothMembers()
    {
        Duo<Hub> actual = Hub.HandedOverCycle(changingValue: 1);
        Duo<Hub> expected = Hub.HandedOverCycle(changingValue: 2);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(BothDuoMembersChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenObjectWasFoundEqualRelyingOnAnObjectThatThenReliedOnAChangedOne_ReportsBothMembers()
    {
        Duo<Hub> actual = Hub.AbsorbedCycle(changingValue: 1);
        Duo<Hub> expected = Hub.AbsorbedCycle(changingValue: 2);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(BothDuoMembersChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenObjectReliesOnAChangedObjectBeforeAndOnItselfAfterADeeperOne_ReportsBothMembers()
    {
        FieldDuo actual = Strut.RelianceBeforeDeeperObject(changingValue: 1);
        FieldDuo expected = Strut.RelianceBeforeDeeperObject(changingValue: 2);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(BothFieldDuoMembersChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenObjectReliesOnAChangedObjectAndThenOnItself_ReportsBothMembers()
    {
        FieldDuo actual = Strut.RelianceThenSelfLoop(changingValue: 1);
        FieldDuo expected = Strut.RelianceThenSelfLoop(changingValue: 2);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(BothFieldDuoMembersChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenObjectReliesOnAChangedObjectAndThenOnAProvisionallyEqualOne_ReportsBothMembers()
    {
        FieldDuo actual = Strut.RelianceThenProvisionalObject(changingValue: 1);
        FieldDuo expected = Strut.RelianceThenProvisionalObject(changingValue: 2);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(BothFieldDuoMembersChanged, exception.Description);
    }

    private static object? SemaphoreWaitHandle(SemaphoreSlim semaphore)
    {
        return typeof(SemaphoreSlim)
            .GetField("m_waitHandle", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(semaphore);
    }

    // Long enough for a comparison that reads no blocking member; a comparison that waits on a pending task never ends.
    private const int PendingTimeoutMilliseconds = 3000;

    private interface IAnimal;

    private sealed class Elem(int value)
    {
        public int Value { get; } = value;
    }

    private sealed class Box
    {
        public object? Content { get; set; }
    }

    private sealed class Scores
    {
        public IEnumerable<int> Values { get; set; } = [];
    }

    private abstract class Animal;

    private sealed class Cat : Animal
    {
        public int Lives { get; set; }
    }

    private sealed class Shelter
    {
        public Animal? Resident { get; set; }
    }

    private sealed class Puppy : Animal, IAnimal
    {
        public int Tricks { get; set; }
    }

    private sealed class PetOwner
    {
        public IAnimal? Pet { get; set; }
    }

    private struct Bag
    {
        public List<int> L;
    }

    private sealed class Satchel
    {
        public Bag Bag { get; set; }
    }

    private enum Shade
    {
        Warm,
        Cool,
    }

    private struct Stub : IEquatable<Stub>
    {
        public int Id;
        public string Note;

        public readonly bool Equals(Stub other) => Id == other.Id;

        public override readonly bool Equals(object? obj) => obj is Stub other && Equals(other);

        public override readonly int GetHashCode() => Id;
    }

    private sealed class Ticket
    {
        public Stub Stub { get; set; }
    }

    private sealed class Pair
    {
        public (Elem, Elem) Items { get; set; }
    }

    private sealed class Shelf
    {
        public ImmutableArray<Elem> Items { get; set; }
    }

    private sealed class Window
    {
        public ArraySegment<Elem> Items { get; set; }
    }

    private sealed class Frame
    {
        public IReadOnlyList<byte> Bytes { get; set; } = [];
    }

    private readonly record struct Entry(List<int> L);

    private sealed class Ledger
    {
        public Entry Entry { get; set; }
    }

    private sealed class Lamp
    {
        public int? Brightness { get; set; }

        public Shade Shade { get; set; }
    }

    private struct Mood
    {
        public int Level;

#pragma warning disable MA0025 // The fixture models an Equals that was left unimplemented.
        public override readonly bool Equals(object? obj) => throw new NotImplementedException();
#pragma warning restore MA0025

        public override readonly int GetHashCode() => Level;
    }

    private sealed class Grumpy
    {
        public Mood Mood { get; set; }
    }

#pragma warning disable MA0097 // Only CompareTo matters: the member is declared as IComparable.
    private struct Tag : IComparable
    {
        public int Id;
        public List<int> L;

        public readonly int CompareTo(object? obj) => obj is Tag other ? Id.CompareTo(other.Id) : 1;
    }
#pragma warning restore MA0097

    private sealed class Badge
    {
        public IComparable? Tag { get; set; }
    }

    private sealed class ValueBox
    {
        public ValueType? Content { get; set; }
    }

    private readonly struct Seal
    {
        public override bool Equals(object? obj) => false;

        public override int GetHashCode() => 0;
    }

    private sealed class Vault
    {
        public Seal Seal { get; set; }
    }

    private struct Stamp
    {
        public List<int> L;

        public readonly string PrintRun => $"{L.Count}-{Guid.NewGuid()}";
    }

    private sealed class Envelope
    {
        public Stamp Stamp { get; set; }
    }

    private sealed unsafe class Cursor
    {
        public int* P;
    }

    private unsafe struct Mark
    {
        public int* P;
        public List<int> L;
    }

    private sealed class Bookmark
    {
        public Mark Mark { get; set; }
    }

    private sealed class Opaque;

    private struct Pouch
    {
        public Opaque Content;
    }

    private sealed class Holder
    {
        public Pouch Pouch { get; set; }
    }

    [System.Runtime.CompilerServices.InlineArray(2)]
    private struct RefBuf2
    {
        private List<int> _element;
    }

    private sealed class Buffered
    {
        public RefBuf2 B;
    }

    private sealed class Dial
    {
        public Enum? Setting { get; set; }
    }

    [System.Runtime.CompilerServices.InlineArray(2)]
    private struct StringBuf2
    {
        private string _element;
    }

    private sealed class Labelled
    {
        public StringBuf2 Labels;
    }

    [System.Runtime.CompilerServices.InlineArray(2)]
    private struct ListSeq2 : IEnumerable<List<int>>
    {
        private List<int> _element;

        public IEnumerator<List<int>> GetEnumerator() => new List<List<int>> { this[0], this[1] }.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class Sequenced
    {
        public ListSeq2 S;
    }

    private struct Page : IEnumerable<int>
    {
        public List<int> Items;
        public int Total;

        public readonly IEnumerator<int> GetEnumerator() => Items.GetEnumerator();

        readonly System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class Book
    {
        public Page Page;
    }

    // Equal by Id alone, so its Equals ignores a changed Owner.
    private sealed class Account(int id, string owner)
    {
        public int Id { get; } = id;

        public string Owner { get; } = owner;

        public override bool Equals(object? obj) => obj is Account other && Id == other.Id;

        public override int GetHashCode() => Id;
    }

    private sealed class Registry
    {
        public Dictionary<string, Account> Accounts { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class Catalog
    {
        public Dictionary<string, Elem> Items { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class Roster
    {
        public Dictionary<string, List<int>> Scores { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class Listing
    {
        public KeyValuePair<string, Elem>[] Entries { get; set; } = [];
    }

    private sealed class Archive
    {
        public Hashtable Table { get; set; } = [];
    }

    private sealed class Job
    {
        public Task<int> Work { get; set; } = Task.FromResult(0);
    }

    private sealed class Deferred
    {
        public Lazy<int> Value { get; set; } = new(0);
    }

    private sealed class Turnstile
    {
        public SemaphoreSlim? Gate { get; set; }
    }

    private sealed class Request
    {
        public CancellationToken Cancellation { get; set; }
    }

    private sealed class Promise
    {
        public ValueTask<int> Pending;
    }

    private sealed class Canceller
    {
        public CancellationTokenSource? Source { get; set; }
    }

    private sealed class Switch
    {
        public bool IsOn { get; set; }

        public bool Read() => IsOn;

        public bool Flip() => !IsOn;
    }

    private sealed class Label
    {
        public string Text { get; set; } = "";
    }

    // A diamond ladder of this depth has 2^40 paths: a comparison that walks each path never ends.
    private const int LadderDepth = 40;
    private const int LadderTimeoutMilliseconds = 1000;

    private sealed class Leaf
    {
        public int V { get; set; }
    }

    private sealed class Twins
    {
        public Leaf? A { get; set; }
        public Leaf? B { get; set; }
    }

    private sealed class Carrier
    {
        public Leaf? X { get; set; }
        public Rung? Ladder { get; set; }
    }

    // Each rung reaches the rung below twice, through L and through R.
    private sealed class Rung
    {
        public Rung? L { get; set; }
        public Rung? R { get; set; }

        public static Rung AcyclicLadder(int depth)
        {
            return Build(new Rung(), depth);
        }

        // The bottom rung leads back to the top one, so every rung is compared while the top one still is.
        public static Rung CyclicLadder(int depth)
        {
            Rung bottom = new();
            Rung top = Build(bottom, depth);
            bottom.L = top;
            bottom.R = top;
            return top;
        }

        private static Rung Build(Rung bottom, int depth)
        {
            Rung top = bottom;
            for (int i = 0; i < depth; i++)
            {
                top = new Rung { L = top, R = top };
            }

            return top;
        }
    }

    private const int ManyElements = 100_000;
    private const int ManyElementsTimeoutMilliseconds = 2000;

    // Every node of these chains is compared while a shallower node still is, so bookkeeping that revisits
    // the nodes found below each node is quadratic.
    private const int LongChainLength = 20_000;
    private const int LongChainTimeoutMilliseconds = 2000;

    private sealed class Strand
    {
        public Strand? Next { get; set; }
        public Strand? Prev { get; set; }

        // The last node leads back to the first.
        public static Strand Ring(int length)
        {
            Strand head = new();
            Strand last = head;
            for (int i = 1; i < length; i++)
            {
                last.Next = new Strand();
                last = last.Next;
            }

            last.Next = head;
            return head;
        }

        // Each node leads back to the one before it.
        public static Strand DoublyLinkedList(int length)
        {
            Strand head = new();
            Strand last = head;
            for (int i = 1; i < length; i++)
            {
                last.Next = new Strand { Prev = last };
                last = last.Next;
            }

            return head;
        }
    }

    private sealed class Strands
    {
        public Strand? Head { get; set; }
    }

    // Deep enough to overflow a small stack if the walk recursed once per node.
    private const int VeryDeepChainLength = 50_000;
    private const int SmallStackSize = 256 * 1024;

    private sealed class Node
    {
        public int Id { get; set; }
        public Node? Next { get; set; }

        // Built iteratively; the last node's Id is lastId.
        public static Node Chain(int length, int? lastId = null)
        {
            Node head = new() { Id = 0 };
            Node last = head;
            for (int i = 1; i < length; i++)
            {
                last.Next = new Node { Id = i };
                last = last.Next;
            }

            last.Id = lastId ?? last.Id;
            return head;
        }
    }

    private sealed class NodeChain
    {
        public Node? Head { get; set; }
    }

    // Each node relies on the one before it, and the last node is reached again from every node, through a
    // chain of groups handed over once per node: looking its group up without shortening that chain is
    // quadratic (737 ms already at 20 000 nodes), the linear lookup well under a second at this length.
    private const int FarChainLength = 100_000;
    private const int FarChainTimeoutMilliseconds = 5000;

    private sealed class FarNode
    {
        public FarNode? Prev { get; set; }
        public FarNode? Next { get; set; }
        public FarNode? Far { get; set; }

        public static FarNode Chain(int length)
        {
            FarNode[] nodes = new FarNode[length];
            for (int i = 0; i < length; i++)
            {
                nodes[i] = new FarNode { Prev = i > 0 ? nodes[i - 1] : null };
            }

            for (int i = 0; i < length; i++)
            {
                nodes[i].Next = i + 1 < length ? nodes[i + 1] : null;
                nodes[i].Far = nodes[length - 1];
            }

            return nodes[0];
        }
    }

    private sealed class FarChain
    {
        public FarNode? Head { get; set; }
    }

    private sealed class Inventory
    {
        public List<Elem> Items { get; set; } = [];

        public static Inventory WithEqualElements(int count)
        {
            return new Inventory { Items = Enumerable.Range(0, count).Select(i => new Elem(i)).ToList() };
        }
    }

    // Shared by the actual and the expected graph, so it compares equal by reference. It holds each probe's
    // value, so a probe's only member that differs is V, whatever order the members are compared in.
    private sealed class ProbeSource
    {
        private readonly Dictionary<Probe, int> _values = [];

        public int Reads { get; private set; }

        public Probe Create(int value)
        {
            Probe probe = new(this);
            _values[probe] = value;
            return probe;
        }

        public int Read(Probe probe)
        {
            Reads++;
            return _values[probe];
        }
    }

    private sealed class Probe(ProbeSource source)
    {
        private readonly ProbeSource _source = source;

        public int V => _source.Read(this);
    }

    private sealed class Probes
    {
        public Probe? A { get; set; }
        public Probe? B { get; set; }
    }

    private const string BothDuoMembersChanged =
        "Fields were modified that are not marked as assignable:\n  - M1\n  - M2\n"
        + "  - <M1>k__BackingField\n  - <M2>k__BackingField";

    private sealed class Duo<TNode>
        where TNode : class
    {
        public TNode? M1 { get; set; }
        public TNode? M2 { get; set; }
    }

    // Public fields only: each reference is compared once, so no second member records a reliance again.
    private sealed class Cell
    {
        public Cell? X;
        public int V;

        // a -> b -> c -> a, with M1 = a and M2 = b: b relies on a only through c.
        public static Duo<Cell> ThreeCycle(int changingValue)
        {
            Cell a = new() { V = changingValue };
            Cell b = new();
            Cell c = new() { X = a };
            a.X = b;
            b.X = c;
            return new Duo<Cell> { M1 = a, M2 = b };
        }
    }

    private sealed class Hub
    {
        public Hub? X { get; set; }
        public Hub? Y { get; set; }
        public int V { get; set; }

        // a.X = b, b.X = c, c.X = b, b.Y = a, a.Y = d, d.X = c, with M1 = a and M2 = d. c is found equal
        // relying on b, and b relying on a; d, compared next at b's former depth, then reaches c.
        public static Duo<Hub> HandedOverCycle(int changingValue)
        {
            Hub a = new() { V = changingValue };
            Hub b = new() { Y = a };
            Hub c = new() { X = b };
            Hub d = new() { X = c };
            a.X = b;
            a.Y = d;
            b.X = c;
            return new Duo<Hub> { M1 = a, M2 = d };
        }

        // p.X = q, q.X = r, r.X = q, q.Y = p, with M1 = p and M2 = r. r is found equal relying only on q,
        // and q then relies on p, which differs.
        public static Duo<Hub> AbsorbedCycle(int changingValue)
        {
            Hub p = new() { V = changingValue };
            Hub q = new() { Y = p };
            Hub r = new() { X = q };
            p.X = q;
            q.X = r;
            return new Duo<Hub> { M1 = p, M2 = r };
        }
    }

    private const string BothFieldDuoMembersChanged =
        "Fields were modified that are not marked as assignable:\n  - M1\n  - M2";

    // Public fields only, so each member is compared once and records its reliance once.
    private sealed class FieldDuo
    {
        public Strut? M1;
        public Strut? M2;
    }

    // Public fields only, compared in declaration order: P, then Q, then V. In each graph only a.V changes,
    // and it is compared after everything reached through a.P, which relies on a.
    private sealed class Strut
    {
        public Strut? P;
        public Strut? Q;
        public int V;

        // a.P = x, x.P = a, x.Q = c, c.P = x, with M1 = a and M2 = x: x relies on a before it compares c,
        // which relies only on x.
        public static FieldDuo RelianceBeforeDeeperObject(int changingValue)
        {
            Strut a = new() { V = changingValue };
            Strut x = new() { P = a };
            Strut c = new() { P = x };
            a.P = x;
            x.Q = c;
            return new FieldDuo { M1 = a, M2 = x };
        }

        // a.P = x, x.P = a, x.Q = x, with M1 = a and M2 = x: x relies on a, then on itself.
        public static FieldDuo RelianceThenSelfLoop(int changingValue)
        {
            Strut a = new() { V = changingValue };
            Strut x = new() { P = a };
            a.P = x;
            x.Q = x;
            return new FieldDuo { M1 = a, M2 = x };
        }

        // a.P = b, b.P = z, z.P = b, b.Q = y, y.P = a, y.Q = z, with M1 = a and M2 = y: z is provisionally
        // equal relying on b when y, having relied on a, reaches it.
        public static FieldDuo RelianceThenProvisionalObject(int changingValue)
        {
            Strut a = new() { V = changingValue };
            Strut b = new();
            Strut z = new() { P = b };
            Strut y = new() { P = a, Q = z };
            a.P = b;
            b.P = z;
            b.Q = y;
            return new FieldDuo { M1 = a, M2 = y };
        }
    }

    private sealed class Knot
    {
        public Knot? A { get; set; }
        public int B { get; set; }
    }

    // M1 = P and M2 = Q, where P.A = Q and Q.A = P: comparing P reaches Q, which leads back to P while P is
    // still being compared; only after that is P's B (the changing value) compared.
    private sealed class Tangle
    {
        public Knot? M1 { get; set; }
        public Knot? M2 { get; set; }

        public static Tangle Of(int changingValue)
        {
            Knot p = new() { B = changingValue };
            Knot q = new() { A = p };
            p.A = q;
            return new Tangle { M1 = p, M2 = q };
        }
    }

    private sealed class FuseBlownException() : Exception("The comparison read a fused member too often.");

    // Shared by the actual and the expected graph, so it compares equal by reference. It turns a
    // comparison that would recurse forever into a distinct failure instead of a crashed test host.
    private sealed class Fuse
    {
        private const int MaxReads = 200;
        private int _reads;

        public void Read()
        {
            if (++_reads > MaxReads)
            {
                throw new FuseBlownException();
            }
        }
    }

    private sealed class Link(Fuse fuse)
    {
        private readonly Fuse _fuse = fuse;
        private Link? _next;

        public int Id { get; set; }

        public Link? Next
        {
            get
            {
                _fuse.Read();
                return _next;
            }
            set => _next = value;
        }

        public static Link SelfCycle(int id, Fuse fuse)
        {
            Link link = new(fuse) { Id = id };
            link.Next = link;
            return link;
        }
    }

    private sealed class Mirror(Fuse fuse)
    {
        private readonly Fuse _fuse = fuse;

        public int Z { get; set; }

        public Mirror Me
        {
            get
            {
                _fuse.Read();
                return this;
            }
        }
    }

    private sealed class Ticker
    {
        private readonly int _count = 1;

        public Func<int> Tick => Count;

        private int Count() => _count;
    }

    private interface IChainNode
    {
        IChainNode? Next { get; }
    }

    private sealed class Chain(Fuse fuse) : IChainNode
    {
        private readonly Fuse _fuse = fuse;
        private IChainNode? _next;

        public IChainNode? Next
        {
            get
            {
                _fuse.Read();
                return _next;
            }
        }

        public static Chain SelfCycle(Fuse fuse)
        {
            Chain chain = new(fuse);
            chain._next = chain;
            return chain;
        }
    }

    private struct Wrap
    {
        public Nest? Parent;
    }

    private sealed class Nest(Fuse fuse)
    {
        private readonly Fuse _fuse = fuse;
        private Wrap _wrap;

        public Wrap W
        {
            get
            {
                _fuse.Read();
                return _wrap;
            }
        }

        public static Nest CycleThroughWrap(Fuse fuse)
        {
            Nest nest = new(fuse);
            nest._wrap = new Wrap { Parent = nest };
            return nest;
        }
    }
}
