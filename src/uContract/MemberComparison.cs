using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace uContract;

/// <summary>
///     The reflection walk behind <see cref="Contract.EnsureAssignable{T}" />: member discovery and the
///     deep comparison of two objects.
/// </summary>
internal static class MemberComparison
{
    private static readonly ConcurrentDictionary<Type, TypeMetadata> MetadataCache = new();

    // The public key tokens the assemblies of the .NET runtime and libraries are signed with: every token that
    // occurs in the .NET 8 shared frameworks (Microsoft.NETCore.App, Microsoft.AspNetCore.App and
    // Microsoft.WindowsDesktop.App).
    private static readonly string[] FrameworkPublicKeyTokens =
    [
        "7cec85d7bea7798e",
        "b03f5f7f11d50a3a",
        "cc7b13ffcd2ddd51",
        "adb9793829ddae60",
        "31bf3856ad364e35",
        "b77a5c561934e089",
    ];

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
        // These binding flags must stay within what ComparedMembers preserves. Members are lost silently under
        // Native AOT if the flags are widened beyond ComparedMembers, or if the constant is narrowed; widening
        // the constant alone loses nothing.
        return MetadataCache.GetOrAdd(
            type,
            t => CreateMetadata(t, t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        );
    }

    // The members of type, given the public instance properties reflection lists for it. Tests pass a listing
    // of their own to reproduce what Native AOT returns.
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2070",
        Justification = "Callers (EnsureAssignable) are annotated with [RequiresUnreferencedCode]. Nested and element types are known only at run time; under Native AOT a type that exposes no members at all is reported by the \"no members visible\" rule."
    )]
    internal static TypeMetadata CreateMetadata(Type type, PropertyInfo[] properties)
    {
        FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        List<MemberAccessor> members = properties
            .Where(p => p.GetIndexParameters().Length == 0 && !IsOverriddenByAnother(p, properties))
            .Select(p => new MemberAccessor(p, ExistingGetter(p)))
            .Concat(fields.Select(f => new MemberAccessor(f)))
            .ToList();

        // GetFields does not return the private fields declared on a base class; they are added after the
        // members above, which alone decide the "no members visible" rule.
        int ownMemberCount = members.Count;
        members.AddRange(BaseClassPrivateFields(type, properties).Select(f => new MemberAccessor(f)));

        QualifyCollidingNames(members);

        return new TypeMetadata { Members = members, OwnMemberCount = ownMemberCount };
    }

    // Members of one raw name (ordinal), counted after the declarations of one virtual property are merged,
    // are named with their declaring type: its Name, or, for the whole group, its FullName when two of them
    // share a Name. A property and its own backing field ("Id", "<Id>k__BackingField") do not collide.
    private static void QualifyCollidingNames(List<MemberAccessor> members)
    {
        foreach (IGrouping<string, MemberAccessor> sameName in members.GroupBy(m => m.RawName, StringComparer.Ordinal))
        {
            MemberAccessor[] collision = [.. sameName];
            if (collision.Length < 2)
            {
                continue;
            }

            bool typeNamesRepeat =
                collision.Select(m => m.DeclaringType.Name).Distinct(StringComparer.Ordinal).Count() < collision.Length;
            foreach (MemberAccessor member in collision)
            {
                Type declaringType = member.DeclaringType;
                member.Qualify(typeNamesRepeat ? declaringType.FullName ?? declaringType.Name : declaringType.Name);
            }
        }
    }

    // Under Native AOT, GetProperties returns both a setter-only override and the declarations below it of the
    // same virtual property, a getter-only override among them; they are one member, listed as the override.
    private static bool IsOverriddenByAnother(PropertyInfo property, PropertyInfo[] properties)
    {
        return properties.Any(other =>
            !ReferenceEquals(other, property)
            && string.Equals(other.Name, property.Name, StringComparison.Ordinal)
            && other.DeclaringType is { } otherDeclaringType
            && property.DeclaringType is { } declaringType
            && otherDeclaringType.IsSubclassOf(declaringType)
            && AreOneVirtualProperty(other, property)
        );
    }

    // Two declarations of one name are one virtual property when their accessors go back to the same first
    // declaration, whichever accessors each of them declares: in A { virtual P get; set; } <- B { override P
    // get; } <- C { override P set; }, C.P and B.P declare no accessor in common. A property hidden with "new"
    // goes back to a declaration of its own.
    private static bool AreOneVirtualProperty(PropertyInfo first, PropertyInfo second)
    {
        return FirstDeclarations(first).Any(declaringType => FirstDeclarations(second).Contains(declaringType));
    }

    // The types that declare the base definitions of the accessors of property.
    private static IEnumerable<Type?> FirstDeclarations(PropertyInfo property)
    {
        return property.GetAccessors(nonPublic: true).Select(accessor => accessor.GetBaseDefinition().DeclaringType);
    }

    // The getter a property is read with. Through a derived type, reflection shows neither a private getter
    // declared on a base class nor the getter that a setter-only override inherits, so it is looked up on the
    // declaring type of the property and then on its base classes, framework classes included: the getter of
    // the nearest declaration of the same virtual property (see AreOneVirtualProperty) that has one. Null when
    // there is none (write-only, or trimmed away).
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2075",
        Justification = "Callers (EnsureAssignable) are annotated with [RequiresUnreferencedCode]. Under Native AOT a getter that the trimmer removed is not found, and reading the property throws the \"no get method\" exception."
    )]
    private static MethodInfo? ExistingGetter(PropertyInfo property)
    {
        if (property.GetMethod is not null)
        {
            return property.GetMethod;
        }

        for (Type? type = property.DeclaringType; type is not null; type = type.BaseType)
        {
            PropertyInfo? declaration = type.GetProperties(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly
                )
                .FirstOrDefault(declared =>
                    declared.GetMethod is not null
                    && string.Equals(declared.Name, property.Name, StringComparison.Ordinal)
                    && AreOneVirtualProperty(property, declared)
                );
            if (declaration is not null)
            {
                return declaration.GetMethod;
            }
        }

        return null;
    }

    // The private instance fields declared on the base classes of type, up to the first framework class. A
    // backing field is left out when one of the compared properties is, or overrides, the property it backs.
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2075",
        Justification = "Callers (EnsureAssignable) are annotated with [RequiresUnreferencedCode]. Under Native AOT a private field of a base class that the trimmer hides from reflection is not compared."
    )]
    private static IEnumerable<FieldInfo> BaseClassPrivateFields(Type type, PropertyInfo[] properties)
    {
        for (
            Type? baseType = type.BaseType;
            baseType is not null && !IsFrameworkClass(baseType);
            baseType = baseType.BaseType
        )
        {
            foreach (
                FieldInfo field in baseType.GetFields(
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly
                )
            )
            {
                if (field.IsPrivate && !BacksAComparedProperty(field, baseType, properties))
                {
                    yield return field;
                }
            }
        }
    }

    // A class of the .NET runtime or libraries, told by the public key token its assembly is signed with, not
    // by its namespace: the walk up the base classes stops at it, and it and the classes above it add nothing.
    private static bool IsFrameworkClass(Type type)
    {
        return IsFrameworkPublicKeyToken(type.Assembly.GetName().GetPublicKeyToken());
    }

    // True for a public key token that the assemblies of the .NET runtime and libraries are signed with.
    internal static bool IsFrameworkPublicKeyToken(byte[]? token)
    {
        return token is { Length: > 0 }
            && FrameworkPublicKeyTokens.Contains(Convert.ToHexString(token), StringComparer.OrdinalIgnoreCase);
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2070",
        Justification = "Callers (EnsureAssignable) are annotated with [RequiresUnreferencedCode]. Under Native AOT a property of a base class that the trimmer hides from reflection matches no compared property, so its backing field is compared."
    )]
    private static bool BacksAComparedProperty(FieldInfo field, Type baseType, PropertyInfo[] properties)
    {
        string name = SourceName(field.Name);
        if (string.Equals(name, field.Name, StringComparison.Ordinal))
        {
            return false;
        }

        return baseType
            .GetProperties(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly
            )
            .Any(declared =>
                string.Equals(declared.Name, name, StringComparison.Ordinal)
                && properties.Any(compared =>
                    string.Equals(compared.Name, name, StringComparison.Ordinal) && IsOrOverrides(compared, declared)
                )
            );
    }

    // Through a derived type, reflection reports an override as declared on that type, so the declaring type
    // alone cannot tell whether compared overrides declared. An override, direct or through further overrides,
    // shares the base definition of its accessors with the property it overrides; a property hidden with "new"
    // does not.
    private static bool IsOrOverrides(PropertyInfo compared, PropertyInfo declared)
    {
        return HaveSameBaseDefinition(compared.GetMethod, declared.GetMethod)
            || HaveSameBaseDefinition(compared.SetMethod, declared.SetMethod);
    }

    private static bool HaveSameBaseDefinition(MethodInfo? compared, MethodInfo? declared)
    {
        if (compared is null || declared is null)
        {
            return false;
        }

        MethodInfo comparedBase = compared.GetBaseDefinition();
        MethodInfo declaredBase = declared.GetBaseDefinition();
        return comparedBase.DeclaringType == declaredBase.DeclaringType
            && string.Equals(comparedBase.Name, declaredBase.Name, StringComparison.Ordinal);
    }

    internal static InvalidOperationException CannotCompareNested(
        Type comparedType,
        MemberAccessor topLevelMember,
        HiddenMembers hidden
    )
    {
        string path = $"{comparedType.Name}.{topLevelMember.PathName}{hidden.PathBelowTopLevelMember}";

        // Patterns are matched against Name, not against the path form; for a qualified member Name holds a
        // declaring type, whose FullName may hold "+" (nested type) or "[[...]]" (closed generic), so the advised
        // pattern is escaped.
        string advice = topLevelMember.IsQualified
            ? $"list '{Regex.Escape(topLevelMember.Name)}' as assignable (patterns are regular expressions "
                + "matched against top-level member names; another member has the same name, so this one carries "
                + "its declaring type, escaped as a regular expression); "
            : $"list '{topLevelMember.PathName}' as assignable (patterns are regular expressions "
                + "matched against top-level member names, so use the plain member name); ";

        return new InvalidOperationException(
            $"EnsureAssignable cannot compare {hidden.Type} (reached through '{path}'): "
                + "no properties or fields are visible to reflection under Native AOT. "
                + "Their Equals reports them unequal (without an Equals override this only means they are different instances), and their members cannot be listed. "
                + "Ways out: "
                + advice
                + "if the type has members, preserve them, for example with "
                + "[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(X))] where X is that type; "
                + "or set DBC_POST=off (disables all postcondition checks)."
        );
    }

    // The comparison of a nullable value type, of a FrozenSet<T> or a FrozenDictionary<TKey, TValue>, or of an
    // array type or System.Array, as a whole met a class whose members are not visible. For a nullable value
    // type, wholeType is the underlying type; for the others it is T itself. The path starts with the name of
    // wholeType, except below an array type or System.Array, where it starts with the parameter name: the
    // name of an array type holds brackets of its own ("X[]"), which would read as one more element.
    internal static InvalidOperationException CannotCompareBelowWhole(Type wholeType, HiddenMembers hidden)
    {
        string start = IsArrayTypeOrSystemArray(wholeType) ? "actual" : wholeType.Name;
        string path = $"{start}{hidden.PathBelowTopLevelMember}";

        return new InvalidOperationException(
            $"EnsureAssignable cannot compare {hidden.Type} (reached through '{path}'): "
                + "no properties or fields are visible to reflection under Native AOT. "
                + "Their Equals reports them unequal (without an Equals override this only means they are different instances), and their members cannot be listed. "
                + "Ways out: if the type has members, preserve them, for example with "
                + "[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(X))] where X is that type; "
                + "or set DBC_POST=off (disables all postcondition checks)."
        );
    }

    // The declared types whose two values EnsureAssignable compares as a whole because they are arrays: an
    // array type of any rank and element type, and System.Array itself. No other type that can hold an array
    // (object, an interface that arrays implement) is one of them.
    internal static bool IsArrayTypeOrSystemArray(Type type)
    {
        return type.IsArray || type == typeof(Array);
    }

    // Under Native AOT a type whose members were not preserved reflects as having none, which
    // would make every comparison of it pass. System.Object genuinely has none (lock objects). The rule is
    // decided before the private fields of base classes are added, also when those fields are visible.
    internal static bool MembersAreHidden(Type type, TypeMetadata metadata)
    {
        return metadata.OwnMemberCount == 0 && type != typeof(object) && !RuntimeFacts.IsDynamicCodeSupported;
    }

    // A compiler-generated backing field "<Name>k__BackingField" is shown as the property it backs.
    internal static string SourceName(string memberName)
    {
        const string backingFieldSuffix = ">k__BackingField";

        return memberName.StartsWith('<') && memberName.EndsWith(backingFieldSuffix, StringComparison.Ordinal)
            ? memberName[1..^backingFieldSuffix.Length]
            : memberName;
    }

    internal static bool IsAssignable(MemberAccessor member, string[] patterns)
    {
        if (patterns == null || patterns.Length == 0)
        {
            return false;
        }

        return patterns.Any(pattern => Regex.IsMatch(member.Name, pattern, RegexOptions.None, TimeSpan.FromSeconds(1)));
    }

    // A false return with context.Hidden set means "could not compare" (a type with no visible members
    // blocked the comparison), not "different". Callers must check it before reporting a difference.
    internal static bool AreEqual(object? actual, object? expected, ComparisonContext context)
    {
        Walk? walk = Start(actual, expected, context, out bool equal);
        return walk is null ? equal : Run(walk, context);
    }

    // The pairs inside a pair are compared depth-first, in member order, on an explicit stack of walks rather
    // than by recursion, so a graph of any depth is compared on a thread of any stack size. A difference ends
    // every enclosing walk, and each of them prepends the segment it was comparing to the path of a blocked
    // comparison on the way back up.
    private static bool Run(Walk root, ComparisonContext context)
    {
        Stack<Walk> walks = new();
        walks.Push(root);
        bool equal = true;

        while (true)
        {
            Walk walk = walks.Peek();
            if (equal)
            {
                Step step = walk.Next();
                if (step == Step.Child)
                {
                    Walk? child = Start(walk.ChildActual, walk.ChildExpected, context, out equal);
                    if (child is not null)
                    {
                        walks.Push(child);
                        equal = true;
                    }

                    continue;
                }

                equal = step == Step.Equal;
            }
            else
            {
                context.Hidden?.Prepend(walk.ChildSegment);
            }

            walks.Pop();
            walk.End(context, equal);
            if (walks.Count == 0)
            {
                return equal;
            }
        }
    }

    // Decides a pair that needs no walk into it (equal is the outcome), or returns the walk that compares it.
    private static Walk? Start(object? actual, object? expected, ComparisonContext context, out bool equal)
    {
        equal = ReferenceEquals(actual, expected);
        if (equal || actual is null || expected is null)
        {
            return null;
        }

        // Values of different runtime types are unequal, and neither is read: the members of one type
        // cannot be read from the other. Two sequences are still compared element by element; two arrays
        // only when they have the same rank, and the same length and lower bound in every dimension (see
        // StartReferences).
        Type actualType = actual.GetType();
        Type expectedType = expected.GetType();
        if (actualType != expectedType && !(IsSequence(actual) && IsSequence(expected)))
        {
            return null;
        }

        // FieldInfo.GetValue boxes a pointer-typed field as a Pointer object, whose Equals compares addresses.
        // An array of pointers or of function pointers is compared by address too (see StartSequences).
        if (actual is Pointer or string)
        {
            equal = Equals(actual, expected);
            return null;
        }

        if (actual is Delegate actualDelegate && expected is Delegate expectedDelegate)
        {
            equal = CompareDelegates(actualDelegate, expectedDelegate);
            return null;
        }

        // A shared type (see SharedTypes) is compared by reference, its members never read, except a
        // FrozenSet<T> or a FrozenDictionary<TKey, TValue>: two of them are compared element by element, in
        // enumeration order, below.
        if (
            actualType == expectedType
            && SharedTypes.IsShared(actualType)
            && !SharedTypes.IsFrozenSetOrDictionary(actualType)
        )
        {
            return null;
        }

        // Values of different runtime types reach this point only as two non-string sequences (for example
        // ImmutableArray<T> and ArraySegment<T>, or a value-type sequence and an array); they are compared
        // element by element below, not field by field, and two arrays among them only when they have the
        // same rank, and the same length and lower bound in every dimension. An array of pointers or of
        // function pointers that has elements is the exception: it is decided without walking a pair of
        // elements (see StartSequences).
        if (actualType.IsValueType && actualType == expectedType)
        {
            return StartValues(actual, expected, actualType, out equal);
        }

        return StartReferences(actual, expected, context, out equal);
    }

    // A pair of objects already found equal or different in this call is not walked again, and a pair
    // reached again while it is being compared (through a cycle) counts as equal; see ComparisonContext.
    // Value types of one runtime type are compared by StartValues and never reach here, but two value-type
    // sequences of different runtime types (for example an ImmutableArray<T> and an ArraySegment<T>) do, as
    // boxes. Such boxes are tracked like any other pair of references, which is sound: a box read from an
    // object- or IEnumerable<T>-typed field is the same reference on every read and holds the same content.
    private static Walk? StartReferences(object actual, object expected, ComparisonContext context, out bool equal)
    {
        ReferencePair pair = new(actual, expected);
        if (context.TryGetSettled(pair, out equal))
        {
            return null;
        }

        // Decided before any element of either array is read, and after the lookup above. The lookup knows
        // only pairs that passed this test, except the top-level pair of a T that is compared by its members,
        // which is entered without it: when the two values of such a call are arrays (T is ICollection, say)
        // and a member leads back to them (SyncRoot), that member still counts as equal, whatever their rank,
        // lengths and lower bounds. Two arrays passed as an array type or System.Array are compared as a whole
        // and come through here like any other pair.
        if (ArraysDifferInRankLengthOrLowerBound(actual, expected))
        {
            equal = false;
            return null;
        }

        ComparisonContext.Frame frame = context.Enter(pair);
        bool equalWithoutWalk = false;
        Walk? walk =
            actual is IEnumerable actualEnum && expected is IEnumerable expectedEnum
                ? StartSequences(actualEnum, expectedEnum)
                : StartMembers(actual, expected, context, out equalWithoutWalk);

        // No walk means the pair was decided (different, equal by Equals, or not comparable) before any of its contents.
        if (walk is null)
        {
            equal = equalWithoutWalk;
            context.Leave(pair, frame, equal);
            return null;
        }

        walk.Track(pair, frame);
        return walk;
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

    // Null means the two sequences are different without walking a pair of elements. A walk that holds no
    // pair ends as equal: it is returned for two sequences without elements, and for two arrays of pointers
    // or of function pointers that hold the same addresses, which are equal without walking a pair of
    // elements.
    private static Walk? StartSequences(IEnumerable actual, IEnumerable expected)
    {
        // Two dictionaries of different counts are different before any entry is read: under Native AOT an
        // entry may hold a value that cannot be compared, and a dictionary that only grew or shrank is
        // still reported as changed.
        if (actual is IDictionary actualDictionary && expected is IDictionary expectedDictionary)
        {
            return actualDictionary.Count == expectedDictionary.Count
                ? new DictionaryWalk(actualDictionary, expectedDictionary)
                : null;
        }

        // Two arrays of pointers or of function pointers are compared by address, whatever the pointers point
        // to; equal addresses leave nothing to walk. Such an array that has elements is different from anything
        // that is not an array of pointers or of function pointers, also from an object[] of boxed pointers.
        bool actualHoldsAddresses = TryGetAddresses(actual, out ReadOnlySpan<nint> actualAddresses);
        bool expectedHoldsAddresses = TryGetAddresses(expected, out ReadOnlySpan<nint> expectedAddresses);
        if (actualHoldsAddresses && expectedHoldsAddresses)
        {
            // Both arrays have the same rank, and the same length and lower bound in every dimension (see
            // StartReferences), so they hold the same number of addresses in the same order.
            return actualAddresses.SequenceEqual(expectedAddresses) ? new CollectionWalk([], []) : null;
        }

        if (actualHoldsAddresses || expectedHoldsAddresses)
        {
            return null;
        }

        // Two sequences of different runtime types: one that cannot be enumerated (a default
        // ImmutableArray<T>) is reported as changed rather than letting its exception escape.
        if (actual.GetType() != expected.GetType())
        {
            return
                TryEnumerate(actual, out object?[]? actualItems) && TryEnumerate(expected, out object?[]? expectedItems)
                ? new CollectionWalk(actualItems, expectedItems)
                : null;
        }

        return new CollectionWalk(actual.Cast<object?>().ToArray(), expected.Cast<object?>().ToArray());
    }

    // A value type decides equality first; when it says "unequal", its elements (for ArraySegment<T> or
    // ImmutableArray<T>) or its fields are compared, because ValueType.Equals calls each field's own
    // Equals, which for most classes (List<T>, arrays) compares by reference.
    private static Walk? StartValues(object actual, object expected, Type type, out bool equal)
    {
        equal = Equals(actual, expected);
        if (equal || type.IsPrimitive || type.IsEnum)
        {
            return null;
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
            return new CollectionWalk(actualItems, expectedItems);
        }

        // Reflection shows only the first element of an inline array, so it has no fully visible fields.
        if (type.IsDefined(typeof(InlineArrayAttribute), inherit: false))
        {
            return null;
        }

        // A value type without visible fields has only its Equals to decide, and it said "unequal".
        return new MemberWalk(GetOrCacheMetadata(type).Members, actual, expected, fieldsOnly: true);
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

    // Two arrays, of one runtime type or of two, are equal only when they have the same rank, and the same
    // length and lower bound in every dimension. An array of any rank enumerates as one sequence, so without
    // this a 1x2 and a 2x1 array of the same elements would compare as equal. No element is read here.
    private static bool ArraysDifferInRankLengthOrLowerBound(object actual, object expected)
    {
        if (actual is not Array actualArray || expected is not Array expectedArray)
        {
            return false;
        }

        if (actualArray.Rank != expectedArray.Rank)
        {
            return true;
        }

        for (int dimension = 0; dimension < actualArray.Rank; dimension++)
        {
            if (
                actualArray.GetLength(dimension) != expectedArray.GetLength(dimension)
                || actualArray.GetLowerBound(dimension) != expectedArray.GetLowerBound(dimension)
            )
            {
                return true;
            }
        }

        return false;
    }

    // True for an array of pointers or of function pointers, of any rank, that has at least one element:
    // addresses are then its elements as the addresses they hold, in the order of the array's memory. An
    // address is read as a number and never dereferenced. Such an array cannot be enumerated: the runtime
    // throws NotSupportedException for an element. With no element there is nothing to compare by address,
    // and the array is compared as any other sequence is.
    // The memory of the array is read as native integers, which is in bounds only when each element is one
    // native integer, as a pointer or a function pointer is. So the type is tested here, before anything is
    // read and before the number of elements is asked for, and never left to a caller.
    private static bool TryGetAddresses(object value, out ReadOnlySpan<nint> addresses)
    {
        addresses = default;
        if (
            value is not Array array
            || !SharedTypes.IsArrayOfPointersOrFunctionPointers(array.GetType())
            || array.LongLength == 0
        )
        {
            return false;
        }

        ref byte first = ref MemoryMarshal.GetArrayDataReference(array);
        addresses = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<byte, nint>(ref first), array.Length);
        return true;
    }

    private static bool IsSequence(object value)
    {
        return value is IEnumerable and not string;
    }

    // Null means the pair needs no member walk. A type with no visible members is left to its own Equals;
    // when that says "unequal" the pair could not be compared (context.Hidden is set).
    private static MemberWalk? StartMembers(
        object actual,
        object expected,
        ComparisonContext context,
        out bool equalWithoutWalk
    )
    {
        Type type = actual.GetType();
        TypeMetadata metadata = GetOrCacheMetadata(type);
        equalWithoutWalk = false;

        if (MembersAreHidden(type, metadata))
        {
            equalWithoutWalk = actual.Equals(expected);
            if (!equalWithoutWalk)
            {
                // CannotCompareNested's wording relies on Equals having returned false here.
                context.Hidden = new HiddenMembers(type);
            }

            return null;
        }

        return new MemberWalk(metadata.Members, actual, expected, fieldsOnly: false);
    }

    private enum Step
    {
        // ChildActual and ChildExpected hold the next pair to compare.
        Child,

        // The walk ended with every pair equal.
        Equal,

        // The walk ended with a difference found without comparing a pair.
        Different,
    }

    // The comparison of one pair's contents, one inner pair at a time. A pair of references being walked is
    // tracked in the ComparisonContext, and its outcome recorded there when the walk ends.
    private abstract class Walk
    {
        private ReferencePair _pair;
        private ComparisonContext.Frame _frame;
        private bool _tracked;

        public object? ChildActual { get; private set; }

        public object? ChildExpected { get; private set; }

        // The path segment of the current child, prepended when the child could not be compared.
        public abstract string ChildSegment { get; }

        public abstract Step Next();

        public void Track(ReferencePair pair, ComparisonContext.Frame frame)
        {
            _pair = pair;
            _frame = frame;
            _tracked = true;
        }

        public void End(ComparisonContext context, bool equal)
        {
            if (_tracked)
            {
                context.Leave(_pair, _frame, equal);
            }
        }

        protected Step Child(object? actual, object? expected)
        {
            ChildActual = actual;
            ChildExpected = expected;
            return Step.Child;
        }
    }

    // Members in order, reading the actual value before the expected one. A value type is compared by its
    // fields only, and one without any is different (its Equals said "unequal").
    private sealed class MemberWalk(List<MemberAccessor> members, object actual, object expected, bool fieldsOnly)
        : Walk
    {
        private int _index = -1;
        private bool _comparedAny;

        public override string ChildSegment => "." + members[_index].PathName;

        public override Step Next()
        {
            while (++_index < members.Count)
            {
                MemberAccessor member = members[_index];
                if (fieldsOnly && !member.IsField)
                {
                    continue;
                }

                _comparedAny = true;
                return Child(member.GetValue(actual), member.GetValue(expected));
            }

            return _comparedAny || !fieldsOnly ? Step.Equal : Step.Different;
        }
    }

    private sealed class CollectionWalk(object?[] actual, object?[] expected) : Walk
    {
        private int _index = -1;

        public override string ChildSegment => "[]";

        public override Step Next()
        {
            if (actual.Length != expected.Length)
            {
                return Step.Different;
            }

            _index++;
            return _index < actual.Length ? Child(actual[_index], expected[_index]) : Step.Equal;
        }
    }

    // A dictionary, even one compared with a dictionary of another type, is compared entry by entry, in
    // enumeration order, by the keys and values its own enumerator reports, not by the KeyValuePair or
    // DictionaryEntry it yields as a sequence: those differ between dictionary types, and their Equals
    // calls each value's Equals, which may ignore content that the comparison rules compare.
    private sealed class DictionaryWalk(IDictionary actual, IDictionary expected) : Walk
    {
        private readonly IDictionaryEnumerator _actualEntries = actual.GetEnumerator();
        private readonly IDictionaryEnumerator _expectedEntries = expected.GetEnumerator();
        private bool _keyCompared;

        public override string ChildSegment => "[]";

        public override Step Next()
        {
            if (_keyCompared)
            {
                _keyCompared = false;
                return Child(_actualEntries.Value, _expectedEntries.Value);
            }

            bool hasActual = _actualEntries.MoveNext();
            bool hasExpected = _expectedEntries.MoveNext();
            if (hasActual != hasExpected)
            {
                return Step.Different;
            }

            if (!hasActual)
            {
                return Step.Equal;
            }

            _keyCompared = true;
            return Child(_actualEntries.Key, _expectedEntries.Key);
        }
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
