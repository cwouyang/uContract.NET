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
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the member is a property with no get method (write-only, or its getter was trimmed)
    /// </exception>
    public object? GetValue(object obj)
    {
        if (_property is { GetMethod: null })
        {
            throw new InvalidOperationException(
                $"EnsureAssignable cannot read property {_property.DeclaringType}.{_property.Name}: "
                    + "it has no get method. If trimming removed it, preserve the type's members, for example with "
                    + "[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(X))] where X is that type. "
                    + "Otherwise list the top-level member that leads to it as assignable, or set DBC_POST=off."
            );
        }

        return _property?.GetValue(obj) ?? _field?.GetValue(obj);
    }
}
