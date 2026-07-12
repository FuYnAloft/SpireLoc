namespace SpireLoc.Cli.Pipeline;

internal sealed record InvocationSource(string Description)
{
    public CliException Error(string message) => new($"{Description}: {message}");
}
