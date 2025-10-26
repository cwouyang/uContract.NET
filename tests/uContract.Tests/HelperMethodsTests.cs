using System.Collections.Immutable;

using uContract.Exceptions;

namespace uContract.Tests;

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

public class ImplyTests
{
    [Fact]
    public void Imply_ReturnsTrue_WhenAntecedentFalseAndConsequentFalse()
    {
        // False → False = True
        bool result = Contract.Imply(() => false, () => false);

        Assert.True(result);
    }

    [Fact]
    public void Imply_ReturnsTrue_WhenAntecedentFalseAndConsequentTrue()
    {
        // False → True = True
        bool result = Contract.Imply(() => false, () => true);

        Assert.True(result);
    }

    [Fact]
    public void Imply_ReturnsFalse_WhenAntecedentTrueAndConsequentFalse()
    {
        // True → False = False
        bool result = Contract.Imply(() => true, () => false);

        Assert.False(result);
    }

    [Fact]
    public void Imply_ReturnsTrue_WhenAntecedentTrueAndConsequentTrue()
    {
        // True → True = True
        bool result = Contract.Imply(() => true, () => true);

        Assert.True(result);
    }

    [Fact]
    public void Imply_ThrowsArgumentNullException_WhenAntecedentIsNull()
    {
        Func<bool>? antecedent = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>
        (() =>
             Contract.Imply(antecedent!, () => true)
        );

        Assert.Equal("antecedent", exception.ParamName);
    }

    [Fact]
    public void Imply_ThrowsArgumentNullException_WhenConsequentIsNull()
    {
        Func<bool>? consequent = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>
        (() =>
             Contract.Imply(() => true, consequent!)
        );

        Assert.Equal("consequent", exception.ParamName);
    }

