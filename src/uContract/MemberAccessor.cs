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
    private readonly MethodInfo? _getter;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MemberAccessor" /> class.
    /// </summary>
    /// <param name="member">The PropertyInfo or FieldInfo to wrap</param>
    /// <exception cref="ArgumentException">Thrown when member is neither PropertyInfo nor FieldInfo</exception>
    public MemberAccessor(MemberInfo member)
        : this(member, (member as PropertyInfo)?.GetMethod) { }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MemberAccessor" /> class for a property read with the
    ///     given getter, which may be declared on a base class rather than visible through the compared type.
    /// </summary>
    /// <param name="member">The PropertyInfo or FieldInfo to wrap</param>
    /// <param name="getter">The get method a property is read with; null when it has none</param>
    /// <exception cref="ArgumentException">Thrown when member is neither PropertyInfo nor FieldInfo</exception>
    public MemberAccessor(MemberInfo member, MethodInfo? getter)
    {
        Name = member.Name;

        switch (member)
        {
            case PropertyInfo prop:
                _property = prop;
                _getter = getter;
                break;
            case FieldInfo field:
                _field = field;
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
    ///     Gets a value indicating whether the member is a field.
    /// </summary>
    public bool IsField => _field is not null;

    /// <summary>
    ///     Gets the value of the member from the specified object.
    /// </summary>
    /// <param name="obj">The object to get the value from</param>
    /// <returns>The value of the member</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the member is a property with no get method (write-only, or its getter was trimmed)
    /// </exception>
    /// <exception cref="TargetInvocationException">Thrown when the get method throws</exception>
    public object? GetValue(object obj)
    {
        if (_property is not null && _getter is null)
        {
            throw new InvalidOperationException(
                $"EnsureAssignable cannot read property {_property.DeclaringType}.{_property.Name}: "
                    + "no get method is visible through the compared type. "
                    + "If trimming removed it, preserve the type's members, for example with "
                    + "[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(X))] where X is that type. "
                    + "Otherwise list the top-level member that leads to it as assignable, "
                    + "or set DBC_POST=off (disables all postcondition checks)."
            );
        }

        return _getter is not null ? _getter.Invoke(obj, null) : _field?.GetValue(obj);
    }
}
