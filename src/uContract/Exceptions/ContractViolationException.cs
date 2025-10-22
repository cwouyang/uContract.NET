using System;

namespace uContract.Exceptions;

/// <summary>
///     Base exception for all Design by Contract violations.
///     This is the parent class for all contract-related exceptions thrown by the uContract library.
/// </summary>
/// <remarks>
///     This exception should not be thrown directly. Instead, use one of its derived types:
///     <see cref="PreconditionViolationException" />, <see cref="PostconditionViolationException" />,
///     or <see cref="InvariantViolationException" />.
/// </remarks>
public class ContractViolationException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ContractViolationException" /> class
    ///     with a description, violation type, and formatted message.
    /// </summary>
    /// <param name="description">The human-readable description of the violated contract.</param>
    /// <param name="violationType">The type of contract that was violated.</param>
    /// <param name="message">The formatted exception message.</param>
    public ContractViolationException(string description, ContractType violationType, string message)
        : base(message)
    {
        Description = description;
        ViolationType = violationType;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ContractViolationException" /> class
    ///     with a description, violation type, formatted message, and inner exception.
    /// </summary>
    /// <param name="description">The human-readable description of the violated contract.</param>
    /// <param name="violationType">The type of contract that was violated.</param>
    /// <param name="message">The formatted exception message.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ContractViolationException(string description, ContractType violationType, string message, Exception innerException)
        : base(message, innerException)
    {
        Description = description;
        ViolationType = violationType;
    }

    /// <summary>
    ///     Gets the human-readable description of the contract that was violated.
    /// </summary>
    /// <remarks>
    ///     This is the description provided when the contract was defined,
    ///     explaining what condition was expected to hold.
    /// </remarks>
    public string Description { get; }

    /// <summary>
    ///     Gets the type of contract that was violated.
    /// </summary>
    /// <remarks>
    ///     Indicates whether this was a precondition, postcondition, invariant, or check violation.
    /// </remarks>
    public ContractType ViolationType { get; }
}