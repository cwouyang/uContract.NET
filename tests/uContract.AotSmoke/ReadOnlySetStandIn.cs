// The framework declares System.Collections.ObjectModel.ReadOnlySet<T> from .NET 9, and this program is built for
// net8.0. This class has the full name of that type and stands for it, so that a check of this program gets its
// answer from the comparison by full name of Contract.EnsureImmutableCollection: the library, also built for
// net8.0, cannot refer to the type and compares that one by name only.
#pragma warning disable IDE0130 // The namespace is the point of this type.
namespace System.Collections.ObjectModel;

#pragma warning restore IDE0130

internal sealed class ReadOnlySet<T>;
