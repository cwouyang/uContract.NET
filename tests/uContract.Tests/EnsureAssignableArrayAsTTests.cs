using System.Collections;
using System.Collections.Frozen;
using System.Collections.Immutable;
using uContract.Exceptions;

namespace uContract.Tests;

public unsafe class EnsureAssignableArrayAsTTests
{
    private const string ActualIsNull = "actual is null and expected is not.";

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfIntegersOfEqualContent_DoesNotThrow()
    {
        int[] actual = [1, 2];
        int[] expected = [1, 2];
        Assert.NotSame(actual, expected);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfIntegersAndOneElementDiffers_ReportsTheTwoAsNotEqual()
    {
        int[] actual = [1, 2];
        int[] expected = [1, 3];
        Assert.NotSame(actual, expected);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(int[])), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfIntegersOfLengthsTwoAndOne_ReportsTheTwoAsNotEqual()
    {
        int[] actual = [1, 2];
        int[] expected = [1];
        Assert.NotSame(actual, expected);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(int[])), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfStringsAndOneElementDiffers_ReportsTheTwoAsNotEqual()
    {
        string[] actual = ["a", "b"];
        string[] expected = ["a", "x"];
        Assert.NotSame(actual, expected);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(string[])), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfStringsAndANullElementStandsAgainstAString_ReportsTheTwoAsNotEqual()
    {
        string?[] actual = [null];
        string?[] expected = ["a"];
        Assert.NotSame(actual, expected);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(string[])), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfStringsAndAnElementDiffersOnlyInCase_ReportsTheTwoAsNotEqual()
    {
        string[] actual = ["aB"];
        string[] expected = ["ab"];
        Assert.NotSame(actual, expected);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(string[])), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfObjectsAndAPropertyOfAnElementDiffers_ReportsTheTwoAsNotEqual()
    {
        Box[] actual = [new Box { Value = 1 }];
        Box[] expected = [new Box { Value = 2 }];
        Assert.NotSame(actual, expected);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(Box[])), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfArraysAndAnInnerElementDiffers_ReportsTheTwoAsNotEqual()
    {
        int[][] actual =
        [
            [1],
        ];
        int[][] expected =
        [
            [2],
        ];
        Assert.NotSame(actual, expected);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(int[][])), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsATwoDimensionalArrayAndOneElementDiffers_ReportsTheTwoAsNotEqual()
    {
        int[,] actual =
        {
            { 1, 2 },
        };
        int[,] expected =
        {
            { 1, 3 },
        };
        Assert.NotSame(actual, expected);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(int[,])), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfObjectAndOneElementDiffers_ReportsTheTwoAsNotEqual()
    {
        object[] actual = [1, "a"];
        object[] expected = [1, "b"];
        Assert.NotSame(actual, expected);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(object[])), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfFrozenSetsOfDifferentContent_ReportsTheTwoAsNotEqual()
    {
        int[] numbers = [1, 2, 3];
        int[] otherNumbers = [1, 2, 4];
        FrozenSet<int>[] actual = [numbers.ToFrozenSet()];
        FrozenSet<int>[] expected = [otherNumbers.ToFrozenSet()];
        Assert.NotSame(actual, expected);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(FrozenSet<int>[])), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfIntegersAndAPatternNamesLength_StillReportsADifferentElement()
    {
        int[] actual = [1, 2];
        int[] expected = [1, 3];
        Assert.NotSame(actual, expected);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected, "Length")
        );

        Assert.Equal(NotEqual(typeof(int[])), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfIntegersAndAPatternNamesSyncRoot_StillReportsADifferentElement()
    {
        int[] actual = [1, 2];
        int[] expected = [1, 3];
        Assert.NotSame(actual, expected);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected, "SyncRoot")
        );

        Assert.Equal(NotEqual(typeof(int[])), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfIntegersAndAPatternMatchesEveryName_StillReportsDifferentLengths()
    {
        int[] actual = [1, 2];
        int[] expected = [1];
        Assert.NotSame(actual, expected);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected, ".*")
        );

