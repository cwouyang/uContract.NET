using System;
using System.Collections.Generic;

namespace uContract;

/// <summary>
///     An (actual, expected) pair of objects, equal to another pair only when it holds the same two instances.
/// </summary>
internal readonly struct ReferencePair(object actual, object expected) : IEquatable<ReferencePair>
{
    private readonly object _actual = actual;
    private readonly object _expected = expected;

    public bool Equals(ReferencePair other)
    {
        return ReferenceEqualityComparer.Instance.Equals(_actual, other._actual)
            && ReferenceEqualityComparer.Instance.Equals(_expected, other._expected);
    }

    public override bool Equals(object? obj)
    {
        return obj is ReferencePair other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            ReferenceEqualityComparer.Instance.GetHashCode(_actual),
            ReferenceEqualityComparer.Instance.GetHashCode(_expected)
        );
    }
}
