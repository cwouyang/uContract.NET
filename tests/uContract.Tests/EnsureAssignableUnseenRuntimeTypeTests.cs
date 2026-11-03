using System.Collections;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using uContract.Exceptions;

namespace uContract.Tests;

// The runtime is simulated as Native AOT, with chosen types showing no declared member, as Native AOT shows a type
// that nothing preserves. Under the JIT every type shows its members, so dynamic code off alone cannot reach this.
[Collection("EnvironmentVariables")]
public class EnsureAssignableUnseenRuntimeTypeTests
{
    // The C# name of this class, which the nested fixtures below are named through.
    private const string ThisClass = "uContract.Tests.EnsureAssignableUnseenRuntimeTypeTests";

    // The fix names the runtime type in C# syntax, so the attribute can be copied as it is.
    private static string CannotCompare(Type runtimeType, Type declaredType, string runtimeTypeInCSharp)
    {
        return $"EnsureAssignable cannot compare {runtimeType}, the runtime type of the values (T is {declaredType}): "
            + "under Native AOT it shows no declared member to reflection, as a type that nothing preserves does, so "
            + "its members cannot be listed. Preserve it with "
            + $"[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof({runtimeTypeInCSharp}))] on a method "
            + "that runs, or pass the values as that type; otherwise set DBC_POST=off (disables all postcondition "
            + "checks).";
    }

    // Two sequences of different runtime types: the message names the side, and the values cannot be passed as
    // one runtime type.
    private static string CannotCompareOneSide(
        Type runtimeType,
        string side,
        Type declaredType,
        string runtimeTypeInCSharp
    )
    {
        return $"EnsureAssignable cannot compare {runtimeType}, the runtime type of {side} (T is {declaredType}): "
            + "under Native AOT it shows no declared member to reflection, as a type that nothing preserves does, so "
            + "its members cannot be listed. Preserve it with "
            + $"[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof({runtimeTypeInCSharp}))] on a method "
            + "that runs; otherwise set DBC_POST=off (disables all postcondition checks).";
    }

    // A compiler-generated type cannot be named in typeof, so the fix offers what can be written instead.
    private const string CompilerGeneratedWaysOut =
        "The type is compiler-generated and cannot be named in typeof. Ways out: use a value of a type of yours "
        + "instead; or set DBC_POST=off (disables all postcondition checks).";

    // A list of anonymous values is already materialised; what can change is its element type.
    private const string GeneratedElementWaysOut =
        "The type has a compiler-generated type among its generic arguments and cannot be named in typeof. Ways out: "
        + "use an element type of yours instead of the compiler-generated one; or set DBC_POST=off (disables all "
        + "postcondition checks).";

