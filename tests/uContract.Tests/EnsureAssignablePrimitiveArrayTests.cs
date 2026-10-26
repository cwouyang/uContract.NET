using System.Runtime.CompilerServices;
using uContract.Exceptions;

namespace uContract.Tests;

public class EnsureAssignablePrimitiveArrayTests
{
    private const int LargeLength = 1_000_000;

    // Far below one byte per element: boxing the elements of the two arrays allocates tens of bytes per element.
    private const long AllocationBound = LargeLength / 10;

    private const string ItemsChanged =
        "Fields were modified that are not marked as assignable:\n  - Items\n  - <Items>k__BackingField";

    public static TheoryData<ArrayPair> ArraysOfEqualContent =>
        [
            new ArrayPair("bool", Of(true, false), Of(true, false)),
            new ArrayPair("char", Of('a', 'b'), Of('a', 'b')),
            new ArrayPair("sbyte", Of<sbyte>(-1, 2), Of<sbyte>(-1, 2)),
            new ArrayPair("byte", Of<byte>(1, 2), Of<byte>(1, 2)),
            new ArrayPair("short", Of<short>(-1, 2), Of<short>(-1, 2)),
            new ArrayPair("ushort", Of<ushort>(1, 2), Of<ushort>(1, 2)),
            new ArrayPair("int", Of(-1, 2), Of(-1, 2)),
            new ArrayPair("uint", Of<uint>(1, 2), Of<uint>(1, 2)),
            new ArrayPair("long", Of(-1L, 2L), Of(-1L, 2L)),
            new ArrayPair("ulong", Of<ulong>(1, 2), Of<ulong>(1, 2)),
            new ArrayPair("nint", Of<nint>(-1, 2), Of<nint>(-1, 2)),
            new ArrayPair("nuint", Of<nuint>(1, 2), Of<nuint>(1, 2)),
            new ArrayPair("float", Of(1.5f, 2f), Of(1.5f, 2f)),
            new ArrayPair("double", Of(1.5, 2.0), Of(1.5, 2.0)),
            new ArrayPair("enum : byte", Of(ByteColor.Red, ByteColor.Blue), Of(ByteColor.Red, ByteColor.Blue)),
            new ArrayPair("enum : long", Of(LongSize.Small, LongSize.Huge), Of(LongSize.Small, LongSize.Huge)),
        ];

    public static TheoryData<ArrayPair> ArraysWithTheLastElementDifferent =>
        [
            new ArrayPair("bool", Of(true, false), Of(true, true)),
            new ArrayPair("char", Of('a', 'b'), Of('a', 'c')),
            new ArrayPair("sbyte", Of<sbyte>(-1, 2), Of<sbyte>(-1, 3)),
            new ArrayPair("byte", Of<byte>(1, 2), Of<byte>(1, 3)),
            new ArrayPair("short", Of<short>(-1, 2), Of<short>(-1, 3)),
            new ArrayPair("ushort", Of<ushort>(1, 2), Of<ushort>(1, 3)),
            new ArrayPair("int", Of(-1, 2), Of(-1, 3)),
            new ArrayPair("uint", Of<uint>(1, 2), Of<uint>(1, 3)),
            new ArrayPair("long", Of(-1L, 2L), Of(-1L, 3L)),
            new ArrayPair("ulong", Of<ulong>(1, 2), Of<ulong>(1, 3)),
            new ArrayPair("nint", Of<nint>(-1, 2), Of<nint>(-1, 3)),
            new ArrayPair("nuint", Of<nuint>(1, 2), Of<nuint>(1, 3)),
            new ArrayPair("float", Of(1f, 1f), Of(1f, 2f)),
            new ArrayPair("double", Of(1.0, 1.0), Of(1.0, 2.0)),
            new ArrayPair("enum : byte", Of(ByteColor.Red, ByteColor.Blue), Of(ByteColor.Red, ByteColor.Red)),
            new ArrayPair("enum : long", Of(LongSize.Small, LongSize.Huge), Of(LongSize.Small, LongSize.Small)),
        ];

