using System.Collections;
using System.Collections.Frozen;
using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Reflection;
using uContract.Exceptions;

namespace uContract.Tests;

public class EnsureAssignableBaseClassFieldTests
{
    private const string Violation = "Fields were modified that are not marked as assignable:\n";

    private const int Changed = 7;

    private const string BackingFieldOfP = "<P>k__BackingField";

    // ---- Private fields of a base class ----

    [Fact]
    public void EnsureAssignable_WhenAGenericAggregateRaisesAnEvent_ReportsTheDomainEventsOfItsBaseClass()
    {
        Order actual = new();
        Order expected = new();
        actual.Place();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - _domainEvents", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyAPrivateFieldOfTheBaseClassChanges_ReportsThatField()
    {
        Derived actual = new();
        Derived expected = new();
        actual.SetCount(Changed);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - _count", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenAPrivateListOfTheBaseClassGainsAnItem_ReportsThatField()
    {
        ListHolder actual = new();
        ListHolder expected = new();
        actual.AddItem(Changed);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - _items", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenAPrivateListOfTheBaseClassIsUnchanged_DoesNotThrow()
    {
        ListHolder actual = new();
        ListHolder expected = new();
        actual.AddItem(Changed);
        expected.AddItem(Changed);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenAPrivateFieldTwoLevelsUpChanges_ReportsThatField()
    {
        Top actual = new();
        Top expected = new();
        actual.SetCount(Changed);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - _count", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenASingleGenericBaseRaisesAnEvent_ReportsTheDomainEvents()
    {
        Invoice actual = new();
        Invoice expected = new();
        actual.Issue();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - _domainEvents", exception.Description);
    }

    [Fact]
    public void Members_WhenTheBaseClassHasAStaticPrivateField_DoNotHoldIt()
    {
        string[] names = MemberNames<DerivedFromStaticHolder>();

        Assert.DoesNotContain("_instances", names, StringComparer.Ordinal);
    }

    [Fact]
    public void Members_WhenTheBaseClassHasAProtectedAndAPrivateProtectedField_HoldEachOnce()
    {
        string[] names = MemberNames<DerivedFromProtectedFields>();

        Assert.Single(names, name => string.Equals(name, "_protected", StringComparison.Ordinal));
        Assert.Single(names, name => string.Equals(name, "_privateProtected", StringComparison.Ordinal));
    }

    // ---- Backing fields of base-class auto-properties ----

    [Fact]
    public void EnsureAssignable_WhenAPublicAutoPropertyOfAGenericBaseClassChanges_ReportsOnlyTheProperty()
    {
        Order actual = new();
        Order expected = new();
        actual.IncrementVersion();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Version", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenAPublicAutoPropertyOfAGenericBaseClassIsAssignable_DoesNotThrow()
    {
        Order actual = new();
        Order expected = new();
        actual.IncrementVersion();

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected, "^Version$"));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenAPublicAutoPropertyOfTheBaseClassChanges_ReportsOnlyTheProperty()
    {
        NamedDerived actual = new();
        NamedDerived expected = new();
        actual.Name = "renamed";

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Name", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenAPublicAutoPropertyOfTheBaseClassIsAssignable_DoesNotThrow()
    {
        NamedDerived actual = new();
        NamedDerived expected = new();
        actual.Name = "renamed";

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected, "^Name$"));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenAPrivateAutoPropertyOfTheBaseClassChanges_ReportsItsBackingField()
    {
        DerivedFromNonPublicProperties actual = new();
        DerivedFromNonPublicProperties expected = new();
        actual.SetPrivateNote("changed");

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - <PrivateNote>k__BackingField", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenAProtectedAutoPropertyOfTheBaseClassChanges_ReportsItsBackingField()
    {
        DerivedFromNonPublicProperties actual = new();
        DerivedFromNonPublicProperties expected = new();
        actual.SetProtectedNote("changed");

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - <ProtectedNote>k__BackingField", exception.Description);
    }

    [Fact]
    public void Members_WhenAnAutoPropertyOverridesABaseAutoProperty_HoldNoBackingFieldOfTheBase()
    {
        string[] names = MemberNames<D10>();

        Assert.Single(names, name => string.Equals(name, BackingFieldOfP, StringComparison.Ordinal));
    }

    [Fact]
    public void Members_WhenAnAutoPropertyOverridesThroughTwoBaseLevels_HoldNoBackingFieldOfEitherBase()
    {
        string[] names = MemberNames<D>();

        Assert.Single(names, name => string.Equals(name, BackingFieldOfP, StringComparison.Ordinal));
    }

    [Fact]
    public void EnsureAssignable_WhenAnOverridingAutoPropertyChanges_ReportsThePropertyAndItsOwnBackingField()
    {
        D10 actual = new();
        D10 expected = new();
        actual.P = "changed";

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - P\n  - " + BackingFieldOfP, exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenAnOverridingAutoPropertyIsAssignable_DoesNotThrow()
    {
        D10 actual = new();
        D10 expected = new();
        actual.P = "changed";

        Exception? exception = Record.Exception(() =>
            Contract.EnsureAssignable(actual, expected, "^(P|<P>k__BackingField)$")
        );

        Assert.Null(exception);
    }

    [Fact]
    public void Members_WhenASetterOnlyOverrideOverridesABaseAutoProperty_HoldNoBackingFieldOfTheBase()
    {
        string[] names = MemberNames<SetterOnlyOverride>();

        Assert.DoesNotContain("<Label>k__BackingField", names, StringComparer.Ordinal);
    }

    [Fact]
    public void Members_WhenABaseAutoPropertyHasAPrivateGetter_HoldNoBackingFieldOfTheBase()
    {
        string[] names = MemberNames<DerivedFromPrivateGetter>();

        Assert.DoesNotContain("<Secret>k__BackingField", names, StringComparer.Ordinal);
    }

    [Fact]
    public void Members_WhenABaseAutoPropertyIsHiddenWithNew_HoldTheBackingFieldOfTheBase()
    {
        string[] names = MemberNames<HidesId>();

        Assert.Contains("<Id>k__BackingField (declared on ProtectedId)", names, StringComparer.Ordinal);
    }

    // ---- Compared types below the top level ----

    [Fact]
    public void EnsureAssignable_WhenOnlyABasePrivateFieldOfANestedObjectChanges_ReportsTheTopLevelMember()
    {
        Holder actual = new() { Inner = new Derived1N() };
        Holder expected = new() { Inner = new Derived1N() };
        actual.Inner.SetCount(Changed);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Inner\n  - <Inner>k__BackingField", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenABasePrivateFieldOfAnArrayElementChanges_Throws()
    {
        Derived[] actual = [new Derived()];
        Derived[] expected = [new Derived()];
        actual[0].SetCount(Changed);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            $"actual and expected are not equal. {typeof(Derived[])} is compared as a whole, so assignable patterns do not apply.",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenABasePrivateFieldOfAFrozenSetElementChanges_Throws()
    {
        Derived changedElement = new();
        changedElement.SetCount(Changed);
        FrozenSet<Derived> actual = new[] { changedElement }.ToFrozenSet();
        FrozenSet<Derived> expected = new[] { new Derived() }.ToFrozenSet();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            $"actual and expected are not equal. {typeof(FrozenSet<Derived>)} is compared as a whole, so assignable patterns do not apply.",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyTheBaseStateOfANestedCollectionChanges_ComparesItByElement()
    {
        CollectionHolder actual = new() { Bag = new Bag([1, 2]) };
        CollectionHolder expected = new() { Bag = new Bag([1, 2]) };
        actual.Bag.SetNote("changed");

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    // ---- Framework base classes ----

    [Fact]
    public void Members_WhenTheBaseClassIsException_HoldNoPrivateFieldOfException()
    {
        string[] privateFieldsOfException = PrivateInstanceFieldNames(typeof(Exception));

        string[] names = MemberNames<OrderRejectedException>();

        Assert.NotEmpty(privateFieldsOfException);
        Assert.Empty(names.Intersect(privateFieldsOfException, StringComparer.Ordinal));
    }

    [Fact]
    public void Members_WhenTheBaseClassIsObservableCollection_HoldNoPrivateFieldOfIt()
    {
        string[] privateFieldsOfObservableCollection = PrivateInstanceFieldNames(typeof(ObservableCollection<int>));

        string[] names = MemberNames<ObservableNumbers>();

        Assert.NotEmpty(privateFieldsOfObservableCollection);
        Assert.Empty(names.Intersect(privateFieldsOfObservableCollection, StringComparer.Ordinal));
    }

    [Fact]
    public void Members_WhenTheBaseClassIsDeflateStream_HoldNoPrivateFieldOfIt()
    {
        string[] privateFieldsOfDeflateStream = PrivateInstanceFieldNames(typeof(DeflateStream));
        byte[]? token = typeof(DeflateStream).Assembly.GetName().GetPublicKeyToken();

        string[] names = MemberNames<CompressingStream>();

        Assert.Equal("B77A5C561934E089", Convert.ToHexString(token!));
        Assert.NotEmpty(privateFieldsOfDeflateStream);
        Assert.Empty(names.Intersect(privateFieldsOfDeflateStream, StringComparer.Ordinal));
    }

    // No class signed with 31bf3856ad364e35 is reachable from this net8.0 test project: the token ships in
    // Microsoft.WindowsDesktop.App (for example WindowsBase), so the token decision itself is pinned, for
    // both tokens added to the framework stop.
    [Theory]
    [InlineData("31bf3856ad364e35")]
    [InlineData("b77a5c561934e089")]
    public void IsFrameworkPublicKeyToken_ForATokenOfTheSharedFrameworks_ReturnsTrue(string token)
    {
        bool isFramework = MemberComparison.IsFrameworkPublicKeyToken(Convert.FromHexString(token));

        Assert.True(isFramework);
    }

    [Fact]
    public void Members_WhenTheBaseClassIsList_StillHoldItsNonPrivateSize()
    {
        string[] names = MemberNames<Numbers>();

        Assert.Contains("_size", names, StringComparer.Ordinal);
    }

    private static string[] MemberNames<T>()
    {
        return MemberComparison.GetOrCacheMetadata(typeof(T)).Members.Select(member => member.Name).ToArray();
    }

    // The private instance fields a framework class declares, read from the framework itself.
    private static string[] PrivateInstanceFieldNames(Type type)
    {
        return type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(field => field.IsPrivate)
            .Select(field => field.Name)
            .ToArray();
    }

    private class Base
    {
        private int _count;

        public void SetCount(int count)
        {
            _count = count;
        }
    }

    private sealed class Derived : Base;

    private class Mid : Base;

    private sealed class Top : Mid;

    private class Base1N
    {
        private int _count;

        public void SetCount(int count)
        {
            _count = count;
        }
    }

    private sealed class Derived1N : Base1N;

    private sealed class Holder
    {
        public required Derived1N Inner { get; set; }
    }

    private class ListBase
    {
        private readonly List<int> _items = [];

        public void AddItem(int item)
        {
            _items.Add(item);
        }
    }

    private sealed class ListHolder : ListBase;

    private class StaticHolder
    {
        private static int _instances;

        protected StaticHolder()
        {
            _instances++;
        }
    }

    private sealed class DerivedFromStaticHolder : StaticHolder;

    private class ProtectedFields
    {
#pragma warning disable CS0414 // Assigned but never read: the fields exist to be compared.
        protected int _protected = 1;

        private protected int _privateProtected = 2;
#pragma warning restore CS0414
    }

    private sealed class DerivedFromProtectedFields : ProtectedFields;

    private class NamedBase
    {
        public string Name { get; set; } = "original";
    }

    private sealed class NamedDerived : NamedBase;

    private class NonPublicProperties
    {
        private string PrivateNote { get; set; } = "original";

        protected string ProtectedNote { get; set; } = "original";

        public void SetPrivateNote(string note)
        {
            PrivateNote = note;
        }

        public void SetProtectedNote(string note)
        {
            ProtectedNote = note;
        }
    }

    private sealed class DerivedFromNonPublicProperties : NonPublicProperties;

    private class B10
    {
        public virtual string P { get; set; } = "base";
    }

    private sealed class D10 : B10
    {
        public override string P { get; set; } = "derived";
    }

    private class A
    {
        public virtual string P { get; set; } = "a";
    }

    private class B : A
    {
        public override string P { get; set; } = "b";
    }

    private sealed class D : B
    {
        public override string P { get; set; } = "d";
    }

    private class LabelBase
    {
        public virtual string Label { get; set; } = "base";
    }

    private sealed class SetterOnlyOverride : LabelBase
    {
        public override string Label
        {
            set => base.Label = value;
        }
    }

    private class PrivateGetter
    {
        public string Secret { private get; set; } = "hidden";
    }

    private sealed class DerivedFromPrivateGetter : PrivateGetter;

    private class ProtectedId
    {
        protected int Id { get; set; }
    }

    private sealed class HidesId : ProtectedId
    {
        public new int Id { get; set; }
    }

    private class BagBase
    {
        private string _note = "original";

        public void SetNote(string note)
        {
            _note = note;
        }
    }

    private sealed class Bag(int[] items) : BagBase, IEnumerable<int>
    {
        public IEnumerator<int> GetEnumerator() => ((IEnumerable<int>)items).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class CollectionHolder
    {
        public required Bag Bag { get; set; }
    }

    private sealed class OrderRejectedException : Exception;

    private sealed class ObservableNumbers : ObservableCollection<int>;

    private sealed class CompressingStream() : DeflateStream(Stream.Null, CompressionMode.Compress);

    private sealed class Numbers : List<int>;

    // One generic base level.
    private abstract class AggregateLike<TEvent>
    {
        private readonly List<TEvent> _domainEvents = [];

        protected void Raise(TEvent domainEvent)
        {
            _domainEvents.Add(domainEvent);
        }
    }

    private sealed class Invoice : AggregateLike<string>
    {
        public void Issue()
        {
            Raise("InvoiceIssued");
        }
    }

    // Two generic base levels, as in an event-sourced aggregate.
    private abstract class AggregateRoot<TId, TEvent>
    {
        private readonly List<TEvent> _domainEvents = [];

        public TId? Id { get; protected set; }

        public long Version { get; protected set; }

        public void IncrementVersion()
        {
            Version++;
        }

        protected void Raise(TEvent domainEvent)
        {
            _domainEvents.Add(domainEvent);
        }
    }

    private abstract class EsAggregateRoot<TId, TEvent> : AggregateRoot<TId, TEvent>;

    private sealed class Order : EsAggregateRoot<string, string>
    {
        public void Place()
        {
            Raise("OrderPlaced");
        }
    }
}

// The runtime is simulated as Native AOT. The "no members visible" rule is decided on the members of the
// compared type, before the private fields of its base classes are added.
[Collection("EnvironmentVariables")]
public class EnsureAssignableBaseClassFieldWithoutDynamicCodeTests
{
    [Fact]
    public void EnsureAssignable_WhenTheOnlyStateIsAPrivateFieldOfTheBaseClass_ThrowsNoMembersVisible()
    {
        OnlyBaseState actual = new();
        OnlyBaseState expected = new();

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(
            $"EnsureAssignable cannot compare {typeof(OnlyBaseState)}: no properties or fields are visible to "
                + "reflection under Native AOT. If the type has members, the generic argument passed to "
                + "EnsureAssignable is missing its [DynamicallyAccessedMembers] annotation; otherwise set "
                + "DBC_POST=off (disables all postcondition checks).",
            cannotCompare.Message
        );
    }

    [Fact]
    public void EnsureAssignable_WhenANestedObjectsOnlyStateIsAPrivateFieldOfItsBaseClass_ThrowsNoMembersVisible()
    {
        StateHolder actual = new() { Inner = new OnlyBaseState() };
        StateHolder expected = new() { Inner = new OnlyBaseState() };

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(
            $"EnsureAssignable cannot compare {typeof(OnlyBaseState)} (reached through 'StateHolder.Inner'): "
                + "no properties or fields are visible to reflection under Native AOT. "
                + "Their Equals reports them unequal (without an Equals override this only means they are different instances), and their members cannot be listed. "
                + "Ways out: list 'Inner' as assignable (patterns are regular expressions "
                + "matched against top-level member names, so use the plain member name); "
                + "if the type has members, preserve them, for example with "
                + "[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(X))] where X is that type; "
                + "or set DBC_POST=off (disables all postcondition checks).",
            cannotCompare.Message
        );
    }

    private class StateBase
    {
#pragma warning disable CS0414 // Assigned but never read: the field exists to be compared.
        private int _state = 1;
#pragma warning restore CS0414
    }

    private sealed class OnlyBaseState : StateBase;

    private sealed class StateHolder
    {
        public required OnlyBaseState Inner { get; set; }
    }
}
