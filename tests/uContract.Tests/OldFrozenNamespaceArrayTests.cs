using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using uContract.Exceptions;

namespace uContract.Tests;

/// <summary>
///     What <c>Old</c> copies, and what <c>EnsureAssignable</c> then compares, for an array whose element type
///     is declared in <c>System.Collections.Frozen</c> and for a value type declared in
///     <c>System.Collections.Frozen</c>. A <c>FrozenSet&lt;T&gt;</c> or a
///     <c>FrozenDictionary&lt;TKey, TValue&gt;</c> itself stays shared, and so does a class of the caller
///     declared in that namespace.
/// </summary>
public class OldFrozenNamespaceArrayTests
{
    private const string ItemsChanged =
        "Fields were modified that are not marked as assignable:\n  - Items\n  - <Items>k__BackingField";

    private const string NumbersChanged = "Fields were modified that are not marked as assignable:\n  - Numbers";

    private const int SpareCapacity = 4;
    private const int CapacityOfOne = 1;
    private const string Key = "a";

    private static readonly int[] Numbers = [1, 2, 3];
    private static readonly int[] OtherNumbers = [1, 2, 4];

    // An array whose element type is declared in System.Collections.Frozen, as a member.

    [Fact]
    public void Old_WhenAMemberIsAnArrayOfFrozenSets_CopiesTheArrayAndSharesTheSet()
    {
        Holder<FrozenSet<int>[]> holder = new() { Items = [Numbers.ToFrozenSet()] };

        Holder<FrozenSet<int>[]> old = Contract.Old(() => holder);

        Assert.NotSame(holder, old);
        Assert.NotSame(holder.Items, old.Items);
        Assert.Same(holder.Items[0], old.Items[0]);
    }

    [Fact]
    public void EnsureAssignable_WhenAnElementOfAnArrayOfFrozenSetsIsReplacedAfterOld_ReportsTheMember()
    {
        Holder<FrozenSet<int>[]> holder = new() { Items = [Numbers.ToFrozenSet()] };
        Holder<FrozenSet<int>[]> old = Contract.Old(() => holder);
        holder.Items[0] = OtherNumbers.ToFrozenSet();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(holder, old)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenAnArrayOfFrozenSetsIsUnchangedAfterOld_DoesNotThrow()
    {
        Holder<FrozenSet<int>[]> holder = new() { Items = [Numbers.ToFrozenSet()] };
        Holder<FrozenSet<int>[]> old = Contract.Old(() => holder);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(holder, old));

        Assert.Null(exception);
    }

    [Fact]
    public void Old_WhenAMemberIsAnArrayOfArraysOfFrozenSets_CopiesBothArraysAndSharesTheSet()
    {
        Holder<FrozenSet<int>[][]> holder = new()
        {
            Items =
            [
                [Numbers.ToFrozenSet()],
            ],
        };

        Holder<FrozenSet<int>[][]> old = Contract.Old(() => holder);

        Assert.NotSame(holder.Items, old.Items);
        Assert.NotSame(holder.Items[0], old.Items[0]);
        Assert.Same(holder.Items[0][0], old.Items[0][0]);
    }

