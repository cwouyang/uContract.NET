using System.Collections.Immutable;
using uContract.Exceptions;

namespace uContract.Tests;

public class EnsureTests
{
    [Fact]
    public void Ensure_WhenConditionIsTrue_DoesNotThrow()
    {
        const int x = 10;

        Exception? exception = Record.Exception(() => Contract.Ensure("x must be positive", () => x > 0));

        Assert.Null(exception);
    }

    [Fact]
    public void Ensure_WhenConditionIsFalse_ThrowsPostconditionViolationException()
    {
        const int x = -1;

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.Ensure("x must be positive", () => x > 0)
        );

        Assert.NotNull(exception);
    }

    [Fact]
    public void Ensure_WhenConditionIsFalse_ExceptionMessageHasCorrectFormat()
    {
        const int x = -1;
        const string description = "x must be positive";

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.Ensure(description, () => x > 0)
        );

        Assert.Equal($"Postcondition violated: {description}", exception.Message);
    }

    [Fact]
    public void Ensure_WhenDescriptionIsNull_ThrowsArgumentNullException()
    {
        string? description = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            Contract.Ensure(description!, () => true)
        );

        Assert.Equal("description", exception.ParamName);
    }

    [Fact]
    public void Ensure_WhenConditionIsNull_ThrowsArgumentNullException()
    {
        Func<bool>? condition = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            Contract.Ensure("some description", condition!)
        );

        Assert.Equal("condition", exception.ParamName);
    }

    [Fact]
    public void Ensure_WhenConditionCallsEnsure_DoesNotRecurse()
    {
        int callCount = 0;

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.Ensure(
                "outer",
                () =>
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
    public async Task Ensure_WhenUsedInAsyncContext_WorksCorrectly()
    {
        const int x = 10;

        await Task.Run(() =>
        {
            Contract.Ensure("x must be positive", () => x > 0);
        });

        Assert.True(true);
    }

    [Fact]
    public async Task Ensure_WhenConditionIsFalseInAsyncContext_ThrowsException()
    {
        const int x = -1;

        await Assert.ThrowsAsync<PostconditionViolationException>(async () =>
        {
            await Task.Run(() =>
            {
                Contract.Ensure("x must be positive", () => x > 0);
            });
        });
    }

    [Fact]
    public void Ensure_WhenCalled_EvaluatesConditionLazily()
    {
        bool conditionEvaluated = false;

        Contract.Ensure(
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
    public void Ensure_WhenConditionThrows_PropagatesException()
    {
        InvalidOperationException expectedException = new("test error");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            Contract.Ensure("test", () => throw expectedException)
        );

        Assert.Same(expectedException, exception);
    }
}

public class OldTests
{
    [Fact]
    public void Old_WhenPostconditionsEnabled_CapturesValue()
    {
        const decimal balance = 100m;

        decimal oldBalance = Contract.Old(() => balance);

        Assert.Equal(100m, oldBalance);
    }

    [Fact]
    public void Old_WhenCalled_CreatesDeepCopyNotReference()
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
    public void Old_WhenUsedWithReferenceTypes_Works()
    {
        TestAccount account = new() { Balance = 100m, Owner = "John" };

        TestAccount oldAccount = Contract.Old(() => account);

        Assert.NotNull(oldAccount);
        Assert.Equal(100m, oldAccount.Balance);
        Assert.Equal("John", oldAccount.Owner);
    }

    [Fact]
    public void Old_WhenUsedWithValueTypes_Works()
    {
        TestPoint point = new() { X = 10, Y = 20 };

        TestPoint oldPoint = Contract.Old(() => point);

        Assert.Equal(10, oldPoint.X);
        Assert.Equal(20, oldPoint.Y);
    }

    [Fact]
    public void Old_WhenUsedWithPrimitiveTypes_Works()
    {
        const int value = 42;

        int oldValue = Contract.Old(() => value);

        Assert.Equal(42, oldValue);
    }

    [Fact]
    public void Old_WhenUsedWithStrings_Works()
    {
        const string text = "Hello";

        string oldText = Contract.Old(() => text);

        Assert.Equal("Hello", oldText);
    }

    [Fact]
    public void Old_WhenSupplierReturnsNull_ReturnsDefault()
    {
        TestAccount? account = null;

        TestAccount? oldAccount = Contract.Old(() => account);

        Assert.Null(oldAccount);
    }

    [Fact]
    public void Old_WhenSupplierIsNull_ThrowsArgumentNullException()
    {
        Func<int>? supplier = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Contract.Old(supplier!));

        Assert.Equal("supplier", exception.ParamName);
    }

    [Fact]
    public void Old_WhenRecursionGuardActive_ReturnsDefault()
    {
        const int outerValue = 100;
        int innerValue = 0;

        int outerOld = Contract.Old(() =>
        {
            // This inner Old should return default due to recursion guard
            innerValue = Contract.Old(() => 42);
            return outerValue;
        });

        Assert.Equal(100, outerOld);
        Assert.Equal(0, innerValue);
    }

    [Fact]
    public async Task Old_WhenUsedInAsyncContext_WorksCorrectly()
    {
        const decimal balance = 100m;

        decimal oldBalance = await Task.Run(() => Contract.Old(() => balance));

        Assert.Equal(100m, oldBalance);
    }

    [Fact]
    public void Old_WhenCalled_EvaluatesSupplierLazily()
    {
        bool supplierEvaluated = false;

        Contract.Old(() =>
        {
            supplierEvaluated = true;
            return 42;
        });

        Assert.True(supplierEvaluated);
    }

    [Fact]
    public void Old_WhenTypeNotSerializable_ThrowsInvalidOperationException()
    {
        NonSerializableType obj = new();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Contract.Old(() => obj));

        Assert.Contains("cannot be serialized", exception.Message);
        Assert.IsType<NotSupportedException>(exception.InnerException);
    }

    [Fact]
    public void Old_WhenUsedInPostcondition_Works()
    {
        decimal balance = 100m;
        // ReSharper disable once AccessToModifiedClosure
        decimal oldBalance = Contract.Old(() => balance);

        balance -= 50m;

        Contract.Ensure("Balance decreased", () => balance < oldBalance);

        Assert.Equal(50m, balance);
    }

    [Fact]
    public void Old_WhenSupplierThrows_PropagatesException()
    {
        InvalidOperationException expectedException = new("test error");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            Contract.Old<int>(() => throw expectedException)
        );

        Assert.Same(expectedException, exception);
    }
}

