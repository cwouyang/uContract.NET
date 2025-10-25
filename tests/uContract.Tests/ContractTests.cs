using uContract.Exceptions;

namespace uContract.Tests;

// Test helper classes for Old<T>() tests
public class TestAccount
{
    public decimal Balance { get; set; }
    public string Owner { get; set; } = string.Empty;
}

public struct TestPoint
{
    public int X { get; set; }
    public int Y { get; set; }
}

public class NonSerializableType
{
    public Func<bool> Callback { get; set; } = () => true;
}

public class RequireTests
{
    [Fact]
    public void Require_DoesNotThrow_WhenConditionIsTrue()
    {
        const int x = 10;

        Exception? exception = Record.Exception
        (() =>
             Contract.Require("x must be positive", () => x > 0)
        );

        Assert.Null(exception);
    }

    [Fact]
    public void Require_ThrowsPreconditionViolationException_WhenConditionIsFalse()
    {
        const int x = -1;

        PreconditionViolationException exception = Assert.Throws<PreconditionViolationException>
        (() =>
             Contract.Require("x must be positive", () => x > 0)
        );

        Assert.NotNull(exception);
    }

    [Fact]
    public void Require_ExceptionMessage_HasCorrectFormat()
    {
        const int x = -1;
        const string description = "x must be positive";

        PreconditionViolationException exception = Assert.Throws<PreconditionViolationException>
        (() =>
             Contract.Require(description, () => x > 0)
        );

        Assert.Equal($"Precondition violated: {description}", exception.Message);
    }

    [Fact]
    public void Require_ThrowsArgumentNullException_WhenDescriptionIsNull()
    {
        string? description = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>
        (() =>
             Contract.Require(description!, () => true)
        );

        Assert.Equal("description", exception.ParamName);
    }

    [Fact]
    public void Require_ThrowsArgumentNullException_WhenConditionIsNull()
    {
        Func<bool>? condition = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>
        (() =>
             Contract.Require("some description", condition!)
        );

        Assert.Equal("condition", exception.ParamName);
    }

    [Fact]
    public void Require_DoesNotRecurse_WhenConditionCallsRequire()
    {
        int callCount = 0;

        PreconditionViolationException exception = Assert.Throws<PreconditionViolationException>
        (() =>
             Contract.Require
             (
                 "outer", () =>
                 {
                     callCount++;
                     // This inner Require should be ignored due to recursion guard
                     Contract.Require("inner", () => false);
                     return false;
                 }
             )
        );

        Assert.Equal(1, callCount);
        Assert.Contains("outer", exception.Message);
    }

    [Fact]
    public async Task Require_WorksCorrectly_InAsyncContext()
    {
        const int x = 10;

        await Task.Run
        (() =>
            {
                Contract.Require("x must be positive", () => x > 0);
            }
        );

        Assert.True(true);
    }

    [Fact]
    public async Task Require_ThrowsException_InAsyncContext_WhenConditionIsFalse()
    {
        const int x = -1;

        await Assert.ThrowsAsync<PreconditionViolationException>
        (async () =>
            {
                await Task.Run
                (() =>
                    {
                        Contract.Require("x must be positive", () => x > 0);
                    }
                );
            }
        );
    }

    [Fact]
    public void Require_EvaluatesConditionLazily()
    {
        bool conditionEvaluated = false;

        Contract.Require
        (
            "test", () =>
            {
                conditionEvaluated = true;
                return true;
            }
        );

        Assert.True(conditionEvaluated);
    }

    [Fact]
    public void Require_PropagatesException_WhenConditionThrows()
    {
        InvalidOperationException expectedException = new("test error");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>
        (() =>
             Contract.Require("test", () => throw expectedException)
        );

        Assert.Same(expectedException, exception);
    }
}

public class EnsureTests
{
    [Fact]
    public void Ensure_DoesNotThrow_WhenConditionIsTrue()
    {
        const int x = 10;

        Exception? exception = Record.Exception
        (() =>
             Contract.Ensure("x must be positive", () => x > 0)
        );

        Assert.Null(exception);
    }

    [Fact]
    public void Ensure_ThrowsPostconditionViolationException_WhenConditionIsFalse()
    {
        const int x = -1;

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>
        (() =>
             Contract.Ensure("x must be positive", () => x > 0)
        );

        Assert.NotNull(exception);
    }

