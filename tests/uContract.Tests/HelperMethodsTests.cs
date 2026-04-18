using System.Collections.Immutable;
using uContract.Exceptions;

namespace uContract.Tests;

public class CheckTests
{
    [Fact]
    public void Check_WhenConditionIsTrue_DoesNotThrow()
    {
        const int value = 10;

        Exception? exception = Record.Exception(() => Contract.Check("value must be positive", () => value > 0));

        Assert.Null(exception);
    }

    [Fact]
    public void Check_WhenConditionIsFalse_ThrowsCheckViolationException()
    {
        const int value = -1;

        CheckViolationException exception = Assert.Throws<CheckViolationException>(() =>
            Contract.Check("value must be positive", () => value > 0)
        );

        Assert.NotNull(exception);
    }

    [Fact]
    public void Check_WhenConditionIsFalse_ExceptionMessageHasCorrectFormat()
    {
        const int value = -1;
        const string description = "value must be positive";

        CheckViolationException exception = Assert.Throws<CheckViolationException>(() =>
            Contract.Check(description, () => value > 0)
        );

        Assert.Equal($"Check failed: {description}", exception.Message);
    }

    [Fact]
    public void Check_WhenDescriptionIsNull_ThrowsArgumentNullException()
    {
        string? description = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            Contract.Check(description!, () => true)
        );

        Assert.Equal("description", exception.ParamName);
    }

    [Fact]
    public void Check_WhenConditionIsNull_ThrowsArgumentNullException()
    {
        Func<bool>? condition = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            Contract.Check("some description", condition!)
        );

        Assert.Equal("condition", exception.ParamName);
    }

    [Fact]
    public void Check_WhenConditionCallsCheck_DoesNotRecurse()
    {
        int callCount = 0;

        CheckViolationException exception = Assert.Throws<CheckViolationException>(() =>
            Contract.Check(
                "outer",
                () =>
                {
                    callCount++;
                    // This inner Check should be ignored due to recursion guard
                    Contract.Check("inner", () => false);
                    return false;
                }
            )
        );

        Assert.Equal(1, callCount);
        Assert.Contains("outer", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Check_WhenUsedInAsyncContext_WorksCorrectly()
    {
        const int value = 10;

        await Task.Run(() =>
        {
            Contract.Check("value must be positive", () => value > 0);
        });

        Assert.True(true);
    }

    [Fact]
    public async Task Check_WhenConditionIsFalseInAsyncContext_ThrowsException()
    {
        const int value = -1;

        await Assert.ThrowsAsync<CheckViolationException>(async () =>
        {
            await Task.Run(() =>
            {
                Contract.Check("value must be positive", () => value > 0);
            });
        });
    }

    [Fact]
    public void Check_WhenCalled_EvaluatesConditionLazily()
    {
        bool conditionEvaluated = false;

        Contract.Check(
            "test",
            () =>
            {
                conditionEvaluated = true;
                return true;
            }
        );

        Assert.True(conditionEvaluated);
    }

    [Fact]
    public void Check_WhenConditionThrows_PropagatesException()
    {
        InvalidOperationException expectedException = new("test error");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            Contract.Check("test", () => throw expectedException)
        );

        Assert.Same(expectedException, exception);
    }
}

public class IgnoreTests
{
    [Fact]
    public void Ignore_WhenConditionIsTrue_ReturnsTrue()
    {
        const int x = 10;

        bool result = Contract.Ignore("x is positive", () => x > 0);

        Assert.True(result);
    }

    [Fact]
    public void Ignore_WhenConditionIsFalse_ReturnsFalse()
    {
        const int x = -1;

        bool result = Contract.Ignore("x is positive", () => x > 0);

        Assert.False(result);
    }

    [Fact]
    public void Ignore_WhenReasonIsNull_ThrowsArgumentNullException()
    {
        string? reason = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            Contract.Ignore(reason!, () => true)
        );

