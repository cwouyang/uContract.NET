using uContract.Exceptions;

namespace uContract.Tests;

public class InvariantTests
{
    [Fact]
    public void Invariant_WhenConditionIsTrue_DoesNotThrow()
    {
        const int balance = 100;

        Exception? exception = Record.Exception
        (() =>
             Contract.Invariant("balance must be non-negative", () => balance >= 0)
        );

        Assert.Null(exception);
    }

    [Fact]
    public void Invariant_WhenConditionIsFalse_ThrowsInvariantViolationException()
    {
        const int balance = -1;

        InvariantViolationException exception = Assert.Throws<InvariantViolationException>
        (() =>
             Contract.Invariant("balance must be non-negative", () => balance >= 0)
        );

        Assert.NotNull(exception);
    }

    [Fact]
    public void Invariant_WhenConditionIsFalse_ExceptionMessageHasCorrectFormat()
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
    public void Invariant_WhenDescriptionIsNull_ThrowsArgumentNullException()
    {
        string? description = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>
        (() =>
             Contract.Invariant(description!, () => true)
        );

        Assert.Equal("description", exception.ParamName);
    }

    [Fact]
    public void Invariant_WhenConditionIsNull_ThrowsArgumentNullException()
    {
        Func<bool>? condition = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>
        (() =>
             Contract.Invariant("some description", condition!)
        );

        Assert.Equal("condition", exception.ParamName);
    }

    [Fact]
    public void Invariant_WhenConditionCallsInvariant_DoesNotRecurse()
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
    public async Task Invariant_WhenUsedInAsyncContext_WorksCorrectly()
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
    public async Task Invariant_WhenConditionIsFalseInAsyncContext_ThrowsException()
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
    public void Invariant_WhenCalled_EvaluatesConditionLazily()
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
    public void Invariant_WhenConditionThrows_PropagatesException()
    {
        InvalidOperationException expectedException = new("test error");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>
        (() =>
             Contract.Invariant("test", () => throw expectedException)
        );

        Assert.Same(expectedException, exception);
    }
}

public class InvariantNotNullTests
{
    [Fact]
    public void InvariantNotNull_WhenValueIsNotNull_DoesNotThrow()
    {
        const string value = "not null";

        Exception? exception = Record.Exception
        (() =>
             Contract.InvariantNotNull("Value", value)
        );

        Assert.Null(exception);
    }

    [Fact]
    public void InvariantNotNull_WhenValueIsNull_ThrowsInvariantViolationException()
    {
        string? value = null;

        InvariantViolationException exception = Assert.Throws<InvariantViolationException>
        (() =>
             Contract.InvariantNotNull("Value", value)
        );

        Assert.NotNull(exception);
    }

    [Fact]
    public void InvariantNotNull_WhenValueIsNull_ExceptionMessageHasCorrectFormat()
    {
        string? value = null;
        const string description = "Value must not be null";

        InvariantViolationException exception = Assert.Throws<InvariantViolationException>
        (() =>
             Contract.InvariantNotNull(description, value)
        );

        Assert.Equal($"Invariant violated: {description}", exception.Message);
    }

    [Fact]
    public void InvariantNotNull_WhenDescriptionIsNull_ThrowsArgumentNullException()
    {
        string? description = null;
        const string value = "not null";

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>
        (() =>
             Contract.InvariantNotNull(description!, value)
        );

        Assert.Equal("description", exception.ParamName);
    }

    [Fact]
    public void InvariantNotNull_WhenRecursionGuardActive_DoesNotRecurse()
    {
        int callCount = 0;
        string? nullValue = null;

        InvariantViolationException exception = Assert.Throws<InvariantViolationException>
        (() =>
             Contract.Invariant
             (
                 "outer", () =>
                 {
                     callCount++;
                     // This inner InvariantNotNull should be ignored due to recursion guard
                     Contract.InvariantNotNull("inner", nullValue);
                     return false;
                 }
             )
        );

        Assert.Equal(1, callCount);
        Assert.Contains("outer", exception.Message);
    }

    [Fact]
    public async Task InvariantNotNull_WhenUsedInAsyncContext_WorksCorrectly()
    {
        const string value = "not null";

        await Task.Run
        (() =>
            {
                Contract.InvariantNotNull("Value", value);
            }
        );

        Assert.True(true);
    }

    [Fact]
    public async Task InvariantNotNull_WhenValueIsNullInAsyncContext_ThrowsException()
    {
        string? value = null;

        await Assert.ThrowsAsync<InvariantViolationException>
        (async () =>
            {
                await Task.Run
                (() =>
                    {
                        Contract.InvariantNotNull("Value", value);
                    }
                );
            }
        );
    }

    [Fact]
    public void InvariantNotNull_WhenUsedWithReferenceTypes_Works()
    {
        TestAccount account = new() { Balance = 100m, Owner = "John" };

        Exception? exception = Record.Exception
        (() =>
             Contract.InvariantNotNull("Account", account)
        );

        Assert.Null(exception);
    }
}