using System;

namespace uContract.Exceptions;

/// <summary>
///     Exception thrown when a class invariant is violated.
///     Invariants define the consistency constraints that must hold throughout the object's lifetime.
/// </summary>
/// <remarks>
///     This exception indicates that the object's internal state became inconsistent.
///     Invariants should be checked at the end of constructors and at the end of public methods.
///     Invariants can be disabled at runtime using the <c>DBC_INV</c> environment variable.
/// </remarks>
/// <example>
///     <code>
/// public class BankAccount
/// {
///     private decimal _balance;
///
///     private void CheckInvariant()
///     {
///         Contract.Invariant("Balance cannot be negative", () => _balance >= 0);
///     }
///
///     public void Withdraw(decimal amount)
///     {
///         _balance -= amount;
///         CheckInvariant();
///     }
/// }
/// </code>
/// </example>
public sealed class InvariantViolationException : ContractViolationException
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="InvariantViolationException" /> class
    ///     with a human-readable description.
    /// </summary>
    /// <param name="description">The description of the invariant that was violated.</param>
    public InvariantViolationException(string description)
        : base(description, ContractType.Invariant, $"Invariant violated: {description}") { }

    /// <summary>
    ///     Initializes a new instance of the <see cref="InvariantViolationException" /> class
    ///     with a human-readable description and inner exception.
    /// </summary>
    /// <param name="description">The description of the invariant that was violated.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public InvariantViolationException(string description, Exception innerException)
        : base(description, ContractType.Invariant, $"Invariant violated: {description}", innerException) { }
}