        Assert.Equal("reason", exception.ParamName);
    }

    [Fact]
    public void Ignore_WhenConditionIsNull_ThrowsArgumentNullException()
    {
        Func<bool>? condition = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            Contract.Ignore("some reason", condition!)
        );

        Assert.Equal("condition", exception.ParamName);
    }

    [Fact]
    public void Ignore_WhenRecursionGuardActive_ReturnsFalse()
    {
        bool innerResult = true;

        bool outerResult = Contract.Ignore(
            "outer",
            () =>
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
    public async Task Ignore_WhenUsedInAsyncContext_WorksCorrectly()
    {
        const int value = 10;

        bool result = await Task.Run(() => Contract.Ignore("value is positive", () => value > 0));

        Assert.True(result);
    }

    [Fact]
    public async Task Ignore_WhenConditionIsFalseInAsyncContext_ReturnsFalse()
    {
        const int value = -1;

        bool result = await Task.Run(() => Contract.Ignore("value is positive", () => value > 0));

        Assert.False(result);
    }

    [Fact]
    public void Ignore_WhenCalled_EvaluatesConditionLazily()
    {
        bool conditionEvaluated = false;

        Contract.Ignore(
            "test",
            () =>
            {
                conditionEvaluated = true;
                return true;
            }
        );

        Assert.True(conditionEvaluated);
    }

    [Fact]
    public void Ignore_WhenConditionThrows_PropagatesException()
    {
        InvalidOperationException expectedException = new("test error");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            Contract.Ignore("test", () => throw expectedException)
        );

        Assert.Same(expectedException, exception);
    }

    [Fact]
    public void Ignore_WhenUsedForEarlyReturn_Works()
    {
        const string currentEmail = "test@example.com";
        const string newEmail = "test@example.com";
        bool methodExecuted = false;

        ChangeEmail(newEmail);

        Assert.False(methodExecuted);
        return;

        void ChangeEmail(string email)
        {
            if (Contract.Ignore("Email unchanged", () => string.Equals(currentEmail, email, StringComparison.Ordinal)))
            {
                return;
            }

            methodExecuted = true;
        }
    }

    [Fact]
    public void Ignore_WhenConditionIsFalse_AllowsMethodExecution()
    {
        const string currentEmail = "old@example.com";
        const string newEmail = "new@example.com";
        bool methodExecuted = false;

        ChangeEmail(newEmail);

        Assert.True(methodExecuted);
        return;

        void ChangeEmail(string email)
        {
            if (Contract.Ignore("Email unchanged", () => string.Equals(currentEmail, email, StringComparison.Ordinal)))
            {
                return;
            }

            methodExecuted = true;
        }
    }
}

public class ImplyTests
{
    [Fact]
    public void Imply_WhenAntecedentFalseAndConsequentFalse_ReturnsTrue()
    {
        // False → False = True
        bool result = Contract.Imply(() => false, () => false);

        Assert.True(result);
    }

    [Fact]
    public void Imply_WhenAntecedentFalseAndConsequentTrue_ReturnsTrue()
    {
        // False → True = True
        bool result = Contract.Imply(() => false, () => true);

        Assert.True(result);
    }

    [Fact]
    public void Imply_WhenAntecedentTrueAndConsequentFalse_ReturnsFalse()
    {
        // True → False = False
        bool result = Contract.Imply(() => true, () => false);

        Assert.False(result);
    }

    [Fact]
    public void Imply_WhenAntecedentTrueAndConsequentTrue_ReturnsTrue()
    {
        // True → True = True
        bool result = Contract.Imply(() => true, () => true);

        Assert.True(result);
    }

    [Fact]
    public void Imply_WhenAntecedentIsNull_ThrowsArgumentNullException()
    {
        Func<bool>? antecedent = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            Contract.Imply(antecedent!, () => true)
        );

