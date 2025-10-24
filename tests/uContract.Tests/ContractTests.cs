using uContract.Exceptions;

namespace uContract.Tests;

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