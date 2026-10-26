using System.Reflection;
using uContract.Exceptions;

namespace uContract.Tests;

// Members of one compared type that share a name are named with their declaring type.
public class EnsureAssignableNameCollisionTests
{
    private const string Violation = "Fields were modified that are not marked as assignable:\n";

    private const int Changed = 7;

    // ---- Two private fields of one name on two levels ----

    [Fact]
    public void EnsureAssignable_WhenOneOfTwoPrivateFieldsOfOneNameChanges_ReportsItWithItsDeclaringType()
    {
        Descendant actual = new();
        Descendant expected = new();
        actual.SetAncestorShadowed(Changed);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - _shadowed (declared on Ancestor)", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenBothPrivateFieldsOfOneNameChangeAndThePlainNameIsAssignable_DoesNotThrow()
    {
        Descendant actual = new();
        Descendant expected = new();
        actual.SetAncestorShadowed(Changed);
        actual.SetShadowed(Changed);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected, "_shadowed"));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenBothPrivateFieldsOfOneNameChangeAndTheAnchoredNameIsAssignable_DoesNotThrow()
    {
        Descendant actual = new();
        Descendant expected = new();
        actual.SetAncestorShadowed(Changed);
        actual.SetShadowed(Changed);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected, "^_shadowed$"));

        Assert.Null(exception);
    }

    // Patterns are matched against raw names only: the declaring type of a qualified name takes no part.
    [Fact]
    public void EnsureAssignable_WhenAPatternMatchesOnlyTheDeclaringTypeOfACollidingMember_ReportsThatMember()
    {
        StateDerived actual = new() { BaseCount = Changed };
        StateDerived expected = new();
        actual.SetBaseState(Changed);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected, "Base")
        );

        Assert.Equal(Violation + "  - _state (declared on StateBase)", exception.Description);
    }

    // A caller copies the text of a qualified entry up to its first space and lists it as assignable.
    [Fact]
    public void EnsureAssignable_WhenTheNameCopiedFromAQualifiedEntryIsAssignable_DoesNotThrow()
    {
        StateDerived actual = new();
        StateDerived expected = new();
        actual.SetBaseState(Changed);
        PostconditionViolationException violation = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );
        string copiedName = violation.Description[(Violation + "  - ").Length..].Split(' ')[0];

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected, copiedName));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenAPatternSpansTheDeclaringTypeAndTheNameOfACollidingMember_ReportsThatMember()
    {
        OrderEntity actual = new() { BaseParentId = Changed };
        OrderEntity expected = new();
        ((BaseEntity)actual).Id = Changed;

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected, "Base.*Id")
        );

        Assert.Equal(Violation + "  - Id (declared on BaseEntity)", exception.Description);
    }

    // ---- A public field hidden with "new" ----

    [Fact]
    public void EnsureAssignable_WhenAFieldAndTheFieldItHidesChange_ReportsEachWithItsDeclaringType()
    {
        CodeDerived actual = new() { Code = "changed" };
        CodeDerived expected = new();
        ((CodeBase)actual).Code = Changed;

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(
            Violation + "  - Code (declared on CodeDerived)\n  - Code (declared on CodeBase)",
            exception.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenAFieldAndTheFieldItHidesChangeAndTheAnchoredNameIsAssignable_DoesNotThrow()
    {
        CodeDerived actual = new() { Code = "changed" };
        CodeDerived expected = new();
        ((CodeBase)actual).Code = Changed;

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected, "^Code$"));

        Assert.Null(exception);
    }

    // ---- Backing fields of two auto-properties of one name ----

    [Fact]
    public void Members_WhenAProtectedAutoPropertyIsHiddenWithNew_NameThePropertyWithoutItsDeclaringType()
    {
        string[] names = MemberNames<HidesId>();

        Assert.Equal(
            ["Id", "<Id>k__BackingField (declared on HidesId)", "<Id>k__BackingField (declared on ProtectedId)"],
            names
        );
    }

    // ---- Names that do not collide ----

    [Fact]
    public void Members_WhenAnAutoPropertyIsListedWithItsOwnBackingField_NameBothWithoutTheirDeclaringType()
    {
        string[] names = MemberNames<WithId>();

        Assert.Equal(["Id", "<Id>k__BackingField"], names);
    }

    // Under Native AOT, GetProperties returns both a setter-only override and its base declaration; the JIT
    // returns the override alone, so the listing Native AOT returns is passed in.
    [Fact]
    public void Members_WhenTheListingHoldsASetterOnlyOverrideAndItsBaseDeclaration_NameTheLabelWithoutItsDeclaringType()
    {
        PropertyInfo[] listing =
        [
            typeof(LabelOverride).GetProperties().Single(),
            typeof(LabelBase).GetProperties().Single(),
        ];

        TypeMetadata metadata = MemberComparison.CreateMetadata(typeof(LabelOverride), listing);

        Assert.Equal(["Label"], metadata.Members.Select(member => member.Name).ToArray());
    }

    private static string[] MemberNames<T>()
    {
        return MemberComparison.GetOrCacheMetadata(typeof(T)).Members.Select(member => member.Name).ToArray();
    }

    private class BaseEntity
    {
        public int Id;

        public int BaseParentId;
    }

    private sealed class OrderEntity : BaseEntity
    {
#pragma warning disable CS0649 // Never assigned: the field exists to collide with the base field.
        public new int Id;
#pragma warning restore CS0649
    }

    private class StateBase
    {
        private int _state;

        public void SetBaseState(int state)
        {
            _state = state;
        }
    }

    private sealed class StateDerived : StateBase
    {
#pragma warning disable CS0169, CS0649 // Never assigned or read: the field exists to collide with the base field.
        private int _state;
#pragma warning restore CS0169, CS0649

        public int BaseCount { get; set; }
    }

    private class CodeBase
    {
        public int Code;
    }

    private sealed class CodeDerived : CodeBase
    {
        public new string Code = "original";
    }

    private sealed class WithId
    {
        public int Id { get; set; }
    }

    private class LabelBase
    {
        public virtual string Label { get; set; } = "";
    }

    private sealed class LabelOverride : LabelBase
    {
        public override string Label
        {
            set => base.Label = value;
        }
    }

    private class ProtectedId
    {
        protected int Id { get; set; }
    }

    private sealed class HidesId : ProtectedId
    {
        public new int Id { get; set; }
    }

    private class Ancestor
    {
        private int _shadowed;

        public void SetAncestorShadowed(int shadowed)
        {
            _shadowed = shadowed;
        }
    }

    private sealed class Descendant : Ancestor
    {
        private int _shadowed;

        public void SetShadowed(int shadowed)
        {
            _shadowed = shadowed;
        }
    }
}

