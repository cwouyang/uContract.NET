using System.Collections.ObjectModel;
using uContract.Exceptions;

namespace uContract.Tests;

// The runtime is simulated as Native AOT, with chosen types showing no declared member, as Native AOT shows a type
// that nothing preserves. Under the JIT every type shows its members, so dynamic code off alone cannot reach this.
[Collection("EnvironmentVariables")]
public class EnsureAssignableUnseenRuntimeTypeTests
{
    private static string CannotCompare(Type runtimeType, Type declaredType)
    {
        return $"EnsureAssignable cannot compare {runtimeType}, the runtime type of the values (T is {declaredType}): "
            + "under Native AOT it shows no declared member to reflection, as a type that nothing preserves does, so "
            + "its members cannot be listed. Preserve it with "
            + "[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(X))] on a method that runs, where X is "
            + $"{runtimeType}, or pass the values as that type; otherwise set DBC_POST=off (disables all "
            + "postcondition checks).";
    }

    // ---- Top level, runtime type other than T ----

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTheRuntimeTypeShowsNoDeclaredMemberAndNothingChanged_ThrowsCannotCompare()
    {
        object actual = new Ticket { Seat = 1 };
        object expected = new Ticket { Seat = 1 };

        Exception? exception = TestRuntime.WithUnseenTypes(
            [typeof(Ticket)],
            () => Contract.EnsureAssignable(actual, expected)
        );

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(CannotCompare(typeof(Ticket), typeof(object)), cannotCompare.Message);
    }

