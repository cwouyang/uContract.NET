using System;
using System.Reflection;

namespace uContract;

/// <summary>
///     Unified accessor for both PropertyInfo and FieldInfo members.
/// </summary>
internal sealed class MemberAccessor
{
    private readonly FieldInfo? _field;
    private readonly PropertyInfo? _property;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MemberAccessor" /> class.
    /// </summary>
    /// <param name="member">The PropertyInfo or FieldInfo to wrap</param>
    /// <exception cref="ArgumentException">Thrown when member is neither PropertyInfo nor FieldInfo</exception>
    public MemberAccessor(MemberInfo member)
    {
        Name = member.Name;

        switch (member)
        {
            case PropertyInfo prop:
                _property = prop;
                MemberType = prop.PropertyType;
                break;
            case FieldInfo field:
                _field = field;
                MemberType = field.FieldType;
                break;
            default:
                throw new ArgumentException("Member must be PropertyInfo or FieldInfo", nameof(member));
        }
    }

    /// <summary>
    ///     Gets the name of the member.
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///     Gets the type of the member.
    /// </summary>
    public Type MemberType { get; }

    /// <summary>
    ///     Gets the value of the member from the specified object.
    /// </summary>
    /// <param name="obj">The object to get the value from</param>
    /// <returns>The value of the member</returns>
    public object? GetValue(object obj)
    {
        return _property?.GetValue(obj) ?? _field?.GetValue(obj);
    }
}
