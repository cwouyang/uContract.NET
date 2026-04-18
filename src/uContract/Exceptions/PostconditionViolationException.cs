using System;

namespace uContract.Exceptions;

/// <summary>
///     Exception thrown when a postcondition (Ensure) is violated.
///     Postconditions define the obligations of the method implementation to the caller.
/// </summary>
/// <remarks>
///     This exception indicates that the method implementation failed to satisfy a guaranteed
///     condition after executing. Postconditions can be disabled at runtime using the
///     <c>DBC_POST</c> environment variable.
/// </remarks>
/// <example>
///     <code>
/// public void Deposit(decimal amount)
/// {
///     var oldBalance = _balance;
///     _balance += amount;
///     Contract.Ensure("Balance increased", () => _balance == oldBalance + amount);
/// }
/// </code>
/// </example>
public sealed class PostconditionViolationException : ContractViolationException
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="PostconditionViolationException" /> class
    ///     with a human-readable description.
    /// </summary>
    /// <param name="description">The description of the postcondition that was violated.</param>
    public PostconditionViolationException(string description)
        : base(description, ContractType.Postcondition, $"Postcondition violated: {description}") { }

    /// <summary>
    ///     Initializes a new instance of the <see cref="PostconditionViolationException" /> class
    ///     with a human-readable description and inner exception.
    /// </summary>
    /// <param name="description">The description of the postcondition that was violated.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public PostconditionViolationException(string description, Exception innerException)
        : base(description, ContractType.Postcondition, $"Postcondition violated: {description}", innerException) { }
}
