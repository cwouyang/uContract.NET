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
    ///     The number of members at the start of <see cref="Members" /> that reflection lists for the type (its
    ///     public properties and the fields GetFields returns), before the private fields declared on its base
    ///     classes; the "no members visible" rule is decided on them.
    /// </summary>
    public int OwnMemberCount { get; set; }
}
