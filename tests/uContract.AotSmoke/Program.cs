using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using uContract.Exceptions;

namespace uContract.AotSmoke;

// ---- Fixtures that are NOT preserved. Never pass these to a reflection API in this program. ----

internal sealed class FlatType
{
    public string Name { get; set; } = "";
    public int Count { get; set; }
}

internal sealed class FieldsOnlyType
{
    public int Value;
}

internal struct PointStruct
{
    public int X { get; set; }
    public int Y { get; set; }
}

internal sealed class StructHolder
{
    public PointStruct Location { get; set; }
}

internal sealed class LockHolder
{
    private readonly object _lock = new();

    public int Value { get; private set; }

    public void Touch()
    {
        lock (_lock)
        {
            Value++;
        }
    }
}

internal sealed class CustomerInfo
{
    public string Name { get; set; } = "";
}

internal sealed class OrderHolder
{
    public CustomerInfo Customer { get; set; } = new();
}

internal sealed record AddressRecord(string Street);

// Has no members at all. As T of EnsureAssignable it is "not visible" under Native AOT.
internal sealed class Marker;

internal struct ListHolder
{
    public List<int> Items;
}

internal sealed class Runner
{
    public int Runs { get; private set; }

    public void Run()
    {
        Runs++;
    }
}

internal sealed class AddressHolder
{
    public AddressRecord Address { get; set; } = new("");
}

internal sealed class LineElement
{
    public string Sku { get; set; } = "";
}

internal sealed class LinesHolder
{
    public List<LineElement> Items { get; set; } = [];
}

// ---- Members the library must preserve for the top-level type itself (no DynamicDependency). ----

internal sealed class PrivateFieldOnly(int secret)
{
    private readonly int _secret = secret;

    public bool Matches(int value) => _secret == value;
}

internal class InheritedPropertyBase
{
    public int Level { get; set; }
}

internal sealed class InheritedPropertyDerived : InheritedPropertyBase;

internal class ProtectedFieldBase(int secret)
{
    protected int Secret = secret;

    public bool Matches(int value) => Secret == value;
}

internal sealed class ProtectedFieldDerived(int secret) : ProtectedFieldBase(secret);

// ---- Fixtures that ARE preserved: only the nested type, via DynamicDependency. ----

internal sealed class PreservedInner
{
    public string Value { get; set; } = "";
}

internal sealed class PreservedOuter
{
    public PreservedInner Inner { get; set; } = new();
}

// ---- Old fixtures (issue #45). Each check that uses them names its mutation and its source of visibility. ----

// Visible as the T of Old only.
internal sealed class VaultCode(int code, string label)
{
    private readonly int _code = code;
    private readonly string _label = label;

    public string Describe() => $"code={_code}|label={_label}";
}

// Never the T of any call; only reached through the private field of an Old-only T.
internal sealed class TallyCounter
{
    public int Value;
}

// Visible as the T of Old only: its private reference field is rooted by Old's [DynamicallyAccessedMembers].
internal sealed class OldOnlyTally
{
    private readonly TallyCounter _counter = new() { Value = 1 };

    public int Count => _counter.Value;

    public void Bump() => _counter.Value++;
}

// Visible only as the T of an annotated generic forwarder of Old.
internal sealed class ForwardedTally
{
    private readonly TallyCounter _counter = new() { Value = 1 };

    public int Count => _counter.Value;

    public void Bump() => _counter.Value++;
}

// Old is called with T = SnapshotShape; the runtime type is SnapshotCircle, whose fields are not preserved.
internal class SnapshotShape
{
    public SnapshotShape Snapshot() => Contract.Old(() => this);
}

internal sealed class SnapshotCircle : SnapshotShape
{
    public int Radius;
}

// Hidden: never a T, never preserved. It may be a type argument of a T or of a member of a T (the element
// type of FrozenLeavesHolder.Leaves).
internal sealed class HiddenLeaf
{
    public int Value;
}

// Hidden: never a T, never preserved; reached through a visible field of HiddenNestOuter.
internal sealed class HiddenNestInner
{
    public int Value;
    public HiddenLeaf Leaf = new();
}

internal sealed class HiddenNestOuter
{
    public HiddenNestInner Inner = new();
}

internal sealed class PreservedLeaf
{
    public int Value;
}

// Preserved with DynamicDependency(All) by its check.
internal sealed class PreservedNestInner
{
    public int Value;
    public PreservedLeaf Leaf = new();
}

internal sealed class PreservedNestOuter
{
    public PreservedNestInner Inner = new();
}

// Base-class auto-property: its backing field is private to the base and not preserved.
internal sealed record HiddenLine(string Sku);

internal class HiddenLinesBase
{
    public List<HiddenLine> Lines { get; set; } = [];
}

internal sealed class HiddenLinesDerived : HiddenLinesBase;

// The same shape on a separate hierarchy, preserved with DynamicDependency(All, typeof(PreservedLinesDerived)).
internal sealed record PreservedLine(string Sku);

internal class PreservedLinesBase
{
    public List<PreservedLine> Lines { get; set; } = [];
}

internal sealed class PreservedLinesDerived : PreservedLinesBase;

// SortedList<,> is used nowhere else: its definition is hidden.
internal sealed class ScoreBoard
{
    public SortedList<string, int> Scores = new(StringComparer.Ordinal) { ["a"] = 1 };
}

internal sealed class StockSheet
{
    public Dictionary<string, int> Stock { get; set; } = [];
}

internal sealed class PreservedStockSheet
{
    public Dictionary<string, int> Stock { get; set; } = [];
}

// Preserved with DynamicDependency(All) so that a change to it is reported, not "cannot compare".
internal sealed class CatalogItem
{
    public int Price;
}

internal sealed class Catalog
{
    public Dictionary<string, CatalogItem> Items = new(StringComparer.Ordinal);
}

// Preserved with DynamicDependency(All).
internal struct TagSet
{
    public List<string> Tags;
}

internal sealed class TagShelf
{
    public TagSet[] Sets = [];
}

// Preserved with DynamicDependency(All): a readonly field inside a struct field.
internal readonly struct ReadonlyTagBox(List<string> tags)
{
    public readonly List<string> Tags = tags;
}

internal struct OuterTagBox
{
    public ReadonlyTagBox Inner;
}

internal sealed class TagCabinet
{
    public OuterTagBox Box;
}

// Preserved with DynamicDependency(All), and Nullable<LabelSet> as well.
internal struct LabelSet
{
    public List<string> Labels;
}

internal sealed class LabelRack
{
    public LabelSet?[] Slots = [];
}

internal sealed class FinalizableFixture
{
    private static int s_finalized;

    public static int Finalized => Volatile.Read(ref s_finalized);

    public int Id;

#pragma warning disable MA0055 // The fixture must be finalizable: the check proves that Old's copies are never finalized.
    ~FinalizableFixture()
    {
        Interlocked.Increment(ref s_finalized);
    }
#pragma warning restore MA0055
}

internal sealed class PayloadHolder
{
    public object Payload = "";
}

internal sealed class Appointment
{
    public DateTime At { get; set; }
    public int Room { get; set; }
}

