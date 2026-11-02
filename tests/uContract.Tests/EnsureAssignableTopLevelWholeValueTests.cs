using System.Collections.Frozen;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using uContract.Exceptions;

namespace uContract.Tests;

// At the top level the runtime type, not T, decides whether two values are compared as a whole, and whether
// they are compared by reference.
public class EnsureAssignableTopLevelWholeValueTests
{
    private static readonly int[] Numbers = [1, 2, 3];
    private static readonly int[] NumbersWithAnotherLast = [1, 2, 4];
    private static readonly string[] Letters = ["a", "b", "c"];
    private static readonly string[] LettersWithAnotherLast = ["a", "b", "d"];
    private static readonly string[] InvalidPattern = ["("];
    private static readonly string[] OneNullPattern = [null!];
    private static readonly byte[] Content = [1, 2, 3];

    // ---- An array ----

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTwoArraysDifferInOneElement_ReportsTheArrayAsComparedAsAWhole()
    {
        object actual = Numbers.ToArray();
        object expected = NumbersWithAnotherLast.ToArray();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(int[])), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTwoArraysAreEqual_DoesNotThrow()
    {
        object actual = Numbers.ToArray();
        object expected = Numbers.ToArray();

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAListInterfaceAndTwoArraysOfOneLengthDiffer_ReportsTheArrayAsComparedAsAWhole()
    {
        IList<int> actual = Numbers.ToArray();
        IList<int> expected = NumbersWithAnotherLast.ToArray();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(int[])), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnEnumerableInterfaceAndTwoArraysOfOneLengthDiffer_ReportsTheArrayAsComparedAsAWhole()
    {
        IEnumerable<int> actual = Numbers.ToArray();
        IEnumerable<int> expected = NumbersWithAnotherLast.ToArray();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(int[])), exception.Description);
    }

    // The patterns are not examined: an invalid regular expression raises no regex exception.
    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTwoArraysDifferAndAPatternIsInvalid_ReportsTheArrayAsComparedAsAWhole()
    {
        object actual = Numbers.ToArray();
        object expected = NumbersWithAnotherLast.ToArray();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected, InvalidPattern)
        );

        Assert.Equal(NotEqual(typeof(int[])), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTwoArraysDifferAndAPatternIsNull_ReportsTheArrayAsComparedAsAWhole()
    {
        object actual = Numbers.ToArray();
        object expected = NumbersWithAnotherLast.ToArray();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected, OneNullPattern)
        );

        Assert.Equal(NotEqual(typeof(int[])), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectHoldingAnArrayAndThePatternArrayIsNull_ThrowsArgumentNull()
    {
        object actual = Numbers.ToArray();
        object expected = Numbers.ToArray();

        Assert.Throws<ArgumentNullException>(() => Contract.EnsureAssignable(actual, expected, null!));
    }

    // ---- A string ----

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTwoStringsDiffer_ReportsTheStringAsComparedAsAWhole()
    {
        object actual = "renamed";
        object expected = "named";

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(string)), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTwoDifferentInstancesOfOneStringAreEqual_DoesNotThrow()
    {
        object actual = new string('a', 3);
        object expected = new string('a', 3);
        Assert.NotSame(actual, expected);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    // ---- A delegate ----

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTwoDelegatesCallDifferentMethods_ReportsTheDelegateAsComparedAsAWhole()
    {
        object actual = new Func<int>(One);
        object expected = new Func<int>(Two);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(Func<int>)), exception.Description);
    }

    // ---- A frozen collection ----

    // The message names the public type, not the internal runtime type of the set.
    [Fact]
    public void EnsureAssignable_WhenTIsAReadOnlySetAndTwoFrozenSetsDifferInOneElement_ReportsThePublicFrozenType()
    {
        IReadOnlySet<string> actual = Letters.ToFrozenSet(StringComparer.Ordinal);
        IReadOnlySet<string> expected = LettersWithAnotherLast.ToFrozenSet(StringComparer.Ordinal);
        Assert.Equal(expected.GetType(), actual.GetType());
        Assert.NotEqual(typeof(FrozenSet<string>), actual.GetType());

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(FrozenSet<string>)), exception.Description);
    }

    // Two frozen sets of one public type are compared as a whole also when the framework built them as
    // different internal classes.
    [Fact]
    public void EnsureAssignable_WhenTIsAReadOnlySetAndTwoFrozenSetsOfDifferentInternalTypesDiffer_ReportsThePublicFrozenType()
    {
        IReadOnlySet<string> actual = ManyKeys(last: 30).ToFrozenSet(StringComparer.Ordinal);
        IReadOnlySet<string> expected = ManyKeys(last: 31).ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        Assert.NotEqual(expected.GetType(), actual.GetType());

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(FrozenSet<string>)), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTwoFrozenSetsOfDifferentInternalTypesDifferInCount_ReportsThePublicFrozenType()
    {
        object actual = Letters.ToFrozenSet(StringComparer.Ordinal);
        object expected = ManyKeys(last: 30).ToFrozenSet(StringComparer.Ordinal);
        Assert.NotEqual(expected.GetType(), actual.GetType());

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(FrozenSet<string>)), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAReadOnlySetAndTwoFrozenSetsOfDifferentInternalTypesHoldEqualElements_DoesNotThrow()
    {
        IReadOnlySet<string> actual = ManyKeys(last: 30).ToFrozenSet(StringComparer.Ordinal);
        IReadOnlySet<string> expected = ManyKeys(last: 30).ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        Assert.NotEqual(expected.GetType(), actual.GetType());

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAReadOnlyDictionaryAndTwoFrozenDictionariesOfDifferentInternalTypesDiffer_ReportsThePublicFrozenType()
    {
        IReadOnlyDictionary<string, int> actual = ManyKeys(last: 30)
            .ToFrozenDictionary(key => key, key => key.Length, StringComparer.Ordinal);
        IReadOnlyDictionary<string, int> expected = ManyKeys(last: 30)
            .ToFrozenDictionary(key => key, key => key.Length + 1, StringComparer.OrdinalIgnoreCase);
        Assert.NotEqual(expected.GetType(), actual.GetType());

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(FrozenDictionary<string, int>)), exception.Description);
    }

    // ---- A boxed pointer: compared by address, as a member is ----

    [Fact]
    public unsafe void EnsureAssignable_WhenTIsObjectAndTwoPointerBoxesHoldTheSameAddress_DoesNotThrow()
    {
        int target = 1;
        object actual = Pointer.Box(&target, typeof(int*));
        object expected = Pointer.Box(&target, typeof(int*));
        Assert.NotSame(actual, expected);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public unsafe void EnsureAssignable_WhenTIsAPointerAndTwoBoxesHoldTheSameAddress_DoesNotThrow()
    {
        int target = 1;
        Pointer actual = (Pointer)Pointer.Box(&target, typeof(int*));
        Pointer expected = (Pointer)Pointer.Box(&target, typeof(int*));
        Assert.NotSame(actual, expected);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public unsafe void EnsureAssignable_WhenTIsObjectAndTwoPointerBoxesHoldDifferentAddresses_ReportsThePointerAsComparedAsAWhole()
    {
        int first = 1;
        int second = 1;
        object actual = Pointer.Box(&first, typeof(int*));
        object expected = Pointer.Box(&second, typeof(int*));

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(Pointer)), exception.Description);
    }

    [Fact]
    public unsafe void EnsureAssignable_WhenTIsAPointerAndTwoBoxesHoldDifferentAddresses_ReportsThePointerAsComparedAsAWhole()
    {
        int first = 1;
        int second = 1;
        Pointer actual = (Pointer)Pointer.Box(&first, typeof(int*));
        Pointer expected = (Pointer)Pointer.Box(&second, typeof(int*));

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotEqual(typeof(Pointer)), exception.Description);
    }

    // ---- A shared type ----

    [Fact]
    public void EnsureAssignable_WhenTIsAMemoryStreamAndTwoInstancesHoldEqualContent_ReportsComparedByReference()
    {
        using MemoryStream actual = new(Content);
        using MemoryStream expected = new(Content);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        PostconditionViolationException violation = Assert.IsType<PostconditionViolationException>(exception);
        Assert.Equal(ComparedByReference(typeof(MemoryStream)), violation.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTwoEqualRegexesAreDifferentInstances_ReportsComparedByReference()
    {
        object actual = NewRegex();
        object expected = NewRegex();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ComparedByReference(typeof(Regex)), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsARegexAndTwoEqualRegexesAreDifferentInstances_ReportsComparedByReference()
    {
        Regex actual = NewRegex();
        Regex expected = NewRegex();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ComparedByReference(typeof(Regex)), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAComponentSubclassAndTwoInstances_ReportsComparedByReference()
    {
        using Widget actual = new();
        using Widget expected = new();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ComparedByReference(typeof(Widget)), exception.Description);
    }

    // A Regex is shared and fixed: one instance on both sides is not reported by step 4, and equals itself.
    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndOneRegexIsOnBothSides_DoesNotThrow()
    {
        object regex = NewRegex();

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(regex, regex));

        Assert.Null(exception);
    }

    // ---- Different runtime types ----

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndAStringAndANumberAreCompared_NamesBothRuntimeTypesAndT()
    {
        object actual = "1";
        object expected = 1;

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(DifferentRuntimeTypes(typeof(string), typeof(int), typeof(object)), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndAFrozenSetAndARegexAreCompared_NamesThePublicFrozenType()
    {
        object actual = Letters.ToFrozenSet(StringComparer.Ordinal);
        object expected = NewRegex();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            DifferentRuntimeTypes(typeof(FrozenSet<string>), typeof(Regex), typeof(object)),
            exception.Description
        );
    }

    private static Regex NewRegex() => new("a", RegexOptions.None, TimeSpan.FromSeconds(1));

    // "key1" to "key{last}": enough keys for the framework to choose another internal class than for three.
    private static string[] ManyKeys(int last) =>
        [.. Enumerable.Range(1, last).Select(number => "key" + number.ToString(CultureInfo.InvariantCulture))];

    private static int One() => 1;

    private static int Two() => 2;

    private static string NotEqual(Type type) =>
        $"actual and expected are not equal. {type} is compared as a whole, so assignable patterns do not apply.";

    private static string ComparedByReference(Type type) =>
        $"actual and expected are different instances of {type}, which is compared by reference: its state is "
        + "not compared, and assignable patterns do not apply. To check its state, use Contract.Ensure.";

    private static string DifferentRuntimeTypes(Type actual, Type expected, Type declared) =>
        $"actual and expected are of different runtime types: actual is {actual}, expected is {expected}, "
        + $"and T is {declared}. No member was compared, so assignable patterns do not apply.";

    private sealed class Widget : Component;
}

// The runtime is simulated as Native AOT. Below a frozen collection compared as a whole, the path of a value
// that cannot be compared starts with the public frozen type.
[Collection("EnvironmentVariables")]
public class EnsureAssignableTopLevelWholeValueWithoutDynamicCodeTests
{
    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndAFrozenSetHoldsATypeWithNoVisibleMembers_StartsThePathWithThePublicFrozenType()
    {
        object actual = new[] { new Opaque() }.ToFrozenSet();
        object expected = new[] { new Opaque() }.ToFrozenSet();

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.StartsWith(
            $"EnsureAssignable cannot compare {typeof(Opaque)} (reached through 'FrozenSet`1[]'): ",
            cannotCompare.Message,
            StringComparison.Ordinal
        );
    }

    private sealed class Opaque;
}
