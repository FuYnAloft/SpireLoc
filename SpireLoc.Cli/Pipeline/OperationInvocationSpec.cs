namespace SpireLoc.Cli.Pipeline;

internal sealed record OperationInvocationSpec(
    IReadOnlyList<string> FactoryPath,
    IReadOnlyDictionary<string, InvocationScalar> Arguments,
    InvocationSource Source) : PipelineItem(Source);
