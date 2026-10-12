// System.Collections.ObjectModel.ReadOnlySet<T> is in the framework from .NET 9 only, and this suite runs on
// net8.0. Contract.EnsureImmutableCollection matches that type by its full name, so this stand-in, declared in
// the test assembly, exercises the rule.
#pragma warning disable IDE0130 // The namespace is the point of this type.
namespace System.Collections.ObjectModel;

#pragma warning restore IDE0130

internal class ReadOnlySet<T>(ISet<T> set) : IEnumerable<T>
{
    public IEnumerator<T> GetEnumerator()
    {
        return set.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
