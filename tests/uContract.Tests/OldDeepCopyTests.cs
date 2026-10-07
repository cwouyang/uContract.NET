using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using uContract.Exceptions;

namespace uContract.Tests;

/// <summary>
///     Old without dynamic code, simulated through the <see cref="RuntimeFacts" /> seam.
///     The override is process-wide state, so this class shares the collection of the other
///     tests that mutate process-wide state.
/// </summary>
[Collection("EnvironmentVariables")]
public sealed class OldWithoutDynamicCodeTests
{
    [Fact]
    public void Old_WhenDynamicCodeIsUnsupported_ReturnsACopyOfTheValue()
    {
        CallbackHolder original = new();

        CallbackHolder copy = TestRuntime.WithoutDynamicCode(() => Contract.Old(() => original));

        Assert.NotSame(original, copy);
        Assert.Same(original.Callback, copy.Callback);
    }

    [Fact]
    public void Old_WhenDynamicCodeIsUnsupportedAndSupplierReturnsNull_ReturnsDefault()
    {
        CallbackHolder? original = null;

        CallbackHolder? copy = TestRuntime.WithoutDynamicCode(() => Contract.Old(() => original));

        Assert.Null(copy);
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

    [Fact]
    public void Old_WhenCalledInBaseClassMethodOnDerivedInstance_CopiesAsDerivedType()
    {
        const int derivedOnly = 7;
        DerivedKennel original = new(derivedOnly);

        Kennel copy = original.Snapshot();

        DerivedKennel derivedCopy = Assert.IsType<DerivedKennel>(copy);
        Assert.Equal(derivedOnly, derivedCopy.DerivedOnly);
    }

    [Fact]
    public void Old_WhenMemberIsDeclaredAsBaseClass_CopiesItAsRuntimeType()
    {
        Zoo original = new();

        Zoo copy = Contract.Old(() => original);

        Assert.IsType<Puppy>(copy.Pet);
        Assert.NotSame(original.Pet, copy.Pet);
    }

    [Fact]
    public void Old_WhenMemberIsDeclaredAsInterface_CopiesItAsRuntimeType()
    {
        Zoo original = new();

        Zoo copy = Contract.Old(() => original);

        Assert.IsType<Circle>(copy.Shape);
        Assert.NotSame(original.Shape, copy.Shape);
    }

    [Fact]
    public void Old_WhenMemberIsDeclaredAsAbstractClass_CopiesItAsRuntimeType()
    {
        Zoo original = new();

        Zoo copy = Contract.Old(() => original);

        Assert.IsType<ConcreteExhibit>(copy.Exhibit);
        Assert.NotSame(original.Exhibit, copy.Exhibit);
    }

    [Fact]
    public void Old_WhenObjectMemberHoldsBoxedInt_CopiesItAsInt()
    {
        Zoo original = new();

        Zoo copy = Contract.Old(() => original);

        Assert.IsType<int>(copy.BoxedCount);
        Assert.Equal(BoxedCount, copy.BoxedCount);
    }

    [Fact]
    public void Old_WhenObjectMemberHoldsBoxedStruct_LeavesOriginalBoxUntouched()
    {
        Zoo original = new();
        List<int> originalVisits = ((Tag)original.BoxedTag!).Visits;

        Zoo copy = Contract.Old(() => original);

        Assert.Same(originalVisits, ((Tag)original.BoxedTag!).Visits);
    }

    [Fact]
    public void Old_WhenObjectMemberHoldsBoxedStruct_CopiesItsReferenceTypedField()
    {
        Zoo original = new();

        Zoo copy = Contract.Old(() => original);

        List<int> copiedVisits = ((Tag)copy.BoxedTag!).Visits;
        Assert.NotSame(((Tag)original.BoxedTag!).Visits, copiedVisits);
        Assert.Equal(((Tag)original.BoxedTag!).Visits, copiedVisits);
    }

    [Fact]
    public void Old_WhenNullableAndObjectMembersAreNull_KeepsThemNull()
    {
        Zoo original = new();

        Zoo copy = Contract.Old(() => original);

        Assert.Null(copy.MissingCount);
        Assert.Null(copy.Nothing);
    }

    [Fact]
    public void Old_WhenValueIsRecordWithInitProperties_CopiesContentAndReferences()
    {
        PersonRecord original = new() { Name = "Alice", Home = new Address("Main Street") };

        PersonRecord copy = Contract.Old(() => original);

        Assert.Equal(original.Name, copy.Name);
        Assert.Equal(original.Home.Street, copy.Home.Street);
        Assert.NotSame(original.Home, copy.Home);
    }

    [Fact]
    public void Old_WhenValueIsAnonymousType_CopiesContentAndReferences()
    {
        var original = new { Name = "Alice", Home = new Address("Main Street") };

        var copy = Contract.Old(() => original);

        Assert.Equal(original.Name, copy.Name);
        Assert.Equal(original.Home.Street, copy.Home.Street);
        Assert.NotSame(original.Home, copy.Home);
    }

    [Fact]
    public void Old_WhenValueIsTuple_CopiesContentAndReferences()
    {
        (Address Home, int Rooms) original = (new Address("Main Street"), 3);

        (Address Home, int Rooms) copy = Contract.Old(() => original);

        Assert.Equal(original.Rooms, copy.Rooms);
        Assert.Equal(original.Home.Street, copy.Home.Street);
        Assert.NotSame(original.Home, copy.Home);
    }

    [Fact]
    public void EnsureAssignable_WhenNothingChangedAfterOld_DoesNotThrow()
    {
        Zoo zoo = new();
        Zoo oldZoo = Contract.Old(() => zoo);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(zoo, oldZoo));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenFieldInsideDeclaredBaseMemberChanged_ListsThatMember()
    {
        Zoo zoo = new();
        Zoo oldZoo = Contract.Old(() => zoo);
        ((Puppy)zoo.Pet).Nickname = "Rex";

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(zoo, oldZoo)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:"
                + NL
                + "  - Pet"
                + NL
                + "  - <Pet>k__BackingField",
            exception.Message.Replace("Postcondition violated: ", "", StringComparison.Ordinal)
        );
    }

