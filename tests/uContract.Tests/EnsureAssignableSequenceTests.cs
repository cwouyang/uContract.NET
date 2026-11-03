using System.Collections;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Xml;
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
    public void EnsureAssignable_WhenTIsObjectAndTwoListsOfEqualContentDifferInCapacity_DoesNotThrow()
    {
        List<int> actualList = new(SpareCapacity);
        actualList.AddRange([1, 2]);
        object actual = actualList;
        object expected = ListOf(1, 2);

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
    public void EnsureAssignable_WhenTIsObjectAndTwoDictionariesDifferInOneValue_ReportsTheValuesAndTheElements()
    {
        object actual = new Dictionary<string, int>(StringComparer.Ordinal) { ["apples"] = 1 };
        object expected = new Dictionary<string, int>(StringComparer.Ordinal) { ["apples"] = 2 };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            Violation + "  - Values\n" + ElementsEntry + ListedFrom(typeof(Dictionary<string, int>), typeof(object)),
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTwoDictionariesDifferInCount_ReportsTheCountTheKeysTheValuesAndTheElements()
    {
        object actual = new Dictionary<string, int>(StringComparer.Ordinal) { ["apples"] = 1 };
        object expected = new Dictionary<string, int>(StringComparer.Ordinal) { ["apples"] = 1, ["pears"] = 2 };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            Violation
                + "  - Count\n  - Keys\n  - Values\n"
                + ElementsEntry
                + ListedFrom(typeof(Dictionary<string, int>), typeof(object)),
            exception.Description
        );
    }

    // ---- The public properties of the framework classes, at the top level ----

    [Fact]
    public void EnsureAssignable_WhenTIsADictionaryAndOnlyItsComparerDiffers_ReportsTheComparer()
    {
        Dictionary<string, int> actual = new(StringComparer.Ordinal) { ["apples"] = 1 };
        Dictionary<string, int> expected = new(StringComparer.OrdinalIgnoreCase) { ["apples"] = 1 };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Comparer", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenAPatternNamesTheComparerAndOnlyTheComparerOfTwoDictionariesDiffers_DoesNotThrow()
    {
        Dictionary<string, int> actual = new(StringComparer.Ordinal) { ["apples"] = 1 };
        Dictionary<string, int> expected = new(StringComparer.OrdinalIgnoreCase) { ["apples"] = 1 };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected, "Comparer"));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAHashSetAndOnlyItsComparerDiffers_ReportsTheComparer()
    {
        HashSet<string> actual = new(StringComparer.Ordinal) { "red" };
        HashSet<string> expected = new(StringComparer.OrdinalIgnoreCase) { "red" };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Comparer", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAGroupingAndOnlyTheKeyDiffers_ReportsTheKey()
    {
        int[] numbers = [1, 2];
        IGrouping<string, int> actual = numbers.GroupBy(_ => "odd", StringComparer.Ordinal).Single();
        IGrouping<string, int> expected = numbers.GroupBy(_ => "even", StringComparer.Ordinal).Single();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            Violation + "  - Key" + ListedFrom(actual.GetType(), typeof(IGrouping<string, int>)),
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnXmlDocumentAndAnAttributeChanges_ReportsItsOuterXml()
    {
        XmlDocument actual = new();
        actual.LoadXml("<order status=\"open\" />");
        XmlDocument expected = new();
        expected.LoadXml("<order status=\"closed\" />");

        // XmlResolver has no getter, nor has the XmlResolver of the XmlSchemaSet that Schemas holds: comparing
        // either ends the call, so both are listed as assignable.
        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected, "^(Schemas|XmlResolver)$")
        );

        Assert.Equal(Violation + "  - InnerXml\n  - OuterXml", exception.Description);
    }

    // An XmlDocument is a sequence of its child nodes but not a collection: under a T other than its own type it
    // is not compared at all.
    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTheValuesAreXmlDocuments_AsksForTheirOwnTypeAsT()
    {
        XmlDocument actualDocument = new();
        actualDocument.LoadXml("<order status=\"open\" />");
        XmlDocument expectedDocument = new();
        expectedDocument.LoadXml("<order status=\"closed\" />");
        object actual = actualDocument;
        object expected = expectedDocument;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(NotACollection(typeof(XmlDocument), typeof(object)), exception.Message);
    }

    // ---- A sequence that is not a collection, under a T other than its own type ----

    [Fact]
    public void EnsureAssignable_WhenTIsAnEnumerableInterfaceAndTheValuesAreSelectIterators_ThrowsWithoutEnumerating()
    {
        int selectorCalls = 0;
        int[] numbers = [1, 2];
        IEnumerable<int> actual = numbers.Select(number => ++selectorCalls + number);
        IEnumerable<int> expected = numbers.Select(number => ++selectorCalls + number);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            (NotACollection(actual.GetType(), typeof(IEnumerable<int>)), 0),
            (exception.Message, selectorCalls)
        );
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTheValuesAreSelectIterators_ThrowsWithoutEnumerating()
    {
        int selectorCalls = 0;
        int[] numbers = [1, 2];
        object actual = numbers.Select(number => ++selectorCalls + number);
        object expected = numbers.Select(number => ++selectorCalls + number);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal((NotACollection(actual.GetType(), typeof(object)), 0), (exception.Message, selectorCalls));
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnEnumerableInterfaceAndASelectIteratorStandsAgainstAList_ThrowsWithoutEnumerating()
    {
        int selectorCalls = 0;
        int[] numbers = [1, 2];
        IEnumerable<int> actual = numbers.Select(number => ++selectorCalls + number);
        IEnumerable<int> expected = ListOf(2, 4);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            (NotACollection(actual.GetType(), typeof(IEnumerable<int>)), 0),
            (exception.Message, selectorCalls)
        );
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnXmlDocument_StillReadsItsXmlResolverAsAPropertyOfT()
    {
        XmlDocument actual = new();
        actual.LoadXml("<order status=\"open\" />");
        XmlDocument expected = new();
        expected.LoadXml("<order status=\"closed\" />");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            Contract.EnsureAssignable(actual, expected, "^Schemas$")
        );

        Assert.Contains($"{typeof(XmlDocument)}.XmlResolver", exception.Message, StringComparison.Ordinal);
    }

    // ---- A sequence that is not a collection, passed as its own type, keeps all its members ----

    [Fact]
    public void EnsureAssignable_WhenTIsAnImmutableQueueAndItsElementDiffers_ReportsItsPrivateField()
    {
        ImmutableQueue<int> actual = ImmutableQueue.Create(1);
        ImmutableQueue<int> expected = ImmutableQueue.Create(2);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - _forwards", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnImmutableQueueAndTheElementsAreEqual_DoesNotThrow()
    {
        ImmutableQueue<int> actual = ImmutableQueue.Create(1);
        ImmutableQueue<int> expected = ImmutableQueue.Create(1);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnImmutableStackAndItsElementDiffers_ReportsItsPrivateField()
    {
        ImmutableStack<int> actual = ImmutableStack.Create(1);
        ImmutableStack<int> expected = ImmutableStack.Create(2);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - _head", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnImmutableStackAndTheElementsAreEqual_DoesNotThrow()
    {
        ImmutableStack<int> actual = ImmutableStack.Create(1);
        ImmutableStack<int> expected = ImmutableStack.Create(1);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnXmlDocumentAndOnlyItsResolverChanges_ReportsItsPrivateField()
    {
        XmlDocument actual = new();
        actual.LoadXml("<order />");
        actual.XmlResolver = new XmlUrlResolver();
        XmlDocument expected = new();
        expected.LoadXml("<order />");

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected, "^(Schemas|XmlResolver)$")
        );

        Assert.Equal(Violation + "  - _resolver\n  - bSetResolver", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAReadOnlyListInterfaceAndTwoListsOfEqualContentDifferInCapacity_DoesNotThrow()
    {
        List<int> actualList = new(SpareCapacity);
        actualList.AddRange([1, 2]);
        IReadOnlyList<int> actual = actualList;
        IReadOnlyList<int> expected = ListOf(1, 2);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
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

    // ---- Two sequences of different runtime types ----

    [Fact]
    public void EnsureAssignable_WhenTIsAnEnumerableInterfaceAndAListAndAnArrayDifferInOneElement_ReportsTheElements()
    {
        IEnumerable<int> actual = ListOf(1, 2);
        IEnumerable<int> expected = new[] { 1, 3 };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            DifferentSequences(typeof(List<int>), typeof(int[]), typeof(IEnumerable<int>)),
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnEnumerableInterfaceAndAListAndAnArrayHoldEqualElements_DoesNotThrow()
    {
        IEnumerable<int> actual = ListOf(1, 2);
        IEnumerable<int> expected = new[] { 1, 2 };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnEnumerableInterfaceAndAListAndAHashSetHoldEqualElements_DoesNotThrow()
    {
        IEnumerable<int> actual = ListOf(1);
        HashSet<int> expected = [1];

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnEnumerableInterfaceAndACodedCollectionAndAListHoldEqualElements_ReportsBothRuntimeTypes()
    {
        IEnumerable<int> actual = CodedCollectionOf("A", 1, 2);
        IEnumerable<int> expected = ListOf(1, 2);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            DifferentRuntimeTypes(typeof(CodedCollection), typeof(List<int>), typeof(IEnumerable<int>)),
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndArraysOfTwoElementTypesDiffer_ReportsTheElements()
    {
        object actual = new[] { 1 };
        object expected = new[] { 2L };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(DifferentSequences(typeof(int[]), typeof(long[]), typeof(object)), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndArraysOfTwoRanksHoldEqualElements_ReportsTheElements()
    {
        object actual = new[] { 1, 2 };
        object expected = new[,]
        {
            { 1, 2 },
        };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(DifferentSequences(typeof(int[]), typeof(int[,]), typeof(object)), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndFrozenSetsOfTwoElementTypesDiffer_ReportsTheElements()
    {
        object actual = FrozenSet.ToFrozenSet([1]);
        object expected = FrozenSet.ToFrozenSet([2L]);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            DifferentSequences(typeof(FrozenSet<int>), typeof(FrozenSet<long>), typeof(object)),
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndAFrozenSetAndAFrozenDictionaryDiffer_ReportsTheElements()
    {
        object actual = FrozenSet.ToFrozenSet([1]);
        object expected = new Dictionary<int, int> { [1] = 1 }.ToFrozenDictionary();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            DifferentSequences(typeof(FrozenSet<int>), typeof(FrozenDictionary<int, int>), typeof(object)),
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnEnumerableInterfaceAndAFrozenSetAndAListDiffer_ReportsTheElements()
    {
        IEnumerable<int> actual = FrozenSet.ToFrozenSet([1]);
        IEnumerable<int> expected = ListOf(2);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            DifferentSequences(typeof(FrozenSet<int>), typeof(List<int>), typeof(IEnumerable<int>)),
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnEnumerableInterfaceAndAFrozenSetAndAListHoldEqualElements_DoesNotThrow()
    {
        IEnumerable<int> actual = FrozenSet.ToFrozenSet([1]);
        IEnumerable<int> expected = ListOf(1);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenANestedCodedCollectionBecomesAListOfEqualElements_ReportsTheTopLevelMember()
    {
        Holder<IEnumerable<int>> actual = new() { Value = CodedCollectionOf("A", 1, 2) };
        Holder<IEnumerable<int>> expected = new() { Value = ListOf(1, 2) };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsACollectionInterfaceAndAListAndAReadOnlyCollectionHoldEqualElements_ReportsIsReadOnly()
    {
        ICollection<int> actual = ListOf(1);
        ICollection<int> expected = new ReadOnlyCollection<int>([1]);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - IsReadOnly", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenAPatternNamesIsReadOnlyAndAListAndAReadOnlyCollectionHoldEqualElements_DoesNotThrow()
    {
        ICollection<int> actual = ListOf(1);
        ICollection<int> expected = new ReadOnlyCollection<int>([1]);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected, "IsReadOnly"));

        Assert.Null(exception);
    }

    // ---- A yield iterator of yours ----

    [Fact]
    public void EnsureAssignable_WhenANestedYieldIteratorWasEnumeratedAndYieldsEqualElements_DoesNotThrow()
    {
        Order order = new([1, 2]);
        Order old = Contract.Old(() => order);
        _ = order.Total();

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(order, old));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTheSourceOfANestedYieldIteratorChanges_ReportsTheIterator()
    {
        List<int> source = [1, 2];
        Order order = new(source);
        Order old = Contract.Old(() => order);
        source[1] = 3;

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(order, old)
        );

        Assert.Equal(Violation + "  - _ids", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnEnumerableInterfaceAndOneOfTwoEqualYieldIteratorsWasEnumerated_DoesNotThrow()
    {
        IEnumerable<int> actual = Order.Generate([1, 2]);
        IEnumerable<int> expected = Order.Generate([1, 2]);
        _ = actual.Sum();

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnEnumerableInterfaceAndTwoYieldIteratorsYieldDifferentElements_ReportsTheElements()
    {
        IEnumerable<int> actual = Order.Generate([1, 2]);
        IEnumerable<int> expected = Order.Generate([1, 3]);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + ElementsEntry, exception.Description);
    }

    // ---- The iterator of a local function that captures a variable ----

    // The compiler nests such an iterator (<<Upto>g__Impl|0>d) in the display class that holds the captured
    // variables (<>c__DisplayClass…); the iterator itself carries no [CompilerGenerated], the display class does
    // (measured on SDK 8.0.204). It is compared by its elements, as any yield iterator is.
    [Fact]
    public void EnsureAssignable_WhenANestedCapturingLocalIteratorAndAListHoldEqualElements_DoesNotThrow()
    {
        Holder<IEnumerable<int>> actual = new() { Value = LocalIterators.Upto(3) };
        Holder<IEnumerable<int>> expected = new() { Value = ListOf(1, 2, 3) };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenANestedCapturingLocalIteratorAndAListDifferInOneElement_ReportsTheMember()
    {
        Holder<IEnumerable<int>> actual = new() { Value = LocalIterators.Upto(3) };
        Holder<IEnumerable<int>> expected = new() { Value = ListOf(1, 2, 4) };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnEnumerableInterfaceAndACapturingLocalIteratorAndAListHoldEqualElements_DoesNotThrow()
    {
        IEnumerable<int> actual = LocalIterators.Upto(3);
        IEnumerable<int> expected = ListOf(1, 2, 3);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnEnumerableInterfaceAndACapturingLocalIteratorAndAListDifferInOneElement_ReportsTheElements()
    {
        IEnumerable<int> actual = LocalIterators.Upto(3);
        IEnumerable<int> expected = ListOf(1, 2, 4);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            DifferentSequences(actual.GetType(), typeof(List<int>), typeof(IEnumerable<int>)),
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndACapturingLocalIteratorAndAListHoldEqualElements_DoesNotThrow()
    {
        object actual = LocalIterators.Upto(3);
        object expected = ListOf(1, 2, 3);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndACapturingLocalIteratorAndAListDifferInOneElement_ReportsTheElements()
    {
        object actual = LocalIterators.Upto(3);
        object expected = ListOf(1, 2, 4);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(DifferentSequences(actual.GetType(), typeof(List<int>), typeof(object)), exception.Description);
    }

    // The captured state is a delegate: two iterators that scale by different lambdas to the same elements are
    // equal, because only their elements are compared.
    [Fact]
    public void EnsureAssignable_WhenTIsAnEnumerableInterfaceAndTwoLocalIteratorsCaptureDifferentDelegatesButYieldEqualElements_DoesNotThrow()
    {
        IEnumerable<int> actual = LocalIterators.Scaled([1, 2], number => number * 2);
        IEnumerable<int> expected = LocalIterators.Scaled([1, 2], number => number + number);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    // ---- A class of yours that only looks generated ----

    // A [CompilerGenerated] class with a name C# can write that is its own enumerator (a source generator's cursor)
    // is a class of yours, not a yield iterator: its state is compared.
    [Fact]
    public void EnsureAssignable_WhenTIsAGeneratedCursorOfYoursAndItsTagChanged_ReportsTheTag()
    {
        TaggedCursor actual = new(1, 2) { Tag = "b" };
        TaggedCursor expected = new(1, 2) { Tag = "a" };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Tag", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTheTagOfAGeneratedCursorOfYoursChanged_ReportsTheTag()
    {
        object actual = new TaggedCursor(1, 2) { Tag = "b" };
        object expected = new TaggedCursor(1, 2) { Tag = "a" };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Tag" + ListedFrom(typeof(TaggedCursor), typeof(object)), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTheTagOfANestedGeneratedCursorOfYoursChanged_ReportsTheTopLevelMember()
    {
        Holder<TaggedCursor> actual = new() { Value = new TaggedCursor(1, 2) { Tag = "b" } };
        Holder<TaggedCursor> expected = new() { Value = new TaggedCursor(1, 2) { Tag = "a" } };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    // A source generator marks its classes [CompilerGenerated]; one that implements only IEnumerable is a class of
    // yours, compared by its members, and its state is not hidden behind its elements.
    [Fact]
    public void EnsureAssignable_WhenTIsAGeneratedEnumerableOfYoursAndItsTagChanged_ReportsTheTag()
    {
        TaggedBag actual = new(1, 2) { Tag = "b" };
        TaggedBag expected = new(1, 2) { Tag = "a" };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Tag", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTheTagOfAGeneratedEnumerableOfYoursChanged_ReportsTheTag()
    {
        object actual = new TaggedBag(1, 2) { Tag = "b" };
        object expected = new TaggedBag(1, 2) { Tag = "a" };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Tag" + ListedFrom(typeof(TaggedBag), typeof(object)), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTheTagOfANestedGeneratedEnumerableOfYoursChanged_ReportsTheTopLevelMember()
    {
        Holder<TaggedBag> actual = new() { Value = new TaggedBag(1, 2) { Tag = "b" } };
        Holder<TaggedBag> expected = new() { Value = new TaggedBag(1, 2) { Tag = "a" } };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    // A C# 11 file class has a metadata name that starts with '<' (<File>F…__FileBag); it is a class of yours,
    // compared by its members and not enumerated.
    [Fact]
    public void EnsureAssignable_WhenTIsAFileClassOfYoursAndItsExtraChanged_ReportsExtraWithoutEnumerating()
    {
        FileBag actual = new() { Extra = 2 };
        FileBag expected = new() { Extra = 1 };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Extra", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTheExtraOfAFileClassOfYoursChanged_ReportsExtraWithoutEnumerating()
    {
        object actual = new FileBag { Extra = 2 };
        object expected = new FileBag { Extra = 1 };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Extra" + ListedFrom(typeof(FileBag), typeof(object)), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTheExtraOfANestedFileClassOfYoursChanged_ReportsTheTopLevelMemberWithoutEnumerating()
    {
        Holder<FileBag> actual = new() { Value = new FileBag { Extra = 2 } };
        Holder<FileBag> expected = new() { Value = new FileBag { Extra = 1 } };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    // ---- A collection expression ----

    // C# 12 builds a collection expression held as IReadOnlyList<T> as <>z__ReadOnlyArray<T>, recognised by its
    // "<>" name: it carries no [CompilerGenerated] (measured on SDK 8.0.204). It is compared by its elements, as a
    // yield iterator is, also against a sequence of another runtime type.
    [Fact]
    public void EnsureAssignable_WhenANestedCollectionExpressionAndAListHoldEqualElements_DoesNotThrow()
    {
        Holder<IReadOnlyList<int>> actual = new() { Value = [1, 2, 3] };
        Holder<IReadOnlyList<int>> expected = new() { Value = ListOf(1, 2, 3) };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenANestedCollectionExpressionAndAListDifferInOneElement_ReportsTheMember()
    {
        Holder<IReadOnlyList<int>> actual = new() { Value = [1, 2, 3] };
        Holder<IReadOnlyList<int>> expected = new() { Value = ListOf(1, 2, 4) };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenANestedCollectionExpressionGainedAnElement_ReportsTheMember()
    {
        IReadOnlyList<int> items = [1, 2, 3];
        Holder<IReadOnlyList<int>> actual = new() { Value = [.. items, 4] };
        Holder<IReadOnlyList<int>> expected = new() { Value = items };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    // On the .NET 8 SDK a single-element collection expression is built as the same class as a longer one.
    [Fact]
    public void EnsureAssignable_WhenANestedSingleElementCollectionExpressionAndAListHoldTheSameElement_DoesNotThrow()
    {
        Holder<IReadOnlyList<int>> actual = new() { Value = [5] };
        Holder<IReadOnlyList<int>> expected = new() { Value = ListOf(5) };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    // The empty collection expression is Array.Empty<T>(), an array.
    [Fact]
    public void EnsureAssignable_WhenANestedEmptyCollectionExpressionAndAnEmptyListAreCompared_DoesNotThrow()
    {
        Holder<IReadOnlyList<int>> actual = new() { Value = [] };
        Holder<IReadOnlyList<int>> expected = new() { Value = ListOf() };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    // A spread of a source with no known length is built as <>z__ReadOnlyList<T> (measured on SDK 8.0.204), also
    // recognised by its "<>" name.
    [Fact]
    public void EnsureAssignable_WhenANestedSpreadOfAYieldIteratorAndAListHoldEqualElements_DoesNotThrow()
    {
        Holder<IReadOnlyList<int>> actual = new() { Value = [.. Order.Generate([1, 2, 3])] };
        Holder<IReadOnlyList<int>> expected = new() { Value = ListOf(1, 2, 3) };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Equal<(string, Exception?)>(("<>z__ReadOnlyList`1", null), (actual.Value.GetType().Name, exception));
    }

    [Fact]
    public void EnsureAssignable_WhenANestedSpreadOfAYieldIteratorAndAListDifferInOneElement_ReportsTheMember()
    {
        Holder<IReadOnlyList<int>> actual = new() { Value = [.. Order.Generate([1, 2, 3])] };
        Holder<IReadOnlyList<int>> expected = new() { Value = ListOf(1, 2, 4) };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnEnumerableInterfaceAndTwoCollectionExpressionsHoldEqualElements_DoesNotThrow()
    {
        IEnumerable<int> actual = [1, 2, 3];
        IEnumerable<int> expected = [1, 2, 3];

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAReadOnlyListInterfaceAndTwoCollectionExpressionsDifferInOneElement_ReportsTheElements()
    {
        IReadOnlyList<int> actual = [1, 2, 3];
        IReadOnlyList<int> expected = [1, 2, 4];

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + ElementsEntry, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndACollectionExpressionAndAListDifferInOneElement_ReportsTheSequences()
    {
        IReadOnlyList<int> items = [1, 2, 3];
        object actual = items;
        object expected = ListOf(1, 2, 4);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(DifferentSequences(items.GetType(), typeof(List<int>), typeof(object)), exception.Description);
    }

    // ---- Cycles ----

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTwoListsHoldThemselvesAndDifferInAnotherElement_ReportsTheElements()
    {
        List<object> actualList = ListHoldingItselfAnd(1);
        List<object> expectedList = ListHoldingItselfAnd(2);
        object actual = actualList;
        object expected = expectedList;

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            Violation + ElementsEntry + ListedFrom(typeof(List<object>), typeof(object)),
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTwoListsHoldThemselvesAndAnEqualElement_DoesNotThrow()
    {
        object actual = ListHoldingItselfAnd(1);
        object expected = ListHoldingItselfAnd(1);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenANestedNodeListWithAParentCycleDiffersInTheOwnFieldOfAChild_ReportsTheTopLevelMember()
    {
        Holder<Node> actual = new() { Value = NodeWithChildNamed("left") };
        Holder<Node> expected = new() { Value = NodeWithChildNamed("right") };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Value", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTwoNestedNodeListsWithAParentCycleAreEqual_DoesNotThrow()
    {
        Holder<Node> actual = new() { Value = NodeWithChildNamed("left") };
        Holder<Node> expected = new() { Value = NodeWithChildNamed("left") };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    private static List<object> ListHoldingItselfAnd(int number)
    {
        List<object> list = [];
        list.Add(list);
        list.Add(number);
        return list;
    }

    private static Node NodeWithChildNamed(string childName)
    {
        Node root = new() { Name = "root" };
        root.Add(new Node { Name = childName, Parent = root });
        return root;
    }

    private static string NotACollection(Type runtimeType, Type declaredType)
    {
        return $"EnsureAssignable cannot compare {runtimeType} as T ({declaredType}): it is a sequence that is not a "
            + "collection (it implements none of ICollection, ICollection<T> and IReadOnlyCollection<T>), and "
            + "enumerating it could run a query, consume its source or block. Pass a materialised collection (for "
            + "example ToList()), or pass the value as its own type as T.";
    }

    private static string DifferentRuntimeTypes(Type actualType, Type expectedType, Type declaredType)
    {
        return $"actual and expected are of different runtime types: actual is {actualType}, expected is "
            + $"{expectedType}, and T is {declaredType}. No member was compared, so assignable patterns do not apply.";
    }

    private static string DifferentSequences(Type actualType, Type expectedType, Type declaredType)
    {
        return $"actual and expected are not equal. They are sequences of different runtime types (actual is "
            + $"{actualType}, expected is {expectedType}, and T is {declaredType}), compared by their elements, so "
            + "assignable patterns do not apply.";
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

    // Holds a value as a member, so that it is compared as a nested value.
    private sealed class Holder<TValue>
    {
        public required TValue Value;
    }

    // A list of yours built on List<string>, with a field of its own.
    private sealed class TagList : List<string>
    {
        public string Owner = "";
    }

    // Holds a yield iterator over a list it was given; Total enumerates it.
    private sealed class Order(List<int> source)
    {
        private readonly IEnumerable<int> _ids = Generate(source);

        public int Total()
        {
            return _ids.Sum();
        }

        public static IEnumerable<int> Generate(List<int> numbers)
        {
            foreach (int number in numbers)
            {
                yield return number;
            }
        }
    }

    // Iterators of local functions that capture a variable of the method that returns them.
    private static class LocalIterators
    {
        public static IEnumerable<int> Upto(int last)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(last);
            return Impl();

            IEnumerable<int> Impl()
            {
                for (int number = 1; number <= last; number++)
                {
                    yield return number;
                }
            }
        }

        public static IEnumerable<int> Scaled(int[] numbers, Func<int, int> scale)
        {
            ArgumentNullException.ThrowIfNull(scale);
            return Impl();

            IEnumerable<int> Impl()
            {
                foreach (int number in numbers)
                {
                    yield return scale(number);
                }
            }
        }
    }

    // A tree node of yours built on List<Node>, whose children point back to it.
    private sealed class Node : List<Node>
    {
        public string Name = "";
        public Node? Parent;
    }

    // A collection of yours built on Collection<int>, with a field of its own.
    private sealed class CodedCollection : Collection<int>
    {
        public string Code = "";
    }

    // A class of yours that implements IEnumerable<int> itself, over a private list. Its enumerator throws, so
    // a comparison that enumerates it fails with that exception.
    private sealed class LineBook(params int[] lines) : IEnumerable<int>
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

    // Marked as a source generator marks its classes, with a name C# can write; it is its own enumerator, over a
    // private array, and has state of its own.
    [System.Runtime.CompilerServices.CompilerGenerated]
    private sealed class TaggedCursor(params int[] items) : IEnumerable<int>, IEnumerator<int>
    {
        private readonly int[] _items = items;
        private int _index = -1;

        public string Tag = "";

        int IEnumerator<int>.Current => _items[_index];

        object IEnumerator.Current => _items[_index];

        public IEnumerator<int> GetEnumerator()
        {
            _index = -1;
            return this;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool MoveNext()
        {
            return ++_index < _items.Length;
        }

        public void Reset()
        {
            _index = -1;
        }

        public void Dispose() { }
    }

    // Marked as a source generator marks its classes; implements only IEnumerable<int>, over a private list, and has
    // state of its own.
    [System.Runtime.CompilerServices.CompilerGenerated]
    private sealed class TaggedBag(params int[] items) : IEnumerable<int>
    {
        private readonly List<int> _items = [.. items];

        public string Tag = "";

        public IEnumerator<int> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}

// A C# 11 file class of yours that implements IEnumerable<int> itself and has state of its own. Its enumerator
// throws, so a comparison that enumerates it fails with that exception.
file sealed class FileBag : IEnumerable<int>
{
    public int Extra;

    public IEnumerator<int> GetEnumerator()
    {
        throw new NotSupportedException("A FileBag is not enumerated by the comparison.");
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
