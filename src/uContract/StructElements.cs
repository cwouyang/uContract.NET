using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace uContract;

/// <summary>
///     Reads the struct elements of an inline array, boxed, from its memory (see
///     <see cref="MemberComparison" />, which compares them as the elements of an array), and writes boxed ones back
///     (see <see cref="DeepCopier" />, which copies them as the elements of an array).
/// </summary>
/// <remarks>
///     The .NET 9+ runtime offers <c>RuntimeHelpers.SizeOf</c> and <c>RuntimeHelpers.Box</c>, which need no dynamic
///     code; this library targets .NET 8, so they are bound at run time. The .NET 8 runtime has neither, and its only
///     way to read a struct element of a type known at run time is a generic method instantiated over it, which needs
///     dynamic code. Neither runtime offers a way to write one without such a method.
/// </remarks>
internal static class StructElements
{
    private static readonly TypeCache<int> SizeCache = new();
    private static readonly TypeCache<Reader> ReaderCache = new();
    private static readonly TypeCache<Writer> WriterCache = new();
    private static readonly Func<RuntimeTypeHandle, int>? SizeOf = BindSizeOf();
    private static readonly BoxFunction? Box = BindBox();

    // RuntimeHelpers.Box(ref byte, RuntimeTypeHandle) of the .NET 9+ runtime.
    private delegate object? BoxFunction(ref byte data, RuntimeTypeHandle type);

    // Reads length struct elements at data, boxed (see Instantiate).
    private delegate object?[] Reader(ref byte data, int length);

    // Writes boxed struct elements over as many elements at data (see Instantiate).
    private delegate void Writer(ref byte data, object?[] elements);

    // Whether Write is available: neither runtime writes a struct element of a type known at run time without a
    // generic method instantiated over it, which needs dynamic code.
    internal static bool CanWrite => RuntimeFacts.IsDynamicCodeSupported;

    // Whether this runtime offers RuntimeHelpers.SizeOf and RuntimeHelpers.Box (the .NET 9 runtime or later), through
    // which Read returns the elements without dynamic code. Bound once, by looking the methods up.
    [MemberNotNullWhen(true, nameof(SizeOf), nameof(Box))]
    internal static bool ReadsWithoutDynamicCode => SizeOf is not null && Box is not null;

    // Whether Read returns the elements: through RuntimeHelpers on the .NET 9 runtime or later, or with dynamic code.
    internal static bool CanRead => ReadsWithoutDynamicCode || RuntimeFacts.IsDynamicCodeSupported;

    // The length elements of the struct type elementType at data, boxed. Null when neither way is available: the
    // .NET 8 runtime without dynamic code.
    internal static object?[]? Read(Type elementType, ref byte data, int length)
    {
        if (ReadsWithoutDynamicCode)
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
            ? ReaderCache.GetOrAdd(elementType, Instantiate<Reader>)(ref data, length)
            : null;
    }

    // Writes elements, boxed values of the struct type elementType, over the elements.Length elements at data, through
    // typed references, so that the references they hold stay visible to the GC. Called only when CanWrite.
    internal static void Write(Type elementType, ref byte data, object?[] elements)
    {
        Debug.Assert(CanWrite, "StructElements.Write is called only when CanWrite.");
        WriterCache.GetOrAdd(elementType, Instantiate<Writer>)(ref data, elements);
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

    // The generic method of this class that a TDelegate stands for (UnboxElements for a Writer, BoxElements for a
    // Reader) instantiated over elementType. The only place that needs dynamic code: every caller is reached only where
    // it is supported: Read asks for a Reader on the .NET 8 runtime under the JIT, and Write is called only when
    // CanWrite. Both methods are looked up here by name, so the trimmer sees which one is instantiated.
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "Called only when RuntimeFeature.IsDynamicCodeSupported is true (through RuntimeFacts), so the instantiation over the element type can be compiled at run time. Without dynamic code the elements are boxed by the .NET 9+ RuntimeHelpers.Box, or the comparison reports that it cannot compare them; an Old copy leaves them as the field walk copies them."
    )]
    private static TDelegate Instantiate<TDelegate>(Type elementType)
        where TDelegate : Delegate
    {
        MethodInfo definition =
            typeof(TDelegate) == typeof(Writer)
                ? typeof(StructElements).GetMethod(nameof(UnboxElements), BindingFlags.NonPublic | BindingFlags.Static)!
                : typeof(StructElements).GetMethod(nameof(BoxElements), BindingFlags.NonPublic | BindingFlags.Static)!;
        return definition.MakeGenericMethod(elementType).CreateDelegate<TDelegate>();
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

    // The memory at data is written as elements.Length elements of TElement, which is in bounds: TElement is the
    // element type of the inline array that holds them, and elements holds one value for each of its elements.
    private static void UnboxElements<TElement>(ref byte data, object?[] elements)
    {
        Span<TElement> span = MemoryMarshal.CreateSpan(ref Unsafe.As<byte, TElement>(ref data), elements.Length);
        for (int i = 0; i < elements.Length; i++)
        {
            span[i] = (TElement)elements[i]!;
        }
    }
}
