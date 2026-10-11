using System.Collections;
using System.Collections.Immutable;
using uContract.Exceptions;

namespace uContract.Tests;

public class EnsureImmutableCollectionInterfaceTests
{
    private const string ViolationDescription = "Ensure resultingCollection is an immutable collection";

    [Fact]
    public void EnsureImmutableCollection_WhenClassImplementsImmutableStack_ReturnsCollection()
    {
        StackOfTheCaller argument = new();

        StackOfTheCaller result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenClassImplementingImmutableStackIsDeclaredAsTheInterface_ReturnsCollection()
    {
        IImmutableStack<int> argument = new StackOfTheCaller();

        IImmutableStack<int> result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public unsafe void EnsureImmutableCollection_WhenGenericImmutableStackOfFunctionPointerArrays_ReturnsCollection()
    {
        GenericStackOfTheCaller<delegate* <void>[]> argument = new();

        GenericStackOfTheCaller<delegate* <void>[]> result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenGenericImmutableStackOfStrings_ReturnsCollection()
    {
        GenericStackOfTheCaller<string> argument = new();

        GenericStackOfTheCaller<string> result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    private sealed class GenericStackOfTheCaller<T> : IImmutableStack<T>
    {
        public bool IsEmpty => throw new NotSupportedException();

        public IImmutableStack<T> Clear() => throw new NotSupportedException();

        public T Peek() => throw new NotSupportedException();

        public IImmutableStack<T> Pop() => throw new NotSupportedException();

        public IImmutableStack<T> Push(T value) => throw new NotSupportedException();

        public IEnumerator<T> GetEnumerator() => throw new NotSupportedException();

        IEnumerator IEnumerable.GetEnumerator() => throw new NotSupportedException();
    }

    [Fact]
    public void EnsureImmutableCollection_WhenBaseClassImplementsImmutableStack_ReturnsCollection()
    {
        DerivedStackOfTheCaller argument = new();

        DerivedStackOfTheCaller result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    private class StackBaseOfTheCaller : IImmutableStack<int>
    {
        public bool IsEmpty => throw new NotSupportedException();

        public IImmutableStack<int> Clear() => throw new NotSupportedException();

        public int Peek() => throw new NotSupportedException();

        public IImmutableStack<int> Pop() => throw new NotSupportedException();

        public IImmutableStack<int> Push(int value) => throw new NotSupportedException();

        public IEnumerator<int> GetEnumerator() => throw new NotSupportedException();

        IEnumerator IEnumerable.GetEnumerator() => throw new NotSupportedException();
    }

    private sealed class DerivedStackOfTheCaller : StackBaseOfTheCaller;

    [Fact]
    public void EnsureImmutableCollection_WhenClassImplementsImmutableStackExplicitly_ReturnsCollection()
    {
        ExplicitStackOfTheCaller argument = new();

        ExplicitStackOfTheCaller result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    private sealed class ExplicitStackOfTheCaller : IImmutableStack<int>
    {
        bool IImmutableStack<int>.IsEmpty => throw new NotSupportedException();

        IImmutableStack<int> IImmutableStack<int>.Clear() => throw new NotSupportedException();

        int IImmutableStack<int>.Peek() => throw new NotSupportedException();

        IImmutableStack<int> IImmutableStack<int>.Pop() => throw new NotSupportedException();

        IImmutableStack<int> IImmutableStack<int>.Push(int value) => throw new NotSupportedException();

        IEnumerator<int> IEnumerable<int>.GetEnumerator() => throw new NotSupportedException();

        IEnumerator IEnumerable.GetEnumerator() => throw new NotSupportedException();
    }

    [Fact]
    public void EnsureImmutableCollection_WhenClassImplementsImmutableListAndItsMembersThrow_ReturnsCollection()
    {
        ListOfTheCaller argument = new();

        ListOfTheCaller result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenClassImplementsImmutableSet_ReturnsCollection()
    {
        SetOfTheCaller argument = new();

        SetOfTheCaller result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenClassImplementsImmutableDictionary_ReturnsCollection()
    {
        DictionaryOfTheCaller argument = new();

        DictionaryOfTheCaller result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenStructImplementsImmutableQueue_ReturnsCollection()
    {
        const int marker = 7;
        QueueOfTheCaller argument = new(marker);

        QueueOfTheCaller result = Contract.EnsureImmutableCollection(argument);

        Assert.Equal(marker, result.Marker);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenBuilderOfImmutableList_ThrowsPostconditionViolationException()
    {
        ImmutableList<int>.Builder argument = ImmutableList.CreateBuilder<int>();

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenBuilderOfImmutableArray_ThrowsPostconditionViolationException()
    {
        ImmutableArray<int>.Builder argument = ImmutableArray.CreateBuilder<int>();

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenBuilderOfImmutableDictionary_ThrowsPostconditionViolationException()
    {
        ImmutableDictionary<int, int>.Builder argument = ImmutableDictionary.CreateBuilder<int, int>();

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenBuilderOfImmutableHashSet_ThrowsPostconditionViolationException()
    {
        ImmutableHashSet<int>.Builder argument = ImmutableHashSet.CreateBuilder<int>();

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenBuilderOfImmutableSortedSet_ThrowsPostconditionViolationException()
    {
        ImmutableSortedSet<int>.Builder argument = ImmutableSortedSet.CreateBuilder<int>();

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenBuilderOfImmutableSortedDictionary_ThrowsPostconditionViolationException()
    {
        ImmutableSortedDictionary<int, int>.Builder argument = ImmutableSortedDictionary.CreateBuilder<int, int>();

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenKeysOfImmutableDictionary_ThrowsPostconditionViolationException()
    {
        IEnumerable<int> argument = ImmutableDictionary<int, int>.Empty.Add(1, 1).Keys;

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenValuesOfImmutableDictionary_ThrowsPostconditionViolationException()
    {
        IEnumerable<int> argument = ImmutableDictionary<int, int>.Empty.Add(1, 1).Values;

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenReverseOfImmutableSortedSet_ThrowsPostconditionViolationException()
    {
        IEnumerable<int> argument = ImmutableSortedSet.Create(1, 2).Reverse();

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenEnumeratorOfImmutableList_ThrowsPostconditionViolationException()
    {
        ImmutableList<int>.Enumerator argument = ImmutableList.Create(1).GetEnumerator();

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenEnumeratorOfImmutableArray_ThrowsPostconditionViolationException()
    {
        ImmutableArray<int>.Enumerator argument = ImmutableArray.Create(1).GetEnumerator();

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenDefaultImmutableArray_ReturnsCollection()
    {
        ImmutableArray<int> argument = default;

        ImmutableArray<int> result = Contract.EnsureImmutableCollection(argument);

        Assert.True(result.IsDefault);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenNullableImmutableArrayWithValue_ReturnsCollection()
    {
        ImmutableArray<int> content = ImmutableArray.Create(1);
        ImmutableArray<int>? argument = content;

        ImmutableArray<int>? result = Contract.EnsureImmutableCollection(argument);

        Assert.True(result.HasValue);
        Assert.True(content.Equals(result.Value));
    }

    [Fact]
    public unsafe void EnsureImmutableCollection_WhenImmutableListOfFunctionPointerArrays_ReturnsCollection()
    {
        delegate* <void>[] element = [&TargetFunction];
        ImmutableList<delegate* <void>[]> argument = ImmutableList.Create(element);

        ImmutableList<delegate* <void>[]> result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    private static void TargetFunction() { }

    [Fact]
    public void EnsureImmutableCollection_WhenPlainClassWithPublicList_ThrowsPostconditionViolationException()
    {
        BagOfTheCaller argument = new();

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    private sealed class BagOfTheCaller
    {
        public List<int> Items { get; } = [];
    }

    [Fact]
    public void EnsureImmutableCollection_WhenCollectionThatSaysItIsReadOnly_ThrowsPostconditionViolationException()
    {
        FixedCollectionOfTheCaller argument = new();

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    private sealed class FixedCollectionOfTheCaller : ICollection<int>
    {
        public int Count => throw new NotSupportedException();

        public bool IsReadOnly => true;

        public void Add(int item) => throw new NotSupportedException();

        public void Clear() => throw new NotSupportedException();

        public bool Contains(int item) => throw new NotSupportedException();

        public void CopyTo(int[] array, int arrayIndex) => throw new NotSupportedException();

        public bool Remove(int item) => throw new NotSupportedException();

        public IEnumerator<int> GetEnumerator() => throw new NotSupportedException();

        IEnumerator IEnumerable.GetEnumerator() => throw new NotSupportedException();
    }

    private static void AssertViolation(Action call)
    {
        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(call);

        Assert.Equal(ViolationDescription, exception.Description);
    }

    private readonly struct QueueOfTheCaller(int marker) : IImmutableQueue<int>
    {
        public readonly int Marker = marker;

        public bool IsEmpty => throw new NotSupportedException();

        public IImmutableQueue<int> Clear() => throw new NotSupportedException();

        public IImmutableQueue<int> Dequeue() => throw new NotSupportedException();

        public IImmutableQueue<int> Enqueue(int value) => throw new NotSupportedException();

        public int Peek() => throw new NotSupportedException();

        public IEnumerator<int> GetEnumerator() => throw new NotSupportedException();

        IEnumerator IEnumerable.GetEnumerator() => throw new NotSupportedException();
    }

    private sealed class DictionaryOfTheCaller : IImmutableDictionary<int, int>
    {
        public int Count => throw new NotSupportedException();

        public IEnumerable<int> Keys => throw new NotSupportedException();

        public IEnumerable<int> Values => throw new NotSupportedException();

        public int this[int key] => throw new NotSupportedException();

        public IImmutableDictionary<int, int> Add(int key, int value) => throw new NotSupportedException();

        public IImmutableDictionary<int, int> AddRange(IEnumerable<KeyValuePair<int, int>> pairs) =>
            throw new NotSupportedException();

        public IImmutableDictionary<int, int> Clear() => throw new NotSupportedException();

        public bool Contains(KeyValuePair<int, int> pair) => throw new NotSupportedException();

        public bool ContainsKey(int key) => throw new NotSupportedException();

        public IImmutableDictionary<int, int> Remove(int key) => throw new NotSupportedException();

        public IImmutableDictionary<int, int> RemoveRange(IEnumerable<int> keys) => throw new NotSupportedException();

        public IImmutableDictionary<int, int> SetItem(int key, int value) => throw new NotSupportedException();

        public IImmutableDictionary<int, int> SetItems(IEnumerable<KeyValuePair<int, int>> items) =>
            throw new NotSupportedException();

        public bool TryGetKey(int equalKey, out int actualKey) => throw new NotSupportedException();

        public bool TryGetValue(int key, out int value) => throw new NotSupportedException();

        public IEnumerator<KeyValuePair<int, int>> GetEnumerator() => throw new NotSupportedException();

        IEnumerator IEnumerable.GetEnumerator() => throw new NotSupportedException();
    }

    private sealed class SetOfTheCaller : IImmutableSet<int>
    {
        public int Count => throw new NotSupportedException();

        public IImmutableSet<int> Add(int value) => throw new NotSupportedException();

        public IImmutableSet<int> Clear() => throw new NotSupportedException();

        public bool Contains(int value) => throw new NotSupportedException();

        public IImmutableSet<int> Except(IEnumerable<int> other) => throw new NotSupportedException();

        public IImmutableSet<int> Intersect(IEnumerable<int> other) => throw new NotSupportedException();

        public bool IsProperSubsetOf(IEnumerable<int> other) => throw new NotSupportedException();

        public bool IsProperSupersetOf(IEnumerable<int> other) => throw new NotSupportedException();

        public bool IsSubsetOf(IEnumerable<int> other) => throw new NotSupportedException();

        public bool IsSupersetOf(IEnumerable<int> other) => throw new NotSupportedException();

        public bool Overlaps(IEnumerable<int> other) => throw new NotSupportedException();

        public IImmutableSet<int> Remove(int value) => throw new NotSupportedException();

        public bool SetEquals(IEnumerable<int> other) => throw new NotSupportedException();

        public IImmutableSet<int> SymmetricExcept(IEnumerable<int> other) => throw new NotSupportedException();

        public bool TryGetValue(int equalValue, out int actualValue) => throw new NotSupportedException();

        public IImmutableSet<int> Union(IEnumerable<int> other) => throw new NotSupportedException();

        public IEnumerator<int> GetEnumerator() => throw new NotSupportedException();

        IEnumerator IEnumerable.GetEnumerator() => throw new NotSupportedException();
    }

    private sealed class ListOfTheCaller : IImmutableList<int>
    {
        public int Count => throw new NotSupportedException();

        public int this[int index] => throw new NotSupportedException();

        public IImmutableList<int> Add(int value) => throw new NotSupportedException();

        public IImmutableList<int> AddRange(IEnumerable<int> items) => throw new NotSupportedException();

        public IImmutableList<int> Clear() => throw new NotSupportedException();

        public int IndexOf(int item, int index, int count, IEqualityComparer<int>? equalityComparer) =>
            throw new NotSupportedException();

        public IImmutableList<int> Insert(int index, int element) => throw new NotSupportedException();

        public IImmutableList<int> InsertRange(int index, IEnumerable<int> items) => throw new NotSupportedException();

        public int LastIndexOf(int item, int index, int count, IEqualityComparer<int>? equalityComparer) =>
            throw new NotSupportedException();

        public IImmutableList<int> Remove(int value, IEqualityComparer<int>? equalityComparer) =>
            throw new NotSupportedException();

        public IImmutableList<int> RemoveAll(Predicate<int> match) => throw new NotSupportedException();

        public IImmutableList<int> RemoveAt(int index) => throw new NotSupportedException();

        public IImmutableList<int> RemoveRange(IEnumerable<int> items, IEqualityComparer<int>? equalityComparer) =>
            throw new NotSupportedException();

        public IImmutableList<int> RemoveRange(int index, int count) => throw new NotSupportedException();

        public IImmutableList<int> Replace(int oldValue, int newValue, IEqualityComparer<int>? equalityComparer) =>
            throw new NotSupportedException();

        public IImmutableList<int> SetItem(int index, int value) => throw new NotSupportedException();

        public IEnumerator<int> GetEnumerator() => throw new NotSupportedException();

        IEnumerator IEnumerable.GetEnumerator() => throw new NotSupportedException();
    }

    private sealed class StackOfTheCaller : IImmutableStack<int>
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
