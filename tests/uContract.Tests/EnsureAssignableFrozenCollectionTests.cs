using System.Collections.Frozen;
using System.Globalization;
using uContract.Exceptions;

namespace uContract.Tests;

public class EnsureAssignableFrozenCollectionTests
{
    private const string ItemsChanged =
        "Fields were modified that are not marked as assignable:\n  - Items\n  - <Items>k__BackingField";

    private static readonly string[] Letters = ["a", "b", "c"];
    private static readonly string[] LettersWithAnotherLast = ["a", "b", "d"];
    private static readonly string[] UpperCaseLetters = ["A", "B", "C"];
    private static readonly string?[] LetterAndNull = ["a", null];
    private static readonly string?[] TwoNullableLetters = ["a", "b"];
    private static readonly int[] Numbers = [1, 2];
    private static readonly string[] OneNullPattern = [null!];

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoFrozenSetsWithEqualElements_DoesNotThrow()
    {
        FrozenSet<string> actualItems = Letters.ToFrozenSet(StringComparer.Ordinal);
        FrozenSet<string> expectedItems = Letters.ToFrozenSet(StringComparer.Ordinal);
        Assert.NotSame(actualItems, expectedItems);
        Assert.True(actualItems.SequenceEqual(expectedItems, StringComparer.Ordinal));
        Holder<FrozenSet<string>> actual = new() { Items = actualItems };
        Holder<FrozenSet<string>> expected = new() { Items = expectedItems };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoFrozenDictionariesWithEqualEntries_DoesNotThrow()
    {
        FrozenDictionary<string, int> actualItems = FrozenEntries(("a", 1), ("b", 2), ("c", 3));
        FrozenDictionary<string, int> expectedItems = FrozenEntries(("a", 1), ("b", 2), ("c", 3));
        Assert.NotSame(actualItems, expectedItems);
        Assert.True(actualItems.Keys.SequenceEqual(expectedItems.Keys, StringComparer.Ordinal));
        Holder<FrozenDictionary<string, int>> actual = new() { Items = actualItems };
        Holder<FrozenDictionary<string, int>> expected = new() { Items = expectedItems };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoFrozenSetsWithOneDifferentElement_ReportsTheMember()
    {
        FrozenSet<string> actualItems = Letters.ToFrozenSet(StringComparer.Ordinal);
        FrozenSet<string> expectedItems = LettersWithAnotherLast.ToFrozenSet(StringComparer.Ordinal);
        Assert.NotSame(actualItems, expectedItems);
        Assert.Equal(expectedItems.Count, actualItems.Count);
        Assert.Single(
            actualItems.Zip(expectedItems),
            pair => !string.Equals(pair.First, pair.Second, StringComparison.Ordinal)
        );
        Holder<FrozenSet<string>> actual = new() { Items = actualItems };
        Holder<FrozenSet<string>> expected = new() { Items = expectedItems };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoFrozenDictionariesWithOneDifferentValue_ReportsTheMember()
    {
        FrozenDictionary<string, int> actualItems = FrozenEntries(("a", 1), ("b", 2), ("c", 3));
        FrozenDictionary<string, int> expectedItems = FrozenEntries(("a", 1), ("b", 2), ("c", 4));
        Assert.NotSame(actualItems, expectedItems);
        Assert.True(actualItems.Keys.SequenceEqual(expectedItems.Keys, StringComparer.Ordinal));
        Holder<FrozenDictionary<string, int>> actual = new() { Items = actualItems };
        Holder<FrozenDictionary<string, int>> expected = new() { Items = expectedItems };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoFrozenDictionariesWithOneDifferentKey_ReportsTheMember()
    {
        FrozenDictionary<string, int> actualItems = FrozenEntries(("a", 1), ("b", 2), ("c", 3));
        FrozenDictionary<string, int> expectedItems = FrozenEntries(("a", 1), ("b", 2), ("d", 3));
        Assert.NotSame(actualItems, expectedItems);
        Assert.Equal(expectedItems.Count, actualItems.Count);
        Assert.Single(
            actualItems.Keys.Zip(expectedItems.Keys),
            pair => !string.Equals(pair.First, pair.Second, StringComparison.Ordinal)
        );
        Assert.True(actualItems.Values.SequenceEqual(expectedItems.Values));
        Holder<FrozenDictionary<string, int>> actual = new() { Items = actualItems };
        Holder<FrozenDictionary<string, int>> expected = new() { Items = expectedItems };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoFrozenSetsWithDifferentCounts_ReportsTheMember()
    {
        FrozenSet<string> actualItems = Letters.ToFrozenSet(StringComparer.Ordinal);
        FrozenSet<string> expectedItems = Letters.Take(2).ToFrozenSet(StringComparer.Ordinal);
        Assert.NotSame(actualItems, expectedItems);
        Holder<FrozenSet<string>> actual = new() { Items = actualItems };
        Holder<FrozenSet<string>> expected = new() { Items = expectedItems };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoFrozenSetsThatEnumerateEqualElementsInAnotherOrder_ReportsTheMember()
    {
        FrozenSet<string> actualItems = Letters.ToFrozenSet(StringComparer.Ordinal);
        FrozenSet<string> expectedItems = Letters.Reverse().ToFrozenSet(StringComparer.Ordinal);
        Assert.NotSame(actualItems, expectedItems);
        Assert.True(actualItems.SetEquals(expectedItems));
        Assert.False(actualItems.SequenceEqual(expectedItems, StringComparer.Ordinal));
        Holder<FrozenSet<string>> actual = new() { Items = actualItems };
        Holder<FrozenSet<string>> expected = new() { Items = expectedItems };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoFrozenDictionariesThatEnumerateEqualEntriesInAnotherOrder_ReportsTheMember()
    {
        FrozenDictionary<string, int> actualItems = FrozenEntries(("a", 1), ("b", 2), ("c", 3));
        FrozenDictionary<string, int> expectedItems = FrozenEntries(("c", 3), ("b", 2), ("a", 1));
        Assert.NotSame(actualItems, expectedItems);
        Assert.False(actualItems.Keys.SequenceEqual(expectedItems.Keys, StringComparer.Ordinal));
        Holder<FrozenDictionary<string, int>> actual = new() { Items = actualItems };
        Holder<FrozenDictionary<string, int>> expected = new() { Items = expectedItems };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoFrozenSetsOfOneTypeWithDifferentComparers_DoesNotThrow()
    {
        FrozenSet<string> actualItems = Letters.ToFrozenSet(StringComparer.Ordinal);
        FrozenSet<string> expectedItems = Letters.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        Assert.NotSame(actualItems, expectedItems);
        Assert.Equal(expectedItems.GetType(), actualItems.GetType());
        Assert.True(actualItems.SequenceEqual(expectedItems, StringComparer.Ordinal));
        Holder<FrozenSet<string>> actual = new() { Items = actualItems };
        Holder<FrozenSet<string>> expected = new() { Items = expectedItems };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoFrozenSetsOfTwoTypesWithEqualElements_DoesNotThrow()
    {
        string[] keys =
        [
            .. Enumerable.Range(1, 30).Select(number => "key" + number.ToString(CultureInfo.InvariantCulture)),
        ];
        FrozenSet<string> actualItems = keys.ToFrozenSet(StringComparer.Ordinal);
        FrozenSet<string> expectedItems = keys.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        Assert.NotSame(actualItems, expectedItems);
        Assert.NotEqual(expectedItems.GetType(), actualItems.GetType());
        Assert.True(actualItems.SequenceEqual(expectedItems, StringComparer.Ordinal));
        Holder<FrozenSet<string>> actual = new() { Items = actualItems };
        Holder<FrozenSet<string>> expected = new() { Items = expectedItems };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoFrozenSetsEqualOnlyByTheirComparer_ReportsTheMember()
    {
        FrozenSet<string> actualItems = Letters.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        FrozenSet<string> expectedItems = UpperCaseLetters.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        Assert.NotSame(actualItems, expectedItems);
        Assert.Equal(expectedItems.GetType(), actualItems.GetType());
        Assert.True(actualItems.SetEquals(expectedItems));
        Holder<FrozenSet<string>> actual = new() { Items = actualItems };
        Holder<FrozenSet<string>> expected = new() { Items = expectedItems };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoFrozenSetsOfDistinctObjectsWithEqualContent_DoesNotThrow()
    {
        FrozenSet<Box> actualItems = new[] { new Box { Value = 1 } }.ToFrozenSet();
        FrozenSet<Box> expectedItems = new[] { new Box { Value = 1 } }.ToFrozenSet();
        Assert.NotSame(actualItems, expectedItems);
        Holder<FrozenSet<Box>> actual = new() { Items = actualItems };
        Holder<FrozenSet<Box>> expected = new() { Items = expectedItems };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoFrozenSetsOfDistinctObjectsWithDifferentContent_ReportsTheMember()
    {
        FrozenSet<Box> actualItems = new[] { new Box { Value = 1 } }.ToFrozenSet();
        FrozenSet<Box> expectedItems = new[] { new Box { Value = 2 } }.ToFrozenSet();
        Assert.NotSame(actualItems, expectedItems);
        Holder<FrozenSet<Box>> actual = new() { Items = actualItems };
        Holder<FrozenSet<Box>> expected = new() { Items = expectedItems };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoFrozenDictionariesOfDistinctObjectsWithEqualContent_DoesNotThrow()
    {
        FrozenDictionary<string, Box> actualItems = Letters.ToFrozenDictionary(
            letter => letter,
            _ => new Box { Value = 1 },
            StringComparer.Ordinal
        );
        FrozenDictionary<string, Box> expectedItems = Letters.ToFrozenDictionary(
            letter => letter,
            _ => new Box { Value = 1 },
            StringComparer.Ordinal
        );
        Assert.NotSame(actualItems, expectedItems);
        Assert.True(actualItems.Keys.SequenceEqual(expectedItems.Keys, StringComparer.Ordinal));
        Holder<FrozenDictionary<string, Box>> actual = new() { Items = actualItems };
        Holder<FrozenDictionary<string, Box>> expected = new() { Items = expectedItems };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsOneFrozenSetOnBothSides_DoesNotThrow()
    {
        FrozenSet<string> items = Letters.ToFrozenSet(StringComparer.Ordinal);
        Holder<FrozenSet<string>> actual = new() { Items = items };
        Holder<FrozenSet<string>> expected = new() { Items = items };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberDeclaredAsInterfaceHoldsTwoFrozenSetsWithEqualElements_DoesNotThrow()
    {
        FrozenSet<string> actualItems = Letters.ToFrozenSet(StringComparer.Ordinal);
        FrozenSet<string> expectedItems = Letters.ToFrozenSet(StringComparer.Ordinal);
        Assert.NotSame(actualItems, expectedItems);
        Assert.True(actualItems.SequenceEqual(expectedItems, StringComparer.Ordinal));
        Holder<IReadOnlySet<string>> actual = new() { Items = actualItems };
        Holder<IReadOnlySet<string>> expected = new() { Items = expectedItems };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberDeclaredAsObjectHoldsTwoFrozenSetsWithEqualElements_DoesNotThrow()
    {
        FrozenSet<string> actualItems = Letters.ToFrozenSet(StringComparer.Ordinal);
        FrozenSet<string> expectedItems = Letters.ToFrozenSet(StringComparer.Ordinal);
        Assert.NotSame(actualItems, expectedItems);
        Assert.True(actualItems.SequenceEqual(expectedItems, StringComparer.Ordinal));
        Holder<object> actual = new() { Items = actualItems };
        Holder<object> expected = new() { Items = expectedItems };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenListElementsAreTwoFrozenSetsWithEqualElements_DoesNotThrow()
    {
        FrozenSet<string> actualItems = Letters.ToFrozenSet(StringComparer.Ordinal);
        FrozenSet<string> expectedItems = Letters.ToFrozenSet(StringComparer.Ordinal);
        Assert.NotSame(actualItems, expectedItems);
        Assert.True(actualItems.SequenceEqual(expectedItems, StringComparer.Ordinal));
        Holder<List<FrozenSet<string>>> actual = new() { Items = [actualItems] };
        Holder<List<FrozenSet<string>>> expected = new() { Items = [expectedItems] };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenDictionaryValuesAreTwoFrozenSetsWithEqualElements_DoesNotThrow()
    {
        FrozenSet<string> actualItems = Letters.ToFrozenSet(StringComparer.Ordinal);
        FrozenSet<string> expectedItems = Letters.ToFrozenSet(StringComparer.Ordinal);
        Assert.NotSame(actualItems, expectedItems);
        Assert.True(actualItems.SequenceEqual(expectedItems, StringComparer.Ordinal));
        Holder<Dictionary<string, FrozenSet<string>>> actual = new()
        {
            Items = new Dictionary<string, FrozenSet<string>>(StringComparer.Ordinal) { ["letters"] = actualItems },
        };
        Holder<Dictionary<string, FrozenSet<string>>> expected = new()
        {
            Items = new Dictionary<string, FrozenSet<string>>(StringComparer.Ordinal) { ["letters"] = expectedItems },
        };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoArraysOfOneFrozenSet_ReportsTheMember()
    {
        FrozenSet<int> numbers = Numbers.ToFrozenSet();
        FrozenSet<int>[] actualItems = [numbers];
        FrozenSet<int>[] expectedItems = [numbers];
        Assert.NotSame(actualItems, expectedItems);
        Holder<FrozenSet<int>[]> actual = new() { Items = actualItems };
        Holder<FrozenSet<int>[]> expected = new() { Items = expectedItems };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoEqualSequencesOfAnotherTypeInTheFrozenNamespace_ReportsTheMember()
    {
        FrozenNamespaceSequence actualItems = new(1, 2);
        FrozenNamespaceSequence expectedItems = new(1, 2);
        Assert.NotSame(actualItems, expectedItems);
        Holder<FrozenNamespaceSequence> actual = new() { Items = actualItems };
        Holder<FrozenNamespaceSequence> expected = new() { Items = expectedItems };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoEqualComponentSequencesInTheFrozenNamespace_ReportsTheMember()
    {
        using FrozenNamespaceComponentSequence actualItems = new(1, 2);
        using FrozenNamespaceComponentSequence expectedItems = new(1, 2);
        Assert.NotSame(actualItems, expectedItems);
        Holder<FrozenNamespaceComponentSequence> actual = new() { Items = actualItems };
        Holder<FrozenNamespaceComponentSequence> expected = new() { Items = expectedItems };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoEqualObjectsOfAnotherTypeInTheFrozenNamespace_ReportsTheMember()
    {
        FrozenNamespaceValue actualItems = new() { Value = 1 };
        FrozenNamespaceValue expectedItems = new() { Value = 1 };
        Assert.NotSame(actualItems, expectedItems);
        Holder<FrozenNamespaceValue> actual = new() { Items = actualItems };
        Holder<FrozenNamespaceValue> expected = new() { Items = expectedItems };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenFrozenSetIsReplacedByOneWithEqualElementsAfterOld_DoesNotThrow()
    {
        Holder<FrozenSet<string>> actual = new() { Items = Letters.ToFrozenSet(StringComparer.Ordinal) };
        Holder<FrozenSet<string>>? old = Contract.Old(() => actual);
        Assert.NotNull(old);
        actual.Items = Letters.ToFrozenSet(StringComparer.Ordinal);
        Assert.NotSame(actual.Items, old.Items);
        Assert.True(actual.Items.SequenceEqual(old.Items, StringComparer.Ordinal));

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, old));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenAnElementOfTheFrozenSetThatOldSharesIsChanged_DoesNotThrow()
    {
        Box box = new() { Value = 1 };
        Holder<FrozenSet<Box>> actual = new() { Items = new[] { box }.ToFrozenSet() };
        Holder<FrozenSet<Box>>? old = Contract.Old(() => actual);
        Assert.NotNull(old);
        Assert.Same(old.Items, actual.Items);
        box.Value = 2;

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, old));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenAnElementIsChangedAndItsFrozenSetReplacedAfterOld_DoesNotThrow()
    {
        Box box = new() { Value = 1 };
        Holder<FrozenSet<Box>> actual = new() { Items = new[] { box }.ToFrozenSet() };
        Holder<FrozenSet<Box>>? old = Contract.Old(() => actual);
        Assert.NotNull(old);
        box.Value = 2;
        actual.Items = new[] { box }.ToFrozenSet();
        Assert.NotSame(actual.Items, old.Items);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, old));

        Assert.Null(exception);
    }

    // Old shares the FrozenSet<T>, so the elements of the old value are the original objects as they are now,
    // not as they were when Old ran. The new element is compared with the changed original, so this change is
    // not seen.
    [Fact]
    public void EnsureAssignable_WhenAnElementIsChangedAndItsFrozenSetReplacedByOneWithANewEqualElement_DoesNotThrow()
    {
        Box box = new() { Value = 1 };
        Holder<FrozenSet<Box>> actual = new() { Items = new[] { box }.ToFrozenSet() };
        Holder<FrozenSet<Box>>? old = Contract.Old(() => actual);
        Assert.NotNull(old);
        Assert.Same(old.Items, actual.Items);
        box.Value = 2;
        actual.Items = new[] { new Box { Value = 2 } }.ToFrozenSet();
        Assert.NotSame(old.Items, actual.Items);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, old));

        Assert.Null(exception);
    }

    // Old shares the FrozenSet<T>, so the elements of the old value are the original objects as they are now,
    // not as they were when Old ran. The new element is compared with the changed original, so a violation is
    // reported although the content is as it was when Old ran.
    [Fact]
    public void EnsureAssignable_WhenAnElementIsChangedAndItsFrozenSetReplacedByOneWithANewElementAsItWas_ReportsTheMember()
    {
        Box box = new() { Value = 1 };
        Holder<FrozenSet<Box>> actual = new() { Items = new[] { box }.ToFrozenSet() };
        Holder<FrozenSet<Box>>? old = Contract.Old(() => actual);
        Assert.NotNull(old);
        Assert.Same(old.Items, actual.Items);
        box.Value = 2;
        actual.Items = new[] { new Box { Value = 1 } }.ToFrozenSet();
        Assert.NotSame(old.Items, actual.Items);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, old)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenFrozenSetIsReplacedByOneWithADifferentElementAfterOld_ReportsTheMember()
    {
        Holder<FrozenSet<Box>> actual = new() { Items = new[] { new Box { Value = 1 } }.ToFrozenSet() };
        Holder<FrozenSet<Box>>? old = Contract.Old(() => actual);
        Assert.NotNull(old);
        actual.Items = new[] { new Box { Value = 2 } }.ToFrozenSet();
        Assert.NotSame(actual.Items, old.Items);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, old)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenElementsOfTwoFrozenSetsReferToTheirOwnSetAndAreEqual_DoesNotThrow()
    {
        FrozenSet<Node> actualItems = SetOfNodeThatRefersToIt(value: 1);
        FrozenSet<Node> expectedItems = SetOfNodeThatRefersToIt(value: 1);
        Assert.NotSame(actualItems, expectedItems);
        Holder<FrozenSet<Node>> actual = new() { Items = actualItems };
        Holder<FrozenSet<Node>> expected = new() { Items = expectedItems };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenElementsOfTwoFrozenSetsReferToTheirOwnSetAndDiffer_ReportsTheMember()
    {
        FrozenSet<Node> actualItems = SetOfNodeThatRefersToIt(value: 1);
        FrozenSet<Node> expectedItems = SetOfNodeThatRefersToIt(value: 2);
        Assert.NotSame(actualItems, expectedItems);
        Holder<FrozenSet<Node>> actual = new() { Items = actualItems };
        Holder<FrozenSet<Node>> expected = new() { Items = expectedItems };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoFrozenSetsWithAnEqualNullElement_DoesNotThrow()
    {
        FrozenSet<string?> actualItems = LetterAndNull.ToFrozenSet(StringComparer.Ordinal);
        FrozenSet<string?> expectedItems = LetterAndNull.ToFrozenSet(StringComparer.Ordinal);
        Assert.NotSame(actualItems, expectedItems);
        Assert.Equal(expectedItems.GetType(), actualItems.GetType());
        Assert.True(actualItems.SequenceEqual(expectedItems, StringComparer.Ordinal));
        Holder<FrozenSet<string?>> actual = new() { Items = actualItems };
        Holder<FrozenSet<string?>> expected = new() { Items = expectedItems };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyOneOfTwoFrozenSetsHasANullElement_ReportsTheMember()
    {
        FrozenSet<string?> actualItems = LetterAndNull.ToFrozenSet(StringComparer.Ordinal);
        FrozenSet<string?> expectedItems = TwoNullableLetters.ToFrozenSet(StringComparer.Ordinal);
        Assert.NotSame(actualItems, expectedItems);
        Assert.NotEqual(expectedItems.GetType(), actualItems.GetType());
        Holder<FrozenSet<string?>> actual = new() { Items = actualItems };
        Holder<FrozenSet<string?>> expected = new() { Items = expectedItems };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsFrozenSetAndAPatternNamesAMemberOfIt_StillReportsADifferentElement()
    {
        FrozenSet<string> actual = Letters.ToFrozenSet(StringComparer.Ordinal);
        FrozenSet<string> expected = LettersWithAnotherLast.ToFrozenSet(StringComparer.Ordinal);
        Assert.NotSame(actual, expected);
        Assert.Equal(expected.Count, actual.Count);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected, "Items")
        );

        Assert.Equal(NotEqual(typeof(FrozenSet<string>)), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsFrozenSetAndOneElementDiffers_ReportsTheTwoAsNotEqual()
    {
        FrozenSet<string> actual = Letters.ToFrozenSet(StringComparer.Ordinal);
        FrozenSet<string> expected = LettersWithAnotherLast.ToFrozenSet(StringComparer.Ordinal);
        Assert.NotSame(actual, expected);
        Assert.Equal(expected.Count, actual.Count);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(FrozenSet<string>)), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsFrozenSetAndTheCountsDiffer_ReportsTheTwoAsNotEqual()
    {
        FrozenSet<string> actual = Letters.ToFrozenSet(StringComparer.Ordinal);
        FrozenSet<string> expected = Letters.Take(2).ToFrozenSet(StringComparer.Ordinal);
        Assert.NotSame(actual, expected);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(FrozenSet<string>)), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsFrozenSetAndTheTwoEnumerateEqualElementsInAnotherOrder_ReportsTheTwoAsNotEqual()
    {
        FrozenSet<string> actual = Letters.ToFrozenSet(StringComparer.Ordinal);
        FrozenSet<string> expected = Letters.Reverse().ToFrozenSet(StringComparer.Ordinal);
        Assert.NotSame(actual, expected);
        Assert.True(actual.SetEquals(expected));
        Assert.False(actual.SequenceEqual(expected, StringComparer.Ordinal));

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(FrozenSet<string>)), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsFrozenSetAndBothSidesAreOneInstance_DoesNotThrow()
    {
        FrozenSet<string> items = Letters.ToFrozenSet(StringComparer.Ordinal);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(items, items));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsFrozenSetAndAPatternIsNotARegularExpression_DoesNotExamineThePattern()
    {
        FrozenSet<string> actual = Letters.ToFrozenSet(StringComparer.Ordinal);
        FrozenSet<string> expected = Letters.ToFrozenSet(StringComparer.Ordinal);
        Assert.NotSame(actual, expected);
        Assert.True(actual.SequenceEqual(expected, StringComparer.Ordinal));

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected, "["));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsFrozenSetAndAPatternIsNull_DoesNotExamineThePattern()
    {
        FrozenSet<string> actual = Letters.ToFrozenSet(StringComparer.Ordinal);
        FrozenSet<string> expected = Letters.ToFrozenSet(StringComparer.Ordinal);
        Assert.NotSame(actual, expected);
        Assert.True(actual.SequenceEqual(expected, StringComparer.Ordinal));

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected, OneNullPattern));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsFrozenDictionaryAndTheEntriesAreEqual_DoesNotThrow()
    {
        FrozenDictionary<string, int> actual = FrozenEntries(("a", 1), ("b", 2), ("c", 3));
        FrozenDictionary<string, int> expected = FrozenEntries(("a", 1), ("b", 2), ("c", 3));
        Assert.NotSame(actual, expected);
        Assert.True(actual.Keys.SequenceEqual(expected.Keys, StringComparer.Ordinal));

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsFrozenDictionaryAndOneValueDiffers_ReportsTheTwoAsNotEqual()
    {
        FrozenDictionary<string, int> actual = FrozenEntries(("a", 1), ("b", 2), ("c", 3));
        FrozenDictionary<string, int> expected = FrozenEntries(("a", 1), ("b", 2), ("c", 4));
        Assert.NotSame(actual, expected);
        Assert.True(actual.Keys.SequenceEqual(expected.Keys, StringComparer.Ordinal));

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(FrozenDictionary<string, int>)), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsFrozenDictionaryAndTheTwoEnumerateEqualEntriesInAnotherOrder_ReportsTheTwoAsNotEqual()
    {
        FrozenDictionary<string, int> actual = FrozenEntries(("a", 1), ("b", 1), ("c", 1));
        FrozenDictionary<string, int> expected = FrozenEntries(("c", 1), ("b", 1), ("a", 1));
        Assert.NotSame(actual, expected);
        Assert.False(actual.Keys.SequenceEqual(expected.Keys, StringComparer.Ordinal));

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(FrozenDictionary<string, int>)), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsFrozenSetAndItsElementsReferToTheirOwnSetAndAreEqual_DoesNotThrow()
    {
        FrozenSet<Node> actual = SetOfNodeThatRefersToIt(value: 1);
        FrozenSet<Node> expected = SetOfNodeThatRefersToIt(value: 1);
        Assert.NotSame(actual, expected);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsFrozenSetAndItsElementsReferToTheirOwnSetAndDiffer_ReportsTheTwoAsNotEqual()
    {
        FrozenSet<Node> actual = SetOfNodeThatRefersToIt(value: 1);
        FrozenSet<Node> expected = SetOfNodeThatRefersToIt(value: 2);
        Assert.NotSame(actual, expected);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(FrozenSet<Node>)), exception.Description);
    }

    // An interface as T compares only the properties it declares itself, and IReadOnlySet<T> declares no property itself.
    [Fact]
    public void EnsureAssignable_WhenTIsAnInterfaceAndTwoFrozenSetsDifferInOneElement_DoesNotThrow()
    {
        FrozenSet<string> actual = Letters.ToFrozenSet(StringComparer.Ordinal);
        FrozenSet<string> expected = LettersWithAnotherLast.ToFrozenSet(StringComparer.Ordinal);
        Assert.NotSame(actual, expected);
        Assert.Equal(expected.Count, actual.Count);

        Exception? exception = Record.Exception(() =>
            Contract.EnsureAssignable<IReadOnlySet<string>>(actual, expected)
        );

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnInterfaceAndTwoFrozenSetsDifferInCount_DoesNotThrow()
    {
        FrozenSet<string> actual = Letters.ToFrozenSet(StringComparer.Ordinal);
        FrozenSet<string> expected = Letters.Take(2).ToFrozenSet(StringComparer.Ordinal);
        Assert.NotSame(actual, expected);
        Assert.NotEqual(expected.Count, actual.Count);

        Exception? exception = Record.Exception(() =>
            Contract.EnsureAssignable<IReadOnlySet<string>>(actual, expected)
        );

        Assert.Null(exception);
    }

    // The declared type decides, and object has no members.
    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTwoFrozenSetsDifferInOneElement_DoesNotThrow()
    {
        FrozenSet<string> actual = Letters.ToFrozenSet(StringComparer.Ordinal);
        FrozenSet<string> expected = LettersWithAnotherLast.ToFrozenSet(StringComparer.Ordinal);
        Assert.NotSame(actual, expected);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable<object>(actual, expected));

        Assert.Null(exception);
    }

    private static string NotEqual(Type type) =>
        $"actual and expected are not equal. {type} is compared as a whole, so assignable patterns do not apply.";

    // Adds the entries in the order given.
    private static FrozenDictionary<string, int> FrozenEntries(params (string Key, int Value)[] entries)
    {
        return entries.ToFrozenDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
    }

    // The set holds one node, and that node refers back to the set.
    private static FrozenSet<Node> SetOfNodeThatRefersToIt(int value)
    {
        Node node = new() { Value = value };
        FrozenSet<Node> set = new[] { node }.ToFrozenSet();
        node.Owner = set;
        return set;
    }

    private sealed class Holder<TItems>
    {
        public required TItems Items { get; set; }
    }

    private sealed class Box
    {
        public int Value { get; set; }
    }

    private sealed class Node
    {
        public int Value { get; set; }

        public FrozenSet<Node>? Owner { get; set; }
    }
}
