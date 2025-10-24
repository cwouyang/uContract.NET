using System;
using System.Threading;

using uContract.Exceptions;

namespace uContract;

/// <summary>
///     Design by Contract assertion methods for preconditions, postconditions, and invariants.
/// </summary>
/// <remarks>
///     This class provides static methods to validate contracts in accordance with
///     Design by Contract principles. All contract conditions use lazy evaluation
///     to ensure zero performance overhead when contracts are disabled.
///     Contract evaluation can be controlled via environment variables:
///     - DBC: Global toggle for all contracts
///     - DBC_PRE: Toggle for preconditions only
///     - DBC_POST: Toggle for postconditions only
///     - DBC_INV: Toggle for invariants only
///     - DBC_CHECK: Toggle for check statements only
/// </remarks>
public static class Contract
{
    private static readonly ContractConfiguration Config = new();
    private static readonly AsyncLocal<bool> Entered = new();

    /// <summary>
    ///     Validates a precondition and throws an exception if the condition is false.
    /// </summary>
    /// <param name="description">Human-readable description of the precondition.</param>
    /// <param name="condition">Lazy-evaluated boolean condition that must be true.</param>
    /// <exception cref="PreconditionViolationException">Thrown when the condition evaluates to false.</exception>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="description" /> or <paramref name="condition" /> is
    ///     null.
    /// </exception>
    /// <remarks>
    ///     This method is disabled when the DBC_PRE environment variable is set to "false" or "off".
    ///     The condition is evaluated lazily to ensure zero overhead when contracts are disabled.
    ///     Uses a recursion guard to prevent infinite loops when contract checks trigger other contract checks.
    /// </remarks>
    /// <example>
    ///     <code>
    /// public void Transfer(decimal amount)
    /// {
    ///     Contract.Require("Amount must be positive", () => amount > 0);
    ///     // ... transfer logic
    /// }
    /// </code>
    /// </example>
    public static void Require(string description, Func<bool> condition)
    {
        // Step 1: Validate parameters (ALWAYS - even if DBC disabled)
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(condition);

        // Step 2: Check recursion guard
        if (Entered.Value)
        {
            return;
        }

        // Step 3: Check if enabled
        if (!Config.PreconditionsEnabled)
        {
            return;
        }

        // Step 4: Execute with guard
        try
        {
            Entered.Value = true;
            if (!condition())
            {
                throw new PreconditionViolationException(description);
            }
        }
        finally
        {
            Entered.Value = false;
        }
    }

    /// <summary>
    ///     Validates a postcondition and throws an exception if the condition is false.
    /// </summary>
    /// <param name="description">Human-readable description of the postcondition.</param>
    /// <param name="condition">Lazy-evaluated boolean condition that must be true.</param>
    /// <exception cref="PostconditionViolationException">Thrown when the condition evaluates to false.</exception>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="description" /> or <paramref name="condition" /> is
    ///     null.
    /// </exception>
    /// <remarks>
    ///     This method is disabled when the DBC_POST environment variable is set to "false" or "off".
    ///     The condition is evaluated lazily to ensure zero overhead when contracts are disabled.
    ///     Uses a recursion guard to prevent infinite loops when contract checks trigger other contract checks.
    /// </remarks>
    /// <example>
    ///     <code>
    /// public void ChangeEmail(string newEmail)
    /// {
    ///     var oldEmail = _email;
    ///     _email = newEmail;
    ///     Contract.Ensure("Email must be changed", () => _email != oldEmail);
    /// }
    /// </code>
    /// </example>
    public static void Ensure(string description, Func<bool> condition)
    {
        // Step 1: Validate parameters (ALWAYS - even if DBC disabled)
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(condition);

        // Step 2: Check recursion guard
        if (Entered.Value)
        {
            return;
        }

        // Step 3: Check if enabled
        if (!Config.PostconditionsEnabled)
        {
            return;
        }

        // Step 4: Execute with guard
        try
        {
            Entered.Value = true;
            if (!condition())
            {
                throw new PostconditionViolationException(description);
            }
        }
        finally
        {
            Entered.Value = false;
        }
    }

    /// <summary>
    ///     Validates an invariant and throws an exception if the condition is false.
    /// </summary>
    /// <param name="description">Human-readable description of the invariant.</param>
    /// <param name="condition">Lazy-evaluated boolean condition that must be true.</param>
    /// <exception cref="InvariantViolationException">Thrown when the condition evaluates to false.</exception>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="description" /> or <paramref name="condition" /> is
    ///     null.
    /// </exception>
    /// <remarks>
    ///     This method is disabled when the DBC_INV environment variable is set to "false" or "off".
    ///     The condition is evaluated lazily to ensure zero overhead when contracts are disabled.
    ///     Uses a recursion guard to prevent infinite loops when contract checks trigger other contract checks.
    /// </remarks>
    /// <example>
    ///     <code>
    /// private void CheckInvariant()
    /// {
    ///     Contract.Invariant("Balance must be non-negative", () => _balance >= 0);
    /// }
    /// </code>
    /// </example>
    public static void Invariant(string description, Func<bool> condition)
    {
        // Step 1: Validate parameters (ALWAYS - even if DBC disabled)
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(condition);

        // Step 2: Check recursion guard
        if (Entered.Value)
        {
            return;
        }

        // Step 3: Check if enabled
        if (!Config.InvariantsEnabled)
        {
            return;
        }

        // Step 4: Execute with guard
        try
        {
            Entered.Value = true;
            if (!condition())
            {
                throw new InvariantViolationException(description);
            }
        }
        finally
        {
            Entered.Value = false;
        }
    }
}