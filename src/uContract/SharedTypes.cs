using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices.Marshalling;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace uContract;

/// <summary>
///     Classifies the shared types: resources and identities (tasks, locks, streams, handles, reflection
///     objects, comparers, ...) whose instances are compared by reference, never by their members, so that
///     no getter or factory of theirs runs. <see cref="string" /> is listed too; it is compared by Equals.
///     A <c>FrozenSet&lt;T&gt;</c> or a <c>FrozenDictionary&lt;TKey, TValue&gt;</c> is shared too, and two of
///     them are compared element by element, in enumeration order, also without reading their members.
///     Every other class declared in <c>System.Collections.Frozen</c> is shared and compared by reference.
///     A value type declared in <c>System.Collections.Frozen</c> is not shared, and an array whose element
///     type is declared in <c>System.Collections.Frozen</c> is not: <c>Old</c> copies them, and two of them
///     are compared like any other two values or arrays.
///     An array of pointers or of function pointers is never shared: <c>Old</c> copies it with its
///     addresses, and two of them are compared by address. This class never reads
///     <c>Type.Namespace</c> of an array type.
/// </summary>
internal static class SharedTypes
{
    private static readonly ConcurrentDictionary<Type, bool> Cache = new();

    // A listed type matches itself and every type derived from it.
    internal static readonly Type[] Listed =
    [
        typeof(string),
        typeof(Delegate),
        typeof(Type),
        typeof(MemberInfo),
        typeof(Assembly),
        typeof(Module),
        typeof(Pointer),
        typeof(Stream),
        typeof(WaitHandle),
        typeof(CancellationTokenSource),
        typeof(Thread),
        typeof(Timer),
        typeof(SynchronizationContext),
        typeof(SemaphoreSlim),
        typeof(ManualResetEventSlim),
        typeof(CountdownEvent),
        typeof(ReaderWriterLockSlim),
        typeof(Barrier),
        typeof(Task),
        typeof(WeakReference),
        typeof(Component),
        typeof(HttpMessageHandler),
        typeof(HttpClient),
        typeof(Socket),
        typeof(Regex),
    ];

    // A listed open generic type matches every construction of it and every type derived from one.
    internal static readonly Type[] ListedGenerics =
    [
        typeof(ThreadLocal<>),
        typeof(Lazy<>),
        typeof(WeakReference<>),
        typeof(ConditionalWeakTable<,>),
        typeof(AsyncLocal<>),
    ];

    // A listed type that net8.0 cannot reference, matched by full name; it and every type derived from it.
    internal const string LockFullName = "System.Threading.Lock";

    // The listed types whose state can change, in the order in which a type is matched against them. The
    // other listed types are fixed: a string, a delegate, the reflection objects and a Regex.
    internal static readonly Type[] ListedWithChangingState =
    [
        typeof(Stream),
        typeof(WaitHandle),
        typeof(CancellationTokenSource),
        typeof(Thread),
        typeof(Timer),
        typeof(SynchronizationContext),
        typeof(SemaphoreSlim),
        typeof(ManualResetEventSlim),
        typeof(CountdownEvent),
        typeof(ReaderWriterLockSlim),
        typeof(Barrier),
        typeof(Task),
        typeof(WeakReference),
        typeof(Component),
        typeof(HttpMessageHandler),
        typeof(HttpClient),
        typeof(Socket),
    ];

    internal static bool IsShared(Type type)
    {
        return Cache.GetOrAdd(type, Classify);
    }

    /// <summary>
    ///     Says whether <paramref name="type" /> is or derives from a constructed <c>FrozenSet&lt;T&gt;</c> or
    ///     <c>FrozenDictionary&lt;TKey, TValue&gt;</c>. Two instances of such a type are compared element by
    ///     element, in enumeration order, and not by reference, and
    ///     <c>Contract.EnsureImmutableCollection</c> accepts an instance of it. No other type declared
    ///     in <c>System.Collections.Frozen</c> matches. An array of them does not match either: the type of an
    ///     array is not a <c>FrozenSet&lt;T&gt;</c> or a <c>FrozenDictionary&lt;TKey, TValue&gt;</c> and does
    ///     not derive from one.
    /// </summary>
    // Contract.EnsureImmutableCollection calls this helper, so a type added to it is accepted by that method too.
    // The helper reads no static field of this class: an explicit static constructor on the class, or a static
    // field read here, would make that method run the initialiser of the class.
    internal static bool IsFrozenSetOrDictionary(Type type)
    {
        return DerivesFromGeneric(type, typeof(FrozenSet<>)) || DerivesFromGeneric(type, typeof(FrozenDictionary<,>));
    }

    /// <summary>
    ///     An array of pointers or of function pointers, of any rank. It is never shared, and two of them are
    ///     compared by address. Reads the element type only, never the namespace of <paramref name="type" />.
    /// </summary>
    internal static bool IsArrayOfPointersOrFunctionPointers(Type type)
    {
        return type.IsArray && type.GetElementType() is { IsPointer: true } or { IsFunctionPointer: true };
    }