    // Today the members of T alone would be compared, and the change to Holder would pass silently.
    [Fact]
    public void EnsureAssignable_WhenTIsABaseClassAndTheDerivedTypeShowsNoDeclaredMemberAndItsOwnMemberChanged_ThrowsCannotCompare()
    {
        TicketBase actual = new DerivedTicket { Holder = "b" };
        TicketBase expected = new DerivedTicket { Holder = "a" };

        Exception? exception = TestRuntime.WithUnseenTypes(
            [typeof(DerivedTicket)],
            () => Contract.EnsureAssignable(actual, expected)
        );

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(CannotCompare(typeof(DerivedTicket), typeof(TicketBase)), cannotCompare.Message);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAFrameworkClassAndTheSubclassShowsNoDeclaredMember_ThrowsCannotCompare()
    {
        Random actual = new SeededRandom(1);
        Random expected = new SeededRandom(1);

        Exception? exception = TestRuntime.WithUnseenTypes(
            [typeof(SeededRandom)],
            () => Contract.EnsureAssignable(actual, expected)
        );

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(CannotCompare(typeof(SeededRandom), typeof(Random)), cannotCompare.Message);
    }

    [Fact]
    public void EnsureAssignable_WhenAFrameworkCollectionThatShowsNoDeclaredMemberIsHeldByAnInterface_ThrowsCannotCompare()
    {
        List<int> actual = [1];
        List<int> expected = [1];

        Exception? exception = TestRuntime.WithUnseenTypes(
            [typeof(List<int>)],
            () => Contract.EnsureAssignable<IReadOnlyList<int>>(actual, expected)
        );

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(CannotCompare(typeof(List<int>), typeof(IReadOnlyList<int>)), cannotCompare.Message);
    }

    [Fact]
    public void EnsureAssignable_WhenTheRuntimeTypeIsTAndShowsNoDeclaredMember_ComparesByTheMembersOfT()
    {
        Ticket actual = new() { Seat = 1 };
        Ticket expected = new() { Seat = 1 };

        Exception? exception = TestRuntime.WithUnseenTypes(
            [typeof(Ticket)],
            () => Contract.EnsureAssignable(actual, expected)
        );

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenDynamicCodeIsSupportedAndTheRuntimeTypeIsReportedAsShowingNoDeclaredMember_ComparesItsMembers()
    {
        object actual = new Ticket { Seat = 2 };
        object expected = new Ticket { Seat = 1 };

        Exception? exception = TestRuntime.WithUnseenTypes(
            [typeof(Ticket)],
            () => Contract.EnsureAssignable(actual, expected),
            dynamicCodeSupported: true
        );

        PostconditionViolationException violation = Assert.IsType<PostconditionViolationException>(exception);
        Assert.StartsWith(
            "Fields were modified that are not marked as assignable:\n  - Seat\n",
            violation.Description,
            StringComparison.Ordinal
        );
    }

    // A primitive is decided by its Equals alone, as a whole: no member of it is listed.
    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTheValuesAreEqualBoxedIntegersThatShowNoDeclaredMember_DoesNotThrow()
    {
        object actual = 42;
        object expected = 42;

        Exception? exception = TestRuntime.WithUnseenTypes(
            [typeof(int)],
            () => Contract.EnsureAssignable(actual, expected)
        );

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTwoSequencesOfDifferentRuntimeTypesAndOneShowsNoDeclaredMember_ThrowsCannotCompare()
    {
        TaggedList actual = [1];
        List<int> expected = [1];

        Exception? exception = TestRuntime.WithUnseenTypes(
            [typeof(TaggedList)],
            () => Contract.EnsureAssignable<IEnumerable<int>>(actual, expected)
        );

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(CannotCompare(typeof(TaggedList), typeof(IEnumerable<int>)), cannotCompare.Message);
    }

    // ---- Nested values ----

    [Fact]
    public void EnsureAssignable_WhenANestedClassShowsNoDeclaredMemberAndItsEqualsReportsDifferentInstancesUnequal_ThrowsCannotCompareNested()
    {
        Shelf actual = new() { Part = new Part { Level = 1 } };
        Shelf expected = new() { Part = new Part { Level = 1 } };

        Exception? exception = TestRuntime.WithUnseenTypes(
            [typeof(Part)],
            () => Contract.EnsureAssignable(actual, expected)
        );

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.StartsWith(
            $"EnsureAssignable cannot compare {typeof(Part)} (reached through 'Shelf.Part'): ",
            cannotCompare.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void EnsureAssignable_WhenANestedSequenceOnAFrameworkClassHasAClassOfYoursThatShowsNoDeclaredMember_ThrowsCannotCompareNested()
    {
        Bin actual = new() { Items = new CodedCollection { 1 } };
        Bin expected = new() { Items = new CodedCollection { 1 } };

        Exception? exception = TestRuntime.WithUnseenTypes(
            [typeof(CodedCollection)],
            () => Contract.EnsureAssignable(actual, expected)
        );

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.StartsWith(
            $"EnsureAssignable cannot compare {typeof(CodedCollection)} (reached through 'Bin.Items'): ",
            cannotCompare.Message,
            StringComparison.Ordinal
        );
    }

    // Framework types reached as nested values keep their comparison: List<int> by its elements.
    [Fact]
    public void EnsureAssignable_WhenANestedFrameworkCollectionShowsNoDeclaredMember_ComparesItsElements()
    {
        Rack actual = new() { Numbers = [1, 2] };
        Rack expected = new() { Numbers = [1, 3] };

        Exception? exception = TestRuntime.WithUnseenTypes(
            [typeof(List<int>)],
            () => Contract.EnsureAssignable(actual, expected)
        );

        PostconditionViolationException violation = Assert.IsType<PostconditionViolationException>(exception);
        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Numbers\n  - <Numbers>k__BackingField",
            violation.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenANestedClassShowsNoDeclaredMemberAndItsEqualsReportsTheValuesEqual_DoesNotThrow()
    {
        Badge actual = new() { Part = new KeyedPart(7) };
        Badge expected = new() { Part = new KeyedPart(7) };

        Exception? exception = TestRuntime.WithUnseenTypes(
            [typeof(KeyedPart)],
            () => Contract.EnsureAssignable(actual, expected)
        );

        Assert.Null(exception);
    }

    private sealed class Rack
    {
        public List<int> Numbers { get; set; } = [];
    }

    private sealed class KeyedPart(int key) : IEquatable<KeyedPart>
    {
        public int Key { get; } = key;

        public bool Equals(KeyedPart? other) => other is not null && other.Key == Key;

        public override bool Equals(object? obj) => Equals(obj as KeyedPart);

        public override int GetHashCode() => Key;
    }

    private sealed class Badge
    {
        public KeyedPart? Part { get; set; }
    }

    private class TicketBase
    {
        public int Number { get; set; }
    }

    private sealed class DerivedTicket : TicketBase
    {
        public string Holder { get; set; } = "";
    }

    private sealed class SeededRandom(int seed) : Random(seed);

    private sealed class CodedCollection : Collection<int>
    {
        public int Code { get; set; }
    }

    private sealed class Bin
    {
        public Collection<int> Items { get; set; } = [];
    }

    private sealed class Part
    {
        public int Level { get; set; }
    }

    private sealed class Shelf
    {
        public Part Part { get; set; } = new();
    }

    private sealed class TaggedList : List<int>
    {
        public string Tag { get; set; } = "";
    }

    private sealed class Ticket
    {
        public int Seat { get; set; }
    }
}
