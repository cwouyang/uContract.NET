// SharedTypes treats every type in the namespace System.Collections.Frozen as shared, and fixed. These
// classes, declared in the test assembly, are in that namespace. None of them is a FrozenSet<T> or a
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