    [Fact]
    public void Ensure_ExceptionMessage_HasCorrectFormat()
    {
        const int x = -1;
        const string description = "x must be positive";

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>
        (() =>
             Contract.Ensure(description, () => x > 0)
        );

        Assert.Equal($"Postcondition violated: {description}", exception.Message);
    }

    [Fact]
    public void Ensure_ThrowsArgumentNullException_WhenDescriptionIsNull()
    {
        string? description = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>
        (() =>
             Contract.Ensure(description!, () => true)
        );

        Assert.Equal("description", exception.ParamName);
    }

    [Fact]
    public void Ensure_ThrowsArgumentNullException_WhenConditionIsNull()
    {
        Func<bool>? condition = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>
        (() =>
             Contract.Ensure("some description", condition!)
        );

        Assert.Equal("condition", exception.ParamName);
    }

    [Fact]
    public void Ensure_DoesNotRecurse_WhenConditionCallsEnsure()
    {
        int callCount = 0;

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>
        (() =>
             Contract.Ensure
             (
                 "outer", () =>
                 {
                     callCount++;
                     // This inner Ensure should be ignored due to recursion guard
                     Contract.Ensure("inner", () => false);
                     return false;
                 }
             )
        );

        Assert.Equal(1, callCount);
        Assert.Contains("outer", exception.Message);
    }

    [Fact]
    public async Task Ensure_WorksCorrectly_InAsyncContext()
    {
        const int x = 10;

        await Task.Run
        (() =>
            {
                Contract.Ensure("x must be positive", () => x > 0);
            }
        );

        Assert.True(true);
    }

    [Fact]
    public async Task Ensure_ThrowsException_InAsyncContext_WhenConditionIsFalse()
    {
        const int x = -1;

        await Assert.ThrowsAsync<PostconditionViolationException>
        (async () =>
            {
                await Task.Run
                (() =>
                    {
                        Contract.Ensure("x must be positive", () => x > 0);
                    }
                );
            }
        );
    }

    [Fact]
    public void Ensure_EvaluatesConditionLazily()
    {
        bool conditionEvaluated = false;

        Contract.Ensure
        (
            "test", () =>
            {
                conditionEvaluated = true;
                return true;
            }
        );

        Assert.True(conditionEvaluated);
    }

    [Fact]
    public void Ensure_PropagatesException_WhenConditionThrows()
    {
        InvalidOperationException expectedException = new("test error");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>
        (() =>
             Contract.Ensure("test", () => throw expectedException)
        );

        Assert.Same(expectedException, exception);
    }
}

public class InvariantTests
{
    [Fact]
    public void Invariant_DoesNotThrow_WhenConditionIsTrue()
    {
        const int balance = 100;

        Exception? exception = Record.Exception
        (() =>
             Contract.Invariant("balance must be non-negative", () => balance >= 0)
        );

        Assert.Null(exception);
    }

    [Fact]
    public void Invariant_ThrowsInvariantViolationException_WhenConditionIsFalse()
    {
        const int balance = -1;

        InvariantViolationException exception = Assert.Throws<InvariantViolationException>
        (() =>
             Contract.Invariant("balance must be non-negative", () => balance >= 0)
        );

        Assert.NotNull(exception);
    }

    [Fact]
    public void Invariant_ExceptionMessage_HasCorrectFormat()
    {
        const int balance = -1;
        const string description = "balance must be non-negative";

        InvariantViolationException exception = Assert.Throws<InvariantViolationException>
        (() =>
             Contract.Invariant(description, () => balance >= 0)
        );

        Assert.Equal($"Invariant violated: {description}", exception.Message);
    }

    [Fact]
    public void Invariant_ThrowsArgumentNullException_WhenDescriptionIsNull()
    {
        string? description = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>
        (() =>
             Contract.Invariant(description!, () => true)
        );

        Assert.Equal("description", exception.ParamName);
    }

    [Fact]
    public void Invariant_ThrowsArgumentNullException_WhenConditionIsNull()
    {
        Func<bool>? condition = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>
        (() =>
             Contract.Invariant("some description", condition!)
        );

        Assert.Equal("condition", exception.ParamName);
    }