    [Theory]
    [MemberData(nameof(SharedTypesTests.ListedTypeInstances), MemberType = typeof(SharedTypesTests))]
    public void Old_WhenFieldHoldsAListedType_SharesItWithTheOriginal(string listedType, Func<object> createInstance)
    {
        object instance = createInstance();
        try
        {
            Holder<object> original = new(instance);

            Holder<object> copy = Contract.Old(() => original);

            Assert.True(ReferenceEquals(original.Value, copy.Value), $"{listedType}: {instance.GetType()}");
        }
        finally
        {
            (instance as IDisposable)?.Dispose();
        }
    }

    [Theory]
    [MemberData(nameof(SharedTypesTests.CategoryInstances), MemberType = typeof(SharedTypesTests))]
    public void Old_WhenFieldHoldsASharedCategoryType_SharesItWithTheOriginal(
        string category,
        Func<object> createInstance
    )
    {
        object instance = createInstance();
        try
        {
            Holder<object> original = new(instance);

            Holder<object> copy = Contract.Old(() => original);

            Assert.True(ReferenceEquals(original.Value, copy.Value), $"{category}: {instance.GetType()}");
        }
        finally
        {
            (instance as IDisposable)?.Dispose();
        }
    }

    [Fact]
    public void Old_WhenFieldHoldsADelegate_SharesItWithTheOriginal()
    {
        CallbackHolder original = new();

        CallbackHolder copy = Contract.Old(() => original);

        Assert.Same(original.Callback, copy.Callback);
    }

    [Fact]
    public void Old_WhenFieldHoldsAnUncreatedLazy_SharesItWithoutRunningTheFactory()
    {
        int factoryCalls = 0;
        Holder<Lazy<int>> original = new(
            new Lazy<int>(() =>
            {
                factoryCalls++;
                return Seed;
            })
        );

        Holder<Lazy<int>> copy = Contract.Old(() => original);

        Assert.Same(original.Value, copy.Value);
        Assert.Equal(0, factoryCalls);
    }

