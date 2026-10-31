using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using uContract.Exceptions;

namespace uContract.Tests;

/// <summary>
///     Tests that a collectible type which went through <c>Old</c> or <c>EnsureAssignable</c> can still be
///     collected once the caller drops it. The collectible types are emitted with
///     <see cref="AssemblyBuilderAccess.RunAndCollect" />; each scenario runs in a method of its own that is not
///     inlined, so no reference to the type stays on the test's stack frame.
/// </summary>
public sealed class CollectibleTypeCacheTests
{
    private const string OwnerField = "Owner";
    private const int MaxCollections = 10;

    [Fact]
    public void OldAndEnsureAssignable_WhenCollectibleTypesAreDropped_TypesAreCollected()
    {
        WeakReference[] types = CopyAndCompareListAndArrayOfCollectibleTypes();

        Assert.All(types, type => Assert.True(IsCollected(type)));
    }

    [Fact]
    public void Old_WhenCollectibleClassIsDropped_TypeIsCollected()
    {
        WeakReference type = CopyCollectibleAccount();

        Assert.True(IsCollected(type));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CopyCollectibleAccount()
    {
        Type account = DefineAccount(AssemblyBuilderAccess.RunAndCollect);
        object original = NewInstance(account, "Alice");

        _ = Contract.Old(() => original);

        return new WeakReference(account);
    }

    [Fact]
    public void EnsureAssignable_WhenListOfCollectibleClassIsDropped_TypeIsCollected()
    {
        WeakReference type = CompareListsOfCollectibleAccount();

        Assert.True(IsCollected(type));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CompareListsOfCollectibleAccount()
    {
        Type account = DefineAccount(AssemblyBuilderAccess.RunAndCollect);

        Contract.EnsureAssignable(new Holder { Value = NewListOf(account) }, new Holder { Value = NewListOf(account) });

        return new WeakReference(account);
    }

    [Fact]
    public void Old_WhenArrayOfCollectibleStructIsDropped_StructTypeIsCollected()
    {
        WeakReference type = CopyArrayOfCollectibleTag();

        Assert.True(IsCollected(type));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CopyArrayOfCollectibleTag()
    {
        Type tag = DefineTag(AssemblyBuilderAccess.RunAndCollect);
        Array original = NewArrayOf(tag);

        _ = Contract.Old(() => original);

        return new WeakReference(tag);
    }

    [Fact]
    public void Old_WhenListOfClassIsCollectible_CopiesItLikeNonCollectibleOne()
    {
        string collectible = DescribeCopiesOfListOfAccount(AssemblyBuilderAccess.RunAndCollect);

        Assert.Equal(DescribeCopiesOfListOfAccount(AssemblyBuilderAccess.Run), collectible);
    }

    [Fact]
    public void Old_WhenArrayOfStructIsCollectible_CopiesItLikeNonCollectibleOne()
    {
        string collectible = DescribeCopiesOfArrayOfTag(AssemblyBuilderAccess.RunAndCollect);

        Assert.Equal(DescribeCopiesOfArrayOfTag(AssemblyBuilderAccess.Run), collectible);
    }

    [Fact]
    public void EnsureAssignable_WhenCollectibleClassDiffers_ReportsItLikeNonCollectibleOne()
    {
        string nonCollectible = DescribeViolationsOfAccount(AssemblyBuilderAccess.Run);

        string collectible = DescribeViolationsOfAccount(AssemblyBuilderAccess.RunAndCollect);

        Assert.StartsWith(typeof(PostconditionViolationException).FullName!, nonCollectible, StringComparison.Ordinal);
        Assert.Equal(nonCollectible, collectible);
    }

    // Each Describe method runs its scenario twice: the second run reads what the first one cached.
    private static string DescribeCopiesOfListOfAccount(AssemblyBuilderAccess access)
    {
        IList original = NewListOf(DefineAccount(access));

        return DescribeTwice(() =>
        {
            IList copy = Contract.Old(() => original);
            return $"{ReferenceEquals(copy, original)} {ReferenceEquals(copy[0], original[0])} {OwnerOf(copy[0]!)}";
        });
    }

    private static string DescribeCopiesOfArrayOfTag(AssemblyBuilderAccess access)
    {
        Array original = NewArrayOf(DefineTag(access));
        TestAccount owner = new() { Owner = "Alice" };
        object tag = original.GetValue(0)!;
        tag.GetType().GetField(OwnerField)!.SetValue(tag, owner);
        original.SetValue(tag, 0);

        return DescribeTwice(() =>
        {
            Array copy = Contract.Old(() => original);
            object copiedOwner = OwnerOf(copy.GetValue(0)!)!;
            return $"{ReferenceEquals(copy, original)} {ReferenceEquals(copiedOwner, owner)} {((TestAccount)copiedOwner).Owner}";
        });
    }

    private static string DescribeViolationsOfAccount(AssemblyBuilderAccess access)
    {
        Type account = DefineAccount(access);

        return DescribeTwice(() =>
        {
            Exception? exception = Record.Exception(() =>
                Contract.EnsureAssignable(
                    new Holder { Value = NewInstance(account, "Bob") },
                    new Holder { Value = NewInstance(account, "Alice") }
                )
            );
            return $"{exception?.GetType()}: {exception?.Message}";
        });
    }

    private static string DescribeTwice(Func<string> describe)
    {
        return $"{describe()} | {describe()}";
    }

    private static object? OwnerOf(object instance)
    {
        return instance.GetType().GetField(OwnerField)!.GetValue(instance);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] CopyAndCompareListAndArrayOfCollectibleTypes()
    {
        Type account = DefineAccount(AssemblyBuilderAccess.RunAndCollect);
        Type tag = DefineTag(AssemblyBuilderAccess.RunAndCollect);
        object[] models = [NewListOf(account), NewArrayOf(tag)];

        foreach (object model in models)
        {
            object old = Contract.Old(() => model);
            Contract.EnsureAssignable(new Holder { Value = model }, new Holder { Value = old });
        }

        return [new WeakReference(account), new WeakReference(tag)];
    }

    private static bool IsCollected(WeakReference reference)
    {
        for (int collection = 0; collection < MaxCollections && reference.IsAlive; collection++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        return !reference.IsAlive;
    }

    private static Type DefineAccount(AssemblyBuilderAccess access)
    {
        TypeBuilder builder = NewModule(access)
            .DefineType("Emitted.Account", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class);
        builder.DefineField(OwnerField, typeof(string), FieldAttributes.Public);
        builder.DefineDefaultConstructor(MethodAttributes.Public);
        return builder.CreateType();
    }

    private static Type DefineTag(AssemblyBuilderAccess access)
    {
        TypeBuilder builder = NewModule(access)
            .DefineType(
                "Emitted.Tag",
                TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout,
                typeof(ValueType)
            );
        builder.DefineField(OwnerField, typeof(object), FieldAttributes.Public);
        return builder.CreateType();
    }

    private static ModuleBuilder NewModule(AssemblyBuilderAccess access)
    {
        string name = $"Emitted{Guid.NewGuid():N}";
        return AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), access).DefineDynamicModule(name);
    }

    private static object NewInstance(Type type, string owner)
    {
        object instance = Activator.CreateInstance(type)!;
        type.GetField(OwnerField)!.SetValue(instance, owner);
        return instance;
    }

    private static IList NewListOf(Type elementType)
    {
        IList list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
        list.Add(NewInstance(elementType, "Alice"));
        return list;
    }

    private static Array NewArrayOf(Type elementType)
    {
        Array array = Array.CreateInstance(elementType, 1);
        array.SetValue(NewInstance(elementType, "Alice"), 0);
        return array;
    }

    private sealed class Holder
    {
        public object? Value { get; set; }
    }
}
