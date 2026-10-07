using System;
using System.Collections.Concurrent;
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

    private static readonly ConcurrentDictionary<Type, bool> HoldsReferencesCache = new();

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
            if (clone is Array array)
            {
                ReplaceElements(array, copies, pending);
            }
            else
            {
                ReplaceReferenceFields(clone, copies, pending);
            }
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

    // The clone already has the original's runtime type, rank and bounds; only its elements are replaced.
    private static void ReplaceElements(Array clone, Dictionary<object, object> copies, Stack<object> pending)
    {
        Type elementType = clone.GetType().GetElementType()!;
        if (!HoldsReferences(elementType))
        {
            return;
        }

        int[] indices = new int[clone.Rank];
        for (int dimension = 0; dimension < clone.Rank; dimension++)
        {
            if (clone.GetLength(dimension) == 0)
            {
                return;
            }

            indices[dimension] = clone.GetLowerBound(dimension);
        }

        do
        {
            if (clone.GetValue(indices) is not { } element)
            {
                continue;
            }

            if (elementType.IsValueType)
            {
                // The element arrives boxed: replace the references inside the box, then store it back.
                ReplaceReferenceFields(element, copies, pending);
                clone.SetValue(element, indices);
            }
            else
            {
                clone.SetValue(CopyOf(element, copies, pending), indices);
            }
        } while (Advance(clone, indices));
    }

    // Moves the indices to the next element in row-major order; false once past the last one.
    private static bool Advance(Array array, int[] indices)
    {
        for (int dimension = indices.Length - 1; dimension >= 0; dimension--)
        {
            if (indices[dimension] < array.GetUpperBound(dimension))
            {
                indices[dimension]++;
                return true;
            }

            indices[dimension] = array.GetLowerBound(dimension);
        }

        return false;
    }

    // Whether a value of this type can refer to an object: a reference type, or a struct with a
    // reference-typed field at any depth of its struct fields.
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2070",
        Justification = "Callers (Old/EnsureAssignable) are annotated with [RequiresUnreferencedCode]. Under Native AOT a field the trimmer hides from reflection is not seen, so a struct holding references only there stays bitwise: the copy shares what it refers to with the original."
    )]
    private static bool HoldsReferences(Type type)
    {
        if (!type.IsValueType)
        {
            return IsReferenceTyped(type);
        }

        // A primitive declares a field of its own type (Int32.m_value), so it would never end.
        if (type.IsPrimitive || type.IsEnum)
        {
            return false;
        }

        return HoldsReferencesCache.GetOrAdd(
            type,
            static valueType =>
                Array.Exists(
                    valueType.GetFields(DeclaredInstanceFields),
                    field => field.FieldType != valueType && HoldsReferences(field.FieldType)
                )
        );
    }

    // Pointer, function-pointer, IntPtr/UIntPtr fields and fixed buffers stay bitwise.
    private static bool IsReferenceTyped(Type fieldType)
    {
        return !fieldType.IsValueType && !fieldType.IsPointer && !fieldType.IsFunctionPointer;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "MemberwiseClone")]
    private static extern object CloneBitwise(object original);
}
