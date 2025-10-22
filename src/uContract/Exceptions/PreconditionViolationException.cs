using System;

namespace uContract.Exceptions;

/// <summary>
///     Exception thrown when a precondition (Require) is violated.
///     Preconditions define the obligations of the caller before invoking a method.
/// </summary>
/// <remarks>
///     This exception indicates that the caller failed to satisfy a required condition
///     before calling a method. Preconditions can be disabled at runtime using the
///     <c>DBC_PRE</c> environment variable.
/// </remarks>
/// <example>
///     <code>
/// public void SetAge(int age)
/// {
///     Contract.Require("Age must be positive", () => age > 0);
///     _age = age;
/// }
/// </code>
/// </example>
public sealed class PreconditionViolationException : ContractViolationException
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="PreconditionViolationException" /> class
    ///     with a human-readable description.
    /// </summary>
    /// <param name="description">The description of the precondition that was violated.</param>
    public PreconditionViolationException(string description)
        : base(description, ContractType.Precondition, $"Precondition violated: {description}")
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="PreconditionViolationException" /> class
    ///     with a human-readable description and inner exception.
    /// </summary>
    /// <param name="description">The description of the precondition that was violated.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public PreconditionViolationException(string description, Exception innerException)
        : base(description, ContractType.Precondition, $"Precondition violated: {description}", innerException)
    {
    }
}