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
                + "The two values differ, but their members cannot be listed. "
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
        // cannot be read from the other. Two sequences are still compared element by element.
        Type actualType = actual.GetType();
        Type expectedType = expected.GetType();
        if (actualType != expectedType && !(IsSequence(actual) && IsSequence(expected)))
        {
            return null;
        }

        // FieldInfo.GetValue boxes a pointer-typed field as a Pointer object, whose Equals compares addresses.
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

        // A shared type (see SharedTypes) is compared by reference, its members never read.
        if (actualType == expectedType && SharedTypes.IsShared(actualType))
        {
            return null;
        }

        // Values of different runtime types reach this point only as two non-string sequences (for example
        // ImmutableArray<T> and ArraySegment<T>, or a value-type sequence and an array); they are compared
        // element by element below, not field by field.
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

    // Null means the two sequences are different without comparing any element.
    private static Walk? StartSequences(IEnumerable actual, IEnumerable expected)
    {
        if (actual is IDictionary actualDictionary && expected is IDictionary expectedDictionary)
        {
            return new DictionaryWalk(actualDictionary, expectedDictionary);
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

        public override string ChildSegment => "." + SourceName(members[_index].Name);

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