internal sealed class Greeter(string name)
{
    public string Greet() => "hi " + name;
}

internal sealed class GreetingHook
{
    public Func<string>? Hook { get; set; }
}

internal sealed class CompareHook
{
    public Func<int, int, int>? Hook { get; set; }
}

// The base auto-property's backing field is private to the base and not preserved.
internal class TaggedBase
{
    public ImmutableArray<string> Tags { get; set; } = [];
}

internal sealed class TaggedDerived : TaggedBase;

internal sealed class FrozenHolder
{
    public FrozenSet<string> Codes = FrozenSet<string>.Empty;
}

internal sealed class FrozenDictionaryHolder
{
    public FrozenDictionary<string, int> Counts = FrozenDictionary<string, int>.Empty;
}

// The elements are of a hidden type: under Native AOT they cannot be compared member by member.
internal sealed class FrozenLeavesHolder
{
    public FrozenSet<HiddenLeaf> Leaves = FrozenSet<HiddenLeaf>.Empty;
}

internal sealed unsafe class PointerHolder
{
    public int* Address;
}

// Hidden: never a T, never preserved. Reached through an interface-typed member.
internal interface IShippingLabel;

internal sealed record ShippingLabel(string Text) : IShippingLabel;

internal sealed class Parcel
{
    public IShippingLabel? Label { get; set; }
}

// The User class of docs/examples/USAGE_EXAMPLES.md (Field Assignment Validation), reduced to ChangeEmail
// and a ChangeEmailAndName that changes a member that is not assignable.
internal sealed class User(string email, string name)
{
    private string _email = email;
    private string _name = name;
    private DateTime _lastModified = DateTime.UtcNow;
    private readonly DateTime _createdAt = DateTime.UtcNow;

    // Neither changes in ChangeEmail; both are compared as public properties.
    public string Name => _name;

    public DateTime CreatedAt => _createdAt;

    public void ChangeEmail(string newEmail)
    {
        var oldState = Contract.Old(() => this);

        _email = newEmail;
        _lastModified = DateTime.UtcNow;

        Contract.EnsureAssignable(this, oldState, nameof(_email), nameof(_lastModified));
    }

    public void ChangeEmailAndName(string newEmail, string newName)
    {
        var oldState = Contract.Old(() => this);

        _email = newEmail;
        _name = newName;
        _lastModified = DateTime.UtcNow;

        Contract.EnsureAssignable(this, oldState, nameof(_email), nameof(_lastModified));
    }
}

internal sealed class RingNode
{
    public int Value;
    public RingNode? Next;
}

// Old shares an instance of this class with the original: it derives from CancellationTokenSource (issue #54).
internal sealed class CountingTokenSource : CancellationTokenSource
{
    public int Uses;

    public void Use()
    {
        var old = Contract.Old(() => this);
        Uses++;
        Contract.EnsureAssignable(this, old);
    }
}

internal sealed record Expect(
    Type? ExceptionType,
    string[] Substrings,
    bool ExpectDefault,
    string? ExpectedValue = null
)
{
    public static Expect Ok { get; } = new(null, [], false);

    public static Expect DefaultValue { get; } = new(null, [], true);

    // Passes when the result, rendered as a string, equals expected (ordinal); a null or default result renders as "default".
    public static Expect Value(string expected) => new(null, [], false, expected);

    public static Expect Throws<TException>(params string[] substrings)
        where TException : Exception => new(typeof(TException), substrings, false);
}

internal sealed record Check(int Id, string Name, Func<object?> Run, Expect Enabled, Expect Disabled);

public static class Program
{
    private const string NestedTypeName = "uContract.AotSmoke.CustomerInfo";
    private const string LineTypeName = "uContract.AotSmoke.LineElement";
    private const string PreservedLineTypeName = "uContract.AotSmoke.PreservedLine";

    private static readonly string[] s_frozenCodes = ["a", "b"];
    private static readonly string[] s_otherFrozenCodes = ["a", "c"];

    public static int Main(string[] args)
    {
        // --assert refuses to fall back to report mode, so a publish that stopped being Native AOT fails the run.
        bool assert = args.Contains("--assert", StringComparer.Ordinal);
        if (assert && RuntimeFeature.IsDynamicCodeSupported)
        {
            Console.WriteLine("FAIL 0 Assert mode expected=Native AOT got=dynamic code supported");
            return 1;
        }

        bool report =
            !assert && (RuntimeFeature.IsDynamicCodeSupported || args.Contains("--report", StringComparer.Ordinal));
        bool postconditionsEnabled = new ContractConfiguration().PostconditionsEnabled;
        int failures = 0;

        foreach (Check check in BuildChecks())
        {
            Expect expect = postconditionsEnabled ? check.Enabled : check.Disabled;
            (string status, string expected, string got, string message) = Evaluate(check, expect, report);
            Console.WriteLine($"{status} {check.Id} {check.Name} expected={expected} got={got} {message}");
            if (string.Equals(status, "FAIL", StringComparison.Ordinal))
            {
                failures++;
            }
        }

        return failures == 0 ? 0 : 1;
    }

    private static (string Status, string Expected, string Got, string Message) Evaluate(
        Check check,
        Expect expect,
        bool report
    )
    {
        object? value = null;
        Exception? thrown = null;
        try
        {
            value = check.Run();
        }
        catch (Exception ex)
        {
            thrown = ex;
        }

        string expected;
        string got;
        bool pass;
        if (expect.ExceptionType is not null)
        {
            expected = expect.ExceptionType.Name;
            got = thrown?.GetType().Name ?? "no exception";
            string text = thrown?.Message ?? "";
            string? missing = expect.Substrings.FirstOrDefault(s => !text.Contains(s, StringComparison.Ordinal));
            pass = thrown?.GetType() == expect.ExceptionType && missing is null;
            if (thrown is not null && missing is not null)
            {
                got += $" missing '{missing}'";
            }
        }
        else
        {
            bool isDefault = value is null || (value is int number && number == 0);
            string rendered = isDefault ? "default" : value?.ToString() ?? "default";
            expected =
                expect.ExpectedValue is not null ? $"value {expect.ExpectedValue}"
                : expect.ExpectDefault ? "default value"
                : "no exception";
            if (thrown is not null)
            {
                got = thrown.GetType().Name;
            }
            else if (expect.ExpectedValue is not null)
            {
                got = $"value {rendered}";
            }
            else if (expect.ExpectDefault)
            {
                got = isDefault ? "default value" : $"value {value}";
            }
            else
            {
                got = "no exception";
            }

            pass =
                thrown is null
                && (
                    expect.ExpectedValue is not null
                        ? string.Equals(rendered, expect.ExpectedValue, StringComparison.Ordinal)
                        : !expect.ExpectDefault || isDefault
                );
        }

        string status =
            report ? "INFO"
            : pass ? "PASS"
            : "FAIL";
        return (status, expected, got, FirstLine(thrown?.Message));
    }

    private static string FirstLine(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return "";
        }