    [Fact]
    public void Invariant_DoesNotRecurse_WhenConditionCallsInvariant()
    {
        int callCount = 0;

        InvariantViolationException exception = Assert.Throws<InvariantViolationException>
        (() =>
             Contract.Invariant
             (
                 "outer", () =>
                 {
                     callCount++;
                     // This inner Invariant should be ignored due to recursion guard
                     Contract.Invariant("inner", () => false);
                     return false;
                 }
             )
        );

        Assert.Equal(1, callCount);
        Assert.Contains("outer", exception.Message);
    }

    [Fact]
    public async Task Invariant_WorksCorrectly_InAsyncContext()
    {
        const int balance = 100;

        await Task.Run
        (() =>
            {
                Contract.Invariant("balance must be non-negative", () => balance >= 0);
            }
        );

        Assert.True(true);
    }

    [Fact]
    public async Task Invariant_ThrowsException_InAsyncContext_WhenConditionIsFalse()
    {
        const int balance = -1;

        await Assert.ThrowsAsync<InvariantViolationException>
        (async () =>
            {
                await Task.Run
                (() =>
                    {
                        Contract.Invariant("balance must be non-negative", () => balance >= 0);
                    }
                );
            }
        );
    }

    [Fact]
    public void Invariant_EvaluatesConditionLazily()
    {
        bool conditionEvaluated = false;

        Contract.Invariant
        (
            "test", () =>
            {
                conditionEvaluated = true;
                return true;
            }
        );

        Assert.True(conditionEvaluated);
    }

    [Fact]
    public void Invariant_PropagatesException_WhenConditionThrows()
    {
        InvalidOperationException expectedException = new("test error");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>
        (() =>
             Contract.Invariant("test", () => throw expectedException)
        );

        Assert.Same(expectedException, exception);
    }
}

public class CheckTests
{
    [Fact]
    public void Check_DoesNotThrow_WhenConditionIsTrue()
    {
        const int value = 10;

        Exception? exception = Record.Exception
        (() =>
             Contract.Check("value must be positive", () => value > 0)
        );

        Assert.Null(exception);
    }

    [Fact]
    public void Check_ThrowsCheckViolationException_WhenConditionIsFalse()
    {
        const int value = -1;

        CheckViolationException exception = Assert.Throws<CheckViolationException>
        (() =>
             Contract.Check("value must be positive", () => value > 0)
        );

        Assert.NotNull(exception);
    }

    [Fact]
    public void Check_ExceptionMessage_HasCorrectFormat()
    {
        const int value = -1;
        const string description = "value must be positive";

        CheckViolationException exception = Assert.Throws<CheckViolationException>
        (() =>
             Contract.Check(description, () => value > 0)
        );

        Assert.Equal($"Check failed: {description}", exception.Message);
    }

    [Fact]
    public void Check_ThrowsArgumentNullException_WhenDescriptionIsNull()
    {
        string? description = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>
        (() =>
             Contract.Check(description!, () => true)
        );

        Assert.Equal("description", exception.ParamName);
    }

    [Fact]
    public void Check_ThrowsArgumentNullException_WhenConditionIsNull()
    {
        Func<bool>? condition = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>
        (() =>
             Contract.Check("some description", condition!)
        );

        Assert.Equal("condition", exception.ParamName);
    }

    [Fact]
    public void Check_DoesNotRecurse_WhenConditionCallsCheck()
    {
        int callCount = 0;

        CheckViolationException exception = Assert.Throws<CheckViolationException>
        (() =>
             Contract.Check
             (
                 "outer", () =>
                 {
                     callCount++;
                     // This inner Check should be ignored due to recursion guard
                     Contract.Check("inner", () => false);
                     return false;
                 }
             )
        );

        Assert.Equal(1, callCount);
        Assert.Contains("outer", exception.Message);
    }

    [Fact]
    public async Task Check_WorksCorrectly_InAsyncContext()
    {
        const int value = 10;

        await Task.Run
        (() =>
            {
                Contract.Check("value must be positive", () => value > 0);
            }
        );

        Assert.True(true);
    }

