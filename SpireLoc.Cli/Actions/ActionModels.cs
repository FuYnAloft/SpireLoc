using SpireLoc.Cli.Pipeline;

namespace SpireLoc.Cli.Actions;

internal enum ActionParameterType
{
    String,
    Boolean,
    Integer,
}

internal sealed record ActionSourceLocation(string FilePath, int Line, int Column)
{
    public string Display => $"{FilePath}:{Line}:{Column}";
}

internal sealed record ActionParameterDefinition(
    string Name,
    string? Description,
    ActionParameterType Type,
    int Position,
    bool IsFlag,
    bool HasDefaultValue,
    InvocationScalar? DefaultValue,
    ActionSourceLocation Source);

internal sealed record ActionDocument(
    string FilePath,
    bool IsBuiltin,
    int Version,
    string? Description,
    IReadOnlyList<ActionParameterDefinition> Parameters,
    IReadOnlyList<ActionStep> Steps);

internal sealed record BuiltinActionInfo(
    string Name,
    string? Description,
    IReadOnlyList<ActionParameterDefinition> Parameters);

internal abstract record ActionStep(ActionCondition? Condition, ActionSourceLocation Source);

internal sealed record ActionOperationStep(
    string Head,
    IReadOnlyList<InvocationScalar> Kind,
    IReadOnlyDictionary<string, InvocationScalar> Arguments,
    ActionCondition? Condition,
    ActionSourceLocation Source) : ActionStep(Condition, Source);

internal sealed record ActionUsesStep(
    InvocationScalar ActionPath,
    IReadOnlyDictionary<string, InvocationScalar> With,
    ActionCondition? Condition,
    ActionSourceLocation Source) : ActionStep(Condition, Source);

internal abstract record ActionCondition(ActionSourceLocation Source);

internal sealed record ScalarActionCondition(
    InvocationScalar Value,
    ActionSourceLocation Source) : ActionCondition(Source);

internal sealed record EqualsActionCondition(
    InvocationScalar Left,
    InvocationScalar Right,
    ActionSourceLocation Source) : ActionCondition(Source);

internal sealed record NotActionCondition(
    ActionCondition Value,
    ActionSourceLocation Source) : ActionCondition(Source);

internal sealed record AllActionCondition(
    IReadOnlyList<ActionCondition> Values,
    ActionSourceLocation Source) : ActionCondition(Source);

internal sealed record AnyActionCondition(
    IReadOnlyList<ActionCondition> Values,
    ActionSourceLocation Source) : ActionCondition(Source);