        int newline = message.IndexOf('\n', StringComparison.Ordinal);
        return (newline < 0 ? message : message[..newline]).TrimEnd('\r');
    }

    private static string[] NestedText(string typeName, string path, string member) =>
        [typeName, path, $"'{member}' as assignable", "DynamicDependency", "DBC_POST=off"];

    private static object? NoResult(Action action)
    {
        action();
        return null;
    }

    private static IEnumerable<Check> BuildChecks()
    {
        Expect customerThrows = Expect.Throws<InvalidOperationException>(
            NestedText(NestedTypeName, "OrderHolder.Customer", "Customer")
        );
        Expect post = Expect.Throws<PostconditionViolationException>();

        yield return new Check(
            1,
            "Require false",
            static () => NoResult(static () => Contract.Require("smoke", static () => false)),
            Expect.Throws<PreconditionViolationException>(),
            Expect.Throws<PreconditionViolationException>()
        );
        // Old int: no mutation; visibility: T (int).
        yield return new Check(
            2,
            "Old int",
            static () => Contract.Old(static () => 42),
            Expect.Value("42"),
            Expect.Value("default")
        );

        // Old list: no mutation; visibility: T (List<string>), which makes List<> visible to every check.
        yield return new Check(3, "Old list", OldList, Expect.Value("a|sameInstance=False"), Expect.Value("default"));
        yield return new Check(
            4,
            "Flat unchanged",
            static () =>
                NoResult(static () =>
                    Contract.EnsureAssignable(
                        new FlatType { Name = "a", Count = 1 },
                        new FlatType { Name = "a", Count = 1 }
                    )
                ),
            Expect.Ok,
            Expect.Ok
        );
        yield return new Check(
            5,
            "Flat property changed",
            static () =>
                NoResult(static () =>
                    Contract.EnsureAssignable(
                        new FlatType { Name = "b", Count = 1 },
                        new FlatType { Name = "a", Count = 1 }
                    )
                ),
            post,
            Expect.Ok
        );
        yield return new Check(
            6,
            "Flat property changed assignable",
            static () =>
                NoResult(static () =>
                    Contract.EnsureAssignable(
                        new FlatType { Name = "b", Count = 1 },
                        new FlatType { Name = "a", Count = 1 },
                        "Name"
                    )
                ),
            Expect.Ok,
            Expect.Ok
        );
        yield return new Check(
            7,
            "Fields-only field changed",
            static () =>
                NoResult(static () =>
                    Contract.EnsureAssignable(new FieldsOnlyType { Value = 2 }, new FieldsOnlyType { Value = 1 })
                ),
            post,
            Expect.Ok
        );
        yield return new Check(
            8,
            "Struct member changed",
            static () =>
                NoResult(static () =>
                    Contract.EnsureAssignable(
                        new StructHolder
                        {
                            Location = new PointStruct { X = 2, Y = 2 },
                        },
                        new StructHolder
                        {
                            Location = new PointStruct { X = 1, Y = 2 },
                        }
                    )
                ),
            post,
            Expect.Ok
        );
        yield return new Check(
            9,
            "Private lock unchanged",
            static () =>
                NoResult(static () =>
                {
                    LockHolder actual = new();
                    LockHolder expected = new();
                    actual.Touch();
                    expected.Touch();
                    Contract.EnsureAssignable(actual, expected);
                }),
            Expect.Ok,
            Expect.Ok
        );
        yield return new Check(
            10,
            "Nested class unchanged",
            static () =>
                NoResult(static () =>
                    Contract.EnsureAssignable(
                        new OrderHolder { Customer = new CustomerInfo { Name = "a" } },
                        new OrderHolder { Customer = new CustomerInfo { Name = "a" } }
                    )
                ),
            customerThrows,
            Expect.Ok
        );
        yield return new Check(
            11,
            "Nested class value changed",
            static () =>
                NoResult(static () =>
                    Contract.EnsureAssignable(
                        new OrderHolder { Customer = new CustomerInfo { Name = "b" } },
                        new OrderHolder { Customer = new CustomerInfo { Name = "a" } }
                    )
                ),
            customerThrows,
            Expect.Ok
        );
        yield return new Check(
            12,
            "Nested class member assignable",
            static () =>
                NoResult(static () =>
                    Contract.EnsureAssignable(
                        new OrderHolder { Customer = new CustomerInfo { Name = "b" } },
                        new OrderHolder { Customer = new CustomerInfo { Name = "a" } },
                        "Customer"
                    )
                ),
            Expect.Ok,
            Expect.Ok
        );
        yield return new Check(
            13,
            "Nested record unchanged",
            static () =>
                NoResult(static () =>
                    Contract.EnsureAssignable(
                        new AddressHolder { Address = new AddressRecord("x") },
                        new AddressHolder { Address = new AddressRecord("x") }
                    )
                ),
            // R8: AddressRecord has no visible members, and its Equals says the two records are equal.
            Expect.Ok,
            Expect.Ok
        );
        yield return new Check(
            14,
            "List of elements",
            static () =>
                NoResult(static () =>
                    Contract.EnsureAssignable(
                        new LinesHolder { Items = [new LineElement { Sku = "s" }] },
                        new LinesHolder { Items = [new LineElement { Sku = "s" }] }
                    )
                ),
            Expect.Throws<InvalidOperationException>(NestedText(LineTypeName, "LinesHolder.Items[]", "Items")),
            Expect.Ok
        );
        yield return new Check(15, "Preserved nested value changed", PreservedChanged, post, Expect.Ok);
        yield return new Check(16, "Preserved nested unchanged", PreservedUnchanged, Expect.Ok, Expect.Ok);
        yield return new Check(
            17,
            "Immutable collection",
            static () => Contract.EnsureImmutableCollection(ImmutableList.Create("a", "b")),
            Expect.Ok,
            Expect.Ok
        );
        yield return new Check(
            18,
            "Mutable collection",
            static () => Contract.EnsureImmutableCollection(new List<string> { "a" }),
            post,
            Expect.Ok
        );
        yield return new Check(
            19,
            "Private field only changed",
            static () =>
                NoResult(static () => Contract.EnsureAssignable(new PrivateFieldOnly(2), new PrivateFieldOnly(1))),
            post,
            Expect.Ok
        );
        yield return new Check(
            20,
            "Inherited property changed",
            static () =>
                NoResult(static () =>
                    Contract.EnsureAssignable(
                        new InheritedPropertyDerived { Level = 2 },
                        new InheritedPropertyDerived { Level = 1 }
                    )
                ),
            post,
            Expect.Ok
        );
        yield return new Check(
            21,
            "Inherited protected field changed",
            static () =>
                NoResult(static () =>
                    Contract.EnsureAssignable(new ProtectedFieldDerived(2), new ProtectedFieldDerived(1))
                ),
            post,
            Expect.Ok
        );

        // ---- Old and EnsureAssignable under Native AOT (issue #45, spec section 5). ----
        // Each check names its mutation and its source of visibility. "Off" is the DBC_POST=off column: Old
        // returns default there, and EnsureAssignable returns without comparing and without checking its
        // compared values, so a check that passes Old's result to EnsureAssignable does nothing (issue #47).
        Expect offDefault = Expect.Value("default");

        // Mutation: none. Visibility: T. Private fields of T keep their values.
        yield return new Check(
            22,
            "Old private-field type",
            static () => Contract.Old(static () => new VaultCode(7, "x"))?.Describe(),
            Expect.Value("code=7|label=x"),
            offDefault
        );

        // Mutation: field write in the object a private field of T refers to. Visibility: T only, through
        // Old's [DynamicallyAccessedMembers]: the private field is visited, so the counter is copied.
        yield return new Check(
            23,
            "Old-only T private reference",
            OldOnlyPrivateReference,
            Expect.Value("copy=1|original=2"),
            offDefault
        );

        // The same through a generic forwarder annotated with Old's [DynamicallyAccessedMembers].
        yield return new Check(
            24,
            "Annotated forwarder private reference",
            ForwardedPrivateReference,
            Expect.Value("copy=1|original=2"),
            offDefault
        );

        // Mutation: none. Visibility: T = SnapshotShape (a base-class method calls Old(() => this)); the
        // runtime type SnapshotCircle is kept and its field keeps its value bitwise.
        yield return new Check(
            25,
            "Base-method runtime type",
            BaseMethodRuntimeType,
            Expect.Value("derived=True|radius=5"),
            offDefault
        );

        // Mutation: field writes on the nested object and on the object it refers to. Visibility: T's field
        // Inner only; HiddenNestInner's fields are hidden, so its copy keeps Value bitwise and shares Leaf.
        yield return new Check(
            26,
            "Nested unpreserved class",
            NestedUnpreserved,
            Expect.Value("inner=3|leaf=9"),
            offDefault
        );

        // The same with the nested type preserved by DynamicDependency(All): Leaf is copied too.
        yield return new Check(
            27,
            "Nested preserved class",
            NestedPreserved,
            Expect.Value("inner=3|leaf=4"),
            offDefault
        );

        // Mutation: indexer set on a List held by a base-class auto-property. Visibility: none for the
        // base's private backing field, so the copy shares the List: the change passes silently.
        yield return new Check(
            28,
            "Base auto-property hidden list indexer set",
            HiddenBaseListIndexerSet,
            Expect.Ok,
            Expect.Ok
        );

        // Mutation: Add. Visibility: DynamicDependency(All, typeof(PreservedLinesDerived)) covers the base's
        // backing field, and List<> is visible (check 3): the copy has its own List, so the count shows it.
        yield return new Check(29, "Preserved base auto-property list Add", PreservedBaseListAdd, post, Expect.Ok);

        // Mutation: indexer set. The List is copied, but its PreservedLine elements have no visible members
        // and their Equals says unequal: the comparison reports that it cannot compare them.
        yield return new Check(
            30,
            "Preserved base auto-property list indexer set",
            PreservedBaseListIndexerSet,
            Expect.Throws<InvalidOperationException>(
                NestedText(PreservedLineTypeName, "PreservedLinesDerived.Lines[]", "Lines")
            ),
            Expect.Ok
        );

        // Mutation: indexer set. Visibility: none for SortedList<,>: the copy shares its arrays, so the
        // change passes silently.
        yield return new Check(31, "Hidden SortedList indexer set", HiddenSortedListIndexerSet, Expect.Ok, Expect.Ok);

        // Mutation: Add beyond the copy's count. The copy's stale count shows it.
        yield return new Check(32, "Hidden SortedList Add", HiddenSortedListAdd, post, Expect.Ok);

        // Mutation: none (two separate graphs). Entries are compared through IDictionaryEnumerator (R7).
        yield return new Check(
            33,
            "Nested Dictionary unchanged",
            static () =>
                NoResult(static () =>
                    Contract.EnsureAssignable(
                        new StockSheet { Stock = new(StringComparer.Ordinal) { ["a"] = 1 } },
                        new StockSheet { Stock = new(StringComparer.Ordinal) { ["a"] = 1 } }
                    )
                ),
            Expect.Ok,
            Expect.Ok
        );

        // Mutation: a different value for the same key.
        yield return new Check(
            34,
            "Nested Dictionary value changed",
            static () =>
                NoResult(static () =>
                    Contract.EnsureAssignable(
                        new StockSheet { Stock = new(StringComparer.Ordinal) { ["a"] = 2 } },
                        new StockSheet { Stock = new(StringComparer.Ordinal) { ["a"] = 1 } }
                    )
                ),
            post,
            Expect.Ok
        );

#if !HIDDEN_DICTIONARY_ENTRY // -p:AotSmokeHiddenDictionaryEntry=true builds the variant in which Dictionary<,>.Entry is hidden.
        // Mutation: none. Visibility: DynamicDependency(All) on Dictionary<string, int>; entries are still
        // compared through IDictionaryEnumerator (R7), so no KeyValuePair fields are needed.
        yield return new Check(
            35,
            "Preserved Dictionary unchanged",
            PreservedDictionaryUnchanged,
            Expect.Ok,
            Expect.Ok
        );

        // Mutation: field write on a dictionary value. Visibility: DynamicDependency(All) on
        // Dictionary<string, CatalogItem> makes Dictionary<,>.Entry visible, so the values are copied.
        yield return new Check(
            36,
            "Dictionary value field write, Entry visible",
            CatalogValueChangedEntryVisible,
            post,
            Expect.Ok
        );
#else
        // The same with Dictionary<,>.Entry hidden (this build preserves no Dictionary): the copy shares
        // the values, so the change passes silently.
        yield return new Check(
            36,
            "Dictionary value field write, Entry hidden",
            CatalogValueChangedEntryHidden,
            Expect.Ok,
            Expect.Ok
        );
#endif

        // Mutation: Add on a List inside a struct array element (Array.GetValue/SetValue). Visibility:
        // DynamicDependency(All) on TagSet.
        yield return new Check(37, "Struct array element List Add", StructArrayListAdd, post, Expect.Ok);

        // Mutation: Add on a List in a readonly field of a struct nested in a struct field. Visibility:
        // DynamicDependency(All) on both structs.
        yield return new Check(
            38,
            "Nested readonly struct field",
            NestedReadonlyStructField,
            Expect.Value("copy=a"),
            offDefault
        );

        // Mutation: Add on a List inside a Nullable<LabelSet>[] element (Array.GetValue/SetValue on a
        // Nullable array). Visibility: DynamicDependency(All) on LabelSet and on LabelSet?.
        yield return new Check(
            39,
            "Nullable struct array element",
            NullableStructArray,
            Expect.Value("copy=null|a"),
            offDefault
        );

        // Mutation: none; the copy is dropped and collected. Visibility: T. The queue stays empty.
        yield return new Check(
            40,
            "Finalizable copy not finalized",
            FinalizableCopyNotFinalized,
            Expect.Value("finalized=0"),
            Expect.Value("finalized=0")
        );

        // Mutation: none. Visibility: T. A string in an object field is shared, not copied.
        yield return new Check(
            41,
            "String in object field same",
            static () =>
            {
                PayloadHolder original = new() { Payload = new string('p', 3) };
                PayloadHolder? copy = Contract.Old(() => original);
                return copy is null ? null : $"same={ReferenceEquals(copy.Payload, original.Payload)}";
            },
            Expect.Value("same=True"),
            offDefault
        );

        // Mutation: a different DateTime (hidden fields). R3: Equals decides; no visible field makes it unequal.
        yield return new Check(
            42,
            "R3 DateTime changed",
            static () =>
                NoResult(static () =>
                    Contract.EnsureAssignable(
                        new Appointment { At = new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc), Room = 1 },
                        new Appointment { At = new DateTime(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc), Room = 1 }
                    )
                ),
            post,
            Expect.Ok
        );

        // Mutation: a different int.
        yield return new Check(
            43,
            "R3 int changed",
            static () =>
                NoResult(static () =>
                    Contract.EnsureAssignable(
                        new Appointment { At = new DateTime(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc), Room = 2 },
                        new Appointment { At = new DateTime(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc), Room = 1 }
                    )
                ),
            post,
            Expect.Ok
        );

        // Mutation: the delegate's target replaced by another instance. R4: same method, different target.
        yield return new Check(
            44,
            "R4 same method different target",
            static () =>
                NoResult(static () =>
                    Contract.EnsureAssignable(
                        new GreetingHook { Hook = new Greeter("a").Greet },
                        new GreetingHook { Hook = new Greeter("b").Greet }
                    )
                ),
            Expect.Ok,
            Expect.Ok
        );

        // Mutation: the delegate replaced by an equal one (same target, same CoreLib-internal override,
        // ComparisonComparer<int>.Compare). Visibility: none; Native AOT still reads Method for this delegate,
        // and Delegate.Equals decides first. The null-Method / NotSupportedException branch is not reachable
        // with this fixture under Native AOT; unit tests cover it.
        yield return new Check(
            45,
            "Delegate Equals fast path (CoreLib override, Method readable)",
            DelegateEqualsFastPath,
            Expect.Value("method=readable|ensure=passed"),
            Expect.Value("method=readable|ensure=passed")
        );

        // Mutation: the target replaced by another instance of the same CoreLib-internal type (R4: same
        // method, different target). Visibility: none; Native AOT reads Method, so the methods are compared.
        // The null-Method / NotSupportedException branch is not reachable with this fixture under Native AOT;
        // unit tests cover it.
        yield return new Check(
            46,
            "Delegate same method, different target (CoreLib override)",
            DelegateSameMethodDifferentTarget,
            Expect.Value("method=readable|ensure=passed"),
            Expect.Value("method=readable|ensure=passed")
        );

        // Mutation: none. Visibility: none for the base's ImmutableArray backing field (bitwise, shared);
        // the inherited public property is visible through T.
        yield return new Check(
            47,
            "Hidden ImmutableArray backing field unchanged",
            HiddenImmutableArrayUnchanged,
            Expect.Ok,
            Expect.Ok
        );

        // Mutation: field write through the property setter (different content).
        yield return new Check(
            48,
            "Hidden ImmutableArray backing field replaced",
            HiddenImmutableArrayReplaced,
            post,
            Expect.Ok
        );

        // Mutation: none. Visibility: T. A System.Collections.Frozen set is shared by Old (namespace rule).
        yield return new Check(
            49,
            "Frozen set shared by Old",
            FrozenSharedByOld,
            Expect.Value("same=True"),
            offDefault
        );

        // Mutation: none (two frozen sets of equal content). A FrozenSet<T> is compared element by element,
        // in enumeration order: different instances with equal elements are equal.
        yield return new Check(
            50,
            "Frozen set compared by elements",
            static () =>
                NoResult(static () =>
                    Contract.EnsureAssignable(
                        new FrozenHolder { Codes = s_frozenCodes.ToFrozenSet(StringComparer.Ordinal) },
                        new FrozenHolder { Codes = s_frozenCodes.ToFrozenSet(StringComparer.Ordinal) }
                    )
                ),
            Expect.Ok,
            Expect.Ok
        );

        // Mutation: none. Visibility: T; FieldInfo.GetValue boxes the pointer field as a Pointer.
        yield return new Check(51, "Pointer field unchanged", PointerUnchanged, Expect.Ok, Expect.Ok);

        // Mutation: field write (another address).
        yield return new Check(52, "Pointer field changed", PointerChanged, post, Expect.Ok);

        // Mutation: none (two equal records). R8: the interface member holds a hidden record whose Equals
        // says equal.
        yield return new Check(
            53,
            "R8 interface member equal hidden record",
            static () =>
                NoResult(static () =>
                    Contract.EnsureAssignable(
                        new Parcel { Label = new ShippingLabel("x") },
                        new Parcel { Label = new ShippingLabel("x") }
                    )
                ),
            Expect.Ok,
            Expect.Ok
        );

        // Mutation: field writes on assignable members only. Visibility: T (User) for Old and EnsureAssignable.
        yield return new Check(
            54,
            "User.ChangeEmail pairing",
            static () => NoResult(static () => new User("ada@example.com", "Ada").ChangeEmail("ada@byron.example")),
            Expect.Ok,
            Expect.Ok
        );

        // Mutation: field write on _name, which is not assignable.
        yield return new Check(
            55,
            "User.ChangeEmailAndName pairing",
            static () =>
                NoResult(static () =>
                    new User("ada@example.com", "Ada").ChangeEmailAndName("ada@byron.example", "Ada Byron")
                ),
            post,
            Expect.Ok
        );

        // Mutation: none. Visibility: T (RingNode). A cycle is copied and compared.
        yield return new Check(56, "Cyclic pairing unchanged", CyclicUnchanged, Expect.Ok, Expect.Ok);

        // Mutation: field write on the second node of the cycle.
        yield return new Check(57, "Cyclic pairing changed", CyclicChanged, post, Expect.Ok);

        // ---- EnsureAssignable and its arguments (issues #47 and #52). Each check returns "none", "violation",
        // or the ParamName of an ArgumentNullException. FlatType is already the T of checks 4 to 6. ----

        // A null compared value is compared as a value: one null is a violation. With DBC_POST=off the
        // method returns without comparing.
        yield return new Check(
            58,
            "EnsureAssignable null expected",
            static () =>
                CallOutcomeOf(static () => Contract.EnsureAssignable(new FlatType { Name = "a", Count = 1 }, null!)),
            Expect.Value("violation"),
            Expect.Value("none")
        );
        yield return new Check(
            59,
            "EnsureAssignable null actual",
            static () =>
                CallOutcomeOf(static () => Contract.EnsureAssignable(null!, new FlatType { Name = "a", Count = 1 })),
            Expect.Value("violation"),
            Expect.Value("none")
        );

        // The pattern array is checked in every call. Passes before and after issue #47: a pin.
        yield return new Check(
            60,
            "EnsureAssignable null patterns",
            static () =>
                CallOutcomeOf(static () =>
                    Contract.EnsureAssignable(
                        new FlatType { Name = "a", Count = 1 },
                        new FlatType { Name = "a", Count = 1 },
                        null!
                    )
                ),
            Expect.Value("assignableFieldPatterns"),
            Expect.Value("assignableFieldPatterns")
        );

        // With a null compared value too, the exception names the pattern array in every call.
        yield return new Check(
            61,
            "EnsureAssignable null expected and patterns",
            static () =>
                CallOutcomeOf(static () =>
                    Contract.EnsureAssignable(new FlatType { Name = "a", Count = 1 }, null!, null!)
                ),
            Expect.Value("assignableFieldPatterns"),
            Expect.Value("assignableFieldPatterns")
        );

        // The pairing in a call made while another contract check is running: Old returns default there,
        // with postconditions on or off. "skipped" would mean that the condition of Require did not run:
        // preconditions are off in the environment, or an earlier check left the recursion guard set.
        // "guard-cleared" would mean that the pairing cleared the recursion guard that Require had set.
        yield return new Check(
            62,
            "User.ChangeEmail pairing inside Require",
            GuardedUserPairing,
            Expect.Value("ran"),
            Expect.Value("ran")
        );

        // Two nulls are equal.
        yield return new Check(
            63,
            "EnsureAssignable both null",
            static () => CallOutcomeOf(static () => Contract.EnsureAssignable<FlatType>(null!, null!)),
            Expect.Value("none"),
            Expect.Value("none")
        );

        // EnsureImmutableCollection checks its collection for null only when it checks the collection.
        yield return new Check(
            64,
            "EnsureImmutableCollection null collection",
            static () => CallOutcomeOf(static () => Contract.EnsureImmutableCollection<ImmutableList<string>>(null!)),
            Expect.Value("collection"),
            Expect.Value("none")
        );

        // ---- T is a string, a delegate type or a nullable value type: the two values are compared as a
        // whole, without the members of T (issue #52). ----
        yield return new Check(
            65,
            "EnsureAssignable strings that differ",
            static () => CallOutcomeOf(static () => Contract.EnsureAssignable("abc", "abd")),
            Expect.Value("violation"),
            Expect.Value("none")
        );
        yield return new Check(
            66,
            "EnsureAssignable equal int?",
            static () => CallOutcomeOf(static () => Contract.EnsureAssignable<int?>(1, 1)),
            Expect.Value("none"),
            Expect.Value("none")
        );
        yield return new Check(
            67,
            "EnsureAssignable different DateTime?",
            static () =>
                CallOutcomeOf(static () =>
                    Contract.EnsureAssignable<DateTime?>(
                        new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc),
                        new DateTime(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc)
                    )
                ),
            Expect.Value("violation"),
            Expect.Value("none")
        );
        yield return new Check(
            68,
            "EnsureAssignable equal DateTime?",
            static () =>
                CallOutcomeOf(static () =>
                    Contract.EnsureAssignable<DateTime?>(
                        new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc),
                        new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc)
                    )
                ),
            Expect.Value("none"),
            Expect.Value("none")
        );

        // "same-instance" would mean that the two delegates are one reference and the check proves nothing.
        yield return new Check(
            69,
            "EnsureAssignable delegates for one method",
            SameMethodDelegates,
            Expect.Value("none"),
            Expect.Value("none")
        );

        // Checks 70 and 72 record what Native AOT does with a nullable struct that holds a reference. No
        // attribute preserves ListHolder, and the annotation on T names the fields of Nullable<ListHolder>
        // only. Measured: its field is visible to reflection in this program all the same, so the results are
        // those of the JIT. If it were hidden, Equals would say "unequal" for two lists with equal content
        // (check 70: "violation"), and Old could not copy what it cannot see (check 72: "none").
        yield return new Check(
            70,
            "EnsureAssignable ListHolder? with equal lists",
            NullableStructsWithEqualLists,
            Expect.Value("none"),
            Expect.Value("none")
        );

        // The null rule comes before the rule "no members visible": Marker has none, and two Markers that
        // are not null throw InvalidOperationException under Native AOT.
        yield return new Check(
            71,
            "EnsureAssignable one null, T without members",
            static () => CallOutcomeOf(static () => Contract.EnsureAssignable(new Marker(), null!)),
            Expect.Value("violation"),
            Expect.Value("none")
        );
        yield return new Check(
            72,
            "Old and EnsureAssignable on a ListHolder? changed in place",
            NullableStructChangedInPlace,
            Expect.Value("violation"),
            Expect.Value("none")
        );

        // ---- One instance of a type that Old shares, and its state can change, passed as actual and as
        // expected: nothing is compared and the call says so (issue #54). ----
        Expect sameSharedInstance = Expect.Throws<InvalidOperationException>(
            "uContract.AotSmoke.CountingTokenSource",
            "the same instance",
            "System.Threading.CancellationTokenSource",
            "DBC_POST=off"
        );
        yield return new Check(
            73,
            "Old and EnsureAssignable on this in a class derived from CancellationTokenSource",
            SharedInstanceComparedWithOld,
            sameSharedInstance,
            Expect.Ok
        );

        // IDisposable declares no property, so under Native AOT T has no visible members: this case is
        // reported first.
        yield return new Check(
            74,
            "EnsureAssignable<IDisposable> one CancellationTokenSource-derived instance",
            SharedInstanceThroughAnInterface,
            sameSharedInstance,
            Expect.Ok
        );

        // ---- A FrozenSet<T> or a FrozenDictionary<TKey, TValue> that a member holds is compared element by
        // element, in enumeration order, and one passed as T is compared as a whole in the same way
        // (issue #49). ----

        // Mutation: one element replaced (two frozen sets of different content). Visibility: T.
        yield return new Check(
            75,
            "Frozen sets of different content",
            static () =>
                NoResult(static () =>
                    Contract.EnsureAssignable(
                        new FrozenHolder { Codes = s_frozenCodes.ToFrozenSet(StringComparer.Ordinal) },
                        new FrozenHolder { Codes = s_otherFrozenCodes.ToFrozenSet(StringComparer.Ordinal) }
                    )
                ),
            post,
            Expect.Ok
        );

        // Mutation: none (two frozen dictionaries of equal content). Visibility: T.
        yield return new Check(
            76,
            "Frozen dictionary compared by entries",
            FrozenDictionariesOfEqualContent,
            Expect.Ok,
            Expect.Ok
        );

        // Mutation: one element replaced (two frozen sets of different content). T is the FrozenSet<T> itself:
        // the two sets are compared as a whole, and the violation says so.
        yield return new Check(
            77,
            "EnsureAssignable<FrozenSet<string>> sets of different content",
            static () =>
                NoResult(static () =>
                    Contract.EnsureAssignable(
                        s_frozenCodes.ToFrozenSet(StringComparer.Ordinal),
                        s_otherFrozenCodes.ToFrozenSet(StringComparer.Ordinal)
                    )
                ),
            Expect.Throws<PostconditionViolationException>("is compared as a whole"),
            Expect.Ok
        );

        // Mutation: none (two frozen sets, each with its own element of equal Value). Visibility: T; none for
        // HiddenLeaf under Native AOT, where its Equals says unequal: the comparison reports that it cannot
        // compare the elements. Under the JIT the members of HiddenLeaf are visible and the elements are equal.
        yield return new Check(
            78,
            "Frozen set of elements with hidden members",
            static () =>
                NoResult(static () =>
                    Contract.EnsureAssignable(
                        new FrozenLeavesHolder { Leaves = new[] { new HiddenLeaf { Value = 1 } }.ToFrozenSet() },
                        new FrozenLeavesHolder { Leaves = new[] { new HiddenLeaf { Value = 1 } }.ToFrozenSet() }
                    )
                ),
            Expect.Throws<InvalidOperationException>("cannot compare", "uContract.AotSmoke.HiddenLeaf"),
            Expect.Ok
        );

        // The same two sets, with the FrozenSet<T> itself as T: the sets are compared as a whole, so the path
        // to the elements that cannot be compared starts at the set and names no member. Under the JIT the
        // members of HiddenLeaf are visible and the elements are equal.
        yield return new Check(
            79,
            "EnsureAssignable<FrozenSet<HiddenLeaf>> elements with hidden members",
            static () =>
                NoResult(static () =>
                    Contract.EnsureAssignable(
                        new[] { new HiddenLeaf { Value = 1 } }.ToFrozenSet(),
                        new[] { new HiddenLeaf { Value = 1 } }.ToFrozenSet()
                    )
                ),
            Expect.Throws<InvalidOperationException>(
                "cannot compare",
                "uContract.AotSmoke.HiddenLeaf",
                "reached through 'FrozenSet`1[]'"
            ),
            Expect.Ok
        );
    }

    private static object? FrozenDictionariesOfEqualContent()
    {
        static FrozenDictionary<string, int> Counts() =>
            new Dictionary<string, int>(StringComparer.Ordinal) { ["a"] = 1, ["b"] = 2 }.ToFrozenDictionary(
                StringComparer.Ordinal
            );

        return NoResult(static () =>
            Contract.EnsureAssignable(
                new FrozenDictionaryHolder { Counts = Counts() },
                new FrozenDictionaryHolder { Counts = Counts() }
            )
        );
    }

    private static object? SharedInstanceComparedWithOld()
    {
        using CountingTokenSource source = new();
        return NoResult(source.Use);
    }

    private static object? SharedInstanceThroughAnInterface()
    {
        using CountingTokenSource source = new();
        return NoResult(() => Contract.EnsureAssignable<IDisposable>(source, source));
    }

    private static string? OldList()
    {
        List<string> original = ["a"];
        List<string>? copy = Contract.Old(() => original);
        return copy is null ? null : string.Join('|', copy) + $"|sameInstance={ReferenceEquals(copy, original)}";
    }

    private static string? OldOnlyPrivateReference()
    {
        OldOnlyTally original = new();
        OldOnlyTally? copy = Contract.Old(() => original);
        original.Bump();
        return copy is null ? null : $"copy={copy.Count}|original={original.Count}";
    }

    private static T OldThrough<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields
        )]
            T
    >(Func<T> supplier) => Contract.Old(supplier);

    private static string? ForwardedPrivateReference()
    {
        ForwardedTally original = new();
        ForwardedTally? copy = OldThrough(() => original);
        original.Bump();
        return copy is null ? null : $"copy={copy.Count}|original={original.Count}";
    }

    private static string? BaseMethodRuntimeType()
    {
        SnapshotShape? copy = new SnapshotCircle { Radius = 5 }.Snapshot();
        return copy is null ? null : $"derived={copy is SnapshotCircle}|radius={(copy as SnapshotCircle)?.Radius}";
    }

    private static string? NestedUnpreserved()
    {
        HiddenNestOuter original = new()
        {
            Inner = new HiddenNestInner
            {
                Value = 3,
                Leaf = new HiddenLeaf { Value = 4 },
            },
        };
        HiddenNestOuter? copy = Contract.Old(() => original);
        original.Inner.Value = 8;
        original.Inner.Leaf.Value = 9;
        return copy is null ? null : $"inner={copy.Inner.Value}|leaf={copy.Inner.Leaf.Value}";
    }

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(PreservedNestInner))]
    private static string? NestedPreserved()
    {
        PreservedNestOuter original = new()
        {
            Inner = new PreservedNestInner
            {
                Value = 3,
                Leaf = new PreservedLeaf { Value = 4 },
            },
        };
        PreservedNestOuter? copy = Contract.Old(() => original);
        original.Inner.Value = 8;
        original.Inner.Leaf.Value = 9;
        return copy is null ? null : $"inner={copy.Inner.Value}|leaf={copy.Inner.Leaf.Value}";
    }

    private static object? HiddenBaseListIndexerSet()
    {
        HiddenLinesDerived original = new() { Lines = [new HiddenLine("a")] };
        HiddenLinesDerived copy = Contract.Old(() => original);
        original.Lines[0] = new HiddenLine("z");
        return NoResult(() => Contract.EnsureAssignable(original, copy));
    }

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(PreservedLinesDerived))]
    private static object? PreservedBaseListAdd()
    {
        PreservedLinesDerived original = new() { Lines = [new PreservedLine("a")] };
        PreservedLinesDerived copy = Contract.Old(() => original);
        original.Lines.Add(new PreservedLine("b"));
        return NoResult(() => Contract.EnsureAssignable(original, copy));
    }

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(PreservedLinesDerived))]
    private static object? PreservedBaseListIndexerSet()
    {
        PreservedLinesDerived original = new() { Lines = [new PreservedLine("a")] };
        PreservedLinesDerived copy = Contract.Old(() => original);
        original.Lines[0] = new PreservedLine("z");
        return NoResult(() => Contract.EnsureAssignable(original, copy));
    }

    private static object? HiddenSortedListIndexerSet()
    {
        ScoreBoard original = new();
        ScoreBoard copy = Contract.Old(() => original);
        original.Scores["a"] = 2;
        return NoResult(() => Contract.EnsureAssignable(original, copy));
    }

    private static object? HiddenSortedListAdd()
    {
        ScoreBoard original = new();
        ScoreBoard copy = Contract.Old(() => original);
        original.Scores.Add("b", 2);
        return NoResult(() => Contract.EnsureAssignable(original, copy));
    }

