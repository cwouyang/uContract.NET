using System.Runtime.CompilerServices;
using uContract.Exceptions;

namespace uContract.Tests;

public class EnsureAssignablePrimitiveArrayTests
{
    private const string ItemsChanged =
        "Fields were modified that are not marked as assignable:\n  - Items\n  - <Items>k__BackingField";

    public static TheoryData<Array, Array> ArraysOfEqualContent =>
        new()
        {
            { Of(true, false), Of(true, false) },
            { Of('a', 'b'), Of('a', 'b') },
            { Of<sbyte>(-1, 2), Of<sbyte>(-1, 2) },
            { Of<byte>(1, 2), Of<byte>(1, 2) },
            { Of<short>(-1, 2), Of<short>(-1, 2) },
            { Of<ushort>(1, 2), Of<ushort>(1, 2) },
            { Of(-1, 2), Of(-1, 2) },
            { Of<uint>(1, 2), Of<uint>(1, 2) },
            { Of(-1L, 2L), Of(-1L, 2L) },
            { Of<ulong>(1, 2), Of<ulong>(1, 2) },
            { Of<nint>(-1, 2), Of<nint>(-1, 2) },
            { Of<nuint>(1, 2), Of<nuint>(1, 2) },
            { Of(1.5f, 2f), Of(1.5f, 2f) },
            { Of(1.5, 2.0), Of(1.5, 2.0) },
            { Of(ByteColor.Red, ByteColor.Blue), Of(ByteColor.Red, ByteColor.Blue) },
            { Of(LongSize.Small, LongSize.Huge), Of(LongSize.Small, LongSize.Huge) },
        };

    public static TheoryData<Array, Array> ArraysWithTheLastElementDifferent =>
        new()
        {
            { Of(true, false), Of(true, true) },
            { Of('a', 'b'), Of('a', 'c') },
            { Of<sbyte>(-1, 2), Of<sbyte>(-1, 3) },
            { Of<byte>(1, 2), Of<byte>(1, 3) },
            { Of<short>(-1, 2), Of<short>(-1, 3) },
            { Of<ushort>(1, 2), Of<ushort>(1, 3) },
            { Of(-1, 2), Of(-1, 3) },
            { Of<uint>(1, 2), Of<uint>(1, 3) },
            { Of(-1L, 2L), Of(-1L, 3L) },
            { Of<ulong>(1, 2), Of<ulong>(1, 3) },
            { Of<nint>(-1, 2), Of<nint>(-1, 3) },
            { Of<nuint>(1, 2), Of<nuint>(1, 3) },
            { Of(1f, 1f), Of(1f, 2f) },
            { Of(1.0, 1.0), Of(1.0, 2.0) },
            { Of(ByteColor.Red, ByteColor.Blue), Of(ByteColor.Red, ByteColor.Red) },
            { Of(LongSize.Small, LongSize.Huge), Of(LongSize.Small, LongSize.Small) },
        };

    // Each pair is equal by float.Equals or double.Equals, though == says otherwise for NaN, and the bits
    // differ for 0.0 and -0.0 and for two different NaNs.
    public static TheoryData<Array, Array> FloatingPointArraysEqualByEquals =>
        new()
        {
            { Of(float.NaN), Of(float.NaN) },
            { Of(double.NaN), Of(double.NaN) },
            { Of(0f), Of(-0f) },
            { Of(0.0), Of(-0.0) },
            { Of(float.NaN), Of(BitConverter.Int32BitsToSingle(0x7FC0_0001)) },
            { Of(double.NaN), Of(BitConverter.Int64BitsToDouble(0x7FF8_0000_0000_0001)) },
        };

    [Theory]
    [MemberData(nameof(ArraysOfEqualContent))]
    public void EnsureAssignable_WhenMemberHoldsTwoArraysOfAPrimitiveOrEnumOfEqualContent_DoesNotThrow(
        Array actualItems,
        Array expectedItems
    )
    {
        Holder<Array> actual = new() { Items = actualItems };
        Holder<Array> expected = new() { Items = expectedItems };
        Assert.NotSame(actual.Items, expected.Items);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Theory]
    [MemberData(nameof(ArraysWithTheLastElementDifferent))]
    public void EnsureAssignable_WhenMemberHoldsTwoArraysOfAPrimitiveOrEnumAndOneElementDiffers_ReportsTheMember(
        Array actualItems,
        Array expectedItems
    )
    {
        Holder<Array> actual = new() { Items = actualItems };
        Holder<Array> expected = new() { Items = expectedItems };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Theory]
    [MemberData(nameof(FloatingPointArraysEqualByEquals))]
    public void EnsureAssignable_WhenMemberHoldsTwoFloatingPointArraysOfElementsEqualByEquals_DoesNotThrow(
        Array actualItems,
        Array expectedItems
    )
    {
        Holder<Array> actual = new() { Items = actualItems };
        Holder<Array> expected = new() { Items = expectedItems };

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
