using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
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
///     - DBC: default for every contract type whose own flag is not set
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
    ///     This method is disabled when DBC_PRE is set to "false", "off", "0" or "no" (case-insensitive).
    ///     When DBC_PRE is unset, empty or not recognised, DBC decides in the same way;
    ///     if neither decides, the method is enabled.
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
    ///     This method is disabled when DBC_POST is set to "false", "off", "0" or "no" (case-insensitive).
    ///     When DBC_POST is unset, empty or not recognised, DBC decides in the same way;
    ///     if neither decides, the method is enabled.
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
    ///     This method is disabled when DBC_INV is set to "false", "off", "0" or "no" (case-insensitive).
    ///     When DBC_INV is unset, empty or not recognised, DBC decides in the same way;
    ///     if neither decides, the method is enabled.
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
    ///     This method is disabled when DBC_CHECK is set to "false", "off", "0" or "no" (case-insensitive).
    ///     When DBC_CHECK is unset, empty or not recognised, DBC decides in the same way;
    ///     if neither decides, the method is enabled.
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
    ///     Unlike Require/Ensure/Invariant, it is never disabled and throws no contract violation of its own:
    ///     it evaluates the condition and returns its result whether preconditions are enabled or not.
    ///     The exception is a call made while another contract check is running: the recursion guard then
    ///     returns <c>false</c> without evaluating the condition.
    ///     DBC_PRE (or DBC, when DBC_PRE is not set) changes one thing only. While preconditions are enabled,
    ///     contract checks made inside the condition are skipped by the recursion guard; while they are
    ///     disabled, those checks run if their own contract type is enabled.
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
    ///     Creates a deep copy field by field, private state included.
    /// </summary>
    /// <typeparam name="T">The type of object to capture.</typeparam>
    /// <param name="supplier">Lazy-evaluated supplier that provides the object to capture.</param>
    /// <returns>
    ///     A deep copy of the object when postconditions are enabled;
    ///     default value when postconditions are disabled, recursion guard is active, or the supplier
    ///     returns null.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="supplier" /> is null.</exception>
    /// <remarks>
    ///     This method is disabled when DBC_POST is set to "false", "off", "0" or "no" (case-insensitive).
    ///     When DBC_POST is unset, empty or not recognised, DBC decides in the same way;
    ///     if neither decides, the method is enabled.
    ///     The supplier is evaluated lazily to ensure zero overhead when contracts are disabled.
    ///     Uses a recursion guard to prevent infinite loops when contract checks trigger other contract checks.
    ///     Each copied object starts as a bitwise clone, so every instance field keeps the original's value,
    ///     public or not, readonly or not, declared on the type or on a base class. Each reference-typed field
    ///     visible to reflection is then replaced by the copy of what it refers to. A <see cref="string" />,
    ///     a delegate and other resource or identity types are shared with the original, not copied.
    ///     An instance reached more than once is copied once, so cycles are reproduced.
    ///     No user code runs while copying: no constructor, property accessor, Equals, GetHashCode or
    ///     serialization callback.
    ///     This method supports both reference types and value types (no generic constraint).
    ///     An exception thrown by the supplier propagates unchanged. The copy itself throws nothing of its own.
    ///     Instances of types that wrap an operating system handle, a timer, a callback list or a lazily run factory
    ///     (for example any <see cref="System.IO.Stream" />, <c>Task</c>, <c>Lazy&lt;T&gt;</c>) and the
    ///     comparers of the base class library are shared, so state inside them is not snapshotted.
    ///     The copy is a read-only snapshot: delegates are shared, so raising an event on the copy notifies the
    ///     original's subscribers. Everything reachable is copied, so <c>Old(() =&gt; _balance)</c> is cheaper than
    ///     <c>Old(() =&gt; this)</c>.
    ///     Native AOT and trimming: a field that the trimmer removed from reflection keeps its bitwise value, so
    ///     the object it refers to is shared with the original. The fields declared on <typeparamref name="T" />
    ///     are preserved. To preserve other types, put
    ///     <c>[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(X))]</c> on <c>Main</c> or on any method
    ///     that runs, where <c>X</c> is the type. Neither <c>JsonSerializerIsReflectionEnabledByDefault</c> nor
    ///     dynamic code is needed. A caller that forwards its own generic parameter to this method gets warning
    ///     IL2091 unless it carries <c>[RequiresUnreferencedCode]</c> or the same
    ///     <c>[DynamicallyAccessedMembers]</c> annotation on that parameter.
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
    [RequiresUnreferencedCode(
        "Old<T> copies T field by field through reflection. Trimming preserves the fields declared on T. "
            + "These may not be preserved: private fields of T's base classes, fields of the types that T's fields refer to, and fields of runtime types other than T. "
            + "A field that is not preserved is copied bitwise, so an object it refers to is shared with the original. "
            + "Preserve such types with [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(X))]."
    )]
    public static T Old<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields
        )]
            T
    >(Func<T> supplier)
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

            return DeepCopier.Copy(obj);
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
    ///     This method is disabled when DBC_PRE is set to "false", "off", "0" or "no" (case-insensitive).
    ///     When DBC_PRE is unset, empty or not recognised, DBC decides in the same way;
    ///     if neither decides, the method is enabled.
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
    ///     This method is disabled when DBC_PRE is set to "false", "off", "0" or "no" (case-insensitive).
    ///     When DBC_PRE is unset, empty or not recognised, DBC decides in the same way;
    ///     if neither decides, the method is enabled.
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
    ///     This method is disabled when DBC_POST is set to "false", "off", "0" or "no" (case-insensitive).
    ///     When DBC_POST is unset, empty or not recognised, DBC decides in the same way;
    ///     if neither decides, the method is enabled.
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
    ///     This method is disabled when DBC_POST is set to "false", "off", "0" or "no" (case-insensitive).
    ///     When DBC_POST is unset, empty or not recognised, DBC decides in the same way;
    ///     if neither decides, the method is enabled.
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
    ///     specifically for null checks.
    ///     This method is disabled when DBC_INV is set to "false", "off", "0" or "no" (case-insensitive).
    ///     When DBC_INV is unset, empty or not recognised, DBC decides in the same way;
    ///     if neither decides, the method is enabled.
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
    ///     Evaluates reverse logical implication: <paramref name="consequent" /> follows from <paramref name="antecedent" />.
    ///     Semantically equivalent to <c>Imply(antecedent, consequent)</c> with arguments swapped (A ⟸ B).
    /// </summary>
    /// <param name="consequent">The condition that follows from the antecedent (the "then" side)</param>
    /// <param name="antecedent">The condition from which the consequent follows (the "if" side)</param>
    /// <returns>
    ///     True when either <paramref name="consequent" /> is true, or <paramref name="antecedent" /> is false;
    ///     false only when the antecedent holds but the consequent does not
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="consequent" /> or <paramref name="antecedent" /> is null
    /// </exception>
    /// <remarks>
    ///     This is a pure logic function used within contract conditions.
    ///     It does not throw contract violation exceptions and is not controlled by DBC configuration.
    ///     Reverse implication A ⟸ B is equivalent to A ∨ ¬B, which is logically identical to B → A.
    ///     <para>
    ///         Evaluation short-circuits: if <paramref name="consequent" /> returns true,
    ///         <paramref name="antecedent" /> is never invoked.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code>
    /// // "Discount greater than zero follows from customer being VIP"
    /// Contract.Require("VIP discount rule",
    ///     () => FollowsFrom(() => discount > 0, () => customer.IsVip));
    ///     </code>
    /// </example>
    public static bool FollowsFrom(Func<bool> consequent, Func<bool> antecedent)
    {
        ArgumentNullException.ThrowIfNull(consequent);
        ArgumentNullException.ThrowIfNull(antecedent);

        return consequent() || !antecedent();
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
    ///     This method is disabled when DBC_POST is set to "false", "off", "0" or "no" (case-insensitive).
    ///     When DBC_POST is unset, empty or not recognised, DBC decides in the same way;
    ///     if neither decides, the method is enabled.
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
            bool isImmutable =
                namespaceName?.StartsWith("System.Collections.Immutable", StringComparison.Ordinal) == true
                || typeName.StartsWith("Immutable", StringComparison.Ordinal)
                || typeName.StartsWith("ReadOnly", StringComparison.Ordinal);

            if (!isImmutable)
            {
                throw new PostconditionViolationException("Ensure resultingCollection is an immutable collection");
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
    ///     Compares the public instance properties and the instance fields of the compared type, non-public fields
    ///     included, throwing an exception if any non-assignable field has been modified.
    ///     Private fields declared on a base class are not compared.
    /// </summary>
    /// <typeparam name="T">
    ///     The type of objects to compare (supports both reference and value types).
    ///     Its public properties and its public and non-public fields are preserved for reflection in trimmed and
    ///     Native AOT applications. Members inherited from base classes are compared and preserved too, except
    ///     private fields declared on a base class.
    /// </typeparam>
    /// <param name="actual">The current state of the object. Null is compared as a value: see the remarks.</param>
    /// <param name="expected">
    ///     The expected (old) state of the object. Null is compared as a value: see the remarks.
    /// </param>
    /// <param name="assignableFieldPatterns">
    ///     Regular expression patterns matching top-level member names that are allowed to change.
    ///     A pattern matches anywhere in the member name (it is not anchored), so "Email" also matches
    ///     "EmailVerified". An auto-property declared on the compared type is compared twice, as the property
    ///     and as its backing field <c>&lt;Email&gt;k__BackingField</c>, so a pattern anchored with <c>^</c> and
    ///     <c>$</c> must cover both, for example <c>^(Email|&lt;Email&gt;k__BackingField)$</c>.
    ///     Examples: "Email", ".*Timestamp", "^_.*"
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="assignableFieldPatterns" /> is null.
    /// </exception>
    /// <exception cref="PostconditionViolationException">
    ///     Thrown when fields not marked as assignable have been modified, or when exactly one of
    ///     <paramref name="actual" /> and <paramref name="expected" /> is null (for a nullable value type: has
    ///     no value).
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown under Native AOT when <typeparamref name="T" /> has no properties or fields visible to
    ///     reflection and neither <paramref name="actual" /> nor <paramref name="expected" /> is null, or when the runtime type of a nested member or collection element that has to be compared
    ///     has none and its <c>Equals</c> reports the two values unequal (without an <c>Equals</c> override this
    ///     only means they are different instances).
    ///     The contract could not be checked, so this is not a contract violation. <see cref="object" /> is exempt.
    ///     Also thrown, in any build, when a public property that is reached by the comparison has no get method
    ///     visible through the compared type: a write-only property, or one whose getter was removed by trimming.
    ///     This applies to properties of <typeparamref name="T" />, of nested members, of collection elements and
    ///     of base classes. A property whose getter is non-public is compared as usual when the getter is declared
    ///     on the compared type; a getter that is not visible through the compared type — a private getter
    ///     declared on a base class, or a virtual property whose derived class overrides only the setter — is
    ///     reported by this rule.
    /// </exception>
    /// <remarks>
    ///     This method uses reflection to compare the public instance properties (indexers excluded)
    ///     and the instance fields of each compared type, non-public fields included. Private fields declared on a
    ///     base class are not compared. The walk goes deeper into nested objects and collection elements
    ///     without recursion, so cycles and deep graphs are safe.
    ///     Every comparison decides on the runtime types of the two values. Values of different runtime types are
    ///     unequal, except two sequences, which are compared by element. Value types are equal when their
    ///     <c>Equals</c> says so, and are otherwise compared by their fields. Dictionaries are compared entry by
    ///     entry in enumeration order. Delegates are equal when their methods match; their targets are not
    ///     compared. Other shared instances (see <see cref="Old{T}" />) are compared by reference; a
    ///     <see cref="string" /> is compared with <c>Equals</c> and a delegate by its methods.
    ///     Under Native AOT, a nested class whose members were not preserved is compared by its own
    ///     <c>Equals</c>: equal when it returns true. When it returns false, the method throws
    ///     <see cref="InvalidOperationException" /> rather than report that nothing changed. A type whose
    ///     <c>Equals</c> ignores state (for example entity equality by ID) therefore hides a change in such a
    ///     member; preserve the type with <c>[DynamicDependency]</c> to compare it member by member.
    ///     When <typeparamref name="T" /> itself has no visible members, the method throws and does not ask
    ///     <c>Equals</c>, unless <paramref name="actual" /> or <paramref name="expected" /> is null.
    ///     Reflection metadata is cached for performance (using <see cref="ConcurrentDictionary{TKey,TValue}" />).
    ///     This method is disabled when DBC_POST is set to "false", "off", "0" or "no" (case-insensitive).
    ///     When DBC_POST is unset, empty or not recognised, DBC decides in the same way;
    ///     if neither decides, the method is enabled.
    ///     When the method is disabled, or in a call made while another contract check is running, it returns
    ///     without comparing. Work started from inside such a check (a task, a timer, a continuation) counts
    ///     as such a call, also after the check has returned. <see cref="Old{T}" /> returns <c>default</c>
    ///     without running its supplier in the same cases, so the two calls together do nothing there.
    ///     When the method compares, null is a value: two nulls are equal, and exactly one null is a
    ///     violation, whatever the patterns, which are not examined then. The method never throws
    ///     <see cref="ArgumentNullException" /> for <paramref name="actual" /> or <paramref name="expected" />.
    ///     An <see cref="Old{T}" /> result taken while another contract check is running is <c>default</c>
    ///     (null for a reference type or a nullable value type). Comparing it later reports a violation that
    ///     did not happen, or misses one: take the snapshot outside such a check.
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
    [RequiresUnreferencedCode(
        "EnsureAssignable uses reflection to enumerate and compare fields and properties. The public properties and the public and non-public fields of the top-level type are preserved; the types of nested objects and collection elements are not."
    )]
    public static void EnsureAssignable<[DynamicallyAccessedMembers(MemberComparison.ComparedMembers)] T>(
        T actual,
        T expected,
        params string[] assignableFieldPatterns
    )
    {
        // Step 1: Validate the pattern array. It is checked in every call, first. actual and expected are
        // never rejected: a null is a value.
        ArgumentNullException.ThrowIfNull(assignableFieldPatterns);

        // Step 2: Check recursion guard and whether the method is enabled. A call that does not compare returns.
        if (Entered.Value || !Config.PostconditionsEnabled)
        {
            return;
        }

        // Step 3: Compare a null as a value, before the guard is set. Two nulls are equal; exactly one null is
        // a violation. The patterns and the metadata of T are not read.
        bool actualIsNull = actual is null;
        bool expectedIsNull = expected is null;
        if (actualIsNull || expectedIsNull)
        {
            if (actualIsNull && expectedIsNull)
            {
                return;
            }

            throw new PostconditionViolationException(
                expectedIsNull
                    ? "expected is null and actual is not. If expected came from Contract.Old, null can also mean "
                        + "that no snapshot was taken: Old returns default while another contract check is running."
                    : "actual is null and expected is not."
            );
        }

        // Step 4: Execute with guard
        try
        {
            Entered.Value = true;

            List<string> differences = _FindDifferences(actual, expected, assignableFieldPatterns);

            if (differences.Count > 0)
            {
                string message =
                    "Fields were modified that are not marked as assignable:\n"
                    + string.Join("\n", differences.Select(d => $"  - {d}"));
                throw new PostconditionViolationException(message);
            }
        }
        finally
        {
            Entered.Value = false;
        }
    }

    private static List<string> _FindDifferences<[DynamicallyAccessedMembers(MemberComparison.ComparedMembers)] T>(
        T actual,
        T expected,
        string[] assignableFieldPatterns
    )
    {
        Type type = typeof(T);
        TypeMetadata metadata = MemberComparison.GetOrCacheMetadata(type);

        if (MemberComparison.MembersAreHidden(type, metadata))
        {
            throw new InvalidOperationException(
                $"EnsureAssignable cannot compare {type}: no properties or fields are visible to reflection "
                    + "under Native AOT. If the type has members, the generic argument passed to EnsureAssignable "
                    + "is missing its [DynamicallyAccessedMembers] annotation; otherwise set DBC_POST=off "
                    + "(disables all postcondition checks)."
            );
        }

        List<string> differences = [];
        ComparisonContext context = new();

        // The top-level pair is in progress for the whole call, so a member leading back to it is not a
        // difference. (A value-type T is boxed here afresh, so that pair is never reached again.)
        context.EnterTopLevel(new ReferencePair(actual!, expected!));

        foreach (MemberAccessor member in metadata.Members)
        {
            if (MemberComparison.IsAssignable(member.Name, assignableFieldPatterns))
            {
                continue;
            }

            object? actualValue = member.GetValue(actual!);
            object? expectedValue = member.GetValue(expected!);

            if (!MemberComparison.AreEqual(actualValue, expectedValue, context))
            {
                if (context.Hidden is not null)
                {
                    throw MemberComparison.CannotCompareNested(
                        type,
                        MemberComparison.SourceName(member.Name),
                        context.Hidden
                    );
                }

                differences.Add(member.Name);
            }
        }

        return differences;
    }

    // ============================================================
    // CallerArgumentExpression overloads (ADR-0018)
    // ------------------------------------------------------------
    // These overloads let callers omit the description parameter;
    // the compiler captures the source text of the condition or
    // value argument via [CallerArgumentExpression]. The existing
    // description-first overloads above remain available unchanged.
    // ============================================================

    /// <summary>
    ///     Precondition overload that captures the source text of <paramref name="condition" /> via
    ///     <see cref="CallerArgumentExpressionAttribute" /> when <paramref name="description" /> is omitted.
    ///     See <see cref="Require(string, Func{bool})" /> for full semantics.
    /// </summary>
    /// <param name="condition">Lazy-evaluated boolean condition that must be true.</param>
    /// <param name="description">
    ///     Optional explicit description. When omitted, the compiler captures the source text of
    ///     <paramref name="condition" />.
    /// </param>
    /// <exception cref="PreconditionViolationException">Thrown when the condition evaluates to false.</exception>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="condition" /> is null, or when <paramref name="description" /> is
    ///     explicitly passed as null.
    /// </exception>
    /// <remarks>Added per ADR-0018.</remarks>
    public static void Require(
        Func<bool> condition,
        [CallerArgumentExpression(nameof(condition))] string? description = null
    )
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(description);

        if (Entered.Value)
        {
            return;
        }

        if (!Config.PreconditionsEnabled)
        {
            return;
        }

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
    ///     Postcondition overload that captures the source text of <paramref name="condition" /> via
    ///     <see cref="CallerArgumentExpressionAttribute" /> when <paramref name="description" /> is omitted.
    ///     See <see cref="Ensure(string, Func{bool})" /> for full semantics.
    /// </summary>
    /// <param name="condition">Lazy-evaluated boolean condition that must be true.</param>
    /// <param name="description">
    ///     Optional explicit description. When omitted, the compiler captures the source text of
    ///     <paramref name="condition" />.
    /// </param>
    /// <exception cref="PostconditionViolationException">Thrown when the condition evaluates to false.</exception>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="condition" /> is null, or when <paramref name="description" /> is
    ///     explicitly passed as null.
    /// </exception>
    /// <remarks>Added per ADR-0018.</remarks>
    public static void Ensure(
        Func<bool> condition,
        [CallerArgumentExpression(nameof(condition))] string? description = null
    )
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(description);

        if (Entered.Value)
        {
            return;
        }

        if (!Config.PostconditionsEnabled)
        {
            return;
        }

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
    ///     Invariant overload that captures the source text of <paramref name="condition" /> via
    ///     <see cref="CallerArgumentExpressionAttribute" /> when <paramref name="description" /> is omitted.
    ///     See <see cref="Invariant(string, Func{bool})" /> for full semantics.
    /// </summary>
    /// <param name="condition">Lazy-evaluated boolean condition that must be true.</param>
    /// <param name="description">
    ///     Optional explicit description. When omitted, the compiler captures the source text of
    ///     <paramref name="condition" />.
    /// </param>
    /// <exception cref="InvariantViolationException">Thrown when the condition evaluates to false.</exception>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="condition" /> is null, or when <paramref name="description" /> is
    ///     explicitly passed as null.
    /// </exception>
    /// <remarks>Added per ADR-0018.</remarks>
    public static void Invariant(
        Func<bool> condition,
        [CallerArgumentExpression(nameof(condition))] string? description = null
    )
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(description);

        if (Entered.Value)
        {
            return;
        }

        if (!Config.InvariantsEnabled)
        {
            return;
        }

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
    ///     Check overload that captures the source text of <paramref name="condition" /> via
    ///     <see cref="CallerArgumentExpressionAttribute" /> when <paramref name="description" /> is omitted.
    ///     See <see cref="Check(string, Func{bool})" /> for full semantics.
    /// </summary>
    /// <param name="condition">Lazy-evaluated boolean condition that must be true.</param>
    /// <param name="description">
    ///     Optional explicit description. When omitted, the compiler captures the source text of
    ///     <paramref name="condition" />.
    /// </param>
    /// <exception cref="CheckViolationException">Thrown when the condition evaluates to false.</exception>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="condition" /> is null, or when <paramref name="description" /> is
    ///     explicitly passed as null.
    /// </exception>
    /// <remarks>Added per ADR-0018.</remarks>
    public static void Check(
        Func<bool> condition,
        [CallerArgumentExpression(nameof(condition))] string? description = null
    )
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(description);

        if (Entered.Value)
        {
            return;
        }

        if (!Config.CheckEnabled)
        {
            return;
        }

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

    // Note: The value-based null-check methods (RequireNotNull<T>, RequireNotEmpty, EnsureNotNull<T>,
    // InvariantNotNull<T>) have NO CAE overload, contrary to the initial 8-overload plan in ADR-0018.
    // Two distinct C# overload-resolution limitations prevented implementation:
    //
    //  1) RequireNotEmpty(string, string) vs a proposed RequireNotEmpty(string? value, [CAE] string?
    //     description) reduce to the same (string, string) signature at the CLR level — nullable
    //     annotations are metadata, not part of overload resolution → CS0111 duplicate member.
    //
    //  2) RequireNotNull<T>(string, T?) where T : class vs a proposed RequireNotNull<T>(T? value,
    //     [CAE] string? description) where T : class produce an ambiguous call (CS0121) whenever
    //     T is inferred to be string, because both overloads then accept (string?, string?) and C#
    //     has no "more specific" tie-breaker between them. Using non-generic object? for the new
    //     overload resolves the ambiguity but silently accepts value types (e.g. RequireNotNull(42)
    //     compiles and passes), a footgun the existing where T : class guard prevents.
    //
    // Callers wanting CAE-style terse description-less null-checks can use the condition-based
    // overloads:
    //     Contract.Require(() => user is not null);
    //     Contract.Require(() => !string.IsNullOrEmpty(email));
    // ADR-0018 captures 4 overloads instead of the initially-planned 8; the condition-based
    // overloads (Require, Ensure, Invariant, Check) above cover the bulk of the UX value.
}