#if !HIDDEN_DICTIONARY_ENTRY
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Dictionary<string, int>))]
    private static object? PreservedDictionaryUnchanged()
    {
        PreservedStockSheet original = new() { Stock = new(StringComparer.Ordinal) { ["a"] = 1 } };
        PreservedStockSheet copy = Contract.Old(() => original);
        return NoResult(() => Contract.EnsureAssignable(original, copy));
    }

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Dictionary<string, CatalogItem>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(CatalogItem))]
    private static object? CatalogValueChangedEntryVisible() => CatalogValueChanged();
#else
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(CatalogItem))]
    private static object? CatalogValueChangedEntryHidden() => CatalogValueChanged();
#endif

    private static object? CatalogValueChanged()
    {
        Catalog original = new();
        original.Items.Add("a", new CatalogItem { Price = 1 });
        Catalog copy = Contract.Old(() => original);
        original.Items["a"].Price = 2;
        return NoResult(() => Contract.EnsureAssignable(original, copy));
    }

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(TagSet))]
    private static object? StructArrayListAdd()
    {
        TagShelf original = new() { Sets = [new TagSet { Tags = ["a"] }] };
        TagShelf copy = Contract.Old(() => original);
        original.Sets[0].Tags.Add("b");
        return NoResult(() => Contract.EnsureAssignable(original, copy));
    }

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(OuterTagBox))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ReadonlyTagBox))]
    private static string? NestedReadonlyStructField()
    {
        TagCabinet original = new() { Box = new OuterTagBox { Inner = new ReadonlyTagBox(["a"]) } };
        TagCabinet? copy = Contract.Old(() => original);
        original.Box.Inner.Tags.Add("b");
        return copy is null ? null : "copy=" + string.Join(',', copy.Box.Inner.Tags);
    }

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(LabelSet))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(LabelSet?))]
    private static string? NullableStructArray()
    {
        LabelRack original = new() { Slots = [null, new LabelSet { Labels = ["a"] }] };
        LabelRack? copy = Contract.Old(() => original);
        original.Slots[1]!.Value.Labels.Add("b");
        return copy is null
            ? null
            : "copy="
                + string.Join(
                    '|',
                    copy.Slots.Select(static slot => slot is { } set ? string.Join(',', set.Labels) : "null")
                );
    }

    private static string? FinalizableCopyNotFinalized()
    {
        FinalizableFixture original = new() { Id = 1 };
        CopyAndDrop(original);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        string result = $"finalized={FinalizableFixture.Finalized}";
        GC.KeepAlive(original);
        return result;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CopyAndDrop(FinalizableFixture original)
    {
        _ = Contract.Old(() => original);
    }

    private static string MethodProbe(Delegate hook)
    {
        try
        {
            return (MethodInfo?)hook.Method is null ? "null" : "readable";
        }
        catch (NotSupportedException)
        {
            return "throws";
        }
    }

    private static string EnsureOutcome(Action ensure)
    {
        try
        {
            ensure();
            return "passed";
        }
        catch (PostconditionViolationException)
        {
            return "violation";
        }
    }

    // "none", "violation", or the ParamName of an ArgumentNullException. Any other exception fails the check.
    private static string CallOutcomeOf(Action call)
    {
        try
        {
            call();
            return "none";
        }
        catch (PostconditionViolationException)
        {
            return "violation";
        }
        catch (ArgumentNullException ex)
        {
            return ex.ParamName ?? "unnamed";
        }
    }

    private static string SameMethodDelegates()
    {
        Runner runner = new();
        Action first = runner.Run;
        Action second = runner.Run;
        return ReferenceEquals(first, second)
            ? "same-instance"
            : CallOutcomeOf(() => Contract.EnsureAssignable(first, second));
    }

    private static string NullableStructsWithEqualLists()
    {
        ListHolder? first = new ListHolder { Items = [1] };
        ListHolder? second = new ListHolder { Items = [1] };
        return CallOutcomeOf(() => Contract.EnsureAssignable(first, second));
    }

    private static string NullableStructChangedInPlace()
    {
        List<int> items = [1];
        ListHolder? holder = new ListHolder { Items = items };
        ListHolder? old = Contract.Old(() => holder);
        items.Add(2);
        return CallOutcomeOf(() => Contract.EnsureAssignable(holder, old));
    }

    private static string GuardedUserPairing()
    {
        bool ran = false;
        bool nestedRan = false;
        Contract.Require(
            "smoke",
            () =>
            {
                new User("ada@example.com", "Ada").ChangeEmail("ada@byron.example");
                Contract.Require(
                    "smoke nested",
                    () =>
                    {
                        nestedRan = true;
                        return true;
                    }
                );
                ran = true;
                return true;
            }
        );
        return nestedRan ? "guard-cleared"
            : ran ? "ran"
            : "skipped";
    }

    private static string? DelegateEqualsFastPath()
    {
        Comparer<int> comparer = Comparer<int>.Create(static (x, y) => x.CompareTo(y));
        Func<int, int, int> actual = comparer.Compare;
        Func<int, int, int> expected = comparer.Compare;
        string ensure = EnsureOutcome(() =>
            Contract.EnsureAssignable(new CompareHook { Hook = actual }, new CompareHook { Hook = expected })
        );
        return $"method={MethodProbe(actual)}|ensure={ensure}";
    }

    private static string? DelegateSameMethodDifferentTarget()
    {
        Func<int, int, int> actual = Comparer<int>.Create(static (x, y) => x.CompareTo(y)).Compare;
        Func<int, int, int> expected = Comparer<int>.Create(static (x, y) => x.CompareTo(y)).Compare;
        string ensure = EnsureOutcome(() =>
            Contract.EnsureAssignable(new CompareHook { Hook = actual }, new CompareHook { Hook = expected })
        );
        return $"method={MethodProbe(actual)}|ensure={ensure}";
    }

    private static object? HiddenImmutableArrayUnchanged()
    {
        TaggedDerived original = new() { Tags = ["a"] };
        TaggedDerived copy = Contract.Old(() => original);
        return NoResult(() => Contract.EnsureAssignable(original, copy));
    }

    private static object? HiddenImmutableArrayReplaced()
    {
        TaggedDerived original = new() { Tags = ["a"] };
        TaggedDerived copy = Contract.Old(() => original);
        original.Tags = ["z"];
        return NoResult(() => Contract.EnsureAssignable(original, copy));
    }

    private static string? FrozenSharedByOld()
    {
        FrozenHolder original = new() { Codes = s_frozenCodes.ToFrozenSet(StringComparer.Ordinal) };
        FrozenHolder? copy = Contract.Old(() => original);
        return copy is null ? null : $"same={ReferenceEquals(copy.Codes, original.Codes)}";
    }

    private static unsafe object? PointerUnchanged()
    {
        PointerHolder original = new() { Address = (int*)16 };
        PointerHolder copy = Contract.Old(() => original);
        return NoResult(() => Contract.EnsureAssignable(original, copy));
    }

    private static unsafe object? PointerChanged()
    {
        PointerHolder original = new() { Address = (int*)16 };
        PointerHolder copy = Contract.Old(() => original);
        original.Address = (int*)32;
        return NoResult(() => Contract.EnsureAssignable(original, copy));
    }

    private static RingNode NewRing()
    {
        RingNode first = new() { Value = 1 };
        RingNode second = new() { Value = 2, Next = first };
        first.Next = second;
        return first;
    }

    private static object? CyclicUnchanged()
    {
        RingNode original = NewRing();
        RingNode copy = Contract.Old(() => original);
        return NoResult(() => Contract.EnsureAssignable(original, copy));
    }

    private static object? CyclicChanged()
    {
        RingNode original = NewRing();
        RingNode copy = Contract.Old(() => original);
        original.Next!.Value = 9;
        return NoResult(() => Contract.EnsureAssignable(original, copy));
    }

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(PreservedInner))]
    private static object? PreservedChanged() =>
        NoResult(static () =>
            Contract.EnsureAssignable(
                new PreservedOuter { Inner = new PreservedInner { Value = "b" } },
                new PreservedOuter { Inner = new PreservedInner { Value = "a" } }
            )
        );

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(PreservedInner))]
    private static object? PreservedUnchanged() =>
        NoResult(static () =>
            Contract.EnsureAssignable(
                new PreservedOuter { Inner = new PreservedInner { Value = "a" } },
                new PreservedOuter { Inner = new PreservedInner { Value = "a" } }
            )
        );
}
