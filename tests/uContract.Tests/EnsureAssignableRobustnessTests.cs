using System.Collections;
using System.Collections.Immutable;
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
}