    [Fact]
    public void Imply_EvaluatesBothLambdas()
    {
        bool antecedentEvaluated = false;
        bool consequentEvaluated = false;

        Contract.Imply
        (
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
    public void Imply_PropagatesException_WhenAntecedentThrows()
    {
        InvalidOperationException expectedException = new("antecedent error");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>
        (() =>
             Contract.Imply(() => throw expectedException, () => true)
        );

        Assert.Same(expectedException, exception);
    }

    [Fact]
    public void Imply_PropagatesException_WhenConsequentThrows()
    {
        InvalidOperationException expectedException = new("consequent error");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>
        (() =>
             Contract.Imply(() => true, () => throw expectedException)
        );

        Assert.Same(expectedException, exception);
    }

    [Fact]
    public void Imply_WorksInContractCondition()
    {
        const bool isVip = true;
        const int discount = 10;

        // If customer is VIP, then discount must be > 0
        Exception? exception = Record.Exception
        (() =>
             Contract.Require
             (
                 "VIP discount rule",
                 () => Contract.Imply(() => isVip, () => discount > 0)
             )
        );

        Assert.Null(exception);
    }
}

public class IfAndOnlyIfTests
{
    [Fact]
    public void IfAndOnlyIf_ReturnsTrue_WhenBothTrue()
    {
        // True ⟺ True = True
        bool result = Contract.IfAndOnlyIf(() => true, () => true);

        Assert.True(result);
    }

    [Fact]
    public void IfAndOnlyIf_ReturnsFalse_WhenFirstTrueSecondFalse()
    {
        // True ⟺ False = False
        bool result = Contract.IfAndOnlyIf(() => true, () => false);

        Assert.False(result);
    }

    [Fact]
    public void IfAndOnlyIf_ReturnsFalse_WhenFirstFalseSecondTrue()
    {
        // False ⟺ True = False
        bool result = Contract.IfAndOnlyIf(() => false, () => true);

        Assert.False(result);
    }

    [Fact]
    public void IfAndOnlyIf_ReturnsTrue_WhenBothFalse()
    {
        // False ⟺ False = True
        bool result = Contract.IfAndOnlyIf(() => false, () => false);

        Assert.True(result);
    }

    [Fact]
    public void IfAndOnlyIf_ThrowsArgumentNullException_WhenFirstIsNull()
    {
        Func<bool>? a = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>
        (() =>
             Contract.IfAndOnlyIf(a!, () => true)
        );

        Assert.Equal("a", exception.ParamName);
    }

    [Fact]
    public void IfAndOnlyIf_ThrowsArgumentNullException_WhenSecondIsNull()
    {
        Func<bool>? b = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>
        (() =>
             Contract.IfAndOnlyIf(() => true, b!)
        );

        Assert.Equal("b", exception.ParamName);
    }

    [Fact]
    public void IfAndOnlyIf_EvaluatesBothLambdas()
    {
        bool aEvaluated = false;
        bool bEvaluated = false;

        Contract.IfAndOnlyIf
        (
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
    public void IfAndOnlyIf_PropagatesException_WhenFirstThrows()
    {
        InvalidOperationException expectedException = new("first error");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>
        (() =>
             Contract.IfAndOnlyIf(() => throw expectedException, () => true)
        );

        Assert.Same(expectedException, exception);
    }

    [Fact]
    public void IfAndOnlyIf_PropagatesException_WhenSecondThrows()
    {
        InvalidOperationException expectedException = new("second error");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>
        (() =>
             Contract.IfAndOnlyIf(() => true, () => throw expectedException)
        );

        Assert.Same(expectedException, exception);
    }

    [Fact]
    public void IfAndOnlyIf_WorksInContractCondition()
    {
        const bool isPaid = true;
        const bool paymentCompleted = true;

        // Order is paid if and only if payment is completed
        Exception? exception = Record.Exception
        (() =>
             Contract.Invariant
             (
                 "Payment consistency",
                 () => Contract.IfAndOnlyIf(() => isPaid, () => paymentCompleted)
             )
        );

        Assert.Null(exception);
    }
}

public class CheckUnsupportedOperationTests
{
    [Fact]
    public void CheckUnsupportedOperation_ReturnsTrue_WhenNotSupportedExceptionThrown()
    {
        bool result = Contract.CheckUnsupportedOperation
        (() =>
             throw new NotSupportedException("Operation not supported")
        );

        Assert.True(result);
    }

    [Fact]
    public void CheckUnsupportedOperation_ReturnsFalse_WhenNoExceptionThrown()
    {
        bool result = Contract.CheckUnsupportedOperation
        (() =>
            {
                // Do nothing - no exception
            }
        );

        Assert.False(result);
    }

    [Fact]
    public void CheckUnsupportedOperation_ReturnsFalse_WhenOtherExceptionThrown()
    {
        bool result = Contract.CheckUnsupportedOperation
        (() =>
             throw new InvalidOperationException("Different exception")
        );

        Assert.False(result);
    }

    [Fact]
    public void CheckUnsupportedOperation_ReturnsFalse_WhenArgumentExceptionThrown()
    {
        bool result = Contract.CheckUnsupportedOperation
        (() =>
             throw new ArgumentException("Argument exception")
        );

        Assert.False(result);
    }

    [Fact]
    public void CheckUnsupportedOperation_ThrowsArgumentNullException_WhenActionIsNull()
    {
        Action? action = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>
        (() =>
             Contract.CheckUnsupportedOperation(action!)
        );

        Assert.Equal("action", exception.ParamName);
    }

    [Fact]
    public void CheckUnsupportedOperation_WorksWithImmutableList()
    {
        ImmutableList<string> immutableList = ImmutableList.Create("Alice", "Bob");

        bool result = Contract.CheckUnsupportedOperation
        (() =>
            {
                IList<string> list = immutableList;
                list.Add("Charlie");
            }
        );

        Assert.True(result);
    }

    [Fact]
    public void CheckUnsupportedOperation_WorksWithImmutableArray()
    {
        ImmutableArray<int> immutableArray = ImmutableArray.Create(1, 2, 3);

        bool result = Contract.CheckUnsupportedOperation
        (() =>
            {
                IList<int> list = immutableArray;
                list.Add(4);
            }
        );

        Assert.True(result);
    }

    [Fact]
    public void CheckUnsupportedOperation_ReturnsFalse_WithMutableList()
    {
        List<string> mutableList = new() { "Alice", "Bob" };

        bool result = Contract.CheckUnsupportedOperation
        (() =>
             mutableList.Add("Charlie")
        );

        Assert.False(result);
        Assert.Equal(3, mutableList.Count);
    }

    [Fact]
    public void CheckUnsupportedOperation_CanBeUsedInContract()
    {
        ImmutableList<string> immutableList = ImmutableList.Create("Alice");

        Exception? exception = Record.Exception
        (() =>
             Contract.Check
             (
                 "List is immutable",
                 () => Contract.CheckUnsupportedOperation
                 (() =>
                     {
                         IList<string> list = immutableList;
                         list.Add("Bob");
                     }
                 )
             )
        );

        Assert.Null(exception);
    }

    [Fact]
    public async Task CheckUnsupportedOperation_WorksCorrectly_InAsyncContext()
    {
        ImmutableList<string> immutableList = ImmutableList.Create("Alice");

        bool result = await Task.Run
        (() =>
             Contract.CheckUnsupportedOperation
             (() =>
                 {
                     IList<string> list = immutableList;
                     list.Add("Bob");
                 }
             )
        );

        Assert.True(result);
    }
}