    [Fact]
    public void Old_WhenValueIsACancellationTokenSource_ReturnsTheSameInstance()
    {
        using CancellationTokenSource original = new();

        CancellationTokenSource copy = Contract.Old(() => original);

        Assert.Same(original, copy);
    }

    [Fact]
    public void Old_WhenFieldHoldsAMemoryStream_SharesItWithTheOriginal()
    {
        using MemoryStream stream = new();
        Holder<MemoryStream> original = new(stream);

        Holder<MemoryStream> copy = Contract.Old(() => original);

        Assert.Same(original.Value, copy.Value);
    }

    [Fact]
    public void Old_WhenFieldHoldsAMemoryStreamSubclass_SharesItWithTheOriginal()
    {
        using RecordingStream stream = new();
        Holder<MemoryStream> original = new(stream);

        Holder<MemoryStream> copy = Contract.Old(() => original);

        Assert.Same(original.Value, copy.Value);
    }

    [Fact]
    public void Old_WhenSortedSetUsesAStringComparer_SharesTheComparerWithTheOriginal()
    {
        Holder<SortedSet<string>> original = new(new SortedSet<string>(StringComparer.Ordinal) { "a", "b" });

        Holder<SortedSet<string>> copy = Contract.Old(() => original);

        Assert.Same(original.Value.Comparer, copy.Value.Comparer);
    }

    [Fact]
    public void Old_WhenFieldHoldsTheDefaultStringEqualityComparer_SharesItWithTheOriginal()
    {
#pragma warning disable MA0024 // The row is about the default comparer itself.
        Holder<IEqualityComparer<string>> original = new(EqualityComparer<string>.Default);
#pragma warning restore MA0024

        Holder<IEqualityComparer<string>> copy = Contract.Old(() => original);

        Assert.Same(original.Value, copy.Value);
    }

    [Fact]
    public void Old_WhenFieldHoldsAPlainObject_CopiesItAsANewObject()
    {
        Holder<object> original = new(new object());

        Holder<object> copy = Contract.Old(() => original);

        Assert.NotSame(original.Value, copy.Value);
        Assert.IsType<object>(copy.Value);
    }

    [Fact]
    public void EnsureAssignable_WhenSharedResourcesAreUnchangedAfterOld_DoesNotThrow()
    {
        using CancellationTokenSource source = new();
        using Component component = new();
        FrozenSet<string> names = new List<string> { "a", "b" }.ToFrozenSet(StringComparer.Ordinal);
        ResourceBundle bundle = new(component, source, names);
        ResourceBundle oldBundle = Contract.Old(() => bundle);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(bundle, oldBundle));

