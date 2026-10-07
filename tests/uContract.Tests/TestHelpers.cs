using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;

namespace uContract.Tests;

/// <summary>
///     Test helper class representing a bank account for testing deep copy functionality.
/// </summary>
public class TestAccount
{
    public decimal Balance { get; set; }
    public string Owner { get; set; } = string.Empty;
}

/// <summary>
///     Test helper struct representing a 2D point for testing value type support.
/// </summary>
public struct TestPoint
{
    public int X { get; set; }
    public int Y { get; set; }
}

/// <summary>
///     Test helper class with non-serializable members for testing serialization error handling.
/// </summary>
public class NonSerializableType
{
    public Func<bool> Callback { get; set; } = () => true;
}

/// <summary>
///     Test helper class representing an object owned by someone, hidden behind an interface.
/// </summary>
public interface IHasOwner
{
    string Owner { get; }
}

/// <summary>
///     Test helper class implementing <see cref="IHasOwner" />.
/// </summary>
public sealed class OwnedAccount : IHasOwner
{
    public string Owner { get; set; } = "";
}

/// <summary>
///     Test helper class whose property getter throws <see cref="NotSupportedException" />.
/// </summary>
public sealed class UnsupportedLengthGetter
{
    private readonly string _message = "length is not supported";

    public long Length => throw new NotSupportedException(_message);
}

/// <summary>
///     Test helper class holding a lock object.
/// </summary>
public sealed class HoldsLock
{
    private readonly object _lock = new();

    public object LockHandle => _lock;
}

/// <summary>
///     Runs code with the runtime simulated as Native AOT, through the <see cref="RuntimeFacts" /> seam.
///     The override is process-wide state, so callers belong to the "EnvironmentVariables" collection.
/// </summary>
public static class TestRuntime
{
    public static Exception? WithoutDynamicCode(Action act)
    {
        try
        {
            RuntimeFacts.DynamicCodeSupportedOverride = false;
            return Record.Exception(act);
        }
        finally
        {
            RuntimeFacts.DynamicCodeSupportedOverride = null;
        }
    }

    public static T WithoutDynamicCode<T>(Func<T> act)
    {
        try
        {
            RuntimeFacts.DynamicCodeSupportedOverride = false;
            return act();
        }
        finally
        {
            RuntimeFacts.DynamicCodeSupportedOverride = null;
        }
    }
}

/// <summary>
///     Runs code on a thread with a small stack, to prove that an algorithm does not recurse per node.
/// </summary>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Names the small thread stack, not a stack collection."
)]
public static class SmallStack
{
    public static void OnSmallStack(Action action, int maxStackSize = 256 * 1024, int timeoutMilliseconds = 10_000)
    {
        ExceptionDispatchInfo? failure = null;
        Thread thread = new(
            () =>
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    failure = ExceptionDispatchInfo.Capture(exception);
                }
            },
            maxStackSize
        )
        {
            // A hung action must not keep the test host alive.
            IsBackground = true,
        };

        thread.Start();
        if (!thread.Join(timeoutMilliseconds))
        {
            throw new TimeoutException(
                $"The action did not finish within {timeoutMilliseconds} ms on a {maxStackSize}-byte stack."
            );
        }

        failure?.Throw();
    }
}
