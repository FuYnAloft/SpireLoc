namespace SpireLoc.Core.Registration;

/// <summary>Configures command-line binding for an operation factory parameter.</summary>
[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
public sealed class OperationParameterAttribute(string? name = null, int position = -1) : Attribute
{
    public string? Name { get; } = name;
    public int Position { get; } = position;
    public bool IsFlag { get; set; }
}
