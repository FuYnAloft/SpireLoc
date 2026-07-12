namespace SpireLoc.Cli.Pipeline;

internal sealed record OperationInvocationSpec(
    IReadOnlyList<string> FactoryPath,
    IReadOnlyDictionary<string, InvocationArgument> Arguments,
    InvocationSource Source) : PipelineItem(Source);
