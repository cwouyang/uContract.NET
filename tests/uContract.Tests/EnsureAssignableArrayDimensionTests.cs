using System.Collections;
using System.Collections.Immutable;
using uContract.Exceptions;

namespace uContract.Tests;

public class EnsureAssignableArrayDimensionTests
{
    private const string ItemsChanged =
        "Fields were modified that are not marked as assignable:\n  - Items\n  - <Items>k__BackingField";

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsOneByTwoAndTwoByOneArraysOfTheSameElements_ReportsTheMember()
    {
        int[,] actualItems =
        {
            { 1, 2 },
        };
        int[,] expectedItems =
        {
            { 1 },
            { 2 },
        };
        Assert.NotSame(actualItems, expectedItems);
        Holder<int[,]> actual = new() { Items = actualItems };
        Holder<int[,]> expected = new() { Items = expectedItems };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsAnEmptyArrayOfArraysAndAnEmptyTwoDimensionalArray_ReportsTheMember()
    {
        Holder<Array> actual = new() { Items = ArrayOfArrays(length: 0) };
        Holder<Array> expected = new() { Items = Grid(rows: 0, columns: 0) };
        Assert.NotSame(actual.Items, expected.Items);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsEmptyArraysThatDifferInTheLengthOfTheSecondDimension_ReportsTheMember()
    {
        Holder<int[,]> actual = new() { Items = Grid(rows: 0, columns: 3) };
        Holder<int[,]> expected = new() { Items = Grid(rows: 0, columns: 5) };
        Assert.NotSame(actual.Items, expected.Items);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsArraysOfEqualElementsWithLowerBoundsZeroAndOne_ReportsTheMember()
    {
        int[] zeroBased = [1, 2];
        Array oneBased = OneDimensional(lowerBound: 1, 1, 2);
        Assert.NotSame(zeroBased, oneBased);
        Holder<Array> actual = new() { Items = zeroBased };
        Holder<Array> expected = new() { Items = oneBased };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoByThreeAndThreeByTwoArraysOfTheSameElements_ReportsTheMember()
    {
        int[,] actualItems =
        {
            { 1, 2, 3 },
            { 4, 5, 6 },
        };
        int[,] expectedItems =
        {
            { 1, 2 },
            { 3, 4 },
            { 5, 6 },
        };
        Assert.NotSame(actualItems, expectedItems);
        Holder<int[,]> actual = new() { Items = actualItems };
        Holder<int[,]> expected = new() { Items = expectedItems };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsEmptyTwoByZeroAndZeroByTwoArrays_ReportsTheMember()
    {
        Holder<int[,]> actual = new() { Items = Grid(rows: 2, columns: 0) };
        Holder<int[,]> expected = new() { Items = Grid(rows: 0, columns: 2) };
        Assert.NotSame(actual.Items, expected.Items);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoByTwoArraysOfEqualElements_DoesNotThrow()
    {
        int[,] actualItems =
        {
            { 1, 2 },
            { 3, 4 },
        };
        int[,] expectedItems =
        {
            { 1, 2 },
            { 3, 4 },
        };
        Assert.NotSame(actualItems, expectedItems);
        Holder<int[,]> actual = new() { Items = actualItems };
        Holder<int[,]> expected = new() { Items = expectedItems };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoByTwoArraysWithOneDifferentElement_ReportsTheMember()
    {
        int[,] actualItems =
        {
            { 1, 2 },
            { 3, 4 },
        };
        int[,] expectedItems =
        {
            { 1, 2 },
            { 3, 5 },
        };
        Assert.NotSame(actualItems, expectedItems);
        Holder<int[,]> actual = new() { Items = actualItems };
        Holder<int[,]> expected = new() { Items = expectedItems };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsThreeDimensionalArraysThatDifferInTheLengthsOfTheLaterDimensions_ReportsTheMember()
    {
        Holder<int[,,]> actual = new() { Items = new int[1, 2, 3] };
        Holder<int[,,]> expected = new() { Items = new int[1, 3, 2] };
        Assert.NotSame(actual.Items, expected.Items);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsAOneDimensionalAndATwoDimensionalArrayOfTheSameElements_ReportsTheMember()
    {
        int[] oneDimensional = [1, 2];
        int[,] twoDimensional = OneByTwo();
        Assert.NotSame(oneDimensional, twoDimensional);
        Holder<Array> actual = new() { Items = oneDimensional };
        Holder<Array> expected = new() { Items = twoDimensional };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsATwoDimensionalAndAThreeDimensionalArrayOfTheSameElements_ReportsTheMember()
    {
        int[,] twoDimensional = OneByTwo();
        int[,,] threeDimensional =
        {
            {
                { 1, 2 },
            },
        };
        Assert.NotSame(twoDimensional, threeDimensional);
        Holder<Array> actual = new() { Items = twoDimensional };
        Holder<Array> expected = new() { Items = threeDimensional };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsArraysOfEqualElementsWithLowerBoundOne_DoesNotThrow()
    {
        Holder<Array> actual = new() { Items = OneDimensional(lowerBound: 1, 1, 2) };
        Holder<Array> expected = new() { Items = OneDimensional(lowerBound: 1, 1, 2) };
        Assert.NotSame(actual.Items, expected.Items);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsArraysOfEqualElementsWithLowerBoundsOneAndTwo_ReportsTheMember()
    {
        Holder<Array> actual = new() { Items = OneDimensional(lowerBound: 1, 1, 2) };
        Holder<Array> expected = new() { Items = OneDimensional(lowerBound: 2, 1, 2) };
        Assert.NotSame(actual.Items, expected.Items);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoDimensionalArraysOfEqualElementsWithLowerBoundsZeroAndOne_ReportsTheMember()
    {
        int[,] zeroBased = OneByTwo();
        Array oneBased = TwoDimensional(rowLowerBound: 1, columnLowerBound: 1, OneByTwo());
        Assert.NotSame(zeroBased, oneBased);
        Holder<Array> actual = new() { Items = zeroBased };
        Holder<Array> expected = new() { Items = oneBased };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoDimensionalArraysThatDifferInTheLowerBoundOfTheSecondDimension_ReportsTheMember()
    {
        Holder<Array> actual = new() { Items = TwoDimensional(rowLowerBound: 0, columnLowerBound: 0, OneByTwo()) };
        Holder<Array> expected = new() { Items = TwoDimensional(rowLowerBound: 0, columnLowerBound: 1, OneByTwo()) };
        Assert.NotSame(actual.Items, expected.Items);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsEmptyArraysWithLowerBoundsZeroAndOne_ReportsTheMember()
    {
        int[] zeroBased = ArrayOfNumbers(length: 0);
        Array oneBased = OneDimensional(lowerBound: 1);
        Assert.NotSame(zeroBased, oneBased);
        Holder<Array> actual = new() { Items = zeroBased };
        Holder<Array> expected = new() { Items = oneBased };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsAnIntArrayAndAnObjectArrayOfEqualElements_DoesNotThrow()
    {
        int[] numbers = [1, 2];
        object[] boxedNumbers = [1, 2];
        Assert.NotSame(numbers, boxedNumbers);
        Holder<Array> actual = new() { Items = numbers };
        Holder<Array> expected = new() { Items = boxedNumbers };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsAnIntArrayAndALongArrayOfTheSameNumbers_ReportsTheMember()
    {
        int[] numbers = [1, 2];
        long[] longNumbers = [1L, 2L];
        Assert.NotSame(numbers, longNumbers);
        Holder<Array> actual = new() { Items = numbers };
        Holder<Array> expected = new() { Items = longNumbers };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsATwoDimensionalArrayAndAListOfTheSameElements_DoesNotThrow()
    {
        int[,] array = OneByTwo();
        List<int> list = [1, 2];
        Assert.NotSame(array, list);
        Holder<IEnumerable> actual = new() { Items = array };
        Holder<IEnumerable> expected = new() { Items = list };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsAListAndAOneDimensionalArrayOfTheSameElements_DoesNotThrow()
    {
        List<int> list = [1, 2];
        int[] array = [1, 2];
        Assert.NotSame(list, array);
        Holder<IEnumerable> actual = new() { Items = list };
        Holder<IEnumerable> expected = new() { Items = array };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberDeclaredAsEnumerableHoldsATwoDimensionalAndAOneDimensionalArray_ReportsTheMember()
    {
        int[,] twoDimensional = OneByTwo();
        int[] oneDimensional = [1, 2];
        Assert.NotSame(twoDimensional, oneDimensional);
        Holder<IEnumerable> actual = new() { Items = twoDimensional };
        Holder<IEnumerable> expected = new() { Items = oneDimensional };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberDeclaredAsObjectHoldsOneByTwoAndTwoByOneArrays_ReportsTheMember()
    {
        Holder<object> actual = new() { Items = OneByTwo() };
        Holder<object> expected = new() { Items = TwoByOne() };
        Assert.NotSame(actual.Items, expected.Items);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenListElementsAreOneByTwoAndTwoByOneArrays_ReportsTheMember()
    {
        Holder<List<int[,]>> actual = new() { Items = [OneByTwo()] };
        Holder<List<int[,]>> expected = new() { Items = [TwoByOne()] };
        Assert.NotSame(actual.Items, expected.Items);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenDictionaryValuesAreOneByTwoAndTwoByOneArrays_ReportsTheMember()
    {
        Holder<Dictionary<string, int[,]>> actual = new()
        {
            Items = new Dictionary<string, int[,]>(StringComparer.Ordinal) { ["grid"] = OneByTwo() },
        };
        Holder<Dictionary<string, int[,]>> expected = new()
        {
            Items = new Dictionary<string, int[,]>(StringComparer.Ordinal) { ["grid"] = TwoByOne() },
        };
        Assert.NotSame(actual.Items, expected.Items);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenDictionaryKeysAreOneByTwoAndTwoByOneArrays_ReportsTheMember()
    {
        Holder<Dictionary<int[,], int>> actual = new() { Items = new Dictionary<int[,], int> { [OneByTwo()] = 1 } };
        Holder<Dictionary<int[,], int>> expected = new() { Items = new Dictionary<int[,], int> { [TwoByOne()] = 1 } };
        Assert.NotSame(actual.Items, expected.Items);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenStructFieldsHoldOneByTwoAndTwoByOneArrays_ReportsTheMember()
    {
        Holder<Board> actual = new() { Items = new Board { Cells = OneByTwo() } };
        Holder<Board> expected = new() { Items = new Board { Cells = TwoByOne() } };
        Assert.NotSame(actual.Items.Cells, expected.Items.Cells);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenImmutableArrayElementsAreOneByTwoAndTwoByOneArrays_ReportsTheMember()
    {
        Holder<ImmutableArray<int[,]>> actual = new() { Items = [OneByTwo()] };
        Holder<ImmutableArray<int[,]>> expected = new() { Items = [TwoByOne()] };
        Assert.NotSame(actual.Items[0], expected.Items[0]);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenArrayElementsAreOneByTwoAndTwoByOneArrays_ReportsTheMember()
    {
        Holder<int[][,]> actual = new() { Items = [OneByTwo()] };
        Holder<int[][,]> expected = new() { Items = [TwoByOne()] };
        Assert.NotSame(actual.Items, expected.Items);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsATwoByTwoArrayAndNothingChangedSinceOld_DoesNotThrow()
    {
        Holder<int[,]> holder = new()
        {
            Items = new[,]
            {
                { 1, 2 },
                { 3, 4 },
            },
        };
        Holder<int[,]> old = Contract.Old(() => holder);
        Assert.NotSame(holder.Items, old.Items);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(holder, old));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsATwoByThreeArrayAndNothingChangedSinceOld_DoesNotThrow()
    {
        Holder<int[,]> holder = new() { Items = Grid(rows: 2, columns: 3) };
        Holder<int[,]> old = Contract.Old(() => holder);
        Assert.NotSame(holder.Items, old.Items);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(holder, old));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsAnArrayWithLowerBoundOneAndNothingChangedSinceOld_DoesNotThrow()
    {
        Holder<Array> holder = new() { Items = OneDimensional(lowerBound: 1, 1, 2) };
        Holder<Array> old = Contract.Old(() => holder);
        Assert.NotSame(holder.Items, old.Items);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(holder, old));

        Assert.Null(exception);
    }

    [Fact]
    public unsafe void EnsureAssignable_WhenMemberHoldsEmptyArraysOfPointersThatDifferInTheLengthOfTheSecondDimension_ReportsTheMember()
    {
        Holder<Array> actual = new() { Items = new int*[0, 3] };
        Holder<Array> expected = new() { Items = new int*[0, 5] };
        Assert.NotSame(actual.Items, expected.Items);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsEmptyArraysThatDifferInTheLengthOfTheFirstDimension_ReportsTheMember()
    {
        Holder<int[,]> actual = new() { Items = Grid(rows: 2, columns: 0) };
        Holder<int[,]> expected = new() { Items = Grid(rows: 3, columns: 0) };
        Assert.NotSame(actual.Items, expected.Items);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    // ICollection as T is compared by the properties it declares: Count, IsSynchronized and SyncRoot. The two
    // arrays hold the same number of elements, and SyncRoot of an array is the array itself: it leads back
    // to the two values passed to the method, which counts as equal. The rank, the lengths and the lower
    // bounds of the two arrays are not compared in such a call.
    [Fact]
    public void EnsureAssignable_WhenOneByTwoAndTwoByOneArraysArePassedAsCollections_DoesNotThrow()
    {
        ICollection actual = OneByTwo();
        ICollection expected = TwoByOne();
        Assert.NotSame(actual, expected);
        Assert.Equal(expected.Count, actual.Count);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    // Each helper below returns a new array on every call, also for a length of 0.

    private static int[,] OneByTwo() =>
        new[,]
        {
            { 1, 2 },
        };

    private static int[,] TwoByOne() =>
        new[,]
        {
            { 1 },
            { 2 },
        };

    private static int[] ArrayOfNumbers(int length) => new int[length];

    private static int[][] ArrayOfArrays(int length) => new int[length][];

    private static int[,] Grid(int rows, int columns) => new int[rows, columns];

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

    private static Array TwoDimensional(int rowLowerBound, int columnLowerBound, int[,] values)
    {
        int[] lengths = [values.GetLength(0), values.GetLength(1)];
        int[] lowerBounds = [rowLowerBound, columnLowerBound];
#pragma warning disable IL3050 // Test fixture only: the library itself never creates arrays this way.
        Array array = Array.CreateInstance(typeof(int), lengths, lowerBounds);
#pragma warning restore IL3050
        for (int row = 0; row < lengths[0]; row++)
        {
            for (int column = 0; column < lengths[1]; column++)
            {
                array.SetValue(values[row, column], rowLowerBound + row, columnLowerBound + column);
            }
        }

        return array;
    }

    private struct Board
    {
        public int[,] Cells;
    }

    private sealed class Holder<TItems>
    {
        public required TItems Items { get; set; }
    }
}
