using uContract.Exceptions;

namespace uContract.Tests;

public class RequireTests
{
    [Fact]
    public void Require_WhenConditionIsTrue_DoesNotThrow()
    {
        const int x = 10;

        Exception? exception = Record.Exception
        (() =>
             Contract.Require("x must be positive", () => x > 0)
        );

        Assert.Null(exception);
    }

    [Fact]
    public void Require_WhenConditionIsFalse_ThrowsPreconditionViolationException()
    {
        const int x = -1;

        PreconditionViolationException exception = Assert.Throws<PreconditionViolationException>
        (() =>
             Contract.Require("x must be positive", () => x > 0)
        );

        Assert.NotNull(exception);
    }

    [Fact]
    public void Require_WhenConditionIsFalse_ExceptionMessageHasCorrectFormat()
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
    public void Require_WhenDescriptionIsNull_ThrowsArgumentNullException()
    {
        string? description = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>
        (() =>
             Contract.Require(description!, () => true)
        );

        Assert.Equal("description", exception.ParamName);
    }

    [Fact]
    public void Require_WhenConditionIsNull_ThrowsArgumentNullException()
    {
        Func<bool>? condition = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>
        (() =>
             Contract.Require("some description", condition!)
        );

        Assert.Equal("condition", exception.ParamName);
    }

    [Fact]
    public void Require_WhenConditionCallsRequire_DoesNotRecurse()
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
        Assert.Contains("outer", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Require_WhenUsedInAsyncContext_WorksCorrectly()
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
    public async Task Require_WhenConditionIsFalseInAsyncContext_ThrowsException()
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
    public void Require_WhenCalled_EvaluatesConditionLazily()
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
    public void Require_WhenConditionThrows_PropagatesException()
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
    public void RequireNotNull_WhenValueIsNotNull_DoesNotThrow()
    {
        const string value = "not null";

        Exception? exception = Record.Exception
        (() =>
             Contract.RequireNotNull("Value", value)
        );

        Assert.Null(exception);
    }

    [Fact]
    public void RequireNotNull_WhenValueIsNull_ThrowsPreconditionViolationException()
    {
        string? value = null;

        PreconditionViolationException exception = Assert.Throws<PreconditionViolationException>
        (() =>
             Contract.RequireNotNull("Value", value)
        );

        Assert.NotNull(exception);
    }

    [Fact]
    public void RequireNotNull_WhenValueIsNull_ExceptionMessageHasCorrectFormat()
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
    public void RequireNotNull_WhenDescriptionIsNull_ThrowsArgumentNullException()
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
    public void RequireNotNull_WhenRecursionGuardActive_DoesNotRecurse()
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
        Assert.Contains("outer", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequireNotNull_WhenUsedInAsyncContext_WorksCorrectly()
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
    public async Task RequireNotNull_WhenValueIsNullInAsyncContext_ThrowsException()
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
    public void RequireNotNull_WhenUsedWithReferenceTypes_Works()
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
    public void RequireNotEmpty_WhenValueIsNotEmpty_DoesNotThrow()
    {
        const string value = "not empty";

        Exception? exception = Record.Exception
        (() =>
             Contract.RequireNotEmpty("Value", value)
        );

        Assert.Null(exception);
    }

    [Fact]
    public void RequireNotEmpty_WhenValueIsEmpty_ThrowsPreconditionViolationException()
    {
        const string value = "";

        PreconditionViolationException exception = Assert.Throws<PreconditionViolationException>
        (() =>
             Contract.RequireNotEmpty("Value", value)
        );

        Assert.NotNull(exception);
    }

    [Fact]
    public void RequireNotEmpty_WhenValueIsNull_ThrowsPreconditionViolationException()
    {
        string? value = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>
        (() =>
             Contract.RequireNotEmpty("Value", value!)
        );

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void RequireNotEmpty_WhenValueIsEmpty_ExceptionMessageHasCorrectFormat()
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
    public void RequireNotEmpty_WhenDescriptionIsNull_ThrowsArgumentNullException()
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
    public void RequireNotEmpty_WhenRecursionGuardActive_DoesNotRecurse()
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
        Assert.Contains("outer", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequireNotEmpty_WhenUsedInAsyncContext_WorksCorrectly()
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