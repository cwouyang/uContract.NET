using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace uContract;

/// <summary>
///     The name of a type as C# writes it inside <c>typeof</c>, for the fixes that messages give.
/// </summary>
internal static class CSharpTypeName
{
    private static readonly Regex FileClassName = new(
        "^<[^<>]+>F[0-9A-F]+__(?<name>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1)
    );

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

    // False for a type that C# cannot name in typeof: one the compiler generated (see IsCompilerGenerated), or one
    // with such a type among its generic arguments or as its element type. A [CompilerGenerated] class with a name
    // C# can write (a source generator's) and a file class (written by its own name inside its file) can be named.
    internal static bool CanBeWritten(Type type)
    {
        while (type.HasElementType)
        {
            type = type.GetElementType()!;
        }

        return !IsCompilerGenerated(type) && (!type.IsGenericType || type.GetGenericArguments().All(CanBeWritten));
    }

    // A type the compiler generated, or nested in one: its metadata name starts with "<>" (an anonymous type, a
    // closure, the class of a collection expression), or it is marked [CompilerGenerated] with a name C# cannot
    // write (the enumerator of a yield method, <Method>d__N; a VB iterator, VB$StateMachine_…).
    internal static bool IsCompilerGenerated(Type type)
    {
        for (Type? declaring = type; declaring is not null; declaring = declaring.DeclaringType)
        {
            if (HasCompilerPrefix(declaring) || IsMarkedWithUnwritableName(declaring))
            {
                return true;
            }
        }

        return false;
    }

    // The metadata name starts with "<>", a prefix only the C# compiler gives.
    internal static bool HasCompilerPrefix(Type type)
    {
        return type.Name.StartsWith("<>", StringComparison.Ordinal);
    }

    // Marked [CompilerGenerated], with a '<' (C#) or a '$' (VB) in the metadata name, which C# cannot write. A
    // [CompilerGenerated] class with a name C# can write is a source generator's, a class of yours.
    internal static bool IsMarkedWithUnwritableName(Type type)
    {
        return type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
            && (type.Name.Contains('<', StringComparison.Ordinal) || type.Name.Contains('$', StringComparison.Ordinal));
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

        string name = SourceName(type.Name);
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

    // Where the typeof of type can be written: a file class only inside the file that declares it. Empty for a type
    // with no file class in it, so its message keeps its text.
    internal static string WhereItCanBeWritten(Type type)
    {
        Type? fileClass = FirstFileClass(type);
        return fileClass is null ? "" : $" inside the file that declares {SourceName(fileClass.Name)}";
    }

    private static Type? FirstFileClass(Type type)
    {
        while (type.HasElementType)
        {
            type = type.GetElementType()!;
        }

        for (Type? declaring = type; declaring is not null; declaring = declaring.DeclaringType)
        {
            if (FileClassName.IsMatch(declaring.Name))
            {
                return declaring;
            }
        }

        return type.IsGenericType
            ? type.GetGenericArguments().Select(FirstFileClass).FirstOrDefault(found => found is not null)
            : null;
    }

    // A file class is emitted as <File>F{checksum}__Name; inside its file C# names it Name.
    private static string SourceName(string metadataName)
    {
        Match fileClass = FileClassName.Match(metadataName);
        return fileClass.Success ? fileClass.Groups["name"].Value : metadataName;
    }
}
