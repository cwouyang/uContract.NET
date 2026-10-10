// SharedTypes treats every type in the namespace System.Collections.Frozen as shared, and fixed. This
// class, declared in the test assembly, is in that namespace and also derives from Component, a type that is
// shared, and its state can change.
#pragma warning disable IDE0130 // The namespace is the point of this type.
namespace System.Collections.Frozen;

#pragma warning restore IDE0130

internal sealed class FrozenNamespaceComponent : System.ComponentModel.Component;
