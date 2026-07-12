namespace SpireLoc.Cli.Pipeline;

internal sealed record ActionInvocationSpec(
    string ActionPath,
    IReadOnlyList<string> ArgumentTokens,
    InvocationSource Source) : PipelineItem(Source);
