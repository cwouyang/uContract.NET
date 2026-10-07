namespace uContract.Tests;

/// <summary>
///     Old under trimming and Native AOT, simulated through the <see cref="RuntimeFacts" /> seam.
///     The overrides are process-wide state, so this class shares the collection of the other
///     tests that mutate process-wide state.
/// </summary>
[Collection("EnvironmentVariables")]
public sealed class OldWithoutDynamicCodeTests
{
    [Fact]
    public void Old_WhenJsonReflectionDisabled_ThrowsInvalidOperationExceptionExplainingHowToProceed()
    {
        TestAccount account = new() { Balance = 100m, Owner = "Alice" };

        Exception? exception = RecordWithoutJsonReflection(() => Contract.Old(() => account));

        InvalidOperationException cannotCopy = Assert.IsType<InvalidOperationException>(exception);
        Assert.Null(cannotCopy.InnerException);
        Assert.Contains("Old<T>()", cannotCopy.Message);
        Assert.Contains("JsonSerializerIsReflectionEnabledByDefault", cannotCopy.Message);
        Assert.Contains("Native AOT", cannotCopy.Message);
        Assert.Contains("DynamicDependency", cannotCopy.Message);
        Assert.Contains("DBC_POST=off", cannotCopy.Message);
        Assert.Contains("DBC_POST=off (disables all postcondition checks)", cannotCopy.Message);
    }

    [Fact]
    public void Old_WhenJsonReflectionDisabled_ResetsRecursionGuardAfterThrowing()
    {
        const int balance = 42;
        TestAccount account = new() { Balance = 100m, Owner = "Alice" };

        Exception? exception = RecordWithoutJsonReflection(() => Contract.Old(() => account));
        int oldBalance = Contract.Old(() => balance);

        Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(balance, oldBalance);
    }

    [Fact]
    public void Old_WhenTypeNotSerializableWithoutDynamicCode_AppendsNativeAotGuidanceToMessage()
    {
        NonSerializableType obj = new();

        Exception? exception = TestRuntime.WithoutDynamicCode((Action)(() => Contract.Old(() => obj)));

        InvalidOperationException cannotSerialize = Assert.IsType<InvalidOperationException>(exception);
        Assert.IsType<NotSupportedException>(cannotSerialize.InnerException);
        Assert.StartsWith(
            $"Type {nameof(NonSerializableType)} cannot be serialized for Old<T>(). "
                + "Ensure the type is JSON-serializable. ",
            cannotSerialize.Message
        );
        Assert.Contains("Native AOT", cannotSerialize.Message);
        Assert.Contains("DynamicDependency", cannotSerialize.Message);
        Assert.Contains("DBC_POST=off", cannotSerialize.Message);
        Assert.Contains("DBC_POST=off (disables all postcondition checks)", cannotSerialize.Message);
    }

    [Fact]
    public void Old_WhenJsonReflectionDisabledAndSupplierReturnsNull_ReturnsDefault()
    {
        TestAccount? account = null;
        TestAccount? oldAccount = new();

        Exception? exception = RecordWithoutJsonReflection(() => oldAccount = Contract.Old(() => account));

        Assert.Null(exception);
        Assert.Null(oldAccount);
    }

    [Fact]
    public void Old_WhenJsonReflectionDisabledAndSupplierThrows_PropagatesExceptionAndResetsRecursionGuard()
    {
        const int balance = 42;
        ArgumentException supplierFailure = new("supplier failed");

        Exception? exception = RecordWithoutJsonReflection(() => Contract.Old<int>(() => throw supplierFailure));
        int oldBalance = Contract.Old(() => balance);

        Assert.Same(supplierFailure, exception);
        Assert.Equal(balance, oldBalance);
    }

    [Fact]
    public void Old_WhenSupplierThrowsNotSupportedExceptionWithoutDynamicCode_PropagatesSameInstance()
    {
        NotSupportedException supplierFailure = new("stream is not seekable");

        Exception? exception = TestRuntime.WithoutDynamicCode(
            (Action)(() => Contract.Old<long>(() => throw supplierFailure))
        );

        Assert.Same(supplierFailure, exception);
    }

    private static Exception? RecordWithoutJsonReflection(Action act)
    {
        try
        {
            RuntimeFacts.JsonReflectionEnabledOverride = false;
            return Record.Exception(act);
        }
        finally
        {
            RuntimeFacts.JsonReflectionEnabledOverride = null;
        }
    }
}

public class OldDeepCopyTests;