        Assert.Equal("antecedent", exception.ParamName);
    }

    [Fact]
    public void Imply_WhenConsequentIsNull_ThrowsArgumentNullException()
    {
        Func<bool>? consequent = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            Contract.Imply(() => true, consequent!)
        );

        Assert.Equal("consequent", exception.ParamName);
    }

    [Fact]
    public void Imply_WhenCalled_EvaluatesBothLambdas()
    {
        bool antecedentEvaluated = false;
        bool consequentEvaluated = false;

        Contract.Imply(
            () =>
            {
                antecedentEvaluated = true;
                return true;
            },
            () =>
            {
                consequentEvaluated = true;
                return true;
            }
        );

        Assert.True(antecedentEvaluated);
        Assert.True(consequentEvaluated);
    }

    [Fact]
    public void Imply_WhenAntecedentThrows_PropagatesException()
    {
        InvalidOperationException expectedException = new("antecedent error");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            Contract.Imply(() => throw expectedException, () => true)
        );

        Assert.Same(expectedException, exception);
    }

    [Fact]
    public void Imply_WhenConsequentThrows_PropagatesException()
    {
        InvalidOperationException expectedException = new("consequent error");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            Contract.Imply(() => true, () => throw expectedException)
        );

        Assert.Same(expectedException, exception);
    }

    [Fact]
    public void Imply_WhenUsedInContractCondition_Works()
    {
        const bool isVip = true;
        const int discount = 10;

        // If customer is VIP, then discount must be > 0
        Exception? exception = Record.Exception(() =>
            Contract.Require("VIP discount rule", () => Contract.Imply(() => isVip, () => discount > 0))
        );

        Assert.Null(exception);
    }
}

public class IfAndOnlyIfTests
{
    [Fact]
    public void IfAndOnlyIf_WhenBothTrue_ReturnsTrue()
    {
        // True ⟺ True = True
        bool result = Contract.IfAndOnlyIf(() => true, () => true);

        Assert.True(result);
    }

    [Fact]
    public void IfAndOnlyIf_WhenFirstTrueSecondFalse_ReturnsFalse()
    {
        // True ⟺ False = False
        bool result = Contract.IfAndOnlyIf(() => true, () => false);

        Assert.False(result);
    }

    [Fact]
    public void IfAndOnlyIf_WhenFirstFalseSecondTrue_ReturnsFalse()
    {
        // False ⟺ True = False
        bool result = Contract.IfAndOnlyIf(() => false, () => true);

        Assert.False(result);
    }

    [Fact]
    public void IfAndOnlyIf_WhenBothFalse_ReturnsTrue()
    {
        // False ⟺ False = True
        bool result = Contract.IfAndOnlyIf(() => false, () => false);

        Assert.True(result);
    }

    [Fact]
    public void IfAndOnlyIf_WhenFirstIsNull_ThrowsArgumentNullException()
    {
        Func<bool>? a = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            Contract.IfAndOnlyIf(a!, () => true)
        );

        Assert.Equal("a", exception.ParamName);
    }

    [Fact]
    public void IfAndOnlyIf_WhenSecondIsNull_ThrowsArgumentNullException()
    {
        Func<bool>? b = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            Contract.IfAndOnlyIf(() => true, b!)
        );

        Assert.Equal("b", exception.ParamName);
    }

    [Fact]
    public void IfAndOnlyIf_WhenCalled_EvaluatesBothLambdas()
    {
        bool aEvaluated = false;
        bool bEvaluated = false;

        Contract.IfAndOnlyIf(
            () =>
            {
                aEvaluated = true;
                return true;
            },
            () =>
            {
                bEvaluated = true;
                return true;
            }
        );

        Assert.True(aEvaluated);
        Assert.True(bEvaluated);
    }

    [Fact]
    public void IfAndOnlyIf_WhenFirstThrows_PropagatesException()
    {
        InvalidOperationException expectedException = new("first error");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            Contract.IfAndOnlyIf(() => throw expectedException, () => true)
        );

        Assert.Same(expectedException, exception);
    }

    [Fact]
    public void IfAndOnlyIf_WhenSecondThrows_PropagatesException()
    {
        InvalidOperationException expectedException = new("second error");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            Contract.IfAndOnlyIf(() => true, () => throw expectedException)
        );

        Assert.Same(expectedException, exception);
    }

    [Fact]
    public void IfAndOnlyIf_WhenUsedInContractCondition_Works()
    {
        const bool isPaid = true;
        const bool paymentCompleted = true;

        // Order is paid if and only if payment is completed
        Exception? exception = Record.Exception(() =>
            Contract.Invariant("Payment consistency", () => Contract.IfAndOnlyIf(() => isPaid, () => paymentCompleted))
        );

        Assert.Null(exception);
    }
}

