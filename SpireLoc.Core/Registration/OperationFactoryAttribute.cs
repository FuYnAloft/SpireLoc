namespace SpireLoc.Core.Registration;

/// <summary>Marks a static method or constructor as a named CLI operation factory.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor, Inherited = false)]
public sealed class OperationFactoryAttribute(params string[] path) : Attribute
{
    public IReadOnlyList<string> Path { get; } = path;
}
