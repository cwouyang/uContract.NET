using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using uContract.Exceptions;

namespace uContract.Tests;

public unsafe class EnsureAssignablePointerArrayTests
{
    private const string ItemsFieldChanged = "Fields were modified that are not marked as assignable:\n  - Items";

    private const string ItemsPropertyChanged =
        "Fields were modified that are not marked as assignable:\n  - Items\n  - <Items>k__BackingField";

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsArraysOfPointersToTheSameAddress_DoesNotThrow()
    {
        int target = 1;
        PointerArrayHolder actual = new() { Items = [&target] };
        PointerArrayHolder expected = new() { Items = [&target] };
        Assert.NotSame(actual.Items, expected.Items);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsArraysOfPointersToDifferentAddresses_ReportsTheMember()
    {
        int first = 1;
        int second = 1;
        PointerArrayHolder actual = new() { Items = [&first] };
        PointerArrayHolder expected = new() { Items = [&second] };
        Assert.NotSame(actual.Items, expected.Items);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsFieldChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsArraysOfPointersOfLengthsOneAndTwoWithTheFirstAddressEqual_ReportsTheMember()
    {
        int first = 1;
        int second = 1;
        PointerArrayHolder actual = new() { Items = [&first] };
        PointerArrayHolder expected = new() { Items = [&first, &second] };
        Assert.NotSame(actual.Items, expected.Items);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsFieldChanged, exception.Description);
    }

    [Fact]
    public void Old_WhenObjectHoldsAnArrayOfPointers_CopiesTheArrayWithItsAddress()
    {
        int target = 1;
        PointerArrayHolder holder = new() { Items = [&target] };

        PointerArrayHolder old = Contract.Old(() => holder);

        Assert.NotSame(holder, old);
        Assert.NotSame(holder.Items, old.Items);
        Assert.True(holder.Items[0] == old.Items[0]);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsAnArrayOfPointersAndNothingChangedSinceOld_DoesNotThrow()
    {
        int target = 1;
        PointerArrayHolder holder = new() { Items = [&target] };
        PointerArrayHolder old = Contract.Old(() => holder);
        Assert.NotSame(holder.Items, old.Items);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(holder, old));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenAnElementOfAnArrayOfPointersIsSetToAnotherAddressSinceOld_ReportsTheMember()
    {
        int first = 1;
        int second = 1;
        PointerArrayHolder holder = new() { Items = [&first] };
        PointerArrayHolder old = Contract.Old(() => holder);
        Assert.NotSame(holder.Items, old.Items);
        holder.Items[0] = &second;

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(holder, old)
        );

        Assert.Equal(ItemsFieldChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTheValueBehindAnAddressInAnArrayOfPointersChangedSinceOld_DoesNotThrow()
    {
        int target = 1;
        PointerArrayHolder holder = new() { Items = [&target] };
        PointerArrayHolder old = Contract.Old(() => holder);
        Assert.NotSame(holder.Items, old.Items);
        target = 2;

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(holder, old));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMembersHoldOneArrayOfPointers_DoesNotThrow()
    {
        int target = 1;
        int*[] items = [&target];
        PointerArrayHolder actual = new() { Items = items };
        PointerArrayHolder expected = new() { Items = items };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoEmptyArraysOfPointers_DoesNotThrow()
    {
        ArrayHolder actual = new() { Items = Pointers(length: 0) };
        ArrayHolder expected = new() { Items = Pointers(length: 0) };
        Assert.NotSame(actual.Items, expected.Items);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoDimensionalArraysOfPointersToTheSameAddresses_DoesNotThrow()
    {
        int first = 1;
        int second = 1;
        PointerGridHolder actual = new() { Items = OneByTwo(&first, &second) };
        PointerGridHolder expected = new() { Items = OneByTwo(&first, &second) };
        Assert.NotSame(actual.Items, expected.Items);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoDimensionalArraysOfPointersWhoseSecondAddressDiffers_ReportsTheMember()
    {
        int first = 1;
        int second = 1;
        PointerGridHolder actual = new() { Items = OneByTwo(&first, &second) };
        PointerGridHolder expected = new() { Items = OneByTwo(&first, &first) };
        Assert.NotSame(actual.Items, expected.Items);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsFieldChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsOneByTwoAndTwoByOneArraysOfPointersToTheSameAddresses_ReportsTheMember()
    {
        int first = 1;
        int second = 1;
        int*[,] twoByOne = new int*[2, 1];
        twoByOne[0, 0] = &first;
        twoByOne[1, 0] = &second;
        PointerGridHolder actual = new() { Items = OneByTwo(&first, &second) };
        PointerGridHolder expected = new() { Items = twoByOne };
        Assert.NotSame(actual.Items, expected.Items);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsFieldChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsArraysOfPointersToDifferentTypesAtTheSameAddress_DoesNotThrow()
    {
        int target = 1;
        int*[] pointersToInt = [&target];
        long*[] pointersToLong = [(long*)&target];
        ArrayHolder actual = new() { Items = pointersToInt };
        ArrayHolder expected = new() { Items = pointersToLong };
        Assert.NotSame(actual.Items, expected.Items);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsAnArrayOfPointersAndAnArrayOfNativeIntegersOfTheSameAddress_ReportsTheMember()
    {
        int target = 1;
        int*[] pointers = [&target];
        nint[] integers = [(nint)(&target)];
        ArrayHolder actual = new() { Items = pointers };
        ArrayHolder expected = new() { Items = integers };
        Assert.NotSame(actual.Items, expected.Items);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsFieldChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsAnArrayOfPointersWithAnElementAndAnEmptyArrayOfNativeIntegers_ReportsTheMember()
    {
        int target = 1;
        int*[] pointers = [&target];
        ArrayHolder actual = new() { Items = pointers };
        ArrayHolder expected = new() { Items = NativeIntegers(length: 0) };
        Assert.NotSame(actual.Items, expected.Items);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsFieldChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsAnArrayOfPointersWithAnElementAndAnEmptyList_ReportsTheMember()
    {
        int target = 1;
        int*[] pointers = [&target];
        Holder<IEnumerable> actual = new() { Items = pointers };
        Holder<IEnumerable> expected = new() { Items = new List<int>() };
        Assert.NotSame(actual.Items, expected.Items);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsPropertyChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsAnArrayOfPointersAndAnArrayOfObjectsHoldingTheBoxedPointer_ReportsTheMember()
    {
        int target = 1;
        int*[] pointers = [&target];
        object[] boxes = [Pointer.Box(&target, typeof(int*))];
        ArrayHolder actual = new() { Items = pointers };
        ArrayHolder expected = new() { Items = boxes };
        Assert.NotSame(actual.Items, expected.Items);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsFieldChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsAnEmptyArrayOfPointersAndAnEmptyList_DoesNotThrow()
    {
        Holder<IEnumerable> actual = new() { Items = Pointers(length: 0) };
        Holder<IEnumerable> expected = new() { Items = new List<int>() };
        Assert.NotSame(actual.Items, expected.Items);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsAnEmptyArrayOfPointersAndAnEmptyArrayOfIntegers_DoesNotThrow()
    {
        ArrayHolder actual = new() { Items = Pointers(length: 0) };
        ArrayHolder expected = new() { Items = Integers(length: 0) };
        Assert.NotSame(actual.Items, expected.Items);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsEmptyArraysOfPointersToDifferentTypes_DoesNotThrow()
    {
        ArrayHolder actual = new() { Items = Pointers(length: 0) };
        ArrayHolder expected = new() { Items = PointersToLong(length: 0) };
        Assert.NotSame(actual.Items, expected.Items);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsAnEmptyArrayOfPointersAndAnEmptyArrayOfNativeIntegers_DoesNotThrow()
    {
        ArrayHolder actual = new() { Items = Pointers(length: 0) };
        ArrayHolder expected = new() { Items = NativeIntegers(length: 0) };
        Assert.NotSame(actual.Items, expected.Items);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsOneAndTwoDimensionalArraysOfPointersToTheSameAddresses_ReportsTheMember()
    {
        int first = 1;
        int second = 1;
        int*[] oneDimensional = [&first, &second];
        ArrayHolder actual = new() { Items = oneDimensional };
        ArrayHolder expected = new() { Items = OneByTwo(&first, &second) };
        Assert.NotSame(actual.Items, expected.Items);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsFieldChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsArraysOfPointersToTheSameAddressesWithLowerBoundsZeroAndOne_ReportsTheMember()
    {
        int first = 1;
        int second = 1;
        int*[] zeroBased = [&first, &second];
        ArrayHolder actual = new() { Items = zeroBased };
        ArrayHolder expected = new() { Items = PairOfPointers(lowerBound: 1, &first, &second) };
        Assert.NotSame(actual.Items, expected.Items);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsFieldChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsArraysOfPointersWithLowerBoundOneToTheSameAddresses_DoesNotThrow()
    {
        int first = 1;
        int second = 1;
        ArrayHolder actual = new() { Items = PairOfPointers(lowerBound: 1, &first, &second) };
        ArrayHolder expected = new() { Items = PairOfPointers(lowerBound: 1, &first, &second) };
        Assert.NotSame(actual.Items, expected.Items);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsArraysOfPointersWithLowerBoundOneWhoseSecondAddressDiffers_ReportsTheMember()
    {
        int first = 1;
        int second = 1;
        ArrayHolder actual = new() { Items = PairOfPointers(lowerBound: 1, &first, &second) };
        ArrayHolder expected = new() { Items = PairOfPointers(lowerBound: 1, &first, &first) };
        Assert.NotSame(actual.Items, expected.Items);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsFieldChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsOneByTwoAndTwoByOneArraysOfNullPointers_ReportsTheMember()
    {
        ArrayHolder actual = new() { Items = new int*[1, 2] };
        ArrayHolder expected = new() { Items = new int*[2, 1] };
        Assert.NotSame(actual.Items, expected.Items);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsFieldChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenElementsAreArraysOfPointersToTheSameAddress_DoesNotThrow()
    {
        int target = 1;
        int*[] actualInner = [&target];
        int*[] expectedInner = [&target];
        Assert.NotSame(actualInner, expectedInner);
        Holder<int*[][]> actual = new() { Items = [actualInner] };
        Holder<int*[][]> expected = new() { Items = [expectedInner] };
        Assert.NotSame(actual.Items, expected.Items);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenElementsAreArraysOfPointersToDifferentAddresses_ReportsTheMember()
    {
        int first = 1;
        int second = 1;
        int*[] actualInner = [&first];
        int*[] expectedInner = [&second];
        Assert.NotSame(actualInner, expectedInner);
        Holder<int*[][]> actual = new() { Items = [actualInner] };
        Holder<int*[][]> expected = new() { Items = [expectedInner] };
        Assert.NotSame(actual.Items, expected.Items);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsPropertyChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenListElementsAreArraysOfPointersToDifferentAddresses_ReportsTheMember()
    {
        int first = 1;
        int second = 1;
        int*[] actualInner = [&first];
        int*[] expectedInner = [&second];
        Assert.NotSame(actualInner, expectedInner);
        Holder<List<int*[]>> actual = new() { Items = [actualInner] };
        Holder<List<int*[]>> expected = new() { Items = [expectedInner] };
        Assert.NotSame(actual.Items, expected.Items);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsPropertyChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenObjectHoldsAPointerFieldAndNothingChangedSinceOld_DoesNotThrow()
    {
        int target = 1;
        PointerHolder holder = new() { P = &target };
        PointerHolder old = Contract.Old(() => holder);
        Assert.NotSame(holder, old);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(holder, old));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTheValueBehindAPointerFieldChangedSinceOld_DoesNotThrow()
    {
        int target = 1;
        PointerHolder holder = new() { P = &target };
        PointerHolder old = Contract.Old(() => holder);
        Assert.NotSame(holder, old);
        target = 2;

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(holder, old));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsArraysOfFunctionPointersToTheSameFunction_DoesNotThrow()
    {
        FunctionPointerArrayHolder actual = new() { Items = [&FirstFunction] };
        FunctionPointerArrayHolder expected = new() { Items = [&FirstFunction] };
        Assert.NotSame(actual.Items, expected.Items);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsArraysOfFunctionPointersToAFunctionAndToNull_ReportsTheMember()
    {
        FunctionPointerArrayHolder actual = new() { Items = [&FirstFunction] };
        FunctionPointerArrayHolder expected = new() { Items = [null] };
        Assert.NotSame(actual.Items, expected.Items);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsFieldChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsArraysOfFunctionPointersToDifferentFunctions_ReportsTheMember()
    {
        FunctionPointerArrayHolder actual = new() { Items = [&FirstFunction] };
        FunctionPointerArrayHolder expected = new() { Items = [&SecondFunction] };
        Assert.NotSame(actual.Items, expected.Items);
        Assert.NotEqual((nint)actual.Items[0], (nint)expected.Items[0]);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsFieldChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsAnArrayOfPointersAndAnArrayOfFunctionPointersOfTheSameAddress_DoesNotThrow()
    {
        delegate* <void> function = &FirstFunction;
        int*[] pointers = [(int*)function];
        delegate* <void>[] functions = [function];
        ArrayHolder actual = new() { Items = pointers };
        ArrayHolder expected = new() { Items = functions };
        Assert.NotSame(actual.Items, expected.Items);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsAnArrayOfFunctionPointersAndNothingChangedSinceOld_DoesNotThrow()
    {
        FunctionPointerArrayHolder holder = new() { Items = [&FirstFunction] };
        FunctionPointerArrayHolder old = Contract.Old(() => holder);
        Assert.NotSame(holder.Items, old.Items);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(holder, old));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenAnElementOfAnArrayOfFunctionPointersIsSetToAnotherFunctionSinceOld_ReportsTheMember()
    {
        FunctionPointerArrayHolder holder = new() { Items = [&FirstFunction] };
        FunctionPointerArrayHolder old = Contract.Old(() => holder);
        Assert.NotSame(holder.Items, old.Items);
        holder.Items[0] = &SecondFunction;
        Assert.NotEqual((nint)holder.Items[0], (nint)old.Items[0]);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(holder, old)
        );

        Assert.Equal(ItemsFieldChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenValuesAreArraysOfFunctionPointersToTheSameFunction_DoesNotThrow()
    {
        delegate* <void>[] actual = [&FirstFunction];
        delegate* <void>[] expected = [&FirstFunction];
        Assert.NotSame(actual, expected);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void Old_WhenObjectHoldsAnArrayOfFunctionPointers_ReturnsACopyWithAnotherArray()
    {
        FunctionPointerArrayHolder holder = new() { Items = [&FirstFunction] };

        FunctionPointerArrayHolder old = Contract.Old(() => holder);

        Assert.NotSame(holder.Items, old.Items);
    }

    [Fact]
    public void Old_WhenObjectHoldsAnArrayOfFunctionPointers_CopiesTheAddress()
    {
        FunctionPointerArrayHolder holder = new() { Items = [&FirstFunction] };

        FunctionPointerArrayHolder old = Contract.Old(() => holder);

        Assert.Equal((nint)holder.Items[0], (nint)old.Items[0]);
    }

    private static void FirstFunction() { }

    private static void SecondFunction() { }

    // Each helper below returns a new array on every call, also for a length of 0.

    private static int*[] Pointers(int length) => new int*[length];

    private static long*[] PointersToLong(int length) => new long*[length];

    private static int[] Integers(int length) => new int[length];

    private static nint[] NativeIntegers(int length) => new nint[length];

    private static int*[,] OneByTwo(int* first, int* second)
    {
        int*[,] grid = new int*[1, 2];
        grid[0, 0] = first;
        grid[0, 1] = second;
        return grid;
    }

    // Array.SetValue rejects a pointer, so the two addresses are written into the array's memory.
    private static Array PairOfPointers(int lowerBound, int* first, int* second)
    {
        int[] lengths = [2];
        int[] lowerBounds = [lowerBound];
#pragma warning disable IL3050 // Test fixture only: the library itself never creates arrays this way.
        Array array = Array.CreateInstance(typeof(int*), lengths, lowerBounds);
#pragma warning restore IL3050
        ref byte start = ref MemoryMarshal.GetArrayDataReference(array);
        Span<nint> addresses = MemoryMarshal.CreateSpan(ref Unsafe.As<byte, nint>(ref start), lengths[0]);
        addresses[0] = (nint)first;
        addresses[1] = (nint)second;
        return array;
    }

    private sealed class PointerArrayHolder
    {
        public required int*[] Items;
    }

    private sealed class PointerGridHolder
    {
        public required int*[,] Items;
    }

    private sealed class FunctionPointerArrayHolder
    {
        public required delegate* <void>[] Items;
    }

    private sealed class ArrayHolder
    {
        public required Array Items;
    }

    private sealed class PointerHolder
    {
        public int* P;
    }

    private sealed class Holder<TItems>
    {
        public required TItems Items { get; set; }
    }
}
