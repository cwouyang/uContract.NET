using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace uContract;

/// <summary>
///     Copies a value for <see cref="Contract.Old{T}" /> without running user code: each object starts as a
///     bitwise clone, so every instance field keeps the original's value; then each reference-typed field
///     visible to reflection is replaced by the copy of what it refers to. A struct field stays bitwise
///     except for the reference-typed fields inside it, replaced the same way at any nesting depth. An
///     array keeps its runtime type, rank and bounds, and its elements are replaced by the same rules. So are the
///     elements of an inline array, read from its memory; struct elements only where the runtime can write them back
///     (see <see cref="StructElements" />), and elsewhere element 0 alone, as a field.
/// </summary>
/// <remarks>
///     Shared types (<see cref="SharedTypes" />, <see cref="string" /> included) are never cloned. An instance
///     reached more than once is copied once, so cycles are reproduced. The graph is walked with an explicit
///     work list, not recursion. An array whose element type holds no reference is not visited.
/// </remarks>
internal static class DeepCopier
{
    private const BindingFlags DeclaredInstanceFields =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static readonly TypeCache<bool> HoldsReferencesCache = new();

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
        // MemberwiseClone registers a clone of a finalizable type for finalization; a copy must never run user code.
#pragma warning disable CA1816 // The clone is a fresh object that this copier owns; it is not an IDisposable pattern.
        GC.SuppressFinalize(copy);
#pragma warning restore CA1816
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
        if (TryReplaceInlineArrayElements(clone, copies, pending))
        {
            return;
        }

        for (Type? type = clone.GetType(); type is not null; type = type.BaseType)
        {
            foreach (FieldInfo field in type.GetFields(DeclaredInstanceFields))
            {
                if (!HoldsReferences(field.FieldType) || field.GetValue(clone) is not { } value)
                {
                    continue;
                }

                field.SetValue(clone, Replacement(value, field.FieldType, copies, pending));
            }
        }
    }

    // A boxed inline array: reflection lists only its element 0, so every element is replaced here, from the memory
    // of the box. False leaves the box to the field walk.
    private static bool TryReplaceInlineArrayElements(
        object box,
        Dictionary<object, object> copies,
        Stack<object> pending
    )
    {
        Type type = box.GetType();
        if (!MemberComparison.IsInlineArray(type))
        {
            return false;
        }

        MemberComparison.InlineArrayShape shape = MemberComparison.InlineArrayShapeOf(type);
        if (shape is not { ElementType: { } elementType } || !HoldsReferences(elementType))
        {
            return false;
        }

        ref byte data = ref Unsafe.As<StrongBox<byte>>(box).Value;
        if (elementType.IsValueType)
        {
            return TryReplaceStructElements(elementType, ref data, shape.Length, copies, pending);
        }

        Span<object?> elements = MemoryMarshal.CreateSpan(ref Unsafe.As<byte, object?>(ref data), shape.Length);
        for (int i = 0; i < elements.Length; i++)
        {
            if (elements[i] is { } element)
            {
                elements[i] = CopyOf(element, copies, pending);
            }
        }

        return true;
    }

    // The length struct elements at data are read boxed, the references inside each box are replaced, and the boxes
    // are written back (see StructElements). False, changing nothing, where this runtime cannot write them.
    private static bool TryReplaceStructElements(
        Type elementType,
        ref byte data,
        int length,
        Dictionary<object, object> copies,
        Stack<object> pending
    )
    {
        if (!StructElements.CanWrite || StructElements.Read(elementType, ref data, length) is not { } elements)
        {
            return false;
        }

        for (int i = 0; i < length; i++)
        {
            elements[i] = Replacement(elements[i]!, elementType, copies, pending);
        }

        StructElements.Write(elementType, ref data, elements);
        return true;
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

            clone.SetValue(Replacement(element, elementType, copies, pending), indices);
        } while (Advance(clone, indices));
    }

    // A struct (field or element) arrives boxed: the references inside the box are replaced and the box is
    // stored back. This recurses once per level of struct nesting, which the type bounds, not the graph.
    private static object Replacement(
        object value,
        Type declaredType,
        Dictionary<object, object> copies,
        Stack<object> pending
    )
    {
        if (!declaredType.IsValueType)
        {
            return CopyOf(value, copies, pending);
        }

        ReplaceReferenceFields(value, copies, pending);
        return value;
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
