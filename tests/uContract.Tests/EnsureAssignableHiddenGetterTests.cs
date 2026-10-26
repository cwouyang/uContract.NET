using System.Reflection;
using uContract.Exceptions;

namespace uContract.Tests;

// A public property whose getter is not visible through the compared type is read through the getter that
// exists: the private getter its declaring type declares, or the getter of the nearest base declaration that
// a setter-only override overrides.
public class EnsureAssignableHiddenGetterTests
{
    private const string Violation = "Fields were modified that are not marked as assignable:\n";

    // ---- A base-class property with a non-public getter ----

    [Fact]
    public void EnsureAssignable_WhenAnInheritedPropertyWithAPrivateGetterChanges_ReportsOnlyThatProperty()
    {
        SecretDerived actual = new() { Secret = "a" };
        SecretDerived expected = new() { Secret = "b" };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Secret", exception.Description);
    }

    // ---- A virtual property whose override has only a setter ----

    [Fact]
    public void EnsureAssignable_WhenASetterOnlyOverrideChanges_ReportsOnlyThatProperty()
    {
        LabelOverride actual = new() { Label = "a" };
        LabelOverride expected = new() { Label = "b" };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Label", exception.Description);
    }

    // Under Native AOT, GetProperties returns both the setter-only override and the base declaration; the JIT
    // returns the override alone, so the listing Native AOT returns is passed in.
    [Fact]
    public void Members_WhenTheListingHoldsASetterOnlyOverrideAndItsBaseDeclaration_ListTheLabelOnce()
    {
        PropertyInfo[] listing =
        [
            typeof(LabelOverride).GetProperties().Single(),
            typeof(LabelBase).GetProperties().Single(),
        ];

        TypeMetadata metadata = MemberComparison.CreateMetadata(typeof(LabelOverride), listing);

        Assert.Single(
            metadata.Members,
            member => string.Equals(member.Name, nameof(LabelOverride.Label), StringComparison.Ordinal)
        );
    }

    // In the chain TitleBase <- GetterOverride (getter only) <- SetterOverride (setter only), the two overrides
    // declare no accessor in common; under Native AOT, GetProperties on SetterOverride returns both of them.
    [Fact]
    public void Members_WhenTheListingHoldsASetterOnlyOverrideAndTheGetterOnlyOverrideBelowIt_ListTheTitleOnce()
    {
        PropertyInfo[] listing =
        [
            typeof(SetterOverride).GetProperties().Single(),
            typeof(GetterOverride).GetProperties().Single(),
        ];

        TypeMetadata metadata = MemberComparison.CreateMetadata(typeof(SetterOverride), listing);

        Assert.Single(metadata.Members, IsTitle);
    }

    [Fact]
    public void Members_WhenTheListingHoldsBothOverridesAndTheBaseDeclaration_ListTheTitleOnce()
    {
        PropertyInfo[] listing =
        [
            typeof(SetterOverride).GetProperties().Single(),
            typeof(GetterOverride).GetProperties().Single(),
            typeof(TitleBase).GetProperties().Single(),
        ];

        TypeMetadata metadata = MemberComparison.CreateMetadata(typeof(SetterOverride), listing);

        Assert.Single(metadata.Members, IsTitle);
    }

    [Fact]
    public void Members_WhenTheListingHoldsBothOverrides_ReadTheTitleThroughTheGetterThatRuns()
    {
        PropertyInfo[] listing =
        [
            typeof(SetterOverride).GetProperties().Single(),
            typeof(GetterOverride).GetProperties().Single(),
        ];
        SetterOverride instance = new() { Title = "x" };

        MemberAccessor title = Assert.Single(
            MemberComparison.CreateMetadata(typeof(SetterOverride), listing).Members,
            IsTitle
        );

        Assert.Equal(GetterOverride.Prefix + "x", title.GetValue(instance));
    }

