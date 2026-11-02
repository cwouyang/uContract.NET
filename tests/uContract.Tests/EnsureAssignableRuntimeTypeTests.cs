using System.Runtime.CompilerServices;
using uContract.Exceptions;

namespace uContract.Tests;

// The top level is compared by the runtime type of the two values, not by T.
public class EnsureAssignableRuntimeTypeTests
{
    private const string Violation = "Fields were modified that are not marked as assignable:\n";

    private const int Changed = 7;

    private static string ListedFrom<TRuntime, TDeclared>()
    {
        return $"\nMembers were listed from the runtime type {typeof(TRuntime)}, not from T ({typeof(TDeclared)}).";
    }

    // ---- A base-class method that checks this ----

    [Fact]
    public void EnsureAssignable_WhenABaseClassMethodChecksThisAndASubclassFieldChanged_ReportsThatField()
    {
        Draft draft = new(retitleOnTouch: true);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(draft.Touch);

        Assert.Equal(Violation + "  - _title" + ListedFrom<Draft, Document>(), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenABaseClassMethodChecksThisAndNothingButAnAssignableFieldChanged_DoesNotThrow()
    {
        Draft draft = new(retitleOnTouch: false);

        Exception? exception = Record.Exception(draft.Touch);

        Assert.Null(exception);
    }

    // ---- T is object ----

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndAPublicAutoPropertyChanged_ReportsThePropertyAndItsBackingField()
    {
        object actual = new Profile { Name = "renamed" };
        object expected = new Profile();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            Violation + "  - Name\n  - <Name>k__BackingField" + ListedFrom<Profile, object>(),
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndAPositionalPropertyOfARecordChanged_ReportsThatProperty()
    {
        object actual = new Coordinate(1, 2);
        object expected = new Coordinate(1, 3);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            Violation + "  - Y\n  - <Y>k__BackingField" + ListedFrom<Coordinate, object>(),
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTwoRecordsAreEqual_DoesNotThrow()
    {
        object actual = new Coordinate(1, 2);
        object expected = new Coordinate(1, 2);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndAChildPointsBackToTheTopLevelObject_ComparesThatMemberAsEqual()
    {
        Parent actual = new();
        actual.Child.Parent = actual;
        Parent expected = new();
        expected.Child.Parent = expected;

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable<object>(actual, expected));

        Assert.Null(exception);
    }

    // ---- T is an interface ----

    [Fact]
    public void EnsureAssignable_WhenTIsAnInterfaceAndAPropertyTheImplementationAddsChanged_ReportsThatProperty()
    {
        ITagged actual = new TaggedNote { Body = "edited" };
        ITagged expected = new TaggedNote();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            Violation + "  - Body\n  - <Body>k__BackingField" + ListedFrom<TaggedNote, ITagged>(),
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnInterfaceAndItsOwnPropertyChanged_ReportsThatProperty()
    {
        ITagged actual = new TaggedNote { Tag = "urgent" };
        ITagged expected = new TaggedNote();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            Violation + "  - Tag\n  - <Tag>k__BackingField" + ListedFrom<TaggedNote, ITagged>(),
            exception.Description
        );
    }

    // ---- T is a base class ----

    [Fact]
    public void EnsureAssignable_WhenTIsAnAbstractBaseAndAnAutoPropertyTheSubclassAddsChanged_ReportsThatProperty()
    {
        Animal actual = new Dog { Name = "Rex" };
        Animal expected = new Dog();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            Violation + "  - Name\n  - <Name>k__BackingField" + ListedFrom<Dog, Animal>(),
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnAbstractBaseAndAPrivateFieldTheSubclassAddsChanged_ReportsThatField()
    {
        Dog dog = new();
        Animal actual = dog;
        Animal expected = new Dog();
        dog.Bark();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - _barks" + ListedFrom<Dog, Animal>(), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnAbstractBaseAndAPrivateFieldOfTheBaseChanged_ReportsThatField()
    {
        Animal actual = new Dog();
        Animal expected = new Dog();
        actual.Feed();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - _meals" + ListedFrom<Dog, Animal>(), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAConcreteBaseAndAnAutoPropertyTheSubclassAddsChanged_ReportsThatProperty()
    {
        Shape actual = new Circle { Radius = 2 };
        Shape expected = new Circle();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            Violation + "  - Radius\n  - <Radius>k__BackingField" + ListedFrom<Circle, Shape>(),
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenASubclassFieldHasTheNameOfAPrivateFieldOfTheBase_QualifiesTheEntry()
    {
        Customer customer = new();
        Entity actual = customer;
        Entity expected = new Customer();
        customer.SetOwnId(Changed);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            Violation + "  - _id (declared on Customer)" + ListedFrom<Customer, Entity>(),
            exception.Description
        );
    }

    // ---- Assignable patterns ----

    [Fact]
    public void EnsureAssignable_WhenAPatternNamesAMemberOfTheBaseClassT_ExcusesIt()
    {
        Shape actual = new Circle { Sides = 1 };
        Shape expected = new Circle();

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected, "^Sides$"));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenAPatternNamesAMemberOnlyTheSubclassHas_ExcusesIt()
    {
        Shape actual = new Circle { Radius = 2 };
        Shape expected = new Circle();

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected, "^Radius$"));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenNoPatternNamesAChangedSubclassMember_ReportsIt()
    {
        Shape actual = new Circle { Sides = 1, Radius = 2 };
        Shape expected = new Circle();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected, "^Sides$")
        );

        Assert.Equal(
            Violation + "  - Radius\n  - <Radius>k__BackingField" + ListedFrom<Circle, Shape>(),
            exception.Description
        );
    }

    // ---- T is the runtime type: unchanged ----

    [Fact]
    public void EnsureAssignable_WhenAStructIsPassedAsItself_ReportsItsPropertiesAndFieldsWithoutTheRuntimeTypeLine()
    {
        TestPoint actual = new() { X = 1, Y = 2 };
        TestPoint expected = new() { X = 1, Y = 3 };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Y\n  - <Y>k__BackingField", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsTheRuntimeClass_ReportsWithoutTheRuntimeTypeLine()
    {
        Circle actual = new() { Radius = 2 };
        Circle expected = new();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Radius\n  - <Radius>k__BackingField", exception.Description);
    }

    // ---- A sequence, a string, a shared type or a delegate keeps the comparison by T ----

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTheValuesAreSequencesOfDifferentRuntimeTypes_ComparesByTAsBefore()
    {
        object actual = new List<int> { 1 };
        object expected = new[] { 2 };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndOneValueIsAString_ComparesByTAsBefore()
    {
        object actual = "renamed";
        object expected = new Vault();

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndOneValueIsOfASharedType_ComparesByTAsBefore()
    {
        using MemoryStream stream = new();
        object actual = stream;
        object expected = new Vault();

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndOneValueIsADelegate_ComparesByTAsBefore()
    {
        Func<int> callback = () => 1;
        object actual = callback;
        object expected = new Vault();

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndOnlyAPrivateFieldChanged_ReportsThatField()
    {
        Vault actual = new();
        Vault expected = new();
        actual.Lock();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable<object>(actual, expected)
        );

        Assert.Equal(Violation + "  - _locks" + ListedFrom<Vault, object>(), exception.Description);
    }

    // ---- A boxed struct, compared as a member of that struct is: Equals, then its fields ----

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndAFieldOfABoxedStructDiffers_ReportsThatField()
    {
        object actual = new TestPoint { X = 1, Y = 2 };
        object expected = new TestPoint { X = 1, Y = 3 };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - <Y>k__BackingField" + ListedFrom<TestPoint, object>(), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTwoBoxedStructsAreEqual_DoesNotThrow()
    {
        object actual = new TestPoint { X = 1, Y = 2 };
        object expected = new TestPoint { X = 1, Y = 2 };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTheEqualsOfABoxedStructReportsEqual_DoesNotThrow()
    {
        object actual = new Reading(20, "warm");
        object expected = new Reading(20, "mild");

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndAPatternNamesAChangedPropertyOfABoxedStruct_ExcusesItsBackingField()
    {
        object actual = new TestPoint { X = 1, Y = 2 };
        object expected = new TestPoint { X = 1, Y = 3 };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected, "^Y$"));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndABoxedStructWithoutFieldsIsUnequalByEquals_ReportsTheTwoAsNotEqual()
    {
        object actual = new Token();
        object expected = new Token();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            $"actual and expected are not equal. They are compared as {typeof(Token)}, which has no fields, "
                + $"and its Equals reports them unequal; T is {typeof(object)}.",
            exception.Description
        );
    }

    // ---- T is an interface and the values are compared by members: the interface's properties too ----

    [Fact]
    public void EnsureAssignable_WhenTIsAnInterfaceAndAStructIgnoresAChangedInterfacePropertyInEquals_ReportsIt()
    {
        IReading actual = new Reading(20, "warm");
        IReading expected = new Reading(20, "mild");

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Label" + ListedFrom<Reading, IReading>(), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnInterfaceAndAFieldOfAStructChanged_ReportsThatField()
    {
        IReading actual = new Reading(20, "warm");
        IReading expected = new Reading(21, "warm");

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - <Value>k__BackingField" + ListedFrom<Reading, IReading>(), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenAnExplicitInterfacePropertyReadingStateOutsideTheFieldsChanged_ReportsIt()
    {
        IPriced actual = new Quote("12.50");
        IPriced expected = new Quote("10.00");

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Price" + ListedFrom<Quote, IPriced>(), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenAPatternNamesAChangedExplicitInterfaceProperty_ExcusesIt()
    {
        IPriced actual = new Quote("12.50");
        IPriced expected = new Quote("10.00");

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected, "^Price$"));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenAnExplicitInterfaceAutoPropertyChanged_ReportsItsBackingFieldAndTheInterfaceProperty()
    {
        ICoded actual = new Ticket();
        ICoded expected = new Ticket();
        actual.Code = "B";

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            Violation
                + $"  - <{typeof(ICoded).FullName!.Replace('+', '.')}.Code>k__BackingField\n  - Code"
                + ListedFrom<Ticket, ICoded>(),
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyAnExplicitInterfacePropertyBesideAPublicOneOfTheSameNameChanged_ReportsIt()
    {
        IPriced actual = new Listing("12.50");
        IPriced expected = new Listing("10.00");

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Price" + ListedFrom<Listing, IPriced>(), exception.Description);
    }

    private interface IReading
    {
        string Label { get; }
    }

    private interface IPriced
    {
        string Price { get; }
    }

    private interface ICoded
    {
        string Code { get; set; }
    }

    // Its price is kept outside its fields.
    private sealed class Quote : IPriced
    {
        private static readonly ConditionalWeakTable<Quote, string> Prices = [];

        public Quote(string price)
        {
            Prices.Add(this, price);
        }

        string IPriced.Price => Prices.TryGetValue(this, out string? price) ? price : "";
    }

    // A public Price, unchanged, and an explicit IPriced.Price kept outside its fields.
    private sealed class Listing : IPriced
    {
        private static readonly ConditionalWeakTable<Listing, string> Prices = [];

        public Listing(string price)
        {
            Prices.Add(this, price);
        }

        public string Price { get; } = "on request";

        string IPriced.Price => Prices.TryGetValue(this, out string? price) ? price : "";
    }

    private sealed class Ticket : ICoded
    {
        string ICoded.Code { get; set; } = "A";
    }

    // Unequal to everything, and without a field to compare.
    private readonly struct Token : IEquatable<Token>
    {
        public bool Equals(Token other) => false;

        public override bool Equals(object? obj) => false;

        public override int GetHashCode() => 0;
    }

    // Equal by its value alone: the label is not part of its identity.
    private readonly struct Reading(int value, string label) : IEquatable<Reading>, IReading
    {
        public int Value { get; } = value;

        public string Label { get; } = label;

        public bool Equals(Reading other) => Value == other.Value;

        public override bool Equals(object? obj) => obj is Reading other && Equals(other);

        public override int GetHashCode() => Value;
    }

    // ---- A boxed primitive, enum or inline array is decided by its Equals alone ----

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTwoBoxedIntegersDiffer_ReportsTheTwoAsNotEqual()
    {
        object actual = 1;
        object expected = 2;

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(DecidedByEquals<int, object>(), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTwoBoxedIntegersAreEqual_DoesNotThrow()
    {
        object actual = 1;
        object expected = 1;

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTwoBoxedEnumValuesDiffer_ReportsTheTwoAsNotEqual()
    {
        object actual = DayOfWeek.Monday;
        object expected = DayOfWeek.Tuesday;

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(DecidedByEquals<DayOfWeek, object>(), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTheFirstElementOfABoxedInlineArrayChanged_ReportsTheTwoAsNotEqual()
    {
        Triple actualBuffer = new();
        actualBuffer[0] = Changed;
        object actual = actualBuffer;
        object expected = new Triple();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(DecidedByEquals<Triple, object>(), exception.Description);
    }

    // As for a member (see EnsureAssignable_WhenInlineArrayFirstElementIsEqual_DoesNotSeeLaterElements):
    // ValueType.Equals sees only element 0 of an inline array, and decides it alone.
    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndOnlyTheSecondElementOfABoxedInlineArrayChanged_DoesNotSeeIt()
    {
        Triple actualBuffer = new();
        actualBuffer[1] = Changed;
        object actual = actualBuffer;
        object expected = new Triple();

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    private static string DecidedByEquals<TRuntime, TDeclared>()
    {
        return $"actual and expected are not equal. They are compared as {typeof(TRuntime)} by its Equals alone, "
            + $"which reports them unequal; T is {typeof(TDeclared)}.";
    }

    [InlineArray(3)]
    private struct Triple
    {
        private int _element;
    }

    // ---- A framework class as T: its private fields are compared as on master ----

    [Fact]
    public void EnsureAssignable_WhenTIsAFrameworkClassAndASubclassOfItHasAnotherSeed_ReportsThePrivateFieldOfT()
    {
        Random actual = new SeededRandom(1);
        Random expected = new SeededRandom(2);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - _impl" + ListedFrom<SeededRandom, Random>(), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAFrameworkClassAndTwoSubclassInstancesHaveTheSameSeed_DoesNotThrow()
    {
        Random actual = new SeededRandom(1);
        Random expected = new SeededRandom(1);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAFrameworkClassAndAPrivateFieldOfAUserClassBetweenChanged_ReportsThatField()
    {
        CountingRandom counting = new(1);
        Random actual = counting;
        Random expected = new CountingRandom(1);
        counting.Tick();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - _ticks" + ListedFrom<CountingRandom, Random>(), exception.Description);
    }

    private sealed class SeededRandom(int seed) : Random(seed);

    private class TickingRandom(int seed) : Random(seed)
    {
        private int _ticks;

        public void Tick()
        {
            _ticks++;
        }
    }

    private sealed class CountingRandom(int seed) : TickingRandom(seed);

    // ---- Two different runtime types ----

    [Fact]
    public void EnsureAssignable_WhenTIsABaseClassAndTheValuesAreTwoDifferentSubclasses_ReportsBothRuntimeTypesAndT()
    {
        Shape actual = new Circle();
        Shape expected = new Square();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(DifferentRuntimeTypes<Circle, Square, Shape>(), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsABaseClassAndExpectedIsABaseInstance_ReportsBothRuntimeTypesAndT()
    {
        Shape actual = new Circle();
        Shape expected = new();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(DifferentRuntimeTypes<Circle, Shape, Shape>(), exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTheValuesAreOfUnrelatedClasses_ReportsBothRuntimeTypesAndT()
    {
        object actual = new Vault();
        object expected = new Circle();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(DifferentRuntimeTypes<Vault, Circle, object>(), exception.Description);
    }

    private static string DifferentRuntimeTypes<TActual, TExpected, TDeclared>()
    {
        return $"actual and expected are of different runtime types: actual is {typeof(TActual)}, "
            + $"expected is {typeof(TExpected)}, and T is {typeof(TDeclared)}. "
            + "No member was compared, so assignable patterns do not apply.";
    }

    private class Shape
    {
        public int Sides { get; set; }
    }

    private sealed class Circle : Shape
    {
        public int Radius { get; set; }
    }

    private sealed class Square : Shape;

    private sealed class Profile
    {
        public string Name { get; set; } = "original";
    }

    private sealed record Coordinate(int X, int Y);

    private sealed class Parent
    {
        public Child Child { get; } = new();
    }

    private sealed class Child
    {
        public Parent? Parent { get; set; }
    }

    private interface ITagged
    {
        string Tag { get; }
    }

    private sealed class TaggedNote : ITagged
    {
        public string Tag { get; set; } = "none";

        public string Body { get; set; } = "";
    }

    private abstract class Animal
    {
        private int _meals;

        public void Feed()
        {
            _meals++;
        }
    }

    private sealed class Dog : Animal
    {
        private int _barks;

        public string Name { get; set; } = "";

        public void Bark()
        {
            _barks++;
        }
    }

    private class Entity
    {
        private readonly int _id = 1;

        public int BaseId => _id;
    }

    private sealed class Customer : Entity
    {
#pragma warning disable CS0414 // Assigned but never read: the field exists to be compared.
        private int _id;
#pragma warning restore CS0414

        public void SetOwnId(int id)
        {
            _id = id;
        }
    }

    private sealed class Vault
    {
        private int _locks;

        public void Lock()
        {
            _locks++;
        }
    }

    private class Document
    {
        private int _touches;

        public void Touch()
        {
            Document old = Contract.Old(() => this);
            _touches++;
            OnTouch();
            Contract.EnsureAssignable(this, old, "^_touches$");
        }

        protected virtual void OnTouch() { }
    }

    private sealed class Draft : Document
    {
        private readonly bool _retitleOnTouch;
#pragma warning disable CS0414 // Assigned but never read: the field exists to be compared.
        private string _title = "draft";
#pragma warning restore CS0414

        public Draft(bool retitleOnTouch)
        {
            _retitleOnTouch = retitleOnTouch;
        }

        protected override void OnTouch()
        {
            if (_retitleOnTouch)
            {
                _title = "retitled";
            }
        }
    }
}

// The runtime is simulated as Native AOT. The "no members visible" rule is decided on the runtime type compared.
[Collection("EnvironmentVariables")]
public class EnsureAssignableRuntimeTypeWithoutDynamicCodeTests
{
    [Fact]
    public void EnsureAssignable_WhenTheRuntimeTypeIsNotTAndHasNoVisibleMembers_NamesTheRuntimeTypeAndT()
    {
        IMarker actual = new Marker();
        IMarker expected = new Marker();

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(
            $"EnsureAssignable cannot compare {typeof(Marker)}, the runtime type of the values (T is {typeof(IMarker)}): "
                + "no properties or fields are visible to reflection under Native AOT. If the type has members, "
                + "preserve them, for example with [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(X))] "
                + "on a method that runs, where X is that type, or pass the values as that type; otherwise set "
                + "DBC_POST=off (disables all postcondition checks).",
            cannotCompare.Message
        );
    }

    private interface IMarker;

    private sealed class Marker : IMarker;
}
