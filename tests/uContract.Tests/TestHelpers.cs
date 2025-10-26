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
