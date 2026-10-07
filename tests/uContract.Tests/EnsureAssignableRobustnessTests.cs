using System.Collections.Immutable;
using uContract.Exceptions;

namespace uContract.Tests;

public class EnsureAssignableRobustnessTests
{
    [Fact]
    public void EnsureAssignable_WhenInterfaceMemberHoldsEqualDistinctInstances_DoesNotThrow()
    {
        PetOwner actual = new() { Pet = new Puppy { Tricks = 3 } };
        PetOwner expected = new() { Pet = new Puppy { Tricks = 3 } };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenClassMemberHoldsDifferentRuntimeTypes_ReportsTheMember()
    {
        Shelter actual = new() { Resident = new Puppy { Tricks = 1 } };
        Shelter expected = new() { Resident = new Cat { Lives = 9 } };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Resident\n  - <Resident>k__BackingField",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenSequenceMemberHoldsDifferentCollectionTypesWithEqualElements_DoesNotThrow()
    {
        List<int> list = [1, 2];
        int[] array = [1, 2];
        Scores actual = new() { Values = list };
        Scores expected = new() { Values = array };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenObjectMemberHoldsStringAndCharArrayWithSameCharacters_ReportsTheMember()
    {
        char[] characters = ['a', 'b'];
        Box actual = new() { Content = "ab" };
        Box expected = new() { Content = characters };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Content\n  - <Content>k__BackingField",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenSequenceMemberHoldsImmutableArrayAndArrayWithEqualElements_DoesNotThrow()
    {
        ImmutableArray<int> immutable = [1, 2];
        int[] array = [1, 2];
        Scores actual = new() { Values = immutable };
        Scores expected = new() { Values = array };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenObjectMemberHoldsClassInstanceAndBoxedInt_ReportsTheMember()
    {
        Box actual = new() { Content = new Elem(1) };
        Box expected = new() { Content = 1 };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Content\n  - <Content>k__BackingField",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenObjectMemberHoldsStringAndBoxedInt_ReportsTheMember()
    {
        Box actual = new() { Content = "1" };
        Box expected = new() { Content = 1 };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Content\n  - <Content>k__BackingField",
            exception.Description
        );
    }

    private interface IAnimal;

    private sealed class Elem(int value)
    {
        public int Value { get; } = value;
    }

    private sealed class Box
    {
        public object? Content { get; set; }
    }

    private sealed class Scores
    {
        public IEnumerable<int> Values { get; set; } = [];
    }

    private abstract class Animal;

    private sealed class Cat : Animal
    {
        public int Lives { get; set; }
    }

    private sealed class Shelter
    {
        public Animal? Resident { get; set; }
    }

    private sealed class Puppy : Animal, IAnimal
    {
        public int Tricks { get; set; }
    }

    private sealed class PetOwner
    {
        public IAnimal? Pet { get; set; }
    }
}