public class EnsureNotNullTests
{
    [Fact]
    public void EnsureNotNull_WhenValueIsNotNull_DoesNotThrow()
    {
        const string value = "not null";

        Exception? exception = Record.Exception(() => Contract.EnsureNotNull("Value", value));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureNotNull_ThrowsPostconditionViolationException_WhenValueIsNull()
    {
        string? value = null;

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureNotNull("Value", value)
        );

        Assert.NotNull(exception);
    }

    [Fact]
    public void EnsureNotNull_WhenValueIsNull_ExceptionMessageHasCorrectFormat()
    {
        string? value = null;
        const string description = "Value must not be null";

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureNotNull(description, value)
        );

        Assert.Equal($"Postcondition violated: {description}", exception.Message);
    }

    [Fact]
    public void EnsureNotNull_WhenDescriptionIsNull_ThrowsArgumentNullException()
    {
        string? description = null;
        const string value = "not null";

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            Contract.EnsureNotNull(description!, value)
        );

        Assert.Equal("description", exception.ParamName);
    }

    [Fact]
    public void EnsureNotNull_WhenRecursionGuardActive_DoesNotRecurse()
    {
        int callCount = 0;
        string? nullValue = null;

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.Ensure(
                "outer",
                () =>
                {
                    callCount++;
                    // This inner EnsureNotNull should be ignored due to recursion guard
                    Contract.EnsureNotNull("inner", nullValue);
                    return false;
                }
            )
        );

        Assert.Equal(1, callCount);
        Assert.Contains("outer", exception.Message);
    }

    [Fact]
    public async Task EnsureNotNull_WhenUsedInAsyncContext_WorksCorrectly()
    {
        const string value = "not null";

        await Task.Run(() =>
        {
            Contract.EnsureNotNull("Value", value);
        });

        Assert.True(true);
    }

    [Fact]
    public async Task EnsureNotNull_WhenValueIsNullInAsyncContext_ThrowsException()
    {
        string? value = null;

        await Assert.ThrowsAsync<PostconditionViolationException>(async () =>
        {
            await Task.Run(() =>
            {
                Contract.EnsureNotNull("Value", value);
            });
        });
    }

    [Fact]
    public void EnsureNotNull_WhenUsedWithReferenceTypes_Works()
    {
        TestAccount account = new() { Balance = 100m, Owner = "John" };

        Exception? exception = Record.Exception(() => Contract.EnsureNotNull("Account", account));

        Assert.Null(exception);
    }
}

