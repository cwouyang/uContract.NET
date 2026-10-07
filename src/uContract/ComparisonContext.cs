namespace uContract;

/// <summary>
///     The state of one <see cref="Contract.EnsureAssignable{T}" /> comparison, threaded through the walk.
/// </summary>
internal sealed class ComparisonContext
{
    /// <summary>
    ///     Set when a type with no visible members blocked the comparison.
    /// </summary>
    public MemberComparison.HiddenMembers? Hidden { get; set; }
}