        Assert.Equal(NotEqual(typeof(int[])), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfEqualContentAndAPatternIsNotAValidRegularExpression_DoesNotThrow()
    {
        int[] actual = [1, 2];
        int[] expected = [1, 2];
        Assert.NotSame(actual, expected);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected, "("));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfEqualContentAndThePatternsHoldNull_DoesNotThrow()
    {
        int[] actual = [1, 2];
        int[] expected = [1, 2];
        Assert.NotSame(actual, expected);
        string[] patterns = [null!];

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected, patterns));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayAndThePatternsArgumentIsNull_ThrowsArgumentNull()
    {
        int[] actual = [1, 2];
        int[] expected = [1, 3];
        Assert.NotSame(actual, expected);

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            Contract.EnsureAssignable(actual, expected, null!)
        );

        Assert.Equal("assignableFieldPatterns", exception.ParamName);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfIntegersAndActualIsNull_ReportsTheNull()
    {
        int[] expected = [1];

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable<int[]?>(null, expected)
        );

        Assert.Equal(ActualIsNull, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfIntegersAndBothAreNull_DoesNotThrow()
    {
        Exception? exception = Record.Exception(() => Contract.EnsureAssignable<int[]?>(null, null));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsArrayAndActualIsNull_ReportsTheNull()
    {
        int[] expected = [1];

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable<Array?>(null, expected)
        );

        Assert.Equal(ActualIsNull, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsArrayAndBothAreNull_DoesNotThrow()
    {
        Exception? exception = Record.Exception(() => Contract.EnsureAssignable<Array?>(null, null));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfIntegersAndOneInstanceIsOnBothSides_DoesNotThrow()
    {
        int[] numbers = [1, 2];

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(numbers, numbers));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfIntegersAndBothAreEmpty_DoesNotThrow()
    {
        int[] actual = Integers(length: 0);
        int[] expected = Integers(length: 0);
        Assert.NotSame(actual, expected);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenAnElementOfAnArrayOfIntegersChangedSinceOld_ReportsTheTwoAsNotEqual()
    {
        int[] numbers = [1, 2];
        int[] old = Contract.Old(() => numbers);
        Assert.NotSame(numbers, old);
        numbers[0] = 9;

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(numbers, old)
        );

        Assert.Equal(NotEqual(typeof(int[])), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenAPropertyOfAnElementOfAnArrayOfObjectsChangedSinceOld_ReportsTheTwoAsNotEqual()
    {
        Box[] boxes = [new Box { Value = 1 }];
        Box[] old = Contract.Old(() => boxes);
        Assert.NotSame(boxes, old);
        boxes[0].Value = 2;

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(boxes, old)
        );

        Assert.Equal(NotEqual(typeof(Box[])), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfStreamsAndTwoArraysHoldOneStream_DoesNotThrow()
    {
        using MemoryStream stream = new();
        Stream[] actual = [stream];
        Stream[] expected = [stream];
        Assert.NotSame(actual, expected);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfStreamsAndOneInstanceIsOnBothSides_DoesNotThrow()
    {
        using MemoryStream stream = new();
        Stream[] streams = [stream];

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(streams, streams));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfStreamsAndTwoArraysHoldTwoStreamsOfEqualContent_ReportsTheTwoAsNotEqual()
    {
        using MemoryStream actualStream = new();
        using MemoryStream expectedStream = new();
        Stream[] actual = [actualStream];
        Stream[] expected = [expectedStream];
        Assert.NotSame(actual, expected);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(Stream[])), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenAStreamInAnArrayOfStreamsWasWrittenToSinceOld_DoesNotThrow()
    {
        using MemoryStream stream = new();
        Stream[] streams = [stream];
        Stream[] old = Contract.Old(() => streams);
        Assert.NotSame(streams, old);
        stream.WriteByte(1);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(streams, old));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfDatesWithTheSameTicksAndDifferentKinds_DoesNotThrow()
    {
        DateTime[] actual = [new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)];
        DateTime[] expected = [new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Local)];
        Assert.NotSame(actual, expected);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsADateAndTheTwoHaveTheSameTicksAndDifferentKinds_ReportsTheMembers()
    {
        DateTime actual = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime expected = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Local);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Kind\n  - _dateData",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfABaseClassAndAMemberOfTheDerivedElementsDiffers_ReportsTheTwoAsNotEqual()
    {
        Vehicle[] actual = [new Truck { Wheels = 1, Load = 1 }];
        Vehicle[] expected = [new Truck { Wheels = 1, Load = 2 }];
        Assert.NotSame(actual, expected);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(Vehicle[])), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfObjectAndEachHoldsItselfAndTheSecondElementDiffers_ReportsTheTwoAsNotEqual()
    {
        object[] actual = new object[2];
        actual[0] = actual;
        actual[1] = 1;
        object[] expected = new object[2];
        expected[0] = expected;
        expected[1] = 2;
        Assert.NotSame(actual, expected);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(object[])), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfObjectAndArraysHoldEachOtherAndOneOtherElementDiffers_ReportsTheTwoAsNotEqual()
    {
        object[] actual = new object[2];
        object[] actualInner = new object[1];
        actual[0] = actualInner;
        actual[1] = 1;
        actualInner[0] = actual;
        object[] expected = new object[2];
        object[] expectedInner = new object[1];
        expected[0] = expectedInner;
        expected[1] = 2;
        expectedInner[0] = expected;
        Assert.NotSame(actual, expected);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(object[])), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsArrayAndOneElementOfTwoArraysOfIntegersDiffers_ReportsTheTwoAsNotEqual()
    {
        Array actual = new[] { 1, 2 };
        Array expected = new[] { 1, 3 };
        Assert.NotSame(actual, expected);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(Array)), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsArrayAndAPatternNamesLength_StillReportsDifferentLengths()
    {
        Array actual = new[] { 1, 2 };
        Array expected = new[] { 1 };
        Assert.NotSame(actual, expected);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected, "Length")
        );

        Assert.Equal(NotEqual(typeof(Array)), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsArrayAndAnArrayOfIntegersStandsAgainstAnArrayOfLongsOfTheSameNumbers_ReportsTheTwoAsNotEqual()
    {
        Array actual = new[] { 1, 2 };
        Array expected = new[] { 1L, 2L };
        Assert.NotSame(actual, expected);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(Array)), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsArrayAndAnElementChangedSinceOld_ReportsTheTwoAsNotEqual()
    {
        int[] numbers = [1, 2];
        Array current = numbers;
        Array old = Contract.Old(() => current);
        Assert.NotSame(current, old);
        numbers[0] = 9;

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(current, old)
        );

        Assert.Equal(NotEqual(typeof(Array)), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfObjectAndAnArrayOfStringsStandsAgainstAnArrayOfObjectOfEqualContent_DoesNotThrow()
    {
        object[] actual = new string[] { "a" };
        object[] expected = ["a"];
        Assert.NotSame(actual, expected);
        Assert.NotEqual(expected.GetType(), actual.GetType());

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndOneElementOfTwoArraysDiffers_DoesNotThrow()
    {
        object actual = new[] { 1, 2 };
        object expected = new[] { 1, 3 };
        Assert.NotSame(actual, expected);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsEnumerableOfIntegersAndOneElementOfTwoArraysDiffers_DoesNotThrow()
    {
        IEnumerable<int> actual = new[] { 1, 2 };
        IEnumerable<int> expected = new[] { 1, 3 };
        Assert.NotSame(actual, expected);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsListInterfaceOfIntegersAndOneElementOfTwoArraysDiffers_DoesNotThrow()
    {
        IList<int> actual = new[] { 1, 2 };
        IList<int> expected = new[] { 1, 3 };
        Assert.NotSame(actual, expected);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsReadOnlyListOfIntegersAndOneElementOfTwoArraysDiffers_DoesNotThrow()
    {
        IReadOnlyList<int> actual = new[] { 1, 2 };
        IReadOnlyList<int> expected = new[] { 1, 3 };
        Assert.NotSame(actual, expected);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsTheNonGenericListInterfaceAndOneElementOfTwoArraysDiffers_DoesNotThrow()
    {
        IList actual = new[] { 1, 2 };
        IList expected = new[] { 1, 3 };
        Assert.NotSame(actual, expected);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsCloneableAndOneElementOfTwoArraysDiffers_DoesNotThrow()
    {
        ICloneable actual = new[] { 1, 2 };
        ICloneable expected = new[] { 1, 3 };
        Assert.NotSame(actual, expected);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAListAndOneElementDiffers_ReportsThePrivateField()
    {
        List<int> actual = [1, 2];
        List<int> expected = [1, 3];
        Assert.NotSame(actual, expected);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal("Fields were modified that are not marked as assignable:\n  - _items", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAListOfEqualContent_DoesNotThrow()
    {
        List<int> actual = [1, 2];
        List<int> expected = [1, 2];
        Assert.NotSame(actual, expected);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAListAndAPatternNamesThePrivateFieldThatDiffers_DoesNotThrow()
    {
        List<int> actual = [1, 2];
        List<int> expected = [1, 3];
        Assert.NotSame(actual, expected);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected, "_items"));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnImmutableArrayAndAPatternNamesThePrivateFieldThatDiffers_DoesNotThrow()
    {
        ImmutableArray<int> actual = [1, 2];
        ImmutableArray<int> expected = [1, 3];

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected, "array"));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsATwoDimensionalArrayAndOneByTwoStandsAgainstTwoByOneOfTheSameElements_ReportsTheTwoAsNotEqual()
    {
        int[,] actual =
        {
            { 1, 2 },
        };
        int[,] expected =
        {
            { 1 },
            { 2 },
        };
        Assert.NotSame(actual, expected);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(int[,])), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsArrayAndAOneDimensionalArrayStandsAgainstATwoDimensionalArrayOfTheSameElements_ReportsTheTwoAsNotEqual()
    {
        Array actual = new[] { 1, 2 };
        Array expected = new[,]
        {
            { 1, 2 },
        };
        Assert.NotSame(actual, expected);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(Array)), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsArrayAndArraysOfEqualElementsHaveLowerBoundsZeroAndOne_ReportsTheTwoAsNotEqual()
    {
        Array actual = new[] { 1, 2 };
        Array expected = OneDimensional(lowerBound: 1, 1, 2);
        Assert.NotSame(actual, expected);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(Array)), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfPointersAndOneInstanceIsOnBothSides_DoesNotThrow()
    {
        int target = 1;
        int*[] pointers = [&target];

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(pointers, pointers));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfPointersAndBothAreEmpty_DoesNotThrow()
    {
        int*[] actual = Pointers(length: 0);
        int*[] expected = Pointers(length: 0);
        Assert.NotSame(actual, expected);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfPointersToTheSameAddress_DoesNotThrow()
    {
        int target = 1;
        int*[] actual = [&target];
        int*[] expected = [&target];
        Assert.NotSame(actual, expected);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfPointersToDifferentAddresses_ReportsTheTwoAsNotEqual()
    {
        int first = 1;
        int second = 1;
        int*[] actual = [&first];
        int*[] expected = [&second];
        Assert.NotSame(actual, expected);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(int*[])), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenAnElementOfAnArrayOfPointersIsSetToAnotherAddressSinceOld_ReportsTheTwoAsNotEqual()
    {
        int first = 1;
        int second = 1;
        int*[] pointers = [&first];
        int*[] old = Contract.Old(() => pointers);
        Assert.NotSame(pointers, old);
        pointers[0] = &second;

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(pointers, old)
        );

        Assert.Equal(NotEqual(typeof(int*[])), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsArrayAndTwoArraysOfPointersHoldTheSameAddress_DoesNotThrow()
    {
        int target = 1;
        Array actual = new[] { &target };
        Array expected = new[] { &target };
        Assert.NotSame(actual, expected);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfFunctionPointersToDifferentFunctions_ReportsTheTwoAsNotEqual()
    {
        delegate* <void>[] actual = [&FirstFunction];
        delegate* <void>[] expected = [&SecondFunction];
        Assert.NotSame(actual, expected);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(delegate* <void>[])), exception.Description);
    }

    private static string NotEqual(Type type) =>
        $"actual and expected are not equal. {type} is compared as a whole, so assignable patterns do not apply.";

    private static void FirstFunction() { }

    private static void SecondFunction() { }

    // Each helper below returns a new array on every call, also for a length of 0.

    private static int[] Integers(int length) => new int[length];

    private static int*[] Pointers(int length) => new int*[length];

    private static Array OneDimensional(int lowerBound, params int[] values)
    {
        int[] lengths = [values.Length];
        int[] lowerBounds = [lowerBound];
#pragma warning disable IL3050 // Test fixture only: the library itself never creates arrays this way.
        Array array = Array.CreateInstance(typeof(int), lengths, lowerBounds);
#pragma warning restore IL3050
        for (int i = 0; i < values.Length; i++)
        {
            array.SetValue(values[i], lowerBound + i);
        }

        return array;
    }

    private sealed class Box
    {
        public int Value { get; set; }
    }

    private class Vehicle
    {
        public int Wheels { get; set; }
    }

    private sealed class Truck : Vehicle
    {
        public int Load { get; set; }
    }
}

[Collection("EnvironmentVariables")]
public class EnsureAssignableArrayAsTWithoutDynamicCodeTests
{
    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfATypeWithNoVisibleMembersAndEachArrayHasItsOwnInstance_ThrowsInvalidOperationNamingTheElementOfActual()
    {
        NoMembers[] actual = [new NoMembers()];
        NoMembers[] expected = [new NoMembers()];
        Assert.NotSame(actual, expected);

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(CannotCompareBelowActual("actual[]"), cannotCompare.Message);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfATypeWithNoVisibleMembersAndNothingChangedSinceOld_ThrowsInvalidOperationNamingTheElementOfActual()
    {
        NoMembers[] items = [new NoMembers()];

        Exception? exception = TestRuntime.WithoutDynamicCode(() =>
        {
            NoMembers[] old = Contract.Old(() => items);
            Contract.EnsureAssignable(items, old);
        });

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(CannotCompareBelowActual("actual[]"), cannotCompare.Message);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfATypeWithNoVisibleMembersAndTwoArraysHoldOneInstance_DoesNotThrow()
    {
        NoMembers item = new();
        NoMembers[] actual = [item];
        NoMembers[] expected = [item];
        Assert.NotSame(actual, expected);

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfATypeWithNoVisibleMembersAndBothAreEmpty_DoesNotThrow()
    {
        NoMembers[] actual = ItemsWithNoMembers(length: 0);
        NoMembers[] expected = ItemsWithNoMembers(length: 0);
        Assert.NotSame(actual, expected);

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfATypeWithNoVisibleMembersWhoseEqualsSaysEqual_DoesNotThrow()
    {
        AlwaysEqualNoMembers[] actual = [new AlwaysEqualNoMembers()];
        AlwaysEqualNoMembers[] expected = [new AlwaysEqualNoMembers()];
        Assert.NotSame(actual, expected);
        Assert.NotSame(actual[0], expected[0]);

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsArraysOfATypeWithNoVisibleMembersEachWithItsOwnInstance_ThrowsInvalidOperationNamingTheMember()
    {
        Holder<NoMembers[]> actual = new() { Items = [new NoMembers()] };
        Holder<NoMembers[]> expected = new() { Items = [new NoMembers()] };
        Assert.NotSame(actual.Items, expected.Items);

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(CannotCompareBelowItems(typeof(Holder<NoMembers[]>)), cannotCompare.Message);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsAnArrayOfATypeWithNoVisibleMembersAndNothingChangedSinceOld_ThrowsInvalidOperationNamingTheMember()
    {
        Holder<NoMembers[]> holder = new() { Items = [new NoMembers()] };

        Exception? exception = TestRuntime.WithoutDynamicCode(() =>
        {
            Holder<NoMembers[]> old = Contract.Old(() => holder);
            Contract.EnsureAssignable(holder, old);
        });

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(CannotCompareBelowItems(typeof(Holder<NoMembers[]>)), cannotCompare.Message);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsArraysOfATypeWithNoVisibleMembersWhoseEqualsSaysEqual_DoesNotThrow()
    {
        Holder<AlwaysEqualNoMembers[]> actual = new() { Items = [new AlwaysEqualNoMembers()] };
        Holder<AlwaysEqualNoMembers[]> expected = new() { Items = [new AlwaysEqualNoMembers()] };
        Assert.NotSame(actual.Items, expected.Items);
        Assert.NotSame(actual.Items[0], expected.Items[0]);

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfArraysOfATypeWithNoVisibleMembers_ThrowsInvalidOperationNamingTheElementOfAnElementOfActual()
    {
        NoMembers[][] actual =
        [
            [new NoMembers()],
        ];
        NoMembers[][] expected =
        [
            [new NoMembers()],
        ];
        Assert.NotSame(actual, expected);

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(CannotCompareBelowActual("actual[][]"), cannotCompare.Message);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsATwoDimensionalArrayOfATypeWithNoVisibleMembers_ThrowsInvalidOperationNamingTheElementOfActual()
    {
        NoMembers[,] actual =
        {
            { new NoMembers() },
        };
        NoMembers[,] expected =
        {
            { new NoMembers() },
        };
        Assert.NotSame(actual, expected);

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(CannotCompareBelowActual("actual[]"), cannotCompare.Message);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsArrayAndEachArrayHasItsOwnInstanceOfATypeWithNoVisibleMembers_ThrowsInvalidOperationNamingTheElementOfActual()
    {
        Array actual = new[] { new NoMembers() };
        Array expected = new[] { new NoMembers() };
        Assert.NotSame(actual, expected);

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(CannotCompareBelowActual("actual[]"), cannotCompare.Message);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayWhoseElementsHoldATypeWithNoVisibleMembers_ThrowsInvalidOperationNamingTheMemberOfTheElementOfActual()
    {
        LeafHolder[] actual = [new LeafHolder()];
        LeafHolder[] expected = [new LeafHolder()];
        Assert.NotSame(actual, expected);

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(CannotCompareBelowActual("actual[].Leaf"), cannotCompare.Message);
    }

    private static string CannotCompareBelowActual(string path) =>
        $"EnsureAssignable cannot compare {typeof(NoMembers)} (reached through '{path}'): "
        + "no properties or fields are visible to reflection under Native AOT. "
        + "Their Equals reports them unequal (without an Equals override this only means they are different instances), and their members cannot be listed. "
        + "Ways out: if the type has members, preserve them, for example with "
        + "[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(X))] where X is that type; "
        + "or set DBC_POST=off (disables all postcondition checks).";

    private static string CannotCompareBelowItems(Type holder) =>
        $"EnsureAssignable cannot compare {typeof(NoMembers)} (reached through '{holder.Name}.Items[]'): "
        + "no properties or fields are visible to reflection under Native AOT. "
        + "Their Equals reports them unequal (without an Equals override this only means they are different instances), and their members cannot be listed. "
        + "Ways out: list 'Items' as assignable (patterns are regular expressions "
        + "matched against top-level member names, so use the plain member name); "
        + "if the type has members, preserve them, for example with "
        + "[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(X))] where X is that type; "
        + "or set DBC_POST=off (disables all postcondition checks).";

    // Returns a new array on every call, also for a length of 0.
    private static NoMembers[] ItemsWithNoMembers(int length) => new NoMembers[length];

    private sealed class NoMembers;

    private sealed class AlwaysEqualNoMembers
    {
        public override bool Equals(object? obj) => obj is AlwaysEqualNoMembers;

        public override int GetHashCode() => 1;
    }

    private sealed class LeafHolder
    {
        public NoMembers Leaf { get; set; } = new();
    }

    private sealed class Holder<TItems>
    {
        public required TItems Items { get; set; }
    }
}
