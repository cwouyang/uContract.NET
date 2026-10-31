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
    private readonly ConcurrentDictionary<Type, TValue> _types = new();
    private readonly ConditionalWeakTable<Type, StrongBox<TValue>> _collectibleTypes = [];

    /// <summary>
    ///     Returns the value cached for <paramref name="type" />, created by <paramref name="create" /> on first
    ///     use. As with <see cref="ConcurrentDictionary{TKey,TValue}.GetOrAdd(TKey, Func{TKey, TValue})" />,
    ///     <paramref name="create" /> can run more than once for a type when threads race, and one value wins.
    /// </summary>
    internal TValue GetOrAdd(Type type, Func<Type, TValue> create)
    {
        return type.IsCollectible ? GetOrAddCollectible(type, create) : _types.GetOrAdd(type, create);
    }

    // Kept apart from GetOrAdd: the closure over create is then allocated only for a collectible type.
    private TValue GetOrAddCollectible(Type type, Func<Type, TValue> create)
    {
        return _collectibleTypes.GetValue(type, t => new StrongBox<TValue>(create(t))).Value!;
    }
}