    [Fact]
    public async Task Check_ThrowsException_InAsyncContext_WhenConditionIsFalse()
    {
        const int value = -1;

        await Assert.ThrowsAsync<CheckViolationException>
        (async () =>
            {
                await Task.Run
                (() =>
                    {
                        Contract.Check("value must be positive", () => value > 0);
                    }
                );
            }
        );
    }

    [Fact]
    public void Check_EvaluatesConditionLazily()
    {
        bool conditionEvaluated = false;

        Contract.Check
        (
            "test", () =>
            {
                conditionEvaluated = true;
                return true;
            }
        );

        Assert.True(conditionEvaluated);
    }

    [Fact]
    public void Check_PropagatesException_WhenConditionThrows()
    {
        InvalidOperationException expectedException = new("test error");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>
        (() =>
             Contract.Check("test", () => throw expectedException)
        );

        Assert.Same(expectedException, exception);
    }
}

public class IgnoreTests
{
    [Fact]
    public void Ignore_ReturnsTrue_WhenConditionIsTrue()
    {
        const int x = 10;

        bool result = Contract.Ignore("x is positive", () => x > 0);

        Assert.True(result);
    }

    [Fact]
    public void Ignore_ReturnsFalse_WhenConditionIsFalse()
    {
        const int x = -1;

        bool result = Contract.Ignore("x is positive", () => x > 0);

        Assert.False(result);
    }

    [Fact]
    public void Ignore_ThrowsArgumentNullException_WhenReasonIsNull()
    {
        string? reason = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>
        (() =>
             Contract.Ignore(reason!, () => true)
        );

        Assert.Equal("reason", exception.ParamName);
    }

    [Fact]
    public void Ignore_ThrowsArgumentNullException_WhenConditionIsNull()
    {
        Func<bool>? condition = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>
        (() =>
             Contract.Ignore("some reason", condition!)
        );

        Assert.Equal("condition", exception.ParamName);
    }

    [Fact]
    public void Ignore_ReturnsFalse_WhenRecursionGuardActive()
    {
        bool innerResult = true;

        bool outerResult = Contract.Ignore
        (
            "outer", () =>
            {
                // This inner Ignore should return false due to recursion guard
                innerResult = Contract.Ignore("inner", () => true);
                return true;
            }
        );

        Assert.True(outerResult);
        Assert.False(innerResult);
    }

    [Fact]
    public async Task Ignore_WorksCorrectly_InAsyncContext()
    {
        const int value = 10;

        bool result = await Task.Run
        (() =>
             Contract.Ignore("value is positive", () => value > 0)
        );

        Assert.True(result);
    }

    [Fact]
    public async Task Ignore_ReturnsFalse_InAsyncContext_WhenConditionIsFalse()
    {
        const int value = -1;

        bool result = await Task.Run
        (() =>
             Contract.Ignore("value is positive", () => value > 0)
        );

        Assert.False(result);
    }

    [Fact]
    public void Ignore_EvaluatesConditionLazily()
    {
        bool conditionEvaluated = false;

        Contract.Ignore
        (
            "test", () =>
            {
                conditionEvaluated = true;
                return true;
            }
        );

        Assert.True(conditionEvaluated);
    }

    [Fact]
    public void Ignore_PropagatesException_WhenConditionThrows()
    {
        InvalidOperationException expectedException = new("test error");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>
        (() =>
             Contract.Ignore("test", () => throw expectedException)
        );

        Assert.Same(expectedException, exception);
    }

    [Fact]
    public void Ignore_SupportsEarlyReturnPattern()
    {
        const string currentEmail = "test@example.com";
        const string newEmail = "test@example.com";
        bool methodExecuted = false;

        ChangeEmail(newEmail);

        Assert.False(methodExecuted);
        return;

        void ChangeEmail(string email)
        {
            if (Contract.Ignore("Email unchanged", () => currentEmail == email))
            {
                return;
            }

            methodExecuted = true;
        }
    }

    [Fact]
    public void Ignore_AllowsMethodExecution_WhenConditionFalse()
    {
        const string currentEmail = "old@example.com";
        const string newEmail = "new@example.com";
        bool methodExecuted = false;

        ChangeEmail(newEmail);

        Assert.True(methodExecuted);
        return;

        void ChangeEmail(string email)
        {
            if (Contract.Ignore("Email unchanged", () => currentEmail == email))
            {
                return;
            }

            methodExecuted = true;
        }
    }
}

