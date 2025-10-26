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

public class RequireNotNullTests
{
    [Fact]
    public void RequireNotNull_DoesNotThrow_WhenValueIsNotNull()
    {
        const string value = "not null";

        Exception? exception = Record.Exception
        (() =>
             Contract.RequireNotNull("Value", value)
        );

        Assert.Null(exception);
    }

    [Fact]
    public void RequireNotNull_ThrowsPreconditionViolationException_WhenValueIsNull()
    {
        string? value = null;

        PreconditionViolationException exception = Assert.Throws<PreconditionViolationException>
        (() =>
             Contract.RequireNotNull("Value", value)
        );

        Assert.NotNull(exception);
    }

    [Fact]
    public void RequireNotNull_ExceptionMessage_HasCorrectFormat()
    {
        string? value = null;
        const string description = "Value must not be null";

        PreconditionViolationException exception = Assert.Throws<PreconditionViolationException>
        (() =>
             Contract.RequireNotNull(description, value)
        );

        Assert.Equal($"Precondition violated: {description}", exception.Message);
    }

    [Fact]
    public void RequireNotNull_ThrowsArgumentNullException_WhenDescriptionIsNull()
    {
        string? description = null;
        const string value = "not null";

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>
        (() =>
             Contract.RequireNotNull(description!, value)
        );

        Assert.Equal("description", exception.ParamName);
    }

    [Fact]
    public void RequireNotNull_DoesNotRecurse_WhenRecursionGuardActive()
    {
        int callCount = 0;
        string? nullValue = null;

        PreconditionViolationException exception = Assert.Throws<PreconditionViolationException>
        (() =>
             Contract.Require
             (
                 "outer", () =>
                 {
                     callCount++;
                     // This inner RequireNotNull should be ignored due to recursion guard
                     Contract.RequireNotNull("inner", nullValue);
                     return false;
                 }
             )
        );

        Assert.Equal(1, callCount);
        Assert.Contains("outer", exception.Message);
    }

    [Fact]
    public async Task RequireNotNull_WorksCorrectly_InAsyncContext()
    {
        const string value = "not null";

        await Task.Run
        (() =>
            {
                Contract.RequireNotNull("Value", value);
            }
        );

        Assert.True(true);
    }

    [Fact]
    public async Task RequireNotNull_ThrowsException_InAsyncContext_WhenValueIsNull()
    {
        string? value = null;

        await Assert.ThrowsAsync<PreconditionViolationException>
        (async () =>
            {
                await Task.Run
                (() =>
                    {
                        Contract.RequireNotNull("Value", value);
                    }
                );
            }
        );
    }

    [Fact]
    public void RequireNotNull_SupportsReferenceTypes()
    {
        TestAccount account = new() { Balance = 100m, Owner = "John" };

        Exception? exception = Record.Exception
        (() =>
             Contract.RequireNotNull("Account", account)
        );

        Assert.Null(exception);
    }
}

public class RequireNotEmptyTests
{
    [Fact]
    public void RequireNotEmpty_DoesNotThrow_WhenValueIsNotEmpty()
    {
        const string value = "not empty";

        Exception? exception = Record.Exception
        (() =>
             Contract.RequireNotEmpty("Value", value)
        );

        Assert.Null(exception);
    }

    [Fact]
    public void RequireNotEmpty_ThrowsPreconditionViolationException_WhenValueIsEmpty()
    {
        const string value = "";

        PreconditionViolationException exception = Assert.Throws<PreconditionViolationException>
        (() =>
             Contract.RequireNotEmpty("Value", value)
        );

        Assert.NotNull(exception);
    }

    [Fact]
    public void RequireNotEmpty_ThrowsPreconditionViolationException_WhenValueIsNull()
    {
        string? value = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>
        (() =>
             Contract.RequireNotEmpty("Value", value!)
        );

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void RequireNotEmpty_ExceptionMessage_HasCorrectFormat()
    {
        const string value = "";
        const string description = "Value must not be empty";

        PreconditionViolationException exception = Assert.Throws<PreconditionViolationException>
        (() =>
             Contract.RequireNotEmpty(description, value)
        );

        Assert.Equal($"Precondition violated: {description}", exception.Message);
    }

    [Fact]
    public void RequireNotEmpty_ThrowsArgumentNullException_WhenDescriptionIsNull()
    {
        string? description = null;
        const string value = "not empty";

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>
        (() =>
             Contract.RequireNotEmpty(description!, value)
        );

        Assert.Equal("description", exception.ParamName);
    }

    [Fact]
    public void RequireNotEmpty_DoesNotRecurse_WhenRecursionGuardActive()
    {
        int callCount = 0;
        const string emptyValue = "";

        PreconditionViolationException exception = Assert.Throws<PreconditionViolationException>
        (() =>
             Contract.Require
             (
                 "outer", () =>
                 {
                     callCount++;
                     // This inner RequireNotEmpty should be ignored due to recursion guard
                     Contract.RequireNotEmpty("inner", emptyValue);
                     return false;
                 }
             )
        );

        Assert.Equal(1, callCount);
        Assert.Contains("outer", exception.Message);
    }

    [Fact]
    public async Task RequireNotEmpty_WorksCorrectly_InAsyncContext()
    {
        const string value = "not empty";

        await Task.Run
        (() =>
            {
                Contract.RequireNotEmpty("Value", value);
            }
        );

        Assert.True(true);
    }
}