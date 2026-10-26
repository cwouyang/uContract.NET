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
        RawName = member.Name;
        PathName = MemberComparison.SourceName(member.Name);

        // A property or field reached through a type is always declared on one.
        DeclaringType = member.DeclaringType!;

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
    ///     Gets the name of the member in violation entries: its raw name, followed by "(declared on T)" when it
    ///     is qualified, so the text before the first space is always a pattern that excuses it (assignable
    ///     patterns are matched against <see cref="RawName" />).
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    ///     Gets the name of the member in the path of a "cannot compare" message: the raw name, a backing field
    ///     shown as the property it backs, with "T." in front when it is qualified.
    /// </summary>
    public string PathName { get; private set; }

    /// <summary>
    ///     Gets the name reflection gives the member.
    /// </summary>
    public string RawName { get; }

    /// <summary>
    ///     Gets a value indicating whether the member is named with its declaring type, because another
    ///     member of the compared type has the same raw name.
    /// </summary>
    public bool IsQualified { get; private set; }

    /// <summary>
    ///     Gets the type that declares the member.
    /// </summary>
    public Type DeclaringType { get; }

    /// <summary>
    ///     Names the member with the given declaring type name in <see cref="Name" /> and
    ///     <see cref="PathName" />.
    /// </summary>
    /// <param name="declaringTypeName">The name of the declaring type</param>
    public void Qualify(string declaringTypeName)
    {
        Name = $"{RawName} (declared on {declaringTypeName})";
        PathName = declaringTypeName + "." + MemberComparison.SourceName(RawName);
        IsQualified = true;
    }

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