public class OldTests
{
    [Fact]
    public void Old_CapturesValue_WhenPostconditionsEnabled()
    {
        const decimal balance = 100m;

        decimal oldBalance = Contract.Old(() => balance);

        Assert.Equal(100m, oldBalance);
    }

    [Fact]
    public void Old_CreatesDeepCopy_NotReference()
    {
        TestAccount account = new() { Balance = 100m, Owner = "John" };

        TestAccount oldAccount = Contract.Old(() => account);

        // Modify original
        account.Balance = 200m;
        account.Owner = "Jane";

        // Old copy should be unchanged
        Assert.Equal(100m, oldAccount.Balance);
        Assert.Equal("John", oldAccount.Owner);
    }

    [Fact]
    public void Old_SupportsReferenceTypes()
    {
        TestAccount account = new() { Balance = 100m, Owner = "John" };

        TestAccount oldAccount = Contract.Old(() => account);

        Assert.NotNull(oldAccount);
        Assert.Equal(100m, oldAccount.Balance);
        Assert.Equal("John", oldAccount.Owner);
    }

    [Fact]
    public void Old_SupportsValueTypes()
    {
        TestPoint point = new() { X = 10, Y = 20 };

        TestPoint oldPoint = Contract.Old(() => point);

        Assert.Equal(10, oldPoint.X);
        Assert.Equal(20, oldPoint.Y);
    }

    [Fact]
    public void Old_SupportsPrimitiveTypes()
    {
        const int value = 42;

        int oldValue = Contract.Old(() => value);

        Assert.Equal(42, oldValue);
    }

    [Fact]
    public void Old_SupportsStrings()
    {
        const string text = "Hello";

        string oldText = Contract.Old(() => text);

        Assert.Equal("Hello", oldText);
    }

    [Fact]
    public void Old_ReturnsDefault_WhenSupplierReturnsNull()
    {
        TestAccount? account = null;

        TestAccount? oldAccount = Contract.Old(() => account);

        Assert.Null(oldAccount);
    }

    [Fact]
    public void Old_ThrowsArgumentNullException_WhenSupplierIsNull()
    {
        Func<int>? supplier = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>
        (() =>
             Contract.Old(supplier!)
        );

        Assert.Equal("supplier", exception.ParamName);
    }

    [Fact]
    public void Old_ReturnsDefault_WhenRecursionGuardActive()
    {
        const int outerValue = 100;
        int innerValue = 0;

        int outerOld = Contract.Old
        (() =>
            {
                // This inner Old should return default due to recursion guard
                innerValue = Contract.Old(() => 42);
                return outerValue;
            }
        );

        Assert.Equal(100, outerOld);
        Assert.Equal(0, innerValue);
    }

    [Fact]
    public async Task Old_WorksCorrectly_InAsyncContext()
    {
        const decimal balance = 100m;

        decimal oldBalance = await Task.Run
        (() =>
             Contract.Old(() => balance)
        );

        Assert.Equal(100m, oldBalance);
    }

    [Fact]
    public void Old_EvaluatesSupplierLazily()
    {
        bool supplierEvaluated = false;

        Contract.Old
        (() =>
            {
                supplierEvaluated = true;
                return 42;
            }
        );

        Assert.True(supplierEvaluated);
    }

    [Fact]
    public void Old_ThrowsInvalidOperationException_WhenTypeNotSerializable()
    {
        NonSerializableType obj = new();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>
        (() =>
             Contract.Old(() => obj)
        );

        Assert.Contains("cannot be serialized", exception.Message);
        Assert.IsType<NotSupportedException>(exception.InnerException);
    }

    [Fact]
    public void Old_UsedInPostcondition()
    {
        decimal balance = 100m;
        // ReSharper disable once AccessToModifiedClosure
        decimal oldBalance = Contract.Old(() => balance);

        balance -= 50m;

        Contract.Ensure("Balance decreased", () => balance < oldBalance);

        Assert.Equal(50m, balance);
    }

    [Fact]
    public void Old_PropagatesException_WhenSupplierThrows()
    {
        InvalidOperationException expectedException = new("test error");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>
        (() =>
             Contract.Old<int>(() => throw expectedException)
        );

        Assert.Same(expectedException, exception);
    }
}