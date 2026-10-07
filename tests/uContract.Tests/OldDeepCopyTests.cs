using System.Runtime.Serialization;

namespace uContract.Tests;

/// <summary>
///     Old under trimming and Native AOT, simulated through the <see cref="RuntimeFacts" /> seam.
///     The overrides are process-wide state, so this class shares the collection of the other
///     tests that mutate process-wide state.
/// </summary>
[Collection("EnvironmentVariables")]
public sealed class OldWithoutDynamicCodeTests
{
    [Fact]
    public void Old_WhenJsonReflectionDisabledAndSupplierReturnsNull_ReturnsDefault()
    {
        TestAccount? account = null;
        TestAccount? oldAccount = new();

        Exception? exception = RecordWithoutJsonReflection(() => oldAccount = Contract.Old(() => account));

        Assert.Null(exception);
        Assert.Null(oldAccount);
    }

    private static Exception? RecordWithoutJsonReflection(Action act)
    {
        try
        {
            RuntimeFacts.JsonReflectionEnabledOverride = false;
            return Record.Exception(act);
        }
        finally
        {
            RuntimeFacts.JsonReflectionEnabledOverride = null;
        }
    }
}

public class OldDeepCopyTests
{
    private const int Seed = 40;

    [Fact]
    public void Old_WhenPrivateFieldIsMutated_CopiesItsValue()
    {
        Descendant original = new(Seed);

        Descendant copy = Contract.Old(() => original);

        Assert.Equal(original.PrivateValue, copy.PrivateValue);
    }

    [Fact]
    public void Old_WhenReadonlyFieldIsSet_CopiesItsValue()
    {
        Descendant original = new(Seed);

        Descendant copy = Contract.Old(() => original);

        Assert.Equal(original.ReadonlyValue, copy.ReadonlyValue);
    }

    [Fact]
    public void Old_WhenPropertyHasPrivateSetter_CopiesItsValue()
    {
        Descendant original = new(Seed);

        Descendant copy = Contract.Old(() => original);

        Assert.Equal(original.PrivateSetValue, copy.PrivateSetValue);
    }

    [Fact]
    public void Old_WhenAutoPropertyIsGetOnly_CopiesItsValue()
    {
        Descendant original = new(Seed);

        Descendant copy = Contract.Old(() => original);

        Assert.Equal(original.GetOnlyValue, copy.GetOnlyValue);
    }

    [Fact]
    public void Old_WhenBaseClassPrivateFieldIsMutated_CopiesItsValue()
    {
        Descendant original = new(Seed);

        Descendant copy = Contract.Old(() => original);

        Assert.Equal(original.AncestorSecret, copy.AncestorSecret);
    }

    [Fact]
    public void Old_WhenDerivedFieldShadowsBaseFieldByName_CopiesBothValues()
    {
        Descendant original = new(Seed);

        Descendant copy = Contract.Old(() => original);

        Assert.Equal(original.DescendantShadowed, copy.DescendantShadowed);
        Assert.Equal(original.AncestorShadowed, copy.AncestorShadowed);
    }

    [Fact]
    public void Old_WhenGenericBaseClassDeclaresPrivateField_CopiesItsValue()
    {
        IntTally original = new();
        original.Add(Seed);

        IntTally copy = Contract.Old(() => original);

        Assert.Equal(Seed, copy.Total);
    }

    [Fact]
    public void Old_WhenOriginalPrivateAndNestedStateIsMutatedAfterwards_LeavesCopyUnchanged()
    {
        const int startingCoins = 100;
        const int spent = 30;
        Wallet original = new(startingCoins);
        Wallet copy = Contract.Old(() => original);

        original.Spend(spent);

        Assert.Equal(startingCoins, copy.Coins);
        Assert.Equal(0, copy.Payments);
    }

    [Fact]
    public void Old_WhenInstanceIsReachedTwice_CopiesItOnce()
    {
        Purse shared = new(1);
        PurseTwice original = new(shared, shared);

        PurseTwice copy = Contract.Old(() => original);

        Assert.Same(copy.First, copy.Second);
        Assert.NotSame(shared, copy.First);
    }

    [Fact]
    public void Old_WhenConstructorValidatesAndCounts_ReturnsCopyWithoutRunningConstructor()
    {
        UserLike original = new("alice@example.com", "Alice");
        int constructedBefore = UserLike.Constructed;

        UserLike copy = Contract.Old(() => original);

        Assert.Equal("alice@example.com", copy.ContactEmail);
        Assert.Equal(constructedBefore, UserLike.Constructed);
    }

    [Theory]
    [InlineData(typeof(ThrowingGetterNode))]
    [InlineData(typeof(ThrowingSetterNode))]
    [InlineData(typeof(OnDeserializedNode))]
    [InlineData(typeof(DeserializationCallbackNode))]
    [InlineData(typeof(ThrowingEqualsNode))]
    [InlineData(typeof(ThrowingHashCodeNode))]
    public void Old_WhenGraphContainsMemberThatThrowsWhenRun_CopiesWithoutRunningIt(Type nodeType)
    {
        NodePair original = new(Activator.CreateInstance(nodeType)!, Activator.CreateInstance(nodeType)!);

        Exception? exception = Record.Exception(() => Contract.Old(() => original));

        Assert.Null(exception);
    }

