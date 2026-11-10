using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;

namespace uContract;

/// <summary>
///     The reflection walk behind <see cref="Contract.EnsureAssignable{T}" />: member discovery and the
///     deep comparison of two objects.
/// </summary>
internal static class MemberComparison
{
    private static readonly TypeCache<TypeMetadata> MetadataCache = new();
    private static readonly TypeCache<SequenceRule> SequenceRuleCache = new();
    private static readonly TypeCache<bool> NoDeclaredMemberCache = new();
    private static readonly TypeCache<InlineArrayShape> InlineArrayShapeCache = new();
    private static readonly TypeCache<bool> InlineArrayCache = new();
    private static readonly TypeCache<ValueRoutes> ValueRoutesCache = new();
    private static readonly TypeCache<bool> UserEqualsCache = new();
    private static readonly TypeCache<ClassEquals> ClassEqualsCache = new();

    private static readonly Type[] TupleDefinitions =
    [
        typeof(Tuple<>),
        typeof(Tuple<,>),
        typeof(Tuple<,,>),
        typeof(Tuple<,,,>),
        typeof(Tuple<,,,,>),
        typeof(Tuple<,,,,,>),
        typeof(Tuple<,,,,,,>),
        typeof(Tuple<,,,,,,,>),
    ];

    private static readonly Type[] ValueTupleDefinitions =
    [
        typeof(ValueTuple<>),
        typeof(ValueTuple<,>),
        typeof(ValueTuple<,,>),
        typeof(ValueTuple<,,,>),
        typeof(ValueTuple<,,,,>),
        typeof(ValueTuple<,,,,,>),
        typeof(ValueTuple<,,,,,,>),
        typeof(ValueTuple<,,,,,,,>),
    ];

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
        Justification = "Callers (EnsureAssignable) are annotated with [RequiresUnreferencedCode]. Nested and element types are known only at run time; under Native AOT a type that exposes no members at all is reported by the \"no members visible\" rule, and a typed Equals the trimmer hides from reflection is not found, so a record's hand-written one is treated as generated."
    )]
    internal static TypeMetadata GetOrCacheMetadata(Type type)
    {
        // These binding flags must stay within what ComparedMembers preserves. Members are lost silently under
        // Native AOT if the flags are widened beyond ComparedMembers, or if the constant is narrowed; widening
        // the constant alone loses nothing. The typed Equals is not a compared member (see
        // HasHandWrittenTypedEquals).
        return MetadataCache.GetOrAdd(
            type,
            t =>
            {
                TypeMetadata metadata = CreateMetadata(t, t.GetProperties(BindingFlags.Public | BindingFlags.Instance));
                metadata.HasHandWrittenTypedEquals =
                    (
                        t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                            .Any(method =>
                                string.Equals(method.Name, nameof(Equals), StringComparison.Ordinal)
                                && method.GetParameters() is [{ } parameter]
                                && parameter.ParameterType == t
                                && !method.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
                            ) && !IsAnonymousType(t)
                    )
                    || (
                        t.BaseType is { } baseType
                        && !IsFrameworkClass(baseType)
                        && GetOrCacheMetadata(baseType).HasHandWrittenTypedEquals
                    );
                return metadata;
            }
        );
    }

    // The members the top level compares for a runtime type other than T. When T is a framework class (other
    // than System.Object) that the runtime type derives from, the private fields of the base classes are
    // listed up to and including T, which the cached members stop short of; they are listed afresh on each
    // call. Otherwise these are the cached members of the runtime type; when T is another class that the
    // runtime type derives from, the private fields declared on T also count as visible, in a copy of the
    // cached metadata. In both cases [DynamicallyAccessedMembers] on T preserves those fields under Native AOT.
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2070",
        Justification = "Callers (EnsureAssignable) are annotated with [RequiresUnreferencedCode]. The runtime type is known only at run time; under Native AOT a type that exposes no members at all is reported by the \"no members visible\" rule."
    )]
    internal static TypeMetadata GetTopLevelMetadata(Type runtimeType, Type declaredType)
    {
        if (declaredType == runtimeType || declaredType == typeof(object) || !declaredType.IsClass)
        {
            return GetOrCacheMetadata(runtimeType);
        }

        if (!IsFrameworkClass(declaredType))
        {
            // The cached members already hold the private fields declared on T, but leave them out of
            // OwnMemberCount; they are counted here, in a copy, so that the cache stays independent of T.
            TypeMetadata cached = GetOrCacheMetadata(runtimeType);
            return new TypeMetadata
            {
                Members = cached.Members,
                OwnMemberCount =
                    cached.OwnMemberCount
                    + cached.Members.Skip(cached.OwnMemberCount).Count(m => m.DeclaringType == declaredType),
            };
        }

        return CreateMetadata(
            runtimeType,
            runtimeType.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            privateFieldsUpTo: declaredType
        );
    }

    // The members of type, given the public instance properties reflection lists for it. Tests pass a listing
    // of their own to reproduce what Native AOT returns. The private fields of base classes are listed up to the
    // first framework class or, when privateFieldsUpTo is given, up to and including that class.
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2070",
        Justification = "Callers (EnsureAssignable) are annotated with [RequiresUnreferencedCode]. Nested and element types are known only at run time; under Native AOT a type that exposes no members at all is reported by the \"no members visible\" rule."
    )]
    internal static TypeMetadata CreateMetadata(Type type, PropertyInfo[] properties, Type? privateFieldsUpTo = null)
    {
        FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        // Indexers are not compared. A property of a byref-like type cannot be read through reflection, so it is
        // not compared either; the fields behind it are. Nor does it count for the "no members visible" rule: under
        // Native AOT those fields may be hidden (see MembersAreHidden).
        PropertyInfo[] comparedProperties =
        [
            .. properties.Where(p =>
                p.GetIndexParameters().Length == 0
                && !p.PropertyType.IsByRefLike
                && !IsOverriddenByAnother(p, properties)
            ),
        ];
        List<MemberAccessor> members = comparedProperties
            .Select(p => new MemberAccessor(p, ExistingGetter(p)))
            .Concat(fields.Select(f => new MemberAccessor(f)))
            .ToList();

        // GetFields does not return the private fields declared on a base class; they are added after the
        // members above, which decide the "no members visible" rule together with the private fields declared
        // on privateFieldsUpTo: [DynamicallyAccessedMembers] on T preserves those under Native AOT.
        int ownMemberCount = members.Count;
        List<FieldInfo> baseClassPrivateFields = BaseClassPrivateFields(type, comparedProperties, privateFieldsUpTo)
            .ToList();
        ownMemberCount += baseClassPrivateFields.Count(f => f.DeclaringType == privateFieldsUpTo);
        members.AddRange(baseClassPrivateFields.Select(f => new MemberAccessor(f)));

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
    private static IEnumerable<FieldInfo> BaseClassPrivateFields(
        Type type,
        PropertyInfo[] comparedProperties,
        Type? privateFieldsUpTo
    )
    {
        for (
            Type? baseType = type.BaseType;
            baseType is not null
                && (
                    !IsFrameworkClass(baseType)
                    || (privateFieldsUpTo is not null && privateFieldsUpTo.IsAssignableFrom(baseType))
                );
            baseType = baseType.BaseType
        )
        {
            foreach (
                FieldInfo field in baseType.GetFields(
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly
                )
            )
            {
                if (field.IsPrivate && !BacksAComparedProperty(field, baseType, comparedProperties))
                {
                    yield return field;
                }
            }
        }
    }

    // A class of the .NET runtime or libraries, told by the public key token its assembly is signed with, not
    // by its namespace: the walk up the base classes stops at it, and it and the classes above it add nothing.
    internal static bool IsFrameworkClass(Type type)
    {
        return IsFrameworkPublicKeyToken(type.Assembly.GetName().GetPublicKeyToken());
    }

    // How a sequence of the runtime type is compared: when its class chain holds a framework class below
    // System.Object, by its elements and by OwnMembers, the members of its member list declared on a type that
    // is not a framework class; otherwise, as a class of yours that implements IEnumerable itself, by its
    // members alone. A compiler-generated sequence of yours (see IsCompilerGeneratedSequence) is compared by its
    // elements alone: its fields are the state of an iteration or the storage of a collection expression.
    internal static SequenceRule GetSequenceRule(Type runtimeType)
    {
        return SequenceRuleCache.GetOrAdd(runtimeType, CreateSequenceRule);
    }

    private static SequenceRule CreateSequenceRule(Type type)
    {
        bool hasFrameworkClass = HasFrameworkClassBelowObject(type);
        if (!hasFrameworkClass && IsCompilerGeneratedSequence(type))
        {
            return new SequenceRule(HasFrameworkClass: false, Array.Empty<MemberAccessor>())
            {
                IsCompilerGenerated = true,
            };
        }

        return new SequenceRule(
            hasFrameworkClass,
            GetOrCacheMetadata(type).Members.Where(m => !IsFrameworkClass(m.DeclaringType)).ToList().AsReadOnly()
        )
        {
            IsCollection = IsCollection(type),
            DeclaresByRefLikeProperty = DeclaresByRefLikeProperty(type),
        };
    }

    // A property of a byref-like type (see ByRefLikeProperties) declared on a type that is not a framework class.
    private static bool DeclaresByRefLikeProperty(Type type)
    {
        return ByRefLikeProperties(type)
            .Any(p => p.DeclaringType is { } declaringType && !IsFrameworkClass(declaringType));
    }

    // A class the compiler generated for a sequence: a yield iterator (it implements IEnumerator and is
    // compiler-generated as the messages define it, CSharpTypeName.IsCompilerGenerated: it, or a type in its
    // declaring chain, has a name that starts with "<>" or is marked [CompilerGenerated] with a '<' or a '$' in its
    // name, which C# cannot write:
    // <Method>d__N, VB$StateMachine_…, or the unmarked <<Method>g__Local|0>d of a local function that captures a
    // variable, nested in its marked display class <>c__DisplayClass…, measured on SDK 8.0.204), or a class
    // whose metadata name starts with "<>" that implements IEnumerable (the class of a collection expression,
    // <>z__ReadOnlyArray<T> or <>z__ReadOnlyList<T>, which the 8.0.2xx SDK emits without [CompilerGenerated],
    // measured; one element is <>z__ReadOnlyArray<T> on 8.0.2xx and <>z__ReadOnlySingleElementList<T> on 8.0.427,
    // measured: the class depends on the compiler, the "<>" prefix does not). A [CompilerGenerated] class with a name C# can write (a source generator's, even one that is its
    // own enumerator) and a file class (<File>F…__Name) are classes of yours.
    private static bool IsCompilerGeneratedSequence(Type type)
    {
        return (CSharpTypeName.IsCompilerGenerated(type) && typeof(IEnumerator).IsAssignableFrom(type))
            || (CSharpTypeName.HasCompilerPrefix(type) && typeof(IEnumerable).IsAssignableFrom(type));
    }

    // A collection holds its elements: it implements ICollection, ICollection<T> (the grouping GroupBy returns)
    // or IReadOnlyCollection<T>. Another sequence (a LINQ query, a consuming enumerable) may compute, consume or
    // wait for its elements when enumerated.
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2070",
        Justification = "Callers (EnsureAssignable) are annotated with [RequiresUnreferencedCode]. The runtime type is known only at run time; an interface the trimmer removed is one the type is not used as, and the type is then treated as not a collection, which makes the top level throw rather than enumerate it."
    )]
    private static bool IsCollection(Type type)
    {
        return typeof(ICollection).IsAssignableFrom(type)
            || type.GetInterfaces()
                .Any(i =>
                    i.IsGenericType
                    && (
                        i.GetGenericTypeDefinition() == typeof(IReadOnlyCollection<>)
                        || i.GetGenericTypeDefinition() == typeof(ICollection<>)
                    )
                );
    }

    // A sequence built on a framework class that is not a collection (see IsCollection).
    internal static bool IsNonCollectionSequence(Type runtimeType)
    {
        SequenceRule rule = GetSequenceRule(runtimeType);
        return rule.HasFrameworkClass && !rule.IsCollection;
    }

    // A sequence that is compared by its elements alone, also against a sequence of another runtime type: its
    // class chain holds a framework class, and it declares no member on a class of yours (a property of a byref-like
    // type counts, although it is not compared).
    internal static bool IsComparedByElementsOnly(Type runtimeType)
    {
        SequenceRule rule = GetSequenceRule(runtimeType);
        return rule.IsComparedByElements && rule.OwnMembers.Count == 0 && !rule.DeclaresByRefLikeProperty;
    }

    private static bool HasFrameworkClassBelowObject(Type type)
    {
        for (
            Type? classType = type;
            classType is not null && classType != typeof(object);
            classType = classType.BaseType
        )
        {
            if (IsFrameworkClass(classType))
            {
                return true;
            }
        }

        return false;
    }

    // Compares the elements of two sequences, as a nested sequence is compared, in the given context. A false
    // return with context.Hidden set means "could not compare", as for AreEqual.
    internal static bool ElementsAreEqual(IEnumerable actual, IEnumerable expected, ComparisonContext context)
    {
        Walk? walk = StartSequences(actual, expected);
        if (walk is null)
        {
            return false;
        }

        walk.Uncopied = context.UncopiedInTopLevel;
        return Run(walk, context);
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
    private static bool BacksAComparedProperty(FieldInfo field, Type baseType, PropertyInfo[] comparedProperties)
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
                && comparedProperties.Any(compared =>
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

    // The ways out for a type that C# cannot name in typeof (see CSharpTypeName.CanBeWritten), matched to why it
    // cannot. firstWayOut is empty, or a way out that ends with "; ". A compiler-generated type is replaced by a
    // value of a type of yours, except an enumerator (from a yield method that returns only IEnumerator or
    // IEnumerator<T>), whose method can return a sequence instead. A type of yours or of .NET with a
    // compiler-generated generic argument (List<anonymous type>) keeps its type and takes another argument: for a
    // sequence, an element type of yours. A type with file classes of more than one file in its generic arguments
    // and no compiler-generated type takes other generic arguments.
    internal static string WaysOutForAnUnnamedType(Type type, string firstWayOut = "")
    {
        if (!CSharpTypeName.HoldsCompilerGeneratedType(type))
        {
            return "The type has file classes of more than one file in its generic arguments, and C# names a file "
                + "class only inside the file that declares it, so no one file can name the type in typeof. Ways out: "
                + firstWayOut
                + "use types that are not file classes, or file classes of one file, as its generic arguments; or set "
                + "DBC_POST=off (disables all postcondition checks).";
        }

        bool isSequence = typeof(IEnumerable).IsAssignableFrom(type);
        if (!CSharpTypeName.IsCompilerGenerated(type))
        {
            Type[] arguments = type.GetGenericArguments();
            bool isAnArgument = arguments.Any(CSharpTypeName.IsCompilerGenerated);
            string where = isAnArgument ? "among" : "inside";
            string replaceArgument =
                !isAnArgument ? "use a type of yours in place of the compiler-generated one; "
                : isSequence && arguments.Length == 1
                    ? "use an element type of yours instead of the compiler-generated one; "
                : "use a type of yours as its generic argument instead; ";
            return $"The type has a compiler-generated type {where} its generic arguments and cannot be named in typeof. "
                + "Ways out: "
                + firstWayOut
                + replaceArgument
                + "or set DBC_POST=off (disables all postcondition checks).";
        }

        string replaceIt =
            typeof(IEnumerator).IsAssignableFrom(type) && !isSequence
                ? "return IEnumerable<T> from the yield method that builds it, or use a value of a type of yours instead; "
                : "use a value of a type of yours instead; ";
        return "The type is compiler-generated and cannot be named in typeof. Ways out: "
            + firstWayOut
            + replaceIt
            + "or set DBC_POST=off (disables all postcondition checks).";
    }

    // The sentence of a "cannot compare" message that says what reflection shows of a type under Native AOT.
    // onlyByRefLikePropertiesVisible: the rule "no members visible" fired (see MembersAreHidden) for a type that
    // lists properties of a byref-like type. Its properties are visible, so they were preserved; those properties
    // are not compared and do not count for the rule.
    internal static string WhatIsVisible(bool onlyByRefLikePropertiesVisible)
    {
        return onlyByRefLikePropertiesVisible
            ? "under Native AOT the only properties or fields visible to reflection are properties of a byref-like "
                + "type (Span<T>, ReadOnlySpan<T>, a ref struct), which are not compared and do not count as members. "
            : "no properties or fields are visible to reflection under Native AOT. ";
    }

    // What preserving the type would bring: its fields, when its properties are visible already.
    private static string PreserveWhat(bool onlyByRefLikePropertiesVisible)
    {
        return onlyByRefLikePropertiesVisible ? "if the type has fields" : "if the type has members";
    }

    // Below the top level the Equals of the type decides before its members are listed; an override that compares
    // the state of a type whose only visible properties are byref-like lets two equal values pass.
    private static string OverrideEqualsWayOut(bool onlyByRefLikePropertiesVisible)
    {
        return onlyByRefLikePropertiesVisible ? "override Equals so that it compares the state; " : "";
    }

    internal static bool ListsByRefLikeProperty(Type type)
    {
        return ByRefLikeProperties(type).Any();
    }

    // The public instance properties of a byref-like type that reflection lists for type, indexers left out.
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2070",
        Justification = "Callers (EnsureAssignable) are annotated with [RequiresUnreferencedCode]. The runtime type is known only at run time; under Native AOT a property the trimmer removed is not listed, and the type is then treated as one without it."
    )]
    private static IEnumerable<PropertyInfo> ByRefLikeProperties(Type type)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0 && p.PropertyType.IsByRefLike);
    }

    internal static InvalidOperationException CannotCompareNested(
        Type comparedType,
        MemberAccessor topLevelMember,
        HiddenMembers hidden
    )
    {
        string path = $"{comparedType.Name}.{topLevelMember.PathName}{hidden.PathBelowTopLevelMember}";

        // Patterns are matched against raw names and source names (see IsAssignable), so the advice names the
        // member without its qualifier, and a qualified member shares that name with another member, which it excuses too.
        string sharedName = topLevelMember.IsQualified ? "; it also excuses the other members of that name" : "";
        string listAsAssignable =
            $"list '{SourceName(topLevelMember.RawName)}' as assignable (patterns are regular expressions "
            + $"matched against top-level member names, so use the plain member name{sharedName}); ";
        if (hidden.ElementsNeedDynamicCode is { } elementType)
        {
            return CannotReadStructElements(hidden.Type, elementType, path, listAsAssignable);
        }

        if (hidden.ElementsNotCopied is { } notCopied)
        {
            return CannotCompareUncopiedElements(hidden.Type, notCopied, path, listAsAssignable);
        }

        if (hidden.ElementFieldHidden)
        {
            return CannotReadHiddenElements(hidden.Type, path, listAsAssignable);
        }

        string cannotList =
            $"EnsureAssignable cannot compare {hidden.Type} (reached through '{path}'): "
            + WhatIsVisible(hidden.OnlyByRefLikePropertiesVisible)
            + "Their Equals reports them unequal (without an Equals override this only means they are different instances), and their members cannot be listed. ";
        if (!CSharpTypeName.CanBeWritten(hidden.Type))
        {
            return new InvalidOperationException(cannotList + WaysOutForAnUnnamedType(hidden.Type, listAsAssignable));
        }

        return new InvalidOperationException(
            cannotList
                + "Ways out: "
                + OverrideEqualsWayOut(hidden.OnlyByRefLikePropertiesVisible)
                + listAsAssignable
                + PreserveWhat(hidden.OnlyByRefLikePropertiesVisible)
                + ", preserve them, for example with "
                + $"[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof({CSharpTypeName.Of(hidden.Type)}))] on a method that runs{CSharpTypeName.WhereItCanBeWritten(hidden.Type)}; "
                + "or set DBC_POST=off (disables all postcondition checks)."
        );
    }

    // The comparison of a nullable value type, of a FrozenSet<T> or a FrozenDictionary<TKey, TValue>, or of an
    // array type or System.Array, as a whole met a class whose members are not visible. For a nullable value
    // type, wholeType is the underlying type; for the others it is T itself, or the runtime type of the two
    // values when that decided (a frozen collection by its public type). The path starts with the name of
    // wholeType, except below an array type or System.Array, where it starts with the parameter name: the
    // name of an array type holds brackets of its own ("X[]"), which would read as one more element.
    internal static InvalidOperationException CannotCompareBelowWhole(Type wholeType, HiddenMembers hidden)
    {
        string start = IsArrayTypeOrSystemArray(wholeType) ? "actual" : wholeType.Name;
        string path = $"{start}{hidden.PathBelowTopLevelMember}";
        if (hidden.ElementsNeedDynamicCode is { } elementType)
        {
            return CannotReadStructElements(hidden.Type, elementType, path);
        }

        if (hidden.ElementsNotCopied is { } notCopied)
        {
            return CannotCompareUncopiedElements(hidden.Type, notCopied, path);
        }

        if (hidden.ElementFieldHidden)
        {
            return CannotReadHiddenElements(hidden.Type, path);
        }

        string cannotList =
            $"EnsureAssignable cannot compare {hidden.Type} (reached through '{path}'): "
            + WhatIsVisible(hidden.OnlyByRefLikePropertiesVisible)
            + "Their Equals reports them unequal (without an Equals override this only means they are different instances), and their members cannot be listed. ";
        if (!CSharpTypeName.CanBeWritten(hidden.Type))
        {
            return new InvalidOperationException(cannotList + WaysOutForAnUnnamedType(hidden.Type));
        }

        return new InvalidOperationException(
            cannotList
                + "Ways out: "
                + OverrideEqualsWayOut(hidden.OnlyByRefLikePropertiesVisible)
                + PreserveWhat(hidden.OnlyByRefLikePropertiesVisible)
                + ", preserve them, for example with "
                + $"[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof({CSharpTypeName.Of(hidden.Type)}))] on a method that runs{CSharpTypeName.WhereItCanBeWritten(hidden.Type)}; "
                + "or set DBC_POST=off (disables all postcondition checks)."
        );
    }

    // The comparison met an inline array of struct elements, reached through path, that this runtime reads only with
    // dynamic code (see StructElements.Read). firstWayOut is empty, or a way out that ends with "; ". Preserving the
    // type is not offered: the elements stay unreadable whatever is preserved, so no way out names a type in C#.
    private static InvalidOperationException CannotReadStructElements(
        Type inlineArrayType,
        Type elementType,
        string path,
        string firstWayOut = ""
    )
    {
        return new InvalidOperationException(
            $"EnsureAssignable cannot compare {inlineArrayType} (reached through '{path}'): it is an inline array of "
                + $"{elementType}, a struct, whose elements this runtime reads only with dynamic code, and dynamic code "
                + "is not supported here (Native AOT). Ways out: "
                + firstWayOut
                + "run on the .NET 9 runtime or later, which reads struct elements without dynamic code; or set "
                + "DBC_POST=off (disables all postcondition checks)."
        );
    }

    // The comparison met an inline array, reached through path, whose element field reflection does not list (under
    // Native AOT, a type that nothing preserves). firstWayOut is empty, or a way out that ends with "; ".
    private static InvalidOperationException CannotReadHiddenElements(
        Type inlineArrayType,
        string path,
        string firstWayOut = ""
    )
    {
        string cannotRead =
            $"EnsureAssignable cannot compare {inlineArrayType} (reached through '{path}'): it is an inline array whose "
            + "element field is not visible to reflection under Native AOT, so its element type is unknown and its "
            + "elements cannot be read. ";
        if (!CSharpTypeName.CanBeWritten(inlineArrayType))
        {
            return new InvalidOperationException(cannotRead + WaysOutForAnUnnamedType(inlineArrayType, firstWayOut));
        }

        return new InvalidOperationException(
            cannotRead
                + "Ways out: "
                + firstWayOut
                + "preserve it, for example with "
                + $"[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof({CSharpTypeName.Of(inlineArrayType)}))] on a method that runs{CSharpTypeName.WhereItCanBeWritten(inlineArrayType)}; "
                + "or set DBC_POST=off (disables all postcondition checks)."
        );
    }

    // The comparison met an inline array of struct elements, reached through path, that the Old copy being compared
    // could not copy (see UncopiedInlineArrays). firstWayOut is empty, or a way out that ends with "; ". Preserving the
    // type is not offered: the elements stay uncopied whatever is preserved.
    private static InvalidOperationException CannotCompareUncopiedElements(
        Type inlineArrayType,
        Type elementType,
        string path,
        string firstWayOut = ""
    )
    {
        return new InvalidOperationException(
            $"EnsureAssignable cannot compare {inlineArrayType} (reached through '{path}'): it is an inline array of "
                + $"{elementType}, a struct, whose elements Contract.Old could not copy: this runtime writes struct "
                + "elements only with dynamic code, and dynamic code is not supported here (Native AOT), so the copy "
                + "shares what they refer to with the original. Ways out: "
                + firstWayOut
                + (firstWayOut.Length == 0 ? "set" : "or set")
                + " DBC_POST=off (disables all postcondition checks)."
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
    // decided on OwnMemberCount, which leaves out the private fields of base classes, also when those fields
    // are visible, except the private fields declared on a class T at the top level (see GetTopLevelMetadata). A
    // byref-like property is not counted: it is not compared, and the fields behind it may be hidden.
    internal static bool MembersAreHidden(Type type, TypeMetadata metadata)
    {
        return metadata.OwnMemberCount == 0 && type != typeof(object) && !RuntimeFacts.IsDynamicCodeSupported;
    }

    // Under Native AOT a type that nothing preserves shows no declared field, property, method or constructor to
    // reflection, not even a constructor (measured), while the members it inherits may still show. A nested type
    // or an event it declares does not count: a nested type keeps metadata of its own (measured). Never true when
    // dynamic code is supported.
    internal static bool ShowsNoDeclaredMember(Type type)
    {
        if (RuntimeFacts.IsDynamicCodeSupported)
        {
            return false;
        }

        return RuntimeFacts.ShowsNoDeclaredMemberOverride?.Invoke(type)
            ?? NoDeclaredMemberCache.GetOrAdd(type, ListsNoDeclaredMember);
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2070",
        Justification = "Callers (EnsureAssignable) are annotated with [RequiresUnreferencedCode]. The members are only counted, never used: a type that the trimmer kept no member of is the one this detects."
    )]
    private static bool ListsNoDeclaredMember(Type type)
    {
        return !type.GetMembers(
                BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.Instance
                    | BindingFlags.Static
                    | BindingFlags.DeclaredOnly
            )
            .Any(member =>
                member.MemberType
                    is MemberTypes.Field
                        or MemberTypes.Property
                        or MemberTypes.Method
                        or MemberTypes.Constructor
            );
    }

    // A compiler-generated backing field "<Name>k__BackingField" is shown as the property it backs.
    internal static string SourceName(string memberName)
    {
        const string backingFieldSuffix = ">k__BackingField";

        return memberName.StartsWith('<') && memberName.EndsWith(backingFieldSuffix, StringComparison.Ordinal)
            ? memberName[1..^backingFieldSuffix.Length]
            : memberName;
    }

    // A pattern excuses a member when it matches its raw name or the name SourceName gives it (the property name
    // of a compiler-generated backing field). The declaring type of a qualified name takes no part, so a pattern
    // excuses every member of the names it matches.
    internal static bool IsAssignable(MemberAccessor member, string[] patterns)
    {
        if (patterns == null || patterns.Length == 0)
        {
            return false;
        }

        string sourceName = SourceName(member.RawName);
        bool isBackingField = !string.Equals(sourceName, member.RawName, StringComparison.Ordinal);

        return patterns.Any(pattern =>
            Regex.IsMatch(member.RawName, pattern, RegexOptions.None, TimeSpan.FromSeconds(1))
            || (isBackingField && Regex.IsMatch(sourceName, pattern, RegexOptions.None, TimeSpan.FromSeconds(1)))
        );
    }

    // A false return with context.Hidden set means "could not compare" (a type with no visible members
    // blocked the comparison), not "different". Callers must check it before reporting a difference.
    internal static bool AreEqual(object? actual, object? expected, ComparisonContext context)
    {
        Walk? walk = StartHeld(actual, expected, context.UncopiedInTopLevel, context, out bool equal);
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
                    Walk? child = StartHeld(walk.ChildActual, walk.ChildExpected, walk.Uncopied, context, out equal);
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

    // Start, for a pair held by an object whose Old copy could not copy the inline-array types uncopiedInHolder (null
    // when there are none): a pair of objects tells for itself whether it belongs to such a copy (see
    // UncopiedInlineArrays), and a pair of values read from a struct is held where that struct is held. The walk
    // returned passes this on to the pairs it holds.
    private static Walk? StartHeld(
        object? actual,
        object? expected,
        Func<Type, bool>? uncopiedInHolder,
        ComparisonContext context,
        out bool equal
    )
    {
        context.Uncopied =
            UncopiedInlineArrays.Of(actual)
            ?? UncopiedInlineArrays.Of(expected)
            ?? (actual is ValueType ? uncopiedInHolder : null);
        Walk? walk = Start(actual, expected, context, out equal);
        if (walk is not null)
        {
            walk.Uncopied = context.Uncopied;
        }

        return walk;
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
        // cannot be read from the other. Two sequences are still compared element by element when neither
        // declares members on a class of yours (see IsComparedByElementsOnly); two arrays only when they have
        // the same rank, and the same length and lower bound in every dimension (see StartReferences).
        Type actualType = actual.GetType();
        Type expectedType = expected.GetType();
        if (actualType != expectedType && !AreSequencesComparedByElementsOnly(actual, expected))
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
            return StartValues(actual, expected, actualType, context, out equal);
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
            actual is IEnumerable actualEnum
            && expected is IEnumerable expectedEnum
            && (actual.GetType() != expected.GetType() || GetSequenceRule(actual.GetType()).IsComparedByElements)
                ? StartSequencesOfSeenClasses(actualEnum, expectedEnum, context, out equalWithoutWalk)
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

    // Two sequences compared by their elements, unless a class of yours in the class chain of either shows no
    // declared member (see ShowsNoDeclaredMember): they are then left to their own Equals, as a class whose
    // members are hidden is (see StartMembers).
    private static Walk? StartSequencesOfSeenClasses(
        IEnumerable actual,
        IEnumerable expected,
        ComparisonContext context,
        out bool equalWithoutWalk
    )
    {
        equalWithoutWalk = false;
        Type? unseen = UnseenClassOfYours(actual.GetType()) ?? UnseenClassOfYours(expected.GetType());
        if (unseen is null)
        {
            return StartSequencesAndOwnMembers(actual, expected);
        }

        equalWithoutWalk = actual.Equals(expected);
        if (!equalWithoutWalk)
        {
            context.Hidden = new HiddenMembers(unseen);
        }

        return null;
    }

    // The first class in the class chain of type, below System.Object, that is not a framework class and shows no
    // declared member; none for a compiler-generated sequence of yours.
    private static Type? UnseenClassOfYours(Type type)
    {
        // A compiler-generated sequence of yours is compared by its elements alone, which enumerating needs no
        // metadata for.
        if (GetSequenceRule(type).IsCompilerGenerated)
        {
            return null;
        }

        for (
            Type? classType = type;
            classType is not null && classType != typeof(object);
            classType = classType.BaseType
        )
        {
            if (ShowsNoDeclaredMember(classType) && !IsFrameworkClass(classType))
            {
                return classType;
            }
        }

        return null;
    }

    // Two sequences of one runtime type that declares members on a class of yours are compared by their
    // elements and then by those members; any other two sequences by their elements alone.
    private static Walk? StartSequencesAndOwnMembers(IEnumerable actual, IEnumerable expected)
    {
        Walk? elements = StartSequences(actual, expected);
        Type type = actual.GetType();
        if (elements is null || type != expected.GetType())
        {
            return elements;
        }

        IReadOnlyList<MemberAccessor> ownMembers = GetSequenceRule(type).OwnMembers;
        return ownMembers.Count == 0
            ? elements
            : new ElementsThenMembersWalk(elements, new MemberWalk(ownMembers, actual, expected, fieldsOnly: false));
    }

    // Null means the two sequences are different without walking a pair of elements. A walk that holds no
    // pair ends as equal: it is returned for two sequences without elements, for two arrays of pointers or
    // of function pointers that hold the same addresses, and for two arrays of one primitive or enum element
    // type that hold equal elements (see TryCompareInPlace), which are equal without walking a pair of
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

        // Two arrays of one primitive or enum element type (see TryCompareInPlace) are compared in place, without
        // boxing an element; equal elements leave nothing to walk.
        if (TryCompareInPlace(actual, expected, out bool equalInPlace))
        {
            return equalInPlace ? new CollectionWalk([], []) : null;
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
    // Equals, which for most classes (List<T>, arrays) compares by reference. An inline array is compared by
    // its elements instead (see StartInlineArray), and a struct without a hand-written Equals whose built-in
    // Equals would reach an inline array's on either side (see NeedsFieldComparison) by its fields: the built-in
    // Equals of an inline array sees only element 0, and the .NET 10 runtime throws for it. Before any Equals is
    // asked, a pair that belongs to an Old copy which left struct elements uncopied (context.Uncopied) is refused
    // when the struct holds such an inline array by value, through structs of any depth, whatever the Equals of the
    // struct and of those structs (see FindUncopiedHeld): an Equals would find the shared elements equal. For a
    // struct T, context.Uncopied covers what T holds by value also when the values did not come from Old: a struct
    // copy carries no record (see Contract._UncopiedHeldByValue).
    private static Walk? StartValues(
        object actual,
        object expected,
        Type type,
        ComparisonContext context,
        out bool equal
    )
    {
        // A primitive or an enum is decided by its Equals alone; it is never an inline array or a holder.
        if (type.IsPrimitive || type.IsEnum)
        {
            equal = Equals(actual, expected);
            return null;
        }

        if (IsInlineArray(type))
        {
            return StartInlineArray(actual, expected, type, context, out equal);
        }

        if (IsStructComparedByFieldsWithoutEquals(actual, expected, type))
        {
            equal = false;
            return new MemberWalk(GetOrCacheMetadata(type).Members, actual, expected, fieldsOnly: true);
        }

        // A struct that holds, by value, an inline array whose struct elements an Old copy left uncopied is refused
        // before its Equals is asked, whatever that Equals is: it would find the shared elements equal (see
        // FindUncopiedHeld). A struct without a hand-written Equals that is a holder was sent to its fields above, and
        // its inline array is refused there (see StartInlineArray). For a struct T this holds also for values that
        // did not come from Old (see Contract._UncopiedHeldByValue).
        if (
            context.Uncopied is { } isUncopied
            && (FindUncopiedHeld(actual, type, isUncopied) ?? FindUncopiedHeld(expected, type, isUncopied)) is { } held
        )
        {
            context.Hidden = held;
            equal = false;
            return null;
        }

        equal = Equals(actual, expected);
        if (equal)
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

        // A value type without visible fields has only its Equals to decide, and it said "unequal".
        return new MemberWalk(GetOrCacheMetadata(type).Members, actual, expected, fieldsOnly: true);
    }

    // True when two values of type, a struct that is not an inline array, are compared by their fields without
    // asking its Equals: it has no hand-written Equals, and its built-in Equals would reach an inline array's on
    // either side (see NeedsFieldComparison).
    internal static bool IsComparedByFieldsWithoutEquals(object actual, object expected, Type type)
    {
        return !type.IsPrimitive
            && !type.IsEnum
            && !IsInlineArray(type)
            && IsStructComparedByFieldsWithoutEquals(actual, expected, type);
    }

    // As IsComparedByFieldsWithoutEquals, for a type already known to be neither a primitive, an enum nor an inline
    // array.
    private static bool IsStructComparedByFieldsWithoutEquals(object actual, object expected, Type type)
    {
        return !HasUserEquals(actual, type)
            && (NeedsFieldComparison(actual, type) || NeedsFieldComparison(expected, type));
    }

    // Two values of one inline-array type: one whose struct elements the Old copy being compared could not copy
    // (context.Uncopied; for an inline array that a struct T holds by value, also when the values did not come from
    // Old) is refused first, before any Equals is asked (context.Hidden is set); then a hand-written
    // Equals that says "equal" decides; otherwise the n elements are compared pairwise, in index order, by the rules
    // for two elements of an array. The built-in ValueType.Equals is not asked: it sees only element 0, and the .NET
    // 10 runtime throws for it. An inline array of n elements of E is laid out as n consecutive values of E, so the
    // elements are read from the memory of the box. Primitive, enum, pointer and function-pointer elements are
    // compared in place (see TryCompareInPlace); reference and struct elements are walked, a struct element boxed (see
    // StructElements.Read).
    // Struct elements that this runtime cannot read, and elements of a type reflection does not list (Native AOT),
    // leave the pair uncompared (context.Hidden is set), equal or not.
    private static CollectionWalk? StartInlineArray(
        object actual,
        object expected,
        Type type,
        ComparisonContext context,
        out bool equal
    )
    {
        equal = false;
        InlineArrayShape shape = InlineArrayShapeOf(type);

        // Elements an Old copy left shared would look equal to a hand-written Equals, so the refusal comes first.
        if (shape is { ElementType: { } uncopiedElementType } && context.Uncopied?.Invoke(type) == true)
        {
            context.Hidden = new HiddenMembers(type) { ElementsNotCopied = uncopiedElementType };
            return null;
        }

        if (HasUserEquals(actual, type) && Equals(actual, expected))
        {
            equal = true;
            return null;
        }

        if (shape is not { ElementType: { } elementType })
        {
            context.Hidden = new HiddenMembers(type) { ElementFieldHidden = true };
            return null;
        }

        ref byte actualData = ref Unsafe.As<StrongBox<byte>>(actual).Value;
        ref byte expectedData = ref Unsafe.As<StrongBox<byte>>(expected).Value;
        if (TryCompareInPlace(elementType, ref actualData, ref expectedData, shape.Length, out equal))
        {
            return null;
        }

        if (
            ReadElements(elementType, ref actualData, shape.Length) is not { } actualElements
            || ReadElements(elementType, ref expectedData, shape.Length) is not { } expectedElements
        )
        {
            context.Hidden = new HiddenMembers(type) { ElementsNeedDynamicCode = elementType };
            return null;
        }

        return new CollectionWalk(actualElements, expectedElements);
    }

    // The length elements of elementType at data: a reference element as the reference it holds, a struct element
    // boxed (see StructElements.Read). Null when this runtime cannot read struct elements.
    private static object?[]? ReadElements(Type elementType, ref byte data, int length)
    {
        return elementType.IsValueType
            ? StructElements.Read(elementType, ref data, length)
            : MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<byte, object?>(ref data), length).ToArray();
    }

    // True when type overrides Equals(object) by hand. A delegate bound to the virtual object.Equals on the box
    // resolves to the method the box dispatches to: an override declared on type, or else the built-in
    // ValueType.Equals, also when type hides it with an Equals declared with "new". An override marked
    // [CompilerGenerated] is not written by hand, unless it is a record's that calls a typed Equals written by hand.
    private static bool OverridesEquals(object box, Type type)
    {
        Func<object?, bool> equals = box.Equals;
        return equals.Method.DeclaringType == type
            && (
                !equals.Method.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
                || GetOrCacheMetadata(type).HasHandWrittenTypedEquals
            );
    }

    // True when the Equals type dispatches to is written by hand (see OverridesEquals), a fact of the type that box,
    // a value of type, serves to ask. The Equals of a System.ValueTuple<…> only calls the Equals of each item, so
    // it counts as built-in. (A Nullable<T> is never boxed: a field declared Nullable<T> holds a boxed T or null.)
    private static bool HasUserEquals(object box, Type type)
    {
        return UserEqualsCache.GetOrAdd(
            type,
            static (t, b) =>
                OverridesEquals(b, t)
                && !(t.IsGenericType && ValueTupleDefinitions.Contains(t.GetGenericTypeDefinition())),
            box
        );
    }

    // True when the built-in Equals of a value of type, a struct without a hand-written Equals that is not an inline
    // array, would reach the built-in Equals of an inline array: the struct is a holder (see HolderState), or a
    // field declared object, an interface or ValueType holds, on this side, an inline array or a struct that needs
    // this itself (see PushHeldValues). Which fields to look at is a fact of the type (see CreateValueRoutes).
    // The held values still to look at wait in pending, not on the call stack, so a long chain of held values (a
    // linked list of records) does not overflow it. looked holds the held values already looked at in this look,
    // created on the first one, so a value that refers back to itself (through one box or object or more) is looked
    // at once: a cycle alone needs no field comparison.
    private static bool NeedsFieldComparison(object value, Type type)
    {
        Stack<object>? pending = null;
        HashSet<object>? looked = null;
        if (NeedsFieldComparison(value, type, ref pending))
        {
            return true;
        }

        while (pending is { Count: > 0 })
        {
            if (NeedsFieldComparisonAsHeld(pending.Pop(), ref pending, ref looked))
            {
                return true;
            }
        }

        return false;
    }

    // True when value is a holder; otherwise the run-time values it holds are put in pending (see PushHeldValues).
    private static bool NeedsFieldComparison(object value, Type type, ref Stack<object>? pending)
    {
        ValueRoutes routes = ValueRoutesCache.GetOrAdd(type, CreateValueRoutes);
        if (routes.IsEmpty)
        {
            return false;
        }

        if (HolderState(routes, value) == Holder.Yes)
        {
            return true;
        }

        PushHeldValues(routes, value, ref pending);
        return false;
    }

    // A struct is a holder when a field declared (a Nullable<> of) an inline array, or a field declared (a
    // Nullable<> of) a holder struct without a hand-written Equals, belongs to it. That is a fact of the type, cached
    // once known; the value only serves to ask the nested structs whether their Equals is written by hand. A
    // Nullable<> field that is null leaves it unknown for now: the built-in Equals never reaches its Equals then.
    private static Holder HolderState(ValueRoutes routes, object value)
    {
        Holder state = routes.Holder;
        if (state != Holder.Unknown)
        {
            return state;
        }

        state = routes.InlineArrays.Length > 0 ? Holder.Yes : Holder.No;
        foreach (MemberAccessor field in routes.Nested)
        {
            if (state == Holder.Yes)
            {
                break;
            }

            if (field.GetValue(value) is not { } nested)
            {
                state = Holder.Unknown;
                continue;
            }

            Type nestedType = nested.GetType();
            if (HasUserEquals(nested, nestedType))
            {
                continue;
            }

            Holder nestedState = HolderState(ValueRoutesCache.GetOrAdd(nestedType, CreateValueRoutes), nested);
            if (nestedState != Holder.No)
            {
                state = nestedState;
            }
        }

        if (state != Holder.Unknown)
        {
            routes.Holder = state;
        }

        return state;
    }

    // The inline array that value, a struct, holds by value through the fields the holder rule lists (see
    // CreateValueRoutes: a field declared an inline array, or a nested struct with routes of its own, followed to any
    // depth whether or not it has a hand-written Equals, unlike HolderState, which stops at one: its Equals, say one
    // that compares what the shared elements refer to, would find them equal too) and whose type isUncopied tells, as
    // the comparison refuses it, with the path from value to it; null when there is none. Struct nesting by value is
    // finite, so the recursion ends.
    private static HiddenMembers? FindUncopiedHeld(object value, Type type, Func<Type, bool> isUncopied)
    {
        ValueRoutes routes = ValueRoutesCache.GetOrAdd(type, CreateValueRoutes);
        foreach (MemberAccessor field in routes.InlineArrays)
        {
            if (
                field.GetValue(value) is { } array
                && isUncopied(array.GetType())
                && InlineArrayShapeOf(array.GetType()).ElementType is { } elementType
            )
            {
                HiddenMembers held = new(array.GetType()) { ElementsNotCopied = elementType };
                held.Prepend("." + field.PathName);
                return held;
            }
        }

        foreach (MemberAccessor field in routes.Nested)
        {
            if (
                field.GetValue(value) is { } nested
                && FindUncopiedHeld(nested, nested.GetType(), isUncopied) is { } held
            )
            {
                held.Prepend("." + field.PathName);
                return held;
            }
        }

        return null;
    }

    // Puts in pending the run-time values of the fields declared object, an interface, ValueType or a class, of value
    // and of its nested structs that have no hand-written Equals. Nested structs go no deeper than their declared
    // types do.
    private static void PushHeldValues(ValueRoutes routes, object value, ref Stack<object>? pending)
    {
        foreach (MemberAccessor field in routes.RunTime)
        {
            if (field.GetValue(value) is { } held)
            {
                (pending ??= new Stack<object>()).Push(held);
            }
        }

        foreach (MemberAccessor field in routes.Nested)
        {
            if (field.GetValue(value) is { } nested && !HasUserEquals(nested, nested.GetType()))
            {
                PushHeldValues(ValueRoutesCache.GetOrAdd(nested.GetType(), CreateValueRoutes), nested, ref pending);
            }
        }
    }

    // A held value is the same object on every read (a box read from an object field is not copied), so it is
    // recorded in looked before it is looked into, and a value already recorded is not looked into again. The values
    // it holds in turn are put in pending.
    private static bool NeedsFieldComparisonAsHeld(object held, ref Stack<object>? pending, ref HashSet<object>? looked)
    {
        Type type = held.GetType();
        if (type.IsValueType && (type.IsPrimitive || type.IsEnum))
        {
            return false;
        }

        if (IsInlineArray(type))
        {
            return true;
        }

        ClassEquals classEquals = type.IsValueType ? ClassEquals.NotFollowed : ClassEqualsOf(held, type);
        bool followed = type.IsValueType ? !HasUserEquals(held, type) : classEquals != ClassEquals.NotFollowed;
        if (!followed)
        {
            return false;
        }

        looked ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
        if (!looked.Add(held))
        {
            return false;
        }

        if (classEquals == ClassEquals.Tuple)
        {
            PushTupleItems((ITuple)held, ref pending);
            return false;
        }

        return NeedsFieldComparison(held, type, ref pending);
    }

    // The Equals of a System.Tuple<…> compares its items, which a class derived from it does not show as fields of
    // its own (they are private fields of the framework base class), so they are read through ITuple.
    private static void PushTupleItems(ITuple tuple, ref Stack<object>? pending)
    {
        for (int i = 0; i < tuple.Length; i++)
        {
            if (tuple[i] is { } item)
            {
                (pending ??= new Stack<object>()).Push(item);
            }
        }
    }

    // Which Equals the class of box dispatches to, when it compares values but is not written by hand: a record
    // class's [CompilerGenerated] Equals, the Equals of an anonymous type (see IsAnonymousType; its Equals is not
    // marked), or the Equals of a System.Tuple<…>. A fact of the type, cached.
    private static ClassEquals ClassEqualsOf(object box, Type type)
    {
        return ClassEqualsCache.GetOrAdd(
            type,
            static (_, b) =>
            {
                Func<object?, bool> equals = b.Equals;
                Type? declaring = equals.Method.DeclaringType;
                if (declaring is null || declaring == typeof(object))
                {
                    return ClassEquals.NotFollowed;
                }

                if (declaring.IsGenericType && TupleDefinitions.Contains(declaring.GetGenericTypeDefinition()))
                {
                    return ClassEquals.Tuple;
                }

                return
                    (
                        equals.Method.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
                        || IsAnonymousType(declaring)
                    ) && !GetOrCacheMetadata(declaring).HasHandWrittenTypedEquals
                    ? ClassEquals.CompilerGenerated
                    : ClassEquals.NotFollowed;
            },
            box
        );
    }

    // An anonymous type of C# or of Visual Basic: marked [CompilerGenerated] and named by the compiler with a name no
    // source of that language can declare (<>f__AnonymousType… in C#, VB$AnonymousType_… in Visual Basic).
    // [CompilerGenerated] alone does not tell it: a user or a source generator may mark a class of theirs with it.
    private static bool IsAnonymousType(Type type)
    {
        return (
                type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
                && type.Name.StartsWith("<>f__AnonymousType", StringComparison.Ordinal)
            ) || IsVisualBasicAnonymousType(type);
    }

    // An anonymous type of Visual Basic (see IsAnonymousType).
    private static bool IsVisualBasicAnonymousType(Type type)
    {
        return type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
            && type.Name.StartsWith("VB$AnonymousType_", StringComparison.Ordinal);
    }

    // The fields of a struct (or of a followed class) that can lead its Equals to an inline array's (see RouteOf). An
    // inline array has none: its elements are compared by StartInlineArray.
    private static ValueRoutes CreateValueRoutes(Type type)
    {
        if (IsInlineArray(type))
        {
            return ValueRoutes.None;
        }

        List<MemberAccessor> inlineArrays = [];
        List<MemberAccessor> nested = [];
        List<MemberAccessor> runTime = [];
        foreach (MemberAccessor field in FieldsEqualsCompares(type))
        {
            switch (RouteOf(field.MemberType))
            {
                case Route.InlineArray:
                    inlineArrays.Add(field);
                    break;
                case Route.RunTime:
                    runTime.Add(field);
                    break;
                case Route.Nested:
                    nested.Add(field);
                    break;
            }
        }

        return inlineArrays.Count + nested.Count + runTime.Count == 0
            ? ValueRoutes.None
            : new ValueRoutes([.. inlineArrays], [.. nested], [.. runTime]);
    }

    // How a field declared fieldType can lead an Equals to an inline array's: it is (a Nullable<> of) an inline array;
    // it is declared object, ValueType, an interface or a class other than string, an array or a delegate, so only
    // its run-time value tells; or it is (a Nullable<> of) a struct that has routes of its own.
    private static Route RouteOf(Type fieldType)
    {
        Type declared = Nullable.GetUnderlyingType(fieldType) ?? fieldType;
        if (IsInlineArray(declared))
        {
            return Route.InlineArray;
        }

        if (
            declared == typeof(object)
            || declared == typeof(ValueType)
            || declared.IsInterface
            || (
                declared.IsClass
                && declared != typeof(string)
                && !declared.IsArray
                && !typeof(Delegate).IsAssignableFrom(declared)
            )
        )
        {
            return Route.RunTime;
        }

        return
            declared.IsValueType
            && !declared.IsPrimitive
            && !declared.IsEnum
            && !ValueRoutesCache.GetOrAdd(declared, CreateValueRoutes).IsEmpty
            ? Route.Nested
            : Route.None;
    }

    // The fields of type in its cached members, and the fields declared on each base class up to the first framework
    // class that those members leave out: a derived record's members leave out the backing field, declared on a base
    // record, of a positional property it compares as a property, yet the base record's Equals compares that field.
    // The Equals of a Visual Basic anonymous type compares only its Key properties: their fields are its only read-only
    // ones (a non-Key property has a setter, and its field is writable).
    private static IEnumerable<MemberAccessor> FieldsEqualsCompares(Type type)
    {
        bool keyFieldsOnly = IsVisualBasicAnonymousType(type);
        MemberAccessor[] fields =
        [
            .. GetOrCacheMetadata(type)
                .Members.Where(member => member.IsField && (!keyFieldsOnly || member.IsReadOnlyField)),
        ];
        IEnumerable<MemberAccessor> result = fields;
        for (
            Type? baseType = type.BaseType;
            baseType is not null && !IsFrameworkClass(baseType);
            baseType = baseType.BaseType
        )
        {
            Type declaring = baseType;
            result = result.Concat(
                GetOrCacheMetadata(declaring)
                    .Members.Where(member =>
                        member.IsField
                        && member.DeclaringType == declaring
                        && !fields.Any(listed =>
                            listed.DeclaringType == declaring
                            && string.Equals(listed.RawName, member.RawName, StringComparison.Ordinal)
                        )
                    )
            );
        }

        return result;
    }

    // The element type and length of the inline-array type type (see InlineArrayShape), a fact of the type that is
    // cached.
    internal static InlineArrayShape InlineArrayShapeOf(Type type)
    {
        return InlineArrayShapeCache.GetOrAdd(type, CreateInlineArrayShape);
    }

    // True when type is marked [InlineArray], a fact of the type that is cached.
    internal static bool IsInlineArray(Type type)
    {
        return InlineArrayCache.GetOrAdd(type, static t => t.IsDefined(typeof(InlineArrayAttribute), inherit: false));
    }

    // An inline array declares exactly one instance field, whose type is the element type. The field is read from
    // the cached members of the type (see GetOrCacheMetadata); under Native AOT the trimmer may hide it, which
    // leaves the element type unknown.
    private static InlineArrayShape CreateInlineArrayShape(Type type)
    {
        MemberAccessor[] fields = [.. GetOrCacheMetadata(type).Members.Where(member => member.IsField)];
        Type? elementType = fields.Length == 1 ? fields[0].MemberType : null;
        return new InlineArrayShape(elementType, type.GetCustomAttribute<InlineArrayAttribute>(inherit: false)!.Length);
    }

    // True for elements of a type that is compared in place (see InPlaceOf); equal then tells whether the length
    // elements at actualData and at expectedData are the same. A float or a double is compared by its own Equals,
    // as a boxed one is: NaN equals NaN, and 0.0 equals -0.0. Each of the other types is equal by its Equals exactly
    // when its bits are equal (an enum compares its underlying integers), so the memory is compared as integers of
    // the element's size.
    private static bool TryCompareInPlace(
        Type elementType,
        ref byte actualData,
        ref byte expectedData,
        int length,
        out bool equal
    )
    {
        InPlace inPlace = InPlaceOf(elementType);
        equal = inPlace switch
        {
            InPlace.Float => HaveSameElements<float>(ref actualData, ref expectedData, length),
            InPlace.Double => HaveSameElements<double>(ref actualData, ref expectedData, length),
            InPlace.Bits8 => HaveSameElements<byte>(ref actualData, ref expectedData, length),
            InPlace.Bits16 => HaveSameElements<ushort>(ref actualData, ref expectedData, length),
            InPlace.Bits32 => HaveSameElements<uint>(ref actualData, ref expectedData, length),
            InPlace.Bits64 => HaveSameElements<ulong>(ref actualData, ref expectedData, length),
            InPlace.NativeBits => HaveSameElements<nuint>(ref actualData, ref expectedData, length),
            _ => false,
        };
        return inPlace != InPlace.No;
    }

    // How elements of elementType are compared in place: bool, char, an integer type (nint and nuint included), an
    // enum, float or double are, and a pointer or a function pointer by the address it holds, as a pointer member and
    // an array of pointers or of function pointers are (see Start, TryGetAddresses); any other type is not.
    private static InPlace InPlaceOf(Type elementType)
    {
        if (elementType.IsPointer || elementType.IsFunctionPointer)
        {
            return InPlace.NativeBits;
        }

        if (elementType == typeof(float))
        {
            return InPlace.Float;
        }

        if (elementType == typeof(double))
        {
            return InPlace.Double;
        }

        Type bitsType = elementType.IsEnum ? elementType.GetEnumUnderlyingType() : elementType;
        if (bitsType == typeof(bool) || bitsType == typeof(byte) || bitsType == typeof(sbyte))
        {
            return InPlace.Bits8;
        }

        if (bitsType == typeof(char) || bitsType == typeof(short) || bitsType == typeof(ushort))
        {
            return InPlace.Bits16;
        }

        if (bitsType == typeof(int) || bitsType == typeof(uint))
        {
            return InPlace.Bits32;
        }

        if (bitsType == typeof(long) || bitsType == typeof(ulong))
        {
            return InPlace.Bits64;
        }

        return bitsType == typeof(nint) || bitsType == typeof(nuint) ? InPlace.NativeBits : InPlace.No;
    }

    // The memory at each reference is read as length elements of T, which is in bounds only when T has the
    // size of the element type; TryCompareInPlace picks T so. SequenceEqual compares the elements with
    // IEquatable<T>.Equals.
    private static bool HaveSameElements<T>(ref byte actualData, ref byte expectedData, int length)
        where T : unmanaged, IEquatable<T>
    {
        return MemoryMarshal
            .CreateReadOnlySpan(ref Unsafe.As<byte, T>(ref actualData), length)
            .SequenceEqual(MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<byte, T>(ref expectedData), length));
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

    // True for two arrays of one runtime type, of any rank, whose elements are compared in place (see the
    // overload that takes the element type); equal then tells whether they hold the same elements. Both arrays
    // have the same rank, and the same length and lower bound in every dimension (see StartReferences), so they
    // hold the same number of elements in the same order.
    private static bool TryCompareInPlace(object actual, object expected, out bool equal)
    {
        equal = false;
        if (actual is not Array actualArray || expected is not Array expectedArray)
        {
            return false;
        }

        Type arrayType = actualArray.GetType();
        if (arrayType != expectedArray.GetType())
        {
            return false;
        }

        return TryCompareInPlace(
            arrayType.GetElementType()!,
            ref MemoryMarshal.GetArrayDataReference(actualArray),
            ref MemoryMarshal.GetArrayDataReference(expectedArray),
            actualArray.Length,
            out equal
        );
    }

    private static bool AreSequencesComparedByElementsOnly(object actual, object expected)
    {
        return IsSequence(actual)
            && IsSequence(expected)
            && IsComparedByElementsOnly(actual.GetType())
            && IsComparedByElementsOnly(expected.GetType());
    }

    private static bool IsSequence(object value)
    {
        return value is IEnumerable and not string;
    }

    // Null means the pair needs no member walk. A type with no visible members, or a class that is not a
    // framework class and shows no declared member (see ShowsNoDeclaredMember), is left to its own Equals; when
    // that says "unequal" the pair could not be compared (context.Hidden is set).
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

        if (MembersAreHidden(type, metadata) || (ShowsNoDeclaredMember(type) && !IsFrameworkClass(type)))
        {
            equalWithoutWalk = actual.Equals(expected);
            if (!equalWithoutWalk)
            {
                // CannotCompareNested's wording relies on Equals having returned false here.
                context.Hidden = new HiddenMembers(type)
                {
                    OnlyByRefLikePropertiesVisible = MembersAreHidden(type, metadata) && ListsByRefLikeProperty(type),
                };
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

        // Tells which inline-array types the Old copy holding this pair could not copy (see StartHeld).
        public Func<Type, bool>? Uncopied { get; set; }

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
    private sealed class MemberWalk(
        IReadOnlyList<MemberAccessor> members,
        object actual,
        object expected,
        bool fieldsOnly
    ) : Walk
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

    // The elements of a sequence, and then the members declared on its own classes.
    private sealed class ElementsThenMembersWalk(Walk elements, Walk members) : Walk
    {
        private Walk _current = elements;
        private bool _onMembers;

        public override string ChildSegment => _current.ChildSegment;

        public override Step Next()
        {
            Step step = _current.Next();
            if (step == Step.Equal && !_onMembers)
            {
                _onMembers = true;
                _current = members;
                step = members.Next();
            }

            return step == Step.Child ? Child(_current.ChildActual, _current.ChildExpected) : step;
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

    // What StartInlineArray and DeepCopier need of an inline-array type: its element type (null when reflection
    // does not list its one element field; its elements are then neither compared nor copied one by one) and its
    // length.
    internal sealed record InlineArrayShape(Type? ElementType, int Length);

    private enum InPlace
    {
        No,
        Float,
        Double,
        Bits8,
        Bits16,
        Bits32,
        Bits64,
        NativeBits,
    }

    private enum Holder
    {
        Unknown,
        Yes,
        No,
    }

    // How a field can lead an Equals to an inline array's (see RouteOf).
    private enum Route
    {
        None,
        InlineArray,
        Nested,
        RunTime,
    }

    // How the Equals of a class compares (see ClassEqualsOf): only CompilerGenerated and Tuple are followed.
    private enum ClassEquals
    {
        NotFollowed,
        CompilerGenerated,
        Tuple,
    }

    // What NeedsFieldComparison looks at in a struct: fields declared (a Nullable<> of) an inline array, fields
    // declared (a Nullable<> of) a struct that has routes of its own, and fields declared object, an interface or
    // ValueType. Holder caches HolderState once it is known.
    private sealed class ValueRoutes(MemberAccessor[] inlineArrays, MemberAccessor[] nested, MemberAccessor[] runTime)
    {
        private int _holder;

        public static ValueRoutes None { get; } = new([], [], []);

        public MemberAccessor[] InlineArrays { get; } = inlineArrays;

        public MemberAccessor[] Nested { get; } = nested;

        public MemberAccessor[] RunTime { get; } = runTime;

        public bool IsEmpty => InlineArrays.Length + Nested.Length + RunTime.Length == 0;

        public Holder Holder
        {
            get => (Holder)Volatile.Read(ref _holder);
            set => Volatile.Write(ref _holder, (int)value);
        }
    }

    // OwnMembers is cached for every caller, so it is read-only.
    internal sealed record SequenceRule(bool HasFrameworkClass, IReadOnlyList<MemberAccessor> OwnMembers)
    {
        // A compiler-generated sequence of yours (a yield iterator, a collection expression): compared by its
        // elements alone, at every level.
        public bool IsCompilerGenerated { get; init; }

        // Implements ICollection or IReadOnlyCollection<T> (see MemberComparison.IsCollection).
        public bool IsCollection { get; init; }

        // Declares a public property of a byref-like type on a class of yours. The property is never read, but it
        // is a member of yours: the sequence is not compared by its elements alone (see IsComparedByElementsOnly).
        public bool DeclaresByRefLikeProperty { get; init; }

        // Compared by its elements (and OwnMembers), not by its members alone.
        public bool IsComparedByElements => HasFrameworkClass || IsCompilerGenerated;
    }

    // Created only when a comparison is blocked. The path is prepended one segment per level on
    // the way back up, so a comparison that is not blocked allocates nothing for it.
    internal sealed class HiddenMembers(Type type)
    {
        public Type Type { get; } = type;

        // The rule "no members visible" fired (see MembersAreHidden), and the only properties the type lists are of
        // a byref-like type. False for a type that shows no declared member while the members it inherits stay
        // visible (see ShowsNoDeclaredMember).
        public bool OnlyByRefLikePropertiesVisible { get; init; }

        // Set when Type is an inline array whose elements, of this struct type, this runtime reads only with dynamic
        // code, which is not supported (see StructElements.Read); its members are visible.
        public Type? ElementsNeedDynamicCode { get; init; }

        // Set when Type is an inline array whose elements, of this struct type, the Old copy being compared could not
        // copy (see UncopiedInlineArrays); its members are visible.
        public Type? ElementsNotCopied { get; init; }

        // Set when Type is an inline array whose element field reflection does not list (under Native AOT, a type
        // that nothing preserves), so its element type, and so its elements, are unknown.
        public bool ElementFieldHidden { get; init; }

        public string PathBelowTopLevelMember { get; private set; } = "";

        public void Prepend(string segment)
        {
            PathBelowTopLevelMember = segment + PathBelowTopLevelMember;
        }
    }
}