    [Fact]
    public void EnsureAssignable_WhenAnElementOfAnInnerArrayOfFrozenSetsIsReplacedAfterOld_ReportsTheMember()
    {
        Holder<FrozenSet<int>[][]> holder = new()
        {
            Items =
            [
                [Numbers.ToFrozenSet()],
            ],
        };
        Holder<FrozenSet<int>[][]> old = Contract.Old(() => holder);
        holder.Items[0][0] = OtherNumbers.ToFrozenSet();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(holder, old)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenAnElementOfAnArrayOfRankTwoOfFrozenSetsIsReplacedAfterOld_ReportsTheMember()
    {
        Holder<FrozenSet<int>[,]> holder = new() { Items = GridOfOne(Numbers.ToFrozenSet()) };
        Holder<FrozenSet<int>[,]> old = Contract.Old(() => holder);
        holder.Items[0, 0] = OtherNumbers.ToFrozenSet();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(holder, old)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenAnElementOfAnArrayOfAClassInTheFrozenNamespaceIsReplacedAfterOld_ReportsTheMember()
    {
        Holder<FrozenNamespaceMutableValue[]> holder = new() { Items = [new() { Value = 1 }] };
        Holder<FrozenNamespaceMutableValue[]> old = Contract.Old(() => holder);
        holder.Items[0] = new() { Value = 2 };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(holder, old)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenAnArrayOfAClassInTheFrozenNamespaceIsUnchangedAfterOld_DoesNotThrow()
    {
        Holder<FrozenNamespaceMutableValue[]> holder = new() { Items = [new() { Value = 1 }] };
        Holder<FrozenNamespaceMutableValue[]> old = Contract.Old(() => holder);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(holder, old));

        Assert.Null(exception);
    }

    // The element is shared: the copy holds the original object, so a change inside it is not seen.
    [Fact]
    public void EnsureAssignable_WhenAPropertyOfAnElementOfAClassInTheFrozenNamespaceChangesAfterOld_DoesNotThrow()
    {
        Holder<FrozenNamespaceMutableValue[]> holder = new() { Items = [new() { Value = 1 }] };
        Holder<FrozenNamespaceMutableValue[]> old = Contract.Old(() => holder);
        holder.Items[0].Value = 2;

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(holder, old));

        Assert.Null(exception);
    }

    // The collections that keep their elements in an array whose element type is declared in
    // System.Collections.Frozen.

    [Fact]
    public void Old_WhenAnElementOfAListOfFrozenSetsIsReplacedAfterwards_KeepsTheOriginalSet()
    {
        FrozenSet<int> original = Numbers.ToFrozenSet();
        Holder<List<FrozenSet<int>>> holder = HolderOfAListOf(original);

        Holder<List<FrozenSet<int>>> old = Contract.Old(() => holder);
        holder.Items[0] = OtherNumbers.ToFrozenSet();

        Assert.Same(original, old.Items[0]);
    }

    [Fact]
    public void EnsureAssignable_WhenAnElementOfAListOfFrozenSetsIsReplacedAfterOld_ReportsTheMember()
    {
        Holder<List<FrozenSet<int>>> holder = HolderOfAListOf(Numbers.ToFrozenSet());
        Holder<List<FrozenSet<int>>> old = Contract.Old(() => holder);
        holder.Items[0] = OtherNumbers.ToFrozenSet();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(holder, old)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void Old_WhenAListOfFrozenSetsIsClearedAfterwards_KeepsItsElements()
    {
        FrozenSet<int> first = Numbers.ToFrozenSet();
        Holder<List<FrozenSet<int>>> holder = HolderOfAListOf(first, OtherNumbers.ToFrozenSet());

        Holder<List<FrozenSet<int>>> old = Contract.Old(() => holder);
        holder.Items.Clear();

        Assert.Equal(2, old.Items.Count);
        Assert.Same(first, old.Items[0]);
    }

    [Fact]
    public void EnsureAssignable_WhenAnElementOfAListOfFrozenDictionariesIsReplacedAfterOld_ReportsTheMember()
    {
        Holder<List<FrozenDictionary<string, int>>> holder = new()
        {
            Items = new List<FrozenDictionary<string, int>>(SpareCapacity) { DictionaryOfOneEntry(1) },
        };
        Holder<List<FrozenDictionary<string, int>>> old = Contract.Old(() => holder);
        holder.Items[0] = DictionaryOfOneEntry(2);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(holder, old)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void Old_WhenTheTopOfAStackOfFrozenSetsIsReplacedAfterwards_KeepsTheOriginalSet()
    {
        FrozenSet<int> original = Numbers.ToFrozenSet();
        Holder<Stack<FrozenSet<int>>> holder = HolderOfAStackOf(original);

        Holder<Stack<FrozenSet<int>>> old = Contract.Old(() => holder);
        holder.Items.Pop();
        holder.Items.Push(OtherNumbers.ToFrozenSet());

        Assert.Same(original, old.Items.Peek());
    }

    [Fact]
    public void EnsureAssignable_WhenTheTopOfAStackOfFrozenSetsIsReplacedAfterOld_ReportsTheMember()
    {
        Holder<Stack<FrozenSet<int>>> holder = HolderOfAStackOf(Numbers.ToFrozenSet());
        Holder<Stack<FrozenSet<int>>> old = Contract.Old(() => holder);
        holder.Items.Pop();
        holder.Items.Push(OtherNumbers.ToFrozenSet());

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(holder, old)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void Old_WhenTheOnlyElementOfAQueueOfFrozenSetsIsReplacedAfterwards_KeepsTheOriginalSet()
    {
        FrozenSet<int> original = Numbers.ToFrozenSet();
        Holder<Queue<FrozenSet<int>>> holder = HolderOfAFullQueueOf(original);

        Holder<Queue<FrozenSet<int>>> old = Contract.Old(() => holder);
        holder.Items.Dequeue();
        holder.Items.Enqueue(OtherNumbers.ToFrozenSet());

        Assert.Same(original, old.Items.Peek());
    }

    [Fact]
    public void EnsureAssignable_WhenTheOnlyElementOfAQueueOfFrozenSetsIsReplacedAfterOld_ReportsTheMember()
    {
        Holder<Queue<FrozenSet<int>>> holder = HolderOfAFullQueueOf(Numbers.ToFrozenSet());
        Holder<Queue<FrozenSet<int>>> old = Contract.Old(() => holder);
        holder.Items.Dequeue();
        holder.Items.Enqueue(OtherNumbers.ToFrozenSet());

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(holder, old)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void Old_WhenAnElementOfTheArrayOfAnImmutableArrayOfFrozenSetsIsReplacedAfterwards_KeepsTheOriginalSet()
    {
        FrozenSet<int> original = Numbers.ToFrozenSet();
        Holder<ImmutableArray<FrozenSet<int>>> holder = new() { Items = [original] };

        Holder<ImmutableArray<FrozenSet<int>>> old = Contract.Old(() => holder);
        ImmutableCollectionsMarshal.AsArray(holder.Items)![0] = OtherNumbers.ToFrozenSet();

        Assert.Same(original, old.Items[0]);
    }

    [Fact]
    public void EnsureAssignable_WhenAnElementOfTheArrayOfAnImmutableArrayOfFrozenSetsIsReplacedAfterOld_ReportsTheMember()
    {
        Holder<ImmutableArray<FrozenSet<int>>> holder = new() { Items = [Numbers.ToFrozenSet()] };
        Holder<ImmutableArray<FrozenSet<int>>> old = Contract.Old(() => holder);
        ImmutableCollectionsMarshal.AsArray(holder.Items)![0] = OtherNumbers.ToFrozenSet();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(holder, old)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenAnElementOfTheArrayOfAnArraySegmentOfFrozenSetsIsReplacedAfterOld_ReportsTheMember()
    {
        FrozenSet<int>[] array = [Numbers.ToFrozenSet()];
        Holder<ArraySegment<FrozenSet<int>>> holder = new() { Items = new ArraySegment<FrozenSet<int>>(array) };
        Holder<ArraySegment<FrozenSet<int>>> old = Contract.Old(() => holder);
        array[0] = OtherNumbers.ToFrozenSet();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(holder, old)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenAnElementOfACollectionOfFrozenSetsIsReplacedAfterOld_ReportsTheMember()
    {
        Holder<Collection<FrozenSet<int>>> holder = new() { Items = [Numbers.ToFrozenSet()] };
        Holder<Collection<FrozenSet<int>>> old = Contract.Old(() => holder);
        holder.Items[0] = OtherNumbers.ToFrozenSet();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(holder, old)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    // An array of frozen sets behind a member declared as object, and passed to Old on its own.

    [Fact]
    public void Old_WhenAMemberDeclaredAsObjectHoldsAnArrayOfFrozenSets_CopiesTheArray()
    {
        FrozenSet<int>[] array = [Numbers.ToFrozenSet()];
        Holder<object> holder = new() { Items = array };

        Holder<object> old = Contract.Old(() => holder);

        Assert.NotSame(array, old.Items);
    }

    [Fact]
    public void Old_WhenTheValueIsAnArrayOfFrozenSets_CopiesTheArray()
    {
        FrozenSet<int>[] array = [Numbers.ToFrozenSet()];

        FrozenSet<int>[] old = Contract.Old(() => array);

        Assert.NotSame(array, old);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfFrozenSetsAndAnElementIsReplacedAfterOld_ReportsTheTwoAsNotEqual()
    {
        FrozenSet<int>[] array = [Numbers.ToFrozenSet()];
        FrozenSet<int>[] old = Contract.Old(() => array);
        array[0] = OtherNumbers.ToFrozenSet();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(array, old)
        );

        Assert.Equal(NotEqual(typeof(FrozenSet<int>[])), exception.Description);
    }

    // Two arrays whose element type is declared in System.Collections.Frozen are compared element by element.

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoArraysOfFrozenSetsOfEqualContent_DoesNotThrow()
    {
        FrozenSet<int> actualSet = Numbers.ToFrozenSet();
        FrozenSet<int> expectedSet = Numbers.ToFrozenSet();
        Assert.NotSame(actualSet, expectedSet);
        Assert.True(actualSet.SequenceEqual(expectedSet));
        Holder<FrozenSet<int>[]> actual = new() { Items = [actualSet] };
        Holder<FrozenSet<int>[]> expected = new() { Items = [expectedSet] };
        Assert.NotSame(actual.Items, expected.Items);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoArraysOfFrozenSetsOfDifferentContent_ReportsTheMember()
    {
        Holder<FrozenSet<int>[]> actual = new() { Items = [Numbers.ToFrozenSet()] };
        Holder<FrozenSet<int>[]> expected = new() { Items = [OtherNumbers.ToFrozenSet()] };
        Assert.NotSame(actual.Items, expected.Items);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoArraysOfOneFrozenSetOfDifferentLengths_ReportsTheMember()
    {
        FrozenSet<int> set = Numbers.ToFrozenSet();
        Holder<FrozenSet<int>[]> actual = new() { Items = [set] };
        Holder<FrozenSet<int>[]> expected = new() { Items = [set, set] };
        Assert.NotSame(actual.Items, expected.Items);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoArraysOfFrozenSetsWithANullElementEach_DoesNotThrow()
    {
        Holder<FrozenSet<int>?[]> actual = new() { Items = [null] };
        Holder<FrozenSet<int>?[]> expected = new() { Items = [null] };
        Assert.NotSame(actual.Items, expected.Items);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoEmptyArraysOfFrozenSets_DoesNotThrow()
    {
        Holder<FrozenSet<int>[]> actual = new() { Items = ArrayOfSets(length: 0) };
        Holder<FrozenSet<int>[]> expected = new() { Items = ArrayOfSets(length: 0) };
        Assert.NotSame(actual.Items, expected.Items);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoArraysOfArraysOfOneFrozenSet_DoesNotThrow()
    {
        FrozenSet<int> set = Numbers.ToFrozenSet();
        Holder<FrozenSet<int>[][]> actual = new()
        {
            Items =
            [
                [set],
            ],
        };
        Holder<FrozenSet<int>[][]> expected = new()
        {
            Items =
            [
                [set],
            ],
        };
        Assert.NotSame(actual.Items, expected.Items);
        Assert.NotSame(actual.Items[0], expected.Items[0]);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoArraysOfRankTwoOfOneFrozenSet_DoesNotThrow()
    {
        FrozenSet<int> set = Numbers.ToFrozenSet();
        Holder<FrozenSet<int>[,]> actual = new() { Items = GridOfOne(set) };
        Holder<FrozenSet<int>[,]> expected = new() { Items = GridOfOne(set) };
        Assert.NotSame(actual.Items, expected.Items);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoArraysOfFrozenDictionariesOfEqualContent_DoesNotThrow()
    {
        FrozenDictionary<string, int> actualDictionary = DictionaryOfOneEntry(1);
        FrozenDictionary<string, int> expectedDictionary = DictionaryOfOneEntry(1);
        Assert.NotSame(actualDictionary, expectedDictionary);
        Assert.True(actualDictionary.Keys.SequenceEqual(expectedDictionary.Keys, StringComparer.Ordinal));
        Holder<FrozenDictionary<string, int>[]> actual = new() { Items = [actualDictionary] };
        Holder<FrozenDictionary<string, int>[]> expected = new() { Items = [expectedDictionary] };
        Assert.NotSame(actual.Items, expected.Items);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoArraysOfOneObjectOfAClassInTheFrozenNamespace_DoesNotThrow()
    {
        FrozenNamespaceMutableValue element = new() { Value = 1 };
        Holder<FrozenNamespaceMutableValue[]> actual = new() { Items = [element] };
        Holder<FrozenNamespaceMutableValue[]> expected = new() { Items = [element] };
        Assert.NotSame(actual.Items, expected.Items);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    // A class of the caller declared in the namespace is compared by reference, also as an element.
    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoArraysOfEqualObjectsOfAClassInTheFrozenNamespace_ReportsTheMember()
    {
        Holder<FrozenNamespaceMutableValue[]> actual = new() { Items = [new() { Value = 1 }] };
        Holder<FrozenNamespaceMutableValue[]> expected = new() { Items = [new() { Value = 1 }] };
        Assert.NotSame(actual.Items, expected.Items);
        Assert.NotSame(actual.Items[0], expected.Items[0]);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsOneObjectOfAClassInTheFrozenNamespaceOnBothSides_DoesNotThrow()
    {
        FrozenNamespaceMutableValue value = new() { Value = 1 };
        Holder<FrozenNamespaceMutableValue> actual = new() { Items = value };
        Holder<FrozenNamespaceMutableValue> expected = new() { Items = value };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    // An array of frozen sets passed as T is compared as a whole, element by element.

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfFrozenSetsOfEqualContent_DoesNotThrow()
    {
        FrozenSet<int> actualSet = Numbers.ToFrozenSet();
        FrozenSet<int> expectedSet = Numbers.ToFrozenSet();
        Assert.NotSame(actualSet, expectedSet);
        Assert.True(actualSet.SequenceEqual(expectedSet));
        FrozenSet<int>[] actual = [actualSet];
        FrozenSet<int>[] expected = [expectedSet];
        Assert.NotSame(actual, expected);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfFrozenSetsAndTheTwoHoldOneSet_DoesNotThrow()
    {
        FrozenSet<int> set = Numbers.ToFrozenSet();
        FrozenSet<int>[] actual = [set];
        FrozenSet<int>[] expected = [set];
        Assert.NotSame(actual, expected);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfFrozenSetsAndTheTwoHoldOneSetAndAPatternNamesSyncRoot_DoesNotThrow()
    {
        FrozenSet<int> set = Numbers.ToFrozenSet();
        FrozenSet<int>[] actual = [set];
        FrozenSet<int>[] expected = [set];
        Assert.NotSame(actual, expected);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected, "SyncRoot"));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnArrayOfRankTwoOfFrozenSetsAndTheTwoHoldOneSet_DoesNotThrow()
    {
        FrozenSet<int> set = Numbers.ToFrozenSet();
        FrozenSet<int>[,] actual = GridOfOne(set);
        FrozenSet<int>[,] expected = GridOfOne(set);
        Assert.NotSame(actual, expected);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    // The enumerators of a FrozenSet<T> and of a FrozenDictionary<TKey, TValue> are value types declared in
    // System.Collections.Frozen.

    [Fact]
    public void EnsureAssignable_WhenAnEnumeratorOfAFrozenSetIsUnchangedAfterOld_DoesNotThrow()
    {
        Holder<FrozenSet<int>.Enumerator> holder = new() { Items = Numbers.ToFrozenSet().GetEnumerator() };
        Holder<FrozenSet<int>.Enumerator> old = Contract.Old(() => holder);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(holder, old));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenAnEnumeratorOfAFrozenSetIsAdvancedAfterOld_ReportsTheMember()
    {
        Holder<FrozenSet<int>.Enumerator> holder = new() { Items = Numbers.ToFrozenSet().GetEnumerator() };
        Holder<FrozenSet<int>.Enumerator> old = Contract.Old(() => holder);
        FrozenSet<int>.Enumerator enumerator = holder.Items;
        Assert.True(enumerator.MoveNext());
        holder.Items = enumerator;

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(holder, old)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenAnEnumeratorOfAFrozenDictionaryIsUnchangedAfterOld_DoesNotThrow()
    {
        Holder<FrozenDictionary<string, int>.Enumerator> holder = new()
        {
            Items = DictionaryOfOneEntry(1).GetEnumerator(),
        };
        Holder<FrozenDictionary<string, int>.Enumerator> old = Contract.Old(() => holder);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(holder, old));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenAnArrayOfEnumeratorsOfAFrozenSetIsUnchangedAfterOld_DoesNotThrow()
    {
        Holder<FrozenSet<int>.Enumerator[]> holder = new() { Items = [Numbers.ToFrozenSet().GetEnumerator()] };
        Holder<FrozenSet<int>.Enumerator[]> old = Contract.Old(() => holder);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(holder, old));

        Assert.Null(exception);
    }

    // A struct of the caller declared in System.Collections.Frozen.

    [Fact]
    public void EnsureAssignable_WhenAStructInTheFrozenNamespaceIsUnchangedAfterOld_DoesNotThrow()
    {
        Holder<FrozenNamespaceStruct> holder = new() { Items = new() { Value = 1 } };
        Holder<FrozenNamespaceStruct> old = Contract.Old(() => holder);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(holder, old));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoEqualStructsInTheFrozenNamespace_DoesNotThrow()
    {
        Holder<FrozenNamespaceStruct> actual = new() { Items = new() { Value = 1 } };
        Holder<FrozenNamespaceStruct> expected = new() { Items = new() { Value = 1 } };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenAnElementOfAnArrayOfStructsInTheFrozenNamespaceChangesAfterOld_ReportsTheMember()
    {
        Holder<FrozenNamespaceStruct[]> holder = new() { Items = [new() { Value = 1 }] };
        Holder<FrozenNamespaceStruct[]> old = Contract.Old(() => holder);
        holder.Items[0].Value = 2;

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(holder, old)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenAListOfStructsInTheFrozenNamespaceIsUnchangedAfterOld_DoesNotThrow()
    {
        Holder<List<FrozenNamespaceStruct>> holder = new() { Items = [new() { Value = 1 }] };
        Holder<List<FrozenNamespaceStruct>> old = Contract.Old(() => holder);
        Assert.NotSame(holder.Items, old.Items);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(holder, old));

        Assert.Null(exception);
    }

    [Fact]
    public void Old_WhenAMemberDeclaredAsObjectHoldsAStructInTheFrozenNamespace_CopiesTheBox()
    {
        Holder<object> holder = new() { Items = new FrozenNamespaceStruct { Value = 1 } };

        Holder<object> old = Contract.Old(() => holder);

        Assert.NotSame(holder.Items, old.Items);
    }

    [Fact]
    public void EnsureAssignable_WhenABoxedStructInTheFrozenNamespaceIsReplacedByAnEqualOneAfterOld_DoesNotThrow()
    {
        Holder<object> holder = new() { Items = new FrozenNamespaceStruct { Value = 1 } };
        Holder<object> old = Contract.Old(() => holder);
        holder.Items = new FrozenNamespaceStruct { Value = 1 };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(holder, old));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenANullableStructInTheFrozenNamespaceIsUnchangedAfterOld_DoesNotThrow()
    {
        Holder<FrozenNamespaceStruct?> holder = new() { Items = new FrozenNamespaceStruct { Value = 1 } };
        Holder<FrozenNamespaceStruct?> old = Contract.Old(() => holder);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(holder, old));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsANullableStructInTheFrozenNamespaceAndTheTwoAreEqual_DoesNotThrow()
    {
        FrozenNamespaceStruct? actual = new FrozenNamespaceStruct { Value = 1 };
        FrozenNamespaceStruct? expected = new FrozenNamespaceStruct { Value = 1 };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenAnArrayOfNullableStructsInTheFrozenNamespaceIsUnchangedAfterOld_DoesNotThrow()
    {
        Holder<FrozenNamespaceStruct?[]> holder = new() { Items = [new FrozenNamespaceStruct { Value = 1 }, null] };
        Holder<FrozenNamespaceStruct?[]> old = Contract.Old(() => holder);
        Assert.NotSame(holder.Items, old.Items);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(holder, old));

        Assert.Null(exception);
    }

    // A struct of the caller declared in System.Collections.Frozen that holds a list: the list is copied.

    [Fact]
    public void EnsureAssignable_WhenAStructWithAListInTheFrozenNamespaceIsUnchangedAfterOld_DoesNotThrow()
    {
        Holder<FrozenNamespaceStructWithList> holder = new() { Items = new() { Numbers = [1] } };
        Holder<FrozenNamespaceStructWithList> old = Contract.Old(() => holder);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(holder, old));

        Assert.Null(exception);
    }

    [Fact]
    public void Old_WhenTheValueIsAStructWithAListInTheFrozenNamespace_CopiesTheList()
    {
        FrozenNamespaceStructWithList value = new() { Numbers = [1] };

        FrozenNamespaceStructWithList old = Contract.Old(() => value);

        Assert.NotSame(value.Numbers, old.Numbers);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAStructWithAListInTheFrozenNamespaceAndTheListGainsAnElementAfterOld_ReportsTheField()
    {
        FrozenNamespaceStructWithList value = new() { Numbers = [1] };
        FrozenNamespaceStructWithList old = Contract.Old(() => value);
        value.Numbers.Add(2);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(value, old)
        );

        Assert.Equal(NumbersChanged, exception.Description);
    }

    [Fact]
    public void Old_WhenAMemberDeclaredAsObjectHoldsAStructWithAListInTheFrozenNamespace_CopiesTheBoxAndTheList()
    {
        Holder<object> holder = new() { Items = new FrozenNamespaceStructWithList { Numbers = [1] } };

        Holder<object> old = Contract.Old(() => holder);

        Assert.NotSame(holder.Items, old.Items);
        Assert.NotSame(
            ((FrozenNamespaceStructWithList)holder.Items).Numbers,
            ((FrozenNamespaceStructWithList)old.Items).Numbers
        );
    }

    [Fact]
    public void EnsureAssignable_WhenTheListOfABoxedStructInTheFrozenNamespaceGainsAnElementAfterOld_ReportsTheMember()
    {
        Holder<object> holder = new() { Items = new FrozenNamespaceStructWithList { Numbers = [1] } };
        Holder<object> old = Contract.Old(() => holder);
        ((FrozenNamespaceStructWithList)holder.Items).Numbers.Add(2);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(holder, old)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTheListOfABoxedStructInTheFrozenNamespaceInAnObjectArrayGainsAnElementAfterOld_ReportsTheMember()
    {
        Holder<object[]> holder = new() { Items = [new FrozenNamespaceStructWithList { Numbers = [1] }] };
        Holder<object[]> old = Contract.Old(() => holder);
        ((FrozenNamespaceStructWithList)holder.Items[0]).Numbers.Add(2);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(holder, old)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    // An enum declared in System.Collections.Frozen.

    [Fact]
    public void EnsureAssignable_WhenAnEnumInTheFrozenNamespaceIsUnchangedAfterOld_DoesNotThrow()
    {
        Holder<FrozenNamespaceEnum> holder = new() { Items = FrozenNamespaceEnum.First };
        Holder<FrozenNamespaceEnum> old = Contract.Old(() => holder);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(holder, old));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenAnElementOfAnArrayOfEnumsInTheFrozenNamespaceChangesAfterOld_ReportsTheMember()
    {
        Holder<FrozenNamespaceEnum[]> holder = new() { Items = [FrozenNamespaceEnum.First] };
        Holder<FrozenNamespaceEnum[]> old = Contract.Old(() => holder);
        holder.Items[0] = FrozenNamespaceEnum.Second;

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(holder, old)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    private static string NotEqual(Type type) =>
        $"actual and expected are not equal. {type} is compared as a whole, so assignable patterns do not apply.";

    // The list has room for more elements than it holds.
    private static Holder<List<FrozenSet<int>>> HolderOfAListOf(params FrozenSet<int>[] sets)
    {
        List<FrozenSet<int>> list = new(SpareCapacity);
        list.AddRange(sets);
        return new() { Items = list };
    }

    private static Holder<Stack<FrozenSet<int>>> HolderOfAStackOf(FrozenSet<int> set)
    {
        Stack<FrozenSet<int>> stack = new(SpareCapacity);
        stack.Push(set);
        return new() { Items = stack };
    }

    // The queue has room for the one element only.
    private static Holder<Queue<FrozenSet<int>>> HolderOfAFullQueueOf(FrozenSet<int> set)
    {
        Queue<FrozenSet<int>> queue = new(CapacityOfOne);
        queue.Enqueue(set);
        return new() { Items = queue };
    }

    private static FrozenDictionary<string, int> DictionaryOfOneEntry(int value)
    {
        return new Dictionary<string, int>(StringComparer.Ordinal) { [Key] = value }.ToFrozenDictionary(
            StringComparer.Ordinal
        );
    }

    // Returns a new array on every call, also for a length of 0.
    private static FrozenSet<int>[] ArrayOfSets(int length) => new FrozenSet<int>[length];

    private static FrozenSet<int>[,] GridOfOne(FrozenSet<int> set)
    {
        FrozenSet<int>[,] grid = new FrozenSet<int>[1, 1];
        grid[0, 0] = set;
        return grid;
    }

    private sealed class Holder<TItems>
    {
        public required TItems Items { get; set; }
    }
}