    [Fact]
    public void Old_WhenTypesAreCopiedForTheFirstTimeInParallel_CopiesEachCorrectly()
    {
        Func<int, ParallelNode>[] factories =
        [
            coins => new ParallelNode<byte>(coins),
            coins => new ParallelNode<short>(coins),
            coins => new ParallelNode<int>(coins),
            coins => new ParallelNode<long>(coins),
            coins => new ParallelNode<float>(coins),
            coins => new ParallelNode<double>(coins),
            coins => new ParallelNode<decimal>(coins),
            coins => new ParallelNode<char>(coins),
        ];
        ParallelNode[] originals = factories.Select((factory, index) => factory(Seed + index)).ToArray();
        ParallelNode[] copies = new ParallelNode[originals.Length];

        Parallel.For(0, originals.Length, index => copies[index] = Contract.Old(() => originals[index]));
        foreach (ParallelNode original in originals)
        {
            original.Spend(Seed);
        }

        Assert.Equal(Enumerable.Range(Seed, originals.Length), copies.Select(copy => copy.Coins));
    }

    private class Ancestor
    {
        private int _secret;
        private int _shadowed;

        public int AncestorSecret => _secret;
        public int AncestorShadowed => _shadowed;

        protected void SetAncestor(int secret, int shadowed)
        {
            _secret = secret;
            _shadowed = shadowed;
        }
    }

    private sealed class Descendant : Ancestor
    {
        private readonly int _readonlyValue;
        private readonly int _privateValue;
        private readonly int _shadowed;

        public Descendant() { }

        public Descendant(int seed)
        {
            _readonlyValue = seed + 1;
            GetOnlyValue = seed + 2;
            _privateValue = seed + 3;
            PrivateSetValue = seed + 4;
            SetAncestor(seed + 5, seed + 6);
            _shadowed = seed + 7;
        }

        public int ReadonlyValue => _readonlyValue;
        public int GetOnlyValue { get; }
        public int PrivateValue => _privateValue;
        public int PrivateSetValue { get; private set; }
        public int DescendantShadowed => _shadowed;
    }

    private class Tally<TItem>
    {
        private int _total;

        public int Total => _total;

        protected void AddToTotal(int amount)
        {
            _total += amount;
        }
    }

    private sealed class IntTally : Tally<int>
    {
        public void Add(int amount)
        {
            AddToTotal(amount);
        }
    }

    private sealed class Wallet(int coins)
    {
        private readonly Purse _purse = new(coins);
        private int _payments;

        public int Coins => _purse.Coins;
        public int Payments => _payments;

        public void Spend(int amount)
        {
            _purse.Take(amount);
            _payments++;
        }
    }

    private sealed class Purse(int coins)
    {
        private int _coins = coins;

        public int Coins => _coins;

        public void Take(int amount)
        {
            _coins -= amount;
        }
    }

    private sealed class PurseTwice(Purse first, Purse second)
    {
        private readonly Purse _first = first;
        private readonly Purse _second = second;

        public Purse First => _first;
        public Purse Second => _second;
    }

    private sealed class UserLike
    {
        private static int s_constructed;
        private readonly string _email;
        private readonly string _name;

        public UserLike(string email, string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(email);
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            if (!email.Contains('@', StringComparison.Ordinal))
            {
                throw new ArgumentException("An email address needs an @.", nameof(email));
            }

            _email = email;
            _name = name;
            Interlocked.Increment(ref s_constructed);
        }

        public static int Constructed => Volatile.Read(ref s_constructed);
        public string ContactEmail => _email;
        public string DisplayName => _name;
    }

    private sealed class NodePair(object first, object second)
    {
        public object First { get; } = first;
        public object Second { get; } = second;
    }

    private sealed class ThrowingGetterNode
    {
        private readonly string _message = "The getter ran.";

        public int Value => throw new InvalidOperationException(_message);
    }

    private sealed class ThrowingSetterNode
    {
        private readonly int _value = 1;

        public int Value
        {
            get => _value;
            set => throw new InvalidOperationException($"The setter ran with {value}.");
        }
    }

    private sealed class OnDeserializedNode
    {
        private readonly string _message = "The [OnDeserialized] method ran.";

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            throw new InvalidOperationException(_message);
        }
    }

    private sealed class DeserializationCallbackNode : IDeserializationCallback
    {
        public void OnDeserialization(object? sender)
        {
            throw new InvalidOperationException("OnDeserialization ran.");
        }
    }

    private sealed class ThrowingEqualsNode
    {
        public override bool Equals(object? obj) => throw new InvalidOperationException("Equals ran.");

        public override int GetHashCode() => 0;
    }

    private sealed class ThrowingHashCodeNode
    {
        public override bool Equals(object? obj) => ReferenceEquals(this, obj);

        public override int GetHashCode() => throw new InvalidOperationException("GetHashCode ran.");
    }

    private abstract class ParallelNode(int coins)
    {
        private readonly Purse _purse = new(coins);

        public int Coins => _purse.Coins;

        public void Spend(int amount)
        {
            _purse.Take(amount);
        }
    }

    private sealed class ParallelNode<TTag>(int coins) : ParallelNode(coins);
}
