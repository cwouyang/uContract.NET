using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace uContract;

/// <summary>
///     The reflection walk behind <see cref="Contract.EnsureAssignable{T}" />: member discovery and the
///     deep comparison of two objects.
/// </summary>
internal static class MemberComparison
{
    private static readonly ConcurrentDictionary<Type, TypeMetadata> MetadataCache = new();

    // The members EnsureAssignable compares, and so the members the trimmer must preserve for its type argument.
    internal const DynamicallyAccessedMemberTypes ComparedMembers =
        DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicFields
        | DynamicallyAccessedMemberTypes.NonPublicFields;

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2070",
        Justification = "Callers (EnsureAssignable) are annotated with [RequiresUnreferencedCode]. Nested and element types are known only at run time; under Native AOT a type that exposes no members at all is reported by the \"no members visible\" rule."
    )]
    internal static TypeMetadata GetOrCacheMetadata(Type type)
    {
        return MetadataCache.GetOrAdd(
            type,
            t =>
            {
                // These binding flags must stay within what ComparedMembers preserves. Members are lost
                // silently under Native AOT if the flags are widened beyond ComparedMembers, or if the
                // constant is narrowed; widening the constant alone loses nothing.
                PropertyInfo[] properties = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                FieldInfo[] fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                List<MemberAccessor> members = properties
                    .Where(p => p.GetIndexParameters().Length == 0)
                    .Cast<MemberInfo>()
                    .Concat(fields)
                    .Select(m => new MemberAccessor(m))
                    .ToList();

                return new TypeMetadata { Members = members };
            }
        );
    }

    internal static InvalidOperationException CannotCompareNested(
        Type comparedType,
        string topLevelMember,
        HiddenMembers hidden
    )
    {
        string path = $"{comparedType.Name}.{topLevelMember}{hidden.PathBelowTopLevelMember}";

        return new InvalidOperationException(
            $"EnsureAssignable cannot compare {hidden.Type} (reached through '{path}'): "
                + "no properties or fields are visible to reflection under Native AOT. "
                + $"Ways out: list '{topLevelMember}' as assignable (patterns are regular expressions "
                + "matched against top-level member names, so use the plain member name); "
                + "if the type has members, preserve them, for example with "
                + "[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(X))] where X is that type; "
                + "or set DBC_POST=off (disables all postcondition checks)."
        );
    }

    // Under Native AOT a type whose members were not preserved reflects as having none, which
    // would make every comparison of it pass. System.Object genuinely has none (lock objects).
    internal static bool MembersAreHidden(Type type, TypeMetadata metadata)
    {
        return metadata.Members.Count == 0 && type != typeof(object) && !RuntimeFacts.IsDynamicCodeSupported;
    }

    // A compiler-generated backing field "<Name>k__BackingField" is shown as the property it backs.
    internal static string SourceName(string memberName)
    {
        const string backingFieldSuffix = ">k__BackingField";

        return memberName.StartsWith('<') && memberName.EndsWith(backingFieldSuffix, StringComparison.Ordinal)
            ? memberName[1..^backingFieldSuffix.Length]
            : memberName;
    }

    internal static bool IsAssignable(string fieldName, string[] patterns)
    {
        if (patterns == null || patterns.Length == 0)
        {
            return false;
        }

        return patterns.Any(pattern => Regex.IsMatch(fieldName, pattern, RegexOptions.None, TimeSpan.FromSeconds(1)));
    }

    // A false return with hidden set means "could not compare" (a type with no visible members
    // blocked the comparison), not "different". Callers must check hidden before reporting a difference.
    internal static bool AreEqual(object? actual, object? expected, ref HiddenMembers? hidden)
    {
        if (ReferenceEquals(actual, expected))
        {
            return true;
        }

        if (actual is null || expected is null)
        {
            return false;
        }

        // Values of different runtime types are unequal, and neither is read: the members of one type
        // cannot be read from the other. Two sequences are still compared element by element.
        Type actualType = actual.GetType();
        Type expectedType = expected.GetType();
        if (actualType != expectedType && !(IsSequence(actual) && IsSequence(expected)))
        {
            return false;
        }

        // FieldInfo.GetValue boxes a pointer-typed field as a Pointer object, whose Equals compares addresses.
        if (actual is Pointer or string)
        {
            return Equals(actual, expected);
        }

        if (actual is Delegate actualDelegate && expected is Delegate expectedDelegate)
        {
            return CompareDelegates(actualDelegate, expectedDelegate);
        }

        // A shared type (see SharedTypes) is compared by reference, its members never read.
        if (actualType == expectedType && SharedTypes.IsShared(actualType))
        {
            return false;
        }

        // Values of different runtime types reach this point only as two non-string sequences (for example
        // ImmutableArray<T> and ArraySegment<T>, or a value-type sequence and an array); they are compared
        // element by element below, not field by field.
        if (actualType.IsValueType && actualType == expectedType)
        {
            return CompareValues(actual, expected, actualType, ref hidden);
        }

        if (actual is IEnumerable actualEnum && expected is IEnumerable expectedEnum)
        {
            return CompareSequences(actualEnum, expectedEnum, ref hidden);
        }

        return CompareRecursively(actual, expected, ref hidden);
    }

    // A delegate is compared by the methods it calls, in order. Its targets are never compared or walked,
    // and a multicast delegate is never walked into: its fields would lead back to the delegates it holds.
    private static bool CompareDelegates(Delegate actual, Delegate expected)
    {
        if (actual.Equals(expected))
        {
            return true;
        }

        Delegate[] actualList = actual.GetInvocationList();
        Delegate[] expectedList = expected.GetInvocationList();
        if (actualList.Length != expectedList.Length)
        {
            return false;
        }

        for (int i = 0; i < actualList.Length; i++)
        {
            if (!HaveSameMethod(actualList[i], expectedList[i]))
            {
                return false;
            }
        }

        return true;
    }

    // A method that is unknown (null) or cannot be read (NotSupportedException, possible under Native AOT)
    // makes the two delegates unequal.
    private static bool HaveSameMethod(Delegate actual, Delegate expected)
    {
        try
        {
            MethodInfo? actualMethod = actual.Method;
            return actualMethod is not null && actualMethod == expected.Method;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private static bool CompareSequences(IEnumerable actual, IEnumerable expected, ref HiddenMembers? hidden)
    {
        if (actual is IDictionary actualDictionary && expected is IDictionary expectedDictionary)
        {
            return CompareDictionaries(actualDictionary, expectedDictionary, ref hidden);
        }

        // Two sequences of different runtime types: one that cannot be enumerated (a default
        // ImmutableArray<T>) is reported as changed rather than letting its exception escape.
        if (actual.GetType() != expected.GetType())
        {
            return TryEnumerate(actual, out object?[]? actualItems)
                && TryEnumerate(expected, out object?[]? expectedItems)
                && CompareCollections(actualItems, expectedItems, ref hidden);
        }

        return CompareCollections(actual, expected, ref hidden);
    }

    // A value type decides equality first; when it says "unequal", its elements (for ArraySegment<T> or
    // ImmutableArray<T>) or its fields are compared, because ValueType.Equals calls each field's own
    // Equals, which for most classes (List<T>, arrays) compares by reference.
    private static bool CompareValues(object actual, object expected, Type type, ref HiddenMembers? hidden)
    {
        if (Equals(actual, expected))
        {
            return true;
        }

        if (type.IsPrimitive || type.IsEnum)
        {
            return false;
        }

        // ArraySegment<T> and ImmutableArray<T> are compared by what they enumerate: the fields of
        // ArraySegment<T> include the whole backing array and the window's offset. One that cannot be
        // enumerated (a default ImmutableArray<T>) is compared by its fields instead. Any other value
        // type, even an enumerable one, is compared by its fields: they may hold more than it enumerates.
        if (
            IsComparedByElements(type)
            && actual is IEnumerable actualEnum
            && expected is IEnumerable expectedEnum
            && TryEnumerate(actualEnum, out object?[]? actualItems)
            && TryEnumerate(expectedEnum, out object?[]? expectedItems)
        )
        {
            return CompareCollections(actualItems, expectedItems, ref hidden);
        }

        // Reflection shows only the first element of an inline array, so it has no fully visible fields.
        if (type.IsDefined(typeof(InlineArrayAttribute), inherit: false))
        {
            return false;
        }

        bool hasFields = false;
        foreach (MemberAccessor field in GetOrCacheMetadata(type).Members.Where(m => m.IsField))
        {
            hasFields = true;
            if (!AreEqual(field.GetValue(actual), field.GetValue(expected), ref hidden))
            {
                hidden?.Prepend("." + SourceName(field.Name));
                return false;
            }
        }

        // A value type without visible fields has only its Equals to decide, and it said "unequal".
        return hasFields;
    }

    private static bool IsComparedByElements(Type type)
    {
        if (!type.IsGenericType)
        {
            return false;
        }

        Type definition = type.GetGenericTypeDefinition();
        return definition == typeof(ArraySegment<>) || definition == typeof(ImmutableArray<>);
    }

    private static bool TryEnumerate(IEnumerable sequence, [NotNullWhen(true)] out object?[]? items)
    {
        try
        {
            items = sequence.Cast<object?>().ToArray();
            return true;
        }
        catch (InvalidOperationException)
        {
            items = null;
            return false;
        }
    }

    private static bool IsSequence(object value)
    {
        return value is IEnumerable and not string;
    }

    internal static bool CompareCollections(IEnumerable actual, IEnumerable expected, ref HiddenMembers? hidden)
    {
        object?[] actualArray = actual.Cast<object?>().ToArray();
        object?[] expectedArray = expected.Cast<object?>().ToArray();

        if (actualArray.Length != expectedArray.Length)
        {
            return false;
        }

        for (int i = 0; i < actualArray.Length; i++)
        {
            object? actualItem = actualArray[i];
            object? expectedItem = expectedArray[i];

            if (actualItem == null && expectedItem == null)
            {
                continue;
            }

            if (actualItem == null || expectedItem == null)
            {
                return false;
            }

            if (!AreEqual(actualItem, expectedItem, ref hidden))
            {
                hidden?.Prepend("[]");
                return false;
            }
        }

        return true;
    }

    // A dictionary, even one compared with a dictionary of another type, is compared entry by entry, in
    // enumeration order, by the keys and values its own enumerator reports, not by the KeyValuePair or
    // DictionaryEntry it yields as a sequence: those differ between dictionary types, and their Equals
    // calls each value's Equals, which may ignore content that the comparison rules compare.
    private static bool CompareDictionaries(IDictionary actual, IDictionary expected, ref HiddenMembers? hidden)
    {
        IDictionaryEnumerator actualEntries = actual.GetEnumerator();
        IDictionaryEnumerator expectedEntries = expected.GetEnumerator();

        while (true)
        {
            bool hasActual = actualEntries.MoveNext();
            bool hasExpected = expectedEntries.MoveNext();

            if (hasActual != hasExpected)
            {
                return false;
            }

            if (!hasActual)
            {
                return true;
            }

            if (
                !AreEqual(actualEntries.Key, expectedEntries.Key, ref hidden)
                || !AreEqual(actualEntries.Value, expectedEntries.Value, ref hidden)
            )
            {
                hidden?.Prepend("[]");
                return false;
            }
        }
    }

    internal static bool CompareRecursively(object actual, object expected, ref HiddenMembers? hidden)
    {
        Type type = actual.GetType();
        TypeMetadata metadata = GetOrCacheMetadata(type);

        if (MembersAreHidden(type, metadata))
        {
            hidden = new HiddenMembers(type);
            return false;
        }

        foreach (MemberAccessor member in metadata.Members)
        {
            object? actualValue = member.GetValue(actual);
            object? expectedValue = member.GetValue(expected);

            if (!AreEqual(actualValue, expectedValue, ref hidden))
            {
                hidden?.Prepend("." + SourceName(member.Name));
                return false;
            }
        }

        return true;
    }

    // Created only when a comparison is blocked. The path is prepended one segment per level on
    // the way back up, so a comparison that is not blocked allocates nothing for it.
    internal sealed class HiddenMembers(Type type)
    {
        public Type Type { get; } = type;

        public string PathBelowTopLevelMember { get; private set; } = "";

        public void Prepend(string segment)
        {
            PathBelowTopLevelMember = segment + PathBelowTopLevelMember;
        }
    }
}