// The comparisons that are blocked simulate the runtime as Native AOT, so a class with no members is one whose
// members are not visible, and comparing two different instances of it is blocked.
[Collection("EnvironmentVariables")]
public class EnsureAssignableNameCollisionWithoutDynamicCodeTests
{
    private const string Violation = "Fields were modified that are not marked as assignable:\n";

    [Fact]
    public void EnsureAssignable_WhenACollidingBaseBackingFieldCannotBeCompared_NamesItWithItsDeclaringType()
    {
        Opaque shared = new();
        ShelfDerived actual = new(shared, new Opaque());
        ShelfDerived expected = new(shared, new Opaque());

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(
            $"EnsureAssignable cannot compare {typeof(Opaque)} (reached through 'ShelfDerived.ShelfBase.X'): "
                + "no properties or fields are visible to reflection under Native AOT. "
                + "Their Equals reports them unequal (without an Equals override this only means they are different instances), and their members cannot be listed. "
                + "Ways out: list 'X' as assignable (patterns are regular expressions "
                + "matched against top-level member names, so use the plain member name; "
                + "it also excuses the other members of that name); "
                + "if the type has members, preserve them, for example with "
                + "[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(X))] where X is that type; "
                + "or set DBC_POST=off (disables all postcondition checks).",
            cannotCompare.Message
        );
    }

    [Fact]
    public void EnsureAssignable_WhenACollidingBackingFieldOneLevelDownCannotBeCompared_NamesItWithItsDeclaringType()
    {
        Opaque shared = new();
        ShelfHolder actual = new() { Inner = new ShelfDerived(shared, new Opaque()) };
        ShelfHolder expected = new() { Inner = new ShelfDerived(shared, new Opaque()) };

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(
            $"EnsureAssignable cannot compare {typeof(Opaque)} (reached through 'ShelfHolder.Inner.ShelfBase.X'): "
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

    [Fact]
    public void EnsureAssignable_WhenTheAdvisedPlainNameIsAssignable_DoesNotThrow()
    {
        Opaque shared = new();
        ShelfDerived actual = new(shared, new Opaque());
        ShelfDerived expected = new(shared, new Opaque());

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected, "X"));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyTheQualifiedNameIsAssignable_ReportsTheMember()
    {
        Opaque shared = new();
        ShelfDerived actual = new(shared, null);
        ShelfDerived expected = new(shared, new Opaque());

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected, @"ShelfBase\.<X>k__BackingField")
        );

        Assert.Equal(Violation + "  - <X>k__BackingField (declared on ShelfBase)", exception.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenACollidingBaseBackingFieldBecomesNull_ReportsItWithItsDeclaringType()
    {
        Opaque shared = new();
        ShelfDerived actual = new(shared, null);
        ShelfDerived expected = new(shared, new Opaque());

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - <X>k__BackingField (declared on ShelfBase)", exception.Description);
    }

    // ---- Declaring types of one name ----

    [Fact]
    public void EnsureAssignable_WhenTheCollidingMembersAreDeclaredOnTypesOfOneName_NamesTheFullNameInThePath()
    {
        Opaque shared = new();
        Item actual = new(shared, new Opaque());
        Item expected = new(shared, new Opaque());

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains(
            "(reached through 'Item.uContract.Tests.EnsureAssignableNameCollisionWithoutDynamicCodeTests+Outer+Item.X'): ",
            cannotCompare.Message
        );
        Assert.Contains("Ways out: list 'X' as assignable", cannotCompare.Message);
    }

    private static class Outer
    {
        public class Item(Opaque? baseX)
        {
            private Opaque? X { get; set; } = baseX;
        }
    }

    private sealed class Item(Opaque x, Opaque? baseX) : Outer.Item(baseX)
    {
        public Opaque X { get; set; } = x;
    }

    private sealed class ShelfHolder
    {
        public required ShelfDerived Inner { get; set; }
    }

    private sealed class Opaque;

    // A non-public auto-property of the base and a public auto-property of the derived class share the name X,
    // so their backing fields share a name too.
    private class ShelfBase(Opaque? baseX)
    {
        private Opaque? X { get; set; } = baseX;
    }

    private sealed class ShelfDerived(Opaque x, Opaque? baseX) : ShelfBase(baseX)
    {
        public Opaque X { get; set; } = x;
    }
}
