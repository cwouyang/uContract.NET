using uContract.Exceptions;

namespace uContract.Tests;

/// <summary>
///     The promise of issue #45: <c>EnsureAssignable(x, Old(() => x))</c> passes when nothing changed and
///     reports exactly the changed member when something did, for common object shapes.
/// </summary>
public class OldPairingTests
{
    private const string NoPattern = "^$";

    private static string[] EntriesOf(PostconditionViolationException exception)
    {
        return exception
            .Description.Split('\n')
            .Where(line => line.StartsWith("  - ", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] Entries(params string[] names)
    {
        return names.Select(name => "  - " + name).Order(StringComparer.Ordinal).ToArray();
    }

    private static string[] ViolationsOf(Action postcondition)
    {
        return EntriesOf(Assert.Throws<PostconditionViolationException>(postcondition));
    }

    // ---- P1: the User class of docs/examples/USAGE_EXAMPLES.md (Field Assignment Validation) ----

    // Copied from docs/examples/USAGE_EXAMPLES.md (Field Assignment Validation). Differences, all required
    // by the analyzers or by the tests: the class is private and sealed; Contains takes StringComparison.Ordinal
    // (MA0001); the CreatedAt accessor reads _createdAt (CS0414); ChangeEmailAndName is added for the failing case.
    private sealed class User
    {
        private string _email;
        private string _name;
        private DateTime _createdAt;
        private DateTime _lastModified;
        private int _loginCount;

        public User(string email, string name)
        {
            Contract.RequireNotEmpty("Email", email);
            Contract.RequireNotEmpty("Name", name);

            _email = email;
            _name = name;
            _createdAt = DateTime.UtcNow;
            _lastModified = DateTime.UtcNow;
            _loginCount = 0;
        }

        public DateTime CreatedAt => _createdAt;

        public void ChangeEmail(string newEmail)
        {
            Contract.RequireNotEmpty("Email", newEmail);
            Contract.Require("Valid email format", () => newEmail.Contains('@', StringComparison.Ordinal));

            var oldState = Contract.Old(() => this);

            _email = newEmail;
            _lastModified = DateTime.UtcNow;

            // Only email and lastModified should change (not name, createdAt, loginCount)
            Contract.EnsureAssignable(this, oldState, nameof(_email), nameof(_lastModified));
        }

        public void ChangeName(string newName)
        {
            Contract.RequireNotEmpty("Name", newName);

            var oldState = Contract.Old(() => this);

            _name = newName;
            _lastModified = DateTime.UtcNow;

            // Only name and lastModified should change
            Contract.EnsureAssignable(this, oldState, nameof(_name), nameof(_lastModified));
        }

        public void RecordLogin()
        {
            var oldState = Contract.Old(() => this);

            _loginCount++;
            _lastModified = DateTime.UtcNow;

            // Only loginCount and lastModified should change
            Contract.EnsureAssignable(this, oldState, nameof(_loginCount), nameof(_lastModified));
        }

        // Not in the sample: a method that changes _name although only _email and _lastModified are assignable.
        public void ChangeEmailAndName(string newEmail, string newName)
        {
            var oldState = Contract.Old(() => this);

            _email = newEmail;
            _name = newName;
            _lastModified = DateTime.UtcNow;

            Contract.EnsureAssignable(this, oldState, nameof(_email), nameof(_lastModified));
        }
    }

    private static User NewUsedUser()
    {
        User user = new("ada@example.com", "Ada");
        user.ChangeName("Ada Byron");
        user.ChangeEmail("ada@byron.example");
        user.RecordLogin();
        return user;
    }

    [Fact]
    public void Pairing_UserChangeEmail_WhenOnlyAssignableMembersChange_DoesNotThrow()
    {
        User user = NewUsedUser();

        Exception? exception = Record.Exception(() => user.ChangeEmail("ada@lovelace.example"));

        Assert.Null(exception);
    }

    [Fact]
    public void Pairing_UserChangeEmailAndName_WhenNameIsNotAssignable_ReportsOnlyName()
    {
        User user = NewUsedUser();

        string[] entries = ViolationsOf(() => user.ChangeEmailAndName("ada@lovelace.example", "Ada Lovelace"));

        Assert.Equal(Entries("_name"), entries);
    }

    // ---- P2: private fields ----

    private sealed class Ledger
    {
        private int _count = 7;
#pragma warning disable CS0414 // Assigned but never read: the field exists to be compared.
        private string _label = "ledger";
#pragma warning restore CS0414
        private decimal _total = 12.5m;

        public void Bump(bool alsoRelabel)
        {
            var old = Contract.Old(() => this);
            _count++;
            if (alsoRelabel)
            {
                _label = "relabelled";
            }

            Contract.EnsureAssignable(this, old, nameof(_count));
        }

        public void Prepare()
        {
            _count = 70;
            _label = "prepared";
            _total = 99.5m;
        }

        public decimal Total => _total;
    }

    [Fact]
    public void Pairing_PrivateFields_WhenOnlyAssignableFieldChanges_DoesNotThrow()
    {
        Ledger ledger = new();
        ledger.Prepare();

        Exception? exception = Record.Exception(() => ledger.Bump(alsoRelabel: false));

        Assert.Null(exception);
        Assert.Equal(99.5m, ledger.Total);
    }

    [Fact]
    public void Pairing_PrivateFields_WhenAnotherFieldChanges_ReportsThatField()
    {
        Ledger ledger = new();
        ledger.Prepare();

        string[] entries = ViolationsOf(() => ledger.Bump(alsoRelabel: true));

        Assert.Equal(Entries("_label"), entries);
    }

    // ---- P3: a method on a base class that takes Old(() => this) ----

    private abstract class Account
    {
        private decimal _balance = 100m;

        private int _status = 1;

        protected abstract bool ChangesStatus { get; }

        public void Prepare()
        {
            _balance = 250m;
            _status = 4;
        }

        public void Run()
        {
            var old = Contract.Old(() => this);
            _balance += 5m;
            if (ChangesStatus)
            {
                _status++;
            }

            Contract.EnsureAssignable(this, old, nameof(_balance));
        }
    }

    private sealed class SavingsAccount(bool changeStatus) : Account
    {
#pragma warning disable CS0414 // Assigned but never read: the field exists to be compared.
        private decimal _rate = 0.02m;
#pragma warning restore CS0414

        protected override bool ChangesStatus => changeStatus;

        public void Reprice()
        {
            _rate = 0.05m;
        }
    }

    [Fact]
    public void Pairing_BaseClassMethod_WhenOnlyAssignableBaseFieldChanges_DoesNotThrow()
    {
        SavingsAccount account = new(changeStatus: false);
        account.Prepare();
        account.Reprice();

        Exception? exception = Record.Exception(account.Run);

        Assert.Null(exception);
    }

    [Fact]
    public void Pairing_BaseClassMethod_WhenAnotherBaseFieldChanges_ReportsThatField()
    {
        SavingsAccount account = new(changeStatus: true);
        account.Prepare();
        account.Reprice();

        string[] entries = ViolationsOf(account.Run);

        Assert.Equal(Entries("_status"), entries);
    }

    // ---- P4-P6, P8: polymorphic, interface, replaced subtype and object members ----

    private abstract class Animal
    {
        public string Name { get; set; } = "";
    }

    private sealed class Dog : Animal
    {
        public int Tricks { get; set; }
    }

    private sealed class Cat : Animal
    {
        public int Lives { get; set; }
    }

    private interface IShape
    {
        int Sides { get; }
    }

    private sealed class Polygon : IShape
    {
        public int Sides { get; set; }
    }

    private sealed class Holder
    {
        private Animal _pet = new Dog { Name = "Rex", Tricks = 2 };
        private IShape _shape = new Polygon { Sides = 4 };
        private object _payload = new Polygon { Sides = 3 };

        public void Retrain()
        {
            ((Dog)_pet).Tricks = 9;
        }

        public void Reshape()
        {
            ((Polygon)_shape).Sides = 5;
        }

        public void Repack()
        {
            ((Polygon)_payload).Sides = 6;
        }

        public void ReplacePet()
        {
            _pet = new Cat { Name = "Tom", Lives = 9 };
        }

        public void ReplacePayload()
        {
            _payload = "text";
        }

        public void Run(Action body)
        {
            var old = Contract.Old(() => this);
            body();
            Contract.EnsureAssignable(this, old, NoPattern);
        }
    }

    [Fact]
    public void Pairing_PolymorphicMember_WhenNothingChanged_DoesNotThrow()
    {
        Holder holder = new();

        Exception? exception = Record.Exception(() => holder.Run(() => { }));

        Assert.Null(exception);
    }

    [Fact]
    public void Pairing_PolymorphicMember_WhenDerivedStateChanges_ReportsTheMember()
    {
        Holder holder = new();

        string[] entries = ViolationsOf(() => holder.Run(holder.Retrain));

        Assert.Equal(Entries("_pet"), entries);
    }

    [Fact]
    public void Pairing_InterfaceMember_WhenNothingChanged_DoesNotThrow()
    {
        Holder holder = new();
        holder.Reshape();

        Exception? exception = Record.Exception(() => holder.Run(() => { }));

        Assert.Null(exception);
    }

    [Fact]
    public void Pairing_InterfaceMember_WhenImplementationStateChanges_ReportsTheMember()
    {
        Holder holder = new();

        string[] entries = ViolationsOf(() => holder.Run(holder.Reshape));

        Assert.Equal(Entries("_shape"), entries);
    }

    [Fact]
    public void Pairing_ReplacedSubtype_WhenNothingChanged_DoesNotThrow()
    {
        Holder holder = new();
        holder.ReplacePet();

        Exception? exception = Record.Exception(() => holder.Run(() => { }));

        Assert.Null(exception);
    }

    [Fact]
    public void Pairing_ReplacedSubtype_WhenMemberHoldsADifferentDerivedType_ReportsTheMember()
    {
        Holder holder = new();

        string[] entries = ViolationsOf(() => holder.Run(holder.ReplacePet));

        Assert.Equal(Entries("_pet"), entries);
    }

    [Fact]
    public void Pairing_ObjectMember_WhenNothingChanged_DoesNotThrow()
    {
        Holder holder = new();
        holder.Repack();

        Exception? exception = Record.Exception(() => holder.Run(() => { }));

        Assert.Null(exception);
    }

    [Fact]
    public void Pairing_ObjectMember_WhenContentChanges_ReportsTheMember()
    {
        Holder holder = new();

        string[] entries = ViolationsOf(() => holder.Run(holder.Repack));

        Assert.Equal(Entries("_payload"), entries);
    }

    [Fact]
    public void Pairing_ObjectMember_WhenRuntimeTypeChanges_ReportsTheMember()
    {
        Holder holder = new();

        string[] entries = ViolationsOf(() => holder.Run(holder.ReplacePayload));

        Assert.Equal(Entries("_payload"), entries);
    }

    // ---- P7: a lock field plus a List of records ----

    private sealed record Entry(string Name, int Quantity)
    {
        public int Quantity { get; set; } = Quantity;
    }

    private sealed class Basket
    {
        private readonly object _gate = new();
        private readonly List<Entry> _entries = [new("apple", 2), new("pear", 5)];

        public void Prepare()
        {
            _entries[0].Quantity = 40;
            _entries.Add(new Entry("fig", 1));
        }

        public void Run(Action<List<Entry>> body)
        {
            var old = Contract.Old(() => this);
            lock (_gate)
            {
                body(_entries);
            }

            Contract.EnsureAssignable(this, old, NoPattern);
        }
    }

    [Fact]
    public void Pairing_LockAndRecordList_WhenNothingChanged_DoesNotThrow()
    {
        Basket basket = new();
        basket.Prepare();

        Exception? exception = Record.Exception(() => basket.Run(_ => { }));

        Assert.Null(exception);
    }

    [Fact]
    public void Pairing_LockAndRecordList_WhenARecordChanges_ReportsTheList()
    {
        Basket basket = new();
        basket.Prepare();

        string[] entries = ViolationsOf(() => basket.Run(list => list[1].Quantity = 6));

        Assert.Equal(Entries("_entries"), entries);
    }

    // ---- P9: a dictionary member ----

    private sealed class Registry
    {
        private readonly Dictionary<string, Entry> _index = new(StringComparer.Ordinal)
        {
            ["a"] = new Entry("alpha", 1),
            ["b"] = new Entry("beta", 2),
        };

        public void Prepare()
        {
            _index["a"].Quantity = 10;
            _index["c"] = new Entry("gamma", 3);
        }

        public void Run(Action<Dictionary<string, Entry>> body)
        {
            var old = Contract.Old(() => this);
            body(_index);
            Contract.EnsureAssignable(this, old, NoPattern);
        }
    }

    [Fact]
    public void Pairing_DictionaryMember_WhenNothingChanged_DoesNotThrow()
    {
        Registry registry = new();
        registry.Prepare();

        Exception? exception = Record.Exception(() => registry.Run(_ => { }));

        Assert.Null(exception);
    }

    [Fact]
    public void Pairing_DictionaryMember_WhenAValueChanges_ReportsTheMember()
    {
        Registry registry = new();
        registry.Prepare();

        string[] entries = ViolationsOf(() => registry.Run(index => index["b"].Quantity = 20));

        Assert.Equal(Entries("_index"), entries);
    }

    // ---- P10: List<(Elem, Elem)> ----

    private sealed class PairList
    {
        private readonly List<(Entry Left, Entry Right)> _pairs = [(new Entry("l", 1), new Entry("r", 2))];

        public void Prepare()
        {
            _pairs[0].Left.Quantity = 7;
            _pairs.Add((new Entry("x", 8), new Entry("y", 9)));
        }

        public void Run(Action<List<(Entry Left, Entry Right)>> body)
        {
            var old = Contract.Old(() => this);
            body(_pairs);
            Contract.EnsureAssignable(this, old, NoPattern);
        }
    }

    [Fact]
    public void Pairing_TupleList_WhenNothingChanged_DoesNotThrow()
    {
        PairList pairs = new();
        pairs.Prepare();

        Exception? exception = Record.Exception(() => pairs.Run(_ => { }));

        Assert.Null(exception);
    }

    [Fact]
    public void Pairing_TupleList_WhenATupleElementChanges_ReportsTheMember()
    {
        PairList pairs = new();
        pairs.Prepare();

        string[] entries = ViolationsOf(() => pairs.Run(list => list[0].Right.Quantity = 3));

        Assert.Equal(Entries("_pairs"), entries);
    }

    // ---- P11, P12: shared resource fields ----

    private sealed class Throttled : IDisposable
    {
        private readonly SemaphoreSlim _slots = new(2);
        private int _served = 4;

        public void Prepare()
        {
            _served = 11;
            _slots.Wait();
        }

        public void Run(bool serve)
        {
            var old = Contract.Old(() => this);
            _slots.Wait();
            if (serve)
            {
                _served++;
            }

            Contract.EnsureAssignable(this, old, NoPattern);
        }

        public void Dispose()
        {
            _slots.Dispose();
        }
    }

    [Fact]
    public void Pairing_SemaphoreField_WhenOnlyTheSemaphoreIsUsed_DoesNotThrow()
    {
        using Throttled throttled = new();
        throttled.Prepare();

        Exception? exception = Record.Exception(() => throttled.Run(serve: false));

        Assert.Null(exception);
    }

    [Fact]
    public void Pairing_SemaphoreField_WhenAnotherFieldChanges_ReportsOnlyThatField()
    {
        using Throttled throttled = new();
        throttled.Prepare();

        string[] entries = ViolationsOf(() => throttled.Run(serve: true));

        Assert.Equal(Entries("_served"), entries);
    }

    private sealed class Journal : IDisposable
    {
        private readonly MemoryStream _sink = new([1, 2, 3]);
        private int _lines = 2;

        public void Prepare()
        {
            _lines = 8;
            _sink.WriteByte(9);
        }

        public void Run(bool addLine)
        {
            var old = Contract.Old(() => this);
            _sink.WriteByte(4);
            if (addLine)
            {
                _lines++;
            }

            Contract.EnsureAssignable(this, old, NoPattern);
        }

        public void Dispose()
        {
            _sink.Dispose();
        }
    }

    [Fact]
    public void Pairing_StreamField_WhenOnlyTheStreamIsWritten_DoesNotThrow()
    {
        using Journal journal = new();
        journal.Prepare();

        Exception? exception = Record.Exception(() => journal.Run(addLine: false));

        Assert.Null(exception);
    }

    [Fact]
    public void Pairing_StreamField_WhenAnotherFieldChanges_ReportsOnlyThatField()
    {
        using Journal journal = new();
        journal.Prepare();

        string[] entries = ViolationsOf(() => journal.Run(addLine: true));

        Assert.Equal(Entries("_lines"), entries);
    }

    // ---- P13: a cyclic parent/child pair, guarded by a fuse ----

    private sealed class FuseBlownException : Exception;

    private sealed class TreeNode
    {
        private const int FuseLimit = 200;

        // Counts reads of every Parent getter; static so the counter is not itself a compared member.
        private static int s_parentReads;

        private TreeNode? _parent;

        public int Id { get; set; }

        public List<TreeNode> Children { get; } = [];

        public TreeNode? Parent
        {
            get
            {
                if (++s_parentReads > FuseLimit)
                {
                    throw new FuseBlownException();
                }

                return _parent;
            }
        }

        public static void ResetFuse()
        {
            s_parentReads = 0;
        }

        public TreeNode AddChild(int id)
        {
            TreeNode child = new() { Id = id, _parent = this };
            Children.Add(child);
            return child;
        }
    }

    private static TreeNode NewTree()
    {
        TreeNode.ResetFuse();
        TreeNode root = new() { Id = 10 };
        root.AddChild(11).AddChild(12);
        return root;
    }

    [Fact]
    public void Pairing_CyclicParentChild_WhenNothingChanged_DoesNotThrow()
    {
        TreeNode root = NewTree();
        var old = Contract.Old(() => root);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(root, old, NoPattern));

        Assert.Null(exception);
    }

    [Fact]
    public void Pairing_CyclicParentChild_WhenRootIdChanges_ReportsOnlyId()
    {
        TreeNode root = NewTree();
        var old = Contract.Old(() => root);
        root.Id = 99;

        string[] entries = ViolationsOf(() => Contract.EnsureAssignable(root, old, NoPattern));

        Assert.Equal(Entries("Id", "<Id>k__BackingField"), entries);
    }
}
