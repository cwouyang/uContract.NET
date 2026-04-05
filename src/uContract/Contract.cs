using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
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

    private static readonly ConcurrentDictionary<Type, TypeMetadata> MetadataCache = new();

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

    /// <summary>
    ///     Validates that a reference type value is not null as a precondition.
    /// </summary>
    /// <typeparam name="T">The reference type to check (must be a class).</typeparam>
    /// <param name="description">Human-readable description of the precondition.</param>
    /// <param name="value">The value to check for null (may be null).</param>
    /// <exception cref="PreconditionViolationException">Thrown when the value is null.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="description" /> is null.</exception>
    /// <remarks>
    ///     This method is disabled when the DBC_PRE environment variable is set to "false" or "off".
    ///     Uses a recursion guard to prevent infinite loops when contract checks trigger other contract checks.
    ///     This is a convenience method that provides clearer intent than Require(() => value != null).
    /// </remarks>
    /// <example>
    ///     <code>
    /// public void SetOwner(string owner)
    /// {
    ///     Contract.RequireNotNull("Owner", owner);
    ///     _owner = owner;
    /// }
    /// </code>
    /// </example>
    public static void RequireNotNull<T>(string description, T? value)
        where T : class
    {
        // Step 1: Validate parameters (ALWAYS - even if DBC disabled)
        ArgumentNullException.ThrowIfNull(description);

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
            if (value is null)
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
    ///     Validates that a string is not null or empty as a precondition.
    /// </summary>
    /// <param name="description">Human-readable description of the precondition.</param>
    /// <param name="value">The string to check (must not be null or empty).</param>
    /// <exception cref="PreconditionViolationException">Thrown when the value is null or empty.</exception>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="description" /> or <paramref name="value" /> is null.
    /// </exception>
    /// <remarks>
    ///     This method is disabled when the DBC_PRE environment variable is set to "false" or "off".
    ///     Uses a recursion guard to prevent infinite loops when contract checks trigger other contract checks.
    ///     This is a convenience method that provides clearer intent than Require(() => !string.IsNullOrEmpty(value)).
    /// </remarks>
    /// <example>
    ///     <code>
    /// public void SetEmail(string email)
    /// {
    ///     Contract.RequireNotEmpty("Email", email);
    ///     _email = email;
    /// }
    /// </code>
    /// </example>
    public static void RequireNotEmpty(string description, string value)
    {
        // Step 1: Validate parameters (ALWAYS - even if DBC disabled)
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(value);

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
            if (string.IsNullOrEmpty(value))
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
    ///     Validates that a reference type value is not null as a postcondition.
    /// </summary>
    /// <typeparam name="T">The reference type to check (must be a class).</typeparam>
    /// <param name="description">Human-readable description of the postcondition.</param>
    /// <param name="value">The value to check for null (may be null).</param>
    /// <exception cref="PostconditionViolationException">Thrown when the value is null.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="description" /> is null.</exception>
    /// <remarks>
    ///     This method is disabled when the DBC_POST environment variable is set to "false" or "off".
    ///     Uses a recursion guard to prevent infinite loops when contract checks trigger other contract checks.
    ///     This is a convenience method that provides clearer intent than Ensure(() => value != null).
    /// </remarks>
    /// <example>
    ///     <code>
    /// public User GetUser(string id)
    /// {
    ///     var user = _repository.Find(id);
    ///     Contract.EnsureNotNull("User found", user);
    ///     return user;
    /// }
    /// </code>
    /// </example>
    public static void EnsureNotNull<T>(string description, T? value)
        where T : class
    {
        // Step 1: Validate parameters (ALWAYS - even if DBC disabled)
        ArgumentNullException.ThrowIfNull(description);

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
            if (value is null)
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
    ///     Validates a postcondition and returns the result value for method chaining.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="description">Human-readable description of the postcondition.</param>
    /// <param name="result">The result value to validate and return.</param>
    /// <param name="assertion">Predicate that validates the result (receives result, returns bool).</param>
    /// <returns>The result value if the assertion passes.</returns>
    /// <exception cref="PostconditionViolationException">Thrown when the assertion returns false.</exception>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="description" /> or <paramref name="assertion" /> is null.
    /// </exception>
    /// <remarks>
    ///     This method is disabled when the DBC_POST environment variable is set to "false" or "off".
    ///     Uses a recursion guard to prevent infinite loops when contract checks trigger other contract checks.
    ///     This method returns the result value, enabling fluent method chaining in query methods.
    /// </remarks>
    /// <example>
    ///     <code>
    /// public User GetUser(string id)
    /// {
    ///     var user = _repository.Find(id);
    ///     return Contract.EnsureResult("User found", user, u => u != null);
    /// }
    /// </code>
    /// </example>
    public static T EnsureResult<T>(string description, T result, Func<T, bool> assertion)
    {
        // Step 1: Validate parameters (ALWAYS - even if DBC disabled)
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(assertion);

        // Step 2: Check recursion guard
        if (Entered.Value)
        {
            return result;
        }

        // Step 3: Check if enabled
        if (!Config.PostconditionsEnabled)
        {
            return result;
        }

        // Step 4: Execute with guard
        try
        {
            Entered.Value = true;
            return assertion(result) ? result : throw new PostconditionViolationException(description);
        }
        finally
        {
            Entered.Value = false;
        }
    }

    /// <summary>
    ///     Checks an invariant to ensure the specified value is not null.
    ///     Throws <see cref="InvariantViolationException" /> if the value is null and invariants are enabled.
    /// </summary>
    /// <typeparam name="T">The reference type of the value to check</typeparam>
    /// <param name="description">A description of the invariant being checked</param>
    /// <param name="value">The value to check for null</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="description" /> is null</exception>
    /// <exception cref="InvariantViolationException">Thrown when <paramref name="value" /> is null and invariants are enabled</exception>
    /// <remarks>
    ///     This method is a convenience wrapper around <see cref="Invariant(string, Func{bool})" />
    ///     specifically for null checks. It is controlled by the DBC_INV environment variable.
    /// </remarks>
    /// <example>
    ///     <code>
    /// Contract.InvariantNotNull("Balance must exist", _balance);
    /// </code>
    /// </example>
    public static void InvariantNotNull<T>(string description, T? value)
        where T : class
    {
        // Step 1: Validate parameters (ALWAYS - even if DBC disabled)
        ArgumentNullException.ThrowIfNull(description);

        // Step 2: Check recursion guard
        if (Entered.Value)
        {
            return;
        }

        // Step 3: Check if invariants enabled
        if (!Config.InvariantsEnabled)
        {
            return;
        }

        // Step 4: Execute with guard
        try
        {
            Entered.Value = true;
            if (value is null)
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
    ///     Evaluates logical implication: if <paramref name="antecedent" /> is true, then <paramref name="consequent" /> must
    ///     be true.
    ///     Returns true when the implication holds (¬A ∨ B).
    /// </summary>
    /// <param name="antecedent">The condition that implies the consequent (the "if" part)</param>
    /// <param name="consequent">The condition that follows from the antecedent (the "then" part)</param>
    /// <returns>
    ///     True if the implication holds (antecedent is false OR consequent is true);
    ///     false if antecedent is true but consequent is false
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="antecedent" /> or <paramref name="consequent" /> is
    ///     null
    /// </exception>
    /// <remarks>
    ///     This is a pure logic function used within contract conditions.
    ///     It does not throw contract violation exceptions and is not controlled by DBC configuration.
    ///     Logical implication A → B is equivalent to ¬A ∨ B.
    /// </remarks>
    /// <example>
    ///     <code>
    /// // "If customer is VIP, then discount must be > 0"
    /// Contract.Require("VIP discount rule",
    ///     () => Imply(() => customer.IsVip, () => discount > 0));
    /// </code>
    /// </example>
    public static bool Imply(Func<bool> antecedent, Func<bool> consequent)
    {
        ArgumentNullException.ThrowIfNull(antecedent);
        ArgumentNullException.ThrowIfNull(consequent);

        return !antecedent() || consequent();
    }

    /// <summary>
    ///     Evaluates logical biconditional: <paramref name="a" /> is true if and only if <paramref name="b" /> is true.
    ///     Returns true when both conditions have the same truth value (A ⟺ B).
    /// </summary>
    /// <param name="a">The first condition</param>
    /// <param name="b">The second condition</param>
    /// <returns>
    ///     True if both conditions are true or both are false;
    ///     false if one is true and the other is false
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="a" /> or <paramref name="b" /> is null</exception>
    /// <remarks>
    ///     This is a pure logic function used within contract conditions.
    ///     It does not throw contract violation exceptions and is not controlled by DBC configuration.
    ///     Logical biconditional A ⟺ B is equivalent to (A ∧ B) ∨ (¬A ∧ ¬B).
    /// </remarks>
    /// <example>
    ///     <code>
    /// // "Order is paid if and only if payment status is Completed"
    /// Contract.Invariant("Payment consistency",
    ///     () => IfAndOnlyIf(() => order.IsPaid, () => order.PaymentStatus == Status.Completed));
    /// </code>
    /// </example>
    public static bool IfAndOnlyIf(Func<bool> a, Func<bool> b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        bool aResult = a();
        bool bResult = b();
        return (aResult && bResult) || (!aResult && !bResult);
    }

    /// <summary>
    ///     Ensures that the specified collection is immutable in postconditions.
    ///     Returns the collection if it is immutable, or throws <see cref="PostconditionViolationException" /> if mutable.
    /// </summary>
    /// <typeparam name="T">The type of the collection</typeparam>
    /// <param name="collection">The collection to verify for immutability</param>
    /// <returns>The original collection if it is immutable</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="collection" /> is null</exception>
    /// <exception cref="PostconditionViolationException">Thrown when the collection is mutable and postconditions are enabled</exception>
    /// <remarks>
    ///     This method verifies that a collection returned from a method is immutable.
    ///     It checks if the type is from System.Collections.Immutable namespace or
    ///     if the type name starts with "Immutable" or "ReadOnly".
    ///     Controlled by the DBC_POST environment variable.
    /// </remarks>
    /// <example>
    ///     <code>
    /// public ImmutableList&lt;string&gt; GetNames()
    /// {
    ///     var names = ImmutableList.Create("Alice", "Bob");
    ///     return Contract.EnsureImmutableCollection(names);
    /// }
    /// </code>
    /// </example>
    public static T EnsureImmutableCollection<T>(T collection)
    {
        // Step 1: Validate parameters (ALWAYS - even if DBC disabled)
        ArgumentNullException.ThrowIfNull(collection);

        // Step 2: Check recursion guard
        if (Entered.Value)
        {
            return collection;
        }

        // Step 3: Check if postconditions enabled
        if (!Config.PostconditionsEnabled)
        {
            return collection;
        }

        // Step 4: Execute with guard
        try
        {
            Entered.Value = true;

            Type collectionType = collection.GetType();
            string typeName = collectionType.Name;
            string? namespaceName = collectionType.Namespace;

            // Check if type is from System.Collections.Immutable namespace
            bool isImmutable = namespaceName?.StartsWith("System.Collections.Immutable", StringComparison.Ordinal) == true
                               || typeName.StartsWith("Immutable", StringComparison.Ordinal)
                               || typeName.StartsWith("ReadOnly", StringComparison.Ordinal);

            if (!isImmutable)
            {
                throw new PostconditionViolationException
                (
                    "Ensure resultingCollection is an immutable collection"
                );
            }

            return collection;
        }
        finally
        {
            Entered.Value = false;
        }
    }

    /// <summary>
    ///     Checks if an operation throws <see cref="NotSupportedException" />, indicating it is unsupported.
    ///     Returns true if the operation is unsupported (throws NotSupportedException).
    /// </summary>
    /// <param name="action">The action to execute and check</param>
    /// <returns>
    ///     True if the action throws <see cref="NotSupportedException" />;
    ///     false if no exception is thrown or a different exception is thrown
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action" /> is null</exception>
    /// <remarks>
    ///     This is a pure utility function for testing immutability enforcement.
    ///     It does not throw contract violation exceptions and is not controlled by DBC configuration.
    ///     Use this to verify that immutable collections properly reject mutation operations.
    /// </remarks>
    /// <example>
    ///     <code>
    /// ImmutableList&lt;string&gt; names = ImmutableList.Create("Alice");
    /// // Verify that Add is not supported (immutable list doesn't have Add)
    /// bool isImmutable = Contract.CheckUnsupportedOperation(() =>
    /// {
    ///     var list = (IList&lt;string&gt;)names;
    ///     list.Add("Bob");
    /// });
    /// </code>
    /// </example>
    public static bool CheckUnsupportedOperation(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            action();
            return false;
        }
        catch (NotSupportedException)
        {
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    ///     Ensures that only specified fields have changed between two object states.
    ///     Compares all public properties and fields, throwing an exception if any non-assignable field has been modified.
    /// </summary>
    /// <typeparam name="T">The type of objects to compare (supports both reference and value types)</typeparam>
    /// <param name="actual">The current state of the object</param>
    /// <param name="expected">The expected (old) state of the object</param>
    /// <param name="assignableFieldPatterns">
    ///     Regular expression patterns matching field names that are allowed to change.
    ///     Examples: "Email", ".*Timestamp", "^_.*"
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="actual" />, <paramref name="expected" />, or
    ///     <paramref name="assignableFieldPatterns" /> is null
    /// </exception>
    /// <exception cref="PostconditionViolationException">
    ///     Thrown when fields not marked as assignable have been modified
    /// </exception>
    /// <remarks>
    ///     This method uses reflection to compare all public properties and private fields recursively.
    ///     Reflection metadata is cached for performance (using <see cref="ConcurrentDictionary{TKey,TValue}" />).
    ///     This method is disabled when the DBC_POST environment variable is set to "false" or "off".
    ///     Uses a recursion guard to prevent infinite loops when contract checks trigger other contract checks.
    ///     Pattern matching uses <see cref="Regex" /> for flexible field name matching.
    /// </remarks>
    /// <example>
    ///     <code>
    /// public void ChangeEmail(string newEmail)
    /// {
    ///     var oldState = Contract.Old(() => this);
    ///     _email = newEmail;
    ///     // Only email field should have changed
    ///     Contract.EnsureAssignable(this, oldState, nameof(_email));
    /// }
    /// </code>
    /// </example>
    public static void EnsureAssignable<T>(T actual, T expected, params string[] assignableFieldPatterns)
    {
        // Step 1: Validate parameters (ALWAYS - even if DBC disabled)
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(assignableFieldPatterns);

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

            List<string> differences = _FindDifferences(actual, expected, assignableFieldPatterns);

            if (differences.Count > 0)
            {
                string message = "Postcondition violated: Fields were modified that are not marked as assignable:\n" +
                                 string.Join("\n", differences.Select(d => $"  - {d}"));
                throw new PostconditionViolationException(message);
            }
        }
        finally
        {
            Entered.Value = false;
        }
    }

    private static List<string> _FindDifferences<T>(T actual, T expected, string[] assignableFieldPatterns)
    {
        Type type = typeof(T);
        TypeMetadata metadata = _GetOrCacheMetadata(type);
        List<string> differences = [];

        foreach (MemberAccessor member in metadata.Members)
        {
            if (_IsAssignable(member.Name, assignableFieldPatterns))
            {
                continue;
            }

            object? actualValue = member.GetValue(actual!);
            object? expectedValue = member.GetValue(expected!);

            if (!_AreEqual(actualValue, expectedValue, member.MemberType))
            {
                differences.Add(member.Name);
            }
        }

        return differences;
    }

    private static TypeMetadata _GetOrCacheMetadata(Type type)
    {
        return MetadataCache.GetOrAdd
        (
            type, t =>
            {
                PropertyInfo[] properties = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                FieldInfo[] fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                List<MemberAccessor> members = properties
                                               .Where(p => p.GetIndexParameters().Length == 0)
                                               .Cast<MemberInfo>()
                                               .Concat(fields)
                                               .Select(m => new MemberAccessor(m))
                                               .ToList();

                return new TypeMetadata { Members = members };
            }
        );
    }

    private static bool _IsAssignable(string fieldName, string[] patterns)
    {
        if (patterns == null || patterns.Length == 0)
        {
            return false;
        }

        return patterns.Any(pattern => Regex.IsMatch(fieldName, pattern, RegexOptions.None, TimeSpan.FromSeconds(1)));
    }

    private static bool _AreEqual(object? actual, object? expected, Type memberType)
    {
        if (ReferenceEquals(actual, expected))
        {
            return true;
        }

        if (actual is null || expected is null)
        {
            return false;
        }

        if (memberType.IsValueType || memberType == typeof(string))
        {
            return Equals(actual, expected);
        }

        if (actual is IEnumerable actualEnum && expected is IEnumerable expectedEnum)
        {
            return _CompareCollections(actualEnum, expectedEnum);
        }

        return memberType.IsClass ? _CompareRecursively(actual, expected) : Equals(actual, expected);
    }

    private static bool _CompareCollections(IEnumerable actual, IEnumerable expected)
    {
        object?[] actualArray = actual.Cast<object?>().ToArray();
        object?[] expectedArray = expected.Cast<object?>().ToArray();

        if (actualArray.Length != expectedArray.Length)
        {
            return false;
        }

        for (int i = 0; i < actualArray.Length; i++)
        {
            object? actualItem = actualArray[i];
            object? expectedItem = expectedArray[i];

            if (actualItem == null && expectedItem == null)
            {
                continue;
            }

            if (actualItem == null || expectedItem == null)
            {
                return false;
            }

            Type itemType = actualItem.GetType();
            if (!_AreEqual(actualItem, expectedItem, itemType))
            {
                return false;
            }
        }

        return true;
    }

    private static bool _CompareRecursively(object actual, object expected)
    {
        Type type = actual.GetType();
        TypeMetadata metadata = _GetOrCacheMetadata(type);

        foreach (MemberAccessor member in metadata.Members)
        {
            object? actualValue = member.GetValue(actual);
            object? expectedValue = member.GetValue(expected);

            if (!_AreEqual(actualValue, expectedValue, member.MemberType))
            {
                return false;
            }
        }

        return true;
    }
}