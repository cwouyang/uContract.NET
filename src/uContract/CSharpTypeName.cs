using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace uContract;

/// <summary>
///     The name of a type as C# writes it inside <c>typeof</c>, for the fixes that messages give.
/// </summary>
internal static class CSharpTypeName
{
    // Namespace-qualified, with generic arguments in angle brackets, a nested type after its declaring type and a
    // dot, and an array with its rank in brackets.
    internal static string Of(Type type)
    {
        if (type.IsArray)
        {
            return $"{Of(type.GetElementType()!)}[{new string(',', type.GetArrayRank() - 1)}]";
        }

        return type.IsGenericParameter ? type.Name : Of(type, type.GetGenericArguments());
    }

    // The generic arguments of a nested type include those of its declaring types, first; each type in the chain
    // takes the arguments its own definition adds.
    private static string Of(Type type, Type[] arguments)
    {
        Type? declaringType = type.IsNested ? type.DeclaringType : null;
        string prefix =
            declaringType is not null ? Of(declaringType, arguments) + "."
            : type.Namespace is { } ns ? ns + "."
            : "";

        string name = type.Name;
        int tick = name.IndexOf('`', StringComparison.Ordinal);
        if (tick < 0)
        {
            return prefix + name;
        }

        int outerCount = declaringType?.GetGenericArguments().Length ?? 0;
        int ownCount = int.Parse(name[(tick + 1)..], CultureInfo.InvariantCulture);
        IEnumerable<string> own = arguments.Skip(outerCount).Take(ownCount).Select(Of);
        return $"{prefix}{name[..tick]}<{string.Join(", ", own)}>";
    }
}
