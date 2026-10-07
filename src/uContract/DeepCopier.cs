using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace uContract;

/// <summary>
///     Copies a value for <see cref="Contract.Old{T}" /> without running user code: each object starts as a
///     bitwise clone, so every instance field keeps the original's value; then each reference-typed field
///     visible to reflection is replaced by the copy of what it refers to.
/// </summary>
/// <remarks>
///     Shared types (<see cref="SharedTypes" />, <see cref="string" /> included) are never cloned. An instance
///     reached more than once is copied once, so cycles are reproduced. The graph is walked with an explicit
///     work list, not recursion.
/// </remarks>
internal static class DeepCopier
{
    private const BindingFlags DeclaredInstanceFields =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    internal static T Copy<T>(T value)
    {
        if (value is null)
        {
            return value;
        }

        Dictionary<object, object> copies = new(ReferenceEqualityComparer.Instance);
        Stack<object> pending = new();
        object copy = CopyOf(value, copies, pending);
        while (pending.TryPop(out object? clone))
        {
            ReplaceReferenceFields(clone, copies, pending);
        }

        return (T)copy;
    }

    private static object CopyOf(object original, Dictionary<object, object> copies, Stack<object> pending)
    {
        if (SharedTypes.IsShared(original.GetType()))
        {
            return original;
        }

        if (copies.TryGetValue(original, out object? copy))
        {
            return copy;
        }

        copy = CloneBitwise(original);
        copies.Add(original, copy);
        pending.Push(copy);
        return copy;
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2075",
        Justification = "Callers (Old/EnsureAssignable) are annotated with [RequiresUnreferencedCode]. Under Native AOT a field the trimmer hides from reflection is not visited, so the object it refers to stays bitwise: the copy shares it with the original."
    )]
    private static void ReplaceReferenceFields(object clone, Dictionary<object, object> copies, Stack<object> pending)
    {
        for (Type? type = clone.GetType(); type is not null; type = type.BaseType)
        {
            foreach (FieldInfo field in type.GetFields(DeclaredInstanceFields))
            {
                if (!IsReferenceTyped(field.FieldType) || field.GetValue(clone) is not { } referenced)
                {
                    continue;
                }

                field.SetValue(clone, CopyOf(referenced, copies, pending));
            }
        }
    }

    // Pointer, function-pointer, IntPtr/UIntPtr fields and fixed buffers stay bitwise.
    private static bool IsReferenceTyped(Type fieldType)
    {
        return !fieldType.IsValueType && !fieldType.IsPointer && !fieldType.IsFunctionPointer;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "MemberwiseClone")]
    private static extern object CloneBitwise(object original);
}