    // A property hidden with "new" is a different property, not another declaration of the base one: both are
    // listed, and, since their raw names collide, each is named with its declaring type.
    [Fact]
    public void Members_WhenTheListingHoldsAHidingPropertyAndTheBasePropertyItHides_ListBothQualified()
    {
        PropertyInfo[] listing =
        [
            typeof(HidingDerived).GetProperties().Single(),
            typeof(HiddenBase).GetProperties().Single(),
        ];

        TypeMetadata metadata = MemberComparison.CreateMetadata(typeof(HidingDerived), listing);

        Assert.Equal("Value (declared on HidingDerived), Value (declared on HiddenBase)", ValueNames(metadata));
    }

    [Fact]
    public void Members_WhenTheListingHoldsANewVirtualPropertyAndTheVirtualPropertyItHides_ListBothQualified()
    {
        PropertyInfo[] listing =
        [
            typeof(NewVirtualDerived).GetProperties().Single(),
            typeof(VirtualHiddenBase).GetProperties().Single(),
        ];

        TypeMetadata metadata = MemberComparison.CreateMetadata(typeof(NewVirtualDerived), listing);

        Assert.Equal(
            "Value (declared on NewVirtualDerived), Value (declared on VirtualHiddenBase)",
            ValueNames(metadata)
        );
    }

    // The properties named Value, in listing order; the backing fields are left out.
    private static string ValueNames(TypeMetadata metadata)
    {
        return string.Join(
            ", ",
            metadata
                .Members.Where(member =>
                    member.RawName.Equals("Value", StringComparison.Ordinal)
                    && member.Name.Contains("declared on", StringComparison.Ordinal)
                )
                .Select(member => member.Name)
        );
    }

    private static bool IsTitle(MemberAccessor member)
    {
        return string.Equals(member.Name, nameof(TitleBase.Title), StringComparison.Ordinal);
    }

    // ---- A getter found on a base class ----

    [Fact]
    public void EnsureAssignable_WhenTheGetterFoundOnTheBaseClassThrows_PropagatesTargetInvocationException()
    {
        ThrowingLabelOverride actual = new();
        ThrowingLabelOverride expected = new();

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    [Fact]
    public void EnsureAssignable_WhenThePropertyIsAssignable_DoesNotRunTheGetterFoundOnTheBaseClass()
    {
        ThrowingLabelOverride actual = new();
        ThrowingLabelOverride expected = new();

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(actual, expected, "^Label$"));

        Assert.Null(exception);
    }

    // ---- Compared types below the top level ----

    [Fact]
    public void EnsureAssignable_WhenANestedSetterOnlyOverrideChanges_ReportsTheTopLevelMember()
    {
        Holder actual = new()
        {
            Account = new SecretDerived { Secret = "s" },
            Tag = new LabelOverride { Label = "a" },
        };
        Holder expected = new()
        {
            Account = new SecretDerived { Secret = "s" },
            Tag = new LabelOverride { Label = "b" },
        };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(Violation + "  - Tag\n  - <Tag>k__BackingField", exception.Description);
    }

    private class SecretBase
    {
        public string Secret { private get; set; } = "";
    }

    private sealed class SecretDerived : SecretBase;

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

    private class TitleBase
    {
        public virtual string Title { get; set; } = "";
    }

    private class GetterOverride : TitleBase
    {
        public const string Prefix = "title: ";

        public override string Title => Prefix + base.Title;
    }

    private sealed class SetterOverride : GetterOverride
    {
        public override string Title
        {
            set => base.Title = value;
        }
    }

    private class HiddenBase
    {
        public string Value { get; set; } = "";
    }

    private sealed class HidingDerived : HiddenBase
    {
        public new string Value { get; set; } = "";
    }

    private class VirtualHiddenBase
    {
        public virtual string Value { get; set; } = "";
    }

    private class NewVirtualDerived : VirtualHiddenBase
    {
        public new virtual string Value { get; set; } = "";
    }

    private sealed class NewVirtualLeaf : NewVirtualDerived;

    private class ThrowingLabelBase
    {
        private readonly string _message = "getter failed";

        public virtual string Label
        {
            get => throw new ArgumentException(_message);
            set => _ = value;
        }
    }

    private sealed class ThrowingLabelOverride : ThrowingLabelBase
    {
        public override string Label
        {
            set => base.Label = value;
        }
    }

    private sealed class Holder
    {
        public SecretDerived Account { get; set; } = new();

        public LabelOverride Tag { get; set; } = new();
    }
}
