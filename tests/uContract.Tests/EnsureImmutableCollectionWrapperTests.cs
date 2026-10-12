using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using uContract.Exceptions;

namespace uContract.Tests;

public class EnsureImmutableCollectionWrapperTests
{
    private const string ViolationDescription = "Ensure resultingCollection is an immutable collection";

    [Fact]
    public void EnsureImmutableCollection_WhenClassDerivedFromReadOnlySetStandIn_ReturnsCollection()
    {
        DerivedFromReadOnlySet argument = new(new HashSet<int>());

        DerivedFromReadOnlySet result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenClassDerivedFromReadOnlyCollection_ReturnsCollection()
    {
        LinesOnTop argument = new([]);

        LinesOnTop result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenClassDerivedFromReadOnlyDictionary_ReturnsCollection()
    {
        DictionaryOnTop argument = new(new Dictionary<int, int>());

        DictionaryOnTop result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenClassNamedImmutableHoldsPublicList_ThrowsPostconditionViolationException()
    {
        ImmutableLookingBag argument = new();

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenClassDerivedFromReadOnlyCollectionAddsMethodThatChangesTheList_ReturnsCollection()
    {
        SneakyLines argument = new([]);

        SneakyLines result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenGenericClassDerivedFromReadOnlyCollection_ReturnsCollection()
    {
        GenericLines<int> argument = new([]);

        GenericLines<int> result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenClassDerivedFromReadOnlyObservableCollection_ReturnsCollection()
    {
        ObservableLines argument = new([]);

        ObservableLines result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenClassNamedReadOnlyHoldsPublicList_ThrowsPostconditionViolationException()
    {
        ReadOnlyLookingBag argument = new();

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenReadOnlyArrayListOfArrayList_ThrowsPostconditionViolationException()
    {
        ArrayList wrapped = [];
        ArrayList argument = ArrayList.ReadOnly(wrapped);

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenReadOnlyArrayListOfList_ThrowsPostconditionViolationException()
    {
        IList argument = ArrayList.ReadOnly((IList)new List<object>());

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenReadOnlyMemory_ThrowsPostconditionViolationException()
    {
        int[] elements = [1];
        ReadOnlyMemory<int> argument = elements;

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenReadOnlySequence_ThrowsPostconditionViolationException()
    {
        int[] elements = [1];
        ReadOnlySequence<int> argument = new(elements);

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenReadOnlyCollectionBuilder_ThrowsPostconditionViolationException()
    {
        ReadOnlyCollectionBuilder<int> argument = [];

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenReadOnlyCollection_ReturnsCollection()
    {
        List<int> wrapped = [1];
        ReadOnlyCollection<int> argument = new(wrapped);

        ReadOnlyCollection<int> result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenListAsReadOnly_ReturnsCollection()
    {
        List<int> elements = [1];
        IReadOnlyList<int> argument = elements.AsReadOnly();

        IReadOnlyList<int> result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenReadOnlyDictionary_ReturnsCollection()
    {
        ReadOnlyDictionary<int, int> argument = new(new Dictionary<int, int> { [1] = 1 });

        ReadOnlyDictionary<int, int> result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenReadOnlyObservableCollection_ReturnsCollection()
    {
        ReadOnlyObservableCollection<int> argument = new([]);

        ReadOnlyObservableCollection<int> result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenKeysOfConcurrentDictionary_ReturnsCollection()
    {
        ICollection<int> argument = new ConcurrentDictionary<int, int>().Keys;

        ICollection<int> result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenClassNamedReadOnlyDerivedFromReadOnlyCollection_ReturnsCollection()
    {
        ReadOnlyOrderLines argument = new([]);

        ReadOnlyOrderLines result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenReadOnlySetStandIn_ReturnsCollection()
    {
        ReadOnlySet<int> argument = new(new HashSet<int>());

        ReadOnlySet<int> result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenKeysOfReadOnlyDictionary_ThrowsPostconditionViolationException()
    {
        ReadOnlyDictionary<int, int> dictionary = new(new Dictionary<int, int> { [1] = 1 });
        ICollection<int> argument = dictionary.Keys;

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenValuesOfReadOnlyDictionary_ThrowsPostconditionViolationException()
    {
        ReadOnlyDictionary<int, int> dictionary = new(new Dictionary<int, int> { [1] = 1 });
        ICollection<int> argument = dictionary.Values;

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenKeysOfDerivedReadOnlyDictionary_ThrowsPostconditionViolationException()
    {
        DictionaryOnTop dictionary = new(new Dictionary<int, int>());
        ICollection<int> argument = dictionary.Keys;

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public unsafe void EnsureImmutableCollection_WhenListOfFunctionPointerArrays_ThrowsPostconditionViolationException()
    {
        List<delegate* <void>[]> argument =
        [
            [&TargetFunction],
        ];

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenCollectionExpressionReturnedAsReadOnlyList_ThrowsPostconditionViolationException()
    {
        IReadOnlyList<int> argument = ReadOnlyListOfOne();

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenGenericClassNamedReadOnlySetIsNestedInAnotherClass_ThrowsPostconditionViolationException()
    {
        OtherSets.ReadOnlySet<int> argument = new();

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    private static void TargetFunction() { }

    private static IReadOnlyList<int> ReadOnlyListOfOne() => [1];

    private static void AssertViolation(Action call)
    {
        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(call);

        Assert.Equal(ViolationDescription, exception.Description);
    }

    private sealed class ImmutableLookingBag
    {
        public List<int> Items { get; } = [];
    }

    private sealed class ReadOnlyLookingBag
    {
        public List<int> Items { get; } = [];
    }

    private sealed class ReadOnlyOrderLines(IList<int> list) : ReadOnlyCollection<int>(list);

    private sealed class LinesOnTop(IList<int> list) : ReadOnlyCollection<int>(list);

    private sealed class SneakyLines(IList<int> list) : ReadOnlyCollection<int>(list)
    {
        public void Sneak(int value) => Items.Add(value);
    }

    private sealed class GenericLines<T>(IList<T> list) : ReadOnlyCollection<T>(list);

    private sealed class ObservableLines(ObservableCollection<int> collection)
        : ReadOnlyObservableCollection<int>(collection);

    private sealed class DictionaryOnTop(IDictionary<int, int> dictionary) : ReadOnlyDictionary<int, int>(dictionary);

    private sealed class DerivedFromReadOnlySet(ISet<int> set) : ReadOnlySet<int>(set);

    // The holder keeps the simple name ReadOnlySet of the other tests of this class meaning the stand-in.
    private static class OtherSets
    {
        public sealed class ReadOnlySet<T>;
    }
}
