using System.Collections;
using System.Collections.ObjectModel;
using uContract.Exceptions;

namespace uContract.Tests;

// A sequence (a class that is an IEnumerable, not a string) is compared by the class chain of its runtime type,
// at the top level and wherever a member, an element, a key or a value holds it: one built on a framework class
// by its elements and by the members declared on its own classes, and a class of yours that implements
// IEnumerable itself by its members only.
public class EnsureAssignableSequenceTests
{
    private const string Violation = "Fields were modified that are not marked as assignable:\n";
    private const string ElementsEntry = "  - (the elements; assignable patterns do not apply to them)";
    private const int SpareCapacity = 16;
    private const int OtherCapacity = 2;

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTwoListsDifferInOneElement_ReportsTheElements()
    {
        object actual = new List<int> { 1, 2 };
        object expected = new List<int> { 1, 3 };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + ElementsEntry + ListedFrom(typeof(List<int>), typeof(object)), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAListAndTwoListsOfEqualContentDifferInCapacity_DoesNotThrow()
    {
        List<int> actual = new(SpareCapacity);
        actual.AddRange([1, 2]);
        List<int> expected = new(OtherCapacity);
        expected.AddRange([1, 2]);
        Assert.NotEqual(actual.Capacity, expected.Capacity);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAListAndTwoListsOfEqualContentDifferInAddAndRemoveHistory_DoesNotThrow()
    {
        List<int> actual = [1, 2];
        List<int> expected = [1, 2, 3];
        expected.Remove(3);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAReadOnlyListInterfaceAndTwoListsDifferInOneElement_ReportsTheElements()
    {
        IReadOnlyList<int> actual = ListOf(1, 2);
        IReadOnlyList<int> expected = ListOf(1, 3);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            Violation + ElementsEntry + ListedFrom(typeof(List<int>), typeof(IReadOnlyList<int>)),
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAReadOnlyCollectionInterfaceAndTwoHashSetsDifferInOneElement_ReportsTheElements()
    {
        IReadOnlyCollection<string> actual = new HashSet<string>(StringComparer.Ordinal) { "red", "green" };
        IReadOnlyCollection<string> expected = new HashSet<string>(StringComparer.Ordinal) { "red", "blue" };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            Violation + ElementsEntry + ListedFrom(typeof(HashSet<string>), typeof(IReadOnlyCollection<string>)),
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTwoDictionariesDifferInOneValue_ReportsTheElements()
    {
        object actual = new Dictionary<string, int>(StringComparer.Ordinal) { ["apples"] = 1 };
        object expected = new Dictionary<string, int>(StringComparer.Ordinal) { ["apples"] = 2 };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            Violation + ElementsEntry + ListedFrom(typeof(Dictionary<string, int>), typeof(object)),
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTwoDictionariesDifferInCount_ReportsTheElements()
    {
        object actual = new Dictionary<string, int>(StringComparer.Ordinal) { ["apples"] = 1 };
        object expected = new Dictionary<string, int>(StringComparer.Ordinal) { ["apples"] = 1, ["pears"] = 2 };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            Violation + ElementsEntry + ListedFrom(typeof(Dictionary<string, int>), typeof(object)),
            exception.Description
        );
    }

    // ---- A collection of yours built on a framework class, with a member of its own ----

    [Fact]
    public void EnsureAssignable_WhenTIsACodedCollectionAndOnlyItsCodeDiffers_ReportsTheCode()
    {
        CodedCollection actual = CodedCollectionOf("A", 1, 2);
        CodedCollection expected = CodedCollectionOf("B", 1, 2);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Code", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndOnlyTheCodeOfTwoCodedCollectionsDiffers_ReportsTheCode()
    {
        object actual = CodedCollectionOf("A", 1, 2);
        object expected = CodedCollectionOf("B", 1, 2);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            Violation + "  - Code" + ListedFrom(typeof(CodedCollection), typeof(object)),
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenTIsACodedCollectionAndOnlyOneElementDiffers_ReportsTheElements()
    {
        CodedCollection actual = CodedCollectionOf("A", 1, 2);
        CodedCollection expected = CodedCollectionOf("A", 1, 3);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + ElementsEntry, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndOnlyOneElementOfTwoCodedCollectionsDiffers_ReportsTheElements()
    {
        object actual = CodedCollectionOf("A", 1, 2);
        object expected = CodedCollectionOf("A", 1, 3);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            Violation + ElementsEntry + ListedFrom(typeof(CodedCollection), typeof(object)),
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenAPatternNamesTheCodeAndOnlyTheCodeDiffers_DoesNotThrow()
    {
        CodedCollection actual = CodedCollectionOf("A", 1, 2);
        CodedCollection expected = CodedCollectionOf("B", 1, 2);

        Exception? exception = Record.Exception(() =>
            Contract.EnsureAssignable(actual, expected, nameof(CodedCollection.Code))
        );

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenAPatternNamesTheCodeAndTheCodeAndOneElementDiffer_ReportsTheElementsOnly()
    {
        CodedCollection actual = CodedCollectionOf("A", 1, 2);
        CodedCollection expected = CodedCollectionOf("B", 1, 3);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected, nameof(CodedCollection.Code))
        );

        Assert.Equal(Violation + ElementsEntry, exception.Description);
    }

    // ---- A class of yours that implements IEnumerable itself ----

    [Fact]
    public void EnsureAssignable_WhenTIsAnEnumerableOfYoursAndItsPrivateListDiffers_ReportsThatFieldWithoutEnumerating()
    {
        LineBook actual = new(1, 2);
        LineBook expected = new(1, 3);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - _lines", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndThePrivateListOfTwoEnumerablesOfYoursDiffers_ReportsThatField()
    {
        object actual = new LineBook(1, 2);
        object expected = new LineBook(1, 3);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - _lines" + ListedFrom(typeof(LineBook), typeof(object)), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenAPatternNamesThePrivateListOfAnEnumerableOfYours_DoesNotThrow()
    {
        LineBook actual = new(1, 2);
        LineBook expected = new(1, 3);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected, "_lines"));

        Assert.Null(exception);
    }

    // ---- Nested sequences ----

    [Fact]
    public void EnsureAssignable_WhenANestedEnumerableOfYoursDiffersInItsPrivateList_ReportsTheTopLevelMember()
    {
        Holder<LineBook> actual = new() { Value = new LineBook(1, 2) };
        Holder<LineBook> expected = new() { Value = new LineBook(1, 3) };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyTheOwnFieldOfANestedListOfYoursDiffers_ReportsTheTopLevelMember()
    {
        Holder<TagList> actual = new() { Value = TagListOf("ann", "red") };
        Holder<TagList> expected = new() { Value = TagListOf("bob", "red") };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyOneElementOfANestedListOfYoursDiffers_ReportsTheTopLevelMember()
    {
        Holder<TagList> actual = new() { Value = TagListOf("ann", "red") };
        Holder<TagList> expected = new() { Value = TagListOf("ann", "blue") };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    private static TagList TagListOf(string owner, params string[] tags)
    {
        TagList list = new() { Owner = owner };
        list.AddRange(tags);
        return list;
    }

    private static CodedCollection CodedCollectionOf(string code, params int[] numbers)
    {
        CodedCollection collection = new() { Code = code };
        foreach (int number in numbers)
        {
            collection.Add(number);
        }

        return collection;
    }

    private static List<int> ListOf(params int[] numbers)
    {
        return [.. numbers];
    }

    private static string ListedFrom(Type runtimeType, Type declaredType)
    {
        return $"\nMembers were listed from the runtime type {runtimeType}, not from T ({declaredType}).";
    }
}

// Holds a value as a member, so that it is compared as a nested value.
internal sealed class Holder<TValue>
{
    public required TValue Value;
}

// A list of yours built on List<string>, with a field of its own.
internal sealed class TagList : List<string>
{
    public string Owner = "";
}

// A collection of yours built on Collection<int>, with a field of its own.
internal sealed class CodedCollection : Collection<int>
{
    public string Code = "";
}

// A class of yours that implements IEnumerable<int> itself, over a private list. Its enumerator throws, so a
// comparison that enumerates it fails with that exception.
internal sealed class LineBook(params int[] lines) : IEnumerable<int>
{
    private readonly List<int> _lines = [.. lines];

    public IEnumerator<int> GetEnumerator()
    {
        throw new NotSupportedException("A LineBook is not enumerated by the comparison.");
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