    // Each pair is equal by float.Equals or double.Equals, though == says otherwise for NaN, and the bits
    // differ for 0.0 and -0.0 and for two different NaNs.
    public static TheoryData<ArrayPair> FloatingPointArraysEqualByEquals =>
        [
            new ArrayPair("float NaN and NaN", Of(float.NaN), Of(float.NaN)),
            new ArrayPair("double NaN and NaN", Of(double.NaN), Of(double.NaN)),
            new ArrayPair("float 0 and -0", Of(0f), Of(-0f)),
            new ArrayPair("double 0 and -0", Of(0.0), Of(-0.0)),
            new ArrayPair(
                "float NaNs of different bits",
                Of(float.NaN),
                Of(BitConverter.Int32BitsToSingle(0x7FC0_0001))
            ),
            new ArrayPair(
                "double NaNs of different bits",
                Of(double.NaN),
                Of(BitConverter.Int64BitsToDouble(0x7FF8_0000_0000_0001))
            ),
        ];

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfAMillionEqualBytes_AllocatesFarLessThanOneBytePerElement()
    {
        byte[] actual = new byte[LargeLength];
        byte[] expected = new byte[LargeLength];

        long allocated = AllocatedBy(() => Contract.EnsureAssignable(actual, expected));

        Assert.InRange(allocated, 0, AllocationBound);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfAMillionEqualDoubles_AllocatesFarLessThanOneBytePerElement()
    {
        double[] actual = new double[LargeLength];
        double[] expected = new double[LargeLength];

        long allocated = AllocatedBy(() => Contract.EnsureAssignable(actual, expected));

        Assert.InRange(allocated, 0, AllocationBound);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfAMillionEqualEnumValues_AllocatesFarLessThanOneBytePerElement()
    {
        LongSize[] actual = new LongSize[LargeLength];
        LongSize[] expected = new LongSize[LargeLength];

        long allocated = AllocatedBy(() => Contract.EnsureAssignable(actual, expected));

        Assert.InRange(allocated, 0, AllocationBound);
    }

    [Theory]
    [MemberData(nameof(ArraysOfEqualContent))]
    public void EnsureAssignable_WhenMemberHoldsTwoArraysOfAPrimitiveOrEnumOfEqualContent_DoesNotThrow(ArrayPair pair)
    {
        Holder<Array> actual = new() { Items = pair.Actual };
        Holder<Array> expected = new() { Items = pair.Expected };
        Assert.NotSame(actual.Items, expected.Items);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Theory]
    [MemberData(nameof(ArraysWithTheLastElementDifferent))]
    public void EnsureAssignable_WhenMemberHoldsTwoArraysOfAPrimitiveOrEnumAndOneElementDiffers_ReportsTheMember(
        ArrayPair pair
    )
    {
        Holder<Array> actual = new() { Items = pair.Actual };
        Holder<Array> expected = new() { Items = pair.Expected };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Theory]
    [MemberData(nameof(FloatingPointArraysEqualByEquals))]
    public void EnsureAssignable_WhenMemberHoldsTwoFloatingPointArraysOfElementsEqualByEquals_DoesNotThrow(
        ArrayPair pair
    )
    {
        Holder<Array> actual = new() { Items = pair.Actual };
        Holder<Array> expected = new() { Items = pair.Expected };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsABoolStoredAsTwoAgainstTrue_ReportsTheMember()
    {
        bool[] actualItems = [true];
        Unsafe.As<bool, byte>(ref actualItems[0]) = 2;
        Holder<Array> actual = new() { Items = actualItems };
        Holder<Array> expected = new() { Items = new[] { true } };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoDimensionalArraysOfIntegersOfEqualContent_DoesNotThrow()
    {
        Holder<int[,]> actual = new() { Items = TwoByTwo(last: 4) };
        Holder<int[,]> expected = new() { Items = TwoByTwo(last: 4) };
        Assert.NotSame(actual.Items, expected.Items);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoDimensionalArraysOfIntegersAndOneElementDiffers_ReportsTheMember()
    {
        Holder<int[,]> actual = new() { Items = TwoByTwo(last: 4) };
        Holder<int[,]> expected = new() { Items = TwoByTwo(last: 5) };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsArraysWithLowerBoundOneAndOneElementDiffers_ReportsTheMember()
    {
        Holder<Array> actual = new() { Items = OneBased(1, 2) };
        Holder<Array> expected = new() { Items = OneBased(1, 3) };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfBytesAndOneElementDiffers_ReportsTheTwoAsNotEqual()
    {
        byte[] actual = [1, 2];
        byte[] expected = [1, 3];

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(byte[])), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfBytesWithoutElements_DoesNotThrow()
    {
        byte[] actual = [];
        byte[] expected = Bytes(length: 0);
        Assert.NotSame(actual, expected);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsArrayAndTwoArraysOfDoublesHoldNaN_DoesNotThrow()
    {
        Array actual = new[] { double.NaN };
        Array expected = new[] { double.NaN };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsAnArrayOfIntegersAgainstAnArrayOfUnsignedIntegersOfTheSameNumbers_ReportsTheMember()
    {
        Holder<Array> actual = new() { Items = new[] { 1, 2 } };
        Holder<Array> expected = new() { Items = new uint[] { 1, 2 } };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsAnArrayOfAnEnumAgainstAnArrayOfItsUnderlyingTypeOfTheSameValues_ReportsTheMember()
    {
        Holder<Array> actual = new() { Items = new[] { ByteColor.Red, ByteColor.Blue } };
        Holder<Array> expected = new() { Items = new byte[] { (byte)ByteColor.Red, (byte)ByteColor.Blue } };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    // The call runs once before it is measured, so caches filled on the first call are not counted.
    private static long AllocatedBy(Action act)
    {
        act();
        long before = GC.GetAllocatedBytesForCurrentThread();
        act();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static T[] Of<T>(params T[] items) => items;

    // Returns a new array on every call, also for a length of 0.
    private static byte[] Bytes(int length) => new byte[length];

    private static string NotEqual(Type type) =>
        $"actual and expected are not equal. {type} is compared as a whole, so assignable patterns do not apply.";

    private static int[,] TwoByTwo(int last) =>
        new[,]
        {
            { 1, 2 },
            { 3, last },
        };

    private static Array OneBased(params int[] values)
    {
        int[] lengths = [values.Length];
        int[] lowerBounds = [1];
#pragma warning disable IL3050 // Test fixture only: the library itself never creates arrays this way.
        Array array = Array.CreateInstance(typeof(int), lengths, lowerBounds);
#pragma warning restore IL3050
        for (int i = 0; i < values.Length; i++)
        {
            array.SetValue(values[i], 1 + i);
        }

        return array;
    }

    // A pair of arrays for a theory row, displayed by its label: the arrays of several element types display alike.
    public sealed record ArrayPair(string Label, Array Actual, Array Expected)
    {
        public override string ToString() => Label;
    }

    public enum ByteColor : byte
    {
        Red = 1,
        Blue = 200,
    }

    public enum LongSize : long
    {
        Small = -1,
        Huge = long.MaxValue,
    }

    private sealed class Holder<TItems>
    {
        public required TItems Items { get; set; }
    }
}
