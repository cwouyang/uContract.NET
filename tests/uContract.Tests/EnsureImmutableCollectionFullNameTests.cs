using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using uContract.Exceptions;

namespace uContract.Tests;

public class EnsureImmutableCollectionFullNameTests
{
    private const string ViolationDescription = "Ensure resultingCollection is an immutable collection";

    // The framework assembly that declares the immutable and the frozen collections, loaded once more: a type of
    // this copy has the full name of the framework's type and is another type. The tests only read from it.
    private static readonly Assembly SecondLoadedCopy = LoadSecondCopy();

    [Fact]
    public void EnsureImmutableCollection_WhenImmutableListOfSecondLoadedCopy_ReturnsCollection()
    {
        object argument = EmptyOfSecondLoadedCopy(typeof(ImmutableList<int>));
        Assert.False(argument is IImmutableList<int>);

        object result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenImmutableStackOfSecondLoadedCopy_ReturnsCollection()
    {
        object argument = EmptyOfSecondLoadedCopy(typeof(ImmutableStack<int>));
        Assert.False(argument is IImmutableStack<int>);

        object result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenImmutableDictionaryOfSecondLoadedCopy_ReturnsCollection()
    {
        object argument = EmptyOfSecondLoadedCopy(typeof(ImmutableDictionary<int, int>));
        Assert.False(argument is IImmutableDictionary<int, int>);

        object result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenImmutableHashSetOfSecondLoadedCopy_ReturnsCollection()
    {
        object argument = EmptyOfSecondLoadedCopy(typeof(ImmutableHashSet<int>));
        Assert.False(argument is IImmutableSet<int>);

        object result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenImmutableQueueOfSecondLoadedCopy_ReturnsCollection()
    {
        object argument = EmptyOfSecondLoadedCopy(typeof(ImmutableQueue<int>));
        Assert.False(argument is IImmutableQueue<int>);

        object result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenFrozenSetOfSecondLoadedCopy_ReturnsCollection()
    {
        object argument = EmptyOfSecondLoadedCopy(typeof(FrozenSet<int>));
        Assert.False(argument is FrozenSet<int>);

        object result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenFrozenDictionaryOfSecondLoadedCopy_ReturnsCollection()
    {
        object argument = EmptyOfSecondLoadedCopy(typeof(FrozenDictionary<int, int>));
        Assert.False(argument is FrozenDictionary<int, int>);

        object result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenClassOfAnotherAssemblyHasFullNameOfReadOnlyCollection_ReturnsCollection()
    {
        object argument = NewInstanceOfClassOfAnotherAssembly(typeof(ReadOnlyCollection<>).FullName!, arity: 1);
        Type definition = argument.GetType().GetGenericTypeDefinition();
        Assert.NotEqual(typeof(ReadOnlyCollection<>), definition);
        Assert.Equal(typeof(ReadOnlyCollection<>).FullName, definition.FullName);

        object result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenClassOfAnotherAssemblyHasFullNameOfReadOnlyDictionary_ReturnsCollection()
    {
        object argument = NewInstanceOfClassOfAnotherAssembly(typeof(ReadOnlyDictionary<,>).FullName!, arity: 2);
        Type definition = argument.GetType().GetGenericTypeDefinition();
        Assert.NotEqual(typeof(ReadOnlyDictionary<,>), definition);
        Assert.Equal(typeof(ReadOnlyDictionary<,>).FullName, definition.FullName);

        object result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenClassOfAnotherAssemblyHasFullNameOfFrozenSet_ReturnsCollection()
    {
        object argument = NewInstanceOfClassOfAnotherAssembly(typeof(FrozenSet<>).FullName!, arity: 1);
        Type definition = argument.GetType().GetGenericTypeDefinition();
        Assert.NotEqual(typeof(FrozenSet<>), definition);
        Assert.Equal(typeof(FrozenSet<>).FullName, definition.FullName);

        object result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenInterfaceOfAnotherAssemblyHasFullNameOfIImmutableStack_ReturnsCollection()
    {
        object argument = NewInstanceOfClassImplementingInterfaceOfAnotherAssembly(typeof(IImmutableStack<>).FullName!);
        Type definition = Assert.Single(argument.GetType().GetInterfaces()).GetGenericTypeDefinition();
        Assert.NotEqual(typeof(IImmutableStack<>), definition);
        Assert.Equal(typeof(IImmutableStack<>).FullName, definition.FullName);

        object result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenInterfaceOfAnotherAssemblyHasFullNameOfIImmutableList_ReturnsCollection()
    {
        object argument = NewInstanceOfClassImplementingInterfaceOfAnotherAssembly(typeof(IImmutableList<>).FullName!);
        Type definition = Assert.Single(argument.GetType().GetInterfaces()).GetGenericTypeDefinition();
        Assert.NotEqual(typeof(IImmutableList<>), definition);
        Assert.Equal(typeof(IImmutableList<>).FullName, definition.FullName);

        object result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenBuilderOfImmutableListOfSecondLoadedCopy_ThrowsPostconditionViolationException()
    {
        object list = EmptyOfSecondLoadedCopy(typeof(ImmutableList<int>));
        object argument = list.GetType().GetMethod("ToBuilder")!.Invoke(list, null)!;

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenArrayOfImmutableListsOfSecondLoadedCopy_ThrowsPostconditionViolationException()
    {
        Type elementType = EmptyOfSecondLoadedCopy(typeof(ImmutableList<int>)).GetType();
#pragma warning disable IL3050 // Test fixture only: the library itself never creates arrays this way.
        object argument = Array.CreateInstance(elementType, 1);
#pragma warning restore IL3050

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenClassOfAnotherAssemblyIsNamedReadOnlyCollectionInAnotherNamespace_ThrowsPostconditionViolationException()
    {
        object argument = NewInstanceOfClassOfAnotherAssembly("AnotherAssembly.ReadOnlyCollection`1", arity: 1);

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenInterfaceOfAnotherAssemblyIsNamedIImmutableStackInAnotherNamespace_ThrowsPostconditionViolationException()
    {
        object argument = NewInstanceOfClassImplementingInterfaceOfAnotherAssembly("AnotherAssembly.IImmutableStack`1");

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenClassOfAnotherAssemblyHasNameOfReadOnlyCollectionWithoutArity_ThrowsPostconditionViolationException()
    {
        object argument = NewInstanceOfClassOfAnotherAssembly(
            "System.Collections.ObjectModel.ReadOnlyCollection",
            arity: 0
        );

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenClassOfAnotherAssemblyHasFullNameThatDiffersByCaseOnly_ThrowsPostconditionViolationException()
    {
        object collectionLookalike = NewInstanceOfClassOfAnotherAssembly(
            "System.Collections.ObjectModel.READONLYCOLLECTION`1",
            arity: 1
        );
        object setLookalike = NewInstanceOfClassOfAnotherAssembly(
            "System.Collections.ObjectModel.READONLYSET`1",
            arity: 1
        );

        AssertViolation(() => Contract.EnsureImmutableCollection(collectionLookalike));
        AssertViolation(() => Contract.EnsureImmutableCollection(setLookalike));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenClassOfAnotherAssemblyHasFullNameOfImmutableListAndImplementsNoInterface_ThrowsPostconditionViolationException()
    {
        object argument = NewInstanceOfClassOfAnotherAssembly(typeof(ImmutableList<>).FullName!, arity: 1);

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenClassOfAnotherAssemblyHasFullNameOfIImmutableStack_ThrowsPostconditionViolationException()
    {
        object argument = NewInstanceOfClassOfAnotherAssembly(typeof(IImmutableStack<>).FullName!, arity: 1);

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenInterfaceOfAnotherAssemblyHasFullNameOfFrozenSet_ThrowsPostconditionViolationException()
    {
        object argument = NewInstanceOfClassImplementingInterfaceOfAnotherAssembly(typeof(FrozenSet<>).FullName!);

        AssertViolation(() => Contract.EnsureImmutableCollection(argument));
    }

    [Fact]
    public void EnsureImmutableCollection_WhenClassOfAnotherAssemblyHasFullNameOfReadOnlySet_ReturnsCollection()
    {
        object argument = NewInstanceOfClassOfAnotherAssembly("System.Collections.ObjectModel.ReadOnlySet`1", arity: 1);

        object result = Contract.EnsureImmutableCollection(argument);

        Assert.Same(argument, result);
    }

    private static void AssertViolation(Action call)
    {
        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(call);

        Assert.Equal(ViolationDescription, exception.Description);
    }

    private static Assembly LoadSecondCopy()
    {
        AssemblyLoadContext context = new("second loaded copy");
        using FileStream stream = File.OpenRead(typeof(ImmutableList).Assembly.Location);

        return context.LoadFromStream(stream);
    }

    private static object EmptyOfSecondLoadedCopy(Type frameworkType)
    {
        const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;
        Type type = SecondLoadedCopy
            .GetType(frameworkType.GetGenericTypeDefinition().FullName!, throwOnError: true)!
            .MakeGenericType(frameworkType.GetGenericArguments());

        return (
            type.GetField("Empty", PublicStatic)?.GetValue(null)
            ?? type.GetProperty("Empty", PublicStatic)!.GetValue(null)
        )!;
    }

    // Builds an assembly at run time and declares in it a public class with the given full name; a class with
    // type parameters is closed over int.
    private static object NewInstanceOfClassOfAnotherAssembly(string fullName, int arity)
    {
        TypeBuilder builder = NewModuleOfAnotherAssembly()
            .DefineType(fullName, TypeAttributes.Public | TypeAttributes.Class);
        if (arity > 0)
        {
            builder.DefineGenericParameters([.. Enumerable.Range(1, arity).Select(index => $"T{index}")]);
        }

        builder.DefineDefaultConstructor(MethodAttributes.Public);
        Type created = builder.CreateType();
        if (arity > 0)
        {
            created = created.MakeGenericType([.. Enumerable.Repeat(typeof(int), arity)]);
        }

        return Activator.CreateInstance(created)!;
    }

    // Builds an assembly at run time and declares in it a public interface with the given full name and one type
    // parameter, and a class that implements the interface closed over int and has no member of its own.
    private static object NewInstanceOfClassImplementingInterfaceOfAnotherAssembly(string interfaceFullName)
    {
        ModuleBuilder module = NewModuleOfAnotherAssembly();
        TypeBuilder interfaceBuilder = module.DefineType(
            interfaceFullName,
            TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract
        );
        interfaceBuilder.DefineGenericParameters("T");
        Type implemented = interfaceBuilder.CreateType().MakeGenericType(typeof(int));
        TypeBuilder builder = module.DefineType("AnotherAssembly.Bag", TypeAttributes.Public | TypeAttributes.Class);
        builder.AddInterfaceImplementation(implemented);
        builder.DefineDefaultConstructor(MethodAttributes.Public);

        return Activator.CreateInstance(builder.CreateType())!;
    }

    private static ModuleBuilder NewModuleOfAnotherAssembly()
    {
        return AssemblyBuilder
            .DefineDynamicAssembly(new AssemblyName("AnotherAssembly"), AssemblyBuilderAccess.Run)
            .DefineDynamicModule("AnotherAssembly");
    }
}
