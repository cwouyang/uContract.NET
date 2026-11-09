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
    ///     True when the type declares a public typed <c>Equals(T)</c> that is not marked
    ///     <see cref="System.Runtime.CompilerServices.CompilerGeneratedAttribute" />: a record's hand-written one, which
    ///     its generated <c>Equals(object)</c> calls. A type marked
    ///     <see cref="System.Runtime.CompilerServices.CompilerGeneratedAttribute" /> (an anonymous type) declares none.
    ///     Also true when a base class below the framework classes has one: a derived record's generated typed
    ///     <c>Equals</c> calls that of its base record. Set only in the cached metadata.
    /// </summary>
    public bool DeclaresHandWrittenTypedEquals { get; set; }
}
