using System.Collections;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using uContract.Exceptions;

namespace uContract.Tests;

public unsafe class EnsureImmutableCollectionArrayTests
{
    private const string ViolationDescription = "Ensure resultingCollection is an immutable collection";

    [Fact]
    public void EnsureImmutableCollection_WhenArrayOfImmutableLists_ReportsViolation()
    {
        ImmutableList<int>[] argument = [ImmutableList.Create(1)];

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenArrayOfImmutableArrays_ReportsViolation()
    {
        ImmutableArray<int>[] argument = [ImmutableArray.Create(1)];

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenJaggedArrayOfImmutableLists_ReportsViolation()
    {
        ImmutableList<int>[][] argument =
        [
            [ImmutableList.Create(1)],
        ];

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenTwoDimensionalArrayOfImmutableLists_ReportsViolation()
    {
        ImmutableList<int>[,] argument = new ImmutableList<int>[1, 1];

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenArrayOfImmutableListsIsDeclaredAsObject_ReportsViolation()
    {
        object argument = new[] { ImmutableList.Create(1) };

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenArrayOfReadOnlyCollections_ReportsViolation()
    {
        ReadOnlyCollection<int>[] argument = [new ReadOnlyCollection<int>([1])];

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenArrayOfReadOnlyMemory_ReportsViolation()
    {
        ReadOnlyMemory<int>[] argument = new ReadOnlyMemory<int>[1];

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenArrayOfClassWhoseNameStartsWithReadOnly_ReportsViolation()
    {
        ReadOnlyLookingBag[] argument = [new ReadOnlyLookingBag()];

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenArrayOfFunctionPointers_ReportsViolation()
    {
        delegate* <void>[] argument = [&TargetFunction];

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenJaggedArrayOfFunctionPointers_ReportsViolation()
    {
        delegate* <void>[][] argument =
        [
            [&TargetFunction],
        ];

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenTwoDimensionalArrayOfFunctionPointers_ReportsViolation()
    {
        delegate* <void>[,] argument = new delegate* <void>[1, 1];

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenArrayOfFrozenSets_ReportsViolation()
    {
        FrozenSet<int> element = ImmutableArray.Create(1).ToFrozenSet();
        FrozenSet<int>[] argument = [element];

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenArrayOfOwnImmutableStacks_ReportsViolation()
    {
        OwnImmutableStack[] argument = [new OwnImmutableStack()];

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    private static void TargetFunction() { }

    private static void AssertViolation(Action call)
    {
        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(call);

        Assert.Equal(ViolationDescription, exception.Description);
    }

    private sealed class ReadOnlyLookingBag
    {
        public List<int> Items { get; } = [];
    }

    private sealed class OwnImmutableStack : IImmutableStack<int>
    {
        public bool IsEmpty => throw new NotSupportedException();

        public IImmutableStack<int> Clear() => throw new NotSupportedException();

        public int Peek() => throw new NotSupportedException();

        public IImmutableStack<int> Pop() => throw new NotSupportedException();

        public IImmutableStack<int> Push(int value) => throw new NotSupportedException();

        public IEnumerator<int> GetEnumerator() => throw new NotSupportedException();

        IEnumerator IEnumerable.GetEnumerator() => throw new NotSupportedException();
    }
}