        Assert.Null(exception);
    }

    [Fact]
    public void Old_WhenObjectRefersToItself_ReproducesTheCycleInTheCopy()
    {
        Loop original = new();
        original.Self = original;

        Loop copy = Contract.Old(() => original);

        Assert.Same(copy, copy.Self);
        Assert.NotSame(original, copy);
    }

    [Fact]
    public void Old_WhenParentAndChildReferToEachOther_ReproducesTheCycleInTheCopy()
    {
        Parent original = new();
        original.Child = new Child { Parent = original };

        Parent copy = Contract.Old(() => original);

        Assert.Same(copy, copy.Child!.Parent);
        Assert.NotSame(original.Child, copy.Child);
    }

    [Fact]
    public void Old_WhenTwoDistinctRecordsAreEqual_KeepsThemDistinct()
    {
        Pair<Twin> original = new(new Twin(Seed), new Twin(Seed));

        Pair<Twin> copy = Contract.Old(() => original);

        Assert.NotSame(copy.First, copy.Second);
    }

    [Fact]
    public void Old_WhenChainIsVeryDeep_CopiesItOnASmallStack()
    {
        Node original = Node.Chain(VeryDeepChainLength);
        Node? copy = null;

        SmallStack.OnSmallStack(() => copy = Contract.Old(() => original), maxStackSize: SmallStackSize);

        // Walked here, not by Assert.Equal, so that no assertion formats the 50 000-node graph.
        int mismatches = 0;
        int visited = 0;
        for (Node? from = original, to = copy; from is not null; from = from.Next, to = to?.Next)
        {
            visited++;
            if (to is null || ReferenceEquals(from, to) || from.Id != to.Id)
            {
                mismatches++;
            }
        }

        Assert.Equal(VeryDeepChainLength, visited);
        Assert.Equal(0, mismatches);
    }

    [Fact]
    public void Old_WhenRootIsInItsOwnList_ReproducesTheCycleInTheCopy()
    {
        Basket original = new();
        original.Items.Add(original);

        Basket copy = Contract.Old(() => original);

        Assert.Same(copy, copy.Items[0]);
        Assert.NotSame(original.Items, copy.Items);
    }

    [Fact]
    public void Old_WhenArrayHoldsObjectsNullsAndStrings_CopiesObjectsAndKeepsNullsAndStrings()
    {
        Holder<object?[]> original = new([new Address("Main Street"), null, "Alice"]);

        Holder<object?[]> copy = Contract.Old(() => original);

        Address copiedHome = Assert.IsType<Address>(copy.Value[0]);
        Assert.NotSame(original.Value[0], copiedHome);
        Assert.Equal("Main Street", copiedHome.Street);
        Assert.Null(copy.Value[1]);
        Assert.Same(original.Value[2], copy.Value[2]);
    }

    [Fact]
    public void Old_WhenArrayHasNonZeroLowerBounds_KeepsItsShapeAndCopiesItsElements()
    {
        int[] lengths = [2, 3];
        int[] lowerBounds = [1, 5];
#pragma warning disable IL3050 // Test fixture only: the library itself never creates arrays this way.
        Array grid = Array.CreateInstance(typeof(Address), lengths, lowerBounds);
#pragma warning restore IL3050
        for (int row = 1; row < 3; row++)
        {
            for (int column = 5; column < 8; column++)
            {
                grid.SetValue(new Address($"{row},{column}"), row, column);
            }
        }

        Holder<Array> original = new(grid);

        Holder<Array> copy = Contract.Old(() => original);

        Assert.IsType(grid.GetType(), copy.Value);
        Assert.Equal(lowerBounds, new[] { copy.Value.GetLowerBound(0), copy.Value.GetLowerBound(1) });
        Assert.Equal(lengths, new[] { copy.Value.GetLength(0), copy.Value.GetLength(1) });
        Address copiedCorner = Assert.IsType<Address>(copy.Value.GetValue(2, 7));
        Assert.NotSame(grid.GetValue(2, 7), copiedCorner);
        Assert.Equal("2,7", copiedCorner.Street);
    }

    [Fact]
    public void Old_WhenArrayIsJagged_CopiesItsInnerArrays()
    {
        Holder<Address[][]> original = new([
            [new Address("a")],
            [new Address("b"), new Address("c")],
        ]);

        Holder<Address[][]> copy = Contract.Old(() => original);

        Assert.NotSame(original.Value[1], copy.Value[1]);
        string[] expectedStreets = ["b", "c"];
        Assert.Equal(expectedStreets, copy.Value[1].Select(home => home.Street), StringComparer.Ordinal);
    }

    [Fact]
    public void Old_WhenArrayMemberHoldsDerivedElementArray_CopiesItAsTheRuntimeArrayType()
    {
        Holder<Animal[]> original = new(new Puppy[] { new() });

        Holder<Animal[]> copy = Contract.Old(() => original);

        Puppy[] copiedPuppies = Assert.IsType<Puppy[]>(copy.Value);
        Assert.NotSame(original.Value[0], copiedPuppies[0]);
    }

    [Fact]
    public void Old_WhenArrayHoldsStructsWithReferences_CopiesEachReference()
    {
        Holder<Slot[]> original = new([new Slot(new Address("a")), new Slot(new Address("b"))]);

        Holder<Slot[]> copy = Contract.Old(() => original);

        Assert.NotSame(original.Value[0].Home, copy.Value[0].Home);
        Assert.NotSame(original.Value[1].Home, copy.Value[1].Home);
        string[] expectedStreets = ["a", "b"];
        Assert.Equal(expectedStreets, copy.Value.Select(slot => slot.Home.Street), StringComparer.Ordinal);
    }

    [Fact]
    public void Old_WhenArrayHoldsOnlyBytes_CopiesItWithoutVisitingItsElements()
    {
        Holder<byte[]> original = new(new byte[LargeByteCount]);
        original.Value[^1] = 1;
        Stopwatch watch = Stopwatch.StartNew();

        Holder<byte[]> copy = Contract.Old(() => original);

        watch.Stop();
        Assert.NotSame(original.Value, copy.Value);
        Assert.Equal(1, copy.Value[^1]);
        Assert.True(watch.Elapsed < LargeByteCopyBound, $"Copying took {watch.Elapsed.TotalMilliseconds} ms.");
    }

    [Fact]
    public void Old_WhenDictionaryHasStringKeys_LooksUpCopiedValues()
    {
        Holder<Dictionary<string, Elem>> original = new(
            new Dictionary<string, Elem>(StringComparer.Ordinal) { ["one"] = new Elem { Value = 1 } }
        );

        Holder<Dictionary<string, Elem>> copy = Contract.Old(() => original);

        Assert.Equal(1, copy.Value["one"].Value);
        Assert.NotSame(original.Value["one"], copy.Value["one"]);
    }

    [Fact]
    public void Old_WhenHashSetIgnoresCase_FindsAnElementInAnotherCase()
    {
        Holder<HashSet<string>> original = new(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "a" });

        Holder<HashSet<string>> copy = Contract.Old(() => original);

        Assert.Contains("A", copy.Value);
        Assert.NotSame(original.Value, copy.Value);
    }

    [Fact]
    public void Old_WhenDictionaryHasRecordKeys_FindsAValueByAnEqualKey()
    {
        Holder<Dictionary<RecordKey, Elem>> original = new(
            new Dictionary<RecordKey, Elem> { [new RecordKey("one")] = new Elem { Value = 1 } }
        );

        Holder<Dictionary<RecordKey, Elem>> copy = Contract.Old(() => original);

        Assert.Equal(1, copy.Value[new RecordKey("one")].Value);
        Assert.NotSame(original.Value, copy.Value);
        Assert.NotSame(original.Value[new RecordKey("one")], copy.Value[new RecordKey("one")]);
    }

    // Accepted limitation: a key without a value GetHashCode keeps its original's identity hash in the
    // buckets, but the copied key gets a new one, so every lookup misses. Pinned so that a change is noticed.
    [Fact]
    public void Old_WhenDictionaryKeyHasIdentityHash_EnumeratesButMissesLookups()
    {
        Holder<Dictionary<KeyWithoutOverride, int>> original = new(
            new Dictionary<KeyWithoutOverride, int> { [new KeyWithoutOverride(1)] = 1, [new KeyWithoutOverride(2)] = 2 }
        );

        Holder<Dictionary<KeyWithoutOverride, int>> copy = Contract.Old(() => original);

        Assert.Equal([1, 2], copy.Value.Keys.Select(key => key.Id).Order());
        Assert.False(copy.Value.ContainsKey(copy.Value.Keys.First()));
    }

    [Fact]
    public void EnsureAssignable_WhenListElementsAreUnchangedAfterOld_DoesNotThrow()
    {
        Inventory inventory = new();
        Inventory oldInventory = Contract.Old(() => inventory);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(inventory, oldInventory));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenFieldOfListElementChangedAfterOld_ListsThatMember()
    {
        Inventory inventory = new();
        Inventory oldInventory = Contract.Old(() => inventory);
        inventory.Items[0].Value = Seed;

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(inventory, oldInventory)
        );

        Assert.Equal(
            "Fields were modified that are not marked as assignable:"
                + NL
                + "  - Items"
                + NL
                + "  - <Items>k__BackingField",
            exception.Message.Replace("Postcondition violated: ", "", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void Old_WhenStructMemberHoldsAPrivateList_CopiesTheList()
    {
        Holder<Ledger> original = new(new Ledger([1, 2]));

        Holder<Ledger> copy = Contract.Old(() => original);

        Assert.NotSame(original.Value.Entries, copy.Value.Entries);
        Assert.Equal(original.Value.Entries, copy.Value.Entries);
    }

    [Fact]
    public void Old_WhenStructInsideAStructHoldsAList_CopiesTheList()
    {
        Holder<Folder> original = new(new Folder(new Ledger([1, 2])));

        Holder<Folder> copy = Contract.Old(() => original);

        Assert.NotSame(original.Value.Ledger.Entries, copy.Value.Ledger.Entries);
        Assert.Equal(original.Value.Ledger.Entries, copy.Value.Ledger.Entries);
    }

    [Fact]
    public void Old_WhenArrayElementNestsAStructHoldingAList_CopiesTheList()
    {
        Holder<Folder[]> original = new([new Folder(new Ledger([1, 2]))]);

        Holder<Folder[]> copy = Contract.Old(() => original);

        Assert.NotSame(original.Value[0].Ledger.Entries, copy.Value[0].Ledger.Entries);
        Assert.Equal(original.Value[0].Ledger.Entries, copy.Value[0].Ledger.Entries);
    }

    [Fact]
    public void Old_WhenBoxNestsAStructHoldingAList_CopiesTheList()
    {
        Holder<object> original = new(new Folder(new Ledger([1, 2])));

        Holder<object> copy = Contract.Old(() => original);

        List<int> originalEntries = ((Folder)original.Value).Ledger.Entries;
        List<int> copiedEntries = ((Folder)copy.Value).Ledger.Entries;
        Assert.NotSame(originalEntries, copiedEntries);
        Assert.Equal(originalEntries, copiedEntries);
    }

    [Fact]
    public void Old_WhenNullableStructMemberHoldsAList_CopiesTheList()
    {
        Holder<Ledger?> original = new(new Ledger([1, 2]));

        Holder<Ledger?> copy = Contract.Old(() => original);

        Assert.NotSame(original.Value!.Value.Entries, copy.Value!.Value.Entries);
        Assert.Equal(original.Value.Value.Entries, copy.Value.Value.Entries);
    }

    [Fact]
    public void Old_WhenTypeHasFinalizer_NeverRunsTheCopysFinalizer()
    {
        FinalizableToken control = new(Guid.NewGuid());
        FinalizableToken original = new(Guid.NewGuid());
        DropControl(control.Token);
        DropCopyOf(original);

        CollectGarbage();

        Assert.Contains(control.Token, FinalizableToken.Finalized);
        Assert.DoesNotContain(original.Token, FinalizableToken.Finalized);
        GC.KeepAlive(original);
    }

    [Fact]
    public void Old_WhenFinalizableObjectIsNestedInGraph_NeverRunsTheNestedCopysFinalizer()
    {
        Guid controlToken = Guid.NewGuid();
        Holder<FinalizableToken> original = new(new FinalizableToken(Guid.NewGuid()));
        DropControl(controlToken);
        DropCopyOf(original);

        CollectGarbage();

        Assert.Contains(controlToken, FinalizableToken.Finalized);
        Assert.DoesNotContain(original.Value.Token, FinalizableToken.Finalized);
        GC.KeepAlive(original);
    }

    [Fact]
    public void Old_WhenTypeDeclaresStaticReferenceField_LeavesTheStaticUntouched()
    {
        object before = StaticOwner.Shared;
        StaticOwner original = new();

        _ = Contract.Old(() => original);

        Assert.Same(before, StaticOwner.Shared);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DropControl(Guid token) => _ = new FinalizableToken(token);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DropCopyOf<T>(T original) => _ = Contract.Old(() => original);

    private static void CollectGarbage()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
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

    private const int BoxedCount = 5;
    private static readonly string NL = char.ConvertFromUtf32(10);

    private class Kennel
    {
        public Kennel Snapshot()
        {
            return Contract.Old(() => this);
        }
    }

    private sealed class DerivedKennel(int derivedOnly) : Kennel
    {
        private readonly int _derivedOnly = derivedOnly;

        public int DerivedOnly => _derivedOnly;
    }

    private class Animal
    {
        public string Name { get; set; } = "Animal";
    }

    private sealed class Puppy : Animal
    {
        public string Nickname { get; set; } = "Pup";
    }

    private interface IShape;

    private sealed class Circle : IShape
    {
        public int Radius { get; set; } = 2;
    }

    private abstract class Exhibit;

    private sealed class ConcreteExhibit : Exhibit
    {
        public int Visitors { get; set; } = 3;
    }

    private struct Tag
    {
        public List<int> Visits { get; set; }
    }

    private sealed class Zoo
    {
        public Animal Pet { get; set; } = new Puppy();
        public IShape Shape { get; set; } = new Circle();
        public Exhibit Exhibit { get; set; } = new ConcreteExhibit();
        public object? BoxedCount { get; set; } = OldDeepCopyTests.BoxedCount;
        public object? BoxedTag { get; set; } = new Tag { Visits = [1, 2] };
        public int? MissingCount { get; set; }
        public object? Nothing { get; set; }
    }

    private sealed class Address(string street)
    {
        public string Street { get; } = street;
    }

    private sealed record PersonRecord
    {
        public required string Name { get; init; }
        public required Address Home { get; init; }
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

    private sealed class Holder<T>(T value)
    {
        public T Value { get; } = value;
    }

    private sealed class RecordingStream : MemoryStream;

    private sealed class Loop
    {
        public Loop? Self { get; set; }
    }

    private sealed class Parent
    {
        public Child? Child { get; set; }
    }

    private sealed class Child
    {
        public Parent? Parent { get; set; }
    }

    private sealed record Twin(int Id);

    private sealed class Pair<T>(T first, T second)
    {
        public T First { get; } = first;
        public T Second { get; } = second;
    }

    // A copy that visited every element would take seconds; a bitwise clone takes milliseconds.
    private const int LargeByteCount = 50_000_000;
    private static readonly TimeSpan LargeByteCopyBound = TimeSpan.FromSeconds(1);

    private sealed class Basket
    {
        public List<Basket> Items { get; } = [];
    }

    private readonly struct Slot(Address home)
    {
        public Address Home { get; } = home;
    }

    private readonly struct Ledger(List<int> entries)
    {
        private readonly List<int> _entries = entries;

        public List<int> Entries => _entries;
    }

    private readonly struct Folder(Ledger ledger)
    {
        public Ledger Ledger { get; } = ledger;
    }

    private sealed class Elem
    {
        public int Value { get; set; }
    }

    private sealed record RecordKey(string Name);

    private sealed class KeyWithoutOverride(int id)
    {
        public int Id { get; } = id;
    }

    private sealed class Inventory
    {
        public List<Elem> Items { get; } = [new Elem { Value = 1 }, new Elem { Value = 2 }];
    }

    // Deep enough to overflow a small stack if the copy recursed once per node.
    private const int VeryDeepChainLength = 50_000;
    private const int SmallStackSize = 256 * 1024;

    private sealed class Node
    {
        public int Id { get; set; }
        public Node? Next { get; set; }

        // Built iteratively, so that building it needs no deep stack.
        public static Node Chain(int length)
        {
            Node head = new() { Id = 0 };
            Node last = head;
            for (int i = 1; i < length; i++)
            {
                last.Next = new Node { Id = i };
                last = last.Next;
            }

            return head;
        }
    }

    private sealed class ResourceBundle(Component component, CancellationTokenSource source, FrozenSet<string> names)
    {
        public Component Component { get; } = component;
        public CancellationTokenSource Source { get; } = source;
        public FrozenSet<string> Names { get; } = names;
    }

    private sealed class FinalizableToken(Guid token)
    {
        public static readonly ConcurrentQueue<Guid> Finalized = new();

        public Guid Token { get; } = token;

#pragma warning disable MA0055 // The fixture needs a finalizer: it is the behaviour under test.
        ~FinalizableToken() => Finalized.Enqueue(Token);
#pragma warning restore MA0055
    }

    private sealed class StaticOwner
    {
        public static readonly object Shared = new();
    }
}
