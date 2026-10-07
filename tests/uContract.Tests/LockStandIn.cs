// System.Threading.Lock is in CoreLib from .NET 9 only, and this suite runs on net8.0. SharedTypes matches
// that type by its full name, so this stand-in, declared in the test assembly, exercises the rule.
#pragma warning disable IDE0130 // The namespace is the point of this type.
namespace System.Threading;

#pragma warning restore IDE0130

internal class Lock;