public class EnsureResultTests
{
    [Fact]
    public void EnsureResult_WhenAssertionIsTrue_ReturnsResult()
    {
        const int value = 42;

        int result = Contract.EnsureResult("Value is positive", value, v => v > 0);

        Assert.Equal(42, result);
    }

    [Fact]
    public void EnsureResult_WhenAssertionIsFalse_ThrowsPostconditionViolationException()
    {
        const int value = -1;

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureResult("Value is positive", value, v => v > 0)
        );

        Assert.NotNull(exception);
    }

    [Fact]
    public void EnsureResult_WhenAssertionIsFalse_ExceptionMessageHasCorrectFormat()
    {
        const int value = -1;
        const string description = "Value must be positive";

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureResult(description, value, v => v > 0)
        );

        Assert.Equal($"Postcondition violated: {description}", exception.Message);
    }

    [Fact]
    public void EnsureResult_WhenDescriptionIsNull_ThrowsArgumentNullException()
    {
        string? description = null;
        const int value = 42;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            Contract.EnsureResult(description!, value, v => v > 0)
        );

        Assert.Equal("description", exception.ParamName);
    }

    [Fact]
    public void EnsureResult_WhenAssertionIsNull_ThrowsArgumentNullException()
    {
        const int value = 42;
        Func<int, bool>? assertion = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            Contract.EnsureResult("test", value, assertion!)
        );

        Assert.Equal("assertion", exception.ParamName);
    }

    [Fact]
    public void EnsureResult_WhenRecursionGuardActive_ReturnsResult()
    {
        const int outerValue = 100;
        int innerResult = 0;

        int result = Contract.EnsureResult(
            "outer",
            outerValue,
            v =>
            {
                // This inner EnsureResult should return result due to recursion guard
                innerResult = Contract.EnsureResult("inner", 42, x => x > 0);
                return v > 0;
            }
        );

        Assert.Equal(100, result);
        Assert.Equal(42, innerResult);
    }

    [Fact]
    public void EnsureResult_WhenCalled_SupportsChaining()
    {
        TestAccount account = new() { Balance = 100m, Owner = "John" };

        TestAccount result = Contract.EnsureResult(
            "Account valid",
            account,
            a => a.Balance > 0 && !string.IsNullOrEmpty(a.Owner)
        );

        Assert.Same(account, result);
        Assert.Equal(100m, result.Balance);
        Assert.Equal("John", result.Owner);
    }
}

