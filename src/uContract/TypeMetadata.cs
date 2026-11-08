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
    /// </summary>
    public int OwnMemberCount { get; set; }

    /// <summary>
    ///     The number of public properties of a byref-like type that reflection lists for the type. They cannot be
    ///     read through reflection and are left out of <see cref="Members" />, but they are visible, so they count
    ///     for the "no members visible" rule.
    /// </summary>
    public int ByRefLikePropertyCount { get; set; }
}
