using System;
using System.Collections;
using System.Collections.Concurrent;
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
        return IsInSharedCategory(type)
            || Listed.Any(listed => listed.IsAssignableFrom(type))
            || ListedGenerics.Any(definition => DerivesFromGeneric(type, definition))
            || DerivesFromNamed(type, LockFullName);
    }

    // Handles (SafeHandle, CriticalHandle), COM objects, frozen collections and CoreLib comparers.
    // A category added here must be classified too: DescribeChangingShare names it when its state can
    // change, as it does for the handles and the COM objects; otherwise it is fixed, as the frozen
    // collections and the CoreLib comparers are. The same holds for a type added to a list above.
    private static bool IsInSharedCategory(Type type)
    {
        return typeof(CriticalFinalizerObject).IsAssignableFrom(type)
            || type.IsCOMObject
            || typeof(ComObject).IsAssignableFrom(type)
            || string.Equals(type.Namespace, "System.Collections.Frozen", StringComparison.Ordinal)
            || IsCoreLibComparer(type);
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