public class EnsureImmutableCollectionTests
{
    [Fact]
    public void EnsureImmutableCollection_WhenImmutableList_ReturnsCollection()
    {
        ImmutableList<string> immutableList = ImmutableList.Create("Alice", "Bob");

        ImmutableList<string> result = Contract.EnsureImmutableCollection(immutableList);

        Assert.Same(immutableList, result);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenImmutableArray_ReturnsCollection()
    {
        ImmutableArray<int> immutableArray = [1, 2, 3];

        ImmutableArray<int> result = Contract.EnsureImmutableCollection(immutableArray);

        Assert.Equal(3, result.Length);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenImmutableDictionary_ReturnsCollection()
    {
        ImmutableDictionary<string, int> immutableDict = ImmutableDictionary
            .Create<string, int>(StringComparer.Ordinal)
            .Add("key", 42);

        ImmutableDictionary<string, int> result = Contract.EnsureImmutableCollection(immutableDict);

        Assert.Same(immutableDict, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenImmutableHashSet_ReturnsCollection()
    {
        ImmutableHashSet<string> immutableSet = ImmutableHashSet.Create(StringComparer.Ordinal, "A", "B");

        ImmutableHashSet<string> result = Contract.EnsureImmutableCollection(immutableSet);

        Assert.Same(immutableSet, result);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenMutableList_ThrowsPostconditionViolationException()
    {
        List<string> mutableList = ["Alice", "Bob"];

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureImmutableCollection(mutableList)
        );

        Assert.Contains("immutable collection", exception.Message);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenArray_ThrowsPostconditionViolationException()
    {
        int[] mutableArray = [1, 2, 3];

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureImmutableCollection(mutableArray)
        );

        Assert.Contains("immutable collection", exception.Message);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenDictionary_ThrowsPostconditionViolationException()
    {
        Dictionary<string, int> mutableDict = new(StringComparer.Ordinal) { { "key", 42 } };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureImmutableCollection(mutableDict)
        );

        Assert.Contains("immutable collection", exception.Message);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenCollectionIsNull_ThrowsArgumentNullException()
    {
        ImmutableList<string>? collection = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            Contract.EnsureImmutableCollection(collection!)
        );

        Assert.Equal("collection", exception.ParamName);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenRecursionGuardActive_ReturnsCollection()
    {
        List<string> mutableList = ["Alice", "Bob"];
        List<string> innerResult = null!;

        List<string> result = Contract.EnsureResult(
            "outer",
            mutableList,
            list =>
            {
                // This inner EnsureImmutableCollection should return collection due to recursion guard
                innerResult = Contract.EnsureImmutableCollection(list);
                return list.Count > 0;
            }
        );

        Assert.Same(mutableList, result);
        Assert.Same(mutableList, innerResult);
    }

    [Fact]
    public void EnsureImmutableCollection_WhenCalled_SupportsMethodChaining()
    {
        ImmutableList<string> result = GetNames();

        Assert.Equal(3, result.Count);
        Assert.Contains("Alice", result, StringComparer.Ordinal);
        return;

        ImmutableList<string> GetNames()
        {
            ImmutableList<string> names = ImmutableList.Create("Alice", "Bob", "Charlie");
            return Contract.EnsureImmutableCollection(names);
        }
    }

    [Fact]
    public async Task EnsureImmutableCollection_WhenUsedInAsyncContext_WorksCorrectly()
    {
        ImmutableList<string> immutableList = ImmutableList.Create("Alice", "Bob");

        ImmutableList<string> result = await Task.Run(() => Contract.EnsureImmutableCollection(immutableList));

        Assert.Same(immutableList, result);
    }

    [Fact]
    public async Task EnsureImmutableCollection_WhenMutableInAsyncContext_ThrowsException()
    {
        List<string> mutableList = ["Alice", "Bob"];

        await Assert.ThrowsAsync<PostconditionViolationException>(async () =>
        {
            await Task.Run(() => Contract.EnsureImmutableCollection(mutableList));
        });
    }
}

public class EnsureAssignableTests
{
    [Fact]
    public void EnsureAssignable_WhenNoFieldsModified_DoesNotThrow()
    {
        TestPerson person1 = new()
        {
            Name = "Alice",
            Age = 30,
            Email = "alice@example.com",
        };
        TestPerson person2 = new()
        {
            Name = "Alice",
            Age = 30,
            Email = "alice@example.com",
        };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(person1, person2, "Name"));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenOnlyAssignableFieldModified_DoesNotThrow()
    {
        TestPerson oldPerson = new()
        {
            Name = "Alice",
            Age = 30,
            Email = "alice@example.com",
        };
        TestPerson newPerson = new()
        {
            Name = "Alice",
            Age = 30,
            Email = "newemail@example.com",
        };

        Exception? exception = Record.Exception(() =>
            Contract.EnsureAssignable(newPerson, oldPerson, "Email", "_email")
        );

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_ThrowsException_WhenNonAssignableFieldModified()
    {
        TestPerson oldPerson = new()
        {
            Name = "Alice",
            Age = 30,
            Email = "alice@example.com",
        };
        TestPerson newPerson = new()
        {
            Name = "Bob",
            Age = 30,
            Email = "alice@example.com",
        };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(newPerson, oldPerson, "Email")
        );

        Assert.Contains("Name", exception.Message);
        Assert.Contains("not marked as assignable", exception.Message);
    }

    [Fact]
    public void EnsureAssignable_WorksWithRecordClass()
    {
        TestPersonRecord oldRecord = new("Alice", 30, "alice@example.com");
        TestPersonRecord newRecord = new("Alice", 30, "newemail@example.com");

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(newRecord, oldRecord, "Email"));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_SupportsMultipleAssignableFields()
    {
        TestPerson oldPerson = new()
        {
            Name = "Alice",
            Age = 30,
            Email = "alice@example.com",
        };
        TestPerson newPerson = new()
        {
            Name = "Bob",
            Age = 31,
            Email = "bob@example.com",
        };

        Exception? exception = Record.Exception(() =>
            Contract.EnsureAssignable(newPerson, oldPerson, "Name", "Age", "Email", "_email")
        );

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_ThrowsException_WhenMultipleFieldsModified()
    {
        TestPerson oldPerson = new()
        {
            Name = "Alice",
            Age = 30,
            Email = "alice@example.com",
        };
        TestPerson newPerson = new()
        {
            Name = "Bob",
            Age = 31,
            Email = "bob@example.com",
        };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(newPerson, oldPerson, "Email")
        );

        Assert.Contains("Name", exception.Message);
        Assert.Contains("Age", exception.Message);
    }

    [Fact]
    public void EnsureAssignable_WorksWithStruct()
    {
        TestPoint oldPoint = new(0, 0);
        TestPoint newPoint = new(5, 0);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(newPoint, oldPoint, "X"));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WorksWithRecordStruct()
    {
        TestPointRecordStruct oldPoint = new(0, 0);
        TestPointRecordStruct newPoint = new(5, 0);

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(newPoint, oldPoint, "X"));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WorksWithStructWithPrivateFields()
    {
        TestPoint oldPoint = new(0, 0);
        TestPoint newPoint = new(5, 0);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(newPoint, oldPoint, "Y")
        );

        Assert.Contains("X", exception.Message);
    }

    [Fact]
    public void EnsureAssignable_ThrowsException_WhenStructFieldNotAssignable()
    {
        TestPoint oldPoint = new(0, 0);
        TestPoint newPoint = new(0, 5);

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(newPoint, oldPoint, "X")
        );

        Assert.Contains("Y", exception.Message);
    }

    [Fact]
    public void EnsureAssignable_SupportsLiteralFieldName()
    {
        TestPerson oldPerson = new()
        {
            Name = "Alice",
            Age = 30,
            Email = "alice@example.com",
        };
        TestPerson newPerson = new()
        {
            Name = "Alice",
            Age = 30,
            Email = "newemail@example.com",
        };

        Exception? exception = Record.Exception(() =>
            Contract.EnsureAssignable(newPerson, oldPerson, "Email", "_email")
        );

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_SupportsRegexPattern_EndsWith()
    {
        TestPerson oldPerson = new()
        {
            Name = "Alice",
            Age = 30,
            Email = "alice@example.com",
        };
        TestPerson newPerson = new()
        {
            Name = "Alice",
            Age = 30,
            Email = "newemail@example.com",
        };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(newPerson, oldPerson, ".*mail"));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_SupportsRegexPattern_StartsWith()
    {
        TestPerson oldPerson = new()
        {
            Name = "Alice",
            Age = 30,
            Email = "alice@example.com",
        };
        TestPerson newPerson = new()
        {
            Name = "Alice",
            Age = 30,
            Email = "newemail@example.com",
        };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(newPerson, oldPerson, "^_.*", "Email"));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_SupportsRegexPattern_Or()
    {
        TestPerson oldPerson = new()
        {
            Name = "Alice",
            Age = 30,
            Email = "alice@example.com",
        };
        TestPerson newPerson = new()
        {
            Name = "Bob",
            Age = 30,
            Email = "newemail@example.com",
        };

        Exception? exception = Record.Exception(() =>
            Contract.EnsureAssignable(newPerson, oldPerson, "Name|Email|_email")
        );

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_RegexPatternIsCaseSensitive()
    {
        TestPerson oldPerson = new()
        {
            Name = "Alice",
            Age = 30,
            Email = "alice@example.com",
        };
        TestPerson newPerson = new()
        {
            Name = "Alice",
            Age = 30,
            Email = "newemail@example.com",
        };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(newPerson, oldPerson, "email")
        );

        Assert.Contains("Email", exception.Message);
    }

    [Fact]
    public void EnsureAssignable_ComparesNestedObjects()
    {
        TestOrder oldOrder = new()
        {
            OrderId = 1,
            Customer = new TestPerson
            {
                Name = "Alice",
                Age = 30,
                Email = "alice@example.com",
            },
        };
        TestOrder newOrder = new()
        {
            OrderId = 1,
            Customer = new TestPerson
            {
                Name = "Alice",
                Age = 30,
                Email = "alice@example.com",
            },
        };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(newOrder, oldOrder, "OrderId"));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_DetectsNestedObjectChanges()
    {
        TestOrder oldOrder = new()
        {
            OrderId = 1,
            Customer = new TestPerson
            {
                Name = "Alice",
                Age = 30,
                Email = "alice@example.com",
            },
        };
        TestOrder newOrder = new()
        {
            OrderId = 1,
            Customer = new TestPerson
            {
                Name = "Bob",
                Age = 30,
                Email = "alice@example.com",
            },
        };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(newOrder, oldOrder, "OrderId")
        );

        Assert.Contains("Customer", exception.Message);
    }

    [Fact]
    public void EnsureAssignable_AllowsNestedObjectChangesWhenMarkedAssignable()
    {
        TestOrder oldOrder = new()
        {
            OrderId = 1,
            Customer = new TestPerson
            {
                Name = "Alice",
                Age = 30,
                Email = "alice@example.com",
            },
        };
        TestOrder newOrder = new()
        {
            OrderId = 1,
            Customer = new TestPerson
            {
                Name = "Bob",
                Age = 30,
                Email = "alice@example.com",
            },
        };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(newOrder, oldOrder, "Customer"));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_ComparesCollectionFields()
    {
        List<string> oldList = ["Alice", "Bob"];
        List<string> newList = ["Alice", "Bob"];

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(oldList, newList));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_DetectsCollectionModification()
    {
        List<string> oldList = ["Alice", "Bob"];
        List<string> newList = ["Alice", "Bob", "Charlie"];

        Assert.Throws<PostconditionViolationException>(() => Contract.EnsureAssignable(newList, oldList));
    }

    [Fact]
    public void EnsureAssignable_ThrowsArgumentNullException_WhenActualIsNull()
    {
        TestPerson? actual = null;
        TestPerson expected = new();

        Assert.Throws<ArgumentNullException>(() => Contract.EnsureAssignable(actual, expected, "Name"));
    }

    [Fact]
    public void EnsureAssignable_ThrowsArgumentNullException_WhenExpectedIsNull()
    {
        TestPerson actual = new();
        TestPerson? expected = null;

        Assert.Throws<ArgumentNullException>(() => Contract.EnsureAssignable(actual, expected, "Name"));
    }

    [Fact]
    public void EnsureAssignable_ThrowsArgumentNullException_WhenPatternsIsNull()
    {
        TestPerson actual = new();
        TestPerson expected = new();

        Assert.Throws<ArgumentNullException>(() => Contract.EnsureAssignable(actual, expected, null!));
    }

    [Fact]
    public void EnsureAssignable_RespectsRecursionGuard()
    {
        TestPerson person = new()
        {
            Name = "Alice",
            Age = 30,
            Email = "alice@example.com",
        };

        Contract.Ensure(
            "Outer contract",
            () =>
            {
                Contract.EnsureAssignable(person, person, "Name");
                return true;
            }
        );
    }

    private sealed class TestPerson
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }

        public string Email { get; set; } = "";
    }

    private sealed record TestPersonRecord(string Name, int Age, string Email);

    private struct TestPoint
    {
        public int X { get; set; }
        public int Y { get; set; }

        public TestPoint(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    private record struct TestPointRecordStruct(int X, int Y);

    private sealed class TestOrder
    {
        public int OrderId { get; set; }
        public TestPerson Customer { get; set; } = new();
    }
}

public class EnsureCaeTests
{
    [Fact]
    public void Ensure_WhenConditionTrueWithoutDescription_DoesNotThrow()
    {
        Exception? exception = Record.Exception(() => Contract.Ensure(() => true));

        Assert.Null(exception);
    }

    [Fact]
    public void Ensure_WhenConditionFalseWithoutDescription_ThrowsWithCapturedExpression()
    {
        const int balance = -5;

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.Ensure(() => balance >= 0)
        );

        Assert.Contains("balance >= 0", exception.Message);
    }

    [Fact]
    public void Ensure_WhenExplicitDescriptionProvided_OverridesCapturedExpression()
    {
        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.Ensure(() => false, "my explicit description")
        );

        Assert.Contains("my explicit description", exception.Message);
        Assert.DoesNotContain("() => false", exception.Message);
    }

    [Fact]
    public void Ensure_WhenConditionIsNull_ThrowsArgumentNullException()
    {
        Func<bool>? condition = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Contract.Ensure(condition!));

        Assert.Equal("condition", exception.ParamName);
    }

    [Fact]
    public void Ensure_WhenConditionThrows_PropagatesException()
    {
        InvalidOperationException expected = new("inner error");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            Contract.Ensure(() => throw expected)
        );

        Assert.Same(expected, exception);
    }
}

// EnsureNotNull has no CAE overload — see the note near the CAE section in Contract.cs.
