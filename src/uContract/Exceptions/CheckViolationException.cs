using System;

namespace uContract.Exceptions;

/// <summary>
///     Exception thrown when a check statement fails.
///     Check statements are used for runtime assertions that are neither preconditions nor postconditions.
/// </summary>
/// <remarks>
///     This exception indicates that a custom validation check has failed during method execution.
///     Check statements can be disabled at runtime using the <c>DBC_CHECK</c> environment variable.
/// </remarks>
/// <example>
///     <code>
/// public void ProcessOrder(Order order)
/// {
///     Contract.Check("Order state valid", () => order.IsValidState());
///     // ... processing
/// }
/// </code>
/// </example>
public sealed class CheckViolationException : ContractViolationException
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="CheckViolationException" /> class
    ///     with a human-readable description.
    /// </summary>
    /// <param name="description">The description of the check that failed.</param>
    public CheckViolationException(string description)
        : base(description, ContractType.Check, $"Check failed: {description}")
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="CheckViolationException" /> class
    ///     with a human-readable description and inner exception.
    /// </summary>
    /// <param name="description">The description of the check that failed.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public CheckViolationException(string description, Exception innerException)
        : base(description, ContractType.Check, $"Check failed: {description}", innerException)
    {
    }
}