namespace uContract.Exceptions;

/// <summary>
///     Specifies the type of contract that was violated.
/// </summary>
/// <remarks>
///     This enumeration is used to categorize contract violations for diagnostic purposes.
///     Each contract type can be independently enabled or disabled via environment variables.
/// </remarks>
public enum ContractType
{
    /// <summary>
    ///     A precondition was violated.
    ///     Preconditions define the obligations of the caller before invoking a method.
    /// </summary>
    /// <remarks>
    ///     Controlled by the <c>DBC_PRE</c> environment variable.
    /// </remarks>
    Precondition,

    /// <summary>
    ///     A postcondition was violated.
    ///     Postconditions define the obligations of the method implementation to the caller.
    /// </summary>
    /// <remarks>
    ///     Controlled by the <c>DBC_POST</c> environment variable.
    /// </remarks>
    Postcondition,

    /// <summary>
    ///     A class invariant was violated.
    ///     Invariants define the consistency constraints that must hold throughout the object's lifetime.
    /// </summary>
    /// <remarks>
    ///     Controlled by the <c>DBC_INV</c> environment variable.
    /// </remarks>
    Invariant,

    /// <summary>
    ///     A check statement failed.
    ///     Check statements are used for runtime assertions that are neither pre- nor postconditions.
    /// </summary>
    /// <remarks>
    ///     Controlled by the <c>DBC_CHECK</c> environment variable.
    ///     Note: Check functionality is part of Phase 2 implementation.
    /// </remarks>
    Check
}