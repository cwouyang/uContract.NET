using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
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
    private const string AddressTypeName = "uContract.AotSmoke.AddressRecord";
    private const string LineTypeName = "uContract.AotSmoke.LineElement";

    private static readonly string[] s_oldHelperText = ["Old<T>()", "DBC_POST=off"];

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
        Expect old = Expect.Throws<InvalidOperationException>(s_oldHelperText);
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
        yield return new Check(2, "Old int", static () => Contract.Old(static () => 42), old, Expect.DefaultValue);
        yield return new Check(
            3,
            "Old list",
            static () => Contract.Old(static () => new List<string> { "a" }),
            old,
            Expect.DefaultValue
        );
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
            Expect.Throws<InvalidOperationException>(NestedText(AddressTypeName, "AddressHolder.Address", "Address")),
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
