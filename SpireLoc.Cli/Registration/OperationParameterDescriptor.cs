namespace SpireLoc.Cli.Registration;

internal sealed class OperationParameterDescriptor(
    string name,
    string? description,
    Type valueType,
    Type? listElementType,
    int position,
    bool isFlag,
    bool hasDefaultValue,
    object? defaultValue,
    int invocationIndex)
{
    public string Name { get; } = name;
    public string? Description { get; } = description;
    public Type ValueType { get; } = valueType;
    public Type? ListElementType { get; } = listElementType;
    public bool IsList => ListElementType is not null;
    public int Position { get; } = position;
    public bool IsFlag { get; } = isFlag;
    public bool HasDefaultValue { get; } = hasDefaultValue;
    public object? DefaultValue { get; } = defaultValue;
    public int InvocationIndex { get; } = invocationIndex;
}
