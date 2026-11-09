using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace uContract;

/// <summary>
///     Reads the struct elements of an inline array, boxed, from its memory (see
///     <see cref="MemberComparison" />, which compares them as the elements of an array).
/// </summary>
/// <remarks>
///     The .NET 9+ runtime offers <c>RuntimeHelpers.SizeOf</c> and <c>RuntimeHelpers.Box</c>, which need no dynamic
///     code; this library targets .NET 8, so they are bound at run time. The .NET 8 runtime has neither, and its only
///     way to read a struct element of a type known at run time is a generic method instantiated over it, which needs
///     dynamic code.
/// </remarks>
internal static class StructElements
{
    private static readonly TypeCache<int> SizeCache = new();
    private static readonly TypeCache<Reader> ReaderCache = new();
    private static readonly Func<RuntimeTypeHandle, int>? SizeOf = BindSizeOf();
    private static readonly BoxFunction? Box = BindBox();

    // RuntimeHelpers.Box(ref byte, RuntimeTypeHandle) of the .NET 9+ runtime.
    private delegate object? BoxFunction(ref byte data, RuntimeTypeHandle type);

    // Reads length struct elements at data, boxed (see CreateReader).
    private delegate object?[] Reader(ref byte data, int length);

    // The length elements of the struct type elementType at data, boxed. Null when neither way is available: the
    // .NET 8 runtime without dynamic code.
    internal static object?[]? Read(Type elementType, ref byte data, int length)
    {
        if (SizeOf is not null && Box is not null)
        {
            RuntimeTypeHandle handle = elementType.TypeHandle;
            int size = SizeCache.GetOrAdd(elementType, static t => SizeOf(t.TypeHandle));
            object?[] elements = new object?[length];
            for (int i = 0; i < length; i++)
            {
                elements[i] = Box(ref Unsafe.Add(ref data, i * size), handle);
            }

            return elements;
        }

        return RuntimeFacts.IsDynamicCodeSupported
            ? ReaderCache.GetOrAdd(elementType, CreateReader)(ref data, length)
            : null;
    }

    private static Func<RuntimeTypeHandle, int>? BindSizeOf()
    {
        return typeof(RuntimeHelpers)
            .GetMethod("SizeOf", BindingFlags.Public | BindingFlags.Static, [typeof(RuntimeTypeHandle)])
            ?.CreateDelegate<Func<RuntimeTypeHandle, int>>();
    }

    private static BoxFunction? BindBox()
    {
        return typeof(RuntimeHelpers)
            .GetMethod(
                "Box",
                BindingFlags.Public | BindingFlags.Static,
                [typeof(byte).MakeByRefType(), typeof(RuntimeTypeHandle)]
            )
            ?.CreateDelegate<BoxFunction>();
    }

    // Reached only where dynamic code is supported (see Read): on the .NET 8 runtime under the JIT.
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "Called only when RuntimeFeature.IsDynamicCodeSupported is true (through RuntimeFacts), so the instantiation over the element type can be compiled at run time. Without dynamic code the elements are boxed by the .NET 9+ RuntimeHelpers.Box, or the comparison reports that it cannot compare them."
    )]
    private static Reader CreateReader(Type elementType)
    {
        return typeof(StructElements)
            .GetMethod(nameof(BoxElements), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(elementType)
            .CreateDelegate<Reader>();
    }

    // The memory at data is read as length elements of TElement, which is in bounds: TElement is the element type of
    // the inline array that holds them.
    private static object?[] BoxElements<TElement>(ref byte data, int length)
    {
        ReadOnlySpan<TElement> span = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<byte, TElement>(ref data), length);
        object?[] elements = new object?[length];
        for (int i = 0; i < length; i++)
        {
            elements[i] = span[i];
        }

        return elements;
    }
}
