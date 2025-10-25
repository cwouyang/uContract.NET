using System;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        WriteIndented = false,
        IncludeFields = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

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

    /// <summary>
    ///     Validates a custom check assertion and throws an exception if the condition is false.
    /// </summary>
    /// <param name="description">Human-readable description of the check.</param>
    /// <param name="condition">Lazy-evaluated boolean condition that must be true.</param>
    /// <exception cref="CheckViolationException">Thrown when the condition evaluates to false.</exception>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="description" /> or <paramref name="condition" /> is
    ///     null.
    /// </exception>
    /// <remarks>
    ///     This method is disabled when the DBC_CHECK environment variable is set to "false" or "off".
    ///     The condition is evaluated lazily to ensure zero overhead when contracts are disabled.
    ///     Uses a recursion guard to prevent infinite loops when contract checks trigger other contract checks.
    ///     Check statements are for runtime assertions that are neither preconditions nor postconditions.
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
    public static void Check(string description, Func<bool> condition)
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
        if (!Config.CheckEnabled)
        {
            return;
        }

        // Step 4: Execute with guard
        try
        {
            Entered.Value = true;
            if (!condition())
            {
                throw new CheckViolationException(description);
            }
        }
        finally
        {
            Entered.Value = false;
        }
    }

    /// <summary>
    ///     Evaluates a condition for early return pattern in Domain-Driven Design.
    ///     Returns true if the condition is met, allowing the caller to exit early.
    /// </summary>
    /// <param name="reason">Human-readable reason for potential early return.</param>
    /// <param name="condition">Lazy-evaluated boolean condition to check.</param>
    /// <returns>True if the condition is met (early return recommended); false otherwise.</returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="reason" /> or <paramref name="condition" /> is null.
    /// </exception>
    /// <remarks>
    ///     This method supports the DDD pattern of avoiding unnecessary work when a condition is met.
    ///     Unlike Require/Ensure/Invariant, this method always evaluates the condition and returns its result,
    ///     without throwing exceptions. Uses the DBC_PRE environment variable for control.
    ///     The condition is evaluated lazily to ensure zero overhead when contracts are disabled.
    ///     Uses a recursion guard to prevent infinite loops when contract checks trigger other contract checks.
    /// </remarks>
    /// <example>
    ///     <code>
    /// public void ChangeEmail(string newEmail)
    /// {
    ///     if (Contract.Ignore("Email unchanged", () => _email == newEmail))
    ///         return;  // Early return - no work needed
    /// 
    ///     // ... rest of method
    ///     _email = newEmail;
    /// }
    /// </code>
    /// </example>
    public static bool Ignore(string reason, Func<bool> condition)
    {
        // Step 1: Validate parameters (ALWAYS - even if DBC disabled)
        ArgumentNullException.ThrowIfNull(reason);
        ArgumentNullException.ThrowIfNull(condition);

        // Step 2: Check recursion guard
        if (Entered.Value)
        {
            return false;
        }

        // Step 3: Check if enabled
        if (!Config.PreconditionsEnabled)
        {
            return condition();
        }

        // Step 4: Execute with guard
        try
        {
            Entered.Value = true;
            return condition();
        }
        finally
        {
            Entered.Value = false;
        }
    }

    /// <summary>
    ///     Captures the state of an object for use in postcondition validation.
    ///     Creates a deep copy via JSON serialization.
    /// </summary>
    /// <typeparam name="T">The type of object to capture. Must be JSON-serializable.</typeparam>
    /// <param name="supplier">Lazy-evaluated supplier that provides the object to capture.</param>
    /// <returns>
    ///     A deep copy of the object when postconditions are enabled;
    ///     default value when postconditions are disabled or recursion guard is active.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="supplier" /> is null.</exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the type is not JSON-serializable (e.g., delegates, DbContext).
    /// </exception>
    /// <remarks>
    ///     This method is disabled when the DBC_POST environment variable is set to "false" or "off".
    ///     The supplier is evaluated lazily to ensure zero overhead when contracts are disabled.
    ///     Uses a recursion guard to prevent infinite loops when contract checks trigger other contract checks.
    ///     Deep copy is performed via System.Text.Json serialization, which requires the type to be serializable.
    ///     This method supports both reference types and value types (no generic constraint).
    /// </remarks>
    /// <example>
    ///     <code>
    /// public void Transfer(decimal amount)
    /// {
    ///     var oldBalance = Contract.Old(() => _balance);
    /// 
    ///     _balance -= amount;
    /// 
    ///     Contract.Ensure("Balance decreased", () => _balance &lt; oldBalance);
    /// }
    /// </code>
    /// </example>
    public static T Old<T>(Func<T> supplier)
    {
        // Step 1: Validate parameters (ALWAYS - even if DBC disabled)
        ArgumentNullException.ThrowIfNull(supplier);

        // Step 2: Check recursion guard
        if (Entered.Value)
        {
            return default!;
        }

        // Step 3: Check if enabled
        if (!Config.PostconditionsEnabled)
        {
            return default!;
        }

        // Step 4: Execute with guard
        try
        {
            Entered.Value = true;

            T obj = supplier();
            if (obj is null)
            {
                return default!;
            }

            // Deep copy via JSON serialization
            string json = JsonSerializer.Serialize(obj, JsonOptions);
            return JsonSerializer.Deserialize<T>(json, JsonOptions)!;
        }
        catch (NotSupportedException ex)
        {
            throw new InvalidOperationException
            (
                $"Type {typeof(T).Name} cannot be serialized for Old<T>(). " +
                "Ensure the type is JSON-serializable.",
                ex
            );
        }
        finally
        {
            Entered.Value = false;
        }
    }
}