    private static string CannotCompareCompilerGenerated(Type runtimeType, Type declaredType, string waysOut)
    {
        return $"EnsureAssignable cannot compare {runtimeType}, the runtime type of the values (T is {declaredType}): "
            + "under Native AOT it shows no declared member to reflection, as a type that nothing preserves does, so "
            + "its members cannot be listed. "
            + waysOut;
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTheRuntimeTypeThatShowsNoDeclaredMemberIsAnonymous_NamesNoTypeofInTheFix()
    {
        object actual = new { Seat = 1 };
        object expected = new { Seat = 1 };

        Exception? exception = TestRuntime.WithUnseenTypes(
            [actual.GetType()],
            () => Contract.EnsureAssignable(actual, expected)
        );

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(
            CannotCompareCompilerGenerated(actual.GetType(), typeof(object), CompilerGeneratedWaysOut),
            cannotCompare.Message
        );
    }

    // List<T> can be written, but not with an anonymous type as T.
    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTheRuntimeTypeThatShowsNoDeclaredMemberHasAnAnonymousGenericArgument_NamesNoTypeofInTheFix()
    {
        object actual = new[] { new { Seat = 1 } }.ToList();
        object expected = new[] { new { Seat = 1 } }.ToList();

        Exception? exception = TestRuntime.WithUnseenTypes(
            [actual.GetType()],
            () => Contract.EnsureAssignable(actual, expected)
        );

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(
            CannotCompareCompilerGenerated(actual.GetType(), typeof(object), GeneratedElementWaysOut),
            cannotCompare.Message
        );
    }

    [Fact]
    public void EnsureAssignable_WhenTheRuntimeTypeThatShowsNoDeclaredMemberIsGeneric_NamesItInCSharpSyntaxInTheFix()
    {
        TicketBase actual = new GenericTicket<int> { Value = 1 };
        TicketBase expected = new GenericTicket<int> { Value = 1 };

        Exception? exception = TestRuntime.WithUnseenTypes(
            [typeof(GenericTicket<int>)],
            () => Contract.EnsureAssignable(actual, expected)
        );

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(
            CannotCompare(typeof(GenericTicket<int>), typeof(TicketBase), $"{ThisClass}.GenericTicket<System.Int32>"),
            cannotCompare.Message
        );
    }

    [Fact]
    public void EnsureAssignable_WhenTheRuntimeTypeThatShowsNoDeclaredMemberIsNestedInAGenericClass_NamesItInCSharpSyntaxInTheFix()
    {
        object actual = new Box<string>.Lid { Color = "red" };
        object expected = new Box<string>.Lid { Color = "red" };

        Exception? exception = TestRuntime.WithUnseenTypes(
            [typeof(Box<string>.Lid)],
            () => Contract.EnsureAssignable(actual, expected)
        );

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(
            CannotCompare(typeof(Box<string>.Lid), typeof(object), $"{ThisClass}.Box<System.String>.Lid"),
            cannotCompare.Message
        );
    }

    // C# writes the ranks of an array of arrays outermost first: int[][,] is an array of int[,].
    [Fact]
    public void EnsureAssignable_WhenAGenericArgumentOfTheRuntimeTypeIsAnArrayOfArrays_WritesItsRanksOutermostFirst()
    {
        TicketBase actual = new GenericTicket<int[][,]>();
        TicketBase expected = new GenericTicket<int[][,]>();

        Exception? exception = TestRuntime.WithUnseenTypes(
            [typeof(GenericTicket<int[][,]>)],
            () => Contract.EnsureAssignable(actual, expected)
        );

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(
            CannotCompare(
                typeof(GenericTicket<int[][,]>),
                typeof(TicketBase),
                $"{ThisClass}.GenericTicket<System.Int32[][,]>"
            ),
            cannotCompare.Message
        );
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
        Assert.Equal(CannotCompare(typeof(Ticket), typeof(object), $"{ThisClass}.Ticket"), cannotCompare.Message);
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
        Assert.Equal(
            CannotCompare(typeof(DerivedTicket), typeof(TicketBase), $"{ThisClass}.DerivedTicket"),
            cannotCompare.Message
        );
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
        Assert.Equal(
            CannotCompare(typeof(SeededRandom), typeof(Random), $"{ThisClass}.SeededRandom"),
            cannotCompare.Message
        );
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
        Assert.Equal(
            CannotCompare(
                typeof(List<int>),
                typeof(IReadOnlyList<int>),
                "System.Collections.Generic.List<System.Int32>"
            ),
            cannotCompare.Message
        );
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
    public void EnsureAssignable_WhenTheRuntimeTypeIsTAndShowsNoDeclaredMemberAndAMemberOfTChanged_ReportsThatMember()
    {
        Ticket actual = new() { Seat = 2 };
        Ticket expected = new() { Seat = 1 };

        Exception? exception = TestRuntime.WithUnseenTypes(
            [typeof(Ticket)],
            () => Contract.EnsureAssignable(actual, expected)
        );

        PostconditionViolationException violation = Assert.IsType<PostconditionViolationException>(exception);
        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Seat\n  - <Seat>k__BackingField",
            violation.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenTwoSequencesOfDifferentRuntimeTypesAndExpectedShowsNoDeclaredMember_ThrowsCannotCompare()
    {
        List<int> actual = [1];
        TaggedList expected = [1];

        Exception? exception = TestRuntime.WithUnseenTypes(
            [typeof(TaggedList)],
            () => Contract.EnsureAssignable<IEnumerable<int>>(actual, expected)
        );

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(
            CannotCompareOneSide(typeof(TaggedList), "expected", typeof(IEnumerable<int>), $"{ThisClass}.TaggedList"),
            cannotCompare.Message
        );
    }

    // Two sequences of different runtime types are compared by their elements; no Equals is asked, so a value
    // type is no exception here.
    [Fact]
    public void EnsureAssignable_WhenTwoSequencesOfDifferentRuntimeTypesAndOneIsAStructThatShowsNoDeclaredMember_ThrowsCannotCompare()
    {
        TaggedNumbers actual = new([1], "renamed");
        List<int> expected = [1];

        Exception? exception = TestRuntime.WithUnseenTypes(
            [typeof(TaggedNumbers)],
            () => Contract.EnsureAssignable<IEnumerable<int>>(actual, expected)
        );

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(
            CannotCompareOneSide(
                typeof(TaggedNumbers),
                "actual",
                typeof(IEnumerable<int>),
                $"{ThisClass}.TaggedNumbers"
            ),
            cannotCompare.Message
        );
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

    // A value of a value type other than T is compared as a member is: its Equals decides first.
    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTheValuesAreEqualBoxedDateTimesThatShowNoDeclaredMember_DoesNotThrow()
    {
        object actual = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);
        object expected = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);

        Exception? exception = TestRuntime.WithUnseenTypes(
            [typeof(DateTime)],
            () => Contract.EnsureAssignable(actual, expected)
        );

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAnInterfaceAndTheValuesAreEqualBoxedDecimalsThatShowNoDeclaredMember_DoesNotThrow()
    {
        IComparable actual = 12.50m;
        IComparable expected = 12.50m;

        Exception? exception = TestRuntime.WithUnseenTypes(
            [typeof(decimal)],
            () => Contract.EnsureAssignable(actual, expected)
        );

        Assert.Null(exception);
    }

    // A struct without a field is always equal to another of its type.
    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTheValuesAreOfAnEmptyStructThatShowsNoDeclaredMember_DoesNotThrow()
    {
        object actual = new EmptyStamp();
        object expected = new EmptyStamp();

        Exception? exception = TestRuntime.WithUnseenTypes(
            [typeof(EmptyStamp)],
            () => Contract.EnsureAssignable(actual, expected)
        );

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTheValuesOfAStructThatShowsNoDeclaredMemberAreUnequal_ThrowsCannotCompare()
    {
        object actual = new Stamp { Code = 2 };
        object expected = new Stamp { Code = 1 };

        Exception? exception = TestRuntime.WithUnseenTypes(
            [typeof(Stamp)],
            () => Contract.EnsureAssignable(actual, expected)
        );

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(CannotCompare(typeof(Stamp), typeof(object), $"{ThisClass}.Stamp"), cannotCompare.Message);
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
        Assert.Equal(
            CannotCompareOneSide(typeof(TaggedList), "actual", typeof(IEnumerable<int>), $"{ThisClass}.TaggedList"),
            cannotCompare.Message
        );
    }

    [Fact]
    public void EnsureAssignable_WhenTheRuntimeTypeIsAYieldIteratorThatShowsNoDeclaredMemberAndItsElementsAreEqual_DoesNotThrow()
    {
        IEnumerable<int> actual = CountTo(2);
        IEnumerable<int> expected = CountTo(2);

        Exception? exception = TestRuntime.WithUnseenTypes(
            [CountTo(0).GetType()],
            () => Contract.EnsureAssignable(actual, expected)
        );

        Assert.Null(exception);
    }

    // A real yield iterator (<CountTo>d__N) is compared by its elements without dynamic code. Under Native AOT one
    // that nothing preserves also lists no field to reflection, which the older "no members visible" rule would read
    // as hidden members; that is AotSmoke check 133. (A hand-written [CompilerGenerated] stand-in with a name C# can
    // write is a class of yours since spec amendment 8.)
    [Fact]
    public void EnsureAssignable_WhenDynamicCodeIsNotSupportedAndTheRuntimeTypeIsAYieldIteratorOfEqualElements_DoesNotThrow()
    {
        IEnumerable<int> actual = CountTo(2);
        IEnumerable<int> expected = CountTo(2);

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        Assert.Null(exception);
    }

    // A [CompilerGenerated] class with a name C# can write (as a source generator emits) can be named in typeof, so
    // the fix keeps it. (The enumerator of a real yield method, named <Method>d__N, is checked by AotSmoke 144.)
    [Fact]
    public void EnsureAssignable_WhenTIsAnEnumeratorInterfaceAndAGeneratedEnumeratorWithAWritableNameListsNoMember_KeepsTheTypeofFix()
    {
        IEnumerator<int> actual = new EnumeratorWithoutFields();
        IEnumerator<int> expected = new EnumeratorWithoutFields();

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(
            $"EnsureAssignable cannot compare {typeof(EnumeratorWithoutFields)}, the runtime type of the values (T is "
                + $"{typeof(IEnumerator<int>)}): no properties or fields are visible to reflection under Native AOT. "
                + "If the type has members, preserve them, for example with [DynamicDependency("
                + $"DynamicallyAccessedMemberTypes.All, typeof({ThisClass}.EnumeratorWithoutFields))] on a method that "
                + "runs, or pass the values as that type; otherwise set DBC_POST=off (disables all postcondition checks).",
            cannotCompare.Message
        );
    }

    // A C# 11 file class has the metadata name <File>F…__Name, which C# cannot write, but inside its file it is
    // named by its own name: the fix names it so.
    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTheRuntimeTypeThatShowsNoDeclaredMemberIsAFileClass_NamesItBySourceNameInTheFix()
    {
        object actual = new FileTicket { Seat = 1 };
        object expected = new FileTicket { Seat = 1 };

        Exception? exception = TestRuntime.WithUnseenTypes(
            [actual.GetType()],
            () => Contract.EnsureAssignable(actual, expected)
        );

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(
            CannotCompare(actual.GetType(), typeof(object), "uContract.Tests.FileTicket"),
            cannotCompare.Message
        );
    }

    // Preserving the internal type of a query cannot be written; materialising it can.
    [Fact]
    public void EnsureAssignable_WhenTIsObjectAndTheValuesAreQueriesThatShowNoDeclaredMember_ThrowsNotACollection()
    {
        int[] numbers = [1, 2];
        object actual = numbers.Select(number => number + 1);
        object expected = numbers.Select(number => number + 1);

        Exception? exception = TestRuntime.WithUnseenTypes(
            [actual.GetType()],
            () => Contract.EnsureAssignable(actual, expected)
        );

        InvalidOperationException notACollection = Assert.IsType<InvalidOperationException>(exception);
        Assert.StartsWith(
            $"EnsureAssignable cannot compare {actual.GetType()} as T ({typeof(object)}): it is a sequence that is not a "
                + "collection",
            notACollection.Message,
            StringComparison.Ordinal
        );
    }

    // ---- What counts as a declared member ----

    // A nested type is not a member of the type that declares it: Native AOT can keep the metadata of a nested
    // type while it keeps none of the type around it.
    [Fact]
    public void ShowsNoDeclaredMember_WhenTheTypeDeclaresOnlyANestedType_ReturnsTrue()
    {
        bool showsNone = TestRuntime.WithoutDynamicCode(() =>
            MemberComparison.ShowsNoDeclaredMember(typeof(OnlyANestedType))
        );

        Assert.True(showsNone);
    }

    // ---- Nested values ----

    // A yield iterator of yours is compared by its elements: enumerating it needs no metadata.
    [Fact]
    public void EnsureAssignable_WhenANestedYieldIteratorShowsNoDeclaredMemberAndItsElementsAreEqual_DoesNotThrow()
    {
        Tray actual = new() { Numbers = CountTo(2) };
        Tray expected = new() { Numbers = CountTo(2) };

        Exception? exception = TestRuntime.WithUnseenTypes(
            [CountTo(0).GetType()],
            () => Contract.EnsureAssignable(actual, expected)
        );

        Assert.Null(exception);
    }

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
    public void EnsureAssignable_WhenANestedAnonymousValueShowsNoDeclaredMemberAndItsEqualsReportsItUnequal_NamesNoTypeofInTheFix()
    {
        Crate actual = new() { Content = new { Level = 2 } };
        Crate expected = new() { Content = new { Level = 1 } };

        Exception? exception = TestRuntime.WithUnseenTypes(
            [actual.Content.GetType()],
            () => Contract.EnsureAssignable(actual, expected)
        );

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(
            $"EnsureAssignable cannot compare {actual.Content.GetType()} (reached through 'Crate.Content'): "
                + "no properties or fields are visible to reflection under Native AOT. Their Equals reports them "
                + "unequal (without an Equals override this only means they are different instances), and their "
                + "members cannot be listed. The type is compiler-generated and cannot be named in typeof. Ways out: "
                + "list 'Content' as assignable (patterns are regular expressions matched against top-level member "
                + "names, so use the plain member name); use a value of a type of yours instead; or set DBC_POST=off "
                + "(disables all postcondition checks).",
            cannotCompare.Message
        );
    }

    [Fact]
    public void EnsureAssignable_WhenAnArrayComparedAsAWholeHoldsAnonymousValuesThatShowNoDeclaredMember_NamesNoTypeofInTheFix()
    {
        object[] actual = [new { Level = 2 }];
        object[] expected = [new { Level = 1 }];

        Exception? exception = TestRuntime.WithUnseenTypes(
            [actual[0].GetType()],
            () => Contract.EnsureAssignable(actual, expected)
        );

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(
            $"EnsureAssignable cannot compare {actual[0].GetType()} (reached through 'actual[]'): no properties or "
                + "fields are visible to reflection under Native AOT. Their Equals reports them unequal (without an "
                + "Equals override this only means they are different instances), and their members cannot be "
                + "listed. "
                + CompilerGeneratedWaysOut,
            cannotCompare.Message
        );
    }

    // An enumerator cannot be materialised; the yield method that builds it can return a sequence instead.
    [Fact]
    public void EnsureAssignable_WhenANestedYieldEnumeratorShowsNoDeclaredMember_SuggestsReturningASequenceFromItsMethod()
    {
        Crate actual = new() { Content = CountUpTo(2) };
        Crate expected = new() { Content = CountUpTo(2) };

        Exception? exception = TestRuntime.WithUnseenTypes(
            [actual.Content.GetType()],
            () => Contract.EnsureAssignable(actual, expected)
        );

        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.EndsWith(
            "The type is compiler-generated and cannot be named in typeof. Ways out: list 'Content' as assignable "
                + "(patterns are regular expressions matched against top-level member names, so use the plain member "
                + "name); return IEnumerable<T> from the yield method that builds it, or use a value of a type of yours "
                + "instead; or set DBC_POST=off (disables all postcondition checks).",
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

    // ---- A collection expression ----

    // C# 12 builds a collection expression held as IReadOnlyList<T> as <>z__ReadOnlyArray<T>, which nothing
    // preserves; it is recognised by its "<>" name and carries no [CompilerGenerated] (measured on SDK 8.0.204). It
    // is compared by its elements, which enumerating needs no metadata for.
    [Fact]
    public void EnsureAssignable_WhenAnAggregateHoldsItsLinesAsACollectionExpressionThatShowsNoDeclaredMemberAndNothingChanged_DoesNotThrow()
    {
        Order order = new();
        order.AddLine(new Line("tea", 1));
        order.AddLine(new Line("cake", 2));
        Order old = Contract.Old(() => order);

        Exception? exception = TestRuntime.WithUnseenTypes(
            [order.Lines.GetType()],
            () => Contract.EnsureAssignable(order, old)
        );

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenAnAggregateHoldsItsLinesAsACollectionExpressionThatShowsNoDeclaredMemberAndALineChanged_ReportsTheLines()
    {
        Order order = new();
        order.AddLine(new Line("tea", 1));
        Order old = Contract.Old(() => order);
        order.ChangeQuantity(0, 3);

        Exception? exception = TestRuntime.WithUnseenTypes(
            [order.Lines.GetType()],
            () => Contract.EnsureAssignable(order, old)
        );

        PostconditionViolationException violation = Assert.IsType<PostconditionViolationException>(exception);
        Assert.Equal(
            "Fields were modified that are not marked as assignable:\n  - Lines\n  - <Lines>k__BackingField",
            violation.Description
        );
    }

    [Fact]
    public void EnsureAssignable_WhenTIsAReadOnlyListInterfaceAndTwoCollectionExpressionsThatShowNoDeclaredMemberHoldEqualElements_DoesNotThrow()
    {
        IReadOnlyList<int> actual = [1, 2, 3];
        IReadOnlyList<int> expected = [1, 2, 3];

        Exception? exception = TestRuntime.WithUnseenTypes(
            [actual.GetType()],
            () => Contract.EnsureAssignable(actual, expected)
        );

        Assert.Null(exception);
    }

    private sealed record Line(string Product, int Quantity);

    // A DDD-style aggregate that replaces its lines with a new collection expression on each change.
    private sealed class Order
    {
        public IReadOnlyList<Line> Lines { get; private set; } = [];

        public void AddLine(Line line)
        {
            Lines = [.. Lines, line];
        }

        public void ChangeQuantity(int index, int quantity)
        {
            Line[] lines = [.. Lines];
            lines[index] = lines[index] with { Quantity = quantity };
            Lines = [.. lines];
        }
    }

    private sealed class Crate
    {
        public object Content { get; set; } = new();
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

    private struct EmptyStamp;

    private readonly struct TaggedNumbers(int[] items, string tag) : IReadOnlyCollection<int>
    {
        public string Tag { get; } = tag;

        public int Count => items.Length;

        public IEnumerator<int> GetEnumerator() => ((IEnumerable<int>)items).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private struct Stamp
    {
        public int Code { get; set; }
    }

    private sealed class GenericTicket<TValue> : TicketBase
    {
        public TValue? Value { get; set; }
    }

    private sealed class Box<TItem>
    {
        public TItem? Item { get; set; }

        public sealed class Lid
        {
            public string Color { get; set; } = "";
        }
    }

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

    private static IEnumerable<int> CountTo(int last)
    {
        for (int number = 1; number <= last; number++)
        {
            yield return number;
        }
    }

    // Marked as the compiler marks the enumerator of a yield method that returns only IEnumerator<T>, and declares
    // no field and no public property.
    [CompilerGenerated]
    private sealed class EnumeratorWithoutFields : IEnumerator<int>
    {
        int IEnumerator<int>.Current => throw new NotSupportedException();

        object IEnumerator.Current => throw new NotSupportedException();

        bool IEnumerator.MoveNext()
        {
            return false;
        }

        void IEnumerator.Reset() { }

        void IDisposable.Dispose() { }
    }

    // A yield method that returns only an enumerator.
    private static IEnumerator<int> CountUpTo(int last)
    {
        for (int number = 1; number <= last; number++)
        {
            yield return number;
        }
    }

    private sealed class Tray
    {
        public IEnumerable<int> Numbers { get; set; } = [];
    }

    // Declares no field, property, method or constructor (a static class has no instance constructor).
    private static class OnlyANestedType
    {
        public enum State
        {
            Open,
            Closed,
        }
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

// A C# 11 file class of yours, which nothing preserves in the tests that report it as unseen.
file sealed class FileTicket
{
    public int Seat { get; set; }
}
