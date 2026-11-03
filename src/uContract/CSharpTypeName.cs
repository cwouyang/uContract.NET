using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

namespace uContract;

/// <summary>
///     The name of a type as C# writes it inside <c>typeof</c>, for the fixes that messages give.
/// </summary>
internal static class CSharpTypeName
{
    // Namespace-qualified, with generic arguments in angle brackets, a nested type after its declaring type and a
    // dot, and an array with its rank in brackets. C# writes the ranks of an array of arrays outermost first
    // (int[][,] is an array of int[,]), the reverse of how the runtime nests them.
    internal static string Of(Type type)
    {
        string ranks = "";
        while (type.IsArray)
        {
            ranks += $"[{new string(',', type.GetArrayRank() - 1)}]";
            type = type.GetElementType()!;
        }

        return (type.IsGenericParameter ? type.Name : Of(type, type.GetGenericArguments())) + ranks;
    }

    // False for a type that C# cannot name in typeof: one the compiler generated (marked [CompilerGenerated], or
    // with a '<' in its name, which C# cannot write), nested in such a type, or with such a type among its generic
    // arguments or as its element type.
    internal static bool CanBeWritten(Type type)
    {
        while (type.HasElementType)
        {
            type = type.GetElementType()!;
        }

        for (Type? declaring = type; declaring is not null; declaring = declaring.DeclaringType)
        {
            if (
                declaring.Name.Contains('<', StringComparison.Ordinal)
                || declaring.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
            )
            {
                return false;
            }
        }

        return !type.IsGenericType || type.GetGenericArguments().All(CanBeWritten);
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
