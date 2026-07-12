namespace SpireLoc.Cli.Registration;

internal sealed class OperationParameterDescriptor(
    string name,
    Type valueType,
    int position,
    bool isFlag,
    bool hasDefaultValue,
    object? defaultValue,
    int invocationIndex)
{
    public string Name { get; } = name;
    public Type ValueType { get; } = valueType;
    public int Position { get; } = position;
    public bool IsFlag { get; } = isFlag;
    public bool HasDefaultValue { get; } = hasDefaultValue;
    public object? DefaultValue { get; } = defaultValue;
    public int InvocationIndex { get; } = invocationIndex;
}