    /// <summary>
    ///     Says what <c>Old</c> shares when <paramref name="type" /> is shared, and its state can change: the
    ///     words that follow "Contract.Old shares" in a message. Returns null for a type that is shared, and
    ///     fixed, and for a type that is not shared. Reads only the type: no instance is touched.
    ///     The first match is the one that is named: a listed type, a listed open generic type, the type
    ///     matched by full name, a handle, a source-generated COM object, any other COM object. A type that
    ///     matches one of them is reported even when it also belongs to a fixed category.
    /// </summary>
    internal static string? DescribeChangingShare(Type type)
    {
        foreach (Type candidate in ListedWithChangingState)
        {
            if (candidate.IsAssignableFrom(type))
            {
                return InstanceOf(candidate.ToString());
            }
        }

        foreach (Type definition in ListedGenerics)
        {
            if (DerivesFromGeneric(type, definition))
            {
                return InstanceOf(definition.ToString());
            }
        }

        if (DerivesFromNamed(type, LockFullName))
        {
            return InstanceOf(LockFullName);
        }

        if (typeof(CriticalFinalizerObject).IsAssignableFrom(type))
        {
            return InstanceOf(typeof(CriticalFinalizerObject).ToString());
        }

        if (typeof(ComObject).IsAssignableFrom(type))
        {
            return InstanceOf(typeof(ComObject).ToString());
        }

        return type.IsCOMObject ? "a COM object" : null;
    }

    private static string InstanceOf(string typeName)
    {
        return $"an instance of {typeName} or of a type derived from it";
    }

    private static bool Classify(Type type)
    {
        // Decided first: an array of pointers or of function pointers is never shared, and neither is a type
        // for which NamespaceCannotBeRead answers true. The answer for these types is given here, and no test
        // below is asked about them. IsClassOfTheFrozenNamespace reads the namespace of no array type, but it
        // would read that of a pointer to a function pointer, for which NamespaceCannotBeRead answers true
        // too; no object has such a runtime type. Whoever removes this early answer has to check, for each
        // test below, that it does not throw and does not answer true for these types.
        if (IsArrayOfPointersOrFunctionPointers(type) || NamespaceCannotBeRead(type))
        {
            return false;
        }

        return IsInSharedCategory(type)
            || Listed.Any(listed => listed.IsAssignableFrom(type))
            || ListedGenerics.Any(definition => DerivesFromGeneric(type, definition))
            || DerivesFromNamed(type, LockFullName);
    }

    // The runtime looks for the namespace of an array type in its innermost element type, and throws
    // BadImageFormatException when that is a function pointer: for an array of function pointers, of any
    // rank, and for an array of such arrays. Only the element types are read here.
    private static bool NamespaceCannotBeRead(Type type)
    {
        Type innermost = type;
        while (innermost.HasElementType)
        {
            innermost = innermost.GetElementType()!;
        }

        return innermost.IsFunctionPointer;
    }

    // Handles (SafeHandle, CriticalHandle), COM objects, the classes declared in System.Collections.Frozen
    // and CoreLib comparers.
    // A category added here must be classified too: DescribeChangingShare names it when its state can
    // change, as it does for the handles and the COM objects; otherwise it is fixed, as the classes declared
    // in System.Collections.Frozen and the CoreLib comparers are. The same holds for a type added to a list
    // above.
    private static bool IsInSharedCategory(Type type)
    {
        return typeof(CriticalFinalizerObject).IsAssignableFrom(type)
            || type.IsCOMObject
            || typeof(ComObject).IsAssignableFrom(type)
            || IsClassOfTheFrozenNamespace(type)
            || IsCoreLibComparer(type);
    }

    // A FrozenSet<T>, a FrozenDictionary<TKey, TValue> and every other class declared in
    // System.Collections.Frozen. A value type declared there (a struct, an enum, an enumerator of a frozen
    // collection) is not one of them, and neither is an array, whatever its element type: the runtime
    // reports the namespace of an array type as that of its innermost element type, so that namespace is
    // not read. The exclusion is made here only; the other rules are asked about arrays and value types as
    // before.
    private static bool IsClassOfTheFrozenNamespace(Type type)
    {
        return !type.IsArray
            && !type.IsValueType
            && string.Equals(type.Namespace, "System.Collections.Frozen", StringComparison.Ordinal);
    }

    private static bool IsCoreLibComparer(Type type)
    {
        return type.Assembly == typeof(object).Assembly
            && (
                typeof(IEqualityComparer).IsAssignableFrom(type)
                || typeof(IComparer).IsAssignableFrom(type)
                || typeof(IEqualityComparer<string>).IsAssignableFrom(type)
                || typeof(IComparer<string>).IsAssignableFrom(type)
            );
    }

    private static bool DerivesFromNamed(Type type, string fullName)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            if (string.Equals(current.FullName, fullName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool DerivesFromGeneric(Type type, Type genericDefinition)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == genericDefinition)
            {
                return true;
            }
        }

        return false;
    }
}
