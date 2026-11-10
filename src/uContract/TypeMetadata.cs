using System.Collections.Generic;

namespace uContract;

/// <summary>
///     Internal metadata cache for type information used in reflection-based field comparison.
/// </summary>
internal sealed class TypeMetadata
{
    /// <summary>
    ///     List of members (properties and fields) for a given type.
    /// </summary>
    public List<MemberAccessor> Members { get; set; } = [];

    /// <summary>
    ///     The number of members in <see cref="Members" /> that decide the "no members visible" rule: those
    ///     reflection lists for the type (its public properties and the fields GetFields returns), plus, in the
    ///     metadata the top level builds for a class T other than the runtime type, the private fields declared
    ///     on T. Other private fields of base classes are not counted, and the cached metadata never counts them.
    ///     Properties of a byref-like type are not in <see cref="Members" /> and are not counted either.
    /// </summary>
    public int OwnMemberCount { get; set; }

    /// <summary>
    ///     True when the type is not an anonymous type and it declares a public <c>Equals</c> whose one parameter is of
    ///     exactly the type <c>T</c> and that is not marked
    ///     <see cref="System.Runtime.CompilerServices.CompilerGeneratedAttribute" />: a record's hand-written typed
    ///     <c>Equals(T)</c>, which its generated <c>Equals(object)</c> calls. An <c>Equals</c> that takes
    ///     <c>object</c>, a base class or an interface of <c>T</c> does not count. The <c>Equals</c> an anonymous type
    ///     declares is not marked (only the type is), so anonymous types are left out by name (see
    ///     CSharpTypeName.IsAnonymousType).
    ///     Also true when a base class below the framework classes has the fact, whatever fields the type declares: a
    ///     derived record's generated typed <c>Equals</c> calls that of its base record, then compares the fields the
    ///     record declares with their own <c>Equals</c>. Set only in the cached metadata.
    /// </summary>
    public bool HasHandWrittenTypedEquals { get; set; }
}