public class CheckUnsupportedOperationTests
{
    [Fact]
    public void CheckUnsupportedOperation_WhenNotSupportedExceptionThrown_ReturnsTrue()
    {
        bool result = Contract.CheckUnsupportedOperation(() =>
            throw new NotSupportedException("Operation not supported")
        );

        Assert.True(result);
    }

    [Fact]
    public void CheckUnsupportedOperation_WhenNoExceptionThrown_ReturnsFalse()
    {
        bool result = Contract.CheckUnsupportedOperation(() => {
            // Do nothing - no exception
        });

        Assert.False(result);
    }

    [Fact]
    public void CheckUnsupportedOperation_WhenOtherExceptionThrown_ReturnsFalse()
    {
        bool result = Contract.CheckUnsupportedOperation(() =>
            throw new InvalidOperationException("Different exception")
        );

        Assert.False(result);
    }

    [Fact]
    public void CheckUnsupportedOperation_WhenArgumentExceptionThrown_ReturnsFalse()
    {
        bool result = Contract.CheckUnsupportedOperation(() => throw new ArgumentException("Argument exception"));

        Assert.False(result);
    }

    [Fact]
    public void CheckUnsupportedOperation_WhenActionIsNull_ThrowsArgumentNullException()
    {
        Action? action = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            Contract.CheckUnsupportedOperation(action!)
        );

        Assert.Equal("action", exception.ParamName);
    }

    [Fact]
    public void CheckUnsupportedOperation_WhenUsedWithImmutableList_Works()
    {
        ImmutableList<string> immutableList = ImmutableList.Create("Alice", "Bob");

        bool result = Contract.CheckUnsupportedOperation(() =>
        {
            IList<string> list = immutableList;
            list.Add("Charlie");
        });

        Assert.True(result);
    }

    [Fact]
    public void CheckUnsupportedOperation_WhenUsedWithImmutableArray_Works()
    {
        ImmutableArray<int> immutableArray = ImmutableArray.Create(1, 2, 3);

        bool result = Contract.CheckUnsupportedOperation(() =>
        {
            IList<int> list = immutableArray;
            list.Add(4);
        });

        Assert.True(result);
    }

    [Fact]
    public void CheckUnsupportedOperation_WhenUsedWithMutableList_ReturnsFalse()
    {
        List<string> mutableList = new() { "Alice", "Bob" };

        bool result = Contract.CheckUnsupportedOperation(() => mutableList.Add("Charlie"));

        Assert.False(result);
        Assert.Equal(3, mutableList.Count);
    }

    [Fact]
    public void CheckUnsupportedOperation_WhenUsedInContract_Works()
    {
        ImmutableList<string> immutableList = ImmutableList.Create("Alice");

        Exception? exception = Record.Exception(() =>
            Contract.Check(
                "List is immutable",
                () =>
                    Contract.CheckUnsupportedOperation(() =>
                    {
                        IList<string> list = immutableList;
                        list.Add("Bob");
                    })
            )
        );

        Assert.Null(exception);
    }

    [Fact]
    public async Task CheckUnsupportedOperation_WhenUsedInAsyncContext_WorksCorrectly()
    {
        ImmutableList<string> immutableList = ImmutableList.Create("Alice");

        bool result = await Task.Run(() =>
            Contract.CheckUnsupportedOperation(() =>
            {
                IList<string> list = immutableList;
                list.Add("Bob");
            })
        );

        Assert.True(result);
    }
}
