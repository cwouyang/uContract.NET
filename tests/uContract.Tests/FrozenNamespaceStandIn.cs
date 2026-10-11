// These types, declared in the test assembly, are in the namespace System.Collections.Frozen. SharedTypes
// treats a class of that namespace as shared, and fixed; it does not treat a value type declared in it, or
// an array whose element type is declared in it, as shared. None of these types is a FrozenSet<T> or a
// FrozenDictionary<TKey, TValue>. FrozenNamespaceComponent also derives from Component, a type that is
// shared, and its state can change.
#pragma warning disable IDE0130 // The namespace is the point of these types.
namespace System.Collections.Frozen;

#pragma warning restore IDE0130

internal sealed class FrozenNamespaceComponent : System.ComponentModel.Component;

internal sealed class FrozenNamespaceSequence(params int[] items) : IEnumerable<int>
{
    public IEnumerator<int> GetEnumerator()
    {
        return ((IEnumerable<int>)items).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

internal sealed class FrozenNamespaceComponentSequence(params int[] items)
    : System.ComponentModel.Component,
        IEnumerable<int>
{
    public IEnumerator<int> GetEnumerator()
    {
        return ((IEnumerable<int>)items).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

internal sealed class FrozenNamespaceValue
{
    public int Value { get; init; }
}

internal sealed class FrozenNamespaceMutableValue
{
    public int Value { get; set; }
}

internal struct FrozenNamespaceStruct
{
    public int Value;
}

internal struct FrozenNamespaceStructWithList
{
    public List<int> Numbers;
}

internal enum FrozenNamespaceEnum
{
    First,
    Second,
}
