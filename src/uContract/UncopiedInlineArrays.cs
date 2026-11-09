using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace uContract;

/// <summary>
///     The inline-array types whose struct elements a <see cref="Contract.Old{T}" /> copy could not copy (see
///     <see cref="DeepCopier" />), recorded on every object of that copy, so that the comparison refuses to compare
///     those inline arrays of it (see <see cref="MemberComparison" />).
/// </summary>
/// <remarks>
///     The objects are held weakly: recording them does not keep a copy alive. Nothing is looked up until a first copy
///     has been recorded, which happens only without dynamic code on the .NET 9 runtime or later.
/// </remarks>
internal static class UncopiedInlineArrays
{
    private static readonly ConditionalWeakTable<object, IReadOnlySet<Type>> ByObject = [];

    private static volatile bool _anyRecorded;

    // Records types on each object of one copy.
    internal static void Record(IEnumerable<object> copies, IReadOnlySet<Type> types)
    {
        foreach (object copy in copies)
        {
            ByObject.AddOrUpdate(copy, types);
        }

        _anyRecorded = true;
    }

    // The types recorded on value, or null when value is not an object of a copy that could not copy them all.
    internal static IReadOnlySet<Type>? Of(object? value)
    {
        return _anyRecorded && value is not null && ByObject.TryGetValue(value, out IReadOnlySet<Type>? types)
            ? types
            : null;
    }
}
