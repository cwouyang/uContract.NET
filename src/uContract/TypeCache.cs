using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace uContract;

/// <summary>
///     A thread-safe cache of one value per type that does not keep a collectible type alive. A type that is not
///     collectible is cached in a <see cref="ConcurrentDictionary{TKey,TValue}" />. A collectible type (see
///     <see cref="System.Reflection.MemberInfo.IsCollectible" />, which is true also for a constructed generic type
///     or an array type that is collectible only through a generic argument or an element type) is cached in a
///     <see cref="ConditionalWeakTable{TKey,TValue}" />, so the type and its assembly can be collected once no one
///     else refers to them.
/// </summary>
/// <typeparam name="TValue">The type of the cached values.</typeparam>
internal sealed class TypeCache<TValue>
{
    private readonly ConcurrentDictionary<Type, TValue> _nonCollectibleTypes = new();
    private readonly ConditionalWeakTable<Type, StrongBox<TValue>> _collectibleTypes = [];

    /// <summary>
    ///     Returns the value cached for <paramref name="type" />, created by <paramref name="create" /> on first
    ///     use. As with <see cref="ConcurrentDictionary{TKey,TValue}.GetOrAdd(TKey, Func{TKey, TValue})" />,
    ///     <paramref name="create" /> can run more than once for a type when threads race, and one value wins.
    /// </summary>
    internal TValue GetOrAdd(Type type, Func<Type, TValue> create)
    {
        return GetOrAdd(type, static (t, c) => c(t), create);
    }

    /// <summary>
    ///     As <see cref="GetOrAdd(Type, Func{Type, TValue})" />, with <paramref name="argument" /> passed to
    ///     <paramref name="create" />, so a caller that needs a value to create the cached one allocates no closure
    ///     on a hit.
    /// </summary>
    internal TValue GetOrAdd<TArg>(Type type, Func<Type, TArg, TValue> create, TArg argument)
    {
        // A hit proves the type is not collectible, so IsCollectible, a native call, is read only on a miss.
        if (_nonCollectibleTypes.TryGetValue(type, out TValue? value))
        {
            return value;
        }

        return type.IsCollectible
            ? GetOrAddCollectible(type, create, argument)
            : _nonCollectibleTypes.GetOrAdd(type, create, argument);
    }

    private TValue GetOrAddCollectible<TArg>(Type type, Func<Type, TArg, TValue> create, TArg argument)
    {
        return _collectibleTypes.TryGetValue(type, out StrongBox<TValue>? box)
            ? box.Value!
            : AddCollectible(type, create, argument);
    }

    // Kept apart from GetOrAddCollectible: the closure over create and argument is then allocated only when a
    // collectible type is not cached yet.
    private TValue AddCollectible<TArg>(Type type, Func<Type, TArg, TValue> create, TArg argument)
    {
        return _collectibleTypes.GetValue(type, t => new StrongBox<TValue>(create(t, argument))).Value!;
    }
}
