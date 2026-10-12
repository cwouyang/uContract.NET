using System.Collections.Frozen;
using System.Collections.Immutable;
using uContract.Exceptions;

namespace uContract.Tests;

public class EnsureImmutableCollectionFrozenTests
{
    private const string ViolationDescription = "Ensure resultingCollection is an immutable collection";

    [Fact]
    public void EnsureImmutableCollection_WhenFrozenSetWithOneElement_ReturnsCollection()
    {
        int[] elements = [1];
        FrozenSet<int> argument = elements.ToFrozenSet();

        FrozenSet<int> result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenEmptyFrozenSet_ReturnsCollection()
    {
        FrozenSet<int> argument = FrozenSet<int>.Empty;

        FrozenSet<int> result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenFrozenSetOfThreeStrings_ReturnsCollection()
    {
        string[] elements = ["a", "bb", "ccc"];
        FrozenSet<string> argument = elements.ToFrozenSet();

        FrozenSet<string> result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenFrozenSetIgnoringCase_ReturnsCollection()
    {
        string[] elements = ["a", "bb"];
        FrozenSet<string> argument = elements.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

        FrozenSet<string> result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenFrozenSetOfObjects_ReturnsCollection()
    {
        object[] elements = [new(), new()];
        FrozenSet<object> argument = elements.ToFrozenSet();

        FrozenSet<object> result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenFrozenSetOfHundredIntegers_ReturnsCollection()
    {
        FrozenSet<int> argument = Enumerable.Range(0, 100).ToFrozenSet();

        FrozenSet<int> result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenFrozenDictionaryWithStringKeys_ReturnsCollection()
    {
        Dictionary<string, int> entries = new(StringComparer.Ordinal) { ["a"] = 1 };
        FrozenDictionary<string, int> argument = entries.ToFrozenDictionary();

        FrozenDictionary<string, int> result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenEmptyFrozenDictionary_ReturnsCollection()
    {
        FrozenDictionary<int, int> argument = FrozenDictionary<int, int>.Empty;

        FrozenDictionary<int, int> result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenFrozenDictionaryWithOneEntry_ReturnsCollection()
    {
        Dictionary<int, int> entries = new() { [1] = 1 };
        FrozenDictionary<int, int> argument = entries.ToFrozenDictionary();

        FrozenDictionary<int, int> result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenFrozenSetIsDeclaredAsReadOnlySet_ReturnsCollection()
    {
        int[] elements = [1];
        IReadOnlySet<int> argument = elements.ToFrozenSet();

        IReadOnlySet<int> result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenFrozenSetIsDeclaredAsObject_ReturnsCollection()
    {
        int[] elements = [1];
        object argument = elements.ToFrozenSet();

        object result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public unsafe void EnsureImmutableCollection_WhenFrozenSetOfFunctionPointerArrays_ReturnsCollection()
    {
        delegate* <void>[][] elements =
        [
            [&TargetFunction],
        ];
        FrozenSet<delegate* <void>[]> argument = elements.ToFrozenSet();

        FrozenSet<delegate* <void>[]> result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenFrozenSetOfIntegerArrays_ReturnsCollection()
    {
        int[][] elements =
        [
            [1],
        ];
        FrozenSet<int[]> argument = elements.ToFrozenSet();

        FrozenSet<int[]> result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenFrozenSetEnumerator_ThrowsPostconditionViolationException()
    {
        int[] elements = [1];
        FrozenSet<int>.Enumerator argument = elements.ToFrozenSet().GetEnumerator();

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenFrozenDictionaryEnumerator_ThrowsPostconditionViolationException()
    {
        Dictionary<int, int> entries = new() { [1] = 1 };
        FrozenDictionary<int, int>.Enumerator argument = entries.ToFrozenDictionary().GetEnumerator();

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenFrozenDictionaryKeys_ReturnsCollection()
    {
        Dictionary<int, int> entries = new() { [1] = 1 };
        ImmutableArray<int> argument = entries.ToFrozenDictionary().Keys;

        ImmutableArray<int> result = Contract.EnsureImmutableCollection(argument);

        Assert.True(argument.Equals(result));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenFrozenDictionaryValues_ReturnsCollection()
    {
        Dictionary<int, int> entries = new() { [1] = 1 };
        ImmutableArray<int> argument = entries.ToFrozenDictionary().Values;

        ImmutableArray<int> result = Contract.EnsureImmutableCollection(argument);

        Assert.True(argument.Equals(result));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenFrozenSetItems_ReturnsCollection()
    {
        int[] elements = [1];
        ImmutableArray<int> argument = elements.ToFrozenSet().Items;

        ImmutableArray<int> result = Contract.EnsureImmutableCollection(argument);

        Assert.True(argument.Equals(result));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenClassDeclaredInFrozenNamespace_ThrowsPostconditionViolationException()
    {
        FrozenNamespaceSequence argument = new(1, 2);

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenStructDeclaredInFrozenNamespace_ThrowsPostconditionViolationException()
    {
        FrozenNamespaceStruct argument = new();

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenClassNamedFrozenSetDerivesFromNoFrozenType_ThrowsPostconditionViolationException()
    {
        OtherSets.FrozenSet<int> argument = new();

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    private static void AssertViolation(Action call)
    {
        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(call);

        Assert.Equal(ViolationDescription, exception.Description);
    }

    private static void TargetFunction() { }

    // The holder keeps the simple name FrozenSet of the other tests of this class meaning the type of the framework.
    private static class OtherSets
    {
        public sealed class FrozenSet<T>;
    }
}
